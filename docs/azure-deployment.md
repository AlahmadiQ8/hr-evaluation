# Azure deployment (deferred)

Deployment is **prepared but not executed**. This document describes how to deploy Taqyeem to
Azure with `aspire deploy`. The application code needs **no changes** — the AppHost already
models Azure SQL, so publishing swaps the local SQL Server container for a provisioned
**Azure SQL Database** and hosts the API + Blazor UI on **Azure Container Apps (ACA)**.

## How the model maps to Azure

| Local (`aspire run`) | Azure (`aspire deploy`) |
|---|---|
| `AddAzureSqlServer("sql").RunAsContainer()` → SQL Server container | Provisioned **Azure SQL Database** (`evaluationdb`) |
| `api` / `web` project resources | Container images on **Azure Container Apps** |
| Connection string injected by Aspire | Azure SQL connection string injected by Aspire (Entra or admin auth) |

Because EF Core uses the SQL Server provider in both cases, the swap is a connection-string
change that Aspire performs automatically — no migration or code change.

## One-time prep

1. **Add an ACA compute environment** to `apphost.cs` so `aspire deploy` has a target:

   ```csharp
   #:package Aspire.Hosting.Azure.AppContainers@13.4.6

   builder.AddAzureContainerAppEnvironment("aca");
   ```

   With a single compute environment, the `api` and `web` resources bind to it automatically.

2. **Azure login & settings** (locally or in CI):

   ```bash
   az login
   export Azure__SubscriptionId="<subscription-guid>"
   export Azure__Location="<region, e.g. uaenorth>"
   export Azure__ResourceGroup="rg-taqyeem"
   ```

3. **Seeding in Azure** — the demo seeds on startup while `DemoData:Seed = true`. For a real
   deployment, set `DemoData:Seed = false` (app setting) so production data is never seeded.

## Deploy

```bash
aspire ls
aspire deploy --list-steps      # preview the plan
aspire deploy --environment Production --non-interactive
```

Aspire provisions the Azure SQL Database and the ACA environment, builds and pushes the API
and Web container images, injects the connection string and service-discovery values, and
exposes the Web app's external HTTPS endpoint.

Validate afterwards:

```bash
az resource list -g "$Azure__ResourceGroup" -o table
az containerapp list -g "$Azure__ResourceGroup" -o table
```

## CI/CD

`.github/workflows/deploy.yml` runs the same `aspire deploy` on a **manual** trigger, gated by
a `production` GitHub Environment. Configure it with:

| Kind | Names |
|---|---|
| Secrets | `AZURE_CLIENT_ID`, `AZURE_TENANT_ID`, `AZURE_SUBSCRIPTION_ID` |
| Variables | `AZURE_LOCATION`, `AZURE_RESOURCE_GROUP` |

Use an Entra app registration with a **federated credential** for the repo so `azure/login`
can use OIDC (no client secret).

## Authentication in Azure

The demo runs with offline personas (`Auth:Mode = Demo`). To use real identities in Azure,
set `Auth:Mode = EntraId` and register the apps in Microsoft Entra ID:

1. Register the **API** (expose an app-scope, e.g. `access_as_user`).
2. Register the **Web** app (redirect URIs for the ACA hostname) with permission to the API.
3. Populate the `AzureAd` configuration section (tenant, client id) for both, using
   `Microsoft.Identity.Web`.

## Tear down

```bash
aspire destroy --environment Production --yes
```

## Notes & considerations

- **Azure SQL auth policy** — some tenants require Microsoft Entra-only authentication on SQL.
  Prefer Entra (managed identity) auth for the Azure SQL Database; disable SQL admin auth if
  policy requires it.
- **Region** — pick a region where Azure Container Apps and Azure SQL are both available.
- **Secrets** — never commit connection strings or client secrets; provide them through Azure
  app settings, Key Vault, or GitHub Environment secrets.
