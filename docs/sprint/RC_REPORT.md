# RC report (14-day sprint) — audit2

**Branch:** `sprint/14d-rc`  
**Tag:** `rc-14d-audit2-20260805`  
**Date:** 2026-08-05

## P0 / manufacturing chain vs RC definition

| Requirement | Status | Evidence |
|-------------|--------|----------|
| Editable 2.5D workpiece | Done (auto) | Day 3–4 commits; undo/inspector/mirror tests |
| Nest by material+thickness | Done (auto) | `GroupedBlfNesterTests` + `RcRegressionCoverageTests` |
| Clipper/AABB export hard gate | Done (auto) | `NestExportGate` |
| Per-op ToolId | Done (auto) | `ToolBinderTests` |
| Safe order + depth by thickness | Done (auto) | `CamSafetyTests` + groove depth audit |
| Pocket area clear v1 | Done (auto) | `PocketClearerTests` + segment audit |
| Pocket safety gates | Done (auto) | `PocketSafetyGateTests` (`pocket_depth_missing`, `pocket_too_small_for_tool`) |
| Sheet × Tool NC export | Done (auto) | `SheetToolSplitNcTests` — no mixed-tool NC; no invented M6 |
| Per-sheet DXF/manifest | Done (auto) | `SheetBundleBuilderTests` |
| Preflight blocks bad export | Done (auto) | Domain `Build` enforcePreflight + Desktop hard-block |
| A/B requires registration | **Partial** | Gate blocks B; no production flip NC |
| Labels + BOM same WorkpieceId | Done (auto) | `LabelBomBuilderTests` |
| Desktop/Worker nest engine align | Done (auto) | `DesktopWorkerNestParityTests` |
| Full auto tests green | Done (auto) | Domain+Package+Infra |
| Machine checklist | Done (doc) | `MACHINE_DRYRUN_CHECKLIST.md` |
| Desktop UIA smoke | **MANUAL PENDING** | `MANUAL_SMOKE_10MIN.md` — do not mark UIA PASS in agent shell |

## Day auto-gate rollup

| Day | Auto gates | Notes |
|-----|------------|-------|
| 1–2 | Pass | baseline + workpiece contract |
| 3–4 | Pass | editor + clipboard/mirror |
| 5–6 | Pass | grouped nest + engine router (BLF authority) |
| 7–9 | Pass | tools, CamSafety, pocket zigzag |
| 10 | Pass | per-sheet DXF/manifest; **NC now Sheet×Tool** |
| 11 | **Partial** | registration gate only |
| 12 | Pass (core) | labels/BOM/DXF rect; importer Domain-only |
| 13 | Pass (core) | Worker router; Desktop still local Domain call |
| 14 / audit2 | Pass (auto) + MANUAL PENDING | pocket gates + tool-split NC + regression |

## Explicit non-claims

- Not NFP
- Not production dual-face WCS
- Not material-removal simulation
- Not signed installer
- Not automatic M6 / ATC (interface reserved: `IToolChangePost` / `NullToolChangePost`)

## Single-face support (conditional RC)

Supported for single-face jobs with T1/T2/T3 presets when Preflight is green and export is Sheet×Tool NC + per-sheet DXF.

## Recommend

- **Conditional RC** for single-face nesting jobs.
- **Do not** merge to `main` until manual Desktop smoke is signed and machine dry-run checklist started.
- **Do not** ship dual-face jobs until Day 11 flip answers implemented.
