# Displays and window controls

The dashboard reuses its controls when the window changes shape. A wide window puts metrics beside the clock; portrait mode stacks them. Resizing keeps the samples and cooldown without sending another query.

The title bar is 32 device-independent pixels high. At 150% scaling it occupies 48 physical pixels. Refresh, settings and the More menu are in that bar.

Choose More -> Display size preset or Settings -> Appearance. There are 27 presets: Auto, Desktop, twelve landscape/portrait pairs and Square. Sizes are physical-pixel targets, limited by Windows DPI and the available screen area. They do not change Windows resolution or rotation. Auto keeps the current window size; manual resizing remains available.

| Control | Action |
|---|---|
| F11 / Esc | Enter / leave fullscreen |
| F5 | Request a refresh, subject to the 20 s limit |
| Ctrl+, | Open settings |
| Right-click / Shift+F10 | Open the dashboard menu |

Fullscreen hides the title bar. It also hides below 260 logical pixels wide; the context menu remains available. Settings move to a larger connected screen when the secondary LCD is too small.

## Panel references

These manufacturer references were used for layout targets. No physical panel acceptance has been recorded for this app. Portrait dimensions assume rotation in Windows.

| Panel | Landscape pixels | Portrait pixels | Notes |
|---|---|---|---|
| [Waveshare 3.5" HDMI](https://www.waveshare.com/wiki/3.5inch_HDMI_LCD) | 480 x 320 | 320 x 480 | Small case display |
| [Waveshare 5" HDMI H](https://www.waveshare.com/5inch-HDMI-LCD-H.htm) | 800 x 480 | 480 x 800 | Compact secondary screen |
| [Waveshare 7" HDMI H](https://files.waveshare.com/upload/5/58/7inch_HDMI_LCD_%28H%29_User_Manual.pdf) | 1024 x 600 | 600 x 1024 | Native resolution differs from some accepted input timings |
| [Waveshare 7.9" Touch Monitor](https://docs.waveshare.net/7.9inch_Touch_Monitor/) | 1280 x 400 | 400 x 1280 | Enclosure 211 x 73 x 20 mm; display area about 191 x 60 mm |
| [Waveshare 8.8" Side Monitor](https://www.waveshare.com/wiki/8.8inch_Side_Monitor) | 1920 x 480 | 480 x 1920 | HDMI video, separate USB power |
| [Waveshare 11.9" Touch Monitor](https://www.waveshare.com/wiki/11.9inch_Touch_Monitor) | 1480 x 320 | 320 x 1480 | Enclosure 290.5 x 73 x 21.5 mm |
| [Thermaltake 3.9" LCD kit](https://thermaltake.com/products/3-9-lcd-screen-kit) | 480 x 128 content | 128 x 480 | TT RGB Plus USB panel, not verified as a Windows desktop display |

Dimensions are the manufacturers' figures, not measurements made for this project. The 11.9" instructions contain a conflicting 400 x 1280 sentence; the product parameters and timing table specify 320 x 1480, which the preset uses.

Other targets are 1280 x 800, 1366 x 768, 1920 x 1080, 1920 x 1200, 2560 x 1440, their rotated forms and 720 x 720. Micro ribbon is a window-size target, not a Thermaltake driver.

The app needs a display exposed to Windows as an extended desktop. HDMI/DisplayPort, USB-C video and USB display adapters with suitable drivers fit that model. USB power/touch or a vendor command channel alone does not. JPG/GIF sensor panels and Pi DSI/SPI panels need different integrations; those are not implemented. AIDA64 is not required.

## Layouts

| Shape | Arrangement |
|---|---|
| Tall desktop | Clock, countdown, metrics, history and details |
| Short/wide | Clock/details left, countdown/metrics/graph right |
| Wide strip | Clock, countdown, metric column and graph across |
| Portrait | Clock/countdown above status, players, RTT and history |
| 480 x 320 class | Essential tiles; graph/details return with more room |
| Shallow ribbon | Clock, restart, players, status and RTT with smaller labels removed |

Small modes may hide world clocks, details or the graph. Collection continues. Compact mode explicitly hides the graph and address details. Check readability on the actual LCD at the intended distance.

Version 1.10.1 centers the dashboard labels and values. Regular labels use 13-17 logical pixels; the clock and countdown resize separately. Tall clock cards stack the extra information below the time. The address panel keeps its font size, truncates long lines with a tooltip and hides secondary rows when space runs out. Recovery/storage messages can be scrolled. Small LCD and ribbon layouts still have physical readability limits.

![Wide layout with sample data](images/layout-wide.png)

![1280 x 400 layout with sample data](images/layout-bar-79.png)

These renders use the WPF dashboard without the OS window frame. They are not photos of connected panels.

## Themes and closing

The ten [themes](THEMES.md) cover the whole window. Luna/Glass use original gradients, without Aero blur; Sakura has no character artwork and Terminal uses no animation.

Settings are in `config.json` beside the EXE or in the `--data-dir` folder. Window bounds and screen choice are saved along with the selected theme, profiles and clocks.

The close button quits by default. Settings -> App behavior -> Close button minimizes instead of quitting changes it to taskbar minimization or hiding beside the clock. Double-click the tray icon or use Open monitor to restore it. Windows may put the icon in the overflow area.

Monitoring, alerts and Stream Deck continue while hidden. Quit monitor in the dashboard/tray menu stops them. Quit and Windows session termination bypass minimize-on-close.

For an update, close the app and unpack the new ZIP into its writable folder, keeping `config.json` and `data`. The optional Stream Deck installer is included; install it when updating the plugin too.
