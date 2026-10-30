using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using System.Windows.Input;
using System.Windows.Threading;

namespace MagDeer.App;

public sealed class RuntimeState
{
    public Dictionary<string, DateTimeOffset> Alerts { get; set; } = [];
    public Dictionary<string, Recovery> Recoveries { get; set; } = [];
}

public partial class MainWindow : Window
{
    private MonitorSettings settings;
    private RestartSchedule schedule;
    private AvailabilityTracker tracker = new();
    private readonly A2sClient client = new();
    private readonly NtpClient ntpClient = new();
    private readonly MonitorClock clock = new();
    private readonly QueryRefreshGuard refreshGuard = new();
    private StreamDeckBridge? deckBridge;
    private DeckSnapshot deckSnapshot = new();
    private readonly CancellationTokenSource stop = new();
    private readonly DispatcherTimer timer = new() { Interval = TimeSpan.FromSeconds(1) };
    private readonly Notifications notifications;
    private AlertLedger ledger;
    private readonly RuntimeState runtime;
    private HistoryStore history;
    private Task pollTask = Task.CompletedTask;
    private long? lastAttempt;
    private Task ntpTask = Task.CompletedTask;
    private long? lastNtpAttempt;
    private int ntpRetrySeconds, ntpFailures;
    private bool ntpSyncing, ntpDenied;
    private DateTimeOffset? lastTickUtc;
    private long lastTickTimestamp;
    private bool polling, closing, allowClose, fullscreen, configuring;
    private Rect normalBounds;
    private WindowState normalState;
    private string RuntimePath => Path.Combine(App.DataRoot, "data", "session.json");
    private static Brush Mint => (Brush)Application.Current.FindResource("AccentBrush");
    private static Brush Success => (Brush)Application.Current.FindResource("SuccessBrush");
    private static Brush Yellow => (Brush)Application.Current.FindResource("WarningBrush");
    private static Brush Red => (Brush)Application.Current.FindResource("DangerBrush");
    private static Brush Muted => (Brush)Application.Current.FindResource("MutedBrush");
    private bool changingProfile, exitRequested, started;
    private WindowState lastVisibleState;
    private static Brush Brush(string color) { var b = (SolidColorBrush)new BrushConverter().ConvertFromString(color)!; b.Freeze(); return b; }

