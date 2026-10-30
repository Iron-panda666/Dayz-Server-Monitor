namespace MagDeer.Core;

public enum ServerStatus { Online, Offline, QueryUnavailable, RestartExpected }
public enum QueryFailure { None, Timeout, Network, InvalidResponse, UnsupportedCompression }
public sealed record ServerInfo(string Name, string Map, string Game, byte Players, byte MaxPlayers, string Version)
{
    public DayZGameClock? GameClock { get; init; }
    public int? Queue { get; init; }
}
public sealed record QueryResult(DateTimeOffset At, ServerInfo? Info, double? RttMs, QueryFailure Failure, string Detail)
{
    public bool Success => Info is not null && Failure == QueryFailure.None;
    public string TimeSource { get; init; } = "Windows";
    public DateTimeOffset? SystemAt { get; init; }
    public DateTimeOffset? RequestedAt { get; init; }
}
public sealed record Recovery(DateTimeOffset LastSuccessBeforeGap, DateTimeOffset FirstFailure,
    DateTimeOffset LastFailure, DateTimeOffset RecoveredAt, int FailedQueries, bool NearPlannedRestart);

public sealed class AvailabilityTracker
{
    public QueryResult? Latest { get; private set; }
    public DateTimeOffset? LastSuccess { get; private set; }
    public DateTimeOffset? FirstFailure { get; private set; }
    public DateTimeOffset? LastFailure { get; private set; }
    public int Failures { get; private set; }
    public bool ManualOffline { get; private set; }
    public Recovery? LastRecovery { get; private set; }
    public bool RestartGap { get; private set; }
    public bool ConfirmedQueryGap { get; private set; }

    public void ConfirmOffline() { if (Failures > 0) ManualOffline = true; }
    public void ClearOffline() => ManualOffline = false;

    public Recovery? Apply(QueryResult result, int failureThreshold, RestartSchedule schedule)
    {
        Recovery? recovery = null;
        if (result.Success)
        {
            if (ConfirmedQueryGap && FirstFailure is { } first && LastSuccess is { } previous)
            {
                recovery = new(previous, first, LastFailure ?? first, result.At, Failures, RestartGap);
                LastRecovery = recovery;
            }
            LastSuccess = result.At;
            FirstFailure = null;
            LastFailure = null;
            Failures = 0;
            RestartGap = false;
            ConfirmedQueryGap = false;
            ManualOffline = false;
        }
        else
        {
            FirstFailure ??= result.At;
            LastFailure = result.At;
            Failures++;
            ConfirmedQueryGap |= Failures >= failureThreshold;
            var until = schedule.Next(result.At) - result.At;
            var since = result.At - schedule.Previous(result.At);
            RestartGap |= until <= TimeSpan.FromMinutes(15) || since < TimeSpan.FromMinutes(5);
        }
        Latest = result;
        return recovery;
    }

    public ServerStatus Status(DateTimeOffset now, RestartSchedule schedule, int threshold)
    {
        if (Latest?.Success == true) return ServerStatus.Online;
        if (ManualOffline) return ServerStatus.Offline;
        if (now - schedule.Previous(now) < TimeSpan.FromMinutes(5) || RestartGap && ConfirmedQueryGap)
            return ServerStatus.RestartExpected;
        // One missed query keeps a previously verified server online, but its metrics are unavailable.
        if (LastSuccess is not null && Failures < threshold) return ServerStatus.Online;
        return ServerStatus.QueryUnavailable;
    }
}
