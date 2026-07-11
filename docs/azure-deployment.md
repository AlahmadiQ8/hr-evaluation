# Azure deployment

Taqyeem deploys to Azure with two environments — **staging** and **production** — separated by
**resource group**. Deployment is **Aspire-native**: the AppHost (`apphost.cs`) is the single
source of truth, and `aspire deploy` provisions and updates everything. GitHub Actions drives
continuous staging deploys and **release-gated** production deploys with post-deploy verification,
and a GitHub Models workflow writes a human-friendly changelog on every release.

> Why Aspire-native (not `azd`)? This repo uses a **file-based** AppHost (`apphost.cs`). `azd`'s
> first-class Aspire support expects an AppHost *project*, so `aspire deploy`/`aspire publish` is
> the reliable path here. You still get reviewable Bicep — see [Reviewable IaC](#reviewable-iac).

## What gets deployed

| Local (`aspire run`) | Azure (`aspire deploy`) |
|---|---|
| `AddAzureSqlServer("sql").RunAsContainer()` | **Azure SQL Database** (`evaluationdb`), Entra-only auth, **private endpoint** |
| `api` project | **Container App** (internal ingress only) |
| `web` project | **Container App** (public HTTPS ingress) |
| — | Azure Container Registry, Container Apps environment, Log Analytics, **Application Insights** |
| — | User-assigned **managed identities** (ACR pull + SQL access), **virtual network** + private DNS |

Because EF Core uses the SQL Server provider both locally and in Azure, the only change between
`aspire run` and `aspire deploy` is the connection string, which Aspire injects automatically.

### Production-grade configuration

The AppHost applies these on `aspire deploy` (see `apphost.cs`, guarded by `IsPublishMode`):

- **Only the web app is public.** The API has internal ingress; the Blazor server app calls it
  server-side via service discovery.
- **Managed identity everywhere.** ACR pull and Azure SQL access use user-assigned identities —
  no secrets or SQL passwords. Azure SQL has `azureADOnlyAuthentication` enabled.
- **No cold start.** Both container apps run with `minReplicas: 1`.
- **Telemetry.** `Taqyeem.ServiceDefaults` exports OpenTelemetry to **Application Insights** when
  `APPLICATIONINSIGHTS_CONNECTION_STRING` is present (injected on deploy).
- **Health + version.** `/health`, `/alive`, and `/version` are mapped in every environment;
  `/version` reports `APP_VERSION` and is used to verify releases.
- **Seeded demo.** Auth stays in `Demo` mode and the fictional data is seeded (`DemoData:Seed=true`)
  so the deployed demo is usable. Set `DemoData:Seed=false` for an empty database.

### Private networking

The target subscription's governance denies Azure SQL public endpoints (policy
`DenyPublicEndpointEnabled`). The AppHost therefore provisions a **virtual network** with three
subnets and reaches SQL privately — no public firewall rules:

| Subnet | Purpose |
|---|---|
| `containerapps` (`/23`) | Delegated to `Microsoft.App/environments` — the Container Apps environment |
| `sqlpe` (`/24`) | Private endpoint for Azure SQL (`privatelink.database.windows.net`) |
| `sqlscript` (`/24`) | VNet-integrated deployment script that grants the API's managed identity a SQL role |

Aspire also creates the private DNS zones, a storage account for the deployment script, and its
private endpoint. If you deploy into a subscription **without** this policy, the private-networking
block is still valid but could be simplified to public SQL with firewall rules.

## Environments, triggers, and gating

| Environment | Resource group | Trigger | Gate |
|---|---|---|---|
| **staging** | `rg-taqyeem-staging` | CI succeeds on `main` (`deploy-staging.yml`) | none (continuous) |
| **production** | `rg-taqyeem-production` | a **GitHub Release** is published (`deploy-production.yml`) | `production` environment **required reviewers** |

- **Staging** deploys the exact commit that passed CI and stamps `APP_VERSION=staging-<sha>`.
- **Production** checks out the **release tag**, deploys it, and then asserts the live `/version`
  equals the tag — so a release is only "green" if the intended build is actually serving.
  A `workflow_dispatch` fallback accepts a `ref` input.

Both workflows run `.github/scripts/verify-deployment.sh`, which resolves the web app's ingress
FQDN, waits for `/health`, and (for production) checks `/version`.

## One-time setup

### 1. Azure identity (OIDC, no secrets)

Create a Microsoft Entra app registration with **federated credentials** for each environment and
grant it access to the two resource groups (or the subscription):

