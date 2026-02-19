# C4 Level 1 — System Context: CowetaConnect

> **C4 Model Level:** 1 — System Context  
> **Purpose:** Shows CowetaConnect as a black box and how it relates to its users and external systems.  
> **Audience:** All stakeholders — technical and non-technical.

---

## Diagram

```mermaid
C4Context
    title System Context — CowetaConnect

    Person(community_member, "Community Member", "A resident of Coweta, Wagoner County, or surrounding areas looking to discover local small businesses and events.")
    Person(business_owner, "Business Owner", "A small business operator who lists their business, creates events, and reviews AI-generated market lead insights.")
    Person(platform_admin, "Platform Admin", "CowetaConnect staff who moderate listings, manage categories, and monitor platform health.")

    System(coweta_connect, "CowetaConnect", "Community web platform for discovering local small businesses, attending events, and surfacing AI-powered geographic demand signals.")

    System_Ext(google_oauth, "Google OAuth 2.0", "Provides social sign-in for users who prefer to authenticate with their Google account.")
    System_Ext(mapbox, "MapBox / OpenStreetMap", "Provides interactive map tiles and geocoding for location-based business discovery.")
    System_Ext(sendgrid, "SendGrid", "Transactional email delivery for event reminders, account verification, and owner alerts.")
    System_Ext(maxmind, "MaxMind GeoLite2", "Embedded IP geolocation database. Resolves anonymous user IP addresses to city/ZIP for analytics. No data leaves the server.")
    System_Ext(azure_blob, "Azure Blob Storage", "Stores business photos and ML model artifacts.")
    System_Ext(azure_insights, "Azure Application Insights", "Application performance monitoring, structured logs, and alerting.")

    Rel(community_member, coweta_connect, "Searches for businesses, browses events, views maps", "HTTPS / Browser")
    Rel(business_owner, coweta_connect, "Manages listings, creates events, reviews lead insights", "HTTPS / Browser")
    Rel(platform_admin, coweta_connect, "Moderates content, verifies listings, monitors platform", "HTTPS / Browser")

    Rel(coweta_connect, google_oauth, "Delegates authentication", "OAuth 2.0 / HTTPS")
    Rel(coweta_connect, mapbox, "Fetches map tiles and geocoding", "HTTPS / REST")
    Rel(coweta_connect, sendgrid, "Sends transactional emails", "HTTPS / REST")
    Rel(coweta_connect, maxmind, "Resolves IP to city/ZIP (embedded, offline)", "Local file read")
    Rel(coweta_connect, azure_blob, "Stores and retrieves photos and ML models", "Azure SDK / HTTPS")
    Rel(coweta_connect, azure_insights, "Streams logs and telemetry", "Azure SDK")
```

---

## Narrative

### What is CowetaConnect?

CowetaConnect is a **community web platform** serving Coweta, OK and the wider Wagoner County region. Its core purpose is threefold:

1. **Business Discovery** — Residents can find local small businesses by category, keyword, location, or map.
2. **Event Calendar** — Business owners can publish workshops, pop-ups, markets, and community events that residents can browse and RSVP to.
3. **AI Market Intelligence** — The platform passively observes search patterns and uses ML to surface geographic demand signals to business owners — identifying cities with high search interest where a business has low visibility.

### Who uses it?

| Actor | Primary Goal | Account Required? |
|---|---|---|
| Community Member | Find local businesses, discover events | No (read-only browsing is anonymous) |
| Business Owner | Grow their business, manage presence, understand demand | Yes (Owner role) |
| Platform Admin | Keep content accurate and platform healthy | Yes (Admin role) |

### External Dependencies

| System | Role | Data Shared |
|---|---|---|
| Google OAuth 2.0 | Optional social sign-in | Email + display name (one-time, on registration) |
| MapBox / OpenStreetMap | Map rendering, geocoding | Business coordinates (public) |
| SendGrid | Email delivery | Recipient email, event name, date |
| MaxMind GeoLite2 | IP → City/ZIP resolution | **Embedded locally** — no data leaves the server |
| Azure Blob Storage | Photo and ML model file storage | Business photos, model binary files |
| Azure Application Insights | APM and structured logging | Anonymized performance telemetry |

### Privacy Commitment

The platform is designed with privacy as a default:
- Anonymous browsing requires no account and sets no tracking cookies.
- IP addresses are resolved to city/ZIP locally using an embedded database and are **never stored or transmitted**.
- The analytics pipeline captures only aggregate geographic data — no user-level behavioral profiles are built.

---

## Scope Boundaries

| In Scope | Out of Scope |
|---|---|
| Business directory for Coweta / Wagoner County region | National or multi-state business directory |
| Community events (workshops, markets, pop-ups) | Ticket payment processing (link to external ticketing only) |
| AI demand signal alerts for business owners | Full CRM or marketing automation |
| Owner-managed listings | User-submitted reviews or ratings (Phase 4 consideration) |
| Geographic demand insights | Real-time inventory or e-commerce |
