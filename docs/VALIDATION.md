# Validation

Recorded release results for Windows x64, built with .NET SDK 8.0.427:

| Version | Core | WPF | Plugin | Total |
|---|---:|---:|---:|---:|
| 1.8.0 | 42 | 15 | 11 | 68 |
| 1.9.0 | 58 | 16 | 15 | 89 |
| 1.9.1 | 58 | 20 | 15 | 93 |

These are automated check counts. The outstanding physical/live checks are listed at the end.

## Query, time and storage

A2S fixtures cover direct UDP replies, challenge exchange, reordered/duplicate Source fragments, invalid/truncated packets, timeout and cancellation. Compressed BZip2 replies are rejected. Single-packet GoldSource 0x6D parsing has truncation checks too.

Availability checks keep failed metrics at N/A, distinguish transient failures from query gaps and record recovery only after a measured gap. OFFLINE requires manual confirmation. The 20 s refresh guard is tested with concurrent requests and monotonic time.

Schedule checks cover Prague DST gaps/duplicate times, midnight, UTC countdowns and persisted alert deduplication. NTP checks cover offset/delay, reply validation, rate/denial responses, the 2036 era rollover, holdover/fallback and cancellation. [NTP measurements](NTP.md).

Storage tests cover JSON roundtrips, JSONL retention, profile edits/deletion, draft isolation, catalog migration and preserved custom settings/removals. Translation checks compare all five languages and placeholders. Graph tests use real query-start spacing, date changes and missing samples.

Session checks select the actual DayZ executable in the current Windows session, excluding Steam/launchers/dedicated servers. They cover a game started before the monitor, clock jumps, process exit/PID reuse and unknown/multiple cases. The implementation reads process metadata, not memory or chat.

## WPF and closing

WPF tests load compiled resources and render controls to PNG without creating a Window or starting monitoring. They check selected ComboBox labels, embedded ICO sizes, language changes, world clocks and all ten palettes. Player numbers exceed 7:1 contrast. Existing controls keep their data and update their colors across layout/theme changes.

Dashboard geometry checks cover 27 presets at 100%, 125%, 150% and 200% DPI, with both compact states and world-clock visibility. Settings checks cover all seven pages in five languages, three palettes and 360/720/940 logical widths. They verify retained fields, sidebar/dropdown navigation, footer bounds and disabled export details. [Settings renders](SETTINGS.md) | [LCD references](DISPLAYS.md).

The dialog controller was reviewed for profile draft handling and Cancel behavior. The new settings tests render the view and forward actions; they do not run the full modal dialog workflow.

Closing hides the main window, cancels network work and saves settings off the UI thread. Cleanup has a 1.2 s wait budget. Task checks cover finished, faulted, cancelled and permanently stalled workers. A stalled disk can still prevent the final write. Tray hiding keeps the timer and bridge running; explicit Quit and Windows shutdown bypass minimize-on-close.

## Stream Deck and export

Node tests run the bundled official-SDK plugin against a simulated WebSocket host and real C# pipe fixture. They cover separate server keys, endpoint selection/list delivery, per-server refresh protection, short press/hold behavior and monitor disconnect. Render tests cover missing/stale metrics, queue separation, game-clock extrapolation, session/service faces, optional restart overrides, escaped SVG and 72-pixel rasterization.

Multi-monitor fixtures check opt-in polling, deduplicated endpoints, isolated metrics/history and rejected missing targets. HTTP fixtures check cached JSON, Prometheus labels/content type, missing values and rejection of control/config paths. Mock service replies distinguish operator incidents, local web access and blocked checks.

Elgato CLI 1.10.4 validated/packed the plugin. The recorded warning is the private repo URL returning 404 anonymously. Package checks compare the embedded EXE/plugin with standalone files, exclude private config/history and compare source ZIP files with the committed sources. Packages include SHA-256 sums.

## Live observations

On 29 October 2026, 17 of 19 public DayZ endpoints replied: five MAG-labelled endpoints and twelve Sunnyvale. The current catalog has 16 entries after the separate MAG source check. [Addresses and provenance](SERVERS.md).

A MAG DEER query on 30 October at 01:44:59 Prague time returned 47/121 players, game clock 02:52, base speed 4x, extra night speed 4x, queue 0 and about 134 ms UDP RTT. It exposed a placeholder legacy AppID; the parser now also accepts the extended Steam GameID, with a regression fixture.

On 30 October, Discord's status JSON reported operational and Spotify, WhatsApp Web and Steam Store returned HTTP 200 headers. Those web checks did not exercise login, playback, messaging or matchmaking.

The user confirmed physical MK.2 output on 30 October after the image fix. That fix sends base64 SVG data URLs instead of raw SVG; the host fixture now rejects the old payload. Access denial also has a separate face instead of MONITOR OFF.

Version 1.7.1 added queue separation in the WPF card and key display. Version 1.8.0 added the dashboard layouts and close options. Version 1.9.0 added local session, profile handling, multiple targets and export. Version 1.9.1 added the settings reorganization. These records describe those builds, not a new live check on every documentation edit.

## Still untested

The full live settings/tray/close workflow, measured shutdown time, physical LCD readability and DPI changes between displays need manual checks. The same applies to startup registration, notification delivery, suspend/resume, Stream Deck sleep/reconnect, cross-account denial and several days of resource use.

New multi-server/session/service key workflows have not been accepted on the physical MK.2. HA/Grafana deployment and LAN firewall setup have not been tested. Rust/CS/Project Zomboid have parser-level support only; [other adapters](MONITOR-TYPES.md) remain unimplemented. Provider restart plans are unverified. A query reply alone cannot establish game login or server ownership.
