# Included DayZ servers

The built-in list was checked on **29 October 2026**. Each endpoint below returned A2S_INFO. The app contacts only the selected server; it does not query the entire list in the background or depend on a web directory at runtime.

| Server | Game address | UDP query port |
|---|---|---:|
| MAG #1 PVE Chernarus | 54.36.109.179:2302 | 2303 |
| MAG #2 PVE Deer Isle | 217.182.198.57:2302 | 2303 |
| MAG #3 Vanilla+ | 57.129.39.17:2302 | 2303 |
| MAG #5 Badlands Vanilla+ | 57.129.39.17:2802 | 2803 |
| MAG Test | 5.252.100.201:2602 | 2603 |
| Sunnyvale #1 Livonia · PvP Clans | 172.111.51.75:2302 | 2303 |
| Sunnyvale #2 Chernarus · PvP Solo/Duo/Trio | 172.111.51.75:2402 | 2403 |
| Sunnyvale #3 Chernarus · PvE / PvP zones | 172.111.51.90:2302 | 2303 |
| Sunnyvale #4 Banov · PvE / PvP zones | 172.111.51.90:2502 | 2503 |
| Sunnyvale #5 Deer Isle · PvP 8 Man | 172.111.51.169:2302 | 2303 |
| Sunnyvale #6 Chernarus · PvP Clans | 172.111.51.75:2502 | 2503 |
| Sunnyvale #7 Frostline Sakhal · PvE / PvP zones | 172.111.51.90:2402 | 2403 |
| Sunnyvale #8 In Development | 172.111.51.169:2402 | 2403 |
| Sunnyvale #9 Namalsk · PvE / PvP zones | 172.111.51.80:2402 | 2403 |
| Sunnyvale #10 Livonia · PvE / PvP zones | 172.111.51.135:2302 | 2303 |
| Sunnyvale #11 Chernarus · PvE / PvP zones | 172.111.51.135:2402 | 2403 |
| Sunnyvale #12 Deer Isle · PvE / PvP zones | 172.111.51.80:2302 | 2303 |

Sources: [MAG homepage](https://www.middleagedgamers.co.uk/new/), [MAG direct-connect selector](https://www.middleagedgamers.co.uk/new/dayz-direct-connect/), [MAG June 2026 update](https://www.middleagedgamers.co.uk/forum/index.php?topic=16729.0), [Sunnyvale DayZ list](https://sunnyvaleservers.com/servers/dayz-servers/). MAG Badlands was also listed by [DayZ Beans](https://dayzbeanslauncher.com/metrics/server/mag-middleagedgamers-5-badlands-vanilla-295593) and then checked directly.

Some website labels were outdated. Sunnyvale numbers and maps above follow the actual UDP replies. MAG's old Namalsk address `57.129.39.17:2402` is retired according to its June announcement; neither its query endpoint nor the old Alteria query at `57.129.39.17:2603` replied during this check, so they are not presented as current presets. No replacement MAG #4 endpoint was verified. MAG Test and Sunnyvale #8 are listed as test/development servers. The list covers DayZ, not the operators' other games.

A response confirms a query endpoint at that moment, not the ability to join the game. Server names, maps and addresses can change. Restart times have not been verified for these providers: all newly added profiles start with an **unconfirmed example schedule**.

The list is merged once into an existing config, using IP and query port to avoid duplicates. Existing custom names and plans are retained. Removing a preset after that migration does not make it reappear at the next launch. The fifty-profile limit still applies; a full user list is never overwritten.
