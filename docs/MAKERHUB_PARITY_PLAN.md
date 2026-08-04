# MakerHub 对标 — 完成度与 Loop 总计划

> 2026-07-22 重写。不绑定历史 Grok 输出；以「车间能走通同类路径」为验收。

## 诚实完成度（分口径）

| 口径 | 本轮前 | 本轮后 | 说明 |
|------|--------|--------|------|
| MakerHub 商用品深度 | ≈25% | **≈40%** | 已有 Clipper 校验/刀补/CAM playhead；仍缺 DXOPT/NFP、标签/BOM、CSV、机型档案、模板 CAD |
| Vite 产品能力迁入 Desktop | ≈60% | **≈85%** | 导出/预检/groove/offset/poly verify/CAM playhead 已补；仍缺 DXF/SVG 导入、recent |
| 自有 Foundation gate | ≈75% | **≈85%** | 七模块与生产闭环已成形，但多个模块仍是浅层 CRUD |

| 域 | 本轮后估分 | 主要残差 |
|----|------------|----------|
| 壳 / IA | 75% | 信息密度、Inspector、多窗口 |
| 导入 | 75% | DXF/SVG/CSV、加密 woodjob |
| 几何编辑 | 55% | 异形/内腔/工艺模板编辑 |
| 排版 Nest | 50% | Clipper gap 校验已有；缺真 NFP、多策略 |
| 刀路 CAM | 60% | 刀补/playhead 已有；缺完整参数表与材料去除仿真 |
| 后置 NC | 45% | 可编辑机型档案、方言向导、试切 |
| 导出 | 65% | 标签/BOM、批量多板输出 |
| 车间库 | 50% | 工具真正驱动全部后置参数 |

「完美实现所有功能」是多周战役；不能用“闭环可演示”替代 MakerHub 深度完成度。

## 北极星路径（车间任务）

```
载入 woodjob → 原料/设备确认 → 密排（含补板/缺陷）→ 刀路可检 → 预检 → 导出 NC/DXF/工单
```

同路径能交付上机文件 = 该切片达标。

## Loop 战役（本轮一次推进）

### P0 — 本轮必须落地
1. **导出套件**：NC 预检门 · 排版 DXF · cut-package JSON · 工单 HTML  
2. **CAM**：groove 进 Ops · 工序表（可勾选启用）· 导出前预检  
3. **Nest**：板材缺陷区避让 · 补板可作为额外板材参与排版 · 利用率报告加深  
4. **路线**：机型工序开关可写并影响 Ops/NC  

### P1 — 本轮尽量落地
5. 最近打开 · Out 阶段一键打包（NC+DXF+工单）  
6. 多材料分组排版（按 materialId 分 sheet 队列）  
7. 简单 CAM 步进列表（非完整仿真）  

### P2 — 记入后续 loop（本轮不硬撑完美）
- 真 NFP / DXOPT 级异形 nest  
- 完整材料去除 CAM 仿真  
- 后置方言向导 / 试切  
- 补板库可视化画布  
- 加密 woodjob · 签名 MSI  
- 关系图驱动装配校验  

## 验收门（本轮结束）

- [x] Out：预检失败则拦截导出（可强制继续）
- [x] 可导出 `.nc` / `.dxf` / `.html` 工单 / `.json` 包 / 一键打包
- [x] groove 出现在工序与 NC（路线开关）
- [x] sheets.defectRegions → nest 不踩（AABB）
- [x] 补板库「参与密排」进入 sheet 队列
- [x] `dotnet build` Desktop 绿

## Loop 2（2026-07-22）

- [x] 引入 Clipper2 C#（无专有二进制）
- [x] 轮廓刀补：外轮廓外偏 / 内轮廓内偏
- [x] 多边形 + gap inflate 排版校验
- [x] CAM point playhead、播放/步进、画布高亮
- [x] 车间刀具直径/进给/转速覆盖实时 NC
- [x] 统一旋转 bbox 原点，修复 90° 板件负 X 刀位风险
- [x] 全 Solution 测试 + Windows UIA 冒烟

### 残差（下一 loop）
- 真 NFP · 材料去除仿真 · 后置向导 · DXF/SVG/CSV 导入 · 标签/BOM · MSI

## Loop 3 — 发布就绪评估

- [x] 建立 100 分评估规则，停止线 85 + 六项硬门槛
- [x] 建立 30+ 手工测试案例及最小发布冒烟集
- [x] UIA 自动冒烟覆盖 8 组关键路径并输出 JSON
- [x] 评估器自动执行全测试、Desktop 构建、UIA、评分报告
- [x] 添加 DXF/工单与 library.json 持久化回归测试
- [x] SQLitePCLRaw native SQLite 从高危 2.1.11 升至 3.53.3
- [x] 最终发布就绪度 **99/100**，目标 85，全部硬门槛 PASS
- [x] 自动冒烟 8/8；多边形校验与预检断言成功文本，防止假阳性

规则：`docs/testing/PRODUCT_EVALUATION_RULES.md`  
案例：`docs/testing/SMOKE_CASE_LIBRARY.md`


## 文档角色

| 文档 | 角色 |
|------|------|
| **本文件** | 完成度真相 + loop 战役 |
| `MAKERHUB_SHELL.md` | 壳模块清单 |
| `FEATURE_GAP.md` | Vite↔Desktop 行级缺口 |
| `VISION.md` | 北极星（可随本文件修订） |
| `LOOP_DESKTOP.md` | Desktop 切片勾选 |
