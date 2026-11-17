using MyApp.Monitor.Masking;
using MyApp.Monitor.Sources;

namespace MyApp.Monitor.Analysis;

/// <summary>
/// Batch to decisions: duplicates dropped by <c>RecordId</c> (at-least-once delivery), records masked with the <c>signal</c> section
/// before anything is stored (operations.md 8.2), then analysed. Storing and committing the checkpoint are the caller's job.
/// </summary>
public sealed class AnalysisPipeline(Masker masker, Analyser analyser, ISourceStateStore state, TimeProvider time)
{
    public async Task<IReadOnlyList<AnalysisResult>> ProcessAsync(SignalBatch batch, CancellationToken ct)
    {
        var results = new List<AnalysisResult>();
        foreach (var raw in batch.Records)
        {
            if (!await state.TryMarkSeenAsync(batch.SourceId, raw.RecordId, time.GetUtcNow(), ct).ConfigureAwait(false)) continue;
            var masked = masker.MaskRecord(raw, out _);
            results.Add(analyser.Analyse(masked));
        }
        return results;
    }

    public AnalysisResult Process(LogRecord raw) => analyser.Analyse(masker.MaskRecord(raw, out _));
}
