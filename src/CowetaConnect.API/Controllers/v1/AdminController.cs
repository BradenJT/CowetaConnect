using Asp.Versioning;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace CowetaConnect.API.Controllers.V1;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/admin")]
[Authorize(Policy = "RequireAdmin")]
[EnableRateLimiting("admin-endpoints")]
public class AdminController(IMediator mediator) : ControllerBase
{
    // Phase 2: GET  /api/v1/admin/businesses          (pending verification queue)
    // Phase 2: POST /api/v1/admin/businesses/{id}/verify
    // Phase 2: POST /api/v1/admin/businesses/{id}/reject
    // Phase 2: GET  /api/v1/admin/platform-analytics
}
