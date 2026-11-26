using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Vaelith.Docencia2;

/// <summary>
/// Captura un set completo 1920x1080 del edificio Docencia 2 por fuera (todos los lados, esquinas, aérea y modo terror)
/// y por dentro (vestíbulo, pasillos PB/PA, escalera, aulas con pupitres originales, auditorio, baño y modo terror),
/// listo para pasar a Claude / Dreamina AI para generación de prompts y tráiler.
/// Sin [MenuItem] para conservar el menú VAELITH consolidado a 1 sola opción.
/// </summary>
public static class VaelithTrailerPhotoCapture
{
    private const string ScenePath = "Assets/Scenes/Exterior_Docencia2.unity";
    private const string OutDir = "Fotos_Edificio_Trailer";

    public static void RestoreDesksAndCaptureAll()
    {
        // 1. Restaurar primero todos los pupitres originales sin errores y guardar escena limpia
        MesabancoIntegrator.IntegrateAll();

        // 2. Capturar fotos en alta resolución (sin modificar ni guardar cambios temporales de iluminación en la escena)
        CaptureAll();
    }

    public static void IntegrateNewTestigoAndCaptureTerrorPack()
    {
        // 1. Integrar la nueva La Testigo (2.30 m, Mixamo + CreepyIdle + 4K PBR) y guardar escena
        LaTestigoIntegrator.IntegrateAll();

        // 2. Capturar 12 fotos oscuras terroríficas del interior + refrescar pasillos con la nueva La Testigo
        CaptureDarkTerrorInteriorPack();
    }

    public static void CaptureDarkTerrorInteriorPack()
    {
        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        string fullOutDir = Path.GetFullPath(OutDir);
        Directory.CreateDirectory(fullOutDir);

        var switcher = UnityEngine.Object.FindAnyObjectByType<Docencia2ModeSwitcher>();
        Docencia2VisualMode originalMode = switcher != null ? switcher.modoActual : Docencia2VisualMode.Terror;

        var player = UnityEngine.Object.FindAnyObjectByType<Docencia2FirstPersonPlayer>();
        GameObject protaGo = player != null && player.rigDriver != null ? player.rigDriver.gameObject : null;
        bool prevProtaActive = protaGo != null && protaGo.activeSelf;
        if (protaGo != null) protaGo.SetActive(false);

        var allCams = UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsInactive.Include);
        var prevCamEnabled = new bool[allCams.Length];
        for (int i = 0; i < allCams.Length; i++)
        {
            prevCamEnabled[i] = allCams[i].enabled;
            allCams[i].enabled = false;
        }

        // Posar a La Testigo en su pose inquietante CreepyIdle (brazos caídos + cabeza ladeada) solo en memoria para las capturas
        GameObject testigoGo = GameObject.Find("PB_Interior/LaTestigo") ?? GameObject.Find("LaTestigo");
        if (testigoGo != null)
        {
            var anim = testigoGo.GetComponent<Animator>();
            if (anim != null && anim.isHuman)
            {
                void RotBone(HumanBodyBones b, Vector3 euler)
                {
                    Transform t = anim.GetBoneTransform(b);
                    if (t != null) t.localRotation = t.localRotation * Quaternion.Euler(euler);
                }
                // Aproximar brazos caídos y cabeza ladeada inquietante para las fotos en EditMode (sin guardar a disco)
                RotBone(HumanBodyBones.Spine, new Vector3(4f, 0f, 0f));
                RotBone(HumanBodyBones.Chest, new Vector3(6f, 0f, 0f));
                RotBone(HumanBodyBones.Head, new Vector3(5f, 3f, 15f));
                RotBone(HumanBodyBones.LeftUpperArm, new Vector3(0f, 0f, -34f));
                RotBone(HumanBodyBones.RightUpperArm, new Vector3(0f, 0f, 34f));
                RotBone(HumanBodyBones.LeftLowerArm, new Vector3(0f, -10f, -8f));
                RotBone(HumanBodyBones.RightLowerArm, new Vector3(0f, 10f, 8f));
            }
        }

