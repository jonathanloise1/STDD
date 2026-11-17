using Microsoft.Extensions.Logging;

namespace MyApp.Infrastructure.Logging;

/// <summary>Settings of the JSON lines file sink, section <c>MyApp:Logging:File</c> (operations.md 3.4).</summary>
public sealed class FileLoggerOptions
{
    public const string SectionName = "MyApp:Logging:File";

    public bool Enabled { get; set; }
    public string Directory { get; set; } = "logs";
    public string FilePrefix { get; set; } = "myapp-api";
    public int MaxFileSizeMB { get; set; } = 100;
    public int RetainedFiles { get; set; } = 14;
    public LogLevel MinimumLevel { get; set; } = LogLevel.Information;
}
