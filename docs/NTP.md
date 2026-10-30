# Optional NTP time source · 1.4

Open **Settings → Time & clocks** and enable **Use an NTP time server in this app**. Choose `time.cloudflare.com`, `pool.ntp.org`, or type your own hostname/IP address. The destination port is UDP 123. Save settings to apply the source. The default remains Windows time; NTP is opt-in.

The default synchronization interval is 60 minutes and the total DNS + UDP timeout is 4 seconds. Allowed values: interval 2–1440 minutes, timeout 1–15 seconds. **Sync now** under the main clock allows a manual query, with at least 64 seconds between attempts. A server's RATE response lengthens the wait to at least 15 minutes; a DENY/RSTR response prevents further requests until the host or NTP-enabled setting changes. Ordinary failures retry with exponential backoff starting at two minutes, bounded by the configured normal interval.

## What changes

The selected source drives the main Prague clock, every world clock, the calculated offset clock, restart countdown, warning timing, restart observation times and new history timestamps. The restart schedule and its Europe/Prague time zone do not change. New history rows include `TimeSource` and the original `SystemAt` value alongside their selected-source UTC timestamp, so a later review can identify the time basis used.

NTP only corrects this app's view of time. **The app never changes the Windows clock, Windows Time service, system NTP server, privileges or registry time settings.** No administrator access is required. This does not read or change the DayZ game's accelerated clock.

## Status under the main clock

- **Time source: Windows**: NTP is disabled.
- **NTP**: a validated sample from the selected server is in use. The display shows its hostname and the current difference from Windows in milliseconds. Positive difference means the app's NTP clock is ahead of Windows. The smaller line shows the last synchronization and estimated network roundtrip delay.
- **NTP holdover**: the latest refresh failed, but the last successful sample is still valid. The clock continues from that sample using a monotonic timer. With the default interval it is valid for up to three hours; in general the limit is the greater of 30 minutes and three synchronization intervals.
- **Windows time · NTP unavailable or expired**: no valid sample is available or the stored sample has expired. System time is used and the fallback is marked. No sample is persisted across app restarts.

Changing the server or disabling NTP clears the old calibration. Resume from sleep also invalidates it and starts a new synchronization. While a sample is valid, later Windows wall-clock adjustments do not move the app's NTP clock. UDP query scheduling uses monotonic elapsed time independently of the display source. If an effective wall-clock jump greater than five seconds is observed, availability observations are started again so timestamps from different clock bases do not invent a measured outage/recovery. Prior history stays on disk. Alert identifiers remain tied to the scheduled UTC restart instant and therefore survive source changes without being regenerated for each data refresh.

## Implementation and limits

The dependency-free, unicast SNTP client uses the NTP v3/v4 packet format and four timestamps to estimate clock offset and network delay. It checks packet size, version, server mode, synchronization indicator, stratum, exact originate/request timestamp match, nonzero receive/transmit timestamps and timestamp ordering. The UDP socket is connected to the resolved server endpoint. It also handles NTP timestamp-era unfolding across the 2036 rollover. DNS and receive operations share one cancellable deadline and synchronization attempts never overlap.

This is plain SNTP from one chosen server, not an authenticated NTS implementation or the multi-source discipline/filtering algorithm of a full system NTP service. It relies on the selected server and a roughly plausible local date to choose the NTP era. Network path asymmetry and oscillator drift can affect accuracy; the shown millisecond values are measurements, not guaranteed clock accuracy. Leap seconds are handled at the next synchronization; the app does not implement its own leap-second clock. Only one configured source is used, so the client does not silently mix servers with differing leap-smear policies.

References: [NTPv4 specification, RFC 5905](https://datatracker.ietf.org/doc/html/rfc5905), [Cloudflare Time Services](https://developers.cloudflare.com/time-services/ntp/), [NTP Pool usage](https://www.ntppool.org/en/use.html).

## Verification

Eight NTP-specific automated checks cover the four-timestamp offset/delay formula, invalid or mismatched replies, rate limiting and denial, the 2036 era rollover, monotonic operation through Windows time jumps, sample expiry and fallback, warning timing independent of Windows, configuration persistence, and a local UDP exchange including timeout and cancellation.

A single real query to `time.cloudflare.com` on **28 October 2026 at 22:28:08 Prague time** succeeded with stratum 3, approximately **−0.9 ms** difference from Windows and **4.9 ms** estimated network delay. These are historical measurements, not a guarantee of present availability or accuracy. No Windows time settings were modified, and the desktop monitor was not opened during the DayZ session.

The EXE is included in the Windows x64 release package. NTP presets and translations are embedded; no account, key or paid service is required.
