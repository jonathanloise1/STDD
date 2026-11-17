using MyApp.Application.DTOs.Help;

namespace MyApp.Application.Help;

/// <summary>Why an answer request did not start. The web layer maps the kind to a status code.</summary>
public enum HelpAnswerFailure
{
    /// <summary>The request is malformed or names an unknown section (400).</summary>
    InvalidRequest,

    /// <summary>The pack of the panel is not the loaded one (409).</summary>
    PackChanged,

    /// <summary>The server tier is off or not available (503).</summary>
    Unavailable,

    /// <summary>All generation slots stayed taken for the queue timeout (503).</summary>
    Busy,

    /// <summary>The host did not accept the request within the time limit (504).</summary>
    Timeout
}

public sealed class HelpAnswerException(HelpAnswerFailure failure, string message) : Exception(message)
{
    public HelpAnswerFailure Failure { get; } = failure;
}

/// <summary>One line of the answer stream.</summary>
public sealed record HelpAnswerEvent(string Type, string? Text = null, string? Outcome = null,
    IReadOnlyList<int>? Cited = null, string? Body = null, string? Model = null, string? Device = null, string? Reason = null)
{
    public const string Delta = "delta";
    public const string Done = "done";
    public const string Error = "error";
}

/// <summary>The state of the model host as the status and the answer need it.</summary>
public sealed record HelpHostState(bool Reachable, string? Alias, string? Device, bool AliasMatches);

/// <summary>
/// The server tier (models.md 7.4): the status for the panel and the streamed answer. The host is probed at most
/// every <see cref="ProbeCache"/>.
/// </summary>
public sealed class HelpAssistant : IDisposable
{
    private readonly HelpRuntime _runtime;
    private readonly IModelHost _host;
    private readonly SemaphoreSlim _slots;
    private readonly TimeSpan _probeCache;
    private readonly SemaphoreSlim _probeLock = new(1, 1);
    private HelpHostState? _state;
    private long _stateAt;

    public HelpAssistant(HelpRuntime runtime, IModelHost host, TimeSpan? probeCache = null)
    {
        _runtime = runtime;
        _host = host;
        _slots = new SemaphoreSlim(runtime.Settings.ServerMaxConcurrent, runtime.Settings.ServerMaxConcurrent);
        _probeCache = probeCache ?? TimeSpan.FromSeconds(10);
    }

    /// <summary>How long a probe result is reused.</summary>
    public TimeSpan ProbeCache => _probeCache;

    private bool ServerAllowed => _runtime.Settings.ServerEnabled && _runtime.GenerationAllowed;

    // ---------------------------------------------------------------- status

    /// <summary>The values the panel needs (models.md 7.4, 7.7).</summary>
    // US-HELP-04, US-HELP-05
    public async Task<HelpStatusDto> GetStatusAsync(CancellationToken ct)
    {
        var s = _runtime.Settings;
        var allowed = _runtime.GenerationAllowed;
        var server = new HelpServerStatusDto { Enabled = ServerAllowed, TimeoutSeconds = s.ServerTimeoutSeconds };
        if (server.Enabled)
        {
            var state = await GetHostStateAsync(ct);
            server.Available = state.Reachable && state.AliasMatches;
            server.Device = server.Available ? state.Device : null;
            server.Reason = !state.Reachable ? "host_unreachable" : !state.AliasMatches ? "model_alias_mismatch" : null;
        }

        return new HelpStatusDto
        {
            Order = allowed ? s.Order : HelpSettings.OrderRetrievalOnly,
            ConfiguredOrder = s.Order,
            Compatible = _runtime.Compatibility.Compatible,
            Reason = _runtime.Compatibility.Reason,
            Pack = new HelpPackStatusDto
            {
                Loaded = _runtime.Packs.Count > 0,
                ProjectVersion = _runtime.Packs.Values.OrderBy(p => p.Lang, StringComparer.Ordinal).Select(p => p.ProjectVersion).FirstOrDefault(),
                Sha256 = _runtime.Packs.ToDictionary(p => p.Key, p => p.Value.Sha256)
            },
            Model = _runtime.Lock is { } l ? new HelpModelStatusDto { Id = l.ModelId, Version = l.ModelVersion } : null,
            Browser = new HelpBrowserStatusDto
            {
                Enabled = s.BrowserEnabled && allowed,
                ModelBaseUrl = s.BrowserModelBaseUrl,
                MinTokensPerSecond = s.BrowserMinTokensPerSecond,
                AskBeforeDownloadMb = s.BrowserAskBeforeDownloadMb,
                TimeoutSeconds = s.BrowserTimeoutSeconds
            },
            Server = server,
            GapsEnabled = s.GapsEnabled
        };
    }

