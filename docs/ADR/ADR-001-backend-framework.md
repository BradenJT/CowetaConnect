# ADR-001: Backend Framework Selection

**Date:** 2026-02-18  
**Status:** Accepted  
**Deciders:** Architecture Team

## Context

We need to select the backend framework for the CowetaConnect API. The primary developer skill set is C#/.NET. We need to support REST APIs, background jobs, ML integration, and PostgreSQL.

## Decision

**ASP.NET Core 8 Web API**

## Rationale

- Native C# ecosystem — no cross-language friction for the ML.NET integration
- Excellent performance (top-tier in TechEmpower benchmarks)
- First-class support for PostgreSQL via EF Core + Npgsql
- Built-in dependency injection, middleware, rate limiting, authorization
- Strong long-term Microsoft support guarantee
- Team already has C# experience

## Alternatives Considered

| Alternative | Reason Rejected |
|---|---|
| Node.js / Express | C# ML.NET integration would require cross-process calls or switching to Python |
| Python / FastAPI | Team skill gap; complicates the stack |
| Java / Spring Boot | No team experience; heavier JVM startup |

## Consequences

- ML model training and inference stays in-process (no separate ML service needed in Phase 1-3)
- Hangfire available for background jobs (native .NET)
- Excellent Azure App Service integration
