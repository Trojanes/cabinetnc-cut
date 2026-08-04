# CabinetNC Cut — 14 天成果计划（验收驱动修订版）

> 基线：`main` @ `7b53081`  
> 立场：**只关心最终成果**；Cursor loop 可持续推进；Troy **只做监督与规则拍板**。  
> 本文件替代交接稿中“按 28 人工小时估工期”的读法，保留并细化其制造主链目标。

---

## 0. 最终成果定义（Release Candidate）

14 天后必须交付一个 **RC**，满足：

```text
真实 woodjob / cut-package
  → 可编辑 2.5D 工件
  → 按材料+厚度分组 Nest（BLF + Clipper 硬校验）
  → 每道工序绑定刀具
  → 安全工序顺序 + 按板厚切深
  → Pocket 真清角 v1（非只走一圈）
  → 每 Sheet 独立 NC/DXF/manifest/工单
  → Preflight 错误挡导出
  → A/B 面有定位策略才允许反面程序
  → 标签 + BOM 与 Workpiece ID 同源
  → Desktop 与 ComputeWorker 对 Nest/Ops/Post 结果一致
  → 全自动测试绿 + 上机检查表
```

**明确仍可留到 Campaign 2+（不挡 RC）：**

- 真 NFP / Part-in-part / 多引擎竞赛
- 材料去除体仿真、后置向导、Signed MSI
- 自动学习人工 pattern
- DXF/SVG 完整 CAD 编辑器

**RC 成功标准一句话：**

> 混厚、多 Sheet、多刀具时，错误深度/顺序/缺刀/碰撞会挡导出；每张板有可追踪程序与标签；空跑检查表可执行。

---

## 1. 监督模式（Troy）

每天验收控制在 **10–20 分钟**：

1. 打开当日 `docs/sprint/day-NN-report.md`
2. 看 **Gate 勾选表**（全绿才算过）
3. 若需规则决策：只回答当日「待 Troy 拍板」清单（是/否或选一项）
4. 回复：`Day N 验收通过` / `Day N 退回：<一条原因>`

不要求 Troy 写代码、不要求 Troy 跑完整测试套件（Cursor 跑）。

---

## 2. Cursor 每日协议

### 开场必须写明

```text
1. 今日目标（一句话）
2. 对应本文 Day N
3. 预计改动面（Domain / Desktop / Worker / schema / docs）
4. 是否改 woodjob schema（默认否；改则需 Troy 事前批准）
5. 自动测试计划
6. 验收 Gate 列表（复制自本文）
```

### 收工必须交付

```text
docs/sprint/day-NN-report.md
- 实际改动文件
- 测试命令与结果
- Gate 勾选
- 已知限制
- Commit SHA（小步、按功能拆分）
- 明日入口
```

### 硬规则

- Gate 未全绿 → **不得进入下一天**（可同日继续修）。
- 禁止用“能编译 / UI 看着行”代替 Gate。
- Desktop 新增算法权威路径 → 禁止；长计算必须可走 Worker（Day 13 收敛）。
- 大快照提交禁止；按 Gate 切片 commit。

---

## 3. 制造主链里程碑（跨天）

| 里程碑 | 完成于 | 含义 |
|--------|--------|------|
| M0 基线冻结 | Day 1 | 测试全绿 + golden 快照 |
| M1 数据合同 | Day 2 | Workpiece 制造信息完整、不靠 SVG 猜 |
| M2 可撤销编辑 | Day 3–4 | 编辑器 + 剪贴板/镜像 |
| M3 Nest 可靠 | Day 5–6 | 参数统一 + 引擎接口（BLF 权威，高级可回退） |
| M4 刀具与安全 CAM | Day 7–9 | Tool 绑定 + 顺序/切深 + Pocket v1 |
| M5 可追踪输出 | Day 10–12 | 后置/分 Sheet + A/B + 标签 BOM |
| M6 RC | Day 13–14 | Worker 一致 + 全回归 + 上机表 |

---

## 4. 逐日计划（目标更细 + 可验收）

每日本节结构：