```bash
SUBSCRIPTION_ID="<subscription-guid>"
APP_ID=$(az ad app create --display-name "taqyeem-github-oidc" --query appId -o tsv)
az ad sp create --id "$APP_ID"

# One federated credential per environment (repo: AlahmadiQ8/hr-evaluation)
for ENV in staging production; do
  az ad app federated-credential create --id "$APP_ID" --parameters "{
    \"name\": \"gh-$ENV\",
    \"issuer\": \"https://token.actions.githubusercontent.com\",
    \"subject\": \"repo:AlahmadiQ8/hr-evaluation:environment:$ENV\",
    \"audiences\": [\"api://AzureADTokenExchange\"]
  }"
done

# Contributor on each resource group (create the RGs first, or scope to the subscription)
az role assignment create --assignee "$APP_ID" --role Contributor \
  --scope "/subscriptions/$SUBSCRIPTION_ID/resourceGroups/rg-taqyeem-staging"
az role assignment create --assignee "$APP_ID" --role Contributor \
  --scope "/subscriptions/$SUBSCRIPTION_ID/resourceGroups/rg-taqyeem-production"
```

`aspire deploy` also assigns SQL roles via a deployment script, so the identity needs rights to
create role assignments in the target RG (Contributor + User Access Administrator, or Owner).

### 2. GitHub Environments

Create two environments under **Settings → Environments**:

| Environment | Variables | Secrets | Protection |
|---|---|---|---|
| `staging` | `AZURE_LOCATION`, `AZURE_RESOURCE_GROUP=rg-taqyeem-staging` | `AZURE_CLIENT_ID`, `AZURE_TENANT_ID`, `AZURE_SUBSCRIPTION_ID` | — |
| `production` | `AZURE_LOCATION`, `AZURE_RESOURCE_GROUP=rg-taqyeem-production` | `AZURE_CLIENT_ID`, `AZURE_TENANT_ID`, `AZURE_SUBSCRIPTION_ID` | **required reviewers** (+ optional tag rule) |

`AZURE_CLIENT_ID` is the app registration's `appId`. Use a region where Azure Container Apps and
Azure SQL both have capacity (e.g. `westeurope`).

## Deploy

CI/CD handles deploys, but you can deploy locally with the Azure CLI logged in (`az login`):

```bash
export Azure__SubscriptionId="<subscription-guid>"
export Azure__Location="westeurope"
export Azure__ResourceGroup="rg-taqyeem-staging"
export APP_VERSION="local-$(git rev-parse --short HEAD)"

aspire deploy --apphost ./apphost.cs --environment staging --list-steps   # preview
aspire deploy --apphost ./apphost.cs --environment staging --non-interactive
```

Swap `staging` → `production` and the resource group for production.

### Verify

```bash
az resource list -g "$Azure__ResourceGroup" -o table
FQDN=$(az containerapp list -g "$Azure__ResourceGroup" \
  --query "[?contains(name,'web')].properties.configuration.ingress.fqdn | [0]" -o tsv)
curl -fsS "https://$FQDN/health" && curl -fsS "https://$FQDN/version"
```

## Reviewable IaC

`aspire publish` writes the Bicep the deployment will apply — useful for review without
provisioning (this is the "prepare" step):

```bash
Azure__SubscriptionId="<sub>" Azure__Location="westeurope" Azure__ResourceGroup="rg-taqyeem-staging" \
  aspire publish --apphost ./apphost.cs -o infra-preview/staging --environment staging --non-interactive
```

Output (gitignored) includes `sql/sql.bicep` (private, Entra-only), `vnet/vnet.bicep`,
the private endpoints, `api/api.bicep` (internal), and `web/web.bicep` (public).

## Changelog on release

`release-changelog.yml` runs on every published release, uses **GitHub Models**
(`actions/ai-inference`, permission `models: read`) to summarize commits since the previous tag
into a human-friendly changelog, and updates the release notes. No external model provider or
secret is required.

## Tear down

```bash
aspire destroy --apphost ./apphost.cs --environment staging --non-interactive
# or, to remove everything in the resource group:
az group delete -n rg-taqyeem-staging --yes
```

## Notes

- **Region capacity** — Container Apps can be capacity-constrained in some regions (e.g.
  `swedencentral` returned `AKSCapacityHeavyUsage`). Pick a region with capacity such as
  `westeurope`.
- **Governance policies** — beyond the SQL public-endpoint policy, subscriptions may restrict
  VNets, private endpoints, or deployment scripts. Inspect failures with
  `az deployment group list -g <rg>` and `az policy state list`.
- **Secrets** — never commit connection strings or client secrets. Managed identity covers SQL and
  ACR; provide everything else through GitHub Environment secrets.
- **Real Entra ID** — set `Auth:Mode=EntraId` and populate an `AzureAd` section to replace the
  offline demo personas with real sign-in.
