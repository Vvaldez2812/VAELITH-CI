$ErrorActionPreference = 'Stop'

if ([string]::IsNullOrWhiteSpace($env:UNITY_EDITOR) -or !(Test-Path $env:UNITY_EDITOR -PathType Leaf)) {
  throw 'Configure la variable UNITY_EDITOR apuntando a Unity.exe 6000.6.0f1'
}

$version = Get-Content 'ProjectSettings/ProjectVersion.txt' -Raw
if ($version -notmatch 'm_EditorVersion:\s*6000\.6\.0f1') {
  throw 'El proyecto no usa la version acordada (6000.6.0f1)'
}

$installed = (Get-Item $env:UNITY_EDITOR).VersionInfo.ProductVersion
if ($installed -notlike '6000.6.0f1*') {
  throw "Editor instalado incompatible: $installed"
}

$required = @(
  'Assets/Scenes/Exterior_Docencia2.unity',
  'Packages/manifest.json',
  'Packages/packages-lock.json',
  'Assets/Editor/BuildCI.cs',
  'Assets/Tests/EditMode/VAELITH.Tests.EditMode.asmdef',
  'Assets/Tests/PlayMode/VAELITH.Tests.PlayMode.asmdef'
)
foreach ($file in $required) {
  if (!(Test-Path $file)) { throw "Falta archivo obligatorio: $file" }
}

if ([string]::IsNullOrWhiteSpace($env:DEPLOY_ROOT)) {
  throw 'Configure DEPLOY_ROOT fuera del repositorio'
}

if ($env:BUILD_ID -notmatch '^\d+-\d+$') {
  throw 'BUILD_ID debe tener formato run_id-run_attempt (ejemplo: 20261115-1)'
}

if (Test-Path 'artifacts') {
  Remove-Item 'artifacts' -Recurse -Force
}

New-Item 'artifacts/logs', 'artifacts/tests', 'artifacts/player' -ItemType Directory -Force | Out-Null

$commitSha = $env:GITHUB_SHA
if ([string]::IsNullOrWhiteSpace($commitSha)) {
  try {
    $commitSha = (git rev-parse HEAD 2>$null | Out-String).Trim()
  } catch {
    $commitSha = 'local-sin-commit'
  }
  if ([string]::IsNullOrWhiteSpace($commitSha)) { $commitSha = 'local-sin-commit' }
}

@{
  project = 'VAELITH: After You Wake'
  scene   = 'Assets/Scenes/Exterior_Docencia2.unity'
  build   = $env:BUILD_ID
  commit  = $commitSha
  editor  = $installed
  date    = (Get-Date).ToUniversalTime().ToString('o')
} | ConvertTo-Json | Set-Content -Path 'artifacts/manifest.json' -Encoding UTF8

Write-Output "[prepare] Entorno verificado correctamente. BUILD_ID=$($env:BUILD_ID) | Commit=$commitSha"
