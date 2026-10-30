# Dayz Server Monitor

Malá aplikace pro Windows, kterou si můžeš nechat otevřenou na druhém monitoru při hraní DayZ. Ukazuje počet hráčů, poslední úspěšnou odpověď a odpočet do plánovaného restartu.

## Spuštění

V Releases stáhni `Dayz-Server-Monitor-1.8.0-win-x64.zip`, rozbal ho do zapisovatelné složky a spusť `DayzServerMonitor.exe`. Funguje na Windows 10/11 x64. .NET je součástí balíčku, správce ani API klíč nepotřebuješ.

Vedle EXE se ukládá `config.json`, historie je ve složce `data`. Při aktualizaci starou aplikaci zavři a nový ZIP rozbal do stejné složky; konfiguraci a historii ponech. V **Settings → Display** můžeš přepnout angličtinu na češtinu a vybrat z deseti motivů, včetně modrého Ocean, Luna ve stylu XP, Glass ve stylu Aero, Sakura a zelenočerného Terminal.

Horní lišta má běžných 32 bodů. Ovládání je v záhlaví a nabídce **⋯**, spodní pás tlačítek odpadl. Při zkrácení a roztažení okna se panely s údaji a graf přesunou doprava; na výšku se skládají pod sebe. Nabídka obsahuje 27 velikostních profilů. **F11** vyplní monitor, **Esc** vrátí okno, pravé tlačítko otevře ovládání i bez záhlaví. [Rozbor LCD, rozlišení, rozměry a profily](docs/DISPLAYS.md).

V nastavení můžeš zapnout, aby křížek aplikaci místo ukončení minimalizoval na hlavní panel nebo skryl k hodinám. Nastavení i rozměry okna se uloží. Ze skrytí ji obnovíš dvojklikem na ikonu; přes **Quit monitor / Ukončit monitor** ji úplně vypneš. Monitoring a Stream Deck při minimalizaci pokračují.

Hotové balíčky jsou také v místní složce `release`. Windows ZIP obsahuje program i instalátor pluginu pro Stream Deck. Žádná instalace ani aplikace se při sestavení sama nespouští.

## K čemu slouží

V horním seznamu vybereš server; další můžeš přidat v nastavení. Připravené jsou dostupné MAG DayZ servery a všech 12 Sunnyvale. Seznam obsahuje i MAG testovací server. Ověření adres proběhlo 29. října 2026 a není zárukou jejich budoucí dostupnosti. Podrobnosti jsou v [seznamu serverů](docs/SERVERS.md).

Hodiny a plán restartů používají Europe/Prague včetně letního času. Můžeš si přidat světové hodiny, města a zkratky zemí. Předstih upozornění na odhlášení i očekávaný kick nastavíš po minutách. Zvuk a Windows oznámení jsou volitelné.

Dotazy chodí přímo na server přes UDP. Měří se odezva A2S query, nikoli ICMP ping. Když odpověď nepřijde, ukáže se N/A a v grafu mezera. Jeden timeout neznamená OFFLINE; opakované chyby znamenají nedostupné query a skutečné vypnutí hry z nich nelze prokázat. OFFLINE se potvrzuje ručně v nastavení.

Tlačítko **Obnovit** po zahájení dotazu odpočítává 20 sekund a nedovolí další ruční požadavek. Automatické dotazy běží dál podle plánu. Graf má datum a čas, po najetí myší uvidíš podrobnosti konkrétního měření.

Výchozí časy 03/07/11/15/19/23 jsou příklad, který musíš ověřit pro svůj server. Nové předvolby mají plán označený jako nepotvrzený. Volitelný NTP zdroj upravuje jen hodiny této aplikace. Posun serverového času v hlavním okně je výpočet. Plugin pro Stream Deck umí navíc zobrazit herní čas zveřejněný v A2S, pokud jej server poskytne; dopočet a den/noc jsou označené odhady. Herní chat aplikace nečte.

## Vlastní sestavení

Na Windows nainstaluj .NET 8 SDK a Node.js 24+ a spusť `./build.ps1`, případně otevři `DayzServerMonitor.sln` ve Visual Studiu 2022 s podporou desktopového .NET. Ve složce `artifacts` vznikne portable EXE, distribuční ZIP a zdrojový ZIP. Build aplikaci neotevírá. Při hraní DayZ ji pro ověření sami nespouštíme ani nepřepínáme do popředí.

## Jak aplikace vznikla

Je to hobby projekt vytvořený pomocí **vibe codingu s podporou AI**. Nápad a požadavky vycházejí z hraní DayZ; AI pomáhala s psaním a postupným laděním kódu. Funkce přibývaly po malých krocích. Co se ověřovalo a co ještě potřebuje vyzkoušet v praxi, je popsané v [ověření](docs/VALIDATION.md). Hlášení chyb a nápady z běžného používání jsou vítané.

## Stream Deck a licence

Součástí vydání je plugin pro **Stream Deck MK.2**. Dvojklikem nainstaluj `Dayz-Server-Monitor-1.8.0.streamDeckPlugin` a přetáhni akci Cycle server information na klávesu. Krátký stisk střídá údaje, podržení jednu sekundu obnoví dotaz se společnou 20sekundovou ochranou. Akce Pinned server information udrží jeden vybraný pohled na klávese; můžeš si tak připravit více kláves vedle sebe. Výchozí popisky a náhledy jsou anglicky. Monitor musí běžet, může být minimalizovaný; plugin jej sám neotevírá. [Podrobný návod a omezení](docs/STREAM-DECK.md).

Vlastní zdrojový kód i ikona T/R jsou pod [licencí MIT](LICENSE). Licence přibaleného .NET zůstávají zachované v [oznámeních třetích stran](THIRD_PARTY_NOTICES.md). Jde o nezávislý komunitní nástroj.

Názvy DayZ, MAG, Sunnyvale a Stream Deck slouží k identifikaci hry, serverů a kompatibilního zařízení. Nejsme jejich oficiální produkt a MIT se nevztahuje na jejich značky. Používáme vlastní ikonu T/R. [Podrobnosti ke značkám](docs/TRADEMARKS.md).

Karta hráčů i klávesa Stream Decku zobrazují frontu samostatně: **121/121 +21 ve frontě** znamená 121 hráčů ve hře a 21 čekajících. Nula se ukáže jen tehdy, když ji server skutečně zveřejní; chybějící údaj nebo chyba dotazu znamená N/A.
