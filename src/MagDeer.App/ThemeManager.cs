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
        new ThemeChoice("Spotify", "ThemeSpotify", true),
        new ThemeChoice("Ocean", "ThemeOcean", true),
        new ThemeChoice("XP", "ThemeXP", false),
        new ThemeChoice("Aero", "ThemeAero", false),
        new ThemeChoice("Anime", "ThemeAnime", true),
        new ThemeChoice("Matrix", "ThemeMatrix", true),
        new ThemeChoice("Violet", "ThemeViolet", true)
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
        Brush caption = theme is "XP" or "Aero"
            ? new LinearGradientBrush((Color)ColorConverter.ConvertFromString(theme == "XP" ? "#A8CFFF" : "#C5E6F6"),
                (Color)ColorConverter.ConvertFromString(colors["HeaderBrush"]), 90)
            : (Brush)Application.Current.Resources["HeaderBrush"];
        caption.Freeze(); Application.Current.Resources["CaptionBackgroundBrush"] = caption;
    }

    private static IReadOnlyDictionary<string, string> Tint(string basis, params (string Key, string Color)[] values)
    {
        var colors = new Dictionary<string, string>(Colors(basis));
        foreach (var (key, color) in values) colors[key] = color;
        return colors;
    }

    public static IReadOnlyDictionary<string, string> Colors(string theme) => theme switch
    {
        "Ocean" => Tint("Dark", ("BackgroundBrush", "#0B1D30"), ("PanelBrush", "#142E48"), ("HeaderBrush", "#10253D"),
            ("AccentBrush", "#7CD8FF"), ("BorderBrush", "#406D92"), ("DeepBrush", "#0E2239"), ("AccentButtonBrush", "#234F72")),
        "XP" => Tint("Light", ("BackgroundBrush", "#ECE9D8"), ("PanelBrush", "#FFFEF5"), ("HeaderBrush", "#DBE9FB"),
            ("RaisedBrush", "#E6EDD6"), ("AccentBrush", "#174CA5"), ("BorderBrush", "#8AA4C2"), ("SuccessBrush", "#235C19"), ("AccentButtonBrush", "#D7E8CA")),
        "Aero" => Tint("Light", ("BackgroundBrush", "#E8F3F9"), ("PanelBrush", "#F8FCFF"), ("HeaderBrush", "#E3F4FF"),
            ("RaisedBrush", "#DCEBF6"), ("AccentBrush", "#075D91"), ("BorderBrush", "#99BED7"), ("DeepBrush", "#E6F2FA")),
        "Anime" => Tint("Dark", ("BackgroundBrush", "#231A30"), ("PanelBrush", "#34243F"), ("HeaderBrush", "#2B2038"),
            ("RaisedBrush", "#4D365B"), ("AccentBrush", "#FFB4DF"), ("BorderBrush", "#896391"), ("DeepBrush", "#291D34"), ("MetricBrush", "#FFF4FC"), ("AccentButtonBrush", "#543448")),
        "Matrix" => Tint("Dark", ("BackgroundBrush", "#030A06"), ("PanelBrush", "#091A10"), ("HeaderBrush", "#07140C"),
            ("RaisedBrush", "#173924"), ("DeepBrush", "#06110A"), ("TextBrush", "#BDFFD0"), ("MetricBrush", "#CAFFD8"),
            ("MutedBrush", "#9DDBAD"), ("SubtleBrush", "#8EC69D"), ("AccentBrush", "#63FF94"), ("BorderBrush", "#326244"),
            ("InputBrush", "#0B2114"), ("InputBorderBrush", "#59936C"), ("HoverBrush", "#244330"), ("SelectionBrush", "#21422D"), ("AccentButtonBrush", "#193A25")),
        "Violet" => Tint("Dark", ("BackgroundBrush", "#1B1730"), ("PanelBrush", "#2B2545"), ("HeaderBrush", "#211C39"),
            ("AccentBrush", "#C8B4FF"), ("BorderBrush", "#746294"), ("DeepBrush", "#201B36"), ("AccentButtonBrush", "#45345D")),
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
