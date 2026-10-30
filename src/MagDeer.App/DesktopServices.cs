using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Interop;
using Microsoft.Win32;
using Forms = System.Windows.Forms;

namespace MagDeer.App;

public static class DesktopServices
{
    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowPos(IntPtr hwnd, IntPtr after, int x, int y, int width, int height, uint flags);
    [DllImport("user32.dll")]
    private static extern uint GetDpiForWindow(IntPtr hwnd);
    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);

    public static void TitleBarTheme(Window window, bool dark)
    {
        int value = dark ? 1 : 0;
        var handle = new WindowInteropHelper(window).Handle;
        if (DwmSetWindowAttribute(handle, 20, ref value, sizeof(int)) != 0)
            DwmSetWindowAttribute(handle, 19, ref value, sizeof(int));
    }
    public static Forms.Screen[] Screens => Forms.Screen.AllScreens;
    public static void OpenFolder(string path) => Process.Start(new ProcessStartInfo("explorer.exe", path) { UseShellExecute = true });
    public static void SetStartup(bool enabled)
    {
        // Retain the registry value name to update/remove existing startup registrations.
        using var key = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run");
        if (enabled)
        {
            var exe = Environment.ProcessPath ?? throw new IOException(L.Text("ExePathError"));
            key.SetValue("MAGDeerMonitor", $"\"{exe}\" --data-dir \"{App.DataRoot}\"");
        }
        else key.DeleteValue("MAGDeerMonitor", false);
    }

    public static void PlaceWindow(Window window, MonitorSettings s, bool forceMonitor)
    {
        var screen = Screens.FirstOrDefault(x => x.DeviceName == s.MonitorDevice) ?? Forms.Screen.PrimaryScreen ?? Screens[0];
        var area = screen.WorkingArea;
        var handle = new WindowInteropHelper(window).Handle;
        // Enter the target screen first so per-monitor DPI is resolved before sizing the window.
        SetWindowPos(handle, IntPtr.Zero, area.Left + 10, area.Top + 10, 0, 0, 0x0015);
        double scale = Math.Max(96, GetDpiForWindow(handle)) / 96.0;
        int width = Math.Min(area.Width, (int)(Math.Max(window.MinWidth, s.Width) * scale));
        int height = Math.Min(area.Height, (int)(Math.Max(window.MinHeight, s.Height) * scale));
        int x = forceMonitor ? area.Left + (area.Width - width) / 2 : (int)(s.Left * scale);
        int y = forceMonitor ? area.Top + (area.Height - height) / 2 : (int)(s.Top * scale);
        x = Math.Clamp(x, area.Left, area.Right - width);
        y = Math.Clamp(y, area.Top, area.Bottom - height);
        SetWindowPos(handle, IntPtr.Zero, x, y, width, height, 0x0014);
    }

    public static void Fullscreen(Window window)
    {
        window.WindowState = WindowState.Normal;
        var handle = new WindowInteropHelper(window).Handle;
        var bounds = Forms.Screen.FromHandle(handle).Bounds;
        window.ResizeMode = ResizeMode.NoResize;
        SetWindowPos(handle, IntPtr.Zero, bounds.X, bounds.Y, bounds.Width, bounds.Height, 0x0014);
    }
    public static void SizeForProfile(Window window, DisplayProfile profile)
    {
        var handle = new WindowInteropHelper(window).Handle;
        double scale = Math.Max(96, GetDpiForWindow(handle)) / 96.0;
        var area = Forms.Screen.FromHandle(handle).WorkingArea;
        window.Width = Math.Clamp(profile.PixelWidth / scale, window.MinWidth, Math.Max(window.MinWidth, area.Width / scale));
        window.Height = Math.Clamp(profile.PixelHeight / scale, window.MinHeight, Math.Max(window.MinHeight, area.Height / scale));
        window.Left = Math.Clamp(window.Left, area.Left / scale, Math.Max(area.Left / scale, area.Right / scale - window.Width));
        window.Top = Math.Clamp(window.Top, area.Top / scale, Math.Max(area.Top / scale, area.Bottom / scale - window.Height));
    }
    public static void PlaceSettingsWindow(Window window, Window? owner)
    {
        var handle = new WindowInteropHelper(window).Handle;
        var screen = owner is null ? Forms.Screen.FromHandle(handle) : Forms.Screen.FromHandle(new WindowInteropHelper(owner).Handle);
        if (screen.WorkingArea.Width < 560 || screen.WorkingArea.Height < 400)
            screen = Screens.OrderByDescending(x => x.WorkingArea.Width * (long)x.WorkingArea.Height).First();
        var area = screen.WorkingArea;
        SetWindowPos(handle, IntPtr.Zero, area.Left, area.Top, 0, 0, 0x0015);
        double scale = Math.Max(96, GetDpiForWindow(handle)) / 96.0;
            window.Width = Math.Max(window.MinWidth, Math.Min(940, area.Width / scale - 24));
        window.Height = Math.Max(window.MinHeight, Math.Min(800, area.Height / scale - 24));
        window.Left = area.Left / scale + (area.Width / scale - window.Width) / 2;
        window.Top = area.Top / scale + (area.Height / scale - window.Height) / 2;
    }
}

