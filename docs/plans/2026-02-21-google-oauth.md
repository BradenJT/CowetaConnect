# Google OAuth 2.0 Sign-In Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Add Google OAuth 2.0 as an alternative sign-in path that issues the platform's own JWT/refresh token pair — Google tokens are never stored.

**Architecture:** Server-side OAuth flow via ASP.NET Core's built-in Google middleware. A temp httpOnly cookie carries OAuth state across the redirect dance; the callback populates a `ClaimsPrincipal`; `GoogleSignInCommand` (new MediatR command) upserts the user and issues the standard token pair. Vue receives the JWT via URL fragment at `/auth/callback#token=...`, stores it in a Pinia auth store, and clears the hash.

**Tech Stack:** .NET 10, `Microsoft.AspNetCore.Authentication.Google` 10.0.3, MediatR 14, MSTest + Moq, Vue 3, vue-router 4, pinia 2

---

## Codebase Context (read before starting)

- `Program.cs` — already has `AddGoogle()` but as a second `AddAuthentication()` call (wrong — will be fixed in Task 1)
- `AuthController.cs` — thin primary-constructor controller; all new endpoints follow the same pattern
- `IAuthUserService` — Application layer interface; add one new method (`UpsertGoogleUserAsync`)
- `AuthUserService.cs` — Infrastructure implementation; `UserManager<ApplicationUser>` is injected
- `ApplicationUser.cs` — already has `GoogleSubject` nullable property
- `ApplicationUserConfiguration.cs` — already indexes `GoogleSubject`; **no migration needed**
- `appsettings.Development.json` — already has `App:SpaOrigin: "http://localhost:5173"`
- `LoginCommandHandler.cs` — reference implementation for the token-issuance pattern
- `LoginCommandHandlerTests.cs` — reference for MSTest + Moq test style
- Vue SPA at `src/CowetaConnect.UI/` — scaffold only (no router, no store); needs vue-router + pinia

---

## Task 1: Add NuGet package + fix Program.cs auth registration

**Files:**
- Modify: `src/CowetaConnect.API/CowetaConnect.API.csproj`
- Modify: `src/CowetaConnect.API/Program.cs`

### Step 1: Add the Google package to the API project

In `CowetaConnect.API.csproj`, add inside the existing `<ItemGroup>` with packages:

```xml
<PackageReference Include="Microsoft.AspNetCore.Authentication.Google" Version="10.0.3" />
```

### Step 2: Fix the authentication registration in Program.cs

Find the two separate auth blocks (lines ~112–144). Replace both with a single chained registration:

```csharp
// ── Authentication ────────────────────────────────────────────────────────
builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer           = true,
            ValidIssuer              = builder.Configuration["Jwt:Issuer"],
            ValidateAudience         = true,
            ValidAudience            = builder.Configuration["Jwt:Audience"],
            ValidateLifetime         = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey         = signingKey,
            ClockSkew                = TimeSpan.Zero
        };
    })
    .AddCookie("GoogleOAuth", options =>
    {
        // Temporary cookie that carries OAuth state across the Google redirect.
        // SameSite must be Lax (not Strict) — the return redirect from Google is cross-site.
        options.Cookie.Name         = "google_oauth_state";
        options.Cookie.HttpOnly     = true;
        options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
        options.Cookie.SameSite     = SameSiteMode.Lax;
    })
    .AddGoogle(options =>
    {
        options.SignInScheme  = "GoogleOAuth";   // use the temp cookie, not a persistent session
        options.ClientId      = builder.Configuration["Google:ClientId"] ?? string.Empty;
        options.ClientSecret  = builder.Configuration["Google:ClientSecret"] ?? string.Empty;
        options.CallbackPath  = "/api/v1/auth/google/callback"; // middleware-handled; no controller action here
        options.SaveTokens    = false;           // Google tokens are never stored
    });
```

Delete the old second `builder.Services.AddAuthentication()` block entirely.

### Step 3: Verify it builds

```bash
dotnet build src/CowetaConnect.API/CowetaConnect.API.csproj
```

Expected: Build succeeded, 0 errors.

### Step 4: Commit

```bash
git add src/CowetaConnect.API/CowetaConnect.API.csproj src/CowetaConnect.API/Program.cs
git commit -m "feat: wire Google OAuth middleware into authentication chain"
```

---

