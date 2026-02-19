# C4 Level 3 — Component Diagrams: CowetaConnect

> **C4 Model Level:** 3 — Components  
> **Purpose:** Shows the internal components of each major container and how they interact.  
> **Audience:** Developers building or maintaining CowetaConnect.

---

## 1. ASP.NET Core API — Directory Module

```mermaid
C4Component
    title Component Diagram — Directory Module (ASP.NET Core API)

    Container_Boundary(api, "ASP.NET Core API") {

        Component(businesses_ctrl, "BusinessesController", "ASP.NET Controller", "Exposes REST endpoints: GET /businesses, GET /businesses/{slug}, POST /businesses, PUT /businesses/{id}, DELETE /businesses/{id}, GET /businesses/map, POST /businesses/{id}/photos")

        Component(business_svc, "BusinessService", "C# Service", "Orchestrates business creation, update, photo upload, verification. Enforces ownership rules. Publishes domain events via MediatR.")

        Component(business_repo, "BusinessRepository", "EF Core Repository", "CRUD and PostGIS radius queries against the businesses table. Implements IBusinessRepository.")

        Component(search_handler, "SearchBusinessesQueryHandler", "MediatR Handler", "Builds Elasticsearch query from search parameters (keyword, filters, geo). Returns paginated business DTOs with relevance scoring.")

        Component(search_indexer, "BusinessSearchIndexer", "MediatR Event Handler", "Listens for BusinessCreatedEvent and BusinessUpdatedEvent. Syncs the Elasticsearch 'businesses' index.")

        Component(photo_svc, "BusinessPhotoService", "C# Service", "Validates file type and size. Uploads to Azure Blob Storage. Returns CDN URL. Updates business_photos table.")

        Component(category_repo, "CategoryRepository", "EF Core Repository", "Reads categories and tags. Results cached in Redis (1 hour TTL) since categories change infrequently.")

        Component(geo_json_builder, "GeoJsonBuilder", "C# Utility", "Converts business records to GeoJSON FeatureCollection for map endpoint responses.")
    }

    ContainerDb(postgres, "PostgreSQL", "Primary DB")
    ContainerDb(elasticsearch, "Elasticsearch", "Search Index")
    ContainerDb(redis, "Redis", "Cache")
    Container(blob, "Azure Blob Storage", "Photo storage")

    Rel(businesses_ctrl, business_svc, "Delegates write operations to")
    Rel(businesses_ctrl, search_handler, "Dispatches SearchBusinessesQuery via MediatR")
    Rel(businesses_ctrl, category_repo, "Fetches categories for filter UI")
    Rel(businesses_ctrl, geo_json_builder, "Uses for /map endpoint")

    Rel(business_svc, business_repo, "Reads/writes business records")
    Rel(business_svc, photo_svc, "Delegates photo upload to")
    Rel(business_svc, search_indexer, "Triggers via MediatR domain event")

    Rel(search_handler, elasticsearch, "Executes multi-match + geo_distance query")
    Rel(search_handler, business_repo, "Hydrates full records for result IDs")

    Rel(search_indexer, elasticsearch, "Upserts document in businesses index")

    Rel(business_repo, postgres, "SQL via EF Core + Npgsql/PostGIS")
    Rel(photo_svc, blob, "Uploads via Azure SDK")
    Rel(category_repo, redis, "Checks/sets cache before DB query")
    Rel(category_repo, postgres, "Reads categories table on cache miss")
    Rel(geo_json_builder, business_repo, "Reads lightweight geo records")
```

---

## 2. ASP.NET Core API — Events Module

