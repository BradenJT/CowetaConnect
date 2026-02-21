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
