# Games and online service types

The app keeps its Dayz Server Monitor name. Several A2S-compatible games can use the same direct-query monitor; other protocols need separate adapters. Do not point A2S at an unrelated game port and interpret silence as a confirmed outage.

| Type | Current 1.9.0 support | Limits / evidence |
|---|---|---|
| DayZ | Direct UDP A2S_INFO, challenge, Source split replies | Live MAG/Sunnyvale checks plus packet fixtures. Optional DayZ clock/queue only when published. |
| Rust | Generic Source A2S_INFO parser | [Facepunch describes A2S discovery](https://support.facepunchstudios.com/hc/en-us/articles/360019452718-No-servers-showing). Enter the real query port; no live Rust acceptance is claimed. DayZ clock/queue extensions are not applied. |
| Counter-Strike: Source / CS2 | Generic Source A2S_INFO parser | Public query-enabled dedicated servers only. Matchmaking/coordinator health is a separate service. No live server acceptance claimed. |
| Counter-Strike 1.6 | Source-format replies and legacy single-packet GoldSource 0x6D replies | Parser fixtures include truncation checks. Legacy GoldSource split framing and compressed BZip2 responses are not supported; these show N/A. No live server acceptance claimed. |
| Project Zomboid | Generic A2S for a Steam-query-enabled server | The [developer documents Steam queries on the game port](https://projectzomboid.com/blog/news/2022/09/the-good-life/). That is a dated B41 reference; verify the actual build/configuration. No custom game-clock extraction or live acceptance claimed. |
| Minecraft Java | **Not implemented** | Needs TCP Server List Ping, distinct from optional UDP GameSpy query. [Paper documents enable-status](https://docs.papermc.io/paper/reference/server-properties/). Bedrock needs a separate UDP/RakNet adapter. |
| PlanetSide 2 | **Not implemented** | Official [Daybreak Census REST/events](https://census.daybreakgames.com/) is an external service with service-ID/rate-limit requirements; not a direct community A2S endpoint. |
| WARDOGS (BULKHEAD / Team17) | **Not verified or implemented** | [Official Steam listing](https://store.steampowered.com/app/1867240/). A documented provider-supported query interface has not been established here. A third-party server-list API is not treated as the game's official API or as proof of direct A2S support. |
| Discord / Spotify / WhatsApp / Steam | Optional public-status/web-reachability checks | See [integrations](INTEGRATIONS.md). Operator report versus local web response is always distinguished. No personal data or messages. |

There is no game-specific auto-discovery or preset query-port guessing. Add a saved server with its IP, game port and actual A2S query port; server lists remain alphabetically sorted. The restart plan is always user configuration, never inferred from another game's default. Non-DayZ fields that do not exist stay N/A. The local session line continues to describe **DayZ only**; it does not silently become a Rust/CS playtime tracker.

Next adapters should supply their own transport label, timeout/validation rules and optional capabilities (players, capacity, map, game clock, queue). Minecraft TCP RTT must not be displayed as UDP RTT. An API outage, blocked local access and a game-server outage must remain different outcomes. The existing multi-monitor selector and export are designed to carry separate cached snapshots for these future adapters; the table above describes what is actually shipped, not a promise that those adapters already exist.
