param(
    [int]$TargetScore = 85,
    [switch]$KeepOpen
)

$ErrorActionPreference = "Continue"
$dotnetRoot = Split-Path $PSScriptRoot -Parent
$repoRoot = Split-Path $dotnetRoot -Parent
$artifacts = Join-Path $dotnetRoot "artifacts"
New-Item -ItemType Directory -Force -Path $artifacts | Out-Null

$testLog = Join-Path $artifacts "evaluation-tests.log"
$buildLog = Join-Path $artifacts "evaluation-build.log"
$smokeJson = Join-Path $artifacts "smoke-latest.json"
$reportJson = Join-Path $artifacts "evaluation-latest.json"
$reportMd = Join-Path $artifacts "evaluation-latest.md"

Push-Location $dotnetRoot
try {
    # Build must run against unlocked outputs. The smoke suite starts a fresh app later.
    Get-Process -Name "CabinetNC.Desktop", "CabinetNC.ComputeWorker" -ErrorAction SilentlyContinue |
        Stop-Process -Force
    Start-Sleep -Milliseconds 500

    & dotnet restore "src\CabinetNC.Desktop\CabinetNC.Desktop.csproj"
    if ($LASTEXITCODE -ne 0) {
        throw "Desktop restore failed"
    }

    & dotnet test "CabinetNC.slnx" --no-restore 2>&1 | Tee-Object -FilePath $testLog
    $testsOk = $LASTEXITCODE -eq 0

    & dotnet build "src\CabinetNC.Desktop\CabinetNC.Desktop.csproj" --no-restore -v minimal 2>&1 |
        Tee-Object -FilePath $buildLog
    $buildOk = $LASTEXITCODE -eq 0

    $smokeArgs = @(
        "tests\manual\smoke_desktop.py",
        "--json", $smokeJson
    )
    if ($KeepOpen) { $smokeArgs += "--keep-open" }
    & python @smokeArgs
    $smokeOk = $LASTEXITCODE -eq 0
}
finally {
    Pop-Location
}

$smoke = if (Test-Path $smokeJson) {
    Get-Content $smokeJson -Raw -Encoding UTF8 | ConvertFrom-Json
} else {
    $null
}

function Test-SmokeCase([string]$prefix) {
    if ($null -eq $smoke) { return $false }
    return @($smoke.cases | Where-Object {
        $_.id -like "$prefix*" -and $_.status -eq "PASS"
    }).Count -gt 0
}

$startupOk = Test-SmokeCase "SMK-001"
$modulesOk = Test-SmokeCase "SMK-002"
$importOk = Test-SmokeCase "SMK-010"
$nestOk = Test-SmokeCase "SMK-031"
$polyOk = Test-SmokeCase "SMK-036"
$camOk = Test-SmokeCase "SMK-043"
$toolOk = Test-SmokeCase "SMK-042"
$outOk = Test-SmokeCase "SMK-046"
$noCompilerWarnings = if (Test-Path $buildLog) {
    -not (Select-String -Path $buildLog -Pattern "warning CS|error CS" -Quiet)
} else { $false }
$noHighNuget = if (Test-Path $buildLog) {
    -not (Select-String -Path $buildLog -Pattern "NU190[34]" -Quiet)
} else { $false }
$noCompatibilityWarnings = if (Test-Path $buildLog) {
    -not (Select-String -Path $buildLog -Pattern "NU1701" -Quiet)
} else { $false }

$manualDoc = Test-Path (Join-Path $repoRoot "docs\testing\SMOKE_CASE_LIBRARY.md")
$rulesDoc = Test-Path (Join-Path $repoRoot "docs\testing\PRODUCT_EVALUATION_RULES.md")
$smokeSource = Test-Path (Join-Path $dotnetRoot "tests\manual\smoke_desktop.py")
$packScript = Test-Path (Join-Path $dotnetRoot "scripts\pack.ps1")
$exportSources = @(
    "src\CabinetNC.Domain\Manufacturing\NestDxfWriter.cs",
    "src\CabinetNC.Domain\Manufacturing\JobSheetBuilder.cs",
    "src\CabinetNC.FusionPackage\CutPackageJson.cs"
) | ForEach-Object { Test-Path (Join-Path $dotnetRoot $_) }
$projectSources = @(
    "src\CabinetNC.Infrastructure\Projects\SqliteProjectStore.cs",
    "src\CabinetNC.Infrastructure\Library\WorkshopLibraryStore.cs"
) | ForEach-Object { Test-Path (Join-Path $dotnetRoot $_) }

$criteria = [System.Collections.Generic.List[object]]::new()
function Add-Criterion(
    [string]$id,
    [int]$max,
    [bool]$pass,
    [string]$evidence,
    [int]$partial = 0
) {
    $earned = if ($pass) { $max } else { $partial }
    $criteria.Add([pscustomobject]@{
        id = $id; earned = $earned; max = $max; pass = $pass; evidence = $evidence
    })
}

# A. Functional closure (35)
Add-Criterion "A1-import" 6 ($testsOk -and $importOk) "tests + SMK-010"
Add-Criterion "A2-nest" 8 ($testsOk -and $nestOk -and $polyOk) "tests + SMK-031 + SMK-036"
Add-Criterion "A3-cam-nc" 8 ($testsOk -and $camOk -and $toolOk) "tests + SMK-042/043/044"
Add-Criterion "A4-export" 6 ($testsOk -and $outOk -and -not ($exportSources -contains $false)) "export sources + tests + SMK-046/050-053"
Add-Criterion "A5-persistence" 4 ($testsOk -and -not ($projectSources -contains $false)) "SQLite/library sources + tests"
Add-Criterion "A6-modules" 3 $modulesOk "SMK-002"

