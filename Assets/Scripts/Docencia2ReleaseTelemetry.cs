using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.SceneManagement;
using UnityEngine.Scripting;

namespace Vaelith.Docencia2
{
    /// <summary>
    /// Monitor ligero de telemetria y niveles de servicio (SLI/SLO) para el entorno de liberacion.
    /// Se activa unicamente cuando el ejecutable recibe el parametro CLI: -vaelith-telemetry &lt;rutaJson&gt;
    /// </summary>
    public sealed class Docencia2ReleaseTelemetry : MonoBehaviour
    {
        [Serializable]
        public sealed class TelemetryReport
        {
            public string project = "VAELITH: After You Wake";
            public string scene = "";
            public string unityVersion = "";
            public string graphicsDeviceName = "";
            public string graphicsDeviceType = "";
            public string operatingSystem = "";
            public int systemMemoryMB;
            public double bootTimeSeconds;
            public double sampleDurationSeconds;
            public int sampledFrames;
            public double avgFps;
            public double minFps;
            public double p95FrameTimeMs;
            public double maxFrameTimeMs;
            public double ramTotalAllocatedPeakMB;
            public double ramTotalReservedPeakMB;
            public double monoHeapPeakMB;
            public double gfxDriverAllocatedPeakMB;
            public int interactiveDoorsCount;
            public int paDoorsCount;
            public bool playerSpawnValid;
            public bool modeToggleVerified;
            public int errorCount;
            public int exceptionCount;
            public bool slaPassed;
            public string generatedAtUtc = "";
        }

        private string _outputPath;
        private float _durationSeconds = 7.5f;
        private bool _autoQuitAfterSample;
        private double _bootCompletedRealtime;
        private double _sampleStartRealtime;
        private double _lastFrameRealtime;
        private readonly List<double> _frameDeltas = new List<double>(1024);

        private double _peakAllocMB;
        private double _peakReservedMB;
        private double _peakMonoMB;
        private double _peakGfxMB;
        private int _errorCount;
        private int _exceptionCount;
        private bool _modeToggledMidRun;
        private bool _modeToggleVerified;
        private bool _written;

