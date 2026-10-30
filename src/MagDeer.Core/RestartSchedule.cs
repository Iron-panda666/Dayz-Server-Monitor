using System.Globalization;

namespace MagDeer.Core;

public sealed class RestartSchedule
{
    public static TimeZoneInfo Prague { get; } = FindPrague();
    private readonly TimeOnly[] times;
    public RestartSchedule(IEnumerable<string> times) => this.times = times.Select(t =>
        TimeOnly.ParseExact(t, "HH:mm", CultureInfo.InvariantCulture)).Distinct().Order().ToArray();

    private static TimeZoneInfo FindPrague()
    {
        try { return TimeZoneInfo.FindSystemTimeZoneById("Europe/Prague"); }
        catch (TimeZoneNotFoundException) { return TimeZoneInfo.FindSystemTimeZoneById("Central Europe Standard Time"); }
    }

    public static DateTimeOffset CzechTime(DateTimeOffset instant) => TimeZoneInfo.ConvertTime(instant, Prague);

    public IEnumerable<DateTimeOffset> Around(DateTimeOffset now)
    {
        var date = DateOnly.FromDateTime(CzechTime(now).DateTime);
        var slots = new List<DateTimeOffset>();
        for (int day = -2; day <= 3; day++)
        foreach (var time in times)
        {
            var local = date.AddDays(day).ToDateTime(time, DateTimeKind.Unspecified);
            // A nonexistent spring time is skipped. Both occurrences of an autumn time are real slots.
            if (Prague.IsInvalidTime(local)) continue;
            if (Prague.IsAmbiguousTime(local))
                slots.AddRange(Prague.GetAmbiguousTimeOffsets(local).Select(offset => new DateTimeOffset(local, offset).ToUniversalTime()));
            else slots.Add(new DateTimeOffset(local, Prague.GetUtcOffset(local)).ToUniversalTime());
        }
        return slots.Distinct().Order();
    }

    public DateTimeOffset Next(DateTimeOffset now) => Around(now).First(t => t > now);
    public DateTimeOffset Previous(DateTimeOffset now) => Around(now).Last(t => t <= now);

    public static string[] GenerateDaily(int everyHours, string anchor)
    {
        if (everyHours is < 1 or > 24 || 24 % everyHours != 0)
            throw new ArgumentException("Pro stejný denní plán zvolte interval 1, 2, 3, 4, 6, 8, 12 nebo 24 hodin. Ostatní časy zadejte ručně.");
        var start = TimeOnly.ParseExact(anchor, "HH:mm", CultureInfo.InvariantCulture);
        return Enumerable.Range(0, 24 / everyHours).Select(i => start.AddHours(i * everyHours))
            .Order().Select(t => t.ToString("HH:mm", CultureInfo.InvariantCulture)).ToArray();
    }

    public int PollInterval(MonitorSettings settings, DateTimeOffset now, bool restartGap)
    {
        if (restartGap || now - Previous(now) < TimeSpan.FromMinutes(5)) return settings.RestartIntervalSeconds;
        if (Next(now) - now <= TimeSpan.FromMinutes(15)) return settings.BeforeRestartIntervalSeconds;
        return settings.NormalIntervalSeconds;
    }
}
