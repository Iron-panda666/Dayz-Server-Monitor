namespace MagDeer.App;

/// <summary>Form columns become full-width rows when the settings window becomes narrow.</summary>
public sealed class SettingsFields : Panel
{
    public static readonly DependencyProperty MaxColumnsProperty = DependencyProperty.Register(nameof(MaxColumns), typeof(int), typeof(SettingsFields), new FrameworkPropertyMetadata(2, FrameworkPropertyMetadataOptions.AffectsMeasure));
    public int MaxColumns { get => (int)GetValue(MaxColumnsProperty); set => SetValue(MaxColumnsProperty, value); }
    private const double Gap = 14, MinimumWidth = 168;
    private int Columns(double width) => Math.Clamp((int)((width + Gap) / (MinimumWidth + Gap)), 1, Math.Clamp(MaxColumns, 1, 3));
    protected override Size MeasureOverride(Size availableSize)
    {
        double width = double.IsFinite(availableSize.Width) ? Math.Max(0, availableSize.Width) : MinimumWidth * Math.Clamp(MaxColumns, 1, 3);
        int columns = Columns(width); double fieldWidth = Math.Max(0, (width - Gap * (columns - 1)) / columns), height = 0;
        for (int i = 0; i < InternalChildren.Count; i += columns)
        {
            double rowHeight = 0;
            for (int j = i; j < Math.Min(i + columns, InternalChildren.Count); j++)
            { InternalChildren[j].Measure(new Size(fieldWidth, double.PositiveInfinity)); rowHeight = Math.Max(rowHeight, InternalChildren[j].DesiredSize.Height); }
            height += rowHeight;
        }
        return new(width, height);
    }
    protected override Size ArrangeOverride(Size finalSize)
    {
        int columns = Columns(finalSize.Width); double width = Math.Max(0, (finalSize.Width - Gap * (columns - 1)) / columns), y = 0;
        for (int i = 0; i < InternalChildren.Count; i += columns)
        {
            double height = Enumerable.Range(i, Math.Min(columns, InternalChildren.Count - i)).Max(j => InternalChildren[j].DesiredSize.Height);
            for (int j = i; j < Math.Min(i + columns, InternalChildren.Count); j++) InternalChildren[j].Arrange(new Rect((j - i) * (width + Gap), y, width, height));
            y += height;
        }
        return finalSize;
    }
}
