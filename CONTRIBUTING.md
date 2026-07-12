# Contributing to Taqyeem

Thanks for your interest in improving **Taqyeem** — a runnable, Azure-native demo of a
bilingual (AR/EN) employee performance-evaluation app. This guide covers local setup, the
test suite, and how changes get merged.

> ⚠️ All data in this repo is **fictional** and for demonstration only.

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- [.NET Aspire CLI](https://aspire.dev) — `curl -sSL https://aspire.dev/install.sh | bash`
- **Docker** running (for the local SQL Server container). On Apple Silicon, enable Docker
  Desktop's "Use Rosetta for x86/amd64 emulation".
- Node.js 20+ (only for the end-to-end tests)

## Run it locally

```bash
aspire run
```

This starts SQL Server (container), applies EF Core migrations, seeds the fictional data, and
launches the API and Blazor UI. The Aspire dashboard (printed on start) lists endpoints, logs,
and traces. See [`README.md`](README.md) for demo personas and the suggested tour.

## Tests

Unit tests (scoring / routing / quota engines) — the fast inner loop:

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

## Branching & pull requests

1. Create a topic branch off `main` (e.g. `feat/...`, `fix/...`, `chore/...`).
2. Keep changes focused; update docs when behavior changes.
3. Open a pull request against `main`. The branch is protected, so:
   - **At least 1 approving review** is required before merge.
   - **CI must pass** — the `Build & unit tests` and `End-to-end (Playwright)` checks.
   - **Conversations must be resolved**, and your branch kept up to date with `main`.
4. Prefer clear, imperative commit messages (Conventional Commits — `feat:`, `fix:`, `docs:` —
   are welcome and make the auto-generated release changelog nicer).

## Code style

- Match the existing conventions in each layer (Domain → Application → Infrastructure → API → Web).
- If you touch C#, run `dotnet format` before committing to keep formatting consistent.
- Keep the core rule engines (scoring / routing / quota) covered by unit tests.

## Reporting security issues

Please do **not** open a public issue for security problems. See [`SECURITY.md`](SECURITY.md)
for how to report a vulnerability privately.
