# Vite → Desktop feature gap

Product oracle: `src/`. Runtime: `dotnet/`.  
Plan: `docs/MAKERHUB_PARITY_PLAN.md`.

| Area | Vite | Desktop now | Pri |
|------|------|-------------|-----|
| Shell 七模块 + 五步 | partial | ✅ | ✅ |
| woodjob 导入 | JS assemble | ✅ zip/folder | ✅ |
| 导入结果弹窗 | hint | ✅ | ✅ |
| Geom 编辑 | ✅ | ✅ | ✅ |
| Nest BLF + 锁定 | ✅ | ✅ local BLF + Clipper2 verify | ✅ |
| 缺陷区避让 | — | ✅ AABB punch | ✅ |
| 补板参与密排 | — | ✅ UseInNest queue | ✅ |
| groove / cutout ops | ✅ | ✅ | ✅ |
| 路线工序开关 | — | ✅ writable | ✅ |
| NC 预检门 | ✅ | ✅ | ✅ |
| DXF / 工单 / JSON / 打包 | ✅ | ✅ Out 阶段 | ✅ |
| CAM 仿真动画 | cam_sim | ✅ point playhead + canvas highlight | ✅ |
| 轮廓刀补 | native offset | ✅ Clipper2 outer/inner offset | ✅ |
| Poly/gap verify | nest_verify | ✅ Clipper2 inflate/intersect | ✅ |
| 真 NFP 排样 | DXOPT/advanced | ❌ BLF placement remains AABB | P2 |
| 刀具→实时 NC | library/machine | ✅ diameter/feed/rpm override | ✅ |
| 后置方言向导 | machine wizard | ❌ | P2 |
| 加密 woodjob / MSI | — | ❌ | P2 |

**本轮：** Clipper2 刀补/多边形校验 + CAM playhead + 刀具实时驱动 NC。残差 = 真 NFP / 后置向导 / DXF-SVG-CSV 导入。
