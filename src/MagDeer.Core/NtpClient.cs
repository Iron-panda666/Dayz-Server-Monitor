using System.Buffers.Binary;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace MagDeer.Core;

public enum NtpFailure { None, Timeout, Network, InvalidResponse, RateLimited, Denied }
public sealed record NtpSample(string Server, DateTimeOffset UtcAtReceive, DateTimeOffset SystemAtReceive,
    long ReceiveTimestamp, double OffsetMilliseconds, double DelayMilliseconds, byte Stratum);
public sealed record NtpResult(NtpSample? Sample, NtpFailure Failure, string Detail)
{
    public bool Success => Sample is not null && Failure == NtpFailure.None;
}

/// <summary>Read-only unicast SNTP client. Never changes the OS clock or its NTP settings.</summary>
public sealed class NtpClient(TimeProvider? timeProvider = null)
{
    private readonly TimeProvider time = timeProvider ?? TimeProvider.System;
    public async Task<NtpResult> QueryAsync(string host, TimeSpan timeout, CancellationToken ct, int port = 123)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct);
        deadline.CancelAfter(timeout);
        try
        {
            var addresses = await Dns.GetHostAddressesAsync(host, deadline.Token).ConfigureAwait(false);
            var address = addresses.OrderBy(x => x.AddressFamily == AddressFamily.InterNetwork ? 0 : 1).FirstOrDefault()
                ?? throw new SocketException((int)SocketError.HostNotFound);
            using var udp = new UdpClient(address.AddressFamily);
            udp.Connect(new IPEndPoint(address, port));
            var sentAt = time.GetUtcNow();
            long sentStamp = time.GetTimestamp();
            var request = new byte[48]; request[0] = 0x23; request[2] = 6; request[3] = 0xEC;
            NtpWire.WriteTimestamp(request.AsSpan(40), sentAt);
            await udp.SendAsync(request, deadline.Token).ConfigureAwait(false);
            var response = (await udp.ReceiveAsync(deadline.Token).ConfigureAwait(false)).Buffer;
            long receivedStamp = time.GetTimestamp();
            // Reconstruct T4 from the monotonic timer; an OS time adjustment in transit
            // must not corrupt the measured roundtrip or the four-timestamp calculation.
            var receivedAt = sentAt + time.GetElapsedTime(sentStamp, receivedStamp);
            var sample = NtpWire.Parse(response, request, sentAt, receivedAt, host, receivedStamp);
            var systemAtReceive = time.GetUtcNow();
            sample = sample with { SystemAtReceive = systemAtReceive, OffsetMilliseconds = (sample.UtcAtReceive - systemAtReceive).TotalMilliseconds };
            return new(sample, NtpFailure.None, "SNTP OK");
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested) { return new(null, NtpFailure.Timeout, "NTP timeout (including DNS)"); }
        catch (NtpPacketException ex) { return new(null, ex.Failure, ex.Message); }
        catch (SocketException ex) { return new(null, NtpFailure.Network, ex.SocketErrorCode.ToString()); }
        catch (ArgumentException ex) { return new(null, NtpFailure.InvalidResponse, ex.Message); }
    }
}

public sealed class NtpPacketException(NtpFailure failure, string message) : Exception(message)
{
    public NtpFailure Failure { get; } = failure;
}

public static class NtpWire
{
    private static readonly DateTimeOffset Epoch = new(1900, 1, 1, 0, 0, 0, TimeSpan.Zero);
    private const double EraSeconds = 4294967296d;
    public static void WriteTimestamp(Span<byte> target, DateTimeOffset value)
    {
        if (target.Length < 8) throw new ArgumentException("NTP timestamp requires eight bytes.");
        long ticks = (value.ToUniversalTime() - Epoch).Ticks;
        if (ticks < 0) throw new ArgumentException("System date before 1900 is unsupported.");
        long seconds = ticks / TimeSpan.TicksPerSecond;
        uint fraction = (uint)((ulong)(ticks % TimeSpan.TicksPerSecond) * (1UL << 32) / TimeSpan.TicksPerSecond);
        BinaryPrimitives.WriteUInt32BigEndian(target, unchecked((uint)seconds));
        BinaryPrimitives.WriteUInt32BigEndian(target[4..], fraction);
    }
    public static DateTimeOffset ReadTimestamp(ReadOnlySpan<byte> bytes, DateTimeOffset pivot)
    {
        if (bytes.Length < 8) throw new NtpPacketException(NtpFailure.InvalidResponse, "Truncated NTP timestamp");
        uint seconds = BinaryPrimitives.ReadUInt32BigEndian(bytes);
        uint fraction = BinaryPrimitives.ReadUInt32BigEndian(bytes[4..]);
        // Choose the era nearest the local date. This handles the February 2036 wrap.
        double era = Math.Round(((pivot - Epoch).TotalSeconds - seconds) / EraSeconds);
        long ticks = checked((long)((seconds + era * EraSeconds) * TimeSpan.TicksPerSecond));
        ticks += (long)((ulong)fraction * TimeSpan.TicksPerSecond >> 32);
        return Epoch.AddTicks(ticks);
    }
    public static NtpSample Parse(ReadOnlySpan<byte> packet, ReadOnlySpan<byte> request, DateTimeOffset t1,
        DateTimeOffset t4, string host, long receivedStamp)
    {
        void Invalid(string message) => throw new NtpPacketException(NtpFailure.InvalidResponse, message);
        if (packet.Length < 48 || request.Length < 48) Invalid("Truncated NTP reply");
        int version = packet[0] >> 3 & 7, mode = packet[0] & 7, leap = packet[0] >> 6;
        if (version is not (3 or 4) || mode != 4) Invalid("Not an NTP v3/v4 server response");
        if (!packet.Slice(24, 8).SequenceEqual(request.Slice(40, 8))) Invalid("NTP originate timestamp does not match this request");
        byte stratum = packet[1];
        if (stratum == 0)
        {
            string code = Encoding.ASCII.GetString(packet.Slice(12, 4));
            throw new NtpPacketException(code == "RATE" ? NtpFailure.RateLimited : code is "DENY" or "RSTR" ? NtpFailure.Denied : NtpFailure.InvalidResponse, "NTP kiss-o'-death: " + code);
        }
        if (leap == 3 || stratum > 15) Invalid("NTP server is not synchronized");
        if (BinaryPrimitives.ReadUInt64BigEndian(packet.Slice(32, 8)) == 0 || BinaryPrimitives.ReadUInt64BigEndian(packet.Slice(40, 8)) == 0)
            Invalid("NTP receive/transmit timestamp is zero");
        var t2 = ReadTimestamp(packet.Slice(32, 8), t4);
        var t3 = ReadTimestamp(packet.Slice(40, 8), t4);
        if (t3 < t2 || t4 < t1) Invalid("Invalid NTP timestamp order");
        var delay = (t4 - t1) - (t3 - t2);
        if (delay < TimeSpan.FromMilliseconds(-1)) Invalid("NTP processing time exceeds measured roundtrip");
        var offset = TimeSpan.FromTicks(((t2 - t1).Ticks + (t3 - t4).Ticks) / 2);
        return new(host, t4 + offset, t4, receivedStamp, offset.TotalMilliseconds, Math.Max(0, delay.TotalMilliseconds), stratum);
    }
}
