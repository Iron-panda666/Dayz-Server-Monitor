using System.Buffers.Binary;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using MagDeer.Core;

if (args.Length == 2 && args[0] == "--deck-fixture")
{
    // Console-only IPC fixture: no desktop Window, game server query or user-profile writes.
    string directory = Path.GetFullPath(args[1]);
    Directory.CreateDirectory(directory);
    var started = DateTimeOffset.UtcNow; int refreshes = 0;
    var guard = new QueryRefreshGuard(); var secondGuard = new QueryRefreshGuard(); int secondRefreshes = 0;
    DeckSnapshot Snapshot() => new()
    {
        Now = DateTimeOffset.UtcNow, ServerName = "MAG fixture #2", Endpoint = "127.0.0.1:2303", Status = "Online",
        NextRestartAt = started.AddHours(2.5), LogoutWarningMinutes = 10, KickWarningMinutes = 5,
        LastQueryAt = started, LastSuccessAt = started, QuerySuccess = true, Players = 39 + refreshes,
        Capacity = 121, Queue = 0, RttMs = 87, GameClock = new(12, 0, 4, 4), QueryIntervalSeconds = 300,
        GameSession = new("Running", started.AddMinutes(-30), 1800),
        RefreshRemainingSeconds = guard.RemainingSeconds,
        History = [new(started.AddMinutes(-10), 35, 121), new(started.AddMinutes(-5), null, null), new(started, 39, 121)]
    };
    await using var bridge = new StreamDeckBridge(Path.Combine(directory, "fixture-data"), Snapshot, _ =>
    {
        bool accepted = guard.TryBeginManual(); if (accepted) Interlocked.Increment(ref refreshes);
        return Task.FromResult(new DeckRefreshResult(accepted, guard.RemainingSeconds, accepted ? "accepted" : "cooldown"));
    }, directory, () => [Snapshot(), Snapshot() with { Endpoint = "127.0.0.1:2403", ServerName = "Second fixture", Players = 77 + secondRefreshes, RefreshRemainingSeconds = secondGuard.RemainingSeconds }],
    (endpoint, _) =>
    {
        bool accepted = endpoint == "127.0.0.1:2403" && secondGuard.TryBeginManual(); if (accepted) Interlocked.Increment(ref secondRefreshes);
        return Task.FromResult(new DeckRefreshResult(accepted, secondGuard.RemainingSeconds, accepted ? "accepted" : "cooldown"));
    });
    bridge.Start(); Console.WriteLine("DECK_FIXTURE_READY");
    await Console.In.ReadLineAsync();
    return 0;
}

if (args.Length == 1 && args[0] == "--services")
{
    var guards = new Dictionary<string, QueryRefreshGuard>();
    QueryRefreshGuard Guard(string key) { if (!guards.TryGetValue(key, out var guard)) guards[key] = guard = new(); return guard; }
    using var monitor = new ServiceMonitoring(() => DateTimeOffset.UtcNow, Guard);
    monitor.Configure(true); monitor.Tick(CancellationToken.None); await monitor.CurrentTask;
    Console.WriteLine(JsonSerializer.Serialize(monitor.Snapshots(new("Unavailable", null, null)).Select(s => new { s.ServerName, s.Service }), JsonStorage.Options));
    return 0;
}
if (args.Length == 3 && args[0] == "--probe-list")
{
    var profiles = JsonSerializer.Deserialize<List<ServerProfile>>(File.ReadAllText(args[1]), JsonStorage.Options)!;
    using var concurrency = new SemaphoreSlim(4);
    var results = await Task.WhenAll(profiles.Select(async profile =>
    {
        await concurrency.WaitAsync();
        try
        {
            var result = await new A2sClient().QueryAsync(profile.IpAddress, profile.QueryPort, TimeSpan.FromSeconds(3), CancellationToken.None);
            return new { profile.Name, profile.IpAddress, profile.GamePort, profile.QueryPort, Result = result };
        }
        finally { concurrency.Release(); }
    }));
    File.WriteAllText(args[2], JsonSerializer.Serialize(results, JsonStorage.Options));
    foreach (var row in results) Console.WriteLine($"{row.IpAddress}:{row.QueryPort} {row.Result.Failure} {row.Result.Info?.Name}");
    return 0;
}

if (args.Length > 0 && args[0] == "--ntp")
{
    var result = await new NtpClient().QueryAsync(args.Length > 1 ? args[1] : "time.cloudflare.com", TimeSpan.FromSeconds(5), CancellationToken.None);
    Console.WriteLine(JsonSerializer.Serialize(result, JsonStorage.Options));
    return result.Success ? 0 : 2;
}

if (args.Length > 0 && args[0] == "--query")
{
    var result = await new A2sClient().QueryAsync(args.Length > 1 ? args[1] : "217.182.198.57", args.Length > 2 ? int.Parse(args[2]) : 2303,
        TimeSpan.FromSeconds(5), CancellationToken.None);
    Console.WriteLine(JsonSerializer.Serialize(result, JsonStorage.Options));
    return result.Success ? 0 : 2;
}

int passed = 0, failed = 0;
async Task Test(string name, Func<Task> test)
{
    try { await test(); passed++; Console.WriteLine("PASS " + name); }
    catch (Exception ex) { failed++; Console.WriteLine("FAIL " + name + ": " + ex.Message); }
}
void Assert(bool condition, string message = "Assertion failed") { if (!condition) throw new Exception(message); }
Task Sync(Action action) { action(); return Task.CompletedTask; }
DateTimeOffset Utc(string value) => DateTimeOffset.Parse(value, System.Globalization.CultureInfo.InvariantCulture);
var schedule = new RestartSchedule(new MonitorSettings().RestartTimes);

