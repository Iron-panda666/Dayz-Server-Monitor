# Settings

Settings in 1.9.1 have seven sections. The list is on the left; below 680 logical pixels wide it becomes a dropdown. Form fields stack vertically in narrow windows. Each page scrolls, with Cancel and Save settings kept below it.

![Settings with example values](images/settings-overview.png)

| Section | Contents |
|---|---|
| Servers | Saved profiles, New/Delete, name, IP and ports, background monitoring |
| Restarts & alerts | Selected server's Prague schedule, confirmation, known kick lead, countdown target and notification style |
| Monitoring & data | Poll intervals, timeout/failure rules, offline confirmation, local session and history |
| Appearance | Live theme/background preview, glass option, language, display/size preset, widget mode, compact mode and always on top |
| Time & clocks | 12/24-hour format, offset display, NTP and world clocks |
| Integrations | Stream Deck setup notes, service checks and Grafana/Home Assistant export |
| App behavior | Close-to-tray/taskbar and startup with Windows |

The profile name above Servers and Restarts identifies the server being edited. Poll intervals and notification style are shared settings. Restart times use Europe/Prague.

Kick settings belong to that server. MAG DEER starts with a user-reported 5-minute lead; other endpoints remain unknown. Editing an address clears the kick choice, so it is not copied to another server by accident. Count down to player kick is available only when the kick lead is known. The actual restart schedule and export restart timestamp do not move.

Theme and wallpaper changes preview as soon as selected. Save settings keeps them; Cancel restores the previous appearance. Custom image controls appear only for Local image. System glass applies only to Glass/Aero without a wallpaper, on supported Windows 11 builds. A palette fallback remains available.

## Editing profiles

Choose a saved server to edit it. Switching sections keeps the fields intact. Switching servers keeps valid changes in the dialog's draft, so several profiles can be edited before one Save settings. The profile selected at that point becomes the desktop selection.

New server keeps the current valid draft and opens an empty form. Leaving an untouched empty form adds nothing. Invalid profile values stop the switch and show an error. Delete server asks for confirmation, protects the last profile and leaves historical data on disk.

Cancel discards profile, world-clock and other setting changes made in this dialog. The configuration is written only after Save settings succeeds.

## Optional controls

The repeating schedule generator, world-clock editor and service results are expandable. NTP/export connection details are collapsed and disabled while the feature is off. Their last saved connection values are retained. Minimize destination is disabled until minimize-on-close is enabled.

Theme and language are at the top of Appearance. Selecting a world clock opens its editor. Details: [world clocks](LANGUAGES-AND-CLOCKS.md), [NTP](NTP.md) and [integrations](INTEGRATIONS.md).

![Restart settings in the dark theme](images/settings-restarts.png)

![Settings in a narrow window](images/settings-narrow.png)

The images are renders of the WPF control with sample data. Layout checks cover all seven sections in five languages, three palettes and 360/720/940 logical widths. Physical monitor and full dialog checks are listed in [VALIDATION.md](VALIDATION.md).
