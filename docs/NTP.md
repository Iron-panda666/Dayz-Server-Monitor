# NTP time source

Open Settings -> Time & clocks and enable Use an NTP time server in this app. Presets are `time.cloudflare.com` and `pool.ntp.org`; a hostname or IP can also be entered. NTP uses UDP 123. Save settings applies the source.

Defaults are a 60-minute interval and 4 s timeout for DNS plus UDP. The interval accepts 2-1440 minutes, the timeout 1-15 s. Sync now under the main clock allows manual requests at least 64 s apart.

A RATE reply extends the wait to at least 15 minutes. DENY/RSTR stops requests until the host or NTP enable setting changes. Other failures retry from two minutes with exponential backoff, capped by the normal interval.

## Clock behavior

NTP supplies time for the Prague/world/offset clocks, countdown, warnings, query observations and new history timestamps. Restart slots still use Europe/Prague. History keeps `TimeSource`, selected-source UTC time and the original Windows `SystemAt` value.

Windows time settings and the Windows Time service are left alone. The accelerated DayZ clock is a separate value published by the game server.

| Clock label | State |
|---|---|
| Time source: Windows | NTP disabled |
| NTP | A validated sample is in use; hostname, offset from Windows and last sync/delay are shown |
| NTP holdover | Refresh failed, but the previous sample is still valid |
| Windows time / NTP unavailable or expired | No valid sample; marked fallback to Windows time |

A positive offset means the app's NTP time is ahead of Windows. A sample lasts for the greater of 30 minutes and three sync intervals, so three hours at the default interval. It advances on a monotonic timer and is not saved across app restarts.

Changing the host, disabling NTP or resuming from sleep clears calibration. Poll cadence uses monotonic elapsed time regardless of the displayed source. A clock-basis jump over 5 s resets availability observations; prior history remains on disk. Alert IDs use scheduled UTC restart instants to avoid repeat warnings on refresh/source changes.

## Implementation

The unicast SNTP client uses NTP v3/v4 packets and four timestamps for offset/delay. It validates packet size, version, server mode, leap/sync state, stratum, originate timestamp, nonzero receive/transmit timestamps and ordering. The UDP socket is connected to the resolved endpoint. DNS and UDP share a cancellable deadline; sync attempts cannot overlap. Timestamp-era unfolding covers the 2036 rollover.

This is one plain SNTP source, without NTS authentication or full system-NTP filtering. It needs a roughly correct local date to select the era. Path asymmetry and clock drift affect accuracy; displayed milliseconds are measurements, not an accuracy guarantee. Leap seconds are picked up at the next sync. There is no separate leap-second clock or mixing of sources with different smear policies.

References: [RFC 5905](https://datatracker.ietf.org/doc/html/rfc5905), [Cloudflare Time Services](https://developers.cloudflare.com/time-services/ntp/) and [NTP Pool usage](https://www.ntppool.org/en/use.html).

## Recorded check

Eight automated NTP checks cover the formulas, reply validation, rate/denial handling, era rollover, clock jumps, expiry, warning timing, configuration and local UDP exchange/cancellation.

A query to `time.cloudflare.com` on 28 October 2026 at 22:28:08 Prague time returned stratum 3, about -0.9 ms offset from Windows and 4.9 ms estimated network delay. That was one sample. No Windows time settings were changed. [Other test results](VALIDATION.md).
