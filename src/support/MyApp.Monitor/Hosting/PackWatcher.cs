using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;
using MyApp.Monitor.Analysis;
using MyApp.Monitor.Pack;

namespace MyApp.Monitor.Hosting;

/// <summary>
/// Watches the pack path (operations.md 13.3): a new valid pack replaces the old one between batches; an invalid one is refused and
/// shown on the Sources page. A change is acted on once the files have stopped changing for one check, so a pack that is still being
/// copied is never loaded half-written.
/// </summary>
public sealed class PackWatcher(string path, RunContextHolder holder, RunContextFactory contexts, MonitorStatus status, ILogger? logger = null)
{
    private string _loaded = Fingerprint(path);
    private string _lastSeen = Fingerprint(path);

    /// <summary>One check. Returns true when the context was replaced.</summary>
    public bool Check()
    {
        var now = Fingerprint(path);
        if (now != _lastSeen) { _lastSeen = now; return false; }   // still changing: wait for the next check
        if (now == _loaded) return false;
        _loaded = now;
        try
        {
            var pack = PackLoader.Load(path);
            var context = contexts.Create(pack);
            holder.Replace(context);
            status.PackLoaded();
            logger?.LogInformation("Operations pack replaced: version {Version}", pack.Version);
            return true;
        }
        catch (Exception ex) when (ex is MonitorException or IOException or InvalidDataException or UnauthorizedAccessException)
        {
            status.PackRefused(ex.Message.Length > 500 ? ex.Message[..500] : ex.Message);
            logger?.LogError("A new operations pack at {Path} was refused: {Error}", path, ex.Message);
            return false;
        }
    }

    public static string Fingerprint(string path)
    {
        var sb = new StringBuilder();
        try
        {
            if (Directory.Exists(path))
                foreach (var f in Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories).OrderBy(x => x, StringComparer.Ordinal))
                {
                    var info = new FileInfo(f);
                    sb.Append(Path.GetRelativePath(path, f)).Append('|').Append(info.Length).Append('|').Append(info.LastWriteTimeUtc.Ticks).Append('\n');
                }
            else if (File.Exists(path))
            {
                var info = new FileInfo(path);
                sb.Append(info.Length).Append('|').Append(info.LastWriteTimeUtc.Ticks);
            }
            else sb.Append("missing");
        }
        catch (IOException) { sb.Append("unreadable"); }
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(sb.ToString())));
    }
}
