# CabinetNC Cut — Dual-window ownership

Use with [ARCHITECTURE.md](./ARCHITECTURE.md).  
Goal: two Cursor windows can develop in parallel without stepping on each other.

## Lanes

| Lane | Window role | Owns | Must not touch |
|------|-------------|------|----------------|
| **A · Geom** | Geometry / modeling | `src/geom/**`, `native/cabinetnc_core/**`, `scripts/check-geom.mjs`, `scripts/check-hit.mjs`, `scripts/check-native.mjs` | `src/pack.js`, `src/ops.js`, `src/nc.js`, `src/render.js`, Fusion plugin |
| **B · Nest/CAM** | Nest + ops + NC | `src/pack.js`, `src/ops.js`, `src/nc.js`, `src/package.js`, `src/render.js`, `scripts/check-pack.mjs`, `scripts/check-ops.mjs`, `scripts/check-nc.mjs`, `scripts/check-sample.mjs`, `scripts/smoke-pipeline.mjs`, `public/samples/**` | `src/geom/**` |

## Shared contract (both read; change needs both ACK)

- Schema: `cabinetnc.cut-package` v1  
  - `panels[].outline.points`, `panels[].features[]`, `bbox`, `sheets`, `nestSettings`
- Optional: `nestResult.placements` (written by lane B; cleared when lane A writes geom back)
- Downstream: `cabinetnc.cut-ops` (lane B)

**Rule:** Lane A may add optional fields (e.g. future `edges`) only after both windows agree; lane B must ignore unknown fields safely.

## Hot files (edit only with ACK)

Do not edit in parallel. One window proposes; user (or the other window) ACKs; then one window patches.

| File | Why hot |
|------|---------|
| `src/main.js` | Wires Geom + Nest views, drag, import/export |
| `index.html` | Buttons / view toggle / layout chrome |
| `src/styles.css` | Shared layout |
| `package.json` | `check` / `smoke` script list |

**Preferred pattern:**  
- Geom UI hooks → thin calls into `src/geom/*` (A owns logic; A may send a minimal `main.js` diff for wiring).  
- Nest UI hooks → thin calls into `pack` / `render` (B owns logic; same for wiring).

## Upstream Fusion (default frozen)

| Path | Owner |
|------|--------|
| `../troysfirstfusionproject-main/fusion360-unified-cabinet-plugin/nesting/cut_package.py` | Schedule explicitly (usually B or “upstream” task) |
| `../troysfirstfusionproject-main/fusion360-unified-cabinet-plugin/palette.html` Nesting buttons | Same |
| Other plugin modules | Out of dual-window cut-station scope |

## Paste-ready prompts

### Window A

```text
你是窗口 A · Geom。
仓库根：d:\\project\\cabinetnc-cut（独立项目，勿改 troysfirstfusionproject-main）。
只改：src/geom/** 、native/cabinetnc_core/** 、scripts/check-geom|hit|native.mjs。
禁止改：pack.js ops.js nc.js package.js render.js、Fusion 插件。
共享合同：cabinetnc.cut-package v1；改 schema 先停手说明字段。
若必须改 main.js / index.html / package.json：先列出「请求编辑」意图，等 ACK 再改。
完成时汇报：文件列表、跑了 check-geom/check-native、是否动合同。
当前优先：cabinetnc_core offset（Clipper 下一刀）或 Geom 编辑，不要倒计时 loop。
```

### Window B

```text
仓库根：d:\\project\\cabinetnc-cut（独立项目，勿改 troysfirstfusionproject-main）。
你是窗口 B · Nest/CAM。
只改：src/pack.js ops.js nc.js package.js render.js，
以及 scripts/check-pack|ops|nc|sample、smoke-pipeline、public/samples/**。
禁止改：src/geom/**。
共享合同：cabinetnc.cut-package v1；nestResult 由你写；A 写回几何会清 nestResult。
若必须改 main.js / index.html / package.json：先列出「请求编辑」意图，等 ACK 再改。
完成时汇报：文件列表、跑了哪些 check/smoke、是否动合同。
当前优先：nest 碰撞/间距或 shelf 升级，或 machine profile 接口，不要倒计时 loop。
```

## Sync checklist (user as dispatcher)

1. Both windows start with the prompt above.  
2. Schema change → pause both → you decide → one window implements.  
3. Hot-file change → one ACK’d patch only.  
4. End of session: A runs `node scripts/check-geom.mjs`; B runs pack/ops/nc checks; you run `npm run check` once.  
5. One Vite `npm run dev` is enough for both.

## Git tip

- Option 1: same branch, file-lane commits (`geom:` / `nest:` prefixes).  
- Option 2: `feat/geom-*` and `feat/nest-*` branches; you merge.
