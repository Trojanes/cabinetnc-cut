# CabinetNC Product Evaluation

- Evaluated: 2026-07-22T13:04:19.9605470+10:00
- Score: **99/100** (target 85)
- Hard gates: **PASS**
- Status: **READY**

## Criteria

| ID | Score | Result | Evidence |
|----|-------|--------|----------|
| A1-import | 6/6 | PASS | tests + SMK-010 |
| A2-nest | 8/8 | PASS | tests + SMK-031 + SMK-036 |
| A3-cam-nc | 8/8 | PASS | tests + SMK-042/043/044 |
| A4-export | 6/6 | PASS | export sources + tests + SMK-046/050-053 |
| A5-persistence | 4/4 | PASS | SQLite/library sources + tests |
| A6-modules | 3/3 | PASS | SMK-002 |
| B1-full-tests | 6/6 | PASS | D:\project\cabinetnc-cut\dotnet\artifacts\evaluation-tests.log |
| B2-rotation-xy | 5/5 | PASS | rotation unit test + UI NC negative-XY assertion |
| B3-invalid-input | 3/3 | PASS | CutPackageImporter rejection tests |
| B4-poly-gap | 4/4 | PASS | Clipper tests + SMK-036 |
| B5-preflight | 4/4 | PASS | NcPreflight tests + SMK-046 |
| B6-review-lint | 2/3 | PARTIAL | 0 CS/high-NuGet/compatibility warnings |
| C1-startup-worker | 3/3 | PASS | SMK-001 |
| C2-stage-gates | 2/2 | PASS | SMK-003 |
| C3-module-navigation | 3/3 | PASS | SMK-002 |
| C4-feedback-dialog | 2/2 | PASS | SMK-010 import result dialog |
| C5-manual-library | 3/3 | PASS | SMOKE_CASE_LIBRARY.md |
| C6-status-preflight | 2/2 | PASS | SMK-046 |
| D1-domain-tests | 3/3 | PASS | Domain/Package/Infrastructure suites |
| D2-repeatable-uia | 2/2 | PASS | smoke JSON + exit code |
| D3-open-dependencies | 2/2 | PASS | Clipper2 package; no MakerHub binaries |
| D4-doc-consistency | 3/3 | PASS | rubric + manual library |
| E1-desktop-build | 5/5 | PASS | D:\project\cabinetnc-cut\dotnet\artifacts\evaluation-build.log |
| E2-pack-script | 3/3 | PASS | scripts/pack.ps1 |
| E3-automated-smoke | 4/4 | PASS | D:\project\cabinetnc-cut\dotnet\artifacts\smoke-latest.json |
| E4-manual-runbook | 3/3 | PASS | manual cases with steps/expected/risk |

## Residual risk

- True NFP/DXOPT-grade placement is not implemented
- CAM simulation is point-playhead, not material removal
- SkiaSharp/OpenTK emits NU1701 target-framework compatibility warnings
- Signed MSI and real-machine validation remain
