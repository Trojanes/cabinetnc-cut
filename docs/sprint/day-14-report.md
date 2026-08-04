# Day 14 report

## Goals

RC freeze docs + regression evidence; no new product features beyond leftover Domain import helper needed for Day 12 gate closure.

## Done

- `KNOWN_LIMITATIONS.md`, `MACHINE_DRYRUN_CHECKLIST.md`, `RC_REPORT.md`
- `WorkpieceImporter` + test (Day 12 leftover)
- End-to-end pipeline test + 120-panel stress timing test
- Full suite green; Desktop Release build OK
- Tag: `rc-14d-20260805` (conditional RC — see limitations)

## Auto gates (RC checklist)

- [x] Automated tests pass (Domain 46 + Package 16 + Infra 2)
- [x] Preflight / nest / tool / order / per-sheet / registration-block covered by tests
- [x] Golden: baseline docs remain under `docs/sprint/golden/` (no unjustified wipe)
- [ ] UIA smoke — still manual / agent-shell hang (listed in limitations)

## Conditional RC

Single-face jobs OK for dry-run checklist. Dual-face production NC **not** RC-complete.

## ASSUMPTION

- Desktop continues Domain-local nest (same engine class as Worker); not every UI nest is gRPC.
