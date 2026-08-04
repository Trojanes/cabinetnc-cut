# Day 12 report

## Goals

Labels + BOM CSV sharing WorkpieceId with manifests; basic DXF outline import.

## Done (evidence)

- `LabelBomBuilder` → labels HTML + BOM CSV (Project/Module/Workpiece/Material/Thickness/Sheet/Side/Edge/Tools)
- Bundled into `SheetBundleBuilder` (`*_labels.html`, `*_bom.csv`)
- `DxfOutlineImporter` rectangle LWPOLYLINE round-trip via NestDxfWriter
- ID subset test: label WorkpieceId ⊆ manifest panel→workpiece map

## Auto gates

- [x] Export includes labels + bom.csv
- [x] Label IDs align with workpiece ids on panels
- [x] DXF rectangle outline import
- [x] Suite Domain **42** + Package 16 + Infra 2

## Not done

- Cross-project workpiece import UI
- Arc tessellation DXF (rectangles only)

## Next

Day 13 Worker↔Desktop nest/ops convergence — after dual-side flip answers if needed for B NC files.
