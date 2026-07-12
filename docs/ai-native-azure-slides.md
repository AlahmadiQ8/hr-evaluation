# AI-Native Azure Engineering

### Building a cloud app the way an agent can build it *with* you

A field guide, told through **Taqyeem** — an opinionated, Azure-native .NET demo.

`greenfield → skills & MCPs → Aspire → git → CI/CD → production infra`

---

## What is "AI-Native" engineering?

Not "an AI wrote some code." It's **designing the repo so an agent can build, run, and ship it — with minimal hand-holding.**

- **Opinionated > flexible** — one obvious way to do each thing
- **Executable, not tribal** — the app runs with *one command*, so agents (and humans) get a fast feedback loop
- **Curated context** — skills + MCP servers give the agent the *right* tools and docs, not the whole internet
- **Guardrails, not vibes** — branch protection, CI, and gated deploys make autonomy safe

> The goal: an agent can go from *clone* → *running* → *deployed* — and you **review**, not babysit.

---

## The testbed: Taqyeem (تقييم, "evaluation")

A bilingual (AR/EN) employee performance-appraisal app for a *fictional* sovereign fund.

- **Single .NET stack** — ASP.NET Core Web API + Blazor Web App (C#)
- **Clean architecture** — Domain → Application (scoring / routing / quota engines, unit-tested) → Infrastructure (EF Core) → API → UI
- **.NET Aspire** orchestration — one command runs *everything*
- **Offline demo auth** — swap personas with no cloud tenant; a config flag turns on real Entra ID

Why it's a good autonomy testbed: **`aspire run` = the whole system**, and the core rules are covered by tests an agent can run.

---

## Greenfield: make the repo agent-ready on day one

Four pillars, set up *before* writing features, so autonomy is the default:

1. **A curated AI toolbox** — skills + MCP servers checked into the repo
2. **One-command local dev** — `aspire run` boots the whole system
3. **Protected trunk** — humans and agents both land changes through PRs + CI
4. **Guarded, repeatable deploys** — the same model runs locally and in Azure

Everything below is *in the repo* — versioned and reviewable, not in someone's head or on one laptop.

---

## The AI toolbox: skills & MCP servers

**MCP servers** give the agent live tools & docs — `.mcp.json`:

```json
{
  "mcpServers": {
    "aspire":      { "command": "aspire", "args": ["agent", "mcp"] },
    "azure-icons": { "command": "uvx", "args": ["--from", "git+https://github.com/AlahmadiQ8/icons#subdirectory=mcp-server", "azure-icons-mcp"] }
  }
}
```

**Skills** teach the agent *how* to use them — `.agents/skills/`:

```
aspire   aspire-init   aspireify   aspire-orchestration
aspire-deployment   aspire-monitoring   dotnet-inspect   playwright-cli
```

Plus **azure-skills** (provision & diagnose Azure) and **ms-learn** (authoritative Microsoft docs) — the agent reads docs *before* it edits.

---

## Why Aspire for orchestration

One command — `aspire run` — brings up SQL + API + Web, wired together:

- **Automatic port management** — no hand-assigned ports or `launchSettings` juggling
- **Service discovery** — `web` finds `api` by name; no hardcoded URLs
- **Correlated telemetry** — logs, traces, and metrics from every service in **one dashboard**
- **Health checks + startup ordering** — `WaitFor` gates the boot sequence

```csharp
var sql = builder.AddAzureSqlServer("sql").RunAsContainer(c => c.WithDataVolume());
var db  = sql.AddDatabase("evaluationdb");

var api = builder.AddProject("api", "src/Taqyeem.Api/Taqyeem.Api.csproj")
    .WithReference(db).WaitFor(db).WithHttpHealthCheck("/health");

var web = builder.AddProject("web", "src/Taqyeem.Web/Taqyeem.Web.csproj")
    .WithReference(api).WaitFor(api).WithExternalHttpEndpoints();
```

For an agent: **one process to start, one dashboard to read, one place to see failures.**

---

## One AppHost, two topologies

The *same* `apphost.cs` describes local dev **and** Azure — only the connection string changes.

```csharp
if (builder.ExecutionContext.IsPublishMode)
{
    builder.AddAzureContainerAppEnvironment("aca");
    var insights = builder.AddAzureApplicationInsights("insights");
    api.WithReference(insights);
    web.WithReference(insights);
}
```

| `aspire run` (local) | `aspire deploy` (Azure) |
|---|---|
| SQL Server container | Azure SQL Database (managed identity) |
| `api` / `web` projects | Azure Container Apps (only `web` public) |
| Aspire dashboard | Application Insights + Log Analytics |

No second set of scripts to maintain — **the deploy story *is* the dev story.**

---

## Securing the trunk

Autonomy needs guardrails: agents and humans land changes through the *same* protected path.

Protect `main` with a **repository ruleset**:

- ✅ **Require a pull request** — no direct pushes to `main`
- ✅ **Require review approval** — a human signs off on agent-authored PRs
- ✅ **Require status checks** — CI (build + unit tests + E2E) must be green
- ✅ **Require linear history**, block force-pushes and deletions

```bash
gh api repos/AlahmadiQ8/hr-evaluation/rulesets --method POST --input ruleset.json
```

> Do this first on a greenfield repo — it's the safety net that makes *"let the agent open a PR"* a good idea.

---

## CI: prove every change actually runs

`.github/workflows/ci.yml` runs on every push & PR to `main`:

- **Restore → build → unit tests** for the scoring / routing / quota engines
- **End-to-end** job: installs the Aspire CLI, **starts the real app**, waits for `/health`, runs Playwright

```yaml
- name: Install Aspire CLI
  run: curl -sSL https://aspire.dev/install.sh | bash
- name: Start the app (Aspire)
  run: aspire start --non-interactive
- name: Run Playwright E2E
  working-directory: e2e
  run: npx playwright test
```

The same `aspire` command a developer (or agent) runs locally is what CI runs — **no drift.**

---

## CD: continuous staging, gated production

Two environments, separated by resource group, deployed **Aspire-native** (`aspire deploy`):

- **Staging** — auto-deploys the exact commit that passed CI on `main`
- **Production** — **release-gated**: publishing a GitHub Release deploys that tag, behind **required reviewers**, then verifies the live `/version` matches

**No secrets** — Azure login uses **OIDC** federated credentials:

```yaml
- name: Azure login (OIDC)
  uses: azure/login@v2
  with:
    client-id: ${{ secrets.AZURE_CLIENT_ID }}
    tenant-id: ${{ secrets.AZURE_TENANT_ID }}
    subscription-id: ${{ secrets.AZURE_SUBSCRIPTION_ID }}

- name: Deploy with Aspire
  run: aspire deploy --apphost ./apphost.cs --environment production --non-interactive
```

---

## AI *in* the pipeline, too

`release-changelog.yml` turns raw commits into human-friendly release notes with **GitHub Models** — no external provider, no secret:

```yaml
permissions:
  contents: write   # edit the release notes
  models: read      # GitHub Models inference

steps:
  - name: Generate changelog with GitHub Models
    uses: actions/ai-inference@v1
    with:
      model: openai/gpt-4o
      prompt: |
        Turn these commit messages into a concise, human-friendly changelog...
```

AI-native isn't only *authoring* code — it's baking model calls into the delivery workflow where they earn their place.

---

## Prepare production infra with azure-skills

Before writing a line of IaC, let the agent **plan against best practices**:

- **azure-skills** (Azure MCP) — query real Azure: SKUs, quotas, regions, cost, and Well-Architected guidance
- **ms-learn** MCP — authoritative Microsoft docs, so recommendations cite sources
- Ask: *"what identity, networking, and SKUs should this app use in production?"*

The topology it lands on for Taqyeem:

- **Managed identity everywhere** — no SQL passwords, no secrets
- **Only the web app is public** — the API has internal ingress only
- **Entra-only SQL auth**, `minReplicas: 1` (no cold start), OpenTelemetry → App Insights

The agent proposes the topology; you review it against the framework — *then* scaffold.

---

## Reviewable IaC: scaffold Bicep from the model

`aspire publish` emits the **exact Bicep** the deploy will apply — reviewable without provisioning:

```bash
aspire publish --apphost ./apphost.cs -o infra-preview/staging \
  --environment staging --non-interactive
```

```
infra-preview/staging/
  main.bicep    aca/   aca-acr/   api/   web/   sql/   sql-store/
  api-identity/   api-roles-sql/   insights/
  vnet/   privatelink-*/   sqlpe-sql-pe/   ...
```

You get **diff-able infrastructure** (identities, private endpoints, Container Apps, SQL) to review in a PR — the AppHost stays the source of truth, but the IaC is fully inspectable.

---

## Takeaways: the opinionated checklist

- **Check the AI toolbox into the repo** — MCP servers (`.mcp.json`) + skills (`.agents/skills/`) travel *with* the code
- **One command to run everything** — Aspire gives ports, discovery, and correlated telemetry for free
- **One model, two topologies** — local containers and Azure from the *same* AppHost
- **Protect the trunk** — PRs + review + required CI turn "let the agent ship" into a safe default
- **Aspire-native CI/CD** — the deploy command *is* the dev command; production is release-gated with OIDC
- **Plan infra with azure-skills, scaffold reviewable Bicep** — review IaC in a PR before it's real

> AI-native = **opinionated defaults + fast feedback + guardrails.** Then let the agent drive.

---

## Resources

**This repo**

- `README.md` — run it locally with one command
- `docs/architecture.md` — local vs Azure topology (with Azure icons)
- `docs/azure-deployment.md` — environments, gating, managed identity, OIDC setup
- `apphost.cs` — the file-based Aspire AppHost
- `.mcp.json` · `.agents/skills/` — the AI toolbox

**Tools**

- .NET Aspire — <https://aspire.dev>
- azure-skills & Azure MCP · GitHub MCP · ms-learn MCP
- GitHub Models — `actions/ai-inference`

*All Taqyeem data is fictional — for demonstration only.*
