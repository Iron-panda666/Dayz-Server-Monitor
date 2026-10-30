# Languages and world clocks

English is the default. Settings -> Appearance -> Application language offers EN, CZ, PL, DE and ES. Saving applies the language without restarting. Translations are embedded; server names and custom city labels stay as entered. Status codes, time-zone IDs and original OS/query diagnostics remain untranslated.

`Language` stores `en`, `cs`, `pl`, `de` or `es`. Older configs without the field use English. Language changes leave the restart schedule and query endpoint alone.

## World clocks

Open Settings -> Time & clocks -> Add or edit a world clock. Pick a preset or time zone, enter a city label and optional country code, then use Add clock. To edit one, select its row and use Update selected. Save settings applies the list. Up to eight clocks; removal, reordering and hiding are supported.

Presets: Prague, London, Berlin, Warsaw, Madrid, New York, Los Angeles, Tokyo, Sydney, Dubai, Delhi and UTC. The zone list also includes Windows zones. The city name is just a label; choose its time zone explicitly.

Each clock shows city/country, local time, date and UTC offset. Windows/.NET time-zone rules handle DST, including different US/European change dates, India's half-hour offset and Australian seasons. The 12/24-hour setting applies to all clocks.

The main clock and restart plan stay in Europe/Prague. World clocks only affect the display. Controls and resolved zones are built when settings are applied; each tick updates their text from the same time source.

## JSON example

Merge these fields into the existing configuration:

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

IANA IDs and Windows IDs work where the installed system supports them. Invalid zones or more than eight clocks are rejected.

When moving to a new build folder, close the old copy and carry over `config.json` and `data`. Alternatively use `--data-dir` with the existing folder. Only one monitor instance can use that folder at a time.

`world-clocks.png` is an offscreen render at a fixed timestamp. Test coverage is recorded in [VALIDATION.md](VALIDATION.md).
