$ErrorActionPreference = 'Stop'
$root = (Get-Location).Path
$log = Join-Path $root 'artifacts/logs/build.log'

Write-Output '[build] Compilando VAELITH (StandaloneWindows64, Exterior_Docencia2.unity)...'
$args = @(
  '-batchmode',
  '-quit',
  '-projectPath', "`"$root`"",
  '-buildTarget', 'Win64',
  '-executeMethod', 'BuildCI.Windows',
  '-logFile', "`"$log`""
)
$proc = Start-Process -FilePath $env:UNITY_EDITOR -ArgumentList $args -Wait -PassThru

if ($proc.ExitCode -ne 0) {
  throw "Fallo la compilacion de Unity (ExitCode=$($proc.ExitCode)); consulte $log"
}
if (!(Test-Path 'artifacts/player/VAELITH.exe')) {
  throw 'No se genero artifacts/player/VAELITH.exe'
}

$exeInfo = Get-Item 'artifacts/player/VAELITH.exe'
Write-Output "[build] Compilacion exitosa: $($exeInfo.FullName) ($($exeInfo.Length) bytes)"
