using Asp.Versioning;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace CowetaConnect.API.Controllers.V1;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/events")]
[EnableRateLimiting("anonymous")]
public class EventController(IMediator mediator) : ControllerBase
{
    // Phase 2: GET  /api/v1/events
    // Phase 2: GET  /api/v1/events/{id}
    // Phase 2: POST /api/v1/events              [Authorize(Policy = "RequireOwner")]
    // Phase 2: PUT  /api/v1/events/{id}         [Authorize(Policy = "RequireOwner")]
    // Phase 2: DELETE /api/v1/events/{id}       [Authorize(Policy = "RequireOwner")]
    // Phase 2: POST /api/v1/events/{id}/rsvp    [Authorize]
    // Phase 2: GET  /api/v1/events/{id}/ical    (iCal feed)
}
