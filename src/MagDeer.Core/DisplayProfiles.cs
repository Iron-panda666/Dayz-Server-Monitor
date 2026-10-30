namespace MagDeer.Core;

/// <summary>Window size presets in physical pixels, not display-driver modes.</summary>
public sealed record DisplayProfile(string Id, string Name, int PixelWidth, int PixelHeight)
{
    public string Label => Id == "Auto" ? L.Text("ProfileAuto") : $"{Name} · {PixelWidth} × {PixelHeight}";
    public override string ToString() => Label;
}

public static class DisplayProfiles
{
    public static IReadOnlyList<DisplayProfile> All { get; } = Create();
    private static IReadOnlyList<DisplayProfile> Create()
    {
        var result = new List<DisplayProfile> { new("Auto", "Auto", 0, 0), new("Desktop", "Desktop", 850, 890) };
        foreach (var (id, name, w, h) in new (string, string, int, int)[] {
            ("Mini", "Mini 3.5\"", 480, 320), ("Small", "Small 5\"", 800, 480),
            ("Seven", "HDMI 7\" / 10.1\"", 1024, 600), ("HD", "HD 7\" / 10.1\"", 1280, 800),
            ("Bar79", "Bar 7.9\"", 1280, 400), ("Bar88", "Bar 8.8\"", 1920, 480),
            ("Bar119", "Bar 11.9\"", 1480, 320), ("Ribbon", "Micro ribbon", 480, 128),
            ("Laptop", "Laptop", 1366, 768), ("FullHD", "Full HD", 1920, 1080),
            ("WUXGA", "WUXGA", 1920, 1200), ("QHD", "QHD", 2560, 1440) })
        {
            result.Add(new(id + "L", name + " / L", w, h));
            result.Add(new(id + "P", name + " / P", h, w));
        }
        result.Add(new("Square", "Square", 720, 720));
        return result;
    }
}
