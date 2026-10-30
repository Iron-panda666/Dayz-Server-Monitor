using System.Net;
using System.Net.Sockets;
using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using MagDeer.Core;

internal static class MultiMonitoringChecks
{
    static void Assert(bool value, [System.Runtime.CompilerServices.CallerLineNumber] int line = 0) { if (!value) throw new Exception("Multi-monitor/export assertion failed at line " + line); }
    public static IEnumerable<(string Name, Func<Task> Run)> All()
    {
        yield return ("Background monitoring is opt-in, deduplicates endpoints and isolates failures/history", async () =>
        {
            var directory = Path.Combine(Path.GetTempPath(), "DayzMulti-" + Guid.NewGuid().ToString("N"));
            try
            {
                int calls = 0; var now = DateTimeOffset.UtcNow; var guardTime = new GuardClock();
                var settings = new MonitorSettings { SavedServers = [new() { Name = "Off", QueryPort = 2403 },
                    new() { Name = "Good", QueryPort = 2503, MonitorInBackground = true },
                    new() { Name = "Duplicate", QueryPort = 2503, MonitorInBackground = true },
                    new() { Name = "Timeout", QueryPort = 2603, MonitorInBackground = true }] };
                var monitor = new BackgroundMonitoring(() => now, (_, port, _, _) =>
                {
                    calls++; return Task.FromResult(new QueryResult(now, port == 2503 ? new("Good", "map", "game", 10, 20, "1") : null,
                        port == 2503 ? 12 : null, port == 2503 ? QueryFailure.None : QueryFailure.Timeout, "fixture"));
                }, _ => new QueryRefreshGuard(guardTime));
                monitor.Configure(settings, new HistoryStore(directory, 1));
                monitor.Tick(CancellationToken.None); await monitor.CurrentTask;
                monitor.Tick(CancellationToken.None); await monitor.CurrentTask;
                monitor.Tick(CancellationToken.None); await monitor.CurrentTask;
                var states = monitor.Snapshots(new("NotRunning", null, null));
                Assert(calls == 2 && states.Length == 2 && states.Single(s => s.Endpoint.EndsWith(":2503")).Players == 10);
                Assert(states.Single(s => s.Endpoint.EndsWith(":2603")).Players is null);
                Assert(File.ReadAllLines(Directory.GetFiles(directory, "*.jsonl").Single()).Length == 2);
                // Automatic cadence and manual limit are separate; duplicate keys cannot bypass it.
                Assert(!monitor.Refresh(settings.IpAddress + ":2503", CancellationToken.None).Accepted);
                guardTime.Seconds = 25;
                Assert(monitor.Refresh(settings.IpAddress + ":2503", CancellationToken.None).Accepted); await monitor.CurrentTask;
                Assert(!monitor.Refresh(settings.IpAddress + ":2503", CancellationToken.None).Accepted);
                settings.SavedServers.Clear(); monitor.Configure(settings, new HistoryStore(directory, 1));
                Assert(monitor.Snapshots(new("NotRunning", null, null)).Length == 0);
            }
            finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
        });
        yield return ("Monitoring and exporter settings roundtrip and reject oversized background sets", () =>
        {
            var s = new MonitorSettings { ExportEnabled = true, ExportAddress = "192.168.1.50", ExportPort = 9187, MonitorOnlineServices = true };
            s.SavedServers.Add(new() { MonitorInBackground = true }); s.Validate();
            var copy = s.Copy(); Assert(copy.SavedServers[0].MonitorInBackground && copy.ExportEnabled && copy.MonitorOnlineServices);
            s.SavedServers = Enumerable.Range(0, 17).Select(i => new ServerProfile { Name = i.ToString(), MonitorInBackground = true }).ToList();
            bool rejected = false; try { s.Validate(); } catch (ArgumentException) { rejected = true; } Assert(rejected);
            s.SavedServers.Clear(); s.ExportAddress = "0.0.0.0"; rejected = false;
            try { s.Validate(); } catch (ArgumentException) { rejected = true; } Assert(rejected);
            return Task.CompletedTask;
        });
        yield return ("Prometheus escapes labels, omits missing metrics and separates UDP and service scope", () =>
        {
            var now = DateTimeOffset.UtcNow;
            var metrics = MonitoringExport.Metrics([new() { Now = now, ServerName = "A\"B\\C\nD", Endpoint = "127.0.0.1:1", QuerySuccess = true,
                Players = 0, Capacity = 121, RttMs = 87, NextRestartAt = now.AddHours(1) }, new() { Endpoint = "failed", QuerySuccess = false, Players = 999 },
                new() { Endpoint = "service:discord", Service = new("Degraded", "incident", "url", "ProviderStatus", now, 0) }]);
            Assert(metrics.Contains("server=\"A\\\"B\\\\C\\nD\"") && metrics.Contains("} 0.087\n"));
            Assert(!metrics.Contains("dayz_monitor_players{endpoint=\"failed\"") && !metrics.Contains("999"));
            Assert(metrics.Contains("dayz_monitor_service_ok{endpoint=\"service:discord\",scope=\"ProviderStatus\"} 0\n"));
            Assert(!metrics.Contains("dayz_monitor_query_success{endpoint=\"service:")); return Task.CompletedTask;
        });
        yield return ("Read-only HTTP exposes cached JSON/metrics and rejects query commands", async () =>
        {
            // Reserve an ephemeral port, then bind the exporter to that known-free loopback port.
            var reservation = new TcpListener(IPAddress.Loopback, 0); reservation.Start(); int port = ((IPEndPoint)reservation.LocalEndpoint).Port; reservation.Stop();
            await using var server = new MonitoringHttpServer("127.0.0.1", port, () => [new() { Now = DateTimeOffset.UtcNow, ServerName = "Fixture", Endpoint = "fixture" }]);
            server.Start(); using var http = new HttpClient { BaseAddress = new Uri($"http://127.0.0.1:{port}"), Timeout = TimeSpan.FromSeconds(4) };
            using var reply = await http.GetAsync("/metrics"); Assert(reply.IsSuccessStatusCode && reply.Content.Headers.ContentType?.Parameters.Any(p => p.Name == "version") == true);
            using var json = JsonDocument.Parse(await http.GetStringAsync("/api/snapshots")); Assert(json.RootElement.GetProperty("servers")[0].GetProperty("endpoint").GetString() == "fixture");
            using var forbidden = await http.PostAsync("/refresh", new StringContent("{}")); Assert(forbidden.StatusCode == HttpStatusCode.MethodNotAllowed);
            using var absent = await http.GetAsync("/config.json"); Assert(absent.StatusCode == HttpStatusCode.NotFound);
        });
        yield return ("Named pipe targets each monitored server and never silently falls back after removal", async () =>
        {
            var directory = Path.Combine(Path.GetTempPath(), "DayzMultiPipe-" + Guid.NewGuid().ToString("N"));
            var a = new DeckSnapshot { Now = DateTimeOffset.UtcNow, Endpoint = "a", ServerName = "A" };
            var b = a with { Endpoint = "b", ServerName = "B", Players = 47 }; string? refreshed = null;
            await using var bridge = new StreamDeckBridge(directory, () => a, _ => Task.FromResult(new DeckRefreshResult(true, 20, "a")), directory,
                () => [a, b], (endpoint, _) => { refreshed = endpoint; return Task.FromResult(new DeckRefreshResult(true, 20, "b")); });
            try
            {
                bridge.Start(); using var pipe = new NamedPipeClientStream(".", bridge.PipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
                await pipe.ConnectAsync(2000); using var reader = new StreamReader(pipe); using var writer = new StreamWriter(pipe) { AutoFlush = true };
                await writer.WriteLineAsync("{\"command\":\"snapshot\",\"endpoint\":\"b\"}");
                using var state = JsonDocument.Parse((await reader.ReadLineAsync())!); Assert(state.RootElement.GetProperty("state").GetProperty("players").GetInt32() == 47 && state.RootElement.GetProperty("monitors").GetArrayLength() == 2);
                await writer.WriteLineAsync("{\"command\":\"refresh\",\"endpoint\":\"b\"}"); await reader.ReadLineAsync(); Assert(refreshed == "b");
                await writer.WriteLineAsync("{\"command\":\"snapshot\",\"endpoint\":\"removed\"}"); Assert((await reader.ReadLineAsync())!.Contains("server-not-monitored"));
            }
            finally { await bridge.DisposeAsync(); if (Directory.Exists(directory)) Directory.Delete(directory, true); }
        });
        yield return ("Service status distinguishes operator incidents, local web access and unknown failures", async () =>
        {
            var guards = new Dictionary<string, QueryRefreshGuard>(); var guardTime = new GuardClock();
            QueryRefreshGuard Guard(string key) { if (!guards.TryGetValue(key, out var g)) guards[key] = g = new(guardTime); return g; }
            using var service = new ServiceMonitoring(() => DateTimeOffset.UtcNow, Guard, new FakeHttp());
            Assert(service.Snapshots(new("NotRunning", null, null)).Length == 0);
            service.Configure(true); service.Tick(CancellationToken.None); await service.CurrentTask;
            var states = service.Snapshots(new("NotRunning", null, null));
            Assert(states.Single(s => s.Endpoint == "service:discord").Service?.State == "Degraded");
            Assert(states.Single(s => s.Endpoint == "service:spotify").Service?.State == "Reachable");
            Assert(states.Single(s => s.Endpoint == "service:whatsapp").Service?.State == "Unavailable");
            Assert(!states.Any(s => s.Players is not null || s.QuerySuccess));
            Assert(!service.Refresh("service:steam", CancellationToken.None).Accepted); guardTime.Seconds = 25;
            Assert(service.Refresh("service:steam", CancellationToken.None).Accepted); await service.CurrentTask;
            Assert(!service.Refresh("service:steam", CancellationToken.None).Accepted);
            service.Configure(false); Assert(service.Snapshots(new("NotRunning", null, null)).Length == 0);
        });
    }
    private sealed class FakeHttp : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) => Task.FromResult(
            request.RequestUri!.Host == "discordstatus.com" ? new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{\"status\":{\"indicator\":\"minor\",\"description\":\"Partial incident\"}}") }
            : new HttpResponseMessage(request.RequestUri.Host == "web.whatsapp.com" ? HttpStatusCode.Forbidden : HttpStatusCode.OK));
    }
    private sealed class GuardClock : TimeProvider
    {
        public long Seconds;
        public override long TimestampFrequency => 1;
        public override long GetTimestamp() => Seconds;
    }
}
