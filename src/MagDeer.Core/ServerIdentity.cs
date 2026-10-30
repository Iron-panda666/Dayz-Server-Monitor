namespace MagDeer.Core;

public sealed record PublishedServerAddress(string Ip, int GamePort, string Source);

/// <summary>Address provenance from MAG-owned pages, checked 2026-10-30; not a cryptographic identity guarantee.</summary>
public static class ServerIdentity
{
    public const string CheckedOn = "2026-10-30";
    public static IReadOnlyList<PublishedServerAddress> MagAddresses { get; } = new[]
    {
        new PublishedServerAddress("54.36.109.179", 2302, "https://www.middleagedgamers.co.uk/forum/index.php?topic=16729.0"),
        new PublishedServerAddress("217.182.198.57", 2302, "https://www.middleagedgamers.co.uk/forum/index.php?topic=16729.0"),
        new PublishedServerAddress("57.129.39.17", 2302, "https://www.middleagedgamers.co.uk/forum/index.php?topic=16729.0"),
        new PublishedServerAddress("5.252.100.201", 2602, "https://www.middleagedgamers.co.uk/new/dayz-direct-connect/")
    };
    public static PublishedServerAddress? MagSource(string ip, int gamePort) => MagAddresses.FirstOrDefault(x => x.Ip == ip && x.GamePort == gamePort);
    public static bool UsesMagName(string name) => name.Contains("MiddleAgedGamers", StringComparison.OrdinalIgnoreCase)
        || name.StartsWith("MAG ", StringComparison.OrdinalIgnoreCase) || name.StartsWith("OFFICIAL MAG ", StringComparison.OrdinalIgnoreCase);
    public static string Note(ServerProfile profile) => MagSource(profile.IpAddress, profile.GamePort) is not null
        ? L.Text("MagAddressListed", CheckedOn) : UsesMagName(profile.Name) ? L.Text("MagAddressUnverified") : "";
    public static IOrderedEnumerable<ServerProfile> Sort(IEnumerable<ServerProfile> profiles) => profiles.OrderBy(p => p.Name, StringComparer.CurrentCultureIgnoreCase)
        .ThenBy(p => p.IpAddress, StringComparer.Ordinal).ThenBy(p => p.GamePort).ThenBy(p => p.QueryPort);
}
