# Architecture

Taqyeem is a single-stack .NET application (ASP.NET Core Web API + Blazor Web App) orchestrated
by a file-based [.NET Aspire](https://aspire.dev) AppHost ([`apphost.cs`](../apphost.cs)). The
**same** AppHost model describes two topologies:

- **Local development** — `aspire run` brings up a SQL Server container, the API, and the Blazor
  UI, wired together with service discovery, health checks, and OpenTelemetry.
- **Azure deployment** — `aspire deploy` provisions the equivalent Azure resources: Azure SQL,
  two Azure Container Apps, a container registry, Application Insights + Log Analytics, and
  user-assigned managed identities. See [`azure-deployment.md`](azure-deployment.md) for the full
  deployment story.

The diagrams below are Mermaid `flowchart`s whose nodes embed the official Azure service icons.

> The Mermaid source is validated with the [`mmdc`](https://github.com/mermaid-js/mermaid-cli)
> CLI: `mmdc -i docs/architecture.md -o /tmp/arch.svg` (renders one SVG per diagram). Icons are
> referenced by URL from the Azure icon set, so a network connection is needed to render them.

## Local development (`aspire run`)

```mermaid
flowchart LR
  dev@{ img: "https://raw.githubusercontent.com/AlahmadiQ8/icons/main/icons/users_48_color.svg", label: "Developer browser", pos: "b", w: 54, h: 54, constraint: "on" }

  subgraph AH["Aspire AppHost — aspire run · apphost.cs"]
    web@{ img: "https://raw.githubusercontent.com/AlahmadiQ8/icons/main/icons/app_services_48_color.svg", label: "web — Blazor Web App", pos: "b", w: 54, h: 54, constraint: "on" }
    api@{ img: "https://raw.githubusercontent.com/AlahmadiQ8/icons/main/icons/app_services_48_color.svg", label: "api — ASP.NET Core", pos: "b", w: 54, h: 54, constraint: "on" }
    sql@{ img: "https://raw.githubusercontent.com/AlahmadiQ8/icons/main/icons/azure_sql_48_color.svg", label: "SQL Server container — evaluationdb", pos: "b", w: 54, h: 54, constraint: "on" }
    dash["Aspire dashboard<br/>logs · traces · metrics"]
  end

  dev -->|HTTPS · InteractiveServer| web
  web -->|service discovery| api
  api -->|EF Core| sql
  web -. OpenTelemetry .-> dash
  api -. OpenTelemetry .-> dash

  style AH fill:#fff8e6,stroke:#bf8700
```

- **`web`** (`Taqyeem.Web`) is the only externally reachable endpoint (`WithExternalHttpEndpoints`).
  The Blazor **InteractiveServer** UI runs server-side and talks to the API over service discovery.
- **`api`** (`Taqyeem.Api`) owns EF Core and the scoring / routing / quota engines; it exposes
  `/health` (`WithHttpHealthCheck`) and OpenAPI.
- **`sql`** is `AddAzureSqlServer("sql").RunAsContainer(...)` — a local SQL Server container with a
  data volume, hosting the `evaluationdb` database. Startup is ordered by `WaitFor` (api waits for
  the database, web waits for the api).
- **OpenTelemetry** logs, traces, and metrics from both services flow to the **Aspire dashboard**
  via `Taqyeem.ServiceDefaults`.

## Azure deployment (`aspire deploy`)

```mermaid
flowchart LR
  user@{ img: "https://raw.githubusercontent.com/AlahmadiQ8/icons/main/icons/users_48_color.svg", label: "User / browser", pos: "b", w: 54, h: 54, constraint: "on" }

  subgraph AZ["Azure — resource group rg-taqyeem-stg / -production"]
    subgraph ACA["Azure Container Apps environment"]
      web@{ img: "https://raw.githubusercontent.com/AlahmadiQ8/icons/main/icons/worker_container_app_48_color.svg", label: "web — public HTTPS ingress", pos: "b", w: 54, h: 54, constraint: "on" }
      api@{ img: "https://raw.githubusercontent.com/AlahmadiQ8/icons/main/icons/worker_container_app_48_color.svg", label: "api — internal ingress only", pos: "b", w: 54, h: 54, constraint: "on" }
    end
    sql@{ img: "https://raw.githubusercontent.com/AlahmadiQ8/icons/main/icons/azure_sql_48_color.svg", label: "Azure SQL — evaluationdb", pos: "b", w: 54, h: 54, constraint: "on" }
    acr@{ img: "https://raw.githubusercontent.com/AlahmadiQ8/icons/main/icons/container_registries_48_color.svg", label: "Container Registry", pos: "b", w: 54, h: 54, constraint: "on" }
    mi@{ img: "https://raw.githubusercontent.com/AlahmadiQ8/icons/main/icons/managed_identities_48_color.svg", label: "User-assigned managed identities", pos: "b", w: 54, h: 54, constraint: "on" }
    appi@{ img: "https://raw.githubusercontent.com/AlahmadiQ8/icons/main/icons/application_insights_48_color.svg", label: "Application Insights", pos: "b", w: 54, h: 54, constraint: "on" }
    law@{ img: "https://raw.githubusercontent.com/AlahmadiQ8/icons/main/icons/log_analytics_workspaces_48_color.svg", label: "Log Analytics workspace", pos: "b", w: 54, h: 54, constraint: "on" }
  end

  user -->|HTTPS| web
  web -->|service discovery · internal| api
  api -->|Entra-only auth| sql

  web -. assumes .-> mi
  api -. assumes .-> mi
  mi -. AcrPull .-> acr
  mi -. db role .-> sql

  web -->|OpenTelemetry| appi
  api -->|OpenTelemetry| appi
  appi --> law

  style AZ fill:#f0f6ff,stroke:#0969da
  style ACA fill:#e6f4ea,stroke:#2da44e
```

- **Only `web` is public.** It runs with a public HTTPS ingress; **`api` has internal ingress**
  and is reached server-side over service discovery — it is never exposed to the internet.
- **Azure SQL Database** (`evaluationdb`) has **Entra-only authentication**. The API connects as
  its **user-assigned managed identity** (`Authentication=Active Directory Default`); a deployment
  script grants that identity a database role. No SQL passwords or secrets are used.
- **Managed identities** are also granted **AcrPull** so the Container Apps pull their images from
  the **Azure Container Registry** without credentials.
- **Telemetry**: `Taqyeem.ServiceDefaults` exports OpenTelemetry to **Application Insights** (which
  is workspace-based, backed by a **Log Analytics workspace**) when
  `APPLICATIONINSIGHTS_CONNECTION_STRING` is injected on deploy.
- Both apps run with `minReplicas: 1` (no cold start) and map `/health`, `/alive`, and `/version`.

Everything above is provisioned and updated by `aspire deploy` from the single AppHost model; the
only difference between local and Azure is the connection string, which Aspire injects
automatically. The Azure-only resources (Container Apps environment, Application Insights) are
wired in `apphost.cs` under `if (builder.ExecutionContext.IsPublishMode)`.

## Icons

Service icons are from the Microsoft **Azure architecture icon** set, referenced by URL at render
time. They are illustrative; all application data in this demo is fictional.
