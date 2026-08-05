# Machine dry-run checklist

Use after loading a real job and exporting a bundle from Desktop (`一键打包`).

## Before power

- [ ] RC build/tag identified (`rc-14d-audit2-20260805` or commit SHA)
- [ ] Read `KNOWN_LIMITATIONS.md`
- [ ] Confirm machine dialect (generic vs Fanuc-like M30)
- [ ] Confirm tool magazine: T1 6.35 / T2 10 / T3 3 (or remapped)
- [ ] Confirm **no M6 in files** — operator loads one Tool program at a time (or uses shop ATC manually)

## Bundle contents

- [ ] `{job}.bundle.json` present (`outputPolicy: sheet_x_tool_nc`)
- [ ] Each sheet has `{job}_S{n}.dxf`, `{job}_S{n}.manifest.json`
- [ ] Each sheet×tool has `{job}_S{n}_{Tn}.nc` (not a mixed-tool single NC)
- [ ] Manifest `programs[]` lists every tool NC
- [ ] `{job}_bom.csv` and `{job}_labels.html` present
- [ ] Manifest panel/tool IDs match labels

## Preflight (software)

- [ ] Manufacturing not dirty (re-nest after edits)
- [ ] Nest export gate OK (no poly/AABB/mixed-group errors)
- [ ] No missing ToolId
- [ ] No `pocket_depth_missing` / `pocket_too_small_for_tool`
- [ ] Outer contour after drill/groove in NC order within each tool file
- [ ] Mixed thickness: outer depth = thickness + 0.5 (spot-check 15 vs 18)
- [ ] If any B-side ops exist: registration strategy configured — else expect block
- [ ] NC header shows ToolId / DiameterMm / FeedXY / FeedZ / RPM matching ToolCatalog

## On machine (air cut)

- [ ] Load `S1_T1.nc` only first (single tool)
- [ ] Origin = sheet SW (or note if fixture differs)
- [ ] Spindle/tool length offset verified for that tool
- [ ] Dry-run contour clears clamps
- [ ] Then load `S1_T2.nc` / `S1_T3.nc` as required (manual tool change)
- [ ] Peck/drill depth safe vs spoilboard (+1.0 mm max software allowance)
- [ ] Repeat for remaining sheets

## Stop / escalate

- [ ] Any collision warning → do not cut
- [ ] Dual-face needed → wait for Day 11 flip answers before B programs
- [ ] Need automatic M6 → stop and confirm controller syntax before coding Post
