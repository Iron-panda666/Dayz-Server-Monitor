namespace MagDeer.Core;

public enum ClockSource { Windows, Ntp, NtpHoldover, WindowsFallback }

public sealed class MonitorClock(TimeProvider? timeProvider = null)
{
    private readonly TimeProvider time = timeProvider ?? TimeProvider.System;
    private NtpSample? sample;
    private TimeSpan maxAge = TimeSpan.FromHours(3);
    public bool Enabled { get; private set; }
    public string Server { get; private set; } = "time.cloudflare.com";
    public NtpFailure LastFailure { get; private set; }
    public string LastDetail { get; private set; } = "";
    public NtpSample? LastSample => sample;
    public TimeSpan SampleAge => sample is null ? TimeSpan.MaxValue : time.GetElapsedTime(sample.ReceiveTimestamp, time.GetTimestamp());
    public ClockSource Source => !Enabled ? ClockSource.Windows : sample is not null && SampleAge <= maxAge
        ? LastFailure == NtpFailure.None ? ClockSource.Ntp : ClockSource.NtpHoldover : ClockSource.WindowsFallback;
    public DateTimeOffset UtcNow => Source is ClockSource.Ntp or ClockSource.NtpHoldover
        ? sample!.UtcAtReceive + time.GetElapsedTime(sample.ReceiveTimestamp, time.GetTimestamp()) : time.GetUtcNow();
    public double OffsetFromWindowsMilliseconds => (UtcNow - time.GetUtcNow()).TotalMilliseconds;

    public void Configure(bool enabled, string server, TimeSpan maximumAge)
    {
        if (Enabled != enabled || !Server.Equals(server, StringComparison.OrdinalIgnoreCase)) Invalidate();
        Enabled = enabled; Server = server; maxAge = maximumAge;
    }
    public void Accept(NtpSample value)
    {
        if (!Enabled || !Server.Equals(value.Server, StringComparison.OrdinalIgnoreCase)) return;
        sample = value; LastFailure = NtpFailure.None; LastDetail = "";
    }
    public void Failed(NtpResult result) { LastFailure = result.Failure; LastDetail = result.Detail; }
    public void Invalidate() { sample = null; LastFailure = NtpFailure.None; LastDetail = ""; }
}
