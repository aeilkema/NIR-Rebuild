# Legacy binary inventory

This is the complete native-binary inventory currently present at the root of
the private NIR-Rebuild corpus. “Static inventory” means file type, exports,
imports and relevant strings were recorded. “Protocol-detailed” means the
binary was additionally used to establish a wire-format or model-format fact.
No legacy binary is executed by the reverse-engineering workflow.

## Inventory totals

| Type | Count |
|---|---:|
| EXE | 17 |
| DLL | 20 |
| LIB | 2 |
| SYS | 1 |
| **Total** | **40** |

## Protocol-detailed or model-detailed

| File | Role | Status |
|---|---|---|
| `6500.dll` | instrument transport, header and spectrum conversion | Protocol- and conversion-detailed; NIR 5000 compatibility still needs a device/header capture |
| `diagnstc.exe` | legacy diagnostics and calibration UI | 16-bit NE; workflow and operation names extracted; exact calibration writes still require caller decoding/capture |
| `Fileio.dll` / `Fileio.lib` / `Fileio.sys` | legacy file and I/O support | Static inventory; file-format evidence used where applicable |
| `prod_ini.dll` | product/profile configuration | Static inventory and profile resources inspected |
| `Pvplus.dll` | legacy prediction/project support | Static inventory; prediction responsibility is implemented independently from proven EQA records |
| `SimDiag.dll` | diagnostics simulation support | Static inventory and strings inspected |
| `Winctl.dll` | Win16 UI/control support | Static inventory; no NIR wire protocol attributed without evidence |
| `Superpro.dll` | legacy protection support | Static inventory only; not copied into NIR-Rebuild |

## Complete application inventory

These files are present and accounted for. They are not all protocol drivers;
many are UI, reporting, conversion or analysis utilities. Each is currently
classified as static-inventory-only unless listed above.

### Executables

`3dgraph.exe`, `diagnstc.exe`, `Discrim.exe`, `Fileconv.exe`, `Fileview.exe`,
`Global.exe`, `Graphisi.exe`, `Isireg.exe`, `Local.exe`, `Monitor.exe`,
`Nn.exe`, `Prjman2.exe`, `Reduce.exe`, `Scores.exe`, `Stand.exe`,
`unstall.exe`, `winscan.exe`.

### Dynamic libraries

`6500.dll`, `Cg16.dll`, `Chkcell.dll`, `control.dll`, `Fileio.dll`, `hist.DLL`,
`lang.dll`, `prod_ini.dll`, `Pvplus.dll`, `Qainst.dll`, `Reporter.dll`,
`results.dll`, `Sampnum.dll`, `Sampnum2.dll`, `SimDiag.dll`, `spectrum.dll`,
`Superpro.dll`, `Symmdll.dll`, `Text.dll`, `Winctl.dll`.

### Import libraries and driver file

`demo.lib`, `Fileio.lib`, `Fileio.sys`.

## Interpretation

The inventory is complete as a corpus accounting mechanism, not a claim that
all 40 binaries have been decompiled function-by-function. The implementation
claims in NIR-Rebuild are tied to the protocol-detailed rows and to the
separate ISIscan/Nova installer inventories. The remaining static-inventory
rows are candidates for focused analysis when a function is needed; they are
not silently treated as equivalent to the NIR 5000 instrument driver.

The calibration boundary is separately documented in
`legacy-calibration-workflow.md`, and the full native export audit of the newer
driver is in `nirsinst-export-audit.md`.
