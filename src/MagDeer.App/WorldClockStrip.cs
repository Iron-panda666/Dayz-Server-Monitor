using System.Globalization;

namespace MagDeer.App;

/// <summary>A small reusable strip. It never creates windows or starts timers.</summary>
public sealed class WorldClockStrip : WrapPanel
{
    private readonly List<(TimeZoneInfo Zone, TextBlock Time, TextBlock Date)> clocks = [];
    public void Configure(IEnumerable<WorldClock> settings)
    {
        Children.Clear(); clocks.Clear();
        foreach (var clock in settings.Where(x => x.Enabled))
        {
            var zone = clock.ResolveZone();
            var label = new TextBlock { Text = clock.Label, FontSize = 13, TextAlignment = TextAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis,
                TextWrapping = TextWrapping.NoWrap, ToolTip = clock.Label + " / " + clock.TimeZoneId };
            label.SetResourceReference(TextBlock.ForegroundProperty, "MutedBrush");
            var time = new TextBlock { FontSize = 30, FontWeight = FontWeights.Light, TextAlignment = TextAlignment.Center };
            time.SetResourceReference(TextBlock.ForegroundProperty, "MetricBrush");
            var date = new TextBlock { FontSize = 12, TextAlignment = TextAlignment.Center };
            date.SetResourceReference(TextBlock.ForegroundProperty, "SubtleBrush");
            var content = new StackPanel(); content.Children.Add(label); content.Children.Add(time); content.Children.Add(date);
            var card = new Border { Width = 182, Padding = new Thickness(12, 9, 12, 9), CornerRadius = new CornerRadius(8),
                Margin = new Thickness(0, 0, 8, 8), BorderThickness = new Thickness(1), Child = content };
            card.SetResourceReference(Border.BackgroundProperty, "PanelBrush"); card.SetResourceReference(Border.BorderBrushProperty, "BorderBrush");
            Children.Add(card); clocks.Add((zone, time, date));
        }
        Visibility = clocks.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
    }
    public void Update(DateTimeOffset now, bool use24Hours)
    {
        foreach (var (zone, time, date) in clocks)
        {
            var local = TimeZoneInfo.ConvertTime(now, zone);
            time.Text = local.ToString(use24Hours ? "HH:mm" : "hh:mm tt", CultureInfo.InvariantCulture);
            time.FontSize = use24Hours ? 30 : 25;
            date.Text = local.ToString("d MMM · 'UTC'zzz", L.Culture);
        }
    }
}
