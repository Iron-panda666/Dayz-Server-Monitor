using System.Globalization;
using System.Windows.Interop;

namespace MagDeer.App;

public partial class SettingsWindow : Window
{
    public MonitorSettings? Result { get; private set; }
    public bool ConfirmOffline => Editor.OfflineBox.IsChecked == true;
    private readonly MonitorSettings original;
    private readonly List<ServerProfile> profiles;
    private readonly List<WorldClock> clocks;
    private readonly List<ZoneChoice> zones = [];
    private bool loadingProfile;
    private ServerProfile? editedProfile;
    private sealed record DisplayOption(string Device, string Label)
    {
        public override string ToString() => Label;
    }
    private sealed record ZoneChoice(string Id, string Label)
    {
        public override string ToString() => Label;
    }
    private sealed record CloseOption(string Id, string Label);

    public SettingsWindow(MonitorSettings settings, bool canConfirmOffline, bool isOffline, Func<DeckSnapshot[]>? snapshots = null)
    {
        original = settings;
        profiles = settings.Copy().SavedServers;
        clocks = settings.WorldClocks.Select(x => x.Copy()).ToList();
        if (profiles.Count == 0) profiles.Add(ServerProfile.From(settings));
        InitializeComponent();
        Editor.ActionRequested += DispatchAction;
        SourceInitialized += (_, _) => DesktopServices.TitleBarTheme(this, ThemeManager.IsDark(settings.Theme));
        Loaded += (_, _) => DesktopServices.PlaceSettingsWindow(this, Owner);
        Editor.NameBox.Text = settings.ServerName; Editor.IpBox.Text = settings.IpAddress;
        Editor.GamePortBox.Text = settings.GamePort.ToString(); Editor.QueryPortBox.Text = settings.QueryPort.ToString();
        Editor.ScheduleBox.Text = string.Join(", ", settings.RestartTimes);
        Editor.ConfirmedBox.IsChecked = settings.ScheduleConfirmed;
        Editor.NormalBox.Text = settings.NormalIntervalSeconds.ToString(); Editor.BeforeBox.Text = settings.BeforeRestartIntervalSeconds.ToString();
        Editor.DuringBox.Text = settings.RestartIntervalSeconds.ToString(); Editor.TimeoutBox.Text = settings.QueryTimeoutSeconds.ToString();
        Editor.FailuresBox.Text = settings.FailureThreshold.ToString(); Editor.RetentionBox.Text = settings.HistoryRetentionDays.ToString();
        Editor.EveryHoursBox.Text = settings.RestartEveryHours.ToString(); Editor.AnchorBox.Text = settings.RestartAnchor;
        Editor.LogoutMinutesBox.Text = settings.LogoutWarningMinutes.ToString(); Editor.KickMinutesBox.Text = settings.KickWarningMinutes.ToString();
        Editor.OffsetBox.Text = settings.ServerOffsetHours.ToString(CultureInfo.CurrentCulture);
        Editor.ServerClockBox.IsChecked = settings.ShowServerTime; Editor.Clock24Box.IsChecked = settings.Use24HourClock;
        Editor.GameSessionBox.IsChecked = settings.ShowGameSession;
        Editor.ExportBox.IsChecked = settings.ExportEnabled; Editor.ExportAddressBox.Text = settings.ExportAddress; Editor.ExportPortBox.Text = settings.ExportPort.ToString();
        Editor.ServicesBox.IsChecked = settings.MonitorOnlineServices;
        Editor.BackgroundBox.IsChecked = settings.SavedServers.FirstOrDefault(p => p.Name == settings.ServerName && p.IpAddress == settings.IpAddress && p.QueryPort == settings.QueryPort)?.MonitorInBackground == true;
        Editor.TopBox.IsChecked = settings.AlwaysOnTop; Editor.CompactBox.IsChecked = settings.CompactMode;
        Editor.VisualBox.IsChecked = settings.VisualAlerts; Editor.SoundBox.IsChecked = settings.SoundAlerts;
        Editor.NotifyBox.IsChecked = settings.WindowsNotifications; Editor.StartupBox.IsChecked = settings.StartWithWindows;
        Editor.ThemeBox.ItemsSource = ThemeManager.Choices;
        Editor.ThemeBox.SelectedItem = ThemeManager.Choices.FirstOrDefault(x => x.Id == settings.Theme) ?? ThemeManager.Choices[0];
        Editor.DisplayProfileBox.ItemsSource = DisplayProfiles.All;
        Editor.DisplayProfileBox.SelectedItem = DisplayProfiles.All.First(x => x.Id == settings.DisplayProfile);
        Editor.MinimizeCloseBox.IsChecked = settings.MinimizeOnClose;
        Editor.MinimizeTargetBox.ItemsSource = new[] { new CloseOption("Tray", L.Text("TargetTray")), new CloseOption("Taskbar", L.Text("TargetTaskbar")) };
        Editor.MinimizeTargetBox.SelectedIndex = settings.MinimizeTarget == "Taskbar" ? 1 : 0;
        Editor.LanguageBox.ItemsSource = L.Languages;
        Editor.LanguageBox.SelectedItem = L.Languages.First(x => x.Id == settings.Language);
        Editor.NtpEnabledBox.IsChecked = settings.NtpEnabled;
        Editor.NtpServerBox.Text = settings.NtpServer;
        Editor.NtpIntervalBox.Text = settings.NtpSyncIntervalMinutes.ToString();
        Editor.NtpTimeoutBox.Text = settings.NtpTimeoutSeconds.ToString();
        Editor.NtpPresetBox.ItemsSource = new[] { "time.cloudflare.com", "pool.ntp.org" };
        Editor.OfflineBox.IsEnabled = canConfirmOffline; Editor.OfflineBox.IsChecked = isOffline;
        var screens = DesktopServices.Screens;
        Editor.MonitorBox.ItemsSource = screens.Select((s, i) => new DisplayOption(s.DeviceName,
            $"{i + 1}. {s.DeviceName} · {s.Bounds.Width} × {s.Bounds.Height}" + (s.Primary ? " · " + L.Text("Primary") : ""))).ToArray();
        Editor.MonitorBox.SelectedIndex = Math.Max(0, Array.FindIndex(screens, s => s.DeviceName == settings.MonitorDevice));
        Editor.DataPathText.Text = App.DataRoot;
        Editor.ScheduleBox.TextChanged += (_, _) => Editor.ConfirmedBox.IsChecked = false;
        Editor.IpBox.TextChanged += (_, _) => { Editor.ConfirmedBox.IsChecked = false; Editor.OfflineBox.IsChecked = false; };
        Editor.QueryPortBox.TextChanged += (_, _) => { Editor.ConfirmedBox.IsChecked = false; Editor.OfflineBox.IsChecked = false; };
        foreach (var zone in TimeZoneInfo.GetSystemTimeZones().Concat(WorldClock.Presets.Select(x => x.ResolveZone())).GroupBy(x => x.Id).Select(x => x.First()).OrderBy(x => x.BaseUtcOffset).ThenBy(x => x.Id))
            AddZone(zone);
        Editor.TimeZoneBox.ItemsSource = zones;
        Editor.ClockPresetBox.ItemsSource = WorldClock.Presets;
        Editor.ClockPresetBox.SelectedIndex = 1;
        RefreshClocks();
        editedProfile = profiles.FirstOrDefault(p => p.Name == settings.ServerName && p.IpAddress == settings.IpAddress && p.QueryPort == settings.QueryPort);
        RefreshProfiles();
        Editor.WorldClockEditor.IsExpanded = false;
        if (snapshots is not null)
        {
            var statusTimer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
            void UpdateServices()
            {
                var values = snapshots().Where(s => s.Service is not null).Select(s => s.ServerName + ": " + s.Service!.State +
                    " · " + (s.Service.CheckedAt is { } at ? at.ToLocalTime().ToString("HH:mm:ss") : "N/A") + "\n" + s.Service.Description).ToArray();
                Editor.ServicesStatusText.Text = values.Length == 0 ? L.Text("ServiceSaveFirst") : string.Join("\n\n", values);
            }
            statusTimer.Tick += (_, _) => UpdateServices();
            Loaded += (_, _) => { UpdateServices(); statusTimer.Start(); };
            Closed += (_, _) => statusTimer.Stop();
        }
    }

