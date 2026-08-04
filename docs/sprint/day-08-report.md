# Day 08 report

## Goals

Safe op order Drill→Pocket→Groove→Inner→Outer; outer depth = thickness + allowance; stop global ContourDepthMm overwrite; spoilboard/depth preflight.

## Done (evidence)

- `CamSafety` sequence ranks + `ApplyPanelDepths` + `DepthIssues`
- `OpsPlanner` emits outer depth = `ThicknessMm + 0.5`; pocket as `Op=pocket`
- `NcEmitter` uses CamSafety order; comments `(tool Tn)` + `depth=…`; no global depth wipe
- Desktop + Worker removed `ContourDepthMm` overwrite on all contours
- Preflight accepts panel map for depth/spoilboard errors

## Auto gates

- [x] Outer index after drill/groove
- [x] 15mm outer depth 15.5 vs 18mm outer 18.5 (not global 18)
- [x] Illegal groove depth → preflight fail
- [x] Tests Domain **29** + Package 16 + Infra 2

## ASSUMPTION

- ThroughAllowanceMm = 0.5
- SpoilboardAllowMm = 1.0

## Next

Day 9: Pocket area clear v1 (not boundary-only).
