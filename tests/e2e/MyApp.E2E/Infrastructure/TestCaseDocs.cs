using System.Text.RegularExpressions;
using NUnit.Framework;

namespace MyApp.E2E.Infrastructure;

/// <summary>
/// Reads facts from the test case file of the running test, so a test class never repeats them.
/// The test case is the one named by the class's <c>TestCase</c> tag (development.md 11.1).
/// </summary>
public static class TestCaseDocs
{
    private static readonly Regex LogKeyRow = new(@"^\|\s*Log key\s*\|\s*`(?<key>[^`]+)`\s*\|", RegexOptions.Multiline | RegexOptions.Compiled);

    /// <summary>The id of the test case the class is tagged with.</summary>
    public static string TestCaseIdOf(Type testClass)
    {
        var id = testClass
            .GetCustomAttributes(typeof(PropertyAttribute), inherit: true)
            .Cast<PropertyAttribute>()
            .SelectMany(a => a.Properties.Keys.Where(k => k == "TestCase").Select(k => a.Properties.Get(k) as string))
            .FirstOrDefault(v => !string.IsNullOrEmpty(v));

        return id ?? throw new InvalidOperationException(
            $"{testClass.Name} has no [Property(\"TestCase\", ...)] tag, so the test case file cannot be found.");
    }

    /// <summary>The Log key of the Observable trace of the test case (development.md 8.5).</summary>
    public static string LogKeyOf(string testCaseId)
    {
        var areas = Path.Combine(TestConfiguration.RepoRoot, "docs", "areas");
        var file = Directory.Exists(areas)
            ? Directory.GetFiles(areas, testCaseId + ".md", SearchOption.AllDirectories).FirstOrDefault()
            : null;
        if (file is null)
            throw new InvalidOperationException($"No test case file {testCaseId}.md under {areas}.");

        var match = LogKeyRow.Match(File.ReadAllText(file));
        return match.Success
            ? match.Groups["key"].Value
            : throw new InvalidOperationException(
                $"{testCaseId} has no Observable trace with a Log key, so no operation can be made to fail for it.");
    }
}