await Test("Countdown colors follow 15/10/5 minutes and pulse only digits gently during the last 30 seconds", () => Sync(() =>
{
    var even = DateTimeOffset.FromUnixTimeSeconds(1000); var odd = even.AddSeconds(1);
    Assert(CountdownVisuals.Evaluate(901, even).Stage == CountdownStage.Normal);
    Assert(CountdownVisuals.Evaluate(900, even).Stage == CountdownStage.Yellow);
    Assert(CountdownVisuals.Evaluate(600, even).Stage == CountdownStage.Orange);
    var start = CountdownVisuals.Evaluate(300, even); var middle = CountdownVisuals.Evaluate(165, even);
    Assert(start.RedMix == 0 && middle.RedMix == .5);
    Assert(CountdownVisuals.Color(middle, true) == "#FF7B48");
    Assert(CountdownVisuals.Evaluate(31, odd).Opacity == 1);
    Assert(CountdownVisuals.Evaluate(30, even).Stage == CountdownStage.Red);
    Assert(CountdownVisuals.Evaluate(30, odd).Opacity == .78 && CountdownVisuals.Evaluate(30, even).Opacity == 1);
    Assert(CountdownVisuals.Evaluate(0, odd).Opacity == 1 && CountdownVisuals.Evaluate(-1, odd).Opacity == 1);
    Assert(CountdownVisuals.Evaluate(double.NaN, even).Stage == CountdownStage.Normal);
}));
await Test("Known MAG DEER kick is per endpoint and never changes the actual restart schedule", () => Sync(() =>
{
    var mag = new MonitorSettings { CountdownToKick = true };
    var restart = Utc("2026-10-31T02:00:00Z");
    Assert(mag.HasKnownKick && RestartSchedule.CzechTime(KickPlan.Target(mag, restart)).Hour == 2);
    Assert(RestartSchedule.CzechTime(KickPlan.Target(mag, restart)).Minute == 55);
    Assert(new RestartSchedule(mag.RestartTimes).Next(restart.AddMinutes(-10)) == restart);
    var other = new MonitorSettings { IpAddress = "203.0.113.10", ServerName = "MAG label copied by someone", CountdownToKick = true };
    Assert(!other.HasKnownKick && KickPlan.Target(other, restart) == restart);
    other.KickBeforeRestart = true; other.KickWarningMinutes = 7;
    Assert(KickPlan.Target(other, restart) == restart.AddMinutes(-7));
    var copy = other.Copy(); ServerProfile.From(mag).ApplyTo(copy);
    Assert(copy.HasKnownKick && copy.KickWarningMinutes == 5);
    ServerProfile.From(other).ApplyTo(copy); Assert(copy.KickWarningMinutes == 7 && copy.CountdownToKick);
    mag.KickBeforeRestart = false; Assert(!mag.HasKnownKick && KickPlan.Target(mag, restart) == restart);
}));
await Test("Appearance options persist without leaking private wallpaper paths to exported snapshots", () => Sync(() =>
{
    var s = new MonitorSettings { WidgetMode = true, BackgroundStyle = "Custom", BackgroundImagePath = "C:/Private/wallpaper.png", AeroGlass = false };
    s.Validate(); var copy = s.Copy(); Assert(copy.WidgetMode && copy.BackgroundImagePath == s.BackgroundImagePath && !copy.AeroGlass);
    string json = JsonSerializer.Serialize(new DeckSnapshot { Now = DateTimeOffset.UtcNow }, StreamDeckBridge.WireJson);
    Assert(!json.Contains("Private") && !json.Contains("backgroundImagePath"));
    s.BackgroundStyle = "remote-url"; bool failed = false;
    try { s.Validate(); } catch (ArgumentException) { failed = true; } Assert(failed);
}));

await Test("DayZ keyword clock, queue and multipliers use invariant bounded values", () => Sync(() =>
{
    var (clock, queue) = DayZKeywords.Parse("battleye,lqs3,etm4.000000,entm4.000000,mod,19:42");
    Assert(clock == new DayZGameClock(19, 42, 4, 4) && queue == 3);
    Assert(clock!.DayMultiplier * clock.NightMultiplier == 16);
    var missing = DayZKeywords.Parse("etmNaN,entmInfinity,24:80,lqs-1"); Assert(missing.Clock is null && missing.Queue is null);
    var invalid = DayZKeywords.Parse("00:00,etm0,entm65"); Assert(invalid.Clock == new DayZGameClock(0, 0, null, null));
    Assert(DayZKeywords.Parse("07:15,etm2.5,entm9").Clock == new DayZGameClock(7, 15, 2.5, 9));
}));
await Test("Stream Deck named pipe shares cooldown, rejects other commands and closes promptly", async () =>
{
    var directory = Path.Combine(Path.GetTempPath(), "DayzBridgeTests-" + Guid.NewGuid().ToString("N"));
    var time = new FakeTimeProvider(DateTimeOffset.UtcNow); var guard = new QueryRefreshGuard(time); int refreshes = 0;
    var bridge = new StreamDeckBridge(directory, () => new DeckSnapshot { Now = time.GetUtcNow(), ServerName = "Fixture", RefreshRemainingSeconds = guard.RemainingSeconds }, _ =>
    {
        bool accepted = guard.TryBeginManual(); if (accepted) refreshes++;
        return Task.FromResult(new DeckRefreshResult(accepted, guard.RemainingSeconds, accepted ? "accepted" : "cooldown"));
    }, directory);
    try
    {
        bridge.Start();
        using var client = new System.IO.Pipes.NamedPipeClientStream(".", bridge.PipeName, System.IO.Pipes.PipeDirection.InOut, System.IO.Pipes.PipeOptions.Asynchronous);
        await client.ConnectAsync(2000);
        using var reader = new StreamReader(client, Encoding.UTF8, false, 1024, true);
        using var writer = new StreamWriter(client, new UTF8Encoding(false), 1024, true) { AutoFlush = true };
        async Task<JsonDocument> Send(string command)
        {
            await writer.WriteLineAsync(JsonSerializer.Serialize(new { command }));
            return JsonDocument.Parse((await reader.ReadLineAsync().WaitAsync(TimeSpan.FromSeconds(2)))!);
        }
        using var first = await Send("snapshot"); Assert(first.RootElement.GetProperty("state").GetProperty("protocolVersion").GetInt32() == 1);
        using var accepted = await Send("refresh"); Assert(accepted.RootElement.GetProperty("refresh").GetProperty("accepted").GetBoolean());
        using var rejected = await Send("refresh"); Assert(!rejected.RootElement.GetProperty("refresh").GetProperty("accepted").GetBoolean() && refreshes == 1);
        using var unsupported = await Send("launch"); Assert(!unsupported.RootElement.GetProperty("ok").GetBoolean() && refreshes == 1);
        time.Advance(TimeSpan.FromSeconds(20)); using var again = await Send("refresh"); Assert(refreshes == 2);
        await bridge.DisposeAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(1));
        Assert(!Directory.EnumerateFiles(directory, "*.json").Any(), "Discovery file left after clean shutdown");
    }
    finally { await bridge.DisposeAsync(); if (Directory.Exists(directory)) Directory.Delete(directory, true); }
});

