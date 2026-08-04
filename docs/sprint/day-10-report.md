# Day 10 report

## Goals

Per-sheet NC/DXF/manifest bundle; pluggable post (generic / fanuc-like).

## Done (evidence)

- `IPostProcessor` + `GenericMmPostProcessor` / `FanucLikePostProcessor`
- `SheetBundleBuilder.Build` / `WriteToDirectory` → `{job}_S{n}.nc|.dxf|.manifest.json` + `{job}.bundle.json`
- Desktop 一键打包 uses per-sheet bundle (no longer single combined NC + S1-only DXF)
- Tests: two sheets → two artifacts; Fanuc post ends M30

## Auto gates

- [x] Per-sheet files generated in builder tests
- [x] Fanuc-like dialect selectable
- [x] Suite: Domain **35** + Package 16 + Infra 2

## ASSUMPTION

- Default mode: one NC per sheet with `(tool Tn)` comments (not yet split-by-tool files)
- Bundle layout documented in this report

## Bundle layout

```
{job}.bundle.json
{job}_S1.nc / .dxf / .manifest.json
{job}_S2.nc / .dxf / .manifest.json
{job}_sheet.html
```

## Next

Day 11: A/B locating gate (block backside without strategy) — conservative, no fake dual-side CAM.
