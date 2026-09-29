# Deploy CabinetNC.CloudApi (omni-api) to Google Cloud Run. No Docker needed:
# the .NET SDK builds and pushes the container image itself.
#
# Prerequisites (once per PC): .NET 10 SDK, Google Cloud CLI, `gcloud auth login`.
#
#   powershell -ExecutionPolicy Bypass -File dotnet/scripts/deploy-cloud-api.ps1 -ProjectId omni-cloud-prod -WriteLocalConfig
#
# Region is a parameter so a later move (e.g. Singapore asia-southeast1) is a
# re-run against the new project/region, then an edit of cloud.json.
param(
  [Parameter(Mandatory = $true)][string]$ProjectId,
  [string]$Region = "australia-southeast1",
  [ValidateSet("prod", "dev")][string]$Environment = "prod",
  [string]$Service = "omni-api",
  [string]$Repository = "omni",
  [switch]$WriteLocalConfig
)
$ErrorActionPreference = "Stop"
$env:Path = "C:\Program Files\dotnet;" + $env:Path
$root = Resolve-Path (Join-Path $PSScriptRoot "..")
Set-Location $root

function Invoke-Native([string]$What, [scriptblock]$Command) {
  Write-Host "==> $What"
  & $Command
  if ($LASTEXITCODE -ne 0) { throw "$What failed (exit $LASTEXITCODE)" }
}

foreach ($tool in "gcloud", "dotnet") {
  if (-not (Get-Command $tool -ErrorAction SilentlyContinue)) { throw "$tool not found on PATH" }
}

Invoke-Native "Enable Cloud Run + Artifact Registry" {
  gcloud services enable run.googleapis.com artifactregistry.googleapis.com --project $ProjectId
}

$prev = $ErrorActionPreference
$ErrorActionPreference = "Continue"
gcloud artifacts repositories describe $Repository --location $Region --project $ProjectId *> $null
$repoExists = ($LASTEXITCODE -eq 0)
$ErrorActionPreference = $prev
if (-not $repoExists) {
  Invoke-Native "Create image repository $Repository in $Region" {
    gcloud artifacts repositories create $Repository --repository-format docker --location $Region --project $ProjectId
  }
}

$registry = "$Region-docker.pkg.dev"
Invoke-Native "Let the .NET SDK push to $registry" {
  gcloud auth configure-docker $registry --quiet
}

$tag = (Get-Date).ToUniversalTime().ToString("yyyyMMdd-HHmmss")
$imageRepo = "$ProjectId/$Repository/$Service"
Invoke-Native "Build and push image $registry/${imageRepo}:$tag" {
  dotnet publish src/CabinetNC.CloudApi/CabinetNC.CloudApi.csproj -c Release -t:PublishContainer `
    -p:ContainerRegistry=$registry `
    -p:ContainerRepository=$imageRepo `
    -p:ContainerImageTags=$tag
}

# Public ingress: /v1/health and /v1/version are open; job routes will check device tokens in-app.
Invoke-Native "Deploy $Service to Cloud Run ($Region)" {
  gcloud run deploy $Service `
    --image "$registry/${imageRepo}:$tag" `
    --region $Region `
    --project $ProjectId `
    --allow-unauthenticated `
    --memory 512Mi `
    --max-instances 3 `
    --set-env-vars "OMNI_REGION=$Region,OMNI_ENV=$Environment"
}

$url = (gcloud run services describe $Service --region $Region --project $ProjectId --format "value(status.url)").Trim()
if (-not $url) { throw "Could not read the Cloud Run service URL" }
$health = Invoke-RestMethod "$url/v1/health"
Write-Host ""
Write-Host "OK $($health.service) $($health.serverVersion) region=$($health.region) env=$($health.environment)"
Write-Host "cloudBaseUrl = $url"

if ($WriteLocalConfig) {
  $configPath = Join-Path $env:ProgramData "Omni\cloud.json"
  $config = [ordered]@{ cloudBaseUrl = $url; shopId = "main" }
  if (Test-Path $configPath) {
    $old = Get-Content $configPath -Raw | ConvertFrom-Json
    if ($old.shopId) { $config.shopId = $old.shopId }
  }
  New-Item -ItemType Directory -Force -Path (Split-Path $configPath) | Out-Null
  $config | ConvertTo-Json | Set-Content -Path $configPath -Encoding UTF8
  Write-Host "Wrote $configPath"
}
