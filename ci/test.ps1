# ci/test.ps1
# Ejecuta las suites automatizadas EditMode y PlayMode en el entorno de liberacion y valida resultados NUnit XML.
$ErrorActionPreference = 'Stop'
$root = (Get-Location).Path
$suitesSummary = @()

foreach ($mode in @('EditMode', 'PlayMode')) {
  $result = Join-Path $root "artifacts/tests/$mode.xml"
  $log = Join-Path $root "artifacts/logs/$mode.log"
  Write-Output "[test] Ejecutando suite $mode en Unity Test Framework..."

  $sw = [Diagnostics.Stopwatch]::StartNew()
  # Sin -quit: Unity Test Framework controla el cierre al terminar los casos.
  # Usamos WaitForExit() sobre el proceso directo para no bloquearnos en demonios Roslyn VBCSCompiler.
  $args = @(
    '-batchmode',
    '-projectPath', "`"$root`"",
    '-runTests',
    '-testPlatform', $mode,
    '-testResults', "`"$result`"",
    '-logFile', "`"$log`""
  )
  $proc = Start-Process -FilePath $env:UNITY_EDITOR -ArgumentList $args -PassThru
  $proc.WaitForExit()
  $sw.Stop()

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
  $passRate = if ($total -gt 0) { [Math]::Round(($passed * 100.0) / $total, 2) } else { 0.0 }

  Write-Output "[test] $mode -> Result=$($run.result) | Total=$total | Passed=$passed | Failed=$failed | Skipped=$skipped | Duration=$([Math]::Round($sw.Elapsed.TotalSeconds, 2))s"

  if ($passed -lt 1 -or $failed -gt 0 -or $run.result -ne 'Passed') {
    throw "Suite $mode vacia, incompleta o fallida ($passed aprobadas, $failed fallidas, result=$($run.result))"
  }

  $suitesSummary += [ordered]@{
    suite           = $mode
    result          = [string]$run.result
    total           = $total
    passed          = $passed
    failed          = $failed
    skipped         = $skipped
    passRatePercent = $passRate
    durationSeconds = [Math]::Round($sw.Elapsed.TotalSeconds, 2)
    xmlReport       = "artifacts/tests/$mode.xml"
  }
}

[ordered]@{
  executedAtUtc = (Get-Date).ToUniversalTime().ToString('o')
  overallResult = 'Passed'
  suites        = $suitesSummary
} | ConvertTo-Json -Depth 5 | Set-Content -Path 'artifacts/tests/test_summary.json' -Encoding UTF8