- **成果**：今天结束后世界多了什么
- **实现切片**：Cursor 要落地的具体项
- **自动 Gate**：机器可判
- **监督 Gate**：Troy 10–20 分钟可判
- **待拍板**：没有答案则当日可实现默认，但报告里标 `ASSUMED`

---

### Day 1 — 基线、分支、Golden、调用图

**成果：** 可回归的起点；后续 diff 有对照物。

**实现切片：**

1. 建分支 `sprint/14d-rc`
2. 跑通：`dotnet test`、Desktop Release build、UIA smoke、120 板导入
3. 导出一套 golden：`docs/sprint/golden/`（NC/DXF/工单/JSON）
4. 列出 Desktop→Domain 直调清单 → `docs/sprint/desktop-domain-calls.md`
5. 建 `docs/sprint/log.md` 与本计划链接

**自动 Gate：**

- [ ] `dotnet test` 全绿
- [ ] Desktop Release build 0 error
- [ ] UIA smoke 通过（或记录阻塞原因并修到过）
- [ ] golden 文件存在且路径写入 baseline 文档

**监督 Gate：**

- [ ] `docs/sprint/current-baseline.md` 存在，且完成度表述诚实（非 99% 商用）
- [ ] 直调清单可读

**待拍板（可 ASSUMED 默认）：**

- 目标控制器方言：默认 `Fanuc-like`（可改）
- T1/T2/T3：默认 6.35 / 10 / 3 mm（可改）

---

### Day 2 — Workpiece 合同冻结（兼容 flat 运行时）

**成果：** 制造身份与朝向进入正式模型；legacy cut-package 仍可用。

**实现切片：**

1. 定义 `Project / Module / Workpiece` **数据字段**（可先映射到现有 Panel + Source 元数据，不强制立刻拆 UI 树）
2. woodjob 扩展或 version bump，字段含：Material、Thickness、Primary/Milling Face、Grain、AllowedRotations、AllowMirror、EdgeBanding、Notes、Side A/B 占位
3. Importer validation + migration tests
4. legacy cut-package adapter 保持绿灯
5. 文档：`docs/sprint/workpiece-contract.md`

**自动 Gate：**

- [ ] 新字段 round-trip 测试通过
- [ ] 旧样本 `demo_woodjob_120` / `demo_cut_package` 仍导入成功
- [ ] 缺关键制造字段时 validation 报明确错误（至少 thickness + outline）

**监督 Gate：**

- [ ] 打开一份真实柜体样本：板件能看到材料/厚度/朝向，不靠猜
- [ ] 合同文档一页能看懂

**待拍板：**

- 板件命名规则
- 单面色/双面色 → Milling Face 规则

---

### Day 3 — Workpiece Editor：Inspector + Undo + 失效联动

**成果：** 编辑器是“工件编辑器”，不是只能挪孔的补丁工具。

**实现切片：**

1. Feature 属性 Inspector（Drill/Groove/Pocket 参数）
2. Undo / Redo（至少覆盖移动/改参/增删特征）
3. 编辑后使 Nest/CAM/NC **失效**（UI 明确脏状态，禁止当有效导出）
4. 修现有乱码文案
5. 编辑回归测试

**自动 Gate：**

- [ ] Undo/Redo 单测或 UI 自动化至少覆盖 3 类编辑
- [ ] 脏状态时 Preflight/导出拦截（或强制提示+阻断默认路径）
- [ ] 现有 Domain/Package 测试不回退

**监督 Gate：**

- [ ] 改一个孔深 → Undo 回来
- [ ] 改几何后旧 Nest 不再显示为“已完成有效”

---

### Day 4 — 剪贴板 / 镜像 / 右键 / 小板警告

**成果：** 常用编辑手势齐备，镜像保留制造语义。

**实现切片：**

1. Ctrl+C/V/X、Delete
2. Mirror X/Y（Outline + Features + Grain/Side/EdgeBanding 策略）
3. 右键菜单
4. 复制 ID 生成规则
5. 小板尺寸阈值警告入口

**自动 Gate：**

- [ ] Mirror 后特征坐标与面向字段测试通过
- [ ] 复制产生新 ID、不破坏原件引用
- [ ] 快捷键/菜单烟雾用例通过

