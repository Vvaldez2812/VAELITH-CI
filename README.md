# VAELITH: After You Wake — Demo «Las 3:17»

Proyecto de **Gestión del Proceso de Desarrollo de Software (IDGS10-1)** — Unidad II (Segunda Fase: Integración, Liberación y Despliegue Continuo).

## Integrantes
- Fausto de Jesús Beltrán Valenzuela
- Jade Anahy Verdugo Barajas

## Entorno técnico verificado
- **Motor:** Unity `6000.6.0f1` (URP `17.7.0`)
- **Entrada:** Unity Input System `1.20.0`
- **Pruebas:** Unity Test Framework `1.8.0` (`EditMode` y `PlayMode`)
- **Escena principal:** `Assets/Scenes/Exterior_Docencia2.unity`
- **Destino interno de QA:** `C:\VAELITH_QA` (fuera del repositorio)

## Estructura de liberación continua (CI/CD)
- [`.github/workflows/release.yml`](.github/workflows/release.yml): Pipeline de GitHub Actions para ejecutor Windows autoalojado (`[self-hosted, Windows, X64, unity-6000.6]`, entorno `qa`).
- [`ci/prepare.ps1`](ci/prepare.ps1): Valida editor `6000.6.0f1`, escena, `manifest.json`, `packages-lock.json`, `BuildCI.cs`, suites de prueba, `DEPLOY_ROOT`, `BUILD_ID` y genera `artifacts/manifest.json`.
- [`ci/test.ps1`](ci/test.ps1): Ejecuta las suites `EditMode` y `PlayMode` mediante Unity Test Framework y valida XML no vacío sin fallos.
- [`Assets/Editor/BuildCI.cs`](Assets/Editor/BuildCI.cs) + [`ci/build.ps1`](ci/build.ps1): Compila explícitamente `Assets/Scenes/Exterior_Docencia2.unity` hacia `artifacts/player/VAELITH.exe` (`StandaloneWindows64`).
- [`ci/smoke.ps1`](ci/smoke.ps1): Prueba de arranque real de `VAELITH.exe` durante 10 segundos verificando `Player.log` sin errores críticos.
- [`ci/deploy.ps1`](ci/deploy.ps1): Empaqueta el build completo en ZIP, valida integridad por `SHA256`, copia al directorio de versión en `DEPLOY_ROOT` y actualiza atómicamente `current.json` / `previous.json`.
- [`ci/run_local_pipeline.ps1`](ci/run_local_pipeline.ps1): Ejecuta de principio a fin las 5 etapas en el entorno local Windows para validación previa o demostración en vivo.

## Ejecución local del pipeline
```powershell
.\ci\run_local_pipeline.ps1
```
