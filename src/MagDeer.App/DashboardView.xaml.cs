namespace MagDeer.App;

/// <summary>The real dashboard can also be rendered without a Window, network or timers.</summary>
public partial class DashboardView : UserControl
{
    public event SelectionChangedEventHandler? ServerSelected;
    public event RoutedEventHandler? ClockSyncRequested;
    public DashboardPanel LayoutPanel => Surface;
    private bool use24HourClock = true;
    public DashboardView()
    {
        InitializeComponent();
        Surface.SlotsChanged += () => ApplyDensity(Surface.Mode);
    }
    public void Configure(MonitorSettings settings)
    {
        Surface.Compact = settings.CompactMode;
        Surface.HasWorldClocks = settings.WorldClocks.Any(x => x.Enabled);
        FontFamily = new FontFamily(settings.Theme == "Matrix" ? "Consolas" : "Segoe UI");
        SetClockFormat(settings.Use24HourClock);
    }
    public void SetClockFormat(bool use24Hours)
    {
        if (use24HourClock == use24Hours) return;
        use24HourClock = use24Hours;
        if (Surface.Slots.Count > 0) ApplyDensity(Surface.Mode);
    }
    private void ApplyDensity(DashboardMode mode)
    {
        bool tiny = mode == DashboardMode.Ribbon;
        double padding = tiny ? 4 : 12;
        foreach (var border in Surface.Children.OfType<Border>()) border.Padding = new Thickness(padding);
        Size Inner(string role) => Surface.Slots.TryGetValue(role, out var rect)
            ? new(Math.Max(1, rect.Width - padding * 2 - 2), Math.Max(1, rect.Height - padding * 2 - 2)) : new(1, 1);

        var clock = Inner("Clock");
        bool details = !tiny && clock.Width >= 160 && clock.Height >= 90;
        bool verticalClock = details && (clock.Height >= 310 || clock.Width < 420 && clock.Height >= 190);
        ClockDetails.Visibility = details ? Visibility.Visible : Visibility.Collapsed;
        ClockHeading.Visibility = tiny || clock.Width < 180 ? Visibility.Collapsed : Visibility.Visible;
        DateText.Visibility = tiny ? Visibility.Collapsed : Visibility.Visible;
        bool clockExtras = verticalClock ? clock.Height >= 295 : clock.Height >= 145;
        bool clockSource = clockExtras && (verticalClock ? clock.Height >= 335 : clock.Height >= 180);
        ClockExtras.Visibility = clockExtras ? Visibility.Visible : Visibility.Collapsed;
        ClockSourcePanel.Visibility = clockSource ? Visibility.Visible : Visibility.Collapsed;
        ClockDesign.Width = clock.Width;
        ClockDetails.Width = verticalClock ? Math.Min(400, clock.Width) : clock.Width * .44;
        Grid.SetColumn(ClockDetails, verticalClock ? 0 : 1); Grid.SetRow(ClockDetails, verticalClock ? 1 : 0);
        Grid.SetColumnSpan(ClockDetails, verticalClock ? 2 : 1);
        Grid.SetColumnSpan(ClockMain, verticalClock || !details ? 2 : 1);
        ClockDetails.HorizontalAlignment = HorizontalAlignment.Center;
        ClockDetails.Margin = new Thickness(verticalClock ? 0 : 14, verticalClock ? 12 : 0, 0, 0);
        double clockWidth = verticalClock || !details ? clock.Width : clock.Width - ClockDetails.Width - 14;
        double detailHeight = verticalClock ? 38 + (clockExtras ? 75 : 0) + (clockSource ? 40 : 0) : 0;
        double clockHeight = Math.Max(20, clock.Height - (tiny ? 0 : 48 + detailHeight));
        ClockText.FontSize = Math.Clamp(Math.Min(clockWidth / (use24HourClock ? 2.6 : 4.6), clockHeight / 1.12), 14, 170);
        ClockText.LineHeight = ClockText.FontSize * 1.12;

        var restart = Inner("Restart");
        bool sideRestart = !tiny && restart.Width >= 540;
        NextRestartPanel.Visibility = sideRestart ? Visibility.Visible : Visibility.Collapsed;
        NextRestartPanel.Width = Math.Min(155, restart.Width * .27);
        RestartExtras.Visibility = !tiny && restart.Height >= 160 ? Visibility.Visible : Visibility.Collapsed;
        ReminderTimes.Visibility = restart.Height >= 185 ? Visibility.Visible : Visibility.Collapsed;
        CompactNextRestart.Visibility = !tiny && !sideRestart ? Visibility.Visible : Visibility.Collapsed;
        CountdownDesign.Width = restart.Width;
        double countdownWidth = sideRestart ? restart.Width - NextRestartPanel.Width - 14 : restart.Width;
        double countdownHeight = restart.Height - (RestartExtras.Visibility == Visibility.Visible
            ? ReminderTimes.Visibility == Visibility.Visible ? 120 : 70 : sideRestart || tiny ? 30 : 52);
        CountdownText.FontSize = Math.Clamp(Math.Min(countdownWidth / 4.9, Math.Max(14, countdownHeight) / 1.2), 14, 96);

        var status = Inner("Status");
        StatusDesign.Width = tiny ? 100 : status.Width;
        StatusHint.Visibility = !tiny && status.Height >= 115 ? Visibility.Visible : Visibility.Collapsed;
        StatusLabel.Visibility = !tiny && status.Height >= 75 ? Visibility.Visible : Visibility.Collapsed;
        StatusText.FontSize = Math.Clamp(status.Height * .30, 18, 24);
        StatusText.MaxWidth = tiny ? 80 : Math.Max(1, status.Width - 16);
        StatusText.TextTrimming = TextTrimming.CharacterEllipsis;
        StatusText.TextWrapping = tiny ? TextWrapping.NoWrap : TextWrapping.Wrap;

        var rtt = Inner("Rtt");
        RttDesign.Width = tiny ? 90 : rtt.Width;
        RttText.FontSize = Math.Clamp(rtt.Height * .43, 20, 40);
        RttLabel.Text = tiny ? "UDP RTT" : "UDP QUERY RTT";
        RttHint.Visibility = !tiny && rtt.Height >= 90 ? Visibility.Visible : Visibility.Collapsed;
        PlayerCounts.Width = Inner("Players").Width;
        PlayerCounts.Condensed = tiny || Inner("Players").Height < 100;
        PlayerCounts.ShowTitle = !tiny && Inner("Players").Height >= 85;
        PlayerCounts.FitTo(Inner("Players"));

        var info = Inner("Details");
        bool stackedTimes = info.Width < 380;
        Grid.SetColumn(PollText, stackedTimes ? 0 : 1); Grid.SetRow(PollText, stackedTimes ? 1 : 0);
        Grid.SetColumnSpan(LastUpdateText, stackedTimes ? 2 : 1); Grid.SetColumnSpan(PollText, stackedTimes ? 2 : 1);
        PollText.Visibility = !stackedTimes || info.Height >= 42 ? Visibility.Visible : Visibility.Collapsed;
        DetailText.Visibility = info.Height >= 100 ? Visibility.Visible : Visibility.Collapsed;
        EndpointText.Visibility = info.Height >= 64 ? Visibility.Visible : Visibility.Collapsed;
        ServerNameText.Visibility = info.Height >= (stackedTimes ? 68 : 48) ? Visibility.Visible : Visibility.Collapsed;
        if (Surface.Slots.TryGetValue("Server", out var server)) ServerDesign.Width = Math.Max(1, server.Width);
        ServerLabel.Visibility = ServerDesign.Width >= 560 ? Visibility.Visible : Visibility.Collapsed;
        WorldClocksPanel.Width = Math.Max(182, Inner("World").Width);
    }
    private void ServerSelectionChanged(object sender, SelectionChangedEventArgs e) => ServerSelected?.Invoke(sender, e);
    private void SyncNowClick(object sender, RoutedEventArgs e) => ClockSyncRequested?.Invoke(sender, e);
}