**监督 Gate：**

- [ ] 镜像一块门板：图形对、朝向/封边字段仍合理
- [ ] 小板警告能看见

**待拍板：**

- 小板阈值（默认 ASSUMED：最短边 < 80 mm 或面积 < 0.02 m²）

---

### Day 5 — Nest 参数统一与硬门

**成果：** Nest 设置单一真相；混材料/混厚度不混板；校验挡导出。

**实现切片：**

1. 统一 Nest Settings UI+模型：Margin、Clearance、AllowRotation、AllowedRotations、RotationStep、GrainLock、MirrorPermission、Locked、Defects、Remnants
2. Grain 与 AllowedRotations 一致性校验
3. **按 MaterialId + ThicknessMm 分组** sheet 队列
4. 利用率 + 未排原因报告
5. Clipper 多边形/间距验证 = 导出硬门

**自动 Gate：**

- [x] 两材料或两厚度样本不会排进同一 sheet
- [x] 碰撞/间距失败 → 导出被拦
- [x] Nest 相关单测扩展且全绿

**监督 Gate：**

- [ ] 看一眼报告：未排件有原因
- [ ] 改间距后重排结果变化符合预期

**待拍板：**

- 默认边距/间距数值 — ASSUMED 15 / 12（见 day-05-report）
- 有木纹默认是否锁 0/180 — ASSUMED GrainLock=true

---

### Day 6 — Nest 引擎接口（BLF 权威 + 可插拔高级）

**成果：** 架构允许换引擎；高级失败自动回退；结果带来源标记。

**实现切片：**

1. `INestingEngine` + BLF adapter
2. Advanced engine **原型**（可为 stub/简化 Clipper 搜索或上游桥接评估），不要求商用 NFP
3. Validator 共用
4. 引擎对比日志（利用率、耗时、回退原因）
5. Part-in-part **数据模型**占位（可不启用）
6. 选型记录：`docs/sprint/nest-engine-decision.md`

**自动 Gate：**

- [x] 默认路径仍是 BLF，行为与 Day 5 兼容
- [x] 高级引擎抛错/超时 → 自动 BLF 且标记 `engine=blf_fallback`
- [x] 接口测试覆盖切换/回退

**监督 Gate：**

- [ ] 报告里能看到引擎名
- [ ] 批准“RC 以 BLF 为准，NFP 不挡 RC”

---

### Day 7 — Operation 模型 + 刀具绑定

**成果：** 不存在“一把活动刀静默覆盖全部工序”。

**实现切片：**

1. 正式 `Operation`（Type/Side/ToolId/Depth/Stepdown/Feeds/SequenceGroup/Enabled）
2. 从 Feature 生成 Operation
3. Tool Library preset：T1 6.35 Router、T2 10 Router、T3 3 Drill（可编辑）
4. 缺 ToolId → Preflight **error**
5. 同刀成组排序准备
6. NC 预留换刀或分文件钩子（实现可在 Day 10）

**自动 Gate：**

- [ ] 每个生成的 Operation 都有 ToolId（或显式默认映射表）
- [ ] 去掉某类刀具 → 导出失败并指出缺失
- [ ] 单测覆盖绑定与缺刀

**监督 Gate：**

- [ ] 工序表能看见每行刀具
- [ ] 确认刀号与真实刀库是否一致（可事后改号）

**待拍板：**

- 真实刀号/刀长/刀库位
- 哪些孔必须钻刀、哪些可铣

---

### Day 8 — 安全工序顺序 + 按板厚切深

**成果：** 外轮廓最后；切深跟板厚；混厚正确。

**实现切片：**

1. 顺序固定：

```text
Drill → Pocket → Groove → Inner Contour → Outer Profile
```

2. Outer depth = `ThicknessMm + ThroughAllowance`（可配置）
3. Groove ≤ thickness；Blind/Through drill 语义
4. 删除/停止 Desktop 对 contour depth 的全局覆盖
5. Spoilboard 最大切入检查（Preflight）
6. 15/16/18 mm 混厚 golden 测试

