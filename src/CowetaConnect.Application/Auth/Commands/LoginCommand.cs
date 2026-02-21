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
