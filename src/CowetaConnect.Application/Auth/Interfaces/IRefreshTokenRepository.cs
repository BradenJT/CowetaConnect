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
