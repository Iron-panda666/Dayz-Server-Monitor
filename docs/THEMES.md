# Themes

Choose Settings -> Appearance -> Whole-window theme. The palette covers cards, inputs, menus, text and graphs. It is saved in `config.json`.

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

The retro palette keeps the old config key `Spotify`. Its colors were inspired by a historical Windows Mobile theme; no extracted assets are shipped. Luna and Glass use gradients, without desktop blur. Terminal is static.

Automated checks verify text contrast and changes to existing controls when switching themes. Main player numbers exceed 7:1 against their cards.

![Theme preview renders](images/theme-comparison.png)

The image uses the actual WPF styles with fixed sample data.
