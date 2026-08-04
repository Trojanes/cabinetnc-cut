# Day 01 report

## Goals

Baseline freeze: branch, tests, golden, call map, honest status.

## Done

- Branch `sprint/14d-rc`
- `dotnet test` PASS (25 → later days may add)
- Desktop Release + Debug build PASS
- GoldenExport tool + `docs/sprint/golden/` (120 panels placed, 19 sheets)
- `current-baseline.md`, `desktop-domain-calls.md`, `log.md`

## Auto gates

- [x] `dotnet test` green
- [x] Desktop Release build 0 error
- [~] UIA smoke — **blocked in Cursor agent shell**: `pywinauto Application.start` hangs with no output; Desktop window title verified interactively as `CabinetNC Cut`. Prior machine pass retained: `dotnet/artifacts/smoke-latest.json` (8/8, 2026-07-22). Re-run on interactive desktop: `python smoke_desktop.py --json ...`
- [x] Golden files present under `docs/sprint/golden/`

## Supervise gates

- [x] Honest baseline doc (not “99% commercial”)
- [x] Desktop→Domain call map readable

## ASSUMED

- Post: Fanuc-like available; golden used `nesting_router_6`
- T1/T2/T3 defaults per plan

## Commits

(see git log on `sprint/14d-rc`)

## Next

Day 2 workpiece contract fields + validation (started in same overnight push).
