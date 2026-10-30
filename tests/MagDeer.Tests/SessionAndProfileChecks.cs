using MagDeer.Core;

internal static class SessionAndProfileChecks
{
    private sealed class Clock : TimeProvider
    {
        public DateTimeOffset Utc = new(2026, 10, 30, 12, 0, 0, TimeSpan.Zero);
        public long Stamp;
        public override long TimestampFrequency => TimeSpan.TicksPerSecond;
        public override DateTimeOffset GetUtcNow() => Utc;
        public override long GetTimestamp() => Stamp;
        public void Advance(int seconds) { Utc = Utc.AddSeconds(seconds); Stamp += TimeSpan.FromSeconds(seconds).Ticks; }
    }
    private static void Assert(bool value, string message = "check failed") { if (!value) throw new InvalidOperationException(message); }
    private static void Reject(Action action) { try { action(); } catch (ArgumentException) { return; } throw new InvalidOperationException("Invalid edit accepted"); }
    public static IEnumerable<(string Name, Action Run)> All()
    {
        yield return ("Game session excludes Steam, launchers, server and other Windows sessions", () =>
        {
            var at = DateTimeOffset.UtcNow.AddHours(-1);
            var launchers = new[] { "steam", "DayZLauncher", "DayZ_BE", "DZSA Launcher", "DayZServer_x64", "DayzServerMonitor" }
                .Select((name, i) => new GameProcessCandidate(i + 1, name, 2, at)).ToArray();
            Assert(GameSessionProbe.Select(launchers, 2).State == GameSessionState.NotRunning);
            var games = launchers.Concat(new[] { new GameProcessCandidate(99, "DayZ_x64.exe", 2, at), new GameProcessCandidate(100, "DayZ_x64", 3, at) });
            Assert(GameSessionProbe.Select(games, 2).ProcessId == 99);
            Assert(GameSessionProbe.Select(launchers, 2).State == GameSessionState.NotRunning, "Launcher kept game session alive");
        });
        yield return ("Game session finds an already running game and does not reset on each probe", () =>
        {
            var clock = new Clock(); var tracker = new GameSessionTracker(clock);
            var game = new GameSessionObservation(GameSessionState.Running, 10, clock.Utc.AddHours(-2));
            tracker.Observe(game); Assert(tracker.Snapshot().ElapsedSeconds == 7200);
            clock.Advance(17); tracker.Observe(game); Assert(tracker.Snapshot().ElapsedSeconds == 7217);
            Assert(tracker.Snapshot().Source == "WindowsProcess");
        });
        yield return ("Game session ignores wall-clock jumps after anchoring", () =>
        {
            var clock = new Clock(); var tracker = new GameSessionTracker(clock);
            var game = new GameSessionObservation(GameSessionState.Running, 10, clock.Utc.AddMinutes(-3));
            tracker.Observe(game); clock.Utc = clock.Utc.AddHours(2); clock.Advance(5); tracker.Observe(game);
            Assert(tracker.Snapshot().ElapsedSeconds == 185);
            clock.Utc = clock.Utc.AddHours(-4); clock.Advance(2); tracker.Observe(game);
            Assert(tracker.Snapshot().ElapsedSeconds == 187);
        });
        yield return ("Closing the game clears session and a reused PID with a new start resets it", () =>
        {
            var clock = new Clock(); var tracker = new GameSessionTracker(clock);
            tracker.Observe(new(GameSessionState.Running, 10, clock.Utc.AddHours(-2)));
            tracker.Observe(new(GameSessionState.NotRunning)); Assert(tracker.Snapshot().ElapsedSeconds is null);
            clock.Advance(10); tracker.Observe(new(GameSessionState.Running, 10, clock.Utc.AddSeconds(-3)));
            Assert(tracker.Snapshot().ElapsedSeconds == 3);
            clock.Advance(20); tracker.Observe(new(GameSessionState.Running, 10, clock.Utc)); Assert(tracker.Snapshot().ElapsedSeconds == 0);
        });
        yield return ("Unknown, disabled, multiple or future game start never fabricates session duration", () =>
        {
            var clock = new Clock(); var tracker = new GameSessionTracker(clock);
            foreach (var state in new[] { GameSessionState.Unavailable, GameSessionState.Disabled, GameSessionState.MultipleInstances })
            { tracker.Observe(new(state)); Assert(tracker.Snapshot().ElapsedSeconds is null); }
            tracker.Observe(new(GameSessionState.Running, 10, clock.Utc.AddMinutes(1))); Assert(tracker.Snapshot().State == "Unavailable");
            Assert(GameSessionProbe.Select(new[] { new GameProcessCandidate(1, "DayZ_x64", 2, null) }, 2).State == GameSessionState.Unavailable);
            Assert(GameSessionTracker.Format(90007) == "25:00:07");
        });
        yield return ("Server edit renames and changes endpoint in place, rejects collisions, and leaves original settings intact", () =>
        {
            var original = new MonitorSettings { SavedServers = new() { new() { Name = "Alpha" }, new() { Name = "Beta" } } };
            var draft = original.Copy().SavedServers; var edited = draft[0];
            var replacement = new ServerProfile { Name = "Gamma", IpAddress = "127.0.0.1", QueryPort = 2403 };
            ServerProfileEditor.Save(draft, edited, replacement);
            Assert(draft.Count == 2 && ReferenceEquals(draft[0], replacement) && original.SavedServers[0].Name == "Alpha");
            Reject(() => ServerProfileEditor.Save(draft, replacement, new() { Name = "beta" }));
            Assert(draft[0].Name == "Gamma" && draft.Count == 2);
        });
        yield return ("Server removal selects a remaining profile and protects the last one", () =>
        {
            var profiles = new List<ServerProfile> { new() { Name = "Alpha" }, new() { Name = "Beta" } };
            var next = ServerProfileEditor.Remove(profiles, profiles[0]); Assert(profiles.Count == 1 && next.Name == "Beta");
            Reject(() => ServerProfileEditor.Remove(profiles, next)); Assert(profiles.Count == 1);
        });
        yield return ("Server names sort alphabetically and MAG provenance requires a published IP and game port", () =>
        {
            var sorted = ServerIdentity.Sort(new[] { new ServerProfile { Name = "Zulu" }, new() { Name = "beta" }, new() { Name = "Alpha" } }).ToArray();
            Assert(sorted[0].Name == "Alpha" && sorted[2].Name == "Zulu");
            Assert(ServerIdentity.MagSource("57.129.39.17", 2302) is not null && ServerIdentity.MagSource("57.129.39.17", 2802) is null);
            Assert(ServerIdentity.MagSource("203.0.113.1", 2302) is null);
            Assert(new ServerProfile { Name = "OFFICIAL MAG Clone", IpAddress = "203.0.113.1" }.ToString().Contains(L.Text("AddressUnverifiedShort")));
        });
        yield return ("Catalog v2 removes misleading legacy label without restoring removed servers or overwriting custom names", () =>
        {
            var settings = new MonitorSettings { ServerCatalogVersion = 1, SavedServers = new() {
                new() { Name = "OFFICIAL MAG MiddleAgedGamers #3 Vanilla+", IpAddress = "57.129.39.17", GamePort = 2302 },
                new() { Name = "My custom name", IpAddress = "217.182.198.57", GamePort = 2302 } } };
            Assert(ServerCatalog.MergeOnce(settings)); Assert(settings.SavedServers.Count == 2);
            Assert(settings.SavedServers[0].Name == "MAG MiddleAgedGamers #3 Vanilla+" && settings.SavedServers[1].Name == "My custom name");
            settings.SavedServers.RemoveAt(0); Assert(!ServerCatalog.MergeOnce(settings) && settings.SavedServers.Count == 1);
        });
    }
}
