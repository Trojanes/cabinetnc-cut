# CabinetNC Cut (standalone)

Independent cutting-station product. Repo root: **`d:\project\cabinetnc-cut`**  
(Not inside `troysfirstfusionproject-main`.)

Consumes **`cabinetnc.woodjob`** (folder / `.zip`, primary) or legacy `cabinetnc.cut-package` JSON from Fusion.

## Run

```bash
cd d:\project\cabinetnc-cut
npm install
npm run dev
```

Open http://localhost:5177 — **加载示例** or import a woodjob folder (manifest.json + parts.json + …) / legacy `cut_package_*.json`.

Desktop:

```powershell
$env:Path="C:\Program Files\dotnet;"+$env:Path
cd d:\project\cabinetnc-cut\dotnet
dotnet run --project src\CabinetNC.Desktop
```

## Quality gate / manual testing

```powershell
cd d:\project\cabinetnc-cut\dotnet
python -m pip install -r tests\manual\requirements.txt
.\scripts\evaluate-product.ps1 -TargetScore 85 -KeepOpen
```

- Evaluation rules: [docs/testing/PRODUCT_EVALUATION_RULES.md](./docs/testing/PRODUCT_EVALUATION_RULES.md)
- Manual smoke cases: [docs/testing/SMOKE_CASE_LIBRARY.md](./docs/testing/SMOKE_CASE_LIBRARY.md)
- Latest reports: `dotnet/artifacts/evaluation-latest.md` and `smoke-latest.json`

## Native kernel (optional)

```powershell
cd native\cabinetnc_core
.\build.ps1
npm run check:native
```

## Scope

**深度对标 MakerHub 切割站** — 见 [docs/VISION.md](./docs/VISION.md)。

当前 Desktop 已有七模块/五步生产、woodjob 导入、BLF+Clipper2 校验、刀补、
CAM playhead、NC/DXF/工单/JSON 导出、SQLite 工程与车间库。

残差与诚实完成度见 [docs/MAKERHUB_PARITY_PLAN.md](./docs/MAKERHUB_PARITY_PLAN.md)。
