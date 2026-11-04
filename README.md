# NIR-Rebuild

64-bit Windows-basis voor het uitlezen en verwerken van legacy FOSS/NIRSystem
5000-bestanden.

De drie projecten zijn samengebracht in `Nir5000.sln`; `dotnet build Nir5000.sln -c Release`
bouwt core, CLI en GUI samen.

## Installatie op een Windows-laptop

Maak een lege doelmap, zet `Install-NirRebuild.ps1` daarin en voer PowerShell
uit vanuit die map:

```powershell
Set-ExecutionPolicy -Scope Process Bypass
.\Install-NirRebuild.ps1 -Launch
```

Voor automatisch starten bij Windows-aanmelding, met de in de GUI opgeslagen
seriële auto-connectinstelling:

```powershell
.\Install-NirRebuild.ps1 -InstallStartup -Launch
```

Het script haalt branch `codex/nir5000-modern` uit de private repository,
bouwt de solution, publiceert self-contained `win-x64`-binaries en voert de
offline zelftest uit. Voor lokale wijzigingen terug naar GitHub:

```powershell
.\Install-NirRebuild.ps1 -SkipBuild -PushChanges -CommitMessage "Mijn wijziging"
```

Vereist op de laptop: Git en een .NET SDK die `net8.0` ondersteunt (de .NET 8
SDK of nieuwer, zoals .NET 11). Het script herstelt pakketten via de meegeleverde
`NuGet.Config` vanaf `https://api.nuget.org/v3/index.json`; die URL moet vanaf de
laptop bereikbaar zijn. De hoofdbranch wordt niet gewijzigd;
de moderne bron staat op `codex/nir5000-modern`.

Als je de eerste run al in een bestaande checkout deed en Git meldt dat
`Install-NirRebuild.ps1` ongetrackt wordt overschreven, herstel je eenmalig met:

```powershell
Rename-Item .\Install-NirRebuild.ps1 .\Install-NirRebuild.bootstrap.ps1
git fetch origin codex/nir5000-modern
git checkout -B codex/nir5000-modern origin/codex/nir5000-modern
.\Install-NirRebuild.ps1 -Launch
```

## Huidige release

De self-contained Windows-builds staan lokaal in `.staging`:

- `Nir5000.App-win-x64/Nir5000.App.exe` — Avalonia GUI
- `Nir5000.Cli-win-x64/Nir5000.Cli.exe` — CLI

De GUI kan een `Prod5000.ini`-profiel laden, een gesimuleerde scan uitvoeren en
een legacy `.NIR`-bestand inspecteren. De GUI kan ook een expliciete COM-poort
openen/sluiten via de transportlaag en de transparante COM-naar-TCP Ethernet-
bridge starten, stoppen en automatisch bij Windows-start activeren. De bridge
wijzigt geen bytes; Nova of een geautoriseerde host verzorgt de protocoluitwisseling.
De parser leest de bewezen legacy
float-array en schrijft een byte-perfecte roundtrip voor de aanwezige 5000- en
4500-testbestanden.

`Nir5000Capture` kan ruwe seriële captures op checksum-valide frames
doorzoeken wanneer de payloadlengte bekend is. Er is momenteel geen echte
NIR 5000-communicatiecapture aanwezig; de aanwezige `.bin`-bestanden uit Nova
zijn spectra en fabrieksdata.

## CLI

```text
Nir5000.Cli.exe --self-test
Nir5000.Cli.exe --inspect-project Prod5000.ini
Nir5000.Cli.exe --inspect-command-inventory
Nir5000.Cli.exe --inspect-nir input.nir output.nir
Nir5000.Cli.exe --inspect-capture capture.bin payloadLength
Nir5000.Cli.exe --inspect-capture-pair tx.bin rx.bin payloadLength
Nir5000.Cli.exe --inspect-calibration calibration.bin
Nir5000.Cli.exe --inspect-eqa model.json spectrum.csv
Nir5000.Cli.exe --inspect-legacy-eqa model.eqa
Nir5000.Cli.exe --inspect-legacy-eqa-model model.eqa
Nir5000.Cli.exe --predict-legacy-eqa model.eqa spectrum.nir
Nir5000.Cli.exe --export-foss-nir-csv2 model.eqa spectrum.nir ordno cmr05-1 output.txt
Nir5000.Cli.exe --upload-starlims output.txt https://lims/rest.web.api/upload ACCESS_KEY SECRET_KEY upload
Nir5000.Cli.exe --export-nova-sample spectrum.csv|spectrum.nir nova-sample.json
Nir5000.Cli.exe --serial-tcp-bridge COM3 38400 9000 [listenAddress] [txCapture.bin rxCapture.bin]
Nir5000.Cli.exe Prod5000.ini spectrum.csv
```

