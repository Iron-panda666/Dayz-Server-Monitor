using System.Diagnostics;
using System.Net;
using System.Text.Json;

namespace MagDeer.Core;

public sealed record ServiceDefinition(string Id, string Name, string Url, string Scope);
public sealed record ServiceAvailability(string State, string Description, string SourceUrl, string Scope, DateTimeOffset? CheckedAt, int ConsecutiveFailures);

/// <summary>Public status/reachability only. Never signs into accounts or reads messages or local applications.</summary>
public sealed class ServiceMonitoring : IDisposable
{
    public static readonly ServiceDefinition[] Definitions =
    [
        new("discord", "Discord", "https://discordstatus.com/api/v2/status.json", "ProviderStatus"),
        new("spotify", "Spotify", "https://open.spotify.com/", "WebReachability"),
        new("whatsapp", "WhatsApp", "https://web.whatsapp.com/", "WebReachability"),
        new("steam", "Steam Store", "https://store.steampowered.com/", "WebReachability")
    ];
    private readonly HttpClient http;
    private readonly Func<DateTimeOffset> utcNow;
    private readonly Func<string, QueryRefreshGuard> guard;
    private readonly Dictionary<string, ServiceAvailability> states = [];
    private long? attempted;
    private bool enabled, busy;
    private int generation;
    public Task CurrentTask { get; private set; } = Task.CompletedTask;
    public ServiceMonitoring(Func<DateTimeOffset> utcNow, Func<string, QueryRefreshGuard> guard, HttpMessageHandler? handler = null)
    {
        this.utcNow = utcNow; this.guard = guard;
        http = handler is null ? new(new SocketsHttpHandler { AllowAutoRedirect = false, AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate }) : new(handler);
        http.Timeout = TimeSpan.FromSeconds(8); http.DefaultRequestHeaders.UserAgent.ParseAdd("DayzServerMonitor/1.10.0");
    }
    public void Configure(bool value)
    { if (enabled == value) return; enabled = value; generation++; states.Clear(); attempted = null; }
    public void Tick(CancellationToken ct)
    {
        if (!enabled || busy || ct.IsCancellationRequested || attempted is { } stamp && Stopwatch.GetElapsedTime(stamp).TotalSeconds < 300) return;
        attempted = Stopwatch.GetTimestamp(); CurrentTask = PollAsync(Definitions, ct);
    }
    public DeckRefreshResult Refresh(string endpoint, CancellationToken ct)
    {
        var definition = Definitions.FirstOrDefault(d => "service:" + d.Id == endpoint);
        if (!enabled || definition is null) return new(false, 0, "service-not-monitored");
        var g = guard(endpoint);
        if (busy) return new(false, g.RemainingSeconds, "polling");
        if (!g.TryBeginManual()) return new(false, g.RemainingSeconds, "cooldown");
        CurrentTask = PollAsync([definition], ct); return new(true, g.RemainingSeconds, "accepted");
    }
    private async Task PollAsync(IEnumerable<ServiceDefinition> definitions, CancellationToken ct)
    {
        busy = true; int currentGeneration = generation;
        try
        {
            foreach (var d in definitions)
            {
                if (ct.IsCancellationRequested || !enabled || generation != currentGeneration) break;
                var g = guard("service:" + d.Id); g.RecordAutomaticStart();
                ServiceAvailability value;
                try
                {
                    using var request = new HttpRequestMessage(d.Scope == "ProviderStatus" ? HttpMethod.Get : HttpMethod.Head, d.Url);
                    using var reply = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
                    if (d.Scope == "ProviderStatus")
                    {
                        reply.EnsureSuccessStatusCode();
                        if (reply.Content.Headers.ContentLength > 65536) throw new InvalidDataException("Oversized status reply");
                        using var bodyDeadline = CancellationTokenSource.CreateLinkedTokenSource(ct); bodyDeadline.CancelAfter(TimeSpan.FromSeconds(4));
                        await using var stream = await reply.Content.ReadAsStreamAsync(bodyDeadline.Token);
                        using var memory = new MemoryStream(); var chunk = new byte[4096]; int read;
                        while ((read = await stream.ReadAsync(chunk, bodyDeadline.Token)) > 0)
                        { if (memory.Length + read > 65536) throw new InvalidDataException("Oversized status reply"); memory.Write(chunk, 0, read); }
                        using var json = JsonDocument.Parse(memory.ToArray(), new JsonDocumentOptions { MaxDepth = 8 });
                        value = ParseStatuspage(json.RootElement, d, utcNow());
                    }
                    else
                    {
                        // Redirect proves the front door responded; authentication/bot blocks are unknown, not an outage.
                        int status = (int)reply.StatusCode;
                        value = status is >= 200 and < 400
                            ? new("Reachable", $"HTTP {status}: web endpoint reachable from this PC; backend/login untested.", d.Url, d.Scope, utcNow(), 0)
                            : new("Unavailable", $"HTTP {status}: reachability uncertain; not evidence of a global outage.", d.Url, d.Scope, utcNow(), 0);
                    }
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested) { break; }
                catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException or IOException or JsonException or KeyNotFoundException or InvalidOperationException)
                { value = new("Unavailable", "Check unavailable from this PC: " + ex.Message, d.Url, d.Scope, utcNow(), 0); }
                if (!enabled || generation != currentGeneration) break;
                int failures = value.State == "Unavailable" ? (states.TryGetValue(d.Id, out var previous) ? previous.ConsecutiveFailures : 0) + 1 : 0;
                states[d.Id] = value with { ConsecutiveFailures = failures };
            }
        }
        finally { busy = false; }
    }
    public static ServiceAvailability ParseStatuspage(JsonElement json, ServiceDefinition definition, DateTimeOffset now)
    {
        var status = json.GetProperty("status");
        string indicator = status.GetProperty("indicator").GetString() ?? "";
        string description = status.GetProperty("description").GetString() ?? "Status not described";
        string state = indicator == "none" ? "Operational" : indicator is "minor" or "major" or "critical" or "maintenance" ? "Degraded" : "Unavailable";
        return new(state, description.Length > 300 ? description[..300] : description, definition.Url, definition.Scope, now, 0);
    }
    public DeckSnapshot[] Snapshots(GameSessionSnapshot session)
    {
        if (!enabled) return [];
        var now = utcNow();
        return Definitions.Select(d =>
        {
            var service = states.TryGetValue(d.Id, out var value) ? value : new("Checking", "Waiting for a public status/reachability check.", d.Url, d.Scope, null, 0);
            return new DeckSnapshot { Now = now, ServerName = d.Name, Endpoint = "service:" + d.Id,
                Status = service.State, Service = service, LastQueryAt = service.CheckedAt,
                QueryIntervalSeconds = 300, Polling = busy, RefreshRemainingSeconds = guard("service:" + d.Id).RemainingSeconds, GameSession = session };
        }).ToArray();
    }
    public void Dispose() => http.Dispose();
}
