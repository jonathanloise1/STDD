using System.Text;
using System.Text.Json.Nodes;
using MyApp.Monitor.Sources;
using NUnit.Framework;

namespace MyApp.Monitor.Tests.Integration;

public class LogFileSourceTests
{
    private TempDir _dir = null!;
    private InMemorySourceStateStore _state = null!;
    private FakeTime _time = null!;

    [SetUp]
    public void SetUp()
    {
        _dir = new TempDir();
        _state = new InMemorySourceStateStore();
        _time = FakeTime.Default();
    }

    [TearDown]
    public void TearDown() => _dir.Dispose();

    private static string Jl(string message, string level = "Warning", string? key = null, string? trace = null, int pad = 0)
    {
        var o = new JsonObject { ["Timestamp"] = "2026-11-06T10:00:00.000Z", ["Level"] = level, ["Category"] = "Cat", ["Message"] = message + new string('x', pad) };
        if (key is not null) o["LogKey"] = key;
        if (trace is not null) o["TraceId"] = trace;
        return o.ToJsonString();
    }

    private string Path(string name) => _dir.File(name);
    private void Append(string name, params string[] lines) => File.AppendAllText(Path(name), string.Concat(lines.Select(l => l + "\n")));

    private async Task<LogFileSource> Start(string startAt = "end", string glob = "app*.jsonl", string format = "jsonl", string? lineRegex = null, InMemorySourceStateStore? state = null)
    {
        var settings = new JsonObject { ["paths"] = new JsonArray(glob), ["format"] = format, ["start_at"] = startAt, ["poll_seconds"] = 5 };
        if (lineRegex is not null) settings["line_regex"] = lineRegex;
        var source = new LogFileSource("applogs");
        await source.StartAsync(new SourceContext("applogs", "log_files", "prod", settings, _dir.Path, state ?? _state, _time), default);
        return source;
    }

    private static async Task<List<string>> Poll(LogFileSource source, bool commit = true)
    {
        var result = await source.RunOnceAsync(default);
        var messages = new List<string>();
        foreach (var batch in result.Batches)
        {
            messages.AddRange(batch.Records.Select(r => r.Message));
            if (commit) await source.CommitAsync(batch, default);
        }
        return messages;
    }

    [Test]
    public async Task First_run_at_end_skips_existing_lines_and_reads_what_is_appended_later()
    {
        Append("app.jsonl", Jl("old 1"), Jl("old 2"));
        var source = await Start("end");
        Assert.That(await Poll(source), Is.Empty);
        Append("app.jsonl", Jl("new 1"));
        Assert.That(await Poll(source), Is.EqualTo(new[] { "new 1" }));
        Assert.That(await Poll(source), Is.Empty);
    }

    [Test]
    public async Task First_run_at_beginning_reads_everything_once()
    {
        Append("app.jsonl", Jl("a"), Jl("b"));
        var source = await Start("beginning");
        Assert.That(await Poll(source), Is.EqualTo(new[] { "a", "b" }));
        Assert.That(await Poll(source), Is.Empty);
    }

    [Test]
    public async Task A_file_that_appears_after_the_first_poll_is_read_from_its_start_even_with_start_at_end()
    {
        Append("app-1.jsonl", Jl("old"));
        var source = await Start("end");
        await Poll(source);
        Append("app-2.jsonl", Jl("fresh"));
        Assert.That(await Poll(source), Is.EqualTo(new[] { "fresh" }));
    }

    [Test]
    public async Task Records_carry_the_source_id_the_environment_and_the_normal_form()
    {
        File.WriteAllText(Path("app.jsonl"), Jl("m", "Error", key: "task.create.access_lost", trace: "4BF92F3577B34DA6A3CE929D0E0E4736") + "\n");
        var source = await Start("beginning");
        var result = await source.RunOnceAsync(default);
        var r = result.Batches.Single().Records.Single();
        Assert.That(r.SourceId, Is.EqualTo("applogs"));
        Assert.That(r.Environment, Is.EqualTo("prod"));
        Assert.That(r.Level, Is.EqualTo(RecordLevel.Error));
        Assert.That(r.LogKey, Is.EqualTo("task.create.access_lost"));
        Assert.That(r.TraceId, Is.EqualTo("4bf92f3577b34da6a3ce929d0e0e4736"), "trace ids are lower-case");
        Assert.That(r.RecordId, Has.Length.EqualTo(32));
    }

