# Import forever residual — CLOSED

| Slice | Status | Notes |
|-------|--------|-------|
| F1 DXF BLOCK + INSERT explode | ✅ | BLOCKS → INSERT explode (`317d393`) |
| F2 SVG transform + use | ✅ | matrix chain · `href`/`xlink:href` resolve |
| F3 close | ✅ | `npm run check` green incl. `check-import` |

## Shipped (this campaign)

- Session `machineOverrides` round-trip
- Drag-drop + multi-file merge
- Soft schema / field-level validation paths
- DXF LWPOLYLINE bulge, CIRCLE, BLOCK+INSERT
- SVG rect/polygon/path, transform lists, `<use>`
- Folder watch (`showDirectoryPicker` + poll)

## Honest residual (out of scope for this loop)

- DXF INSERT without BLOCK def · nested BLOCK depth >8
- SVG `clipPath` / nested viewBox quirks
- Continuous FS permission UX on Chromium folder watch

Do **not** re-arm `AGENT_LOOP_WAKE_import3` unless starting a new import campaign.
