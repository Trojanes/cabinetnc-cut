# Desktop loop tracker — ACTIVE (MakerHub parity)

| Slice | Status | Notes |
|-------|--------|-------|
| SDK .NET 10 | ✅ | winget Microsoft.DotNet.SDK.10 |
| Solution `dotnet/CabinetNC.slnx` | ✅ | Desktop / Worker / Domain / FusionPackage / Contracts |
| gRPC Named Pipe Ping | ✅ | `cabinetnc.compute.v1` |
| Import demo cut-package | ✅ | `CutPackageImporter` |
| Import `cabinetnc.woodjob` | ✅ | `WoodJobImporter` / `PackageImporter` · sample `demo_woodjob_120.zip` |
| MakerHub IA shell | ✅ | 七模块 · 五步 · 空态 · 导入弹窗 · 车间库 |
| Export suite + preflight | ✅ | NC/DXF/工单/JSON/一键打包 · 预检门 |
| Nest defects + remnants | ✅ | BLF 缺陷避让 · 补板队列 |
| Clipper2 geometry | ✅ | contour offset · poly/gap verify |
| CAM playhead | ✅ | point frames · playback · canvas highlight |
| Tool → live NC | ✅ | diameter/feed/rpm override |
| Rotation coordinate safety | ✅ | shared bbox-origin transform · no negative rotated XY |
| WPF + Skia + 5 stages | ✅ | 载入/板材/密排/刀路/导出 |
| Ops + NC post | ✅ | contour/drill/groove · machine/tool profile |
| SQLite project store | ✅ | `project.db` Save/Open project |

**Pipeline:** open package → nest/poly verify → offset/sim → preflight → export bundle/project.

## Residual (next campaign)

- True NFP/DXOPT-grade placement (current placement remains AABB-BLF)
- Material-removal CAM simulation (current sim is point playhead)
- Editable post/machine archive · DXF/SVG/CSV import · label/BOM
- SQLitePCLRaw NU1903 · Signed MSI
- See `docs/FEATURE_GAP.md` for full Vite→Desktop checklist
