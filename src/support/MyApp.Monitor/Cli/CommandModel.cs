using Microsoft.Extensions.Configuration;

namespace MyApp.Monitor.Cli;

public static class ExitCodes
{
    public const int Ok = 0;
    /// <summary>The command ran and found a problem (failed checks, a tool call that did not succeed).</summary>
    public const int Failed = 1;
    public const int Usage = 2;
    public const int NotAvailable = 3;
}

/// <summary>Positional words and options of one command line (<c>--name value</c> or <c>--flag</c>).</summary>
public sealed class CommandArgs
{
    public List<string> Positional { get; } = [];
    public Dictionary<string, string?> Options { get; } = new(StringComparer.Ordinal);

    public string? Get(string name) => Options.GetValueOrDefault(name);
    public bool Has(string name) => Options.ContainsKey(name);

    /// <summary>Splits <paramref name="args"/>; <paramref name="spec"/> maps each known option to whether it takes a value.</summary>
    public static CommandArgs Parse(IEnumerable<string> args, IReadOnlyDictionary<string, bool> spec)
    {
        var result = new CommandArgs();
        var list = args.ToList();
        for (var i = 0; i < list.Count; i++)
        {
            var a = list[i];
            if (!a.StartsWith("--", StringComparison.Ordinal)) { result.Positional.Add(a); continue; }
            var name = a[2..];
            string? inlineValue = null;
            var eq = name.IndexOf('=');
            if (eq >= 0) { inlineValue = name[(eq + 1)..]; name = name[..eq]; }
            if (!spec.TryGetValue(name, out var takesValue)) throw new UsageException($"unknown option --{name}");
            if (takesValue)
            {
                if (inlineValue is null)
                {
                    if (i + 1 >= list.Count) throw new UsageException($"option --{name} needs a value");
                    inlineValue = list[++i];
                }
                result.Options[name] = inlineValue;
            }
            else
            {
                if (inlineValue is not null) throw new UsageException($"option --{name} takes no value");
                result.Options[name] = null;
            }
        }
        return result;
    }
}

public sealed class UsageException(string message) : MonitorException(message);

public sealed record CommandContext(TextWriter Out, TextWriter Error, IServiceProvider Services, MonitorOptions Options, IConfiguration Configuration, TimeProvider Time);

/// <summary>One command of the monitor (operations.md 14.4). Later passes register <c>run</c> the same way.</summary>
public interface ICommandHandler
{
    /// <summary>One or two words: <c>analyse</c>, <c>pack build</c>.</summary>
    string Name { get; }
    string Usage { get; }
    IReadOnlyDictionary<string, bool> Options { get; }
    Task<int> RunAsync(CommandArgs args, CommandContext context, CancellationToken ct);
}

/// <summary>A command that belongs to a later pass: prints a clear message and exits non-zero.</summary>
public sealed class NotAvailableCommand(string name, string usage) : ICommandHandler
{
    public string Name => name;
    public string Usage => usage;
    public IReadOnlyDictionary<string, bool> Options { get; } = new Dictionary<string, bool>
    {
        ["profile"] = true, ["pack"] = true, ["pack-version"] = true, ["version"] = true, ["out"] = true, ["args"] = true,
        ["incident"] = true, ["world"] = true, ["input"] = true, ["explain"] = false, ["dry-run"] = false,
    };

    public Task<int> RunAsync(CommandArgs args, CommandContext context, CancellationToken ct)
    {
        context.Error.WriteLine($"The command '{name}' is not available in this build.");
        return Task.FromResult(ExitCodes.NotAvailable);
    }
}

public sealed class CommandDispatcher(IEnumerable<ICommandHandler> handlers)
{
    private readonly List<ICommandHandler> _handlers = handlers.OrderByDescending(h => h.Name.Split(' ').Length).ToList();

    public string HelpText() =>
        "Usage: dotnet run --project src/support/MyApp.Monitor -- {command}\n\n" +
        string.Join('\n', _handlers.OrderBy(h => h.Name, StringComparer.Ordinal).Select(h => "  " + h.Usage));

    public async Task<int> RunAsync(string[] args, CommandContext context, CancellationToken ct)
    {
        if (args.Length == 0 || args[0] is "-h" or "--help" or "help")
        {
            context.Out.WriteLine(HelpText());
            return args.Length == 0 ? ExitCodes.Usage : ExitCodes.Ok;
        }

        foreach (var handler in _handlers)
        {
            var words = handler.Name.Split(' ');
            if (args.Length < words.Length || !words.Select((w, i) => args[i] == w).All(x => x)) continue;
            try
            {
                var parsed = CommandArgs.Parse(args.Skip(words.Length), handler.Options);
                return await handler.RunAsync(parsed, context, ct).ConfigureAwait(false);
            }
            catch (UsageException ex)
            {
                context.Error.WriteLine($"{ex.Message}\nUsage: {handler.Usage}");
                return ExitCodes.Usage;
            }
            catch (MonitorException ex)
            {
                context.Error.WriteLine(ex.Message);
                return ExitCodes.Failed;
            }
        }

        context.Error.WriteLine($"Unknown command '{string.Join(' ', args.TakeWhile(a => !a.StartsWith("--", StringComparison.Ordinal)))}'.\n\n{HelpText()}");
        return ExitCodes.Usage;
    }
}
