using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using MagDeer.App;
using MagDeer.Core;
using Localization = MagDeer.App.Localization;

internal static class Program
{
    private static int passed, failed;
    [STAThread]
    private static int Main(string[] args)
    {
        // Load the real compiled application resources, without starting the app, creating
        // a Window, calling Show/Run, querying a server or changing the foreground window.
        var app = new App();
        app.InitializeComponent();
        Localization.Apply("en");
        Test("Embedded original T/R ICO contains seven usable sizes", () =>
        {
            var resource = Application.GetResourceStream(new Uri("pack://application:,,,/DayzServerMonitor;component/Assets/app.ico"))!;
            using var stream = resource.Stream;
            var decoder = new IconBitmapDecoder(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
            Assert(decoder.Frames.Count == 7, "Missing icon sizes");
            Assert(decoder.Frames.Any(f => f.PixelWidth == 16) && decoder.Frames.Any(f => f.PixelWidth == 256), "Missing smallest/largest icon");
        });
        Test("ComboBox selected item renders DisplayMemberPath instead of the record dump", () =>
        {
            var choice = new DisplayProbe("Readable choice");
            var combo = new ComboBox { Style = (Style)app.FindResource(typeof(ComboBox)), ItemsSource = new[] { choice },
                DisplayMemberPath = "Label", SelectedIndex = 0, Width = 500 };
            var host = new Grid(); host.Resources.MergedDictionaries.Add(app.Resources); host.Children.Add(combo);
            Layout(host, 540, 100); combo.ApplyTemplate(); Layout(host, 540, 100);
            var texts = VisualTexts(combo).ToArray();
            Assert(texts.Contains("Readable choice"), "Selected value is not the friendly label: " + string.Join(" / ", texts));
            Assert(!texts.Any(x => x.Contains("DisplayProbe", StringComparison.Ordinal)), "Internal record fields are visible");
        });
        Test("Existing WPF labels change through all five UI languages", () =>
        {
            var host = new Grid(); host.Resources.MergedDictionaries.Add(app.Resources);
            var label = new TextBlock(); label.SetResourceReference(TextBlock.TextProperty, "L.Players"); host.Children.Add(label);
            var expected = new[] { "PLAYERS", "HRÁČI", "GRACZE", "SPIELER", "JUGADORES" };
            for (int i = 0; i < L.Languages.Count; i++)
            { Localization.Apply(L.Languages[i].Id); Layout(host, 250, 70); Assert(label.Text == expected[i], "Label failed in " + L.Language); }
            Localization.Apply("en");
        });
        Test("World clock strip uses selected labels, 12/24 hours and hides disabled clocks", () =>
        {
            var strip = new WorldClockStrip();
            strip.Configure([new WorldClock { City = "My NYC", CountryCode = "US", TimeZoneId = "America/New_York" }, new WorldClock { Enabled = false }]);
            strip.Update(DateTimeOffset.Parse("2026-10-28T20:59:00Z", CultureInfo.InvariantCulture), true);
            Assert(strip.Children.Count == 1 && strip.Visibility == Visibility.Visible, "Disabled clock shown");
            var row = (StackPanel)((Border)strip.Children[0]).Child;
            Assert(((TextBlock)row.Children[0]).Text == "My NYC · US", "Custom city not above the time");
            Assert(((TextBlock)row.Children[1]).Text == "16:59", "Wrong New York time");
            strip.Update(DateTimeOffset.Parse("2026-10-28T20:59:00Z", CultureInfo.InvariantCulture), false);
            Assert(((TextBlock)row.Children[1]).Text == "04:59 PM", "12-hour format not applied");
            strip.Configure([]); Assert(strip.Visibility == Visibility.Collapsed, "Empty strip still visible");
        });
        Test("All four theme choices validate and roundtrip in config", () =>
        {
            Assert(ThemeManager.Choices.Count == 4, "Missing a requested theme");
            foreach (var choice in ThemeManager.Choices)
            {
                var settings = new MonitorSettings { Theme = choice.Id };
                settings.Validate(); settings.Copy().Validate();
                Assert(settings.Copy().Theme == choice.Id, "Theme lost during JSON roundtrip");
            }
        });

        Test("Existing cards and player numbers update together across theme switches", () =>
        {
            var number = Text("87 / 121", "MetricBrush", 31);
            var card = new Border { Style = (Style)app.FindResource("Card"), Child = number };
            var host = new Grid();
            // A live Window normally owns the resource lookup scope. Give the offscreen
            // visual an equivalent logical owner so dictionary changes invalidate it too.
            host.Resources.MergedDictionaries.Add(app.Resources);
            host.Children.Add(card);
            foreach (var choice in ThemeManager.Choices.Concat(ThemeManager.Choices.Reverse()))
            {
                ThemeManager.Apply(choice.Id);
                Layout(host, 220, 100);
                Assert(((SolidColorBrush)card.Background).Color == ColorOf(ThemeManager.Colors(choice.Id)["PanelBrush"]),
                    choice.Id + ": card kept an old background");
                Assert(((SolidColorBrush)number.Foreground).Color == ColorOf(ThemeManager.Colors(choice.Id)["MetricBrush"]),
                    choice.Id + ": player number kept an old text color");
            }
        });

        Test("Every theme has matching keys and sufficient contrast on real surfaces", () =>
        {
            var keys = ThemeManager.Colors("Light").Keys.Order().ToArray();
            foreach (var choice in ThemeManager.Choices)
            {
                var colors = ThemeManager.Colors(choice.Id);
                Assert(keys.SequenceEqual(colors.Keys.Order()), choice.Id + ": incomplete palette");
                void Check(string text, string background, double minimum)
                {
                    double contrast = Contrast(ColorOf(colors[text]), ColorOf(colors[background]));
                    Assert(contrast >= minimum, $"{choice.Id}: {text}/{background} {contrast:F2} < {minimum}");
                }
                Check("MetricBrush", "PanelBrush", 7);
                foreach (var surface in new[] { "PanelBrush", "BackgroundBrush", "HeaderBrush", "InputBrush", "RaisedBrush", "HoverBrush", "SelectionBrush", "AccentButtonBrush" })
                    Check("TextBrush", surface, 4.5);
                Check("MutedBrush", "PanelBrush", 4.5);
                Check("SubtleBrush", "BackgroundBrush", 4.5);
                Check("AccentBrush", "PanelBrush", 4.5);
                Check("SuccessBrush", "SuccessSurfaceBrush", 4.5);
                Check("WarningBrush", "WarningSurfaceBrush", 4.5);
                Check("DangerBrush", "PanelBrush", 4.5);
                Console.WriteLine($"  {choice.Id}: player number contrast {Contrast(ColorOf(colors["MetricBrush"]), ColorOf(colors["PanelBrush"])):F2}:1");
            }
        });

        if (args.Length == 2 && args[0] == "--render")
        {
            Test("Render comparison using real WPF card styles without any windows", () => RenderComparison(args[1]));
            Test("Render world clocks without any windows", () => RenderWorldClocks(Path.Combine(Path.GetDirectoryName(args[1])!, "world-clocks.png")));
            Test("Render dated query chart with irregular samples and unavailable gap", () => RenderChart(Path.Combine(Path.GetDirectoryName(args[1])!, "query-chart.png")));
        }
        Console.WriteLine($"{passed} theme checks passed, {failed} failed. No desktop windows were opened.");
        return failed == 0 ? 0 : 1;
    }

    private static void Test(string name, Action test)
    {
        try { test(); passed++; Console.WriteLine("PASS " + name); }
        catch (Exception ex) { failed++; Console.WriteLine("FAIL " + name + ": " + ex.Message); }
    }
    private static void Assert(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
    private static System.Collections.Generic.IEnumerable<string> VisualTexts(DependencyObject node)
    {
        if (node is TextBlock text) yield return text.Text;
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(node); i++)
            foreach (var value in VisualTexts(VisualTreeHelper.GetChild(node, i))) yield return value;
    }
    private static Color ColorOf(string color) => (Color)ColorConverter.ConvertFromString(color);
    private static double Contrast(Color a, Color b)
    {
        static double Linear(byte value) { double c = value / 255.0; return c <= .04045 ? c / 12.92 : Math.Pow((c + .055) / 1.055, 2.4); }
        static double Luminance(Color c) => .2126 * Linear(c.R) + .7152 * Linear(c.G) + .0722 * Linear(c.B);
        var first = Luminance(a); var second = Luminance(b);
        return (Math.Max(first, second) + .05) / (Math.Min(first, second) + .05);
    }
    private static TextBlock Text(string value, string brush, double size)
    {
        var text = new TextBlock { Text = value, FontFamily = new FontFamily("Segoe UI"), FontSize = size, Margin = new Thickness(0, 2, 0, 2) };
        text.SetResourceReference(TextBlock.ForegroundProperty, brush);
        return text;
    }
    private static void Layout(FrameworkElement element, double width, double height)
    {
        element.Measure(new Size(width, height)); element.Arrange(new Rect(0, 0, width, height)); element.UpdateLayout();
    }

    private static void RenderComparison(string path)
    {
        Localization.Apply("en");
        const int width = 480, height = 400;
        var images = new List<BitmapSource>();
        foreach (var choice in ThemeManager.Choices)
        {
            ThemeManager.Apply(choice.Id);
            var stack = new StackPanel { Margin = new Thickness(22, 14, 22, 12) };
            stack.Children.Add(Text(choice.Label, "TextBrush", 21));
            stack.Children.Add(Text("Dayz Server Monitor   ·   THEME PREVIEW / DEMO", "MutedBrush", 11));
            stack.Children.Add(Text("16:30", "MetricBrush", 55));
            var countdown = new StackPanel();
            countdown.Children.Add(Text(L.Text("Countdown"), "MutedBrush", 11));
            countdown.Children.Add(Text("02:30:00", "AccentBrush", 39));
            var restartCard = new Border { Style = (Style)Application.Current.FindResource("Card"), Padding = new Thickness(15, 7, 15, 7), Child = countdown };
            stack.Children.Add(restartCard);
            var metrics = new Grid { Margin = new Thickness(0, 9, 0, 8) };
            metrics.ColumnDefinitions.Add(new ColumnDefinition()); metrics.ColumnDefinitions.Add(new ColumnDefinition());
            foreach (var (title, value, column) in new[] { (L.Text("Players"), "87 / 121", 0), ("UDP QUERY RTT", "32 ms", 1) })
            {
                var content = new StackPanel(); content.Children.Add(Text(title, "MutedBrush", 11));
                var number = Text(value, "MetricBrush", 30); number.FontWeight = FontWeights.SemiBold; content.Children.Add(number);
                var card = new Border { Style = (Style)Application.Current.FindResource("Card"), Padding = new Thickness(15, 9, 15, 9), Child = content,
                    Margin = column == 0 ? new Thickness(0, 0, 8, 0) : new Thickness(0) };
                Grid.SetColumn(card, column); metrics.Children.Add(card);
            }
            stack.Children.Add(metrics);
            var footer = new Grid(); footer.ColumnDefinitions.Add(new ColumnDefinition()); footer.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            footer.Children.Add(Text("● ONLINE  ·  example values", "SuccessBrush", 12));
            var button = new Button { Content = L.Text("Settings"), Padding = new Thickness(12, 6, 12, 6) }; Grid.SetColumn(button, 1); footer.Children.Add(button);
            stack.Children.Add(footer);
            var root = new Border { Width = width, Height = height, Child = stack, BorderThickness = new Thickness(1) };
            root.SetResourceReference(Border.BackgroundProperty, "BackgroundBrush"); root.SetResourceReference(Border.BorderBrushProperty, "BorderBrush");
            Layout(root, width, height);
            var bitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32); bitmap.Render(root); bitmap.Freeze(); images.Add(bitmap);
        }
        var visual = new DrawingVisual();
        using (var drawing = visual.RenderOpen())
            for (int i = 0; i < images.Count; i++) drawing.DrawImage(images[i], new Rect(i % 2 * width, i / 2 * height, width, height));
        var combined = new RenderTargetBitmap(width * 2, height * 2, 96, 96, PixelFormats.Pbgra32); combined.Render(visual);
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        using var file = File.Create(path); var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(combined)); encoder.Save(file);
    }

    private static void RenderWorldClocks(string path)
    {
        Localization.Apply("en"); ThemeManager.Apply("Dark");
        var content = new StackPanel { Margin = new Thickness(24) };
        content.Children.Add(Text("Dayz Server Monitor · WORLD CLOCKS · DEMO", "MutedBrush", 12));
        content.Children.Add(Text(L.Text("PragueClock"), "MutedBrush", 13));
        content.Children.Add(Text("21:59", "MetricBrush", 88));
        content.Children.Add(Text("Wednesday, 28 October 2026", "MutedBrush", 15));
        var strip = new WorldClockStrip { Margin = new Thickness(0, 18, 0, 0) };
        strip.Configure(new[] { "Europe/London", "America/New_York", "Asia/Tokyo", "Australia/Sydney" }.Select(id => WorldClock.Presets.First(x => x.TimeZoneId == id).Copy()));
        strip.Update(DateTimeOffset.Parse("2026-10-28T20:59:00Z", CultureInfo.InvariantCulture), true); content.Children.Add(strip);
        var root = new Border { Width = 810, Height = 350, Child = content }; root.SetResourceReference(Border.BackgroundProperty, "BackgroundBrush");
        Layout(root, 810, 350);
        var bitmap = new RenderTargetBitmap(810, 350, 96, 96, PixelFormats.Pbgra32); bitmap.Render(root);
        using var file = File.Create(path); var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap)); encoder.Save(file);
    }

    private static void RenderChart(string path)
    {
        Localization.Apply("en"); ThemeManager.Apply("Dark");
        var content = new StackPanel { Margin = new Thickness(24) };
        content.Children.Add(Text("Dayz Server Monitor · QUERY HISTORY · DEMO", "MutedBrush", 14));
        var chart = new PlayerChart { Height = 134, Margin = new Thickness(0, 12, 0, 8) };
        var t = DateTimeOffset.Parse("2026-10-29T22:50:00Z", CultureInfo.InvariantCulture);
        foreach (var (seconds, players) in new[] { (0, 71), (300, 74), (600, 77), (630, -1), (660, 80), (690, 82), (720, 79), (750, 85), (900, 88) })
        {
            var at = t.AddSeconds(seconds);
            chart.Add(new(at.AddMilliseconds(80), players < 0 ? null : new ServerInfo("DEMO", "deerisle", "DayZ", (byte)players, 121, "demo"),
                players < 0 ? null : 80, players < 0 ? QueryFailure.Timeout : QueryFailure.None, "demo") { RequestedAt = at });
        }
        content.Children.Add(chart);
        content.Children.Add(Text("Prague time · gap = N/A · hover over a sample for query details", "MutedBrush", 12));
        content.Children.Add(new Button { Content = L.Text("RefreshCooldown", 20), IsEnabled = false, HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, 12, 0, 0) });
        var root = new Border { Width = 820, Height = 292, Child = content }; root.SetResourceReference(Border.BackgroundProperty, "BackgroundBrush");
        Layout(root, 820, 292);
        var bitmap = new RenderTargetBitmap(820, 292, 96, 96, PixelFormats.Pbgra32); bitmap.Render(root);
        using var file = File.Create(path); var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap)); encoder.Save(file);
    }
}

public sealed record DisplayProbe(string Label);
