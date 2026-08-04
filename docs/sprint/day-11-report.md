# Day 11 report (PARTIAL)

## Goals

A/B face + back-side export only with registration; no fake dual-side CAM.

## Done (auto-gated)

- `FaceRegistration` + `DoubleSideGate`
- B-side ops without strategy → `no_registration` preflight **error**
- Single-side unaffected
- Local mirror helper for FlipAxis X/Y (math only)

## NOT done (blocked — need Troy)

- Production `S{n}_A.nc` / `S{n}_B.nc` WCS after physical flip
- Job sheet flip/origin wording tied to real fixture
- Claiming “可生产双面加工”

## Auto gates

- [x] 无定位策略时 B 导出失败
- [x] 单面工件不受影响
- [ ] A/B 变换用于真实翻板 WCS — **blocked**

## Question for Troy

1. Flip axis after face A: **X or Y** (panel local)?
2. Origin after flip: stay SW of sheet, or re-zero on fixture pins?
3. Default registration strategy when woodjob has no data: keep **block B** (current), or allow `manual_mark`?

## Next while blocked

Day 12 Label/BOM can proceed (WorkpieceId already on panels); full dual-side NC waits for answers above.
