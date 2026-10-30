namespace MagDeer.Core;

/// <summary>Transactional settings-dialog edits; identity survives a rename or endpoint change.</summary>
public static class ServerProfileEditor
{
    public static void Save(List<ServerProfile> profiles, ServerProfile? edited, ServerProfile replacement)
    {
        replacement.Validate();
        int index = edited is null ? -1 : profiles.FindIndex(x => ReferenceEquals(x, edited));
        if (edited is not null && index < 0) throw new ArgumentException("ServerProfileMissing");
        if (profiles.Where((_, i) => i != index).Any(p => p.Name.Equals(replacement.Name, StringComparison.OrdinalIgnoreCase)))
            throw new ArgumentException("ServerNameExists");
        if (index >= 0) profiles[index] = replacement;
        else
        {
            if (profiles.Count >= 50) throw new ArgumentException("ServerLimit");
            profiles.Add(replacement);
        }
    }
    public static ServerProfile Remove(List<ServerProfile> profiles, ServerProfile selected)
    {
        if (profiles.Count <= 1) throw new ArgumentException("LastServer");
        int index = profiles.FindIndex(x => ReferenceEquals(x, selected));
        if (index < 0) throw new ArgumentException("ServerProfileMissing");
        profiles.RemoveAt(index);
        return profiles[Math.Min(index, profiles.Count - 1)];
    }
}
