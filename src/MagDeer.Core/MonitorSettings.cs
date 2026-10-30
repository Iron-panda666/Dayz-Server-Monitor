using System.Globalization;
using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace MagDeer.Core;

public sealed class MonitorSettings
{
    public string ServerName { get; set; } = "MAG MiddleAgedGamers #2 PVE DEER";
    public string IpAddress { get; set; } = "217.182.198.57";
    public int GamePort { get; set; } = 2302;
    public int QueryPort { get; set; } = 2303;
    public string[] RestartTimes { get; set; } = ["03:00", "07:00", "11:00", "15:00", "19:00", "23:00"];
    public bool ScheduleConfirmed { get; set; }
    public int RestartEveryHours { get; set; } = 4;
    public string RestartAnchor { get; set; } = "03:00";
    public int LogoutWarningMinutes { get; set; } = 10;
    public int KickWarningMinutes { get; set; } = 5;
    public bool? KickBeforeRestart { get; set; }
    public bool CountdownToKick { get; set; }
    [JsonIgnore] public bool HasKnownKick => KickPlan.Known(KickBeforeRestart, IpAddress, GamePort, QueryPort);
    public List<ServerProfile> SavedServers { get; set; } = [];
    public int ServerCatalogVersion { get; set; }
    public string Theme { get; set; } = "Light";
    public string BackgroundStyle { get; set; } = "Theme";
    public string BackgroundImagePath { get; set; } = "";
    public bool AeroGlass { get; set; } = true;
    public bool WidgetMode { get; set; }
    public string DisplayProfile { get; set; } = "Auto";
    public bool MinimizeOnClose { get; set; }
    public string MinimizeTarget { get; set; } = "Tray";
    public string Language { get; set; } = "en";
    public List<WorldClock> WorldClocks { get; set; } = [];
    public bool NtpEnabled { get; set; }
    public string NtpServer { get; set; } = "time.cloudflare.com";
    public int NtpSyncIntervalMinutes { get; set; } = 60;
    public int NtpTimeoutSeconds { get; set; } = 4;
    public int NormalIntervalSeconds { get; set; } = 300;
    public int BeforeRestartIntervalSeconds { get; set; } = 30;
    public int RestartIntervalSeconds { get; set; } = 15;
    public int QueryTimeoutSeconds { get; set; } = 4;
    public int FailureThreshold { get; set; } = 3;
    public int HistoryRetentionDays { get; set; } = 90;
    public double ServerOffsetHours { get; set; } = -1;
    public bool ShowServerTime { get; set; } = true;
    public bool ShowGameSession { get; set; } = true;
    public bool ExportEnabled { get; set; }
    public bool MonitorOnlineServices { get; set; }
    public string ExportAddress { get; set; } = "127.0.0.1";
    public int ExportPort { get; set; } = 9187;
    public bool Use24HourClock { get; set; } = true;
    public bool SoundAlerts { get; set; }
    public bool WindowsNotifications { get; set; }
    public bool VisualAlerts { get; set; } = true;
    public bool AlwaysOnTop { get; set; }
    public bool CompactMode { get; set; }
    public bool Fullscreen { get; set; }
    public bool StartWithWindows { get; set; }
    public string MonitorDevice { get; set; } = "";
    public double Left { get; set; } = 80;
    public double Top { get; set; } = 80;
    public double Width { get; set; } = 850;
    public double Height { get; set; } = 890;
    public bool Maximized { get; set; }