    [Test]
    public async Task Only_complete_lines_are_read_and_the_rest_waits_for_the_next_poll()
    {
        var whole = Jl("whole");
        var second = Jl("second");
        File.WriteAllText(Path("app.jsonl"), whole + "\n" + second[..20]);
        var source = await Start("beginning");
        Assert.That(await Poll(source), Is.EqualTo(new[] { "whole" }));
        File.AppendAllText(Path("app.jsonl"), second[20..] + "\n");
        Assert.That(await Poll(source), Is.EqualTo(new[] { "second" }));
    }

    [Test]
    public async Task CRLF_blank_lines_BOM_and_invalid_json_are_handled()
    {
        File.WriteAllBytes(Path("app.jsonl"), new UTF8Encoding(true).GetPreamble().Concat(Encoding.UTF8.GetBytes(Jl("one") + "\r\n\r\nnot json\r\n" + Jl("two") + "\r\n")).ToArray());
        var source = await Start("beginning");
        Assert.That(await Poll(source), Is.EqualTo(new[] { "one", "two" }));
        var health = await source.GetHealthAsync(default);
        Assert.That(health.Details["skipped_lines"], Is.EqualTo("1"));
    }

    [Test]
    public async Task Unread_data_after_a_restart_comes_back_because_the_checkpoint_was_not_committed()
    {
        Append("app.jsonl", Jl("a"), Jl("b"));
        var first = await Start("beginning");
        Assert.That(await Poll(first, commit: false), Is.EqualTo(new[] { "a", "b" }), "analysis did not finish: no commit");

        var restarted = await Start("beginning");
        Assert.That(await Poll(restarted), Is.EqualTo(new[] { "a", "b" }), "at least once");
    }

    [Test]
    public async Task A_committed_offset_survives_a_restart_and_only_new_lines_are_read()
    {
        Append("app.jsonl", Jl("a"), Jl("b"));
        var first = await Start("beginning");
        await Poll(first);
        Append("app.jsonl", Jl("c"));

        var restarted = await Start("end");   // even start_at end: saved offsets win
        Assert.That(await Poll(restarted), Is.EqualTo(new[] { "c" }));
        Assert.That((await _state.GetOffsetsAsync("applogs", default)).Single().Offset, Is.EqualTo(new FileInfo(Path("app.jsonl")).Length));
    }

    [Test]
    public async Task A_short_file_stays_identified_while_it_grows_across_polls_and_restarts()
    {
        Append("app.jsonl", Jl("a", pad: 0));
        var source = await Start("beginning");
        await Poll(source);
        Assert.That(new FileInfo(Path("app.jsonl")).Length, Is.LessThan(LogFileSource.HeadBytes));
        Append("app.jsonl", Jl("b"));
        Assert.That(await Poll(source), Is.EqualTo(new[] { "b" }));

        var restarted = await Start("end");
        Append("app.jsonl", Jl("c"));
        Assert.That(await Poll(restarted), Is.EqualTo(new[] { "c" }));
        Assert.That(await _state.GetOffsetsAsync("applogs", default), Has.Count.EqualTo(1), "one entry per file, replaced as the head hash changes");
    }

    [Test]
    public async Task Rotation_reads_the_old_file_to_its_end_under_its_new_name_then_the_new_file()
    {
        Append("app.jsonl", Jl("a", pad: 2000), Jl("b", pad: 10));
        var source = await Start("beginning", glob: "app*.jsonl");
        Assert.That(await Poll(source), Is.EqualTo(new[] { new string('a', 0) + "a" + new string('x', 2000), "b" + new string('x', 10) }.Select(s => s)));

        // the writer appends one more line, then the file is rotated and a new one starts
        Append("app.jsonl", Jl("c-before-rotation"));
        File.Move(Path("app.jsonl"), Path("app.1.jsonl"));
        File.SetLastWriteTimeUtc(Path("app.1.jsonl"), new DateTime(2026, 11, 6, 9, 0, 0, DateTimeKind.Utc));
        Append("app.jsonl", Jl("d-after-rotation"));
        File.SetLastWriteTimeUtc(Path("app.jsonl"), new DateTime(2026, 11, 6, 9, 30, 0, DateTimeKind.Utc));

        Assert.That(await Poll(source), Is.EqualTo(new[] { "c-before-rotation", "d-after-rotation" }), "old file first (last write time), no duplicates");
        Assert.That(await Poll(source), Is.Empty);

        Append("app.jsonl", Jl("e"));
        Assert.That(await Poll(source), Is.EqualTo(new[] { "e" }));
    }

