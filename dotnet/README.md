# CabinetNC .NET desktop (PDF stack)

Product content comes from the Vite repo (`cabinetnc.cut-package`, stages, nest/ops/nc behavior).  
Runtime stack follows `docs/STACK_MERGE.md` / the architecture PDF.

## Pack (win-x64)

```powershell
$env:Path = "C:\Program Files\dotnet;" + $env:Path
powershell -ExecutionPolicy Bypass -File dotnet/scripts/pack.ps1
```

Outputs under `dist/`:
- `CabinetNC-Cut-win-x64/` — runnable folder (`CabinetNC.Desktop.exe`)
- `CabinetNC-Cut-win-x64-*.zip` — self-contained app zip
- `CabinetNC-Cut-src-*.zip` — source archive (no bin/obj/node_modules)


## Layout

| Project | Role |
|---------|------|
| `CabinetNC.Desktop` | WPF + SkiaSharp + worker host |
| `CabinetNC.ComputeWorker` | gRPC Named Pipes worker |
| `CabinetNC.Compute.Contracts` | protobuf + pipe name |
| `CabinetNC.Domain` | panels / outline / package |
| `CabinetNC.FusionPackage` | JSON import of existing cut-package |
| `CabinetNC.Application` | `ProjectSession` |
| `CabinetNC.Infrastructure` | stub (SQLite later) |

No commits from the desktop loop — iterate in-tree.