    private async Task<HelpHostState> GetHostStateAsync(CancellationToken ct)
    {
        await _probeLock.WaitAsync(ct);
        try
        {
            if (_state is not null && _probeCache > TimeSpan.Zero
                && System.Diagnostics.Stopwatch.GetElapsedTime(_stateAt) < _probeCache)
                return _state;

            ModelHostInfo? info = null;
            try
            {
                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
                timeout.CancelAfter(TimeSpan.FromSeconds(2));
                info = await _host.GetInfoAsync(timeout.Token);
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
                // the probe timed out: the host counts as unreachable
            }
            catch (ModelHostUnavailableException)
            {
            }

            _state = Evaluate(info);
            _stateAt = System.Diagnostics.Stopwatch.GetTimestamp();
            return _state;
        }
        finally
        {
            _probeLock.Release();
        }
    }

    /// <summary>The host alias must be <c>{model id}-{model_version}@{gpu|cpu}</c> of the lock (models.md 7.4, 7.5).</summary>
    private HelpHostState Evaluate(ModelHostInfo? info)
    {
        if (info is null)
            return new HelpHostState(false, null, null, false);
        if (_runtime.Lock is not { } l)
            return new HelpHostState(true, info.Alias, null, false);

        var prefix = $"{l.ModelId}-{l.ModelVersion}@";
        var device = info.Alias.StartsWith(prefix, StringComparison.Ordinal) ? info.Alias[prefix.Length..] : null;
        return device is "gpu" or "cpu"
            ? new HelpHostState(true, info.Alias, device, true)
            : new HelpHostState(true, info.Alias, null, false);
    }

    // ---------------------------------------------------------------- answer

    /// <summary>
    /// Validates the request, waits for a generation slot and sends the prompt to the host. Throws
    /// <see cref="HelpAnswerException"/> before anything is streamed; after it returns, the stream always ends with
    /// a <c>done</c> or an <c>error</c> event.
    /// </summary>
    // US-HELP-05
    public async Task<HelpAnswerStream> StartAnswerAsync(HelpAnswerRequest request, CancellationToken ct)
    {
        var pack = Resolve(request, out var protocol, out var selected, out var question);

        if (!ServerAllowed)
            throw new HelpAnswerException(HelpAnswerFailure.Unavailable, "The assistant on the server is not available.");

        var state = await GetHostStateAsync(ct);
        if (!state.Reachable || !state.AliasMatches)
            throw new HelpAnswerException(HelpAnswerFailure.Unavailable, "The assistant on the server is not available.");

        var settings = _runtime.Settings;
        if (!await _slots.WaitAsync(TimeSpan.FromSeconds(settings.ServerQueueTimeoutSeconds), ct))
            throw new HelpAnswerException(HelpAnswerFailure.Busy, "The assistant is busy. Try again in a moment.");

        var released = false;
        void Release()
        {
            if (!released)
            {
                released = true;
                _slots.Release();
            }
        }

        CancellationTokenSource? limit = null;
        try
        {
            limit = CancellationTokenSource.CreateLinkedTokenSource(ct);
            limit.CancelAfter(TimeSpan.FromSeconds(settings.ServerTimeoutSeconds));

            var messages = protocol.RenderMessages(pack.Lang, pack.ProjectName,
                selected.Select(s => new ProtocolSection(s.Title, s.Text)).ToList(), question);
            var deltas = await _host.StartAsync(new ModelChatRequest(
                state.Alias!, messages.System, messages.User, _runtime.Lock!.ChatTemplateKwargs,
                protocol.Temperature, protocol.MaxNewTokens), limit.Token);

            return new HelpAnswerStream(deltas, limit, Release, protocol, pack.Lang, selected.Count, state.Alias!, state.Device!, ct);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            limit?.Dispose();
            Release();
            throw new HelpAnswerException(HelpAnswerFailure.Timeout, "The assistant took too long.");
        }
        catch (ModelHostUnavailableException)
        {
            limit?.Dispose();
            Release();
            throw new HelpAnswerException(HelpAnswerFailure.Unavailable, "The assistant on the server is not available.");
        }
        catch
        {
            limit?.Dispose();
            Release();
            throw;
        }
    }