        [Preserve]
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void BootstrapFromCommandLine()
        {
            string[] args = Environment.GetCommandLineArgs();
            string outPath = null;
            float duration = 6.0f;
            bool autoQuit = false;

            for (int i = 0; i < args.Length; i++)
            {
                string cleanArg = args[i]?.Trim('"', ' ') ?? "";
                if (string.Equals(cleanArg, "-vaelith-telemetry", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
                {
                    outPath = args[i + 1]?.Trim('"', ' ');
                }
                else if (string.Equals(cleanArg, "-vaelith-telemetry-seconds", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
                {
                    if (float.TryParse(args[i + 1]?.Trim('"', ' '), NumberStyles.Float, CultureInfo.InvariantCulture, out float parsed))
                        duration = Mathf.Clamp(parsed, 2.0f, 60.0f);
                }
                else if (string.Equals(cleanArg, "-vaelith-telemetry-quit", StringComparison.OrdinalIgnoreCase))
                {
                    autoQuit = true;
                }
            }

            if (string.IsNullOrWhiteSpace(outPath))
                return;

            // Critico en ejecutores CI sin foco de ventana (-WindowStyle Hidden): evitar que Unity pause Update()
            Application.runInBackground = true;
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = -1;

            var go = new GameObject("VAELITH_ReleaseTelemetryMonitor");
            DontDestroyOnLoad(go);
            var monitor = go.AddComponent<Docencia2ReleaseTelemetry>();
            monitor._outputPath = outPath;
            monitor._durationSeconds = duration;
            monitor._autoQuitAfterSample = autoQuit;
        }

        private void OnEnable()
        {
            Application.logMessageReceived += HandleLog;
            _bootCompletedRealtime = Time.realtimeSinceStartupAsDouble;
            _sampleStartRealtime = _bootCompletedRealtime;
            _lastFrameRealtime = _bootCompletedRealtime;
        }

        private void OnDisable()
        {
            Application.logMessageReceived -= HandleLog;
            if (!_written && !string.IsNullOrWhiteSpace(_outputPath))
                WriteTelemetrySnapshot();
        }

        private void OnApplicationQuit()
        {
            if (!_written && !string.IsNullOrWhiteSpace(_outputPath))
                WriteTelemetrySnapshot();
        }

        private void HandleLog(string condition, string stackTrace, LogType type)
        {
            if (type == LogType.Error || type == LogType.Assert)
                _errorCount++;
            else if (type == LogType.Exception)
                _exceptionCount++;
        }

        private void Update()
        {
            double now = Time.realtimeSinceStartupAsDouble;
            double dt = now - _lastFrameRealtime;
            _lastFrameRealtime = now;

            // Ignorar los primeros 3 frames de carga inicial de shaders/escena
            if (Time.frameCount > 3 && dt > 0.00005 && dt < 1.0)
                _frameDeltas.Add(dt);

            double allocMB = Profiler.GetTotalAllocatedMemoryLong() / (1024.0 * 1024.0);
            double resMB = Profiler.GetTotalReservedMemoryLong() / (1024.0 * 1024.0);
            double monoMB = Profiler.GetMonoUsedSizeLong() / (1024.0 * 1024.0);
            double gfxMB = Profiler.GetAllocatedMemoryForGraphicsDriver() / (1024.0 * 1024.0);

            if (allocMB > _peakAllocMB) _peakAllocMB = allocMB;
            if (resMB > _peakReservedMB) _peakReservedMB = resMB;
            if (monoMB > _peakMonoMB) _peakMonoMB = monoMB;
            if (gfxMB > _peakGfxMB) _peakGfxMB = gfxMB;

            double elapsed = now - _sampleStartRealtime;

            // A mitad del muestreo verificamos la conmutacion real NOCHE <-> TERROR en el cliente compilado
            if (!_modeToggledMidRun && elapsed >= _durationSeconds * 0.45)
            {
                _modeToggledMidRun = true;
                var switcher = FindAnyObjectByType<Docencia2ModeSwitcher>();
                if (switcher != null)
                {
                    var prev = switcher.modoActual;
                    switcher.ToggleMode();
                    bool changed = switcher.modoActual != prev;
                    switcher.ToggleMode();
                    _modeToggleVerified = changed && switcher.modoActual == prev;
                }
            }

            if (!_written && elapsed >= _durationSeconds)
            {
                WriteTelemetrySnapshot();
                if (_autoQuitAfterSample)
                    Application.Quit(0);
            }
        }

        public void WriteTelemetrySnapshot()
        {
            if (_written || string.IsNullOrWhiteSpace(_outputPath))
                return;

            _written = true;

            double sampleDuration = Math.Max(0.001, Time.realtimeSinceStartupAsDouble - _sampleStartRealtime);
            var sorted = _frameDeltas.OrderBy(d => d).ToArray();

            double avgFps = sorted.Length > 0 ? sorted.Length / sorted.Sum() : 0.0;
            double maxDt = sorted.Length > 0 ? sorted[sorted.Length - 1] : 0.0;
            double minFps = maxDt > 0.00001 ? 1.0 / maxDt : 0.0;
            double p95Dt = sorted.Length > 0 ? sorted[Mathf.Clamp((int)(sorted.Length * 0.95f), 0, sorted.Length - 1)] : 0.0;

            var player = FindAnyObjectByType<Docencia2FirstPersonPlayer>();
            var cc = player != null ? player.GetComponent<CharacterController>() : null;
            bool spawnValid = player != null && cc != null && cc.enabled && player.playerCamera != null && player.transform.position.y > -0.5f;

            int interactiveDoors = FindObjectsByType<Docencia2InteractiveDoor>(FindObjectsInactive.Include).Length;
            int paDoors = FindObjectsByType<Docencia2PADoor>(FindObjectsInactive.Include).Length;

            // Evaluacion formal de los SLO/SLA acordados para VAELITH
            bool slaOk =
                _exceptionCount == 0 &&
                _errorCount == 0 &&
                spawnValid &&
                interactiveDoors >= 60 &&
                avgFps >= 30.0 &&
                _bootCompletedRealtime <= 20.0;

            var report = new TelemetryReport
            {
                project = "VAELITH: After You Wake — Demo Las 3:17",
                scene = SceneManager.GetActiveScene().path,
                unityVersion = Application.unityVersion,
                graphicsDeviceName = SystemInfo.graphicsDeviceName,
                graphicsDeviceType = SystemInfo.graphicsDeviceType.ToString(),
                operatingSystem = SystemInfo.operatingSystem,
                systemMemoryMB = SystemInfo.systemMemorySize,
                bootTimeSeconds = Math.Round(_bootCompletedRealtime, 3),
                sampleDurationSeconds = Math.Round(sampleDuration, 3),
                sampledFrames = sorted.Length,
                avgFps = Math.Round(avgFps, 2),
                minFps = Math.Round(minFps, 2),
                p95FrameTimeMs = Math.Round(p95Dt * 1000.0, 2),
                maxFrameTimeMs = Math.Round(maxDt * 1000.0, 2),
                ramTotalAllocatedPeakMB = Math.Round(_peakAllocMB, 2),
                ramTotalReservedPeakMB = Math.Round(_peakReservedMB, 2),
                monoHeapPeakMB = Math.Round(_peakMonoMB, 2),
                gfxDriverAllocatedPeakMB = Math.Round(_peakGfxMB, 2),
                interactiveDoorsCount = interactiveDoors,
                paDoorsCount = paDoors,
                playerSpawnValid = spawnValid,
                modeToggleVerified = _modeToggleVerified,
                errorCount = _errorCount,
                exceptionCount = _exceptionCount,
                slaPassed = slaOk,
                generatedAtUtc = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture)
            };

            string dir = Path.GetDirectoryName(Path.GetFullPath(_outputPath));
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);

            File.WriteAllText(_outputPath, JsonUtility.ToJson(report, true), Encoding.UTF8);
            Debug.Log($"[Docencia2ReleaseTelemetry] Reporte escrito en {_outputPath} | AvgFPS={report.avgFps:F1} | p95={report.p95FrameTimeMs:F2}ms | RAM={report.ramTotalAllocatedPeakMB:F1}MB | SLA={report.slaPassed}");
        }
    }
}
