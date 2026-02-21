# Required GitHub Secrets

This document lists all GitHub Actions secrets that must be configured in the
repository settings before workflows can deploy or run migrations.

Navigate to: **Settings → Secrets and variables → Actions → New repository secret**

---

## deploy-api.yml secrets

### Azure OIDC (replaces publish profile — no rotating credentials)

| Secret name | Where to find it | Example value |
|---|---|---|
| `AZURE_CLIENT_ID` | Azure Portal → Azure Active Directory → App registrations → your app → Application (client) ID | `xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx` |
| `AZURE_TENANT_ID` | Azure Portal → Azure Active Directory → Overview → Tenant ID | `xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx` |
| `AZURE_SUBSCRIPTION_ID` | Azure Portal → Subscriptions → your subscription → Subscription ID | `xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx` |

**One-time Azure setup required:**
1. Register an app in Azure AD (or use an existing managed identity).
2. Add a federated credential on the app registration:
   - **Issuer:** `https://token.actions.githubusercontent.com`
   - **Subject:** `repo:<org>/<repo>:ref:refs/heads/main`
   - **Audience:** `api://AzureADTokenExchange`
3. Grant the app the **Contributor** role on the App Service resource (or the resource group).

### Database (EF Core migrations)

| Secret name | Where to find it |
|---|---|
| `DB_CONNECTION_STRING` | Azure Portal → PostgreSQL Flexible Server → Connection strings → ADO.NET |

The connection string must be a full Npgsql connection string, e.g.:
```
Host=cowetaconnect-postgres-prod.postgres.database.azure.com;Port=5432;Database=cowetaconnect;Username=dev;Password=<secret>;SSL Mode=Require;
```

---

## ci.yml secrets

No secrets required. CI runs on public runners with no Azure access.
