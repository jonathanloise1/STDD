using System.Globalization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace MyApp.Infrastructure.Services;

/// <summary>
/// Builds the clock of the application. <c>MyApp:Testing:FixedUtcNow</c> (an ISO 8601 instant) sets the
/// moment the clock starts at; it is honoured only in the Development environment, so a screenshot or test
/// run sees the same dates every time.
/// </summary>
public static class ClockFactory
{
    public const string FixedUtcNowKey = "MyApp:Testing:FixedUtcNow";

    public static TimeProvider Create(IConfiguration configuration, IHostEnvironment environment)
    {
        if (environment.IsDevelopment()
            && DateTimeOffset.TryParse(configuration[FixedUtcNowKey], CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var start))
        {
            return new StartedAtTimeProvider(start);
        }

        return TimeProvider.System;
    }
}

/// <summary>A clock that starts at a given instant and then runs at the speed of the real one.</summary>
public sealed class StartedAtTimeProvider : TimeProvider
{
    private readonly DateTimeOffset _start;
    private readonly DateTimeOffset _realStart = DateTimeOffset.UtcNow;

    public StartedAtTimeProvider(DateTimeOffset start) => _start = start;

    public override DateTimeOffset GetUtcNow() => _start + (DateTimeOffset.UtcNow - _realStart);
}
