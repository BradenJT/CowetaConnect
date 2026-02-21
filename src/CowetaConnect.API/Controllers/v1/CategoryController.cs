using Asp.Versioning;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace CowetaConnect.API.Controllers.V1;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/categories")]
[EnableRateLimiting("anonymous")]
public class CategoryController(IMediator mediator) : ControllerBase
{
    // Phase 2: GET  /api/v1/categories
    // Phase 2: POST /api/v1/categories    [Authorize(Policy = "RequireAdmin")]
    // Phase 2: PUT  /api/v1/categories/{id} [Authorize(Policy = "RequireAdmin")]
    // Phase 2: DELETE /api/v1/categories/{id} [Authorize(Policy = "RequireAdmin")]
}
