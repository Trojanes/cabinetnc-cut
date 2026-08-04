# Day 06 report

## Goals

`INestingEngine` + BLF adapter + advanced stub with automatic BLF fallback; decision doc; part-in-part placeholder.

## Done (evidence)

- `INestingEngine`, `BlfNestingEngine`, `AdvancedNestingEngineStub`, `NestEngineRouter`
- Fallback tags `Engine=blf_fallback` with `FallbackReason` in run log
- Desktop nest uses router (`preferred` → stub fails → BLF)
- `PartInPartSlot` placeholder (not enabled)
- `docs/sprint/nest-engine-decision.md` — RC = BLF, not NFP

## Auto gates

- [x] Default path compatible with Day 5 grouping (`Explicit_blf_stays_grouped_blf`)
- [x] Advanced fail/timeout → `blf_fallback`
- [x] Tests green: Domain **22** + Package 16 + Infra 2

## Not claimed

- No commercial NFP
- Advanced stub is failure/timeout prototype only

## Supervise (Troy)

- [ ] Nest report shows engine name / fallback warning
- [ ] Approve: RC uses BLF; NFP does not block RC

## Next

Day 7: Operation model + per-op ToolId binding (no silent global active tool).
