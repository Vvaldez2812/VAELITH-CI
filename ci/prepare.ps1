# ci/prepare.ps1
# Valida archivos criticos del proyecto, limpia artefactos previos y emite artifacts/manifest.json.
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
  'Assets/Scripts/Docencia2ReleaseTelemetry.cs',
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
  throw 'BUILD_ID debe tener formato run_id-run_attempt (ejemplo: 20261126-1)'
}

# Conservar release_environment.json si setup_release_env.ps1 acaba de generarlo
$savedEnvJson = $null
if (Test-Path 'artifacts/release_environment.json') {
  $savedEnvJson = Get-Content 'artifacts/release_environment.json' -Raw
}

if (Test-Path 'artifacts') {
  Remove-Item 'artifacts' -Recurse -Force
}

New-Item 'artifacts/logs', 'artifacts/tests', 'artifacts/player' -ItemType Directory -Force | Out-Null

if ($null -ne $savedEnvJson) {
  $savedEnvJson | Set-Content -Path 'artifacts/release_environment.json' -Encoding UTF8
}

$commitSha = $env:GITHUB_SHA
if ([string]::IsNullOrWhiteSpace($commitSha)) {
  try {
    $commitSha = (git rev-parse HEAD 2>$null | Out-String).Trim()
  } catch {
    $commitSha = 'local-sin-commit'
  }
  if ([string]::IsNullOrWhiteSpace($commitSha)) { $commitSha = 'local-sin-commit' }
}

$branchName = $env:GITHUB_REF_NAME
if ([string]::IsNullOrWhiteSpace($branchName)) {
  try {
    $branchName = (git rev-parse --abbrev-ref HEAD 2>$null | Out-String).Trim()
  } catch {
    $branchName = 'main'
  }
}

[ordered]@{
  project    = 'VAELITH: After You Wake — Demo Las 3:17'
  scene      = 'Assets/Scenes/Exterior_Docencia2.unity'
  build      = $env:BUILD_ID
  branch     = $branchName
  commit     = $commitSha
  editor     = $installed
  deployRoot = [IO.Path]::GetFullPath($env:DEPLOY_ROOT)
  date       = (Get-Date).ToUniversalTime().ToString('o')
} | ConvertTo-Json | Set-Content -Path 'artifacts/manifest.json' -Encoding UTF8

Write-Output "[prepare] Entorno verificado correctamente. BUILD_ID=$($env:BUILD_ID) | Branch=$branchName | Commit=$commitSha"
