# Machine dry-run checklist

Use after loading a real job and exporting a bundle from Desktop (`一键打包`).

## Before power

- [ ] RC build/tag identified (`rc-14d-*` or commit SHA)
- [ ] Read `KNOWN_LIMITATIONS.md`
- [ ] Confirm machine dialect (generic vs Fanuc-like M30)
- [ ] Confirm tool magazine: T1 6.35 / T2 10 / T3 3 (or remapped)

## Bundle contents

- [ ] `{job}.bundle.json` present
- [ ] Each sheet has `{job}_S{n}.nc`, `.dxf`, `.manifest.json`
- [ ] `{job}_bom.csv` and `{job}_labels.html` present
- [ ] Manifest panel/tool IDs match labels

## Preflight (software)

- [ ] Manufacturing not dirty (re-nest after edits)
- [ ] Nest export gate OK (no poly/AABB/mixed-group errors)
- [ ] No missing ToolId
- [ ] Outer contour after drill/groove in NC comments/order
- [ ] Mixed thickness: outer depth = thickness + 0.5 (spot-check 15 vs 18)
- [ ] If any B-side ops exist: registration strategy configured — else expect block

## On machine (air cut)

- [ ] Load `S1.nc` only first
- [ ] Origin = sheet SW (or note if fixture differs)
- [ ] Spindle/tool length offset verified for T1
- [ ] Dry-run contour clears clamps
- [ ] Peck/drill depth safe vs spoilboard (+1.0 mm max software allowance)
- [ ] Repeat for remaining sheets

## Stop / escalate

- [ ] Any collision warning → do not cut
- [ ] Dual-face needed → wait for Day 11 flip answers before B programs
