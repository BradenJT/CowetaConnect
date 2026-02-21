using Asp.Versioning;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace CowetaConnect.API.Controllers.V1;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/dashboard")]
[Authorize(Policy = "RequireOwner")]
[EnableRateLimiting("authenticated")]
public class DashboardController(IMediator mediator) : ControllerBase
{
    // Phase 2: GET /api/v1/dashboard/overview
    // Phase 3: GET /api/v1/dashboard/leads          (ML lead insights)
    // Phase 3: GET /api/v1/dashboard/analytics      (search analytics summary)
}
