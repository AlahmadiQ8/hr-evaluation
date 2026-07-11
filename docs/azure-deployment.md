# Azure deployment

Taqyeem deploys to Azure with two environments — **staging** and **production** — separated by
**resource group**. Deployment is **Aspire-native**: the AppHost (`apphost.cs`) is the single
source of truth, and `aspire deploy` provisions and updates everything. GitHub Actions drives
continuous staging deploys and **release-gated** production deploys with post-deploy verification,
and a GitHub Models workflow writes a human-friendly changelog on every release.

> Why Aspire-native (not `azd`)? This repo uses a **file-based** AppHost (`apphost.cs`). `azd`'s
> first-class Aspire support expects an AppHost *project*, so `aspire deploy`/`aspire publish` is
> the reliable path here. `aspire publish` still emits reviewable Bicep — see [Reviewable IaC](#reviewable-iac).

## What gets deployed

| Local (`aspire run`) | Azure (`aspire deploy`) |
|---|---|
| `AddAzureSqlServer("sql").RunAsContainer()` | **Azure SQL Database** (`evaluationdb`), Entra-only auth via managed identity |
| `api` project | **Container App** — internal ingress only |
| `web` project | **Container App** — public HTTPS ingress |
| — | Azure Container Registry, Container Apps environment, Log Analytics, **Application Insights** |
| — | User-assigned **managed identities** (ACR pull + SQL access) |

Because EF Core uses the SQL Server provider both locally and in Azure, the only change between
`aspire run` and `aspire deploy` is the connection string, which Aspire injects automatically.

### Production-grade configuration

The AppHost applies these on `aspire deploy` (see `apphost.cs`, guarded by `IsPublishMode`):

- **Only the web app is public.** The API has internal ingress; the Blazor server app calls it
  server-side via service discovery.
- **Managed identity everywhere.** ACR pull and Azure SQL access use user-assigned identities —
  no secrets or SQL passwords. Azure SQL has `azureADOnlyAuthentication` enabled, and a
  deployment script grants the API's identity a database role.
- **No cold start.** Both container apps run with `minReplicas: 1`.
- **Telemetry.** `Taqyeem.ServiceDefaults` exports OpenTelemetry to **Application Insights** when
  `APPLICATIONINSIGHTS_CONNECTION_STRING` is present (injected on deploy).
- **Health + version.** `/health`, `/alive`, and `/version` are mapped in every environment;
  `/version` reports `APP_VERSION` and is used to verify releases.
- **Seeded demo.** Auth stays in `Demo` mode and the fictional data is seeded (`DemoData:Seed=true`)
  so the deployed demo is usable. Set `DemoData:Seed=false` for an empty database.

### Managed-identity SQL authentication

Aspire injects a connection string with `Authentication=Active Directory Default` so the API
authenticates to Azure SQL as its managed identity. `Microsoft.Data.SqlClient` 6+ split the Entra
auth providers into a separate package whose reflection-based assembly loading is trimmed out of
container publishes. `Taqyeem.Infrastructure` therefore registers a small custom
[`AzureIdentitySqlAuthenticationProvider`](../src/Taqyeem.Infrastructure/Persistence/AzureIdentitySqlAuthenticationProvider.cs)
that acquires tokens via `Azure.Identity`'s `DefaultAzureCredential` (referenced statically, so it
is always included). It is a no-op locally, where the SQL Server container uses a normal connection.

## Environments, triggers, and gating

| Environment | Resource group | Region | Trigger | Gate |
|---|---|---|---|---|
| **staging** | `rg-taqyeem-stg` | `centralus` | CI succeeds on `main` (`deploy-staging.yml`) | none (continuous) |
| **production** | `rg-taqyeem-production` | `centralus` | a **GitHub Release** is published (`deploy-production.yml`) | `production` environment **required reviewers** |

- **Staging** deploys the exact commit that passed CI and stamps `APP_VERSION=staging-<sha>`.
- **Production** checks out the **release tag**, deploys it, and then asserts the live `/version`
  equals the tag — so a release is only "green" if the intended build is actually serving.
  A `workflow_dispatch` fallback accepts a `ref` input.

