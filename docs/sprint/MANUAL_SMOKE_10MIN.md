# Desktop manual smoke (10 minutes) — RC audit2

**Status:** `MANUAL PENDING` (automated UIA must not be marked PASS in agent shells)

**Build:** Release Desktop from `sprint/14d-rc`  
**Tag target:** `rc-14d-audit2-20260805`  
**Date:** 2026-08-05

Do **not** claim UIA PASS if `smoke_desktop.py` hangs or is skipped.

## Checklist

| # | Step | Pass? | Notes |
|---|------|-------|-------|
| 1 | Import real woodjob (`demo_woodjob_120.zip` or shop job) | MANUAL PENDING | |
| 2 | Edit a hole/groove → Nest/CAM dirty / export blocked until re-nest | MANUAL PENDING | |
| 3 | Undo / Redo restores feature + dirty flags | MANUAL PENDING | |
| 4 | Mirror panel; confirm outline/features | MANUAL PENDING | |
| 5 | Material × Thickness grouping (no mixed sheet) | MANUAL PENDING | |
| 6 | Pocket with explicit depth clears; missing depth / too-small fails Preflight | MANUAL PENDING | |
| 7 | Preflight: hard errors cannot be overridden (pocket/tool/depth) | MANUAL PENDING | |
| 8 | 一键打包: files are `{job}_S{n}_{Tn}.nc` + `{job}_S{n}.dxf` + manifest | MANUAL PENDING | |
| 9 | Spot-check NC header: ToolId, DiameterMm, FeedXY, FeedZ, RPM | MANUAL PENDING | |
| 10 | Labels HTML + BOM CSV share WorkpieceId with panels | MANUAL PENDING | |

## Stop rules

- Any hard Preflight error → do not cut.
- Dual-face / B-side → blocked without registration (still Partial).
- Do not invent M6 on the controller until shop dialect confirmed.

## Sign-off

- Operator: _______________
- Date: _______________
- Result: PASS / FAIL / PARTIAL
