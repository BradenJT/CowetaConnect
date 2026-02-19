# Deployment Guide — CowetaConnect

> **Document Type:** Infrastructure & Deployment  
> **Version:** 1.0.0  
> **Target:** Azure (primary)

---

## 1. Infrastructure Overview

```
                        ┌────────────────────────┐
                        │   GitHub Actions CI/CD  │
                        └───────────┬────────────┘
                                    │ deploy
              ┌─────────────────────▼──────────────────────┐
              │              Azure Front Door               │
              │         (CDN, WAF, SSL termination)         │
              └──────────────┬─────────────────────────────┘
                             │
              ┌──────────────┼───────────────────┐
              │              │                   │
    ┌─────────▼──────┐  ┌────▼─────────┐  ┌──────▼────────────┐
    │ Azure Static   │  │  Azure App   │  │  Azure Blob       │
    │  Web Apps      │  │  Service     │  │  Storage          │
    │  (Vue.js SPA)  │  │  (ASP.NET)   │  │  (Photos/Assets)  │
    └────────────────┘  └────┬─────────┘  └───────────────────┘
                             │
              ┌──────────────┼──────────────────────────┐
              │              │                          │
    ┌─────────▼──────┐  ┌────▼──────────┐  ┌───────────▼────┐
    │  PostgreSQL    │  │ Elasticsearch  │  │  Azure Cache   │
    │  Flexible Svr  │  │ (Elastic Cloud │  │  for Redis     │
    │                │  │  or Azure)     │  │                │
    └────────────────┘  └───────────────┘  └────────────────┘
              │
    ┌─────────▼──────┐  ┌────────────────┐
    │  Azure Key     │  │  App Insights  │
    │  Vault         │  │  (Monitoring)  │
    └────────────────┘  └────────────────┘
```

---

## 2. Azure Resources

### Resource Group Structure

```
coweta-connect-rg-prod/
├── cowetaconnect-front-door         (Azure Front Door + WAF)
├── cowetaconnect-swa-prod           (Static Web App - Vue)
├── cowetaconnect-api-prod           (App Service - ASP.NET)
├── cowetaconnect-asp-prod           (App Service Plan - B2 or P1v3)
├── cowetaconnect-postgres-prod      (PostgreSQL Flexible Server)
├── cowetaconnect-redis-prod         (Azure Cache for Redis - C1)
├── cowetaconnect-storage-prod       (Storage Account - photos, ML models)
├── cowetaconnect-kv-prod            (Key Vault)
├── cowetaconnect-insights-prod      (Application Insights)
└── cowetaconnect-log-prod           (Log Analytics Workspace)
```

### Sizing (MVP / Phase 1-2)

| Resource | SKU | Est. Cost/Month |
|---|---|---|
| App Service Plan | B2 (2 cores, 3.5GB RAM) | ~$75 |
| PostgreSQL Flexible | Burstable B2ms | ~$35 |
| Redis Cache | C1 Basic (1GB) | ~$17 |
| Static Web Apps | Free tier | $0 |
| Front Door | Standard tier | ~$35 |
| Blob Storage | LRS, 50GB | ~$2 |
| Key Vault | Standard | ~$5 |
| Application Insights | Pay-per-use | ~$10 |
| **Total Estimate** | | **~$180/month** |

Scale up when monthly active users exceed ~1,000.

---

## 3. Environment Configuration

### Environments

| Environment | Purpose | Branch |
|---|---|---|
| `development` | Local dev, mocked services | `feature/*` |
| `staging` | Integration testing, pre-release | `develop` |
| `production` | Live system | `main` |

### App Service Configuration (Key Vault References)

All sensitive values stored in Key Vault, referenced via `@Microsoft.KeyVault(...)`:

```
ConnectionStrings__DefaultConnection  = @Microsoft.KeyVault(VaultName=cowetaconnect-kv-prod;SecretName=postgres-connection-string)
ConnectionStrings__Redis              = @Microsoft.KeyVault(VaultName=cowetaconnect-kv-prod;SecretName=redis-connection-string)
Jwt__PrivateKey                       = @Microsoft.KeyVault(VaultName=cowetaconnect-kv-prod;SecretName=jwt-private-key)
Elasticsearch__Uri                    = @Microsoft.KeyVault(VaultName=cowetaconnect-kv-prod;SecretName=elasticsearch-uri)
SendGrid__ApiKey                      = @Microsoft.KeyVault(VaultName=cowetaconnect-kv-prod;SecretName=sendgrid-api-key)
Azure__BlobStorage__ConnectionString  = @Microsoft.KeyVault(VaultName=cowetaconnect-kv-prod;SecretName=storage-connection-string)
```

Non-sensitive config in `appsettings.Production.json`:
```json
{
  "App": {
    "BaseUrl": "https://api.cowetaconnect.com",
    "SpaOrigin": "https://cowetaconnect.com"
  },
  "Hangfire": {
    "WorkerCount": 2
  },
  "ML": {
    "ModelDirectory": "/home/site/ml-models",
    "RetrainingSchedule": "0 2 * * 0"
  }
}
```

---

## 4. CI/CD Pipeline (GitHub Actions)

### `.github/workflows/deploy-api.yml`

