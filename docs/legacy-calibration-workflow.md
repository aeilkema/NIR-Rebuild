# Legacy NIR 5000 calibration workflow

Static inspection of the supplied legacy installation identifies `DIAGNSTC.EXE`
(`Diagnstc.ini`, `Diagnstc.tx1` and `Diagnstc.tx2`) as the old diagnostics and
instrument-setup application. The resources provide the following workflow:

1. Establish communication and define the instrument setup.
2. Warm the instrument for 15 minutes before automatic diagnostics.
3. Run automatic diagnostics, including reference, noise and bandwidth tests.
4. Edit/check reference wavelengths and scan reference peaks.
5. Run gain adjustment for the NIR amplifier where required.
6. Run wavelength linearization and send the resulting constants to the
   instrument.
7. Position the reference standard and acquire the configured reference scans.
8. Verify tolerance results, then explicitly turn the lamp off when the
   workflow is finished.

The driver boundary in `Nirsinst.dll` corroborates these operations through
exports named `InstrFindLinConst`, `InstrGainAdjust`, `InstrGetLinear`,
`InstrSetAmpCorr`, `InstrSetReference`, `InstrSetWindowCorr`,
`InstrCorrectSpectrum` and `InstrSetLEDPassFail`.

The supplied `Diagnstc.ini` also records the historical diagnostic defaults:
`ModelType=9`, instrument wavelength range 1100–2498 nm in 2-nm steps,
`RefBefore=32`, `RefAfter=32`, `ScanAverage=0`, `Port=0` and `Baud=3` for the
diagnostic program. The production `Prod5000.ini` separately uses its product
configuration, including `BaudRate=5` (resolved by this project as 38400 baud),
so the two files must not be mixed blindly.

The same diagnostic profile contains two configured wavelength ranges:

- visible/reference range: 400.00–1098.00 nm, increment 2.00 nm;
- NIR range: 1100.00–2498.00 nm, increment 2.00 nm.

Its active reference peak list is `805.23`, `878.52`, `1143.63`, `1681.27`,
`2166.40` and `2305.93` nm; reference slots 7 and 8 are zero. The diagnostic
UI also exposes “Edit Reference Wavelengths”, “Wavelength Accuracy”, “Auto
Diagnostics”, “Add diagnostic scans to the file (NIR file)”, gain settings and
repeatability wavelength ranges. These values are now recorded as calibration
metadata, not emitted as instrument writes.

Static strings in the legacy `DIAGNSTC.EXE` add the following named diagnostic
operations to the workflow inventory: `GET_VISGAIN`, `GET_NIRGAIN`,
`SET_DIAGNOSTICS_FLAG`, `GET_K`, `GET_PHI`, `PUT_K`, `PUT_PHI`,
`DOWNLOAD_K_AND_PHI`, `REGISTER_NIRQA_INI`, instrument-response testing,
wavelength-accuracy testing and NIR-repeatability testing. The UI also names
separate callback/test boundaries `FNRESPONSEWNDPROC`, `FNWAVEACCTESTWNDPROC`,
`FNNIRREPEATGRAPHWNDPROC` and `FNINSTRESPONSERESULTSWNDPROC`, plus the
diagnostic file/resource names `WAVEACC`, `NIRPEAT` and `RESPONSE`.
These names confirm additional driver operations. The executable is a 16-bit
NE binary and statically imports `LANG`, `FILEIO`, `Prod_INI`, `PVPLUS`,
`WINCTL`, `COMMDLG`, `GDI`, `KERNEL`, `USER` and `WIN87EM`; the diagnostic
program therefore delegates part of its protocol/configuration work to those
legacy modules. The operation inventory is complete at the name level, but
the byte payload and acknowledgement for each operation still require static
caller decoding or a captured/vendor-verified exchange.

## Static structure of `DIAGNSTC.EXE`

The repository copy is a 16-bit Windows NE executable rather than a modern PE
file. Its module-reference table confirms the ten imports listed above. This
is important for completeness: the calibration workflow is not implemented
only in the diagnostic screen, and a simple string search cannot be treated as
a complete protocol specification. The modern project records every located
operation and keeps unknown operations disabled until their caller and wire
format are proven.

## What is and is not a command specification

The operation names and order above are supported by static evidence. The
exact serial payloads, header fields and instrument acknowledgements for the
calibration operations are not yet proven. The current application therefore
does not transmit guessed calibration bytes. A passive or vendor-controlled
serial capture is required before enabling these operations against hardware.

## Lamp-risk finding

The legacy resources explicitly contain `Lamp is off`, `Turn Lamp Off ?` and
`Turn off the lamp`. They also show a warm-up requirement and separate
reference/gain workflows. Static inspection did not reveal a duty-cycle or
overdrive setting. NIR-Rebuild now exposes the observed lamp on/off request in
the GUI and logs the exact bytes sent; the request is only available after the
COM connection is explicitly opened.

Additional static disassembly found the exported lamp API InstrSetLamp and
InstrWasLampOn in Nirsinst.dll. InstrSetLamp(handle, enabled) builds an
internal two-byte request: 02 FF for enabled and 02 00 for disabled. This is
the command payload observed inside the old driver. The new core sends exactly
this two-byte request after the user explicitly opens the COM connection and
logs it as `TX 02 FF` or `TX 02 00`. The old driver path does not read a reply;
`InstrWasLampOn` reports the driver's cached state. Therefore the application
does not claim an instrument acknowledgement for this command, and the wire
trace is the authoritative diagnostic record.
