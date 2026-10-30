using System.Globalization;

namespace MagDeer.Core;

public sealed record DayZGameClock(int Hour, int Minute, double? DayMultiplier, double? NightMultiplier);

public static class DayZKeywords
{
    public static (DayZGameClock? Clock, int? Queue) Parse(string keywords)
    {
        TimeOnly? clock = null;
        double? day = null, night = null;
        int? queue = null;
        foreach (var tag in keywords.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        {
            if (TimeOnly.TryParseExact(tag, "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var time)) clock = time;
            else if (tag.StartsWith("entm", StringComparison.Ordinal)) night = Multiplier(tag[4..]);
            else if (tag.StartsWith("etm", StringComparison.Ordinal)) day = Multiplier(tag[3..]);
            else if (tag.StartsWith("lqs", StringComparison.Ordinal) && int.TryParse(tag[3..], NumberStyles.None, CultureInfo.InvariantCulture, out int value) && value is >= 0 and <= 10000) queue = value;
        }
        return (clock is { } at ? new(at.Hour, at.Minute, day, night) : null, queue);
    }
    private static double? Multiplier(string value) => double.TryParse(value, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out double number)
        && double.IsFinite(number) && number is >= .1 and <= 64 ? number : null;
}
