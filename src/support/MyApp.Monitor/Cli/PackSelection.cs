using MyApp.Monitor.Pack;

namespace MyApp.Monitor.Cli;

public static class RepositoryLocator
{
    public const string Marker = "docs/guidelines/operations.md";

    public static string Find(string? explicitRoot)
    {
        if (!string.IsNullOrWhiteSpace(explicitRoot))
        {
            var root = Path.GetFullPath(explicitRoot);
            if (!File.Exists(Path.Combine(root, Marker.Replace('/', Path.DirectorySeparatorChar))))
                throw new MonitorException($"{root} is not a repository root of this template ({Marker} not found)");
            return root;
        }
        for (var dir = new DirectoryInfo(Directory.GetCurrentDirectory()); dir is not null; dir = dir.Parent)
            if (File.Exists(Path.Combine(dir.FullName, Marker.Replace('/', Path.DirectorySeparatorChar)))) return dir.FullName;
        throw new MonitorException("not inside the repository: use --pack, Monitor:Pack:Path or --repo");
    }
}

/// <summary>
/// The pack a command works with (operations.md 13.3): <c>--pack</c>, else <c>Monitor:Pack:Path</c>; inside the repository
/// without either, the monitor builds <c>artifacts/ops-pack/local</c> first.
/// </summary>
public static class PackSelection
{
    public static string ProfileName(string? argument, MonitorOptions options, out string? profileFile)
    {
        profileFile = null;
        var value = argument ?? options.Profile;
        if (string.IsNullOrWhiteSpace(value)) return "onprem";
        if (value.Contains('/') || value.Contains('\\') || value.EndsWith(".yaml", StringComparison.OrdinalIgnoreCase))
        {
            profileFile = Path.GetFullPath(value);
            return Path.GetFileNameWithoutExtension(value);
        }
        return value;
    }

    public static OperationsPack Resolve(CommandArgs args, CommandContext context)
    {
        var path = args.Get("pack") ?? context.Options.Pack.Path;
        if (!string.IsNullOrWhiteSpace(path)) return PackLoader.Load(path);

        var repo = RepositoryLocator.Find(args.Get("repo") ?? context.Options.RepositoryRoot);
        var profile = ProfileName(args.Get("profile"), context.Options, out var file);
        var build = new PackBuilder(context.Time).Build(new PackBuildRequest(repo, profile, null, Path.Combine(repo, "artifacts", "ops-pack"))
        {
            DirectoryName = "local", WriteZip = false, ProfileFile = file,
        });
        if (!build.Succeeded)
        {
            foreach (var f in build.Findings) context.Error.WriteLine(f.ToString());
            throw new PackException($"the local operations pack could not be built ({build.Findings.Count} problem(s))", build.Findings);
        }
        return PackLoader.Load(build.PackDirectory!);
    }
}
