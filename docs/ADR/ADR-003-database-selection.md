# ADR-003: Database Selection

**Date:** 2026-02-18  
**Status:** Accepted  
**Deciders:** Architecture Team

## Context

We need a primary relational database for business, user, event, and analytics data. Geographic queries (radius search, proximity sorting) are a core product requirement.

## Decision

**PostgreSQL 16 with PostGIS extension** as primary database.  
**Elasticsearch** for full-text search.  
**Redis** for caching and session data.

## Rationale

### PostgreSQL
- Best-in-class geospatial support via PostGIS (`GEOGRAPHY` type, `ST_DWithin`, `ST_Distance`)
- Open source with no licensing cost
- Excellent EF Core support via Npgsql (including PostGIS geometry types)
- Managed by Azure (PostgreSQL Flexible Server) — no ops burden
- JSONB for flexible data (service area polygons, recurrence rules)
- Mature, battle-tested, widely understood

### Elasticsearch
- Full-text search is a core requirement for the business directory
- Proximity search (`geo_distance` filter) complements PostGIS for search relevance ranking
- PostgreSQL full-text search (`tsvector`) would work for MVP but Elasticsearch provides better relevance scoring, faceting, and autocomplete

### Redis
- Session token caching
- Search result caching (reduce Elasticsearch load)
- Rate limiting counter storage
- Managed by Azure Cache for Redis

## Alternatives Considered

| Alternative | Reason Rejected |
|---|---|
| SQL Server | Higher licensing cost; PostgreSQL is functionally equivalent for this use case |
| MySQL | Weaker geospatial support; less expressive JSON handling |
| MongoDB | Relational integrity needed for business/owner/event relationships |
| Algolia | Paid search SaaS; Elasticsearch gives equivalent capability with more control |
| SQLite | Not appropriate for multi-user production workload |

## Consequences

- EF Core migrations used for schema management; PostGIS `GEOGRAPHY` columns require Npgsql.EntityFrameworkCore.PostgreSQL.NetTopologySuite
- Elasticsearch sync requires a domain event handler to keep indexes fresh when businesses are created/updated/deleted
- Two data stores to manage, but Redis and Elasticsearch are well-supported managed Azure services
