# System Architecture — CowetaConnect

> **Document Type:** Architecture Reference  
> **Architect:** Senior Software Architect  
> **Version:** 1.0.0

---

## 1. Architectural Style

CowetaConnect uses a **Modular Monolith** backend architecture with **vertical slice organization**, deployable as a single ASP.NET Core application but structured so that individual modules (Directory, Events, Analytics, ML) can be extracted into microservices if scale demands it in the future.

This is a deliberate choice for a community platform of this size — microservices introduce operational overhead that is not justified until proven necessary. See [ADR-001](ADR/ADR-001-backend-framework.md).

```
┌─────────────────────────────────────────────────────────────┐
│                        CLIENT TIER                          │
│              Vue.js 3 SPA (Vite + Pinia)                    │
│         Mobile-first, Tailwind CSS, Leaflet Maps            │
└────────────────────────┬────────────────────────────────────┘
                         │ HTTPS / REST + SignalR (WS)
┌────────────────────────▼────────────────────────────────────┐
│                       API TIER                              │
│            ASP.NET Core 8 Web API (C#)                      │
│   ┌──────────┬──────────┬───────────┬────────────────────┐  │
│   │Directory │  Events  │ Analytics │   ML Lead Engine   │  │
│   │ Module   │  Module  │  Module   │      Module        │  │
│   └──────────┴──────────┴───────────┴────────────────────┘  │
│                  Shared Kernel / Domain                      │
└──────┬────────────┬────────────────┬────────────────────────┘
       │            │                │
┌──────▼───┐  ┌─────▼──────┐  ┌────▼────────┐
│PostgreSQL│  │Elasticsearch│  │   Redis     │
│(primary) │  │  (search)  │  │  (cache)    │
└──────────┘  └────────────┘  └─────────────┘
```

---

## 2. System Context (C4 Level 1)

```
┌─────────────────────────────────────────────────────────────────┐
│                        CowetaConnect System                     │
│                                                                 │
│  ┌─────────────┐    ┌─────────────┐    ┌─────────────────────┐  │
│  │  Community  │    │  Business   │    │    Platform Admin   │  │
│  │   Member    │    │   Owner     │    │                     │  │
│  │  (Searcher) │    │  (Operator) │    │  (Staff / Ops)      │  │
│  └──────┬──────┘    └──────┬──────┘    └──────────┬──────────┘  │
│         │                 │                       │             │
│         └─────────────────▼───────────────────────┘            │
│                    CowetaConnect Web App                        │
└──────────────────────────────┬──────────────────────────────────┘
                               │
              ┌────────────────┼────────────────┐
              │                │                │
      ┌───────▼──────┐ ┌───────▼──────┐ ┌──────▼───────┐
      │  Google Maps  │ │  Google      │ │  SendGrid    │
      │  / MapBox API │ │  OAuth 2.0   │ │  (Email)     │
      └──────────────┘ └──────────────┘ └──────────────┘
```

### Actors

| Actor | Description |
|---|---|
| Community Member | Searches for businesses, browses events, no account required for read |
| Business Owner | Manages listing, creates events, views AI lead insights |
| Platform Admin | Moderates content, manages categories, views platform analytics |

---

## 3. Container Diagram (C4 Level 2)

