# JWT Authentication (RS256 + ASP.NET Core Identity) Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Implement full authentication — ASP.NET Core Identity for user management, RS256 JWT access tokens (15 min), and httpOnly refresh tokens (7 days) — exposing /auth/register, /auth/login, /auth/refresh, and /auth/logout.

**Architecture:** Replace the hand-rolled `User` entity with `ApplicationUser : IdentityUser<Guid>` in the Infrastructure project. Application layer defines three auth interfaces (`IAuthUserService`, `IJwtTokenService`, `IRefreshTokenRepository`); Infrastructure implements them. MediatR commands in Application fan out to those interfaces. AuthController is thin — just deserialises DTOs, calls Mediator, sets the refresh cookie.

**Tech Stack:** .NET 10, ASP.NET Core Identity (AddIdentityCore), EF Core 10 + Npgsql, MediatR 14, FluentValidation 12, StackExchange.Redis, MSTest + Moq for tests

---

## Codebase Context (read before starting)

- `Program.cs` — `RsaSecurityKey` is already created from Key Vault / ephemeral key and registered as a **singleton** in DI. JwtTokenService injects it directly.
- `appsettings.json` — `Jwt:Issuer`, `Jwt:Audience`, `Jwt:AccessTokenLifetimeMinutes: 15`, `Jwt:RefreshTokenLifetimeDays: 7`
- `ServiceCollectionExtensions.cs` (Infrastructure) — call `AddIdentityCore` here
- `ExceptionHandlingMiddleware.cs` — add case for `TooManyAttemptsException`
- `Business.cs` has `public User Owner { get; set; } = null!;` — must be removed (Domain cannot reference Infrastructure's `ApplicationUser`)
- `BusinessConfiguration.cs` uses `.HasOne(b => b.Owner).WithMany(u => u.Businesses)` — update to shadow FK
- Existing `users` table was created by `InitialCreate` migration — **squash** (delete old migration files + drop/recreate dev DB)
- Tests project (`CowetaConnect.Tests`) has no project references yet — add them in Task 1

---

## Task 1: Add NuGet packages and project references

**Files:**
- Modify: `src/CowetaConnect.Infrastructure/CowetaConnect.Infrastructure.csproj`
- Modify: `src/CowetaConnect.Tests/CowetaConnect.Tests.csproj`

**Step 1: Add Identity EF Core package to Infrastructure**

Edit `CowetaConnect.Infrastructure.csproj` — add inside the existing `<ItemGroup>` with packages:

```xml
<PackageReference Include="Microsoft.AspNetCore.Identity.EntityFrameworkCore" Version="10.0.3" />
```

**Step 2: Add test packages and project references to Tests.csproj**

Replace the entire `CowetaConnect.Tests.csproj` with:

```xml
<Project Sdk="MSTest.Sdk/4.0.1">

  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <LangVersion>latest</LangVersion>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <UseVSTest>true</UseVSTest>
  </PropertyGroup>

  <ItemGroup>
    <ProjectReference Include="..\CowetaConnect.Application\CowetaConnect.Application.csproj" />
    <ProjectReference Include="..\CowetaConnect.Infrastructure\CowetaConnect.Infrastructure.csproj" />
    <ProjectReference Include="..\CowetaConnect.API\CowetaConnect.API.csproj" />
  </ItemGroup>

  <ItemGroup>
    <PackageReference Include="Moq" Version="4.20.72" />
    <PackageReference Include="Microsoft.EntityFrameworkCore.InMemory" Version="10.0.3" />
    <PackageReference Include="Microsoft.AspNetCore.Identity.EntityFrameworkCore" Version="10.0.3" />
  </ItemGroup>

</Project>
```

**Step 3: Verify restore**

```bash
dotnet restore src/CowetaConnect.sln
```

Expected: no errors

**Step 4: Commit**

```bash
git add src/CowetaConnect.Infrastructure/CowetaConnect.Infrastructure.csproj src/CowetaConnect.Tests/CowetaConnect.Tests.csproj
git commit -m "chore: add Identity EF Core package + test dependencies"
```

---

## Task 2: Create ApplicationUser (Infrastructure)

**Files:**
- Create: `src/CowetaConnect.Infrastructure/Identity/ApplicationUser.cs`

**Step 1: Create the file**

```csharp
// src/CowetaConnect.Infrastructure/Identity/ApplicationUser.cs
using Microsoft.AspNetCore.Identity;

namespace CowetaConnect.Infrastructure.Identity;

/// <summary>
/// Extends IdentityUser&lt;Guid&gt; with domain-specific properties.
/// UserName is always set equal to Email — we don't use a separate display name for login.
/// </summary>
public class ApplicationUser : IdentityUser<Guid>
{
    public string DisplayName { get; set; } = string.Empty;
    public string? AvatarUrl { get; set; }

    /// <summary>Simple string role: Member | Owner | Admin.</summary>
    public string Role { get; set; } = "Member";

    /// <summary>Tracked separately from Identity's EmailConfirmed for domain clarity.</summary>
    public bool IsEmailVerified { get; set; }

    /// <summary>Google OAuth subject claim — null for password-only accounts.</summary>
    public string? GoogleSubject { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? LastLogin { get; set; }
}
```

**Step 2: Build — expect compile errors (DbContext still uses old User)**

```bash
dotnet build src/CowetaConnect.Infrastructure/CowetaConnect.Infrastructure.csproj
```

Errors are expected; proceed.

---

## Task 3: Create RefreshToken entity (Infrastructure)

**Files:**
- Create: `src/CowetaConnect.Infrastructure/Identity/RefreshToken.cs`

**Step 1: Create the file**

```csharp
// src/CowetaConnect.Infrastructure/Identity/RefreshToken.cs
namespace CowetaConnect.Infrastructure.Identity;

/// <summary>
/// Stores the SHA-256 hash of a refresh token — never the raw token.
/// Supports rotation: revoked_at is set when a token is consumed, a new one is issued.
/// </summary>
public class RefreshToken
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }

    /// <summary>SHA-256 hash (Base64) of the 64-byte random token.</summary>
    public string TokenHash { get; set; } = string.Empty;

    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset? RevokedAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }

    public bool IsValid => RevokedAt is null && ExpiresAt > DateTimeOffset.UtcNow;
}
```

---

## Task 4: Create TooManyAttemptsException (Domain)

**Files:**
- Create: `src/CowetaConnect.Domain/Exceptions/TooManyAttemptsException.cs`

**Step 1: Create the file**

```csharp
// src/CowetaConnect.Domain/Exceptions/TooManyAttemptsException.cs
namespace CowetaConnect.Domain.Exceptions;

public sealed class TooManyAttemptsException : Exception
{
    public int RetryAfterSeconds { get; }

    public TooManyAttemptsException(int retryAfterSeconds = 900)
        : base($"Too many failed attempts. Retry after {retryAfterSeconds} seconds.")
    {
        RetryAfterSeconds = retryAfterSeconds;
    }
}
```

---

## Task 5: Update ExceptionHandlingMiddleware for 429

**Files:**
- Modify: `src/CowetaConnect.API/Middleware/ExceptionHandlingMiddleware.cs`

**Step 1: Read the current file** (already done — it has NotFoundException, ForbiddenException, ValidationException cases)

**Step 2: Add TooManyAttemptsException handling**

In `InvokeAsync`, before calling `HandleExceptionAsync`, add the Retry-After header:

```csharp
using CowetaConnect.Domain.Exceptions;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;

namespace CowetaConnect.API.Middleware;

public sealed class ExceptionHandlingMiddleware(
    RequestDelegate next,
    ILogger<ExceptionHandlingMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unhandled exception on {Method} {Path}",
                context.Request.Method, context.Request.Path);

            await HandleExceptionAsync(context, ex);
        }
    }

    private static async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        // Set Retry-After header before writing response.
        if (exception is TooManyAttemptsException tooMany)
            context.Response.Headers.RetryAfter = tooMany.RetryAfterSeconds.ToString();

        var problem = exception switch
        {
            NotFoundException ex => new ProblemDetails
            {
                Type = "https://httpstatuses.com/404",
                Title = "Not Found",
                Status = StatusCodes.Status404NotFound,
                Detail = ex.Message,
                Instance = context.Request.Path
            },
            ForbiddenException ex => new ProblemDetails
            {
                Type = "https://httpstatuses.com/403",
                Title = "Forbidden",
                Status = StatusCodes.Status403Forbidden,
                Detail = ex.Message,
                Instance = context.Request.Path
            },
            TooManyAttemptsException ex => new ProblemDetails
            {
                Type = "https://httpstatuses.com/429",
                Title = "Too Many Requests",
                Status = StatusCodes.Status429TooManyRequests,
                Detail = ex.Message,
                Instance = context.Request.Path
            },
            ValidationException ex => BuildValidationProblem(context, ex),
            _ => new ProblemDetails
            {
                Type = "https://httpstatuses.com/500",
                Title = "Internal Server Error",
                Status = StatusCodes.Status500InternalServerError,
                Detail = "An unexpected error occurred.",
                Instance = context.Request.Path
            }
        };

        context.Response.StatusCode = problem.Status!.Value;
        context.Response.ContentType = "application/problem+json";

        await context.Response.WriteAsJsonAsync(problem);
    }

    private static ProblemDetails BuildValidationProblem(HttpContext context, ValidationException ex)
    {
        var errors = ex.Errors
            .GroupBy(e => e.PropertyName)
            .ToDictionary(
                g => g.Key,
                g => (object)g.Select(e => e.ErrorMessage).ToArray());

        var problem = new ProblemDetails
        {
            Type = "https://httpstatuses.com/422",
            Title = "Validation Failed",
            Status = StatusCodes.Status422UnprocessableEntity,
            Detail = "One or more validation errors occurred.",
            Instance = context.Request.Path
        };

        problem.Extensions["errors"] = errors;

        return problem;
    }
}
```

---

## Task 6: Remove User entity from Domain, update Business

**Files:**
- Delete: `src/CowetaConnect.Domain/Entities/User.cs`
- Modify: `src/CowetaConnect.Domain/Entities/Business.cs`

**Step 1: Delete User.cs**

```bash
rm "src/CowetaConnect.Domain/Entities/User.cs"
```

**Step 2: Remove the User navigation from Business.cs**

In `Business.cs` (line 31), remove:
```csharp
public User Owner { get; set; } = null!;
```

The `OwnerId` Guid property stays. Final `Business.cs` navigation section:

```csharp
    public Category Category { get; set; } = null!;
    public ICollection<BusinessTag> BusinessTags { get; set; } = [];
    public ICollection<BusinessHour> BusinessHours { get; set; } = [];
    public ICollection<BusinessPhoto> BusinessPhotos { get; set; } = [];
```

(No `Owner` navigation — Business only knows `OwnerId`.)

---

## Task 7: Delete old UserConfiguration, create new ApplicationUser + RefreshToken EF configs

**Files:**
- Delete: `src/CowetaConnect.Infrastructure/Data/Configurations/UserConfiguration.cs`
- Create: `src/CowetaConnect.Infrastructure/Identity/ApplicationUserConfiguration.cs`
- Create: `src/CowetaConnect.Infrastructure/Identity/RefreshTokenConfiguration.cs`

**Step 1: Delete UserConfiguration.cs**

```bash
rm "src/CowetaConnect.Infrastructure/Data/Configurations/UserConfiguration.cs"
```

**Step 2: Create ApplicationUserConfiguration.cs**

```csharp
// src/CowetaConnect.Infrastructure/Identity/ApplicationUserConfiguration.cs
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CowetaConnect.Infrastructure.Identity;

/// <summary>
/// Configures the custom columns added by ApplicationUser on top of IdentityUser&lt;Guid&gt;.
/// Identity's own columns (email, password_hash, security_stamp, etc.) are handled by Identity.
/// Table name override is done in CowetaConnectDbContext.OnModelCreating.
/// </summary>
public class ApplicationUserConfiguration : IEntityTypeConfiguration<ApplicationUser>
{
    public void Configure(EntityTypeBuilder<ApplicationUser> builder)
    {
        builder.Property(u => u.DisplayName).HasMaxLength(100).IsRequired();
        builder.Property(u => u.AvatarUrl).IsRequired(false);
        builder.Property(u => u.Role).HasMaxLength(20).HasDefaultValue("Member");
        builder.Property(u => u.IsEmailVerified).HasDefaultValue(false);
        builder.Property(u => u.GoogleSubject).IsRequired(false);
        builder.HasIndex(u => u.GoogleSubject);
        builder.Property(u => u.CreatedAt).IsRequired();
        builder.Property(u => u.LastLogin).IsRequired(false);
    }
}
```

**Step 3: Create RefreshTokenConfiguration.cs**

```csharp
// src/CowetaConnect.Infrastructure/Identity/RefreshTokenConfiguration.cs
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CowetaConnect.Infrastructure.Identity;

public class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> builder)
    {
        builder.ToTable("refresh_tokens");
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).HasDefaultValueSql("gen_random_uuid()");

        builder.Property(r => r.TokenHash).HasMaxLength(100).IsRequired();
        builder.HasIndex(r => r.TokenHash).IsUnique(); // fast lookup on validation

        builder.Property(r => r.UserId).IsRequired();
        builder.HasIndex(r => r.UserId);

        builder.Property(r => r.ExpiresAt).IsRequired();
        builder.Property(r => r.CreatedAt).IsRequired();
        builder.Property(r => r.RevokedAt).IsRequired(false);

        // FK to users table — no navigation property needed.
        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(r => r.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
```

---

## Task 8: Update BusinessConfiguration to use shadow FK to ApplicationUser

**Files:**
- Modify: `src/CowetaConnect.Infrastructure/Data/Configurations/BusinessConfiguration.cs`

**Step 1: Replace the HasOne/WithMany block**

Find and replace this block (lines 43–46):
```csharp
        builder.HasOne(b => b.Owner)
            .WithMany(u => u.Businesses)
            .HasForeignKey(b => b.OwnerId)
            .OnDelete(DeleteBehavior.Restrict);
```

With:
```csharp
        // Shadow FK to ApplicationUser — no navigation property (ApplicationUser lives in Infrastructure).
        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(b => b.OwnerId)
            .OnDelete(DeleteBehavior.Restrict);
```

---

## Task 9: Update CowetaConnectDbContext to extend IdentityDbContext

**Files:**
- Modify: `src/CowetaConnect.Infrastructure/Data/CowetaConnectDbContext.cs`

**Step 1: Replace the entire file**

```csharp
// src/CowetaConnect.Infrastructure/Data/CowetaConnectDbContext.cs
using CowetaConnect.Domain.Entities;
using CowetaConnect.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace CowetaConnect.Infrastructure.Data;

public class CowetaConnectDbContext(DbContextOptions<CowetaConnectDbContext> options)
    : IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>(options)
{
    // Identity's base class exposes: Users, Roles, UserClaims, UserLogins, UserTokens, etc.
    public DbSet<Business> Businesses => Set<Business>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Tag> Tags => Set<Tag>();
    public DbSet<BusinessTag> BusinessTags => Set<BusinessTag>();
    public DbSet<BusinessHour> BusinessHours => Set<BusinessHour>();
    public DbSet<BusinessPhoto> BusinessPhotos => Set<BusinessPhoto>();
    public DbSet<SearchEvent> SearchEvents => Set<SearchEvent>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder); // MUST be called first — wires up Identity schema

        modelBuilder.HasPostgresExtension("postgis");

        // Rename Identity tables to match project snake_case conventions.
        modelBuilder.Entity<ApplicationUser>().ToTable("users");
        modelBuilder.Entity<IdentityRole<Guid>>().ToTable("roles");
        modelBuilder.Entity<IdentityUserRole<Guid>>().ToTable("user_roles");
        modelBuilder.Entity<IdentityUserClaim<Guid>>().ToTable("user_claims");
        modelBuilder.Entity<IdentityUserLogin<Guid>>().ToTable("user_logins");
        modelBuilder.Entity<IdentityUserToken<Guid>>().ToTable("user_tokens");
        modelBuilder.Entity<IdentityRoleClaim<Guid>>().ToTable("role_claims");

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(CowetaConnectDbContext).Assembly);
    }
}
```

> **Note:** `ApplyConfigurationsFromAssembly` will pick up `ApplicationUserConfiguration` and `RefreshTokenConfiguration` automatically since they implement `IEntityTypeConfiguration<T>`. The old `UserConfiguration` is deleted.

---

## Task 10: Register Identity in ServiceCollectionExtensions (Infrastructure)

**Files:**
- Modify: `src/CowetaConnect.Infrastructure/ServiceCollectionExtensions.cs`

**Step 1: Add Identity registration block**

Add these usings at the top:
```csharp
using CowetaConnect.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
```

Add this block AFTER the EF Core block and BEFORE the Redis block:

```csharp
        // ASP.NET Core Identity — lean setup (no cookie auth, just UserManager + stores).
        services.AddIdentityCore<ApplicationUser>(options =>
        {
            // Password policy — matches SECURITY.md requirements.
            options.Password.RequireDigit = true;
            options.Password.RequireUppercase = true;
            options.Password.RequireLowercase = false;
            options.Password.RequireNonAlphanumeric = false;
            options.Password.RequiredLength = 8;

            // Disable Identity's built-in lockout — we handle it in Redis per-IP.
            options.Lockout.MaxFailedAccessAttempts = int.MaxValue;

            options.User.RequireUniqueEmail = true;
        })
        .AddRoles<IdentityRole<Guid>>()
        .AddEntityFrameworkStores<CowetaConnectDbContext>();
```

---

## Task 11: Squash migrations — drop dev DB and regenerate from scratch

> **Why:** The existing `InitialCreate` migration creates a custom `users` table. We now use Identity's schema, which is fundamentally different. Squashing is cleaner than a transitional migration for a dev-only branch.

**Step 1: Drop the dev database**

```bash
dotnet ef database drop \
  --project src/CowetaConnect.Infrastructure \
  --startup-project src/CowetaConnect.API \
  --force
```

**Step 2: Delete old migration files**

```bash
rm src/CowetaConnect.Infrastructure/Migrations/20260221182911_InitialCreate.cs
rm src/CowetaConnect.Infrastructure/Migrations/20260221182911_InitialCreate.Designer.cs
rm src/CowetaConnect.Infrastructure/Migrations/CowetaConnectDbContextModelSnapshot.cs
```

**Step 3: Build to catch any remaining errors**

```bash
dotnet build src/CowetaConnect.sln
```

Fix any remaining compile errors before proceeding.

**Step 4: Add fresh migration**

```bash
dotnet ef migrations add InitialCreate \
  --project src/CowetaConnect.Infrastructure \
  --startup-project src/CowetaConnect.API
```

Expected: new files in `src/CowetaConnect.Infrastructure/Migrations/`

**Step 5: Apply migration to dev database**

```bash
dotnet ef database update \
  --project src/CowetaConnect.Infrastructure \
  --startup-project src/CowetaConnect.API
```

Expected: `Done.`

**Step 6: Commit**

```bash
git add src/CowetaConnect.Infrastructure/ src/CowetaConnect.Domain/ src/CowetaConnect.API/Middleware/
git commit -m "feat: replace User entity with ApplicationUser (Identity) + add RefreshTokens table"
```

---

## Task 12: Define Application-layer auth interfaces

**Files:**
- Create: `src/CowetaConnect.Application/Auth/Interfaces/IAuthUserService.cs`
- Create: `src/CowetaConnect.Application/Auth/Interfaces/IJwtTokenService.cs`
- Create: `src/CowetaConnect.Application/Auth/Interfaces/IRefreshTokenRepository.cs`

**Step 1: IAuthUserService.cs**

```csharp
// src/CowetaConnect.Application/Auth/Interfaces/IAuthUserService.cs
namespace CowetaConnect.Application.Auth.Interfaces;

/// <summary>Result returned by IAuthUserService operations.</summary>
public record AuthUserResult(
    bool Success,
    string? UserId,
    string? Email,
    string? Role,
    string? Error = null);

/// <summary>
/// Abstracts ASP.NET Core Identity UserManager from Application layer commands.
/// </summary>
public interface IAuthUserService
{
    /// <summary>Creates a new user. Returns success/failure with the new UserId.</summary>
    Task<AuthUserResult> RegisterAsync(
        string email, string password, string displayName, CancellationToken ct = default);

    /// <summary>Validates email + password. Returns user info on success.</summary>
    Task<AuthUserResult> ValidateCredentialsAsync(
        string email, string password, CancellationToken ct = default);

    /// <summary>
    /// Increments the per-IP failed login counter in Redis.
    /// Returns the new count.
    /// </summary>
    Task<long> RecordFailedLoginAsync(string ipAddress, CancellationToken ct = default);

    /// <summary>Returns the current failed login count for the given IP.</summary>
    Task<long> GetFailedLoginCountAsync(string ipAddress, CancellationToken ct = default);

    /// <summary>Clears the per-IP failed login counter after a successful login.</summary>
    Task ClearFailedLoginsAsync(string ipAddress, CancellationToken ct = default);

    /// <summary>Updates LastLogin timestamp for the given user.</summary>
    Task UpdateLastLoginAsync(string userId, CancellationToken ct = default);
}
```

**Step 2: IJwtTokenService.cs**

```csharp
// src/CowetaConnect.Application/Auth/Interfaces/IJwtTokenService.cs
namespace CowetaConnect.Application.Auth.Interfaces;

public interface IJwtTokenService
{
    /// <summary>
    /// Generates a signed RS256 JWT access token valid for 15 minutes.
    /// Claims: sub, email, role, jti, iat, exp, iss, aud.
    /// </summary>
    string GenerateAccessToken(string userId, string email, string role);

    /// <summary>
    /// Generates a cryptographically random 64-byte token encoded as Base64.
    /// This is the raw token — hash it with HashToken before storing.
    /// </summary>
    string GenerateRefreshToken();

    /// <summary>Returns SHA-256 hash of the token, encoded as Base64.</summary>
    string HashToken(string token);
}
```

**Step 3: IRefreshTokenRepository.cs**

```csharp
// src/CowetaConnect.Application/Auth/Interfaces/IRefreshTokenRepository.cs
namespace CowetaConnect.Application.Auth.Interfaces;

public interface IRefreshTokenRepository
{
    /// <summary>Stores the SHA-256 hash of a new refresh token.</summary>
    Task StoreAsync(string userId, string tokenHash, DateTimeOffset expiresAt, CancellationToken ct = default);

    /// <summary>
    /// Validates the token hash and revokes it (rotation). Returns the UserId if valid,
    /// null if not found or already revoked/expired.
    /// </summary>
    Task<string?> ConsumeAsync(string tokenHash, CancellationToken ct = default);

    /// <summary>Revokes a token by hash (logout).</summary>
    Task RevokeAsync(string tokenHash, CancellationToken ct = default);

    /// <summary>Revokes all refresh tokens for a user (force logout everywhere).</summary>
    Task RevokeAllForUserAsync(string userId, CancellationToken ct = default);
}
```

---

## Task 13: Implement JwtTokenService (Infrastructure)

**Files:**
- Create: `src/CowetaConnect.Infrastructure/Services/JwtTokenService.cs`

**Step 1: Create the file**

```csharp
// src/CowetaConnect.Infrastructure/Services/JwtTokenService.cs
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using CowetaConnect.Application.Auth.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace CowetaConnect.Infrastructure.Services;

public sealed class JwtTokenService : IJwtTokenService
{
    private readonly RsaSecurityKey _signingKey;
    private readonly string _issuer;
    private readonly string _audience;
    private readonly int _accessTokenLifetimeMinutes;

    public JwtTokenService(RsaSecurityKey signingKey, IConfiguration configuration)
    {
        _signingKey = signingKey;
        _issuer = configuration["Jwt:Issuer"]
            ?? throw new InvalidOperationException("Jwt:Issuer is not configured.");
        _audience = configuration["Jwt:Audience"]
            ?? throw new InvalidOperationException("Jwt:Audience is not configured.");
        _accessTokenLifetimeMinutes = configuration.GetValue("Jwt:AccessTokenLifetimeMinutes", 15);
    }

    public string GenerateAccessToken(string userId, string email, string role)
    {
        var now = DateTime.UtcNow;
        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, userId),
            new Claim(JwtRegisteredClaimNames.Email, email),
            new Claim(ClaimTypes.Role, role),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new Claim(JwtRegisteredClaimNames.Iat,
                new DateTimeOffset(now).ToUnixTimeSeconds().ToString(),
                ClaimValueTypes.Integer64)
        };

        var credentials = new SigningCredentials(_signingKey, SecurityAlgorithms.RsaSha256);

        var token = new JwtSecurityToken(
            issuer: _issuer,
            audience: _audience,
            claims: claims,
            notBefore: now,
            expires: now.AddMinutes(_accessTokenLifetimeMinutes),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public string GenerateRefreshToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(64);
        return Convert.ToBase64String(bytes);
    }

    public string HashToken(string token)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(token));
        return Convert.ToBase64String(bytes);
    }
}
```

---

## Task 14: Implement AuthUserService (Infrastructure)

**Files:**
- Create: `src/CowetaConnect.Infrastructure/Services/AuthUserService.cs`

**Step 1: Create the file**

```csharp
// src/CowetaConnect.Infrastructure/Services/AuthUserService.cs
using CowetaConnect.Application.Auth.Interfaces;
using CowetaConnect.Infrastructure.Data;
using CowetaConnect.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace CowetaConnect.Infrastructure.Services;

public sealed class AuthUserService(
    UserManager<ApplicationUser> userManager,
    IConnectionMultiplexer redis,
    CowetaConnectDbContext db,
    ILogger<AuthUserService> logger) : IAuthUserService
{
    private const string FailedLoginKeyPrefix = "auth:failed:";
    private static readonly TimeSpan FailedLoginTtl = TimeSpan.FromMinutes(15);

    public async Task<AuthUserResult> RegisterAsync(
        string email, string password, string displayName, CancellationToken ct = default)
    {
        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            UserName = email,          // Identity uses UserName as the login key
            Email = email,
            DisplayName = displayName,
            CreatedAt = DateTimeOffset.UtcNow,
            IsEmailVerified = false
        };

        var result = await userManager.CreateAsync(user, password);

        if (!result.Succeeded)
        {
            var error = string.Join("; ", result.Errors.Select(e => e.Description));
            logger.LogWarning("Registration failed for {Email}: {Error}", email, error);
            return new AuthUserResult(false, null, null, null, error);
        }

        return new AuthUserResult(true, user.Id.ToString(), user.Email, user.Role);
    }

    public async Task<AuthUserResult> ValidateCredentialsAsync(
        string email, string password, CancellationToken ct = default)
    {
        var user = await userManager.FindByEmailAsync(email);
        if (user is null)
            return new AuthUserResult(false, null, null, null, "Invalid credentials.");

        var valid = await userManager.CheckPasswordAsync(user, password);
        if (!valid)
            return new AuthUserResult(false, null, null, null, "Invalid credentials.");

        return new AuthUserResult(true, user.Id.ToString(), user.Email, user.Role);
    }

    public async Task<long> RecordFailedLoginAsync(string ipAddress, CancellationToken ct = default)
    {
        var db = redis.GetDatabase();
        var key = $"{FailedLoginKeyPrefix}{ipAddress}";
        var count = await db.StringIncrementAsync(key);
        await db.KeyExpireAsync(key, FailedLoginTtl);
        return count;
    }

    public async Task<long> GetFailedLoginCountAsync(string ipAddress, CancellationToken ct = default)
    {
        var db = redis.GetDatabase();
        var key = $"{FailedLoginKeyPrefix}{ipAddress}";
        var value = await db.StringGetAsync(key);
        return value.HasValue ? (long)value : 0;
    }

    public async Task ClearFailedLoginsAsync(string ipAddress, CancellationToken ct = default)
    {
        var db = redis.GetDatabase();
        await db.KeyDeleteAsync($"{FailedLoginKeyPrefix}{ipAddress}");
    }

    public async Task UpdateLastLoginAsync(string userId, CancellationToken ct = default)
    {
        var user = await userManager.FindByIdAsync(userId);
        if (user is null) return;

        user.LastLogin = DateTimeOffset.UtcNow;
        await userManager.UpdateAsync(user);
    }
}
```

> **Note:** `db` field from the constructor conflicts with the local `var db = redis.GetDatabase()` on the Redis calls. Rename the constructor parameter to `_db` or use a different name. Use `_db` for the EF Core context (unused in this service) and rename the Redis variable. Actually, looking at the code — the `db` parameter (CowetaConnectDbContext) is unused; remove it from the constructor if not needed here. RefreshToken DB ops go via `RefreshTokenRepository`. Fix:

Remove `CowetaConnectDbContext db` from the AuthUserService constructor (RefreshTokenRepository handles DB). The corrected constructor:

```csharp
public sealed class AuthUserService(
    UserManager<ApplicationUser> userManager,
    IConnectionMultiplexer redis,
    ILogger<AuthUserService> logger) : IAuthUserService
```

And update the Redis calls to use a local variable named `cache`:
```csharp
var cache = redis.GetDatabase();
var key = $"{FailedLoginKeyPrefix}{ipAddress}";
var count = await cache.StringIncrementAsync(key);
await cache.KeyExpireAsync(key, FailedLoginTtl);
```

---

## Task 15: Implement RefreshTokenRepository (Infrastructure)

**Files:**
- Create: `src/CowetaConnect.Infrastructure/Services/RefreshTokenRepository.cs`

**Step 1: Create the file**

```csharp
// src/CowetaConnect.Infrastructure/Services/RefreshTokenRepository.cs
using CowetaConnect.Application.Auth.Interfaces;
using CowetaConnect.Infrastructure.Data;
using CowetaConnect.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;

namespace CowetaConnect.Infrastructure.Services;

public sealed class RefreshTokenRepository(CowetaConnectDbContext db) : IRefreshTokenRepository
{
    public async Task StoreAsync(
        string userId, string tokenHash, DateTimeOffset expiresAt, CancellationToken ct = default)
    {
        db.RefreshTokens.Add(new RefreshToken
        {
            UserId = Guid.Parse(userId),
            TokenHash = tokenHash,
            ExpiresAt = expiresAt,
            CreatedAt = DateTimeOffset.UtcNow
        });
        await db.SaveChangesAsync(ct);
    }

    public async Task<string?> ConsumeAsync(string tokenHash, CancellationToken ct = default)
    {
        var token = await db.RefreshTokens
            .FirstOrDefaultAsync(t => t.TokenHash == tokenHash, ct);

        if (token is null || !token.IsValid)
            return null;

        token.RevokedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);

        return token.UserId.ToString();
    }

    public async Task RevokeAsync(string tokenHash, CancellationToken ct = default)
    {
        var token = await db.RefreshTokens
            .FirstOrDefaultAsync(t => t.TokenHash == tokenHash, ct);

        if (token is null) return;

        token.RevokedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);
    }

    public async Task RevokeAllForUserAsync(string userId, CancellationToken ct = default)
    {
        var userGuid = Guid.Parse(userId);
        var tokens = await db.RefreshTokens
            .Where(t => t.UserId == userGuid && t.RevokedAt == null)
            .ToListAsync(ct);

        foreach (var token in tokens)
            token.RevokedAt = DateTimeOffset.UtcNow;

        await db.SaveChangesAsync(ct);
    }
}
```

---

## Task 16: Register auth services in ServiceCollectionExtensions

**Files:**
- Modify: `src/CowetaConnect.Infrastructure/ServiceCollectionExtensions.cs`

**Step 1: Add service registrations**

Add these usings:
```csharp
using CowetaConnect.Application.Auth.Interfaces;
using CowetaConnect.Infrastructure.Services;
```

Add these lines at the end of `AddInfrastructure`, before `return services;`:

```csharp
        // Auth services
        services.AddScoped<IJwtTokenService, JwtTokenService>();
        services.AddScoped<IAuthUserService, AuthUserService>();
        services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();
```

---

## Task 17: Create DTOs, validators, and TokenResponse

**Files:**
- Create: `src/CowetaConnect.Application/Auth/Dtos/RegisterDto.cs`
- Create: `src/CowetaConnect.Application/Auth/Dtos/LoginDto.cs`
- Create: `src/CowetaConnect.Application/Auth/Models/TokenResponse.cs`
- Create: `src/CowetaConnect.Application/Auth/Validators/RegisterDtoValidator.cs`
- Create: `src/CowetaConnect.Application/Auth/Validators/LoginDtoValidator.cs`

**Step 1: RegisterDto.cs**

```csharp
// src/CowetaConnect.Application/Auth/Dtos/RegisterDto.cs
namespace CowetaConnect.Application.Auth.Dtos;

public record RegisterDto(string Email, string Password, string DisplayName);
```

**Step 2: LoginDto.cs**

```csharp
// src/CowetaConnect.Application/Auth/Dtos/LoginDto.cs
namespace CowetaConnect.Application.Auth.Dtos;

public record LoginDto(string Email, string Password);
```

**Step 3: TokenResponse.cs**

```csharp
// src/CowetaConnect.Application/Auth/Models/TokenResponse.cs
namespace CowetaConnect.Application.Auth.Models;

public record TokenResponse(
    string AccessToken,
    string TokenType = "Bearer");
```

**Step 4: RegisterDtoValidator.cs**

```csharp
// src/CowetaConnect.Application/Auth/Validators/RegisterDtoValidator.cs
using CowetaConnect.Application.Auth.Dtos;
using FluentValidation;

namespace CowetaConnect.Application.Auth.Validators;

public class RegisterDtoValidator : AbstractValidator<RegisterDto>
{
    public RegisterDtoValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email is required.")
            .EmailAddress().WithMessage("Email must be a valid email address.")
            .MaximumLength(320);

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("Password is required.")
            .MinimumLength(8).WithMessage("Password must be at least 8 characters.")
            .Matches("[A-Z]").WithMessage("Password must contain at least one uppercase letter.")
            .Matches("[0-9]").WithMessage("Password must contain at least one digit.");

        RuleFor(x => x.DisplayName)
            .NotEmpty().WithMessage("Display name is required.")
            .MinimumLength(2).WithMessage("Display name must be at least 2 characters.")
            .MaximumLength(100);
    }
}
```

**Step 5: LoginDtoValidator.cs**

```csharp
// src/CowetaConnect.Application/Auth/Validators/LoginDtoValidator.cs
using CowetaConnect.Application.Auth.Dtos;
using FluentValidation;

namespace CowetaConnect.Application.Auth.Validators;

public class LoginDtoValidator : AbstractValidator<LoginDto>
{
    public LoginDtoValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email is required.")
            .EmailAddress().WithMessage("Email must be a valid email address.");

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("Password is required.");
    }
}
```

---

## Task 18: Create RegisterCommand + Handler

**Files:**
- Create: `src/CowetaConnect.Application/Auth/Commands/RegisterCommand.cs`

**Step 1: Create the file**

```csharp
// src/CowetaConnect.Application/Auth/Commands/RegisterCommand.cs
using CowetaConnect.Application.Auth.Dtos;
using CowetaConnect.Application.Auth.Interfaces;
using CowetaConnect.Application.Auth.Models;
using MediatR;

namespace CowetaConnect.Application.Auth.Commands;

// ── Command ──────────────────────────────────────────────────────────────────

public record RegisterCommand(RegisterDto Dto) : IRequest<(TokenResponse Token, string RefreshToken)>;

// ── Handler ──────────────────────────────────────────────────────────────────

public sealed class RegisterCommandHandler(
    IAuthUserService authService,
    IJwtTokenService tokenService,
    IRefreshTokenRepository refreshTokenRepo)
    : IRequestHandler<RegisterCommand, (TokenResponse Token, string RefreshToken)>
{
    public async Task<(TokenResponse Token, string RefreshToken)> Handle(
        RegisterCommand request, CancellationToken cancellationToken)
    {
        var result = await authService.RegisterAsync(
            request.Dto.Email,
            request.Dto.Password,
            request.Dto.DisplayName,
            cancellationToken);

        if (!result.Success)
            throw new InvalidOperationException(result.Error ?? "Registration failed.");

        return await IssueTokenPair(result, cancellationToken);
    }

    private async Task<(TokenResponse Token, string RefreshToken)> IssueTokenPair(
        AuthUserResult user, CancellationToken ct)
    {
        var accessToken = tokenService.GenerateAccessToken(user.UserId!, user.Email!, user.Role!);
        var rawRefresh = tokenService.GenerateRefreshToken();
        var hash = tokenService.HashToken(rawRefresh);

        await refreshTokenRepo.StoreAsync(
            user.UserId!,
            hash,
            DateTimeOffset.UtcNow.AddDays(7),
            ct);

        return (new TokenResponse(accessToken), rawRefresh);
    }
}
```

> **Note:** `RegisterCommand` returns both the access token and the raw refresh token. The controller sets the raw refresh token as an httpOnly cookie and returns only the `TokenResponse` (access token) in the body.

---

## Task 19: Create LoginCommand + Handler

**Files:**
- Create: `src/CowetaConnect.Application/Auth/Commands/LoginCommand.cs`

**Step 1: Create the file**

```csharp
// src/CowetaConnect.Application/Auth/Commands/LoginCommand.cs
using CowetaConnect.Application.Auth.Dtos;
using CowetaConnect.Application.Auth.Interfaces;
using CowetaConnect.Application.Auth.Models;
using CowetaConnect.Domain.Exceptions;
using MediatR;
using Microsoft.Extensions.Logging;

namespace CowetaConnect.Application.Auth.Commands;

// ── Command ──────────────────────────────────────────────────────────────────

/// <param name="RemoteIp">Caller's IP address for failed-login rate limiting.</param>
public record LoginCommand(LoginDto Dto, string RemoteIp)
    : IRequest<(TokenResponse Token, string RefreshToken)>;

// ── Handler ──────────────────────────────────────────────────────────────────

public sealed class LoginCommandHandler(
    IAuthUserService authService,
    IJwtTokenService tokenService,
    IRefreshTokenRepository refreshTokenRepo,
    ILogger<LoginCommandHandler> logger)
    : IRequestHandler<LoginCommand, (TokenResponse Token, string RefreshToken)>
{
    private const int MaxFailedAttempts = 5;
    private const int LockoutSeconds = 900; // 15 minutes

    public async Task<(TokenResponse Token, string RefreshToken)> Handle(
        LoginCommand request, CancellationToken cancellationToken)
    {
        // 1. Check per-IP rate limit before touching the database.
        var failedCount = await authService.GetFailedLoginCountAsync(request.RemoteIp, cancellationToken);
        if (failedCount >= MaxFailedAttempts)
            throw new TooManyAttemptsException(LockoutSeconds);

        // 2. Validate credentials.
        var result = await authService.ValidateCredentialsAsync(
            request.Dto.Email, request.Dto.Password, cancellationToken);

        if (!result.Success)
        {
            var newCount = await authService.RecordFailedLoginAsync(request.RemoteIp, cancellationToken);

            logger.LogWarning(
                "Failed login attempt for {Email} from {Ip}. Attempt {Count}/{Max}",
                request.Dto.Email, request.RemoteIp, newCount, MaxFailedAttempts);

            // Return generic message — do not reveal whether email exists.
            throw new InvalidOperationException("Invalid email or password.");
        }

        // 3. Successful login — clear failure counter.
        await authService.ClearFailedLoginsAsync(request.RemoteIp, cancellationToken);
        await authService.UpdateLastLoginAsync(result.UserId!, cancellationToken);

        // 4. Issue token pair.
        var accessToken = tokenService.GenerateAccessToken(result.UserId!, result.Email!, result.Role!);
        var rawRefresh = tokenService.GenerateRefreshToken();
        var hash = tokenService.HashToken(rawRefresh);

        await refreshTokenRepo.StoreAsync(
            result.UserId!,
            hash,
            DateTimeOffset.UtcNow.AddDays(7),
            cancellationToken);

        return (new TokenResponse(accessToken), rawRefresh);
    }
}
```

---

## Task 20: Create RefreshTokenCommand + Handler

**Files:**
- Create: `src/CowetaConnect.Application/Auth/Commands/RefreshTokenCommand.cs`

**Step 1: Create the file**

```csharp
// src/CowetaConnect.Application/Auth/Commands/RefreshTokenCommand.cs
using CowetaConnect.Application.Auth.Interfaces;
using CowetaConnect.Application.Auth.Models;
using CowetaConnect.Domain.Exceptions;
using MediatR;

namespace CowetaConnect.Application.Auth.Commands;

// ── Command ──────────────────────────────────────────────────────────────────

/// <param name="RawRefreshToken">The raw token from the httpOnly cookie.</param>
public record RefreshTokenCommand(string RawRefreshToken)
    : IRequest<(TokenResponse Token, string NewRefreshToken)>;

// ── Handler ──────────────────────────────────────────────────────────────────

public sealed class RefreshTokenCommandHandler(
    IAuthUserService authService,
    IJwtTokenService tokenService,
    IRefreshTokenRepository refreshTokenRepo)
    : IRequestHandler<RefreshTokenCommand, (TokenResponse Token, string NewRefreshToken)>
{
    public async Task<(TokenResponse Token, string NewRefreshToken)> Handle(
        RefreshTokenCommand request, CancellationToken cancellationToken)
    {
        var hash = tokenService.HashToken(request.RawRefreshToken);

        // ConsumeAsync validates + revokes the old token atomically (rotation).
        var userId = await refreshTokenRepo.ConsumeAsync(hash, cancellationToken);

        if (userId is null)
            throw new ForbiddenException("Refresh token is invalid or expired.");

        // Fetch user details to re-issue with current role.
        var result = await authService.ValidateCredentialsAsync(
            string.Empty, string.Empty, cancellationToken); // won't work — need a different lookup

        // NOTE: We need to get user info by userId, not credentials.
        // Add GetByIdAsync to IAuthUserService in the next step.
        // Placeholder:
        throw new NotImplementedException("Implement GetByIdAsync on IAuthUserService — see plan Task 20 note.");
    }
}
```

> **Correction needed:** `RefreshTokenCommand` needs to look up the user by `userId` (not email+password). Add `GetByIdAsync` to `IAuthUserService` before implementing the handler body.

**Step 2: Add GetByIdAsync to IAuthUserService**

In `IAuthUserService.cs`, add:
```csharp
    /// <summary>Returns user info by ID. Returns null if not found.</summary>
    Task<AuthUserResult?> GetByIdAsync(string userId, CancellationToken ct = default);
```

**Step 3: Implement GetByIdAsync in AuthUserService**

In `AuthUserService.cs`, add:
```csharp
    public async Task<AuthUserResult?> GetByIdAsync(string userId, CancellationToken ct = default)
    {
        var user = await userManager.FindByIdAsync(userId);
        if (user is null) return null;
        return new AuthUserResult(true, user.Id.ToString(), user.Email, user.Role);
    }
```

**Step 4: Complete the RefreshTokenCommandHandler**

Replace the placeholder throw with:
```csharp
        var user = await authService.GetByIdAsync(userId, cancellationToken);
        if (user is null)
            throw new ForbiddenException("User not found.");

        var newAccessToken = tokenService.GenerateAccessToken(user.UserId!, user.Email!, user.Role!);
        var newRawRefresh = tokenService.GenerateRefreshToken();
        var newHash = tokenService.HashToken(newRawRefresh);

        await refreshTokenRepo.StoreAsync(
            user.UserId!,
            newHash,
            DateTimeOffset.UtcNow.AddDays(7),
            cancellationToken);

        return (new TokenResponse(newAccessToken), newRawRefresh);
```

The completed handler:

```csharp
// src/CowetaConnect.Application/Auth/Commands/RefreshTokenCommand.cs
using CowetaConnect.Application.Auth.Interfaces;
using CowetaConnect.Application.Auth.Models;
using CowetaConnect.Domain.Exceptions;
using MediatR;

namespace CowetaConnect.Application.Auth.Commands;

public record RefreshTokenCommand(string RawRefreshToken)
    : IRequest<(TokenResponse Token, string NewRefreshToken)>;

public sealed class RefreshTokenCommandHandler(
    IAuthUserService authService,
    IJwtTokenService tokenService,
    IRefreshTokenRepository refreshTokenRepo)
    : IRequestHandler<RefreshTokenCommand, (TokenResponse Token, string NewRefreshToken)>
{
    public async Task<(TokenResponse Token, string NewRefreshToken)> Handle(
        RefreshTokenCommand request, CancellationToken cancellationToken)
    {
        var hash = tokenService.HashToken(request.RawRefreshToken);
        var userId = await refreshTokenRepo.ConsumeAsync(hash, cancellationToken);

        if (userId is null)
            throw new ForbiddenException("Refresh token is invalid or expired.");

        var user = await authService.GetByIdAsync(userId, cancellationToken);
        if (user is null)
            throw new ForbiddenException("User not found.");

        var newAccessToken = tokenService.GenerateAccessToken(user.UserId!, user.Email!, user.Role!);
        var newRawRefresh = tokenService.GenerateRefreshToken();
        var newHash = tokenService.HashToken(newRawRefresh);

        await refreshTokenRepo.StoreAsync(
            user.UserId!,
            newHash,
            DateTimeOffset.UtcNow.AddDays(7),
            cancellationToken);

        return (new TokenResponse(newAccessToken), newRawRefresh);
    }
}
```

---

## Task 21: Create LogoutCommand + Handler

**Files:**
- Create: `src/CowetaConnect.Application/Auth/Commands/LogoutCommand.cs`

**Step 1: Create the file**

```csharp
// src/CowetaConnect.Application/Auth/Commands/LogoutCommand.cs
using CowetaConnect.Application.Auth.Interfaces;
using MediatR;

namespace CowetaConnect.Application.Auth.Commands;

public record LogoutCommand(string? RawRefreshToken) : IRequest;

public sealed class LogoutCommandHandler(
    IJwtTokenService tokenService,
    IRefreshTokenRepository refreshTokenRepo)
    : IRequestHandler<LogoutCommand>
{
    public async Task Handle(LogoutCommand request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(request.RawRefreshToken))
            return; // Cookie absent — nothing to revoke.

        var hash = tokenService.HashToken(request.RawRefreshToken);
        await refreshTokenRepo.RevokeAsync(hash, cancellationToken);
    }
}
```

---

## Task 22: Implement AuthController

**Files:**
- Modify: `src/CowetaConnect.API/Controllers/v1/AuthController.cs`

**Step 1: Replace the stub with the full controller**

```csharp
// src/CowetaConnect.API/Controllers/v1/AuthController.cs
using Asp.Versioning;
using CowetaConnect.Application.Auth.Commands;
using CowetaConnect.Application.Auth.Dtos;
using CowetaConnect.Application.Auth.Models;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace CowetaConnect.API.Controllers.V1;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/auth")]
[EnableRateLimiting("auth-endpoints")]
public class AuthController(IMediator mediator) : ControllerBase
{
    private const string RefreshCookieName = "refresh_token";

    // POST /api/v1/auth/register
    [HttpPost("register")]
    [ProducesResponseType(typeof(TokenResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Register([FromBody] RegisterDto dto, CancellationToken ct)
    {
        var (token, rawRefresh) = await mediator.Send(new RegisterCommand(dto), ct);
        SetRefreshCookie(rawRefresh);
        return StatusCode(StatusCodes.Status201Created, token);
    }

    // POST /api/v1/auth/login
    [HttpPost("login")]
    [ProducesResponseType(typeof(TokenResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> Login([FromBody] LoginDto dto, CancellationToken ct)
    {
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        var (token, rawRefresh) = await mediator.Send(new LoginCommand(dto, ip), ct);
        SetRefreshCookie(rawRefresh);
        return Ok(token);
    }

    // POST /api/v1/auth/refresh
    [HttpPost("refresh")]
    [ProducesResponseType(typeof(TokenResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Refresh(CancellationToken ct)
    {
        var rawRefresh = Request.Cookies[RefreshCookieName];
        if (string.IsNullOrEmpty(rawRefresh))
            return Forbid();

        var (token, newRawRefresh) = await mediator.Send(new RefreshTokenCommand(rawRefresh), ct);
        SetRefreshCookie(newRawRefresh);
        return Ok(token);
    }

    // POST /api/v1/auth/logout
    [HttpPost("logout")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Logout(CancellationToken ct)
    {
        var rawRefresh = Request.Cookies[RefreshCookieName];
        await mediator.Send(new LogoutCommand(rawRefresh), ct);

        Response.Cookies.Delete(RefreshCookieName, new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Strict
        });

        return NoContent();
    }

    private void SetRefreshCookie(string rawRefreshToken)
    {
        Response.Cookies.Append(RefreshCookieName, rawRefreshToken, new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Strict,
            Expires = DateTimeOffset.UtcNow.AddDays(7),
            Path = "/"
        });
    }
}
```

**Step 2: Build the solution**

```bash
dotnet build src/CowetaConnect.sln
```

Expected: 0 errors. Fix any errors before proceeding.

**Step 3: Commit**

```bash
git add src/CowetaConnect.Application/ src/CowetaConnect.API/Controllers/ src/CowetaConnect.Infrastructure/Services/ src/CowetaConnect.Infrastructure/ServiceCollectionExtensions.cs
git commit -m "feat: implement register/login/refresh/logout commands and AuthController"
```

---

## Task 23: Write unit tests

**Files:**
- Create: `src/CowetaConnect.Tests/Auth/JwtTokenServiceTests.cs`
- Create: `src/CowetaConnect.Tests/Auth/RegisterDtoValidatorTests.cs`
- Create: `src/CowetaConnect.Tests/Auth/LoginCommandHandlerTests.cs`

**Step 1: JwtTokenServiceTests.cs**

```csharp
// src/CowetaConnect.Tests/Auth/JwtTokenServiceTests.cs
using System.IdentityModel.Tokens.Jwt;
using System.Security.Cryptography;
using CowetaConnect.Infrastructure.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace CowetaConnect.Tests.Auth;

[TestClass]
public class JwtTokenServiceTests
{
    private JwtTokenService _sut = null!;
    private RsaSecurityKey _signingKey = null!;

    [TestInitialize]
    public void Setup()
    {
        var rsa = RSA.Create(2048);
        _signingKey = new RsaSecurityKey(rsa) { KeyId = "test-key" };

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Issuer"] = "https://test.local",
                ["Jwt:Audience"] = "https://client.local",
                ["Jwt:AccessTokenLifetimeMinutes"] = "15"
            })
            .Build();

        _sut = new JwtTokenService(_signingKey, config);
    }

    [TestMethod]
    public void GenerateAccessToken_ReturnsValidJwt()
    {
        var token = _sut.GenerateAccessToken("user-123", "test@example.com", "Member");

        Assert.IsNotNull(token);
        var handler = new JwtSecurityTokenHandler();
        Assert.IsTrue(handler.CanReadToken(token));
    }

    [TestMethod]
    public void GenerateAccessToken_HasCorrectClaims()
    {
        var token = _sut.GenerateAccessToken("user-123", "test@example.com", "Owner");

        var handler = new JwtSecurityTokenHandler();
        var jwt = handler.ReadJwtToken(token);

        Assert.AreEqual("user-123", jwt.Subject);
        Assert.AreEqual("test@example.com", jwt.Claims.First(c => c.Type == "email").Value);
        Assert.IsNotNull(jwt.Claims.FirstOrDefault(c => c.Type == "jti"));
    }

    [TestMethod]
    public void GenerateAccessToken_ExpiresIn15Minutes()
    {
        var before = DateTime.UtcNow;
        var token = _sut.GenerateAccessToken("user-123", "test@example.com", "Member");
        var after = DateTime.UtcNow;

        var handler = new JwtSecurityTokenHandler();
        var jwt = handler.ReadJwtToken(token);

        // Allow 5 seconds of clock slack in tests.
        Assert.IsTrue(jwt.ValidTo >= before.AddMinutes(15).AddSeconds(-5));
        Assert.IsTrue(jwt.ValidTo <= after.AddMinutes(15).AddSeconds(5));
    }

    [TestMethod]
    public void GenerateRefreshToken_Returns86CharBase64()
    {
        var token = _sut.GenerateRefreshToken();

        Assert.IsNotNull(token);
        // 64 bytes in Base64 = 88 chars (with padding). Length >= 86.
        Assert.IsTrue(token.Length >= 86);
    }

    [TestMethod]
    public void GenerateRefreshToken_IsUnique()
    {
        var t1 = _sut.GenerateRefreshToken();
        var t2 = _sut.GenerateRefreshToken();

        Assert.AreNotEqual(t1, t2);
    }

    [TestMethod]
    public void HashToken_IsDeterministic()
    {
        var token = "some-raw-token";
        var hash1 = _sut.HashToken(token);
        var hash2 = _sut.HashToken(token);

        Assert.AreEqual(hash1, hash2);
    }

    [TestMethod]
    public void HashToken_DifferentInputsDifferentHashes()
    {
        var h1 = _sut.HashToken("token-a");
        var h2 = _sut.HashToken("token-b");

        Assert.AreNotEqual(h1, h2);
    }
}
```

**Step 2: RegisterDtoValidatorTests.cs**

```csharp
// src/CowetaConnect.Tests/Auth/RegisterDtoValidatorTests.cs
using CowetaConnect.Application.Auth.Dtos;
using CowetaConnect.Application.Auth.Validators;

namespace CowetaConnect.Tests.Auth;

[TestClass]
public class RegisterDtoValidatorTests
{
    private readonly RegisterDtoValidator _validator = new();

    [TestMethod]
    public void ValidDto_PassesValidation()
    {
        var dto = new RegisterDto("user@example.com", "Password1", "Jane Doe");
        var result = _validator.Validate(dto);
        Assert.IsTrue(result.IsValid);
    }

    [TestMethod]
    public void InvalidEmail_FailsValidation()
    {
        var dto = new RegisterDto("not-an-email", "Password1", "Jane");
        var result = _validator.Validate(dto);
        Assert.IsFalse(result.IsValid);
        Assert.IsTrue(result.Errors.Any(e => e.PropertyName == "Email"));
    }

    [TestMethod]
    public void PasswordTooShort_FailsValidation()
    {
        var dto = new RegisterDto("user@example.com", "Pass1", "Jane");
        var result = _validator.Validate(dto);
        Assert.IsFalse(result.IsValid);
        Assert.IsTrue(result.Errors.Any(e => e.PropertyName == "Password"));
    }

    [TestMethod]
    public void PasswordNoUppercase_FailsValidation()
    {
        var dto = new RegisterDto("user@example.com", "password1", "Jane");
        var result = _validator.Validate(dto);
        Assert.IsFalse(result.IsValid);
        Assert.IsTrue(result.Errors.Any(e => e.PropertyName == "Password"));
    }

    [TestMethod]
    public void PasswordNoDigit_FailsValidation()
    {
        var dto = new RegisterDto("user@example.com", "Password", "Jane");
        var result = _validator.Validate(dto);
        Assert.IsFalse(result.IsValid);
        Assert.IsTrue(result.Errors.Any(e => e.PropertyName == "Password"));
    }

    [TestMethod]
    public void DisplayNameTooShort_FailsValidation()
    {
        var dto = new RegisterDto("user@example.com", "Password1", "J");
        var result = _validator.Validate(dto);
        Assert.IsFalse(result.IsValid);
        Assert.IsTrue(result.Errors.Any(e => e.PropertyName == "DisplayName"));
    }

    [TestMethod]
    public void EmptyEmail_FailsValidation()
    {
        var dto = new RegisterDto("", "Password1", "Jane");
        var result = _validator.Validate(dto);
        Assert.IsFalse(result.IsValid);
    }
}
```

**Step 3: LoginCommandHandlerTests.cs**

```csharp
// src/CowetaConnect.Tests/Auth/LoginCommandHandlerTests.cs
using CowetaConnect.Application.Auth.Commands;
using CowetaConnect.Application.Auth.Dtos;
using CowetaConnect.Application.Auth.Interfaces;
using CowetaConnect.Application.Auth.Models;
using CowetaConnect.Domain.Exceptions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace CowetaConnect.Tests.Auth;

[TestClass]
public class LoginCommandHandlerTests
{
    private Mock<IAuthUserService> _authService = null!;
    private Mock<IJwtTokenService> _tokenService = null!;
    private Mock<IRefreshTokenRepository> _refreshRepo = null!;
    private LoginCommandHandler _sut = null!;

    [TestInitialize]
    public void Setup()
    {
        _authService = new Mock<IAuthUserService>();
        _tokenService = new Mock<IJwtTokenService>();
        _refreshRepo = new Mock<IRefreshTokenRepository>();

        _sut = new LoginCommandHandler(
            _authService.Object,
            _tokenService.Object,
            _refreshRepo.Object,
            NullLogger<LoginCommandHandler>.Instance);
    }

    [TestMethod]
    public async Task Handle_ValidCredentials_ReturnsTokens()
    {
        _authService.Setup(s => s.GetFailedLoginCountAsync("127.0.0.1", default)).ReturnsAsync(0);
        _authService.Setup(s => s.ValidateCredentialsAsync("user@test.com", "Password1", default))
            .ReturnsAsync(new AuthUserResult(true, "user-1", "user@test.com", "Member"));
        _authService.Setup(s => s.ClearFailedLoginsAsync("127.0.0.1", default)).Returns(Task.CompletedTask);
        _authService.Setup(s => s.UpdateLastLoginAsync("user-1", default)).Returns(Task.CompletedTask);
        _tokenService.Setup(s => s.GenerateAccessToken("user-1", "user@test.com", "Member"))
            .Returns("access-token-jwt");
        _tokenService.Setup(s => s.GenerateRefreshToken()).Returns("raw-refresh");
        _tokenService.Setup(s => s.HashToken("raw-refresh")).Returns("hashed-refresh");
        _refreshRepo.Setup(r => r.StoreAsync("user-1", "hashed-refresh", It.IsAny<DateTimeOffset>(), default))
            .Returns(Task.CompletedTask);

        var (token, rawRefresh) = await _sut.Handle(
            new LoginCommand(new LoginDto("user@test.com", "Password1"), "127.0.0.1"),
            default);

        Assert.AreEqual("access-token-jwt", token.AccessToken);
        Assert.AreEqual("raw-refresh", rawRefresh);
    }

    [TestMethod]
    public async Task Handle_InvalidCredentials_RecordsFailedAttempt()
    {
        _authService.Setup(s => s.GetFailedLoginCountAsync("127.0.0.1", default)).ReturnsAsync(0);
        _authService.Setup(s => s.ValidateCredentialsAsync(It.IsAny<string>(), It.IsAny<string>(), default))
            .ReturnsAsync(new AuthUserResult(false, null, null, null, "Invalid credentials."));
        _authService.Setup(s => s.RecordFailedLoginAsync("127.0.0.1", default)).ReturnsAsync(1);

        await Assert.ThrowsExceptionAsync<InvalidOperationException>(() =>
            _sut.Handle(new LoginCommand(new LoginDto("bad@test.com", "wrong"), "127.0.0.1"), default));

        _authService.Verify(s => s.RecordFailedLoginAsync("127.0.0.1", default), Times.Once);
    }

    [TestMethod]
    public async Task Handle_FiveFailedAttempts_ThrowsTooManyAttemptsException()
    {
        _authService.Setup(s => s.GetFailedLoginCountAsync("10.0.0.1", default)).ReturnsAsync(5);

        await Assert.ThrowsExceptionAsync<TooManyAttemptsException>(() =>
            _sut.Handle(new LoginCommand(new LoginDto("u@t.com", "p"), "10.0.0.1"), default));

        // Credentials should NOT be checked when limit is exceeded.
        _authService.Verify(s => s.ValidateCredentialsAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
```

**Step 4: Run the tests**

```bash
dotnet test src/CowetaConnect.Tests/CowetaConnect.Tests.csproj --logger "console;verbosity=normal"
```

Expected: all tests pass.

**Step 5: Commit**

```bash
git add src/CowetaConnect.Tests/
git commit -m "test: add JWT token service, validator, and login handler unit tests"
```

---

## Task 24: Final build + smoke test

**Step 1: Full solution build**

```bash
dotnet build src/CowetaConnect.sln
```

Expected: 0 errors, 0 warnings (treat Identity-generated NETSDK warnings as acceptable).

**Step 2: Run all tests**

```bash
dotnet test src/CowetaConnect.sln
```

Expected: all tests pass.

**Step 3: Start the API and test register endpoint manually**

```bash
dotnet run --project src/CowetaConnect.API
```

Then in another terminal:

```bash
curl -s -X POST https://localhost:5001/api/v1/auth/register \
  -H "Content-Type: application/json" \
  -d '{"email":"test@coweta.com","password":"Test1234","displayName":"Test User"}' \
  -k | jq .
```

Expected: HTTP 201 with `{"accessToken": "eyJ...", "tokenType": "Bearer"}` and `Set-Cookie: refresh_token=...`

**Step 4: Final commit**

```bash
git add -A
git commit -m "feat: complete Phase 1 JWT auth implementation (RS256 + Identity + refresh tokens)"
```

---

## Summary of all files changed

| Action | File |
|--------|------|
| DELETE | `src/CowetaConnect.Domain/Entities/User.cs` |
| DELETE | `src/CowetaConnect.Infrastructure/Data/Configurations/UserConfiguration.cs` |
| DELETE | `src/CowetaConnect.Infrastructure/Migrations/20260221182911_InitialCreate.cs` |
| DELETE | `src/CowetaConnect.Infrastructure/Migrations/20260221182911_InitialCreate.Designer.cs` |
| DELETE | `src/CowetaConnect.Infrastructure/Migrations/CowetaConnectDbContextModelSnapshot.cs` |
| MODIFY | `src/CowetaConnect.Domain/Entities/Business.cs` |
| MODIFY | `src/CowetaConnect.Infrastructure/Data/CowetaConnectDbContext.cs` |
| MODIFY | `src/CowetaConnect.Infrastructure/Data/Configurations/BusinessConfiguration.cs` |
| MODIFY | `src/CowetaConnect.Infrastructure/ServiceCollectionExtensions.cs` |
| MODIFY | `src/CowetaConnect.Infrastructure/CowetaConnect.Infrastructure.csproj` |
| MODIFY | `src/CowetaConnect.Tests/CowetaConnect.Tests.csproj` |
| MODIFY | `src/CowetaConnect.API/Middleware/ExceptionHandlingMiddleware.cs` |
| MODIFY | `src/CowetaConnect.API/Controllers/v1/AuthController.cs` |
| CREATE | `src/CowetaConnect.Infrastructure/Identity/ApplicationUser.cs` |
| CREATE | `src/CowetaConnect.Infrastructure/Identity/RefreshToken.cs` |
| CREATE | `src/CowetaConnect.Infrastructure/Identity/ApplicationUserConfiguration.cs` |
| CREATE | `src/CowetaConnect.Infrastructure/Identity/RefreshTokenConfiguration.cs` |
| CREATE | `src/CowetaConnect.Infrastructure/Services/JwtTokenService.cs` |
| CREATE | `src/CowetaConnect.Infrastructure/Services/AuthUserService.cs` |
| CREATE | `src/CowetaConnect.Infrastructure/Services/RefreshTokenRepository.cs` |
| CREATE | `src/CowetaConnect.Application/Auth/Interfaces/IAuthUserService.cs` |
| CREATE | `src/CowetaConnect.Application/Auth/Interfaces/IJwtTokenService.cs` |
| CREATE | `src/CowetaConnect.Application/Auth/Interfaces/IRefreshTokenRepository.cs` |
| CREATE | `src/CowetaConnect.Application/Auth/Dtos/RegisterDto.cs` |
| CREATE | `src/CowetaConnect.Application/Auth/Dtos/LoginDto.cs` |
| CREATE | `src/CowetaConnect.Application/Auth/Models/TokenResponse.cs` |
| CREATE | `src/CowetaConnect.Application/Auth/Validators/RegisterDtoValidator.cs` |
| CREATE | `src/CowetaConnect.Application/Auth/Validators/LoginDtoValidator.cs` |
| CREATE | `src/CowetaConnect.Application/Auth/Commands/RegisterCommand.cs` |
| CREATE | `src/CowetaConnect.Application/Auth/Commands/LoginCommand.cs` |
| CREATE | `src/CowetaConnect.Application/Auth/Commands/RefreshTokenCommand.cs` |
| CREATE | `src/CowetaConnect.Application/Auth/Commands/LogoutCommand.cs` |
| CREATE | `src/CowetaConnect.Domain/Exceptions/TooManyAttemptsException.cs` |
| CREATE | `src/CowetaConnect.Tests/Auth/JwtTokenServiceTests.cs` |
| CREATE | `src/CowetaConnect.Tests/Auth/RegisterDtoValidatorTests.cs` |
| CREATE | `src/CowetaConnect.Tests/Auth/LoginCommandHandlerTests.cs` |
| GENERATED | `src/CowetaConnect.Infrastructure/Migrations/` (new InitialCreate) |
