# NIR-Rebuild requirement audit

This audit reflects the current branch and the latest self-contained Windows
publish. It distinguishes implementation evidence from evidence that can only
come from a physical NIR 5000 or a deployed STARLIMS/Nova environment.

| Requirement | Current evidence | Status |
|---|---|---|
| 64-bit Windows application | Self-contained `win-x64` GUI and CLI publish; build has 0 warnings/errors | Implemented and locally verified |
| NIR 5000 EQA prediction | Proven type-9 EQA reader, preprocessing, prediction and LIMS export self-tests | Implemented and replay-verified |
| Serial transport | 8N1, ENQ/ACK, frame/checksum, segments, captures and wire trace | Implemented and simulator/self-test verified |
| Direct COM GUI | Open/close, auto-connect, lamp on/off, persistent settings and logging | Implemented; physical COM not tested in this environment |
| COM-to-Ethernet bridge | GUI/CLI bridge, auto-start, listen address/port, raw relay and per-event logging | Implemented; requires a real COM device and host for end-to-end validation |
| Nova integration | Neutral OpenInstrument sample export and configurable authorized HTTP adapter | Implemented as an integration adapter; not a proven NIR 5000 Nova device plugin |
| SafeNet handling | No dongle code or protected vendor binaries copied or bypassed | Deliberately excluded |
| Legacy calibration workflow | `DIAGNSTC.EXE` workflow, settings, exports and lamp request documented | Workflow inventory implemented; non-lamp calibration wire writes remain unproven |
| Lamp control | `InstrSetLamp` statically yields `02 FF`/`02 00`; GUI sends and traces these bytes | Static evidence and transport self-test; no physical acknowledgement claimed |
| Legacy corpus coverage | 40 native files counted and classified in `legacy-binary-inventory.md` | Complete inventory; not every binary is decompiled function-by-function |
| CommaSep2 DCU compatibility | Exact HDR/PS/SE format and StarLIMS business rules compared with `sl-source` | Implemented and fixture-tested |
| STARLIMS uploader | Signed octet-stream client, GUI/CLI, HMAC test vector, response logging | Implemented; deployment URL/destination still environment-specific |
| Startup integration | Installer creates optional Windows Startup shortcut and settings-driven auto-start | Implemented; requires running installer on target laptop |

The remaining external validation gates are concrete: a real serial capture or
instrument session for scan/calibration frames, and a reachable authorized Nova
or STARLIMS endpoint for end-to-end submission. No local test can prove those
two external facts.