await Test("Catalog contains 16 distinct valid endpoints and never confirms provider schedules", () => Sync(() =>
{
    var catalog = ServerCatalog.Create();
    Assert(catalog.Count == 16 && catalog.Count(p => p.Name.StartsWith("Sunnyvale")) == 12);
    Assert(catalog.Select(p => (p.IpAddress, p.QueryPort)).Distinct().Count() == 16);
    foreach (var profile in catalog) { profile.Validate(); Assert(!profile.ScheduleConfirmed); }
    Assert(catalog.Single(p => p.Name.StartsWith("Sunnyvale #12 ")).IpAddress == "172.111.51.80");
}));
await Test("Catalog migration preserves active server, custom settings and deliberate removals", () => Sync(() =>
{
    var settings = new MonitorSettings { ScheduleConfirmed = true, RestartTimes = ["12:15"] };
    settings.SavedServers.Add(ServerProfile.From(settings));
    Assert(ServerCatalog.MergeOnce(settings) && settings.SavedServers.Count == 16);
    var existing = settings.SavedServers.Single(p => p.IpAddress == settings.IpAddress && p.QueryPort == settings.QueryPort);
    Assert(existing.ScheduleConfirmed && existing.RestartTimes.SequenceEqual(new[] { "12:15" }));
    Assert(settings.ScheduleConfirmed && settings.RestartTimes[0] == "12:15");
    settings.SavedServers.RemoveAt(0); Assert(!ServerCatalog.MergeOnce(settings) && settings.SavedServers.Count == 15);
    var fresh = new MonitorSettings(); ServerCatalog.MergeOnce(fresh); Assert(fresh.SavedServers.Count == 16);
    fresh.SavedServers[0].Name = "Edited"; Assert(ServerCatalog.Create()[0].Name != "Edited");
    var full = new MonitorSettings { SavedServers = Enumerable.Range(0, 50).Select(i => new ServerProfile { Name = "Custom " + i }).ToList() };
    ServerCatalog.MergeOnce(full); Assert(full.SavedServers.Count == 50 && full.SavedServers[49].Name == "Custom 49");
}));
foreach (var check in SessionAndProfileChecks.All()) await Test(check.Name, () => Sync(check.Run));
foreach (var check in MultiMonitoringChecks.All()) await Test(check.Name, check.Run);

await Test("Shutdown drains finished, faulted and canceled tasks without throwing", async () =>
{
    Assert(await ShutdownDrain.WaitAsync([Task.CompletedTask], TimeSpan.FromSeconds(1)));
    Assert(await ShutdownDrain.WaitAsync([Task.FromException(new IOException("fixture")), Task.FromCanceled(new CancellationToken(true))], TimeSpan.FromSeconds(1)));
});
await Test("Shutdown has a bounded wait even if a worker never completes", async () =>
{
    var blocked = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    var watch = System.Diagnostics.Stopwatch.StartNew();
    Assert(!await ShutdownDrain.WaitAsync([blocked.Task], TimeSpan.FromMilliseconds(80)));
    Assert(watch.Elapsed < TimeSpan.FromSeconds(1), "Shutdown waited for a stuck task");
    blocked.SetException(new IOException("late failure is observed"));
});

await Test("Manual query guard rejects duplicates for exactly 20 monotonic seconds", () => Sync(() =>
{
    var time = new FakeTimeProvider(Utc("2026-10-29T12:00:00Z")); var guard = new QueryRefreshGuard(time);
    Assert(guard.RemainingSeconds == 0 && guard.TryBeginManual());
    for (int i = 0; i < 100; i++) Assert(!guard.TryBeginManual());
    time.Advance(TimeSpan.FromSeconds(19.1)); Assert(guard.RemainingSeconds == 1 && !guard.TryBeginManual());
    time.JumpWindows(TimeSpan.FromDays(2)); Assert(guard.RemainingSeconds == 1);
    time.Advance(TimeSpan.FromSeconds(.9)); Assert(guard.RemainingSeconds == 0 && guard.TryBeginManual());
    time.Advance(TimeSpan.FromSeconds(15)); guard.RecordAutomaticStart();
    Assert(guard.RemainingSeconds == 20 && !guard.TryBeginManual(), "Automatic query did not throttle manual refresh");
}));
await Test("Concurrent manual requests have only one winner", () => Sync(() =>
{
    var guard = new QueryRefreshGuard(); int accepted = 0;
    Parallel.For(0, 100, _ => { if (guard.TryBeginManual()) Interlocked.Increment(ref accepted); });
    Assert(accepted == 1);
}));
await Test("Chart uses query start, true irregular intervals and date boundaries", () => Sync(() =>
{
    var t = Utc("2026-10-29T22:59:30Z");
    QueryResult Sample(DateTimeOffset at) => new(at.AddSeconds(2), null, null, QueryFailure.Timeout, "timeout") { RequestedAt = at };
    var range = QueryTimeline.From([Sample(t), Sample(t.AddSeconds(30)), Sample(t.AddMinutes(5))]);
    Assert(range.Start == t && range.End == t.AddMinutes(5));
    Assert(Math.Abs(range.Fraction(t.AddSeconds(30)) - .1) < .000001);
    Assert(RestartSchedule.CzechTime(range.Start).Day != RestartSchedule.CzechTime(range.End).Day);
    Assert(QueryTimeline.From([Sample(t)]).Fraction(t) == 0);
    var legacy = new QueryResult(t, null, null, QueryFailure.Timeout, ""); Assert(QueryTimeline.TimeOf(legacy) == t);
}));

