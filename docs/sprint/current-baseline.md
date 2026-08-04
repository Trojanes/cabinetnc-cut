# Sprint current baseline — Day 1

- Branch: `sprint/14d-rc`
- Base commit: `7b53081` (initial import)
- Captured: 2026-08-05 (local)
- Plan: [14-DAY-ACCEPTANCE-PLAN.md](./14-DAY-ACCEPTANCE-PLAN.md)

## Honest status

This is a **runnable, demo-able cutting-station MVP**, not a production-grade multi-CNC shop system.

| Gauge | Estimate |
|-------|----------|
| MakerHub commercial depth | ~40% |
| Vite → Desktop migration | ~85% |
| Foundation gate | ~85% |

Internal “99/100 READY” evaluation scores are **MVP checklist scores**, not product completion.

## Automated baseline

| Check | Result |
|-------|--------|
| `dotnet test -c Release` | PASS (Domain 11 + Package 12 + Infra 2) |
| Desktop Release build | PASS (after killing leftover Worker/Desktop locks) |
| Desktop Debug build | PASS |
| UIA smoke | see `dotnet/artifacts/smoke-day01.json` / day-01-report |
| Golden outputs | `docs/sprint/golden/` via `dotnet/tools/GoldenExport` |

## ASSUMED defaults (Troy may override)

- Post dialect: Fanuc-like available; golden used `nesting_router_6`
- T1/T2/T3: 6.35 / 10 / 3 mm (library presets; not yet per-op binding)
- ThroughAllowance: 0.5 mm (not implemented yet — Day 8)
- Nest RC engine: BLF

## Known risks entering Day 2+

1. Ops order Contour → Drill → Groove (unsafe for vacuum hold-down)
2. Global active tool overlays MachineProfile (no per-op ToolId)
3. ContourDepthMm global overwrite vs panel thickness
4. Desktop calls Domain nest/ops/nc directly (Worker warm-only)
5. Multi-sheet NC is one file with comments; DXF export is S1-only
6. No Domain A/B Side model

See [desktop-domain-calls.md](./desktop-domain-calls.md).
