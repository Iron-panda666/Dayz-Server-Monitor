using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace MagDeer.App;

public partial class App : Application
{
    private Mutex? mutex;
    private bool ownsMutex;
    public static string DataRoot { get; private set; } = AppContext.BaseDirectory;
    public static string ConfigPath => Path.Combine(DataRoot, "config.json");
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        var args = e.Args;
        var dataArg = Array.IndexOf(args, "--data-dir");
        if (dataArg >= 0 && dataArg + 1 < args.Length) DataRoot = Path.GetFullPath(args[dataArg + 1]);
        var identity = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(DataRoot.ToUpperInvariant())))[..20];
        // Keep the existing identity so old and renamed builds cannot share one data directory.
        mutex = new Mutex(true, @"Local\MagDeerMonitor-" + identity, out ownsMutex);
        if (!ownsMutex)
        {
            MessageBox.Show(L.Text("AlreadyRunning"), "Dayz Server Monitor", MessageBoxButton.OK, MessageBoxImage.Information);
            Shutdown(); return;
        }
        MonitorSettings settings;
        try
        {
            Directory.CreateDirectory(DataRoot);
            settings = JsonStorage.Read<MonitorSettings>(ConfigPath);
            Localization.Apply(settings.Language);
            settings.Validate();
            bool catalogChanged = ServerCatalog.MergeOnce(settings);
            if (!File.Exists(ConfigPath) || catalogChanged) JsonStorage.Write(ConfigPath, settings);
            // Verify data write access before starting a long-running portable monitor.
            var probe = Path.Combine(DataRoot, ".write-test");
            File.WriteAllText(probe, ""); File.Delete(probe);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or ArgumentException)
        {
            MessageBox.Show(L.Text("ConfigError", L.Error(ex), ConfigPath), "Dayz Server Monitor", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(1); return;
        }
        if (settings.SavedServers.Count == 0) settings.SavedServers.Add(ServerProfile.From(settings));
        ThemeManager.Apply(settings.Theme);
        MainWindow = new MainWindow(settings);
        MainWindow.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        if (ownsMutex) mutex?.ReleaseMutex();
        mutex?.Dispose();
        base.OnExit(e);
    }
}