## Task 2: Add Google credentials to appsettings.Development.json

**Files:**
- Modify: `src/CowetaConnect.API/appsettings.Development.json`

This file is git-ignored for secrets. Add placeholder values — replace with real credentials from [Google Cloud Console](https://console.cloud.google.com/) before running locally.

### Step 1: Add the Google section

Add to `appsettings.Development.json`:

```json
{
  "Google": {
    "ClientId": "REPLACE_WITH_GOOGLE_CLIENT_ID",
    "ClientSecret": "REPLACE_WITH_GOOGLE_CLIENT_SECRET"
  }
}
```

The full file after edit should look like:

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Debug",
      "Microsoft.AspNetCore": "Information",
      "Microsoft.EntityFrameworkCore.Database.Command": "Information"
    }
  },
  "App": {
    "SpaOrigin": "http://localhost:5173",
    "BaseUrl": "https://localhost:5001"
  },
  "Jwt": {
    "Issuer": "https://localhost:5001",
    "Audience": "http://localhost:5173"
  },
  "Google": {
    "ClientId": "REPLACE_WITH_GOOGLE_CLIENT_ID",
    "ClientSecret": "REPLACE_WITH_GOOGLE_CLIENT_SECRET"
  },
  "Elasticsearch": {
    "Url": "http://localhost:9200"
  },
  "ConnectionStrings": {
    "Default": "Host=localhost;Port=5432;Database=cowetaconnect;Username=postgres;Password=#SidesWoods$2",
    "Redis": "REPLACE_WITH_REDIS_CONNECTION_STRING",
    "Hangfire": "REPLACE_WITH_HANGFIRE_CONNECTION_STRING"
  }
}
```

> **Note:** `appsettings.Development.json` is git-ignored. Do not commit real credentials. Production values go in Azure Key Vault as `Google--ClientId` and `Google--ClientSecret`.

### Step 2: Commit (placeholder only — no real secrets)

```bash
git add src/CowetaConnect.API/appsettings.Development.json
git commit -m "chore: add Google OAuth credential placeholders to dev appsettings"
```

---

## Task 3: Extend IAuthUserService with UpsertGoogleUserAsync

**Files:**
- Modify: `src/CowetaConnect.Application/Auth/Interfaces/IAuthUserService.cs`

### Step 1: Add the new method signature

Add to the `IAuthUserService` interface (after `UpdateLastLoginAsync`):

```csharp
/// <summary>
/// Finds or creates a user from a Google OAuth sign-in.
/// Priority: (1) match by google_subject, (2) match by email and link, (3) create new.
/// New users get Role=Member and IsEmailVerified=true.
/// </summary>
Task<AuthUserResult> UpsertGoogleUserAsync(
    string googleSub,
    string email,
    string displayName,
    string? avatarUrl,
    CancellationToken ct = default);
```

### Step 2: Build to verify the interface change compiles

```bash
dotnet build src/CowetaConnect.Application/CowetaConnect.Application.csproj
```

Expected: Build fails in Infrastructure (AuthUserService doesn't implement the new method yet) — that's correct.

### Step 3: Commit

```bash
git add src/CowetaConnect.Application/Auth/Interfaces/IAuthUserService.cs
git commit -m "feat: add UpsertGoogleUserAsync to IAuthUserService interface"
```

---

## Task 4: Write failing tests for GoogleSignInCommandHandler

**Files:**
- Create: `src/CowetaConnect.Tests/Auth/GoogleSignInCommandHandlerTests.cs`

These tests use mocked `IAuthUserService` — they do **not** depend on the `UpsertGoogleUserAsync` implementation.

### Step 1: Create the test file

```csharp
// src/CowetaConnect.Tests/Auth/GoogleSignInCommandHandlerTests.cs
using CowetaConnect.Application.Auth.Commands;
using CowetaConnect.Application.Auth.Interfaces;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace CowetaConnect.Tests.Auth;

[TestClass]
public class GoogleSignInCommandHandlerTests
{
    private Mock<IAuthUserService> _authService = null!;
    private Mock<IJwtTokenService> _tokenService = null!;
    private Mock<IRefreshTokenRepository> _refreshRepo = null!;
    private GoogleSignInCommandHandler _sut = null!;

