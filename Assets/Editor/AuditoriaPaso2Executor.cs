using System;
using System.IO;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Vaelith.Docencia2;
using Object = UnityEngine.Object;

public static class AuditoriaPaso2Executor
{
    public const string ScenePath = "Assets/Scenes/Exterior_Docencia2.unity";
    public const string BackupScenePath = @"C:/Users/faust/OneDrive/Desktop/VAELITH/backup_auditoria/Exterior_Docencia2_backup_auditoria.unity";
    public const string OutDir = @"C:/Users/faust/OneDrive/Desktop/VAELITH/FaseA_Correcciones/Auditoria_Paso2";

    static Vector3 W(float x, float y, float z) => Docencia2InteriorBuilder.PlanToUnityPos(x, y, z);
    static string PathOf(Transform t) => t.parent ? PathOf(t.parent) + "/" + t.name : t.name;

    public static void ExecuteAllMenu()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            UnityEngine.Debug.LogWarning("[AuditoriaPaso2] Sal de Play Mode primero para ejecutar Paso 2.");
            return;
        }
        ExecuteAllBatchesAndStartPlayVerify();
    }

    public static void ExecuteAllBatchesAndStartPlayVerify()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            UnityEngine.Debug.LogWarning("[AuditoriaPaso2] Sal de Play Mode primero.");
            return;
        }

        Directory.CreateDirectory(OutDir);
        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        var switcher = Object.FindAnyObjectByType<Docencia2ModeSwitcher>();
        if (switcher != null) switcher.ApplyMode(Docencia2VisualMode.Noche, false);
        foreach (var tracker in Object.FindObjectsByType<Docencia2InteriorModeTracker>(FindObjectsInactive.Include))
            tracker.SendMessage("Update");

        // ====================================================================
        // 0. CAPTURAS "ANTES" (Lote 1 y Lote 2)
        // ====================================================================
        CapturePerspectiveShot("Lote1_Antes_PisoEscaleraPB.png",
            W(11.30f, 2.25f, 19.65f), W(4.80f, 0.45f, 18.70f), 62f);

        CapturePerspectiveShot("Lote2_Antes_ZocloA3_PA.png",
            W(2.40f, 4.45f, 6.20f), W(2.00f, 3.65f, 7.71f), 58f);

        CapturePerspectiveShot("Lote2_Antes_BarandalDescanso.png",
            W(5.60f, 3.35f, 19.29f), W(2.36f, 2.35f, 19.29f), 62f);

        var appliedLog = new List<string>
        {
            "=== REPORTE DE OBJETOS Y ARCHIVOS MODIFICADOS EN PASO 2 (AUDITORÍA DOCENCIA 2) ===",
            $"Fecha: {DateTime.Now:yyyy-MM-dd HH:mm:ss}",
            ""
        };

        // ====================================================================
        // LOTE 1 (CRÍTICO — H01): Reconstruir Piso_Cubo_Escaleras_PB (x = 0.36..8.10 m)
        // ====================================================================
        var pb = GameObject.Find("PB_Interior");
        var pa = GameObject.Find("PA_Interior");
        Material mPiso = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Docencia2/Interior/Mat_Int_Piso_LosetaGris_PB.mat");

        if (pb != null && mPiso != null)
        {
            Transform stairZonePB = pb.transform.Find("22_Cubo_Escaleras_PB");
            if (stairZonePB != null)
            {
                Transform oldPiso = stairZonePB.Find("Piso_Cubo_Escaleras_PB");
                string oldBounds = oldPiso != null && oldPiso.GetComponent<Renderer>() != null
                    ? oldPiso.GetComponent<Renderer>().bounds.ToString("F3")
                    : "N/A";
                if (oldPiso != null) Object.DestroyImmediate(oldPiso.gameObject);

                var newPiso = Docencia2InteriorBuilder.CreatePlanBox(
                    "Piso_Cubo_Escaleras_PB", stairZonePB,
                    0.36f, 8.10f, 0.18f, 0.22f, 15.43f, 23.14f, mPiso, 0.004f, true);
                newPiso.layer = 8;
                var rPiso = newPiso.GetComponent<MeshRenderer>();
                if (rPiso != null)
                {
                    rPiso.renderingLayerMask = 3u;
                    rPiso.shadowCastingMode = ShadowCastingMode.Off;
                }
                string newBounds = rPiso != null ? rPiso.bounds.ToString("F3") : "N/A";
                appliedLog.Add($"[LOTE 1 - H01 CRÍTICO] Reconstruido PB_Interior/22_Cubo_Escaleras_PB/Piso_Cubo_Escaleras_PB de x=0.36..7.10 m a x=0.36..8.10 m (z=15.43..23.14 m, Y=0.18..0.22 m). Bounds antes={oldBounds} -> después={newBounds}.");
            }
        }

        // ====================================================================
        // LOTE 2 (MEDIO/MENOR — H05 y H06): Eliminar duplicados exactos coplanares
        // ====================================================================
        string[] exactDuplicatePaths = new[]
        {
            "PA_Interior/Cubiculo_A3/Cierre_Sec7/Zoclo_A",
            "PA_Interior/Cubiculo_A3/Cierre_Sec7/Zoclo_B",
            "PB_Interior/22_Cubo_Escaleras_PB/Escalera_En_U_Concreto_y_Barandal/Barandal_Descanso_Norte/Barandal_Descanso_Norte_SoporteTubular_12d",
            "PB_Interior/22_Cubo_Escaleras_PB/Escalera_En_U_Concreto_y_Barandal/Barandal_Descanso_Sur/Barandal_Descanso_Sur_SoporteTubular_12d"
        };

        foreach (string dupPath in exactDuplicatePaths)
        {
            var go = GameObject.Find(dupPath);
            if (go != null)
            {
                Object.DestroyImmediate(go);
                appliedLog.Add($"[LOTE 2 - H05/H06 DUPLICADO EXACTO] Eliminado duplicado coplanar: {dupPath}");
            }
        }

        // ====================================================================
        // LOTE 3 (MENOR — H09): Eliminar nodos hoja vacíos en PB_Interior (sin hijos y solo Transform)
        // ====================================================================
        int removedEmptyLeaves = 0;
        var removedLeafNames = new List<string>();
        if (pb != null)
        {
            // Hacer barrido de hojas vacías
            bool foundAny = true;
            while (foundAny)
            {
                foundAny = false;
                foreach (var t in pb.GetComponentsInChildren<Transform>(true))
                {
                    if (t == null || t == pb.transform) continue;
                    if (t.childCount == 0)
                    {
                        var comps = t.GetComponents<Component>();
                        if (comps.Length == 1 && comps[0] is Transform)
                        {
                            string p = PathOf(t);
                            removedLeafNames.Add(p);
                            Object.DestroyImmediate(t.gameObject);
                            removedEmptyLeaves++;
                            foundAny = true;
                            break;
                        }
                    }
                }
            }
        }
        appliedLog.Add($"[LOTE 3 - H09 LIMPIEZA JERARQUÍA] Eliminados {removedEmptyLeaves} objetos hoja vacíos (0 hijos, solo Transform) en PB_Interior.");
        File.WriteAllLines(OutDir + "/nodos_vacios_eliminados_h09.txt", removedLeafNames);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();

        // ====================================================================
        // CAPTURAS "DESPUÉS" (Lote 1 y Lote 2)
        // ====================================================================
        CapturePerspectiveShot("Lote1_Despues_PisoEscaleraPB.png",
            W(11.30f, 2.25f, 19.65f), W(4.80f, 0.45f, 18.70f), 62f);

        CapturePerspectiveShot("Lote2_Despues_ZocloA3_PA.png",
            W(2.40f, 4.45f, 6.20f), W(2.00f, 3.65f, 7.71f), 58f);

        CapturePerspectiveShot("Lote2_Despues_BarandalDescanso.png",
            W(5.60f, 3.35f, 19.29f), W(2.36f, 2.35f, 19.29f), 62f);

        // ====================================================================
        // REGRESIÓN EXTERIOR (3 Cámaras en NOCHE y TERROR contra backup_auditoria)
        // ====================================================================
        RunExteriorRegressionAgainstAuditBackup();

        File.WriteAllLines(OutDir + "/resumen_lotes_aplicados.txt", appliedLog);

        // ====================================================================
        // INICIAR VERIFICACIÓN EN PLAY MODE (Consola 0 warnings de audio, FPS >= 30)
        // ====================================================================
        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        SessionState.SetBool("AUDITORIA_PASO2_PLAY_VERIFY", true);
        EditorApplication.isPlaying = true;
    }

    static void CapturePerspectiveShot(string fileName, Vector3 camPos, Vector3 lookTarget, float fov)
    {
        var rig = new GameObject("TEMP_AuditPaso2_Cam");
        var cam = rig.AddComponent<Camera>();
        cam.enabled = false;
        cam.nearClipPlane = 0.05f;
        cam.farClipPlane = 120f;
        cam.fieldOfView = fov;
        cam.cullingMask = ~0;
        var camData = rig.AddComponent<UniversalAdditionalCameraData>();
        camData.renderPostProcessing = true;

        // Añadir luz de trabajo suave solo para el encuadre del zoclo A3 si estamos en el rincón de Cubículo A3
        GameObject workLightGo = null;
        if (fileName.Contains("ZocloA3"))
        {
            workLightGo = new GameObject("TEMP_WorkLight");
            workLightGo.transform.SetParent(rig.transform, false);
            workLightGo.transform.position = camPos + Vector3.up * 0.25f;
            var wl = workLightGo.AddComponent<Light>();
            wl.type = LightType.Point;
            wl.range = 6.0f;
            wl.intensity = 1.8f;
            wl.shadows = LightShadows.None;
        }

        rig.transform.position = camPos;
        rig.transform.LookAt(lookTarget);

        var rt = new RenderTexture(1920, 1080, 24);
        var prevActive = RenderTexture.active;
        var tex = new Texture2D(1920, 1080, TextureFormat.RGB24, false);
        cam.targetTexture = rt;
        for (int i = 0; i < 3; i++) cam.Render();
        RenderTexture.active = rt;
        tex.ReadPixels(new Rect(0, 0, 1920, 1080), 0, 0);
        tex.Apply();
        File.WriteAllBytes(Path.Combine(OutDir, fileName), tex.EncodeToPNG());

        cam.targetTexture = null;
        RenderTexture.active = prevActive;
        Object.DestroyImmediate(tex);
        rt.Release();
        Object.DestroyImmediate(rt);
        Object.DestroyImmediate(rig);
    }

    public static void RunExteriorRegressionAgainstAuditBackup()
    {
        string tempBackupScene = "Assets/Scenes/Temp_Backup_Auditoria_Compare.unity";
        File.Copy(BackupScenePath, tempBackupScene, true);
        AssetDatabase.ImportAsset(tempBackupScene);

        CaptureThreeExteriorCameras(tempBackupScene, "Antes");
        CaptureThreeExteriorCameras(ScenePath, "Despues");

        AssetDatabase.DeleteAsset(tempBackupScene);

        var rows = new List<string> { "camara,modo,diff_max_canal_0_255,estado" };
        string[] cams = { "Camara_01_Frontal_Baja", "Camara_02_Aerea_Azotea", "Camara_03_Esquina_Contrafuerte" };
        foreach (string mode in new[] { "Noche", "Terror" })
        {
            foreach (string c in cams)
            {
                string fA = Path.Combine(OutDir, $"Exterior_Antes_{mode}_{c}.png");
                string fB = Path.Combine(OutDir, $"Exterior_Despues_{mode}_{c}.png");
                int maxDiff = ComparePngMaxDiff(fA, fB);
                rows.Add($"{c},{mode},{maxDiff},{(maxDiff == 0 ? "INTACTO_0_CAMBIOS" : "DIFERENCIA_" + maxDiff)}");
            }
        }
        File.WriteAllLines(Path.Combine(OutDir, "regresion_exterior_paso2.csv"), rows);
    }

    static void CaptureThreeExteriorCameras(string scenePath, string prefix)
    {
        EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
        var sw = Object.FindAnyObjectByType<Docencia2ModeSwitcher>();
        foreach (var mode in new[] { Docencia2VisualMode.Noche, Docencia2VisualMode.Terror })
        {
            sw.ApplyMode(mode, false);
            foreach (var tracker in Object.FindObjectsByType<Docencia2InteriorModeTracker>(FindObjectsInactive.Include))
                tracker.SendMessage("Update");

            foreach (var camera in GameObject.Find("Camaras_Referencia_Docencia2").GetComponentsInChildren<Camera>(true))
            {
                var rt = new RenderTexture(1920, 1080, 24);
                var oldTarget = camera.targetTexture;
                var old = RenderTexture.active;
                var texture = new Texture2D(1920, 1080, TextureFormat.RGB24, false);
                camera.targetTexture = rt;
                for (int i = 0; i < 3; i++) camera.Render();
                RenderTexture.active = rt;
                texture.ReadPixels(new Rect(0, 0, 1920, 1080), 0, 0);
                texture.Apply();
                File.WriteAllBytes(Path.Combine(OutDir, $"Exterior_{prefix}_{mode}_{camera.name}.png"), texture.EncodeToPNG());
                camera.targetTexture = oldTarget;
                RenderTexture.active = old;
                Object.DestroyImmediate(texture);
                rt.Release();
                Object.DestroyImmediate(rt);
            }
        }
    }

    static int ComparePngMaxDiff(string pathA, string pathB)
    {
        var texA = new Texture2D(2, 2);
        var texB = new Texture2D(2, 2);
        texA.LoadImage(File.ReadAllBytes(pathA));
        texB.LoadImage(File.ReadAllBytes(pathB));
        Color32[] a = texA.GetPixels32();
        Color32[] b = texB.GetPixels32();
        int max = 0;
        int len = Mathf.Min(a.Length, b.Length);
        for (int i = 0; i < len; i++)
        {
            int dr = Mathf.Abs(a[i].r - b[i].r);
            int dg = Mathf.Abs(a[i].g - b[i].g);
            int db = Mathf.Abs(a[i].b - b[i].b);
            if (dr > max) max = dr;
            if (dg > max) max = dg;
            if (db > max) max = db;
        }
        Object.DestroyImmediate(texA);
        Object.DestroyImmediate(texB);
        return max;
    }
}

