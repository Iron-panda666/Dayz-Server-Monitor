using System.Diagnostics;
using System.Text.Json;

namespace MagDeer.Core;

/// <summary>UI-thread-owned state. Only explicitly enabled profiles are polled, one at a time.</summary>
public sealed class BackgroundMonitoring
{
    private sealed class Entry(MonitorSettings settings)
    {
        public MonitorSettings Settings = settings;
        public string Signature = SignatureOf(settings);
        public int Generation;
        public AvailabilityTracker Tracker = new();
        public QueryRefreshGuard Guard = new();
        public long? Attempt;
        public bool Polling;
        public List<DeckSample> History = [];
    }
    private readonly Dictionary<string, Entry> entries = [];
    private readonly Func<string, int, TimeSpan, CancellationToken, Task<QueryResult>> query;
    private readonly Func<DateTimeOffset> utcNow;
    private readonly Func<string, QueryRefreshGuard> getGuard;
    private HistoryStore? historyStore;
    private bool busy;
    public Task CurrentTask { get; private set; } = Task.CompletedTask;
    public string? LastError { get; private set; }

    public BackgroundMonitoring(Func<DateTimeOffset> utcNow,
        Func<string, int, TimeSpan, CancellationToken, Task<QueryResult>>? query = null, Func<string, QueryRefreshGuard>? getGuard = null)
    { this.utcNow = utcNow; this.query = query ?? new A2sClient().QueryAsync; this.getGuard = getGuard ?? (_ => new()); }

    private static string SignatureOf(MonitorSettings s) => JsonSerializer.Serialize(new { s.ServerName, s.RestartTimes, s.ScheduleConfirmed, s.LogoutWarningMinutes, s.KickWarningMinutes });
    public void Configure(MonitorSettings settings, HistoryStore history)
    {
        historyStore = history;
        var enabled = settings.SavedServers.Where(p => p.MonitorInBackground)
            .Select(p => { var s = settings.Copy(); s.SavedServers = []; p.ApplyTo(s); return s; })
            .Where(s => s.EndpointKey != settings.EndpointKey).GroupBy(s => s.EndpointKey).Select(g => g.First()).Take(16).ToArray();
        foreach (var key in entries.Keys.Except(enabled.Select(s => s.EndpointKey)).ToArray()) entries.Remove(key);
        foreach (var s in enabled)
        {
            if (!entries.TryGetValue(s.EndpointKey, out var entry)) entries[s.EndpointKey] = new(s) { Guard = getGuard(s.EndpointKey) };
            else { string signature = SignatureOf(s); if (entry.Signature != signature) entry.Generation++; entry.Settings = s; entry.Signature = signature; }
        }
    }
    public void Tick(CancellationToken ct)
    {
        if (busy || ct.IsCancellationRequested) return;
        var now = utcNow();
        var next = entries.Values.OrderBy(e => e.Attempt ?? long.MinValue).FirstOrDefault(e => e.Attempt is null ||
            Stopwatch.GetElapsedTime(e.Attempt.Value).TotalSeconds >= Interval(e, now));
        if (next is not null) CurrentTask = PollAsync(next, ct);
    }
    public void InvalidateClockBasis()
    {
        foreach (var e in entries.Values) { e.Generation++; e.Tracker = new(); e.History.Clear(); e.Attempt = null; }
    }
    private static int Interval(Entry e, DateTimeOffset now) => new RestartSchedule(e.Settings.RestartTimes)
        .PollInterval(e.Settings, now, e.Tracker.RestartGap && e.Tracker.ConfirmedQueryGap);
    public DeckRefreshResult Refresh(string endpoint, CancellationToken ct)
    {
        if (!entries.TryGetValue(endpoint, out var e)) return new(false, 0, "server-not-monitored");
        if (busy || e.Polling) return new(false, e.Guard.RemainingSeconds, "polling");
        if (!e.Guard.TryBeginManual()) return new(false, e.Guard.RemainingSeconds, "cooldown");
        CurrentTask = PollAsync(e, ct, true);
        return new(true, e.Guard.RemainingSeconds, "accepted");
    }
    private async Task PollAsync(Entry e, CancellationToken ct, bool manual = false)
    {
        busy = e.Polling = true; long queryStamp = Stopwatch.GetTimestamp(); e.Attempt = queryStamp;
        if (!manual) e.Guard.RecordAutomaticStart();
        var s = e.Settings; int generation = e.Generation; var requested = utcNow();
        try
        {
            QueryResult result;
            try { result = await query(s.IpAddress, s.QueryPort, TimeSpan.FromSeconds(s.QueryTimeoutSeconds), ct); }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { return; }
            catch (Exception ex) { result = new(utcNow(), null, null, QueryFailure.Network, ex.Message); }
            if (!entries.TryGetValue(s.EndpointKey, out var current) || !ReferenceEquals(current, e) || generation != e.Generation) return;
            var now = utcNow();
            var elapsed = Stopwatch.GetElapsedTime(queryStamp);
            if (Math.Abs((now - requested - elapsed).TotalSeconds) > 5) requested = now - elapsed;
            result = result with { SystemAt = result.At, At = now, RequestedAt = requested, TimeSource = "MonitorClock" };
            var schedule = new RestartSchedule(s.RestartTimes);
            var recovery = e.Tracker.Apply(result, s.FailureThreshold, schedule);
            e.History.Add(new(QueryTimeline.TimeOf(result), result.Success ? result.Info?.Players : null, result.Success ? result.Info?.MaxPlayers : null));
            if (e.History.Count > 120) e.History.RemoveAt(0);
            if (historyStore is not null) await historyStore.AppendAsync(s.EndpointKey, result, e.Tracker.Status(now, schedule, s.FailureThreshold), recovery, ct);
            LastError = null;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { LastError = ex.Message; }
        finally { busy = e.Polling = false; }
    }
    public DeckSnapshot[] Snapshots(GameSessionSnapshot session)
    {
        var now = utcNow();
        return entries.Values.Select(e =>
        {
            var s = e.Settings; var schedule = new RestartSchedule(s.RestartTimes); var r = e.Tracker.Latest;
            var info = r?.Success == true ? r.Info : null;
            return new DeckSnapshot
            {
                Now = now, ServerName = s.ServerName, Endpoint = s.EndpointKey, Status = e.Tracker.Status(now, schedule, s.FailureThreshold).ToString(),
                NextRestartAt = schedule.Next(now), ScheduleConfirmed = s.ScheduleConfirmed,
                RestartExpected = now - schedule.Previous(now) < TimeSpan.FromMinutes(5) && (e.Tracker.LastSuccess is null || e.Tracker.LastSuccess < schedule.Previous(now)),
                LogoutWarningMinutes = s.LogoutWarningMinutes, KickWarningMinutes = s.KickWarningMinutes,
                LastQueryAt = r?.At, LastSuccessAt = e.Tracker.LastSuccess, QuerySuccess = r?.Success == true,
                Players = info?.Players, Capacity = info?.MaxPlayers, Queue = info?.Queue, RttMs = info is null ? null : r?.RttMs,
                GameClock = info?.GameClock, GameSession = session, Polling = e.Polling,
                RefreshRemainingSeconds = e.Guard.RemainingSeconds, QueryIntervalSeconds = Interval(e, now), History = e.History.ToArray()
            };
        }).OrderBy(s => s.ServerName, StringComparer.CurrentCultureIgnoreCase).ToArray();
    }
}
