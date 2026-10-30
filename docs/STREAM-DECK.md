# Stream Deck MK.2

The plugin uses the official Elgato SDK and is designed for the MK.2's 72 × 72-pixel keys. Use Dayz Server Monitor **1.9.0 or later** for multiple servers, local session and service status. Following the active server remains compatible with older 1.7.1+ monitors, with unavailable features shown as N/A. Windows 10/11 x64 and Stream Deck software **7.1 or later**, including its Node.js 24 runtime, are required. Users do not need to install Node.js separately.

![English key designs, enlarged, example data](images/stream-deck-keys.png)

These are enlarged renders of the actual key images with demo values, not a photograph of a tested device.

## Installation

1. Download and unpack the Windows ZIP. Carry over your config.json and data folder if upgrading. Keep the executable in a stable, writable folder.
2. Start DayzServerMonitor.exe yourself when convenient and select the server to watch. The plugin never starts the monitor or brings its window forward. The monitor can be minimized.
3. Double-click **Dayz-Server-Monitor-1.9.0.streamDeckPlugin** and accept the installation in Stream Deck. The file is also inside the portable Windows ZIP and local ReleasePackages folder. Only this optional plugin is installed; the monitor remains portable.
4. Find **Dayz Server Monitor** in the Stream Deck action list. Drag **Cycle server information** onto a key.
5. Leave the action's default artwork/title in place. A custom image in Stream Deck can override the plugin's changing display.

No Marketplace account, API key, paid service, extra driver or administrator access is required for direct installation from this project. Do not run the Stream Deck app or monitor as administrator.

## One key or several

**Cycle server information** changes the view with a short press. Hold the key for one second to request a refresh. Releasing it after a hold does not also cycle the view. Choose the initial view and the views to include in the key's settings.

**Pinned server information** keeps one view on a key. A short press does not send a query. Hold for one second to refresh. Add several pinned keys to keep the game clock, restart countdown and players visible together.

Keys watching the same endpoint share its cached data and manual refresh guard. A hold cannot bypass a running query or the twenty-second limit, including across different keys and the desktop button. Different servers retain their own limits. Automatic monitoring retains its configured cadence. Selecting another view does not send a game-server query.

| View | Display |
|---|---|
| Game time | Published game clock, optionally extrapolated as ~HH:mm; DAY? / NIGHT? and current assumed speed |
| Restart | Time until the next configured restart; PLAN? while the schedule is unconfirmed |
| Players | Players / capacity plus a separate queue line, e.g. 121/121 and +21 IN QUEUE; age/cooldown below |
| History | One hour of player samples from the current monitor session, with gaps for missing replies |
| UDP RTT | Query round-trip time and age of the query |
| Logout | Time until the configured logout reminder, or LOG OUT NOW |
| Local DayZ session | Actual DayZ game-process runtime, HH:mm; excludes Steam and launchers; N/A when unknown |
| Service target | SERVICE / OK, ISSUE or WEB OK; source scope below; N/A for unknown/stale checks |

Restart warnings can temporarily override the selected view. This is enabled by default and can be disabled per key. Yellow uses the monitor's logout threshold; red uses its kick threshold. RESTART / NOW? is an expectation from the configured plan, not proof of a game restart.

The short server label is generated automatically, such as MAG #2, or can be replaced with up to twelve characters. In **Server for this key**, leave **Follow desktop selection** for the original behavior, or choose a specific monitored endpoint. Enable its **Monitor this server in the background** option in the app and save settings, then click **Reload monitored servers** in the key's settings. Each endpoint is polled centrally by the monitor, independently of how many keys display it. A service target automatically shows service status regardless of its game-information view. [Multi-monitor and export setup](INTEGRATIONS.md).

The session page describes the local DayZ process, including menus. It is the same session on every server key; it does not establish that the player joined that server. It does not read the Steam overlay, memory, chat or account. Runtime starts at the game's Windows process start and advances monotonically. Old default six-page cycles gain the seventh session page once; custom page subsets remain unchanged. A disabled session shows OFF / DISABLED IN APP.

