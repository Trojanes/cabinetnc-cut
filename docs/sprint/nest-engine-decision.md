# Nest engine decision (Day 6)

## Decision

**RC authority = grouped BLF (AABB free-rect).** Advanced / NFP is **not** required for RC.

## Why

- Existing Desktop/Worker path is BLF; Day 5 grouping + Clipper export gate already raise production safety.
- True NFP is Campaign 2+; claiming NFP from AABB is forbidden by sprint rules.

## Interface

- `INestingEngine` — `BlfNestingEngine`, `AdvancedNestingEngineStub`
- `NestEngineRouter` — preference `blf` | `advanced` | `preferred`
- On advanced throw/timeout → result `Engine = blf_fallback` with `FallbackReason` in run log

## Part-in-part

- `PartInPartSlot` data model only; **Enabled=false** by default; packer ignores it.

## ASSUMPTION

- Desktop default preference: `preferred` (try advanced → fall back). For predictable shop runs, UI can force `blf`.