[InitializeOnLoad]
public static class AuditoriaPaso2PlayBoot
{
    static AuditoriaPaso2PlayBoot()
    {
        EditorApplication.update += OnEditorUpdate;
    }

    static void OnEditorUpdate()
    {
        if (SessionState.GetBool("AUDITORIA_PASO2_PLAY_VERIFY", false) && EditorApplication.isPlaying && !EditorApplication.isCompiling)
        {
            SessionState.SetBool("AUDITORIA_PASO2_PLAY_VERIFY", false);
            var go = new GameObject("TEMP_AuditoriaPaso2_PlayVerifier");
            go.AddComponent<AuditoriaPaso2PlayVerifier>();
        }
    }
}

public class AuditoriaPaso2PlayVerifier : MonoBehaviour
{
    List<string> _consoleLogs = new List<string>();
    int _audioWarnings = 0;
    int _errorsOrExceptions = 0;

    void OnLogMessage(string condition, string stackTrace, LogType type)
    {
        if (condition.Contains("Account API did not become accessible")) return; // Aviso interno de Unity Cloud sin conexión
        if (condition.Contains("disabled audio source"))
        {
            _audioWarnings++;
            _consoleLogs.Add($"[AUDIO_WARN] {condition}");
        }
        else if (type == LogType.Error || type == LogType.Exception || type == LogType.Warning)
        {
            if (type == LogType.Error || type == LogType.Exception) _errorsOrExceptions++;
            _consoleLogs.Add($"[{type}] {condition}");
        }
    }

