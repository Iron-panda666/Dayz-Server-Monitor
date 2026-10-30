namespace MagDeer.App;

public sealed class PlayerCountDisplay : StackPanel
{
    private readonly TextBlock title = new() { Style = (Style)Application.Current.FindResource("Label"), FontSize = 14 };
    private readonly TextBlock players = new() { Text = "N/A", FontSize = 31, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 7, 0, 0) };
    private readonly TextBlock queue = new() { FontSize = 17, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 3, 0, 0), TextWrapping = TextWrapping.Wrap };
    private readonly TextBlock capacity = new() { FontSize = 13 };
    public bool Condensed
    {
        set { capacity.Visibility = value ? Visibility.Collapsed : Visibility.Visible; players.FontSize = value ? 28 : 31; players.TextWrapping = TextWrapping.NoWrap; }
    }
    public string PlayerText => players.Text;
    public string QueueText => queue.Text;
    public bool ShowTitle { set => title.Visibility = value ? Visibility.Visible : Visibility.Collapsed; }
    public void FitTo(Size available)
    {
        players.FontSize = Math.Clamp(available.Width / 4.4, 12, 34);
        queue.FontSize = Math.Clamp(available.Width / 9, 13, 18);
    }
    public PlayerCountDisplay()
    {
        TextBlock.SetTextAlignment(this, TextAlignment.Center);
        title.SetResourceReference(TextBlock.TextProperty, "L.Players");
        players.SetResourceReference(TextBlock.ForegroundProperty, "MetricBrush");
        capacity.SetResourceReference(TextBlock.TextProperty, "L.Capacity");
        capacity.SetResourceReference(TextBlock.ForegroundProperty, "MutedBrush");
        queue.SetResourceReference(TextBlock.ToolTipProperty, "L.QueueTip");
        Children.Add(title); Children.Add(players); Children.Add(queue); Children.Add(capacity);
        Update(null);
    }
    public void Update(QueryResult? result)
    {
        var info = result is { Success: true } ? result.Info : null;
        players.Text = info is null ? "N/A" : $"{info.Players} / {info.MaxPlayers}";
        int? waiting = info?.Queue is >= 0 and <= 10000 ? info.Queue : null;
        queue.Text = waiting is > 0 ? L.Text("QueueWaiting", waiting.Value) : waiting == 0 ? L.Text("QueueEmpty") : L.Text("QueueUnknown");
        queue.SetResourceReference(TextBlock.ForegroundProperty, waiting is > 0 ? "WarningBrush" : waiting == 0 ? "MutedBrush" : "MetricBrush");
    }
}
