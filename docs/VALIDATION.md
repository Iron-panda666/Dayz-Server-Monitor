# Validation · 1.7.0

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

The tests load the compiled application resources without creating a Window or calling Show/Run. They check readable selected ComboBox labels, language updates, world-clock labels, all four themes and their text contrast. They render theme, world-clock and dated graph examples into PNG files. The application icon is checked from its embedded ICO resource.

Closing is deferred until after the original Closing event returns. The window is hidden, network tasks are canceled, and settings saving runs off the UI thread in parallel with cleanup. Cleanup has a 1.2-second wait budget. A stalled disk can still prevent the last settings write from finishing; normal local storage is expected for the portable data folder. The app does not forcibly kill another process.

## Live checks and limits

Nineteen public DayZ endpoints were checked once on 29 October 2026. Seventeen responded: five MAG and twelve Sunnyvale. See [the included list](SERVERS.md). These are historical observations, not a promise of current availability. The verification output is kept locally, rather than publishing anyone's runtime configuration or history.

No desktop monitor was opened or activated for this release while DayZ could be running. Closing behavior is covered by code review and bounded-task checks; the complete live window workflow has not been manually timed. Multi-monitor DPI changes, Windows startup registration, notification delivery, suspend/resume and several days of continuous operation still need hands-on testing.

The app does not read the game process, memory or chat. UDP query replies cannot prove that joining the game will work.

## Stream Deck release checks

All 42 core checks, 10 WPF checks and 9 Node/plugin checks passed. The additional core checks exercise DayZ clock/queue keywords, the real named-pipe protocol, shared cooldown, unsupported-command rejection, discovery cleanup and bounded shutdown. The full A2S fixture also checks that extended clock fields reach ServerInfo without affecting player metrics.

The plugin tests run the actual official-SDK bundle against a local simulated Stream Deck WebSocket host and a C# console bridge fixture. They cover live state delivery, short press, one-second hold, pinned keys, the cooldown shared across keys and MONITOR OFF after fixture shutdown. Separate checks cover assumed day/night boundary crossing, midnight, missing/invalid fields, N/A/stale data, optional restart overrides, SVG escaping, history gaps and real 72-pixel rasterization. No desktop Window or real Stream Deck app is opened. The fixture is isolated from real monitor discovery files and does not query a game server.

Elgato CLI 1.10.4 validated and packaged the plugin. Its only warning is the private GitHub project URL returning 404 to anonymous requests. The installer has not been submitted to Marketplace. Physical MK.2 installation, actual key-lens readability, Stream Deck sleep/reconnect, cross-account ACL denial and long-running resource use are not claimed as tested.

A real MAG DEER query on 30 October 2026 at 01:44:59 Prague time confirmed the final parser: 47/121 players, published game clock 02:52, base multiplier 4, extra night multiplier 4, queue 0 and UDP RTT about 134 ms. The first live check exposed a placeholder legacy AppID; the parser now also uses the extended Steam GameID and a regression fixture covers that form. This is one historical query, not a guarantee of present values or accurate sunlight prediction.
