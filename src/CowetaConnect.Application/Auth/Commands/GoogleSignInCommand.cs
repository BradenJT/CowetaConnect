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