byte[] NtpRequest(DateTimeOffset t)
{
    var packet = new byte[48]; packet[0] = 0x23; NtpWire.WriteTimestamp(packet.AsSpan(40), t); return packet;
}
byte[] NtpReply(byte[] request, DateTimeOffset receive, DateTimeOffset send)
{
    var packet = new byte[48]; packet[0] = 0x24; packet[1] = 2;
    request.AsSpan(40, 8).CopyTo(packet.AsSpan(24)); NtpWire.WriteTimestamp(packet.AsSpan(32), receive); NtpWire.WriteTimestamp(packet.AsSpan(40), send); return packet;
}
await Test("NTP four-timestamp offset and network delay", () => Sync(() =>
{
    var t = Utc("2026-10-28T12:00:00Z"); var request = NtpRequest(t);
    var reply = NtpReply(request, t.AddSeconds(5).AddMilliseconds(40), t.AddSeconds(5).AddMilliseconds(60));
    var sample = NtpWire.Parse(reply, request, t, t.AddMilliseconds(100), "fixture", 123);
    Assert(Math.Abs(sample.OffsetMilliseconds - 5000) < .01);
    Assert(Math.Abs(sample.DelayMilliseconds - 80) < .01);
    Assert(Math.Abs((sample.UtcAtReceive - t.AddSeconds(5).AddMilliseconds(100)).TotalMilliseconds) < .01);
}));
await Test("NTP rejects invalid origin, mode, version, unsynchronized and zero timestamps", () => Sync(() =>
{
    var t = Utc("2026-10-28T12:00:00Z"); var request = NtpRequest(t);
    foreach (var corrupt in new Action<byte[]>[]
    {
        x => x[24] ^= 1, x => x[0] = 0x23, x => x[0] = 0x0C, x => x[0] = 0xE4,
        x => x[1] = 16, x => Array.Clear(x, 32, 8), x => Array.Clear(x, 40, 8)
    })
    {
        var reply = NtpReply(request, t, t); corrupt(reply); bool rejected = false;
        try { NtpWire.Parse(reply, request, t, t.AddMilliseconds(100), "fixture", 0); }
        catch (NtpPacketException ex) { rejected = ex.Failure == NtpFailure.InvalidResponse; } Assert(rejected);
    }
    for (int length = 0; length < 48; length++)
    {
        bool rejected = false; try { NtpWire.Parse(new byte[length], request, t, t, "fixture", 0); } catch (NtpPacketException) { rejected = true; }
        Assert(rejected, "Accepted truncated NTP response");
    }
}));
await Test("NTP rate-limit and denial packets are classified separately", () => Sync(() =>
{
    var t = Utc("2026-10-28T12:00:00Z"); var request = NtpRequest(t);
    foreach (var (code, expected) in new[] { ("RATE", NtpFailure.RateLimited), ("DENY", NtpFailure.Denied), ("RSTR", NtpFailure.Denied) })
    {
        var reply = NtpReply(request, t, t); reply[1] = 0; Encoding.ASCII.GetBytes(code).CopyTo(reply, 12);
        bool rejected = false; try { NtpWire.Parse(reply, request, t, t, "fixture", 0); } catch (NtpPacketException ex) { rejected = ex.Failure == expected; } Assert(rejected);
    }
}));
await Test("NTP timestamp codec unfolds the 2036 era rollover", () => Sync(() =>
{
    foreach (var instant in new[] { "2026-10-28T12:00:00.1234567Z", "2036-02-07T06:28:15.5Z", "2036-02-07T06:28:17Z", "2040-07-01T15:20:30Z" })
    {
        var t = Utc(instant); var bytes = new byte[8]; NtpWire.WriteTimestamp(bytes, t);
        Assert(Math.Abs((NtpWire.ReadTimestamp(bytes, t) - t).Ticks) <= 5, "Wrong NTP era or fraction");
    }
}));
await Test("NTP clock is monotonic across Windows jumps and expires to marked fallback", () => Sync(() =>
{
    var time = new FakeTimeProvider(Utc("2026-10-28T12:00:00Z")); var clock = new MonitorClock(time);
    Assert(clock.Source == ClockSource.Windows);
    clock.Configure(true, "fixture", TimeSpan.FromMinutes(30)); Assert(clock.Source == ClockSource.WindowsFallback);
    var ntp = time.GetUtcNow().AddSeconds(5);
    clock.Accept(new("fixture", ntp, time.GetUtcNow(), time.GetTimestamp(), 5000, 25, 2));
    time.Advance(TimeSpan.FromSeconds(30)); Assert(clock.UtcNow == ntp.AddSeconds(30) && clock.Source == ClockSource.Ntp);
    time.JumpWindows(TimeSpan.FromHours(2)); Assert(clock.UtcNow == ntp.AddSeconds(30));
    clock.Failed(new(null, NtpFailure.Timeout, "timeout")); Assert(clock.Source == ClockSource.NtpHoldover);
    time.Advance(TimeSpan.FromMinutes(31)); Assert(clock.Source == ClockSource.WindowsFallback && clock.UtcNow == time.GetUtcNow());
    clock.Configure(false, "fixture", TimeSpan.FromMinutes(30)); Assert(clock.Source == ClockSource.Windows && clock.LastSample is null);
    clock.Configure(true, "other", TimeSpan.FromMinutes(30)); clock.Accept(new("fixture", ntp, ntp, 0, 0, 0, 2)); Assert(clock.LastSample is null);
}));
await Test("NTP time drives restart warnings independently of Windows clock", () => Sync(() =>
{
    var time = new FakeTimeProvider(Utc("2026-10-28T13:46:00Z")); var clock = new MonitorClock(time);
    clock.Configure(true, "fixture", TimeSpan.FromHours(3));
    clock.Accept(new("fixture", Utc("2026-10-28T13:50:00Z"), time.GetUtcNow(), 0, 240000, 20, 2));
    var alerts = new AlertLedger(); Assert(alerts.Evaluate(clock.UtcNow, schedule, "fixture")?.Minutes == 10);
    time.Advance(TimeSpan.FromMinutes(5)); time.JumpWindows(TimeSpan.FromHours(-1));
    Assert(alerts.Evaluate(clock.UtcNow, schedule, "fixture")?.Minutes == 5);
    Assert(alerts.Evaluate(clock.UtcNow, schedule, "fixture") is null);
    Assert(schedule.Next(clock.UtcNow) - clock.UtcNow == TimeSpan.FromMinutes(5));
}));
await Test("NTP optional configuration validates and survives JSON roundtrip", () => Sync(() =>
{
    var settings = new MonitorSettings(); Assert(!settings.NtpEnabled); settings.Validate();
    settings.NtpEnabled = true; settings.NtpServer = "pool.ntp.org"; settings.NtpSyncIntervalMinutes = 30;
    var copy = settings.Copy(); copy.Validate(); Assert(copy.NtpEnabled && copy.NtpServer == "pool.ntp.org" && copy.NtpSyncIntervalMinutes == 30);
    settings.NtpServer = "https://invalid.example/"; bool rejected = false;
    try { settings.Validate(); } catch (ArgumentException) { rejected = true; } Assert(rejected);
}));
async Task<NtpResult> NtpFixture(Func<UdpClient, UdpReceiveResult, Task> handler, double timeout = 2)
{
    using var server = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
    var port = ((IPEndPoint)server.Client.LocalEndPoint!).Port;
    var task = Task.Run(async () => { var request = await server.ReceiveAsync(); await handler(server, request); });
    var result = await new NtpClient().QueryAsync("127.0.0.1", TimeSpan.FromSeconds(timeout), CancellationToken.None, port);
    await task; return result;
}
await Test("NTP UDP client reads a real fixture exchange and propagates cancellation", async () =>
{
    var result = await NtpFixture(async (server, request) =>
    {
        Assert(request.Buffer.Length == 48 && request.Buffer[0] == 0x23);
        var now = DateTimeOffset.UtcNow.AddSeconds(3); await server.SendAsync(NtpReply(request.Buffer, now, now), request.RemoteEndPoint);
    });
    Assert(result.Success && result.Sample?.OffsetMilliseconds is > 2500 and < 3500);
    Assert((await NtpFixture((_, _) => Task.CompletedTask, .15)).Failure == NtpFailure.Timeout);
    Assert((await NtpFixture(async (server, request) => { await server.SendAsync(new byte[] { 1 }, request.RemoteEndPoint); })).Failure == NtpFailure.InvalidResponse);
    using var cancel = new CancellationTokenSource(); cancel.Cancel(); bool canceled = false;
    try { await new NtpClient().QueryAsync("127.0.0.1", TimeSpan.FromSeconds(2), cancel.Token); } catch (OperationCanceledException) { canceled = true; } Assert(canceled);
});