    private HelpPack Resolve(HelpAnswerRequest request, out AssistantProtocol protocol, out List<PackSection> selected, out string question)
    {
        var lang = request.Lang;
        if (string.IsNullOrWhiteSpace(lang) || !_runtime.Packs.TryGetValue(lang, out var pack) || pack.Protocol is null || !pack.Protocol.HasLanguage(lang))
            throw new HelpAnswerException(HelpAnswerFailure.InvalidRequest, "The language is not known.");
        protocol = pack.Protocol;

        if (string.IsNullOrWhiteSpace(request.PackSha256))
            throw new HelpAnswerException(HelpAnswerFailure.InvalidRequest, "The pack hash is missing.");
        if (!string.Equals(request.PackSha256, pack.Sha256, StringComparison.OrdinalIgnoreCase))
            throw new HelpAnswerException(HelpAnswerFailure.PackChanged, "The help content changed. Reload it and ask again.");

        question = protocol.NormalizeQuestion(request.Question ?? string.Empty);
        if (question.Length == 0)
            throw new HelpAnswerException(HelpAnswerFailure.InvalidRequest, "The question is empty.");

        var ids = request.SectionIds ?? [];
        if (ids.Count == 0 || ids.Count > protocol.ScanLimit || ids.Distinct(StringComparer.Ordinal).Count() != ids.Count)
            throw new HelpAnswerException(HelpAnswerFailure.InvalidRequest, "The sections are missing or repeated.");

        var ranked = new List<PackSection>();
        foreach (var id in ids)
        {
            if (!pack.Sections.TryGetValue(id, out var section))
                throw new HelpAnswerException(HelpAnswerFailure.InvalidRequest, "A section is not in the help pack.");
            ranked.Add(section);
        }

        // The panel sends the selected sections; the selection of models.md 3.5 is applied again so the prompt can never exceed the budget.
        selected = protocol.SelectSections(ranked, s => s.Text);
        if (selected.Count == 0)
            throw new HelpAnswerException(HelpAnswerFailure.InvalidRequest, "No section fits the context.");
        return pack;
    }

    public void Dispose()
    {
        _slots.Dispose();
        _probeLock.Dispose();
    }
}

/// <summary>
/// The running generation. Enumerate <see cref="Events"/> once; disposing frees the generation slot.
/// </summary>
public sealed class HelpAnswerStream : IAsyncDisposable
{
    private readonly IAsyncEnumerable<string> _deltas;
    private readonly CancellationTokenSource _limit;
    private readonly Action _release;
    private readonly AssistantProtocol _protocol;
    private readonly string _lang;
    private readonly int _k;
    private readonly string _alias;
    private readonly string _device;
    private readonly CancellationToken _requestAborted;

    internal HelpAnswerStream(IAsyncEnumerable<string> deltas, CancellationTokenSource limit, Action release,
        AssistantProtocol protocol, string lang, int k, string alias, string device, CancellationToken requestAborted)
    {
        _deltas = deltas;
        _limit = limit;
        _release = release;
        _protocol = protocol;
        _lang = lang;
        _k = k;
        _alias = alias;
        _device = device;
        _requestAborted = requestAborted;
    }

    /// <summary>The deltas as they arrive, then <c>done</c> (with the parsed outcome) or <c>error</c> (<c>timeout</c>, <c>unavailable</c>).</summary>
    public async IAsyncEnumerable<HelpAnswerEvent> Events()
    {
        var text = new System.Text.StringBuilder();
        var enumerator = _deltas.GetAsyncEnumerator(_limit.Token);
        string? failure = null;
        try
        {
            while (true)
            {
                string? delta = null;
                try
                {
                    if (!await enumerator.MoveNextAsync())
                        break;
                    delta = enumerator.Current;
                }
                catch (OperationCanceledException) when (!_requestAborted.IsCancellationRequested)
                {
                    failure = "timeout";
                    break;
                }
                catch (ModelHostUnavailableException)
                {
                    failure = "unavailable";
                    break;
                }

                if (delta.Length == 0)
                    continue;
                text.Append(delta);
                yield return new HelpAnswerEvent(HelpAnswerEvent.Delta, Text: delta);
            }
        }
        finally
        {
            await enumerator.DisposeAsync();
        }

        if (failure is not null)
        {
            yield return new HelpAnswerEvent(HelpAnswerEvent.Error, Reason: failure);
            yield break;
        }

        var full = text.ToString();
        var parsed = _protocol.ParseAnswer(_lang, full, _k);
        yield return new HelpAnswerEvent(HelpAnswerEvent.Done, Text: full,
            Outcome: parsed.Outcome.ToString().ToLowerInvariant(), Cited: parsed.Cited, Body: parsed.Body,
            Model: _alias, Device: _device);
    }

    public ValueTask DisposeAsync()
    {
        _limit.Dispose();
        _release();
        return ValueTask.CompletedTask;
    }
}
