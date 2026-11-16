using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.Rendering;

public static class BuildCI
{
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

        // En Windows QA usamos Direct3D11 (alineado con Editor)
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
