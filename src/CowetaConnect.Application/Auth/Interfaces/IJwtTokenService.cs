// src/CowetaConnect.Application/Auth/Interfaces/IJwtTokenService.cs
namespace CowetaConnect.Application.Auth.Interfaces;

public interface IJwtTokenService
{
    /// <summary>
    /// Generates a signed RS256 JWT access token valid for 15 minutes.
    /// Claims: sub, email, role, jti, iat, exp, iss, aud.
    /// </summary>
    string GenerateAccessToken(string userId, string email, string role);

    /// <summary>
    /// Generates a cryptographically random 64-byte token encoded as Base64.
    /// This is the raw token — hash it with HashToken before storing.
    /// </summary>
    string GenerateRefreshToken();

    /// <summary>Returns SHA-256 hash of the token, encoded as Base64.</summary>
    string HashToken(string token);
}
