using System.Text.Json;

namespace MyApp.E2E.Infrastructure;

/// <summary>
/// The files the help surfaces need (manual.md 5.1): the manifest, and a manual bundle and a help pack for each
/// language. They are generated, git-ignored and absent until the owner approves the build, so the help tests
/// check for them first and say which command to run instead of timing out. Test classes never read them: the
/// base class of the area does.
/// </summary>
public static class HelpPackFiles
{
    /// <summary>The command that builds them, run in <c>src/frontend</c>.</summary>
    public const string BuildCommand = "npm run help:build";

    /// <summary>The folder the frontend serves them from.</summary>
    public static string Folder => Path.Combine(TestConfiguration.FrontendPath, "public", "help");

    /// <summary>Whether <c>docs/manual</c> holds at least one page (<c>bench.md</c> is not a page).</summary>
    public static bool ManualHasPages()
    {
        var manual = Path.Combine(TestConfiguration.RepoRoot, "docs", "manual");
        return Directory.Exists(manual)
               && Directory.EnumerateFiles(manual, "*.md", SearchOption.AllDirectories)
                   .Any(file => !string.Equals(Path.GetFileName(file), "bench.md", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>The files that do not exist yet, relative to <see cref="Folder"/>.</summary>
    public static IReadOnlyList<string> MissingFiles() =>
        new[] { "manifest.json", "en/manual.json", "en/pack.json", "it/manual.json", "it/pack.json" }
            .Where(file => !File.Exists(Path.Combine(Folder, file)))
            .ToList();

    /// <summary>The sentence the assistant answers with when it declines, as the pack of a language holds it.</summary>
    public static string RefusalSentence(string lang)
    {
        using var pack = JsonDocument.Parse(File.ReadAllText(Path.Combine(Folder, lang, "pack.json")));
        return pack.RootElement
            .GetProperty("assistant").GetProperty("protocol").GetProperty("content")
            .GetProperty("languages").GetProperty(lang).GetProperty("refusal").GetString()
            ?? throw new InvalidOperationException($"The {lang} pack has no refusal sentence.");
    }

    /// <summary>The id of the section a pack gives a title ("Create a task - Steps"), so a test finds it by what the user reads.</summary>
    public static string SectionId(string lang, string sectionTitle)
    {
        using var pack = JsonDocument.Parse(File.ReadAllText(Path.Combine(Folder, lang, "pack.json")));
        foreach (var section in pack.RootElement.GetProperty("sections").EnumerateArray())
            if (section.GetProperty("title").GetString() == sectionTitle)
                return section.GetProperty("id").GetString()!;
        throw new InvalidOperationException($"The {lang} pack has no section titled \"{sectionTitle}\".");
    }
}
