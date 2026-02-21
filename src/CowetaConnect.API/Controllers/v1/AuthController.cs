using Asp.Versioning;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace CowetaConnect.API.Controllers.V1;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/auth")]
[EnableRateLimiting("auth-endpoints")]
public class AuthController(IMediator mediator) : ControllerBase
{
    // Phase 2: POST /api/v1/auth/register
    // Phase 2: POST /api/v1/auth/login
    // Phase 2: POST /api/v1/auth/refresh
    // Phase 2: POST /api/v1/auth/logout
    // Phase 2: POST /api/v1/auth/google          (Google OAuth exchange)
}
