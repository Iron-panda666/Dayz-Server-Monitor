namespace MagDeer.App;

public static class ThemeManager
{
    public sealed record ThemeChoice(string Id, string LabelKey, bool IsDark)
    {
        public string Label => L.Text(LabelKey);
        public override string ToString() => Label;
    }
    public static IReadOnlyList<ThemeChoice> Choices { get; } = new[]
    {
        new ThemeChoice("Light", "ThemeLight", false),
        new ThemeChoice("Dark", "ThemeDark", true),
        new ThemeChoice("Orange", "ThemeOrange", true),
        new ThemeChoice("Spotify", "ThemeSpotify", true)
    };

    public static bool IsDark(string theme) => Choices.FirstOrDefault(x => x.Id == theme)?.IsDark == true;

    public static void Apply(string theme)
    {
        var colors = Colors(theme);
        foreach (var (key, color) in colors)
        {
            var brush = (SolidColorBrush)new BrushConverter().ConvertFromString(color)!;
            brush.Freeze(); Application.Current.Resources[key] = brush;
        }
    }

    public static IReadOnlyDictionary<string, string> Colors(string theme) => theme switch
    {
        "Dark" => new Dictionary<string, string>
        {
            ["BackgroundBrush"] = "#171C23", ["PanelBrush"] = "#252D38", ["RaisedBrush"] = "#364352",
            ["DeepBrush"] = "#1D252F", ["TextBrush"] = "#F5F8FC", ["MetricBrush"] = "#FFFFFF", ["MutedBrush"] = "#C7D3E0",
            ["SubtleBrush"] = "#AABDD0", ["BorderBrush"] = "#52657A", ["AccentBrush"] = "#90E8FF",
            ["HeaderBrush"] = "#202833", ["InputBrush"] = "#1D252F", ["InputBorderBrush"] = "#7D93AA",
            ["WarningBrush"] = "#FFDB7E", ["DangerBrush"] = "#FFA9A4", ["SuccessBrush"] = "#99ECC0",
            ["SuccessSurfaceBrush"] = "#1E3F32", ["WarningSurfaceBrush"] = "#443619", ["AccentButtonBrush"] = "#254C5A",
            ["HoverBrush"] = "#465A70", ["SelectionBrush"] = "#345365"
        },
        "Orange" => new Dictionary<string, string>
        {
            ["BackgroundBrush"] = "#211B17", ["PanelBrush"] = "#342920", ["RaisedBrush"] = "#4C3828",
            ["DeepBrush"] = "#291F19", ["TextBrush"] = "#FFF6E9", ["MetricBrush"] = "#FFFFFF", ["MutedBrush"] = "#E7D1B8",
            ["SubtleBrush"] = "#CDB99E", ["BorderBrush"] = "#876043", ["AccentBrush"] = "#FFB15C",
            ["HeaderBrush"] = "#2C2119", ["InputBrush"] = "#291F19", ["InputBorderBrush"] = "#BF8E5C",
            ["WarningBrush"] = "#FFE094", ["DangerBrush"] = "#FFB0A6", ["SuccessBrush"] = "#AAF0BA",
            ["SuccessSurfaceBrush"] = "#243B26", ["WarningSurfaceBrush"] = "#473614", ["AccentButtonBrush"] = "#66401E",
            ["HoverBrush"] = "#64472E", ["SelectionBrush"] = "#6A431F"
        },
        // Neutral colors verified in the decoded Windows Mobile static_stylemodel.xml.
        // Only the palette is adapted; no original graphics, fonts or controls are bundled.
        "Spotify" => new Dictionary<string, string>
        {
            ["BackgroundBrush"] = "#313131", ["PanelBrush"] = "#333333", ["RaisedBrush"] = "#444444",
            ["DeepBrush"] = "#282828", ["TextBrush"] = "#FFFFFF", ["MetricBrush"] = "#FFFFFF", ["MutedBrush"] = "#C5C5C5",
            ["SubtleBrush"] = "#CCCCCC", ["BorderBrush"] = "#777777", ["AccentBrush"] = "#BDF39B",
            ["HeaderBrush"] = "#282828", ["InputBrush"] = "#282828", ["InputBorderBrush"] = "#999999",
            ["WarningBrush"] = "#FFE094", ["DangerBrush"] = "#FFB0A6", ["SuccessBrush"] = "#BDFFC6",
            ["SuccessSurfaceBrush"] = "#25432C", ["WarningSurfaceBrush"] = "#443817", ["AccentButtonBrush"] = "#34502A",
            ["HoverBrush"] = "#535353", ["SelectionBrush"] = "#3F5835"
        },
        _ => new Dictionary<string, string>
        {
            ["BackgroundBrush"] = "#EEF3F7", ["PanelBrush"] = "#FFFFFF", ["RaisedBrush"] = "#DDE8F0",
            ["DeepBrush"] = "#F0F6FA", ["TextBrush"] = "#172B3F", ["MetricBrush"] = "#102438", ["MutedBrush"] = "#415B70",
            ["SubtleBrush"] = "#4B6276", ["BorderBrush"] = "#BBCBD9", ["AccentBrush"] = "#005DA8",
            ["HeaderBrush"] = "#E0EAF2", ["InputBrush"] = "#FFFFFF", ["InputBorderBrush"] = "#9BAFBF",
            ["WarningBrush"] = "#805000", ["DangerBrush"] = "#AA2424", ["SuccessBrush"] = "#14643D",
            ["SuccessSurfaceBrush"] = "#DEF1E7", ["WarningSurfaceBrush"] = "#FFF0CA", ["AccentButtonBrush"] = "#D7EAFE",
            ["HoverBrush"] = "#C5DBEF", ["SelectionBrush"] = "#CCE3F7"
        }
    };
}