        var camGo = new GameObject("__TEMP_TERROR_PHOTO_CAM__");
        var cam = camGo.AddComponent<Camera>();
        cam.nearClipPlane = 0.08f;
        cam.farClipPlane = 200f;
        cam.clearFlags = CameraClearFlags.Skybox;
        cam.cullingMask = ~0;
        var camData = camGo.AddComponent<UniversalAdditionalCameraData>();
        camData.renderPostProcessing = true;
        camData.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
        camData.antialiasingQuality = AntialiasingQuality.High;

        // Linterna realista de celular acoplada a la cámara (desplazada ligeramente a la derecha como mano del jugador)
        var flashGo = new GameObject("__TEMP_HAND_FLASHLIGHT__");
        flashGo.transform.SetParent(camGo.transform, false);
        flashGo.transform.localPosition = new Vector3(0.22f, -0.14f, 0.12f);
        flashGo.transform.localRotation = Quaternion.Euler(1.5f, -1.8f, 0f);
        var flash = flashGo.AddComponent<Light>();
        flash.type = LightType.Spot;
        flash.color = new Color(0.88f, 0.94f, 1.0f, 1f);
        flash.intensity = 42f;
        flash.range = 14.0f;
        flash.spotAngle = 48f;
        flash.innerSpotAngle = 16f;
        flash.shadows = LightShadows.Soft;
        flash.shadowStrength = 0.95f;
        flash.cullingMask = ~0;
        var flashUld = flashGo.AddComponent<UniversalAdditionalLightData>();
        flashUld.renderingLayers = uint.MaxValue;

        // Rebote muy tenue de linterna para que las siluetas en sombra no sean negro plano sin perder el terror
        var spillGo = new GameObject("__TEMP_HAND_SPILL__");
        spillGo.transform.SetParent(flashGo.transform, false);
        spillGo.transform.localPosition = Vector3.zero;
        spillGo.transform.localRotation = Quaternion.identity;
        var spill = spillGo.AddComponent<Light>();
        spill.type = LightType.Spot;
        spill.color = new Color(0.62f, 0.76f, 0.88f, 1f);
        spill.intensity = 2.6f;
        spill.range = 8.5f;
        spill.spotAngle = 78f;
        spill.innerSpotAngle = 15f;
        spill.shadows = LightShadows.None;
        spill.cullingMask = ~0;
        var spillUld = spillGo.AddComponent<UniversalAdditionalLightData>();
        spillUld.renderingLayers = uint.MaxValue;

