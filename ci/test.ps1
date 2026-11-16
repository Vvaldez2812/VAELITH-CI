$ErrorActionPreference = 'Stop'
$root = (Get-Location).Path

foreach ($mode in @('EditMode', 'PlayMode')) {
  $result = Join-Path $root "artifacts/tests/$mode.xml"
  $log = Join-Path $root "artifacts/logs/$mode.log"
  Write-Output "[test] Ejecutando suite $mode en Unity Test Framework..."

  # Sin -quit: Unity Test Framework controla el cierre al terminar los casos
  $args = @(
    '-batchmode',
    '-projectPath', "`"$root`"",
    '-runTests',
    '-testPlatform', $mode,
    '-testResults', "`"$result`"",
    '-logFile', "`"$log`""
  )
  $proc = Start-Process -FilePath $env:UNITY_EDITOR -ArgumentList $args -Wait -PassThru

  if ($proc.ExitCode -ne 0) {
    throw "Fallo Unity en suite $mode (ExitCode=$($proc.ExitCode)); consulte $log"
  }
  if (!(Test-Path $result)) {
    throw "No se genero el XML de resultados para $mode en $result"
  }

  [xml]$xml = Get-Content $result -Raw
  $run = $xml.'test-run'
  $total = [int]$run.total
  $passed = [int]$run.passed
  $failed = [int]$run.failed
  $skipped = [int]$run.skipped

  Write-Output "[test] $mode -> Result=$($run.result) | Total=$total | Passed=$passed | Failed=$failed | Skipped=$skipped"

  if ($passed -lt 1 -or $failed -gt 0 -or $run.result -ne 'Passed') {
    throw "Suite $mode vacia, incompleta o fallida ($passed aprobadas, $failed fallidas, result=$($run.result))"
  }
}
