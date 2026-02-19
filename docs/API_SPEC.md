# API Specification — CowetaConnect REST API

> **Base URL:** `https://api.cowetaconnect.com/api/v1`  
> **Auth:** Bearer JWT in Authorization header  
> **Format:** JSON  
> **Version:** 1.0.0

---

## Authentication

### `POST /auth/register`
Register a new user account.

**Body:**
```json
{
  "email": "user@example.com",
  "password": "SecurePass123!",
  "displayName": "Jane Smith",
  "role": "Member"
}
```
**Response:** `201 Created` → `{ "token": "...", "user": { ... } }`

---

### `POST /auth/login`
**Body:** `{ "email": "...", "password": "..." }`  
**Response:** `200 OK` → `{ "accessToken": "...", "expiresIn": 900 }`  
Note: Refresh token set as httpOnly cookie.

---

### `POST /auth/refresh`
Uses httpOnly refresh token cookie automatically.  
**Response:** `200 OK` → `{ "accessToken": "...", "expiresIn": 900 }`

---

### `GET /auth/google`
Initiates Google OAuth flow. Redirects to Google.

---

## Businesses

### `GET /businesses`
Search and list businesses.

**Query Parameters:**

| Param | Type | Description |
|---|---|---|
| `q` | string | Full-text search query |
| `category` | string | Category slug |
| `city` | string | Filter by city |
| `zip` | string | Filter by ZIP code |
| `tags` | string | Comma-separated tag slugs |
| `lat` | float | User latitude (for radius search) |
| `lng` | float | User longitude |
| `radiusMiles` | float | Search radius (default 25, max 100) |
| `verified` | bool | Only verified businesses |
| `page` | int | Page number (default 1) |
| `pageSize` | int | Results per page (default 20, max 50) |
| `sort` | string | `relevance` \| `distance` \| `newest` |

**Response:** `200 OK`
```json
{
  "data": [
    {
      "id": "uuid",
      "name": "Wagoner Honey Co.",
      "slug": "wagoner-honey-co",
      "description": "Local raw honey and bee products...",
      "category": { "id": "uuid", "name": "Food & Beverage", "slug": "food-beverage" },
      "tags": ["honey", "local", "organic"],
      "city": "Wagoner",
      "state": "OK",
      "phone": "918-555-0100",
      "website": "https://wagonerhoney.com",
      "lat": 35.9584,
      "lng": -95.3702,
      "distanceMiles": 12.4,
      "isVerified": true,
      "primaryPhotoUrl": "https://...",
      "businessHours": [...]
    }
  ],
  "pagination": {
    "page": 1,
    "pageSize": 20,
    "totalCount": 47,
    "totalPages": 3
  }
}
```

---

### `GET /businesses/{slug}`
Get full business profile.

**Response:** `200 OK` — Full business object including all photos, hours, upcoming events.

---

### `POST /businesses`
**Auth Required:** Owner or Admin  
Create a new business listing.

**Body:**
```json
{
  "name": "Wagoner Honey Co.",
  "description": "...",
  "categoryId": "uuid",
  "phone": "918-555-0100",
  "email": "contact@wagonerhoney.com",
  "website": "https://wagonerhoney.com",
  "addressLine1": "123 Main St",
  "city": "Wagoner",
  "state": "OK",
  "zip": "74467",
  "tags": ["honey", "beeswax", "local"]
}
```
**Response:** `201 Created` → Created business object

---

### `PUT /businesses/{id}`
**Auth Required:** Owner of this business or Admin  
Update business listing.

---

### `DELETE /businesses/{id}`
**Auth Required:** Admin only  
Soft-delete (sets `is_active = false`).

---

### `POST /businesses/{id}/photos`
**Auth Required:** Business owner  
Upload a photo. Multipart form-data, field: `photo` (image/jpeg, image/png, max 5MB).

**Response:** `201 Created` → `{ "photoId": "uuid", "url": "https://..." }`

---

### `GET /businesses/{id}/events`
Get upcoming events for a specific business.

---

### `GET /businesses/map`
Returns GeoJSON FeatureCollection for map display.

**Query Params:** Same as `/businesses` search.

**Response:** `200 OK`
```json
{
  "type": "FeatureCollection",
  "features": [
    {
      "type": "Feature",
      "geometry": { "type": "Point", "coordinates": [-95.37, 35.96] },
      "properties": {
        "id": "uuid",
        "name": "Wagoner Honey Co.",
        "slug": "wagoner-honey-co",
        "category": "Food & Beverage",
        "photoUrl": "https://..."
      }
    }
  ]
}
```

---

## Events

### `GET /events`
List and search events.

**Query Parameters:**

| Param | Type | Description |
|---|---|---|
| `q` | string | Full-text search |
| `type` | string | Workshop \| Market \| PopUp \| Sale \| Class \| Meetup |
| `city` | string | Filter by city |
| `startAfter` | datetime | Events starting after (ISO 8601) |
| `startBefore` | datetime | Events starting before |
| `isFree` | bool | Free events only |
| `page` | int | |
| `pageSize` | int | |

