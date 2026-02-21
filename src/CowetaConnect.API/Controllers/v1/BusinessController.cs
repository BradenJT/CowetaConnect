using Asp.Versioning;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace CowetaConnect.API.Controllers.V1;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/businesses")]
[EnableRateLimiting("anonymous")]
public class BusinessController(IMediator mediator) : ControllerBase
{
    // Phase 2: GET /api/v1/businesses/search
    // Phase 2: GET /api/v1/businesses/{id}
    // Phase 2: POST /api/v1/businesses            [Authorize(Policy = "RequireOwner")]
    // Phase 2: PUT  /api/v1/businesses/{id}       [Authorize(Policy = "RequireOwner")]
    // Phase 2: DELETE /api/v1/businesses/{id}     [Authorize(Policy = "RequireOwner")]
    // Phase 2: GET /api/v1/businesses/map         (GeoJSON layer)
    // Phase 2: POST /api/v1/businesses/{id}/photos [EnableRateLimiting("file-upload")]
}
