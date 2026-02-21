// src/CowetaConnect.API/Controllers/v1/AuthController.cs
using Asp.Versioning;
using CowetaConnect.Application.Auth.Commands;
using CowetaConnect.Application.Auth.Dtos;
using CowetaConnect.Application.Auth.Models;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace CowetaConnect.API.Controllers.V1;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/auth")]
[EnableRateLimiting("auth-endpoints")]
public class AuthController(IMediator mediator) : ControllerBase
{
    private const string RefreshCookieName = "refresh_token";

    // POST /api/v1/auth/register
    [HttpPost("register")]
    [ProducesResponseType(typeof(TokenResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Register([FromBody] RegisterDto dto, CancellationToken ct)
    {
        var (token, rawRefresh) = await mediator.Send(new RegisterCommand(dto), ct);
        SetRefreshCookie(rawRefresh);
        return StatusCode(StatusCodes.Status201Created, token);
    }

    // POST /api/v1/auth/login
    [HttpPost("login")]
    [ProducesResponseType(typeof(TokenResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> Login([FromBody] LoginDto dto, CancellationToken ct)
    {
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        var (token, rawRefresh) = await mediator.Send(new LoginCommand(dto, ip), ct);
        SetRefreshCookie(rawRefresh);
        return Ok(token);
    }

    // POST /api/v1/auth/refresh
    [HttpPost("refresh")]
    [ProducesResponseType(typeof(TokenResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Refresh(CancellationToken ct)
    {
        var rawRefresh = Request.Cookies[RefreshCookieName];
        if (string.IsNullOrEmpty(rawRefresh))
            return Forbid();

        var (token, newRawRefresh) = await mediator.Send(new RefreshTokenCommand(rawRefresh), ct);
        SetRefreshCookie(newRawRefresh);
        return Ok(token);
    }

    // POST /api/v1/auth/logout
    [HttpPost("logout")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Logout(CancellationToken ct)
    {
        var rawRefresh = Request.Cookies[RefreshCookieName];
        await mediator.Send(new LogoutCommand(rawRefresh), ct);

        Response.Cookies.Delete(RefreshCookieName, new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Strict
        });

        return NoContent();
    }

    private void SetRefreshCookie(string rawRefreshToken)
    {
        Response.Cookies.Append(RefreshCookieName, rawRefreshToken, new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Strict,
            Expires = DateTimeOffset.UtcNow.AddDays(7),
            Path = "/"
        });
    }
}
