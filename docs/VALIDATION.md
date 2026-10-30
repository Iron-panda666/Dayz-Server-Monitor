# Validation · 1.6.1

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

The app does not read the game process, memory or chat, and does not include a Stream Deck plugin. UDP query replies cannot prove that joining the game will work.
