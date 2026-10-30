# Multiple servers and data export

Background monitoring, service checks and HTTP export were added in 1.9.0. They are off by default.

## Select servers for Stream Deck

1. Open Settings -> Servers and choose a profile.
2. Enable Monitor this server in the background. Repeat for the profiles you need.
3. Save settings. Switching profiles keeps valid edits in the dialog draft; Cancel discards them.
4. In each key's Stream Deck settings, use Reload monitored servers and choose Server for this key. Follow desktop selection keeps the original behavior.

Up to 16 saved profiles can be enabled alongside the desktop selection. Duplicate IP/query-port pairs are polled once. Each endpoint has its own restart plan, history and 20 s manual refresh guard. Poll intervals are shared settings. Background requests run serially, so several timeouts can delay later samples; the desktop query runs independently.

Each key holds up to 120 samples from the current app run. Game observations also go into the local JSONL files, with a shared 16 MB/day limit and configured retention. Restart warnings appear on the relevant keys. Desktop sounds/Windows notifications follow the desktop selection.

Changing a key's view or reading an export does not query the game. A removed/disabled target shows SERVER / N/A / CHECK SELECTION rather than another server. Target selection needs app and plugin 1.9+.

## HTTP export

Enable read-only HTTP export under Settings -> Integrations. Default bind: `127.0.0.1:9187`, this PC only.

| Path | Format/use |
|---|---|
| `http://127.0.0.1:9187/metrics` | Prometheus text for collection and Grafana |
| `http://127.0.0.1:9187/api/snapshots` | JSON `servers` array for HA REST sensors or another dashboard |

Both paths serve cached state. There are no control commands or config/history file endpoints. Export runs while the monitor is running, including tray mode. Bind/port errors appear in its error area. No URL reservation, admin access or automatic firewall change is needed.

For another LAN machine, bind to this PC's specific LAN IPv4 address and use it in the examples. `0.0.0.0` is rejected. Windows Firewall may need a private-network rule.

**The export has no authentication or TLS. Use a trusted LAN and do not forward it to the internet.** It exposes server names, addresses, measurements and local session duration. Grafana/HA credentials are not stored by the app.

## Prometheus -> Grafana

Replace the address with the monitor PC's configured bind address:

```yaml
scrape_configs:
  - job_name: dayz_monitor
    scrape_interval: 30s
    static_configs:
      - targets: ['192.168.1.50:9187']
```

Select Prometheus as the Grafana data source. Example queries:

```promql
dayz_monitor_players{endpoint="217.182.198.57:2303"}
dayz_monitor_query_rtt_seconds * 1000
dayz_monitor_next_restart_timestamp_seconds - time()
dayz_monitor_query_success
```

Metrics include players, capacity, queue, UDP RTT in seconds, last-success timestamp, next configured restart and schedule confirmation. Missing/failed player, capacity, queue and RTT values are omitted. `query_success = 0` means the last query failed, not a proven game outage. Use last-success time to check sample age and configure Grafana's no-data handling. Restart timestamps follow the configured plan even if it is unconfirmed.

Service metrics use `dayz_monitor_service_ok` and a `scope` label. Operator incidents give zero; unknown checks are omitted. `WebReachability = 1` only means the web endpoint replied from this PC.

## Home Assistant

One HTTP fetch supplies both sensors. Replace the address and endpoint:

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

Missing numeric values return `none`, not zero. JSON also contains `capacity`, `queue`, `rttMs`, query/success times, restart time/confirmation and optional `gameSession`. Services have a `service` object with state, source, scope, check time and description. Select by `endpoint`, not array index.

HA reports unavailable when the monitor stops or PC sleeps. Collection begins with current snapshots; historical JSONL is not imported. There is no MQTT discovery, remote write/push or cloud setup.

## Service checks

Enable Monitor Discord, Spotify, WhatsApp and Steam services, save and reopen Integrations for results. Services also appear in the key selector and JSON. Checks run every five minutes, with a per-service 20 s manual limit.

| Service | Source | What the result covers |
|---|---|---|
| Discord | [Operator status JSON](https://discordstatus.com/api/v2/status.json) | OK/ISSUE from the provider, which may lag an incident |
| Spotify | [Web player](https://open.spotify.com/) | Web entry point from this PC |
| WhatsApp | [WhatsApp Web](https://web.whatsapp.com/) | Web entry point; WhatsApp Business API status does not cover consumer WhatsApp |
| Steam Store | [Store](https://store.steampowered.com/) | Store entry point, not login, community, downloads or game coordinators |

Redirects count as a replying entry point. Blocks, timeouts and invalid status replies show N/A / CHECK UNKNOWN. WEB OK does not prove that playback, messaging or other backends work globally. No account login, messages or local client state is accessed. A service key shows SERVICE regardless of its saved game-information view. [Other monitor types](MONITOR-TYPES.md).

References: [HA REST](https://www.home-assistant.io/integrations/rest/) and [Prometheus format](https://prometheus.io/docs/instrumenting/exposition_formats/). Local export fixtures have passed; a real HA/Grafana deployment and its LAN/firewall setup have not been tested here.
