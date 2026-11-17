using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.Playwright;
using MyApp.E2E.PageObjects.Common;

namespace MyApp.E2E.PageObjects.Help;

/// <summary>One entry of the downloaded file.</summary>
public sealed record UnansweredQuestion(string Question, string Reason, string Language, string RecordedAt, string Screen);

/// <summary>The file an Admin downloaded: its text and its entries.</summary>
public sealed class DownloadedQuestions
{
    public DownloadedQuestions(string text)
    {
        Text = text;
        using var document = JsonDocument.Parse(text);
        Entries = document.RootElement.EnumerateArray()
            .Select(e => new UnansweredQuestion(
                Question: Read(e, "question"),
                Reason: Read(e, "outcome"),
                Language: Read(e, "lang"),
                RecordedAt: Read(e, "recorded_at"),
                Screen: Read(e, "route")))
            .ToList();
    }

    /// <summary>The whole file, for "the file does not contain ...".</summary>
    public string Text { get; }

    public IReadOnlyList<UnansweredQuestion> Entries { get; }

    /// <summary>The entry with this question, or null.</summary>
    public UnansweredQuestion? EntryFor(string question) => Entries.FirstOrDefault(e => e.Question == question);

    private static string Read(JsonElement entry, string name) =>
        entry.TryGetProperty(name, out var value) && value.ValueKind != JsonValueKind.Null
            ? value.ToString()
            : string.Empty;
}

/// <summary>
/// The Unanswered questions screen (`/unanswered-questions`, Admin only): a period and the "Download" button
/// that saves the questions the manual could not answer. Every method is a step of a test case of the help
/// area. Files the browser downloads while the page is open are kept: <see cref="DownloadedFile"/> reads the
/// last one, <see cref="FilesDownloaded"/> counts them.
/// </summary>
public sealed class UnansweredQuestionsPage : BasePage
{
    private readonly ConcurrentQueue<IDownload> _downloads = new();

    protected override string? PagePath => "/unanswered-questions";

    public UnansweredQuestionsPage(IPage page) : base(page)
    {
        page.Download += (_, download) => _downloads.Enqueue(download);
    }

    // ----- Actions -----

    /// <summary>"Type ... in the "From" field." The format is yyyy-MM-dd.</summary>
    public Task TypeFrom(string date) => Field("From").FillAsync(date);

    /// <summary>"Type ... in the "To" field." The format is yyyy-MM-dd.</summary>
    public Task TypeTo(string date) => Field("To").FillAsync(date);

    /// <summary>"Click the "Download" button." Returns when the request has finished, whatever the outcome.</summary>
    public async Task ClickDownload()
    {
        LogStep("Click", "Download");
        await DownloadButton.ClickAsync();
        await Assertions.Expect(DownloadButton).ToBeEnabledAsync();
        await WaitForPageLoadAsync();
    }

    // ----- State -----

    /// <summary>The "Download" button; only an Admin sees the page that has it.</summary>
    public ILocator DownloadButton => Button("Download");

    /// <summary>The message under the "To" field, for example "The end date cannot be before the start date.".</summary>
    public ILocator ToFieldMessage => Field("To").Locator("xpath=following-sibling::*[1]");

    /// <summary>The number of files downloaded since the page object was created.</summary>
    public int FilesDownloaded => _downloads.Count;

    /// <summary>The last file downloaded, read and parsed; fails when none arrives.</summary>
    public async Task<DownloadedQuestions> DownloadedFile()
    {
        for (var i = 0; i < 100 && _downloads.IsEmpty; i++)
            await Task.Delay(100);

        var download = _downloads.LastOrDefault()
            ?? throw new InvalidOperationException("No file was downloaded.");
        var path = await download.PathAsync();
        return new DownloadedQuestions(await File.ReadAllTextAsync(path));
    }
}
