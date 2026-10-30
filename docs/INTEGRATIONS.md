# Multiple monitors, Stream Deck, Grafana and Home Assistant

These features are included in **1.9.0**. They are optional; existing settings keep background monitoring, service checks and HTTP export off.

## Keep several game servers running

1. Open **Settings → Servers** and choose a saved server to edit it.
2. Check **Monitor this server in the background**. Repeat for the servers you want; switching profiles keeps valid edits in the dialog draft.
3. Choose **Save settings** to apply the draft. Cancel discards edits and deletions.
4. On each Stream Deck key, choose **Server for this key → Reload monitored servers**, then select its server. Leave **Follow desktop selection** for the original behavior.

Up to 16 saved servers can be enabled, in addition to the desktop selection. Duplicate IP/query-port pairs are queried once. The desktop server is excluded from background polling while selected. Every endpoint has its own restart schedule, cached metrics, bounded history and shared twenty-second manual refresh restriction. The normal/before-restart/restart intervals remain the global settings. Background queries run one at a time, so a group of timeouts can delay its other samples; the desktop query remains independent. Each key's history contains at most 120 observations from the current app run.

All game observations are also appended to the existing local JSONL history, with their endpoint. The history file's retention and 16 MB/day cap apply to all servers together. Background restart warnings appear on the relevant Stream Deck keys. Desktop sounds/Windows notifications follow the desktop selection. Selecting pages on a key and reading exported data never sends an extra game query. A disabled/deleted target shows **SERVER / N/A / CHECK SELECTION**, without silently switching that key to another server. App and plugin 1.9+ are needed for targeting.

## Export cached readings

In **Settings → Integrations**, enable **read-only HTTP export**. The default is `127.0.0.1:9187`, accessible from the same PC only.

- `http://127.0.0.1:9187/metrics`: Prometheus text format for collection by Prometheus and display in Grafana.
- `http://127.0.0.1:9187/api/snapshots`: JSON containing a `servers` array for Home Assistant REST sensors or another local dashboard.

The exporter returns cached state only. It does not accept refresh commands, expose configuration/history files or make automatic firewall changes. It uses a normal user TCP port; URL reservations and administrator privileges are unnecessary. It runs only while the monitor is running, including when hidden in the tray. A port/bind error appears in the monitor's error area.

To use another machine on your LAN, enter this Windows PC's **specific LAN IPv4 address** in settings, then use that address in the examples. The endpoint has no authentication/TLS and exposes server names, IPs, measurements and the local session duration. Use it only on a trusted private network; do not forward the port to the internet. Windows Firewall may need a private-network rule that you manage yourself. `0.0.0.0` is rejected to avoid accidentally exposing every interface. The app stores no Grafana/HA credentials and needs no paid service.

## Prometheus → Grafana

Add to your Prometheus configuration; replace the example address with the monitor PC's configured address:

```yaml
scrape_configs:
  - job_name: dayz_monitor
    scrape_interval: 30s
    static_configs:
      - targets: ['192.168.1.50:9187']
```

Use Prometheus as your Grafana data source. Example queries:

```promql
dayz_monitor_players{endpoint="217.182.198.57:2303"}
dayz_monitor_query_rtt_seconds * 1000
dayz_monitor_next_restart_timestamp_seconds - time()
dayz_monitor_query_success
```

The exporter provides players, capacity, queue, UDP RTT in seconds, last-success Unix timestamp, next configured restart and schedule confirmation. Missing/failed player, capacity, queue and RTT measurements are **omitted**, rather than fabricated as zero. `query_success = 0` means the last query failed; it does not prove the game is offline. Grafana collection does not increase the game's polling rate. Check the last-success timestamp to distinguish old cached observations from recent ones. Configure Grafana's no-data display/alerts accordingly. The restart timestamp always refers to the configured schedule, including an unconfirmed plan.

Service readings use `dayz_monitor_service_ok` with a `scope` label. Provider incidents produce zero; unknown checks are omitted. `WebReachability = 1` means the web endpoint replied from this PC, not that login, matchmaking, music streaming or messaging are working globally.

## Home Assistant REST sensors

This example shares one HTTP fetch between status and players. Replace the address and endpoint. Numeric unavailable values become `none`, not zero.

```yaml
rest:
  - resource: http://192.168.1.50:9187/api/snapshots
    scan_interval: 60
    timeout: 5
    sensor:
      - name: MAG Deer Query Status
        unique_id: mag_deer_query_status
        value_template: >-
          {% set rows = value_json.servers | selectattr('endpoint', 'eq', '217.182.198.57:2303') | list %}
          {{ rows[0].status if rows else 'unavailable' }}
      - name: MAG Deer Players
        unique_id: mag_deer_players
        unit_of_measurement: players
        state_class: measurement
        value_template: >-
          {% set rows = value_json.servers | selectattr('endpoint', 'eq', '217.182.198.57:2303') | list %}
          {{ rows[0].players if rows and rows[0].querySuccess and rows[0].players is not none else none }}
```

The JSON also exposes `capacity`, `queue`, `rttMs`, `lastQueryAt`, `lastSuccessAt`, `nextRestartAt`, `scheduleConfirmed` and optional `gameSession`. Service entries have `service` with state, source URL, scope, check time and description. Use the stable `endpoint` field rather than an array index. If the monitor is closed or PC sleeps, HA's fetch fails and sensors become unavailable. Historical JSONL is not bulk-imported into Grafana/HA; collection starts with current snapshots. No MQTT discovery, remote write/push or cloud account setup is included.

## Spotify, Discord, WhatsApp and Steam availability

Enable **Monitor Discord, Spotify, WhatsApp and Steam services** in the Integrations tab. Save settings; live results are visible when reopening that tab. They also appear in Stream Deck's server selector and HTTP JSON. Fixed public endpoints are checked every five minutes, with the same twenty-second manual refresh guard per service.

| Service | Source | Meaning |
|---|---|---|
| Discord | [Official status JSON](https://discordstatus.com/api/v2/status.json) | Provider report: OK or ISSUE; inaccessible/unrecognized replies are N/A. A provider report may lag an incident. |
| Spotify | [Spotify web player](https://open.spotify.com/) | WEB OK: its web entry point replied from this PC; playback/account/backend not tested. |
| WhatsApp | [WhatsApp Web](https://web.whatsapp.com/) | WEB OK: web entry point only. Meta's WhatsApp Business API status does not establish consumer WhatsApp availability. |
| Steam Store | [Steam store](https://store.steampowered.com/) | WEB OK: store front door only, not Steam login, community, downloads or game coordinators. |

Redirects count as an answering web entry point. HTTP blocks, timeouts, connection errors and malformed operator replies show **N/A / CHECK UNKNOWN**, never a global OFFLINE verdict. Credentials, messages, local client state and account sessions are not accessed. Each selected service key shows SERVICE automatically, regardless of its saved game-information page. Further monitoring types and limits are described in [monitor types](MONITOR-TYPES.md).

The sample configuration follows the official [Home Assistant REST integration](https://www.home-assistant.io/integrations/rest/) and [Prometheus exposition format](https://prometheus.io/docs/instrumenting/exposition_formats/). Export transport is covered by local HTTP fixtures; a real Grafana/HA installation and its dashboard are not configured or validated by the build.