Both workflows run `.github/scripts/verify-deployment.sh`, which resolves the web app's ingress
FQDN, waits for `/health`, and (for production) checks `/version`.

Resource-group and region names are **GitHub Environment variables** (`AZURE_RESOURCE_GROUP`,
`AZURE_LOCATION`), so you can change them without editing the workflows.

## One-time setup

### 1. Azure identity (OIDC, no secrets)

Create a Microsoft Entra app registration with **federated credentials** for each environment and
grant it access to the subscription (or the two resource groups):

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

# Owner (Aspire assigns SQL/ACR roles, which needs role-assignment rights)
SP_OID=$(az ad sp show --id "$APP_ID" --query id -o tsv)
az role assignment create --assignee-object-id "$SP_OID" --assignee-principal-type ServicePrincipal \
  --role Owner --scope "/subscriptions/$SUBSCRIPTION_ID"
```

### 2. GitHub Environments

Create two environments under **Settings → Environments**:

| Environment | Variables | Secrets | Protection |
|---|---|---|---|
| `staging` | `AZURE_LOCATION`, `AZURE_RESOURCE_GROUP=rg-taqyeem-stg` | `AZURE_CLIENT_ID`, `AZURE_TENANT_ID`, `AZURE_SUBSCRIPTION_ID` | — |
| `production` | `AZURE_LOCATION`, `AZURE_RESOURCE_GROUP=rg-taqyeem-production` | `AZURE_CLIENT_ID`, `AZURE_TENANT_ID`, `AZURE_SUBSCRIPTION_ID` | **required reviewers** (+ optional tag rule) |

`AZURE_CLIENT_ID` is the app registration's `appId`. Use a region where Azure Container Apps and
Azure SQL both have capacity (this deployment uses `centralus`).

## Deploy

CI/CD handles deploys, but you can deploy locally with the Azure CLI logged in (`az login`):

```bash
export Azure__SubscriptionId="<subscription-guid>"
export Azure__Location="centralus"
export Azure__ResourceGroup="rg-taqyeem-stg"
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
provisioning:

```bash
Azure__SubscriptionId="<sub>" Azure__Location="centralus" Azure__ResourceGroup="rg-taqyeem-stg" \
  aspire publish --apphost ./apphost.cs -o infra-preview/staging --environment staging --non-interactive
```

Output (gitignored) includes `sql/sql.bicep` (Entra-only), `api/api.bicep` (internal),
`web/web.bicep` (public), the Container Apps environment, ACR, and Application Insights.

## Changelog on release

`release-changelog.yml` runs on every published release, uses **GitHub Models**
(`actions/ai-inference`, permission `models: read`) to summarize commits since the previous tag
into a human-friendly changelog, and updates the release notes. No external model provider or
secret is required.

## Tear down

```bash
aspire destroy --apphost ./apphost.cs --environment staging --non-interactive
# or, to remove everything in the resource group:
az group delete -n rg-taqyeem-stg --yes
```

## Notes

- **Region / subscription limits** — some subscriptions restrict Azure SQL or Container Apps by
  region (e.g. `ProvisioningDisabled`, `RegionDoesNotAllowProvisioning`, or ACA
  `AKSCapacityHeavyUsage`). Pick a region where both are available for your subscription;
  `centralus` works here. If a subscription **denies public SQL endpoints**, the AppHost can be
  extended with a virtual network + private endpoint for SQL and a delegated subnet for the
  Container Apps environment (see the git history for a worked example) — but that also requires
  the `Microsoft.Network/AllowBringYourOwnPublicIpAddress` feature registered.
- **ACR name reuse** — the ACR name is derived from the resource group id; rapidly deleting and
  recreating the same resource group can leave the reused registry endpoint temporarily broken.
  Use a fresh resource-group name if `login-to-acr` returns 404 on `/oauth2/exchange`.
- **Secrets** — never commit connection strings or client secrets. Managed identity covers SQL and
  ACR; provide everything else through GitHub Environment secrets.
- **Real Entra ID** — set `Auth:Mode=EntraId` and populate an `AzureAd` section to replace the
  offline demo personas with real sign-in.
