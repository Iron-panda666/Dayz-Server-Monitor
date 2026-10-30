# ReleasePackages · portable Windows application

**Dayz Server Monitor is portable. No installation is required.** Extract the Windows ZIP into a writable folder and run `DayzServerMonitor.exe`. No administrator rights or separate .NET installation are needed. Settings stay in `config.json`; history stays in `data`. Keep both when updating.

## Version 1.9.0

| Download | Purpose |
|---|---|
| [Portable Windows x64 ZIP — no installation](https://github.com/Iron-panda666/Dayz-Server-Monitor/releases/download/v1.9.0/Dayz-Server-Monitor-1.9.0-win-x64.zip) | Monitor EXE, documentation and optional Stream Deck installer together |
| [Stream Deck plugin installer](https://github.com/Iron-panda666/Dayz-Server-Monitor/releases/download/v1.9.0/Dayz-Server-Monitor-1.9.0.streamDeckPlugin) | Optional standalone copy of the installer already inside the Windows ZIP |
| [Source ZIP](https://github.com/Iron-panda666/Dayz-Server-Monitor/releases/download/v1.9.0/Dayz-Server-Monitor-1.9.0-source.zip) | Source matching the released application |
| [Published SHA-256 checksums](https://github.com/Iron-panda666/Dayz-Server-Monitor/releases/download/v1.9.0/SHA256SUMS-1.9.0.txt) | Verify the published downloads |

The repository and its downloads are private. Sign in with a GitHub account that has access.

The monitor stays portable. The optional `.streamDeckPlugin` is installed separately **into Elgato Stream Deck**. It does not install or automatically start the monitor.

Local builds create `ReleasePackages/v1.9.0/` with a clearly named `Dayz-Server-Monitor-1.9.0-portable-win-x64.zip`, plugin, source, instructions, `package.json` and `SHA256SUMS.txt`. The portable ZIP is a byte-identical copy of the Windows ZIP linked above; the local checksum file uses its portable filename. Generated binary folders are kept out of source Git history; downloadable binaries are attached to the GitHub release.

Run `build.ps1` for future versions. To collect an already built version without rebuilding or launching anything, use `tools/New-ReleasePackages.ps1` with the version and existing artifact paths. Packaging verifies that the EXE and optional plugin are inside the Windows ZIP and excludes private runtime configuration/history.

**Česky:** Jde o přenosnou verzi **bez instalace**. Stačí rozbalit ZIP a spustit EXE. Při aktualizaci ponech `config.json` a `data`. Samostatnou instalaci do aplikace Elgato vyžaduje pouze volitelný plugin pro Stream Deck.
