# Vue SPA CI/CD Workflow Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Set up ESLint, a Vue Router–compatible Azure SWA config, a PR validation workflow, and a push-to-main deploy workflow for the Vue 3 SPA.

**Architecture:** ESLint v9 flat config is added to the Vue project; `staticwebapp.config.json` is placed in `public/` so Vite copies it to `dist/`; `ci-web.yml` gates PRs with parallel lint + build jobs; `deploy-vue.yml` builds with Vite env vars and deploys to Azure Static Web Apps; `ci.yml`'s redundant `frontend-check` job is removed.

**Tech Stack:** ESLint v9 (flat config), eslint-plugin-vue, @vue/eslint-config-typescript, Azure/static-web-apps-deploy@v1, Node 20, Vite, vue-tsc.

---

### Task 1: Install ESLint and create config

**Files:**
- Modify: `src/CowetaConnect.UI/package.json`
- Create: `src/CowetaConnect.UI/eslint.config.js`

**Step 1: Install ESLint packages**

```bash
cd src/CowetaConnect.UI && npm install --save-dev eslint eslint-plugin-vue @vue/eslint-config-typescript
```

Expected: packages resolve without errors, `package-lock.json` updated.

**Step 2: Verify packages were added to devDependencies**

```bash
node -e "const p = require('./package.json'); console.log(Object.keys(p.devDependencies).filter(k => k.includes('eslint')))"
```

Run from `src/CowetaConnect.UI`. Expected output includes `eslint`, `eslint-plugin-vue`, `@vue/eslint-config-typescript`.

**Step 3: Add lint and type-check scripts to package.json**

Open `src/CowetaConnect.UI/package.json`. The `scripts` block currently reads:

```json
"scripts": {
  "dev": "vite",
  "build": "vue-tsc -b && vite build",
  "preview": "vite preview"
},
```

Replace it with:

```json
"scripts": {
  "dev": "vite",
  "build": "vue-tsc -b && vite build",
  "preview": "vite preview",
  "lint": "eslint .",
  "type-check": "vue-tsc --noEmit"
},
```

**Step 4: Create `src/CowetaConnect.UI/eslint.config.js`**

```js
import pluginVue from 'eslint-plugin-vue'
import { defineConfigWithVueTs, vueTsConfigs } from '@vue/eslint-config-typescript'

export default defineConfigWithVueTs(
  { ignores: ['dist/**'] },
  pluginVue.configs['flat/essential'],
  vueTsConfigs.recommended,
)
```

**Step 5: Verify lint runs without errors**

```bash
cd src/CowetaConnect.UI && npm run lint
```

Expected: exits 0, no errors. If you see TypeScript or Vue rule violations, fix them before continuing (a fresh scaffold should be clean).

**Step 6: Verify type-check runs without errors**

```bash
cd src/CowetaConnect.UI && npm run type-check
```

Expected: exits 0, no errors.

---

### Task 2: Create staticwebapp.config.json

**Files:**
- Create: `src/CowetaConnect.UI/public/staticwebapp.config.json`

**Step 1: Confirm the public/ directory exists**

```bash
ls src/CowetaConnect.UI/public/
```

