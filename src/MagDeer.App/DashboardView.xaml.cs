namespace MagDeer.App;

/// <summary>The real dashboard can also be rendered without a Window, network or timers.</summary>
public partial class DashboardView : UserControl
{
    public event SelectionChangedEventHandler? ServerSelected;
    public event RoutedEventHandler? ClockSyncRequested;
    public DashboardPanel LayoutPanel => Surface;
    public DashboardView()
    {
        InitializeComponent();
        Surface.ModeChanged += ApplyDensity;
    }
    public void Configure(MonitorSettings settings)
    {
        Surface.Compact = settings.CompactMode;
        Surface.HasWorldClocks = settings.WorldClocks.Any(x => x.Enabled);
        FontFamily = new FontFamily(settings.Theme == "Matrix" ? "Consolas" : "Segoe UI");
    }
    private void ApplyDensity(DashboardMode mode)
    {
        bool tiny = mode == DashboardMode.Ribbon;
        bool portrait = mode == DashboardMode.Portrait;
        bool bar = mode == DashboardMode.Bar;
        bool mini = mode == DashboardMode.Mini;
        bool verticalClock = portrait || bar || mini;
        foreach (var border in Surface.Children.OfType<Border>()) border.Padding = new Thickness(tiny ? 4 : 12);
        ClockDetails.Visibility = tiny ? Visibility.Collapsed : Visibility.Visible;
        ClockExtras.Visibility = PlanBadge.Visibility = tiny ? Visibility.Collapsed : Visibility.Visible;
        NextRestartPanel.Visibility = tiny || verticalClock ? Visibility.Collapsed : Visibility.Visible;
        RestartExtras.Visibility = tiny ? Visibility.Collapsed : Visibility.Visible;
        CompactNextRestart.Visibility = verticalClock ? Visibility.Visible : Visibility.Collapsed;
        ClockDesign.Width = tiny || verticalClock ? 320 : 640;
        Grid.SetColumn(ClockDetails, verticalClock ? 0 : 1); Grid.SetRow(ClockDetails, verticalClock ? 1 : 0);
        ClockDetails.Margin = new Thickness(verticalClock ? 0 : 14, verticalClock ? 10 : 0, 0, 0);
        CountdownDesign.Width = tiny || verticalClock ? 350 : 600;
        InfoDesign.Width = verticalClock ? 400 : 650;
        StatusDesign.Width = tiny ? 100 : verticalClock ? 200 : 260;
        StatusHint.Visibility = tiny ? Visibility.Collapsed : Visibility.Visible;
        StatusLabel.Visibility = tiny ? Visibility.Collapsed : Visibility.Visible;
        StatusText.MaxWidth = tiny ? 80 : double.PositiveInfinity;
        StatusText.TextTrimming = TextTrimming.CharacterEllipsis;
        StatusText.TextWrapping = TextWrapping.NoWrap;
        RttDesign.Width = tiny ? 90 : 180;
        RttText.FontSize = tiny ? 26 : 34;
        RttLabel.Text = tiny ? "UDP RTT" : "UDP QUERY RTT";
        RttHint.Visibility = tiny ? Visibility.Collapsed : Visibility.Visible;
        PlayerCounts.Width = tiny ? 140 : 190;
        PlayerCounts.Condensed = tiny;
        CountdownText.FontSize = tiny ? 66 : 76;
    }
    private void ServerSelectionChanged(object sender, SelectionChangedEventArgs e) => ServerSelected?.Invoke(sender, e);
    private void SyncNowClick(object sender, RoutedEventArgs e) => ClockSyncRequested?.Invoke(sender, e);
}
