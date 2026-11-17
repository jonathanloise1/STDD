using System.Diagnostics;

namespace MyApp.WebApi.Middlewares;

/// <summary>
/// Guarantees that every request runs inside an <see cref="Activity"/> that continues the caller's W3C
/// <c>traceparent</c> (operations.md 4.1). ASP.NET Core already does this when its hosting diagnostics create
/// an activity; this covers the case where nothing listens, so the trace id is always available to the log file,
/// the audit log and the error response. It is the first middleware.
/// </summary>
public class TraceContextMiddleware(RequestDelegate next)
{
    public const string TraceParentHeader = "traceparent";
    public const string TraceStateHeader = "tracestate";

    public async Task InvokeAsync(HttpContext context)
    {
        if (Activity.Current is not null)
        {
            await next(context);
            return;
        }

        var activity = new Activity("MyApp.Request");
        activity.SetIdFormat(ActivityIdFormat.W3C);
        if (context.Request.Headers.TryGetValue(TraceParentHeader, out var traceParent)
            && !string.IsNullOrWhiteSpace(traceParent))
        {
            activity.SetParentId(traceParent.ToString());
            if (context.Request.Headers.TryGetValue(TraceStateHeader, out var traceState))
                activity.TraceStateString = traceState.ToString();
        }

        activity.Start();
        try
        {
            await next(context);
        }
        finally
        {
            activity.Stop();
        }
    }
}