await Test("Default settings valid and JSON roundtrip", () => Sync(() =>
{
    var s = new MonitorSettings(); s.Validate(); s.Copy().Validate();
    foreach (var invalid in new Action<MonitorSettings>[] { x => x.QueryPort = 0, x => x.RestartTimes = ["25:00"], x => x.RestartTimes = ["03:00", "03:00"], x => x.FailureThreshold = 1, x => x.ServerOffsetHours = double.NaN })
    {
        var copy = s.Copy(); invalid(copy); bool rejected = false;
        try { copy.Validate(); } catch (ArgumentException) { rejected = true; }
        Assert(rejected);
    }
}));
await Test("English default and old JSON migration retain server settings", () => Sync(() =>
{
    var s = JsonSerializer.Deserialize<MonitorSettings>("{\"IpAddress\":\"192.0.2.22\",\"Theme\":\"Dark\",\"ScheduleConfirmed\":true}")!;
    s.Validate(); Assert(s.Language == "en" && s.WorldClocks.Count == 0 && s.IpAddress == "192.0.2.22" && s.ScheduleConfirmed);
    foreach (var language in new[] { "en", "cs", "pl", "de", "es" }) { s.Language = language; s.Validate(); Assert(s.Copy().Language == language); }
    s.Language = "ru"; bool rejected = false; try { s.Validate(); } catch (ArgumentException) { rejected = true; } Assert(rejected);
}));
await Test("World clocks respect independent European, US, Indian and Australian offsets", () => Sync(() =>
{
    var london = new WorldClock { TimeZoneId = "Europe/London" };
    var ny = new WorldClock { TimeZoneId = "America/New_York" };
    var delhi = new WorldClock { TimeZoneId = "Asia/Kolkata" };
    var sydney = new WorldClock { TimeZoneId = "Australia/Sydney" };
    Assert(london.At(Utc("2026-07-01T12:00:00Z")).Hour == 13);
    Assert(london.At(Utc("2026-10-28T12:00:00Z")).Hour == 12);
    Assert(ny.At(Utc("2026-10-28T12:00:00Z")).Hour == 8); // Europe has changed; USA has not.
    Assert(ny.At(Utc("2026-11-04T12:00:00Z")).Hour == 7);
    Assert(delhi.At(Utc("2026-10-28T12:00:00Z")).TimeOfDay == new TimeSpan(17, 30, 0));
    Assert(sydney.At(Utc("2026-10-28T12:00:00Z")).Hour == 23);
    Assert(sydney.At(Utc("2026-07-01T12:00:00Z")).Hour == 22);
    Assert(ny.At(Utc("2026-10-28T01:00:00Z")).Day == 27);
}));
await Test("World clock configuration validates and roundtrips custom labels and hidden clocks", () => Sync(() =>
{
    var s = new MonitorSettings { WorldClocks = [new() { City = "My city", CountryCode = "GB", TimeZoneId = "Europe/London", Enabled = false }, new() { City = "UTC", CountryCode = "", TimeZoneId = "UTC" }] };
    s.Validate(); var copy = s.Copy(); copy.Validate();
    Assert(copy.WorldClocks[0].Label == "My city · GB" && !copy.WorldClocks[0].Enabled && copy.WorldClocks[1].Label == "UTC");
    bool invalid = false; try { new WorldClock { TimeZoneId = "Invalid/Test" }.Validate(); } catch (ArgumentException) { invalid = true; } Assert(invalid);
    s.WorldClocks = Enumerable.Range(0, 9).Select(_ => new WorldClock()).ToList();
    invalid = false; try { s.Validate(); } catch (ArgumentException) { invalid = true; } Assert(invalid);
}));
await Test("All UI texts have five complete translations with consistent format placeholders", () => Sync(() =>
{
    Assert(L.Languages.Select(x => x.Id).SequenceEqual(new[] { "en", "cs", "pl", "de", "es" }));
    foreach (var row in L.Texts)
    {
        Assert(row.Value.Length == 5 && row.Value.All(x => !string.IsNullOrWhiteSpace(x)), "Incomplete: " + row.Key);
        var placeholders = System.Text.RegularExpressions.Regex.Matches(row.Value[0], @"\{\d+\}").Select(x => x.Value).Distinct().Order().ToArray();
        foreach (var value in row.Value)
            Assert(placeholders.SequenceEqual(System.Text.RegularExpressions.Regex.Matches(value, @"\{\d+\}").Select(x => x.Value).Distinct().Order()), "Placeholder mismatch: " + row.Key);
        foreach (var language in L.Languages)
        { L.SetLanguage(language.Id); _ = L.Text(row.Key, "a", "b", "c"); }
    }
    L.SetLanguage("xx"); Assert(L.Language == "en" && L.Text("Players") == "PLAYERS");
}));
await Test("Server profiles preserve independent schedules and warning minutes", () => Sync(() =>
{
    var s = new MonitorSettings(); s.SavedServers.Add(ServerProfile.From(s));
    var p = new ServerProfile { Name = "Other server", IpAddress = "192.0.2.10", QueryPort = 27016,
        RestartTimes = ["04:00", "16:00"], LogoutWarningMinutes = 12, KickWarningMinutes = 3 };
    p.Validate(); s.SavedServers.Add(p);
    var restored = s.Copy(); Assert(restored.SavedServers.Count == 2);
    restored.SavedServers[1].ApplyTo(restored); restored.Validate();
    Assert(restored.IpAddress == "192.0.2.10" && restored.LogoutWarningMinutes == 12 && restored.KickWarningMinutes == 3);
    Assert(restored.SavedServers[0].IpAddress == "217.182.198.57");
}));
await Test("Restart interval generator anchored and sorted; irregular daily interval rejected", () => Sync(() =>
{
    Assert(RestartSchedule.GenerateDaily(4, "03:00").SequenceEqual(new[] { "03:00", "07:00", "11:00", "15:00", "19:00", "23:00" }));
    Assert(RestartSchedule.GenerateDaily(6, "01:15").SequenceEqual(new[] { "01:15", "07:15", "13:15", "19:15" }));
    Assert(RestartSchedule.GenerateDaily(24, "19:00").SequenceEqual(new[] { "19:00" }));
    bool rejected = false; try { RestartSchedule.GenerateDaily(5, "03:00"); } catch (ArgumentException) { rejected = true; } Assert(rejected);
}));
await Test("Winter / summer Prague and midnight rollover", () => Sync(() =>
{
    Assert(RestartSchedule.CzechTime(Utc("2026-01-01T12:00:00Z")).Hour == 13);
    Assert(RestartSchedule.CzechTime(Utc("2026-07-01T12:00:00Z")).Hour == 14);
    Assert(schedule.Next(Utc("2026-10-28T22:00:00Z")) == Utc("2026-10-29T02:00:00Z"));
    Assert(schedule.Next(Utc("2026-10-28T13:59:59Z")) == Utc("2026-10-28T14:00:00Z"));
}));
await Test("DST spring gap skipped / autumn duplicate both instants", () => Sync(() =>
{
    var dst = new RestartSchedule(["02:30", "07:00"]);
    Assert(dst.Next(Utc("2026-03-29T00:59:00Z")) == Utc("2026-03-29T05:00:00Z"));
    Assert(dst.Next(Utc("2026-10-25T00:00:00Z")) == Utc("2026-10-25T00:30:00Z"));
    Assert(dst.Next(Utc("2026-10-25T00:30:00Z")) == Utc("2026-10-25T01:30:00Z"));
}));
await Test("Default DST restart countdown uses elapsed UTC time", () => Sync(() =>
{
    Assert(schedule.Next(Utc("2026-03-29T00:00:00Z")) - Utc("2026-03-29T00:00:00Z") == TimeSpan.FromHours(1));
    Assert(schedule.Next(Utc("2026-10-25T00:00:00Z")) - Utc("2026-10-25T00:00:00Z") == TimeSpan.FromHours(2));
}));
await Test("Adaptive cadence boundaries and extended restart gap", () => Sync(() =>
{
    var s = new MonitorSettings();
    Assert(schedule.PollInterval(s, Utc("2026-10-28T13:00:00Z"), false) == 300);
    Assert(schedule.PollInterval(s, Utc("2026-10-28T13:45:00Z"), false) == 30);
    Assert(schedule.PollInterval(s, Utc("2026-10-28T14:00:00Z"), false) == 15);
    Assert(schedule.PollInterval(s, Utc("2026-10-28T14:04:59Z"), false) == 15);
    Assert(schedule.PollInterval(s, Utc("2026-10-28T14:05:00Z"), false) == 300);
    Assert(schedule.PollInterval(s, Utc("2026-10-28T15:00:00Z"), true) == 15);
}));
await Test("Alerts exactly once across refresh, relaunch, clock reversal", () => Sync(() =>
{
    var ledger = new AlertLedger();
    var t = Utc("2026-10-28T13:50:00Z");
    Assert(ledger.Evaluate(t, schedule, "host")?.Minutes == 10);
    Assert(ledger.Evaluate(t.AddSeconds(2), schedule, "host") is null);
    Assert(ledger.Evaluate(t.AddMinutes(5), schedule, "host")?.Minutes == 5);
    var restored = new AlertLedger(ledger.Snapshot());
    Assert(restored.Evaluate(t, schedule, "host") is null);
    Assert(restored.Evaluate(t.AddMinutes(5), schedule, "host") is null);
    Assert(restored.Evaluate(t.AddMinutes(10), schedule, "host")?.Minutes == 0);
    Assert(restored.Evaluate(t.AddMinutes(10).AddSeconds(30), schedule, "host") is null);
    Assert(restored.Evaluate(t.AddHours(4), schedule, "host")?.Minutes == 10);
}));
await Test("Late startup emits only strongest applicable alert", () => Sync(() =>
{
    var ledger = new AlertLedger();
    Assert(ledger.Evaluate(Utc("2026-10-28T13:56:00Z"), schedule, "host")?.Minutes == 5);
    Assert(ledger.Evaluate(Utc("2026-10-28T13:51:00Z"), schedule, "host") is null);
    Assert(ledger.Evaluate(Utc("2026-10-28T14:03:00Z"), schedule, "host") is null);
}));
await Test("Custom minute warnings and persistence", () => Sync(() =>
{
    var ledger = new AlertLedger(); var t = Utc("2026-10-28T13:48:00Z");
    Assert(ledger.Evaluate(t.AddSeconds(-1), schedule, "custom", 12, 3) is null);
    Assert(ledger.Evaluate(t, schedule, "custom", 12, 3)?.Minutes == 12);
    Assert(ledger.Evaluate(t.AddMinutes(8), schedule, "custom", 12, 3) is null);
    Assert(ledger.Evaluate(t.AddMinutes(9), schedule, "custom", 12, 3)?.Minutes == 3);
    Assert(new AlertLedger(ledger.Snapshot()).Evaluate(t.AddMinutes(9), schedule, "custom", 12, 3) is null);
}));

