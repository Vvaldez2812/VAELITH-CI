# ci/smoke.ps1
# Prueba de humo y recoleccion de metricas reales de monitoreo (FPS, p95, RAM, SLA) sobre VAELITH.exe compilado.
$ErrorActionPreference = 'Stop'
$exe = (Resolve-Path 'artifacts/player/VAELITH.exe').Path
$log = Join-Path (Get-Location).Path 'artifacts/logs/player.log'
$telemetryJson = Join-Path (Get-Location).Path 'artifacts/tests/telemetry_metrics.json'

if (Test-Path $telemetryJson) { Remove-Item $telemetryJson -Force }

$arguments = @(
  '-screen-fullscreen', '0',
  '-screen-width', '1920',
  '-screen-height', '1080',
  '-logFile', "`"$log`"",
  '-vaelith-telemetry', "`"$telemetryJson`"",
  '-vaelith-telemetry-seconds', '6.0',
  '-vaelith-telemetry-quit'
)

Write-Output '[smoke] Iniciando VAELITH.exe (1920x1080, Direct3D11) con monitor de telemetria en segundo plano...'
$player = Start-Process -FilePath $exe -ArgumentList $arguments `
  -WorkingDirectory (Split-Path $exe) -PassThru -WindowStyle Minimized

try {
  $deadline = (Get-Date).AddSeconds(25)
  while ((Get-Date) -lt $deadline) {
    $player.Refresh()
    if ((Test-Path $telemetryJson) -and $player.HasExited) {
      break
    }
    Start-Sleep -Milliseconds 500
  }

  $player.Refresh()
  if ($player.HasExited -and $player.ExitCode -ne 0 -and !(Test-Path $telemetryJson)) {
    throw "El juego se cerro inesperadamente con error al iniciar (ExitCode=$($player.ExitCode))"
  }
  if (!(Test-Path $log)) {
    throw 'No se genero Player.log durante la prueba de arranque'
  }
  $text = Get-Content $log -Raw
  if ($text -match '(?im)Crash!!!|Fatal error') {
    throw 'El registro Player.log contiene errores fatales o crash de motor'
  }
  if (!(Test-Path $telemetryJson)) {
    throw 'El cliente VAELITH.exe no emitio el reporte de monitoreo telemetry_metrics.json dentro del tiempo limite'
  }

  $metrics = Get-Content $telemetryJson -Raw | ConvertFrom-Json
  if ($metrics.exceptionCount -gt 0 -or $metrics.errorCount -gt 0) {
    throw "Se detectaron errores/excepciones en ejecucion: errors=$($metrics.errorCount), exceptions=$($metrics.exceptionCount)"
  }
  if ($metrics.avgFps -lt 30.0) {
    throw "Incumplimiento de SLO de rendimiento: AvgFPS=$($metrics.avgFps) (< 30.0 FPS acordados)"
  }
  if (!$metrics.slaPassed) {
    throw 'El reporte de telemetria marco slaPassed=false'
  }

  $report = "Smoke + Telemetria SLA APROBADO | AvgFPS=$($metrics.avgFps) | p95=$($metrics.p95FrameTimeMs)ms | RAM_Pico=$($metrics.ramTotalAllocatedPeakMB)MB | Boot=$($metrics.bootTimeSeconds)s | Puertas=$($metrics.interactiveDoorsCount) | ModoToggle=$($metrics.modeToggleVerified) | Fecha=$((Get-Date).ToUniversalTime().ToString('o'))"
  $report | Set-Content -Path 'artifacts/tests/smoke.txt' -Encoding UTF8
  Write-Output "[smoke] $report"
}
finally {
  $player.Refresh()
  if (!$player.HasExited) {
    $player.Kill()
    $player.WaitForExit()
  }
}
