using Microsoft.Extensions.FileSystemGlobbing;

namespace MyApp.Monitor.Sources;

/// <summary>Expands the glob patterns of <c>paths</c> (operations.md 6.4) relative to the content root.</summary>
public static class GlobExpander
{
    public static List<FileInfo> Expand(IEnumerable<string> patterns, string contentRoot)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var result = new List<FileInfo>();
        foreach (var pattern in patterns)
        {
            var (baseDir, rest) = Split(pattern, contentRoot);
            if (rest is null)
            {
                if (baseDir.Length == 0) continue;
                var single = new FileInfo(baseDir);
                if (single.Exists && seen.Add(single.FullName)) result.Add(single);
                continue;
            }
            if (!Directory.Exists(baseDir)) continue;
            var matcher = new Matcher(StringComparison.Ordinal);
            matcher.AddInclude(rest);
            foreach (var match in matcher.GetResultsInFullPath(baseDir))
                if (seen.Add(match)) result.Add(new FileInfo(match));
        }
        return result;
    }

    /// <summary>Splits into the longest wildcard-free directory and the remaining pattern (null for a plain file path).</summary>
    private static (string BaseDir, string? Pattern) Split(string pattern, string contentRoot)
    {
        var full = Path.IsPathRooted(pattern) ? pattern : Path.Combine(contentRoot, pattern);
        full = full.Replace('\\', '/');
        var segments = full.Split('/');
        var wildcard = Array.FindIndex(segments, s => s.Contains('*') || s.Contains('?'));
        if (wildcard < 0)
        {
            // a plain file path: return its full path as "base dir" with no pattern; Directory.Exists check is bypassed below
            var file = Path.GetFullPath(full);
            return (Directory.Exists(Path.GetDirectoryName(file)) ? file : "", null);
        }
        var baseDir = string.Join('/', segments.Take(wildcard));
        if (baseDir.Length == 0) baseDir = "/";
        return (Path.GetFullPath(baseDir), string.Join('/', segments.Skip(wildcard)));
    }
}