    [Test]
    public async Task Rotation_is_recovered_after_a_restart_from_saved_offsets()
    {
        Append("app.jsonl", Jl("a", pad: 2000));
        var source = await Start("beginning");
        await Poll(source);
        Append("app.jsonl", Jl("tail"));
        File.Move(Path("app.jsonl"), Path("app.1.jsonl"));
        File.SetLastWriteTimeUtc(Path("app.1.jsonl"), new DateTime(2026, 11, 6, 8, 0, 0, DateTimeKind.Utc));
        Append("app.jsonl", Jl("new-file"));

        var restarted = await Start("end");
        Assert.That(await Poll(restarted), Is.EqualTo(new[] { "tail", "new-file" }));
    }

    [Test]
    public async Task A_file_smaller_than_the_saved_offset_restarts_at_zero()
    {
        Append("app.jsonl", Jl("one", pad: 3000), Jl("two", pad: 3000));
        var source = await Start("beginning");
        Assert.That(await Poll(source), Has.Count.EqualTo(2));

        File.WriteAllText(Path("app.jsonl"), Jl("fresh") + "\n");   // truncated and rewritten, shorter than the old offset
        Assert.That(await Poll(source), Is.EqualTo(new[] { "fresh" }));
        Append("app.jsonl", Jl("more"));
        Assert.That(await Poll(source), Is.EqualTo(new[] { "more" }));
    }

    [Test]
    public async Task A_file_emptied_in_place_is_read_again_from_the_start()
    {
        Append("app.jsonl", Jl("one", pad: 1500));
        var source = await Start("beginning");
        await Poll(source);
        File.WriteAllText(Path("app.jsonl"), "");
        Assert.That(await Poll(source), Is.Empty);
        Append("app.jsonl", Jl("after"));
        Assert.That(await Poll(source), Is.EqualTo(new[] { "after" }));
    }

    [Test]
    public async Task A_deleted_file_loses_its_offset()
    {
        Append("app.jsonl", Jl("a"));
        var source = await Start("beginning");
        await Poll(source);
        Assert.That(await _state.GetOffsetsAsync("applogs", default), Has.Count.EqualTo(1));
        File.Delete(Path("app.jsonl"));
        await Poll(source);
        Assert.That(await _state.GetOffsetsAsync("applogs", default), Is.Empty);
    }

    [Test]
    public async Task Files_are_read_in_last_write_time_order()
    {
        Append("app-b.jsonl", Jl("from b"));
        Append("app-a.jsonl", Jl("from a"));
        File.SetLastWriteTimeUtc(Path("app-b.jsonl"), new DateTime(2026, 11, 6, 8, 0, 0, DateTimeKind.Utc));
        File.SetLastWriteTimeUtc(Path("app-a.jsonl"), new DateTime(2026, 11, 6, 9, 0, 0, DateTimeKind.Utc));
        var source = await Start("beginning");
        Assert.That(await Poll(source), Is.EqualTo(new[] { "from b", "from a" }));
    }

    [Test]
    public async Task Glob_patterns_with_directories_and_parent_segments_are_expanded()
    {
        Directory.CreateDirectory(Path("logs/sub"));
        File.WriteAllText(Path("logs/sub/app-1.jsonl"), Jl("deep") + "\n");
        File.WriteAllText(Path("logs/other.txt"), Jl("ignored") + "\n");
        Directory.CreateDirectory(Path("run"));
        var settings = new JsonObject { ["paths"] = new JsonArray("../logs/**/app-*.jsonl"), ["start_at"] = "beginning" };
        var source = new LogFileSource("g");
        await source.StartAsync(new SourceContext("g", "log_files", "prod", settings, Path("run"), _state, _time), default);
        Assert.That(await Poll(source), Is.EqualTo(new[] { "deep" }));
    }