```mermaid
C4Component
    title Component Diagram — Events Module (ASP.NET Core API)

    Container_Boundary(api, "ASP.NET Core API") {

        Component(events_ctrl, "EventsController", "ASP.NET Controller", "Exposes: GET /events, GET /events/{id}, POST /events, PUT /events/{id}, DELETE /events/{id}, POST /events/{id}/rsvp, GET /events/{id}/calendar, GET /events/calendar/feed")

        Component(event_svc, "EventService", "C# Service", "Handles event creation and updates. Validates business ownership. Enforces capacity limits on RSVPs. Publishes EventCreatedEvent.")

        Component(rsvp_svc, "RsvpService", "C# Service", "Manages RSVP creation, update, and cancellation. Checks capacity. Triggers confirmation email.")

        Component(event_repo, "EventRepository", "EF Core Repository", "CRUD and date-range queries on events. Includes PostGIS location column for proximity filtering.")

        Component(event_indexer, "EventSearchIndexer", "MediatR Event Handler", "Syncs Elasticsearch 'events' index on create/update/delete.")

        Component(ical_builder, "CalendarFeedBuilder", "C# Service", "Generates iCalendar (.ics) output for single events and filtered feeds. Handles RRULE for recurring events.")

        Component(reminder_job, "EventReminderJob", "Hangfire Background Job", "Runs hourly. Finds events starting in the next 24 hours with RSVPs. Sends reminder emails via SendGrid.")
    }

    ContainerDb(postgres, "PostgreSQL", "Primary DB")
    ContainerDb(elasticsearch, "Elasticsearch", "Search Index")
    System_Ext(sendgrid, "SendGrid", "Email delivery")

    Rel(events_ctrl, event_svc, "Delegates create/update/delete")
    Rel(events_ctrl, rsvp_svc, "Delegates RSVP actions")
    Rel(events_ctrl, event_repo, "Queries for list/detail views")
    Rel(events_ctrl, ical_builder, "Generates .ics responses")

    Rel(event_svc, event_repo, "Reads/writes event records")
    Rel(event_svc, event_indexer, "Triggers via MediatR")

    Rel(rsvp_svc, event_repo, "Checks capacity and RSVP state")
    Rel(rsvp_svc, postgres, "Writes rsvps table")
    Rel(rsvp_svc, sendgrid, "Sends RSVP confirmation email")

    Rel(event_indexer, elasticsearch, "Upserts in events index")
    Rel(event_repo, postgres, "SQL via EF Core")
    Rel(reminder_job, event_repo, "Queries for upcoming events")
    Rel(reminder_job, sendgrid, "Dispatches reminder emails")
```

---

## 3. ASP.NET Core API — Analytics & ML Module

