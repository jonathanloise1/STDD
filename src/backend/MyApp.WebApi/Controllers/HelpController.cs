using Microsoft.AspNetCore.Mvc;
using MyApp.Application.DTOs.Help;
using MyApp.Application.Help;
using MyApp.Application.Logging;
using MyApp.WebApi.ClientEvents;
using MyApp.WebApi.Filters;
using MyApp.WebApi.Middlewares;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace MyApp.WebApi.Controllers;

/// <summary>
/// The server side of the in-page assistant (models.md 7.4) and the documentation gaps (manual.md 7.4). The
/// bodies use the snake_case names of those documents. Retrieval needs no endpoint: it runs in the browser.
/// </summary>
[Route("api/help")]
public class HelpController(HelpAssistant assistant, HelpGapService gaps) : AuthenticatedControllerBase
{
    /// <summary>Content type of the answer stream: one JSON object per line.</summary>
    public const string StreamContentType = "application/x-ndjson";

    /// <summary>
    /// Tier settings, pack and model versions, server availability and device. Any signed-in user.
    /// </summary>
    // US-HELP-04, US-HELP-05
    [HttpGet("status")]
    public async Task<IActionResult> Status(CancellationToken ct) => Ok(await assistant.GetStatusAsync(ct));

    /// <summary>
    /// Streams the answer of the model on the server, one JSON object per line: <c>delta</c> events with the text as
    /// it is written, then one <c>done</c> (outcome <c>answered</c>, <c>refused</c> or <c>invalid</c>, with the cited
    /// positions) or one <c>error</c> (reason <c>timeout</c> or <c>unavailable</c>). A request that cannot start is a
    /// problem document: 400 malformed, 409 pack changed, 503 busy or not available, 504 too slow.
    /// </summary>
    // US-HELP-05
    [HttpPost("answer")]
    [RequestSizeLimit(16 * 1024)]
    public async Task<IActionResult> Answer([FromBody] HelpAnswerRequest request, CancellationToken ct)
    {
        HelpAnswerStream stream;
        try
        {
            stream = await assistant.StartAnswerAsync(request, ct);
        }
        catch (HelpAnswerException e)
        {
            return new ProblemResult(ProblemResponse.Create(StatusOf(e.Failure), e.Message));
        }

        await using (stream)
        {
            Response.StatusCode = StatusCodes.Status200OK;
            Response.ContentType = StreamContentType;
            Response.Headers.CacheControl = "no-store";
            Response.Headers["X-Accel-Buffering"] = "no";

            await foreach (var ev in stream.Events().WithCancellation(ct))
            {
                await Response.WriteAsync(ToLine(ev), ct);
                await Response.Body.FlushAsync(ct);
            }
        }

        return new EmptyResult();
    }

    /// <summary>
    /// Records a documentation gap. The question is masked and cut before it is stored; no user and no organization
    /// is stored. Answers with 204 also when recording is switched off.
    /// </summary>
    // US-HELP-04, US-HELP-05, US-HELP-06
    [HttpPost("gaps")]
    [RequestSizeLimit(16 * 1024)]
    public async Task<IActionResult> RecordGap([FromBody] HelpGapDto body, CancellationToken ct)
    {
        await gaps.RecordAsync(body, ClientEventDispatcher.NormalizeRoute(body.Route), ct);
        return NoContent();
    }

    /// <summary>
    /// The records of a period as a JSON array, oldest first. Admin of an organization only. <c>from</c> and
    /// <c>to</c> are dates; a <c>to</c> without a time covers the whole day.
    /// </summary>
    // US-HELP-07
    [HttpGet("gaps")]
    [ServiceUnavailable(HelpLog.Keys.HelpDownloadFailed)]
    public async Task<IActionResult> ListGaps([FromQuery] DateTime? from = null, [FromQuery] DateTime? to = null, CancellationToken ct = default) =>
        Ok(await gaps.ListAsync(AuthenticatedUserId, from, to, ct));

    private static int StatusOf(HelpAnswerFailure failure) => failure switch
    {
        HelpAnswerFailure.InvalidRequest => StatusCodes.Status400BadRequest,
        HelpAnswerFailure.PackChanged => StatusCodes.Status409Conflict,
        HelpAnswerFailure.Timeout => StatusCodes.Status504GatewayTimeout,
        _ => StatusCodes.Status503ServiceUnavailable
    };

    /// <summary>One event as a JSON line; absent values are omitted.</summary>
    public static string ToLine(HelpAnswerEvent e)
    {
        var json = new JObject { ["type"] = e.Type };
        if (e.Text is not null) json["text"] = e.Text;
        if (e.Outcome is not null) json["outcome"] = e.Outcome;
        if (e.Cited is not null) json["cited"] = new JArray(e.Cited);
        if (e.Body is not null) json["body"] = e.Body;
        if (e.Model is not null) json["model"] = e.Model;
        if (e.Device is not null) json["device"] = e.Device;
        if (e.Reason is not null) json["reason"] = e.Reason;
        return json.ToString(Formatting.None) + "\n";
    }
}
