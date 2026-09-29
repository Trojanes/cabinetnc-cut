# CabinetNC .NET desktop (PDF stack)

Product content comes from the Vite repo (`cabinetnc.cut-package`, stages, nest/ops/nc behavior).  
Runtime stack follows `docs/STACK_MERGE.md` / the architecture PDF.

## Pack (win-x64)

```powershell
$env:Path = "C:\Program Files\dotnet;" + $env:Path
powershell -ExecutionPolicy Bypass -File dotnet/scripts/pack.ps1
```

Outputs under `dist/`:
- `CabinetNC-Cut/` — the only runnable copy (desktop shortcut opens this)
- `CabinetNC-Cut-*.zip` — app zip
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
| `CabinetNC.Cloud.Contracts` | cloud routes + job manifest shared by Omni / OmniLight / CabLab |
| `CabinetNC.Cloud.Client` | `%ProgramData%\Omni\cloud.json`, HTTP client, job zip, machine-folder placement |
| `CabinetNC.CloudApi` | `omni-api` on Cloud Run (`/v1/health`, `/v1/version`) |

## Cloud API deploy

```powershell
gcloud auth login
powershell -ExecutionPolicy Bypass -File dotnet/scripts/deploy-cloud-api.ps1 -ProjectId <project-id> -WriteLocalConfig
```

Default region `australia-southeast1`; pass `-Region` to move. The script prints the service URL and, with `-WriteLocalConfig`, writes it into `cloud.json`.

No commits from the desktop loop — iterate in-tree.