## EQA prediction

The core contains a dongle-free, bounded EQA expression evaluator. A model
JSON maps parameter names to equations using `X1`, `X2`, ... for spectrum
points and supports arithmetic, powers and common mathematical functions. The
CLI and GUI can run the model against a replayed or simulated spectrum. This
is the compatibility layer for JSON models. The CLI can also inspect the
proven NIRSystem 5000 type-9 EQA variant, extracting constituent metadata, the
selection mask, coefficient vector and preprocessing header. Fixed records
with the observed `1,4,4,1` header can be replayed offline through the
reconstructed derivative/difference/smoothing stage. Other valid legacy EQA
variants remain inspect-only until a matching native reference result exists.

Nova integration remains optional. The application does not depend on Nova,
SQL Express, Access or SafeNet; the optional OpenInstrument adapter can export
or submit spectrum payloads without making the NIR 5000 depend on Nova.

`--export-nova-sample` emits the neutral `Sample`/`AbsScan`/`Segment` JSON
shape observed in Nova's official OpenInstrument client. It accepts CSV or a
legacy 700-point `.NIR` file; the latter was verified with the `cmr05.NIR`
replay. It is an explicit interchange artifact; it does not impersonate a
device or bypass licensing.

For an authorized Nova deployment, `NovaOpenInstrumentAdapter` can submit that
same payload to `/instrumentTypes/{instrumentTypeID}/instruments/{chassisID}/samples`.
The endpoint and IDs are explicit constructor options; nothing connects to
Nova automatically.

The serial session defaults to the 1000 ms legacy `Sic.dll` timeout; callers
can override it for diagnostics. `Nir5000SerialOptions` can also receive paired
transmit/receive capture paths; `Nir5000WireCapture` then records the exact raw
bytes in both directions for later `--inspect-capture` analysis. The
serial/TCP bridge is transparent raw transport only. It is useful for a
supported host integration or diagnostics, but it does not emulate a vendor
instrument identity and does not bypass Nova licensing or dongle checks. It
listens on loopback by default; a LAN address must be supplied explicitly.

In de GUI staan hiervoor `Start Ethernet bridge at startup`, `Listen` en `TCP
port`. Directe COM-auto-connect en bridge-auto-start zijn mutually exclusive;
bij beide opties krijgt de bridge voorrang. TX/RX-captures en de timestamped
wire trace gelden ook voor bridgeverkeer.

## Seriële hardware

De transportlaag en framecodec zijn geïsoleerd in `Nir5000.Core`. Statisch is
bevestigd: 8N1, ENQ/ACK, SOH/STX, 8-byte header, checksum, NAK en segmenten van
maximaal `0xF0` bytes. Ook zijn de commandocodes `0x5B` (serienummer) en `0x5D`
(spectrum/response) vastgesteld.

De exacte header- en payloadindeling voor een NIR 5000 is nog niet bewezen.
Daarom verstuurt de GUI nog geen scancommando naar echte hardware. Voor die
stap is een XDS-installatie of een gecontroleerde seriële capture nodig.
Zie `docs/nir5000-protocol.md`.

De historische kalibratie-/diagnoseworkflow en de lampbevindingen staan in
`docs/legacy-calibration-workflow.md`. De oude applicatie heet `DIAGNSTC.EXE`;
de exacte seriële kalibratiepayloads worden pas ingeschakeld nadat die met een
gecontroleerde capture zijn bevestigd.

De complete native corpusinventaris (40 EXE/DLL/LIB/SYS-bestanden) met de
status per bestand staat in `docs/legacy-binary-inventory.md`. Dat document
maakt expliciet onderscheid tussen statische inventarisatie en
protocolgedetailleerde reverse engineering.
