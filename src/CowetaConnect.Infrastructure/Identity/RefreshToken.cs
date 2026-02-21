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