    IEnumerator Start()
    {
        string outDir = AuditoriaPaso2Executor.OutDir;
        Directory.CreateDirectory(outDir);
        Application.logMessageReceived += OnLogMessage;
        Application.runInBackground = true;
        QualitySettings.vSyncCount = 0;
        Application.targetFrameRate = -1;

        yield return null;
        yield return null;

        var player = Docencia2FirstPersonPlayer.Instance ?? Object.FindAnyObjectByType<Docencia2FirstPersonPlayer>();
        var switcher = Object.FindAnyObjectByType<Docencia2ModeSwitcher>();
        var darkness = Docencia2TerrorDarknessController.Instance ?? Object.FindAnyObjectByType<Docencia2TerrorDarknessController>();

        // Probar también que MenuToggleNightTerror funciona en Play Mode sin lanzar InvalidOperationException
        Docencia2SceneBuilder.MenuToggleNightTerror();
        yield return null;
        Docencia2SceneBuilder.MenuToggleNightTerror();
        yield return null;

        // Re-evaluar las 70 puertas
        player.RunDoorAuditAndPopulateList(saveFiles: true);

        // Medir FPS en Play Mode (NOCHE y TERROR) en 4 zonas incluyendo el pie de escalera PB recién corregido
        var fpsRows = new List<string> { "modo,ubicacion,resolucion,fps_promedio,fps_p95_lento,luces_activas,luces_con_sombra" };
        var spots = new (string name, float px, float py, float pz, float yaw)[]
        {
            ("PB_Pie_Escalera_Corregido", 9.80f, 0.20f, 19.29f, -90f),
            ("PB_Vestibulo_Entrada", 24.80f, 0.20f, 19.29f, 0f),
            ("PB_Pasillo_Canceles", 14.24f, 0.20f, 13.50f, -90f),
            ("PA_Desembarque_Escalera", 12.50f, 3.606f, 19.29f, -90f)
        };

        var rt = new RenderTexture(1920, 1080, 24);
        player.playerCamera.targetTexture = rt;

        foreach (bool isTerror in new[] { false, true })
        {
            switcher.ApplyMode(isTerror ? Docencia2VisualMode.Terror : Docencia2VisualMode.Noche, false);
            if (darkness != null) darkness.SyncState();
            player.SetFlashlightState(true, false);
            yield return null;

            foreach (var spot in spots)
            {
                player.TeleportToPlan(spot.px, spot.py, spot.pz, spot.yaw);
                for (int w = 0; w < 10; w++)
                {
                    player.playerCamera.Render();
                    yield return null;
                }

                var samples = new List<double>();
                long last = Stopwatch.GetTimestamp();
                for (int i = 0; i < 60; i++)
                {
                    player.playerCamera.Render();
                    yield return null;
                    long now = Stopwatch.GetTimestamp();
                    samples.Add((now - last) / (double)Stopwatch.Frequency);
                    last = now;
                }
                samples.Sort();
                double avgFps = 60.0 / samples.Sum();
                double p95Fps = 1.0 / samples[56];

                var activeLights = Object.FindObjectsByType<Light>(FindObjectsInactive.Exclude)
                    .Where(l => l.enabled && l.gameObject.activeInHierarchy).ToArray();
                int shadowLights = activeLights.Count(l => l.shadows != LightShadows.None);

                fpsRows.Add(FormattableString.Invariant(
                    $"{(isTerror ? "TERROR" : "NOCHE")},{spot.name},1920x1080,{avgFps:F1},{p95Fps:F1},{activeLights.Length},{shadowLights}"));
            }
        }

        // Capturar vista en Play Mode del pie de escalera PB en modo TERROR con linterna
        switcher.ApplyMode(Docencia2VisualMode.Terror, false);
        if (darkness != null) darkness.SyncState();
        player.SetFlashlightState(true, false);
        player.TeleportToPlan(10.60f, 0.20f, 19.29f, 0f);
        player.transform.LookAt(Docencia2InteriorBuilder.PlanToUnityPos(5.20f, 0.45f, 18.70f));
        player.transform.rotation = Quaternion.Euler(0f, player.transform.eulerAngles.y, 0f);
        player.playerCamera.transform.localRotation = Quaternion.Euler(14f, 0f, 0f);
        yield return null;

        player.playerCamera.Render();
        var prev = RenderTexture.active;
        RenderTexture.active = rt;
        var tex = new Texture2D(1920, 1080, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, 1920, 1080), 0, 0);
        tex.Apply();
        File.WriteAllBytes(Path.Combine(outDir, "Lote1_Despues_PisoEscaleraPB_TerrorLinterna.png"), tex.EncodeToPNG());
        RenderTexture.active = prev;
        Object.DestroyImmediate(tex);

        player.playerCamera.targetTexture = null;
        rt.Release();
        Object.DestroyImmediate(rt);

        switcher.ApplyMode(Docencia2VisualMode.Noche, false);
        if (darkness != null) darkness.RestoreOriginalEnvironment();
        player.TeleportToPlan(24.80f, 0.20f, 19.29f, 0f);

        File.WriteAllLines(Path.Combine(outDir, "fps_play_paso2.csv"), fpsRows);

        var consoleSummary = new List<string>
        {
            $"Advertencias 'Can not play a disabled audio source': {_audioWarnings}",
            $"Errores o excepciones en Play Mode (incluyendo MenuToggleNightTerror): {_errorsOrExceptions}",
            $"Total de mensajes relevantes en consola: {_consoleLogs.Count}"
        };
        consoleSummary.AddRange(_consoleLogs);
        File.WriteAllLines(Path.Combine(outDir, "consola_play_paso2.txt"), consoleSummary);
        File.WriteAllText(Path.Combine(outDir, "paso2_completado_ok.txt"), DateTime.Now.ToString("O"));

        Application.logMessageReceived -= OnLogMessage;
        EditorApplication.Exit(0);
    }
}
