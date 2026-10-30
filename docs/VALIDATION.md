# Validation · 1.9.1

The release is built for Windows x64 using .NET SDK 8.0.427. The automated checks cover the areas below. They do not establish multi-day reliability or confirm any provider's restart schedule.

## Core checks

- Direct UDP A2S_INFO, challenge exchange, reordered/duplicate split fragments, invalid and truncated packets, timeout and cancellation. Compressed BZip2 responses are rejected as unavailable rather than misread.
- Player metrics become N/A on missing replies. One failed query does not mean OFFLINE. Query gaps and recoveries are tracked separately from proof of game availability.
- Prague restart scheduling across midnight, spring DST gaps and autumn duplicate times; alert deduplication across refresh and saved sessions.
- Independent server profiles, minute-based reminders, catalog migration, preservation of custom settings and the fifty-profile limit.
- Exactly twenty monotonic seconds between manual refresh requests, concurrent-click protection, wall-clock changes and automatic restart polling.
- Graph positions based on actual query start times, irregular spacing and date changes.
- Optional NTP calculations and packet validation, rate limits/denial, era rollover, cancellation, holdover and fallback.
- JSON settings, local history and retention; five-language translation completeness; world-clock zones and custom labels.
- Shutdown of completed, faulted and canceled tasks, plus a worker that never completes. A bounded drain prevents that worker from holding shutdown open indefinitely.

## WPF checks without windows

The tests load the compiled application resources without creating a Window or calling Show/Run. They check readable selected ComboBox labels, language updates, world-clock labels, all ten themes and their text contrast. They render theme, world-clock and dated graph examples into PNG files. The application icon is checked from its embedded ICO resource.

Closing is deferred until after the original Closing event returns. The window is hidden, network tasks are canceled, and settings saving runs off the UI thread in parallel with cleanup. Cleanup has a 1.2-second wait budget. A stalled disk can still prevent the last settings write from finishing; normal local storage is expected for the portable data folder. The app does not forcibly kill another process.

## Live checks and limits

Nineteen public DayZ endpoints were checked once on 29 October 2026. Seventeen responded: five MAG and twelve Sunnyvale. See [the included list](SERVERS.md). These are historical observations, not a promise of current availability. The verification output is kept locally, rather than publishing anyone's runtime configuration or history.

No desktop monitor was opened or activated for this release while DayZ could be running. Closing behavior is covered by code review and bounded-task checks; the complete live window workflow has not been manually timed. Multi-monitor DPI changes, Windows startup registration, notification delivery, suspend/resume and several days of continuous operation still need hands-on testing.

The app does not read the game process, memory or chat. UDP query replies cannot prove that joining the game will work.

## Stream Deck release checks

All 42 core checks, 15 WPF checks and 11 Node/plugin checks passed. The additional core checks exercise DayZ clock/queue keywords, the real named-pipe protocol, shared cooldown, unsupported-command rejection, discovery cleanup and bounded shutdown. The full A2S fixture also checks that extended clock fields reach ServerInfo without affecting player metrics.

The plugin tests run the actual official-SDK bundle against a local simulated Stream Deck WebSocket host and a C# console bridge fixture. They cover live state delivery, short press, one-second hold, pinned keys, the cooldown shared across keys and MONITOR OFF after fixture shutdown. Separate checks cover assumed day/night boundary crossing, midnight, missing/invalid fields, N/A/stale data, optional restart overrides, SVG escaping, history gaps and real 72-pixel rasterization. No desktop Window or real Stream Deck app is opened. The fixture is isolated from real monitor discovery files and does not query a game server.

Elgato CLI 1.10.4 validated and packaged the plugin. Its only warning is the private GitHub project URL returning 404 to anonymous requests. The installer has not been submitted to Marketplace. The plugin was updated in place and live output on the physical MK.2 was confirmed by the user on 30 October 2026. Installer UI, detailed key-lens readability, Stream Deck sleep/reconnect, cross-account ACL denial and long-running resource use are not claimed as tested.

A real MAG DEER query on 30 October 2026 at 01:44:59 Prague time confirmed the final parser: 47/121 players, published game clock 02:52, base multiplier 4, extra night multiplier 4, queue 0 and UDP RTT about 134 ms. The first live check exposed a placeholder legacy AppID; the parser now also uses the extended Steam GameID and a regression fixture covers that form. This is one historical query, not a guarantee of present values or accurate sunlight prediction.

## Queue display · 1.7.1

The real WPF player card now distinguishes 121/121 plus 21 waiting, published zero, missing queue data and failed queries, with localized labels. Stream Deck rendering keeps the queue visible while a refresh countdown is shown, without adding it to occupied slots or graph samples. The updated English previews render both cases without desktop windows. Total: 42 core, 11 WPF and 10 plugin checks.

## Stream Deck image repair · 1.7.2

The plugin now sends SVG images as base64 data URLs, as required by the Stream Deck image API. Previously, raw SVG was forwarded by the SDK and the real host kept showing the static START THE APP icon; the simulated host had accepted that invalid payload. The integration fixture now rejects raw markup, and a round-trip encoding check covers the transmitted image.

An access-denied pipe connection gets a separate CHECK ADMIN face instead of being mistaken for a closed monitor. A live local snapshot from the running 1.7.1 monitor succeeded after it was reopened without elevation; the user subsequently confirmed that the physical key displays server data. The monitor and its current-user-only pipe security are unchanged.