        try
        {
            // Primero refrescar 10_Interior_PB_Pasillo_Central_Canceles.png (Modo Noche) con la nueva La Testigo
            if (switcher != null) switcher.ApplyMode(Docencia2VisualMode.Noche, false);
            SetActiveByNameContains(scene, "Iluminacion_NOCHE", true);
            flash.enabled = false;
            spill.enabled = false;
            ShotPlan(cam, fullOutDir, "10_Interior_PB_Pasillo_Central_Canceles.png",
                14.24f, 1.80f, 14.6f, 14.24f, 1.40f, 38.0f, 64f);

            // ========== MODO TERROR REAL + FOTOS OSCURAS TERRORÍFICAS DEL INTERIOR ==========
            if (switcher != null) switcher.ApplyMode(Docencia2VisualMode.Terror, false);
            RenderSettings.ambientSkyColor = new Color(0.022f, 0.042f, 0.056f, 1f);
            RenderSettings.ambientEquatorColor = new Color(0.028f, 0.050f, 0.062f, 1f);
            RenderSettings.ambientGroundColor = new Color(0.018f, 0.032f, 0.040f, 1f);

            // 19 (refrescada) y 20: Pasillo central oscuro con la NUEVA La Testigo (2.30 m) recortada en contraluz terrorífico
            flash.enabled = false;
            spill.enabled = false;
            ShotPlan(cam, fullOutDir, "19_Terror_Interior_Pasillo_Central.png",
                14.24f, 1.55f, 14.8f, 14.24f, 1.40f, 38.0f, 60f);

            // 20: Encuentro tenso a media distancia con linterna apuntando al pasillo hacia La Testigo (2.30 m)
            flash.enabled = true;
            spill.enabled = true;
            flash.intensity = 38f;
            flash.range = 15.0f;
            flash.spotAngle = 44f;
            ShotPlan(cam, fullOutDir, "20_Terror_PB_Pasillo_LaTestigo_Linterna.png",
                14.24f, 1.48f, 17.5f, 14.24f, 1.48f, 25.0f, 56f);

            // 21: Encuentro cercano (altura ojos 1.48 m mirando ligeramente hacia arriba a La Testigo 2.30 m!)
            flash.intensity = 30f;
            flash.range = 10.0f;
            flash.spotAngle = 46f;
            ShotPlan(cam, fullOutDir, "21_Terror_PB_LaTestigo_Encuentro_Cercano_230m.png",
                14.55f, 1.48f, 20.4f, 14.24f, 1.68f, 24.8f, 46f);

            // 22: Retrato atmosférico terrorífico de la nueva La Testigo (2.30 m) en el pasillo oscuro (ideal para prompts de personaje en Dreamina)
            flash.intensity = 22f;
            flash.range = 8.0f;
            flash.spotAngle = 54f;
            spill.intensity = 3.2f;
            ShotPlan(cam, fullOutDir, "22_Terror_Retrato_Nueva_LaTestigo_230m.png",
                14.80f, 1.58f, 21.8f, 14.24f, 1.50f, 24.8f, 42f);

            // 23: Vestíbulo de entrada oscuro con haz de linterna
            flash.intensity = 44f;
            flash.range = 15.0f;
            flash.spotAngle = 48f;
            spill.intensity = 2.4f;
            ShotPlan(cam, fullOutDir, "23_Terror_PB_Vestibulo_Oscuro.png",
                25.4f, 1.55f, 19.3f, 13.5f, 1.35f, 19.3f, 64f);

            // 24: Escalera en U oscura y angustiante
            flash.intensity = 42f;
            ShotPlan(cam, fullOutDir, "24_Terror_PB_Escalera_Oscura.png",
                10.4f, 1.55f, 19.3f, 3.2f, 2.15f, 23.8f, 64f);

            // 25: Aula 3 oscura con pupitres originales iluminados por la linterna
            flash.intensity = 40f;
            ShotPlan(cam, fullOutDir, "25_Terror_PB_Aula_3_Oscura.png",
                10.4f, 1.60f, 47.1f, 17.5f, 0.90f, 52.6f, 62f);

            // 26: Auditorio oscuro enorme con 80 butacas vacías y escenario al fondo
            flash.intensity = 52f;
            flash.range = 18.0f;
            ShotPlan(cam, fullOutDir, "26_Terror_PB_Auditorio_Oscuro.png",
                18.2f, 2.05f, 25.8f, 24.8f, 0.30f, 36.8f, 64f);

            // 27: Baño oscuro y claustrofóbico con cabinas y espejo
            flash.intensity = 32f;
            flash.range = 10.0f;
            ShotPlan(cam, fullOutDir, "27_Terror_PB_Bano_Espejo_Oscuro.png",
                10.4f, 1.55f, 5.7f, 12.6f, 1.35f, 1.2f, 64f);

            // 28: Plaza Sur PB en penumbra con bancas y puertas de aulas cerradas
            flash.intensity = 45f;
            flash.range = 15.0f;
            ShotPlan(cam, fullOutDir, "28_Terror_PB_Plaza_Sur_Oscura.png",
                14.24f, 1.55f, 36.2f, 14.24f, 1.35f, 45.8f, 66f);

            // 29: Pasillo central Planta Alta completamente oscuro cortado por linterna
            flash.intensity = 48f;
            flash.range = 18.0f;
            ShotPlan(cam, fullOutDir, "29_Terror_PA_Pasillo_Central_Oscuro.png",
                14.24f, 5.05f, 18.0f, 14.24f, 4.85f, 41.0f, 64f);

            // 30: Aula 9 Planta Alta oscura con proyector colgando y pupitres vacíos
            flash.intensity = 40f;
            flash.range = 14.0f;
            ShotPlan(cam, fullOutDir, "30_Terror_PA_Aula_9_Oscura.png",
                18.1f, 5.05f, 23.9f, 25.8f, 4.35f, 29.5f, 62f);

            // 31: Zona de cubículos y canceles oscuros (PB Norte)
            flash.intensity = 40f;
            flash.range = 14.0f;
            ShotPlan(cam, fullOutDir, "31_Terror_PB_Cubiculos_Oscuros.png",
                11.8f, 1.55f, 11.2f, 4.5f, 1.30f, 3.2f, 64f);
        }
        finally
        {
            if (switcher != null) switcher.ApplyMode(originalMode, false);
            if (protaGo != null) protaGo.SetActive(prevProtaActive);
            for (int i = 0; i < allCams.Length; i++)
            {
                if (allCams[i] != null) allCams[i].enabled = prevCamEnabled[i];
            }
            UnityEngine.Object.DestroyImmediate(camGo);
        }