    public MainWindow(MonitorSettings settings)
    {
        this.settings = settings;
        ConfigureClock();
        schedule = new(settings.RestartTimes);
        history = new(Path.Combine(App.DataRoot, "data", "history"), settings.HistoryRetentionDays);
        InitializeComponent();
        Dashboard.ServerSelected += ServerSelectionChanged;
        Dashboard.ClockSyncRequested += SyncNowClick;
        notifications = new(this);
        notifications.Failed += ReportStorageError;
        notifications.ExitRequested += Quit;
        Application.Current.SessionEnding += (_, _) => exitRequested = true;
        try { runtime = JsonStorage.Read<RuntimeState>(RuntimePath); }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        { runtime = new(); ReportStorageError(L.Text("LedgerReadError", L.Error(ex))); }
        ledger = new(runtime.Alerts ?? []);
        runtime.Recoveries ??= [];
        timer.Tick += (_, _) => Tick();
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (started) return;
        started = true;
        StateChanged += (_, _) => { if (WindowState != WindowState.Minimized) lastVisibleState = WindowState; };
        DesktopServices.PlaceWindow(this, settings, false);
        if (settings.Maximized) WindowState = WindowState.Maximized;
        ApplyAppearance();
        if (settings.Fullscreen) EnterFullscreen();
        try
        {
            deckBridge = new(App.DataRoot, () => Volatile.Read(ref deckSnapshot), async ct =>
                await Dispatcher.InvokeAsync(RequestManualRefresh, DispatcherPriority.Normal, ct));
            deckBridge.Start();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        { ReportStorageError("Stream Deck: " + ex.Message); deckBridge = null; }
        timer.Start();
        Microsoft.Win32.SystemEvents.PowerModeChanged += OnPowerModeChanged;
        Tick();
    }

    private void Tick()
    {
        if (closing) return;
        var now = clock.UtcNow;
        long tickStamp = Stopwatch.GetTimestamp();
        if (lastTickUtc is { } previous && Math.Abs(((now - previous) - Stopwatch.GetElapsedTime(lastTickTimestamp, tickStamp)).TotalSeconds) > 5)
        {
            // Observations made on another clock basis cannot establish a precise gap.
            tracker = new(); lastAttempt = null; Dashboard.Chart.Clear();
        }
        lastTickUtc = now; lastTickTimestamp = tickStamp;
        UpdateDisplay(now);
        var alert = ledger.Evaluate(now, schedule, settings.EndpointKey, settings.LogoutWarningMinutes, settings.KickWarningMinutes);
        if (alert is not null)
        {
            SaveRuntime();
            string title = alert.Minutes == 0 ? L.Text("RestartNow") : L.Text("RestartNotice", alert.Minutes);
            string body = alert.Minutes == 0 ? L.Text("WarnExpected") : alert.Minutes == settings.KickWarningMinutes
                ? L.Text("WarnKick", alert.Minutes) : L.Text("WarnLogout", alert.Minutes);
            notifications.Show(settings, title + (settings.ScheduleConfirmed ? "" : L.Text("UnconfirmedSuffix")), body);
        }
        int interval = schedule.PollInterval(settings, now, tracker.RestartGap && tracker.ConfirmedQueryGap);
        if (!configuring && !polling && (lastAttempt is null || Stopwatch.GetElapsedTime(lastAttempt.Value) >= TimeSpan.FromSeconds(interval)))
            pollTask = PollAsync();
        if (settings.NtpEnabled && !configuring && !ntpSyncing && !ntpDenied &&
            (lastNtpAttempt is null || Stopwatch.GetElapsedTime(lastNtpAttempt.Value) >= TimeSpan.FromSeconds(ntpRetrySeconds)))
            ntpTask = SynchronizeNtpAsync();
    }

    private async Task PollAsync(bool manual = false)
    {
        if (polling || closing || configuring) return;
        if (manual && !refreshGuard.TryBeginManual()) return;
        if (!manual) refreshGuard.RecordAutomaticStart();
        polling = true;
        UpdateRefreshButton();
        long queryStamp = Stopwatch.GetTimestamp();
        lastAttempt = queryStamp;
        var requestedAt = clock.UtcNow;
        Dashboard.PollText.Text = L.Text("Polling");
        try
        {
            var result = await client.QueryAsync(settings.IpAddress, settings.QueryPort, TimeSpan.FromSeconds(settings.QueryTimeoutSeconds), stop.Token);
            if (closing) return;
            var completedAt = clock.UtcNow;
            // If NTP changed during the request, put both timestamps on the new basis.
            var elapsed = Stopwatch.GetElapsedTime(queryStamp);
            if (Math.Abs((completedAt - requestedAt - elapsed).TotalSeconds) > 5) requestedAt = completedAt - elapsed;
            result = result with { SystemAt = result.At, At = completedAt, RequestedAt = requestedAt, TimeSource = clock.Source.ToString() };
            var recovery = tracker.Apply(result, settings.FailureThreshold, schedule);
            Dashboard.Chart.Add(result);
            if (recovery is not null)
            {
                runtime.Recoveries[settings.EndpointKey] = recovery;
                SaveRuntime();
                notifications.Show(settings, L.Text("RecoveredTitle"), L.Text("RecoveredBody", LocalTime(recovery.RecoveredAt)));
            }
            UpdateDisplay(clock.UtcNow);
            try { await history.AppendAsync(settings.EndpointKey, result, tracker.Status(result.At, schedule, settings.FailureThreshold), recovery, stop.Token); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { ReportStorageError(L.Text("HistoryError", L.Error(ex))); }
        }
        catch (OperationCanceledException) when (stop.IsCancellationRequested) { }
        catch (Exception ex)
        {
            // A failed poll must not silently stop future monitoring.
            tracker.Apply(new(clock.UtcNow, null, null, QueryFailure.Network, ex.Message) { SystemAt = DateTimeOffset.UtcNow, TimeSource = clock.Source.ToString() }, settings.FailureThreshold, schedule);
            if (!closing) UpdateDisplay(clock.UtcNow);
        }
        finally { polling = false; if (!closing) UpdateRefreshButton(); }
    }

    private void UpdateRefreshButton()
    {
        int seconds = refreshGuard.RemainingSeconds;
        RefreshButton.Content = seconds > 0 ? seconds.ToString(CultureInfo.InvariantCulture) : "↻";
        RefreshButton.ToolTip = seconds > 0 ? L.Text("RefreshCooldown", seconds) : L.Text("RefreshTip");
        RefreshButton.IsEnabled = !closing && !configuring && !polling && seconds == 0;
    }

    private string LocalTime(DateTimeOffset instant, bool seconds = false) => RestartSchedule.CzechTime(instant)
        .ToString(settings.Use24HourClock ? seconds ? "HH:mm:ss" : "HH:mm" : seconds ? "hh:mm:ss tt" : "hh:mm tt", CultureInfo.InvariantCulture);

    private void UpdateDisplay(DateTimeOffset now)
    {
        var local = RestartSchedule.CzechTime(now);
        Dashboard.ClockText.Text = LocalTime(now);
        Dashboard.ClockText.FontSize = settings.Use24HourClock ? 94 : 76;
        Dashboard.DateText.Text = local.ToString("D", L.Culture);
        Dashboard.ServerClockText.Text = L.Text("ServerClock", local.AddHours(settings.ServerOffsetHours).ToString(settings.Use24HourClock ? "HH:mm" : "hh:mm tt", CultureInfo.InvariantCulture));
        Dashboard.OffsetLabel.Text = L.Text("OffsetNote", settings.ServerOffsetHours.ToString("+0.##;-0.##;0", L.Culture));
        Dashboard.WorldClocksPanel.Update(now, settings.Use24HourClock);
        UpdateClockStatus();
        var next = schedule.Next(now);
        var remaining = next - now;
        var since = now - schedule.Previous(now);
        var expected = since < TimeSpan.FromMinutes(5) && (tracker.LastSuccess is null || tracker.LastSuccess < schedule.Previous(now));
        Dashboard.CountdownLabel.Text = L.Text(expected ? "CountdownExpected" : "Countdown");
        // Recompute from UTC instants each tick, never decrement a stored counter.
        var seconds = Math.Max(0, (long)Math.Ceiling(remaining.TotalSeconds));
        Dashboard.CountdownText.Text = $"{seconds / 3600:00}:{seconds / 60 % 60:00}:{seconds % 60:00}";
        var accent = expected || remaining <= TimeSpan.FromMinutes(settings.KickWarningMinutes) ? Red : remaining <= TimeSpan.FromMinutes(settings.LogoutWarningMinutes) ? Yellow : Mint;
        Dashboard.CountdownText.Foreground = settings.VisualAlerts ? accent : Mint;
        Dashboard.NextRestartText.Text = LocalTime(next);
        Dashboard.CompactNextRestart.Text = L.Text("NextRestart") + " · " + LocalTime(next);
        Dashboard.NextDateText.Text = RestartSchedule.CzechTime(next).ToString("d MMM · zzz", L.Culture);
        Dashboard.LogoutText.Text = L.Text("LogoutAt", LocalTime(next.AddMinutes(-settings.LogoutWarningMinutes)));
        Dashboard.KickText.Text = L.Text("KickAt", LocalTime(next.AddMinutes(-settings.KickWarningMinutes)));
        Dashboard.WarningText.Foreground = settings.VisualAlerts ? accent : Muted;
        Dashboard.WarningText.Text = expected ? L.Text("WarnExpected")
            : remaining <= TimeSpan.FromMinutes(settings.KickWarningMinutes) ? L.Text("WarnKick", settings.KickWarningMinutes)
            : remaining <= TimeSpan.FromMinutes(settings.LogoutWarningMinutes) ? L.Text("WarnLogout", settings.LogoutWarningMinutes)
            : L.Text("WarnNormal");
        var status = tracker.Status(now, schedule, settings.FailureThreshold);
        Dashboard.StatusText.Text = status switch { ServerStatus.Online => "ONLINE", ServerStatus.Offline => "OFFLINE", ServerStatus.RestartExpected => "RESTART EXPECTED", _ => "QUERY UNAVAILABLE" };
        Dashboard.StatusDot.Fill = status == ServerStatus.Online ? Success : status == ServerStatus.Offline ? Red : Yellow;
        Dashboard.StatusText.Foreground = Dashboard.StatusDot.Fill;
        Dashboard.StatusHint.Text = status switch
        {
            ServerStatus.Online when tracker.Failures > 0 => L.Text("Unverified", tracker.Failures, settings.FailureThreshold),
            ServerStatus.Online => L.Text("Responding"),
            ServerStatus.Offline => L.Text("ManualOffline"),
            ServerStatus.RestartExpected => L.Text("RestartNotProven"),
            _ => tracker.Failures == 0 ? L.Text("Waiting") : L.Text("QueryFailures", tracker.Failures)
        };
        Dashboard.PlayerCounts.Update(tracker.Latest);
        Dashboard.RttText.Text = tracker.Latest is { Success: true, RttMs: { } rtt } ? $"{rtt:0} ms" : "N/A";
        Dashboard.LastUpdateText.Text = tracker.LastSuccess is { } success ? L.Text("LastSuccess", RestartSchedule.CzechTime(success).ToString("d MMM HH:mm:ss", L.Culture)) : L.Text("NoSuccess");
        int interval = schedule.PollInterval(settings, now, tracker.RestartGap && tracker.ConfirmedQueryGap);
        int due = Math.Max(0, (int)Math.Ceiling(interval - (lastAttempt is { } attempt ? Stopwatch.GetElapsedTime(attempt).TotalSeconds : interval)));
        Dashboard.PollText.Text = polling ? L.Text("Polling") : L.Text("NextQuery", due);
        string failure = tracker.Latest?.Failure switch
        {
            QueryFailure.Timeout => L.Text("FailureTimeout", settings.QueryTimeoutSeconds),
            QueryFailure.InvalidResponse => L.Text("FailureInvalid"),
            QueryFailure.UnsupportedCompression => L.Text("FailureCompression"),
            QueryFailure.Network => L.Text("FailureNetwork"), _ => L.Text("Waiting")
        };
        Dashboard.DetailText.Text = tracker.Latest?.Success == true ? L.Text("MapDetail", tracker.Latest.Info!.Map) : failure + L.Text("UdpUncertain");
        Dashboard.DetailText.ToolTip = tracker.Latest?.Detail;
        if (runtime.Recoveries.TryGetValue(settings.EndpointKey, out var recovery))
        {
            Dashboard.RecoveryPanel.Visibility = Visibility.Visible;
            Dashboard.RecoveryText.Text = L.Text("Recovery", RestartSchedule.CzechTime(recovery.RecoveredAt).ToString("d MMM HH:mm:ss", L.Culture), LocalTime(recovery.LastFailure, true), recovery.FailedQueries);
        }
        else Dashboard.RecoveryPanel.Visibility = Visibility.Collapsed;
        UpdateRefreshButton();
        UpdateDeckSnapshot(now, status, next, expected, interval);
    }

    private void UpdateDeckSnapshot(DateTimeOffset now, ServerStatus status, DateTimeOffset next, bool expected, int interval)
    {
        var latest = tracker.Latest;
        var info = latest?.Success == true ? latest.Info : null;
        Volatile.Write(ref deckSnapshot, new DeckSnapshot
        {
            Now = now, ServerName = settings.ServerName, Endpoint = settings.EndpointKey, Status = status.ToString(),
            NextRestartAt = next, RestartExpected = expected, ScheduleConfirmed = settings.ScheduleConfirmed,
            LogoutWarningMinutes = settings.LogoutWarningMinutes, KickWarningMinutes = settings.KickWarningMinutes,
            LastQueryAt = latest?.At, LastSuccessAt = tracker.LastSuccess, QuerySuccess = latest?.Success == true,
            Players = info?.Players, Capacity = info?.MaxPlayers, Queue = info?.Queue, RttMs = info is null ? null : latest?.RttMs,
            GameClock = info?.GameClock, Polling = polling, RefreshRemainingSeconds = refreshGuard.RemainingSeconds,
            QueryIntervalSeconds = interval,
            History = Dashboard.Chart.Snapshot().Select(s => new DeckSample(QueryTimeline.TimeOf(s), s.Success ? s.Info?.Players : null, s.Success ? s.Info?.MaxPlayers : null)).ToArray()
        });
    }

    private void ApplyAppearance()
    {
        Localization.Apply(settings.Language);
        ThemeManager.Apply(settings.Theme);
        Dashboard.WorldClocksPanel.Configure(settings.WorldClocks);
        Topmost = settings.AlwaysOnTop;
        Dashboard.Configure(settings);
        UpdateCaption();
        Dashboard.ServerClockText.Visibility = Dashboard.OffsetLabel.Visibility = settings.ShowServerTime ? Visibility.Visible : Visibility.Collapsed;
        Dashboard.PlanBadgeText.Text = L.Text(settings.ScheduleConfirmed ? "PlanConfirmed" : "PlanUnconfirmed");
        Dashboard.PlanBadgeText.Foreground = settings.ScheduleConfirmed ? Success : Yellow;
        Dashboard.PlanBadge.Background = (Brush)Application.Current.FindResource(settings.ScheduleConfirmed ? "SuccessSurfaceBrush" : "WarningSurfaceBrush");
        Dashboard.ServerNameText.Text = settings.ServerName;
        Dashboard.EndpointText.Text = $"{settings.IpAddress}:{settings.GamePort}  /  Query UDP {settings.QueryPort}  /  Europe/Prague: {string.Join(" · ", settings.RestartTimes)}";
        notifications.Configure(settings);
        changingProfile = true;
        Dashboard.ServerSelector.ItemsSource = settings.SavedServers.ToArray();
        Dashboard.ServerSelector.SelectedItem = settings.SavedServers.FirstOrDefault(p => p.IpAddress == settings.IpAddress && p.QueryPort == settings.QueryPort && p.Name == settings.ServerName);
        changingProfile = false;
        Dashboard.Chart.Use24HourClock = settings.Use24HourClock;
        Dashboard.Chart.InvalidateVisual();
        UpdateRefreshButton();
    }

    private async void ServerSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (changingProfile || Dashboard.ServerSelector.SelectedItem is not ServerProfile profile || closing) return;
        configuring = true; Dashboard.ServerSelector.IsEnabled = false; RefreshButton.IsEnabled = false;
        await pollTask;
        if (closing) return;
        var next = settings.Copy(); profile.ApplyTo(next);
        try { next.Validate(); JsonStorage.Write(App.ConfigPath, next); }
        catch (Exception ex) when (ex is ArgumentException or IOException or UnauthorizedAccessException)
        {
            ReportStorageError(L.Text("ServerChangeError", L.Error(ex)));
            configuring = false; Dashboard.ServerSelector.IsEnabled = true; ApplyAppearance(); return;
        }
        settings = next; schedule = new(settings.RestartTimes); tracker = new(); lastAttempt = null; Dashboard.Chart.Clear();
        configuring = false; Dashboard.ServerSelector.IsEnabled = true;
        ApplyAppearance(); Tick();
    }

    private void SaveRuntime()
    {
        runtime.Alerts = ledger.Snapshot();
        try { JsonStorage.Write(RuntimePath, runtime); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { ReportStorageError(L.Text("LedgerError", L.Error(ex))); }
    }
    private void ReportStorageError(string message) { Dashboard.StorageErrorText.Text = message; Dashboard.StorageErrorText.Visibility = Visibility.Visible; }
    private void SaveSettings()
    {
        CaptureBounds();
        try { JsonStorage.Write(App.ConfigPath, settings); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { ReportStorageError(L.Text("SaveError", L.Error(ex))); }
    }
    private void CaptureBounds()
    {
        var r = fullscreen ? normalBounds : WindowState == WindowState.Normal ? new Rect(Left, Top, Width, Height) : RestoreBounds;
        if (!r.IsEmpty) { settings.Left = r.Left; settings.Top = r.Top; settings.Width = Math.Max(MinWidth, r.Width); settings.Height = Math.Max(MinHeight, r.Height); }
        settings.Maximized = fullscreen ? normalState == WindowState.Maximized : (WindowState == WindowState.Minimized ? lastVisibleState : WindowState) == WindowState.Maximized;
        settings.Fullscreen = fullscreen;
        settings.MonitorDevice = System.Windows.Forms.Screen.FromHandle(new System.Windows.Interop.WindowInteropHelper(this).Handle).DeviceName;
    }

    private async void SettingsClick(object sender, RoutedEventArgs e)
    {
        var dialog = new SettingsWindow(settings, tracker.Failures >= settings.FailureThreshold, tracker.ManualOffline) { Owner = this };
        if (dialog.ShowDialog() != true) return;
        configuring = true;
        RefreshButton.IsEnabled = false;
        // Finish the current endpoint's observation before replacing its configuration.
        await pollTask;
        await ntpTask;
        if (closing) { configuring = false; return; }
        var next = dialog.Result!;
        var endpointChanged = next.EndpointKey != settings.EndpointKey;
        var monitorChanged = next.MonitorDevice != settings.MonitorDevice;
        var displayChanged = next.DisplayProfile != settings.DisplayProfile;
        CaptureBounds();
        next.Left = settings.Left; next.Top = settings.Top; next.Width = settings.Width; next.Height = settings.Height;
        next.Maximized = settings.Maximized; next.Fullscreen = fullscreen;
        try
        {
            if (next.StartWithWindows != settings.StartWithWindows) DesktopServices.SetStartup(next.StartWithWindows);
            JsonStorage.Write(App.ConfigPath, next);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException)
        { MessageBox.Show(this, L.Text("SaveError", L.Error(ex)), "Dayz Server Monitor", MessageBoxButton.OK, MessageBoxImage.Warning); configuring = false; UpdateRefreshButton(); return; }
        settings = next;
        ConfigureClock();
        schedule = new(settings.RestartTimes);
        history = new(Path.Combine(App.DataRoot, "data", "history"), settings.HistoryRetentionDays);
        if (endpointChanged) { tracker = new(); lastAttempt = null; Dashboard.Chart.Clear(); }
        else if (dialog.ConfirmOffline) tracker.ConfirmOffline();
        else tracker.ClearOffline();
        if (monitorChanged)
        {
            var wasFullscreen = fullscreen;
            if (wasFullscreen) ExitFullscreen();
            WindowState = WindowState.Normal;
            DesktopServices.PlaceWindow(this, settings, true);
            if (wasFullscreen) EnterFullscreen();
        }
        if (displayChanged) ApplyDisplayProfile();
        ApplyAppearance();
        SaveSettings();
        configuring = false;
        Tick();
    }

    private DeckRefreshResult RequestManualRefresh()
    {
        int remaining = refreshGuard.RemainingSeconds;
        if (closing || configuring || polling || remaining > 0)
            return new(false, remaining, closing ? "closing" : configuring ? "settings" : polling ? "polling" : "cooldown");
        pollTask = PollAsync(manual: true);
        UpdateDisplay(clock.UtcNow);
        return new(true, refreshGuard.RemainingSeconds, "accepted");
    }
    private void RefreshClick(object sender, RoutedEventArgs e) => RequestManualRefresh();
    private void ConfigureClock()
    {
        bool changed = settings.NtpEnabled != clock.Enabled || !settings.NtpServer.Equals(clock.Server, StringComparison.OrdinalIgnoreCase);
        clock.Configure(settings.NtpEnabled, settings.NtpServer, TimeSpan.FromMinutes(Math.Max(30, settings.NtpSyncIntervalMinutes * 3)));
        if (changed) { lastNtpAttempt = null; ntpFailures = 0; ntpDenied = false; }
        ntpRetrySeconds = !changed && clock.LastFailure == NtpFailure.RateLimited
            ? Math.Max(ntpRetrySeconds, Math.Max(900, settings.NtpSyncIntervalMinutes * 60)) : settings.NtpSyncIntervalMinutes * 60;
    }
    private async Task SynchronizeNtpAsync()
    {
        if (closing || ntpSyncing || !settings.NtpEnabled || ntpDenied) return;
        ntpSyncing = true;
        lastNtpAttempt = Stopwatch.GetTimestamp();
        try
        {
            var result = await ntpClient.QueryAsync(settings.NtpServer, TimeSpan.FromSeconds(settings.NtpTimeoutSeconds), stop.Token);
            if (closing) return;
            if (result.Success)
            {
                clock.Accept(result.Sample!); ntpFailures = 0;
                ntpRetrySeconds = settings.NtpSyncIntervalMinutes * 60;
            }
            else
            {
                clock.Failed(result); ntpFailures++;
                ntpDenied = result.Failure == NtpFailure.Denied;
                ntpRetrySeconds = result.Failure == NtpFailure.RateLimited ? Math.Max(900, settings.NtpSyncIntervalMinutes * 60)
                    : Math.Min(settings.NtpSyncIntervalMinutes * 60, 120 * (1 << Math.Min(8, ntpFailures - 1)));
            }
        }
        catch (OperationCanceledException) when (stop.IsCancellationRequested) { }
        catch (Exception ex)
        {
            clock.Failed(new(null, NtpFailure.Network, ex.Message)); ntpRetrySeconds = Math.Max(120, settings.NtpSyncIntervalMinutes * 60);
        }
        finally { ntpSyncing = false; if (!closing) UpdateClockStatus(); }
    }
    private void UpdateClockStatus()
    {
        var source = clock.Source;
        Dashboard.ClockSourceText.Text = ntpSyncing ? L.Text("ClockSynchronizing", settings.NtpServer) : source switch
        {
            ClockSource.Ntp => L.Text("ClockNtp", settings.NtpServer, clock.OffsetFromWindowsMilliseconds.ToString("+0.0;-0.0;0.0", L.Culture)),
            ClockSource.NtpHoldover => L.Text("ClockHoldover", settings.NtpServer),
            ClockSource.WindowsFallback => L.Text("ClockFallback", settings.NtpServer),
            _ => L.Text("ClockWindows")
        };
        Dashboard.ClockSourceText.Foreground = source == ClockSource.Ntp ? Success : source is ClockSource.NtpHoldover or ClockSource.WindowsFallback ? Yellow : Muted;
        Dashboard.ClockSourceText.ToolTip = clock.LastDetail;
        Dashboard.NtpDetailText.Visibility = clock.Enabled && clock.LastSample is not null ? Visibility.Visible : Visibility.Collapsed;
        if (clock.LastSample is { } sample)
            Dashboard.NtpDetailText.Text = L.Text("NtpLastSync", RestartSchedule.CzechTime(sample.UtcAtReceive).ToString("d MMM HH:mm:ss", L.Culture), sample.DelayMilliseconds.ToString("0.0", L.Culture));
        Dashboard.SyncNowButton.Visibility = settings.NtpEnabled ? Visibility.Visible : Visibility.Collapsed;
        Dashboard.SyncNowButton.IsEnabled = !configuring && !ntpSyncing && !ntpDenied &&
            (lastNtpAttempt is null || Stopwatch.GetElapsedTime(lastNtpAttempt.Value).TotalSeconds >= Math.Max(64, clock.LastFailure == NtpFailure.RateLimited ? ntpRetrySeconds : 64));
    }
    private void SyncNowClick(object sender, RoutedEventArgs e)
    {
        if (Dashboard.SyncNowButton.IsEnabled && settings.NtpEnabled) ntpTask = SynchronizeNtpAsync();
    }
    private void OnPowerModeChanged(object sender, Microsoft.Win32.PowerModeChangedEventArgs e)
    {
        if (e.Mode != Microsoft.Win32.PowerModes.Resume || closing) return;
        _ = Dispatcher.InvokeAsync(() => { if (!closing) { clock.Invalidate(); lastNtpAttempt = null; lastAttempt = null; Tick(); } });
    }
    private void OnWindowSizeChanged(object sender, SizeChangedEventArgs e) => UpdateCaption();
    private void UpdateCaption()
    {
        if (CaptionRow is null) return;
        bool hidden = fullscreen || ActualWidth < 260;
        CaptionRow.Height = new GridLength(hidden ? 0 : 32);
        CaptionBar.Visibility = hidden ? Visibility.Collapsed : Visibility.Visible;
        CaptionTitle.Visibility = ActualWidth < 470 ? Visibility.Collapsed : Visibility.Visible;
        OuterFrame.BorderThickness = new Thickness(fullscreen ? 0 : 1);
    }
    private void ApplyDisplayProfile()
    {
        var profile = DisplayProfiles.All.First(x => x.Id == settings.DisplayProfile);
        if (profile.Id == "Auto") return;
        if (fullscreen) ExitFullscreen();
        WindowState = WindowState.Normal;
        DesktopServices.SizeForProfile(this, profile);
    }
    private void MoreClick(object sender, RoutedEventArgs e)
    {
        DashboardMenu.PlacementTarget = (UIElement)sender;
        DashboardMenu.IsOpen = true;
    }
    private void DashboardMenuOpened(object sender, RoutedEventArgs e)
    {
        DashboardMenu.Items.Clear();
        MenuItem Add(string label, Action clicked, bool check = false, bool selected = false)
        {
            var item = new MenuItem { Header = label, IsCheckable = check, IsChecked = selected };
            item.Click += (_, _) => clicked(); DashboardMenu.Items.Add(item); return item;
        }
        Add(L.Text("Refresh"), () => RequestManualRefresh()).IsEnabled = RefreshButton.IsEnabled;
        Add(L.Text("Settings"), () => SettingsClick(this, new RoutedEventArgs()));
        Add(L.Text("Compact"), () => CompactClick(this, new RoutedEventArgs()), true, settings.CompactMode);
        Add(L.Text("Fullscreen"), () => FullscreenClick(this, new RoutedEventArgs()), true, fullscreen);
        Add(L.Text("AlwaysTop"), () => { settings.AlwaysOnTop = !settings.AlwaysOnTop; Topmost = settings.AlwaysOnTop; SaveSettings(); }, true, settings.AlwaysOnTop);
        var profiles = new MenuItem { Header = L.Text("DisplayProfile") };
        foreach (var profile in DisplayProfiles.All)
        {
            var item = new MenuItem { Header = profile.Label, IsCheckable = true, IsChecked = settings.DisplayProfile == profile.Id };
            item.Click += (_, _) => { settings.DisplayProfile = profile.Id; ApplyDisplayProfile(); SaveSettings(); };
            profiles.Items.Add(item);
        }
        DashboardMenu.Items.Add(profiles);
        var themes = new MenuItem { Header = L.Text("Theme") };
        foreach (var theme in ThemeManager.Choices)
        {
            var item = new MenuItem { Header = theme.Label, IsCheckable = true, IsChecked = settings.Theme == theme.Id };
            item.Click += (_, _) => { settings.Theme = theme.Id; ApplyAppearance(); SaveSettings(); };
            themes.Items.Add(item);
        }
        DashboardMenu.Items.Add(themes);
        DashboardMenu.Items.Add(new Separator());
        Add(L.Text("Quit"), Quit);
    }
    private void Quit() { exitRequested = true; Close(); }
    private void CompactClick(object sender, RoutedEventArgs e) { settings.CompactMode = !settings.CompactMode; ApplyAppearance(); SaveSettings(); }
    private void FullscreenClick(object sender, RoutedEventArgs e) { if (fullscreen) ExitFullscreen(); else EnterFullscreen(); SaveSettings(); }
    private void EnterFullscreen()
    {
        if (fullscreen) return;
        normalBounds = WindowState == WindowState.Normal ? new Rect(Left, Top, Width, Height) : RestoreBounds;
        normalState = WindowState;
        fullscreen = true;
        DesktopServices.Fullscreen(this);
        UpdateCaption();
    }
    private void ExitFullscreen()
    {
        if (!fullscreen) return;
        fullscreen = false; ResizeMode = ResizeMode.CanResize;
        Left = normalBounds.Left; Top = normalBounds.Top; Width = normalBounds.Width; Height = normalBounds.Height;
        WindowState = normalState;
        UpdateCaption();
    }
    private void OnKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.F5) { RefreshClick(sender, e); e.Handled = true; }
        if (e.Key == Key.OemComma && Keyboard.Modifiers == ModifierKeys.Control) { SettingsClick(sender, e); e.Handled = true; }
        if (e.Key == Key.F11) { FullscreenClick(sender, e); e.Handled = true; }
        if (e.Key == Key.Escape && fullscreen) { ExitFullscreen(); SaveSettings(); e.Handled = true; }
    }
    private void DragHeader(object sender, MouseButtonEventArgs e)
    {
        if (fullscreen || e.OriginalSource is System.Windows.Controls.Primitives.ButtonBase || FindButton(e.OriginalSource as DependencyObject)) return;
        if (e.ClickCount == 2) MaximizeClick(sender, e); else if (e.LeftButton == MouseButtonState.Pressed) DragMove();
    }
    private static bool FindButton(DependencyObject? obj)
    {
        while (obj is not null) { if (obj is System.Windows.Controls.Primitives.ButtonBase) return true; obj = VisualTreeHelper.GetParent(obj); }
        return false;
    }
    private void MinimizeClick(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;
    private void MaximizeClick(object sender, RoutedEventArgs e) { if (fullscreen) ExitFullscreen(); WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized; }
    private void CloseClick(object sender, RoutedEventArgs e) => Close();
    private void OnClosing(object? sender, CancelEventArgs e)
    {
        if (allowClose) return;
        if (!exitRequested && !closing && settings.MinimizeOnClose)
        {
            e.Cancel = true; SaveSettings();
            if (settings.MinimizeTarget == "Tray") notifications.HideToTray();
            else WindowState = WindowState.Minimized;
            return;
        }
        e.Cancel = true;
        if (closing) return;
        closing = true;
        timer.Stop();
        // Even completed poll tasks must not make Close() re-enter the current Closing event.
        _ = Dispatcher.BeginInvoke(new Action(() => _ = FinishClosingAsync()));
    }

    private async Task FinishClosingAsync()
    {
        try
        {
            CaptureBounds();
            var snapshot = settings.Copy();
            Hide();
            stop.Cancel();
            Microsoft.Win32.SystemEvents.PowerModeChanged -= OnPowerModeChanged;
            notifications.Dispose();
            var bridgeStop = deckBridge?.DisposeAsync().AsTask() ?? Task.CompletedTask;
            // Flush the snapshot away from the UI thread. Network cleanup and saving run together.
            var save = Task.Run(() => JsonStorage.Write(App.ConfigPath, snapshot));
            bool finished = await ShutdownDrain.WaitAsync([pollTask, ntpTask, bridgeStop, save], TimeSpan.FromMilliseconds(1200));
            if (!finished || save.IsFaulted)
                Debug.WriteLine("Dayz Server Monitor shutdown: background cleanup/save exceeded budget or failed.");
        }
        catch (Exception ex) { Debug.WriteLine("Dayz Server Monitor shutdown: " + ex); }
        finally
        {
            // Dispose only after any canceled consumers have finished using the token.
            _ = Task.WhenAll(pollTask, ntpTask).ContinueWith(t => { _ = t.Exception; stop.Dispose(); }, TaskScheduler.Default);
            allowClose = true;
            Close();
        }
    }
}
