using System.Globalization;
using System.Windows.Interop;

namespace MagDeer.App;

public partial class SettingsWindow : Window
{
    public MonitorSettings? Result { get; private set; }
    public bool ConfirmOffline => OfflineBox.IsChecked == true;
    private readonly MonitorSettings original;
    private readonly List<ServerProfile> profiles;
    private readonly List<WorldClock> clocks;
    private readonly List<ZoneChoice> zones = [];
    private bool loadingProfile;
    private sealed record DisplayOption(string Device, string Label)
    {
        public override string ToString() => Label;
    }
    private sealed record ZoneChoice(string Id, string Label)
    {
        public override string ToString() => Label;
    }

    public SettingsWindow(MonitorSettings settings, bool canConfirmOffline, bool isOffline)
    {
        original = settings;
        profiles = settings.Copy().SavedServers;
        clocks = settings.WorldClocks.Select(x => x.Copy()).ToList();
        if (profiles.Count == 0) profiles.Add(ServerProfile.From(settings));
        InitializeComponent();
        SourceInitialized += (_, _) => DesktopServices.TitleBarTheme(this, ThemeManager.IsDark(settings.Theme));
        NameBox.Text = settings.ServerName; IpBox.Text = settings.IpAddress;
        GamePortBox.Text = settings.GamePort.ToString(); QueryPortBox.Text = settings.QueryPort.ToString();
        ScheduleBox.Text = string.Join(", ", settings.RestartTimes);
        ConfirmedBox.IsChecked = settings.ScheduleConfirmed;
        NormalBox.Text = settings.NormalIntervalSeconds.ToString(); BeforeBox.Text = settings.BeforeRestartIntervalSeconds.ToString();
        DuringBox.Text = settings.RestartIntervalSeconds.ToString(); TimeoutBox.Text = settings.QueryTimeoutSeconds.ToString();
        FailuresBox.Text = settings.FailureThreshold.ToString(); RetentionBox.Text = settings.HistoryRetentionDays.ToString();
        EveryHoursBox.Text = settings.RestartEveryHours.ToString(); AnchorBox.Text = settings.RestartAnchor;
        LogoutMinutesBox.Text = settings.LogoutWarningMinutes.ToString(); KickMinutesBox.Text = settings.KickWarningMinutes.ToString();
        OffsetBox.Text = settings.ServerOffsetHours.ToString(CultureInfo.CurrentCulture);
        ServerClockBox.IsChecked = settings.ShowServerTime; Clock24Box.IsChecked = settings.Use24HourClock;
        TopBox.IsChecked = settings.AlwaysOnTop; CompactBox.IsChecked = settings.CompactMode;
        VisualBox.IsChecked = settings.VisualAlerts; SoundBox.IsChecked = settings.SoundAlerts;
        NotifyBox.IsChecked = settings.WindowsNotifications; StartupBox.IsChecked = settings.StartWithWindows;
        ThemeBox.ItemsSource = ThemeManager.Choices;
        ThemeBox.SelectedItem = ThemeManager.Choices.FirstOrDefault(x => x.Id == settings.Theme) ?? ThemeManager.Choices[0];
        LanguageBox.ItemsSource = L.Languages;
        LanguageBox.SelectedItem = L.Languages.First(x => x.Id == settings.Language);
        NtpEnabledBox.IsChecked = settings.NtpEnabled;
        NtpServerBox.Text = settings.NtpServer;
        NtpIntervalBox.Text = settings.NtpSyncIntervalMinutes.ToString();
        NtpTimeoutBox.Text = settings.NtpTimeoutSeconds.ToString();
        NtpPresetBox.ItemsSource = new[] { "time.cloudflare.com", "pool.ntp.org" };
        OfflineBox.IsEnabled = canConfirmOffline; OfflineBox.IsChecked = isOffline;
        var screens = DesktopServices.Screens;
        MonitorBox.ItemsSource = screens.Select((s, i) => new DisplayOption(s.DeviceName,
            $"{i + 1}. {s.DeviceName} · {s.Bounds.Width} × {s.Bounds.Height}" + (s.Primary ? " · " + L.Text("Primary") : ""))).ToArray();
        MonitorBox.SelectedIndex = Math.Max(0, Array.FindIndex(screens, s => s.DeviceName == settings.MonitorDevice));
        DataPathText.Text = App.DataRoot;
        ScheduleBox.TextChanged += (_, _) => ConfirmedBox.IsChecked = false;
        IpBox.TextChanged += (_, _) => { ConfirmedBox.IsChecked = false; OfflineBox.IsChecked = false; };
        QueryPortBox.TextChanged += (_, _) => { ConfirmedBox.IsChecked = false; OfflineBox.IsChecked = false; };
        foreach (var zone in TimeZoneInfo.GetSystemTimeZones().Concat(WorldClock.Presets.Select(x => x.ResolveZone())).GroupBy(x => x.Id).Select(x => x.First()).OrderBy(x => x.BaseUtcOffset).ThenBy(x => x.Id))
            AddZone(zone);
        TimeZoneBox.ItemsSource = zones;
        ClockPresetBox.ItemsSource = WorldClock.Presets;
        ClockPresetBox.SelectedIndex = 1;
        RefreshClocks();
        RefreshProfiles();
    }

