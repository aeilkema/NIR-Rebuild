# Reproduceerbare verificatie

De bron wordt gebouwd met een .NET SDK die `net8.0` ondersteunt. Op Windows
gebruikt `Install-NirRebuild.ps1` de meegeleverde `NuGet.Config`, herstelt ook de
`win-x64` runtime-assets en publiceert daarna zonder een tweede restore:

```text
dotnet restore Nir5000.sln --configfile NuGet.Config --runtime win-x64 --force
dotnet build Nir5000.sln -c Release --no-restore
```

Op de REA-VM kan de bron ook met .NET 8 worden gebouwd:

```text
dotnet build Nir5000.sln -c Release
dotnet publish src/Nir5000.Cli/Nir5000.Cli.csproj -c Release -r linux-x64 --self-contained true -p:PublishSingleFile=true -o /tmp/nir5000-selftest
/tmp/nir5000-selftest/Nir5000.Cli --self-test
```

De zelftest controleert frame roundtrip, ENQ/ACK, ontvangst-ACK, checksum-NAK,
transportsegmentatie, capture parsing, baudrate mapping en de kalibratiebuffer.

Project- en legacybestandcontrole:

```text
/tmp/nir5000-selftest/Nir5000.Cli --inspect-project /srv/rea/projects/NIR-Rebuild/Prod5000.ini
/tmp/nir5000-selftest/Nir5000.Cli --inspect-nir /srv/rea/projects/NIR-Rebuild/5000.nir /tmp/5000-roundtrip.nir
/tmp/nir5000-selftest/Nir5000.Cli --inspect-nir /srv/rea/projects/NIR-Rebuild/4500.nir 1300 2398 2 /tmp/4500-roundtrip.nir
```

De twee `.NIR`-roundtrips zijn byte-perfect tegen de originele bestanden.

## Huidige Windows-release

De self-contained publicaties zijn x64 PE32+ Windows-binaries:

```text
Nir5000.App.exe  6069D6818BDCEE26E8E02E15261A2B4218544A2AD5AD5165335EDB914678ADF7
Nir5000.Cli.exe  2717D751268A55531E7132BB2F6946E5460E06F2899AB52DA95C79051FF5BC3C
```

De hashes horen bij de laatst gepubliceerde GUI- en CLI-build. Hardware-uitlezing
is nog niet als voltooid aangemerkt: daarvoor ontbreken de echte NIR 5000
handshake, instrumentheader en responsekalibratiegegevens.
