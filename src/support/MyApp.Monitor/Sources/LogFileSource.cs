using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace MyApp.Monitor.Sources;

/// <summary>Checkpoint of a file batch: the offsets to save and the head hashes they replace.</summary>
public sealed record FileCheckpoint(IReadOnlyCollection<string> RemoveHeadHashes, IReadOnlyCollection<FileOffset> Upserts);

/// <summary>
/// The <c>log_files</c> source (operations.md 6.4): tails files by glob with saved offsets. A file is identified by the SHA-256 of
/// the first 1024 bytes (or of the consumed prefix while the file is shorter), so a rotated file is found again under its new name.
/// </summary>
public sealed class LogFileSource(string id) : PollingSourceBase(id, "log_files")
{
    public const int HeadBytes = 1024;
    public const int MaxLineBytes = 64 * 1024;
    public const int MaxBatchBytes = 4 * 1024 * 1024;

    private sealed class Tracked
    {
        public string Path = "";
        public string HeadHash = "";
        public long Offset;
        public bool SkipToLf;
    }

    private List<Tracked> _tracked = [];
    private bool _initialised;
    private string _format = "jsonl";
    private Regex? _lineRegex;
    private bool _startAtEnd = true;
    private IReadOnlyList<string> _patterns = [];
    private long _skippedLines;

    protected override TimeSpan PollInterval => TimeSpan.FromSeconds(Math.Max(1, Context.GetInt("poll_seconds", 5)));

    protected override async Task OnStartAsync(CancellationToken ct)
    {
        _patterns = Context.GetStrings("paths");
        if (_patterns.Count == 0) throw new SourceConfigurationException($"source '{Id}': paths must list at least one glob");
        _format = Context.GetString("format") ?? "jsonl";
        if (_format is not ("jsonl" or "text")) throw new SourceConfigurationException($"source '{Id}': format must be jsonl or text");
        if (_format == "text")
        {
            var pattern = Context.GetString("line_regex") ?? throw new SourceConfigurationException($"source '{Id}': format text needs line_regex");
            try { _lineRegex = new Regex(pattern, RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1)); }
            catch (ArgumentException ex) { throw new SourceConfigurationException($"source '{Id}': line_regex is invalid: {ex.Message}"); }
        }
        var startAt = Context.GetString("start_at") ?? "end";
        if (startAt is not ("end" or "beginning")) throw new SourceConfigurationException($"source '{Id}': start_at must be end or beginning");
        _startAtEnd = startAt == "end";

