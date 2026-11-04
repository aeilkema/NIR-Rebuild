# LIMS/DCU integration

The private `aeilkema/sl-source` repository contains the existing StarLIMS
DCU implementation in `Server Scripts/DCU_Methods/FossNirCommaSep2.srvscr`.
The relevant input is a semicolon-delimited text file:

```text
HDR;order number;date;model;calibration-code[-version]
PS;analyte;result;GlobalH;NH;t-statistic;SD;min;max;actual-min;warning-min;actual-max;warning-max
SE;analyte;S0;S1;S2;S3;S4;S5;S6
```

The DCU method reads the file, maps `PS` rows to NIR result analytes and uses
`SE` severity values to decide whether a result is accepted or annotated. It
moves successfully processed files to `\\VERWERKT\\` and records errors in
`NL_INTERFACE_ERROR` before moving failures to the error folder.

The source also confirms operational rules that are separate from the file
format:

- The DCU entry point receives both `sFileName` and `equipmentId`.
- Missing files, zero-size files and unreadable input are rejected before
  result processing; errors are recorded as DCU interface errors.
- A released order is not recalculated; its input is moved to `VERWERKT`.
- QC order numbers can be converted to a QC sample and later added to the QC
  chart.
- The implementation checks repeated `Eiwit` submissions and may reset a
  previous result when the new `GlobalH` is lower and no batch report has been
  issued. Otherwise the duplicate is moved to `VERWERKT`.
- Severity `S0` is the overall status. `S1` through `S6` correspond to GlobalH,
  NeighbourhoodH, t-statistic, SD, Min and Max. Severity 1 is a warning and
  severity 2 is a failure; the DCU builds the explanatory comment before it
  writes the result.
- A successful import updates the order calibration code and triggers the
  StarLIMS recalculation/status procedures. Those database side effects are
  not part of NIR-Rebuild's file exporter.

NIR-Rebuild implements the file producer in
`FossNirCommaSep2Document` and exposes:

```text
Nir5000.Cli.exe --export-foss-nir-csv2 model.eqa spectrum.nir ordno cmr05-1 output.txt
```

The command replays the legacy EQA model, emits one `PS` and one `SE` row per
constituent, and preserves explicit `replay` provenance. It does not write to
the LIMS database or move files; the existing DCU remains the system of record
for that operational behavior.

The repository also contains an older comma-delimited `FossNirCommaSep.srvscr`
variant, which expects nine columns and updates `RESULTS` directly. It is kept
as a separate legacy format and is not silently conflated with CommaSep2.

For a future HTTP/API integration, the source repository also identifies the
NIR import route as a separate DCU concern (`__IMPORT_NIR.srvscr`). That route
expects a different five-column semicolon format and has a placeholder
`Save_Data` procedure. It must not be used for CommaSep2 result files without a
StarLIMS-side contract and a test endpoint.

The implemented signed uploader and its deployment contract are documented in
`docs/starlims-api-uploader.md`.
