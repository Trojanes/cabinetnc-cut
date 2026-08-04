# Stack merge — PDF 底架 × 当前产品内容

**Decision (2026-07-21):** 两条线合并，不再二选一。

| 来源 | 管什么 | 不管什么 |
|------|--------|----------|
| **PDF《木工制造软件独立客户端架构方案》** | 底层实现架构：进程、语言、IPC、画布、几何库、发布形态 | 不重置产品范围；不要求换品牌/换 MakerHub 对标目标 |
| **当前仓库 `cabinetnc-cut`** | 产品内容：阶段工作流、UI 行为、geom/nest/ops/nc 能力、机型/库、`cabinetnc.cut-package` 合同、验收切片 | 不把 Vite/Node portable 当作最终交付栈 |

## 目标运行时（来自 PDF）

```text
Fusion 导出 cabinetnc.woodjob（zip；legacy: cabinetnc.cut-package JSON）
        ↓
CabinetNC.Desktop.exe     ← WPF + MVVM + SkiaSharp 画布
        │  gRPC over Named Pipes
        ↓
CabinetNC.ComputeWorker.exe  ← Clipper2 C# · Nest · Ops · Toolpath · Post
        ↓
SQLite + 本地 Project 目录 · Signed MSI/EXE
```

首版约束（跟 PDF）：Windows x64 only · 不用 Electron · 暂不采用 Tauri · 不先堆 Node/Rust/Python 多运行时。

## 产品内容映射（来自当前项目）

| 当前（Vite 原型） | 迁入桌面时的落点 |
|-------------------|------------------|
| 四阶段：几何→排版→刀路→输出 | Desktop 主导航 / 页面流（PDF §12 可对照改名，能力保留） |
| 左树 · 中画布 · 右检视 · 状态栏 | WPF 壳布局；画布用 SkiaSharp 重绘，**交互语义**照现网 |
| `src/geom/*` · MakerHub Outline | Domain Geometry + Package Import |
| `src/pack.js` · nest verify | ComputeWorker Nesting MVP + Validator |
| `src/ops.js` · `cam_sim.js` | Worker Operations / Toolpath |
| `src/nc.js` · machine · preflight · job sheet | PostProcessors + Production Output |
| `src/package.js` · `project.js` · library | FusionPackage 合同 + Project/SQLite |
| `native/cabinetnc_core` Clipper CLI | 过渡期可作对照；正式路径 **Clipper2 C#**（PDF）；CLI 非长期依赖 |
| `docs/VISION.md` M1–M6 · MakerHub 壳 | 仍是产品验收北极星 |

## 双轨期间怎么干活

1. **Vite 原型继续可跑** — 验证产品行为、合同、验收；`npm run check` 仍服务内容切片。  
2. **正式增量落在 .NET Solution** — 按 PDF 进程边界实现；算法/UI 行为以当前 JS 为规格说明书（port，不是 re-invent）。  
3. **合同单一真相** — 磁盘主输入为 **`cabinetnc.woodjob`**（多文件 / `.zip`，schemaVersion 2）；运行时仍归一到 Domain `CutPackage`。遗留单文件 `cabinetnc.cut-package` / `.cut.json` 继续可导入。改字段两边 ACK。  
4. **禁止** 在 Desktop UI 进程内跑长 Nest/Toolpath（跟 PDF 原则 3）。

## 当前落地（`dotnet/`）

已在本仓库旁路落地 PDF 底架至 **Desktop MVP**（**不 commit，边改边做**）：

- `CabinetNC.slnx` · WPF Desktop · ComputeWorker gRPC Named Pipes  
- cut-package 导入 · Skia · BLF Nest · Validator · Ops · NC · 机型/保存 · **SQLite `project.db`**  
- 跟踪：`docs/LOOP_DESKTOP.md`（MVP CLOSED）

## 迁移动作优先级（合并后）

1. Solution 骨架 ✅  
2. Package Import ✅  
3. Skia 2D Viewer ✅  
4. Nesting MVP + Validator ✅  
5. Ops / NC ✅  
6. SQLite project ✅ · 安装包 / Clipper C# / poly nest → 下一战役  

详见 [ARCHITECTURE.md](../ARCHITECTURE.md)。
