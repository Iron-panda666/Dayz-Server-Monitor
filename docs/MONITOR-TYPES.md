# Games and services

Version 1.9.1 queries A2S endpoints directly. Other protocols need their own adapters. Set the actual query port; the app does not discover servers or guess ports.

| Game/service | Implemented support | Details |
|---|---|---|
| DayZ | UDP A2S_INFO, challenge and Source split replies | Published DayZ clock/queue fields are optional |
| Rust | Generic Source A2S_INFO | [Facepunch's A2S description](https://support.facepunchstudios.com/hc/en-us/articles/360019452718-No-servers-showing) |
| Counter-Strike: Source / CS2 | Generic Source A2S_INFO | Query-enabled dedicated servers; matchmaking/coordinator health is separate |
| Counter-Strike 1.6 | Source format and single-packet GoldSource 0x6D | Legacy GoldSource split packets and compressed BZip2 replies show N/A |
| Project Zomboid | Generic A2S where Steam queries are enabled | [B41 developer note](https://projectzomboid.com/blog/news/2022/09/the-good-life/) places Steam queries on the game port; check the deployed build/config |
| Minecraft Java/Bedrock | Not implemented | Java needs TCP Server List Ping; optional GameSpy query is separate. Bedrock needs UDP/RakNet. [Paper's enable-status setting](https://docs.papermc.io/paper/reference/server-properties/) |
| PlanetSide 2 | Not implemented | [Daybreak Census](https://census.daybreakgames.com/) has REST/events, service IDs and rate limits |
| WARDOGS | Not implemented | [BULKHEAD/Team17 game](https://store.steampowered.com/app/1867240/). A supported direct-query interface has not been verified here |
| Discord, Spotify, WhatsApp and Steam | Optional service checks | Operator report or local web reachability, depending on service. [Setup](INTEGRATIONS.md) |

DayZ has live MAG/Sunnyvale query checks as well as fixtures. Rust, CS and Project Zomboid have protocol/parser support but no live server acceptance recorded here. WARDOGS third-party listings have not been treated as an official API.

Saved profiles sort by name. Each restart plan is user configuration, not a game default. Unsupported game clock/queue fields stay N/A. The local session counter tracks DayZ only.

A future adapter needs its own transport label, validation and timeout handling. Minecraft TCP timing must not be labelled UDP RTT. An API failure or blocked query cannot establish a game-server outage.