    [TestInitialize]
    public void Setup()
    {
        _authService  = new Mock<IAuthUserService>();
        _tokenService = new Mock<IJwtTokenService>();
        _refreshRepo  = new Mock<IRefreshTokenRepository>();

        _sut = new GoogleSignInCommandHandler(
            _authService.Object,
            _tokenService.Object,
            _refreshRepo.Object,
            NullLogger<GoogleSignInCommandHandler>.Instance);
    }

    [TestMethod]
    public async Task Handle_NewUser_ReturnsTokenPair()
    {
        _authService
            .Setup(s => s.UpsertGoogleUserAsync("sub-new", "new@test.com", "New User", null, default))
            .ReturnsAsync(new AuthUserResult(true, "user-new", "new@test.com", "Member"));
        _authService
            .Setup(s => s.UpdateLastLoginAsync("user-new", default))
            .Returns(Task.CompletedTask);
        _tokenService
            .Setup(s => s.GenerateAccessToken("user-new", "new@test.com", "Member"))
            .Returns("jwt-new");
        _tokenService.Setup(s => s.GenerateRefreshToken()).Returns("raw-refresh");
        _tokenService.Setup(s => s.HashToken("raw-refresh")).Returns("hashed");
        _refreshRepo
            .Setup(r => r.StoreAsync("user-new", "hashed", It.IsAny<DateTimeOffset>(), default))
            .Returns(Task.CompletedTask);

        var (token, rawRefresh) = await _sut.Handle(
            new GoogleSignInCommand("sub-new", "new@test.com", "New User", null), default);

        Assert.AreEqual("jwt-new", token.AccessToken);
        Assert.AreEqual("raw-refresh", rawRefresh);
    }

    [TestMethod]
    public async Task Handle_ExistingGoogleUser_ReturnsTokensWithExistingRole()
    {
        _authService
            .Setup(s => s.UpsertGoogleUserAsync("sub-owner", "owner@test.com", "Owner User", null, default))
            .ReturnsAsync(new AuthUserResult(true, "user-owner", "owner@test.com", "Owner"));
        _authService
            .Setup(s => s.UpdateLastLoginAsync("user-owner", default))
            .Returns(Task.CompletedTask);
        _tokenService
            .Setup(s => s.GenerateAccessToken("user-owner", "owner@test.com", "Owner"))
            .Returns("jwt-owner");
        _tokenService.Setup(s => s.GenerateRefreshToken()).Returns("raw-2");
        _tokenService.Setup(s => s.HashToken("raw-2")).Returns("hashed-2");
        _refreshRepo
            .Setup(r => r.StoreAsync("user-owner", "hashed-2", It.IsAny<DateTimeOffset>(), default))
            .Returns(Task.CompletedTask);

        var (token, _) = await _sut.Handle(
            new GoogleSignInCommand("sub-owner", "owner@test.com", "Owner User", null), default);

        Assert.AreEqual("jwt-owner", token.AccessToken);
    }

    [TestMethod]
    public async Task Handle_UpsertFails_ThrowsInvalidOperationException()
    {
        _authService
            .Setup(s => s.UpsertGoogleUserAsync(
                It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<string?>(), default))
            .ReturnsAsync(new AuthUserResult(false, null, null, null, "DB write failed"));

        await Assert.ThrowsExceptionAsync<InvalidOperationException>(() =>
            _sut.Handle(
                new GoogleSignInCommand("sub-x", "fail@test.com", "Fail User", null), default));
    }
}
```

### Step 2: Run the tests — expect FAIL (command doesn't exist yet)

```bash
dotnet test src/CowetaConnect.Tests/CowetaConnect.Tests.csproj --filter "FullyQualifiedName~GoogleSignInCommandHandlerTests" -v normal
```

Expected: Build error — `GoogleSignInCommand` and `GoogleSignInCommandHandler` not found.

### Step 3: Commit the tests

```bash
git add src/CowetaConnect.Tests/Auth/GoogleSignInCommandHandlerTests.cs
git commit -m "test: add failing tests for GoogleSignInCommandHandler"
```

---

## Task 5: Implement GoogleSignInCommand + handler

**Files:**
- Create: `src/CowetaConnect.Application/Auth/Commands/GoogleSignInCommand.cs`

### Step 1: Create the command and handler

```csharp
// src/CowetaConnect.Application/Auth/Commands/GoogleSignInCommand.cs
using CowetaConnect.Application.Auth.Interfaces;
using CowetaConnect.Application.Auth.Models;
using MediatR;
using Microsoft.Extensions.Logging;

