param(
  [string]$UnityEditor = 'C:\Program Files\Unity\Hub\Editor\6000.6.0f1\Editor\Unity.exe',
  [string]$DeployRoot  = 'C:\VAELITH_QA',
  [string]$BuildId     = ""
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
Push-Location $repoRoot

try {
  if ([string]::IsNullOrWhiteSpace($BuildId)) {
    $runId = Get-Date -Format 'yyyyMMddHHmmss'
    $BuildId = "$runId-1"
  }

  $env:UNITY_EDITOR = $UnityEditor
  $env:DEPLOY_ROOT  = $DeployRoot
  $env:BUILD_ID     = $BuildId

  Write-Output "=== INICIANDO PIPELINE DE LIBERACION VAELITH ($BuildId) ==="
  & "$PSScriptRoot\prepare.ps1"
  & "$PSScriptRoot\test.ps1"
  & "$PSScriptRoot\build.ps1"
  & "$PSScriptRoot\smoke.ps1"
  & "$PSScriptRoot\deploy.ps1"
  Write-Output "=== PIPELINE COMPLETADO CON EXITO ==="
}
finally {
  Pop-Location
}