```mermaid
C4Component
    title Component Diagram — Analytics & ML Lead Engine

    Container_Boundary(api, "ASP.NET Core API") {

        Component(analytics_mw, "SearchAnalyticsMiddleware", "ASP.NET Middleware", "Intercepts all GET /businesses/search responses. Extracts keyword, filters, result IDs, clicked ID. Fires SearchPerformedEvent via MediatR. Non-blocking — does not add latency to search response.")

        Component(geo_resolver, "GeoIpResolver", "C# Service", "Uses embedded MaxMind GeoLite2 database to resolve request IP address to city and ZIP code. Result cached in Redis by IP hash (1 hour TTL).")

        Component(search_event_consumer, "SearchEventConsumer", "MediatR Handler", "Handles SearchPerformedEvent. Calls GeoIpResolver. Writes a single row to search_events table. Async/fire-and-forget.")

        Component(agg_job, "SearchAggregationJob", "Hangfire Job (Nightly 2AM CT)", "Reads search_events from last 24 hours. Computes per-business per-city search_count, click_count, CTR, trend%. Upserts demand_aggregates table. Purges search_events older than 90 days.")

        Component(trainer, "LeadScoringTrainer", "ML.NET C# Service", "Loads training data from demand_aggregates. Builds ML.NET pipeline with FastTree binary classifier. Trains model. Evaluates AUC. If AUC > 0.72, saves .zip to Azure Blob.")

        Component(retrain_job, "ModelRetrainingJob", "Hangfire Job (Weekly Sunday 2AM CT)", "Orchestrates LeadScoringTrainer. Signals LeadScoringService to reload model via Redis pub/sub after successful save.")

        Component(scoring_svc, "LeadScoringService", "C# Singleton Service", "Holds live PredictionEngine<LeadFeatureRow, LeadPrediction>. Exposes Score() method. Listens for model reload signals. Thread-safe model swap via Interlocked.")

        Component(scoring_job, "LeadScoringJob", "Hangfire Job (Weekly Monday 3AM CT)", "Fetches all active businesses and recent demand_aggregates. Calls LeadScoringService.Score() for each pair. Creates lead_alert records where OpportunityScore > 0.65.")

        Component(dashboard_ctrl, "DashboardController", "ASP.NET Controller", "Exposes: GET /dashboard/overview, GET /dashboard/leads, PATCH /dashboard/leads/{id}, GET /dashboard/analytics/{businessId}")

        Component(feature_builder, "LeadFeatureBuilder", "C# Utility", "Maps DemandAggregate + BusinessContext to LeadFeatureRow. Handles distance calculation via Haversine formula.")
    }

    ContainerDb(postgres, "PostgreSQL", "search_events, demand_aggregates, lead_alerts")
    ContainerDb(redis, "Redis", "IP geo cache, model path pointer, pub/sub")
    Container(blob, "Azure Blob Storage", "ML model .zip files")
    System_Ext(maxmind, "MaxMind GeoLite2", "Embedded IP DB")

    Rel(analytics_mw, geo_resolver, "Resolves IP to city/ZIP")
    Rel(analytics_mw, search_event_consumer, "Fires SearchPerformedEvent (MediatR)")
    Rel(geo_resolver, redis, "Caches resolved geo by IP hash")
    Rel(geo_resolver, maxmind, "Reads local GeoLite2 DB file")
    Rel(search_event_consumer, postgres, "Inserts into search_events")

    Rel(agg_job, postgres, "Reads search_events, upserts demand_aggregates")

    Rel(retrain_job, trainer, "Calls TrainAsync()")
    Rel(trainer, postgres, "Reads demand_aggregates for training data")
    Rel(trainer, blob, "Saves trained model .zip")
    Rel(retrain_job, redis, "Publishes model-reloaded signal")
    Rel(scoring_svc, redis, "Subscribes to model-reloaded channel")
    Rel(scoring_svc, blob, "Loads model .zip on startup and reload")

    Rel(scoring_job, postgres, "Reads active businesses + demand_aggregates")
    Rel(scoring_job, scoring_svc, "Calls Score() for each pair")
    Rel(scoring_job, feature_builder, "Uses to map data to feature vectors")
    Rel(scoring_job, postgres, "Writes lead_alerts")

    Rel(dashboard_ctrl, postgres, "Reads lead_alerts, demand_aggregates")
```

---

## 4. ASP.NET Core API — Auth Module

