namespace MagDeer.Core;

public enum GameSessionState { NotRunning, Running, Unavailable, MultipleInstances, Disabled }
public sealed record GameProcessCandidate(int Id, string Name, int WindowsSession, DateTimeOffset? StartedAt);
public sealed record GameSessionObservation(GameSessionState State, int? ProcessId = null, DateTimeOffset? StartedAt = null);
public sealed record GameSessionSnapshot(string State, DateTimeOffset? StartedAt, long? ElapsedSeconds)
{
    public string Source { get; init; } = "WindowsProcess";
}

public static class GameSessionProbe
{
    // Exact executable names: never Steam, DayZLauncher, DayZ_BE or DayZServer.
    public static IReadOnlyList<string> ProcessNames { get; } = Array.AsReadOnly(new[] { "DayZ_x64", "DayZ" });
    public static bool IsGame(string name) => ProcessNames.Contains(Path.GetFileNameWithoutExtension(name), StringComparer.OrdinalIgnoreCase);
    public static GameSessionObservation Select(IEnumerable<GameProcessCandidate> processes, int currentWindowsSession)
    {
        var games = processes.Where(p => IsGame(p.Name) && p.WindowsSession == currentWindowsSession).ToArray();
        if (games.Length == 0) return new(GameSessionState.NotRunning);
        if (games.Length > 1) return new(GameSessionState.MultipleInstances);
        var game = games[0];
        return game.StartedAt is null || game.Id <= 0 ? new(GameSessionState.Unavailable) : new(GameSessionState.Running, game.Id, game.StartedAt);
    }
}

/// <summary>Local game runtime, distinct from Steam overlay and server connection time.</summary>
public sealed class GameSessionTracker(TimeProvider? timeProvider = null)
{
    private readonly TimeProvider time = timeProvider ?? TimeProvider.System;
    private GameSessionState state = GameSessionState.Unavailable;
    private int? process;
    private DateTimeOffset? started;
    private long anchor;
    private TimeSpan initialElapsed;
    public void Observe(GameSessionObservation observation)
    {
        state = observation.State;
        if (state is GameSessionState.Disabled or GameSessionState.NotRunning or GameSessionState.MultipleInstances)
        { process = null; started = null; return; }
        if (state != GameSessionState.Running) return;
        if (observation.ProcessId == process && observation.StartedAt == started && process is not null) return;
        var now = time.GetUtcNow();
        if (observation.ProcessId is not > 0 || observation.StartedAt is not { } start || start > now)
        { state = GameSessionState.Unavailable; return; }
        process = observation.ProcessId; started = start; initialElapsed = now - start; anchor = time.GetTimestamp();
    }
    public GameSessionSnapshot Snapshot()
    {
        if (state != GameSessionState.Running || started is null) return new(state.ToString(), null, null);
        var elapsed = initialElapsed + time.GetElapsedTime(anchor);
        return new(state.ToString(), started, Math.Max(0, (long)Math.Floor(elapsed.TotalSeconds)));
    }
    public static string Format(long seconds) => $"{Math.Max(0, seconds) / 3600:00}:{Math.Max(0, seconds) / 60 % 60:00}:{Math.Max(0, seconds) % 60:00}";
}
