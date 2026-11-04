# NIR-Rebuild

64-bit Windows-basis voor het uitlezen en verwerken van legacy FOSS/NIRSystem
5000-bestanden.

De drie projecten zijn samengebracht in `Nir5000.sln`; `dotnet build Nir5000.sln -c Release`
bouwt core, CLI en GUI samen.

## Huidige release

De self-contained Windows-builds staan lokaal in `.staging`:

- `Nir5000.App-win-x64/Nir5000.App.exe` — Avalonia GUI
- `Nir5000.Cli-win-x64/Nir5000.Cli.exe` — CLI

De GUI kan een `Prod5000.ini`-profiel laden, een gesimuleerde scan uitvoeren en
een legacy `.NIR`-bestand inspecteren. De GUI kan ook een expliciete COM-poort
openen/sluiten via de transportlaag. De parser leest de bewezen legacy
float-array en schrijft een byte-perfecte roundtrip voor de aanwezige 5000- en
4500-testbestanden.

`Nir5000Capture` kan later ruwe seriële captures op checksum-valide frames
doorzoeken wanneer de payloadlengte bekend is.

## CLI

```text
Nir5000.Cli.exe --self-test
Nir5000.Cli.exe --inspect-project Prod5000.ini
Nir5000.Cli.exe --inspect-nir input.nir output.nir
Nir5000.Cli.exe --inspect-capture capture.bin payloadLength
Nir5000.Cli.exe --inspect-calibration calibration.bin
Nir5000.Cli.exe Prod5000.ini spectrum.csv
```

## Seriële hardware

De transportlaag en framecodec zijn geïsoleerd in `Nir5000.Core`. Statisch is
bevestigd: 8N1, ENQ/ACK, SOH/STX, 8-byte header, checksum, NAK en segmenten van
maximaal `0xF0` bytes. Ook zijn de commandocodes `0x5B` (serienummer) en `0x5D`
(spectrum/response) vastgesteld.

De exacte header- en payloadindeling voor een NIR 5000 is nog niet bewezen.
Daarom verstuurt de GUI nog geen scancommando naar echte hardware. Voor die
stap is een XDS-installatie of een gecontroleerde seriële capture nodig.
Zie `docs/nir5000-protocol.md`.
