# CowetaConnect — Community Small Business Platform

> **Version:** 1.0.0  
> **Last Updated:** 2026-02-18  
> **Status:** Architecture Definition Phase

---

## Overview

CowetaConnect is a community-driven web platform serving Coweta, OK and surrounding areas (Wagoner County, Broken Arrow, Muskogee corridor). It helps residents **discover local small businesses**, attend **community events**, and gives business owners **AI-powered market intelligence** to identify growth opportunities.

---

## Core Capabilities

| Capability | Description |
|---|---|
| 🏪 Business Directory | Searchable, filterable listings for small businesses across the region |
| 📅 Event Calendar | Workshops, markets, pop-ups, and community events hosted by businesses |
| 🤖 AI Lead Intelligence | ML-driven geographic demand signals to surface new market opportunities |
| 👤 Business Owner Portal | Dashboard for managing listings, events, and viewing lead insights |
| 🗺️ Map-Based Discovery | Location-aware search and browsing powered by maps |

---

## Tech Stack Summary

| Layer | Technology |
|---|---|
| Frontend | Vue.js 3 (Composition API) + Vite + Pinia + Tailwind CSS |
| Backend API | ASP.NET Core 8 Web API (C#) |
| AI / ML Engine | ML.NET (C#) + custom recommendation pipeline |
| Database | PostgreSQL (primary) + Redis (caching/sessions) |
| Search | Elasticsearch (business + event full-text search) |
| Auth | ASP.NET Core Identity + JWT + OAuth2 (Google) |
| Maps | Leaflet.js + OpenStreetMap / MapBox |
| Hosting | Azure (App Service, Azure SQL flexible, Azure Cache, Azure AI) |
| CI/CD | GitHub Actions |

---

## Repository Structure

```
coweta-connect/
├── README.md                        ← You are here
├── docs/
│   ├── ARCHITECTURE.md              ← Full system architecture
│   ├── DATA_MODEL.md                ← Entity relationships and schemas
│   ├── AI_ML_DESIGN.md              ← ML pipeline and lead scoring design
│   ├── API_SPEC.md                  ← REST API endpoint specification
│   ├── SECURITY.md                  ← Security architecture and policies
│   ├── DEPLOYMENT.md                ← Infrastructure and deployment guide
│   └── ADR/                         ← Architecture Decision Records
│       ├── ADR-001-backend-framework.md
│       ├── ADR-002-ml-framework.md
│       ├── ADR-003-database-selection.md
│       └── ADR-004-frontend-framework.md
├── architecture/
│   ├── system-context.md            ← C4 Level 1: System Context
│   ├── container-diagram.md         ← C4 Level 2: Containers
│   └── component-diagrams.md        ← C4 Level 3: Components
└── diagrams/
    ├── erd.md                       ← Entity Relationship Diagram (Mermaid)
    ├── ml-pipeline.md               ← ML Pipeline flow (Mermaid)
    └── deployment.md                ← Infrastructure diagram (Mermaid)
```

---

## Local Development Quick Start

### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- [Node.js 20+](https://nodejs.org/)
- [Docker Desktop](https://www.docker.com/products/docker-desktop/)

### Setup

```bash
# 1. Start infrastructure (PostgreSQL/PostGIS, Redis, Elasticsearch)
docker-compose up -d

# 2. Copy example config and apply database migrations
cd src/CowetaConnect.API
cp appsettings.Development.json.example appsettings.Development.json
dotnet ef database update \
  --project ../CowetaConnect.Infrastructure \
  --startup-project .

# 3. Run the API
dotnet run

# 4. Run the Vue frontend (separate terminal)
cd ../../src/CowetaConnect.UI
npm install
npm run dev
```

The API will be available at `https://localhost:5001` and the frontend at `http://localhost:5173`.

> **Health checks:** After `docker-compose up -d`, verify services are ready with `docker ps` — all three containers should show `(healthy)` status before running migrations.

---

## Quick Links

- [Full Architecture →](docs/ARCHITECTURE.md)
- [Data Model →](docs/DATA_MODEL.md)
- [AI/ML Design →](docs/AI_ML_DESIGN.md)
- [API Specification →](docs/API_SPEC.md)
- [Security Design →](docs/SECURITY.md)
- [Deployment Guide →](docs/DEPLOYMENT.md)

---

## Guiding Principles

1. **Local First** — Optimized for the Coweta/Wagoner County context. No generic SaaS bloat.
2. **AI as Signal, Not Oracle** — ML surfaces insights; business owners make decisions.
3. **Mobile Responsive by Default** — Most local users browse on phones.
4. **Privacy-Respecting Analytics** — No individual tracking; only aggregate geographic signals.
5. **Operator Simplicity** — The platform must be maintainable by a small team.
6. **Incremental Delivery** — Ship MVP fast, iterate based on community feedback.

---

## Phased Delivery Plan

| Phase | Scope | Target Timeline |
|---|---|---|
| **Phase 1 — MVP** | Business directory, basic search, map view, owner accounts | 10 weeks |
| **Phase 2 — Events** | Event calendar, RSVP, business event management | 6 weeks |
| **Phase 3 — AI Leads** | Search analytics pipeline, lead scoring, owner dashboard insights | 8 weeks |
| **Phase 4 — Growth** | Mobile app (Vue Native / PWA), premium listings, email digests | TBD |

---

*This document is the authoritative entry point for all CowetaConnect architecture artifacts.*
