$ErrorActionPreference = 'Stop'
$exe = (Resolve-Path 'artifacts/player/VAELITH.exe').Path
$log = Join-Path (Get-Location).Path 'artifacts/logs/player.log'
$arguments = @(
  '-screen-fullscreen', '0',
  '-screen-width', '1280',
  '-screen-height', '720',
  '-logFile', "`"$log`""
)

Write-Output "[smoke] Iniciando prueba de arranque de VAELITH.exe durante 10 s..."
$player = Start-Process -FilePath $exe -ArgumentList $arguments `
  -WorkingDirectory (Split-Path $exe) -PassThru -WindowStyle Hidden

try {
  Start-Sleep -Seconds 10
  $player.Refresh()
  if ($player.HasExited) {
    throw "El juego se cerro inesperadamente al iniciar (ExitCode=$($player.ExitCode))"
  }
  if (!(Test-Path $log)) {
    throw 'No se genero Player.log durante la prueba de arranque'
  }
  $text = Get-Content $log -Raw
  if ($text -match '(?im)Exception:|Crash!!!|Fatal error') {
    throw 'El registro Player.log contiene errores criticos o excepciones de arranque'
  }
  $report = "Arranque basico aprobado | Proceso activo 10 s | PID=$($player.Id) | Fecha=$((Get-Date).ToUniversalTime().ToString('o'))"
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