ServerInfo info = new("MAG fixture", "DeerIsle", "DayZ", 87, 121, "1.0");
QueryResult Ok(DateTimeOffset t) => new(t, info, 31, QueryFailure.None, "OK");
QueryResult Fail(DateTimeOffset t) => new(t, null, null, QueryFailure.Timeout, "timeout");
await Test("Single timeout keeps previous status but removes metrics; threshold never claims game offline", () => Sync(() =>
{
    var tr = new AvailabilityTracker(); var t = Utc("2026-10-28T12:00:00Z");
    tr.Apply(Ok(t), 3, schedule); tr.Apply(Fail(t.AddMinutes(5)), 3, schedule);
    Assert(tr.Status(t.AddMinutes(5), schedule, 3) == ServerStatus.Online);
    Assert(tr.Latest?.Info is null);
    tr.Apply(Fail(t.AddMinutes(10)), 3, schedule); tr.Apply(Fail(t.AddMinutes(15)), 3, schedule);
    Assert(tr.Status(t.AddMinutes(15), schedule, 3) == ServerStatus.QueryUnavailable);
    tr.ConfirmOffline(); Assert(tr.Status(t.AddMinutes(15), schedule, 3) == ServerStatus.Offline);
    var recovered = tr.Apply(Ok(t.AddMinutes(20)), 3, schedule);
    Assert(recovered is { FailedQueries: 3, NearPlannedRestart: false });
    Assert(tr.Status(t.AddMinutes(20), schedule, 3) == ServerStatus.Online && !tr.ManualOffline);
}));
await Test("Initial failure / transient failure does not invent recovery", () => Sync(() =>
{
    var t = Utc("2026-10-28T12:00:00Z"); var tr = new AvailabilityTracker();
    tr.Apply(Fail(t), 3, schedule); Assert(tr.Apply(Ok(t.AddSeconds(5)), 3, schedule) is null);
    tr.Apply(Fail(t.AddMinutes(5)), 3, schedule); Assert(tr.Apply(Ok(t.AddMinutes(6)), 3, schedule) is null);
}));
await Test("Restart expectation ends on verified response", () => Sync(() =>
{
    var t = Utc("2026-10-28T14:00:00Z"); var tr = new AvailabilityTracker();
    tr.Apply(Ok(t.AddMinutes(-1)), 3, schedule);
    tr.Apply(Fail(t), 3, schedule); tr.Apply(Fail(t.AddSeconds(15)), 3, schedule); tr.Apply(Fail(t.AddSeconds(30)), 3, schedule);
    Assert(tr.Status(t.AddMinutes(8), schedule, 3) == ServerStatus.RestartExpected);
    Assert(tr.Apply(Ok(t.AddMinutes(9)), 3, schedule)?.NearPlannedRestart == true);
    Assert(!tr.RestartGap && tr.Status(t.AddMinutes(9), schedule, 3) == ServerStatus.Online);
}));