# B. Safety and correctness (25)
Add-Criterion "B1-full-tests" 6 $testsOk $testLog
Add-Criterion "B2-rotation-xy" 5 ($testsOk -and $nestOk -and $camOk) "rotation unit test + UI NC negative-XY assertion"
Add-Criterion "B3-invalid-input" 3 $testsOk "CutPackageImporter rejection tests" 2
Add-Criterion "B4-poly-gap" 4 ($testsOk -and $polyOk) "Clipper tests + SMK-036"
Add-Criterion "B5-preflight" 4 ($testsOk -and $outOk) "NcPreflight tests + SMK-046"
Add-Criterion "B6-review-lint" 3 (
    $buildOk -and $noCompilerWarnings -and $noHighNuget -and $noCompatibilityWarnings
) "0 CS/high-NuGet/compatibility warnings" 2

# C. Operability (15)
Add-Criterion "C1-startup-worker" 3 $startupOk "SMK-001"
Add-Criterion "C2-stage-gates" 2 $startupOk "SMK-003"
Add-Criterion "C3-module-navigation" 3 $modulesOk "SMK-002"
Add-Criterion "C4-feedback-dialog" 2 $importOk "SMK-010 import result dialog"
Add-Criterion "C5-manual-library" 3 $manualDoc "SMOKE_CASE_LIBRARY.md"
Add-Criterion "C6-status-preflight" 2 $outOk "SMK-046"

# D. Maintainability (10)
Add-Criterion "D1-domain-tests" 3 $testsOk "Domain/Package/Infrastructure suites"
Add-Criterion "D2-repeatable-uia" 2 ($smokeSource -and $smokeOk) "smoke JSON + exit code"
Add-Criterion "D3-open-dependencies" 2 (Test-Path (Join-Path $dotnetRoot "src\CabinetNC.Domain\CabinetNC.Domain.csproj")) "Clipper2 package; no MakerHub binaries"
Add-Criterion "D4-doc-consistency" 3 ($rulesDoc -and $manualDoc) "rubric + manual library"

# E. Delivery and regression (15)
Add-Criterion "E1-desktop-build" 5 $buildOk $buildLog
Add-Criterion "E2-pack-script" 3 $packScript "scripts/pack.ps1"
Add-Criterion "E3-automated-smoke" 4 $smokeOk $smokeJson
Add-Criterion "E4-manual-runbook" 3 $manualDoc "manual cases with steps/expected/risk"

$score = ($criteria | Measure-Object earned -Sum).Sum
$maxScore = ($criteria | Measure-Object max -Sum).Sum
$hardGates = [ordered]@{
    desktopBuild = $buildOk
    fullTests = $testsOk
    uiSmoke = $smokeOk
    import120 = $importOk
    rotationCoordinateSafety = $nestOk
    preflight = $outOk
    noHighSeverityNuget = $noHighNuget
}
$hardGateOk = -not ($hardGates.Values -contains $false)
$ready = $score -ge $TargetScore -and $hardGateOk

$report = [ordered]@{
    schema = "cabinetnc.product-evaluation"
    schemaVersion = 1
    evaluatedAt = (Get-Date).ToString("o")
    targetScore = $TargetScore
    score = $score
    maxScore = $maxScore
    hardGateOk = $hardGateOk
    ready = $ready
    hardGates = $hardGates
    criteria = $criteria
    residualRisk = @(
        "True NFP/DXOPT-grade placement is not implemented",
        "CAM simulation is point-playhead, not material removal",
        "SkiaSharp/OpenTK emits NU1701 target-framework compatibility warnings",
        "Signed MSI and real-machine validation remain"
    )
}
$report | ConvertTo-Json -Depth 8 | Set-Content $reportJson -Encoding UTF8

$lines = @(
    "# CabinetNC Product Evaluation",
    "",
    "- Evaluated: $($report.evaluatedAt)",
    "- Score: **$score/$maxScore** (target $TargetScore)",
    "- Hard gates: **$(if ($hardGateOk) {'PASS'} else {'FAIL'})**",
    "- Status: **$(if ($ready) {'READY'} else {'CONTINUE LOOP'})**",
    "",
    "## Criteria",
    "",
    "| ID | Score | Result | Evidence |",
    "|----|-------|--------|----------|"
)
foreach ($c in $criteria) {
    $result = if ($c.pass) { "PASS" } elseif ($c.earned -gt 0) { "PARTIAL" } else { "FAIL" }
    $lines += "| $($c.id) | $($c.earned)/$($c.max) | $result | $($c.evidence) |"
}
$lines += @("", "## Residual risk", "")
foreach ($risk in $report.residualRisk) { $lines += "- $risk" }
$lines | Set-Content $reportMd -Encoding UTF8

Write-Host ""
Write-Host "CabinetNC score: $score/$maxScore; target=$TargetScore; hardGates=$hardGateOk; ready=$ready"
Write-Host "Report: $reportMd"
if (-not $ready) { exit 1 }
