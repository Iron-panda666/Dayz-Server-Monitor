namespace MagDeer.Core;

public enum CountdownStage { Normal, Yellow, Orange, FadeToRed, Red }
public readonly record struct CountdownVisual(CountdownStage Stage, double RedMix, double Opacity);

public static class CountdownVisuals
{
    public static CountdownVisual Evaluate(double seconds, DateTimeOffset now)
    {
        if (!double.IsFinite(seconds) || seconds > 900) return new(CountdownStage.Normal, 0, 1);
        if (seconds > 600) return new(CountdownStage.Yellow, 0, 1);
        if (seconds > 300) return new(CountdownStage.Orange, 0, 1);
        if (seconds > 30) return new(CountdownStage.FadeToRed, (300 - seconds) / 270, 1);
        return new(CountdownStage.Red, 1, seconds > 0 && now.ToUnixTimeSeconds() % 2 != 0 ? .78 : 1);
    }

    public static string Color(CountdownVisual visual, bool dark)
    {
        var yellow = dark ? (255, 218, 88) : (148, 102, 0);
        var orange = dark ? (255, 154, 61) : (185, 82, 0);
        var red = dark ? (255, 91, 82) : (190, 40, 40);
        if (visual.Stage == CountdownStage.Normal) throw new ArgumentException("Use the theme accent for the normal stage.");
        var (r, g, b) = visual.Stage == CountdownStage.Yellow ? yellow : visual.Stage == CountdownStage.Red ? red : orange;
        if (visual.Stage == CountdownStage.FadeToRed)
        {
            r = (int)Math.Round(orange.Item1 + (red.Item1 - orange.Item1) * visual.RedMix, MidpointRounding.AwayFromZero);
            g = (int)Math.Round(orange.Item2 + (red.Item2 - orange.Item2) * visual.RedMix, MidpointRounding.AwayFromZero);
            b = (int)Math.Round(orange.Item3 + (red.Item3 - orange.Item3) * visual.RedMix, MidpointRounding.AwayFromZero);
        }
        return $"#{r:X2}{g:X2}{b:X2}";
    }
}

public static class KickPlan
{
    // User-observed lead for this MAG DEER endpoint only. Other servers remain unknown.
    public static bool Known(bool? configured, string ip, int gamePort, int queryPort) => configured ??
        (ip == "217.182.198.57" && gamePort == 2302 && queryPort == 2303);
    public static DateTimeOffset Target(MonitorSettings settings, DateTimeOffset restart) =>
        settings.CountdownToKick && settings.HasKnownKick ? restart.AddMinutes(-settings.KickWarningMinutes) : restart;
}