        Debug.Log($"[VaelithTrailerPhotoCapture] Pack de 12 fotos oscuras terroríficas + pasillos actualizados guardado en: {fullOutDir}");
    }

    public static void CaptureAll()
    {
        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        string fullOutDir = Path.GetFullPath(OutDir);
        Directory.CreateDirectory(fullOutDir);

        var switcher = UnityEngine.Object.FindAnyObjectByType<Docencia2ModeSwitcher>();
        Docencia2VisualMode originalMode = switcher != null ? switcher.modoActual : Docencia2VisualMode.Terror;

        // Ocultar temporalmente la protagonista en memoria para vistas arquitectónicas limpias de interiores
        var player = UnityEngine.Object.FindAnyObjectByType<Docencia2FirstPersonPlayer>();
        GameObject protaGo = player != null && player.rigDriver != null ? player.rigDriver.gameObject : null;
        bool prevProtaActive = protaGo != null && protaGo.activeSelf;
        if (protaGo != null) protaGo.SetActive(false);

        // Desactivar temporalmente otras cámaras de escena
        var allCams = UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsInactive.Include);
        var prevCamEnabled = new bool[allCams.Length];
        for (int i = 0; i < allCams.Length; i++)
        {
            prevCamEnabled[i] = allCams[i].enabled;
            allCams[i].enabled = false;
        }

        // Crear cámara fotográfica dedicada de alta calidad (URP + PostProcessing + SMAA High)
        var camGo = new GameObject("__TEMP_TRAILER_PHOTO_CAM__");
        var cam = camGo.AddComponent<Camera>();
        cam.nearClipPlane = 0.08f;
        cam.farClipPlane = 450f;
        cam.clearFlags = CameraClearFlags.Skybox;
        cam.cullingMask = ~0;
        var camData = camGo.AddComponent<UniversalAdditionalCameraData>();
        camData.renderPostProcessing = true;
        camData.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
        camData.antialiasingQuality = AntialiasingQuality.High;

        // Luz ambiental de apoyo suave temporal (solo para fotos exteriores de lados oscuros en modo Noche)
        var fillGo = new GameObject("__TEMP_ARCH_FILL_SUN__");
        var fillLight = fillGo.AddComponent<Light>();
        fillLight.type = LightType.Directional;
        fillLight.color = new Color(0.84f, 0.90f, 1.0f, 1f);
        fillLight.intensity = 0.72f;
        fillLight.shadows = LightShadows.None;
        fillGo.transform.rotation = Quaternion.Euler(36f, -35f, 0f);

        try
        {
            // ========== BLOQUE A: MODO NOCHE (Arquitectura 100% legible por fuera y por dentro) ==========
            if (switcher != null)
            {
                switcher.ApplyMode(Docencia2VisualMode.Noche, false);
            }
            // Asegurar que las luces interiores PB y PA estén encendidas y legibles
            SetActiveByNameContains(scene, "Iluminacion_NOCHE", true);

            // --- EXTERIOR: TODOS LOS LADOS (fillLight activo suavemente para ver cada muro/ventana con claridad) ---
            fillLight.enabled = true;
            fillGo.transform.rotation = Quaternion.Euler(36f, -35f, 0f);
            ShotWorld(cam, fullOutDir, "01_Exterior_Fachada_Principal_Este_Entrada.png",
                new Vector3(16.0f, 2.3f, -38.0f), new Vector3(4.0f, 3.6f, -12.0f), 42f);

            ShotWorld(cam, fullOutDir, "02_Exterior_Esquina_Noreste_Entrada_y_Norte.png",
                new Vector3(42.0f, 2.7f, -34.0f), new Vector3(5.0f, 3.6f, -4.0f), 40f);

            fillGo.transform.rotation = Quaternion.Euler(36f, 145f, 0f);
            ShotWorld(cam, fullOutDir, "03_Exterior_Fachada_Lateral_Norte.png",
                new Vector3(49.0f, 3.1f, 3.5f), new Vector3(22.0f, 3.6f, 0.0f), 42f);

            ShotWorld(cam, fullOutDir, "04_Exterior_Esquina_Noroeste_Norte_y_Oeste.png",
                new Vector3(42.0f, 3.1f, 34.0f), new Vector3(4.0f, 3.6f, 4.0f), 40f);

            ShotWorld(cam, fullOutDir, "05_Exterior_Fachada_Trasera_Oeste.png",
                new Vector3(-2.0f, 3.4f, 44.0f), new Vector3(0.0f, 3.6f, 12.0f), 44f);

            ShotWorld(cam, fullOutDir, "06_Exterior_Esquina_Suroeste_Sur_y_Oeste.png",
                new Vector3(-44.0f, 3.1f, 33.0f), new Vector3(-7.0f, 3.6f, 3.0f), 40f);

            fillGo.transform.rotation = Quaternion.Euler(36f, -125f, 0f);
            ShotWorld(cam, fullOutDir, "07_Exterior_Esquina_Sureste_Sur_y_Este.png",
                new Vector3(-44.0f, 2.9f, -33.0f), new Vector3(-7.0f, 3.6f, -4.0f), 40f);

            fillGo.transform.rotation = Quaternion.Euler(42f, -35f, 0f);
            ShotWorld(cam, fullOutDir, "08_Exterior_Vista_Aerea_General.png",
                new Vector3(45.0f, 26.5f, -42.0f), new Vector3(0.0f, 3.0f, 0.0f), 38f);

            // --- INTERIOR: TODAS LAS ZONAS CLAVE (sin luz solar exterior, usando las luminarias reales del edificio) ---
            fillLight.enabled = false;
            ShotPlan(cam, fullOutDir, "09_Interior_PB_Vestibulo_Entrada.png",
                25.2f, 1.85f, 19.3f, 14.2f, 1.50f, 19.3f, 64f);

            ShotPlan(cam, fullOutDir, "10_Interior_PB_Pasillo_Central_Canceles.png",
                14.24f, 1.80f, 14.6f, 14.24f, 1.40f, 38.0f, 64f);

            ShotPlan(cam, fullOutDir, "11_Interior_PB_Escalera_U.png",
                9.9f, 1.85f, 19.3f, 3.2f, 2.25f, 23.8f, 64f);

            ShotPlan(cam, fullOutDir, "12_Interior_PB_Plaza_Sur.png",
                14.24f, 1.85f, 36.2f, 14.24f, 1.35f, 45.8f, 66f);

            ShotPlan(cam, fullOutDir, "13_Interior_PB_Aula_3_Pupitres_Originales.png",
                10.4f, 1.95f, 47.1f, 17.5f, 0.90f, 52.6f, 62f);

            ShotPlan(cam, fullOutDir, "14_Interior_PB_Auditorio.png",
                18.2f, 2.25f, 25.8f, 24.8f, 0.30f, 36.8f, 64f);

            ShotPlan(cam, fullOutDir, "15_Interior_PB_Bano_Espejo.png",
                10.4f, 1.70f, 5.7f, 12.6f, 1.35f, 1.2f, 64f);

            ShotPlan(cam, fullOutDir, "16_Interior_PA_Pasillo_Central.png",
                14.24f, 5.25f, 18.0f, 14.24f, 4.85f, 41.0f, 64f);

            ShotPlan(cam, fullOutDir, "17_Interior_PA_Aula_9_Pupitres_Originales.png",
                18.1f, 5.25f, 23.9f, 25.8f, 4.35f, 29.5f, 62f);

            // ========== BLOQUE B: MODO TERROR (Atmósfera oscura y niebla real del juego para referencia de tono del tráiler) ==========
            if (switcher != null)
            {
                switcher.ApplyMode(Docencia2VisualMode.Terror, false);
            }
            fillLight.enabled = false;

            ShotWorld(cam, fullOutDir, "18_Terror_Exterior_Entrada_Principal.png",
                new Vector3(36.0f, 1.95f, -35.0f), new Vector3(4.0f, 3.6f, -8.0f), 40f);

            ShotPlan(cam, fullOutDir, "19_Terror_Interior_Pasillo_Central.png",
                14.24f, 1.65f, 14.8f, 14.24f, 1.40f, 38.0f, 62f);
        }
        finally
        {
            if (switcher != null) switcher.ApplyMode(originalMode, false);
            if (protaGo != null) protaGo.SetActive(prevProtaActive);
            for (int i = 0; i < allCams.Length; i++)
            {
                if (allCams[i] != null) allCams[i].enabled = prevCamEnabled[i];
            }
            UnityEngine.Object.DestroyImmediate(fillGo);
            UnityEngine.Object.DestroyImmediate(camGo);
        }

        Debug.Log($"[VaelithTrailerPhotoCapture] 19 fotos 1920x1080 guardadas en: {fullOutDir}");
    }

    private static void ShotPlan(Camera cam, string dir, string file,
        float px, float py, float pz, float tx, float ty, float tz, float fov)
    {
        Vector3 pos = Docencia2FirstPersonPlayer.PlanToUnityPos(px, py, pz);
        Vector3 tgt = Docencia2FirstPersonPlayer.PlanToUnityPos(tx, ty, tz);
        ShotWorld(cam, dir, file, pos, tgt, fov);
    }

    private static void ShotWorld(Camera cam, string dir, string file,
        Vector3 pos, Vector3 target, float fov)
    {
        const int width = 1920;
        const int height = 1080;

        cam.fieldOfView = fov;
        cam.transform.position = pos;
        cam.transform.LookAt(target, Vector3.up);
        cam.enabled = true;

        var rt = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
        rt.antiAliasing = 4;
        var prevRT = RenderTexture.active;

        cam.targetTexture = rt;
        cam.Render();
        cam.Render();

        RenderTexture.active = rt;
        var tex = new Texture2D(width, height, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
        tex.Apply();

        cam.targetTexture = null;
        RenderTexture.active = prevRT;
        UnityEngine.Object.DestroyImmediate(rt);

        string outPath = Path.Combine(dir, file);
        File.WriteAllBytes(outPath, tex.EncodeToPNG());
        UnityEngine.Object.DestroyImmediate(tex);
        Debug.Log($"[VaelithTrailerPhotoCapture] Guardada: {file}");
    }

    private static void SetActiveByNameContains(UnityEngine.SceneManagement.Scene scene, string token, bool state)
    {
        foreach (var root in scene.GetRootGameObjects())
        {
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            {
                if (t.name.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0)
                    t.gameObject.SetActive(state);
            }
        }
    }
}
