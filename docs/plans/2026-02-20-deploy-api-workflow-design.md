# Design: GitHub Actions Deploy API Workflow

**Date:** 2026-02-20
**Status:** Approved
**Scope:** `.github/workflows/deploy-api.yml`

---

## Context

CowetaConnect needs a CI/CD pipeline that builds, tests, and deploys the ASP.NET Core API to Azure App Service on every push to `main`. The existing `ci.yml` handles PR validation for the full solution; this workflow handles production deployment only.

---

## Decisions Made

| Decision | Choice | Rationale |
|---|---|---|
| .NET version | 10.0.x (ga) | Matches actual csproj targets and existing ci.yml |
| Azure auth | OIDC (federated identity) | No long-lived secrets; preferred by security policy |
| PR validation | Use existing ci.yml | Avoids duplicating build/test jobs; ci.yml already gates PRs |
| Solution file | `CowetaConnect.slnx` | Actual repo layout; spec paths (`src/api/`) were stale |
| EF migration timing | Post-deploy job | Matches issue spec; acceptable for small-team workflow |

---

## Trigger

```yaml
on:
  push:
    branches: [main]
    paths:
      - 'src/CowetaConnect.API/**'
      - 'src/CowetaConnect.Application/**'
      - 'src/CowetaConnect.Domain/**'
      - 'src/CowetaConnect.Infrastructure/**'
```

Path filter ensures frontend-only pushes don't trigger an API deploy cycle.

---

## Job Architecture

```
build-and-test
      │
      ▼ (needs: build-and-test)
   deploy  ──── environment: production
      │
      ▼ (needs: deploy)
  migrate
```

### Job 1 — `build-and-test`

| Step | Command |
|---|---|
| Checkout | `actions/checkout@v4` |
| Setup .NET | `actions/setup-dotnet@v4`, version `10.0.x`, quality `ga` |
| Restore | `dotnet restore CowetaConnect.slnx` |
| Build | `dotnet build CowetaConnect.slnx --no-restore --configuration Release` |
| Test | `dotnet test CowetaConnect.slnx --no-build --configuration Release --logger trx --results-directory ./TestResults` |
| Upload test results | `actions/upload-artifact@v4`, path `./TestResults/*.trx`, `if: always()` |
| Publish | `dotnet publish src/CowetaConnect.API/CowetaConnect.API.csproj --no-build --configuration Release -o ./publish` |
| Upload publish artifact | `actions/upload-artifact@v4`, name `api-artifact` |

### Job 2 — `deploy`

- `needs: build-and-test`
- `environment: production` (enables GitHub deployment protection rules)
- `permissions: id-token: write, contents: read`

| Step | Action |
|---|---|
| Checkout | `actions/checkout@v4` |
| Download artifact | `actions/download-artifact@v4`, name `api-artifact`, path `./publish` |
| Azure Login | `azure/login@v2` with OIDC secrets |
| Deploy | `azure/webapps-deploy@v3`, app-name `cowetaconnect-api-prod`, package `./publish` |

### Job 3 — `migrate`

- `needs: deploy`
- Fresh checkout + restore to get source for EF tooling

| Step | Command |
|---|---|
| Checkout | `actions/checkout@v4` |
| Setup .NET | `actions/setup-dotnet@v4`, version `10.0.x`, quality `ga` |
| Restore | `dotnet restore CowetaConnect.slnx` |
| Install EF tool | `dotnet tool install --global dotnet-ef` |
| Run migrations | `dotnet ef database update --project src/CowetaConnect.Infrastructure --startup-project src/CowetaConnect.API --connection "${{ secrets.DB_CONNECTION_STRING }}"` |

---

## Required GitHub Secrets

| Secret | Purpose | Notes |
|---|---|---|
| `AZURE_CLIENT_ID` | OIDC app registration client ID | From Azure AD app registration |
| `AZURE_TENANT_ID` | Azure AD tenant ID | From Azure AD |
| `AZURE_SUBSCRIPTION_ID` | Azure subscription ID | From Azure Portal |
| `DB_CONNECTION_STRING` | PostgreSQL connection string for EF migrations | Full ADO.NET / Npgsql connection string |

**Not needed** (replaced by OIDC): `AZURE_API_PUBLISH_PROFILE`

---

## Concurrency

```yaml
concurrency:
  group: deploy-api-${{ github.ref }}
  cancel-in-progress: true
```

Matches pattern from `ci.yml`. Stacked pushes cancel the in-flight run.

---

## Relationship to Existing ci.yml

`ci.yml` is left untouched. It continues to:
- Gate all PRs to `main` (build + test + lint + frontend)
- Run on push to `main` (full solution validation)

On a main push that touches backend paths, both workflows run. Build+test executes twice. This is intentional: the deploy workflow validates independently before deploying. Acceptable duplication for a small team.

---

## Out of Scope

- `ci-api.yml` — not created; `ci.yml` already covers PR validation
- Vue SPA deployment — separate future workflow
- Staging environment — future work