```
┌──────────────────────────────────────────────────────────────────────┐
│  Vue.js SPA                                                          │
│  ┌───────────┐ ┌────────────┐ ┌──────────────┐ ┌─────────────────┐  │
│  │ Directory │ │  Events    │ │  Business    │ │  Admin          │  │
│  │   View    │ │  Calendar  │ │  Dashboard   │ │  Panel          │  │
│  └───────────┘ └────────────┘ └──────────────┘ └─────────────────┘  │
│  Pinia State Store | Vue Router | Axios HTTP Client | Leaflet Maps   │
└──────────────────────────────┬───────────────────────────────────────┘
                               │ REST API (JSON) over HTTPS
┌──────────────────────────────▼───────────────────────────────────────┐
│  ASP.NET Core 8 Web API                                              │
│  ┌────────────────────────────────────────────────────────────────┐  │
│  │  API Controllers (versioned, /api/v1/...)                      │  │
│  │  Middleware: Auth, Rate Limiting, CORS, Error Handling         │  │
│  └────────────────────────────────────────────────────────────────┘  │
│  ┌────────────┐ ┌───────────┐ ┌─────────────┐ ┌──────────────────┐  │
│  │ Directory  │ │  Events   │ │  Analytics  │ │  ML Lead         │  │
│  │  Module    │ │  Module   │ │   Module    │ │  Scoring Module  │  │
│  └────────────┘ └───────────┘ └─────────────┘ └──────────────────┘  │
│  ┌────────────────────────────────────────────────────────────────┐  │
│  │  Shared Kernel: Domain Entities, Repository Interfaces,        │  │
│  │  Event Bus (MediatR), Auth, Logging (Serilog), EF Core        │  │
│  └────────────────────────────────────────────────────────────────┘  │
└───┬──────────────┬───────────────────┬────────────────────────────────┘
    │              │                   │
┌───▼────┐  ┌──────▼──────┐  ┌────────▼────┐  ┌────────────────────┐
│Postgres│  │Elasticsearch│  │   Redis     │  │  ML.NET Model      │
│        │  │             │  │  Cache /    │  │  (file-based,      │
│Primary │  │  Full-text  │  │  Sessions   │  │   loaded at        │
│  DB    │  │   Search    │  │             │  │   startup)         │
└────────┘  └─────────────┘  └─────────────┘  └────────────────────┘
```

---

## 4. Module Breakdown

### 4.1 Directory Module

Responsible for all business listing functionality.

**Features:**
- CRUD for business profiles
- Category and tag management
- Geographic coordinates + service area
- Business hours, contact info, social links
- Photo galleries (stored in Azure Blob)
- Search with Elasticsearch (name, category, city, tags)
- Map-layer data endpoint (GeoJSON)

**Key Components:**
- `BusinessController` — REST endpoints
- `BusinessService` — Business logic, photo upload orchestration
- `BusinessRepository` — EF Core data access
- `SearchIndexer` — Syncs DB changes to Elasticsearch via MediatR events

### 4.2 Events Module

Manages community events created by business owners.

**Features:**
- Event creation (title, description, date/time, location, capacity, tags)
- Event types: Workshop, Market, Pop-up, Sale, Class, Meetup
- RSVP / attendance tracking
- iCal feed generation
- Calendar views (month, week, list)
- Event reminders via email (SendGrid)

**Key Components:**
- `EventController`
- `EventService`
- `RsvpService`
- `CalendarFeedBuilder` — generates .ics output
- `EventReminderJob` — Hangfire background job

### 4.3 Analytics Module

Captures and aggregates search behavior for the ML pipeline.

**Features:**
- Anonymous search event capture (keyword, filters used, result clicked, user ZIP/city)
- No PII collection — only geographic + behavioral signals
- Aggregation jobs that roll up raw events into summary tables
- Powers the AI Lead Scoring module

**Key Components:**
- `SearchEventMiddleware` — Intercepts search API calls, fires domain event
- `SearchEventConsumer` — MediatR handler, writes to analytics tables
- `AggregationJob` — Hangfire daily job, computes business×geography demand scores

### 4.4 ML Lead Scoring Module

The AI engine. See [AI_ML_DESIGN.md](AI_ML_DESIGN.md) for full detail.

**Summary:**
- Trained on aggregated search demand data using ML.NET
- Produces geographic demand scores per business category
- Surfaces "opportunity alerts" to business owners: "High search interest from Broken Arrow for [Honey / Bee Products]"
- Model retrained weekly via background job

---

## 5. Frontend Architecture

### Vue.js 3 Application Structure

