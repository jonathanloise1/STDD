namespace MyApp.WebApi.Filters;

/// <summary>
/// Development-only fault switch for automated browser tests: it makes one chosen operation behave as if
/// the service did not answer, so the "service did not answer" path can be exercised end to end.
/// <para>
/// It is off by default and can never be on outside the Development environment. To use it, start the
/// Development backend with <c>MyApp:Testing:FaultInjection:Enabled=true</c> (environment variable
/// <c>MyApp__Testing__FaultInjection__Enabled=true</c>), then send the header
/// <c>X-MyApp-Fault: {log key}</c> (for example <c>task.create.unavailable</c>; several keys separated by
/// commas) on the requests of the test. Only an operation whose "unavailable" log key is named fails.
/// </para>
/// </summary>
public sealed class FaultSwitch(IConfiguration configuration, IHostEnvironment environment)
{
    public const string HeaderName = "X-MyApp-Fault";
    public const string EnabledKey = "MyApp:Testing:FaultInjection:Enabled";

    /// <summary>True only in the Development environment with the setting turned on.</summary>
    public bool Enabled => environment.IsDevelopment() && configuration.GetValue<bool>(EnabledKey);

    /// <summary>True when the switch is on and the request asks the operation of <paramref name="key"/> to fail.</summary>
    public bool IsRequested(HttpContext context, string key)
    {
        if (!Enabled || !context.Request.Headers.TryGetValue(HeaderName, out var values))
            return false;

        return values
            .SelectMany(v => (v ?? string.Empty).Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
            .Contains(key, StringComparer.Ordinal);
    }
}

/// <summary>The failure the fault switch raises in place of the operation.</summary>
public sealed class SimulatedOutageException(string key)
    : Exception($"The fault switch made the operation of '{key}' fail.");
