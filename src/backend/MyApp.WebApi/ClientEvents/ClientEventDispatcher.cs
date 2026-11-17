using System.Globalization;
using System.Reflection;
using Microsoft.Extensions.Logging;
using MyApp.Application.Logging;

namespace MyApp.WebApi.ClientEvents;

/// <summary>
/// Maps a log key to the log definition methods marked <see cref="ClientReportedAttribute"/> (operations.md 3.6).
/// The map is built once, by reflection over the definition classes of the <c>Logging</c> folder.
/// </summary>
public sealed class ClientEventDispatcher
{
    private readonly Dictionary<string, (Action<ILogger, string> Call, Type Owner)> _map = new(StringComparer.Ordinal);
    private readonly ILoggerFactory _loggerFactory;

    public ClientEventDispatcher(IEnumerable<Type> definitionTypes, ILoggerFactory loggerFactory)
    {
        _loggerFactory = loggerFactory;

        foreach (var type in definitionTypes)
        {
            foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.Static))
            {
                if (method.GetCustomAttribute<ClientReportedAttribute>() is null)
                    continue;

                var key = method.GetCustomAttribute<LoggerMessageAttribute>()?.EventName;
                var parameters = method.GetParameters();
                if (string.IsNullOrEmpty(key)
                    || parameters.Length != 2
                    || parameters[0].ParameterType != typeof(ILogger)
                    || parameters[1].ParameterType != typeof(string)
                    || method.ReturnType != typeof(void))
                {
                    throw new InvalidOperationException(
                        $"{type.Name}.{method.Name} is marked [ClientReported] but is not a keyed LoggerMessage method with the parameters (ILogger, string route).");
                }

                if (!_map.TryAdd(key, (method.CreateDelegate<Action<ILogger, string>>(), type)))
                    throw new InvalidOperationException($"Two [ClientReported] methods use the log key of {type.Name}.{method.Name}.");
            }
        }
    }

    /// <summary>The dispatcher over the definition classes of <c>MyApp.Application.Logging</c>.</summary>
    public static ClientEventDispatcher ForApplication(ILoggerFactory loggerFactory) =>
        new(typeof(ClientReportedAttribute).Assembly.GetTypes()
                .Where(t => t.Namespace == typeof(ClientReportedAttribute).Namespace && t.IsAbstract && t.IsSealed),
            loggerFactory);

    /// <summary>Writes the keyed entry. Returns false, writing nothing, when the key is unknown.</summary>
    public bool TryDispatch(string? key, string? route)
    {
        if (key is null || !_map.TryGetValue(key, out var entry))
            return false;

        entry.Call(_loggerFactory.CreateLogger(entry.Owner), NormalizeRoute(route));
        return true;
    }

    /// <summary>
    /// Query string removed; path segments that are GUIDs or numbers replaced by <c>{id}</c>; at most 200 characters.
    /// </summary>
    public static string NormalizeRoute(string? route)
    {
        if (string.IsNullOrWhiteSpace(route))
            return "/";

        var path = route.Trim();
        var cut = path.IndexOfAny(['?', '#']);
        if (cut >= 0)
            path = path[..cut];

        var segments = path.Split('/')
            .Select(s => Guid.TryParse(s, out _) || (s.Length > 0 && s.All(char.IsAsciiDigit)) ? "{id}" : s);
        var normalized = string.Join('/', segments);
        if (normalized.Length == 0)
            normalized = "/";

        return normalized.Length > 200 ? normalized[..200] : normalized;
    }
}