    [Test]
    public async Task Lines_over_64_KB_are_cut_and_flagged_truncated()
    {
        Append("app.jsonl", Jl("short"), Jl("long", pad: 70_000), Jl("after"));
        var source = await Start("beginning");
        var result = await source.RunOnceAsync(default);
        var records = result.Batches.SelectMany(b => b.Records).ToList();
        Assert.That(records, Has.Count.EqualTo(3));
        Assert.That(records[1].Properties["Truncated"], Is.EqualTo("true"));
        Assert.That(records[1].Level, Is.EqualTo(RecordLevel.Warning));
        Assert.That(records[0].Properties.ContainsKey("Truncated"), Is.False);
        Assert.That(records[2].Message, Is.EqualTo("after"));
    }

    [Test]
    public async Task Text_format_uses_the_regex_and_appends_non_matching_lines_to_the_previous_message()
    {
        File.WriteAllText(Path("app.log"),
            "2026-11-06T10:00:00Z [ERR] MyApp.Worker boom happened\n   at MyApp.Worker.Run()\n   at MyApp.Program.Main()\n2026-11-06T10:00:05Z [INF] MyApp.Worker all fine\n");
        var source = await Start("beginning", glob: "app.log", format: "text",
            lineRegex: @"^(?<timestamp>\S+) \[(?<level>\w+)\] (?<category>\S+) (?<message>.*)$");
        var result = await source.RunOnceAsync(default);
        var records = result.Batches.SelectMany(b => b.Records).ToList();
        Assert.That(records, Has.Count.EqualTo(2));
        Assert.That(records[0].Level, Is.EqualTo(RecordLevel.Error));
        Assert.That(records[0].Category, Is.EqualTo("MyApp.Worker"));
        Assert.That(records[0].Message, Is.EqualTo("boom happened\n   at MyApp.Worker.Run()\n   at MyApp.Program.Main()"));
        Assert.That(records[0].Timestamp, Is.EqualTo(new DateTimeOffset(2026, 11, 6, 10, 0, 0, TimeSpan.Zero)));
        Assert.That(records[1].Level, Is.EqualTo(RecordLevel.Information));
    }

    [Test]
    public async Task Settings_are_validated_at_start()
    {
        var bad = new[]
        {
            new JsonObject(),
            new JsonObject { ["paths"] = new JsonArray("x"), ["format"] = "xml" },
            new JsonObject { ["paths"] = new JsonArray("x"), ["format"] = "text" },
            new JsonObject { ["paths"] = new JsonArray("x"), ["format"] = "text", ["line_regex"] = "(" },
            new JsonObject { ["paths"] = new JsonArray("x"), ["start_at"] = "middle" },
        };
        foreach (var settings in bad)
            Assert.ThrowsAsync<SourceConfigurationException>(async () =>
                await new LogFileSource("x").StartAsync(new SourceContext("x", "log_files", "prod", settings, _dir.Path, _state, _time), default));
        await Task.CompletedTask;
    }

    [Test]
    public async Task A_glob_without_matches_is_healthy_and_empty()
    {
        var source = await Start("beginning", glob: "nothing*.jsonl");
        Assert.That(await Poll(source), Is.Empty);
        Assert.That((await source.GetHealthAsync(default)).Status, Is.EqualTo(SourceStatus.Healthy));
    }

    [Test]
    public async Task A_file_being_written_by_another_process_can_be_read()
    {
        await using var writer = new FileStream(Path("app.jsonl"), FileMode.Create, FileAccess.Write, FileShare.ReadWrite | FileShare.Delete);
        await writer.WriteAsync(Encoding.UTF8.GetBytes(Jl("while open") + "\n"));
        await writer.FlushAsync();
        var source = await Start("beginning");
        Assert.That(await Poll(source), Is.EqualTo(new[] { "while open" }));
    }
}
