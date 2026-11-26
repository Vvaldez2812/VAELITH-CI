# ci/build.ps1
# Compila el cliente StandaloneWindows64 (Direct3D11) invocando BuildCI.Windows.
$ErrorActionPreference = 'Stop'
$root = (Get-Location).Path
$log = Join-Path $root 'artifacts/logs/build.log'

Write-Output '[build] Compilando VAELITH (StandaloneWindows64, Direct3D11, Exterior_Docencia2.unity)...'
$sw = [Diagnostics.Stopwatch]::StartNew()
$args = @(
  '-batchmode',
  '-quit',
  '-projectPath', "`"$root`"",
  '-buildTarget', 'Win64',
  '-executeMethod', 'BuildCI.Windows',
  '-logFile', "`"$log`""
)
$proc = Start-Process -FilePath $env:UNITY_EDITOR -ArgumentList $args -PassThru
$proc.WaitForExit()
$sw.Stop()

if ($proc.ExitCode -ne 0) {
  throw "Fallo la compilacion de Unity (ExitCode=$($proc.ExitCode)); consulte $log"
}
if (!(Test-Path 'artifacts/player/VAELITH.exe')) {
  throw 'No se genero artifacts/player/VAELITH.exe'
}

$exeInfo = Get-Item 'artifacts/player/VAELITH.exe'
$totalBytes = (Get-ChildItem 'artifacts/player' -Recurse -File | Measure-Object -Property Length -Sum).Sum
$totalMB = [Math]::Round($totalBytes / 1MB, 2)
Write-Output "[build] Compilacion exitosa en $([Math]::Round($sw.Elapsed.TotalSeconds, 2))s: $($exeInfo.FullName) | Tamano total directorio player=${totalMB} MB"
