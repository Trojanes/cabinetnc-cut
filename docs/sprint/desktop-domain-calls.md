# Desktop → Domain direct call map (Day 1)

Source of truth at capture: `dotnet/src/CabinetNC.Desktop/MainWindow.xaml.cs`

| Call site (approx) | Domain API | Role today |
|--------------------|------------|------------|
| Nest path (~1173) | `BlfNester.Pack` | Local nest — comment notes “nest is local” |
| Nest verify (~805, ~864, ~1215) | `NestValidator.FindPolygonCollisions` | Clipper poly/gap checks on UI thread |
| Ops rebuild (~717–724) | `OpsPlanner.FeaturesToOps` + `AttachToNest` + `ContourToolOffset.Apply` | CAM ops + tool offset |
| NC preview / export (~1252, ~1362) | `NcEmitter.OpsToNc` | NC text |
| DXF export (~1638, ~1727) | `NestDxfWriter.Write` | Nest DXF (sheet 0) |
| Job sheet (~1659, ~1728) | `JobSheetBuilder.BuildHtml` | HTML shop sheet |
| Preflight (~1601+) | `NcPreflight.Check` (via Out flow) | Export gate |

## Worker usage today

- `CabinetNC.ComputeWorker` is started/hosted for health / gRPC pipe.
- Nest / Ops / Post **authoritative UI path does not go through Worker**.
- Worker services wrap the same Domain types, so drift risk exists until Day 13 convergence.

## Target (plan Day 13)

```text
Desktop → ComputeWorker → Domain kernel
```

Until then, treat Domain static APIs as the algorithmic authority and keep Desktop changes thin.
