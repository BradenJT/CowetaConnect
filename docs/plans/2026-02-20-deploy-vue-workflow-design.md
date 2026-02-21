# Design: GitHub Actions Vue SPA Workflow

**Date:** 2026-02-20
**Status:** Approved
**Scope:** Vue CI/CD — ESLint setup, SWA config, ci-web.yml, deploy-vue.yml

---

## Context

CowetaConnect needs a CI/CD pipeline that lints, type-checks, builds, and deploys the Vue 3 SPA to Azure Static Web Apps on every push to `main`. The existing `ci.yml` has a `frontend-check` job that runs `npm run build` on PRs — this will be replaced by the more capable `ci-web.yml`.

---

## Decisions Made

| Decision | Choice | Rationale |
|---|---|---|
| ESLint version | v9 flat config (`eslint.config.js`) | Modern standard; matches what `create-vue` now generates |
| ESLint packages | `eslint`, `eslint-plugin-vue`, `@vue/eslint-config-typescript` | Vue 3 + TypeScript recommended stack |
| type-check script | `vue-tsc --noEmit` | Standard separate type-check; build uses `vue-tsc -b` (project refs) |
| ci-web.yml vs ci.yml | Replace `frontend-check` in ci.yml | Eliminates duplication; ci-web.yml adds path filter + lint |
| ci-web.yml job structure | Parallel lint + build jobs | Faster signal on PRs |
| SWA auth | `AZURE_STATIC_WEB_APPS_API_TOKEN` (as spec requires) | Simpler than OIDC for SWA; token is repo-scoped |
| staticwebapp.config.json location | `src/CowetaConnect.UI/public/` | Vite copies `public/` to `dist/` unchanged |
| deploy-vue.yml build output | `src/CowetaConnect.UI/dist` | Standard Vite output dir |
| Frontend directory | `src/CowetaConnect.UI/` | Actual path; spec used stale `src/web/` |

---

## Files Changed

| Action | File | Purpose |
|---|---|---|
| Modify | `src/CowetaConnect.UI/package.json` | Add `lint`, `type-check` scripts + ESLint devDeps |
| Create | `src/CowetaConnect.UI/eslint.config.js` | ESLint v9 flat config for Vue 3 + TypeScript |
| Create | `src/CowetaConnect.UI/public/staticwebapp.config.json` | 404→index.html fallback for Vue Router history mode |
| Modify | `.github/workflows/ci.yml` | Remove `frontend-check` job |
| Create | `.github/workflows/ci-web.yml` | PR validation: parallel lint + build jobs |
| Create | `.github/workflows/deploy-vue.yml` | Push-to-main: build + deploy to Azure SWA |
| Append | `docs/SECRETS.md` | Document `AZURE_STATIC_WEB_APPS_API_TOKEN` and `MAPBOX_TOKEN` |

---

## ESLint Config

**`eslint.config.js`** — ESLint v9 flat config:

```js
import pluginVue from 'eslint-plugin-vue'
import { defineConfigWithVueTs, vueTsConfigs } from '@vue/eslint-config-typescript'

export default defineConfigWithVueTs(
  pluginVue.configs['flat/essential'],
  vueTsConfigs.recommended,
)
```

**`package.json` additions:**

```json
"scripts": {
  "dev": "vite",
  "build": "vue-tsc -b && vite build",
  "preview": "vite preview",
  "lint": "eslint .",
  "type-check": "vue-tsc --noEmit"
},
"devDependencies": {
  ...existing...,
  "eslint": "^9.x",
  "eslint-plugin-vue": "^9.x",
  "@vue/eslint-config-typescript": "^14.x"
}
```

---

## staticwebapp.config.json

```json
{
  "navigationFallback": {
    "rewrite": "/index.html",
    "exclude": ["/assets/*", "/favicon.ico"]
  }
}
```

Located at `src/CowetaConnect.UI/public/staticwebapp.config.json` → copied to `dist/` by Vite at build time.

---

## ci-web.yml

```
Trigger: pull_request to main, paths: src/CowetaConnect.UI/**

Jobs (parallel):
  lint:
    - checkout
    - setup-node 20 (npm cache, lock: src/CowetaConnect.UI/package-lock.json)
    - npm ci (working-directory: src/CowetaConnect.UI)
    - npm run lint (working-directory: src/CowetaConnect.UI)

  build:
    - checkout
    - setup-node 20 (npm cache, lock: src/CowetaConnect.UI/package-lock.json)
    - npm ci (working-directory: src/CowetaConnect.UI)
    - npm run build (working-directory: src/CowetaConnect.UI)
      (= vue-tsc -b && vite build — covers type-check + build)
```

---

## deploy-vue.yml

```
Trigger: push to main, paths: src/CowetaConnect.UI/**
Concurrency: cancel-in-progress (group: deploy-vue-${{ github.ref }})

Job: build-and-deploy
  - checkout
  - setup-node 20 (npm cache)
  - npm ci (working-directory: src/CowetaConnect.UI)
  - npm run build (working-directory: src/CowetaConnect.UI)
    env:
      VITE_API_BASE_URL: https://api.cowetaconnect.com/api/v1
      VITE_MAPBOX_TOKEN: ${{ secrets.MAPBOX_TOKEN }}
  - Azure/static-web-apps-deploy@v1
    with:
      azure_static_web_apps_api_token: ${{ secrets.AZURE_STATIC_WEB_APPS_API_TOKEN }}
      repo_token: ${{ secrets.GITHUB_TOKEN }}
      action: upload
      app_location: src/CowetaConnect.UI/dist
      skip_app_build: true
```

---

## ci.yml Change

Remove the `frontend-check` job block entirely. The `build-check` and `lint-check` (.NET) jobs are untouched.

---

## Required GitHub Secrets

| Secret | Workflow | Notes |
|---|---|---|
| `AZURE_STATIC_WEB_APPS_API_TOKEN` | deploy-vue.yml | Download from Azure Portal → Static Web App → Manage deployment token |
| `MAPBOX_TOKEN` | deploy-vue.yml | Public-facing token; restrict to your domain in Mapbox dashboard |

---

## Relationship to Existing Workflows

- `ci.yml`: `frontend-check` job removed; `.NET` jobs unchanged
- `ci-web.yml`: new PR gate for frontend, path-filtered
- `deploy-vue.yml`: new deploy pipeline, path-filtered

On a push to `main` touching `src/CowetaConnect.UI/**`:
- `ci.yml` runs (.NET build + lint only, no frontend)
- `deploy-vue.yml` runs (build + SWA deploy)

On a PR touching `src/CowetaConnect.UI/**`:
- `ci.yml` runs (.NET jobs only)
- `ci-web.yml` runs (lint + build in parallel)
