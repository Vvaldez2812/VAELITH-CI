# ci/run_local_pipeline.ps1
# Orquestador completo del flujo de liberacion continua y despliegue QA en nodo Windows x64.
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

  $totalSw = [Diagnostics.Stopwatch]::StartNew()
  $stageTimings = @()

  function Invoke-Stage([string]$Name, [string]$ScriptName, [scriptblock]$Action) {
    Write-Output "----------------------------------------------------------------"
    Write-Output ">>> ETAPA: $Name"
    $s = [Diagnostics.Stopwatch]::StartNew()
    & $Action
    $s.Stop()
    $script:stageTimings += [ordered]@{
      stage           = $Name
      script          = $ScriptName
      durationSeconds = [Math]::Round($s.Elapsed.TotalSeconds, 2)
      status          = 'Succeeded'
    }
  }

  Write-Output "=== INICIANDO PIPELINE DE LIBERACION CONTINUA VAELITH ($BuildId) ==="
  Invoke-Stage '1. Aprovisionamiento de Entorno de Liberacion' 'setup_release_env.ps1' { & "$PSScriptRoot\setup_release_env.ps1" }
  Invoke-Stage '2. Preparacion y Manifiesto de Trazabilidad'   'prepare.ps1'           { & "$PSScriptRoot\prepare.ps1" }
  Invoke-Stage '3. Pruebas Automatizadas EditMode y PlayMode'  'test.ps1'              { & "$PSScriptRoot\test.ps1" }
  Invoke-Stage '4. Compilacion Cliente StandaloneWindows64'    'build.ps1'             { & "$PSScriptRoot\build.ps1" }
  Invoke-Stage '5. Smoke Test y Telemetria SLA en Ejecucion'   'smoke.ps1'             { & "$PSScriptRoot\smoke.ps1" }
  Invoke-Stage '6. Despliegue Atomico SHA-256 a Entorno QA'    'deploy.ps1'            { & "$PSScriptRoot\deploy.ps1" }
  Invoke-Stage '7. Verificacion de Reversion Atomica (DryRun)' 'rollback.ps1'          { & "$PSScriptRoot\rollback.ps1" -DryRun }

  $totalSw.Stop()

  $pipelineReport = [ordered]@{
    buildId              = $BuildId
    completedAtUtc       = (Get-Date).ToUniversalTime().ToString('o')
    totalDurationSeconds = [Math]::Round($totalSw.Elapsed.TotalSeconds, 2)
    slaUnder60Minutes    = ($totalSw.Elapsed.TotalMinutes -lt 60.0)
    stages               = $stageTimings
  }
  $pipelineReport | ConvertTo-Json -Depth 5 | Set-Content -Path 'artifacts/pipeline_metrics.json' -Encoding UTF8
  Copy-Item -LiteralPath 'artifacts/pipeline_metrics.json' -Destination (Join-Path $DeployRoot "$BuildId\pipeline_metrics.json") -Force

  Write-Output "=== PIPELINE COMPLETADO CON EXITO EN $([Math]::Round($totalSw.Elapsed.TotalSeconds, 2)) s (BUILD_ID=$BuildId) ==="
}
finally {
  Pop-Location
}
