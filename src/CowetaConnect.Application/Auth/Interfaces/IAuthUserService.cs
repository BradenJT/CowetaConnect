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

    /// <summary>Returns user info by ID. Returns null if not found.</summary>
    Task<AuthUserResult?> GetByIdAsync(string userId, CancellationToken ct = default);

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