namespace CowetaConnect.Application.Auth.Commands;

// ── Command ──────────────────────────────────────────────────────────────────

public record GoogleSignInCommand(
    string GoogleSub,
    string Email,
    string DisplayName,
    string? AvatarUrl)
    : IRequest<(TokenResponse Token, string RefreshToken)>;

// ── Handler ──────────────────────────────────────────────────────────────────

public sealed class GoogleSignInCommandHandler(
    IAuthUserService authService,
    IJwtTokenService tokenService,
    IRefreshTokenRepository refreshTokenRepo,
    ILogger<GoogleSignInCommandHandler> logger)
    : IRequestHandler<GoogleSignInCommand, (TokenResponse Token, string RefreshToken)>
{
    public async Task<(TokenResponse Token, string RefreshToken)> Handle(
        GoogleSignInCommand request, CancellationToken cancellationToken)
    {
        var result = await authService.UpsertGoogleUserAsync(
            request.GoogleSub, request.Email, request.DisplayName, request.AvatarUrl,
            cancellationToken);

        if (!result.Success)
        {
            logger.LogError("Google OAuth upsert failed for {Email}: {Error}",
                request.Email, result.Error);
            throw new InvalidOperationException("Google sign-in failed. Please try again.");
        }

        await authService.UpdateLastLoginAsync(result.UserId!, cancellationToken);

        var accessToken = tokenService.GenerateAccessToken(
            result.UserId!, result.Email!, result.Role!);
        var rawRefresh = tokenService.GenerateRefreshToken();
        var hash       = tokenService.HashToken(rawRefresh);

        await refreshTokenRepo.StoreAsync(
            result.UserId!,
            hash,
            DateTimeOffset.UtcNow.AddDays(7),
            cancellationToken);

        return (new TokenResponse(accessToken), rawRefresh);
    }
}
```

### Step 2: Run the tests — expect PASS

```bash
dotnet test src/CowetaConnect.Tests/CowetaConnect.Tests.csproj --filter "FullyQualifiedName~GoogleSignInCommandHandlerTests" -v normal
```

Expected: 3 tests pass.

### Step 3: Commit

```bash
git add src/CowetaConnect.Application/Auth/Commands/GoogleSignInCommand.cs
git commit -m "feat: implement GoogleSignInCommand handler with upsert + token issuance"
```

---

## Task 6: Implement UpsertGoogleUserAsync in AuthUserService

**Files:**
- Modify: `src/CowetaConnect.Infrastructure/Services/AuthUserService.cs`

### Step 1: Add the using directive

At the top of `AuthUserService.cs`, verify this using is present (it likely is):

```csharp
using Microsoft.EntityFrameworkCore;
```

If not, add it.

### Step 2: Add the method

Add after `UpdateLastLoginAsync`:

```csharp
public async Task<AuthUserResult> UpsertGoogleUserAsync(
    string googleSub, string email, string displayName, string? avatarUrl,
    CancellationToken ct = default)
{
    // 1. Match by google_subject — existing Google user.
    var bySubject = await userManager.Users
        .FirstOrDefaultAsync(u => u.GoogleSubject == googleSub, ct);

    if (bySubject is not null)
        return new AuthUserResult(true, bySubject.Id.ToString(), bySubject.Email, bySubject.Role);

    // 2. Match by email — existing password account; link it.
    var byEmail = await userManager.FindByEmailAsync(email);

    if (byEmail is not null)
    {
        byEmail.GoogleSubject = googleSub;
        if (avatarUrl is not null) byEmail.AvatarUrl = avatarUrl;

        var linkResult = await userManager.UpdateAsync(byEmail);
        if (!linkResult.Succeeded)
        {
            var error = string.Join("; ", linkResult.Errors.Select(e => e.Description));
            logger.LogError("Failed to link Google account for {Email}: {Error}", email, error);
            return new AuthUserResult(false, null, null, null, error);
        }

        logger.LogInformation("Linked Google account to existing user {Email}", email);
        return new AuthUserResult(true, byEmail.Id.ToString(), byEmail.Email, byEmail.Role);
    }

    // 3. No match — create a new Google-only user.
    var newUser = new ApplicationUser
    {
        Id              = Guid.NewGuid(),
        UserName        = email,
        Email           = email,
        DisplayName     = displayName,
        AvatarUrl       = avatarUrl,
        Role            = "Member",
        IsEmailVerified = true,   // Google has already verified the email
        GoogleSubject   = googleSub,
        CreatedAt       = DateTimeOffset.UtcNow
    };

    var createResult = await userManager.CreateAsync(newUser);  // no password
    if (!createResult.Succeeded)
    {
        var error = string.Join("; ", createResult.Errors.Select(e => e.Description));
        logger.LogWarning("Google user creation failed for {Email}: {Error}", email, error);
        return new AuthUserResult(false, null, null, null, error);
    }

    logger.LogInformation("Created new user via Google OAuth for {Email}", email);
    return new AuthUserResult(true, newUser.Id.ToString(), newUser.Email, newUser.Role);
}
```

### Step 3: Build to verify no compile errors

```bash
dotnet build src/CowetaConnect.Infrastructure/CowetaConnect.Infrastructure.csproj
```

Expected: Build succeeded, 0 errors.

### Step 4: Run all tests to confirm nothing regressed

```bash
dotnet test src/CowetaConnect.Tests/CowetaConnect.Tests.csproj -v normal
```

Expected: All tests pass.

### Step 5: Commit

```bash
git add src/CowetaConnect.Infrastructure/Services/AuthUserService.cs
git commit -m "feat: implement UpsertGoogleUserAsync with sub/email/create logic"
```

---

## Task 7: Add GoogleLogin + GoogleFinish endpoints to AuthController

**Files:**
- Modify: `src/CowetaConnect.API/Controllers/v1/AuthController.cs`

### Step 1: Add required usings

Add to the top of `AuthController.cs`:

```csharp
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.Extensions.Configuration;
using System.Security.Claims;
```

### Step 2: Add IConfiguration to the constructor

Change the constructor from:

```csharp
public class AuthController(IMediator mediator) : ControllerBase
```

To:

```csharp
public class AuthController(IMediator mediator, IConfiguration config) : ControllerBase
```

### Step 3: Add the two new endpoints

Add after the `Logout` method, before `SetRefreshCookie`:

```csharp
// GET /api/v1/auth/google — initiates Google OAuth redirect
[HttpGet("google")]
[ProducesResponseType(StatusCodes.Status302Found)]
public IActionResult GoogleLogin() =>
    Challenge(
        new AuthenticationProperties { RedirectUri = "/api/v1/auth/google/finish" },
        GoogleDefaults.AuthenticationScheme);

