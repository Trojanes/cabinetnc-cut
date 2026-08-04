# Day 07 report

## Goals

Per-operation ToolId binding; no silent single active tool covering all ops; missing ToolId blocks export.

## Done (evidence)

- `CutOp` extended: `ToolId`, `Side`, `StepdownMm`, `SequenceGroup`, `Enabled`
- `ToolCatalog` presets T1 6.35 / T2 10 / T3 3 drill
- `ToolBinder` + `OpsPlanner.FeaturesToOps` auto-binds role→ToolId
- `NcPreflight` **error** `missing_tool_id` when ToolId stripped
- Library defaults/EnsureDefaults inject T1–T3
- Desktop Ops list shows `op/ToolId` groups

## Auto gates

- [x] Generated ops have ToolId (`FeaturesToOps_assigns_tool_ids`)
- [x] Stripped ToolId → preflight fails
- [x] Tests: Domain **25** + Package 16 + Infra 2; Desktop build OK

## ASSUMPTION

- Role map: contour/pocket→T1, groove→T2, drill→T3 (shop may renumber)

## Not done (Day 8+)

- Safe sequence Drill→…→Outer (still Contour-first rank today)
- Stop Desktop contour depth global overwrite
- NC tool-change emission

## Next

Day 8: safe op order + per-panel thickness depths.
