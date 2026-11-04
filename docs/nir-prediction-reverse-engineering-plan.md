# NIR 5000 prediction and application plan

## Scope

The target is a 64-bit Windows application for the NIRSystems/FOSS NIR 5000,
with an offline simulator for development and regression testing. The first
missing capability is prediction from an EQA model. The `Lijnen/` calibration
directory is deliberately out of scope for the first implementation phase.

The SafeNet/Sentinel dongle mechanism, vendor licensing checks, vendor
installers, Access runtime, SQL Server Express and Nova services are not part
of the new product. We use the observed file formats and behaviour as
compatibility evidence only.

## Evidence established on the isolated REA VM

- `ISIscan2.85.exe` is a 32-bit Wise installer. Its extracted files include
  `Scan32new.mdb`, `demo.eqa`, `demo.nir`, `demo.pca`, `demo.lib` and native
  prediction libraries.
- The ISIscan Access schema contains `PredictionModel`, `PredictionModelParameter`,
  `ProductPM`, `Parameter`, `SampleHeader`, `SampleNirData`, `SampleData` and
  `RINAPrediction`. `PredictionModel.EqaFile` is the explicit link from a
  prediction model to its EQA file.
- The Access instrument table identifies `NIRSystems 5000` with 700 data
  points and a 1100–2498 nm range. This matches the legacy NIR parser and is a
  useful model-selection guard.
- ISIscan `predictionrelease.dll` exports equation parsing and evaluation
  operations: `parse_equation`, `validate_equation`, `make_tree`,
  `evaluate_tree`, `predict` and `standardize`.
- ISIscan also has separate native ANN and local-calibration engines. Their
  exported protection functions and SafeNet strings are intentionally not
  reproduced.
- The Nova DS2500 installer contains `Foss.Mosaic.Foundation.Common.IsiPredictionEngine.dll`.
  Its managed `PlsPredictor` loads EQA/PCA/LIB/STD model blocks, standardizes
  segmented spectra and calls native `PredictNova`. This confirms the model
  pipeline, but Nova is not a suitable runtime dependency for the NIR 5000.
- Nova metadata also contains an explicit `NIRS 6500/5000 compatibility mode`
  setting for an FT2500SR instrument. This is useful evidence for spectrum
  compatibility, but does not prove that Nova can drive a physical NIR 5000 or
  accept an arbitrary network-device emulator.
- The extracted ISIscan installer contains the legacy native driver
  `Nirsinst.dll`. Its exports expose the device boundary: `InstrInitDLL`,
  `InstrInitialize`, `InstrCloseComm`, `InstrReOpenComm`, `InstrGetReference`,
  `InstrGetSpectrum`, `InstrTerminate` and related status/configuration calls.
  Static strings confirm selectable COM port and baud rate, 6500-series/NIR
  1100--2500 nm operation, spectrum acquisition, transport timeout and
  command-range error handling. This is strong evidence for the next adapter
  boundary, but the DLL is 32-bit and is not loaded or executed by NIR-Rebuild.
  A disassembly check also shows `InstrInitialize` validating four non-null
  pointer arguments before dispatching to the driver object, while
  `InstrGetSpectrum` dispatches a buffer/configuration-shaped call with five
  additional arguments. The argument names and wire fields remain unassigned
  until headers or captures provide corroboration.
- `Nirsinst.dll` imports the companion `Sic.dll`. Static export/import analysis
  shows `Sic.dll` exposes `SicCreate`, `SicInitialize`, `SicRead`, `SicWrite`,
  `SicTerminate` and error helpers, and directly uses Win32 `CreateFileA`,
  `ReadFile`, `WriteFile`, `SetCommState`, `SetCommTimeouts`, `PurgeComm`,
  `SetupComm` and `SetCommMask`. This confirms that the legacy instrument
  boundary is a conventional COM transport. NIR-Rebuild's `SerialPort` layer
  therefore targets the right physical boundary; only the instrument command
  and spectrum payload semantics remain to be reconstructed.
- Disassembly of the `Sic.dll` wrappers adds useful parameter constraints:
  `SicRead` passes a caller buffer and 16-bit length to an internal read with
  a literal 1000 ms timeout; `SicWrite` passes a handle plus buffer/length and
  two additional word-sized options. These are transport-level facts, not
  guesses about the NIR payload, and support keeping timeout and framing
  configurable in the new layer.

## Target architecture

1. **Instrument layer**
   - Keep the existing serial transport, frame codec and simulator isolated.
   - Expose the driver-shaped `INir5000Instrument` contract for initialize,
     reference acquisition, sample acquisition and termination. The simulator
     implements this contract now; the real serial implementation will only
     enable acquisition after the wire payload has been captured and verified.
   - Provide a transparent raw serial/TCP bridge for supported host
     integrations and diagnostics. It is not a Nova device impersonator and
     does not bypass licensing.
   - Add a capture/replay source so all prediction work can run without the
     physical instrument.
   - Accept only explicitly identified NIR 5000 spectra: 700 points,
     1100–2498 nm, with segment metadata retained rather than discarded.

2. **Spectrum normalization**
   - Represent one or more wavelength segments, point count, start and step.
   - Implement the standardization stage as a separate service with testable
     error reporting. Do not silently extrapolate or reorder points.
   - Keep raw spectrum, standardized spectrum and model input available for
     diagnostics.

