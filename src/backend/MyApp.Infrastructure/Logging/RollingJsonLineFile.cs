using System.Globalization;
using System.Text.RegularExpressions;

namespace MyApp.Infrastructure.Logging;

/// <summary>
/// Appends whole lines to <c>{prefix}-{yyyyMMdd}.jsonl</c> and rolls to <c>{prefix}-{yyyyMMdd}-{n}.jsonl</c> when a
/// file would exceed the size limit; a new file starts at the UTC date change; the oldest files beyond the retained
/// count are deleted (operations.md 3.4). Files are opened with sharing so a tailer can read them.
/// </summary>
internal sealed class RollingJsonLineFile : IDisposable
{
    private readonly object _gate = new();
    private readonly string _directory;
    private readonly string _prefix;
    private readonly long _maxBytes;
    private readonly int _retainedFiles;
    private readonly TimeProvider _time;
    private readonly Regex _namePattern;

    private FileStream? _stream;
    private string? _currentPath;
    private DateOnly _date;
    private int _index;
    private long _size;
    private bool _failureReported;

    public RollingJsonLineFile(string directory, string prefix, long maxBytes, int retainedFiles, TimeProvider time)
    {
        _directory = directory;
        _prefix = prefix;
        _maxBytes = Math.Max(1, maxBytes);
        _retainedFiles = Math.Max(1, retainedFiles);
        _time = time;
        _namePattern = new Regex($"^{Regex.Escape(prefix)}-(?<date>\\d{{8}})(?:-(?<n>\\d+))?\\.jsonl$", RegexOptions.Compiled);
    }

    public static string FileName(string prefix, DateOnly date, int index) =>
        index == 0
            ? $"{prefix}-{date.ToString("yyyyMMdd", CultureInfo.InvariantCulture)}.jsonl"
            : $"{prefix}-{date.ToString("yyyyMMdd", CultureInfo.InvariantCulture)}-{index}.jsonl";

    /// <summary>Writes one complete line (the bytes include the trailing LF) with a single write call.</summary>
    public void Write(ReadOnlySpan<byte> line)
    {
        lock (_gate)
        {
            try
            {
                var today = DateOnly.FromDateTime(_time.GetUtcNow().UtcDateTime);
                if (_stream is null || today != _date)
                {
                    _date = today;
                    _index = HighestIndex(today);
                    Open();
                    Retain();
                }

                if (_size > 0 && _size + line.Length > _maxBytes)
                {
                    _index++;
                    Open();
                    Retain();
                }

                _stream!.Write(line);
                _stream.Flush();
                _size += line.Length;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // Logging must never break the application: report once and drop the entry.
                CloseStream();
                if (!_failureReported)
                {
                    _failureReported = true;
                    Console.Error.WriteLine($"JSON file logger cannot write to '{_directory}': {ex.Message}");
                }
            }
        }
    }

    private void Open()
    {
        CloseStream();
        Directory.CreateDirectory(_directory);
        _currentPath = Path.Combine(_directory, FileName(_prefix, _date, _index));
        _stream = new FileStream(_currentPath, FileMode.Append, FileAccess.Write,
            FileShare.ReadWrite | FileShare.Delete, 4096, FileOptions.None);
        _size = _stream.Length;
        _failureReported = false;
    }

    private int HighestIndex(DateOnly date)
    {
        var highest = 0;
        foreach (var (fileDate, index, _) in ExistingFiles())
        {
            if (fileDate == date && index > highest)
                highest = index;
        }
        return highest;
    }

    private void Retain()
    {
        var files = ExistingFiles()
            .OrderByDescending(f => f.Date)
            .ThenByDescending(f => f.Index)
            .ToList();

        foreach (var file in files.Skip(_retainedFiles))
        {
            if (string.Equals(file.Path, _currentPath, StringComparison.Ordinal))
                continue;
            try { File.Delete(file.Path); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { /* retry at the next roll */ }
        }
    }

    private IEnumerable<(DateOnly Date, int Index, string Path)> ExistingFiles()
    {
        if (!Directory.Exists(_directory))
            yield break;

        foreach (var path in Directory.EnumerateFiles(_directory, $"{_prefix}-*.jsonl"))
        {
            var match = _namePattern.Match(Path.GetFileName(path));
            if (!match.Success)
                continue;
            if (!DateOnly.TryParseExact(match.Groups["date"].Value, "yyyyMMdd", CultureInfo.InvariantCulture,
                    DateTimeStyles.None, out var date))
                continue;
            var index = match.Groups["n"].Success ? int.Parse(match.Groups["n"].Value, CultureInfo.InvariantCulture) : 0;
            yield return (date, index, path);
        }
    }

    private void CloseStream()
    {
        _stream?.Dispose();
        _stream = null;
    }

    public void Dispose()
    {
        lock (_gate)
            CloseStream();
    }
}