## Game time is a measurement plus an estimate

The monitor reads DayZ's published A2S keyword fields when present. A time such as 19:42 is the server's reported game clock at the last reply. The base multiplier is etm; entm is the additional night multiplier. For example, 4 × base and 4 × night gives an effective night speed of 16 × under standard DayZ settings.

Between replies, the plugin estimates the time using these multipliers. The ~ prefix marks extrapolation. DAY? / NIGHT? use assumed daytime hours, default **06:00–18:00 in game**, editable in the key settings. They are not observations of actual sunlight. Map, game date and custom server scripts can change sunrise, sunset or time behavior. No accurate astronomical day/night state is claimed.

If a multiplier is missing, the key retains the reported clock without extrapolation. If the server does not publish a game clock, it shows N/A. Failed/stale queries do not keep displaying old metrics as fresh. The optional offset clock in the desktop app remains a separate calculated clock.

Reference: [Bohemia's server configuration](https://community.bohemia.net/wiki/DayZ:Server_Configuration).

## Connection and missing data

The plugin reads one local snapshot per second per selected endpoint/data folder, using a Windows named pipe restricted to the current user. It does not use HTTP or contact game/public status sites itself. An optional HTTP exporter is available separately for Grafana/HA; Stream Deck does not require it. Discovery files are stored under `%LOCALAPPDATA%\DayzServerMonitor\bridges`. No credentials are stored there.

Leave **Monitor data folder** blank to use the newest discovered running monitor. If several monitor instances use different data folders, enter the exact data folder to select one. Keys with the same folder and endpoint share one connection. **Pinned** refers to the information page; server targeting is a separate setting on both actions.

ACCESS / DENIED / CHECK ADMIN means Windows denied access to the local monitor pipe. Check that both apps run under the same Windows account and normally, without administrator rights; the pipe checks both account and elevation.

MONITOR OFF means the desktop monitor is unavailable or closed; the plugin does not launch it. QUERY / N/A means no usable query result is available. STALE means the last sample exceeded its expected cadence plus a grace period. A missing UDP reply does not prove the game is offline. WAIT 20s and QUERY RUNNING show the shared refresh restriction.

SERVER / N/A / CHECK SELECTION means the saved target is no longer monitored or the app is too old for targeting. It never silently displays a different server. Re-enable the profile or select an available target. Service targets distinguish operator reports from **WEB CHECK ONLY**; a reachable Spotify/WhatsApp/Steam web page does not prove global backend availability.

## Building and distribution

With .NET 8 SDK and Node.js 24+ installed, `./build.ps1` builds and tests both projects, validates the plugin with Elgato's official CLI, and creates the .streamDeckPlugin installer. `./build.ps1 -SkipStreamDeck` builds just the Windows monitor.

The Node tests include the actual bundled official-SDK plugin connected to a simulated Stream Deck host and a C# named-pipe fixture. They reject raw SVG on the image wire and verify encoded key images, press/hold behavior, data transfer, cooldown across keys, and the MONITOR OFF state without opening the real desktop applications. Live output on the physical MK.2 was confirmed by the user on 30 October 2026 after applying the image repair. Installer UI, detailed readability under its key lenses, sleep/reconnect and extended use still need hands-on testing.

The CLI may report that the project URL returns 404 because this GitHub repository is private. That warning does not make the installer public or submit it to Marketplace. No Marketplace submission or Maker Agreement acceptance is performed by this project.

Original code and the T/R icon are MIT-licensed. Elgato SDK and bundled runtime dependency licenses are preserved in the installer and docs/licenses/STREAMDECK-RUNTIME-NOTICES.md. Build tools are separate: the SVG renderer @resvg/resvg-js is MPL-2.0 and is used only during building/testing; its binary is not shipped in the plugin. See [names and trademarks](TRADEMARKS.md).

The queue is separate from occupied slots: 121/121 +21 means 121 players in game and 21 waiting. A published zero shows QUEUE 0; an absent field, failed query or stale sample shows QUEUE N/A. Queue counts reflect the last query, not continuous live measurement. The player-history graph tracks occupied slots only.
