# Day 03 report

## Goals

Workpiece editor: Feature Inspector, Undo/Redo (3 edit classes), dirty Nest/CAM invalidation, export block, garbled-string sweep.

## Done

- Feature Inspector UI (`MainWindow.xaml`): X/Y/diameter/depth/width + Apply
- `PanelEdit.UpdateFeatureParams` + `ClonePanel` preserves Identity/Orientation/EdgeBanding/Notes/Side
- Undo/Redo unit tests cover **move / param change / add feature**
- Dirty banner + `GuardExportPreflight` hard-block when manufacturing dirty
- Nest success → `MarkManufacturingClean`
- Ctrl+Z/Y + Undo/Redo buttons
- Garbled-string sweep: no mojibake left under `dotnet/src` (UI already Chinese)

## Auto gates

- [x] Undo/Redo covers 3 edit classes (`Undo_covers_move_param_and_add_feature`)
- [x] Dirty state blocks export path
- [x] Domain/Package/Infrastructure tests green (11 + 16 + 2)

## Supervise gates (Troy)

- [ ] Change hole depth → Undo restores
- [ ] After geometry edit, old Nest no longer shown as valid/exportable

## Tests

```text
dotnet test -c Release
# Passed: 11 Domain + 16 Package + 2 Infrastructure
dotnet build src/CabinetNC.Desktop -c Release
# 0 errors
```

## Known limits

- Multi-select still not implemented (Day 4+)
- UIA smoke still hangs in non-interactive agent shell (manual desktop OK)
- Pocket param fields reuse depth/width; no dedicated pocket inspector layout yet

## Next

Day 4: clipboard / mirror / context menu / small-panel warning.
