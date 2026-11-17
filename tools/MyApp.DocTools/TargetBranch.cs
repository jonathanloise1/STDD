using System.ComponentModel;
using System.Diagnostics;

namespace MyApp.DocTools;

/// <summary>Reads files of another git ref. Read-only.</summary>
public interface IGitReader
{
    bool RefExists(string gitRef);
    /// <summary>The text of a file at the ref, or null when the ref does not have that file.</summary>
    string? ReadFile(string gitRef, string path);
}

/// <summary>The git command line, in the repository root.</summary>
public sealed class GitCli(string root) : IGitReader
{
    public bool RefExists(string gitRef) =>
        !gitRef.StartsWith('-') && Run("rev-parse", "--verify", "--quiet", gitRef + "^{commit}").Code == 0;

    public string? ReadFile(string gitRef, string path)
    {
        var (code, text) = Run("show", $"{gitRef}:{path}");
        return code == 0 ? text : null;
    }

    private (int Code, string Output) Run(params string[] args)
    {
        var start = new ProcessStartInfo("git") { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
        start.ArgumentList.Add("-C");
        start.ArgumentList.Add(root);
        foreach (var a in args) start.ArgumentList.Add(a);
        try
        {
            using var process = Process.Start(start)!;
            process.BeginErrorReadLine();
            var text = process.StandardOutput.ReadToEnd();
            process.WaitForExit();
            return (process.ExitCode, text);
        }
        catch (Win32Exception) { return (-1, ""); }
    }
}
