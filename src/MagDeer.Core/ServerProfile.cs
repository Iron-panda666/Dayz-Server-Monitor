namespace MagDeer.Core;

public sealed class ServerProfile
{
    public string Name { get; set; } = "Nový server";
    public string IpAddress { get; set; } = "217.182.198.57";
    public int GamePort { get; set; } = 2302;
    public int QueryPort { get; set; } = 2303;
    public string[] RestartTimes { get; set; } = ["03:00", "07:00", "11:00", "15:00", "19:00", "23:00"];
    public bool ScheduleConfirmed { get; set; }
    public int RestartEveryHours { get; set; } = 4;
    public string RestartAnchor { get; set; } = "03:00";
    public double ServerOffsetHours { get; set; } = -1;
    public int LogoutWarningMinutes { get; set; } = 10;
    public int KickWarningMinutes { get; set; } = 5;
    public override string ToString() => $"{Name} · {IpAddress}:{GamePort}";
    public void ApplyTo(MonitorSettings settings)
    {
        settings.ServerName = Name; settings.IpAddress = IpAddress; settings.GamePort = GamePort; settings.QueryPort = QueryPort;
        settings.RestartTimes = (string[])RestartTimes.Clone(); settings.ScheduleConfirmed = ScheduleConfirmed;
        settings.RestartEveryHours = RestartEveryHours; settings.RestartAnchor = RestartAnchor; settings.ServerOffsetHours = ServerOffsetHours;
        settings.LogoutWarningMinutes = LogoutWarningMinutes; settings.KickWarningMinutes = KickWarningMinutes;
    }
    public static ServerProfile From(MonitorSettings settings) => new()
    {
        Name = settings.ServerName, IpAddress = settings.IpAddress, GamePort = settings.GamePort, QueryPort = settings.QueryPort,
        RestartTimes = (string[])settings.RestartTimes.Clone(), ScheduleConfirmed = settings.ScheduleConfirmed,
        RestartEveryHours = settings.RestartEveryHours, RestartAnchor = settings.RestartAnchor, ServerOffsetHours = settings.ServerOffsetHours,
        LogoutWarningMinutes = settings.LogoutWarningMinutes, KickWarningMinutes = settings.KickWarningMinutes
    };
    public void Validate()
    {
        if (RestartTimes is null) throw new ArgumentException("Profil serveru nemá restartovací plán.");
        var settings = new MonitorSettings(); ApplyTo(settings); settings.Validate();
    }
}
