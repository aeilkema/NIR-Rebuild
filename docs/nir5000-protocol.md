# NIR 5000 protocol reconstruction

This document records only facts supported by static analysis of the supplied
`6500.dll`. It deliberately does not fill unknown header fields with guesses.

## Confirmed transport

- Serial port is opened as an 8-bit, no-parity, one-stop-bit connection by the
  legacy `OPENCOMM`/`BUILDCOMMDCB` path.
- A sender starts a transaction with `ENQ` (`0x04`).
- The receiver responds with `ACK` (`0x06`).
- A data transfer contains `SOH` (`0x01`), an 8-byte header, `STX` (`0x02`),
  a payload, and one checksum byte.
- The checksum is the low 8 bits of the sum of the 8 header bytes and all
  payload bytes.
- A valid received frame is acknowledged with `ACK`; a checksum failure uses
  `NAK` (`0x15`).
- The legacy implementation retries and uses a 20-second transaction timeout
  for the higher-level transfer paths.
- Segmented transfers use a maximum segment length of `0xF0` bytes.
- The reconstructed sender uses sequential byte offsets and caps each segment
  at `0xF0`; this is implemented and self-tested in `Nir5000Transfer`.

## Confirmed command IDs

- `0x5B`: serial-number transfer (`GET_SERNO`/`PUT_SERNO`).
- `0x5D`: spectrum/response transfer (`GET_NIR` and `GET_RESPONSE`).

## Lamp control

`Nirsinst.dll` exports `InstrSetLamp` and `InstrWasLampOn`. Static
disassembly of `InstrSetLamp` shows a call through the imported `SicWrite`
function with a two-byte request buffer: `02 FF` when enabled and `02 00`
when disabled. This is a direct serial write in the legacy driver, not one of
the typed 0x5A–0x5E exchange wrappers. The 64-bit implementation exposes the
same request through `Nir5000SerialSession.SetLamp` and logs the bytes. The
legacy status function returns cached driver state, so the GUI reports the
request as sent rather than claiming a device acknowledgement.

Additional static evidence from the typed wrappers in `Nirsinst.dll` narrows
the command dispatcher without over-naming the operations: wrappers call the
shared exchange routine with `0x5A` and `0x5B` plus a 3-byte typed exchange,
with `0x5C` and `0x5D` plus a 2-byte typed exchange, and with `0x5E` plus an
8-byte typed exchange. Both getter and setter wrappers are present. This
proves command IDs and typed exchange sizes at the driver boundary; it does
not yet prove that the 2-byte `0x5D` wrapper is the full spectrum payload.

## Still unknown

The ISIscan installer provides useful static evidence about the surrounding
driver contract. `Nirsinst.dll` exports `InstrInitialize`, `InstrCloseComm`,
`InstrReOpenComm`, `InstrGetReference`, `InstrGetSpectrum` and
`InstrTerminate`, and contains strings for COM/baud configuration, spectrum
acquisition, transport timeout and command-range errors. These names identify
the correct integration boundary, but they do not reveal the NIR 5000 wire
payload or make the 32-bit vendor DLL usable from the 64-bit application.

The same binary imports `Sic.dll`; that companion exports `SicRead` and
`SicWrite` and uses the normal Win32 serial APIs (`CreateFileA`, `ReadFile`,
`WriteFile`, `SetCommState`, `SetCommTimeouts`, `PurgeComm` and `SetupComm`).
This confirms that the hardware side is a conventional COM transport rather
than a proprietary USB-only layer. It validates the transport choice in
`Nir5000SerialSession`, but not the still-unknown scan payload.

Static disassembly further shows that the legacy `SicRead` wrapper supplies a
caller buffer, a 16-bit byte count and a fixed 1000 ms internal timeout. The
new transport keeps timeout explicit rather than hard-coding this value, so
the same behavior can be selected for compatibility while diagnostics can use
a longer timeout.

- Exact meaning and byte order of every header field for a FOSS NIR 5000 scan.
- Baud-rate table entry corresponding to the legacy `BaudRate=5` setting.
- The payload encoding that maps instrument samples to the 1100–2498 nm float
  spectrum saved in legacy `.NIR` files.

The legacy COM table uses Win16 `CBR_*` baud constants. The project value
`BaudRate=5` maps to `0xFF1B`, i.e. 38400 baud; the modern profile loader now
resolves codes 0–5 to 1200, 2400, 4800, 9600, 19200 and 38400 respectively.

Static analysis narrows this last item down: `GET_NIR` passes the received raw
array to `FUN_1020_0000`. That routine treats the data as signed 32-bit values,
iterates configured detector blocks, applies block-dependent correction and
interpolation, and emits floating-point values. Its required calibration tables
and runtime instrument globals are not encoded in the transport frame itself;
therefore a direct byte-to-float decoder would be incorrect.

The relevant `DAT_1048_2f*` and `DAT_1048_316*` globals are zero-initialized
storage in `6500.dll`, not embedded calibration constants. `INST_INIT` fills
them from the instrument handshake/configuration, which explains why the
available binary alone cannot provide a complete universal decoder.

The reconstructed initialization order is: establish the transport handshake,
request the 8-byte serial-number transfer (`0x5B`), then read a 256-byte
instrument header. `GET_INSTHDR` copies detector counts, block lengths and
calibration values from that header into the runtime tables used by
`FUN_1020_0000`.

The static parser routine `FUN_1020_0FEA` confirms the following little-endian
layout in its calibration configuration buffer: a 16-bit block count at
`0x00`, float table A at `0x04`, float table B at `0xAC`, and float table C/step
at `0x154`; all three tables use an 8-byte entry stride.
`Nir5000CalibrationTable` implements these bounds-checked offsets and has a
synthetic regression test. This must not be confused with the separate
256-byte runtime instrument header returned by `GET_INSTHDR`.

The four static configuration resources selected by `FUN_1020_1138` are present
at offsets `0x288`, `0x36B`, `0x44E` and `0x531` in the 16-bit data segment. Their
leading block counts are respectively 0, 1, 2 and 3. The legacy code selects
among them using the runtime instrument code; the available static evidence does
not justify assigning those four resources to model names without a handshake
capture.

These items require either the XDS/related installation artifacts or a passive
serial capture from a real instrument. The application therefore exposes the
transport and codec, but does not silently send an unverified scan command.
