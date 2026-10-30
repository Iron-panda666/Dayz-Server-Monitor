namespace MagDeer.App;

public sealed class PlayerCountDisplay : StackPanel
{
    private readonly TextBlock players = new() { Text = "N/A", FontSize = 31, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 7, 0, 0) };
    private readonly TextBlock queue = new() { FontSize = 17, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 3, 0, 0), TextWrapping = TextWrapping.Wrap };
    public string PlayerText => players.Text;
    public string QueueText => queue.Text;
    public PlayerCountDisplay()
    {
        var title = new TextBlock { Style = (Style)Application.Current.FindResource("Label") };
        title.SetResourceReference(TextBlock.TextProperty, "L.Players");
        players.SetResourceReference(TextBlock.ForegroundProperty, "MetricBrush");
        var capacity = new TextBlock { FontSize = 11 };
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
