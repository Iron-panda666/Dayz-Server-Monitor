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
    private bool changingProfile;
    private static Brush Brush(string color) { var b = (SolidColorBrush)new BrushConverter().ConvertFromString(color)!; b.Freeze(); return b; }

    public MainWindow(MonitorSettings settings)
    {
        this.settings = settings;
        ConfigureClock();
        schedule = new(settings.RestartTimes);
        history = new(Path.Combine(App.DataRoot, "data", "history"), settings.HistoryRetentionDays);
        InitializeComponent();
        notifications = new(this);
        notifications.Failed += ReportStorageError;
        try { runtime = JsonStorage.Read<RuntimeState>(RuntimePath); }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        { runtime = new(); ReportStorageError(L.Text("LedgerReadError", L.Error(ex))); }
        ledger = new(runtime.Alerts ?? []);
        runtime.Recoveries ??= [];
        timer.Tick += (_, _) => Tick();
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        DesktopServices.PlaceWindow(this, settings, false);
        if (settings.Maximized) WindowState = WindowState.Maximized;
        ApplyAppearance();
        if (settings.Fullscreen) EnterFullscreen();
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
            tracker = new(); lastAttempt = null; Chart.Clear();
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
        PollText.Text = L.Text("Polling");
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
            Chart.Add(result);
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
        RefreshButton.Content = seconds > 0 ? L.Text("RefreshCooldown", seconds) : L.Text("Refresh");
        RefreshButton.IsEnabled = !closing && !configuring && !polling && seconds == 0;
    }

    private string LocalTime(DateTimeOffset instant, bool seconds = false) => RestartSchedule.CzechTime(instant)
        .ToString(settings.Use24HourClock ? seconds ? "HH:mm:ss" : "HH:mm" : seconds ? "hh:mm:ss tt" : "hh:mm tt", CultureInfo.InvariantCulture);

    private void UpdateDisplay(DateTimeOffset now)
    {
        var local = RestartSchedule.CzechTime(now);
        ClockText.Text = LocalTime(now);
        ClockText.FontSize = settings.Use24HourClock ? 108 : 86;
        DateText.Text = local.ToString("D", L.Culture);
        ServerClockText.Text = L.Text("ServerClock", local.AddHours(settings.ServerOffsetHours).ToString(settings.Use24HourClock ? "HH:mm" : "hh:mm tt", CultureInfo.InvariantCulture));
        OffsetLabel.Text = L.Text("OffsetNote", settings.ServerOffsetHours.ToString("+0.##;-0.##;0", L.Culture));
        WorldClocksPanel.Update(now, settings.Use24HourClock);
        UpdateClockStatus();
        var next = schedule.Next(now);
        var remaining = next - now;
        var since = now - schedule.Previous(now);
        var expected = since < TimeSpan.FromMinutes(5) && (tracker.LastSuccess is null || tracker.LastSuccess < schedule.Previous(now));
        CountdownLabel.Text = L.Text(expected ? "CountdownExpected" : "Countdown");
        // Recompute from UTC instants each tick, never decrement a stored counter.
        var seconds = Math.Max(0, (long)Math.Ceiling(remaining.TotalSeconds));
        CountdownText.Text = $"{seconds / 3600:00}:{seconds / 60 % 60:00}:{seconds % 60:00}";
        var accent = expected || remaining <= TimeSpan.FromMinutes(settings.KickWarningMinutes) ? Red : remaining <= TimeSpan.FromMinutes(settings.LogoutWarningMinutes) ? Yellow : Mint;
        CountdownText.Foreground = settings.VisualAlerts ? accent : Mint;
        NextRestartText.Text = LocalTime(next);
        NextDateText.Text = RestartSchedule.CzechTime(next).ToString("d MMM · zzz", L.Culture);
        LogoutText.Text = L.Text("LogoutAt", LocalTime(next.AddMinutes(-settings.LogoutWarningMinutes)));
        KickText.Text = L.Text("KickAt", LocalTime(next.AddMinutes(-settings.KickWarningMinutes)));
        WarningText.Foreground = settings.VisualAlerts ? accent : Muted;
        WarningText.Text = expected ? L.Text("WarnExpected")
            : remaining <= TimeSpan.FromMinutes(settings.KickWarningMinutes) ? L.Text("WarnKick", settings.KickWarningMinutes)
            : remaining <= TimeSpan.FromMinutes(settings.LogoutWarningMinutes) ? L.Text("WarnLogout", settings.LogoutWarningMinutes)
            : L.Text("WarnNormal");
        var status = tracker.Status(now, schedule, settings.FailureThreshold);
        StatusText.Text = status switch { ServerStatus.Online => "ONLINE", ServerStatus.Offline => "OFFLINE", ServerStatus.RestartExpected => "RESTART EXPECTED", _ => "QUERY UNAVAILABLE" };
        StatusDot.Fill = status == ServerStatus.Online ? Success : status == ServerStatus.Offline ? Red : Yellow;
        StatusText.Foreground = StatusDot.Fill;
        StatusHint.Text = status switch
        {
            ServerStatus.Online when tracker.Failures > 0 => L.Text("Unverified", tracker.Failures, settings.FailureThreshold),
            ServerStatus.Online => L.Text("Responding"),
            ServerStatus.Offline => L.Text("ManualOffline"),
            ServerStatus.RestartExpected => L.Text("RestartNotProven"),
            _ => tracker.Failures == 0 ? L.Text("Waiting") : L.Text("QueryFailures", tracker.Failures)
        };
        PlayersText.Text = tracker.Latest is { Success: true, Info: { } info } ? $"{info.Players} / {info.MaxPlayers}" : "N/A";
        RttText.Text = tracker.Latest is { Success: true, RttMs: { } rtt } ? $"{rtt:0} ms" : "N/A";
        LastUpdateText.Text = tracker.LastSuccess is { } success ? L.Text("LastSuccess", RestartSchedule.CzechTime(success).ToString("d MMM HH:mm:ss", L.Culture)) : L.Text("NoSuccess");
        int interval = schedule.PollInterval(settings, now, tracker.RestartGap && tracker.ConfirmedQueryGap);
        int due = Math.Max(0, (int)Math.Ceiling(interval - (lastAttempt is { } attempt ? Stopwatch.GetElapsedTime(attempt).TotalSeconds : interval)));
        PollText.Text = polling ? L.Text("Polling") : L.Text("NextQuery", due);
        string failure = tracker.Latest?.Failure switch
        {
            QueryFailure.Timeout => L.Text("FailureTimeout", settings.QueryTimeoutSeconds),
            QueryFailure.InvalidResponse => L.Text("FailureInvalid"),
            QueryFailure.UnsupportedCompression => L.Text("FailureCompression"),
            QueryFailure.Network => L.Text("FailureNetwork"), _ => L.Text("Waiting")
        };
        DetailText.Text = tracker.Latest?.Success == true ? L.Text("MapDetail", tracker.Latest.Info!.Map) : failure + L.Text("UdpUncertain");
        DetailText.ToolTip = tracker.Latest?.Detail;
        if (runtime.Recoveries.TryGetValue(settings.EndpointKey, out var recovery))
        {
            RecoveryPanel.Visibility = Visibility.Visible;
            RecoveryText.Text = L.Text("Recovery", RestartSchedule.CzechTime(recovery.RecoveredAt).ToString("d MMM HH:mm:ss", L.Culture), LocalTime(recovery.LastFailure, true), recovery.FailedQueries);
        }
        else RecoveryPanel.Visibility = Visibility.Collapsed;
        UpdateRefreshButton();
    }

    private void ApplyAppearance()
    {
        Localization.Apply(settings.Language);
        ThemeManager.Apply(settings.Theme);
        WorldClocksPanel.Configure(settings.WorldClocks);
        Topmost = settings.AlwaysOnTop;
        ExtendedPanel.Visibility = settings.CompactMode ? Visibility.Collapsed : Visibility.Visible;
        ServerClockText.Visibility = OffsetLabel.Visibility = settings.ShowServerTime ? Visibility.Visible : Visibility.Collapsed;
        PlanBadgeText.Text = L.Text(settings.ScheduleConfirmed ? "PlanConfirmed" : "PlanUnconfirmed");
        PlanBadgeText.Foreground = settings.ScheduleConfirmed ? Success : Yellow;
        PlanBadge.Background = (Brush)Application.Current.FindResource(settings.ScheduleConfirmed ? "SuccessSurfaceBrush" : "WarningSurfaceBrush");
        ServerNameText.Text = settings.ServerName;
        EndpointText.Text = $"{settings.IpAddress}:{settings.GamePort}  /  Query UDP {settings.QueryPort}  /  Europe/Prague: {string.Join(" · ", settings.RestartTimes)}";
        notifications.Configure(settings);
        changingProfile = true;
        ServerSelector.ItemsSource = settings.SavedServers.ToArray();
        ServerSelector.SelectedItem = settings.SavedServers.FirstOrDefault(p => p.IpAddress == settings.IpAddress && p.QueryPort == settings.QueryPort && p.Name == settings.ServerName);
        changingProfile = false;
        Chart.Use24HourClock = settings.Use24HourClock;
        Chart.InvalidateVisual();
        UpdateRefreshButton();
    }

    private async void ServerSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (changingProfile || ServerSelector.SelectedItem is not ServerProfile profile || closing) return;
        configuring = true; ServerSelector.IsEnabled = false; RefreshButton.IsEnabled = false;
        await pollTask;
        if (closing) return;
        var next = settings.Copy(); profile.ApplyTo(next);
        try { next.Validate(); JsonStorage.Write(App.ConfigPath, next); }
        catch (Exception ex) when (ex is ArgumentException or IOException or UnauthorizedAccessException)
        {
            ReportStorageError(L.Text("ServerChangeError", L.Error(ex)));
            configuring = false; ServerSelector.IsEnabled = true; ApplyAppearance(); return;
        }
        settings = next; schedule = new(settings.RestartTimes); tracker = new(); lastAttempt = null; Chart.Clear();
        configuring = false; ServerSelector.IsEnabled = true;
        ApplyAppearance(); Tick();
    }

    private void SaveRuntime()
    {
        runtime.Alerts = ledger.Snapshot();
        try { JsonStorage.Write(RuntimePath, runtime); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { ReportStorageError(L.Text("LedgerError", L.Error(ex))); }
    }
    private void ReportStorageError(string message) { StorageErrorText.Text = message; StorageErrorText.Visibility = Visibility.Visible; }
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
        settings.Maximized = fullscreen ? normalState == WindowState.Maximized : WindowState == WindowState.Maximized;
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
        CaptureBounds();
        next.Left = settings.Left; next.Top = settings.Top; next.Width = settings.Width; next.Height = settings.Height;
        next.Maximized = settings.Maximized; next.Fullscreen = fullscreen;
        try
        {
            if (next.StartWithWindows != settings.StartWithWindows) DesktopServices.SetStartup(next.StartWithWindows);
            JsonStorage.Write(App.ConfigPath, next);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException)
        { MessageBox.Show(this, L.Text("SaveError", L.Error(ex)), "MAG DEER", MessageBoxButton.OK, MessageBoxImage.Warning); configuring = false; UpdateRefreshButton(); return; }
        settings = next;
        ConfigureClock();
        schedule = new(settings.RestartTimes);
        history = new(Path.Combine(App.DataRoot, "data", "history"), settings.HistoryRetentionDays);
        if (endpointChanged) { tracker = new(); lastAttempt = null; Chart.Clear(); }
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
        ApplyAppearance();
        configuring = false;
        Tick();
    }

    private void RefreshClick(object sender, RoutedEventArgs e) { if (!polling && !configuring && !closing) pollTask = PollAsync(manual: true); }
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
        ClockSourceText.Text = ntpSyncing ? L.Text("ClockSynchronizing", settings.NtpServer) : source switch
        {
            ClockSource.Ntp => L.Text("ClockNtp", settings.NtpServer, clock.OffsetFromWindowsMilliseconds.ToString("+0.0;-0.0;0.0", L.Culture)),
            ClockSource.NtpHoldover => L.Text("ClockHoldover", settings.NtpServer),
            ClockSource.WindowsFallback => L.Text("ClockFallback", settings.NtpServer),
            _ => L.Text("ClockWindows")
        };
        ClockSourceText.Foreground = source == ClockSource.Ntp ? Success : source is ClockSource.NtpHoldover or ClockSource.WindowsFallback ? Yellow : Muted;
        ClockSourceText.ToolTip = clock.LastDetail;
        NtpDetailText.Visibility = clock.Enabled && clock.LastSample is not null ? Visibility.Visible : Visibility.Collapsed;
        if (clock.LastSample is { } sample)
            NtpDetailText.Text = L.Text("NtpLastSync", RestartSchedule.CzechTime(sample.UtcAtReceive).ToString("d MMM HH:mm:ss", L.Culture), sample.DelayMilliseconds.ToString("0.0", L.Culture));
        SyncNowButton.Visibility = settings.NtpEnabled ? Visibility.Visible : Visibility.Collapsed;
        SyncNowButton.IsEnabled = !configuring && !ntpSyncing && !ntpDenied &&
            (lastNtpAttempt is null || Stopwatch.GetElapsedTime(lastNtpAttempt.Value).TotalSeconds >= Math.Max(64, clock.LastFailure == NtpFailure.RateLimited ? ntpRetrySeconds : 64));
    }
    private void SyncNowClick(object sender, RoutedEventArgs e)
    {
        if (SyncNowButton.IsEnabled && settings.NtpEnabled) ntpTask = SynchronizeNtpAsync();
    }
    private void OnPowerModeChanged(object sender, Microsoft.Win32.PowerModeChangedEventArgs e)
    {
        if (e.Mode != Microsoft.Win32.PowerModes.Resume || closing) return;
        _ = Dispatcher.InvokeAsync(() => { if (!closing) { clock.Invalidate(); lastNtpAttempt = null; lastAttempt = null; Tick(); } });
    }
    private void CompactClick(object sender, RoutedEventArgs e) { settings.CompactMode = !settings.CompactMode; ApplyAppearance(); SaveSettings(); }
    private void FullscreenClick(object sender, RoutedEventArgs e) { if (fullscreen) ExitFullscreen(); else EnterFullscreen(); SaveSettings(); }
    private void EnterFullscreen()
    {
        if (fullscreen) return;
        normalBounds = WindowState == WindowState.Normal ? new Rect(Left, Top, Width, Height) : RestoreBounds;
        normalState = WindowState;
        fullscreen = true;
        DesktopServices.Fullscreen(this);
    }
    private void ExitFullscreen()
    {
        if (!fullscreen) return;
        fullscreen = false; ResizeMode = ResizeMode.CanResize;
        Left = normalBounds.Left; Top = normalBounds.Top; Width = normalBounds.Width; Height = normalBounds.Height;
        WindowState = normalState;
    }
    private void OnKeyDown(object sender, KeyEventArgs e)
    {
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
            // Flush the snapshot away from the UI thread. Network cleanup and saving run together.
            var save = Task.Run(() => JsonStorage.Write(App.ConfigPath, snapshot));
            bool finished = await ShutdownDrain.WaitAsync([pollTask, ntpTask, save], TimeSpan.FromMilliseconds(1200));
            if (!finished || save.IsFaulted)
                Debug.WriteLine("MAG DEER shutdown: background cleanup/save exceeded budget or failed.");
        }
        catch (Exception ex) { Debug.WriteLine("MAG DEER shutdown: " + ex); }
        finally
        {
            // Dispose only after any canceled consumers have finished using the token.
            _ = Task.WhenAll(pollTask, ntpTask).ContinueWith(t => { _ = t.Exception; stop.Dispose(); }, TaskScheduler.Default);
            allowClose = true;
            Close();
        }
    }
}