public sealed class Notifications : IDisposable
{
    public event Action<string>? Failed;
    public event Action? ExitRequested;
    private readonly Window window;
    private bool hiddenToTray;
    private WindowState restoreState;
    private readonly Forms.ToolStripMenuItem restoreItem = new(), quitItem = new();
    private readonly Forms.NotifyIcon icon = new()
    {
        Icon = System.Drawing.SystemIcons.Information,
        Text = "Dayz Server Monitor",
        Visible = false
    };
    public Notifications(Window window)
    {
        this.window = window;
        var resource = Application.GetResourceStream(new Uri("pack://application:,,,/DayzServerMonitor;component/Assets/app.ico"));
        if (resource is not null)
        {
            using var stream = resource.Stream;
            using var original = new System.Drawing.Icon(stream);
            icon.Icon = (System.Drawing.Icon)original.Clone();
        }
        var menu = new Forms.ContextMenuStrip(); menu.Items.Add(restoreItem); menu.Items.Add(quitItem);
        icon.ContextMenuStrip = menu;
        restoreItem.Click += (_, _) => Restore();
        quitItem.Click += (_, _) => ExitRequested?.Invoke();
        icon.DoubleClick += (_, _) => Restore();
        icon.BalloonTipClicked += (_, _) => Restore();
    }
    public void Restore()
    {
        hiddenToTray = false; window.ShowInTaskbar = true; window.Show();
        window.WindowState = restoreState == WindowState.Maximized ? WindowState.Maximized : WindowState.Normal;
        window.Activate();
    }
    public void HideToTray()
    {
        try { icon.Visible = true; }
        catch (System.ComponentModel.Win32Exception ex) { Failed?.Invoke(L.Text("NotificationAreaError", L.Error(ex))); window.WindowState = WindowState.Minimized; return; }
        restoreState = window.WindowState; hiddenToTray = true;
        window.ShowInTaskbar = false; window.Hide();
    }
    public void Show(MonitorSettings s, string title, string body)
    {
        try
        {
        if (s.SoundAlerts) System.Media.SystemSounds.Exclamation.Play();
        if (!s.WindowsNotifications) return;
        icon.Visible = true;
        icon.ShowBalloonTip(10000, title, body, Forms.ToolTipIcon.Info);
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception or ExternalException)
        { Failed?.Invoke(L.Text("NotificationError", L.Error(ex))); }
    }
    public void Configure(MonitorSettings s)
    {
        restoreItem.Text = L.Text("Restore"); quitItem.Text = L.Text("Quit");
        try { icon.Visible = hiddenToTray || s.WindowsNotifications || (s.MinimizeOnClose && s.MinimizeTarget == "Tray"); }
        catch (System.ComponentModel.Win32Exception ex) { Failed?.Invoke(L.Text("NotificationAreaError", L.Error(ex))); }
    }
    public void Dispose() { icon.Visible = false; var image = icon.Icon; var menu = icon.ContextMenuStrip; icon.Dispose(); menu?.Dispose(); image?.Dispose(); }
}
