using System.Text.Json.Serialization;

namespace MagDeer.Core;

public sealed class WorldClock
{
    public string City { get; set; } = "London";
    public string CountryCode { get; set; } = "GB";
    public string TimeZoneId { get; set; } = "Europe/London";
    public bool Enabled { get; set; } = true;
    [JsonIgnore] public string Label => string.IsNullOrWhiteSpace(CountryCode) ? City : $"{City} · {CountryCode}";
    public WorldClock Copy() => new() { City = City, CountryCode = CountryCode, TimeZoneId = TimeZoneId, Enabled = Enabled };
    public TimeZoneInfo ResolveZone() => TimeZoneInfo.FindSystemTimeZoneById(TimeZoneId);
    public DateTimeOffset At(DateTimeOffset instant) => TimeZoneInfo.ConvertTime(instant, ResolveZone());
    public override string ToString() => $"{Label} / {TimeZoneId}" + (Enabled ? "" : " · " + L.Text("Disabled"));
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(City) || City.Length > 40 || CountryCode is null || CountryCode.Length > 8 ||
            string.IsNullOrWhiteSpace(TimeZoneId)) throw new ArgumentException("WorldClockInvalid");
        try { ResolveZone(); }
        catch (Exception ex) when (ex is TimeZoneNotFoundException or InvalidTimeZoneException)
        { throw new ArgumentException("WorldClockZoneInvalid", ex); }
    }

    public static IReadOnlyList<WorldClock> Presets { get; } = new[]
    {
        new WorldClock { City = "Prague", CountryCode = "CZ", TimeZoneId = "Europe/Prague" },
        new WorldClock { City = "London", CountryCode = "GB", TimeZoneId = "Europe/London" },
        new WorldClock { City = "Berlin", CountryCode = "DE", TimeZoneId = "Europe/Berlin" },
        new WorldClock { City = "Warsaw", CountryCode = "PL", TimeZoneId = "Europe/Warsaw" },
        new WorldClock { City = "Madrid", CountryCode = "ES", TimeZoneId = "Europe/Madrid" },
        new WorldClock { City = "New York", CountryCode = "US", TimeZoneId = "America/New_York" },
        new WorldClock { City = "Los Angeles", CountryCode = "US", TimeZoneId = "America/Los_Angeles" },
        new WorldClock { City = "Tokyo", CountryCode = "JP", TimeZoneId = "Asia/Tokyo" },
        new WorldClock { City = "Sydney", CountryCode = "AU", TimeZoneId = "Australia/Sydney" },
        new WorldClock { City = "Dubai", CountryCode = "AE", TimeZoneId = "Asia/Dubai" },
        new WorldClock { City = "Delhi", CountryCode = "IN", TimeZoneId = "Asia/Kolkata" },
        new WorldClock { City = "UTC", CountryCode = "", TimeZoneId = "UTC" }
    };
}
