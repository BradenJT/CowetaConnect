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
