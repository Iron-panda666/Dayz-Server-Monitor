namespace MagDeer.App;

public enum DashboardMode { Stack, Wide, Bar, Portrait, Ribbon, Mini }

/// <summary>Retains the same controls while moving independent tiles into size-based slots.</summary>
public sealed class DashboardPanel : Panel
{
    public static readonly DependencyProperty RoleProperty = DependencyProperty.RegisterAttached("Role", typeof(string), typeof(DashboardPanel), new FrameworkPropertyMetadata("", FrameworkPropertyMetadataOptions.AffectsParentMeasure));
    public static void SetRole(DependencyObject obj, string value) => obj.SetValue(RoleProperty, value);
    public static string GetRole(DependencyObject obj) => (string)obj.GetValue(RoleProperty);
    public static readonly DependencyProperty CompactProperty = DependencyProperty.Register(nameof(Compact), typeof(bool), typeof(DashboardPanel), new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.AffectsMeasure));
    public static readonly DependencyProperty HasWorldClocksProperty = DependencyProperty.Register(nameof(HasWorldClocks), typeof(bool), typeof(DashboardPanel), new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.AffectsMeasure));
    public bool Compact { get => (bool)GetValue(CompactProperty); set => SetValue(CompactProperty, value); }
    public bool HasWorldClocks { get => (bool)GetValue(HasWorldClocksProperty); set => SetValue(HasWorldClocksProperty, value); }
    public DashboardMode Mode { get; private set; }
    public event Action<DashboardMode>? ModeChanged;
    public event Action? SlotsChanged;
    public IReadOnlyDictionary<string, Rect> Slots { get; private set; } = new Dictionary<string, Rect>();
    public static DashboardMode ChooseMode(Size size) => size.Height < 170 || size.Width / size.Height >= 6 ? DashboardMode.Ribbon
        : size.Width / size.Height < .72 ? DashboardMode.Portrait
        : size.Width / size.Height >= 3 ? DashboardMode.Bar
        : size.Width < 650 && size.Height < 430 ? DashboardMode.Mini
        : size.Width / size.Height >= 1.40 ? DashboardMode.Wide : DashboardMode.Stack;

    public static IReadOnlyDictionary<string, Rect> Plan(Size size, bool compact, bool world)
    {
        double w = Math.Max(1, size.Width), h = Math.Max(1, size.Height), gap = Math.Min(10, Math.Min(w, h) * .035);
        var slots = new Dictionary<string, Rect>();
        void Put(string role, double x, double y, double width, double height) => slots[role] = new(x + gap / 2, y + gap / 2, Math.Max(0, width - gap), Math.Max(0, height - gap));
        var mode = ChooseMode(size);
        if (mode == DashboardMode.Ribbon)
        {
            double x = 0;
            foreach (var (role, fraction) in new[] { ("Clock", .20), ("Restart", .34), ("Players", .19), ("Status", .17), ("Rtt", .10) })
            { Put(role, x, 0, w * fraction, h); x += w * fraction; }
            return slots;
        }
        double top = w < 240 || h < 240 ? 0 : 46;
        if (top > 0) Put("Server", 0, 0, w, top);
        double worldHeight = world ? Math.Min(h * .17, w < 240 ? 160 : 210) : 0;
        if (world) Put("World", 0, h - worldHeight, w, worldHeight);
        double body = h - top - worldHeight;
        void Metrics(double x, double y, double width, double height)
        {
            Put("Status", x, y, width * .42, height);
            Put("Players", x + width * .42, y, width * .30, height);
            Put("Rtt", x + width * .72, y, width * .28, height);
        }
        if (mode == DashboardMode.Mini)
        {
            Put("Clock", 0, top, w * .36, body * .62); Put("Restart", w * .36, top, w * .64, body * .62);
            Put("Status", 0, top + body * .62, w * .33, body * .38);
            Put("Players", w * .33, top + body * .62, w * .38, body * .38);
            Put("Rtt", w * .71, top + body * .62, w * .29, body * .38);
        }
        else if (mode == DashboardMode.Bar)
        {
            if (compact)
            {
                Put("Clock", 0, top, w * .25, body); Put("Restart", w * .25, top, w * .40, body);
                Put("Status", w * .65, top, w * .35, body * .29);
                Put("Players", w * .65, top + body * .29, w * .35, body * .42);
                Put("Rtt", w * .65, top + body * .71, w * .35, body * .29);
                return slots;
            }
            Put("Clock", 0, top, w * .22, body); Put("Restart", w * .22, top, w * .30, body);
            Put("Status", w * .52, top, w * .19, body * .29);
            Put("Players", w * .52, top + body * .29, w * .19, body * .42);
            Put("Rtt", w * .52, top + body * .71, w * .19, body * .29);
            Put("History", w * .71, top, w * .29, body * .73); Put("Details", w * .71, top + body * .73, w * .29, body * .27);
        }
        else if (mode == DashboardMode.Wide)
        {
            Put("Clock", 0, top, w * .42, body * (compact ? 1 : .70));
            if (!compact) Put("Details", 0, top + body * .70, w * .42, body * .30);
            Put("Restart", w * .42, top, w * .58, body * (compact ? .68 : .45));
            Metrics(w * .42, top + body * (compact ? .68 : .45), w * .58, body * (compact ? .32 : .24));
            if (!compact) Put("History", w * .42, top + body * .69, w * .58, body * .31);
        }
        else if (mode == DashboardMode.Portrait)
        {
            Put("Clock", 0, top, w, body * (compact ? .27 : .20));
            Put("Restart", 0, top + body * (compact ? .27 : .20), w, body * (compact ? .33 : .25));
            double y = top + body * (compact ? .60 : .45);
            Put("Status", 0, y, w, body * .11); y += body * .11;
            if (w < 240)
            {
                Put("Players", 0, y, w, body * (compact ? .17 : .15)); y += body * (compact ? .17 : .15);
                Put("Rtt", 0, y, w, body * (compact ? .12 : .10)); y += body * (compact ? .12 : .10);
            }
            else
            {
                Put("Players", 0, y, w / 2, body * (compact ? .29 : .20)); Put("Rtt", w / 2, y, w / 2, body * (compact ? .29 : .20));
                y += body * (compact ? .29 : .20);
            }
            if (!compact) { double left = h - worldHeight - y; Put("History", 0, y, w, left * .72); Put("Details", 0, y + left * .72, w, left * .28); }
        }
        else
        {
            double clock = compact ? .40 : .24, restart = compact ? .37 : .28, metrics = compact ? .23 : .17;
            Put("Clock", 0, top, w, body * clock); Put("Restart", 0, top + body * clock, w, body * restart);
            Metrics(0, top + body * (clock + restart), w, body * metrics);
            if (!compact) { Put("History", 0, top + body * .69, w, body * .21); Put("Details", 0, top + body * .90, w, body * .10); }
        }
        return slots;
    }
    protected override Size MeasureOverride(Size availableSize)
    {
        var size = new Size(double.IsFinite(availableSize.Width) ? Math.Max(1, availableSize.Width) : 850, double.IsFinite(availableSize.Height) ? Math.Max(1, availableSize.Height) : 800);
        var mode = ChooseMode(size);
        if (Mode != mode) { Mode = mode; ModeChanged?.Invoke(mode); }
        var next = Plan(size, Compact, HasWorldClocks);
        bool changed = next.Count != Slots.Count || next.Any(x => !Slots.TryGetValue(x.Key, out var old) || old != x.Value);
        Slots = next;
        if (changed) SlotsChanged?.Invoke();
        foreach (UIElement child in InternalChildren)
        {
            bool shown = Slots.TryGetValue(GetRole(child), out var rect);
            child.Visibility = shown ? Visibility.Visible : Visibility.Collapsed;
            if (shown) child.Measure(rect.Size);
        }
        return size;
    }
    protected override Size ArrangeOverride(Size finalSize)
    {
        Slots = Plan(finalSize, Compact, HasWorldClocks);
        foreach (UIElement child in InternalChildren) child.Arrange(Slots.TryGetValue(GetRole(child), out var rect) ? rect : new Rect(0, 0, 0, 0));
        return finalSize;
    }
}
