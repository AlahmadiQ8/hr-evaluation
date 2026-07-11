# Taqyeem — Employee Performance Evaluation (KIA demo)

**Taqyeem** (تقييم, "evaluation") is a runnable demo of a digital employee
performance-appraisal web app for a **fictional** Kuwait Investment Authority (KIA)–like
sovereign fund. It replaces a manual, Excel-based annual appraisal with a bilingual
(Arabic/English) workflow: managers score employees, the evaluation routes up the
management chain for approval, and HR runs a forced-distribution calibration.

> ⚠️ **All data is fictional.** Employees, names, org units, and scores are generated for
> demonstration only and do not represent real people or the real KIA.

It is an opinionated, Azure-native sample: local development is just **`aspire run`** for
every service, and it is built to be both human- and agent-friendly.

## Highlights

- **Single .NET stack** — ASP.NET Core Web API + Blazor Web App (InteractiveServer), C#.
- **.NET Aspire** orchestration: one command brings up SQL Server, the API, and the UI with
  a dashboard, structured logs, traces, and health checks.
- **Bilingual AR/EN** with RTL/LTR switching and accessible controls.
- **Clean architecture**: Domain → Application (scoring/routing/quota engines, unit-tested)
  → Infrastructure (EF Core) → API → UI.
- **Offline demo auth**: switch between seeded personas with no Entra tenant; a config flag
  turns on real Microsoft Entra ID.

## Architecture

```
apphost.cs                     file-based Aspire AppHost (SQL + API + Web)
src/
  Taqyeem.Domain               entities, enums, value objects (LocalizedText, RatingBand…)
  Taqyeem.Application          ScoringEngine · RoutingEngine · QuotaEngine (+ services, DTOs)  ← unit-tested core
  Taqyeem.Infrastructure       EF Core DbContext, migrations, demo seeder
  Taqyeem.Api                  REST API, Demo/Entra auth, OpenAPI
  Taqyeem.Web                  Blazor Web App (bilingual UI)
  Taqyeem.ServiceDefaults      OpenTelemetry, health checks, service discovery
tests/Taqyeem.Application.Tests MSTest unit tests for the three engines
e2e/                            Playwright end-to-end tests
```

The three core rule sets live in the application layer and are covered by unit tests:

- **Scoring** — weighted competencies + objectives → 0–100 → rating band (Outstanding /
  Exceeds / Meets / Partially Meets / Unsatisfactory), thresholds configurable.
- **Routing** — builds the approval chain up the management hierarchy, with a **mid-year
  manager change** (both managers' scores weighted by tenure) and a **Managing-Director-direct**
  shortcut that skips department/sector approvals.
- **Quota** — forced distribution: caps the share of each unit that can receive the top bands
  and flags violations during HR calibration.

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- [.NET Aspire CLI](https://aspire.dev) — `curl -sSL https://aspire.dev/install.sh | bash`
- **Docker** running (for the local SQL Server container). On Apple Silicon, enable
  Docker Desktop's "Use Rosetta for x86/amd64 emulation".
- Node.js 20+ (only for the E2E tests)

## Run it locally

```bash
aspire run
```

That starts SQL Server (as a container), applies EF Core migrations, seeds the fictional
data, and launches the API and Blazor UI. Open the **Aspire dashboard** (printed on start)
to find the endpoints, logs, and traces. The Blazor app is the `web` resource; the API
exposes OpenAPI at `/openapi/v1.json`.

First launch pulls the SQL Server image and can take a few minutes.

## Demo personas & credentials

Auth defaults to **Demo mode** (`Auth:Mode = Demo`): open the app, go to **Sign in**, and pick
a persona — **no password required**. Each persona demonstrates a role and part of the flow.

| Persona (EN) | Persona (AR) | Role | What it demonstrates |
|---|---|---|---|
| Ahmad Al-Rashid | أحمد الرشيد | Managing Director | Top of the chain; evaluates MD-direct reports |
| Layla Al-Sabah | ليلى الصباح | HR Administrator | **HR Calibration** dashboard (quota violations) |
| Fatima Al-Otaibi | فاطمة العتيبي | Sector Head (Investment) | Sector-level approvals inbox |
| Yousef Al-Kandari | يوسف الكندري | Department Manager (Equities) | Department review inbox; sees the mid-year case |
| Noura Al-Mutairi | نورة المطيري | Line Manager (Local Equities) | Scores **Khaled**'s evaluation and submits it |
| Khaled Al-Ajmi | خالد العجمي | Employee | An evaluation awaiting the manager's scoring |
| Sara Al-Fahad | سارة الفهد | Employee (mid-year manager change) | Two evaluating managers in the approval chain |
| Omar Al-Sabah | عمر الصباح | Chief of Staff (MD-direct) | Chain that skips department & sector approval |

A suggested tour: sign in as **Noura**, open **My Inbox**, score Khaled and submit; sign in as
**Yousef** and approve it; then sign in as **Layla** and open **HR Calibration** to see the
Investment sector flagged over quota.

### Real Entra ID (optional)

Set `Auth:Mode = EntraId` and provide an `AzureAd` configuration section
(`Microsoft.Identity.Web`) to authenticate against a real Microsoft Entra tenant instead of
the offline personas.

## Language

Use the **العربية / English** toggle in the header. It sets a culture cookie and re-renders the
app right-to-left for Arabic (`dir="rtl"`) or left-to-right for English.

## Tests

Unit tests (scoring / routing / quota):

```bash
dotnet test tests/Taqyeem.Application.Tests
```

End-to-end (Playwright) — start the app first, then:

```bash
cd e2e
npm ci
npx playwright install chromium
npx playwright test
```

The E2E specs mutate demo data, so reset to the pristine seed before a run:

```bash
aspire stop
./e2e/reset-demo-data.sh   # removes the SQL data volume; next run re-seeds
```

## CI/CD

- **`.github/workflows/ci.yml`** — on push/PR: restore, build, run the unit tests, then a
  Playwright E2E job that starts the app with Aspire and tests it.
- **`.github/workflows/deploy-staging.yml`** — deploys to the **staging** resource group via
  `aspire deploy` after CI succeeds on `main`, then verifies `/health` and `/version`.
- **`.github/workflows/deploy-production.yml`** — **release-gated**: publishing a GitHub Release
  deploys that exact tag to **production** behind the `production` environment's required
  reviewers, and verifies the deployed `/version` matches the release.
- **`.github/workflows/release-changelog.yml`** — on release, generates a human-friendly changelog
  with **GitHub Models** (`actions/ai-inference`) and updates the release notes.

## Azure deployment

Two environments — **staging** and **production** — separated by resource group, deployed
**Aspire-native** (`aspire deploy`). The local SQL Server maps to **Azure SQL Database** (managed-identity
auth), and the API + Blazor UI run on **Azure Container Apps** (only the web app is public) with
Application Insights and a container registry. See
[`docs/azure-deployment.md`](docs/azure-deployment.md) for setup, gating, and verification.

## License

Sample/demo code. Fictional data only.