    private void DispatchAction(object? sender, SettingsAction ev)
    {
        switch (ev.Name)
        {
            case "ProfileSelected": ProfileSelected(ev.Sender, (SelectionChangedEventArgs)ev.Arguments); break;
            case "LoadProfileClick": LoadProfileClick(ev.Sender, ev.Arguments); break;
            case "NewProfileClick": NewProfileClick(ev.Sender, ev.Arguments); break;
            case "DeleteProfileClick": DeleteProfileClick(ev.Sender, ev.Arguments); break;
            case "GenerateClick": GenerateClick(ev.Sender, ev.Arguments); break;
            case "MinutesKeyDown": MinutesKeyDown(ev.Sender, (System.Windows.Input.KeyEventArgs)ev.Arguments); break;
            case "NtpPresetSelected": NtpPresetSelected(ev.Sender, (SelectionChangedEventArgs)ev.Arguments); break;
            case "ClockSelected": ClockSelected(ev.Sender, (SelectionChangedEventArgs)ev.Arguments); break;
            case "RemoveClockClick": RemoveClockClick(ev.Sender, ev.Arguments); break;
            case "ClockUpClick": ClockUpClick(ev.Sender, ev.Arguments); break;
            case "ClockDownClick": ClockDownClick(ev.Sender, ev.Arguments); break;
            case "ClockPresetSelected": ClockPresetSelected(ev.Sender, (SelectionChangedEventArgs)ev.Arguments); break;
            case "AddClockClick": AddClockClick(ev.Sender, ev.Arguments); break;
            case "UpdateClockClick": UpdateClockClick(ev.Sender, ev.Arguments); break;
            case "OpenDataClick": OpenDataClick(ev.Sender, ev.Arguments); break;
            case "SaveClick": SaveClick(ev.Sender, ev.Arguments); break;
        }
    }

