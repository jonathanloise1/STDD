using MyApp.Monitor.Pack;

namespace MyApp.Monitor.Cli;

/// <summary><c>pack build --profile p [--version v] [--out dir]</c> (operations.md 13.2).</summary>
public sealed class PackBuildCommand : ICommandHandler
{
    public string Name => "pack build";
    public string Usage => "pack build --profile {name} [--version {v}] [--out {dir}] [--repo {dir}]";
    public IReadOnlyDictionary<string, bool> Options { get; } = new Dictionary<string, bool>
    {
        ["profile"] = true, ["version"] = true, ["out"] = true, ["repo"] = true,
    };

    public Task<int> RunAsync(CommandArgs args, CommandContext context, CancellationToken ct)
    {
        var profile = args.Get("profile") ?? throw new UsageException("--profile is required");
        var repo = RepositoryLocator.Find(args.Get("repo") ?? context.Options.RepositoryRoot);
        var outDir = args.Get("out") is { } o ? Path.GetFullPath(o) : Path.Combine(repo, "artifacts", "ops-pack");

        var result = new PackBuilder(context.Time).Build(new PackBuildRequest(repo, profile, args.Get("version"), outDir));
        if (!result.Succeeded)
        {
            context.Error.WriteLine($"pack build failed with {result.Findings.Count} problem(s):");
            foreach (var f in result.Findings) context.Out.WriteLine(f.ToString());
            return Task.FromResult(ExitCodes.Failed);
        }
        context.Out.WriteLine($"Pack built: {result.PackDirectory}");
        context.Out.WriteLine($"Zip: {result.ZipPath}");
        context.Out.WriteLine($"Version {result.Manifest!.PackVersion}, profile {result.Manifest.Profile}, {result.SignatureCount} signatures, {result.Manifest.Files.Count} files");
        return Task.FromResult(ExitCodes.Ok);
    }
}

/// <summary><c>pack verify --pack path</c>: the section 16 checks on a built pack.</summary>
public sealed class PackVerifyCommand : ICommandHandler
{
    public string Name => "pack verify";
    public string Usage => "pack verify --pack {path}";
    public IReadOnlyDictionary<string, bool> Options { get; } = new Dictionary<string, bool> { ["pack"] = true };

    public Task<int> RunAsync(CommandArgs args, CommandContext context, CancellationToken ct)
    {
        var path = args.Get("pack") ?? throw new UsageException("--pack is required");
        var files = PackFiles.Read(path);
        var result = PackVerifier.Verify(files, new PackVerifier.Options(DateOnly.FromDateTime(context.Time.GetUtcNow().UtcDateTime)) { Location = path });
        if (result.Findings.Count > 0)
        {
            context.Error.WriteLine($"pack verify failed with {result.Findings.Count} problem(s):");
            foreach (var f in result.Findings) context.Out.WriteLine(f.ToString());
            return Task.FromResult(ExitCodes.Failed);
        }
        var pack = result.Pack!;
        context.Out.WriteLine($"Pack OK: version {pack.Version}, profile {pack.Manifest.Profile}, {pack.Signatures.Count} signatures, {pack.Manifest.Files.Count} files");
        return Task.FromResult(ExitCodes.Ok);
    }
}
