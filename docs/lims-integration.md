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