    private static int Number(TextBox box, string name) => int.TryParse(box.Text.Trim(), out var value) ? value : throw new ArgumentException(L.Text("IntegerError", L.Text(name)));
    private MonitorSettings ReadForm()
    {
            var s = original.Copy();
            s.SavedServers = [];
            ReadProfileForm().ApplyTo(s);
            s.NormalIntervalSeconds = Number(Editor.NormalBox, "NormalMode"); s.BeforeRestartIntervalSeconds = Number(Editor.BeforeBox, "BeforeInterval");
            s.RestartIntervalSeconds = Number(Editor.DuringBox, "DuringInterval"); s.QueryTimeoutSeconds = Number(Editor.TimeoutBox, "Timeout");
            s.FailureThreshold = Number(Editor.FailuresBox, "FailureThreshold"); s.HistoryRetentionDays = Number(Editor.RetentionBox, "Retention");
            s.ShowServerTime = Editor.ServerClockBox.IsChecked == true; s.Use24HourClock = Editor.Clock24Box.IsChecked == true;
            s.ShowGameSession = Editor.GameSessionBox.IsChecked == true;
            s.ExportEnabled = Editor.ExportBox.IsChecked == true;
            if (s.ExportEnabled) { s.ExportAddress = Editor.ExportAddressBox.Text.Trim(); s.ExportPort = Number(Editor.ExportPortBox, "ExportPort"); }
            s.MonitorOnlineServices = Editor.ServicesBox.IsChecked == true;
            s.AlwaysOnTop = Editor.TopBox.IsChecked == true; s.CompactMode = Editor.CompactBox.IsChecked == true;
            s.VisualAlerts = Editor.VisualBox.IsChecked == true; s.SoundAlerts = Editor.SoundBox.IsChecked == true;
            s.WindowsNotifications = Editor.NotifyBox.IsChecked == true; s.StartWithWindows = Editor.StartupBox.IsChecked == true;
            s.MonitorDevice = (Editor.MonitorBox.SelectedItem as DisplayOption)?.Device ?? "";
            s.Theme = (Editor.ThemeBox.SelectedItem as ThemeManager.ThemeChoice)?.Id ?? "Light";
            s.DisplayProfile = (Editor.DisplayProfileBox.SelectedItem as DisplayProfile)?.Id ?? "Auto";
            s.MinimizeOnClose = Editor.MinimizeCloseBox.IsChecked == true;
            s.MinimizeTarget = (Editor.MinimizeTargetBox.SelectedItem as CloseOption)?.Id ?? "Tray";
            s.Language = (Editor.LanguageBox.SelectedItem as L.LanguageChoice)?.Id ?? "en";
            s.NtpEnabled = Editor.NtpEnabledBox.IsChecked == true;
            if (s.NtpEnabled)
            {
                s.NtpServer = Editor.NtpServerBox.Text.Trim();
                s.NtpSyncIntervalMinutes = Number(Editor.NtpIntervalBox, "NtpInterval");
                s.NtpTimeoutSeconds = Number(Editor.NtpTimeoutBox, "NtpTimeout");
            }
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
        catch (ArgumentException ex) { ShowError(ex); }
    }
    private void RefreshProfiles(ServerProfile? selected = null)
    {
        loadingProfile = true;
        Editor.ProfilesBox.ItemsSource = ServerIdentity.Sort(profiles).ToArray();
        Editor.ProfilesBox.SelectedItem = selected ?? editedProfile;
        Editor.DeleteServerButton.IsEnabled = Editor.ProfilesBox.SelectedItem is not null;
        loadingProfile = false;
    }
    private void UpsertProfile(ServerProfile profile)
    {
        profile.MonitorInBackground = Editor.BackgroundBox.IsChecked == true;
        ServerProfileEditor.Save(profiles, editedProfile, profile);
        editedProfile = profile;
    }
    private void ProfileSelected(object sender, SelectionChangedEventArgs e)
    {
        if (loadingProfile || Editor.ProfilesBox.SelectedItem is not ServerProfile selected || ReferenceEquals(selected, editedProfile)) return;
        try
        {
            KeepProfileDraft();
            RefreshProfiles(selected);
            LoadProfileClick(sender, e);
        }
        catch (ArgumentException ex)
        {
            loadingProfile = true; Editor.ProfilesBox.SelectedItem = editedProfile; loadingProfile = false;
            ShowError(ex);
        }
    }
    private ServerProfile ReadProfileForm()
    {
        if (!double.TryParse(Editor.OffsetBox.Text.Trim().Replace(',', '.'), NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var offset))
            throw new ArgumentException(L.Text("OffsetError"));
        return new()
        {
            Name = Editor.NameBox.Text.Trim(), IpAddress = Editor.IpBox.Text.Trim(),
            GamePort = Number(Editor.GamePortBox, "GamePort"), QueryPort = Number(Editor.QueryPortBox, "QueryPort"),
            RestartTimes = Editor.ScheduleBox.Text.Split([',', ';', '\r', '\n'], StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).Order().ToArray(),
            ScheduleConfirmed = Editor.ConfirmedBox.IsChecked == true, RestartEveryHours = Number(Editor.EveryHoursBox, "EveryHours"), RestartAnchor = Editor.AnchorBox.Text.Trim(),
            LogoutWarningMinutes = Number(Editor.LogoutMinutesBox, "LogoutMinutes"), KickWarningMinutes = Number(Editor.KickMinutesBox, "KickMinutes"), ServerOffsetHours = offset
        };
    }
    private void KeepProfileDraft()
    {
        // Switching away from an untouched empty New form simply discards that empty draft.
        if (editedProfile is null && string.IsNullOrWhiteSpace(Editor.NameBox.Text) && string.IsNullOrWhiteSpace(Editor.IpBox.Text)) return;
        UpsertProfile(ReadProfileForm());
    }
    private void ShowError(ArgumentException ex)
    { Editor.ErrorText.Text = L.Error(ex); Editor.ErrorText.SetResourceReference(TextBlock.ForegroundProperty, "DangerBrush"); Editor.ErrorText.Visibility = Visibility.Visible; }
    private void LoadProfileClick(object sender, RoutedEventArgs e)
    {
        if (Editor.ProfilesBox.SelectedItem is not ServerProfile p) return;
        editedProfile = p;
        Editor.BackgroundBox.IsChecked = p.MonitorInBackground;
        Editor.NameBox.Text = p.Name; Editor.IpBox.Text = p.IpAddress; Editor.GamePortBox.Text = p.GamePort.ToString(); Editor.QueryPortBox.Text = p.QueryPort.ToString();
        Editor.ScheduleBox.Text = string.Join(", ", p.RestartTimes); Editor.EveryHoursBox.Text = p.RestartEveryHours.ToString(); Editor.AnchorBox.Text = p.RestartAnchor;
        Editor.OffsetBox.Text = p.ServerOffsetHours.ToString(CultureInfo.CurrentCulture);
        Editor.LogoutMinutesBox.Text = p.LogoutWarningMinutes.ToString(); Editor.KickMinutesBox.Text = p.KickWarningMinutes.ToString();
        Editor.ConfirmedBox.IsChecked = p.ScheduleConfirmed;
        Editor.ErrorText.Visibility = Visibility.Collapsed;
    }
    private void NewProfileClick(object sender, RoutedEventArgs e)
    {
        try { KeepProfileDraft(); RefreshProfiles(); }
        catch (ArgumentException ex) { ShowError(ex); return; }
        loadingProfile = true; Editor.ProfilesBox.SelectedIndex = -1; loadingProfile = false;
        editedProfile = null;
        Editor.DeleteServerButton.IsEnabled = false;
        Editor.BackgroundBox.IsChecked = false;
        Editor.NameBox.Text = ""; Editor.IpBox.Text = ""; Editor.GamePortBox.Text = "2302"; Editor.QueryPortBox.Text = "2303";
        Editor.ConfirmedBox.IsChecked = false; Editor.OfflineBox.IsChecked = false; Editor.NameBox.Focus();
        Editor.ErrorText.Visibility = Visibility.Collapsed;
    }
    private void DeleteProfileClick(object sender, RoutedEventArgs e)
    {
        if (Editor.ProfilesBox.SelectedItem is not ServerProfile selected) return;
        if (profiles.Count <= 1) { ShowError(new ArgumentException("LastServer")); return; }
        if (MessageBox.Show(this, L.Text("DeleteServerConfirm", selected.Name), L.Text("DeleteServer"), MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No) != MessageBoxResult.Yes) return;
        editedProfile = ServerProfileEditor.Remove(profiles, selected);
        RefreshProfiles();
        LoadProfileClick(sender, e);
        Editor.ErrorText.Text = L.Text("ServerDeleted"); Editor.ErrorText.Visibility = Visibility.Visible;
        Editor.ErrorText.SetResourceReference(TextBlock.ForegroundProperty, "SuccessBrush");
    }
    private void GenerateClick(object sender, RoutedEventArgs e)
    {
        try
        {
            Editor.ScheduleBox.Text = string.Join(", ", RestartSchedule.GenerateDaily(Number(Editor.EveryHoursBox, "EveryHours"), Editor.AnchorBox.Text.Trim()));
            Editor.ConfirmedBox.IsChecked = false; Editor.ErrorText.Visibility = Visibility.Collapsed;
        }
        catch (Exception ex) when (ex is ArgumentException or FormatException)
        { Editor.ErrorText.Text = ex is FormatException ? L.Text("InvalidAnchor") : L.Error(ex); Editor.ErrorText.Visibility = Visibility.Visible; }
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
        catch (System.ComponentModel.Win32Exception ex) { Editor.ErrorText.Text = ex.Message; Editor.ErrorText.Visibility = Visibility.Visible; }
    }

