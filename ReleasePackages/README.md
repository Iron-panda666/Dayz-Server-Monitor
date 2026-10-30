# ReleasePackages

Portable build for Windows 10/11 x64. Unpack the ZIP into a writable folder and run `DayzServerMonitor.exe`. The .NET runtime is included. No setup or admin rights needed.

Close the running copy before updating. Keep `config.json` and `data`.

## Downloads for 1.9.2

| File | Contents |
|---|---|
| [Windows x64 ZIP](https://github.com/Iron-panda666/Dayz-Server-Monitor/releases/download/v1.9.2/Dayz-Server-Monitor-1.9.2-win-x64.zip) | EXE, documentation and optional Stream Deck installer |
| [Stream Deck installer](https://github.com/Iron-panda666/Dayz-Server-Monitor/releases/download/v1.9.2/Dayz-Server-Monitor-1.9.2.streamDeckPlugin) | Same installer as the one in the Windows ZIP |
| [Source ZIP](https://github.com/Iron-panda666/Dayz-Server-Monitor/releases/download/v1.9.2/Dayz-Server-Monitor-1.9.2-source.zip) | Sources for this release |
| [SHA-256 checksums](https://github.com/Iron-panda666/Dayz-Server-Monitor/releases/download/v1.9.2/SHA256SUMS-1.9.2.txt) | Checksums for the published files |

The repo is private. Downloads require a GitHub account with access.

Only the optional `.streamDeckPlugin` is installed, into Elgato Stream Deck. The monitor itself stays portable and the plugin does not start it.

A local build puts the files in `ReleasePackages/v1.9.2/`, with `README.txt`, `package.json` and `SHA256SUMS.txt`. Its `Dayz-Server-Monitor-1.9.2-portable-win-x64.zip` is identical to the published Windows ZIP; only the name differs. Generated packages are ignored by Git and uploaded as release assets.

Run `build.ps1` to build and package. To collect existing artifacts, use `tools/New-ReleasePackages.ps1` with their paths and version. It checks the embedded EXE and plugin against the supplied files and rejects private config/history in the ZIP. Neither command installs or opens the app.

Česky: přenosná verze bez instalace. Rozbal ZIP a spusť EXE. Při aktualizaci ponech `config.json` a `data`. Volitelný plugin se instaluje do Elgato Stream Deck.
