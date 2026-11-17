using System.Runtime.CompilerServices;
using Microsoft.Playwright;

namespace MyApp.E2E.Infrastructure;

/// <summary>
/// The fault switch of development.md 16.2: makes the operations named by their log key answer
/// "the service does not answer" for one browser context while the backend keeps serving the others.
///
/// Test classes never call this class and never name a route or a header: they call
/// <c>ServiceDoesNotAnswer()</c> of the base class, which takes the log key from the test case file.
/// The header is added by the one route that <see cref="Auth.E2EImpersonation"/> installs on the context,
/// which asks <see cref="HeaderValueFor"/> for every request.
/// </summary>
public static class FaultSwitch
{
    private sealed class Keys
    {
        public string[] Values = Array.Empty<string>();
    }

    private static readonly ConditionalWeakTable<IBrowserContext, Keys> Active = new();

    /// <summary>Makes the operations with these log keys fail for every request of the context.</summary>
    public static void Enable(IBrowserContext context, params string[] logKeys)
    {
        if (logKeys.Length == 0)
            throw new ArgumentException("At least one log key is needed.", nameof(logKeys));

        Active.GetOrCreateValue(context).Values = logKeys;
    }

    /// <summary>Lets the operations answer again for the context.</summary>
    public static void Disable(IBrowserContext context) => Active.GetOrCreateValue(context).Values = Array.Empty<string>();

    /// <summary>The value of the fault header for the context, or null when no fault is requested.</summary>
    public static string? HeaderValueFor(IBrowserContext context) =>
        Active.TryGetValue(context, out var keys) && keys.Values.Length > 0
            ? string.Join(",", keys.Values)
            : null;
}