// GET /api/v1/auth/google/finish — runs after middleware processes the Google callback
[HttpGet("google/finish")]
[ProducesResponseType(StatusCodes.Status302Found)]
[ProducesResponseType(StatusCodes.Status400BadRequest)]
public async Task<IActionResult> GoogleFinish(CancellationToken ct)
{
    var result = await HttpContext.AuthenticateAsync("GoogleOAuth");
    if (!result.Succeeded)
        return BadRequest("Google authentication failed.");

    // Extract claims populated by the Google middleware.
    var sub = result.Principal!.FindFirstValue(ClaimTypes.NameIdentifier)
              ?? throw new InvalidOperationException("Missing sub claim from Google.");
    var email = result.Principal!.FindFirstValue(ClaimTypes.Email)
                ?? throw new InvalidOperationException("Missing email claim from Google.");
    var name    = result.Principal!.FindFirstValue(ClaimTypes.Name) ?? email;
    // picture claim — try both known claim types from different middleware versions
    var picture = result.Principal!.FindFirstValue("urn:google:picture")
               ?? result.Principal!.FindFirstValue("picture");

    // Delete the temporary OAuth cookie — no longer needed.
    await HttpContext.SignOutAsync("GoogleOAuth");

    var (token, rawRefresh) = await mediator.Send(
        new GoogleSignInCommand(sub, email, name, picture), ct);

    SetRefreshCookie(rawRefresh);

    var spaOrigin = config["App:SpaOrigin"] ?? "https://cowetaconnect.com";
    return Redirect($"{spaOrigin}/auth/callback#token={token.AccessToken}");
}
```

### Step 4: Build to verify

```bash
dotnet build src/CowetaConnect.API/CowetaConnect.API.csproj
```

Expected: Build succeeded, 0 errors.

### Step 5: Run all tests

```bash
dotnet test src/CowetaConnect.Tests/CowetaConnect.Tests.csproj -v normal
```

Expected: All tests pass.

### Step 6: Commit

```bash
git add src/CowetaConnect.API/Controllers/v1/AuthController.cs
git commit -m "feat: add GoogleLogin and GoogleFinish endpoints to AuthController"
```

---

## Task 8: Frontend — install vue-router and pinia

**Files:**
- Modify: `src/CowetaConnect.UI/package.json` (via npm)

### Step 1: Install packages

```bash
cd src/CowetaConnect.UI
npm install vue-router@4 pinia
```

### Step 2: Verify packages appear in package.json

Check that `"vue-router"` and `"pinia"` appear under `"dependencies"` in `package.json`.

### Step 3: Commit

```bash
git add src/CowetaConnect.UI/package.json src/CowetaConnect.UI/package-lock.json
git commit -m "chore: install vue-router and pinia for auth flow"
```

---

## Task 9: Frontend — create auth store and router

**Files:**
- Create: `src/CowetaConnect.UI/src/stores/auth.ts`
- Create: `src/CowetaConnect.UI/src/router/index.ts`
- Modify: `src/CowetaConnect.UI/src/main.ts`

### Step 1: Create the auth store

```typescript
// src/CowetaConnect.UI/src/stores/auth.ts
import { defineStore } from 'pinia'
import { ref, computed } from 'vue'

