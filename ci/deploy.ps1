# ci/deploy.ps1
# Empaqueta la version candidata en ZIP, verifica integridad SHA-256 y despliega atomicamente en DEPLOY_ROOT.
$ErrorActionPreference = 'Stop'
$base = [IO.Path]::GetFullPath($env:DEPLOY_ROOT)
$repo = [IO.Path]::GetFullPath((Get-Location).Path)

if ($base.TrimEnd('\') -eq $repo.TrimEnd('\') -or
    $base.StartsWith($repo.TrimEnd('\') + '\', [StringComparison]::OrdinalIgnoreCase)) {
  throw 'El destino de QA (DEPLOY_ROOT) debe estar fuera del repositorio'
}

New-Item $base -ItemType Directory -Force | Out-Null
New-Item (Join-Path $base 'logs') -ItemType Directory -Force | Out-Null

$id = $env:BUILD_ID
if ($id -notmatch '^\d+-\d+$') { throw "Identificador de compilacion invalido: $id" }

$destination = Join-Path $base $id
if (Test-Path $destination) { throw "La version $id ya existe en $base" }
New-Item $destination -ItemType Directory -Force | Out-Null

$zip = Join-Path (Get-Location).Path "artifacts/VAELITH-$id.zip"
Write-Output "[deploy] Empaquetando build completo en $zip..."
Compress-Archive -Path 'artifacts/player/*' -DestinationPath $zip -Force

$hash = (Get-FileHash $zip -Algorithm SHA256).Hash
$target = Join-Path $destination "VAELITH-$id.zip"
Copy-Item -LiteralPath $zip -Destination $target

if ((Get-FileHash $target -Algorithm SHA256).Hash -ne $hash) {
  throw 'La copia del paquete ZIP no coincide con el SHA256 original'
}

Copy-Item 'artifacts/manifest.json' -Destination $destination -Force
if (Test-Path 'artifacts/build_report.json') {
  Copy-Item 'artifacts/build_report.json' -Destination $destination -Force
}
if (Test-Path 'artifacts/tests') {
  Copy-Item 'artifacts/tests' -Destination (Join-Path $destination 'tests') -Recurse -Force
}

$manifestObj = Get-Content 'artifacts/manifest.json' -Raw | ConvertFrom-Json
$telemetrySummary = $null
if (Test-Path 'artifacts/tests/telemetry_metrics.json') {
  $tm = Get-Content 'artifacts/tests/telemetry_metrics.json' -Raw | ConvertFrom-Json
  $telemetrySummary = [ordered]@{
    avgFps         = $tm.avgFps
    p95FrameTimeMs = $tm.p95FrameTimeMs
    ramPeakMB      = $tm.ramTotalAllocatedPeakMB
    bootSeconds    = $tm.bootTimeSeconds
    slaPassed      = $tm.slaPassed
  }
}

$current = Join-Path $base 'current.json'
$next = Join-Path $base 'next.json'
$previous = Join-Path $base 'previous.json'

$deployRecord = [ordered]@{
  version    = $id
  commit     = $manifestObj.commit
  package    = $target
  sha256     = $hash
  sizeBytes  = (Get-Item $target).Length
  sizeMB     = [Math]::Round((Get-Item $target).Length / 1MB, 2)
  telemetry  = $telemetrySummary
  deployedAt = (Get-Date).ToUniversalTime().ToString('o')
}

$deployRecord | ConvertTo-Json -Depth 4 | Set-Content -Path $next -Encoding UTF8

if (Test-Path $current) {
  [IO.File]::Replace($next, $current, $previous, $true)
} else {
  [IO.File]::Move($next, $current)
}

# Registrar bitacora historica inmutable de liberaciones en QA
$historyLine = ($deployRecord | ConvertTo-Json -Compress -Depth 4)
Add-Content -Path (Join-Path $base 'logs/releases_history.jsonl') -Value $historyLine -Encoding UTF8

Copy-Item -LiteralPath $current -Destination 'artifacts/deployment.json' -Force
Write-Output "[deploy] Despliegue QA completado atomicamente en $destination | Size=$($deployRecord.sizeMB)MB | SHA256=$hash"