**Response:** Paginated event list.

---

### `GET /events/{id}`
Get event detail.

---

### `POST /events`
**Auth Required:** Business Owner  
Create an event.

**Body:**
```json
{
  "businessId": "uuid",
  "title": "Coweta Farmers Market Pop-Up",
  "description": "Come find us at the Coweta Farmers Market...",
  "eventType": "PopUp",
  "startAt": "2026-04-12T08:00:00-05:00",
  "endAt": "2026-04-12T13:00:00-05:00",
  "addressLine1": "Coweta Town Square",
  "city": "Coweta",
  "state": "OK",
  "zip": "74429",
  "isFree": true,
  "capacity": null
}
```

---

### `PUT /events/{id}`
**Auth Required:** Event creator or Admin

---

### `DELETE /events/{id}`
**Auth Required:** Event creator or Admin

---

### `POST /events/{id}/rsvp`
**Auth Required:** Any authenticated user

**Body:** `{ "status": "Going" }`  
**Response:** `200 OK` → RSVP object

---

### `GET /events/{id}/calendar`
Download iCal (.ics) file for this event.  
**Content-Type:** `text/calendar`

---

### `GET /events/calendar/feed`
iCal feed for all upcoming public events.  
Optional query params: `city`, `type`

---

## Categories

### `GET /categories`
Get all categories (with counts).

**Response:**
```json
[
  {
    "id": "uuid",
    "name": "Food & Beverage",
    "slug": "food-beverage",
    "icon": "🍽️",
    "businessCount": 24,
    "children": [
      { "id": "uuid", "name": "Honey & Bee Products", "slug": "honey-bee", "businessCount": 3 }
    ]
  }
]
```

---

## Owner Dashboard

### `GET /dashboard/overview`
**Auth Required:** Business Owner  
Summary stats for owner's business(es).

**Response:**
```json
{
  "businesses": [
    {
      "businessId": "uuid",
      "name": "Wagoner Honey Co.",
      "searchImpressions30d": 142,
      "profileViews30d": 89,
      "eventCount": 3,
      "activeLeadAlerts": 2
    }
  ]
}
```

---

### `GET /dashboard/leads`
**Auth Required:** Business Owner  
Get AI lead alerts for owner's businesses.

**Response:**
```json
{
  "leads": [
    {
      "id": "uuid",
      "businessId": "uuid",
      "businessName": "Wagoner Honey Co.",
      "demandCity": "Broken Arrow",
      "opportunityScore": 0.87,
      "confidence": 0.79,
      "searchCount": 47,
      "alertMessage": "47 people in Broken Arrow searched for honey and bee products in the last 30 days.",
      "trendPct": 34.2,
      "status": "New",
      "generatedAt": "2026-02-18T03:00:00Z"
    }
  ]
}
```

---

### `PATCH /dashboard/leads/{id}`
**Auth Required:** Business Owner  
Update lead status.

**Body:** `{ "status": "Viewed" }` or `{ "status": "Dismissed" }`

---

### `GET /dashboard/analytics/{businessId}`
**Auth Required:** Business Owner (owns this business)  
Search demand analytics for a business.

**Query Params:** `period=30d|90d|12m`

**Response:**
```json
{
  "searchesByCity": [
    { "city": "Broken Arrow", "count": 47, "trendPct": 34.2 },
    { "city": "Tulsa", "count": 31, "trendPct": -5.1 },
    { "city": "Coweta", "count": 28, "trendPct": 12.0 }
  ],
  "topKeywords": ["honey", "raw honey", "local honey", "beeswax"],
  "viewsTimeSeries": [
    { "date": "2026-01-19", "views": 12 },
    ...
  ]
}
```

---

## Admin Endpoints

All require `Admin` role.

### `GET /admin/businesses?status=pending`
List businesses pending verification.

### `POST /admin/businesses/{id}/verify`
Mark business as verified.

### `DELETE /admin/businesses/{id}`
Hard delete a business.

### `GET /admin/analytics/platform`
Platform-wide analytics summary.

---

## Error Responses (RFC 7807)

```json
{
  "type": "https://cowetaconnect.com/errors/validation",
  "title": "Validation Failed",
  "status": 422,
  "detail": "One or more fields are invalid.",
  "errors": {
    "email": ["Email address is already in use."],
    "name": ["Business name is required."]
  },
  "traceId": "00-abc123..."
}
```

| Status | Meaning |
|---|---|
| 400 | Bad Request — malformed JSON |
| 401 | Unauthorized — missing or invalid JWT |
| 403 | Forbidden — insufficient role |
| 404 | Not Found |
| 422 | Unprocessable — validation errors |
| 429 | Too Many Requests — rate limit hit |
| 500 | Internal Server Error |
