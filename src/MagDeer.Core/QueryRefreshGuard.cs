namespace MagDeer.Core;

/// <summary>Manual refresh throttle independent of wall-clock adjustments.</summary>
public sealed class QueryRefreshGuard(TimeProvider? timeProvider = null)
{
    private readonly TimeProvider time = timeProvider ?? TimeProvider.System;
    private readonly object gate = new();
    private long? lastStart;
    public int RemainingSeconds
    {
        get { lock (gate) return Remaining(); }
    }
    private int Remaining() => lastStart is { } stamp
        ? Math.Max(0, (int)Math.Ceiling(20 - time.GetElapsedTime(stamp).TotalSeconds)) : 0;
    public bool TryBeginManual()
    {
        lock (gate)
        {
            if (Remaining() > 0) return false;
            lastStart = time.GetTimestamp();
            return true;
        }
    }
    // Automatic restart monitoring may use its configured 15-second interval.
    public void RecordAutomaticStart() { lock (gate) lastStart = time.GetTimestamp(); }
}
