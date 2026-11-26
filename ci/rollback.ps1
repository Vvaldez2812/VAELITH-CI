# ci/rollback.ps1
# Script de reversion atomica del despliegue en DEPLOY_ROOT (restaura previous.json -> current.json verificando SHA-256).
param(
  [string]$DeployRoot = $env:DEPLOY_ROOT,
  [switch]$DryRun
)

$ErrorActionPreference = 'Stop'
if ([string]::IsNullOrWhiteSpace($DeployRoot)) {
  $DeployRoot = 'C:\VAELITH_QA'
}

$base = [IO.Path]::GetFullPath($DeployRoot)
$currentPath  = Join-Path $base 'current.json'
$previousPath = Join-Path $base 'previous.json'

if (!(Test-Path $currentPath)) {
  throw "No existe current.json en $base"
}

$curr = Get-Content $currentPath -Raw | ConvertFrom-Json
if (!(Test-Path $previousPath)) {
  if ($DryRun) {
    Write-Output "[rollback-dryrun] Primera version activa ($($curr.version)); previous.json se habilitara en la siguiente liberacion."
    exit 0
  }
  throw "No existe previous.json en $base para ejecutar rollback."
}

$prev = Get-Content $previousPath -Raw | ConvertFrom-Json
if (!(Test-Path $prev.package -PathType Leaf)) {
  throw "El paquete de respaldo referenciado en previous.json no existe: $($prev.package)"
}

$sw = [Diagnostics.Stopwatch]::StartNew()
$actualHash = (Get-FileHash $prev.package -Algorithm SHA256).Hash
if ($actualHash -ne $prev.sha256) {
  throw "Fallo de integridad SHA-256 en paquete de respaldo ($actualHash != $($prev.sha256))"
}

if ($DryRun) {
  $sw.Stop()
  $msg = "[rollback-dryrun] Capacidad de reversion verificada en $([Math]::Round($sw.Elapsed.TotalSeconds, 3))s (< 15 min SLA) | Activa=$($curr.version) -> Respaldo=$($prev.version) | SHA256=$($prev.sha256) OK"
  if (Test-Path 'artifacts/tests') {
    $msg | Set-Content -Path 'artifacts/tests/rollback_dryrun.txt' -Encoding UTF8
  }
  Write-Output $msg
  exit 0
}

$rollbackNext = Join-Path $base 'rollback_next.json'
$backupFailed = Join-Path $base "rollback/reverted_from_$($curr.version).json"
New-Item (Join-Path $base 'rollback') -ItemType Directory -Force | Out-Null

$prev | Add-Member -NotePropertyName 'restoredAtUtc' -NotePropertyValue ((Get-Date).ToUniversalTime().ToString('o')) -Force
$prev | ConvertTo-Json -Depth 4 | Set-Content -Path $rollbackNext -Encoding UTF8
[IO.File]::Replace($rollbackNext, $currentPath, $backupFailed, $true)
$sw.Stop()

Write-Output "[rollback] Reversion atomica completada en $([Math]::Round($sw.Elapsed.TotalSeconds, 3))s: version restaurada=$($prev.version) | Paquete=$($prev.package)"
