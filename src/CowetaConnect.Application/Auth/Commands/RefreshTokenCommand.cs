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
