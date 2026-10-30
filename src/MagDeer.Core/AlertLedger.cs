namespace MagDeer.Core;

public sealed record RestartAlert(string Key, string Title, string Message, int Minutes, DateTimeOffset RestartAt);

public sealed class AlertLedger
{
    private readonly Dictionary<string, DateTimeOffset> delivered;
    public AlertLedger(Dictionary<string, DateTimeOffset>? delivered = null) => this.delivered = delivered ?? [];
    public Dictionary<string, DateTimeOffset> Snapshot() => new(delivered);

    public RestartAlert? Evaluate(DateTimeOffset now, RestartSchedule schedule, string endpoint, int logoutMinutes = 10, int kickMinutes = 5)
    {
        foreach (var oldKey in delivered.Where(x => now - x.Value > TimeSpan.FromDays(7)).Select(x => x.Key).ToArray())
            delivered.Remove(oldKey);
        var previous = schedule.Previous(now);
        var next = schedule.Next(now);
        var elapsed = now - previous;
        var remaining = next - now;
        int minutes;
        DateTimeOffset restart;
        if (elapsed < TimeSpan.FromMinutes(1)) { minutes = 0; restart = previous; }
        else if (remaining <= TimeSpan.FromMinutes(kickMinutes)) { minutes = kickMinutes; restart = next; }
        else if (remaining <= TimeSpan.FromMinutes(logoutMinutes)) { minutes = logoutMinutes; restart = next; }
        else return null;
        var prefix = $"{endpoint}|{restart.UtcTicks}|";
        var key = prefix + minutes;
        if (delivered.ContainsKey(key)) return null;
        // Starting at T-4 must not emit the obsolete T-10 warning as well.
        foreach (var level in new[] { logoutMinutes, kickMinutes, 0 }.Where(x => x >= minutes)) delivered[prefix + level] = now;
        return new(key, minutes == 0 ? "Očekávaný restart" : $"Restart za {minutes} min",
            minutes == 0 ? "Nastal plánovaný čas. Sledujeme odpovědi query služby."
            : minutes == kickMinutes ? "Podle plánu se očekává kick hráčů. Odhlaste se."
            : "Doporučujeme se odhlásit. Čas vychází z nastaveného plánu.", minutes, restart);
    }
}
