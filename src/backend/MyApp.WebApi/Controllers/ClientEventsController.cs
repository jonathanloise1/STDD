using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using MyApp.WebApi.ClientEvents;

namespace MyApp.WebApi.Controllers;

/// <summary>
/// Receives the traces a user meets in the browser only (operations.md 3.6) and logs them through the
/// log definition classes, so the log key still has exactly one literal.
/// </summary>
[ApiController]
[AllowAnonymous]
[EnableRateLimiting("DefaultPerUser")]
[Route("api/client-events")]
public class ClientEventsController(ClientEventDispatcher dispatcher) : ControllerBase
{
    /// <summary>Logs the client-reported event named by the key. An unknown key returns 400 and writes nothing.</summary>
    [HttpPost]
    public IActionResult Report([FromBody] ClientEventRequest request)
    {
        if (!dispatcher.TryDispatch(request.Key, request.Route))
            throw new System.ComponentModel.DataAnnotations.ValidationException("Unknown client event.");

        return NoContent();
    }
}

/// <summary>Body of <c>POST /api/client-events</c>.</summary>
public class ClientEventRequest
{
    public string? Key { get; set; }
    public string? Route { get; set; }
}