export const useAuthStore = defineStore('auth', () => {
  // Access token stored in memory only — never in localStorage or sessionStorage.
  const token = ref<string | null>(null)

  const isAuthenticated = computed(() => token.value !== null)

  function setToken(jwt: string) {
    token.value = jwt
  }

  function logout() {
    token.value = null
  }

  return { token, isAuthenticated, setToken, logout }
})
```

### Step 2: Create the router

```typescript
// src/CowetaConnect.UI/src/router/index.ts
import { createRouter, createWebHistory } from 'vue-router'

const router = createRouter({
  history: createWebHistory(import.meta.env.BASE_URL),
  routes: [
    {
      path: '/',
      name: 'home',
      component: () => import('../views/HomeView.vue'),
    },
    {
      path: '/login',
      name: 'login',
      component: () => import('../views/LoginView.vue'),
    },
    {
      // OAuth callback — reads JWT from URL fragment and stores it
      path: '/auth/callback',
      name: 'auth-callback',
      component: () => import('../views/AuthCallback.vue'),
    },
  ],
})

export default router
```

### Step 3: Update main.ts to mount router and pinia

Replace the content of `src/main.ts`:

```typescript
import { createApp } from 'vue'
import { createPinia } from 'pinia'
import './style.css'
import App from './App.vue'
import router from './router'

const app = createApp(App)
app.use(createPinia())
app.use(router)
app.mount('#app')
```

### Step 4: Verify TypeScript compiles

```bash
npm run type-check
```

Expected: No errors. (HomeView.vue doesn't exist yet but the import is lazy — it won't error until runtime.)

### Step 5: Commit

```bash
git add src/CowetaConnect.UI/src/stores/auth.ts \
        src/CowetaConnect.UI/src/router/index.ts \
        src/CowetaConnect.UI/src/main.ts
git commit -m "feat: add pinia auth store and vue-router with auth/callback route"
```

---

## Task 10: Frontend — create AuthCallback.vue and LoginView.vue

**Files:**
- Create: `src/CowetaConnect.UI/src/views/AuthCallback.vue`
- Create: `src/CowetaConnect.UI/src/views/LoginView.vue`
- Create: `src/CowetaConnect.UI/src/views/HomeView.vue`
- Modify: `src/CowetaConnect.UI/src/App.vue`

### Step 1: Create AuthCallback.vue

This component reads the JWT from `window.location.hash`, stores it, and navigates home.

```vue
<!-- src/CowetaConnect.UI/src/views/AuthCallback.vue -->
<script setup lang="ts">
import { onMounted } from 'vue'
import { useRouter } from 'vue-router'
import { useAuthStore } from '../stores/auth'

const router = useRouter()
const auth   = useAuthStore()