    private static int Number(TextBox box, string name) => int.TryParse(box.Text.Trim(), out var value) ? value : throw new ArgumentException(L.Text("IntegerError", L.Text(name)));
    private MonitorSettings ReadForm()
    {
            var s = original.Copy();
            s.SavedServers = [];
            s.ServerName = NameBox.Text.Trim(); s.IpAddress = IpBox.Text.Trim();
            s.GamePort = Number(GamePortBox, "GamePort"); s.QueryPort = Number(QueryPortBox, "QueryPort");
            s.RestartTimes = ScheduleBox.Text.Split([',', ';', '\r', '\n'], StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).Order().ToArray();
            s.ScheduleConfirmed = ConfirmedBox.IsChecked == true;
            s.NormalIntervalSeconds = Number(NormalBox, "NormalMode"); s.BeforeRestartIntervalSeconds = Number(BeforeBox, "BeforeInterval");
            s.RestartIntervalSeconds = Number(DuringBox, "DuringInterval"); s.QueryTimeoutSeconds = Number(TimeoutBox, "Timeout");
            s.FailureThreshold = Number(FailuresBox, "FailureThreshold"); s.HistoryRetentionDays = Number(RetentionBox, "Retention");
            s.RestartEveryHours = Number(EveryHoursBox, "EveryHours"); s.RestartAnchor = AnchorBox.Text.Trim();
            s.LogoutWarningMinutes = Number(LogoutMinutesBox, "LogoutMinutes"); s.KickWarningMinutes = Number(KickMinutesBox, "KickMinutes");
            if (!double.TryParse(OffsetBox.Text.Trim().Replace(',', '.'), NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var offset))
                throw new ArgumentException(L.Text("OffsetError"));
            s.ServerOffsetHours = offset; s.ShowServerTime = ServerClockBox.IsChecked == true; s.Use24HourClock = Clock24Box.IsChecked == true;
            s.AlwaysOnTop = TopBox.IsChecked == true; s.CompactMode = CompactBox.IsChecked == true;
            s.VisualAlerts = VisualBox.IsChecked == true; s.SoundAlerts = SoundBox.IsChecked == true;
            s.WindowsNotifications = NotifyBox.IsChecked == true; s.StartWithWindows = StartupBox.IsChecked == true;
            s.MonitorDevice = (MonitorBox.SelectedItem as DisplayOption)?.Device ?? "";
            s.Theme = (ThemeBox.SelectedItem as ThemeManager.ThemeChoice)?.Id ?? "Light";
            s.Language = (LanguageBox.SelectedItem as L.LanguageChoice)?.Id ?? "en";
            s.NtpEnabled = NtpEnabledBox.IsChecked == true;
            s.NtpServer = NtpServerBox.Text.Trim();
            s.NtpSyncIntervalMinutes = Number(NtpIntervalBox, "NtpInterval");
            s.NtpTimeoutSeconds = Number(NtpTimeoutBox, "NtpTimeout");
            s.WorldClocks = clocks.Select(x => x.Copy()).ToList();
            s.Validate();
            return s;
    }
    private void SaveClick(object sender, RoutedEventArgs e)
    {
        try
        {
            var s = ReadForm();
            UpsertProfile(ServerProfile.From(s));
            s.SavedServers = profiles;
            s.Validate(); Result = s; DialogResult = true;
        }
        catch (ArgumentException ex) { ErrorText.Text = L.Error(ex); ErrorText.Visibility = Visibility.Visible; }
    }
    private void RefreshProfiles()
    {
        loadingProfile = true;
        ProfilesBox.ItemsSource = profiles.ToArray();
        ProfilesBox.SelectedItem = profiles.FirstOrDefault(p => p.Name == NameBox.Text.Trim() && p.IpAddress == IpBox.Text.Trim());
        loadingProfile = false;
    }
    private void UpsertProfile(ServerProfile profile)
    {
        int index = profiles.FindIndex(p => p.Name.Equals(profile.Name, StringComparison.OrdinalIgnoreCase));
        if (index >= 0) profiles[index] = profile;
        else { if (profiles.Count >= 50) throw new ArgumentException(L.Text("ServerLimit")); profiles.Add(profile); }
    }
    private void ProfileSelected(object sender, SelectionChangedEventArgs e)
    {
        if (!loadingProfile) LoadProfileClick(sender, e);
    }
    private void LoadProfileClick(object sender, RoutedEventArgs e)
    {
        if (ProfilesBox.SelectedItem is not ServerProfile p) return;
        NameBox.Text = p.Name; IpBox.Text = p.IpAddress; GamePortBox.Text = p.GamePort.ToString(); QueryPortBox.Text = p.QueryPort.ToString();
        ScheduleBox.Text = string.Join(", ", p.RestartTimes); EveryHoursBox.Text = p.RestartEveryHours.ToString(); AnchorBox.Text = p.RestartAnchor;
        OffsetBox.Text = p.ServerOffsetHours.ToString(CultureInfo.CurrentCulture);
        LogoutMinutesBox.Text = p.LogoutWarningMinutes.ToString(); KickMinutesBox.Text = p.KickWarningMinutes.ToString();
        ConfirmedBox.IsChecked = p.ScheduleConfirmed;
        ErrorText.Visibility = Visibility.Collapsed;
    }
    private void NewProfileClick(object sender, RoutedEventArgs e)
    {
        loadingProfile = true; ProfilesBox.SelectedIndex = -1; loadingProfile = false;
        NameBox.Text = ""; IpBox.Text = ""; GamePortBox.Text = "2302"; QueryPortBox.Text = "2303";
        ConfirmedBox.IsChecked = false; OfflineBox.IsChecked = false; NameBox.Focus();
    }
    private void StoreProfileClick(object sender, RoutedEventArgs e)
    {
        try
        {
            UpsertProfile(ServerProfile.From(ReadForm())); RefreshProfiles();
            ErrorText.Text = L.Text("ServerStored");
            ErrorText.Visibility = Visibility.Visible;
        }
        catch (ArgumentException ex) { ErrorText.Text = L.Error(ex); ErrorText.Visibility = Visibility.Visible; }
    }
    private void GenerateClick(object sender, RoutedEventArgs e)
    {
        try
        {
            ScheduleBox.Text = string.Join(", ", RestartSchedule.GenerateDaily(Number(EveryHoursBox, "EveryHours"), AnchorBox.Text.Trim()));
            ConfirmedBox.IsChecked = false; ErrorText.Visibility = Visibility.Collapsed;
        }
        catch (Exception ex) when (ex is ArgumentException or FormatException)
        { ErrorText.Text = ex is FormatException ? L.Text("InvalidAnchor") : L.Error(ex); ErrorText.Visibility = Visibility.Visible; }
    }
    private void MinutesKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (sender is not TextBox box || e.Key is not (System.Windows.Input.Key.Up or System.Windows.Input.Key.Down)) return;
        if (!int.TryParse(box.Text, out var value)) value = 1;
        box.Text = Math.Clamp(value + (e.Key == System.Windows.Input.Key.Up ? 1 : -1), 1, 120).ToString();
        e.Handled = true;
    }
    private void OpenDataClick(object sender, RoutedEventArgs e)
    {
        try { DesktopServices.OpenFolder(App.DataRoot); }
        catch (System.ComponentModel.Win32Exception ex) { ErrorText.Text = ex.Message; ErrorText.Visibility = Visibility.Visible; }
    }

    private void AddZone(TimeZoneInfo zone)
    {
        if (zones.Any(x => x.Id == zone.Id)) return;
        double hours = zone.BaseUtcOffset.TotalHours;
        string offset = (hours < 0 ? "−" : "+") + zone.BaseUtcOffset.Duration().ToString(@"hh\:mm");
        zones.Add(new(zone.Id, $"UTC{offset} · {zone.Id}"));
    }
    private void NtpPresetSelected(object sender, SelectionChangedEventArgs e)
    { if (NtpPresetBox.SelectedItem is string host) NtpServerBox.Text = host; }
    private void RefreshClocks(int selected = -1)
    {
        ClockList.ItemsSource = clocks.ToArray(); ClockList.SelectedIndex = selected;
    }
    private void SetClockEditor(WorldClock clock)
    {
        CityBox.Text = clock.City; CountryBox.Text = clock.CountryCode; ClockEnabledBox.IsChecked = clock.Enabled;
        AddZone(clock.ResolveZone());
        TimeZoneBox.ItemsSource = null; TimeZoneBox.ItemsSource = zones;
        TimeZoneBox.SelectedItem = zones.First(x => x.Id == clock.TimeZoneId);
    }
    private void ClockSelected(object sender, SelectionChangedEventArgs e)
    { if (ClockList.SelectedItem is WorldClock clock) SetClockEditor(clock); }
    private void ClockPresetSelected(object sender, SelectionChangedEventArgs e)
    { if (ClockPresetBox.SelectedItem is WorldClock clock) SetClockEditor(clock.Copy()); }
    private WorldClock ReadClock()
    {
        var clock = new WorldClock { City = CityBox.Text.Trim(), CountryCode = CountryBox.Text.Trim().ToUpperInvariant(),
            TimeZoneId = (TimeZoneBox.SelectedItem as ZoneChoice)?.Id ?? "", Enabled = ClockEnabledBox.IsChecked == true };
        clock.Validate(); return clock;
    }
    private void ClockError(Exception ex) { ErrorText.Text = L.Error(ex); ErrorText.Visibility = Visibility.Visible; }
    private void AddClockClick(object sender, RoutedEventArgs e)
    {
        try { if (clocks.Count >= 8) throw new ArgumentException("WorldClocksLimit"); clocks.Add(ReadClock()); RefreshClocks(clocks.Count - 1); ErrorText.Visibility = Visibility.Collapsed; }
        catch (ArgumentException ex) { ClockError(ex); }
    }
    private void UpdateClockClick(object sender, RoutedEventArgs e)
    {
        try
        {
            int index = ClockList.SelectedIndex; if (index < 0) throw new ArgumentException("SelectClock");
            clocks[index] = ReadClock(); RefreshClocks(index); ErrorText.Visibility = Visibility.Collapsed;
        }
        catch (ArgumentException ex) { ClockError(ex); }
    }
    private void RemoveClockClick(object sender, RoutedEventArgs e)
    { int index = ClockList.SelectedIndex; if (index >= 0) { clocks.RemoveAt(index); RefreshClocks(Math.Min(index, clocks.Count - 1)); } }
    private void MoveClock(int direction)
    {
        int index = ClockList.SelectedIndex, destination = index + direction;
        if (index < 0 || destination < 0 || destination >= clocks.Count) return;
        (clocks[index], clocks[destination]) = (clocks[destination], clocks[index]); RefreshClocks(destination);
    }
    private void ClockUpClick(object sender, RoutedEventArgs e) => MoveClock(-1);
    private void ClockDownClick(object sender, RoutedEventArgs e) => MoveClock(1);
}
