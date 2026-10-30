# Third-party notices

The application source, documentation and original T/R icon are provided under the [MIT license](LICENSE).

The Windows portable build includes Microsoft .NET 8 and Windows Desktop runtime components. They retain their own notices; the project's MIT license does not replace them. Copies from the runtime packs used for this release are in [docs/licenses](docs/licenses):

- DOTNET-LICENSE.txt
- DOTNET-THIRD-PARTY-NOTICES.txt
- WINDOWS-DESKTOP-LICENSE.txt

There are no third-party NuGet packages in the application projects. A2S and SNTP are implemented in the source tree.

The gray/green theme uses a palette inspired by a historical Windows Mobile music application. No original logos, graphics, fonts, executables or extracted resource files are distributed. The T/R icon is drawn from the included SVG; it is not an official MAG or Sunnyvale logo.

MAG, Sunnyvale, DayZ, Steam and Stream Deck names identify the services, game and devices discussed. This is an independent community tool. Server addresses are public connection details. Their sources and verification date are recorded in docs/SERVERS.md.

The Stream Deck installer additionally bundles @elgato/streamdeck 3.0.4, @elgato/schemas 0.6.0, @elgato/utils 0.6.2, ws 8.22.0 and zod 3.25.76 under their respective MIT notices. Full notices are included in the plugin and docs/licenses/STREAMDECK-RUNTIME-NOTICES.md. The original plugin code remains MIT. Node.js is supplied by the user's Stream Deck installation and is not redistributed in our installer.

Build/test tooling is listed and pinned in streamdeck/package-lock.json. In particular @resvg/resvg-js is an MPL-2.0 build-time SVG renderer; it and its native binary are not included in the installed plugin. No third-party build tool is relicensed by this project's MIT license. See docs/TRADEMARKS.md for descriptive use of third-party names and the independent-project notice.
