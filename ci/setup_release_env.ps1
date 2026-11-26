# ci/setup_release_env.ps1
# Script de generacion, aprovisionamiento y auditoria del entorno de liberacion y despliegue QA.
param(
  [string]$DefaultUnityEditor = 'C:\Program Files\Unity\Hub\Editor\6000.6.0f1\Editor\Unity.exe',
  [string]$DefaultDeployRoot  = 'C:\VAELITH_QA',
  [double]$MinFreeDiskGB      = 15.0,
  [double]$MinSystemRamGB     = 8.0
)

$ErrorActionPreference = 'Stop'

if ([string]::IsNullOrWhiteSpace($env:UNITY_EDITOR)) {
  $env:UNITY_EDITOR = $DefaultUnityEditor
}
if ([string]::IsNullOrWhiteSpace($env:DEPLOY_ROOT)) {
  $env:DEPLOY_ROOT = $DefaultDeployRoot
}

Write-Output "[setup-env] Verificando requisitos del entorno de liberacion VAELITH..."

# 1. Verificar ausencia de bloqueo activo del Editor Unity sobre este workspace
if (Test-Path 'Temp/UnityLockfile') {
  $uProc = Get-Process -Name 'Unity' -ErrorAction SilentlyContinue
  if ($uProc) {
    throw 'Existe una sesion interactiva de Unity abierta sobre este proyecto; cierre Unity antes de liberar.'
  }
}

# 2. Verificar Unity Editor 6000.6.0f1 y modulo Windows Standalone (Win64)
if (!(Test-Path $env:UNITY_EDITOR -PathType Leaf)) {
  throw "No se encontro Unity Editor en UNITY_EDITOR=$($env:UNITY_EDITOR)"
}
$editorDir = Split-Path -Parent $env:UNITY_EDITOR
$winModule = Join-Path $editorDir 'Data\PlaybackEngines\WindowsStandaloneSupport'
if (!(Test-Path $winModule)) {
  throw "Falta el modulo WindowsStandaloneSupport en $winModule"
}
$unityVer = (Get-Item $env:UNITY_EDITOR).VersionInfo.ProductVersion
if ($unityVer -notlike '6000.6.0f1*') {
  throw "Version de Unity incompatible ($unityVer); se requiere 6000.6.0f1"
}

# 3. Verificar Git y Git LFS
$gitVer = (git --version 2>&1 | Out-String).Trim()
$lfsVer = (git lfs version 2>&1 | Out-String).Trim()
if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($lfsVer)) {
  throw 'Git LFS es obligatorio para descargar modelos FBX y texturas PBR de VAELITH.'
}

# 4. Auditar recursos fisicos del nodo de liberacion (CPU, RAM, SSD libre, GPU)
$cs = Get-CimInstance Win32_ComputerSystem
$os = Get-CimInstance Win32_OperatingSystem
$cpu = Get-CimInstance Win32_Processor | Select-Object -First 1
$gpu = Get-CimInstance Win32_VideoController | Select-Object -First 1

$ramGB = [Math]::Round($cs.TotalPhysicalMemory / 1GB, 2)
$deployDriveRoot = [IO.Path]::GetPathRoot([IO.Path]::GetFullPath($env:DEPLOY_ROOT))
$driveInfo = Get-PSDrive -Name ($deployDriveRoot.TrimEnd(':\'))
$freeDiskGB = [Math]::Round($driveInfo.Free / 1GB, 2)

if ($ramGB -lt $MinSystemRamGB) {
  throw "RAM insuficiente en el nodo ($ramGB GB < $MinSystemRamGB GB requeridos)"
}
if ($freeDiskGB -lt $MinFreeDiskGB) {
  throw "Espacio libre insuficiente en unidad $deployDriveRoot ($freeDiskGB GB < $MinFreeDiskGB GB requeridos)"
}

# 5. Generar estructura del entorno de despliegue QA fuera del repositorio
$deployFull = [IO.Path]::GetFullPath($env:DEPLOY_ROOT)
$repoFull   = [IO.Path]::GetFullPath((Get-Location).Path)
if ($deployFull.TrimEnd('\') -eq $repoFull.TrimEnd('\') -or
    $deployFull.StartsWith($repoFull.TrimEnd('\') + '\', [StringComparison]::OrdinalIgnoreCase)) {
  throw "DEPLOY_ROOT ($deployFull) debe residir fuera del arbol del repositorio ($repoFull)"
}

New-Item -Path $deployFull -ItemType Directory -Force | Out-Null
New-Item -Path (Join-Path $deployFull 'logs') -ItemType Directory -Force | Out-Null
New-Item -Path (Join-Path $deployFull 'rollback') -ItemType Directory -Force | Out-Null
New-Item -Path 'artifacts' -ItemType Directory -Force | Out-Null

$envReport = [ordered]@{
  checkedAtUtc         = (Get-Date).ToUniversalTime().ToString('o')
  hostname             = $env:COMPUTERNAME
  osCaption            = $os.Caption
  osBuild              = $os.BuildNumber
  cpuName              = $cpu.Name.Trim()
  logicalProcessors    = [int]$cs.NumberOfLogicalProcessors
  totalRamGB           = $ramGB
  gpuName              = if ($gpu) { $gpu.Name } else { 'Unknown' }
  gpuDriverVersion     = if ($gpu) { $gpu.DriverVersion } else { 'Unknown' }
  deployDrive          = $deployDriveRoot
  freeDiskGB           = $freeDiskGB
  gitVersion           = $gitVer
  gitLfsVersion        = $lfsVer
  unityEditorPath      = $env:UNITY_EDITOR
  unityEditorVersion   = $unityVer
  windowsSupportModule = $winModule
  deployRoot           = $deployFull
  environmentReady     = $true
}

$envReport | ConvertTo-Json -Depth 4 | Set-Content -Path 'artifacts/release_environment.json' -Encoding UTF8
Copy-Item -LiteralPath 'artifacts/release_environment.json' -Destination (Join-Path $deployFull 'logs/last_environment_audit.json') -Force

Write-Output "[setup-env] Entorno de liberacion y QA listo: Host=$($env:COMPUTERNAME) | RAM=${ramGB}GB | Libre=${freeDiskGB}GB | GPU=$($envReport.gpuName) | DeployRoot=$deployFull"
