using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using MagDeer.App;
using MagDeer.Core;
using Localization = MagDeer.App.Localization;

internal static class ResponsiveChecks
{
    public static void Run(Action<string, Action> test, Action<bool, string> assert)
    {
        test("Status dot and text share a vertical center across dashboard shapes", () =>
        {
            var view = Demo("XP");
            foreach (var size in new[] { new Size(480, 320), new Size(1100, 450), new Size(1920, 1080) })
            {
                Layout(view, size.Width, size.Height);
                var dot = (FrameworkElement)view.FindName("StatusDot"); var text = (FrameworkElement)view.FindName("StatusText");
                var a = dot.TransformToAncestor(view).TransformBounds(new Rect(dot.RenderSize));
                var b = text.TransformToAncestor(view).TransformBounds(new Rect(text.RenderSize));
                assert(Math.Abs((a.Top + a.Bottom - b.Top - b.Bottom) / 2) < 1, "Status dot is off-center");
            }
        });
        test("Countdown pulse affects digits only and disabled alerts restore steady theme color", () =>
        {
            var view = Demo("Dark"); var digits = (TextBlock)view.FindName("CountdownText");
            var visual = CountdownVisuals.Evaluate(25, DateTimeOffset.FromUnixTimeSeconds(1001));
            view.ApplyCountdownVisual(visual, true, true);
            assert(digits.Opacity == .78 && view.Opacity == 1 && ((TextBlock)view.FindName("StatusText")).Opacity == 1, "Pulse leaked outside digits");
            view.ApplyCountdownVisual(visual, true, false); assert(digits.Opacity == 1, "Pulse continued with alerts disabled");
        });
        test("Appearance preview cancels cleanly, loads licensed wallpaper and respects interactive drag targets", () =>
        {
            var original = new MonitorSettings { Theme = "Light", BackgroundStyle = "None" };
            var view = Demo("Light"); var preview = new AppearancePreviewSession(original, draft => DashboardArtwork.Apply(view, draft));
            preview.Preview(new MonitorSettings { Theme = "Anime", BackgroundStyle = "Sakura" });
            assert(view.Background is DrawingBrush && original.Theme == "Light", "Preview mutated saved settings or lost original artwork");
            preview.Finish(false); assert(view.Background is SolidColorBrush, "Cancel did not restore appearance");
            var photo = DashboardArtwork.Resolve(new MonitorSettings { BackgroundStyle = "Fuji" }); assert(photo is ImageBrush, "Bundled Fuji photo failed to load");
            assert(DashboardArtwork.Resolve(new MonitorSettings { BackgroundStyle = "Custom", BackgroundImagePath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "missing.png") }) is null, "Missing local wallpaper did not fall back");
            var label = new TextBlock { Text = "Drag here" }; assert(!DesktopServices.IsInteractive(label), "Plain text prevents dragging");
            var button = new Button { Content = label }; assert(DesktopServices.IsInteractive(button), "Buttons can start a window drag");
            assert(DesktopServices.IsInteractive(new ComboBox()) && DesktopServices.IsInteractive(new TextBox()), "Input control starts dragging");
            ThemeManager.Apply("Light");
        });
        test("All 27 presets retain metrics without overlapping or escaping bounds at 100-200% DPI", () =>
        {
            assert(DisplayProfiles.All.Count == 27, "Missing display presets");
            assert(DisplayProfiles.All.Select(x => x.Id).Distinct().Count() == DisplayProfiles.All.Count, "Duplicate preset ID");
            foreach (var profile in DisplayProfiles.All.Where(x => x.PixelWidth > 0))
            foreach (double dpi in new[] { 1, 1.25, 1.5, 2 })
            foreach (bool compact in new[] { false, true })
            foreach (bool world in new[] { false, true })
            {
                var size = new Size(Math.Max(128, profile.PixelWidth / dpi) - 12, Math.Max(96, profile.PixelHeight / dpi) - 44);
                var plan = DashboardPanel.Plan(size, compact, world);
                foreach (var role in new[] { "Clock", "Restart", "Status", "Players", "Rtt" }) assert(plan.ContainsKey(role), profile.Id + ": missing " + role);
                foreach (var (role, rect) in plan)
                {
                    assert(rect.Width > 0 && rect.Height > 0 && rect.X >= 0 && rect.Y >= 0 && rect.Right <= size.Width + .01 && rect.Bottom <= size.Height + .01, profile.Id + ": invalid " + role);
                    foreach (var other in plan.Where(x => x.Key != role)) assert(!rect.IntersectsWith(other.Value), profile.Id + ": overlapping tiles " + role + "/" + other.Key);
                }
            }
        });
        test("Real dashboard reflows stack, side columns, bar and portrait without recreating chart or metrics", () =>
        {
            var view = Demo("Ocean");
            var chart = view.FindName("Chart"); var players = view.FindName("PlayerCounts");
            Layout(view, 760, 850); assert(view.LayoutPanel.Mode == DashboardMode.Stack, "Tall desktop did not stack");
            Layout(view, 1100, 480); assert(view.LayoutPanel.Mode == DashboardMode.Wide, "Shallow window did not move tiles right");
            var slots = view.LayoutPanel.Slots;
            assert(slots["History"].X > slots["Clock"].Right, "History did not move right");
            Layout(view, 1920, 448); assert(view.LayoutPanel.Mode == DashboardMode.Bar, "Bar layout missing");
            Layout(view, 320, 1448); assert(view.LayoutPanel.Mode == DashboardMode.Portrait, "Portrait layout missing");
            Layout(view, 480, 96); assert(view.LayoutPanel.Mode == DashboardMode.Ribbon, "Ribbon layout missing");
            assert(ReferenceEquals(chart, view.FindName("Chart")) && ReferenceEquals(players, view.FindName("PlayerCounts")), "Data controls recreated on resize");
            assert(((PlayerCountDisplay)players).PlayerText == "121 / 121", "Player state lost during reflow");
        });
        test("Dashboard text remains readable when resizing within the same layout", () =>
        {
            var view = Demo("Aero");
            double previous = 0;
            foreach (var size in new[] { new Size(1100, 450), new Size(1920, 1080), new Size(1100, 450) })
            {
                Layout(view, size.Width, size.Height);
                var clock = (TextBlock)view.FindName("ClockText");
                assert(clock.FontSize != previous, "Clock size did not follow a same-mode resize"); previous = clock.FontSize;
                view.SetClockFormat(true);
                foreach (var name in new[] { "ClockHeading", "PlanBadgeText", "DateText", "LastUpdateText", "PollText", "CountdownLabel", "RttLabel", "SessionText" })
                {
                    var text = (TextBlock)view.FindName(name);
                    var transform = text.TransformToAncestor(view);
                    double scale = (transform.Transform(new Point(0, 1)) - transform.Transform(new Point(0, 0))).Length;
                    assert(text.FontSize * scale >= 12.5, name + ": text shrank below a readable size");
                    assert(text.TextAlignment == TextAlignment.Center, name + ": text is not centered");
                }
                if (size.Height > 700)
                {
                    var tile = view.LayoutPanel.Slots["Clock"];
                    var bounds = clock.TransformToAncestor(view.LayoutPanel).TransformBounds(new Rect(clock.RenderSize));
                    assert(Math.Abs((bounds.Left + bounds.Right - tile.Left - tile.Right) / 2) < 2, "Tall clock is off-center in its card");
                }
            }
            view.SetClockFormat(false); ((TextBlock)view.FindName("ClockText")).Text = "11:58 PM";
            Layout(view, 1100, 450);
            assert(((TextBlock)view.FindName("ClockText")).FontSize < previous, "12-hour clock did not reserve space for AM/PM");
        });
        test("Session line remains readable and smaller than the clock without altering player data", () =>
        {
            var view = Demo("Dark"); var line = (TextBlock)view.FindName("SessionText");
            assert(line.FontSize >= 13 && line.FontSize <= 16 && line.Text.Contains("01:23:45"), "Session line missing or unreadable");
            var players = ((PlayerCountDisplay)view.FindName("PlayerCounts")).PlayerText;
            foreach (var language in L.Languages) { Localization.Apply(language.Id); line.Text = L.Text("SessionRunning", "01:23:45"); assert(!line.Text.Contains("{0}") && line.Text.Contains("01:23:45"), "Session translation failed"); }
            assert(((PlayerCountDisplay)view.FindName("PlayerCounts")).PlayerText == players, "Session changed player count");
            Localization.Apply("en");
        });
        test("Display, close target and theme choices survive JSON settings roundtrip and reject invalid values", () =>
        {
            foreach (var profile in DisplayProfiles.All)
            foreach (var target in new[] { "Tray", "Taskbar" })
            {
                var settings = new MonitorSettings { DisplayProfile = profile.Id, MinimizeOnClose = true, MinimizeTarget = target, Width = 128, Height = 128 };
                settings.Validate(); var copy = settings.Copy();
                assert(copy.DisplayProfile == profile.Id && copy.MinimizeOnClose && copy.MinimizeTarget == target, "Options lost on roundtrip");
            }
            foreach (var settings in new[] { new MonitorSettings { DisplayProfile = "unknown" }, new MonitorSettings { MinimizeTarget = "unknown" }, new MonitorSettings { Theme = "unknown" } })
            { bool rejected = false; try { settings.Validate(); } catch (ArgumentException) { rejected = true; } assert(rejected, "Invalid UI option accepted"); }
        });
    }
    private static void Layout(FrameworkElement view, double width, double height)
    { view.Measure(new Size(width, height)); view.Arrange(new Rect(0, 0, width, height)); view.UpdateLayout(); }
    private static DashboardView Demo(string theme)
    {
        Localization.Apply("en"); ThemeManager.Apply(theme);
        var view = new DashboardView();
        view.Resources.MergedDictionaries.Add(Application.Current.Resources);
        view.Configure(new MonitorSettings { Theme = theme });
        DashboardArtwork.Apply(view, new MonitorSettings { Theme = theme });
        void Text(string name, string value) => ((TextBlock)view.FindName(name)).Text = value;
        Text("ClockText", "16:30"); Text("DateText", "Friday, 30 October 2026");
        Text("CountdownText", "02:30:00"); Text("NextRestartText", "19:00"); Text("CompactNextRestart", "NEXT RESTART · 19:00"); Text("NextDateText", "30 Oct · +01:00");
        Text("PlanBadgeText", "PLAN UNCONFIRMED"); ((TextBlock)view.FindName("PlanBadgeText")).SetResourceReference(TextBlock.ForegroundProperty, "WarningBrush");
        ((Border)view.FindName("PlanBadge")).SetResourceReference(Border.BackgroundProperty, "WarningSurfaceBrush");
        Text("SessionText", L.Text("SessionRunning", "01:23:45"));
        Text("ServerClockText", "Offset clock 15:30"); Text("OffsetLabel", "offset -1 h · calculated, not measured");
        Text("StatusText", "ONLINE"); ((TextBlock)view.FindName("StatusText")).SetResourceReference(TextBlock.ForegroundProperty, "SuccessBrush");
        ((System.Windows.Shapes.Ellipse)view.FindName("StatusDot")).SetResourceReference(System.Windows.Shapes.Shape.FillProperty, "SuccessBrush");
        Text("StatusHint", "A2S_INFO replies"); Text("RttText", "87 ms"); Text("LogoutText", "Log out by 18:50"); Text("KickText", "Expected kick 18:55");
        Text("LastUpdateText", "Last success: 30 Oct 16:25:28"); Text("PollText", "Next query in 54s");
        Text("DetailText", "Map: deerisle · UDP query RTT is not ICMP ping."); Text("ServerNameText", "MAG MiddleAgedGamers #2 PVE DEER"); Text("EndpointText", "217.182.198.57:2302 · UDP 2303 · DEMO VALUES");
        var profile = ServerProfile.From(new MonitorSettings());
        var selector = (ComboBox)view.FindName("ServerSelector"); selector.ItemsSource = new[] { profile }; selector.SelectedIndex = 0;
        var info = new ServerInfo("Demo", "deerisle", "DayZ", 121, 121, "demo") { Queue = 21 };
        var at = new DateTimeOffset(2026, 10, 30, 15, 25, 28, TimeSpan.Zero);
        ((PlayerCountDisplay)view.FindName("PlayerCounts")).Update(new(at, info, 87, QueryFailure.None, "demo"));
        var chart = (PlayerChart)view.FindName("Chart");
        for (int i = 0; i < 8; i++) chart.Add(new(at.AddMinutes(-35 + i * 5), info with { Players = (byte)(87 + i * 4) }, 87, QueryFailure.None, "demo"));
        return view;
    }
    public static void Render(string directory)
    {
        Directory.CreateDirectory(directory);
        void Save(DashboardView view, string name, int width, int height)
        {
            var root = new Border { Child = view }; root.SetResourceReference(Border.BackgroundProperty, "BackgroundBrush");
            Layout(root, width, height);
            var bitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32); bitmap.Render(root);
            using var file = File.Create(Path.Combine(directory, name + ".png"));
            var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap)); encoder.Save(file);
        }
        var examples = new[] { ("desktop", "Ocean", 850, 850), ("wide", "Aero", 1100, 450), ("wide-large", "Aero", 1920, 1080), ("bar-79", "Matrix", 1280, 400),
            ("bar-88", "Ocean", 1920, 480), ("bar-119", "Anime", 1480, 320), ("portrait", "Violet", 400, 1280), ("mini", "XP", 480, 320), ("ribbon", "Matrix", 480, 128) };
        foreach (var (name, theme, width, height) in examples)
        {
            Save(Demo(theme), "layout-" + name, width, height);
        }
        foreach (var theme in ThemeManager.Choices) Save(Demo(theme.Id), "theme-" + theme.Id.ToLowerInvariant(), 1120, 700);
        var fuji = Demo("Dark"); DashboardArtwork.Apply(fuji, new MonitorSettings { Theme = "Dark", BackgroundStyle = "Fuji" });
        Save(fuji, "wallpaper-fuji", 1120, 700);
        ThemeManager.Apply("Dark");
        var strip = new System.Windows.Controls.Primitives.UniformGrid { Columns = 6, Width = 1200, Height = 160 };
        foreach (int seconds in new[] { 901, 900, 600, 300, 165, 30 })
        {
            var visual = CountdownVisuals.Evaluate(seconds, DateTimeOffset.FromUnixTimeSeconds(1000));
            var color = visual.Stage == CountdownStage.Normal ? (Brush)Application.Current.FindResource("AccentBrush")
                : new SolidColorBrush((Color)ColorConverter.ConvertFromString(CountdownVisuals.Color(visual, true)));
            var content = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            content.Children.Add(new TextBlock { Text = "UNTIL RESTART", FontSize = 14, TextAlignment = TextAlignment.Center });
            content.Children.Add(new TextBlock { Text = $"00:{seconds / 60:00}:{seconds % 60:00}", Foreground = color, FontSize = 32, FontFamily = new FontFamily("Consolas"), TextAlignment = TextAlignment.Center });
            content.Children.Add(new TextBlock { Text = visual.Stage == CountdownStage.FadeToRed ? "Towards red" : visual.Stage.ToString(), FontSize = 13, TextAlignment = TextAlignment.Center });
            var tile = new Border { Child = content, Style = (Style)Application.Current.FindResource("Card"), Margin = new Thickness(4), Padding = new Thickness(8) };
            strip.Children.Add(tile);
        }
        Layout(strip, 1200, 160);
        var picture = new RenderTargetBitmap(1200, 160, 96, 96, PixelFormats.Pbgra32); picture.Render(strip);
        using var output = File.Create(Path.Combine(directory, "countdown-stages.png"));
        var png = new PngBitmapEncoder(); png.Frames.Add(BitmapFrame.Create(picture)); png.Save(output);
    }
}
