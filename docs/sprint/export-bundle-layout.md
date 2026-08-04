# Export bundle layout (Day 10)

See `SheetBundleBuilder` and `day-10-report.md`.

Per job export directory:

| File | Meaning |
|------|---------|
| `{job}.bundle.json` | Root index of sheets/posts |
| `{job}_S{n}.nc` | Sheet-local NC |
| `{job}_S{n}.dxf` | Sheet nest DXF |
| `{job}_S{n}.manifest.json` | Panel/tool/op inventory for sheet |
| `{job}_sheet.html` | Job sheet (optional) |
| `{job}.cut.json` | Package snapshot (Desktop adds) |
