# Data Model — CowetaConnect

> **Document Type:** Data Architecture  
> **Version:** 1.0.0

---

## Entity Relationship Overview (Mermaid)

```mermaid
erDiagram
    USER {
        uuid id PK
        string email UK
        string display_name
        string role "Member|Owner|Admin"
        timestamp created_at
        timestamp last_login
    }

    BUSINESS {
        uuid id PK
        uuid owner_id FK
        string name
        string slug UK
        string description
        string category_id FK
        string phone
        string email
        string website
        string address_line1
        string city
        string state
        string zip
        float lat
        float lng
        string service_area_geojson
        bool is_active
        bool is_verified
        timestamp created_at
        timestamp updated_at
    }

    CATEGORY {
        uuid id PK
        string name
        string slug UK
        string icon
        uuid parent_id FK
    }

    BUSINESS_TAG {
        uuid business_id FK
        uuid tag_id FK
    }

    TAG {
        uuid id PK
        string name
        string slug
    }

    BUSINESS_HOUR {
        uuid id PK
        uuid business_id FK
        int day_of_week "0=Sun"
        time open_time
        time close_time
        bool is_closed
    }

    BUSINESS_PHOTO {
        uuid id PK
        uuid business_id FK
        string blob_url
        string caption
        bool is_primary
        int display_order
    }

    EVENT {
        uuid id PK
        uuid business_id FK
        uuid created_by FK
        string title
        string description
        string event_type "Workshop|Market|PopUp|Sale|Class|Meetup|Other"
        timestamp start_at
        timestamp end_at
        bool is_recurring
        string recurrence_rule
        string address_line1
        string city
        float lat
        float lng
        int capacity
        bool is_free
        decimal ticket_price
        string ticket_url
        bool is_published
        timestamp created_at
    }

    RSVP {
        uuid id PK
        uuid event_id FK
        uuid user_id FK
        string status "Going|Maybe|Cancelled"
        timestamp created_at
    }

    SEARCH_EVENT {
        uuid id PK
        string keyword
        string[] filters_applied
        string user_city
        string user_zip
        uuid[] result_business_ids
        uuid clicked_business_id
        timestamp occurred_at
    }

    DEMAND_AGGREGATE {
        uuid id PK
        uuid business_id FK
        string demand_city
        string demand_zip
        int search_count
        int click_count
        float trend_pct
        date period_start
        date period_end
        timestamp computed_at
    }

    LEAD_ALERT {
        uuid id PK
        uuid business_id FK
        string demand_city
        float opportunity_score
        float confidence
        int search_count
        string status "New|Viewed|Dismissed"
        timestamp generated_at
        timestamp viewed_at
    }

    USER ||--o{ BUSINESS : "owns"
    BUSINESS }o--|| CATEGORY : "belongs to"
    BUSINESS ||--o{ BUSINESS_TAG : "has"
    TAG ||--o{ BUSINESS_TAG : "tagged in"
    BUSINESS ||--o{ BUSINESS_HOUR : "has hours"
    BUSINESS ||--o{ BUSINESS_PHOTO : "has photos"
    BUSINESS ||--o{ EVENT : "hosts"
    EVENT ||--o{ RSVP : "has"
    USER ||--o{ RSVP : "makes"
    BUSINESS ||--o{ DEMAND_AGGREGATE : "receives"
    BUSINESS ||--o{ LEAD_ALERT : "receives"
```

---

## Table Definitions

### `users`

| Column | Type | Notes |
|---|---|---|
| id | UUID | PK, gen_random_uuid() |
| email | VARCHAR(320) | Unique, normalized |
| password_hash | VARCHAR | Nullable (OAuth users) |
| display_name | VARCHAR(100) | |
| avatar_url | VARCHAR | |
| role | VARCHAR(20) | Member / Owner / Admin |
| is_email_verified | BOOLEAN | Default false |
| google_subject | VARCHAR | For OAuth, nullable |
| created_at | TIMESTAMPTZ | |
| last_login | TIMESTAMPTZ | |

### `businesses`

| Column | Type | Notes |
|---|---|---|
| id | UUID | PK |
| owner_id | UUID | FK → users.id |
| name | VARCHAR(200) | |
| slug | VARCHAR(220) | Unique, URL-safe |
| description | TEXT | |
| category_id | UUID | FK → categories.id |
| phone | VARCHAR(20) | |
| email | VARCHAR(320) | |
| website | VARCHAR | |
| address_line1 | VARCHAR(200) | |
| city | VARCHAR(100) | |
| state | CHAR(2) | Default 'OK' |
| zip | VARCHAR(10) | |
| lat | DOUBLE PRECISION | |
| lng | DOUBLE PRECISION | |
| location | GEOGRAPHY(POINT) | PostGIS, computed from lat/lng |
| service_area_geojson | JSONB | Optional service radius polygon |
| is_active | BOOLEAN | Owner can deactivate |
| is_verified | BOOLEAN | Admin-verified listing |
| featured_rank | INTEGER | For premium placement, null = not featured |
| created_at | TIMESTAMPTZ | |
| updated_at | TIMESTAMPTZ | |

**Indexes:**
- `idx_businesses_location` — GIST index on `location` for radius searches
- `idx_businesses_category` — B-tree on `category_id`
- `idx_businesses_city` — B-tree on `city`
- `idx_businesses_slug` — Unique B-tree on `slug`

