using System.Windows.Media.Imaging;

namespace MagDeer.App;

public static class DashboardArtwork
{
    public sealed record Choice(string Id, string Key) { public string Label => L.Text(Key); }
    public static IReadOnlyList<Choice> Choices { get; } = new[]
    {
        new Choice("Theme", "BackgroundTheme"), new Choice("None", "BackgroundNone"),
        new Choice("Sakura", "BackgroundSakura"), new Choice("Fuji", "BackgroundFuji"), new Choice("Custom", "BackgroundCustom")
    };
    private static Brush? sakura, fuji;
    private static string? cachedPath;
    private static DateTime cachedTime;
    private static Brush? cachedCustom;
    public static Brush? Resolve(MonitorSettings settings)
    {
        string style = settings.BackgroundStyle == "Theme" ? settings.Theme == "Anime" ? "Sakura" : "None" : settings.BackgroundStyle;
        if (style == "Sakura") return sakura ??= Sakura();
        if (style == "Fuji")
        {
            if (fuji is not null) return fuji;
            using var stream = Application.GetResourceStream(new Uri("pack://application:,,,/DayzServerMonitor;component/Assets/fuji-clay-banks.jpg"))!.Stream;
            return fuji = Image(stream);
        }
        if (style != "Custom" || string.IsNullOrWhiteSpace(settings.BackgroundImagePath)) return null;
        try
        {
            var file = new FileInfo(settings.BackgroundImagePath);
            if (!file.Exists || file.Length > 25 * 1024 * 1024) return null;
            if (cachedPath == file.FullName && cachedTime == file.LastWriteTimeUtc) return cachedCustom;
            using var stream = file.OpenRead();
            var brush = Image(stream);
            cachedPath = file.FullName; cachedTime = file.LastWriteTimeUtc; cachedCustom = brush;
            return brush;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException or ArgumentException or FileFormatException)
        { return null; }
    }
    private static Brush Image(Stream stream)
    {
        var decoder = BitmapDecoder.Create(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.None);
        var frame = decoder.Frames[0];
        if ((long)frame.PixelWidth * frame.PixelHeight > 40_000_000 || frame.PixelWidth < 1 || frame.PixelHeight < 1)
            throw new ArgumentException("Image dimensions exceed the wallpaper limit.");
        int width = Math.Max(1, (int)Math.Min(1920, Math.Min(frame.PixelWidth, 1080.0 * frame.PixelWidth / frame.PixelHeight)));
        stream.Position = 0;
        var image = new BitmapImage(); image.BeginInit(); image.CacheOption = BitmapCacheOption.OnLoad;
        image.DecodePixelWidth = width; image.StreamSource = stream; image.EndInit(); image.Freeze();
        var brush = new ImageBrush(image) { Stretch = Stretch.UniformToFill }; brush.Freeze(); return brush;
    }
    private static Brush Sakura()
    {
        var group = new DrawingGroup();
        using (var dc = group.Open())
        {
            dc.DrawRectangle(new LinearGradientBrush(Color.FromRgb(26, 20, 47), Color.FromRgb(99, 53, 91), 90), null, new Rect(0, 0, 1200, 800));
            dc.DrawEllipse(new SolidColorBrush(Color.FromRgb(241, 217, 225)), null, new Point(920, 155), 70, 70);
            var random = new Random(26);
            for (int i = 0; i < 65; i++) dc.DrawEllipse(new SolidColorBrush(Color.FromArgb(140, 245, 225, 240)), null, new Point(random.Next(1200), random.Next(430)), 1.5, 1.5);
            dc.DrawGeometry(new SolidColorBrush(Color.FromRgb(69, 54, 91)), null, Geometry.Parse("M0,550 L290,330 L430,440 L640,200 L920,550 L1200,400 L1200,800 L0,800 Z"));
            dc.DrawGeometry(new SolidColorBrush(Color.FromRgb(191, 169, 200)), null, Geometry.Parse("M540,330 L640,200 L753,343 L690,316 L655,335 L620,300 L590,330 Z"));
            dc.DrawGeometry(new SolidColorBrush(Color.FromRgb(37, 34, 61)), null, Geometry.Parse("M0,660 Q360,520 620,650 Q960,540 1200,640 L1200,800 L0,800 Z"));
            var branches = new Pen(new SolidColorBrush(Color.FromRgb(48, 26, 45)), 18) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round };
            dc.DrawGeometry(null, branches, Geometry.Parse("M0,750 Q130,420 60,190 M80,450 Q260,330 410,330 M67,260 Q200,210 320,140"));
            for (int i = 0; i < 90; i++)
            {
                double x = random.Next(30, 430), y = 135 + random.Next(255);
                dc.DrawEllipse(new SolidColorBrush(Color.FromArgb((byte)random.Next(130, 220), 244, 171, 206)), null, new Point(x, y), 7 + random.Next(7), 5 + random.Next(7));
            }
        }
        group.Freeze(); var brush = new DrawingBrush(group) { Stretch = Stretch.UniformToFill }; brush.Freeze(); return brush;
    }
    public static void Apply(DashboardView view, MonitorSettings settings, bool nativeGlass = false)
    {
        ThemeManager.Apply(settings.Theme);
        Brush? wallpaper = Resolve(settings);
        view.Background = wallpaper ?? (nativeGlass ? Brushes.Transparent : (Brush)Application.Current.FindResource("BackgroundBrush"));
        if (wallpaper is null && !nativeGlass) return;
        foreach (var key in new[] { "PanelBrush", "BackgroundBrush" })
        {
            var original = (SolidColorBrush)Application.Current.FindResource(key);
            var color = original.Color; color.A = wallpaper is not null && ThemeManager.IsDark(settings.Theme) ? (byte)208 : (byte)224;
            var brush = new SolidColorBrush(color); brush.Freeze(); Application.Current.Resources[key] = brush;
        }
        if (wallpaper is not null && ThemeManager.IsDark(settings.Theme))
            Application.Current.Resources["SubtleBrush"] = Application.Current.Resources["MutedBrush"];
    }
}

public sealed class AppearancePreviewSession(MonitorSettings original, Action<MonitorSettings> apply)
{
    private readonly MonitorSettings saved = original.Copy();
    public void Preview(MonitorSettings draft) => apply(draft);
    public void Finish(bool accepted) { if (!accepted) apply(saved); }
}
