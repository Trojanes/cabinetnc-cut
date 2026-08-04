# cabinetnc_core (C++)

Narrow compute kernel slice: **polygon offset** via **Clipper2** (`third_party/Clipper2Lib`, Boost Software License).

## Build (Windows)

Needs CMake + a C++17 compiler (MSVC, MinGW, or LLVM-MinGW).

This machine's portable toolchain (optional): `d:\project\tools\llvm-mingw` + CMake in Program Files.

```powershell
cd native\cabinetnc_core
.\build.ps1
```

Binary: `build/cabinetnc_offset.exe`

## CLI contract

stdin JSON → stdout JSON.

```json
{"op":"offset","delta":5,"polygons":[[[0,0],[100,0],[100,50],[0,50]]]}
```

```json
{"ok":true,"engine":"cabinetnc_core","mode":"clipper_offset","polygons":[[[...]]]}
```

## JS facade

- Browser: `src/geom/native_offset.js` → JS `offsetRect` (no Node APIs)
- Node/tests: `src/geom/native_offset_node.js` → spawns CLI when built

Env: `CABINETNC_OFFSET_CLI`, `CABINETNC_MINGW_BIN`

## Next

Boolean ops / nest collision kernel behind the same C++ lib.
