# Legacy model corpus notes

The private repository contains a large raw-data corpus under `Lijnen/`. It is
used only as an analysis source and is not copied into the application or
published artifacts.

The current checkout contains approximately 300 `.eqa`, 298 `.pca`, 297
`.lib`, 176 `.nir` and 14 `.cal` files. The files are genuine legacy files with
valid, recognizable types and instrument metadata, but the corpus is
heterogeneous and individual calibration lines may be weak, obsolete, or
unsuitable for production. Files are therefore format/regression fixtures
until their instrument, product and laboratory provenance is known.

Useful observations from the corpus:

- `Lijnen/TEMP/cmr05.eqa` is a genuine legacy model header for `NIRSystem 5000`:
  700 source points, 1100 nm start, 2 nm increment, serial `49779610`.
- `Lijnen/TEMP/cmr06.eqa` and `Lijnen/TEMP/EINDPRO1.EQA` provide additional
  multi-constituent examples, including visible names such as `Vet`, `Eiwit`
  and `Vocht`.
- The first visible constituent/model text begins at byte `0x180`; the binary
  record contents after that are not treated as text because coefficient bytes
  produce misleading ASCII runs.
- The `cmr05`/`cmr06` family has a proven `0x180` header plus `0x5F8`-byte
  fixed constituent records. Other valid NIR5000 EQA files, such as
  `EINDPRO1.EQA`, use a different layout and are reported as variants until
  their records are decoded.
- Across 158 fixed-record NIR5000 EQA files in the corpus, the eight-byte
  preprocessing header is consistently `1,4,4,1`. Static reconstruction of
  `predictionrelease.dll` matches this to first-difference order, gap 4, and
  two moving-average windows (4 and 1). The implementation keeps this stage
  explicit and testable rather than hiding it in the coefficient reader.
- The header inspector derives the overall endpoint as
  `start + (sourcePointCount - 1) * increment`. A field near `0xD0` is a
  segment boundary in some files, not the overall endpoint.

The corpus is suitable for format discovery and parser regression tests, but
not yet for validating analytical accuracy. Accuracy requires an authorized
model/spectrum/lab-value pairing and a known-good reference result.

## ISIscan Access cross-check

Static export of ISIscan's `Scan32new.mdb` independently identifies the
hardware model record `NIRSystems 5000` with model ID `9`, start wavelength
`1100`, stop wavelength `2498` and `700` data points. The database schema also
contains explicit `ComPort` and `BaudRate` fields in `InstrumentMainProfile`,
and `ScanWLStart`, `ScanWLInc` and `ScanSize` fields in the scan/profile data.
This corroborates the legacy project and `.NIR` assumptions used by
NIR-Rebuild; it does not provide populated serial captures in this database.