        var saved = await Context.State.GetOffsetsAsync(Id, ct).ConfigureAwait(false);
        _tracked = saved.Select(o => new Tracked { Path = o.Path, HeadHash = o.HeadHash, Offset = o.Offset }).ToList();
    }

    protected override IReadOnlyDictionary<string, string> HealthDetails() => new Dictionary<string, string>
    {
        ["files_tracked"] = _tracked.Count.ToString(CultureInfo.InvariantCulture),
        ["skipped_lines"] = _skippedLines.ToString(CultureInfo.InvariantCulture),
    };

    public override async Task CommitAsync(SignalBatch batch, CancellationToken ct)
    {
        if (batch.Checkpoint is not FileCheckpoint cp) return;
        await Context.State.SaveOffsetsAsync(Id, cp.RemoveHeadHashes, cp.Upserts, ct).ConfigureAwait(false);
    }

    protected override async Task<IReadOnlyList<SignalBatch>> PollAsync(CancellationToken ct)
    {
        var files = GlobExpander.Expand(_patterns, Context.ContentRoot)
            .OrderBy(f => f.LastWriteTimeUtc).ThenBy(f => f.FullName, StringComparer.Ordinal).ToList();
        var firstRun = !_initialised && _tracked.Count == 0;
        _initialised = true;

        // ---- identify files: same path first, then a file found under another name (rotation) ----
        var assigned = new Dictionary<string, Tracked>(StringComparer.Ordinal);   // full path -> entry
        var leftovers = new List<Tracked>();
        var hashCache = new Dictionary<(string Path, int Length), string?>();

        string? Prefix(FileInfo f, int length)
        {
            if (!hashCache.TryGetValue((f.FullName, length), out var h))
                hashCache[(f.FullName, length)] = h = PrefixHash(f.FullName, length);
            return h;
        }

        foreach (var entry in _tracked)
        {
            var n = (int)Math.Min(HeadBytes, entry.Offset);
            var file = files.FirstOrDefault(f => f.FullName == entry.Path);
            if (file is not null && !assigned.ContainsKey(file.FullName) && Prefix(file, n) == entry.HeadHash) assigned[file.FullName] = entry;
            else leftovers.Add(entry);
        }

        var dropped = new List<Tracked>();
        foreach (var entry in leftovers)
        {
            var n = (int)Math.Min(HeadBytes, entry.Offset);
            // an entry with nothing consumed carries no identity: it only matches its own path
            var file = entry.Offset == 0 ? null : files.FirstOrDefault(f => !assigned.ContainsKey(f.FullName) && Prefix(f, n) == entry.HeadHash);
            if (file is null) { dropped.Add(entry); continue; }
            entry.Path = file.FullName;
            assigned[file.FullName] = entry;
        }

        var silentRemove = dropped.Select(d => d.HeadHash).ToList();
        var silentUpserts = new List<FileOffset>();
        var now = Time.GetUtcNow();
        var batches = new List<SignalBatch>();
        var next = new List<Tracked>();

        foreach (var file in files)
        {
            ct.ThrowIfCancellationRequested();
            file.Refresh();
            if (!file.Exists) continue;
            Tracked entry;
            if (assigned.TryGetValue(file.FullName, out var existing))
            {
                entry = existing;
                if (file.Length < entry.Offset)
                {
                    // truncation: restart at 0
                    silentRemove.Add(entry.HeadHash);
                    entry.Offset = 0;
                    entry.HeadHash = Convert.ToHexString(SHA256.HashData([])).ToLowerInvariant();
                    entry.SkipToLf = false;
                }
            }
            else
            {
                entry = new Tracked { Path = file.FullName, HeadHash = Convert.ToHexString(SHA256.HashData([])).ToLowerInvariant() };
                if (firstRun && _startAtEnd)
                {
                    entry.Offset = file.Length;
                    entry.HeadHash = PrefixHash(file.FullName, (int)Math.Min(HeadBytes, entry.Offset)) ?? entry.HeadHash;
                    if (entry.Offset > 0) silentUpserts.Add(new FileOffset(Id, entry.HeadHash, entry.Path, entry.Offset, now));
                }
            }
            next.Add(entry);
            await ReadFileAsync(file, entry, batches, now, ct).ConfigureAwait(false);
        }

        _tracked = next;
        if (silentRemove.Count > 0 || silentUpserts.Count > 0)
            await Context.State.SaveOffsetsAsync(Id, silentRemove, silentUpserts, ct).ConfigureAwait(false);
        return batches;
    }

    private async Task ReadFileAsync(FileInfo file, Tracked entry, List<SignalBatch> batches, DateTimeOffset now, CancellationToken ct)
    {
        long length;
        try { length = file.Length; } catch (IOException) { return; }

        while (entry.Offset < length)
        {
            ct.ThrowIfCancellationRequested();
            var want = (int)Math.Min(MaxBatchBytes, length - entry.Offset);
            byte[] buffer;
            try
            {
                using var stream = new FileStream(file.FullName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                stream.Seek(entry.Offset, SeekOrigin.Begin);
                buffer = new byte[want];
                var read = 0;
                while (read < want)
                {
                    var n = await stream.ReadAsync(buffer.AsMemory(read, want - read), ct).ConfigureAwait(false);
                    if (n == 0) break;
                    read += n;
                }
                if (read < want) Array.Resize(ref buffer, read);
            }
            catch (IOException) { return; }
            if (buffer.Length == 0) return;

            var start = 0;
            if (entry.SkipToLf)
            {
                var lf = Array.IndexOf(buffer, (byte)'\n');
                if (lf < 0) { entry.Offset += buffer.Length; continue; }
                start = lf + 1;
                entry.SkipToLf = false;
            }

            var lastLf = Array.LastIndexOf(buffer, (byte)'\n');
            var lines = new List<(string Text, bool Truncated)>();
            long consumed;
            if (lastLf < start)
            {
                if (buffer.Length < MaxBatchBytes && entry.Offset + buffer.Length >= length)
                {
                    // a partial line: it waits for the next poll
                    if (start > 0) { entry.Offset += start; Advance(entry, file, batches, [], now); }
                    return;
                }
                // a line larger than a whole batch: cut it and skip the rest of it
                lines.Add((Decode(buffer, start, MaxLineBytes), true));
                consumed = buffer.Length;
                entry.SkipToLf = true;
            }
            else
            {
                consumed = lastLf + 1;
                var s = start;
                for (var i = start; i <= lastLf; i++)
                {
                    if (buffer[i] != (byte)'\n') continue;
                    var end = i;
                    if (end > s && buffer[end - 1] == (byte)'\r') end--;
                    var len = end - s;
                    if (len > 0) lines.Add(len > MaxLineBytes ? (Decode(buffer, s, MaxLineBytes), true) : (Decode(buffer, s, len), false));
                    s = i + 1;
                }
            }

            var records = ToRecords(lines, entry.Offset == 0 && start == 0, file, now);
            entry.Offset += consumed;
            Advance(entry, file, batches, records, now);
        }
    }

    /// <summary>Updates the head hash for the new offset and queues the batch with its checkpoint.</summary>
    private void Advance(Tracked entry, FileInfo file, List<SignalBatch> batches, List<LogRecord> records, DateTimeOffset now)
    {
        var oldHash = entry.HeadHash;
        var newHash = PrefixHash(file.FullName, (int)Math.Min(HeadBytes, entry.Offset)) ?? oldHash;
        entry.HeadHash = newHash;
        if (entry.Offset == 0) return;
        var remove = oldHash != newHash ? new[] { oldHash } : [];
        var checkpoint = new FileCheckpoint(remove, [new FileOffset(Id, newHash, entry.Path, entry.Offset, now)]);
        batches.Add(new SignalBatch(Id, records, checkpoint));
    }

    private static string Decode(byte[] buffer, int start, int count) => Encoding.UTF8.GetString(buffer, start, count);

    private List<LogRecord> ToRecords(List<(string Text, bool Truncated)> lines, bool fileStart, FileInfo file, DateTimeOffset now)
    {
        var records = new List<LogRecord>();
        if (_format == "jsonl")
        {
            var first = true;
            foreach (var (text0, truncated) in lines)
            {
                var text = text0;
                if (first && fileStart && text.StartsWith('﻿')) text = text[1..];
                first = false;
                var record = JsonlRecordParser.Parse(text, Id, Context.Environment, truncated);
                if (record is null) _skippedLines++;
                else records.Add(record with { Origin = file.FullName });
            }
            return records;
        }

        // text: a line that does not match line_regex continues the previous record's message (stack traces)
        (DateTimeOffset At, RecordLevel Level, string? Category, System.Text.StringBuilder Message, bool Truncated)? current = null;
        void Flush()
        {
            if (current is null) return;
            var c = current.Value;
            var props = c.Truncated ? new Dictionary<string, string> { ["Truncated"] = "true" } : null;
            records.Add(LogRecord.Create(Id, Context.Environment, c.At, c.Level, c.Category, c.Message.ToString(), properties: props) with { Origin = file.FullName });
            current = null;
        }
        foreach (var (text, truncated) in lines)
        {
            var m = _lineRegex!.Match(text);
            if (m.Success)
            {
                Flush();
                var at = DateTimeOffset.TryParse(m.Groups["timestamp"].Value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var t) ? t : now;
                LogRecord.TryParseLevel(m.Groups["level"].Value, out var level);
                current = (at, level, m.Groups["category"].Success ? m.Groups["category"].Value : null,
                    new System.Text.StringBuilder(m.Groups["message"].Success ? m.Groups["message"].Value : text), truncated);
            }
            else if (current is { } c)
            {
                c.Message.Append('\n').Append(text);
                current = (c.At, c.Level, c.Category, c.Message, c.Truncated || truncated);
            }
            else _skippedLines++;   // a continuation line whose record was read earlier
        }
        Flush();
        return records;
    }

    private static string? PrefixHash(string path, int length)
    {
        try
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            var buffer = new byte[length];
            var read = 0;
            while (read < length)
            {
                var n = stream.Read(buffer, read, length - read);
                if (n == 0) return null;
                read += n;
            }
            return Convert.ToHexString(SHA256.HashData(buffer)).ToLowerInvariant();
        }
        catch (IOException) { return null; }
        catch (UnauthorizedAccessException) { return null; }
    }
}

public sealed class LogFileSourceFactory : ISignalSourceFactory
{
    public string Kind => "log_files";
    public ISignalSource Create(string sourceId) => new LogFileSource(sourceId);
}
