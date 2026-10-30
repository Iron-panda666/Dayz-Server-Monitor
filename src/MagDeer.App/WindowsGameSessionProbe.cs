using System.ComponentModel;
using System.Diagnostics;

namespace MagDeer.App;

/// <summary>Windows process metadata only. No memory reads, game handles with VM rights, or Steam access.</summary>
internal static class WindowsGameSessionProbe
{
    public static GameSessionObservation Read()
    {
        var candidates = new List<GameProcessCandidate>();
        bool unavailable = false;
        using var current = Process.GetCurrentProcess();
        int session = current.SessionId;
        foreach (var name in GameSessionProbe.ProcessNames)
        {
            Process[] processes;
            try { processes = Process.GetProcessesByName(name); }
            catch (Exception ex) when (ex is Win32Exception or InvalidOperationException) { unavailable = true; continue; }
            foreach (var process in processes)
            using (process)
            {
                try
                {
                    if (process.SessionId != session) continue;
                    DateTimeOffset? start = null;
                    try { start = new DateTimeOffset(process.StartTime.ToUniversalTime()); }
                    catch (Win32Exception) { /* Permission denied is unknown, never a fabricated zero. */ }
                    candidates.Add(new(process.Id, name, session, start));
                }
                catch (InvalidOperationException) { /* The game exited during enumeration. */ }
                catch (Win32Exception) { unavailable = true; }
            }
        }
        return unavailable ? new(GameSessionState.Unavailable) : GameSessionProbe.Select(candidates, session);
    }
}
