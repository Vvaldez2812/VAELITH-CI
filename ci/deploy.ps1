$ErrorActionPreference = 'Stop'
$base = [IO.Path]::GetFullPath($env:DEPLOY_ROOT)
$repo = [IO.Path]::GetFullPath((Get-Location).Path)

if ($base.TrimEnd('\') -eq $repo.TrimEnd('\') -or
    $base.StartsWith($repo.TrimEnd('\') + '\', [StringComparison]::OrdinalIgnoreCase)) {
  throw 'El destino de QA (DEPLOY_ROOT) debe estar fuera del repositorio'
}

New-Item $base -ItemType Directory -Force | Out-Null

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

Copy-Item 'artifacts/manifest.json' -Destination $destination
if (Test-Path 'artifacts/tests') {
  Copy-Item 'artifacts/tests' -Destination (Join-Path $destination 'tests') -Recurse -Force
}

$current = Join-Path $base 'current.json'
$next = Join-Path $base 'next.json'
$previous = Join-Path $base 'previous.json'

@{
  version    = $id
  package    = $target
  sha256     = $hash
  sizeBytes  = (Get-Item $target).Length
  deployedAt = (Get-Date).ToUniversalTime().ToString('o')
} | ConvertTo-Json | Set-Content -Path $next -Encoding UTF8

if (Test-Path $current) {
  [IO.File]::Replace($next, $current, $previous, $true)
} else {
  [IO.File]::Move($next, $current)
}

Copy-Item -LiteralPath $current -Destination 'artifacts/deployment.json' -Force
Write-Output "[deploy] Despliegue QA completado en $destination | SHA256=$hash"