## Adaptive dashboard and close options · 1.8.0

The actual DashboardView is rendered without creating a Window, starting monitoring or changing foreground applications. Tests move the same chart and player controls through stacked, wide, bar, portrait and ribbon layouts and confirm their identity/data is retained. A separate layout sweep checks all 27 presets at 100%, 125%, 150% and 200% DPI, both compact states and with/without world clocks, checking essential tiles, bounds and overlap. These are geometry checks, not physical LCD acceptance. Small displays clamp to the minimum logical window size when DPI scaling would otherwise make it smaller.

Ten complete palettes pass text and metric contrast checks, resource updates and JSON validation/roundtrip. The English previews include actual desktop, wide, 7.9/8.8/11.9-style bars, portrait, mini and ribbon dashboard controls. Representative hardware sizes, connection boundaries and source links are in [display research](DISPLAYS.md).

The close-to-taskbar/tray options and display profile IDs are checked through settings validation/roundtrip. Code review confirms that hiding leaves the monitoring timer and Stream Deck bridge running, explicit Quit bypasses minimization, settings/bounds are saved before hiding, and the tray offers restore/quit. Main window startup is guarded against duplicate Loaded events. The full live tray/close workflow, DPI changes between physical screens, small-screen settings placement and extended continuous use still need hands-on verification. No physical LCD compatibility is claimed from rendering alone.

That release placed its packages in the older local handoff folder. Published packages excluded private config.json and data. Total for 1.8.0: 42 core + 15 WPF + 11 plugin checks.

## Profiles, local session, multiple monitors and export · 1.9.0

Core checks cover exact game executable selection (excluding Steam/launchers/dedicated servers and other Windows sessions), startup after an already running game, monotonic runtime across wall-clock jumps, process exit/PID reuse and unknown/multiple cases. WPF checks retain a small, translated session line. This is local process runtime including menus, not authoritative Steam overlay or server-connected time.

Profile tests cover rename/IP edits in place, duplicate-name rejection, draft isolation, deletion/last-profile protection, alphabetical ordering and catalog v2 migration without restoring deleted presets. MAG provenance uses a dated allowlist of IP/game-port pairs, not a server-name heuristic or authenticated identity. Official MAG pages were checked on 30 October 2026; an existing Badlands profile is retained but marked unverified.

Multi-monitor fixtures verify opt-in, endpoint deduplication, isolated successful/failed metrics and history, cadence and refresh protection. Named-pipe fixtures select independent targets, route refreshes, list endpoints and reject unknown targets. The actual official-SDK plugin fixture displays different counts on separate keys and confirms refreshing one does not change the other. A target mismatch from an older app cannot display the wrong server. Service faces cover operator incidents, web-only reachability and unknown results. New multiple-server/session/service workflows still need physical MK.2 acceptance.

Local HTTP tests check Prometheus content type, escaped labels, missing measurements instead of false zeroes, separate service scope, cached JSON and read-only rejection of control/config paths. Mock public-service responses distinguish operator incidents, web access and blocked checks. Requests use fixed endpoints and bounded sizes/timeouts, without personal accounts. Real Grafana/HA setup and LAN firewall behavior are not claimed. Web reachability cannot establish messaging, streaming, login or matchmaking availability.

The A2S parser also accepts legacy single-packet GoldSource CS 1.6 replies, with truncation rejection. Rust/CS/Project Zomboid support is protocol-level, without live provider acceptance. Minecraft, PlanetSide 2 and WARDOGS adapters are not shipped; see [monitor types](MONITOR-TYPES.md).

Packaging creates `ReleasePackages/v1.9.0` with the portable Windows ZIP, matching plugin/source ZIPs, manifest and SHA-256 sums. It verifies the embedded EXE/plugin against standalone files and rejects private config/history in the ZIP. No app/game/installer is launched.

The full release checks passed: **58 core + 16 WPF + 15 plugin = 89 checks**. Elgato CLI validated and packaged the installer; its existing private-repository URL warning remains. A one-time public-service check on 30 October 2026 received Discord's operational report and HTTP 200 headers from Spotify, WhatsApp Web and Steam Store. These are historical source/reachability observations only, with no authentication or backend operation tested. English previews render the local session and service face as demonstration data.

## Settings organization · 1.9.1

The real SettingsView is rendered without constructing a Window, starting monitoring, saving user configuration or changing foreground apps. Four additional WPF checks cover sidebar/compact navigation, retained field identity/contents, visible save footer and wrapped fields on all seven pages in five languages, three palettes and 360/720/940 logical widths. Actions forward to the existing dialog controller and optional export details stay hidden/disabled while off. Rendered English pages include servers, restarts, appearance and narrow mode. These checks do not establish physical monitor/DPI acceptance or exercise the full live dialog workflow.

The dialog deep-copies profile/world-clock drafts as before. Code review confirms that switching profiles validates and retains only the current profile's draft; global settings are read at the final Save. Cancel has no configuration write. Editing multiple profiles no longer requires separate Save server actions. Existing core profile edit/delete/draft-isolation checks remain in place. There are no configuration schema changes.

Final checks for 1.9.1 passed: **58 core + 20 WPF + 15 plugin = 93 checks**. The new English settings renders come from the real view, with no desktop windows opened. Full live dialog interaction remains separate from the offscreen checks.