**自动 Gate：**

- [ ] 顺序单测：Outer 索引大于 Drill/Groove/Pocket
- [ ] 18 mm 板 outer Z 使用 18+allowance，不被 全局 18 误伤 15 mm 板
- [ ] 非法深度 → 导出失败

**监督 Gate：**

- [ ] 抽一份 15/18 混厚 NC：注释或结构上 outer 在后，深度合理

**待拍板：**

- ThroughAllowance 默认（ASSUMED 0.5 mm）
- Spoilboard 允许切入（ASSUMED 1.0 mm）

---

### Day 9 — Pocket Area Clear v1 + 小板策略

**成果：** Pocket 是清角，不是沿边一圈；小板有策略提示。

**实现切片：**

1. Clipper2 offset-inward clearing + stepover + stepdown + finish allowance
2. 默认 Onion Skin（Tabs 可选）
3. CAM playhead 支持 Pocket 帧
4. 小板阈值 → Preflight warn/error 策略
5. 矩形 + 简单异形 Pocket 测试

**自动 Gate：**

- [ ] Pocket 刀路点数显著多于“单圈轮廓”（阈值断言）
- [ ] stepover 参数影响路径
- [ ] 小板触发警告用例通过

**监督 Gate：**

- [ ] 看 playhead：Pocket 在腔内往复/偏移填充
- [ ] Onion Skin 默认值可接受

**待拍板：**

- Onion Skin 默认留皮宽度
- 小板人工覆盖方式

---

### Day 10 — Post 接口 + 多刀具 + 每 Sheet 独立输出

**成果：** 每张板一套可追踪产物；后置可插拔。

**实现切片：**

1. `IPostProcessor`：Generic mm、Fanuc-like、目标机骨架
2. 模式：自动换刀 **或** 按刀具分文件（配置项）
3. 每 Sheet：`S{n}.nc`、`S{n}.dxf`、manifest、工单片段
4. Preflight 扩展：Tool、Depth、Bounds、负坐标、碰撞、顺序、空输出
5. Bundle 目录结构文档化

**自动 Gate：**

- [ ] 3 sheet 样本导出 ≥3 个 NC 文件（或显式单文件模式测试双轨）
- [ ] DXF 不再仅 S1
- [ ] Preflight 失败挡导出单测

**监督 Gate：**

- [ ] 打开导出包：Sheet/刀具可追踪
- [ ] 选定换刀模式（自动 vs 分文件）

**待拍板：**

- 自动换刀 vs 分刀具文件（RC 默认 ASSUMED：分刀具文件更稳）
- 原点角落、M2 vs M30

---

### Day 11 — A/B 面与反面程序

**成果：** 双面不是数学镜像玩具；无定位则禁导出。

**实现切片：**

1. Operation/Workpiece `Side = A|B`
2. FlipAxis、Reference Origin、Registration Holes 模型
3. 输出 `S{n}_A.nc` / `S{n}_B.nc`
4. B 面工单写明翻转、原点、定位
5. 无 registration 策略 → 禁止 B 导出
6. 坐标变换单测

**自动 Gate：**

- [ ] A/B 变换测试通过
- [ ] 无定位策略时 B 导出失败
- [ ] 单面工件不受影响

**监督 Gate：**

- [ ] 至少一块双面板：人工看 B 面原点说明是否可执行

**待拍板：**

- 翻板轴 X/Y
- 定位销/孔/挡块

---

### Day 12 — 标签、BOM、跨项目导入工件

**成果：** 生产身份贯通到标签与 BOM。

**实现切片：**

1. 标签层级 Project → Module → Workpiece
2. 标签字段：ID、材料、厚度、Sheet、Side、封边、Module
3. BOM CSV（板件/材料/数量/Sheet/Tool）
4. 从其他 Project/Module 导入 Workpiece
5. DXF/SVG **基础**外轮廓导入（弧线 tessellation）；完整 CAD 不挡 RC
6. 标签与 manifest 同 ID 断言测试

**自动 Gate：**

- [ ] 导出包含 labels + bom.csv
- [ ] 标签 ID ⊆ manifest WorkpieceId
- [ ] 基础 DXF 外轮廓导入测试（至少矩形）

