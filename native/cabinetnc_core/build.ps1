# Build cabinetnc_offset (Windows). Prefer PATH cmake/clang++; else d:\project\tools\llvm-mingw.
$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$cmake = "cmake"
if (-not (Get-Command cmake -ErrorAction SilentlyContinue)) {
  $cmake = "C:\Program Files\CMake\bin\cmake.exe"
}
$mingw = "d:\project\tools\llvm-mingw\bin"
if (Test-Path "$mingw\clang++.exe") {
  $env:PATH = "$mingw;" + $env:PATH
}
& $cmake -S $root -B "$root\build" -G "MinGW Makefiles" -DCMAKE_CXX_COMPILER=clang++
& $cmake --build "$root\build"
Write-Host "OK $($root)\build\cabinetnc_offset.exe"
