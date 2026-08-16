# Pack CabinetNC Cut — runnable Desktop + source archive
$ErrorActionPreference = "Stop"
$env:Path = "C:\Program Files\dotnet;" + $env:Path

$root = Resolve-Path (Join-Path $PSScriptRoot "..\..")
$stamp = Get-Date -Format "yyyyMMdd-HHmm"
$dist = Join-Path $root "dist"
$pub = Join-Path $dist "CabinetNC-Cut"
$zipApp = Join-Path $dist "CabinetNC-Cut-$stamp.zip"
$zipSrc = Join-Path $dist "CabinetNC-Cut-src-$stamp.zip"

Write-Host "==> clean dist/publish dir"
if (Test-Path $pub) { Remove-Item $pub -Recurse -Force }
New-Item -ItemType Directory -Path $pub -Force | Out-Null
New-Item -ItemType Directory -Path $dist -Force | Out-Null

Write-Host "==> publish ComputeWorker (Release)"
dotnet publish (Join-Path $root "dotnet\src\CabinetNC.ComputeWorker") `
  -c Release -r win-x64 --self-contained true `
  -o (Join-Path $pub "_worker_tmp") `
  /p:PublishSingleFile=false

Write-Host "==> publish Desktop (Release, win-x64, self-contained)"
dotnet publish (Join-Path $root "dotnet\src\CabinetNC.Desktop") `
  -c Release -r win-x64 --self-contained true `
  -o $pub `
  /p:PublishSingleFile=false `
  /p:IncludeNativeLibrariesForSelfExtract=true

# Ensure worker binaries sit next to Desktop
Write-Host "==> merge worker next to Desktop"
Copy-Item (Join-Path $pub "_worker_tmp\*") $pub -Recurse -Force
Remove-Item (Join-Path $pub "_worker_tmp") -Recurse -Force

# Demo package for first-run
$samplesSrc = Join-Path $root "public\samples"
$samplesDst = Join-Path $pub "public\samples"
if (Test-Path $samplesSrc) {
  New-Item -ItemType Directory -Path $samplesDst -Force | Out-Null
  Copy-Item (Join-Path $samplesSrc "*") $samplesDst -Recurse -Force
}

# Launcher used by the desktop shortcut
@"
@echo off
setlocal
set "APP_DIR=%~dp0"
set "DOTNET_ROOT=C:\Program Files\dotnet"
set "PATH=%DOTNET_ROOT%;%PATH%"
cd /d "%APP_DIR%"
start "" "%APP_DIR%CabinetNC.Desktop.exe"
"@ | Set-Content (Join-Path $pub "Start-CabinetNC-Cut.cmd") -Encoding ASCII

# Short readme
@"
OmniCam
Built: $stamp

Run:
  Start-CabinetNC-Cut.cmd
  or CabinetNC.Desktop.exe

This folder is the only runnable copy. The desktop shortcut opens it.
Worker: CabinetNC.ComputeWorker.exe (spawned automatically).
"@ | Set-Content (Join-Path $pub "README.txt") -Encoding UTF8

Write-Host "==> zip runnable app → $zipApp"
if (Test-Path $zipApp) { Remove-Item $zipApp -Force }
Compress-Archive -Path (Join-Path $pub "*") -DestinationPath $zipApp -CompressionLevel Optimal

Write-Host "==> zip source (no bin/obj/node_modules) → $zipSrc"
$srcStage = Join-Path $dist "_src_stage"
if (Test-Path $srcStage) { Remove-Item $srcStage -Recurse -Force }
New-Item -ItemType Directory -Path $srcStage | Out-Null
$excludeDirs = @('node_modules', 'bin', 'obj', 'dist', '.git', '.vs', 'coverage', 'playwright-report')
Get-ChildItem $root -Force | ForEach-Object {
  if ($excludeDirs -contains $_.Name) { return }
  Copy-Item $_.FullName (Join-Path $srcStage $_.Name) -Recurse -Force
}
# strip nested bin/obj
Get-ChildItem $srcStage -Recurse -Directory -Filter bin | Remove-Item -Recurse -Force -ErrorAction SilentlyContinue
Get-ChildItem $srcStage -Recurse -Directory -Filter obj | Remove-Item -Recurse -Force -ErrorAction SilentlyContinue
Get-ChildItem $srcStage -Recurse -Directory -Filter node_modules | Remove-Item -Recurse -Force -ErrorAction SilentlyContinue
if (Test-Path $zipSrc) { Remove-Item $zipSrc -Force }
Compress-Archive -Path (Join-Path $srcStage "*") -DestinationPath $zipSrc -CompressionLevel Optimal
Remove-Item $srcStage -Recurse -Force

Write-Host ""
Write-Host "DONE"
Write-Host "  App folder : $pub"
Write-Host "  App zip    : $zipApp  ($([math]::Round((Get-Item $zipApp).Length/1MB,1)) MB)"
Write-Host "  Source zip : $zipSrc  ($([math]::Round((Get-Item $zipSrc).Length/1MB,1)) MB)"