### `events`

| Column | Type | Notes |
|---|---|---|
| id | UUID | PK |
| business_id | UUID | FK → businesses.id |
| created_by | UUID | FK → users.id |
| title | VARCHAR(300) | |
| description | TEXT | |
| event_type | VARCHAR(30) | Enum-like |
| start_at | TIMESTAMPTZ | |
| end_at | TIMESTAMPTZ | |
| is_recurring | BOOLEAN | |
| recurrence_rule | VARCHAR | iCal RRULE format |
| address_line1 | VARCHAR(200) | May differ from business address |
| city | VARCHAR(100) | |
| lat | DOUBLE PRECISION | |
| lng | DOUBLE PRECISION | |
| location | GEOGRAPHY(POINT) | PostGIS |
| capacity | INTEGER | Null = unlimited |
| is_free | BOOLEAN | |
| ticket_price | NUMERIC(8,2) | |
| ticket_url | VARCHAR | External ticketing link |
| image_url | VARCHAR | Event banner image |
| is_published | BOOLEAN | Draft / Published |
| created_at | TIMESTAMPTZ | |

**Indexes:**
- `idx_events_start_at` — B-tree on `start_at` (calendar queries)
- `idx_events_business_id` — B-tree on `business_id`
- `idx_events_city_start` — Composite on `city, start_at`

### `search_events` (Analytics)

> **Privacy Note:** No user IDs or personal info stored. Only aggregate geography (city, ZIP) derived from IP geolocation.

| Column | Type | Notes |
|---|---|---|
| id | UUID | PK |
| session_hash | VARCHAR(64) | SHA-256 of session token, no reversibility needed |
| keyword | VARCHAR(500) | |
| category_filter | UUID | FK → categories.id, nullable |
| city_filter | VARCHAR(100) | Filter user applied |
| user_city | VARCHAR(100) | Geolocated origin city |
| user_zip | VARCHAR(10) | Geolocated origin ZIP |
| result_count | INTEGER | |
| result_business_ids | UUID[] | Array of returned business IDs |
| clicked_business_id | UUID | Nullable, which result was clicked |
| occurred_at | TIMESTAMPTZ | |

**Indexes:**
- `idx_search_events_keyword` — GIN on `to_tsvector(keyword)` (for keyword aggregation)
- `idx_search_events_occurred_at` — BRIN on `occurred_at` (time-range scans)
- `idx_search_events_user_city` — B-tree on `user_city`

### `demand_aggregates`

Pre-computed by nightly aggregation job. Powers the ML model and dashboards.

| Column | Type | Notes |
|---|---|---|
| id | UUID | PK |
| business_id | UUID | FK → businesses.id |
| demand_city | VARCHAR(100) | Where searches originate |
| demand_zip | VARCHAR(10) | |
| search_count | INTEGER | Count of search events in period |
| click_count | INTEGER | Count of actual clicks |
| click_through_rate | NUMERIC(5,4) | click_count / search_count |
| trend_pct | NUMERIC(8,2) | % change vs prior period |
| period_start | DATE | |
| period_end | DATE | |
| computed_at | TIMESTAMPTZ | |

### `lead_alerts`

| Column | Type | Notes |
|---|---|---|
| id | UUID | PK |
| business_id | UUID | FK → businesses.id |
| demand_city | VARCHAR(100) | |
| opportunity_score | NUMERIC(4,3) | 0.000 – 1.000 |
| confidence | NUMERIC(4,3) | Model confidence |
| search_count | INTEGER | Supporting evidence count |
| alert_message | TEXT | Human-readable description |
| status | VARCHAR(20) | New / Viewed / Dismissed |
| generated_at | TIMESTAMPTZ | |
| viewed_at | TIMESTAMPTZ | Nullable |

---

## Elasticsearch Index Schemas

### `businesses` index

```json
{
  "mappings": {
    "properties": {
      "id": { "type": "keyword" },
      "name": { 
        "type": "text",
        "analyzer": "english",
        "fields": { "keyword": { "type": "keyword" } }
      },
      "description": { "type": "text", "analyzer": "english" },
      "category_name": { "type": "keyword" },
      "tags": { "type": "keyword" },
      "city": { "type": "keyword" },
      "zip": { "type": "keyword" },
      "location": { "type": "geo_point" },
      "is_active": { "type": "boolean" },
      "is_verified": { "type": "boolean" },
      "featured_rank": { "type": "integer" }
    }
  }
}
```

### `events` index

```json
{
  "mappings": {
    "properties": {
      "id": { "type": "keyword" },
      "title": { "type": "text", "analyzer": "english" },
      "description": { "type": "text", "analyzer": "english" },
      "event_type": { "type": "keyword" },
      "start_at": { "type": "date" },
      "city": { "type": "keyword" },
      "location": { "type": "geo_point" },
      "is_free": { "type": "boolean" },
      "business_name": { "type": "keyword" }
    }
  }
}
```

---

## Data Retention Policy

| Table | Retention | Reason |
|---|---|---|
| search_events | 90 days raw | Privacy; aggregated before expiry |
| demand_aggregates | 2 years | Trend analysis |
| lead_alerts | 1 year | Business history |
| rsvps | Indefinite | Attendance records |
| business/event data | Indefinite | Core application data |
