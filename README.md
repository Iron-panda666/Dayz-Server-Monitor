# Dayz Server Monitor

![T/R icon](docs/images/app-icon.png)

Windows monitor for DayZ servers, built for a second screen. It queries the server over UDP, logs player counts and shows the time left before a configured restart. C# / .NET 10, WPF. Runs on Windows 10/11 x64.

[Česky](README.cs.md) | [Downloads](../../releases/latest) | [MIT license](LICENSE)

## Run it

Download `Dayz-Server-Monitor-1.10.0-win-x64.zip`, unpack it into a writable folder and run `DayzServerMonitor.exe`. The .NET runtime is bundled. No installer, account, API key or admin rights needed.

Settings go into `config.json` beside the EXE; query history goes into `data`. Close the old copy before updating and keep both. To use another data folder:

```powershell
.\DayzServerMonitor.exe --data-dir "D:\Dayz Monitor Data"
```

The single-file build extracts its native runtime into the user's temp folder on first launch. If you enable startup with Windows, leave the EXE at a fixed path. [Package contents and checksums](ReleasePackages/README.md).

## Using the monitor

Select a server at the top of the window. The default list has four MAG addresses and twelve Sunnyvale DayZ servers. Address sources and dated query results are in [SERVERS.md](docs/SERVERS.md).

A2S_INFO requests run every 300 s, every 30 s during the last 15 minutes before a restart and every 15 s in the restart window. Manual refresh has a 20 s cooldown shared with Stream Deck. Only the selected server and profiles enabled for background monitoring are queried. Up to 16 saved profiles can run in the background.

The player graph includes query times and gaps for failed replies. `121/121 +21 in queue` means 121 players in game and 21 waiting. Missing values show N/A. UDP query RTT includes the challenge exchange; it is not ICMP ping.

A timeout cannot tell a stopped game server from a blocked query port. Repeated failures show QUERY UNAVAILABLE. OFFLINE needs manual confirmation. When replies resume, the app records the recovery time.

Set your restart plan in Settings -> Restarts & alerts. The example is 03:00, 07:00, 11:00, 15:00, 19:00 and 23:00 in Europe/Prague, including DST. These times have not been verified for the included servers. Logout and kick reminders are adjustable in whole minutes.

Settings -> Appearance contains the ten themes and language selector. English is the default; Czech, Polish, German and Spanish are included. Profiles can be edited or deleted under Servers. One Save settings applies the dialog's changes; Cancel discards them. See the [settings guide](docs/SETTINGS.md).

The window reflows for wide strips and portrait displays. There are 27 size presets, fullscreen and optional close-to-tray/taskbar behavior. [LCD sizes and controls](docs/DISPLAYS.md).

![Wide dashboard with example values](docs/images/layout-wide.png)

![Player graph with example queries](docs/images/query-chart.png)

## Clocks and Stream Deck

World clocks and 12/24-hour format are under Settings -> Time & clocks. Optional NTP changes this app's time source, not Windows time. The offset clock is calculated from Prague time. [Clock setup](docs/LANGUAGES-AND-CLOCKS.md) | [NTP details](docs/NTP.md).

The local session counter uses the actual DayZ process start time. It excludes Steam and launchers but includes the game's menus. It is not a server-connection timer or Steam overlay counter. No game memory or chat is read.

The ZIP includes `Dayz-Server-Monitor-1.10.0.streamDeckPlugin` for Stream Deck MK.2. A short press cycles views; a one-second hold requests a refresh. Each key can follow the desktop selection or a separate monitored server. Views include game time, restart countdown, players, history, UDP RTT, logout reminder and local session. The published game clock may be extrapolated between queries; day/night is an estimate. [Plugin installation and display details](docs/STREAM-DECK.md).

Settings -> Integrations has optional Prometheus metrics for Grafana, JSON for Home Assistant and service checks. Discord uses its operator's status report. Spotify, WhatsApp and Steam checks test web reachability from this PC. They do not establish global service health. [Configuration examples](docs/INTEGRATIONS.md) | [Other games and missing adapters](docs/MONITOR-TYPES.md).

## Build

Install .NET SDK 10.0.403 or later in the 10.x line and Node.js 24+ on Windows:

```powershell
.\build.ps1
```

The script builds the solution, runs the checks and packages the Windows EXE, plugin and source ZIP. Outputs go into `artifacts` and `ReleasePackages/v1.10.0`. It does not launch the app. For Visual Studio 2026, open `DayzServerMonitor.sln` with the .NET desktop workload installed.

Version 1.10.0 includes .NET 10.0.14 and pins Zod 4.6.7 for the Stream Deck SDK. No separate .NET install is needed. Existing config.json and data remain compatible.

Version 1.10.0 passed 58 core, 20 WPF and 17 plugin checks. Physical LCD/DPI behavior, the full settings workflow and multi-day operation still need testing. [Test results](docs/VALIDATION.md).

This is a hobby project built with AI-assisted vibe coding. The source and T/R icon are MIT-licensed. Bundled dependencies keep their own [notices](THIRD_PARTY_NOTICES.md). The project has no official affiliation with Bohemia Interactive, MAG, Sunnyvale, Valve or Elgato. [Names and trademarks](docs/TRADEMARKS.md).