```yaml
name: Deploy API

on:
  push:
    branches: [main]
    paths: ['src/api/**']

jobs:
  build-and-test:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      
      - name: Setup .NET
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '8.0.x'
      
      - name: Restore
        run: dotnet restore src/api/CowetaConnect.Api.sln
      
      - name: Build
        run: dotnet build --no-restore --configuration Release src/api/CowetaConnect.Api.sln
      
      - name: Test
        run: dotnet test --no-build --configuration Release --logger trx src/api/CowetaConnect.Api.sln
      
      - name: Publish
        run: dotnet publish --no-build --configuration Release -o ./publish src/api/CowetaConnect.Api/CowetaConnect.Api.csproj
      
      - name: Deploy to Azure App Service
        uses: azure/webapps-deploy@v3
        with:
          app-name: cowetaconnect-api-prod
          publish-profile: ${{ secrets.AZURE_API_PUBLISH_PROFILE }}
          package: ./publish

  run-migrations:
    needs: build-and-test
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - name: Setup .NET
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '8.0.x'
      - name: Run EF Core Migrations
        run: |
          dotnet tool install --global dotnet-ef
          dotnet ef database update --project src/api/CowetaConnect.Infrastructure \
            --startup-project src/api/CowetaConnect.Api \
            --connection "${{ secrets.DB_CONNECTION_STRING }}"
```

### `.github/workflows/deploy-vue.yml`

```yaml
name: Deploy Vue SPA

on:
  push:
    branches: [main]
    paths: ['src/web/**']

jobs:
  build-and-deploy:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      
      - name: Setup Node
        uses: actions/setup-node@v4
        with:
          node-version: '20'
          cache: 'npm'
          cache-dependency-path: src/web/package-lock.json
      
      - name: Install dependencies
        run: npm ci
        working-directory: src/web
      
      - name: Build
        run: npm run build
        working-directory: src/web
        env:
          VITE_API_BASE_URL: https://api.cowetaconnect.com/api/v1
          VITE_MAPBOX_TOKEN: ${{ secrets.MAPBOX_TOKEN }}
      
      - name: Deploy to Azure Static Web Apps
        uses: Azure/static-web-apps-deploy@v1
        with:
          azure_static_web_apps_api_token: ${{ secrets.AZURE_STATIC_WEB_APPS_API_TOKEN }}
          repo_token: ${{ secrets.GITHUB_TOKEN }}
          action: upload
          app_location: src/web/dist
```

---

## 5. Database Management

### Migrations

EF Core Code First migrations. Migration files are committed to source control.

```bash
# Create new migration
dotnet ef migrations add AddLeadAlerts \
  --project src/api/CowetaConnect.Infrastructure \
  --startup-project src/api/CowetaConnect.Api

# Apply migrations locally
dotnet ef database update \
  --project src/api/CowetaConnect.Infrastructure \
  --startup-project src/api/CowetaConnect.Api
```

### Backup Policy

| Database | Backup Frequency | Retention |
|---|---|---|
| PostgreSQL | Automatic (Azure managed) | 7 days point-in-time, weekly snapshot 35 days |
| Blob Storage | Azure Backup | 30 days |
| Redis | Persistence off (cache only; no critical state) | N/A |

---

## 6. Monitoring & Alerting

### Application Insights Alerts

| Alert | Condition | Notification |
|---|---|---|
| API Errors | 5xx rate > 2% over 5 min | Email + SMS to ops team |
| Response Time | P95 > 2s over 10 min | Email |
| Failed Logins | > 50 in 5 min (single IP) | Email + auto-block |
| ML Job Failure | Hangfire job fails | Email |
| DB CPU | PostgreSQL CPU > 80% for 15 min | Email |

### Health Check Endpoint

`GET /health` — Returns system health (DB, Redis, Elasticsearch connectivity).

```json
{
  "status": "Healthy",
  "checks": {
    "database": "Healthy",
    "redis": "Healthy",
    "elasticsearch": "Healthy",
    "ml-model": "Healthy"
  },
  "duration": "00:00:00.0234"
}
```

---

## 7. Local Development Setup

### Prerequisites

- .NET 8 SDK
- Node.js 20+
- Docker Desktop (for PostgreSQL, Redis, Elasticsearch via Docker Compose)

### Quick Start

```bash
# Clone repo
git clone https://github.com/[org]/coweta-connect.git
cd coweta-connect

# Start infrastructure
docker-compose up -d

# API
cd src/api/CowetaConnect.Api
cp appsettings.Development.json.example appsettings.Development.json
dotnet ef database update
dotnet run

# Vue SPA (separate terminal)
cd src/web
npm install
npm run dev
```

### `docker-compose.yml` (development)

```yaml
version: '3.8'
services:
  postgres:
    image: postgis/postgis:16-3.4
    environment:
      POSTGRES_DB: cowetaconnect
      POSTGRES_USER: dev
      POSTGRES_PASSWORD: devpassword
    ports:
      - "5432:5432"
    volumes:
      - postgres_data:/var/lib/postgresql/data

  redis:
    image: redis:7-alpine
    ports:
      - "6379:6379"

  elasticsearch:
    image: elasticsearch:8.12.0
    environment:
      - discovery.type=single-node
      - xpack.security.enabled=false
      - ES_JAVA_OPTS=-Xms512m -Xmx512m
    ports:
      - "9200:9200"

volumes:
  postgres_data:
```
