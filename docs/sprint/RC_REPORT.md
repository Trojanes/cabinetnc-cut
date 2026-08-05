# RC report (14-day sprint)

**Branch:** `sprint/14d-rc`  
**HEAD at report authoring:** see latest tag / `git rev-parse HEAD`  
**Date:** 2026-08-05

## P0 / manufacturing chain vs RC definition

| Requirement | Status | Evidence |
|-------------|--------|----------|
| Editable 2.5D workpiece | Done (auto) | Day 3–4 commits; undo/inspector/mirror tests |
| Nest by material+thickness | Done (auto) | `GroupedBlfNesterTests` |
| Clipper/AABB export hard gate | Done (auto) | `NestExportGate` |
| Per-op ToolId | Done (auto) | `ToolBinderTests` |
| Safe order + depth by thickness | Done (auto) | `CamSafetyTests` + audit `OrderSafe_runs_all_drills_before_any_outer_across_panels` |
| Pocket area clear v1 | Done (auto) | `PocketClearerTests` (not boundary-only) |
| Per-sheet NC/DXF/manifest | Done (auto) | `SheetBundleBuilderTests` |
| Preflight blocks bad export | Done (auto) | dirty/nest/tool/depth/registration gates |
| A/B requires registration | **Partial** | Gate blocks B; no production flip NC |
| Labels + BOM same WorkpieceId | Done (auto) | `LabelBomBuilderTests` |
| Desktop/Worker nest engine align | Done (auto) | Worker→`NestEngineRouter`; parity test |
| Full auto tests green | Done (auto) | Domain+Package+Infra |
| Machine checklist | Done (doc) | `MACHINE_DRYRUN_CHECKLIST.md` |

## Day auto-gate rollup

| Day | Auto gates | Notes |
|-----|------------|-------|
| 1–2 | Pass | baseline + workpiece contract |
| 3–4 | Pass | editor + clipboard/mirror |
| 5–6 | Pass | grouped nest + engine router (BLF authority) |
| 7–9 | Pass | tools, CamSafety, pocket zigzag |
| 10 | Pass | per-sheet bundle |
| 11 | **Partial** | registration gate only |
| 12 | Pass (core) | labels/BOM/DXF rect; importer Domain-only |
| 13 | Pass (core) | Worker router; Desktop still local Domain call |
| 14 | Docs + regression | freeze new features; limitations listed |

## Explicit non-claims

- Not NFP
- Not production dual-face WCS
- Not material-removal simulation
- Not signed installer

## Recommend

- **Conditional RC** for single-face nesting jobs with T1/T2/T3 and per-sheet bundle.
- **Do not** ship dual-face jobs until Day 11 flip answers implemented.
