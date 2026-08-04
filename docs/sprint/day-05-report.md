# Day 05 report

## Goals

Nest settings SSOT; Material+Thickness grouping; Clipper/AABB spacing as export hard gate; unplaced reasons.

## Done (evidence)

- `NestSettings` + grain-lock consistency (`NestSettings.cs`)
- `GroupedBlfNester.Pack` — packs per `Material × ThicknessMm`; remaps sheet indices; group utilization reports
- `NestExportGate.Check` — AABB + Clipper poly + mixed-group-on-sheet hard errors
- Desktop `RunNestAsync` uses grouped packer; sheet queue carries Material/Thickness; blank STOCK clones per group
- `GuardExportPreflight` hard-blocks on nest gate failure (no Yes/No bypass)
- Report UI shows `export_gate: OK|FAIL` and unplaced/group reasons

## Auto gates

- [x] Two materials never share a sheet (`Different_materials_never_share_sheet`)
- [x] Two thicknesses never share a sheet
- [x] Collision → export gate fails (`Export_gate_blocks_poly_collision`)
- [x] Domain/Package/Infrastructure tests green: **19 + 16 + 2**
- [x] Desktop Release build OK

## ASSUMPTION

- Default margin 15 / clearance 12
- GrainLock=true → panels with grain may not use 90° nest rotation
- Blank UI stock (`ThicknessMm=0`, no material) is cloned per group (not shared across groups)

## Not claimed

- Not NFP / not advanced engine (Day 6)
- Worker gRPC nest path still ungrouped AABB (Desktop is authority for Day 5 path)

## Supervise (Troy)

- [ ] Nest report shows unplaced reasons / group lines
- [ ] Change spacing and re-nest changes layout

## Next

Day 6: `INestingEngine` + BLF adapter + advanced stub fallback.
