# Nirsinst.dll export audit

This inventory is based on the PE export table of the supplied ISIscan
Nirsinst.dll. It separates evidence from implementation so an export cannot be
mistaken for a complete protocol specification.

## Device lifecycle and acquisition

| Export | Evidence/status |
|---|---|
| InstrInitDLL | Export confirmed; native initialization boundary |
| InstrCreate / InstrDestroy | Export confirmed; native driver object lifecycle |
| InstrInitialize / InstrCloseComm / InstrReOpenComm | Export confirmed; COM lifecycle |
| InstrGetReference / InstrGetSpectrum | Export confirmed; acquisition boundary |
| InstrTerminate / InstrTerminateFromTS | Export confirmed; shutdown boundary |
| InstrStabilize | Export confirmed; warm-up/stabilization operation |

NIR-Rebuild currently implements the transport lifecycle, a simulator and
explicit lamp control. The native object ABI is not used from the 64-bit
application.

## Lamp and calibration

| Export | Evidence/status |
|---|---|
| InstrSetLamp / InstrWasLampOn | Disassembled; request payload is 02 FF on and 02 00 off |
| InstrFindLinConst / InstrGetLinear | Export and workflow strings confirmed |
| InstrGainAdjust / InstrGetGain | Export and gain-adjust workflow confirmed |
| InstrSetAmpCorr / InstrGetAmpCorr | Export confirmed |
| InstrSetReference | Export and reference workflow confirmed |
| InstrSetWindowCorr | Export confirmed |
| InstrCorrectSpectrum / InstrGetRefCorrSpectrum | Export confirmed |
| InstrSetLEDPassFail | Export confirmed; this is LED/pass-fail metadata, not the lamp switch |

The new application exposes the proven lamp request. The calibration exports
still need captured request/response sequences before their write operations
are enabled.

## Diagnostics and metadata

| Export | Evidence/status |
|---|---|
| InstrGetTestData | Export and performance-test strings confirmed |
| InstrGetDiagVolts | Export confirmed; diagnostic voltage boundary |
| InstrGetBandwidthNom | Export confirmed |
| InstrGetStatusWnd | Export confirmed |
| InstrGetDriverName / InstrGetDriverVersion | Export confirmed |
| InstrGetInfoString | Export confirmed |
| InstrGetInstrSerialNumber | Export confirmed |
| InstrGetWavelengthRevision | Export confirmed |
| InstrGetNumChannel | Export confirmed |
| InstrGetMotherBoardVer / InstrGetMotherboardSerialNumber | Export confirmed |
| GetAmpSN / GetSplSysSN / GetTSCapabilities | Export confirmed |

## Configuration and optional subsystems

| Export | Evidence/status |
|---|---|
| InstrEditConfiguration / InstrEditDCM / InstrInitializeDCM / InstrSetDCM | Export confirmed; DCM ABI and payloads remain unimplemented |
| InstrCanUseDCM / InstrGetMaxDCMSize | Export confirmed |
| InstrReturnsCorrectedSpec / InstrReturnsSpectrum | Export confirmed; capability/state queries |
| SetInstrFlag / SetWvCorRpt / TSOut | Export confirmed; host-side flags/reporting |

The audit is intentionally explicit: “export confirmed” means the function is
present in the native binary, not that its wire bytes are known. The current
64-bit build does not load the vendor 32-bit DLL and therefore cannot silently
depend on its ABI.
