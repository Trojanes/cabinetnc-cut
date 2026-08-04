# CabinetNC Cut — Vision

## One sentence

**深度对标 MakerHub 的独立切割站**：Fusion（及其他 CAD）只做上游导出；本产品在交互、工作流、功能深度上系统模仿 MakerHub，用自有内核与 `cabinetnc.woodjob` / cut-package 合同实现。

## Honest status (2026-07-22)

按 MakerHub 商用品深度约 **≈40%**；按 Vite→Desktop 迁入约 **≈85%**。壳与导入较强，Clipper2 多边形校验、刀补与 CAM playhead 已落地；Nest 排放仍为 AABB-BLF（非 NFP），标签/BOM、CSV、机型档案与后置向导仍在残差。

真相来源：`docs/MAKERHUB_PARITY_PLAN.md`（优先于历史里程碑表）。

## North star path

```
载入 woodjob → 原料/设备 → 密排（缺陷避让·补板队列）→ 刀路可开关 → 预检 → 导出 NC/DXF/工单/JSON
```

## Product shape

```text
Fusion / CAD
    ↓ cabinetnc.woodjob (+ legacy cut-package)
CabinetNC Cut  ≈  MakerHub 切割站
    七模块 · 五步生产 · 库 · Nest · CAM · 后置 · 导出
    ↓
NC / DXF / 工单 HTML / JSON → 机床 / 车间
```

## Relation to Fusion

Fusion 插件保持上游导出器；切割站功能不回流进 Fusion palette。