    private void AddZone(TimeZoneInfo zone)
    {
        if (zones.Any(x => x.Id == zone.Id)) return;
        double hours = zone.BaseUtcOffset.TotalHours;
        string offset = (hours < 0 ? "−" : "+") + zone.BaseUtcOffset.Duration().ToString(@"hh\:mm");
        zones.Add(new(zone.Id, $"UTC{offset} · {zone.Id}"));
    }
    private void NtpPresetSelected(object sender, SelectionChangedEventArgs e)
    { if (Editor.NtpPresetBox.SelectedItem is string host) Editor.NtpServerBox.Text = host; }
    private void RefreshClocks(int selected = -1)
    {
        Editor.ClockList.ItemsSource = clocks.ToArray(); Editor.ClockList.SelectedIndex = selected;
    }
    private void SetClockEditor(WorldClock clock)
    {
        Editor.WorldClockEditor.IsExpanded = true;
        Editor.CityBox.Text = clock.City; Editor.CountryBox.Text = clock.CountryCode; Editor.ClockEnabledBox.IsChecked = clock.Enabled;
        AddZone(clock.ResolveZone());
        Editor.TimeZoneBox.ItemsSource = null; Editor.TimeZoneBox.ItemsSource = zones;
        Editor.TimeZoneBox.SelectedItem = zones.First(x => x.Id == clock.TimeZoneId);
    }
    private void ClockSelected(object sender, SelectionChangedEventArgs e)
    { if (Editor.ClockList.SelectedItem is WorldClock clock) SetClockEditor(clock); }
    private void ClockPresetSelected(object sender, SelectionChangedEventArgs e)
    { if (Editor.ClockPresetBox.SelectedItem is WorldClock clock) SetClockEditor(clock.Copy()); }
    private WorldClock ReadClock()
    {
        var clock = new WorldClock { City = Editor.CityBox.Text.Trim(), CountryCode = Editor.CountryBox.Text.Trim().ToUpperInvariant(),
            TimeZoneId = (Editor.TimeZoneBox.SelectedItem as ZoneChoice)?.Id ?? "", Enabled = Editor.ClockEnabledBox.IsChecked == true };
        clock.Validate(); return clock;
    }
    private void ClockError(Exception ex) { Editor.ErrorText.SetResourceReference(TextBlock.ForegroundProperty, "DangerBrush"); Editor.ErrorText.Text = L.Error(ex); Editor.ErrorText.Visibility = Visibility.Visible; }
    private void AddClockClick(object sender, RoutedEventArgs e)
    {
        try { if (clocks.Count >= 8) throw new ArgumentException("WorldClocksLimit"); clocks.Add(ReadClock()); RefreshClocks(clocks.Count - 1); Editor.ErrorText.Visibility = Visibility.Collapsed; }
        catch (ArgumentException ex) { ClockError(ex); }
    }
    private void UpdateClockClick(object sender, RoutedEventArgs e)
    {
        try
        {
            int index = Editor.ClockList.SelectedIndex; if (index < 0) throw new ArgumentException("SelectClock");
            clocks[index] = ReadClock(); RefreshClocks(index); Editor.ErrorText.Visibility = Visibility.Collapsed;
        }
        catch (ArgumentException ex) { ClockError(ex); }
    }
    private void RemoveClockClick(object sender, RoutedEventArgs e)
    { int index = Editor.ClockList.SelectedIndex; if (index >= 0) { clocks.RemoveAt(index); RefreshClocks(Math.Min(index, clocks.Count - 1)); } }
    private void MoveClock(int direction)
    {
        int index = Editor.ClockList.SelectedIndex, destination = index + direction;
        if (index < 0 || destination < 0 || destination >= clocks.Count) return;
        (clocks[index], clocks[destination]) = (clocks[destination], clocks[index]); RefreshClocks(destination);
    }
    private void ClockUpClick(object sender, RoutedEventArgs e) => MoveClock(-1);
    private void ClockDownClick(object sender, RoutedEventArgs e) => MoveClock(1);
}
