# Dayz Server Monitor

Monitor DayZ serverů pro Windows 10/11 x64. Vznikl pro druhý displej při hraní. Posílá UDP dotazy, ukládá počty hráčů a ukazuje odpočet do nastaveného restartu. C# / .NET 10, WPF.

[English](README.md) | [Stažení](../../releases/latest)

## Spuštění

Stáhni `Dayz-Server-Monitor-1.10.1-win-x64.zip`, rozbal ho do zapisovatelné složky a spusť `DayzServerMonitor.exe`. Je to portable verze bez instalace. .NET je přibalený, správce ani API klíč nepotřebuješ.

Nastavení je v `config.json`, historie ve složce `data` vedle EXE. Před aktualizací starou kopii zavři a obojí ponech. Jinou datovou složku lze zadat při spuštění:

```powershell
.\DayzServerMonitor.exe --data-dir "D:\Dayz Monitor Data"
```

Při prvním spuštění se nativní část .NET rozbalí do dočasné složky uživatele. Pokud zapneš start s Windows, EXE nepřesouvej. [Obsah balíčků a kontrolní součty](ReleasePackages/README.md).

## Nastavení a měření

Server vybereš nahoře v okně. Předvolby obsahují čtyři MAG adresy a 12 Sunnyvale DayZ serverů. Zdroje adres a data ověření jsou v [seznamu serverů](docs/SERVERS.md).

V Settings -> Appearance, česky Nastavení -> Vzhled, je jazyk a deset motivů. Výchozí je angličtina, další jazyky jsou čeština, polština, němčina a španělština. Uložené servery lze přejmenovat, upravit nebo smazat. Přepnutí profilu ponechá platné úpravy v návrhu. Jedno Uložit nastavení je použije, Zrušit je zahodí. [Návod k nastavení](docs/SETTINGS.md).

A2S_INFO se posílá každých 300 s, posledních 15 minut před restartem po 30 s a v restartovém okně po 15 s. Ruční obnova má 20s ochranu společnou se Stream Deckem. Sleduje se vybraný server a nejvýše 16 profilů zapnutých pro běh na pozadí.

Graf má čas dotazu, podrobnosti po najetí myší a mezery u neúspěšných měření. Zápis `121/121 +21 ve frontě` znamená 121 hráčů ve hře a 21 čekajících. Chybějící údaj je N/A. Měří se UDP query RTT včetně challenge, ne ICMP ping.

Timeout sám neprokazuje výpadek hry. Query port může být blokovaný nebo nemusí odpovídat. Opakované chyby vedou na QUERY UNAVAILABLE; OFFLINE se potvrzuje ručně. Obnovení odpovědí se zaznamená do historie.

Plán restartů je v sekci Restarty a upozornění. Výchozí příklad je 03:00, 07:00, 11:00, 15:00, 19:00 a 23:00 v Europe/Prague, včetně letního času. Pro přibalené servery není ověřený. Upozornění na odhlášení a kick nastavíš po minutách.

Okno se přeuspořádá podle poměru stran. Má 27 velikostních profilů pro běžné monitory, úzké pásy i displeje na výšku. F11 přepíná fullscreen, Esc ho ukončí. Křížek může program ukončit, minimalizovat na panel nebo skrýt k hodinám. Monitoring při skrytí pokračuje. [LCD a ovládání](docs/DISPLAYS.md).

## Hodiny a Stream Deck

Světové hodiny, formát 12/24 h a NTP jsou v sekci Čas a hodiny. NTP upravuje jen čas této aplikace, nastavení Windows nemění. Posun serverového času je výpočet z českého času. [Hodiny](docs/LANGUAGES-AND-CLOCKS.md) | [NTP](docs/NTP.md).

Délka session vychází ze spuštění skutečného procesu DayZ. Steam ani launcher se nepočítá, herní menu ano. Není to délka připojení k serveru ani přesný čítač Steam overlaye. Paměť hry a chat se nečtou.

V ZIPu je `Dayz-Server-Monitor-1.10.1.streamDeckPlugin` pro MK.2. Krátký stisk střídá pohledy, podržení jednu sekundu vyžádá obnovu. Každá klávesa může sledovat jiný zapnutý server. Ukazuje herní čas, restart, hráče, graf, UDP RTT, upomínku odhlášení nebo místní session. Herní čas musí server zveřejňovat; dopočet a den/noc jsou odhady. [Instalace a význam údajů](docs/STREAM-DECK.md).

V Integracích lze zapnout export pro Prometheus/Grafanu a REST senzory Home Assistant. Čte už naměřená data, neposílá další dotazy na hru. Volitelné kontroly služeb berou hlášení Discordu a dostupnost webů Spotify, WhatsApp a Steam z tohoto PC. Odpověď webu neprokazuje funkčnost celé služby. [Příklady konfigurace](docs/INTEGRATIONS.md) | [Podpora dalších her](docs/MONITOR-TYPES.md).

## Sestavení

Na Windows s .NET SDK 10.0.403 nebo novějším v řadě 10.x a Node.js 24+ spusť:

```powershell
.\build.ps1
```

Nebo otevři `DayzServerMonitor.sln` ve Visual Studiu 2026 s desktopovým .NET. Build provede kontroly a připraví EXE, plugin a zdrojový ZIP do `artifacts` a `ReleasePackages/v1.10.1`. Aplikaci nespouští.

Verze 1.10.1 obsahuje .NET 10.0.14 a pro Stream Deck SDK používá opravený Zod 4.6.7. .NET se samostatně neinstaluje. Existující config.json a data zůstávají kompatibilní.

Texty přehledu jsou vystředěné. Velké hodiny mění velikost nezávisle na popiscích. Adresní panel používá pevnou velikost písma; zkrácený řádek se celý zobrazí po najetí myší. Na malých displejích se nejdřív schovají vedlejší vysvětlivky.

Verze 1.10.1 prošla 58 základními, 21 WPF a 17 pluginovými kontrolami. Fyzické LCD/DPI, celý průchod nastavením a několikadenní provoz ještě potřebují ruční ověření. [Výsledky testů](docs/VALIDATION.md).

Je to hobby projekt vzniklý pomocí vibe codingu s podporou AI. Vlastní kód a ikona T/R mají [licenci MIT](LICENSE). Přibalené závislosti mají vlastní [licenční oznámení](THIRD_PARTY_NOTICES.md). Nejde o oficiální produkt Bohemia Interactive, MAG, Sunnyvale, Valve ani Elgato. [Názvy a značky](docs/TRADEMARKS.md).
