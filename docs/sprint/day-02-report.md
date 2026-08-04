# Day 02 report

## Goals

Freeze soft Workpiece contract on flat runtime panels.

## Done

- `WorkpieceIdentity` / `WorkpieceOrientation` / `EdgeBanding` domain types
- Panel fields: Identity, Orientation, EdgeBanding, Notes, Side
- WoodJob + cut-package importers populate faces / banding / hierarchy ids
- Thickness validation error when ≤0
- CutPackageJson emits workpiece/orientation fields
- Tests: identity on 120 import, thickness reject, round-trip fields
- `docs/sprint/workpiece-contract.md`

## Auto gates

- [x] New fields round-trip (serialize → import)
- [x] demo_woodjob_120 + demo cut-package still import
- [x] Missing thickness → explicit error

## Supervise gates

- [ ] Open a real cabinet sample in Desktop and confirm material/thickness/orientation visible in UI (Identity not yet shown in Inspector — Day 3 Inspector will surface; raw import already carries fields)
- [x] Contract doc one-pager exists

## Tests

`dotnet test -c Release` → 27 passed (11+14+2)

## Next

Day 3: Inspector + Undo/Redo + dirty Nest/CAM invalidation.