**监督 Gate：**

- [ ] 打印或预览一张标签字段齐全
- [ ] BOM 行数与板件数对得上

**待拍板：**

- 标签尺寸/打印机（无设备则验收“预览 HTML/PDF”）
- 是否要条码（默认否）

---

### Day 13 — 全链路 + Worker 收敛

**成果：** Desktop 不再是算法权威；长短路径结果一致。

**实现切片：**

1. 全链路脚本/清单：sample → import → edit → nest → CAM → post → bundle
2. 压力：120 板、多材料、多厚、多 Sheet、多刀、Pocket、A/B
3. Nest/Ops/Post **默认经 ComputeWorker**
4. 一致性测试：同输入 Desktop-via-Worker vs 纯 Domain fixture 一致（允许浮点公差）
5. 取消/超时/错误恢复

**自动 Gate：**

- [ ] Worker 路径全绿
- [ ] 一致性测试通过
- [ ] 120 板在约定时限内完成（写入报告；超时则优化或记录硬件基线）

**监督 Gate：**

- [ ] Troy 完整点点点一遍，只记“不自然操作”列表
- [ ] 决定是否进入 Day 14 RC 冻结

---

### Day 14 — RC 冻结（禁止新功能）

**成果：** 可交给机床 dry-run 的 RC 包。

**实现切片：**

1. 全量测试 + build + UIA + golden diff
2. `docs/sprint/KNOWN_LIMITATIONS.md`
3. `docs/sprint/MACHINE_DRYRUN_CHECKLIST.md`
4. `docs/sprint/RC_REPORT.md`（对照 P0/P1）
5. 打 tag：`rc-14d-YYYYMMDD`
6. 不做新功能；只修 Gate 回归

**自动 Gate：**

```text
No missing tool
No invalid depth
No negative XY
No collision
No outer profile before inner ops
Per-sheet output complete
A/B requires registration
Preflight blocks export
All automated tests pass
Golden diff empty or justified
```

**监督 Gate：**

- [ ] 批准或拒绝 RC
- [ ] 确认 dry-run 日期/机台

---

## 5. 与原交接稿的差异（有意为之）

| 原计划 | 本修订 |
|--------|--------|
| 强调 28 人工小时紧张 | **忽略工时焦虑**；以 Gate 为节拍 |
| Day 6 强推真 NFP | 改为 **接口 + 回退**；NFP 不挡 RC |
| Day 2 大改层级 UI | 先 **合同与字段**；UI 树可渐进 |
| Worker 与安全改动并行风险 | Worker **集中 Day 13**，前序先做正确性 |
| 每日 Troy 2h 深测 | 改为 **10–20 分钟监督 Gate** |
| 成功=功能清单打勾 | 成功= **RC 上机检查表可执行** |

原稿 P0 安全项 **全部保留**；P1 标签/BOM/双面/Pocket **保留进 14 天**；P2 真 NFP/学习/MSI **明确不挡 RC**。

---

## 6. 默认假设（Troy 可随时推翻）

未拍板前 Cursor 使用：

| 项 | 默认 |
|----|------|
| Post | Fanuc-like |
| 换刀 | 按刀具分文件 |
| T1/T2/T3 | 6.35 / 10 / 3 mm |
| ThroughAllowance | 0.5 mm |
| Spoilboard | 1.0 mm |
| 小板阈值 | 最短边 < 80 mm |
| Nest RC 引擎 | BLF |
| 标签 | HTML 预览（无打印机不挡） |
| Schema 变更 | 需 Day 报告标明；破坏性变更需 Troy 回复批准 |

---

## 7. 启动指令（给 Cursor）

收到 Troy「开始 Day 1」后：

1. 切换/创建 `sprint/14d-rc`
2. 严格按 Day 1 Gate 执行
3. 收工提交 `docs/sprint/day-01-report.md` + 功能向 commit
4. 等待 Troy：`Day 1 验收通过` 再进 Day 2

Loop engineering 可在单日内多次迭代直到 Gate 全绿，**不要跨日欠债**。
