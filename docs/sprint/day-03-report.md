# Day 03 report (partial → continuing)

## Goals

Workpiece editor: Undo/Redo, dirty Nest/CAM invalidation, export block when dirty.

## Done tonight

- `EditHistory` + `ProjectSession.ManufacturingDirty`
- `ReplacePanel` records history + marks dirty + refreshes PackageJson
- Ctrl+Z / Ctrl+Y restore snapshots
- Edit clears nest/ops/NC; export blocked until re-nest (`GuardExportPreflight`)
- Successful nest calls `MarkManufacturingClean`
- Unit test `ReplacePanel_marks_dirty_and_undo_restores`

## Not yet (continue next wake)

- Feature property Inspector UI (depth/diameter fields)
- Multi-select
- Fix remaining garbled UI strings sweep
- Full Day 3 supervise checklist

## Auto gates so far

- [x] Undo/Redo unit coverage (move hole)
- [x] Dirty blocks export path
- [x] Existing tests green (28 total: 11+15+2)

## Next

Finish Inspector + then Day 4 clipboard/mirror.