Expected: directory exists (it's part of the Vite scaffold — contains `favicon.ico`). If it doesn't exist, create it: `mkdir src/CowetaConnect.UI/public`.

**Step 2: Create the file**

Create `src/CowetaConnect.UI/public/staticwebapp.config.json` with exactly this content:

```json
{
  "navigationFallback": {
    "rewrite": "/index.html",
    "exclude": ["/assets/*", "/favicon.ico"]
  }
}
```

This tells Azure Static Web Apps to serve `index.html` for any route that doesn't match a real file — required for Vue Router history mode so that deep links like `/businesses/123` don't return 404.

**Step 3: Verify the file will be included in the Vite build**

```bash
cd src/CowetaConnect.UI && npm run build 2>&1 | tail -15
```

Expected: build succeeds and the output mentions files written to `dist/`. After the build, verify:

```bash
ls src/CowetaConnect.UI/dist/staticwebapp.config.json
```

Expected: file exists in `dist/`.

---

### Task 3: Commit ESLint setup and SWA config

**Step 1: Stage all changes so far**

```bash
git add src/CowetaConnect.UI/package.json \
        src/CowetaConnect.UI/package-lock.json \
        src/CowetaConnect.UI/eslint.config.js \
        src/CowetaConnect.UI/public/staticwebapp.config.json
```

**Step 2: Verify staged files**

```bash
git diff --cached --name-only
```

Expected (order may vary):
```
src/CowetaConnect.UI/eslint.config.js
src/CowetaConnect.UI/package-lock.json
src/CowetaConnect.UI/package.json
src/CowetaConnect.UI/public/staticwebapp.config.json
```

**Step 3: Commit**

```bash
git commit -m "$(cat <<'EOF'
Add ESLint v9 and staticwebapp.config.json to Vue project

Installs eslint, eslint-plugin-vue, @vue/eslint-config-typescript with
flat config. Adds lint and type-check scripts to package.json.

Adds public/staticwebapp.config.json so Azure SWA serves index.html
for all unmatched routes, enabling Vue Router history mode deep links.

Co-Authored-By: Claude Sonnet 4.6 <noreply@anthropic.com>
EOF
)"
```

---

### Task 4: Remove frontend-check from ci.yml

**Files:**
- Modify: `.github/workflows/ci.yml`

The existing `ci.yml` has a `frontend-check` job that duplicates what `ci-web.yml` will do. Remove it.

**Step 1: Read the current ci.yml to find the exact block to remove**

Open `.github/workflows/ci.yml`. The block to remove starts with the comment `# Vue.js — Type check + build` and ends after the `npm run build` step. It looks like this:

```yaml
  # ─────────────────────────────────────────────
  # Vue.js — Type check + build
  # npm run build = vue-tsc -b && vite build
  # ─────────────────────────────────────────────
  frontend-check:
    name: frontend-check
    runs-on: ubuntu-latest

    steps:
      - name: Checkout
        uses: actions/checkout@v4

      - name: Setup Node 20
        uses: actions/setup-node@v4
        with:
          node-version: '20'
          cache: 'npm'
          cache-dependency-path: src/CowetaConnect.UI/package-lock.json

      - name: Install dependencies
        working-directory: src/CowetaConnect.UI
        run: npm ci

      - name: Type check and build
        working-directory: src/CowetaConnect.UI
        run: npm run build
```

Delete that entire block. The file should end after the `lint-check` job's last line.

**Step 2: Verify ci.yml no longer references frontend-check**

```bash
grep -c 'frontend-check' .github/workflows/ci.yml
```

Expected: `0`

**Step 3: Verify ci.yml still has the two .NET jobs**

```bash
grep -E '^\s+(build-check|lint-check):' .github/workflows/ci.yml
```

Expected:
```
  build-check:
  lint-check:
```

**Step 4: Validate YAML syntax**

```bash
python3 -c "import yaml; yaml.safe_load(open('.github/workflows/ci.yml'))" 2>/dev/null && echo "YAML OK" || node -e "const fs=require('fs'); fs.readFileSync('.github/workflows/ci.yml','utf8'); console.log('YAML readable')"
```

Expected: `YAML OK` or `YAML readable` with no error.

---

### Task 5: Create ci-web.yml

**Files:**
- Create: `.github/workflows/ci-web.yml`

**Step 1: Create the file**

Create `.github/workflows/ci-web.yml` with exactly this content:

```yaml
name: CI Web

on:
  pull_request:
    branches: [main]
    paths:
      - 'src/CowetaConnect.UI/**'

# Cancel in-progress runs for the same ref so stacked pushes don't queue up.
concurrency:
  group: ci-web-${{ github.ref }}
  cancel-in-progress: true

jobs:

  # ─────────────────────────────────────────────
  # ESLint
  # ─────────────────────────────────────────────
  lint:
    name: lint
    runs-on: ubuntu-latest

    steps:
      - name: Checkout
        uses: actions/checkout@v4

      - name: Setup Node 20
        uses: actions/setup-node@v4
        with:
          node-version: '20'
          cache: 'npm'
          cache-dependency-path: src/CowetaConnect.UI/package-lock.json

      - name: Install dependencies
        working-directory: src/CowetaConnect.UI
        run: npm ci

      - name: Lint
        working-directory: src/CowetaConnect.UI
        run: npm run lint

  # ─────────────────────────────────────────────
  # Type check + build
  # npm run build = vue-tsc -b && vite build
  # ─────────────────────────────────────────────
  build:
    name: build
    runs-on: ubuntu-latest

    steps:
      - name: Checkout
        uses: actions/checkout@v4

      - name: Setup Node 20
        uses: actions/setup-node@v4
        with:
          node-version: '20'
          cache: 'npm'
          cache-dependency-path: src/CowetaConnect.UI/package-lock.json

      - name: Install dependencies
        working-directory: src/CowetaConnect.UI
        run: npm ci

      - name: Build
        working-directory: src/CowetaConnect.UI
        run: npm run build
```

**Step 2: Validate YAML syntax**

```bash
python3 -c "import yaml; yaml.safe_load(open('.github/workflows/ci-web.yml'))" 2>/dev/null && echo "YAML OK" || node -e "const fs=require('fs'); fs.readFileSync('.github/workflows/ci-web.yml','utf8'); console.log('YAML readable')"
```

Expected: `YAML OK` or `YAML readable`.

**Step 3: Verify structure**

```bash
python3 -c "
content = open('.github/workflows/ci-web.yml').read()
checks = [
  ('trigger: pull_request', 'pull_request' in content),
  ('branch: main', 'branches: [main]' in content),
  ('path filter: UI', \"'src/CowetaConnect.UI/**'\" in content),
  ('lint job', 'npm run lint' in content),
  ('build job', 'npm run build' in content),
  ('node-version 20', \"node-version: '20'\" in content),
  ('no deploy step', 'deploy' not in content.lower() or 'static-web-apps' not in content),
]
for label, ok in checks:
    print(f'[{\"PASS\" if ok else \"FAIL\"}] {label}')
" 2>/dev/null || grep -E 'pull_request|lint|build|node-version' .github/workflows/ci-web.yml
```

---

### Task 6: Create deploy-vue.yml

**Files:**
- Create: `.github/workflows/deploy-vue.yml`

**Step 1: Create the file**

Create `.github/workflows/deploy-vue.yml` with exactly this content:

```yaml
name: Deploy Vue

on:
  push:
    branches: [main]
    paths:
      - 'src/CowetaConnect.UI/**'

# Cancel in-progress runs for the same ref so stacked pushes don't queue up.
concurrency:
  group: deploy-vue-${{ github.ref }}
  cancel-in-progress: true

jobs:

  # ─────────────────────────────────────────────
  # Build + deploy to Azure Static Web Apps
  # ─────────────────────────────────────────────
  build-and-deploy:
    name: build-and-deploy
    runs-on: ubuntu-latest

    steps:
      - name: Checkout
        uses: actions/checkout@v4

      - name: Setup Node 20
        uses: actions/setup-node@v4
        with:
          node-version: '20'
          cache: 'npm'
          cache-dependency-path: src/CowetaConnect.UI/package-lock.json

      - name: Install dependencies
        working-directory: src/CowetaConnect.UI
        run: npm ci

      - name: Build
        working-directory: src/CowetaConnect.UI
        run: npm run build
        env:
          VITE_API_BASE_URL: https://api.cowetaconnect.com/api/v1
          VITE_MAPBOX_TOKEN: ${{ secrets.MAPBOX_TOKEN }}

      - name: Deploy to Azure Static Web Apps
        uses: Azure/static-web-apps-deploy@v1
        with:
          azure_static_web_apps_api_token: ${{ secrets.AZURE_STATIC_WEB_APPS_API_TOKEN }}
          repo_token: ${{ secrets.GITHUB_TOKEN }}
          action: upload
          app_location: src/CowetaConnect.UI/dist
          skip_app_build: true
```

**Step 2: Validate YAML syntax**

```bash
python3 -c "import yaml; yaml.safe_load(open('.github/workflows/deploy-vue.yml'))" 2>/dev/null && echo "YAML OK" || node -e "const fs=require('fs'); fs.readFileSync('.github/workflows/deploy-vue.yml','utf8'); console.log('YAML readable')"
```

Expected: `YAML OK` or `YAML readable`.

**Step 3: Verify secret references**

```bash
grep -n 'secrets\.' .github/workflows/deploy-vue.yml
```

Expected output — exactly these two:
```
          VITE_MAPBOX_TOKEN: ${{ secrets.MAPBOX_TOKEN }}
          azure_static_web_apps_api_token: ${{ secrets.AZURE_STATIC_WEB_APPS_API_TOKEN }}
```

**Step 4: Verify path filter and build output path**

```bash
python3 -c "
content = open('.github/workflows/deploy-vue.yml').read()
checks = [
  ('trigger: push to main', 'push' in content and 'branches: [main]' in content),
  ('path filter: UI', \"'src/CowetaConnect.UI/**'\" in content),
  ('VITE_API_BASE_URL set', 'VITE_API_BASE_URL' in content),
  ('MAPBOX_TOKEN secret', 'secrets.MAPBOX_TOKEN' in content),
  ('SWA deploy action', 'static-web-apps-deploy@v1' in content),
  ('app_location dist', 'src/CowetaConnect.UI/dist' in content),
  ('skip_app_build', 'skip_app_build: true' in content),
  ('SWA token secret', 'secrets.AZURE_STATIC_WEB_APPS_API_TOKEN' in content),
  ('concurrency cancel', 'cancel-in-progress: true' in content),
]
for label, ok in checks:
    print(f'[{\"PASS\" if ok else \"FAIL\"}] {label}')
" 2>/dev/null || echo "Run manually"
```

---

### Task 7: Update docs/SECRETS.md

**Files:**
- Modify: `docs/SECRETS.md`

**Step 1: Append the Vue secrets section**

Open `docs/SECRETS.md`. At the end of the file, add:

```markdown

## deploy-vue.yml secrets

| Secret name | Where to find it | Notes |
|---|---|---|
| `AZURE_STATIC_WEB_APPS_API_TOKEN` | Azure Portal → Static Web App resource → Settings → Deployment token (Manage token) | Regenerate if compromised; no manual rotation needed otherwise |
| `MAPBOX_TOKEN` | Mapbox account → Tokens → your public token | Restrict to `https://cowetaconnect.com` in Mapbox dashboard to prevent abuse |

**No OIDC setup needed** for Azure Static Web Apps — the deployment token is sufficient and is scoped to the SWA resource.
```

**Step 2: Verify the section was added**

```bash
grep -c 'AZURE_STATIC_WEB_APPS_API_TOKEN' docs/SECRETS.md
```

Expected: `1`

---

### Task 8: Commit all workflow changes

**Step 1: Stage files**

```bash
git add .github/workflows/ci.yml \
        .github/workflows/ci-web.yml \
        .github/workflows/deploy-vue.yml \
        docs/SECRETS.md
```

**Step 2: Verify staged files**

```bash
git diff --cached --name-only
```

Expected:
```
.github/workflows/ci-web.yml
.github/workflows/ci.yml
.github/workflows/deploy-vue.yml
docs/SECRETS.md
```

**Step 3: Commit**

```bash
git commit -m "$(cat <<'EOF'
Add Vue CI/CD workflows and update ci.yml

ci-web.yml: PR validation with parallel lint + build jobs,
path-filtered to src/CowetaConnect.UI/**

deploy-vue.yml: push-to-main pipeline that builds with Vite
env vars (VITE_API_BASE_URL, VITE_MAPBOX_TOKEN) and deploys
to Azure Static Web Apps via deployment token.

ci.yml: remove frontend-check job (replaced by ci-web.yml)

docs/SECRETS.md: add AZURE_STATIC_WEB_APPS_API_TOKEN and
MAPBOX_TOKEN documentation.

Co-Authored-By: Claude Sonnet 4.6 <noreply@anthropic.com>
EOF
)"
```

---

### Task 9: Verify final state

**Step 1: List all workflow files**

```bash
ls .github/workflows/
```

Expected:
```
ci-web.yml
ci.yml
deploy-api.yml
deploy-vue.yml
```

**Step 2: Confirm frontend-check is gone from ci.yml**

```bash
grep 'frontend-check' .github/workflows/ci.yml && echo "FOUND - ERROR" || echo "Not found - PASS"
```

Expected: `Not found - PASS`

**Step 3: Confirm dist/ is in .gitignore (build output should not be committed)**

```bash
grep 'dist/' .gitignore
```

Expected: `dist/` or `**/dist/` appears — the existing `.gitignore` already covers this.

**Step 4: Confirm staticwebapp.config.json is in public/**

```bash
ls src/CowetaConnect.UI/public/staticwebapp.config.json
```

Expected: file exists.

**Step 5: Run a full local build to confirm everything works together**

```bash
cd src/CowetaConnect.UI && npm run lint && npm run type-check && npm run build 2>&1 | tail -10
```

Expected: all three commands succeed (exit 0). The build output ends with Vite reporting files written to `dist/`.