```mermaid
C4Component
    title Component Diagram — Authentication & Authorization

    Container_Boundary(api, "ASP.NET Core API") {

        Component(auth_ctrl, "AuthController", "ASP.NET Controller", "Endpoints: POST /auth/register, POST /auth/login, POST /auth/refresh, GET /auth/google, GET /auth/google/callback, POST /auth/logout")

        Component(auth_svc, "AuthenticationService", "C# Service", "Validates credentials. Calls JwtTokenService to issue tokens. Manages refresh token lifecycle. Coordinates with UserRepository.")

        Component(jwt_svc, "JwtTokenService", "C# Service", "Issues RS256-signed JWT access tokens (15 min expiry). Issues opaque refresh tokens stored in Redis (7 day expiry). Validates and revokes tokens.")

        Component(google_svc, "GoogleOAuthService", "C# Service", "Handles OAuth 2.0 authorization code flow with Google. Exchanges auth code for user profile. Maps to local User record (create or find existing).")

        Component(user_repo, "UserRepository", "EF Core Repository", "CRUD on users table. Handles password hash storage via BCrypt.")

        Component(auth_middleware, "JwtBearerMiddleware", "ASP.NET Middleware", "Built-in ASP.NET Core JWT validation. Validates token signature, expiry, and issuer. Populates HttpContext.User claims.")

        Component(authz_policies, "Authorization Policies", "ASP.NET Core Policy", "Defines: RequireOwner (role=Owner), RequireAdmin (role=Admin), RequireBusinessOwnership (user owns the requested business). Applied via [Authorize] attributes.")

        Component(current_user, "CurrentUserService", "C# Service", "Reads HttpContext.User claims. Exposes UserId, Role, IsAdmin() — used throughout the application layer to enforce scoping.")
    }

    ContainerDb(postgres, "PostgreSQL", "users table")
    ContainerDb(redis, "Redis", "Refresh token store, revocation list")
    System_Ext(google_oauth, "Google OAuth 2.0", "Identity provider")

    Rel(auth_ctrl, auth_svc, "Delegates login/register logic")
    Rel(auth_ctrl, google_svc, "Delegates Google OAuth flow")

    Rel(auth_svc, user_repo, "Reads/writes user records")
    Rel(auth_svc, jwt_svc, "Issues access + refresh tokens")

    Rel(jwt_svc, redis, "Stores refresh tokens and revocation list")

    Rel(google_svc, google_oauth, "Exchanges auth code, fetches profile")
    Rel(google_svc, user_repo, "Finds or creates local user record")
    Rel(google_svc, jwt_svc, "Issues tokens after successful OAuth")

    Rel(auth_middleware, redis, "Checks token revocation on sensitive endpoints")
    Rel(auth_middleware, authz_policies, "Enforces policy requirements")
    Rel(authz_policies, current_user, "Uses to check ownership")
    Rel(current_user, postgres, "Reads user record on demand (cached)")
```

---

## 5. Vue.js SPA — Component Architecture

```mermaid
C4Component
    title Component Diagram — Vue.js SPA

    Container_Boundary(spa, "Vue.js SPA") {

        Component(router, "Vue Router", "vue-router 4", "Defines all routes. Guards protected routes (Dashboard, Admin) by checking auth.store. Handles lazy-loaded route chunks for performance.")

        Component(auth_store, "auth.store (Pinia)", "Pinia Store", "Holds decoded JWT claims, user role, display name. Manages login/logout actions. Axios interceptor refreshes expired tokens via httpOnly cookie.")

        Component(business_store, "business.store (Pinia)", "Pinia Store", "Caches fetched business lists and detail records. Exposes search, fetchBySlug, createBusiness actions. Notifies on stale cache.")

        Component(events_store, "events.store (Pinia)", "Pinia Store", "Holds event list, RSVP state. Actions: fetchEvents, rsvp, cancelRsvp.")

        Component(leads_store, "leads.store (Pinia)", "Pinia Store", "Holds lead alerts and analytics data for owner dashboard. Actions: fetchLeads, updateLeadStatus, fetchAnalytics.")

        Component(api_client, "api.client (Axios)", "Axios Instance", "Centralized HTTP client. Base URL set from VITE_API_BASE_URL env var. Interceptors: attach Bearer token, handle 401→refresh, handle 429 with retry backoff.")

        Component(directory_view, "DirectoryView", "Vue SFC", "Main business search page. Composes: SearchBar, BusinessSearchFilters, BusinessGrid, BusinessMap. Syncs filter state between list and map.")

        Component(business_map, "BusinessMap", "Vue SFC + Leaflet", "Renders interactive Leaflet map. Loads GeoJSON from /businesses/map. Shows markers with business popups. Emits marker-click to parent.")

        Component(event_calendar, "EventCalendarGrid", "Vue SFC", "Monthly calendar grid. Renders EventDayCell for each day. Highlights days with events. Handles month navigation.")

        Component(lead_dashboard, "LeadInsightsView", "Vue SFC", "Owner-only view. Renders LeadAlertCard list and AnalyticsChart. Calls leads.store for data.")

        Component(lead_alert_card, "LeadAlertCard", "Vue SFC", "Displays opportunity score, demand city, search count, trend. Buttons: View Details, Dismiss. Emits status-change to parent.")

        Component(analytics_chart, "AnalyticsChart", "Vue SFC + Chart.js", "Bar chart of searches by city. Line chart of profile views over time. Data sourced from leads.store.analytics.")
    }

    Container_Ext(api, "ASP.NET Core API", "Backend REST API")

    Rel(router, auth_store, "Checks isAuthenticated for guards")
    Rel(router, directory_view, "Renders on /directory route")
    Rel(router, lead_dashboard, "Renders on /dashboard/leads (Owner only)")
    Rel(router, event_calendar, "Renders on /events route")

    Rel(directory_view, business_store, "Calls search action")
    Rel(directory_view, business_map, "Passes filtered businesses")
    Rel(business_map, business_store, "Reads businesses for map markers")

    Rel(event_calendar, events_store, "Reads events for month")
    Rel(lead_dashboard, leads_store, "Reads lead alerts and analytics")
    Rel(lead_alert_card, leads_store, "Calls updateLeadStatus on dismiss/view")
    Rel(analytics_chart, leads_store, "Reads analytics data")

    Rel(business_store, api_client, "Uses for all API calls")
    Rel(events_store, api_client, "Uses for all API calls")
    Rel(leads_store, api_client, "Uses for all API calls")
    Rel(auth_store, api_client, "Uses for login/register/refresh")

    Rel(api_client, api, "HTTP REST", "HTTPS / JSON")
```

