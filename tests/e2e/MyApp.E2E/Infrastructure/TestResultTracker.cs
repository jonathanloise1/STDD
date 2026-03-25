using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace MyApp.E2E.Infrastructure;

/// <summary>
/// Thread-safe tracker that collects individual test results
/// and produces a formatted summary at the end of the test run.
/// </summary>
public sealed class TestResultTracker
{
    public static TestResultTracker Instance { get; } = new();

    private readonly ConcurrentBag<TestResult> _results = new();
    private readonly Stopwatch _stopwatch = new();

    private TestResultTracker() { }

    /// <summary>Start the global timer (call in OneTimeSetUp).</summary>
    public void StartTimer() => _stopwatch.Start();

    /// <summary>Record the outcome of a single test.</summary>
    public void Record(string fullName, string status, string? errorMessage = null, string? screenshotPath = null)
    {
        _results.Add(new TestResult
        {
            FullName = fullName,
            Status = status,
            ErrorMessage = errorMessage,
            ScreenshotPath = screenshotPath,
            Timestamp = DateTime.UtcNow
        });
    }

    /// <summary>
    /// Build and return the final summary string.
    /// Call in OneTimeTearDown to print it.
    /// </summary>
    public string BuildSummary()
    {
        _stopwatch.Stop();

        var results = _results.OrderBy(r => r.Timestamp).ToList();
        var passed = results.Count(r => r.Status == "Passed");
        var failed = results.Count(r => r.Status == "Failed");
        var skipped = results.Count(r => r.Status is "Skipped" or "Inconclusive");
        var total = results.Count;
        var elapsed = _stopwatch.Elapsed;

        var sb = new StringBuilder();

        sb.AppendLine();
        sb.AppendLine("╔══════════════════════════════════════════════════════════════════════╗");
        sb.AppendLine("║                     E2E TEST RUN SUMMARY                            ║");
        sb.AppendLine("╠══════════════════════════════════════════════════════════════════════╣");
        sb.AppendLine($"║  Total: {total,-6}  Passed: {passed,-6}  Failed: {failed,-6}  Skipped: {skipped,-5}║");
        sb.AppendLine($"║  Duration: {elapsed:hh\\:mm\\:ss\\.fff}                                              ║");
        sb.AppendLine("╠══════════════════════════════════════════════════════════════════════╣");

        // List all tests with status icon
        foreach (var r in results)
        {
            var icon = r.Status switch
            {
                "Passed" => "✓",
                "Failed" => "✗",
                "Skipped" or "Inconclusive" => "⊘",
                _ => "?"
            };

            // Truncate long names to fit the box
            var name = r.FullName.Length > 60 ? "..." + r.FullName[^57..] : r.FullName;
            sb.AppendLine($"║  {icon} {name,-63}║");
        }

        // Detail section for failures
        var failures = results.Where(r => r.Status == "Failed").ToList();
        if (failures.Count > 0)
        {
            sb.AppendLine("╠══════════════════════════════════════════════════════════════════════╣");
            sb.AppendLine("║  FAILURE DETAILS                                                    ║");
            sb.AppendLine("╠══════════════════════════════════════════════════════════════════════╣");

            foreach (var f in failures)
            {
                sb.AppendLine($"║  ✗ {f.FullName}");
                if (!string.IsNullOrWhiteSpace(f.ErrorMessage))
                {
                    // Print first 2 lines of the error message
                    var lines = f.ErrorMessage.Split('\n', StringSplitOptions.RemoveEmptyEntries);
                    foreach (var line in lines.Take(2))
                    {
                        sb.AppendLine($"║    {line.TrimEnd()}");
                    }
                }
                if (!string.IsNullOrWhiteSpace(f.ScreenshotPath))
                {
                    sb.AppendLine($"║    Screenshot: {f.ScreenshotPath}");
                }
                sb.AppendLine("║");
            }
        }

        sb.AppendLine("╚══════════════════════════════════════════════════════════════════════╝");

        // Final verdict line (easy to grep in CI logs)
        var verdict = failed > 0 ? "FAILED" : "PASSED";
        sb.AppendLine($"  >> Result: {verdict} ({passed}/{total} passed) in {elapsed:hh\\:mm\\:ss}");

        return sb.ToString();
    }

    /// <summary>
    /// Export results to TestResults/ folder as JSON and plain text.
    /// Returns the output directory path.
    /// </summary>
    public string ExportToFiles(string? outputDir = null)
    {
        outputDir ??= Path.Combine(Directory.GetCurrentDirectory(), "TestResults");
        Directory.CreateDirectory(outputDir);

        var timestamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
        var results = _results.OrderBy(r => r.Timestamp).ToList();
        var elapsed = _stopwatch.Elapsed;

        // === JSON export ===
        var jsonPath = Path.Combine(outputDir, $"e2e-summary_{timestamp}.json");
        var jsonData = new
        {
            RunDate = DateTime.UtcNow,
            Duration = elapsed.ToString(@"hh\:mm\:ss\.fff"),
            Total = results.Count,
            Passed = results.Count(r => r.Status == "Passed"),
            Failed = results.Count(r => r.Status == "Failed"),
            Skipped = results.Count(r => r.Status is "Skipped" or "Inconclusive"),
            Verdict = results.Any(r => r.Status == "Failed") ? "FAILED" : "PASSED",
            Tests = results.Select(r => new
            {
                r.FullName,
                r.Status,
                r.ErrorMessage,
                r.ScreenshotPath,
                r.Timestamp
            })
        };
        var jsonOptions = new JsonSerializerOptions { WriteIndented = true, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull };
        File.WriteAllText(jsonPath, JsonSerializer.Serialize(jsonData, jsonOptions));

        // === Plain text export ===
        var txtPath = Path.Combine(outputDir, $"e2e-summary_{timestamp}.txt");
        File.WriteAllText(txtPath, BuildSummary());

        return outputDir;
    }

    private sealed class TestResult
    {
        public required string FullName { get; init; }
        public required string Status { get; init; }
        public string? ErrorMessage { get; init; }
        public string? ScreenshotPath { get; init; }
        public DateTime Timestamp { get; init; }
    }
}
