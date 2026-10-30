# DayZ server presets

The default catalog has 16 entries. UDP observations below date from 29 October 2026; MAG address references were checked against the operator's pages on 30 October. The catalog is embedded, so runtime queries do not depend on a server-list website.

| Server | Game address | UDP query port |
|---|---|---:|
| MAG #1 PVE Chernarus | 54.36.109.179:2302 | 2303 |
| MAG #2 PVE Deer Isle | 217.182.198.57:2302 | 2303 |
| MAG #3 Vanilla+ | 57.129.39.17:2302 | 2303 |
| MAG Test | 5.252.100.201:2602 | 2603 |
| Sunnyvale #1 Livonia / PvP Clans | 172.111.51.75:2302 | 2303 |
| Sunnyvale #2 Chernarus / PvP Solo/Duo/Trio | 172.111.51.75:2402 | 2403 |
| Sunnyvale #3 Chernarus / PvE / PvP zones | 172.111.51.90:2302 | 2303 |
| Sunnyvale #4 Banov / PvE / PvP zones | 172.111.51.90:2502 | 2503 |
| Sunnyvale #5 Deer Isle / PvP 8 Man | 172.111.51.169:2302 | 2303 |
| Sunnyvale #6 Chernarus / PvP Clans | 172.111.51.75:2502 | 2503 |
| Sunnyvale #7 Frostline Sakhal / PvE / PvP zones | 172.111.51.90:2402 | 2403 |
| Sunnyvale #8 In Development | 172.111.51.169:2402 | 2403 |
| Sunnyvale #9 Namalsk / PvE / PvP zones | 172.111.51.80:2402 | 2403 |
| Sunnyvale #10 Livonia / PvE / PvP zones | 172.111.51.135:2302 | 2303 |
| Sunnyvale #11 Chernarus / PvE / PvP zones | 172.111.51.135:2402 | 2403 |
| Sunnyvale #12 Deer Isle / PvE / PvP zones | 172.111.51.80:2302 | 2303 |

Sources: [MAG homepage](https://www.middleagedgamers.co.uk/new/), [direct-connect selector](https://www.middleagedgamers.co.uk/new/dayz-direct-connect/), [June 2026 announcement](https://www.middleagedgamers.co.uk/forum/index.php?topic=16729.0) and [Sunnyvale DayZ list](https://sunnyvaleservers.com/servers/dayz-servers/).

## MAG labels and address checks

The June announcement lists #3 Vanilla+ at `57.129.39.17:2302`. The older direct-connect selector calls it #4 Chernarus Vanilla and also lists the event/test server at `5.252.100.201:2602`. The preset's old OFFICIAL prefix was removed. It refers to a MAG community server.

Badlands at `57.129.39.17:2802`, query `2803`, replied on 29 October, but that port was not found on the checked official pages. New catalogs omit it. Existing configs retain the profile with an address unverified label. The shared IP and server name do not establish its owner; there is no evidence here that it is an impersonator.

The old Namalsk address `57.129.39.17:2402` was retired according to the June announcement. Its query port and the old Alteria query at `57.129.39.17:2603` did not reply during the check. No replacement MAG #4 address was verified.

MAG address listed means the exact IP/game-port pair appears in the four-entry reference checked on 30 October. Other MAG-like names get an unverified note. This is a dated address reference, not authenticated ownership or a live provider lookup.

Sunnyvale names/maps follow the recorded UDP replies where website labels differed. MAG Test and Sunnyvale #8 are test/development entries. The catalog covers DayZ, not the operators' other games.

## Profiles

Only the selected server and profiles enabled for background monitoring are queried. A successful reply confirms that query endpoint at that time; it does not confirm game login or a restart plan. Included schedules are unverified examples.

Catalog migration adds missing IP/query-port pairs once, preserving custom names and plans. Removing a preset after migration does not restore it at the next launch. The limit is 50 saved profiles; a full list is not overwritten. Selectors sort by name without changing profile identity.
