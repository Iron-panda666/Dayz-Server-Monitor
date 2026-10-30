# Themes

Choose Settings -> Appearance -> Whole-window theme. The palette covers cards, inputs, menus, text and graphs. Selecting a palette previews it immediately in the dashboard and settings. Cancel restores the previous appearance; Save settings stores the choice in config.json. [Full preview gallery](GALLERY.md).

| Theme / config key | Background | Cards | Main numbers | Accent |
|---|---|---|---|---|
| Light | #EEF3F7 | #FFFFFF | #102438 | #005DA8 |
| Dark | #171C23 | #252D38 | #FFFFFF | #90E8FF |
| Orange | #211B17 | #342920 | #FFFFFF | #FFB15C |
| Gray/green retro / Spotify | #313131 | #333333 | #FFFFFF | #BDF39B |
| Ocean | #0B1D30 | #142E48 | #FFFFFF | #7CD8FF |
| Luna / XP | #ECE9D8 | #FFFEF5 | #102438 | #174CA5 |
| Glass / Aero | #E8F3F9 | #F8FCFF | #102438 | #075D91 |
| Sakura / Anime | #231A30 | #34243F | #FFF4FC | #FFB4DF |
| Terminal / Matrix | #030A06 | #091A10 | #CAFFD8 | #63FF94 |
| Violet | #1B1730 | #2B2545 | #FFFFFF | #C8B4FF |

The retro palette keeps the old config key Spotify. Its colors were inspired by a historical Windows Mobile theme; no extracted assets are shipped. Terminal is static. Glass/Aero can use the native Desktop Acrylic backdrop on Windows 11 build 22621 or later, when enabled and no wallpaper is selected. Unsupported systems or disabled system effects fall back to the palette. This is not the Windows Vista Aero implementation. Native glass still needs physical testing.

Dashboard background offers Follow theme, No wallpaper, original Sakura artwork, Mount Fuji and a local PNG/JPG. Follow theme uses Sakura artwork for the Anime palette. Other palettes have no wallpaper by default. Fuji is by Clay Banks under the Unsplash License; the app's MIT license does not replace it. [Photo notice](licenses/FUJI-PHOTO.md).

Local images are loaded only on this PC, with a 25 MB file limit, 40-million-pixel source limit and decoding capped to 1920 x 1080. Missing or invalid images fall back to the palette. Their paths stay in the private config; images and paths are absent from Stream Deck/export snapshots. Keep your image file available when moving a portable installation.

Automated checks verify text contrast and changes to existing controls when switching themes. Main player numbers exceed 7:1 against their cards.

![Theme preview renders](images/theme-comparison.png)

The image uses the actual WPF styles with fixed sample data.