3. **Model package and import**
   - Define a neutral model package containing EQA, PCA, LIB, STD and optional
     ANN/LOCAL blocks plus metadata and checksums.
   - Import metadata from a copied Access database or CSV export into a local,
     dongle-free store (SQLite or JSON). Do not require Access or SQL Server
     at runtime.
   - Map Product → PredictionModel → Parameters and preserve model versions,
     constituent names, display order, moisture-basis corrections and limits.

4. **EQA engine**
   - Implement a bounded parser and AST evaluator for the equation syntax
     observed in the legacy engine. Supported operators/functions must be
     discovered from model corpus and static analysis before adding them.
   - Add validation for unknown variables, malformed expressions, stack depth,
     numeric overflow, missing spectrum/model inputs and unsupported functions.
   - Produce raw prediction, corrected/display prediction, equation mean,
     standard error/SEC, R² where available, and diagnostics.
   - Do not implement `AnnUnprotect`, `AnnPredict` dongle validation or any
     SafeNet/Sentinel bypass.

5. **Prediction orchestration**
   - Select a model by instrument, product, constituent and model version.
   - Run standardization, EQA/PLS evaluation, optional PCA/nearest-neighbour
     diagnostics, then apply the documented slope/bias and moisture-basis
     correction fields.
   - Persist a result equivalent to the legacy `SampleData` fields, while
     clearly marking simulated or replayed measurements.

6. **Application and CLI**
   - Add model import/inspection, product selection, prediction preview and
     result export to the existing 64-bit GUI and CLI.
   - Add a simulator scenario that loads `demo.nir` and a model package,
     allowing deterministic regression tests without hardware.

7. **Optional Nova adapter**
   - Treat Nova as an optional integration target, not as the NIR 5000 driver.
   - Export prediction results and neutral model metadata through a documented
     interchange format first. Only add a Nova-side adapter after its supported
     extension/import contract is verified.
   - A network-facing compatibility service may be investigated only as a
     documented instrument integration, with licensing and authentication left
     intact; it must not impersonate a protected device or bypass a dongle.
   - Never copy Nova licensing, SafeNet calls, SQL schema assumptions or
     protected native binaries into the NIR 5000 application.

## Implementation phases

### Phase 1 — forensic corpus and format readers

Complete static inventories of the extracted ISIscan and Nova files. Capture
Access table headers/relationships and collect all available EQA/PCA/LIB/STD
examples. Use Ghidra/ILSpy only for static analysis. No installer or target
binary is executed.

### Phase 2 — model import and EQA reader

Implement the neutral model package, Access/CSV importer and EQA binary reader.
The first read-only legacy inspector is now present and prints header,
  instrument, source/model point count, wavelength metadata, serial and
constituent names for the proven NIRSystem 5000 fixed-record variant. The
type-9 reader now extracts its selection mask, bias/quality metadata and
coefficient vector. The reconstructed difference/smoothing preprocessing is
implemented separately and is consistent across 158 fixed-record corpus
files. Add golden fixtures
from the demo models without committing proprietary installer/database files.

### Phase 3 — evaluator and simulator integration

Implement the AST evaluator and connect it to the existing simulator. Add
golden prediction tests, malformed-model tests, NaN/overflow tests and
round-trip diagnostics. A prediction is not considered production-ready until
the same input/model gives stable results across CLI, GUI and replay mode.

### Phase 4 — NIR 5000 acquisition validation

Use a real-device capture later to verify command sequence, scan payload,
checksum and wavelength mapping. Compare the captured spectrum with the
simulator and model point-count/range checks. This phase is the only part that
requires the physical NIR 5000; all earlier phases can proceed offline.

### Phase 5 — production packaging

Publish a self-contained win-x64 application, import/export tools and a
diagnostic bundle. Keep the vendor installers, MDB files, model binaries and
REA evidence outside GitHub unless the user explicitly supplies redistribution
rights. Add a reproducible verification document and CI tests.

## Acceptance criteria

- A copied, authorized EQA model can be inspected and imported without Nova,
  SQL Express, Access, a SafeNet dongle or a vendor DLL.
- `demo.nir` (or an equivalent authorized NIR 5000 spectrum) can be replayed
  through the simulator and produces named constituent predictions with
  diagnostics and explicit provenance. The CLI and GUI now support this for
  the proven type-9 NIR5000 EQA variant.
- Invalid models and spectra fail safely with actionable messages.
- The same workflow works from the 64-bit GUI and CLI.
- Real serial I/O remains optional and is enabled only when a NIR 5000 capture
  has validated the protocol.

## Legacy calibration workflow evidence

`Nirsinst.dll` exports the calibration-related boundary functions
`InstrFindLinConst`, `InstrGainAdjust`, `InstrGetLinear`, `InstrSetAmpCorr`,
`InstrSetReference`, `InstrSetWindowCorr`, `InstrCorrectSpectrum` and
`InstrGetRefCorrSpectrum`, alongside `InstrGetTestData` and
`InstrSetLEDPassFail`. Its static strings name the corresponding operator
workflow: wavelength linearization, sending linearization constants to the
instrument, gain adjustment, reference acquisition and performance-test
reference/noise/bandwidth spectra. This identifies the old calibration
application boundary, but not enough byte-level detail to safely implement
those commands without a capture.

The extracted ISIscan installation contains 91 native/ActiveX `.dll`/`.exe`
artifacts; the Nova DS2500 installation contains 514 managed/native artifacts.
The inventory is therefore treated in layers: the NIR 5000 driver, serial
transport, prediction engine, database and Nova OpenInstrument assemblies are
analyzed first. “All binaries reversed” is not a finite or useful completion
criterion; every implementation claim is tied to a specific static finding.