byte[] InfoPacket(bool edf = false, string keywords = "pve", ushort appId = 24492)
{
    using var ms = new MemoryStream(); using var w = new BinaryWriter(ms);
    void Str(string s) { w.Write(Encoding.UTF8.GetBytes(s)); w.Write((byte)0); }
    w.Write(-1); w.Write((byte)0x49); w.Write((byte)17);
    Str("MAG fixture"); Str("DeerIsle"); Str("dayz"); Str("DayZ");
    w.Write(appId); w.Write((byte)87); w.Write((byte)121); w.Write((byte)0);
    w.Write((byte)'d'); w.Write((byte)'w'); w.Write((byte)0); w.Write((byte)1); Str("1.0");
    if (edf) { w.Write((byte)0xF1); w.Write((ushort)2302); w.Write(123UL); w.Write((ushort)2304); Str("spectator"); Str(keywords); w.Write(221100UL); }
    return ms.ToArray();
}
await Test("A2S parser reads DayZ slots and full EDF", () => Sync(() =>
{
    var parsed = A2sClient.ParseInfo(InfoPacket(true)); Assert(parsed == info);
    var extended = A2sClient.ParseInfo(InfoPacket(true, "lqs7,etm4,entm4,19:42"));
    Assert(extended.GameClock == new DayZGameClock(19, 42, 4, 4) && extended.Queue == 7 && extended.Players == 87);
    var modern = A2sClient.ParseInfo(InfoPacket(true, "lqs0,etm4,entm4,19:42", 0));
    Assert(modern.GameClock == new DayZGameClock(19, 42, 4, 4) && modern.Queue == 0, "Extended GameID did not identify DayZ");
}));
await Test("GoldSource CS 1.6 reply parses safely and rejects every truncation", () => Sync(() =>
{
    using var data = new MemoryStream(); using var w = new BinaryWriter(data);
    w.Write(uint.MaxValue); w.Write((byte)0x6d);
    foreach (var text in new[] { "127.0.0.1:27015", "CS fixture", "de_dust2", "cstrike", "Counter-Strike" }) { w.Write(Encoding.UTF8.GetBytes(text)); w.Write((byte)0); }
    w.Write(new byte[] { 12, 32, 48, (byte)'d', (byte)'l', 0, 0, 1, 0 });
    var packet = data.ToArray(); var info = A2sClient.ParseInfo(packet);
    Assert(info.Players == 12 && info.MaxPlayers == 32 && info.Map == "de_dust2" && info.GameClock is null && info.Queue is null);
    for (int length = 0; length < packet.Length; length++)
    { bool rejected = false; try { A2sClient.ParseInfo(packet.AsSpan(0, length)); } catch (InvalidDataException) { rejected = true; } Assert(rejected); }
}));
await Test("Every truncated packet rejected safely", () => Sync(() =>
{
    var packet = InfoPacket();
    for (int length = 0; length < packet.Length; length++)
    {
        bool rejected = false; try { A2sClient.ParseInfo(packet.AsSpan(0, length)); } catch (InvalidDataException) { rejected = true; }
        Assert(rejected, "Accepted truncation at " + length);
    }
}));
async Task<QueryResult> Fixture(Func<UdpClient, UdpReceiveResult, Task> handler, double timeout = 2)
{
    using var server = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
    var port = ((IPEndPoint)server.Client.LocalEndPoint!).Port;
    var task = Task.Run(async () => { var request = await server.ReceiveAsync(); await handler(server, request); });
    var result = await new A2sClient().QueryAsync("127.0.0.1", port, TimeSpan.FromSeconds(timeout), CancellationToken.None);
    await task; return result;
}
await Test("UDP direct info / RTT", async () =>
{
    var result = await Fixture(async (s, req) => { Assert(req.Buffer[4] == 0x54 && req.Buffer[^1] == 0); await s.SendAsync(InfoPacket(), req.RemoteEndPoint); });
    Assert(result.Success && result.Info == info && result.RttMs is >= 0);
});
await Test("UDP challenge handshake preserves exact challenge bytes", async () =>
{
    var result = await Fixture(async (s, req) =>
    {
        byte[] challenge = [255, 255, 255, 255, 0x41, 0x12, 0xFE, 0x34, 0x80];
        await s.SendAsync(challenge, req.RemoteEndPoint);
        var challenged = await s.ReceiveAsync(); Assert(challenged.Buffer.AsSpan(^4).SequenceEqual(challenge.AsSpan(5)));
        Assert(challenged.Buffer.Length == req.Buffer.Length + 4);
        await s.SendAsync(InfoPacket(), challenged.RemoteEndPoint);
    }); Assert(result.Success);
});
byte[] Fragment(byte[] payload, byte index, byte count = 2, uint id = 123)
{
    var packet = new byte[12 + payload.Length]; BinaryPrimitives.WriteInt32LittleEndian(packet, -2);
    BinaryPrimitives.WriteUInt32LittleEndian(packet.AsSpan(4), id); packet[8] = count; packet[9] = index;
    BinaryPrimitives.WriteUInt16LittleEndian(packet.AsSpan(10), 1248); payload.CopyTo(packet, 12); return packet;
}
await Test("UDP split fragments reordered / duplicated", async () =>
{
    var result = await Fixture(async (s, req) =>
    {
        var packet = InfoPacket(); var a = Fragment(packet[..20], 0); var b = Fragment(packet[20..], 1);
        await s.SendAsync(b, req.RemoteEndPoint); await s.SendAsync(b, req.RemoteEndPoint); await s.SendAsync(a, req.RemoteEndPoint);
    }); Assert(result.Success && result.Info?.Players == 87);
});
await Test("UDP malformed response, invalid challenge, missing split, compressed split", async () =>
{
    Assert((await Fixture(async (s, req) => { await s.SendAsync(new byte[] { 1, 2 }, req.RemoteEndPoint); })).Failure == QueryFailure.InvalidResponse);
    Assert((await Fixture(async (s, req) => { await s.SendAsync(new byte[] { 255, 255, 255, 255, 0x41 }, req.RemoteEndPoint); })).Failure == QueryFailure.InvalidResponse);
    Assert((await Fixture(async (s, req) => { await s.SendAsync(Fragment([1, 2], 0), req.RemoteEndPoint); }, .15)).Failure == QueryFailure.Timeout);
    Assert((await Fixture(async (s, req) => { await s.SendAsync(Fragment([1, 2], 0, 2, 0x80000001), req.RemoteEndPoint); })).Failure == QueryFailure.UnsupportedCompression);
});
await Test("Timeout has no fake zero metrics / cancellation propagates", async () =>
{
    var result = await Fixture((_, _) => Task.CompletedTask, .15);
    Assert(result.Failure == QueryFailure.Timeout && result.Info is null && result.RttMs is null);
    using var cancel = new CancellationTokenSource(); cancel.Cancel(); bool canceled = false;
    try { await new A2sClient().QueryAsync("127.0.0.1", 2303, TimeSpan.FromSeconds(2), cancel.Token); }
    catch (OperationCanceledException) { canceled = true; } Assert(canceled);
});
await Test("History JSONL / retention / atomic settings", async () =>
{
    var dir = Path.Combine(Path.GetTempPath(), "MagDeerTests-" + Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(dir);
    try
    {
        var t = DateTimeOffset.UtcNow;
        var old = Path.Combine(dir, $"query-{t.AddDays(-10):yyyy-MM-dd}.jsonl"); await File.WriteAllTextAsync(old, "old");
        await new HistoryStore(dir, 2).AppendAsync("fixture", Ok(t), ServerStatus.Online, null, CancellationToken.None);
        Assert(!File.Exists(old));
        var row = await File.ReadAllTextAsync(Path.Combine(dir, $"query-{t:yyyy-MM-dd}.jsonl"));
        Assert(JsonDocument.Parse(row).RootElement.GetProperty("Info").GetProperty("Players").GetInt32() == 87);
        var config = Path.Combine(dir, "config.json"); JsonStorage.Write(config, new MonitorSettings());
        Assert(JsonStorage.Read<MonitorSettings>(config).QueryPort == 2303 && !File.Exists(config + ".tmp"));
    }
    finally { Directory.Delete(dir, true); }
});
Console.WriteLine($"\n{passed} passed, {failed} failed");
return failed == 0 ? 0 : 1;
