// src/CowetaConnect.Infrastructure/Services/AuthUserService.cs
using CowetaConnect.Application.Auth.Interfaces;
using CowetaConnect.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace CowetaConnect.Infrastructure.Services;

public sealed class AuthUserService(
    UserManager<ApplicationUser> userManager,
    IConnectionMultiplexer redis,
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
            UserName = email,
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

    public async Task<AuthUserResult?> GetByIdAsync(string userId, CancellationToken ct = default)
    {
        var user = await userManager.FindByIdAsync(userId);
        if (user is null) return null;
        return new AuthUserResult(true, user.Id.ToString(), user.Email, user.Role);
    }

    public async Task<long> RecordFailedLoginAsync(string ipAddress, CancellationToken ct = default)
    {
        var cache = redis.GetDatabase();
        var key = $"{FailedLoginKeyPrefix}{ipAddress}";
        var count = await cache.StringIncrementAsync(key);
        await cache.KeyExpireAsync(key, FailedLoginTtl);
        return count;
    }

    public async Task<long> GetFailedLoginCountAsync(string ipAddress, CancellationToken ct = default)
    {
        var cache = redis.GetDatabase();
        var key = $"{FailedLoginKeyPrefix}{ipAddress}";
        var value = await cache.StringGetAsync(key);
        return value.HasValue ? (long)value : 0;
    }

    public async Task ClearFailedLoginsAsync(string ipAddress, CancellationToken ct = default)
    {
        var cache = redis.GetDatabase();
        await cache.KeyDeleteAsync($"{FailedLoginKeyPrefix}{ipAddress}");
    }

    public async Task UpdateLastLoginAsync(string userId, CancellationToken ct = default)
    {
        var user = await userManager.FindByIdAsync(userId);
        if (user is null) return;

        user.LastLogin = DateTimeOffset.UtcNow;
        await userManager.UpdateAsync(user);
    }

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
}