    public MonitorSettings Copy() => JsonSerializer.Deserialize<MonitorSettings>(JsonSerializer.Serialize(this))!;

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(ServerName) || ServerName.Length > 180)
            throw new ArgumentException("Název serveru musí mít 1–180 znaků.");
        if (!IPAddress.TryParse(IpAddress, out var address))
            throw new ArgumentException("Zadejte platnou IP adresu serveru.");
        if (address.Equals(IPAddress.Any) || address.Equals(IPAddress.IPv6Any) || address.Equals(IPAddress.Broadcast))
            throw new ArgumentException("Zadejte konkrétní IP adresu serveru.");
        if (GamePort is < 1 or > 65535 || QueryPort is < 1 or > 65535)
            throw new ArgumentException("Porty musí být v rozsahu 1–65535.");
        if (RestartTimes is null || RestartTimes.Length is < 1 or > 48 ||
            RestartTimes.Any(t => !TimeOnly.TryParseExact(t, "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out _)) ||
            RestartTimes.Distinct().Count() != RestartTimes.Length)
            throw new ArgumentException("Plán musí obsahovat 1–48 různých časů ve formátu HH:mm.");
        if (NormalIntervalSeconds is < 10 or > 86400 || BeforeRestartIntervalSeconds is < 5 or > 3600 ||
            RestartIntervalSeconds is < 5 or > 3600 || QueryTimeoutSeconds is < 1 or > 30)
            throw new ArgumentException("Intervaly: běžný 10–86400 s, před/během restartu 5–3600 s, timeout 1–30 s.");
        if (FailureThreshold is < 2 or > 20 || HistoryRetentionDays is < 1 or > 3650)
            throw new ArgumentException("Práh selhání musí být 2–20, uchování historie 1–3650 dnů.");
        if (LogoutWarningMinutes is < 1 or > 120 || KickWarningMinutes < 1 || KickWarningMinutes >= LogoutWarningMinutes)
            throw new ArgumentException("Upomínka odhlášení musí být 2–120 minut, očekávaný kick 1 až o minutu méně než odhlášení.");
        if (RestartEveryHours is < 1 or > 24 || !TimeOnly.TryParseExact(RestartAnchor, "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out _))
            throw new ArgumentException("Interval restartu musí být 1–24 hodin a výchozí čas ve formátu HH:mm.");
        if (Theme is not ("Light" or "Dark" or "Orange" or "Spotify" or "Ocean" or "XP" or "Aero" or "Anime" or "Matrix" or "Violet"))
            throw new ArgumentException("ThemeInvalid");
        if (BackgroundStyle is not ("Theme" or "None" or "Sakura" or "Fuji" or "Custom") || BackgroundImagePath is null || BackgroundImagePath.Length > 1024)
            throw new ArgumentException("BackgroundInvalid");
        if (!DisplayProfiles.All.Any(x => x.Id == DisplayProfile)) throw new ArgumentException("ProfileInvalid");
        if (MinimizeTarget is not ("Tray" or "Taskbar")) throw new ArgumentException("CloseTargetInvalid");
        if (Language is not ("en" or "cs" or "pl" or "de" or "es")) throw new ArgumentException("LanguageInvalid");
        if (WorldClocks is null || WorldClocks.Count > 8 || WorldClocks.Any(x => x is null)) throw new ArgumentException("WorldClocksLimit");
        foreach (var clock in WorldClocks) clock.Validate();
        if (string.IsNullOrWhiteSpace(NtpServer) || NtpServer.Length > 253 || Uri.CheckHostName(NtpServer) == UriHostNameType.Unknown ||
            NtpSyncIntervalMinutes is < 2 or > 1440 || NtpTimeoutSeconds is < 1 or > 15)
            throw new ArgumentException("NtpValidation");
        if (SavedServers is null || SavedServers.Count > 50 || SavedServers.Any(x => x is null))
            throw new ArgumentException("Lze uložit nejvýše 50 serverů.");
        foreach (var profile in SavedServers) profile.Validate();
        if (SavedServers.Count(p => p.MonitorInBackground) > 16) throw new ArgumentException("BackgroundLimit");
        if (!IPAddress.TryParse(ExportAddress, out var exportAddress) || exportAddress.AddressFamily != System.Net.Sockets.AddressFamily.InterNetwork ||
            exportAddress.Equals(IPAddress.Any) || exportAddress.Equals(IPAddress.Broadcast) || ExportPort is < 1024 or > 65535)
            throw new ArgumentException("ExportInvalid");
        if (!double.IsFinite(ServerOffsetHours) || ServerOffsetHours is < -24 or > 24)
            throw new ArgumentException("Posun serverového času musí být −24 až +24 hodin.");
        if (!double.IsFinite(Left) || !double.IsFinite(Top) || !double.IsFinite(Width) || !double.IsFinite(Height) ||
            Width is < 128 or > 16000 || Height is < 96 or > 16000)
            throw new ArgumentException("Neplatná poloha nebo velikost okna.");
    }

    [JsonIgnore] public string EndpointKey => $"{IpAddress}:{QueryPort}";
}
