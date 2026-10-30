using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;

namespace MagDeer.Core;

public static class MonitoringExport
{
    public static string Metrics(IEnumerable<DeckSnapshot> snapshots)
    {
        var output = new StringBuilder();
        var all = snapshots.ToArray(); var rows = all.Where(s => s.Service is null).ToArray();
        void Metric(string name, string help, Func<DeckSnapshot, double?> value)
        {
            output.Append("# HELP dayz_monitor_").Append(name).Append(' ').Append(help).Append('\n');
            output.Append("# TYPE dayz_monitor_").Append(name).Append(" gauge\n");
            foreach (var s in rows)
            {
                var number = value(s); if (number is null || !double.IsFinite(number.Value)) continue;
                output.Append("dayz_monitor_").Append(name).Append("{endpoint=\"").Append(Escape(s.Endpoint))
                    .Append("\",server=\"").Append(Escape(s.ServerName)).Append("\"} ")
                    .Append(number.Value.ToString("R", CultureInfo.InvariantCulture)).Append('\n');
            }
        }
        Metric("query_success", "Last UDP query succeeded; zero does not prove the game is offline.", s => s.QuerySuccess ? 1 : 0);
        Metric("players", "Players in the last successful query; absent on failure.", s => s.QuerySuccess ? s.Players : null);
        Metric("capacity", "Published player capacity; absent on failure.", s => s.QuerySuccess ? s.Capacity : null);
        Metric("queue", "Published queue length; absent if unknown.", s => s.QuerySuccess ? s.Queue : null);
        Metric("query_rtt_seconds", "UDP query RTT including challenge; not ICMP ping.", s => s.QuerySuccess ? s.RttMs / 1000 : null);
        Metric("last_success_timestamp_seconds", "Unix timestamp of last successful UDP query.", s => s.LastSuccessAt?.ToUnixTimeSeconds());
        Metric("next_restart_timestamp_seconds", "Next configured restart; schedule may be unconfirmed.", s => s.NextRestartAt.ToUnixTimeSeconds());
        Metric("schedule_confirmed", "User confirmed the restart schedule.", s => s.ScheduleConfirmed ? 1 : 0);
        output.Append("# HELP dayz_monitor_service_ok Provider report or web reachability only; not proof of global availability.\n# TYPE dayz_monitor_service_ok gauge\n");
        foreach (var s in all.Where(s => s.Service is not null))
        {
            var service = s.Service!;
            if (service.State is not ("Operational" or "Degraded" or "Reachable")) continue;
            output.Append("dayz_monitor_service_ok{endpoint=\"").Append(Escape(s.Endpoint)).Append("\",scope=\"").Append(Escape(service.Scope))
                .Append("\"} ").Append(service.State == "Degraded" ? 0 : 1).Append('\n');
        }
        return output.ToString();
    }
    private static string Escape(string value) => value.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n").Replace("\r", "");
}

/// <summary>Optional read-only HTTP snapshots. No URL ACL/admin setup, query commands or push credentials.</summary>
public sealed class MonitoringHttpServer : IAsyncDisposable
{
    private readonly TcpListener listener;
    private readonly Func<DeckSnapshot[]> snapshots;
    private readonly CancellationTokenSource stop = new();
    private Task worker = Task.CompletedTask;
    public MonitoringHttpServer(string address, int port, Func<DeckSnapshot[]> snapshots)
    { listener = new(IPAddress.Parse(address), port); this.snapshots = snapshots; }
    public void Start() { listener.Start(8); worker = RunAsync(); }
    private async Task RunAsync()
    {
        while (!stop.IsCancellationRequested)
        {
            try
            {
                using var client = await listener.AcceptTcpClientAsync(stop.Token).ConfigureAwait(false);
                using var deadline = CancellationTokenSource.CreateLinkedTokenSource(stop.Token);
                deadline.CancelAfter(TimeSpan.FromSeconds(2));
                await ServeAsync(client, deadline.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stop.IsCancellationRequested) { break; }
            catch (Exception ex) when (ex is IOException or SocketException or OperationCanceledException) { }
        }
    }
    private async Task ServeAsync(TcpClient client, CancellationToken ct)
    {
        using var stream = client.GetStream();
        // Bound request size and lifetime, then always close. No request bodies or persistent connections.
        var header = new List<byte>(); var one = new byte[1]; bool complete = false;
        while (header.Count < 8192 && await stream.ReadAsync(one, ct).ConfigureAwait(false) != 0)
        {
            header.Add(one[0]); int n = header.Count;
            if (n >= 4 && header[n - 4] == 13 && header[n - 3] == 10 && header[n - 2] == 13 && header[n - 1] == 10) { complete = true; break; }
        }
        if (!complete) return;
        var headers = Encoding.ASCII.GetString(header.ToArray()).Split("\r\n");
        var line = headers[0].Split(' ');
        // Consume small declared bodies before closing, otherwise Windows may reset the TCP stream
        // while the client is still sending a rejected POST and discard the error response.
        if (headers.Any(h => h.StartsWith("Transfer-Encoding:", StringComparison.OrdinalIgnoreCase))) return;
        var length = headers.FirstOrDefault(h => h.StartsWith("Content-Length:", StringComparison.OrdinalIgnoreCase));
        if (length is not null)
        {
            if (!int.TryParse(length[(length.IndexOf(':') + 1)..].Trim(), out var count) || count is < 0 or > 8192) return;
            if (count > 0) await stream.ReadExactlyAsync(new byte[count], ct).ConfigureAwait(false);
        }
        string status = "404 Not Found", type = "text/plain; charset=utf-8", body = "Not found\n";
        if (line.Length == 3 && line[0] == "GET" && line[2] == "HTTP/1.1")
        {
            if (line[1] == "/metrics") { status = "200 OK"; type = "text/plain; version=0.0.4; charset=utf-8"; body = MonitoringExport.Metrics(snapshots()); }
            else if (line[1] == "/api/snapshots")
            { status = "200 OK"; type = "application/json; charset=utf-8"; body = JsonSerializer.Serialize(new { protocolVersion = 1, servers = snapshots() }, StreamDeckBridge.WireJson); }
        }
        else { status = "405 Method Not Allowed"; body = "Read-only GET endpoint\n"; }
        var payload = Encoding.UTF8.GetBytes(body);
        var response = Encoding.ASCII.GetBytes($"HTTP/1.1 {status}\r\nContent-Type: {type}\r\nContent-Length: {payload.Length}\r\nCache-Control: no-store\r\nConnection: close\r\nX-Content-Type-Options: nosniff\r\n\r\n");
        await stream.WriteAsync(response, ct).ConfigureAwait(false);
        await stream.WriteAsync(payload, ct).ConfigureAwait(false);
    }
    public async ValueTask DisposeAsync()
    { stop.Cancel(); listener.Stop(); await worker.ConfigureAwait(false); stop.Dispose(); }
}
