using System.Collections.Concurrent;
using System.IO.Pipes;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace MagDeer.Core;

public sealed record DeckSample(DateTimeOffset At, int? Players, int? Capacity);
public sealed record DeckRefreshResult(bool Accepted, int RemainingSeconds, string Reason);
public sealed record DeckSnapshot
{
    public int ProtocolVersion { get; init; } = 1;
    public DateTimeOffset Now { get; init; }
    public string ServerName { get; init; } = "";
    public string Endpoint { get; init; } = "";
    public string Status { get; init; } = "QueryUnavailable";
    public DateTimeOffset NextRestartAt { get; init; }
    public bool RestartExpected { get; init; }
    public bool ScheduleConfirmed { get; init; }
    public int LogoutWarningMinutes { get; init; }
    public int KickWarningMinutes { get; init; }
    public DateTimeOffset? LastQueryAt { get; init; }
    public DateTimeOffset? LastSuccessAt { get; init; }
    public bool QuerySuccess { get; init; }
    public int? Players { get; init; }
    public int? Capacity { get; init; }
    public int? Queue { get; init; }
    public double? RttMs { get; init; }
    public DayZGameClock? GameClock { get; init; }
    public GameSessionSnapshot? GameSession { get; init; }
    public ServiceAvailability? Service { get; init; }
    public bool Polling { get; init; }
    public int RefreshRemainingSeconds { get; init; }
    public int QueryIntervalSeconds { get; init; }
    public DeckSample[] History { get; init; } = [];
}

