using System;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.Rendering;

public static class BuildCI
{
    [Serializable]
    private sealed class BuildMetricsReport
    {
        public string target = "StandaloneWindows64";
        public string graphicsApi = "Direct3D11";
        public string scene = "";
        public string outputExecutable = "";
        public string result = "";
        public ulong totalSizeBytes;
        public long executableSizeBytes;
        public double buildDurationSeconds;
        public int totalWarnings;
        public int totalErrors;
        public string builtAtUtc = "";
    }

    public static void Windows()
    {
        const string scene = "Assets/Scenes/Exterior_Docencia2.unity";
        if (!File.Exists(scene))
            throw new Exception("Falta la escena principal: " + scene);

        string playerDir = Path.GetFullPath("artifacts/player");
        if (Directory.Exists(playerDir))
            Directory.Delete(playerDir, true);
        Directory.CreateDirectory(playerDir);

        string exePath = Path.Combine(playerDir, "VAELITH.exe");

        // En Windows QA usamos Direct3D11 (alineado con Editor y perfil URP aprobado)
        GraphicsDeviceType[] prevApis = PlayerSettings.GetGraphicsAPIs(BuildTarget.StandaloneWindows64);
        bool prevAuto = PlayerSettings.GetUseDefaultGraphicsAPIs(BuildTarget.StandaloneWindows64);
        try
        {
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.StandaloneWindows64, false);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.StandaloneWindows64, new[] { GraphicsDeviceType.Direct3D11 });

            var options = new BuildPlayerOptions
            {
                scenes = new[] { scene },
                locationPathName = exePath,
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None
            };

            BuildReport report = BuildPipeline.BuildPlayer(options);
            BuildSummary summary = report.summary;
            Debug.Log($"[BuildCI] Result={summary.result} | SizeBytes={summary.totalSize} | Time={summary.totalTime.TotalSeconds:F2}s | Warnings={summary.totalWarnings} | Errors={summary.totalErrors}");

            long exeBytes = File.Exists(exePath) ? new FileInfo(exePath).Length : 0L;
            var metrics = new BuildMetricsReport
            {
                scene = scene,
                outputExecutable = exePath,
                result = summary.result.ToString(),
                totalSizeBytes = summary.totalSize,
                executableSizeBytes = exeBytes,
                buildDurationSeconds = Math.Round(summary.totalTime.TotalSeconds, 2),
                totalWarnings = summary.totalWarnings,
                totalErrors = summary.totalErrors,
                builtAtUtc = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture)
            };

            Directory.CreateDirectory("artifacts");
            File.WriteAllText("artifacts/build_report.json", JsonUtility.ToJson(metrics, true), Encoding.UTF8);

            if (summary.result != BuildResult.Succeeded)
                throw new Exception($"La compilacion StandaloneWindows64 fallo: {summary.result} (errores: {summary.totalErrors})");
        }
        finally
        {
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.StandaloneWindows64, prevAuto);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.StandaloneWindows64, prevApis);
        }
    }
}
