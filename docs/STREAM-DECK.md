# Stream Deck MK.2

The plugin uses Elgato's SDK and renders for 72 x 72-pixel MK.2 keys. It needs Windows 10/11 x64 and Stream Deck 7.1+ with its bundled Node.js 24 runtime. App 1.9.0+ is needed for separate server targets, local session and service status. Following the active server still works with 1.7.1+.

![Key renders with sample data](images/stream-deck-keys.png)

## Install and assign keys

1. Unpack the Windows ZIP. Keep `config.json` and `data` when upgrading.
2. Start `DayzServerMonitor.exe` and select a server. It can stay minimized.
3. Install `Dayz-Server-Monitor-1.9.1.streamDeckPlugin`, included in the ZIP and ReleasePackages folder.
4. Drag Cycle server information or Pinned server information from the Dayz Server Monitor action list onto a key.

Direct installation needs no Marketplace account or extra driver. Run both apps under the same Windows account, without elevation. The plugin does not launch the monitor. A custom key image/title can cover its generated display.

Cycle changes view on a short press. Pinned keeps one view. Both request a refresh after a one-second hold, without also cycling on release. Keys on the same endpoint share its cached data and 20 s refresh guard, including the desktop button. Other endpoints have separate guards; automatic polling keeps its cadence.

| View | Contents |
|---|---|
| Game time | Published clock or extrapolated ~HH:mm, estimated DAY?/NIGHT? and speed |
| Restart | Configured countdown; PLAN? until confirmed |
| Players | Occupied slots/capacity and separate queue, with age/cooldown below |
| History | Up to one hour of samples from this app run, including gaps |
| UDP RTT | Query RTT and sample age |
| Logout | Reminder countdown or LOG OUT NOW |
| Local DayZ session | Local game runtime, HH:mm; OFF when stopped/disabled, N/A when unknown |
| Service target | SERVICE with OK, ISSUE, WEB OK or an unknown result |

Restart warnings can override a game view. This is on by default and configurable per key. Yellow uses the logout threshold, red the kick threshold. NOW? means a scheduled restart is expected.

A player result `121/121 +21 IN QUEUE` keeps 21 waiting players separate from occupied slots. Published zero is QUEUE 0; absent, failed or stale data is QUEUE N/A. The graph tracks occupied slots.

Choose Server for this key or leave Follow desktop selection. For a separate server, enable background monitoring in the app, save and use Reload monitored servers in the key settings. A short label can be entered, up to 12 characters. Service targets show SERVICE automatically. [Multiple servers and export](INTEGRATIONS.md).

The session page is local DayZ process runtime, including menus. It is the same on every server key, not proof that the player joined that server. Steam/launchers are excluded and no memory/chat/account is read. Existing default six-page cycles gain the session page once; custom subsets stay unchanged.

## Game clock

DayZ may publish `etm`, `entm` and a game clock in its A2S keywords. `etm` is the base speed; `entm` multiplies it at night. Base 4x and night 4x therefore give 16x night speed under standard server settings.

The plugin extrapolates between replies and marks that time with `~`. DAY?/NIGHT? use assumed in-game daytime hours, initially 06:00-18:00, editable per key. Map, date and server scripts can change the actual sunrise and time behavior.

A missing multiplier leaves the published time without extrapolation. A missing game clock gives N/A. The desktop offset clock is calculated separately. [Bohemia server configuration](https://community.bohemia.net/wiki/DayZ:Server_Configuration).

## Connection and diagnostics

One local snapshot is read per second for each endpoint/data-folder pair. Keys on the same pair share a connection. The Windows named pipe is restricted to the current user; the plugin does not query game/public status sites or require the optional HTTP exporter.

Discovery files are in `%LOCALAPPDATA%\DayzServerMonitor\bridges`. Leave Monitor data folder empty to use the newest discovered instance or enter an exact data folder to select one.

| Key message | Check |
|---|---|
| MONITOR OFF / START THE APP | Monitor closed or unavailable |
| ACCESS / DENIED / CHECK ADMIN | Same Windows account and normal elevation for both apps |
| QUERY / N/A | No usable query reply |
| STALE | Sample older than its expected interval plus grace period |
| WAIT 20s / QUERY RUNNING | Shared refresh guard |
| SERVER / N/A / CHECK SELECTION | Target disabled/removed or app too old; re-enable or select another |
| WEB CHECK ONLY | Web reachability, not global backend health |

UDP silence does not prove a game outage. Failed/stale readings are not displayed as fresh metrics.

## Build and licenses

`./build.ps1` builds/tests the app and plugin, validates it with Elgato CLI and creates the installer. `./build.ps1 -SkipStreamDeck` builds only the Windows monitor. Node.js 24+ and .NET 8 SDK are build requirements.

The SDK integration fixture uses a simulated Stream Deck host and C# pipe fixture. Physical MK.2 output was confirmed on 30 October 2026 after the image repair; new multi-server/session/service workflows still need hands-on checks. [Validation record](VALIDATION.md).

The CLI's anonymous check of this private GitHub URL returns 404. It does not prevent packaging. Nothing has been submitted to Marketplace or accepted under its Maker Agreement.

Original code and T/R artwork are MIT-licensed. SDK/runtime licenses are kept in [runtime notices](licenses/STREAMDECK-RUNTIME-NOTICES.md). The MPL-2.0 `@resvg/resvg-js` renderer is a build/test tool; its binary is not in the plugin. [Names and trademarks](TRADEMARKS.md).
