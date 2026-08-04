# Day 09 report

## Goals

Pocket area clear v1 (zigzag + inset), not boundary-only; small-panel preflight warn.

## Done (evidence)

- `PocketClearer`: Clipper inset (toolR + onion 0.5) + zigzag scan + finish inset loop
- `OpsPlanner` pocket features expand to clear path via T1 diameter
- `NcPreflight` warn `small_panel` via `PanelEdit.IsSmallPanel`
- Tests assert path ≫ boundary and stepover changes length

## Auto gates

- [x] Pocket point count ≫ single loop
- [x] Stepover affects path length
- [x] Small panel warning case
- [x] Full suite: Domain **33** + Package 16 + Infra 2

## ASSUMPTION

- Onion/finish allowance 0.5 mm; stepover 40% of tool Ø
- Zigzag is area clear v1 — not adaptive Trochoidal / true leftover cleanup

## Not claimed

- Material removal simulation
- Perfect corner residual zero (finish pass is inset boundary only)

## Next

Day 10: per-sheet NC/DXF/manifest bundle + post interface.
