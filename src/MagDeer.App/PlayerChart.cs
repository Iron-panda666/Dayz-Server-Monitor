using System.Globalization;
using System.Windows.Input;

namespace MagDeer.App;

public sealed class PlayerChart : FrameworkElement
{
    private readonly List<QueryResult> samples = [];
    private readonly ToolTip detail = new();
    public bool Use24HourClock { get; set; } = true;
    public PlayerChart()
    {
        ClipToBounds = true;
        ToolTip = detail;
        ToolTipService.SetInitialShowDelay(this, 100);
        ToolTipService.SetShowDuration(this, 60000);
        MouseMove += OnMouseMove;
        MouseLeave += (_, _) => detail.IsOpen = false;
    }
    public void Add(QueryResult result)
    {
        samples.Add(result);
        if (samples.Count > 120) samples.RemoveAt(0);
        detail.IsOpen = false;
        InvalidateVisual();
    }
    public void Clear() { samples.Clear(); detail.IsOpen = false; InvalidateVisual(); }
    public QueryResult[] Snapshot() => samples.ToArray();
    private string Time(DateTimeOffset at, bool seconds = false) => RestartSchedule.CzechTime(at)
        .ToString(Use24HourClock ? seconds ? "HH:mm:ss" : "HH:mm" : seconds ? "hh:mm:ss tt" : "hh:mm tt", CultureInfo.InvariantCulture);
    private string FullTime(DateTimeOffset at) => $"{RestartSchedule.CzechTime(at).ToString("d MMM yyyy", L.Culture)} {Time(at, true)} (UTC{RestartSchedule.CzechTime(at):zzz})";
    private double X(QueryTimeline range, QueryResult sample) => 7 + range.Fraction(QueryTimeline.TimeOf(sample)) * Math.Max(1, ActualWidth - 14);
    private void OnMouseMove(object sender, MouseEventArgs e)
    {
        if (samples.Count == 0) return;
        var range = QueryTimeline.From(samples);
        var sample = samples.MinBy(s => Math.Abs(X(range, s) - e.GetPosition(this).X))!;
        string metrics = sample.Success ? $"{sample.Info!.Players} / {sample.Info.MaxPlayers} · UDP RTT {sample.RttMs:0} ms" : $"N/A · {sample.Failure}";
        detail.Content = L.Text("ChartDetails", FullTime(QueryTimeline.TimeOf(sample)), FullTime(sample.At), metrics + " · " + sample.TimeSource);
    }
    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);
        if (ActualWidth < 1 || ActualHeight < 1) return;
        dc.DrawRectangle(Brushes.Transparent, null, new Rect(RenderSize));
        bool times = ActualHeight >= 38, dates = ActualHeight >= 70, captionVisible = ActualHeight >= 110;
        double bottom = Math.Max(8, ActualHeight - (captionVisible ? 66 : dates ? 44 : times ? 22 : 2));
        var grid = new Pen((Brush)Application.Current.FindResource("BorderBrush"), 1);
        dc.DrawLine(grid, new Point(0, bottom), new Point(ActualWidth, bottom));
        dc.DrawLine(grid, new Point(0, bottom / 2), new Point(ActualWidth, bottom / 2));
        if (samples.Count == 0) return;
        var accent = (Brush)Application.Current.FindResource("AccentBrush");
        var muted = (Brush)Application.Current.FindResource("MutedBrush");
        var amber = new Pen((Brush)Application.Current.FindResource("WarningBrush"), 1.5);
        var line = new Pen(accent, 1.8);
        var max = Math.Max(1, samples.Max(x => (int?)x.Info?.MaxPlayers) ?? 1);
        var range = QueryTimeline.From(samples);
        Point? previous = null;
        foreach (var sample in samples)
        {
            double x = X(range, sample);
            if (!sample.Success)
            { previous = null; dc.DrawLine(amber, new Point(x, bottom - 7), new Point(x, bottom)); continue; }
            var p = new Point(x, bottom - 3 - sample.Info!.Players / (double)max * (bottom - 6));
            if (previous is { } last) dc.DrawLine(line, last, p);
            dc.DrawEllipse(accent, null, p, 2, 2); previous = p;
        }
        var ticks = new[] { range.Start, range.Start + (range.End - range.Start) / 2, range.End };
        for (int i = 0; i < ticks.Length; i++)
        {
            if (!times || (i == 0 && ActualWidth < 160)) continue;
            // Two labels suffice for small windows or a 12-hour clock.
            if (i == 1 && ActualWidth < (Use24HourClock ? 400 : 540)) continue;
            double x = i * (ActualWidth - 14) / 2 + 7;
            dc.DrawLine(grid, new Point(x, bottom), new Point(x, bottom + 4));
            DrawLabel(dc, Time(ticks[i]), muted, x, bottom + 6, i);
            if (dates) DrawLabel(dc, RestartSchedule.CzechTime(ticks[i]).ToString("d MMM yyyy", L.Culture), muted, x, bottom + 22, i);
        }
        if (!captionVisible) return;
        var caption = L.Text("ChartLast", FullTime(QueryTimeline.TimeOf(samples[^1])));
        var text = Text(caption, muted);
        dc.PushClip(new RectangleGeometry(new Rect(0, bottom + 43, ActualWidth, 20)));
        dc.DrawText(text, new Point(0, bottom + 43)); dc.Pop();
    }
    private FormattedText Text(string value, Brush brush) => new(value, L.Culture, FlowDirection.LeftToRight,
        new Typeface("Segoe UI"), 11, brush, VisualTreeHelper.GetDpi(this).PixelsPerDip);
    private void DrawLabel(DrawingContext dc, string value, Brush brush, double x, double y, int align)
    {
        var text = Text(value, brush);
        double left = align == 0 ? 0 : align == 2 ? ActualWidth - text.Width : x - text.Width / 2;
        dc.DrawText(text, new Point(Math.Max(0, left), y));
    }
}
