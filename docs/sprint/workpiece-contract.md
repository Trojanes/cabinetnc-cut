# Workpiece contract (Day 2)

Runtime remains a **flat** `CutPackage.Panels[]`. Project / Module / Workpiece are **soft identity fields** on each panel — no UI tree rewrite required yet.

## Panel manufacturing fields

| Field | Source | Required |
|-------|--------|----------|
| `PanelId` / `Identity.WorkpieceId` | part `panelId` / `workpieceId` | yes |
| `Identity.ProjectId` | `projectId` | optional |
| `Identity.ModuleId` | `moduleId` | optional |
| `Material` | `materialId` / materials.json | recommended |
| `ThicknessMm` | part or materials.json | **yes (>0)** |
| `Orientation.PrimaryFace` | `orientation.primaryFace` / `faceUp` | optional (A/B Day 11) |
| `Orientation.MillingFace` | `millingFace` / `millingSurface` / `fromFace` | optional |
| `Orientation.GrainDirection` | `grainDirection` | optional |
| `Orientation.AllowedRotations` | `allowedRotations` | optional |
| `Orientation.AllowMirror` | `allowMirror` | optional |
| `EdgeBanding.*` | `edgeBanding` | optional |
| `Notes` | `notes` | optional |
| `Side` | `side` or face fields | optional placeholder |

## Validation

- Missing / non-positive `thicknessMm` (and not resolvable via materials) → import **error** `thickness`.
- Outline &lt; 3 points → existing outline error.
- Legacy `cabinetnc.cut-package` JSON still imports; serializer now emits workpiece/orientation fields for round-trip.

## Non-goals (later days)

- Forcing SVG as production geometry
- Full UI Project→Module tree
- Dual-face CAM math (Day 11)