```
src/
├── main.ts
├── App.vue
├── router/
│   └── index.ts                 ← Vue Router with route guards
├── stores/                      ← Pinia stores
│   ├── auth.store.ts
│   ├── business.store.ts
│   ├── events.store.ts
│   └── leads.store.ts
├── composables/                 ← Reusable Vue Composition functions
│   ├── useGeolocation.ts
│   ├── useSearch.ts
│   └── useMapbox.ts
├── views/                       ← Page-level components
│   ├── HomeView.vue
│   ├── DirectoryView.vue
│   ├── BusinessDetailView.vue
│   ├── EventsView.vue
│   ├── EventDetailView.vue
│   ├── Dashboard/
│   │   ├── DashboardView.vue
│   │   ├── LeadInsightsView.vue
│   │   └── ManageEventsView.vue
│   └── Admin/
│       └── AdminView.vue
├── components/
│   ├── ui/                      ← Generic reusable components
│   ├── business/                ← Business-specific components
│   ├── events/                  ← Event-specific components
│   ├── map/                     ← Map components (Leaflet wrappers)
│   └── charts/                  ← Chart components (Chart.js wrappers)
└── services/                    ← API client functions (Axios-based)
    ├── api.client.ts
    ├── business.service.ts
    ├── events.service.ts
    └── analytics.service.ts
```

### State Management (Pinia)

Each domain area has its own Pinia store. Stores are responsible for:
- Holding fetched data in memory
- Exposing actions that call API services
- Caching responses to reduce redundant API calls

### Authentication Flow

1. User clicks "Sign In" → redirected to Google OAuth OR email/password form
2. API returns JWT access token (15 min) + refresh token (7 day, httpOnly cookie)
3. Pinia `auth.store` holds decoded user claims
4. Axios interceptor automatically refreshes expired tokens
5. Route guards check roles (Owner, Admin, Member)

---

## 6. Data Flow: AI Lead Generation (End to End)

```
User searches "honey wagoner ok"
        │
        ▼
Vue SearchView.vue → GET /api/v1/businesses/search?q=honey&city=wagoner
        │
        ▼
BusinessController → SearchService → Elasticsearch query
        │
        │ (in parallel, via MediatR)
        ▼
SearchEventConsumer → writes SearchEvent to analytics_events table
{
  keyword: "honey",
  category_hint: "food-local",
  user_city: "Broken Arrow",    ← resolved from IP geolocation
  business_result_ids: [42],
  timestamp: 2026-02-18T14:32:00Z
}
        │
[Aggregation Job runs nightly]
        │
        ▼
Computes: business_id=42, demand_city="Broken Arrow", 
          search_count=47 (last 30 days), trend=+23%
        │
[ML Scoring Job runs weekly]
        │
        ▼
ML.NET model scores opportunity:
  OpportunityScore: 0.87 (HIGH)
  Confidence: 0.79
        │
        ▼
LeadAlert created → shown on Business Owner Dashboard
"📍 Strong demand signal: 47 people in Broken Arrow searched for 
 products like yours in the last 30 days."
```

---

## 7. Cross-Cutting Concerns

### Logging
- **Serilog** with structured logging
- Sinks: Console (dev), Azure Application Insights (prod)
- Correlation IDs on every request

### Caching Strategy
- Redis for: session tokens, search result caching (5 min TTL), geolocation cache
- EF Core second-level cache for category/tag lookups (static data)

### Error Handling
- Global exception middleware with RFC 7807 Problem Details responses
- Client-side: Axios interceptors catch 4xx/5xx, display toast notifications

### Rate Limiting
- ASP.NET Core built-in Rate Limiting middleware
- Limits: 60 req/min unauthenticated, 300 req/min authenticated

### Background Jobs
- **Hangfire** with PostgreSQL storage
- Jobs: Search aggregation (nightly), ML model retrain (weekly), email reminders (event-triggered)

---

## 8. Technology Decisions Summary

| Decision | Choice | Rationale |
|---|---|---|
| Backend framework | ASP.NET Core 8 | Mature, performant, strong C# ecosystem |
| ML framework | ML.NET | Native C#, no Python dependency |
| Frontend framework | Vue.js 3 | Lighter learning curve than React for small teams, excellent ecosystem |
| Primary DB | PostgreSQL | Robust, open source, strong geospatial support (PostGIS) |
| Search | Elasticsearch | Best-in-class full-text search for directory use cases |
| Caching | Redis | Industry standard, supports pub/sub if needed later |
| Deployment | Azure | Strong .NET integration, managed services reduce ops burden |
| Auth | ASP.NET Identity + JWT | Native to the stack, avoid third-party auth vendor lock-in |

See `/docs/ADR/` for detailed rationale on each decision.
