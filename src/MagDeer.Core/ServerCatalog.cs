namespace MagDeer.Core;

/// <summary>Public DayZ endpoints, checked via A2S_INFO on 2026-10-29. No web dependency at runtime.</summary>
public static class ServerCatalog
{
    public const int Version = 2;
    public static IReadOnlyList<ServerProfile> Create() =>
    [
        Profile("MAG MiddleAgedGamers #1 PVE CHERNO", "54.36.109.179", 2302, 2303),
        Profile("MAG MiddleAgedGamers #2 PVE DEER", "217.182.198.57", 2302, 2303),
        Profile("MAG MiddleAgedGamers #3 Vanilla+", "57.129.39.17", 2302, 2303),
        Profile("MAG MiddleAgedGamers Test", "5.252.100.201", 2602, 2603),
        Profile("Sunnyvale #1 | Livonia | PvP Clans", "172.111.51.75", 2302, 2303),
        Profile("Sunnyvale #2 | Chernarus | PvP Solo/Duo/Trio", "172.111.51.75", 2402, 2403),
        Profile("Sunnyvale #3 | Chernarus | PvE w/ PvP Zones", "172.111.51.90", 2302, 2303),
        Profile("Sunnyvale #4 | Banov | PvE w/ PvP Zones", "172.111.51.90", 2502, 2503),
        Profile("Sunnyvale #5 | Deer Isle | PvP 8 Man", "172.111.51.169", 2302, 2303),
        Profile("Sunnyvale #6 | Chernarus | PvP Clans", "172.111.51.75", 2502, 2503),
        Profile("Sunnyvale #7 | Frostline Sakhal | PvE w/ PvP Zones", "172.111.51.90", 2402, 2403),
        Profile("Sunnyvale #8 | In Development", "172.111.51.169", 2402, 2403),
        Profile("Sunnyvale #9 | Namalsk | PvE w/ PvP Zones", "172.111.51.80", 2402, 2403),
        Profile("Sunnyvale #10 | Livonia | PvE w/ PvP Zones", "172.111.51.135", 2302, 2303),
        Profile("Sunnyvale #11 | Chernarus | PvE w/ PvP Zones", "172.111.51.135", 2402, 2403),
        Profile("Sunnyvale #12 | Deer Isle | PVE w/ PVP Zones", "172.111.51.80", 2302, 2303)
    ];

    private static ServerProfile Profile(string name, string ip, int game, int query) => new()
    {
        Name = name, IpAddress = ip, GamePort = game, QueryPort = query,
        // No provider restart plan has been verified. The UI must retain its unconfirmed badge.
        ScheduleConfirmed = false
    };

    public static bool MergeOnce(MonitorSettings settings)
    {
        if (settings.ServerCatalogVersion >= Version) return false;
        if (settings.ServerCatalogVersion >= 1)
        {
            // Do not repopulate a user's deliberately removed presets on catalog upgrades.
            foreach (var profile in settings.SavedServers)
                if (profile.IpAddress == "57.129.39.17" && profile.GamePort == 2302 && profile.Name == "OFFICIAL MAG MiddleAgedGamers #3 Vanilla+")
                    profile.Name = "MAG MiddleAgedGamers #3 Vanilla+";
            if (settings.IpAddress == "57.129.39.17" && settings.GamePort == 2302 && settings.ServerName == "OFFICIAL MAG MiddleAgedGamers #3 Vanilla+")
                settings.ServerName = "MAG MiddleAgedGamers #3 Vanilla+";
            settings.ServerCatalogVersion = Version;
            return true;
        }
        if (!settings.SavedServers.Any(p => p.IpAddress == settings.IpAddress && p.QueryPort == settings.QueryPort)
            && settings.SavedServers.Count < 50)
            settings.SavedServers.Add(ServerProfile.From(settings));
        foreach (var profile in Create())
            if (settings.SavedServers.Count < 50 && !settings.SavedServers.Any(p => p.IpAddress == profile.IpAddress && p.QueryPort == profile.QueryPort))
                settings.SavedServers.Add(profile);
        settings.ServerCatalogVersion = Version;
        return true;
    }
}
