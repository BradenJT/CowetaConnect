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
