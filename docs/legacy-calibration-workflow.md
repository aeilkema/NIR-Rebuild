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

Static strings in the legacy `DIAGNSTC.EXE` add the following named diagnostic
operations to the workflow inventory: `GET_VISGAIN`, `GET_NIRGAIN`,
`SET_DIAGNOSTICS_FLAG`, instrument-response testing, wavelength-accuracy
testing, and uploading the maximum-statistics K/Phi values to the instrument.
These names confirm additional driver operations, but the old executable is a
16-bit NE binary and its import/caller table still requires separate static
decoding before any of them can be emitted as new serial commands.

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
the command payload observed inside the old driver, not yet the complete
ENQ/frame sequence. The new core exposes this observed request as a diagnostic
constant and deliberately does not transmit it until the outer frame and
acknowledgement are confirmed.
