# VAELITH: After You Wake — Demo «Las 3:17»

**Evaluación Unidad III — Tercera Fase del Proyecto**  
**Asignatura:** Gestión del Proceso de Desarrollo de Software (`IDGS10-1`)  
**Docente:** Martín Cuadras  

## Integrantes
- Fausto de Jesús Beltrán Valenzuela
- Jade Anahy Verdugo Barajas

---

## Entorno técnico bloqueado y verificado
- **Motor:** Unity `6000.6.0f1` (`Universal Render Pipeline 17.7.0`, `Direct3D11`)
- **Entrada y pruebas:** Unity Input System `1.20.0` · Unity Test Framework `1.8.0` (`EditMode` y `PlayMode`)
- **Escena principal:** `Assets/Scenes/Exterior_Docencia2.unity` (`70` puertas interactivas, rig 3D `Protagonista` + celular flotante en 1ª persona, `La Testigo`, modos `NOCHE` y `TERROR`)
- **Control de versiones:** Git + Git LFS (`https://github.com/Vvaldez2812/VAELITH-CI.git`)
- **Entorno de despliegue QA vinculado:** `C:\VAELITH_QA` (aislado fuera del árbol del repositorio, con `current.json`, `previous.json` y bitácora `logs/releases_history.jsonl`)

---

## Inventario de scripts entregados en el repositorio (Rúbrica Unidad III)

### 1. Scripts del flujo de trabajo (Pipeline CI/CD)
- [`.github/workflows/release.yml`](.github/workflows/release.yml): Flujo declarativo de GitHub Actions vinculado al entorno `qa` sobre ejecutor Windows autoalojado (`[self-hosted, Windows, X64, unity-6000.6]`), con control de concurrencia, ejecución en cascada de las 7 compuertas y retención de artefactos por 14 días.
- [`ci/run_local_pipeline.ps1`](ci/run_local_pipeline.ps1): Orquestador integral en PowerShell que ejecuta las 7 etapas secuenciales registrando tiempos por etapa en `artifacts/pipeline_metrics.json`.

### 2. Scripts para la generación del entorno de liberación
- [`ci/setup_release_env.ps1`](ci/setup_release_env.ps1): Aprovisiona y audita el nodo de liberación Windows x64 (ausencia de bloqueo de Editor, Unity `6000.6.0f1` + módulo `WindowsStandaloneSupport`, Git + Git LFS, CPU/RAM/espacio libre SSD/GPU Direct3D11 y creación de `C:\VAELITH_QA\{logs,rollback}`), emitiendo `artifacts/release_environment.json`.
- [`ci/prepare.ps1`](ci/prepare.ps1): Valida la integridad de los archivos críticos (`ProjectVersion.txt`, `Packages/manifest.json`, `packages-lock.json`, `Exterior_Docencia2.unity`, `BuildCI.cs`, `Docencia2ReleaseTelemetry.cs` y ensamblados `.asmdef`), limpia `artifacts/` y genera `artifacts/manifest.json`.

### 3. Scripts de pruebas y monitoreo en el entorno de liberación
- [`ci/test.ps1`](ci/test.ps1): Ejecuta las suites `EditMode` y `PlayMode` mediante Unity Test Framework CLI, valida que no existan suites vacías ni casos fallidos y exporta `artifacts/tests/test_summary.json`.
- [`Assets/Tests/EditMode/VaelithReleaseEditModeTests.cs`](Assets/Tests/EditMode/VaelithReleaseEditModeTests.cs): 5 pruebas estructurales sobre escena principal, paquetes bloqueados, `70` puertas interactivas (`29` en PA), celular flotante en 1ª persona, `Protagonista`, `La Testigo`, escalera `Sec05`, canceles `Sec06` y luminarias.
- [`Assets/Tests/PlayMode/VaelithReleasePlayModeTests.cs`](Assets/Tests/PlayMode/VaelithReleasePlayModeTests.cs): 5 pruebas de integración en tiempo de ejecución (spawn seguro y aislamiento `Layer 28`, conmutación `NOCHE <-> TERROR` con recuperación de `Docencia2InteriorModeTracker`, apertura/cierre de canceles de cristal `P15`/`P16`, bloqueo permanente de `P29` Dirección y aislamiento de oscuridad interior vs. exterior).
- [`ci/smoke.ps1`](ci/smoke.ps1) + [`Assets/Scripts/Docencia2ReleaseTelemetry.cs`](Assets/Scripts/Docencia2ReleaseTelemetry.cs): Lanza el ejecutable real `VAELITH.exe` a `1920x1080` con `-vaelith-telemetry`, recolecta métricas reales de monitoreo (`avgFps`, `minFps`, `p95FrameTimeMs`, `ramTotalAllocatedPeakMB`, `bootTimeSeconds`, `errorCount`, `exceptionCount`, `modeToggleVerified`) en `artifacts/tests/telemetry_metrics.json` y bloquea la liberación si se incumple cualquier SLO/SLA.

### 4. Scripts para la generación del despliegue y reversión
- [`Assets/Editor/BuildCI.cs`](Assets/Editor/BuildCI.cs) + [`ci/build.ps1`](ci/build.ps1): Compila determinísticamente `StandaloneWindows64` (`Direct3D11`) hacia `artifacts/player/VAELITH.exe` y genera `artifacts/build_report.json`.
- [`ci/deploy.ps1`](ci/deploy.ps1): Empaqueta la versión en `VAELITH-<BUILD_ID>.zip`, verifica integridad criptográfica `SHA-256` en origen y destino (`C:\VAELITH_QA\<BUILD_ID>\`), promueve atómicamente `current.json` preservando `previous.json` y registra la entrega en `C:\VAELITH_QA\logs\releases_history.jsonl`.
- [`ci/rollback.ps1`](ci/rollback.ps1): Ejecuta o verifica (`-DryRun`) la reversión atómica en `< 2 s` desde `previous.json` hacia `current.json` validando previamente el hash `SHA-256` del paquete estable anterior.

---

## Ejecución del pipeline completo y reversión
```powershell
# Ejecutar liberacion continua y despliegue QA completo:
.\ci\run_local_pipeline.ps1

# Verificar capacidad de rollback sin alterar version activa:
.\ci\rollback.ps1 -DryRun

# Revertir atomicamente a la version estable anterior en C:\VAELITH_QA:
.\ci\rollback.ps1
```