/// <summary>Current-user-only, read-only state plus throttled manual refresh. No TCP listener.</summary>
public sealed class StreamDeckBridge : IAsyncDisposable
{
    public static readonly JsonSerializerOptions WireJson = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, MaxDepth = 16 };
    private readonly Func<DeckSnapshot> getState;
    private readonly Func<CancellationToken, Task<DeckRefreshResult>> refresh;
    private readonly Func<DeckSnapshot[]>? getMonitors;
    private readonly Func<string, CancellationToken, Task<DeckRefreshResult>>? refreshMonitor;
    private readonly CancellationTokenSource lifetime = new();
    private readonly ConcurrentDictionary<int, Task> clients = new();
    private readonly SemaphoreSlim slots = new(24, 24);
    private readonly string dataRoot, discoveryPath, session = Guid.NewGuid().ToString("N");
    private Task listener = Task.CompletedTask;
    private int nextClient;
    private bool started;
    public string PipeName { get; }
    public static string DefaultDiscoveryDirectory => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DayzServerMonitor", "bridges");

    public StreamDeckBridge(string directory, Func<DeckSnapshot> getState, Func<CancellationToken, Task<DeckRefreshResult>> refresh, string? discoveryDirectory = null,
        Func<DeckSnapshot[]>? getMonitors = null, Func<string, CancellationToken, Task<DeckRefreshResult>>? refreshMonitor = null)
    {
        dataRoot = Path.GetFullPath(directory);
        this.getState = getState; this.refresh = refresh;
        this.getMonitors = getMonitors; this.refreshMonitor = refreshMonitor;
        var identity = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(dataRoot.ToUpperInvariant())))[..20];
        PipeName = "DayzServerMonitor-" + identity;
        discoveryPath = Path.Combine(discoveryDirectory ?? DefaultDiscoveryDirectory, identity + ".json");
    }
    public void Start()
    {
        if (started) throw new InvalidOperationException("Bridge is already running.");
        JsonStorage.Write(discoveryPath, new { protocolVersion = 1, pipeName = PipeName, dataRoot, pid = Environment.ProcessId, sessionId = session, startedAt = DateTimeOffset.UtcNow });
        started = true; listener = ListenAsync();
    }
    private async Task ListenAsync()
    {
        var ct = lifetime.Token;
        while (!ct.IsCancellationRequested)
        {
            bool reserved = false;
            NamedPipeServerStream? pipe = null;
            try
            {
                await slots.WaitAsync(ct).ConfigureAwait(false); reserved = true;
                pipe = new(PipeName, PipeDirection.InOut, 24, PipeTransmissionMode.Byte, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
                await pipe.WaitForConnectionAsync(ct).ConfigureAwait(false);
                int id = Interlocked.Increment(ref nextClient);
                var task = ServeWithSlotAsync(pipe, ct);
                pipe = null; reserved = false;
                clients[id] = task;
                _ = task.ContinueWith(completed => { _ = completed.Exception; clients.TryRemove(id, out _); }, TaskScheduler.Default);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { break; }
            catch (IOException)
            {
                try { await Task.Delay(500, ct).ConfigureAwait(false); } catch (OperationCanceledException) { break; }
            }
            finally { pipe?.Dispose(); if (reserved) slots.Release(); }
        }
    }
    private async Task ServeWithSlotAsync(NamedPipeServerStream pipe, CancellationToken ct)
    {
        try { await ServeAsync(pipe, ct).ConfigureAwait(false); }
        finally { slots.Release(); }
    }
    private async Task ServeAsync(NamedPipeServerStream pipe, CancellationToken ct)
    {
        using (pipe)
        using (var reader = new StreamReader(pipe, new UTF8Encoding(false, true), false, 1024, true))
        using (var writer = new StreamWriter(pipe, new UTF8Encoding(false), 1024, true) { NewLine = "\n" })
        {
            try
            {
                while (!ct.IsCancellationRequested)
                {
                    using var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct);
                    deadline.CancelAfter(TimeSpan.FromSeconds(5));
                    var line = new StringBuilder(); var character = new char[1];
                    while (true)
                    {
                        if (await reader.ReadAsync(character.AsMemory(), deadline.Token).ConfigureAwait(false) == 0) return;
                        if (character[0] == '\n') break;
                        if (line.Length >= 1024) return;
                        if (character[0] != '\r') line.Append(character[0]);
                    }
                    using var request = JsonDocument.Parse(line.ToString(), new JsonDocumentOptions { MaxDepth = 4 });
                    string? command = request.RootElement.TryGetProperty("command", out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
                    string endpoint = request.RootElement.TryGetProperty("endpoint", out var target) && target.ValueKind == JsonValueKind.String ? target.GetString() ?? "" : "";
                    var active = getState();
                    var monitors = getMonitors?.Invoke() ?? [active];
                    var state = endpoint.Length == 0 ? active : monitors.FirstOrDefault(s => s.Endpoint == endpoint);
                    if (state is null)
                    {
                        await writer.WriteLineAsync("{\"ok\":false,\"error\":\"server-not-monitored\"}".AsMemory(), deadline.Token).ConfigureAwait(false);
                        await writer.FlushAsync(deadline.Token).ConfigureAwait(false); continue;
                    }
                    DeckSnapshot Current() => endpoint.Length == 0 ? getState() : getMonitors?.Invoke().FirstOrDefault(s => s.Endpoint == endpoint) ?? state;
                    string response = command switch
                    {
                        "snapshot" => JsonSerializer.Serialize(new { ok = true, state, monitors = monitors.Select(s => new { s.Endpoint, s.ServerName }) }, WireJson),
                        "refresh" => JsonSerializer.Serialize(new { ok = true, refresh = endpoint.Length == 0 || refreshMonitor is null && endpoint == active.Endpoint
                            ? await refresh(deadline.Token).ConfigureAwait(false)
                            : refreshMonitor is null ? new(false, 0, "server-not-monitored") : await refreshMonitor(endpoint, deadline.Token).ConfigureAwait(false), state = Current() }, WireJson),
                        _ => "{\"ok\":false,\"error\":\"unsupported-command\"}"
                    };
                    await writer.WriteLineAsync(response.AsMemory(), deadline.Token).ConfigureAwait(false);
                    await writer.FlushAsync(deadline.Token).ConfigureAwait(false);
                }
            }
            catch (Exception ex) when (ex is OperationCanceledException or IOException or JsonException or InvalidOperationException or DecoderFallbackException) { }
        }
    }
    public async ValueTask DisposeAsync()
    {
        if (!started) return;
        lifetime.Cancel();
        try
        {
            if (File.Exists(discoveryPath))
            {
                using var document = JsonDocument.Parse(File.ReadAllText(discoveryPath));
                if (document.RootElement.GetProperty("sessionId").GetString() == session) File.Delete(discoveryPath);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or KeyNotFoundException) { }
        await listener.ConfigureAwait(false);
        await Task.WhenAll(clients.Values).ConfigureAwait(false);
        lifetime.Dispose(); slots.Dispose(); started = false;
    }
}
