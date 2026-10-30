# Languages and world clocks · 1.3

The default UI language is **English**. Settings → Display → Application language offers exactly five choices: English (EN), Čeština (CZ), Polski (PL), Deutsch (DE) and Español (ES). The setting is stored as `Language: en/cs/pl/de/es`. A configuration from an older version without this field defaults to English. Saving settings applies the selected language without restarting the monitor. Server names and custom city names remain exactly as entered. Technical status codes (ONLINE, OFFLINE, QUERY UNAVAILABLE, RESTART EXPECTED), time-zone IDs, original diagnostic details and operating-system error messages are not translated.

UI labels, settings, date names, reminder text, query-status explanations and common validation errors have embedded translations. They require no online service or key. Language changes do not change the restart schedule, notification identity or UDP endpoint.

## Add world clocks

Open **Settings → World clocks**. Choose a city preset or select a time zone, enter a city/label and optional country abbreviation, then click **Add clock**. To modify an existing row, select it, edit the fields and click **Update selected**. Finally click **Save settings**. The list supports removal, reordering and temporarily hiding a clock. Up to eight clocks can be stored; all are optional and the default list is empty.

Presets include Prague, London, Berlin, Warsaw, Madrid, New York, Los Angeles, Tokyo, Sydney, Dubai, Delhi and UTC. The time-zone list also offers all zones available in Windows. A custom label may describe any city or place using the selected zone; the app does not infer a zone from the spelling of the city or the country code. Changing only a label does not change the clock's zone.

Each clock shows **City · Country** above its time. The country code may be blank. The small line below the time shows the local date and current UTC offset; this identifies date changes and daylight-saving shifts. The app uses each zone's Windows/.NET daylight-saving rules rather than a fixed numeric offset. US and European clock changes can therefore occur on different dates. India’s half-hour offset and Australian southern-hemisphere daylight saving are supported. The 12/24-hour preference applies to all clocks.

The main clock stays **Prague · CZ / Europe/Prague**, because restart times are configured in Czech local time. Added world clocks are display-only. Their presence, order, labels or time zones never move a scheduled restart. Every tick uses the same absolute system time to avoid clocks drifting apart. Clock controls and resolved time-zone objects are created once when settings are applied; a tick only updates text.

## JSON example

```json
{
  "Language": "en",
  "WorldClocks": [
    { "City": "London", "CountryCode": "GB", "TimeZoneId": "Europe/London", "Enabled": true },
    { "City": "New York", "CountryCode": "US", "TimeZoneId": "America/New_York", "Enabled": true },
    { "City": "Tokyo", "CountryCode": "JP", "TimeZoneId": "Asia/Tokyo", "Enabled": true }
  ]
}
```

These are properties to merge into the existing configuration, not a replacement for all server/window settings. Both IANA zone IDs used above and valid Windows time-zone IDs are accepted when available. Invalid IDs and more than eight entries are rejected without overwriting the original configuration.

Build output is written to the versioned directory under `artifacts`. To carry over preferences, close the old monitor when convenient and copy its `config.json` and optional `data` folder to the new folder, or launch the new EXE with `--data-dir` pointing at the original data directory. Only one monitor can use a given data directory at a time.

The image `world-clocks.png` is an offscreen WPF demonstration at a fixed timestamp, not a live screenshot or current server information. No product window was opened for testing while DayZ was running.
