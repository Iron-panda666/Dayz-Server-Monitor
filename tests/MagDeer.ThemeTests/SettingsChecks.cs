using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using MagDeer.App;
using MagDeer.Core;
using Localization = MagDeer.App.Localization;

internal static class SettingsChecks
{
    public static void Run(Action<string, Action> test, Action<bool, string> assert)
    {
        test("Settings navigation changes section without losing edited fields or creating a Window", () =>
        {
            var view = Demo(); var sections = (TabControl)view.FindName("Sections");
            var nav = (ListBox)view.FindName("Navigation"); var compact = (ComboBox)view.FindName("CompactNavigation");
            var name = (TextBox)view.FindName("NameBox"); name.Text = "Unsaved renamed server";
            Layout(view, 940, 760); assert(sections.Items.Count == 7, "Missing settings section");
            nav.SelectedIndex = 3; Layout(view, 940, 760);
            assert(sections.SelectedIndex == 3, "Sidebar selection did not change page");
            Layout(view, 360, 700); assert(compact.Visibility == Visibility.Visible && nav.IsVisible == false, "Narrow navigation did not replace sidebar");
            assert(compact.ActualHeight < 80, "Compact selector rendered a page instead of its label");
            compact.SelectedIndex = 4; Layout(view, 360, 700); assert(sections.SelectedIndex == 4, "Compact selection did not change page");
            sections.SelectedIndex = 0; Layout(view, 940, 760);
            assert(ReferenceEquals(name, view.FindName("NameBox")) && name.Text == "Unsaved renamed server", "Navigation discarded form data");
        });
        test("Real settings keep footer reachable and fields inside bounds in five languages and narrow layouts", () =>
        {
            foreach (var language in L.Languages)
            foreach (var theme in new[] { "Light", "Dark", "Ocean" })
            {
                Localization.Apply(language.Id); ThemeManager.Apply(theme); var view = Demo(false);
                var sections = (TabControl)view.FindName("Sections"); var save = (Button)view.FindName("SaveButton");
                foreach (double width in new[] { 360, 720, 940 })
                for (int page = 0; page < sections.Items.Count; page++)
                {
                    sections.SelectedIndex = page; Layout(view, width, 560);
                    var bounds = save.TransformToAncestor(view).TransformBounds(new Rect(save.RenderSize));
                    assert(bounds.Width > 0 && bounds.Left >= 0 && bounds.Right <= width + 1 && bounds.Bottom <= 561, $"Save button clipped: {language.Id}/{theme}/{width}/{page}");
                    foreach (var fields in Visuals<SettingsFields>(view))
                    foreach (FrameworkElement field in fields.Children)
                    {
                        var box = field.TransformToAncestor(fields).TransformBounds(new Rect(field.RenderSize));
                        assert(box.Left >= -.1 && box.Right <= fields.ActualWidth + .1, "Settings field overflow");
                    }
                    foreach (var scroll in Visuals<ScrollViewer>(view).Where(s => s.Content is StackPanel))
                        assert(scroll.ScrollableWidth < 1, "Settings require horizontal scrolling");
                }
            }
            Localization.Apply("en"); ThemeManager.Apply("Light");
        });
        test("Settings forward user actions and explain disabled optional integrations", () =>
        {
            var view = Demo(); string? action = null; view.ActionRequested += (_, e) => action = e.Name;
            ((Button)view.FindName("SaveButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            assert(action == "SaveClick", "Save action disconnected from window controller");
            var theme = (ComboBox)view.FindName("ThemeBox"); theme.ItemsSource = ThemeManager.Choices; theme.SelectedIndex = 0;
            action = null; theme.SelectedIndex = 1; assert(action == "PreviewAppearance", "Theme selection waits for Save instead of previewing");
            var sections = (TabControl)view.FindName("Sections"); sections.SelectedIndex = 5; Layout(view, 940, 760);
            var export = (CheckBox)view.FindName("ExportBox"); var address = (TextBox)view.FindName("ExportAddressBox");
            var details = (Expander)view.FindName("ExportDetails");
            assert(!details.IsEnabled && !details.IsExpanded && !address.IsVisible, "Disabled export details remain interactive");
            export.IsChecked = true; Layout(view, 940, 760); assert(address.IsEnabled, "Enabling export did not enable its fields");
        });
        test("Render actual settings pages in English without opening windows", () =>
        {
            string directory = Path.GetFullPath("artifacts/previews"); Directory.CreateDirectory(directory);
            foreach (var (name, theme, page, width, height) in new[] {
                ("settings-overview", "Light", 0, 940, 760), ("settings-restarts", "Dark", 1, 940, 760),
                ("settings-appearance", "Ocean", 3, 940, 760), ("settings-narrow", "Light", 0, 360, 740) })
            {
                ThemeManager.Apply(theme); Localization.Apply("en"); var view = Demo(false);
                ((ComboBox)view.FindName("ThemeBox")).SelectedItem = ThemeManager.Choices.First(c => c.Id == theme);
                ((TabControl)view.FindName("Sections")).SelectedIndex = page; Layout(view, width, height);
                var image = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32); image.Render(view);
                var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(image));
                using var stream = File.Create(Path.Combine(directory, name + ".png")); encoder.Save(stream);
            }
            ThemeManager.Apply("Light");
        });
    }
    private static SettingsView Demo(bool reset = true)
    {
        if (reset) { Localization.Apply("en"); ThemeManager.Apply("Light"); }
        var view = new SettingsView(); view.Resources.MergedDictionaries.Add(Application.Current.Resources);
        var values = new Dictionary<string, string> { ["NameBox"] = "MAG MiddleAgedGamers #2 PVE DEER", ["IpBox"] = "217.182.198.57", ["GamePortBox"] = "2302", ["QueryPortBox"] = "2303",
            ["ScheduleBox"] = "03:00, 07:00, 11:00, 15:00, 19:00, 23:00", ["EveryHoursBox"] = "4", ["AnchorBox"] = "03:00", ["LogoutMinutesBox"] = "10", ["KickMinutesBox"] = "5",
            ["NormalBox"] = "300", ["BeforeBox"] = "30", ["DuringBox"] = "15", ["TimeoutBox"] = "4", ["FailuresBox"] = "3", ["RetentionBox"] = "90", ["OffsetBox"] = "-1",
            ["NtpServerBox"] = "time.cloudflare.com", ["NtpIntervalBox"] = "60", ["NtpTimeoutBox"] = "4", ["ExportAddressBox"] = "127.0.0.1", ["ExportPortBox"] = "9187" };
        foreach (var (name, value) in values) ((TextBox)view.FindName(name)).Text = value;
        var profiles = ServerCatalog.Create(); var list = (ComboBox)view.FindName("ProfilesBox"); list.ItemsSource = profiles; list.SelectedIndex = 1;
        Set("ThemeBox", ThemeManager.Choices); Set("LanguageBox", L.Languages); Set("DisplayProfileBox", DisplayProfiles.All);
        Set("WallpaperBox", DashboardArtwork.Choices);
        ((CheckBox)view.FindName("AeroGlassBox")).IsChecked = true;
        ((CheckBox)view.FindName("KickKnownBox")).IsChecked = true;
        Set("MonitorBox", new[] { new { Label = "1. Main display · 1920 × 1080" } });
        Set("MinimizeTargetBox", new[] { new { Label = "System tray · beside the clock" } });
        ((CheckBox)view.FindName("Clock24Box")).IsChecked = true; ((CheckBox)view.FindName("ServerClockBox")).IsChecked = true;
        ((CheckBox)view.FindName("VisualBox")).IsChecked = true; ((CheckBox)view.FindName("GameSessionBox")).IsChecked = true;
        ((TextBlock)view.FindName("DataPathText")).Text = "Portable folder / data";
        return view;
        void Set(string name, System.Collections.IEnumerable items) { var box = (ComboBox)view.FindName(name); box.ItemsSource = items; box.SelectedIndex = 0; }
    }
    private static void Layout(FrameworkElement view, double width, double height)
    { view.Measure(new Size(width, height)); view.Arrange(new Rect(0, 0, width, height)); view.UpdateLayout(); }
    private static IEnumerable<T> Visuals<T>(DependencyObject root) where T : DependencyObject
    {
        if (root is T item) yield return item;
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++) foreach (var child in Visuals<T>(VisualTreeHelper.GetChild(root, i))) yield return child;
    }
}
