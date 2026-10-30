namespace MagDeer.Core;

public readonly record struct QueryTimeline(DateTimeOffset Start, DateTimeOffset End)
{
    public static DateTimeOffset TimeOf(QueryResult result) => result.RequestedAt ?? result.At;
    public static QueryTimeline From(IEnumerable<QueryResult> samples)
    {
        var times = samples.Select(TimeOf).ToArray();
        if (times.Length == 0) throw new ArgumentException("No samples.", nameof(samples));
        var start = times.Min(); var end = times.Max();
        return new(start, end > start ? end : start.AddMinutes(1));
    }
    public double Fraction(DateTimeOffset time) => Math.Clamp((time - Start).TotalSeconds / (End - Start).TotalSeconds, 0, 1);
}
