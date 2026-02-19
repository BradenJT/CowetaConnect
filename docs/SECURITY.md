# Security Architecture — CowetaConnect

> **Document Type:** Security Design  
> **Version:** 1.0.0

---

## 1. Authentication & Authorization

### Authentication Mechanism

- **JWT Bearer Tokens** for API authentication
  - Access token lifetime: **15 minutes**
  - Refresh token lifetime: **7 days** (stored as httpOnly, Secure, SameSite=Strict cookie)
  - Signing algorithm: **RS256** (asymmetric) — private key never leaves the server
  - Token claims: `sub` (user ID), `email`, `role`, `iat`, `exp`, `jti`

- **Google OAuth 2.0** as alternative identity provider
  - Social sign-in only; profile data mapped to local user record
  - Google tokens are never stored; exchanged for internal JWT immediately

### Authorization Model (RBAC)

| Role | Capabilities |
|---|---|
| **Anonymous** | Browse directory, search, view events, view public business profiles |
| **Member** | Above + RSVP to events, save favorites |
| **Owner** | Above + Create/edit own business listings, create events, view lead insights, view own analytics |
| **Admin** | Full access: verify businesses, moderate content, platform analytics, manage categories |

**Policy enforcement:** ASP.NET Core Authorization policies applied at controller/action level. Claims checked server-side on every request.

---

## 2. Data Privacy

### User Data Minimization

- Anonymous browsing is fully supported — no account required to search
- Analytics capture **zero PII** — only aggregate geography (city, ZIP) from IP geolocation
- Raw IP addresses are resolved and discarded; never written to any database
- No tracking cookies; no fingerprinting; no cross-site data sharing

### GDPR / CCPA Considerations

Although operating in Oklahoma (not EU), we adopt privacy-respecting defaults:

- Users can **request data export** of their account and RSVP history
- Users can **delete their account** and associated data (hard delete)
- Business owners can **delete their listings** at any time
- `search_events` rows are automatically purged after **90 days**
- Privacy policy must be displayed before account creation

---

## 3. Transport Security

- **TLS 1.2+** enforced; TLS 1.0/1.1 disabled
- **HSTS** header enforced with 1-year max-age and includeSubDomains
- **HTTPS-only** — all HTTP redirected to HTTPS at load balancer level
- Certificates managed via Azure App Service Managed Certificates (auto-renewal)

---

## 4. API Security

### Rate Limiting

Implemented via ASP.NET Core Rate Limiting middleware:

| Endpoint Group | Limit |
|---|---|
| Unauthenticated search | 60 req/min per IP |
| Authenticated requests | 300 req/min per user |
| Auth endpoints (login/register) | 10 req/min per IP |
| File upload | 5 req/min per user |
| Admin endpoints | 200 req/min per user |

### Input Validation

- All API input validated using **FluentValidation** before reaching business logic
- File uploads: MIME type verified (not just extension), max size 5MB, scanned via Azure Defender for Storage
- SQL injection: Prevented by EF Core parameterized queries — raw SQL never used
- XSS: `description` and `bio` fields sanitized with **HtmlSanitizer** library on write; content-security-policy header on all responses

### CORS

```csharp
builder.Services.AddCors(options =>
{
    options.AddPolicy("VueApp", policy =>
    {
        policy.WithOrigins("https://cowetaconnect.com", "https://www.cowetaconnect.com")
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials(); // Required for refresh token cookie
    });
});
```

---

## 5. Infrastructure Security

### Azure Configuration

- **App Service** — VNET Integration enabled; not publicly exposed directly
- **Azure Front Door / CDN** — WAF (Web Application Firewall) in front of API and Vue SPA
- **PostgreSQL Flexible Server** — Only accessible from App Service VNET; no public endpoint
- **Redis** — Only accessible from VNET; Auth string required
- **Blob Storage** — Private by default; public read only on photo blob container via CDN URL signing
- **Key Vault** — All secrets (connection strings, JWT signing keys, API keys) stored in Azure Key Vault; accessed via Managed Identity (no secrets in app config or environment variables)

### Secrets Management

```
// Correct: Use Managed Identity + Key Vault
builder.Configuration.AddAzureKeyVault(
    new Uri("https://cowetaconnect-kv.vault.azure.net/"),
    new DefaultAzureCredential());

// NEVER: Hardcoded secrets, connection strings in appsettings.json committed to git
```

### Logging & Audit

- All authentication events logged (login, logout, failed attempts, token refresh)
- All admin actions logged with actor ID and timestamp
- Business create/update/delete logged for audit trail
- Logs sent to **Azure Application Insights** and retained for 90 days
- Failed login attempts trigger alert after 5 consecutive failures per IP (auto-block for 15 min)

---

## 6. Business Logic Security

### Ownership Enforcement

Every owner-scoped action verifies ownership server-side:

```csharp
// BusinessService.cs
public async Task UpdateBusinessAsync(Guid businessId, UpdateBusinessDto dto, Guid requestingUserId)
{
    var business = await _repo.GetByIdAsync(businessId);
    if (business is null) throw new NotFoundException();
    
    // Explicit ownership check — never trust client-supplied data alone
    if (business.OwnerId != requestingUserId && !_currentUser.IsAdmin())
        throw new ForbiddenException("You do not own this business listing.");

    // ... proceed with update
}
```

### Lead Data Access

Lead alerts are scoped to the authenticated business owner. The API never returns cross-owner data.

---

## 7. Dependency & Supply Chain Security

- **Dependabot** enabled on GitHub repository for both NuGet and npm dependency updates
- `.github/workflows/security-scan.yml` runs **OWASP Dependency Check** on every PR
- Docker base images pinned to specific SHA digests (if containerized)
- NuGet packages signed where available

---

## 8. Incident Response Plan (Summary)

| Severity | Example | Response Time | Action |
|---|---|---|---|
| Critical | Data breach, auth bypass | 1 hour | Disable affected service, notify users, patch |
| High | XSS vulnerability, mass scraping | 4 hours | Deploy fix, review logs |
| Medium | Rate limit bypass, data leakage | 24 hours | Patch in next release |
| Low | Minor info disclosure | 1 week | Standard release cycle |

Security issues should be reported to: `security@cowetaconnect.com`
