# Global MakerHub parity loop

Repo: `d:\project\cabinetnc-cut`  
North star: `docs/VISION.md` M1–M6  

## Loop rules

- One slice per wake; min-diff; `npm run check` green  
- Sentinel: `AGENT_LOOP_WAKE_mhglobal`  
- Re-arm ~4m until campaign closed or blocked  

## Campaign 1–3 (closed)

C1 L0–L5 · C2 L6–L10 · C3 L11–L15 ✅  

## Campaign 4 (closed) — residual floor

| Slice | Status | Notes |
|-------|--------|-------|
| L16 point-level CAM sim (M4) | ✅ | expandSimFrames · toolhead marker |
| L17 Clipper nest verify (M3) | ✅ | poly + gap inflate · not full NFP |
| L18 portable installer deepen (M6) | ✅ | INSTALL.txt + zip · Tauri blocked (no Rust) |
| L19 job sheet print (M5) | ✅ | Job Sheet HTML export |
| L20 close + honesty | ✅ | see residual below |

## Hard residual (needs external deps / big design)

- True NFP nest (async Clipper packing search)
- Tauri native window (`cargo` / Rust not installed)
- Signed single-exe MSI/NSIS installer
- Full G-code backplot (arc/tool-change)

**Campaign 4 complete — do not re-arm unless user starts Campaign 5.**
