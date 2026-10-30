internal sealed class FakeTimeProvider(DateTimeOffset utc) : TimeProvider
{
    private DateTimeOffset now = utc;
    private long timestamp;
    public override DateTimeOffset GetUtcNow() => now;
    public override long GetTimestamp() => timestamp;
    public override long TimestampFrequency => TimeSpan.TicksPerSecond;
    public void Advance(TimeSpan elapsed) { now += elapsed; timestamp += elapsed.Ticks; }
    public void JumpWindows(TimeSpan change) => now += change;
}
