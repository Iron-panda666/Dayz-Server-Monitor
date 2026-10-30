namespace MagDeer.App;

public static class Localization
{
    public static void Apply(string language)
    {
        L.SetLanguage(language);
        foreach (var key in L.Texts.Keys) Application.Current.Resources["L." + key] = L.Text(key);
        System.Globalization.CultureInfo.CurrentCulture = L.Culture;
        System.Globalization.CultureInfo.CurrentUICulture = L.Culture;
    }
}
