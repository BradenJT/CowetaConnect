# Google OAuth 2.0 Sign-In — Design

> **Date:** 2026-02-21
> **Branch:** Implement-Google-OAuth-2.0-sign-in
> **Status:** Approved — ready for implementation

---

## Goal

Add Google OAuth 2.0 as an alternative authentication method. A successful Google sign-in maps the Google identity to a local user record and issues the platform's own JWT/refresh token pair. Google tokens are never stored.

---

## Approach

Server-side OAuth flow using ASP.NET Core's built-in Google middleware (`Microsoft.AspNetCore.Authentication.Google`). The middleware handles state generation, CSRF validation, code exchange, and claim extraction automatically. The Application layer adds one new MediatR command; the controller stays thin.

---

## End-to-End Flow

```
[Vue] "Sign in with Google" button → full-page navigate to GET /api/v1/auth/google

[AuthController] Challenge("Google", RedirectUri="/api/v1/auth/google/finish")
  → middleware redirects browser to Google's OAuth consent screen

[Google] User approves → Google redirects to /api/v1/auth/google/callback

[Middleware] Intercepts /api/v1/auth/google/callback
  → validates state parameter (CSRF protection)
  → exchanges authorization code for tokens with Google
  → extracts claims (sub, email, name, picture)
  → signs into "GoogleOAuth" cookie scheme (temp cookie)
  → redirects browser to /api/v1/auth/google/finish

[AuthController] GET /api/v1/auth/google/finish
  → HttpContext.AuthenticateAsync("GoogleOAuth")
  → extract sub, email, name, picture
  → delete "GoogleOAuth" temp cookie
  → send GoogleSignInCommand via MediatR
  → SetRefreshCookie(rawRefresh)
  → Redirect("https://cowetaconnect.com/auth/callback#token=JWT")

[Vue] /auth/callback page
  → read window.location.hash → extract token
  → history.replaceState() to clear hash from browser history
  → auth.store.setToken(jwt)
  → navigate to home
```

---

## Backend Changes

### `Program.cs`

Fix the current double `AddAuthentication()` call. Chain Google and the temp cookie scheme from the existing JWT registration:

```csharp
builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options => { /* existing — unchanged */ })
    .AddCookie("GoogleOAuth")           // temp cookie: lives only during the OAuth round-trip
    .AddGoogle(options => {
        options.SignInScheme  = "GoogleOAuth";
        options.ClientId      = config["Google:ClientId"];
        options.ClientSecret  = config["Google:ClientSecret"];
        options.CallbackPath  = "/api/v1/auth/google/callback"; // middleware-only, no controller action
        options.SaveTokens    = false;  // Google tokens are never persisted
    });
```

### `IAuthUserService` — 3 new methods

```csharp
Task<AuthUserResult?> FindByGoogleSubjectAsync(string googleSub, CancellationToken ct = default);
Task<AuthUserResult?> FindByEmailAsync(string email, CancellationToken ct = default);
Task<AuthUserResult> UpsertGoogleUserAsync(
    string googleSub, string email, string displayName, string? avatarUrl,
    CancellationToken ct = default);
```

### `AuthUserService` — `UpsertGoogleUserAsync` logic

1. `FindByGoogleSubject(sub)` → found → update `LastLogin`, return user
2. `FindByEmail(email)` → found, `GoogleSubject` is null → set `GoogleSubject = sub`, update `LastLogin`, return user (account linking — no confirmation step for MVP; spec defers confirmation to future)
3. Neither match → create `ApplicationUser` with `Role = Member`, `IsEmailVerified = true`, `PasswordHash = null`

### New MediatR command

`GoogleSignInCommand(string GoogleSub, string Email, string DisplayName, string? AvatarUrl)`
→ returns `(TokenResponse token, string rawRefresh)`

Handler calls `UpsertGoogleUserAsync`, then `IJwtTokenService.GenerateAccessToken`, then `IRefreshTokenRepository.CreateAsync` — same pattern as `LoginCommand`.

### `AuthController` — 2 new endpoints

```csharp
// GET /api/v1/auth/google — initiates OAuth redirect
[HttpGet("google")]
public IActionResult GoogleLogin() =>
    Challenge(
        new AuthenticationProperties { RedirectUri = "/api/v1/auth/google/finish" },
        GoogleDefaults.AuthenticationScheme);

// GET /api/v1/auth/google/finish — runs after middleware processes the callback
[HttpGet("google/finish")]
public async Task<IActionResult> GoogleFinish(CancellationToken ct)
{
    var result = await HttpContext.AuthenticateAsync("GoogleOAuth");
    if (!result.Succeeded) return BadRequest("Google authentication failed.");

    var sub     = result.Principal!.FindFirstValue(ClaimTypes.NameIdentifier)!;
    var email   = result.Principal!.FindFirstValue(ClaimTypes.Email)!;
    var name    = result.Principal!.FindFirstValue(ClaimTypes.Name) ?? email;
    var picture = result.Principal!.FindFirstValue("picture");

    // Delete the temp GoogleOAuth cookie — we don't need it anymore
    await HttpContext.SignOutAsync("GoogleOAuth");

    var (token, rawRefresh) = await mediator.Send(new GoogleSignInCommand(sub, email, name, picture), ct);
    SetRefreshCookie(rawRefresh);

    var spaOrigin = /* from config */ "https://cowetaconnect.com";
    return Redirect($"{spaOrigin}/auth/callback#token={token.AccessToken}");
}
```

### Database / migrations

No migration needed. `GoogleSubject` column and index already exist in `ApplicationUserConfiguration` and `InitialCreate` migration.

---

## Frontend Changes

The Vue SPA is currently a scaffold. Three additions:

1. **`/auth/callback` route + page component** — reads `window.location.hash`, extracts `token`, calls `auth.store.setToken(jwt)`, clears hash with `history.replaceState(null, '', window.location.pathname)`, navigates to home
2. **Login page** — "Sign in with Google" button using the Google-branded style; triggers a full-page navigation (`window.location.href = '/api/v1/auth/google'`) — not an AJAX call
3. **`auth.store.ts`** — `setToken(jwt: string)` action; Google sessions are indistinguishable from email/password sessions

---

## Security

| Concern | Mitigation |
|---|---|
| CSRF / state parameter | ASP.NET Core OAuth middleware generates and validates `state` automatically |
| Google tokens never stored | `SaveTokens = false` |
| Token in browser history | Vue clears hash immediately with `history.replaceState` |
| `google_subject` index | Already present in `ApplicationUserConfiguration` |
| Secrets | `Google:ClientId` + `Google:ClientSecret` via Azure Key Vault (prod); `appsettings.Development.json` (dev, git-ignored) |
| Rate limiting | Existing `auth-endpoints` policy (10 req/min per IP) applies to `/google` and `/google/finish` |
| PKCE | Not required for server-side flow; middleware handles code exchange server-to-server |

---

## Tests

| Test class | Cases |
|---|---|
| `GoogleSignInCommandHandlerTests` | Existing `google_subject` user → login; existing email user (no subject) → account linking; new user → auto-create with `Role=Member`, `IsEmailVerified=true` |
| `UpsertGoogleUserTests` | Same three branches, unit-testing `AuthUserService` directly against mocked `UserManager` |

---

## Out of Scope (future)

- Email confirmation step before account linking (spec defers this)
- Avatar sync from Google `picture` claim on subsequent logins
- "Disconnect Google" account management UI