onMounted(() => {
  const hash   = window.location.hash          // e.g. "#token=eyJ..."
  const params = new URLSearchParams(hash.substring(1))
  const token  = params.get('token')

  // Clear the token from the URL immediately — don't leave it in browser history.
  history.replaceState(null, '', window.location.pathname)

  if (token) {
    auth.setToken(token)
    router.replace({ name: 'home' })
  } else {
    // No token — something went wrong with the OAuth flow.
    router.replace({ name: 'login' })
  }
})
</script>

<template>
  <div>Signing you in…</div>
</template>
```

### Step 2: Create LoginView.vue

The API base URL is read from the env variable `VITE_API_BASE_URL`, which defaults to `/` for dev proxy. The Google button navigates the full page — it must **not** be an AJAX fetch.

```vue
<!-- src/CowetaConnect.UI/src/views/LoginView.vue -->
<script setup lang="ts">
const apiBase = import.meta.env.VITE_API_BASE_URL ?? ''

function signInWithGoogle() {
  // Full-page navigation — the OAuth flow requires a real redirect, not fetch().
  window.location.href = `${apiBase}/api/v1/auth/google`
}
</script>

<template>
  <div class="login-page">
    <h1>Sign in to CowetaConnect</h1>

    <button class="google-btn" @click="signInWithGoogle">
      <img
        src="https://www.gstatic.com/firebasejs/ui/2.0.0/images/auth/google.svg"
        alt=""
        width="18"
        height="18"
      />
      Sign in with Google
    </button>
  </div>
</template>

<style scoped>
.login-page {
  display: flex;
  flex-direction: column;
  align-items: center;
  padding: 4rem 1rem;
  gap: 1.5rem;
}

.google-btn {
  display: flex;
  align-items: center;
  gap: 0.75rem;
  padding: 0.625rem 1.25rem;
  border: 1px solid #dadce0;
  border-radius: 4px;
  background: #fff;
  color: #3c4043;
  font-size: 0.9375rem;
  font-weight: 500;
  cursor: pointer;
  transition: background 0.15s;
}

.google-btn:hover {
  background: #f8f8f8;
}
</style>
```

### Step 3: Create a minimal HomeView.vue (router requires it)

```vue
<!-- src/CowetaConnect.UI/src/views/HomeView.vue -->
<script setup lang="ts">
import { useAuthStore } from '../stores/auth'
const auth = useAuthStore()
</script>

<template>
  <div>
    <h1>CowetaConnect</h1>
    <p v-if="auth.isAuthenticated">You are signed in.</p>
    <RouterLink v-else to="/login">Sign in</RouterLink>
  </div>
</template>
```

### Step 4: Update App.vue to use RouterView

Replace the content of `src/App.vue`:

```vue
<script setup lang="ts">
// App shell — routes handle all content via <RouterView>
</script>

<template>
  <RouterView />
</template>
```

### Step 5: Verify TypeScript compiles and dev server starts

```bash
npm run type-check
npm run dev
```

Expected: No TypeScript errors. Dev server starts at http://localhost:5173. Navigating to `/login` shows the Google button.

### Step 6: Commit

```bash
git add src/CowetaConnect.UI/src/views/AuthCallback.vue \
        src/CowetaConnect.UI/src/views/LoginView.vue \
        src/CowetaConnect.UI/src/views/HomeView.vue \
        src/CowetaConnect.UI/src/App.vue
git commit -m "feat: add AuthCallback, LoginView, HomeView and wire RouterView in App"
```

---

## Task 11: End-to-end smoke test checklist

Before opening a PR, verify manually:

1. Start the API: `dotnet run --project src/CowetaConnect.API`
2. Start the Vue dev server: `cd src/CowetaConnect.UI && npm run dev`
3. Navigate to `http://localhost:5173/login`
4. Click "Sign in with Google" — browser navigates to Google's consent screen
5. Approve → browser redirects to `http://localhost:5173/auth/callback#token=...`
6. App redirects to `/` and shows "You are signed in."
7. Token hash is **not** visible in the URL bar after redirect
8. Opening DevTools → Application → Cookies: only `refresh_token` cookie is present (no Google tokens)
9. Run the full test suite one final time:

```bash
dotnet test src/CowetaConnect.Tests/CowetaConnect.Tests.csproj -v normal
```

Expected: All tests pass.

### Final commit (if any cleanup)

```bash
git add -p   # stage only intentional changes
git commit -m "chore: post-smoke-test cleanup"
```