---

## Cross-Component Request Trace: Business Search

This trace shows the full path of a single user search across all component layers.

```
User types "honey" + selects Wagoner filter
        │
        ▼
SearchBar.vue (emits 'search' event with params)
        │
        ▼
DirectoryView.vue (handles event, calls business.store.search())
        │
        ▼
business.store.ts → business.service.ts → api.client.ts
        │
        ▼  GET /api/v1/businesses?q=honey&city=wagoner
ASP.NET Core API
        │
        ├── [SearchAnalyticsMiddleware intercepts — async, non-blocking]
        │       └── Fires SearchPerformedEvent via MediatR
        │               └── SearchEventConsumer → GeoIpResolver → write search_events row
        │
        ├── BusinessesController.Search()
        │       └── Dispatches SearchBusinessesQuery via MediatR
        │               └── SearchBusinessesQueryHandler
        │                       ├── Builds Elasticsearch query (multi-match + city filter)
        │                       ├── ES returns top 20 matching IDs + scores
        │                       └── BusinessRepository hydrates full records from PostgreSQL
        │
        └── Returns 200 OK — paginated JSON
                │
                ▼
business.store.ts ← updates businesses state
        │
        ▼
BusinessGrid.vue re-renders with new results
BusinessMap.vue updates markers
```

---

## Component Interaction: ML Lead Generation (Weekly Cycle)

```
[Sunday 2:00 AM CT — ModelRetrainingJob]
        │
        ▼
LeadScoringTrainer.TrainAsync()
  ├── Reads demand_aggregates (PostgreSQL)
  ├── Maps rows → List<LeadFeatureRow>
  ├── Builds MLContext pipeline (Normalize + FastTree)
  ├── Trains model on 80% split
  ├── Evaluates on 20% split → logs AUC, F1, Precision
  └── If AUC > 0.72:
        ├── Saves model to Azure Blob: /ml-models/lead-scoring-20260218.zip
        └── Publishes "model:reloaded" to Redis pub/sub

[LeadScoringService receives Redis message]
  └── Swaps PredictionEngine<> via Interlocked.Exchange (zero downtime)

[Monday 3:00 AM CT — LeadScoringJob]
  ├── Fetches all active businesses (PostgreSQL)
  ├── Fetches recent demand_aggregates (PostgreSQL)
  └── For each business × demand_city pair:
        ├── LeadFeatureBuilder.Build() → LeadFeatureRow
        ├── LeadScoringService.Score() → LeadPrediction
        └── If OpportunityScore > 0.65:
              └── Upsert lead_alerts (PostgreSQL)
                      └── Owner sees new alert on Dashboard next login
```
