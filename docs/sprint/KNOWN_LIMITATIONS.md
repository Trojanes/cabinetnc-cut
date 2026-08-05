# Known limitations (RC)

Honest limits — do **not** treat these as done.

## CAM ordering (audited)

- `CamSafety.OrderSafe` sorts **SequenceRank before PanelId** (sheet → rank → panel → tool → feature). All drills/grooves on a sheet complete before any outer contour, even across panels.
- Over-deep grooves are **not** clamped in `ApplyPanelDepths`; `NcPreflight` / `DepthIssues` must still see the illegal depth (`groove_too_deep`).

## Dual-face CAM (Day 11 PARTIAL)

- B-side ops without `FaceRegistration.Strategy` are **blocked** (`no_registration`).
- Production `S{n}_A.nc` / `S{n}_B.nc` WCS after physical flip is **not** implemented pending Troy answers (flip axis, origin, default strategy).
- `DoubleSideGate.MirrorLocal` is math-only; not shop WCS.

## Nesting

- Engine is **grouped BLF (AABB)**, not NFP. Advanced stub always falls back (`blf_fallback`).
- Part-in-part model exists but is **disabled**.
- Desktop nests via Domain locally; Worker uses the same Domain router when called (not every Desktop nest RPC-roundtrips).

## Pocket

- Zigzag + Clipper inset clear v1. Not trochoidal; corner residual may remain after finish inset pass.
- Scan strokes are **disjoint segments**; `NcEmitter` rapid (G0) between segments and emits finish loop separately — not one continuous contour closed to path[0].

## Import / CAD

- DXF importer: rectangles / LWPOLYLINE points. **No** full arc tessellation / CAD editor.
- Cross-project import: Domain `WorkpieceImporter` only (no polished Desktop UI wizard yet).

## Post / tools

- Per-sheet single NC with `(tool Tn)` comments. **Not** split-by-tool files.
- Tool IDs ASSUMED T1/T2/T3 presets — shop must confirm magazine numbers.
- **Open audit:** ToolCatalog per-tool Feed/RPM may not yet drive real `S`/`F` on tool change (still machine-profile global) — Fix 4 pending.

## UIA

- Automated UIA smoke can hang in non-interactive agent shells; manual Desktop smoke still required.

## Not in RC (Campaign 2+)

- True NFP / engine contest
- Material-removal solid simulation
- Signed MSI / learning patterns
- Encrypted woodjob
