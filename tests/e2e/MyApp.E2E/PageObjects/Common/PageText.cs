using System.Text.Json;
using System.Text.RegularExpressions;
using MyApp.E2E.Infrastructure;

namespace MyApp.E2E.PageObjects.Common;

/// <summary>
/// The language the page objects find labels in. Every run of the tests is in English, and nothing changes
/// for them. A run of the manual capture in Italian (<c>E2E__ManualLanguage=it</c>, manual.md 4.3) sets
/// <see cref="Language"/> to <c>it</c>: a label given in English is then found in English or in any Italian
/// text the frontend translation files hold for that English text, so the same steps drive the Italian screens.
/// </summary>
public static class PageText
{
    private static Dictionary<string, HashSet<string>>? _italian;

    /// <summary>"en" (the default) or "it".</summary>
    public static string Language { get; set; } = "en";

    /// <summary>True while labels are also looked for in Italian.</summary>
    public static bool IsItalian => Language == "it";

    /// <summary>A pattern that matches the whole label: the English text, and in an Italian run its Italian texts.</summary>
    public static Regex Exact(string english)
    {
        var options = new List<string> { english };
        if (IsItalian && Italian().TryGetValue(english, out var italian))
            options.AddRange(italian);
        return new Regex("^(?:" + string.Join("|", options.Select(Regex.Escape)) + ")$");
    }

    /// <summary>
    /// Like <see cref="Exact(string)"/> for a label followed by a fixed tail, such as a title with the name of the
    /// organization: "Team Management" and " - MyApp Dev Org" match "Team Management - MyApp Dev Org" or its Italian form.
    /// </summary>
    public static Regex ExactWithTail(string english, string tail)
    {
        var options = new List<string> { english };
        if (IsItalian && Italian().TryGetValue(english, out var italian))
            options.AddRange(italian);
        return new Regex("^(?:" + string.Join("|", options.Select(Regex.Escape)) + ")" + Regex.Escape(tail) + "$");
    }

    private static Dictionary<string, HashSet<string>> Italian()
    {
        if (_italian is not null)
            return _italian;

        var map = new Dictionary<string, HashSet<string>>();
        var folder = Path.Combine(TestConfiguration.RepoRoot, "src", "frontend", "public", "locales");
        var enFolder = Path.Combine(folder, "en");
        if (Directory.Exists(enFolder))
        {
            foreach (var enFile in Directory.GetFiles(enFolder, "*.json"))
            {
                var itFile = Path.Combine(folder, "it", Path.GetFileName(enFile));
                if (!File.Exists(itFile))
                    continue;
                using var en = JsonDocument.Parse(File.ReadAllText(enFile));
                using var it = JsonDocument.Parse(File.ReadAllText(itFile));
                var itValues = Flatten(it.RootElement).ToDictionary(p => p.Key, p => p.Value);
                foreach (var (key, english) in Flatten(en.RootElement))
                {
                    if (!itValues.TryGetValue(key, out var italian) || italian == english)
                        continue;
                    if (!map.TryGetValue(english, out var set))
                        map[english] = set = new HashSet<string>();
                    set.Add(italian);
                }
            }
        }
        return _italian = map;
    }

    private static IEnumerable<KeyValuePair<string, string>> Flatten(JsonElement element, string prefix = "")
    {
        foreach (var property in element.EnumerateObject())
        {
            var key = prefix + property.Name;
            if (property.Value.ValueKind == JsonValueKind.Object)
            {
                foreach (var inner in Flatten(property.Value, key + "."))
                    yield return inner;
            }
            else if (property.Value.ValueKind == JsonValueKind.String)
            {
                yield return new(key, property.Value.GetString()!);
            }
        }
    }
}
