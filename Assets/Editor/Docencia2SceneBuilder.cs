using System;
using System.IO;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Vaelith.Docencia2
{
    public static class Docencia2SceneBuilder
    {
        private const string FBX_ASSET_PATH = "Assets/Models/Docencia2/Docencia2_Exterior.fbx";
        private const string SCENE_PATH = "Assets/Scenes/Exterior_Docencia2.unity";
        private const string BACKUP_SCENE_PATH = "Assets/Scenes/Exterior_Docencia2_backup.unity";
        private const string BACKUP3_SCENE_PATH = "Assets/Scenes/Exterior_Docencia2_backup3.unity";
        private const string MAT_DIR = "Assets/Materials/Docencia2";
        private const string TEX_DIR = "Assets/Textures/Docencia2";
        private const string MESH_DIR = "Assets/Models/Docencia2/GeneratedMeshes";
        private const string RENDER_OUT_DIR = "Renders_Verificacion";

        [MenuItem("VAELITH/Alternar Modo NOCHE - TERROR (Exterior_Docencia2)")]
        public static void MenuToggleNightTerror()
        {
            var switcher = UnityEngine.Object.FindAnyObjectByType<Docencia2ModeSwitcher>();
            if (switcher != null)
            {
                switcher.ToggleMode();
                if (!Application.isPlaying && !EditorApplication.isPlayingOrWillChangePlaymode)
                {
                    EditorSceneManager.MarkSceneDirty(switcher.gameObject.scene);
                }
                else if (Docencia2TerrorDarknessController.Instance != null)
                {
                    Docencia2TerrorDarknessController.Instance.SyncState();
                }
                Debug.Log("[Docencia2] Modo alternado a: " + switcher.modoActual);
            }
            else
            {
                Debug.LogWarning("[Docencia2] No se encontró Docencia2ModeSwitcher en la escena actual.");
            }
        }

        [MenuItem("VAELITH/Construir Escena Exterior_Docencia2 (NOCHE + TERROR) y Capturar Vistas")]
        public static void BuildAndRenderAll()
        {
            Debug.Log("[Docencia2] Iniciando construcción dual NOCHE + TERROR (ArchViz & Horror Atmosférico URP Unity 6)...");
            EnsureDirectories();

            // 0. Verificar / crear respaldo de la escena antes de empezar
            if (File.Exists(SCENE_PATH) && !File.Exists(BACKUP_SCENE_PATH))
            {
                AssetDatabase.CopyAsset(SCENE_PATH, BACKUP_SCENE_PATH);
                Debug.Log("[Docencia2] Copia de seguridad guardada en: " + BACKUP_SCENE_PATH);
            }
            if (File.Exists(SCENE_PATH) && !File.Exists(BACKUP3_SCENE_PATH))
            {
                AssetDatabase.CopyAsset(SCENE_PATH, BACKUP3_SCENE_PATH);
                Debug.Log("[Docencia2] Copia de seguridad 3 guardada en: " + BACKUP3_SCENE_PATH);
            }

            // 1. Configurar FBX Importer (sin modificar la geometría)
            AssetDatabase.Refresh();
            ModelImporter importer = AssetImporter.GetAtPath(FBX_ASSET_PATH) as ModelImporter;
            if (importer != null)
            {
                importer.globalScale = 1.0f;
                importer.useFileUnits = true;
                importer.bakeAxisConversion = true;
                importer.importNormals = ModelImporterNormals.Calculate;
                importer.normalCalculationMode = ModelImporterNormalCalculationMode.AreaAndAngleWeighted;
                importer.normalSmoothingAngle = 25.0f;
                importer.SaveAndReimport();
            }

            // 2. Generar Texturas PBR Limpias (Modo NOCHE intacto + Variantes Modo TERROR)
            Texture2D texConcAlb, texConcNorm;
            GenerateConcreteTextures(out texConcAlb, out texConcNorm);

            Texture2D texConcTerrorAlb, texConcTerrorNorm;
            GenerateTerrorConcreteTextures(out texConcTerrorAlb, out texConcTerrorNorm);

            Texture2D texGreenAlb, texGreenNorm;
            GenerateGreenPanelTextures(out texGreenAlb, out texGreenNorm);

            Texture2D texGroundAlb, texGroundNorm, texGroundDetAlb, texGroundDetNorm;
            GenerateGroundTextures(out texGroundAlb, out texGroundNorm, out texGroundDetAlb, out texGroundDetNorm);

            Texture2D texGroundTerrorAlb, texGroundTerrorNorm;
            GenerateTerrorGroundTextures(out texGroundTerrorAlb, out texGroundTerrorNorm);

            Texture2D texGravelAlb, texGravelNorm;
            GenerateGravelTextures(out texGravelAlb, out texGravelNorm);

            Texture2D texGlassAlb, texGlassNorm, texGlassEmit;
            GenerateCleanArchGlassTextures(out texGlassAlb, out texGlassNorm, out texGlassEmit);

            Texture2D texFrameAlb;
            GenerateMatteFrameTexture(out texFrameAlb);

            Texture2D texVolCone = GenerateVolumetricBeamTexture();
            Texture2D texContactAO = GenerateContactShadowRingTexture();
            Texture2D texDarkPatch = GenerateDarkSoilPatchTexture();
            Texture2D texMistNoise = GenerateMistCloudTexture();
            Texture2D texMistTerror = GenerateTerrorMistCloudTexture();
            Texture2D texNightSky = GenerateNightSkyPanoramaTexture();
            Texture2D texTerrorSky = GenerateTerrorSkyPanoramaTexture();

            // 3. Crear Materiales URP Lit (Modo NOCHE intacto + Variantes Modo TERROR)
            Dictionary<string, Material> mats = CreateURPMaterials(
                texConcAlb, texConcNorm,
                texConcTerrorAlb, texConcTerrorNorm,
                texGreenAlb, texGreenNorm,
                texGroundAlb, texGroundNorm, texGroundDetAlb, texGroundDetNorm,
                texGroundTerrorAlb, texGroundTerrorNorm,
                texGravelAlb, texGravelNorm,
                texGlassAlb, texGlassNorm, texGlassEmit,
                texFrameAlb,
                texVolCone, texContactAO, texDarkPatch, texMistNoise, texMistTerror, texNightSky, texTerrorSky
            );

            // 4. Crear escena limpia "Exterior_Docencia2" conservando exactamente la geometría y posiciones de cámaras
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // 5. Instanciar el modelo FBX de Docencia 2
            GameObject fbxPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(FBX_ASSET_PATH);
            if (fbxPrefab == null)
            {
                Debug.LogError("[Docencia2] No se encontró el FBX en: " + FBX_ASSET_PATH);
                return;
            }

            GameObject buildingRoot = (GameObject)PrefabUtility.InstantiatePrefab(fbxPrefab);
            buildingRoot.name = "Docencia2_Exterior";
            buildingRoot.transform.position = Vector3.zero;
            buildingRoot.transform.rotation = Quaternion.identity;
            buildingRoot.transform.localScale = Vector3.one;

            // Marcar como Static para GI / Adaptive Probe Volumes / Reflection Probes
            foreach (Transform t in buildingRoot.GetComponentsInChildren<Transform>(true))
            {
                GameObjectUtility.SetStaticEditorFlags(t.gameObject, StaticEditorFlags.ContributeGI | StaticEditorFlags.ReflectionProbeStatic);
            }

            List<Docencia2ModeSwitcher.RendererMaterialPair> matSwaps = new List<Docencia2ModeSwitcher.RendererMaterialPair>();
            AssignMaterialsToHierarchy(buildingRoot, mats, matSwaps);

            // 6. Suelo Exterior
            MeshRenderer groundRenderer = CreateOrganicGroundAndDebris(mats["suelo"], mats["concreto"], mats["contact_ao"], mats["dark_patch"], matSwaps, mats["suelo_terror"], mats["concreto_terror"]);

            // 7. Ambiente, Cielo, Niebla, Luces NOCHE + Luces TERROR, GI + APV + Reflection Probes
            GameObject lightsNightRoot, conesNightRoot, fogNightRoot;
            GameObject lightsTerrorRoot, conesTerrorRoot, fogTerrorRoot;
            List<ReflectionProbe> probes = SetupAtmosphereAndLighting(
                mats,
                out lightsNightRoot, out conesNightRoot, out fogNightRoot,
                out lightsTerrorRoot, out conesTerrorRoot, out fogTerrorRoot
            );

            // 8. Post-procesado URP: Global_Volume_PostProcesado (NOCHE intacto) + Global_Volume_Terror (TERROR)
            GameObject volNightObj, volTerrorObj;
            SetupPostProcessing(out volNightObj, out volTerrorObj);

            // 9. Crear las 3 Cámaras de Camaras_Referencia_Docencia2 (exactamente en las mismas posiciones y rotaciones)
            Camera cam1, cam2, cam3;
            SetupReferenceCameras(out cam1, out cam2, out cam3);
            Camera[] allCams = new Camera[] { cam1, cam2, cam3 };

            // 10. Crear el controlador de alternancia NOCHE / TERROR (Docencia2ModeSwitcher)
            GameObject switcherGo = new GameObject("Controlador_Modo_Noche_Terror");
            Docencia2ModeSwitcher switcher = switcherGo.AddComponent<Docencia2ModeSwitcher>();
            switcher.grupoLucesNoche  = lightsNightRoot;
            switcher.grupoConosNoche  = conesNightRoot;
            switcher.grupoNieblaNoche = fogNightRoot;
            switcher.grupoLucesTerror  = lightsTerrorRoot;
            switcher.grupoConosTerror  = conesTerrorRoot;
            switcher.grupoNieblaTerror = fogTerrorRoot;
            switcher.volumeNoche  = volNightObj;
            switcher.volumeTerror = volTerrorObj;
            switcher.skyboxNoche  = mats.ContainsKey("skybox") ? mats["skybox"] : null;
            switcher.skyboxTerror = mats.ContainsKey("skybox_terror") ? mats["skybox_terror"] : null;
            switcher.materialSwaps = matSwaps;
            switcher.sceneCameras = allCams;
            switcher.reflectionProbes = probes.ToArray();

            // 11. PRIMERO: Verificar y capturar el Modo NOCHE intacto
            switcher.ApplyMode(Docencia2VisualMode.Noche, true);
            RenderSingleCameraClean(cam1, allCams, Path.Combine(RENDER_OUT_DIR, "Unity_Cam_01_Frontal_Baja_Noche_Intacto.png"), 1920, 1080);
            RenderSingleCameraClean(cam2, allCams, Path.Combine(RENDER_OUT_DIR, "Unity_Cam_02_Aerea_Azotea_Noche_Intacto.png"), 1920, 1080);
            RenderSingleCameraClean(cam3, allCams, Path.Combine(RENDER_OUT_DIR, "Unity_Cam_03_Esquina_Contrafuerte_Noche_Intacto.png"), 1920, 1080);

            // 12. SEGUNDO: Activar Modo TERROR, actualizar Reflection Probes y renderizar las 3 cámaras en versión TERROR
            switcher.ApplyMode(Docencia2VisualMode.Terror, true);
            RenderSingleCameraClean(cam1, allCams, Path.Combine(RENDER_OUT_DIR, "Unity_Terror_Cam_01_Frontal_Baja.png"), 1920, 1080);
            RenderSingleCameraClean(cam2, allCams, Path.Combine(RENDER_OUT_DIR, "Unity_Terror_Cam_02_Aerea_Azotea.png"), 1920, 1080);
            RenderSingleCameraClean(cam3, allCams, Path.Combine(RENDER_OUT_DIR, "Unity_Terror_Cam_03_Esquina_Contrafuerte.png"), 1920, 1080);

            // Dejar las 3 cámaras configuradas en la jerarquía (Cam1 principal activa) y el Modo TERROR activo para inspección inmediata (alternable con tecla T o enum en Inspector)
            cam1.enabled = true; cam1.depth = 10;
            cam2.enabled = false; cam2.depth = 5;
            cam3.enabled = false; cam3.depth = 4;
            EditorSceneManager.SaveScene(scene, SCENE_PATH);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("[Docencia2] ¡Escena con sistema NOCHE / TERROR completada y 6 capturas 1920x1080 verificadas!");
        }

        public static void BatchBuildAndRender()
        {
            BuildAndRenderAll();
        }

        private static void EnsureDirectories()
        {
            if (!Directory.Exists("Assets/Scenes")) Directory.CreateDirectory("Assets/Scenes");
            if (!Directory.Exists(MAT_DIR)) Directory.CreateDirectory(MAT_DIR);
            if (!Directory.Exists(TEX_DIR)) Directory.CreateDirectory(TEX_DIR);
            if (!Directory.Exists(MESH_DIR)) Directory.CreateDirectory(MESH_DIR);
            if (!Directory.Exists(RENDER_OUT_DIR)) Directory.CreateDirectory(RENDER_OUT_DIR);
        }

        // ----------------------------------------------------------------------
        // GENERACIÓN DE TEXTURAS REALISTAS LIMPIAS (ARCHVIZ NOCTURNO)
        // Alpha = Smoothness Map (ninguna superficie tiene Smoothness uniforme)
        // ----------------------------------------------------------------------
        private static void GenerateConcreteTextures(out Texture2D albedo, out Texture2D normal)
        {
            int size = 512;
            albedo = new Texture2D(size, size, TextureFormat.RGBA32, true);
            normal = new Texture2D(size, size, TextureFormat.RGBA32, true, true);
            float[,] hMap = new float[size, size];

            // Concreto arquitectónico limpio: gris cálido neutro (CERO tinte verde)
            // R > G > B ligero para tono cálido natural de concreto arquitectónico
            Color warmArchGreyLight = new Color(0.60f, 0.58f, 0.55f, 1.0f);
            Color warmArchGreyMid   = new Color(0.50f, 0.48f, 0.45f, 1.0f);
            Color dampWeatherDark   = new Color(0.32f, 0.31f, 0.29f, 1.0f);

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float u = (float)x / size;
                    float v = (float)y / size;

                    // Variación suave de placas de concreto + poros finos reales de vaciado arquitectónico (100% tileable/periódico)
                    float macro = TileableFbm(u, v, 1.5f, 1.3f, 2.7f);
                    float med   = TileableFbm(u, v, 5.0f, 5.1f, 0.8f) * 0.35f;
                    float fine  = TileableFbm(u, v, 16.0f, 2.4f, 4.1f) * 0.22f;
                    float tone  = Mathf.Clamp01(macro * 0.55f + med + fine);

                    // Manchas grandes y suaves de humedad más oscuras bajo voladizos y en la base
                    float dampBlob = TileableFbm(u, v, 1.5f, 8.2f, 3.4f);
                    float edgeMoisture = Mathf.Pow(1.0f - v, 2.2f) * 0.52f + Mathf.Pow(v, 2.4f) * 0.48f;
                    float dampMask = Mathf.SmoothStep(0.28f, 0.78f, dampBlob * 0.55f + edgeMoisture);

                    // Líneas de cimbra horizontales y verticales finas y limpias
                    float distH = Mathf.Min(y % 128, 128 - (y % 128));
                    float distV = Mathf.Min(x % 256, 256 - (x % 256));
                    float seamH = Mathf.Lerp(0.76f, 1.0f, Mathf.SmoothStep(0.0f, 3.0f, distH));
                    float seamV = Mathf.Lerp(0.84f, 1.0f, Mathf.SmoothStep(0.0f, 2.5f, distV));
                    float formwork = seamH * seamV;

                    float hVal = (tone * 0.70f + (1.0f - dampMask) * 0.30f) * formwork;
                    hMap[x, y] = hVal;

                    Color col = Color.Lerp(warmArchGreyMid, warmArchGreyLight, tone);
                    col = Color.Lerp(col, dampWeatherDark, dampMask * 0.78f) * formwork;

                    // Smoothness variado con máscara entre 0.15 y 0.25 (almacenado en canal Alpha)
                    col.a = Mathf.Lerp(0.15f, 0.25f, Mathf.Clamp01(tone * 0.6f + dampMask * 0.4f));
                    albedo.SetPixel(x, y, col);
                }
            }
            albedo.Apply();
            // Normal map moderado (0.3 - 0.5 en el material)
            BuildNormalFromHeight(hMap, size, 2.2f, normal);
            albedo = SaveTex(albedo, "Tex_Concreto_Albedo.png", false);
            normal = SaveTex(normal, "Tex_Concreto_Normal.png", true);
        }

        private static void GenerateGreenPanelTextures(out Texture2D albedo, out Texture2D normal)
        {
            int size = 512;
            albedo = new Texture2D(size, size, TextureFormat.RGBA32, true);
            normal = new Texture2D(size, size, TextureFormat.RGBA32, true, true);
            float[,] hMap = new float[size, size];

            // Verde bosque oscuro arquitectónico, claramente diferenciado del concreto gris cálido
            Color forestGreenA = new Color(0.11f, 0.23f, 0.165f, 1.0f);
            Color forestGreenB = new Color(0.08f, 0.18f, 0.130f, 1.0f);

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float u = (float)x / size;
                    float v = (float)y / size;

                    // Variación sutil de tono entre paneles (izquierdo vs derecho en el mapa UV) + variación suave de superficie
                    float panelId = (x < 256) ? 0.0f : 1.0f;
                    float subtleVar = Mathf.PerlinNoise(u * 4.5f + 2.1f, v * 4.5f + 3.7f);
                    float fineCoat  = Mathf.PerlinNoise(u * 16.0f, v * 16.0f) * 0.10f;

                    // Juntas verticales y horizontales bien marcadas
                    float distV = Mathf.Min(x % 256, 256 - (x % 256));
                    float distH = Mathf.Min(y % 256, 256 - (y % 256));
                    float seamV = Mathf.Lerp(0.45f, 1.0f, Mathf.SmoothStep(0.0f, 3.0f, distV));
                    float seamH = Mathf.Lerp(0.72f, 1.0f, Mathf.SmoothStep(0.0f, 2.5f, distH));
                    float seam  = seamV * seamH;

                    float h = (0.5f + subtleVar * 0.25f + fineCoat) * seam;
                    hMap[x, y] = h;

                    float mix = Mathf.Clamp01(subtleVar * 0.70f + panelId * 0.22f);
                    Color c = Color.Lerp(forestGreenB, forestGreenA, mix) * seam;

                    // Smoothness satinado arquitectónico variado entre 0.35 y 0.45 con máscara (en Alpha)
                    c.a = Mathf.Lerp(0.35f, 0.45f, Mathf.Clamp01(subtleVar * 0.8f + fineCoat)) * seamV;
                    albedo.SetPixel(x, y, c);
                }
            }
            albedo.Apply();
            BuildNormalFromHeight(hMap, size, 0.85f, normal);
            albedo = SaveTex(albedo, "Tex_PanelVerde_Albedo.png", false);
            normal = SaveTex(normal, "Tex_PanelVerde_Normal.png", true);
        }

        private static void GenerateGroundTextures(
            out Texture2D albedo, out Texture2D normal,
            out Texture2D detailAlbedo, out Texture2D detailNormal)
        {
            int size = 512;
            albedo = new Texture2D(size, size, TextureFormat.RGBA32, true);
            normal = new Texture2D(size, size, TextureFormat.RGBA32, true, true);
            float[,] hMap = new float[size, size];

            // Suelo: Macro-variación de tono con máscara grande (manchas suaves más claras y más oscuras, escala ~15-20 m)
            // para romper la uniformidad sin tileo visible y manteniendo el suelo un poco más claro en primer plano para que nunca caiga a negro
            Color darkEarthPatch  = new Color(0.11f, 0.098f, 0.084f, 1.0f); // Manchas suaves más oscuras de tierra húmeda/compacta
            Color midLoamEarth    = new Color(0.28f, 0.245f, 0.195f, 1.0f); // Tono medio de tierra seca
            Color lightDryPatch   = new Color(0.52f, 0.445f, 0.325f, 1.0f); // Manchas suaves más claras de tierra/pasto seco
            Color warmOchrePatch  = new Color(0.44f, 0.375f, 0.270f, 1.0f); // Variación secundaria cálida

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float u = (float)x / size;
                    float v = (float)y / size;

                    // Máscara grande (escala ~15-20 m por mancha cuando el ciclo UV es 72 m) con manchas suaves más claras y más oscuras
                    float macroBlob1 = TileableFbm(u, v, 3.0f, 1.3f, 2.7f);
                    float macroBlob2 = TileableFbm(u, v, 4.0f, 4.8f, 1.9f);
                    float macroBlob3 = TileableFbm(u, v, 2.0f, 7.4f, 5.2f);

                    float lightMask = Mathf.SmoothStep(0.32f, 0.68f, macroBlob1);
                    float darkMask  = Mathf.SmoothStep(0.35f, 0.68f, 1.0f - macroBlob2);
                    float warmMask  = Mathf.SmoothStep(0.30f, 0.70f, macroBlob3);

                    // Variación media orgánica (escala ~3-5 m)
                    float med1 = TileableFbm(u, v, 14.0f, 2.1f, 3.4f);
                    float med2 = TileableFbm(u, v, 24.0f, 5.2f, 6.1f);

                    Color baseCol = Color.Lerp(midLoamEarth, lightDryPatch, lightMask);
                    baseCol = Color.Lerp(baseCol, warmOchrePatch, warmMask * 0.45f);
                    baseCol = Color.Lerp(baseCol, darkEarthPatch, darkMask * 0.78f);

                    // Modulación orgánica media
                    Color c = baseCol * (0.84f + med1 * 0.22f + med2 * 0.10f);

                    float h = Mathf.Clamp01(lightMask * 0.40f + (1.0f - darkMask) * 0.25f + med1 * 0.25f + med2 * 0.10f);
                    hMap[x, y] = h;

                    // Smoothness variado entre 0.10 y 0.20 (sin charcos brillantes ni aspecto mojado)
                    c.a = Mathf.Lerp(0.10f, 0.20f, Mathf.Clamp01(lightMask * 0.55f + med1 * 0.45f));
                    albedo.SetPixel(x, y, c);
                }
            }
            albedo.Apply();
            BuildNormalFromHeight(hMap, size, 4.2f, normal);
            albedo = SaveTex(albedo, "Tex_SueloTierra_Albedo.png", false);
            normal = SaveTex(normal, "Tex_SueloTierra_Normal.png", true);

            // Detail Map sutil (grano fino de tierra) con tileo no visible (frecuencias no armónicas)
            int dSize = 256;
            detailAlbedo = new Texture2D(dSize, dSize, TextureFormat.RGBA32, true);
            detailNormal = new Texture2D(dSize, dSize, TextureFormat.RGBA32, true, true);
            float[,] dHMap = new float[dSize, dSize];

            for (int y = 0; y < dSize; y++)
            {
                for (int x = 0; x < dSize; x++)
                {
                    float u = (float)x / dSize;
                    float v = (float)y / dSize;
                    float d1 = TileableFbm(u, v, 6.0f, 3.1f, 6.4f);
                    float d2 = TileableFbm(u, v, 15.0f, 8.7f, 2.3f);
                    float d3 = TileableFbm(u, v, 29.0f, 1.9f, 9.1f);
                    float dh = Mathf.Clamp01(d1 * 0.45f + d2 * 0.35f + d3 * 0.20f);
                    dHMap[x, y] = dh;

                    // En URP Lit Detail Albedo (_DETAIL_MULX2), 0.5 es neutro; modulamos entre 0.36 y 0.64 para grano fino de tierra sin ruido duro
                    float g = Mathf.Lerp(0.36f, 0.64f, dh);
                    detailAlbedo.SetPixel(x, y, new Color(g, g * 0.98f, g * 0.95f, 1.0f));
                }
            }
            detailAlbedo.Apply();
            BuildNormalFromHeight(dHMap, dSize, 2.8f, detailNormal);
            detailAlbedo = SaveTex(detailAlbedo, "Tex_SueloTierra_DetailAlbedo.png", false);
            detailNormal = SaveTex(detailNormal, "Tex_SueloTierra_DetailNormal.png", true);
        }

        private static float TileableFbm(float u, float v, float freq, float seedX, float seedY)
        {
            u = u - Mathf.Floor(u);
            v = v - Mathf.Floor(v);
            float s = freq;
            float n00 = RawFbm((u) * s + seedX, (v) * s + seedY);
            float n10 = RawFbm((u - 1.0f) * s + seedX, (v) * s + seedY);
            float n01 = RawFbm((u) * s + seedX, (v - 1.0f) * s + seedY);
            float n11 = RawFbm((u - 1.0f) * s + seedX, (v - 1.0f) * s + seedY);

            float wu = u * u * (3.0f - 2.0f * u);
            float wv = v * v * (3.0f - 2.0f * v);
            float nx0 = Mathf.Lerp(n00, n10, wu);
            float nx1 = Mathf.Lerp(n01, n11, wu);
            return Mathf.Lerp(nx0, nx1, wv);
        }

        private static float RawFbm(float x, float y)
        {
            float n1 = Mathf.PerlinNoise(x, y);
            float n2 = Mathf.PerlinNoise(x * 2.13f + 3.7f, y * 2.13f + 1.9f) * 0.5f;
            float n3 = Mathf.PerlinNoise(x * 4.41f + 7.1f, y * 4.41f + 5.3f) * 0.25f;
            return Mathf.Clamp01((n1 + n2 + n3) / 1.75f);
        }

        private static void GenerateGravelTextures(out Texture2D albedo, out Texture2D normal)
        {
            int size = 512;
            albedo = new Texture2D(size, size, TextureFormat.RGBA32, true);
            normal = new Texture2D(size, size, TextureFormat.RGBA32, true, true);
            float[,] hMap = new float[size, size];

            // Membrana/gravilla arquitectónica de azotea en gris neutro oscuro con Smoothness variado (0.12 - 0.22)
            Color roofDarkGrey  = new Color(0.18f, 0.19f, 0.21f, 1.0f);
            Color roofLightGrey = new Color(0.28f, 0.29f, 0.31f, 1.0f);

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float u = (float)x / size;
                    float v = (float)y / size;
                    float patch1 = TileableFbm(u, v, 2.0f, 1.4f, 2.8f);
                    float patch2 = TileableFbm(u, v, 8.0f, 4.1f, 1.9f) * 0.35f;
                    float h = Mathf.Clamp01(patch1 * 0.70f + patch2);
                    hMap[x, y] = h;

                    Color c = Color.Lerp(roofDarkGrey, roofLightGrey, h);
                    c.a = Mathf.Lerp(0.12f, 0.22f, patch1);
                    albedo.SetPixel(x, y, c);
                }
            }
            albedo.Apply();
            BuildNormalFromHeight(hMap, size, 0.85f, normal);
            albedo = SaveTex(albedo, "Tex_Gravilla_Albedo.png", false);
            normal = SaveTex(normal, "Tex_Gravilla_Normal.png", true);
        }

        private static void GenerateCleanArchGlassTextures(out Texture2D albedo, out Texture2D normal, out Texture2D litEmissionTex)
        {
            int size = 256;
            albedo = new Texture2D(size, size, TextureFormat.RGBA32, true);
            normal = new Texture2D(size, size, TextureFormat.RGBA32, true, true);
            litEmissionTex = new Texture2D(size, size, TextureFormat.RGBA32, true);
            float[,] hMap = new float[size, size];

            // Vidrio arquitectónico con tinte azul oscuro y Smoothness variado 0.85 - 0.90 (reflejo vía Reflection Probe)
            Color deepBlueGlass = new Color(0.08f, 0.13f, 0.20f, 1.0f);
            Color midBlueGlass  = new Color(0.13f, 0.20f, 0.30f, 1.0f);

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float u = (float)x / size;
                    float v = (float)y / size;
                    float wave = TileableFbm(u, v, 1.5f, 1.5f, 2.8f);
                    hMap[x, y] = wave * 0.04f;

                    Color c = Color.Lerp(deepBlueGlass, midBlueGlass, wave);
                    c.a = Mathf.Lerp(0.85f, 0.90f, wave); // Smoothness 0.85 - 0.90
                    albedo.SetPixel(x, y, c);

                    // Gradiente de iluminación interior arquitectónica con caída suave en bordes y variación sutil para que NUNCA parezca un sticker plano
                    float edgeU = Mathf.SmoothStep(0.0f, 0.18f, Mathf.Min(u, 1.0f - u));
                    float edgeV = Mathf.SmoothStep(0.0f, 0.18f, Mathf.Min(v, 1.0f - v));
                    float roomGrad = Mathf.Lerp(0.55f, 1.0f, Mathf.SmoothStep(0.0f, 1.0f, v * 0.65f + wave * 0.35f));
                    float emFactor = Mathf.Lerp(0.42f, 1.0f, edgeU * edgeV) * roomGrad;
                    litEmissionTex.SetPixel(x, y, new Color(emFactor, emFactor * 0.96f, emFactor * 0.90f, 1.0f));
                }
            }
            albedo.Apply();
            litEmissionTex.Apply();
            BuildNormalFromHeight(hMap, size, 0.12f, normal);
            albedo = SaveTex(albedo, "Tex_VidrioSucio_Albedo.png", false);
            normal = SaveTex(normal, "Tex_VidrioSucio_Normal.png", true);
            litEmissionTex = SaveTex(litEmissionTex, "Tex_VidrioInterior_Emision.png", false);
        }

        private static void GenerateMatteFrameTexture(out Texture2D albedo)
        {
            int size = 128;
            albedo = new Texture2D(size, size, TextureFormat.RGBA32, true);
            Color frameLight = new Color(0.46f, 0.47f, 0.49f, 1.0f);
            Color frameMid   = new Color(0.38f, 0.39f, 0.41f, 1.0f);

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float n = Mathf.PerlinNoise(x * 0.06f, y * 0.06f);
                    Color c = Color.Lerp(frameMid, frameLight, n);
                    c.a = Mathf.Lerp(0.14f, 0.24f, n); // Marcos claros mates con Smoothness variado
                    albedo.SetPixel(x, y, c);
                }
            }
            albedo.Apply();
            albedo = SaveTex(albedo, "Tex_MarcoClaro_Albedo.png", false);
        }

        private static Texture2D GenerateVolumetricBeamTexture()
        {
            int w = 128, h = 256;
            Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            for (int y = 0; y < h; y++)
            {
                float v = (float)y / (h - 1);
                float coneHalfWidth = Mathf.Lerp(0.46f, 0.09f, v);
                float verticalFade = Mathf.Pow(v, 0.85f) * Mathf.SmoothStep(0.0f, 0.25f, v);

                for (int x = 0; x < w; x++)
                {
                    float u = (float)x / (w - 1);
                    float distCenter = Mathf.Abs(u - 0.5f);
                    float radial = Mathf.Clamp01(1.0f - (distCenter / coneHalfWidth));
                    radial = Mathf.Pow(radial, 1.9f);

                    float alpha = radial * verticalFade;
                    tex.SetPixel(x, y, new Color(1.0f, 0.76f, 0.40f, alpha));
                }
            }
            tex.Apply();
            return SaveTex(tex, "Tex_Cono_Volumetrico_2700K.png", false, TextureWrapMode.Clamp);
        }

        private static Texture2D GenerateContactShadowRingTexture()
        {
            int s = 256;
            Texture2D tex = new Texture2D(s, s, TextureFormat.RGBA32, false);
            for (int y = 0; y < s; y++)
            {
                float ny = ((float)y / (s - 1)) * 2.0f - 1.0f;
                for (int x = 0; x < s; x++)
                {
                    float nx = ((float)x / (s - 1)) * 2.0f - 1.0f;
                    float dx = Mathf.Max(0.0f, Mathf.Abs(nx) - 0.76f) / 0.24f;
                    float dy = Mathf.Max(0.0f, Mathf.Abs(ny) - 0.66f) / 0.34f;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    float aoAlpha = Mathf.Clamp01(1.0f - d) * 0.72f;
                    tex.SetPixel(x, y, new Color(0.03f, 0.04f, 0.06f, aoAlpha));
                }
            }
            tex.Apply();
            return SaveTex(tex, "Tex_ContactShadow_AO.png", false, TextureWrapMode.Clamp);
        }

        private static Texture2D GenerateDarkSoilPatchTexture()
        {
            int s = 256;
            Texture2D tex = new Texture2D(s, s, TextureFormat.RGBA32, false);
            for (int y = 0; y < s; y++)
            {
                float v = (float)y / (s - 1);
                for (int x = 0; x < s; x++)
                {
                    float u = (float)x / (s - 1);
                    float dx = (u - 0.5f) * 2.0f;
                    float dy = (v - 0.5f) * 2.0f;
                    float dist = Mathf.Sqrt(dx * dx + dy * dy);
                    float noise = Mathf.PerlinNoise(u * 4.5f + 2.3f, v * 4.5f + 5.1f) * 0.35f;
                    float falloff = Mathf.Clamp01(1.0f - (dist - noise * 0.4f));
                    falloff = Mathf.SmoothStep(0.0f, 1.0f, falloff);
                    float alpha = falloff * (0.45f + noise * 0.35f);
                    tex.SetPixel(x, y, new Color(0.04f, 0.035f, 0.030f, alpha));
                }
            }
            tex.Apply();
            return SaveTex(tex, "Tex_Mancha_Tierra_Oscura.png", false, TextureWrapMode.Clamp);
        }

        private static Texture2D GenerateMistCloudTexture()
        {
            int s = 256;
            Texture2D tex = new Texture2D(s, s, TextureFormat.RGBA32, false);
            for (int y = 0; y < s; y++)
            {
                float v = (float)y / (s - 1);
                for (int x = 0; x < s; x++)
                {
                    float u = (float)x / (s - 1);
                    float dx = (u - 0.5f) * 2.0f;
                    // Gradiente horizontal amplio y vertical muy suave en la base y el tope para suavizar la línea recta del horizonte
                    float horizFade = Mathf.SmoothStep(0.0f, 1.0f, Mathf.Clamp01(1.0f - Mathf.Abs(dx)));
                    float vertFade  = Mathf.SmoothStep(0.0f, 0.35f, v) * Mathf.SmoothStep(1.0f, 0.25f, v);

                    float n1 = Mathf.PerlinNoise(u * 3.5f + 1.7f, v * 2.5f + 3.2f);
                    float n2 = Mathf.PerlinNoise(u * 8.0f + 5.4f, v * 5.0f + 2.1f) * 0.35f;
                    float cloud = Mathf.Clamp01(0.45f + n1 * 0.45f + n2 * 0.25f);

                    float a = horizFade * vertFade * cloud * 0.42f;
                    // Niebla baja azul-gris tenue (#1A2433) sin verde
                    tex.SetPixel(x, y, new Color(0.102f, 0.141f, 0.200f, a));
                }
            }
            tex.Apply();
            return SaveTex(tex, "Tex_Niebla_NubeBaja.png", false, TextureWrapMode.Clamp);
        }

        private static Texture2D GenerateNightSkyPanoramaTexture()
        {
            int w = 4096, h = 2048;
            Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false);

            // Skybox oscuro azulado SIN VERDE: horizonte con neblina baja azul-gris (#1A2433) y siluetas oscuras lejanas (colinas bajas) para suavizar la transición piso-cielo
            Color horizonMistBlueGrey = new Color(0.102f, 0.141f, 0.200f, 1.0f); // #1A2433 exacto en la línea de horizonte
            Color distantHillsDark    = new Color(0.048f, 0.068f, 0.102f, 1.0f); // Silueta oscura lejana de colinas bajas
            Color midNightBlue        = new Color(0.055f, 0.090f, 0.150f, 1.0f); // #0E1726
            Color zenithBlueBlack     = new Color(0.027f, 0.043f, 0.078f, 1.0f); // #070B14
            Color cloudSlateBlue      = new Color(0.135f, 0.180f, 0.255f, 1.0f);

            UnityEngine.Random.InitState(777);
            float[,] starField = new float[w, h];
            for (int i = 0; i < 380; i++)
            {
                int sx = UnityEngine.Random.Range(4, w - 4);
                int sy = UnityEngine.Random.Range(h / 2 + 120, h - 40);
                float br = UnityEngine.Random.Range(0.15f, 0.42f);
                for (int dy = -2; dy <= 2; dy++)
                {
                    for (int dx = -2; dx <= 2; dx++)
                    {
                        float r2 = dx * dx + dy * dy;
                        float g = Mathf.Exp(-r2 * 1.25f) * br;
                        if (g > starField[sx + dx, sy + dy])
                            starField[sx + dx, sy + dy] = g;
                    }
                }
            }

            for (int y = 0; y < h; y++)
            {
                float v = (float)y / (h - 1);
                for (int x = 0; x < w; x++)
                {
                    float u = (float)x / (w - 1);
                    float ang = u * Mathf.PI * 2.0f;
                    // Perfil orgánico periódico de colinas bajas lejanas sobre el horizonte (v = 0.500 a 0.524)
                    float hillWave1 = Mathf.Sin(ang * 3.0f + 0.8f) * 0.35f + Mathf.Cos(ang * 7.0f - 1.4f) * 0.25f + Mathf.Sin(ang * 13.0f + 2.2f) * 0.12f;
                    float hillHeight = 0.008f + Mathf.Clamp01(hillWave1 * 0.5f + 0.5f) * 0.015f;

                    Color c;
                    if (v < 0.5f)
                    {
                        float t = Mathf.Clamp01((0.5f - v) * 5.0f);
                        c = Color.Lerp(horizonMistBlueGrey, new Color(0.043f, 0.071f, 0.125f, 1.0f), t);
                    }
                    else
                    {
                        float alt = (v - 0.5f) * 2.0f;
                        c = Color.Lerp(horizonMistBlueGrey, midNightBlue, Mathf.SmoothStep(0.0f, 1.0f, Mathf.Clamp01(alt * 3.2f)));
                        c = Color.Lerp(c, zenithBlueBlack, Mathf.Pow(alt, 0.75f));

                        float c1 = Mathf.PerlinNoise(u * 6.5f + 1.3f, alt * 4.5f + 2.1f);
                        float c2 = Mathf.PerlinNoise(u * 16.0f + 4.2f, alt * 10.0f + 0.7f) * 0.45f;
                        float cloudMask = Mathf.Clamp01((c1 * 0.7f + c2) - 0.36f) * Mathf.Clamp01(1.0f - alt * 1.15f);
                        c = Color.Lerp(c, cloudSlateBlue, cloudMask * 0.55f);

                        // Siluetas oscuras lejanas de colinas bajas fundidas en la bruma baja azul-gris (#1A2433)
                        float vAboveHorizon = v - 0.5f;
                        if (vAboveHorizon < hillHeight + 0.006f)
                        {
                            float hillEdge = Mathf.SmoothStep(hillHeight + 0.004f, hillHeight - 0.002f, vAboveHorizon);
                            float baseMistBlend = Mathf.SmoothStep(0.0f, hillHeight * 0.85f, vAboveHorizon);
                            Color hillCol = Color.Lerp(horizonMistBlueGrey, distantHillsDark, baseMistBlend * 0.75f);
                            c = Color.Lerp(c, hillCol, hillEdge * 0.82f);
                        }

                        // Velo suave de niebla baja (#1A2433) justo en la línea de horizonte
                        float lowMistGlow = Mathf.Exp(-alt * 28.0f);
                        c = Color.Lerp(c, horizonMistBlueGrey, lowMistGlow * 0.72f);

                        float sVal = starField[x, y];
                        if (sVal > 0.01f && cloudMask < 0.25f && vAboveHorizon > hillHeight + 0.01f)
                        {
                            c = Color.Lerp(c, new Color(0.80f, 0.86f, 0.96f, 1.0f), sVal);
                        }
                    }
                    tex.SetPixel(x, y, c);
                }
            }
            tex.Apply();

            string fullPath = Path.Combine(TEX_DIR, "Tex_Cielo_Nocturno_Panorama.png");
            File.WriteAllBytes(fullPath, tex.EncodeToPNG());
            AssetDatabase.ImportAsset(fullPath);
            TextureImporter ti = AssetImporter.GetAtPath(fullPath) as TextureImporter;
            if (ti != null)
            {
                ti.maxTextureSize = 4096;
                ti.textureCompression = TextureImporterCompression.Uncompressed;
                ti.filterMode = FilterMode.Bilinear;
                ti.wrapMode = TextureWrapMode.Repeat;
                ti.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(fullPath);
        }

        // ----------------------------------------------------------------------
        // GENERACIÓN DE TEXTURAS ESPECÍFICAS PARA VERSIÓN TERROR
        // ----------------------------------------------------------------------
        private static void GenerateTerrorConcreteTextures(out Texture2D albedo, out Texture2D normal)
        {
            int size = 512;
            albedo = new Texture2D(size, size, TextureFormat.RGBA32, true);
            normal = new Texture2D(size, size, TextureFormat.RGBA32, true, true);
            float[,] hMap = new float[size, size];

            // Concreto Terror: se sigue leyendo como concreto gris cálido envejecido, con mayor humedad y manchas de suciedad bajo voladizos y en la base + vetas de goteo verticales suaves (sin ruido sucio)
            Color warmArchGreyLight = new Color(0.56f, 0.54f, 0.51f, 1.0f);
            Color warmArchGreyMid   = new Color(0.45f, 0.43f, 0.40f, 1.0f);
            Color dampWeatherDark   = new Color(0.20f, 0.21f, 0.20f, 1.0f);
            Color dripStainDark     = new Color(0.16f, 0.17f, 0.16f, 1.0f);

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float u = (float)x / size;
                    float v = (float)y / size;

                    float macro = TileableFbm(u, v, 1.5f, 1.3f, 2.7f);
                    float med   = TileableFbm(u, v, 5.0f, 5.1f, 0.8f) * 0.32f;
                    float fine  = TileableFbm(u, v, 14.0f, 2.4f, 4.1f) * 0.16f;
                    float tone  = Mathf.Clamp01(macro * 0.58f + med + fine);

                    // Mayor humedad bajo voladizos (v alto) y en la base (v bajo)
                    float dampBlob = TileableFbm(u, v, 1.5f, 8.2f, 3.4f);
                    float edgeMoisture = Mathf.Pow(1.0f - v, 1.65f) * 0.72f + Mathf.Pow(v, 1.75f) * 0.68f;
                    float dampMask = Mathf.SmoothStep(0.18f, 0.72f, dampBlob * 0.55f + edgeMoisture);

                    // Vetas de goteo verticales suaves (alta frecuencia en U pero estiradas suavemente en V, concentradas desde el borde superior e inferior)
                    float dripColumn = TileableFbm(u, v * 0.25f, 11.0f, 3.7f, 1.9f);
                    float dripSub    = TileableFbm(u, v * 0.35f, 21.0f, 6.4f, 4.2f);
                    float dripProfile = Mathf.SmoothStep(0.46f, 0.76f, dripColumn * 0.65f + dripSub * 0.35f);
                    float dripVerticalReach = Mathf.Pow(v, 1.35f) * 0.85f + Mathf.Pow(1.0f - v, 1.85f) * 0.55f;
                    float dripMask = Mathf.Clamp01(dripProfile * dripVerticalReach);

                    // Líneas de cimbra limpias
                    float distH = Mathf.Min(y % 128, 128 - (y % 128));
                    float distV = Mathf.Min(x % 256, 256 - (x % 256));
                    float seamH = Mathf.Lerp(0.72f, 1.0f, Mathf.SmoothStep(0.0f, 3.2f, distH));
                    float seamV = Mathf.Lerp(0.80f, 1.0f, Mathf.SmoothStep(0.0f, 2.6f, distV));
                    float formwork = seamH * seamV;

                    float hVal = (tone * 0.65f + (1.0f - dampMask) * 0.25f + (1.0f - dripMask) * 0.10f) * formwork;
                    hMap[x, y] = hVal;

                    Color col = Color.Lerp(warmArchGreyMid, warmArchGreyLight, tone);
                    col = Color.Lerp(col, dampWeatherDark, dampMask * 0.85f);
                    col = Color.Lerp(col, dripStainDark, dripMask * 0.68f);
                    col *= formwork;

                    // Smoothness ligeramente mayor en las vetas húmedas (0.16 a 0.30) sin verse plástico
                    col.a = Mathf.Lerp(0.15f, 0.29f, Mathf.Clamp01(dampMask * 0.55f + dripMask * 0.45f));
                    albedo.SetPixel(x, y, col);
                }
            }
            albedo.Apply();
            BuildNormalFromHeight(hMap, size, 2.35f, normal);
            albedo = SaveTex(albedo, "Tex_Concreto_Terror_Albedo.png", false);
            normal = SaveTex(normal, "Tex_Concreto_Terror_Normal.png", true);
        }

        private static void GenerateTerrorGroundTextures(out Texture2D albedo, out Texture2D normal)
        {
            int size = 512;
            albedo = new Texture2D(size, size, TextureFormat.RGBA32, true);
            normal = new Texture2D(size, size, TextureFormat.RGBA32, true, true);
            float[,] hMap = new float[size, size];

            // Suelo Terror: húmedo SOLO en parches grandes con Smoothness 0.30-0.45 que reflejen las luces cálidas, y el resto mate (0.10-0.20). Sin aspecto plástico.
            Color darkWetEarth   = new Color(0.085f, 0.082f, 0.075f, 1.0f);
            Color midLoamEarth   = new Color(0.24f,  0.215f, 0.175f, 1.0f);
            Color lightDryPatch  = new Color(0.42f,  0.365f, 0.275f, 1.0f);

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float u = (float)x / size;
                    float v = (float)y / size;

                    float macroBlob1 = TileableFbm(u, v, 3.0f, 1.3f, 2.7f);
                    float macroBlob2 = TileableFbm(u, v, 3.0f, 4.8f, 1.9f);
                    float wetPatchNoise = TileableFbm(u, v, 2.0f, 8.3f, 4.1f);

                    float lightMask = Mathf.SmoothStep(0.35f, 0.70f, macroBlob1);
                    float darkMask  = Mathf.SmoothStep(0.32f, 0.68f, 1.0f - macroBlob2);
                    // Parches grandes de humedad (aprox. 30-35% de la superficie) con transición suave
                    float wetPatchMask = Mathf.SmoothStep(0.54f, 0.72f, wetPatchNoise * 0.75f + (1.0f - macroBlob2) * 0.25f);

                    float med1 = TileableFbm(u, v, 14.0f, 2.1f, 3.4f);
                    float med2 = TileableFbm(u, v, 24.0f, 5.2f, 6.1f);

                    Color baseCol = Color.Lerp(midLoamEarth, lightDryPatch, lightMask);
                    baseCol = Color.Lerp(baseCol, darkWetEarth, Mathf.Clamp01(darkMask * 0.65f + wetPatchMask * 0.55f));
                    Color c = baseCol * (0.85f + med1 * 0.20f + med2 * 0.08f);

                    // En los parches húmedos la superficie es ligeramente más lisa que en la tierra seca
                    float dryHeight = Mathf.Clamp01(lightMask * 0.40f + (1.0f - darkMask) * 0.25f + med1 * 0.25f + med2 * 0.10f);
                    float h = Mathf.Lerp(dryHeight, dryHeight * 0.45f, wetPatchMask);
                    hMap[x, y] = h;

                    // Smoothness: 0.10-0.20 en zonas secas mates, y 0.30-0.45 SOLO en los parches grandes húmedos
                    float drySmooth = Mathf.Lerp(0.10f, 0.20f, Mathf.Clamp01(lightMask * 0.5f + med1 * 0.5f));
                    float wetSmooth = Mathf.Lerp(0.32f, 0.45f, Mathf.Clamp01(wetPatchNoise * 0.7f + med1 * 0.3f));
                    c.a = Mathf.Lerp(drySmooth, wetSmooth, wetPatchMask);
                    albedo.SetPixel(x, y, c);
                }
            }
            albedo.Apply();
            BuildNormalFromHeight(hMap, size, 3.8f, normal);
            albedo = SaveTex(albedo, "Tex_SueloTierra_Terror_Albedo.png", false);
            normal = SaveTex(normal, "Tex_SueloTierra_Terror_Normal.png", true);
        }

        private static Texture2D GenerateTerrorMistCloudTexture()
        {
            int s = 256;
            Texture2D tex = new Texture2D(s, s, TextureFormat.RGBA32, false);
            for (int y = 0; y < s; y++)
            {
                float v = (float)y / (s - 1);
                for (int x = 0; x < s; x++)
                {
                    float u = (float)x / (s - 1);
                    float dx = (u - 0.5f) * 2.0f;
                    float horizFade = Mathf.SmoothStep(0.0f, 1.0f, Mathf.Clamp01(1.0f - Mathf.Abs(dx)));
                    // Más densa cerca del suelo (v bajo) y desvaneciéndose hacia arriba
                    float vertFade  = Mathf.SmoothStep(0.0f, 0.22f, v) * Mathf.Pow(Mathf.Clamp01(1.0f - v), 1.35f);

                    float n1 = Mathf.PerlinNoise(u * 3.2f + 2.1f, v * 2.2f + 4.3f);
                    float n2 = Mathf.PerlinNoise(u * 7.5f + 5.8f, v * 4.5f + 1.9f) * 0.35f;
                    float cloud = Mathf.Clamp01(0.48f + n1 * 0.42f + n2 * 0.22f);

                    float a = horizFade * vertFade * cloud * 0.52f;
                    // Niebla baja azul-gris verdosa (#0F1A1C)
                    tex.SetPixel(x, y, new Color(0.059f, 0.102f, 0.110f, a));
                }
            }
            tex.Apply();
            return SaveTex(tex, "Tex_Niebla_Terror_Baja.png", false, TextureWrapMode.Clamp);
        }

        private static Texture2D GenerateTerrorSkyPanoramaTexture()
        {
            int w = 2048, h = 1024;
            Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false);

            // Cielo Terror: nublado oscuro, SIN estrellas nítidas, con luna apenas visible tras nubes espesas y horizonte fundido en #0F1A1C
            Color horizonFogTeal   = new Color(0.059f, 0.102f, 0.110f, 1.0f); // #0F1A1C
            Color distantHillsDark = new Color(0.032f, 0.058f, 0.076f, 1.0f); // #070D12
            Color overcastMid      = new Color(0.055f, 0.095f, 0.112f, 1.0f);
            Color overcastZenith   = new Color(0.030f, 0.052f, 0.068f, 1.0f);
            Color stormCloudHighlight = new Color(0.135f, 0.210f, 0.235f, 1.0f);
            Color veiledMoonGlow   = new Color(0.48f, 0.64f, 0.72f, 1.0f);

            // Posición de la luna velada tras las nubes (en el cuadrante visible por las cámaras frontales/esquina: u ~ 0.14, v ~ 0.565)
            float moonU = 0.145f;
            float moonV = 0.562f;

            for (int y = 0; y < h; y++)
            {
                float v = (float)y / (h - 1);
                for (int x = 0; x < w; x++)
                {
                    float u = (float)x / (w - 1);
                    float ang = u * Mathf.PI * 2.0f;
                    float hillWave = Mathf.Sin(ang * 3.0f + 0.8f) * 0.35f + Mathf.Cos(ang * 7.0f - 1.4f) * 0.25f;
                    float hillHeight = 0.008f + Mathf.Clamp01(hillWave * 0.5f + 0.5f) * 0.014f;

                    Color c;
                    if (v < 0.5f)
                    {
                        float t = Mathf.Clamp01((0.5f - v) * 5.0f);
                        c = Color.Lerp(horizonFogTeal, distantHillsDark, t);
                    }
                    else
                    {
                        float alt = (v - 0.5f) * 2.0f;
                        c = Color.Lerp(horizonFogTeal, overcastMid, Mathf.SmoothStep(0.0f, 1.0f, Mathf.Clamp01(alt * 2.8f)));
                        c = Color.Lerp(c, overcastZenith, Mathf.Pow(alt, 0.70f));

                        // Capas densas de nubes nocturnas (sin estrellas nítidas)
                        float c1 = TileableFbm(u, alt, 4.0f, 1.7f, 2.3f);
                        float c2 = TileableFbm(u, alt, 9.0f, 4.8f, 1.1f) * 0.55f;
                        float c3 = TileableFbm(u, alt, 18.0f, 7.2f, 5.4f) * 0.25f;
                        float cloudDensity = Mathf.Clamp01(c1 * 0.55f + c2 + c3);
                        float cloudBand = Mathf.SmoothStep(0.28f, 0.82f, cloudDensity) * Mathf.Clamp01(1.0f - alt * 0.85f);
                        c = Color.Lerp(c, stormCloudHighlight, cloudBand * 0.52f);

                        // Luna apenas visible tras las nubes (halo difuso frío velado por capas de nubes)
                        float du = Mathf.Min(Mathf.Abs(u - moonU), 1.0f - Mathf.Abs(u - moonU));
                        float dv = (v - moonV) * 0.5f;
                        float distMoon = Mathf.Sqrt(du * du + dv * dv);
                        float moonHalo = Mathf.Exp(-distMoon * 22.0f) * 0.42f + Mathf.Exp(-distMoon * 85.0f) * 0.48f;
                        // Las nubes tapan parcialmente el disco lunar para que se vea velada y difusa
                        float cloudVeil = Mathf.Lerp(0.35f, 0.95f, 1.0f - cloudDensity * 0.65f);
                        c = Color.Lerp(c, veiledMoonGlow, Mathf.Clamp01(moonHalo * cloudVeil));

                        // Colinas bajas y bruma espesa #0F1A1C en el horizonte
                        float vAboveHorizon = v - 0.5f;
                        if (vAboveHorizon < hillHeight + 0.006f)
                        {
                            float hillEdge = Mathf.SmoothStep(hillHeight + 0.004f, hillHeight - 0.002f, vAboveHorizon);
                            c = Color.Lerp(c, distantHillsDark, hillEdge * 0.75f);
                        }

                        float lowMistGlow = Mathf.Exp(-alt * 20.0f);
                        c = Color.Lerp(c, horizonFogTeal, lowMistGlow * 0.82f);
                    }
                    tex.SetPixel(x, y, c);
                }
            }
            tex.Apply();

            string fullPath = Path.Combine(TEX_DIR, "Tex_Cielo_Terror_Nublado_Panorama.png");
            File.WriteAllBytes(fullPath, tex.EncodeToPNG());
            AssetDatabase.ImportAsset(fullPath);
            TextureImporter ti = AssetImporter.GetAtPath(fullPath) as TextureImporter;
            if (ti != null)
            {
                ti.maxTextureSize = 2048;
                ti.textureCompression = TextureImporterCompression.Uncompressed;
                ti.filterMode = FilterMode.Bilinear;
                ti.wrapMode = TextureWrapMode.Repeat;
                ti.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(fullPath);
        }

        private static void BuildNormalFromHeight(float[,] h, int size, float strength, Texture2D normTex)
        {
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    int xL = (x - 1 + size) % size;
                    int xR = (x + 1) % size;
                    int yD = (y - 1 + size) % size;
                    int yU = (y + 1) % size;

                    float dx = (h[xR, y] - h[xL, y]) * strength;
                    float dy = (h[x, yU] - h[x, yD]) * strength;

                    Vector3 n = new Vector3(-dx, -dy, 1.0f).normalized;
                    normTex.SetPixel(x, y, new Color(n.x * 0.5f + 0.5f, n.y * 0.5f + 0.5f, n.z * 0.5f + 0.5f, 1.0f));
                }
            }
            normTex.Apply();
        }

        private static Texture2D SaveTex(Texture2D tex, string fileName, bool isNormal, TextureWrapMode wrap = TextureWrapMode.Repeat)
        {
            string fullPath = Path.Combine(TEX_DIR, fileName);
            File.WriteAllBytes(fullPath, tex.EncodeToPNG());
            AssetDatabase.ImportAsset(fullPath);
            TextureImporter ti = AssetImporter.GetAtPath(fullPath) as TextureImporter;
            if (ti != null)
            {
                ti.textureType = isNormal ? TextureImporterType.NormalMap : TextureImporterType.Default;
                ti.wrapMode = wrap;
                // Para texturas Albedo que guardan el mapa de Smoothness en el canal Alpha, desactivar alphaIsTransparency evita que Unity altere los colores RGB
                ti.alphaIsTransparency = false;
                ti.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(fullPath);
        }

        // ----------------------------------------------------------------------
        // PASO 3 - MATERIALES (URP Lit) CON SMOOTHNESS VARIADO POR MÁSCARA
        // ----------------------------------------------------------------------
        private static Dictionary<string, Material> CreateURPMaterials(
            Texture2D concAlb, Texture2D concNorm,
            Texture2D concTerrorAlb, Texture2D concTerrorNorm,
            Texture2D greenAlb, Texture2D greenNorm,
            Texture2D groundAlb, Texture2D groundNorm, Texture2D groundDetAlb, Texture2D groundDetNorm,
            Texture2D groundTerrorAlb, Texture2D groundTerrorNorm,
            Texture2D gravelAlb, Texture2D gravelNorm,
            Texture2D glassAlb, Texture2D glassNorm, Texture2D glassEmit,
            Texture2D frameAlb,
            Texture2D volConeTex, Texture2D contactAOTex, Texture2D darkPatchTex, Texture2D mistNoiseTex, Texture2D mistTerrorTex, Texture2D nightSkyTex, Texture2D terrorSkyTex)
        {
            var dict = new Dictionary<string, Material>();
            Shader urpLit = Shader.Find("Universal Render Pipeline/Lit");
            if (urpLit == null) urpLit = Shader.Find("Standard");

            // 1. Concreto NOCHE (Intacto): Albedo gris cálido (NO verde ni azul), Smoothness 0.15-0.25 variado con máscara (Albedo Alpha), Normal map moderado (0.40)
            Material mConc = new Material(urpLit);
            mConc.SetTexture("_BaseMap", concAlb);
            mConc.SetColor("_BaseColor", new Color(1.00f, 0.96f, 0.90f, 1.0f));
            mConc.SetTexture("_BumpMap", concNorm);
            mConc.SetFloat("_BumpScale", 0.40f);
            mConc.SetFloat("_Smoothness", 1.0f); // Multiplicador de la máscara en Albedo Alpha (0.15 - 0.25)
            mConc.SetFloat("_SmoothnessTextureChannel", 1.0f); // 1 = Albedo Alpha
            mConc.SetFloat("_Metallic", 0.0f);
            mConc.EnableKeyword("_NORMALMAP");
            mConc.EnableKeyword("_SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A");
            SaveMat(mConc, "Mat_Concreto_Viejo.mat");
            dict["concreto"] = mConc;

            // 1b. Concreto TERROR: Mayor humedad, manchas de suciedad bajo voladizos y en la base, vetas de goteo verticales suaves, sin ruido
            Material mConcTerror = new Material(urpLit);
            mConcTerror.SetTexture("_BaseMap", concTerrorAlb);
            mConcTerror.SetColor("_BaseColor", new Color(0.95f, 0.93f, 0.89f, 1.0f));
            mConcTerror.SetTexture("_BumpMap", concTerrorNorm);
            mConcTerror.SetFloat("_BumpScale", 0.44f);
            mConcTerror.SetFloat("_Smoothness", 1.0f);
            mConcTerror.SetFloat("_SmoothnessTextureChannel", 1.0f);
            mConcTerror.SetFloat("_Metallic", 0.0f);
            mConcTerror.EnableKeyword("_NORMALMAP");
            mConcTerror.EnableKeyword("_SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A");
            SaveMat(mConcTerror, "Mat_Concreto_Terror.mat");
            dict["concreto_terror"] = mConcTerror;

            // 2. Paneles Verdes (2 variantes sutiles de tono entre paneles, Smoothness 0.35-0.45 variado por máscara)
            Material mGreenA = new Material(urpLit);
            mGreenA.SetTexture("_BaseMap", greenAlb);
            mGreenA.SetColor("_BaseColor", new Color(0.92f, 1.02f, 0.90f, 1.0f));
            mGreenA.SetTexture("_BumpMap", greenNorm);
            mGreenA.SetFloat("_BumpScale", 0.32f);
            mGreenA.SetFloat("_Smoothness", 1.0f); // En Albedo Alpha está mapeado 0.35 - 0.45
            mGreenA.SetFloat("_SmoothnessTextureChannel", 1.0f);
            mGreenA.SetFloat("_Metallic", 0.02f);
            mGreenA.EnableKeyword("_NORMALMAP");
            mGreenA.EnableKeyword("_SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A");
            SaveMat(mGreenA, "Mat_Panel_Verde_Bosque.mat");
            dict["panel_verde"] = mGreenA;

            Material mGreenB = new Material(mGreenA);
            mGreenB.SetColor("_BaseColor", new Color(0.86f, 0.93f, 0.89f, 1.0f));
            SaveMat(mGreenB, "Mat_Panel_Verde_Bosque_VarB.mat");
            dict["panel_verde_b"] = mGreenB;

            // 3. Marcos Claros Mates (para ventanas) y Cancelería Oscura Mate (para juntas/puerta) con Smoothness variado
            Material mFrameClear = new Material(urpLit);
            mFrameClear.SetTexture("_BaseMap", frameAlb);
            mFrameClear.SetColor("_BaseColor", new Color(0.95f, 0.96f, 0.98f, 1.0f));
            mFrameClear.SetFloat("_Metallic", 0.05f);
            mFrameClear.SetFloat("_Smoothness", 1.0f); // En Albedo Alpha (0.14 - 0.24)
            mFrameClear.SetFloat("_SmoothnessTextureChannel", 1.0f);
            mFrameClear.EnableKeyword("_SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A");
            SaveMat(mFrameClear, "Mat_Marcos_Claros_Ventana.mat");
            dict["marco_claro"] = mFrameClear;

            Material mMetal = new Material(urpLit);
            mMetal.SetTexture("_BaseMap", frameAlb);
            mMetal.SetColor("_BaseColor", new Color(0.18f, 0.20f, 0.23f, 1.0f));
            mMetal.SetFloat("_Metallic", 0.25f);
            mMetal.SetFloat("_Smoothness", 1.0f);
            mMetal.SetFloat("_SmoothnessTextureChannel", 1.0f);
            mMetal.EnableKeyword("_SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A");
            SaveMat(mMetal, "Mat_Canceleria_Oscura.mat");
            dict["metal_marco"] = mMetal;

            // 4. Vidrio Oscuro (emisión 0, reflejo tenue vía Reflection Probe)
            Material mGlassDark = new Material(urpLit);
            mGlassDark.SetTexture("_BaseMap", glassAlb);
            mGlassDark.SetColor("_BaseColor", new Color(0.85f, 0.92f, 1.00f, 1.0f));
            mGlassDark.SetTexture("_BumpMap", glassNorm);
            mGlassDark.SetFloat("_BumpScale", 0.12f);
            mGlassDark.SetFloat("_Metallic", 0.22f);
            mGlassDark.SetFloat("_Smoothness", 1.0f); // En Albedo Alpha (0.85 - 0.90)
            mGlassDark.SetFloat("_SmoothnessTextureChannel", 1.0f);
            mGlassDark.EnableKeyword("_NORMALMAP");
            mGlassDark.EnableKeyword("_SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A");
            SaveMat(mGlassDark, "Mat_Vidrio_Oscuro.mat");
            dict["vidrio_oscuro"] = mGlassDark;

            // 5. Ventanas Encendidas NOCHE (Intactas): Ámbar cálido (~2700 K), rango 0.5x a 1.2x + algunas casi apagadas
            Color warmAmber2700K = new Color(1.0f, 0.54f, 0.19f); // Ámbar cálido ~2700 K (sin amarillo saturado)
            Color warmAmberBase  = new Color(0.42f, 0.28f, 0.17f, 1.0f);
            float baseWindowEmission = 1.10f; // Reducido otro 20% respecto al anterior (~1.40 -> 1.10)
            float[] windowMultipliers = new float[] { 0.28f, 0.50f, 0.72f, 0.95f, 1.20f }; // Algunas casi apagadas + rango 0.5x a 1.2x
            string[] dimKeys = new string[] { "vidrio_tenue_0", "vidrio_tenue_1", "vidrio_tenue_2", "vidrio_tenue_3", "vidrio_tenue_4" };
            for (int i = 0; i < windowMultipliers.Length; i++)
            {
                Material mGlassDim = new Material(urpLit);
                mGlassDim.SetTexture("_BaseMap", glassAlb);
                mGlassDim.SetColor("_BaseColor", Color.Lerp(new Color(0.18f, 0.18f, 0.20f, 1.0f), warmAmberBase, Mathf.Clamp01(windowMultipliers[i])));
                mGlassDim.SetTexture("_BumpMap", glassNorm);
                mGlassDim.SetFloat("_BumpScale", 0.12f);
                mGlassDim.SetFloat("_Metallic", 0.12f);
                mGlassDim.SetFloat("_Smoothness", 1.0f); // Conserva Smoothness 0.85-0.90 para reflejar el entorno además de emitir luz tenue
                mGlassDim.SetFloat("_SmoothnessTextureChannel", 1.0f);
                mGlassDim.SetTexture("_EmissionMap", glassEmit);
                mGlassDim.SetColor("_EmissionColor", warmAmber2700K * (baseWindowEmission * windowMultipliers[i]));
                mGlassDim.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
                mGlassDim.EnableKeyword("_NORMALMAP");
                mGlassDim.EnableKeyword("_SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A");
                mGlassDim.EnableKeyword("_EMISSION");
                SaveMat(mGlassDim, i == 0 ? "Mat_Vidrio_Luz_Tenue.mat" : $"Mat_Vidrio_Luz_Tenue_Var{i + 1}.mat");
                dict[dimKeys[i]] = mGlassDim;
            }
            dict["vidrio_tenue"] = dict["vidrio_tenue_2"];

            // 5b. Ventanas TERROR: Casi todas APAGADAS (usan vidrio_oscuro con emisión 0).
            //     Solo 2 ventanas con ámbar muy débil y variación de intensidad (0.35 y 0.56), más 1 sola ventana con acento frío verde-teal tenue desde dentro
            Material mTerrorAmberWeakA = new Material(urpLit);
            mTerrorAmberWeakA.SetTexture("_BaseMap", glassAlb);
            mTerrorAmberWeakA.SetColor("_BaseColor", new Color(0.32f, 0.24f, 0.18f, 1.0f));
            mTerrorAmberWeakA.SetTexture("_BumpMap", glassNorm);
            mTerrorAmberWeakA.SetFloat("_BumpScale", 0.12f);
            mTerrorAmberWeakA.SetFloat("_Metallic", 0.14f);
            mTerrorAmberWeakA.SetFloat("_Smoothness", 1.0f);
            mTerrorAmberWeakA.SetFloat("_SmoothnessTextureChannel", 1.0f);
            mTerrorAmberWeakA.SetTexture("_EmissionMap", glassEmit);
            mTerrorAmberWeakA.SetColor("_EmissionColor", warmAmber2700K * 0.48f);
            mTerrorAmberWeakA.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            mTerrorAmberWeakA.EnableKeyword("_NORMALMAP");
            mTerrorAmberWeakA.EnableKeyword("_SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A");
            mTerrorAmberWeakA.EnableKeyword("_EMISSION");
            SaveMat(mTerrorAmberWeakA, "Mat_Vidrio_Terror_AmbarDebil_A.mat");
            dict["vidrio_terror_ambar_a"] = mTerrorAmberWeakA;

            Material mTerrorAmberWeakB = new Material(mTerrorAmberWeakA);
            mTerrorAmberWeakB.SetColor("_EmissionColor", warmAmber2700K * 0.72f);
            SaveMat(mTerrorAmberWeakB, "Mat_Vidrio_Terror_AmbarDebil_B.mat");
            dict["vidrio_terror_ambar_b"] = mTerrorAmberWeakB;

            // Ventana única con acento frío verde-teal tenue desde dentro (para sugerir presencia sin mostrar nada)
            Material mTerrorTealWindow = new Material(urpLit);
            mTerrorTealWindow.SetTexture("_BaseMap", glassAlb);
            mTerrorTealWindow.SetColor("_BaseColor", new Color(0.16f, 0.32f, 0.30f, 1.0f));
            mTerrorTealWindow.SetTexture("_BumpMap", glassNorm);
            mTerrorTealWindow.SetFloat("_BumpScale", 0.12f);
            mTerrorTealWindow.SetFloat("_Metallic", 0.12f);
            mTerrorTealWindow.SetFloat("_Smoothness", 1.0f);
            mTerrorTealWindow.SetFloat("_SmoothnessTextureChannel", 1.0f);
            mTerrorTealWindow.SetTexture("_EmissionMap", glassEmit);
            mTerrorTealWindow.SetColor("_EmissionColor", new Color(0.18f, 0.72f, 0.65f) * 0.95f);
            mTerrorTealWindow.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            mTerrorTealWindow.EnableKeyword("_NORMALMAP");
            mTerrorTealWindow.EnableKeyword("_SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A");
            mTerrorTealWindow.EnableKeyword("_EMISSION");
            SaveMat(mTerrorTealWindow, "Mat_Vidrio_Terror_Presencia_Teal.mat");
            dict["vidrio_terror_teal"] = mTerrorTealWindow;

            // 6. Vidrio de Entrada Principal NOCHE (Intacto) + Variante TERROR (intensidad reducida)
            Material mGlassEnt = new Material(urpLit);
            mGlassEnt.SetTexture("_BaseMap", glassAlb);
            mGlassEnt.SetColor("_BaseColor", new Color(0.78f, 0.50f, 0.26f, 1.0f));
            mGlassEnt.SetFloat("_Smoothness", 1.0f);
            mGlassEnt.SetFloat("_SmoothnessTextureChannel", 1.0f);
            mGlassEnt.SetTexture("_EmissionMap", glassEmit);
            mGlassEnt.SetColor("_EmissionColor", warmAmber2700K * 2.45f); // Reducido ~20% desde 3.10 hacia ámbar cálido
            mGlassEnt.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            mGlassEnt.EnableKeyword("_SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A");
            mGlassEnt.EnableKeyword("_EMISSION");
            SaveMat(mGlassEnt, "Mat_Vidrio_Entrada_Calida.mat");
            dict["vidrio_entrada"] = mGlassEnt;

            Material mGlassEntTerror = new Material(mGlassEnt);
            mGlassEntTerror.SetColor("_BaseColor", new Color(0.54f, 0.36f, 0.20f, 1.0f));
            mGlassEntTerror.SetColor("_EmissionColor", warmAmber2700K * 1.15f); // Más tenue e inquietante en modo Terror
            SaveMat(mGlassEntTerror, "Mat_Vidrio_Entrada_Terror.mat");
            dict["vidrio_entrada_terror"] = mGlassEntTerror;

            // 7. Luminaria Emisiva 2700K (NOCHE) + Luminaria Apagada (TERROR) + Luminaria Débil Casi Muerta (TERROR)
            Material mEmit = new Material(urpLit);
            mEmit.SetColor("_BaseColor", new Color(1.0f, 0.82f, 0.56f, 1.0f));
            mEmit.SetFloat("_Smoothness", 0.40f);
            mEmit.SetTexture("_EmissionMap", glassEmit);
            mEmit.SetColor("_EmissionColor", new Color(1.0f, 0.64f, 0.28f) * 4.2f);
            mEmit.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            mEmit.EnableKeyword("_EMISSION");
            SaveMat(mEmit, "Mat_Luminaria_Emisiva_2700K.mat");
            dict["luz_emisiva"] = mEmit;

            Material mEmitOff = new Material(urpLit);
            mEmitOff.SetColor("_BaseColor", new Color(0.14f, 0.15f, 0.16f, 1.0f));
            mEmitOff.SetFloat("_Smoothness", 0.22f);
            mEmitOff.SetFloat("_Metallic", 0.20f);
            SaveMat(mEmitOff, "Mat_Luminaria_Apagada_Terror.mat");
            dict["luz_apagada_terror"] = mEmitOff;

            Material mEmitDying = new Material(urpLit);
            mEmitDying.SetColor("_BaseColor", new Color(0.55f, 0.42f, 0.25f, 1.0f));
            mEmitDying.SetFloat("_Smoothness", 0.32f);
            mEmitDying.SetTexture("_EmissionMap", glassEmit);
            mEmitDying.SetColor("_EmissionColor", new Color(1.0f, 0.56f, 0.22f) * 0.95f);
            mEmitDying.EnableKeyword("_EMISSION");
            SaveMat(mEmitDying, "Mat_Luminaria_Debil_Terror.mat");
            dict["luz_debil_terror"] = mEmitDying;

            // 8. Gravilla de Azotea (Smoothness 0.12-0.22 variado con máscara, gris neutro oscuro)
            Material mGravel = new Material(urpLit);
            mGravel.SetTexture("_BaseMap", gravelAlb);
            mGravel.SetColor("_BaseColor", new Color(0.95f, 0.95f, 0.97f, 1.0f));
            mGravel.SetTexture("_BumpMap", gravelNorm);
            mGravel.SetFloat("_BumpScale", 0.35f);
            mGravel.SetFloat("_Smoothness", 1.0f);
            mGravel.SetFloat("_SmoothnessTextureChannel", 1.0f);
            mGravel.SetFloat("_Metallic", 0.0f);
            mGravel.mainTextureScale = new Vector2(3.0f, 2.0f);
            mGravel.EnableKeyword("_NORMALMAP");
            mGravel.EnableKeyword("_SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A");
            SaveMat(mGravel, "Mat_Azotea_Gravilla_Oscura.mat");
            dict["azotea_gravilla"] = mGravel;

            Material mGravelTerror = new Material(mGravel);
            mGravelTerror.SetColor("_BaseColor", new Color(1.32f, 1.38f, 1.42f, 1.0f)); // Mayor respuesta a la tenue luz de luna 8000K para que las losas y vigas de la azotea se lean claramente en la vista aérea
            SaveMat(mGravelTerror, "Mat_Azotea_Gravilla_Terror.mat");
            dict["azotea_gravilla_terror"] = mGravelTerror;

            // 9. Suelo Exterior NOCHE (Intacto): Macro-variación ~18m + Detail Map + Smoothness 0.10-0.20
            Material mGround = new Material(urpLit);
            mGround.SetTexture("_BaseMap", groundAlb);
            mGround.SetColor("_BaseColor", new Color(1.16f, 1.12f, 1.05f, 1.0f));
            mGround.SetTexture("_BumpMap", groundNorm);
            mGround.SetFloat("_BumpScale", 0.56f);
            mGround.SetFloat("_Smoothness", 1.0f); // En Albedo Alpha (0.10 - 0.20, sin charcos brillantes ni aspecto mojado)
            mGround.SetFloat("_SmoothnessTextureChannel", 1.0f);
            mGround.SetFloat("_Metallic", 0.0f);
            mGround.mainTextureScale = new Vector2(0.22f, 0.22f);
            mGround.SetTexture("_DetailAlbedoMap", groundDetAlb);
            mGround.SetTextureScale("_DetailAlbedoMap", new Vector2(8.3f, 7.9f));
            mGround.SetFloat("_DetailAlbedoMapScale", 0.72f);
            mGround.SetTexture("_DetailNormalMap", groundDetNorm);
            mGround.SetTextureScale("_DetailNormalMap", new Vector2(8.3f, 7.9f));
            mGround.SetFloat("_DetailNormalMapScale", 0.48f);
            mGround.EnableKeyword("_NORMALMAP");
            mGround.EnableKeyword("_SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A");
            mGround.EnableKeyword("_DETAIL_MULX2");
            SaveMat(mGround, "Mat_Suelo_Tierra_Roca.mat");
            dict["suelo"] = mGround;

            // 9b. Suelo Exterior TERROR: Húmedo SOLO en parches grandes con Smoothness 0.30-0.45 que reflejen las luces cálidas, y el resto mate (0.10-0.20)
            Material mGroundTerror = new Material(urpLit);
            mGroundTerror.SetTexture("_BaseMap", groundTerrorAlb);
            mGroundTerror.SetColor("_BaseColor", new Color(1.04f, 1.02f, 0.98f, 1.0f));
            mGroundTerror.SetTexture("_BumpMap", groundTerrorNorm);
            mGroundTerror.SetFloat("_BumpScale", 0.52f);
            mGroundTerror.SetFloat("_Smoothness", 1.0f); // En Albedo Alpha: 0.32-0.45 en parches húmedos, 0.10-0.20 en zonas secas
            mGroundTerror.SetFloat("_SmoothnessTextureChannel", 1.0f);
            mGroundTerror.SetFloat("_Metallic", 0.0f);
            mGroundTerror.mainTextureScale = new Vector2(0.22f, 0.22f);
            mGroundTerror.SetTexture("_DetailAlbedoMap", groundDetAlb);
            mGroundTerror.SetTextureScale("_DetailAlbedoMap", new Vector2(8.3f, 7.9f));
            mGroundTerror.SetFloat("_DetailAlbedoMapScale", 0.68f);
            mGroundTerror.SetTexture("_DetailNormalMap", groundDetNorm);
            mGroundTerror.SetTextureScale("_DetailNormalMap", new Vector2(8.3f, 7.9f));
            mGroundTerror.SetFloat("_DetailNormalMapScale", 0.44f);
            mGroundTerror.EnableKeyword("_NORMALMAP");
            mGroundTerror.EnableKeyword("_SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A");
            mGroundTerror.EnableKeyword("_DETAIL_MULX2");
            SaveMat(mGroundTerror, "Mat_Suelo_Terror_Humedo.mat");
            dict["suelo_terror"] = mGroundTerror;

            // 10. Conos de Luz Volumétricos NOCHE (Intactos) + TERROR (Opacidad baja para niebla densa)
            Material mVolCone = CreateTransparentParticleMat("Mat_Cono_Volumetrico.mat", volConeTex, new Color(1.0f, 0.76f, 0.40f, 0.11f), true);
            dict["vol_cone"] = mVolCone;

            Material mVolConeTerror = CreateTransparentParticleMat("Mat_Cono_Volumetrico_Terror.mat", volConeTex, new Color(1.0f, 0.72f, 0.36f, 0.068f), true);
            dict["vol_cone_terror"] = mVolConeTerror;

            // 11. Sombra de Contacto Perimetral (Alpha Blend)
            Material mContactAO = CreateTransparentParticleMat("Mat_Sombra_Contacto_Edificio.mat", contactAOTex, new Color(1f, 1f, 1f, 0.78f), false);
            dict["contact_ao"] = mContactAO;

            // 11b. Manchas de tierra más oscura cerca de la base del edificio
            Material mDarkPatch = CreateTransparentParticleMat("Mat_Mancha_Tierra_Oscura_Base.mat", darkPatchTex, new Color(1f, 1f, 1f, 0.72f), false);
            dict["dark_patch"] = mDarkPatch;

            // 12. Tarjetas de Bruma Horizonte NOCHE (#1A2433) + Niebla Baja TERROR (#0F1A1C más espesa cerca del suelo)
            Material mMist = CreateTransparentParticleMat("Mat_Niebla_Volumetrica_Baja.mat", mistNoiseTex, new Color(0.18f, 0.25f, 0.36f, 0.34f), false);
            dict["mist"] = mMist;

            Material mMistTerror = CreateTransparentParticleMat("Mat_Niebla_Terror_Suelo.mat", mistTerrorTex, new Color(0.10f, 0.17f, 0.18f, 0.44f), false);
            dict["mist_terror"] = mMistTerror;

            // 13. Colinas bajas lejanas en silueta oscura en el horizonte
            Material mHills = new Material(urpLit);
            mHills.SetColor("_BaseColor", new Color(0.055f, 0.075f, 0.110f, 1.0f));
            mHills.SetFloat("_Smoothness", 0.08f);
            mHills.SetFloat("_Metallic", 0.0f);
            SaveMat(mHills, "Mat_Colinas_Lejanas_Horizonte.mat");
            dict["distant_hills"] = mHills;

            Material mHillsTerror = new Material(urpLit);
            mHillsTerror.SetColor("_BaseColor", new Color(0.027f, 0.051f, 0.071f, 1.0f)); // #070D12
            mHillsTerror.SetFloat("_Smoothness", 0.06f);
            mHillsTerror.SetFloat("_Metallic", 0.0f);
            SaveMat(mHillsTerror, "Mat_Colinas_Lejanas_Terror.mat");
            dict["distant_hills_terror"] = mHillsTerror;

            // 14. Skybox Nocturno (NOCHE intacto) + Skybox Nublado con Luna Velada (TERROR)
            Shader skyPano = Shader.Find("Skybox/Panoramic");
            if (skyPano != null)
            {
                Material mSky = new Material(skyPano);
                mSky.SetTexture("_MainTex", nightSkyTex);
                mSky.SetColor("_Tint", new Color(0.50f, 0.54f, 0.62f, 1.0f));
                mSky.SetFloat("_Exposure", 1.00f);
                SaveMat(mSky, "Mat_Skybox_Nocturno_Docencia2.mat");
                dict["skybox"] = mSky;

                Material mSkyTerror = new Material(skyPano);
                mSkyTerror.SetTexture("_MainTex", terrorSkyTex);
                mSkyTerror.SetColor("_Tint", new Color(0.56f, 0.64f, 0.66f, 1.0f));
                mSkyTerror.SetFloat("_Exposure", 1.15f);
                SaveMat(mSkyTerror, "Mat_Skybox_Terror_Docencia2.mat");
                dict["skybox_terror"] = mSkyTerror;
            }

            return dict;
        }

        private static Material CreateTransparentParticleMat(string fileName, Texture2D tex, Color color, bool additive)
        {
            Shader sh = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (sh == null) sh = Shader.Find("Universal Render Pipeline/Unlit");
            Material m = new Material(sh);
            m.SetTexture("_BaseMap", tex);
            m.SetColor("_BaseColor", color);
            m.SetFloat("_Surface", 1.0f);
            m.SetFloat("_Blend", additive ? 2.0f : 0.0f);
            m.SetFloat("_ZWrite", 0.0f);
            m.SetFloat("_Cull", 0.0f);
            if (additive)
            {
                m.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
                m.SetInt("_DstBlend", (int)BlendMode.One);
            }
            else
            {
                m.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
                m.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
            }
            m.renderQueue = 3000;
            SaveMat(m, fileName);
            return m;
        }

        private static void SaveMat(Material m, string fileName)
        {
            string p = Path.Combine(MAT_DIR, fileName);
            if (AssetDatabase.LoadAssetAtPath<Material>(p) != null)
                AssetDatabase.DeleteAsset(p);
            AssetDatabase.CreateAsset(m, p);
        }

        private static void AssignMaterialsToHierarchy(
            GameObject root,
            Dictionary<string, Material> mats,
            List<Docencia2ModeSwitcher.RendererMaterialPair> matSwaps)
        {
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
            int litCount = 0;
            foreach (Renderer r in renderers)
            {
                string n = r.gameObject.name;
                Material shared = r.sharedMaterial;
                string matName = (shared != null) ? shared.name : "";

                if (r.sharedMaterials != null && r.sharedMaterials.Length > 1)
                {
                    Material[] nightArr = (Material[])r.sharedMaterials.Clone();
                    Material[] terrorArr = new Material[nightArr.Length];
                    for (int mIdx = 0; mIdx < nightArr.Length; mIdx++)
                    {
                        Material sm = nightArr[mIdx];
                        string smName = sm != null ? sm.name : "";
                        if (smName.Contains("Panel_Verde") || smName.Contains("Verde"))
                        {
                            bool useVarB = n.EndsWith("_2") || n.Contains("Sec02") || n.Contains("Sec05") || n.Contains("Trasero");
                            nightArr[mIdx] = useVarB ? mats["panel_verde_b"] : mats["panel_verde"];
                            terrorArr[mIdx] = nightArr[mIdx];
                        }
                        else if (smName.Contains("Marco") && !smName.Contains("Canceleria"))
                        {
                            nightArr[mIdx] = mats["marco_claro"];
                            terrorArr[mIdx] = mats["marco_claro"];
                        }
                        else if (smName.Contains("Canceleria") || smName.Contains("Metal"))
                        {
                            nightArr[mIdx] = mats["metal_marco"];
                            terrorArr[mIdx] = mats["metal_marco"];
                        }
                        else if (smName.Contains("Entrada"))
                        {
                            nightArr[mIdx] = mats["vidrio_entrada"];
                            terrorArr[mIdx] = mats["vidrio_entrada_terror"];
                        }
                        else if (smName.Contains("Luz_Tenue") || smName.Contains("Vidrio") || smName.Contains("Cristal"))
                        {
                            bool isLitNight = smName.Contains("Luz_Tenue") || IsIrregularLitWindowPane(n);
                            if (isLitNight)
                            {
                                int hash = Mathf.Abs((n + "_" + mIdx).GetHashCode());
                                int bucket = hash % 6;
                                string key = (bucket == 0) ? "vidrio_tenue_0" :
                                             (bucket == 1) ? "vidrio_tenue_1" :
                                             (bucket == 2 || bucket == 3) ? "vidrio_tenue_2" :
                                             (bucket == 4) ? "vidrio_tenue_3" : "vidrio_tenue_4";
                                nightArr[mIdx] = mats[key];
                                litCount++;
                            }
                            else
                            {
                                nightArr[mIdx] = mats["vidrio_oscuro"];
                            }

                            if (n.Contains("Sec06") && n.Contains("P2") && n.Contains("Frontal"))
                                terrorArr[mIdx] = mats["vidrio_terror_teal"];
                            else if (n.Contains("Sec04") && n.Contains("P1") && n.Contains("Frontal"))
                                terrorArr[mIdx] = mats["vidrio_terror_ambar_a"];
                            else if (n.Contains("Sec03") && n.Contains("P2") && n.Contains("Frontal"))
                                terrorArr[mIdx] = mats["vidrio_terror_ambar_b"];
                            else
                                terrorArr[mIdx] = mats["vidrio_oscuro"];
                        }
                        else if (smName.Contains("Gravilla"))
                        {
                            nightArr[mIdx] = mats["azotea_gravilla"];
                            terrorArr[mIdx] = mats["azotea_gravilla_terror"];
                        }
                        else
                        {
                            nightArr[mIdx] = mats["concreto"];
                            terrorArr[mIdx] = mats["concreto_terror"];
                        }
                    }
                    r.sharedMaterials = nightArr;
                    matSwaps.Add(new Docencia2ModeSwitcher.RendererMaterialPair
                    {
                        targetRenderer = r,
                        nightMaterial = nightArr[0],
                        terrorMaterial = terrorArr[0],
                        nightMaterialsArray = nightArr,
                        terrorMaterialsArray = terrorArr
                    });
                    continue;
                }

                if (matName.Contains("Panel_Verde") || n.Contains("PanelVerde") || n.Contains("MuroVerde") || n.Contains("PanoVerde") || n.Contains("Rejilla_Baja"))
                {
                    // Variación sutil de tono entre paneles verdes (izquierdo vs derecho / secciones pares vs impares)
                    bool useVarB = n.EndsWith("_2") || n.Contains("Sec02") || n.Contains("Sec05") || n.Contains("Trasero");
                    r.sharedMaterial = useVarB ? mats["panel_verde_b"] : mats["panel_verde"];
                }
                else if (n.Contains("Parteluz") || (n.Contains("Ventana") && n.Contains("Marco")))
                    r.sharedMaterial = mats["marco_claro"];
                else if (matName.Contains("Canceleria") || (n.Contains("Marco") && !n.Contains("MarcoConcreto")) || n.Contains("Canceleria") || n.Contains("Manija") || n.Contains("Carcasa") || n.Contains("Junta"))
                    r.sharedMaterial = mats["metal_marco"];
                else if (matName.Contains("Entrada_Calida") || n.Contains("Vidrio_Entrada"))
                {
                    r.sharedMaterial = mats["vidrio_entrada"];
                    matSwaps.Add(new Docencia2ModeSwitcher.RendererMaterialPair
                    {
                        targetRenderer = r,
                        nightMaterial = mats["vidrio_entrada"],
                        terrorMaterial = mats["vidrio_entrada_terror"]
                    });
                }
                else if (matName.Contains("Luz_Tenue") || n.Contains("_Tenue") || IsIrregularLitWindowPane(n))
                {
                    // Modo NOCHE: Variar la intensidad por ventana en rango 0.5x a 1.2x, con algunas casi apagadas (0.28x)
                    int hash = Mathf.Abs(n.GetHashCode());
                    int bucket = hash % 6;
                    string key = (bucket == 0) ? "vidrio_tenue_0" : // Casi apagada (0.28x)
                                 (bucket == 1) ? "vidrio_tenue_1" : // 0.50x
                                 (bucket == 2 || bucket == 3) ? "vidrio_tenue_2" : // 0.72x
                                 (bucket == 4) ? "vidrio_tenue_3" : // 0.95x
                                                 "vidrio_tenue_4";  // 1.20x
                    Material nightWinMat = mats[key];
                    r.sharedMaterial = nightWinMat;
                    litCount++;

                    // Modo TERROR: Casi todas las ventanas APAGADAS (emisión 0 -> vidrio_oscuro).
                    // Solo 2 franjas/paneles con ámbar muy débil y variación de intensidad, y 1 sola ventana con acento frío verde-teal tenue desde dentro.
                    Material terrorWinMat = mats["vidrio_oscuro"];
                    if (n.Contains("Ventana_P2_Frontal_Sec06") && (n.Contains("_03") || n.Contains("_04")))
                    {
                        // Única ventana con acento frío verde-teal tenue desde el interior (sugiere presencia inquietante)
                        terrorWinMat = mats["vidrio_terror_teal"];
                    }
                    else if (n.Contains("Ventana_P1_Frontal_Sec04") && (n.Contains("_02") || n.Contains("_03")))
                    {
                        // Ventana 1 con ámbar muy débil (intensidad A)
                        terrorWinMat = mats["vidrio_terror_ambar_a"];
                    }
                    else if (n.Contains("Ventana_P2_Frontal_Sec03") && (n.Contains("_04") || n.Contains("_05")))
                    {
                        // Ventana 2 con ámbar muy débil (intensidad B)
                        terrorWinMat = mats["vidrio_terror_ambar_b"];
                    }

                    matSwaps.Add(new Docencia2ModeSwitcher.RendererMaterialPair
                    {
                        targetRenderer = r,
                        nightMaterial = nightWinMat,
                        terrorMaterial = terrorWinMat
                    });
                }
                else if (matName.Contains("Vidrio_Oscuro") || n.Contains("PanelCristal") || n.Contains("Cristal"))
                {
                    r.sharedMaterial = mats["vidrio_oscuro"];
                    Material terrorWinMat = mats["vidrio_oscuro"];
                    if (n.Contains("Ventana_P2_Frontal_Sec06") && (n.Contains("_03") || n.Contains("_04")))
                    {
                        terrorWinMat = mats["vidrio_terror_teal"];
                    }
                    else if (n.Contains("Ventana_P1_Frontal_Sec04") && (n.Contains("_02") || n.Contains("_03")))
                    {
                        terrorWinMat = mats["vidrio_terror_ambar_a"];
                    }
                    else if (n.Contains("Ventana_P2_Frontal_Sec03") && (n.Contains("_04") || n.Contains("_05")))
                    {
                        terrorWinMat = mats["vidrio_terror_ambar_b"];
                    }

                    if (terrorWinMat != mats["vidrio_oscuro"])
                    {
                        matSwaps.Add(new Docencia2ModeSwitcher.RendererMaterialPair
                        {
                            targetRenderer = r,
                            nightMaterial = mats["vidrio_oscuro"],
                            terrorMaterial = terrorWinMat
                        });
                    }
                }
                else if (matName.Contains("Emisiva") || n.Contains("_Luz") || n.Contains("_Emisor"))
                {
                    r.sharedMaterial = mats["luz_emisiva"];
                    // En modo Terror solo quedan encendidas las lámparas de la entrada (Sec05), la lámpara casi muerta (Sec06) y el contrafuerte frontal derecho; el resto APAGADAS
                    Material terrorBulbMat = mats["luz_apagada_terror"];
                    if (n.Contains("Sec05") || n.Contains("Entrada") || (n.Contains("Contrafuerte") && (n.Contains("Der") || n.Contains("_1_"))))
                    {
                        terrorBulbMat = mats["luz_emisiva"];
                    }
                    else if (n.Contains("Sec06"))
                    {
                        terrorBulbMat = mats["luz_debil_terror"];
                    }
                    matSwaps.Add(new Docencia2ModeSwitcher.RendererMaterialPair
                    {
                        targetRenderer = r,
                        nightMaterial = mats["luz_emisiva"],
                        terrorMaterial = terrorBulbMat
                    });
                }
                else if (matName.Contains("Gravilla") || n.Contains("Membrana_Gravilla"))
                {
                    r.sharedMaterial = mats["azotea_gravilla"];
                    matSwaps.Add(new Docencia2ModeSwitcher.RendererMaterialPair
                    {
                        targetRenderer = r,
                        nightMaterial = mats["azotea_gravilla"],
                        terrorMaterial = mats["azotea_gravilla_terror"]
                    });
                }
                else
                {
                    r.sharedMaterial = mats["concreto"];
                    matSwaps.Add(new Docencia2ModeSwitcher.RendererMaterialPair
                    {
                        targetRenderer = r,
                        nightMaterial = mats["concreto"],
                        terrorMaterial = mats["concreto_terror"]
                    });
                }
            }
            Debug.Log($"[Docencia2] Materiales PBR asignados a {renderers.Length} renderers (ventanas Noche: {litCount}, swaps Noche/Terror: {matSwaps.Count})");
        }

        private static bool IsIrregularLitWindowPane(string objName)
        {
            if (!objName.Contains("PanelCristal_")) return false;
            if (objName.Contains("Ventana_P1_Frontal_Sec02") ||
                objName.Contains("Ventana_P1_Frontal_Sec03") ||
                objName.Contains("Ventana_P1_Frontal_Sec04") ||
                objName.Contains("Ventana_P2_Frontal_Sec03") ||
                objName.Contains("Ventana_P2_Frontal_Sec04") ||
                objName.Contains("Ventana_P2_Frontal_Sec05") ||
                objName.Contains("Ventana_P2_Frontal_Sec06") ||
                objName.Contains("Ventana_P1_Trasera_Sec02") ||
                objName.Contains("Ventana_P1_Trasera_Sec03") ||
                objName.Contains("Ventana_P1_Trasera_Sec05") ||
                objName.Contains("Ventana_P2_Trasera_Sec03") ||
                objName.Contains("Ventana_P2_Trasera_Sec04"))
            {
                return true;
            }
            if ((objName.Contains("Ventana_P1_Frontal_Sec01") || objName.Contains("Ventana_P2_Frontal_Sec02") || objName.Contains("Ventana_P2_Frontal_Sec07")) &&
                (objName.EndsWith("_03") || objName.EndsWith("_04") || objName.EndsWith("_05")))
            {
                return true;
            }
            return false;
        }

        // ----------------------------------------------------------------------
        // SUELO ORGÁNICO CON MACRO-VARIACIÓN (~18m), DETAIL MAP Y MANCHAS/PIEDRAS JUNTO A LA BASE
        // ----------------------------------------------------------------------
        private static MeshRenderer CreateOrganicGroundAndDebris(
            Material groundMat, Material concreteMat, Material contactAOMat, Material darkPatchMat,
            List<Docencia2ModeSwitcher.RendererMaterialPair> matSwaps,
            Material groundTerrorMat, Material concreteTerrorMat)
        {
            GameObject envRoot = new GameObject("Entorno_Terreno_Roca_y_Escombros");

            Mesh terrainMesh = BuildDisplacedTerrainMesh(120, 420f);
            string meshPath = Path.Combine(MESH_DIR, "Mesh_Terreno_Organico.asset");
            if (AssetDatabase.LoadAssetAtPath<Mesh>(meshPath) != null)
                AssetDatabase.DeleteAsset(meshPath);
            AssetDatabase.CreateAsset(terrainMesh, meshPath);

            GameObject ground = new GameObject("Terreno_Tierra_y_Roca");
            ground.transform.SetParent(envRoot.transform);
            ground.transform.position = Vector3.zero;
            ground.AddComponent<MeshFilter>().sharedMesh = terrainMesh;
            MeshRenderer mr = ground.AddComponent<MeshRenderer>();
            mr.sharedMaterial = groundMat;
            mr.receiveShadows = true;
            mr.shadowCastingMode = ShadowCastingMode.On;
            GameObjectUtility.SetStaticEditorFlags(ground, StaticEditorFlags.ContributeGI | StaticEditorFlags.ReflectionProbeStatic);

            matSwaps.Add(new Docencia2ModeSwitcher.RendererMaterialPair
            {
                targetRenderer = mr,
                nightMaterial = groundMat,
                terrorMaterial = groundTerrorMat
            });

            // Sombra de contacto (Contact AO) donde el edificio toca el suelo
            GameObject aoQuad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            aoQuad.name = "Sombra_Contacto_Perimetral_Edificio";
            UnityEngine.Object.DestroyImmediate(aoQuad.GetComponent<Collider>());
            aoQuad.transform.SetParent(envRoot.transform);
            aoQuad.transform.position = new Vector3(0f, 0.025f, 0f);
            aoQuad.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            aoQuad.transform.localScale = new Vector3(68.0f, 40.0f, 1.0f);
            Renderer aoRen = aoQuad.GetComponent<Renderer>();
            aoRen.sharedMaterial = contactAOMat;
            aoRen.shadowCastingMode = ShadowCastingMode.Off;

            // Manchas suaves de tierra más oscura cerca de la base del edificio (rompiendo aún más la transición edificio-suelo)
            Vector3[] darkSoilPositions = new Vector3[]
            {
                new Vector3( 21.5f, 0.022f, -15.8f),
                new Vector3( 11.0f, 0.022f, -16.4f),
                new Vector3( -2.5f, 0.022f, -16.0f),
                new Vector3(-16.0f, 0.022f, -15.6f),
                new Vector3( 28.4f, 0.022f,  -7.5f),
                new Vector3( 28.6f, 0.022f,   5.2f),
                new Vector3( 27.8f, 0.022f, -17.2f),
                new Vector3(-27.8f, 0.022f, -17.0f),
            };
            Vector2[] darkSoilScales = new Vector2[]
            {
                new Vector2(9.5f, 4.2f),
                new Vector2(8.0f, 3.8f),
                new Vector2(10.5f, 4.5f),
                new Vector2(9.0f, 3.6f),
                new Vector2(4.2f, 8.5f),
                new Vector2(4.5f, 9.0f),
                new Vector2(6.5f, 5.5f),
                new Vector2(6.0f, 5.0f),
            };

            for (int p = 0; p < darkSoilPositions.Length; p++)
            {
                GameObject patch = GameObject.CreatePrimitive(PrimitiveType.Quad);
                patch.name = $"Mancha_Tierra_Oscura_Base_{p + 1:02d}";
                UnityEngine.Object.DestroyImmediate(patch.GetComponent<Collider>());
                patch.transform.SetParent(envRoot.transform);
                patch.transform.position = darkSoilPositions[p];
                patch.transform.rotation = Quaternion.Euler(90f, (p * 37f) % 360f, 0f);
                patch.transform.localScale = new Vector3(darkSoilScales[p].x, darkSoilScales[p].y, 1.0f);
                Renderer pr = patch.GetComponent<Renderer>();
                pr.sharedMaterial = darkPatchMat;
                pr.shadowCastingMode = ShadowCastingMode.Off;
            }

            // Pocas piedras pequeñas discretas cerca de la base del edificio
            UnityEngine.Random.InitState(303);
            Vector3[] pebbleZones = new Vector3[]
            {
                new Vector3(28.5f, 0.02f, -3.2f),
                new Vector3(28.2f, 0.02f, 2.8f),
                new Vector3(28.6f, 0.02f, -15.0f),
                new Vector3(9.2f, 0.02f, -16.8f),
                new Vector3(19.5f, 0.02f, -17.8f),
                new Vector3(-3.5f, 0.02f, -17.0f),
                new Vector3(-14.5f, 0.02f, -16.5f),
            };

            int rIdx = 0;
            foreach (Vector3 zCenter in pebbleZones)
            {
                for (int i = 0; i < 4; i++)
                {
                    GameObject stone = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                    stone.name = $"Piedra_Simple_{++rIdx:02d}";
                    UnityEngine.Object.DestroyImmediate(stone.GetComponent<Collider>());
                    stone.transform.SetParent(envRoot.transform);
                    Vector2 off = UnityEngine.Random.insideUnitCircle * 2.8f;
                    float sx = UnityEngine.Random.Range(0.10f, 0.24f);
                    float sy = UnityEngine.Random.Range(0.04f, 0.09f);
                    float sz = UnityEngine.Random.Range(0.08f, 0.20f);
                    stone.transform.position = zCenter + new Vector3(off.x, sy * 0.25f, off.y);
                    stone.transform.rotation = Quaternion.Euler(
                        UnityEngine.Random.Range(-10f, 10f),
                        UnityEngine.Random.Range(0f, 360f),
                        UnityEngine.Random.Range(-10f, 10f)
                    );
                    stone.transform.localScale = new Vector3(sx, sy, sz);
                    Renderer sr = stone.GetComponent<Renderer>();
                    bool isConc = (i == 0);
                    sr.sharedMaterial = isConc ? concreteMat : groundMat;
                    matSwaps.Add(new Docencia2ModeSwitcher.RendererMaterialPair
                    {
                        targetRenderer = sr,
                        nightMaterial = isConc ? concreteMat : groundMat,
                        terrorMaterial = isConc ? concreteTerrorMat : groundTerrorMat
                    });
                }
            }

            return mr;
        }

        private static Mesh BuildDisplacedTerrainMesh(int res, float totalSize)
        {
            Mesh m = new Mesh();
            m.name = "Terrain_Organic_Docencia2";
            int vCount = (res + 1) * (res + 1);
            Vector3[] verts = new Vector3[vCount];
            Vector2[] uvs = new Vector2[vCount];
            int[] tris = new int[res * res * 6];

            float half = totalSize * 0.5f;
            int idx = 0;
            for (int z = 0; z <= res; z++)
            {
                float fz = (float)z / res;
                float worldZ = Mathf.Lerp(-half, half, fz);
                for (int x = 0; x <= res; x++)
                {
                    float fx = (float)x / res;
                    float worldX = Mathf.Lerp(-half, half, fx);

                    float dx = Mathf.Max(0f, Mathf.Abs(worldX) - 29.5f);
                    float dz = Mathf.Max(0f, Mathf.Abs(worldZ) - 16.5f);
                    float distFromBuilding = Mathf.Sqrt(dx * dx + dz * dz);
                    float mask = Mathf.Clamp01(distFromBuilding / 12.0f);

                    float n1 = Mathf.PerlinNoise(fx * 6.0f + 1.4f, fz * 6.0f + 2.7f) - 0.5f;
                    float n2 = (Mathf.PerlinNoise(fx * 14.0f, fz * 14.0f) - 0.5f) * 0.30f;
                    float y = (n1 * 0.35f + n2 * 0.14f) * mask;

                    verts[idx] = new Vector3(worldX, y, worldZ);
                    // Misma densidad de texel que el edificio: 1 ciclo UV = 16 metros
                    uvs[idx] = new Vector2(worldX / 16.0f, worldZ / 16.0f);
                    idx++;
                }
            }

            int t = 0;
            for (int z = 0; z < res; z++)
            {
                for (int x = 0; x < res; x++)
                {
                    int i0 = z * (res + 1) + x;
                    int i1 = i0 + 1;
                    int i2 = i0 + (res + 1);
                    int i3 = i2 + 1;

                    tris[t++] = i0; tris[t++] = i2; tris[t++] = i1;
                    tris[t++] = i1; tris[t++] = i2; tris[t++] = i3;
                }
            }

            m.vertices = verts;
            m.uv = uvs;
            m.triangles = tris;
            m.RecalculateNormals();
            m.RecalculateBounds();
            return m;
        }

        // ----------------------------------------------------------------------
        // PASO 1 Y PASO 2 - AMBIENTE, NIEBLA, LUCES (NOCHE INTACTA + TERROR), GI, APV Y REFLECTION PROBES
        // ----------------------------------------------------------------------
        private static List<ReflectionProbe> SetupAtmosphereAndLighting(
            Dictionary<string, Material> mats,
            out GameObject lightsNightRoot, out GameObject conesNightRoot, out GameObject fogNightRoot,
            out GameObject lightsTerrorRoot, out GameObject conesTerrorRoot, out GameObject fogTerrorRoot)
        {
            List<ReflectionProbe> probes = new List<ReflectionProbe>();
            lightsNightRoot = new GameObject("Iluminacion_Cinematografica_Docencia2");
            conesNightRoot  = new GameObject("Conos_Luz_Volumetricos");

            GameObject doorObj = GameObject.Find("Vidrio_Entrada_Total");
            float frontSignZ = -1.0f;
            float entranceSignX = 1.0f;
            if (doorObj != null && doorObj.GetComponent<Renderer>() != null)
            {
                Vector3 c = doorObj.GetComponent<Renderer>().bounds.center;
                frontSignZ = Mathf.Sign(c.z);
                entranceSignX = Mathf.Sign(c.x);
            }

            // ==================================================================
            // GRUPO A: LUCES Y CONOS MODO NOCHE (100% INTACTO)
            // ==================================================================
            GameObject moonObj = new GameObject("Luz_Luna_Principal_7500K");
            moonObj.transform.SetParent(lightsNightRoot.transform);
            moonObj.transform.rotation = Quaternion.Euler(34f, -135f, 0f);
            Light moon = moonObj.AddComponent<Light>();
            moon.type = LightType.Directional;
            moon.useColorTemperature = true;
            moon.colorTemperature = 7500f;
            moon.color = new Color(0.95f, 0.97f, 1.00f);
            moon.intensity = 1.35f;
            moon.shadows = LightShadows.Soft;
            moon.shadowStrength = 0.78f;

            GameObject skyFillObj = new GameObject("Luz_Boveda_Celeste_Relleno_7500K");
            skyFillObj.transform.SetParent(lightsNightRoot.transform);
            skyFillObj.transform.rotation = Quaternion.Euler(45f, 45f, 0f);
            Light skyFill = skyFillObj.AddComponent<Light>();
            skyFill.type = LightType.Directional;
            skyFill.useColorTemperature = true;
            skyFill.colorTemperature = 7500f;
            skyFill.color = new Color(0.92f, 0.95f, 1.00f);
            skyFill.intensity = 0.45f;
            skyFill.shadows = LightShadows.None;

            float stepX = 54.0f / 7.0f;
            float halfX = 27.0f;
            float halfZ = 14.25f;
            float roofBottomY = 7.0f;

            for (int isFrontIdx = 0; isFrontIdx < 2; isFrontIdx++)
            {
                bool isFront = (isFrontIdx == 0);
                float signZ = isFront ? frontSignZ : -frontSignZ;
                string tag = isFront ? "Frontal" : "Trasera";
                float zPos = signZ * (halfZ + 0.68f);

                for (int s = 0; s < 7; s++)
                {
                    float xPos = -halfX + (s + 0.5f) * stepX;
                    bool isEntrance = isFront && (Mathf.Abs(xPos - (entranceSignX * 7.714f)) < 1.0f);

                    GameObject spObj = new GameObject($"Spot_Voladizo_{tag}_Sec{s + 1:02d}");
                    spObj.transform.SetParent(lightsNightRoot.transform);
                    spObj.transform.position = new Vector3(xPos, roofBottomY - 0.08f, zPos);
                    Vector3 targetPos = new Vector3(xPos, 0.0f, signZ * (halfZ + 0.18f));
                    spObj.transform.rotation = Quaternion.LookRotation(targetPos - spObj.transform.position);

                    Light sp = spObj.AddComponent<Light>();
                    sp.type = LightType.Spot;
                    sp.useColorTemperature = true;
                    sp.colorTemperature = 2700f;
                    sp.color = Color.white;
                    sp.intensity = isEntrance ? 125.0f : 90.0f;
                    sp.range = 11.8f;
                    sp.spotAngle = 96.0f;
                    sp.innerSpotAngle = 42.0f;
                    sp.shadows = isFront ? LightShadows.Soft : LightShadows.None;
                    sp.shadowStrength = 0.65f;

                    GameObject floorPool = new GameObject($"PoolLight_Piso_{tag}_Sec{s + 1:02d}");
                    floorPool.transform.SetParent(lightsNightRoot.transform);
                    floorPool.transform.position = new Vector3(xPos, 2.50f, signZ * (halfZ + 0.95f));
                    floorPool.transform.rotation = Quaternion.LookRotation(new Vector3(0f, -2.50f, signZ * 0.35f));
                    Light fp = floorPool.AddComponent<Light>();
                    fp.type = LightType.Spot;
                    fp.useColorTemperature = true;
                    fp.colorTemperature = 2700f;
                    fp.color = Color.white;
                    fp.intensity = isEntrance ? 58.0f : 38.0f;
                    fp.range = 6.8f;
                    fp.spotAngle = 98.0f;
                    fp.innerSpotAngle = 44.0f;
                    fp.shadows = LightShadows.None;

                    CreateVolumetricBeamCross(
                        $"ConoVol_Voladizo_{tag}_Sec{s + 1:02d}",
                        conesNightRoot.transform,
                        new Vector3(xPos, roofBottomY - 2.15f, zPos - signZ * 0.12f),
                        Quaternion.Euler(signZ * 6f, 0f, 0f),
                        new Vector3(2.35f, 4.25f, 2.35f),
                        mats["vol_cone"]
                    );
                }
            }

            Vector3 doorCenter = new Vector3(entranceSignX * 7.714f, 1.45f, frontSignZ * halfZ);
            if (doorObj != null && doorObj.GetComponent<Renderer>() != null)
                doorCenter = doorObj.GetComponent<Renderer>().bounds.center;

            GameObject entFloorSpill = new GameObject("Spot_Derrame_Suelo_Entrada_2700K");
            entFloorSpill.transform.SetParent(lightsNightRoot.transform);
            entFloorSpill.transform.position = doorCenter + new Vector3(0f, 1.45f, frontSignZ * 0.45f);
            Vector3 floorTarget = doorCenter + new Vector3(0f, -1.45f, frontSignZ * 3.8f);
            entFloorSpill.transform.rotation = Quaternion.LookRotation(floorTarget - entFloorSpill.transform.position);
            Light spillSpot = entFloorSpill.AddComponent<Light>();
            spillSpot.type = LightType.Spot;
            spillSpot.useColorTemperature = true;
            spillSpot.colorTemperature = 2700f;
            spillSpot.color = Color.white;
            spillSpot.intensity = 165.0f;
            spillSpot.range = 14.5f;
            spillSpot.spotAngle = 100.0f;
            spillSpot.innerSpotAngle = 48.0f;
            spillSpot.shadows = LightShadows.Soft;
            spillSpot.shadowStrength = 0.65f;

            foreach (float sx in new float[] { -1.0f, 1.0f })
            {
                string lbl = (sx > 0) ? "Der" : "Izq";
                bool isVisibleLateral = (Mathf.Sign(sx) == entranceSignX);
                GameObject latSpot = new GameObject($"Spot_Lampara_Pilar4m_{lbl}_2700K");
                latSpot.transform.SetParent(lightsNightRoot.transform);
                latSpot.transform.position = new Vector3(sx * (halfX + 0.78f), roofBottomY - 0.30f, 0f);
                Vector3 targetPilar = new Vector3(sx * (halfX + 0.45f), 0.0f, 0f);
                latSpot.transform.rotation = Quaternion.LookRotation(targetPilar - latSpot.transform.position);

                Light l = latSpot.AddComponent<Light>();
                l.type = LightType.Spot;
                l.useColorTemperature = true;
                l.colorTemperature = 2700f;
                l.color = Color.white;
                l.intensity = 128.0f;
                l.range = 12.5f;
                l.spotAngle = 100.0f;
                l.innerSpotAngle = 44.0f;
                l.shadows = isVisibleLateral ? LightShadows.Soft : LightShadows.None;
                l.shadowStrength = 0.65f;

                GameObject latPool = new GameObject($"PoolLight_Piso_Pilar4m_{lbl}");
                latPool.transform.SetParent(lightsNightRoot.transform);
                latPool.transform.position = new Vector3(sx * (halfX + 1.05f), 2.40f, 0f);
                latPool.transform.rotation = Quaternion.LookRotation(new Vector3(sx * 0.45f, -2.40f, 0f));
                Light lp = latPool.AddComponent<Light>();
                lp.type = LightType.Spot;
                lp.useColorTemperature = true;
                lp.colorTemperature = 2700f;
                lp.color = Color.white;
                lp.intensity = 45.0f;
                lp.range = 6.8f;
                lp.spotAngle = 98.0f;
                lp.innerSpotAngle = 42.0f;
                lp.shadows = LightShadows.None;

                CreateVolumetricBeamCross(
                    $"ConoVol_Pilar4m_{lbl}",
                    conesNightRoot.transform,
                    new Vector3(sx * (halfX + 0.55f), roofBottomY - 2.6f, 0f),
                    Quaternion.Euler(0f, 0f, -sx * 6f),
                    new Vector3(3.0f, 4.6f, 3.0f),
                    mats["vol_cone"]
                );
            }

            foreach (float sx in new float[] { -1.0f, 1.0f })
            {
                foreach (float sz in new float[] { -1.0f, 1.0f })
                {
                    Vector3 upPos = new Vector3(sx * (halfX + 1.35f), 0.24f, sz * (halfZ + 3.40f));
                    Vector3 upTarget = new Vector3(sx * (halfX + 0.65f), 5.6f, sz * (halfZ + 1.05f));
                    bool isHiddenBackCorner = (Mathf.Sign(sx) != entranceSignX) && (Mathf.Sign(sz) != frontSignZ);

                    GameObject upObj = new GameObject($"Uplight_Contrafuerte_{sx}_{sz}_2700K");
                    upObj.transform.SetParent(lightsNightRoot.transform);
                    upObj.transform.position = upPos;
                    upObj.transform.rotation = Quaternion.LookRotation(upTarget - upPos);

                    Light upL = upObj.AddComponent<Light>();
                    upL.type = LightType.Spot;
                    upL.useColorTemperature = true;
                    upL.colorTemperature = 2700f;
                    upL.color = Color.white;
                    upL.intensity = 105.0f;
                    upL.range = 11.0f;
                    upL.spotAngle = 86.0f;
                    upL.innerSpotAngle = 34.0f;
                    upL.shadows = isHiddenBackCorner ? LightShadows.None : LightShadows.Soft;
                    upL.shadowStrength = 0.65f;

                    GameObject poolObj = new GameObject($"PoolLight_Piso_Contrafuerte_{sx}_{sz}");
                    poolObj.transform.SetParent(lightsNightRoot.transform);
                    poolObj.transform.position = upPos + new Vector3(sx * 0.25f, 1.10f, sz * 0.35f);
                    poolObj.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
                    Light poolL = poolObj.AddComponent<Light>();
                    poolL.type = LightType.Spot;
                    poolL.useColorTemperature = true;
                    poolL.colorTemperature = 2700f;
                    poolL.color = Color.white;
                    poolL.intensity = 52.0f;
                    poolL.range = 5.6f;
                    poolL.spotAngle = 98.0f;
                    poolL.innerSpotAngle = 44.0f;
                    poolL.shadows = LightShadows.None;

                    Vector3 beamCenter = Vector3.Lerp(upPos, upTarget, 0.38f);
                    Quaternion beamRot = Quaternion.LookRotation(upTarget - upPos) * Quaternion.Euler(-90f, 0f, 0f);
                    CreateVolumetricBeamCross(
                        $"ConoVol_Contrafuerte_{sx}_{sz}",
                        conesNightRoot.transform,
                        beamCenter,
                        beamRot,
                        new Vector3(1.80f, 3.7f, 1.80f),
                        mats["vol_cone"]
                    );
                }
            }

            // ==================================================================
            // GRUPO B: LUCES Y CONOS MODO TERROR (PASO 2 DE LA EXTENSIÓN)
            // ==================================================================
            lightsTerrorRoot = new GameObject("Iluminacion_Terror_Docencia2");
            conesTerrorRoot  = new GameObject("Conos_Luz_Volumetricos_Terror");

            // 1. Luna Terror: Directional Light fría (~8000 K), intensidad MUY baja (38% de la nocturna: 0.52),
            //    sombras suaves, ligeramente más baja (21°) para proyectar sombras largas e inquietantes.
            GameObject moonTerrorObj = new GameObject("Luz_Luna_Terror_8000K");
            moonTerrorObj.transform.SetParent(lightsTerrorRoot.transform);
            moonTerrorObj.transform.rotation = Quaternion.Euler(21f, -135f, 0f);
            Light moonTerror = moonTerrorObj.AddComponent<Light>();
            moonTerror.type = LightType.Directional;
            moonTerror.useColorTemperature = true;
            moonTerror.colorTemperature = 8000f;
            moonTerror.color = new Color(0.86f, 0.96f, 1.00f);
            moonTerror.intensity = 0.52f; // ~38.5% de 1.35f (rango solicitado 30-40%)
            moonTerror.shadows = LightShadows.Soft;
            moonTerror.shadowStrength = 0.82f;

            // Luz de relleno cenital/lateral fría muy tenue para que las sombras sean profundas pero NUNCA negro puro y siempre se lea la silueta y los materiales del edificio (incluyendo la vista aérea de la azotea)
            GameObject terrorFillObj = new GameObject("Luz_Relleno_Silueta_Terror_8000K");
            terrorFillObj.transform.SetParent(lightsTerrorRoot.transform);
            terrorFillObj.transform.rotation = Quaternion.Euler(52f, 45f, 0f);
            Light terrorFill = terrorFillObj.AddComponent<Light>();
            terrorFill.type = LightType.Directional;
            terrorFill.useColorTemperature = true;
            terrorFill.colorTemperature = 8000f;
            terrorFill.color = new Color(0.75f, 0.92f, 0.96f);
            terrorFill.intensity = 0.44f;
            terrorFill.shadows = LightShadows.None;

            float zFront = frontSignZ * (halfZ + 0.68f);

            // 2. Lámparas cálidas (2700 K): SOLO 3 encendidas con intensidad reducida y el resto APAGADAS:
            //    - Lámpara 1: Voladizo sobre la Entrada (Sec05) con parpadeo irregular (Docencia2LightFlicker)
            //    - Lámpara 2: Voladizo adyacente (Sec06) como lámpara CASI MUERTA con luz débil y tintineo (Docencia2LightFlicker, isDyingLamp = true)
            //    - Lámpara 3: Contrafuerte frontal-derecho con intensidad reducida
            float xSec05 = -halfX + (4 + 0.5f) * stepX; // Sección 05 (Entrada)
            GameObject terrorEntSpotGo = new GameObject("Spot_Terror_Entrada_Sec05_Flicker");
            terrorEntSpotGo.transform.SetParent(lightsTerrorRoot.transform);
            terrorEntSpotGo.transform.position = new Vector3(xSec05, roofBottomY - 0.08f, zFront);
            terrorEntSpotGo.transform.rotation = Quaternion.LookRotation(new Vector3(xSec05, 0f, frontSignZ * (halfZ + 0.18f)) - terrorEntSpotGo.transform.position);
            Light terrorEntSpot = terrorEntSpotGo.AddComponent<Light>();
            terrorEntSpot.type = LightType.Spot;
            terrorEntSpot.useColorTemperature = true;
            terrorEntSpot.colorTemperature = 2700f;
            terrorEntSpot.color = Color.white;
            terrorEntSpot.intensity = 65.0f; // Reducida respecto a 125 en Noche
            terrorEntSpot.range = 12.5f;
            terrorEntSpot.spotAngle = 96.0f;
            terrorEntSpot.innerSpotAngle = 40.0f;
            terrorEntSpot.shadows = LightShadows.Soft;
            terrorEntSpot.shadowStrength = 0.72f;

            // Derrame de suelo de la entrada (intensidad reducida y sincronizada con el flicker de la entrada)
            GameObject terrorEntSpillGo = new GameObject("Spot_Terror_Derrame_Entrada_2700K");
            terrorEntSpillGo.transform.SetParent(lightsTerrorRoot.transform);
            terrorEntSpillGo.transform.position = doorCenter + new Vector3(0f, 1.45f, frontSignZ * 0.45f);
            terrorEntSpillGo.transform.rotation = Quaternion.LookRotation((doorCenter + new Vector3(0f, -1.45f, frontSignZ * 3.8f)) - terrorEntSpillGo.transform.position);
            Light terrorEntSpill = terrorEntSpillGo.AddComponent<Light>();
            terrorEntSpill.type = LightType.Spot;
            terrorEntSpill.useColorTemperature = true;
            terrorEntSpill.colorTemperature = 2700f;
            terrorEntSpill.color = Color.white;
            terrorEntSpill.intensity = 82.0f; // Reducida respecto a 165 en Noche, suficiente para reflejarse en los parches húmedos del suelo
            terrorEntSpill.range = 15.0f;
            terrorEntSpill.spotAngle = 100.0f;
            terrorEntSpill.innerSpotAngle = 46.0f;
            terrorEntSpill.shadows = LightShadows.Soft;
            terrorEntSpill.shadowStrength = 0.70f;

            GameObject coneTerrorSec05 = CreateVolumetricBeamCross(
                "ConoVol_Terror_Entrada_Sec05",
                conesTerrorRoot.transform,
                new Vector3(xSec05, roofBottomY - 2.15f, zFront - frontSignZ * 0.12f),
                Quaternion.Euler(frontSignZ * 6f, 0f, 0f),
                new Vector3(2.35f, 4.25f, 2.35f),
                mats["vol_cone_terror"]
            );

            Docencia2LightFlicker flickerEntrance = terrorEntSpotGo.AddComponent<Docencia2LightFlicker>();
            flickerEntrance.targetLight = terrorEntSpot;
            flickerEntrance.secondaryPoolLight = terrorEntSpill;
            flickerEntrance.volumetricConeRoot = coneTerrorSec05;
            flickerEntrance.baseIntensity = 65.0f;
            flickerEntrance.baseSecondaryIntensity = 82.0f;
            flickerEntrance.isDyingLamp = false;
            flickerEntrance.noiseSeed = 19.4f;
            flickerEntrance.editorPreviewFactor = 0.88f;

            // Lámpara 2 (Casi muerta con luz débil y tintineo en Sección 06)
            float xSec06 = -halfX + (5 + 0.5f) * stepX;
            GameObject terrorDyingSpotGo = new GameObject("Spot_Terror_CasiMuerta_Sec06_Flicker");
            terrorDyingSpotGo.transform.SetParent(lightsTerrorRoot.transform);
            terrorDyingSpotGo.transform.position = new Vector3(xSec06, roofBottomY - 0.08f, zFront);
            terrorDyingSpotGo.transform.rotation = Quaternion.LookRotation(new Vector3(xSec06, 0f, frontSignZ * (halfZ + 0.18f)) - terrorDyingSpotGo.transform.position);
            Light terrorDyingSpot = terrorDyingSpotGo.AddComponent<Light>();
            terrorDyingSpot.type = LightType.Spot;
            terrorDyingSpot.useColorTemperature = true;
            terrorDyingSpot.colorTemperature = 2500f; // Más cálida/envejecida
            terrorDyingSpot.color = new Color(1.0f, 0.88f, 0.72f);
            terrorDyingSpot.intensity = 28.0f; // Luz débil
            terrorDyingSpot.range = 10.5f;
            terrorDyingSpot.spotAngle = 92.0f;
            terrorDyingSpot.innerSpotAngle = 36.0f;
            terrorDyingSpot.shadows = LightShadows.Soft;
            terrorDyingSpot.shadowStrength = 0.70f;

            GameObject terrorDyingPoolGo = new GameObject("PoolLight_Terror_CasiMuerta_Sec06");
            terrorDyingPoolGo.transform.SetParent(lightsTerrorRoot.transform);
            terrorDyingPoolGo.transform.position = new Vector3(xSec06, 2.50f, frontSignZ * (halfZ + 0.95f));
            terrorDyingPoolGo.transform.rotation = Quaternion.LookRotation(new Vector3(0f, -2.50f, frontSignZ * 0.35f));
            Light terrorDyingPool = terrorDyingPoolGo.AddComponent<Light>();
            terrorDyingPool.type = LightType.Spot;
            terrorDyingPool.useColorTemperature = true;
            terrorDyingPool.colorTemperature = 2500f;
            terrorDyingPool.color = Color.white;
            terrorDyingPool.intensity = 16.0f;
            terrorDyingPool.range = 6.2f;
            terrorDyingPool.spotAngle = 94.0f;
            terrorDyingPool.innerSpotAngle = 38.0f;
            terrorDyingPool.shadows = LightShadows.None;

            GameObject coneTerrorSec06 = CreateVolumetricBeamCross(
                "ConoVol_Terror_CasiMuerta_Sec06",
                conesTerrorRoot.transform,
                new Vector3(xSec06, roofBottomY - 2.15f, zFront - frontSignZ * 0.12f),
                Quaternion.Euler(frontSignZ * 6f, 0f, 0f),
                new Vector3(2.20f, 4.10f, 2.20f),
                mats["vol_cone_terror"]
            );

            Docencia2LightFlicker flickerDying = terrorDyingSpotGo.AddComponent<Docencia2LightFlicker>();
            flickerDying.targetLight = terrorDyingSpot;
            flickerDying.secondaryPoolLight = terrorDyingPool;
            flickerDying.volumetricConeRoot = coneTerrorSec06;
            flickerDying.baseIntensity = 28.0f;
            flickerDying.baseSecondaryIntensity = 16.0f;
            flickerDying.isDyingLamp = true;
            flickerDying.noiseSeed = 43.7f;
            flickerDying.editorPreviewFactor = 0.52f; // En captura estática se lee como lámpara débil agonizante

            // Lámpara 3: Un solo contrafuerte encendido (Contrafuerte Frontal-Derecho, visible en las 3 cámaras) con intensidad reducida
            float bSx = entranceSignX;
            float bSz = frontSignZ;
            Vector3 tUpPos = new Vector3(bSx * (halfX + 1.35f), 0.24f, bSz * (halfZ + 3.40f));
            Vector3 tUpTarget = new Vector3(bSx * (halfX + 0.65f), 5.6f, bSz * (halfZ + 1.05f));

            GameObject terrorButtressGo = new GameObject("Uplight_Terror_Contrafuerte_Principal_2700K");
            terrorButtressGo.transform.SetParent(lightsTerrorRoot.transform);
            terrorButtressGo.transform.position = tUpPos;
            terrorButtressGo.transform.rotation = Quaternion.LookRotation(tUpTarget - tUpPos);
            Light terrorButtressL = terrorButtressGo.AddComponent<Light>();
            terrorButtressL.type = LightType.Spot;
            terrorButtressL.useColorTemperature = true;
            terrorButtressL.colorTemperature = 2700f;
            terrorButtressL.color = Color.white;
            terrorButtressL.intensity = 66.0f; // Reducida respecto a 105 en Noche
            terrorButtressL.range = 10.5f;
            terrorButtressL.spotAngle = 86.0f;
            terrorButtressL.innerSpotAngle = 34.0f;
            terrorButtressL.shadows = LightShadows.Soft;
            terrorButtressL.shadowStrength = 0.72f;

            GameObject terrorButtressPoolGo = new GameObject("PoolLight_Terror_Piso_Contrafuerte");
            terrorButtressPoolGo.transform.SetParent(lightsTerrorRoot.transform);
            terrorButtressPoolGo.transform.position = tUpPos + new Vector3(bSx * 0.25f, 1.10f, bSz * 0.35f);
            terrorButtressPoolGo.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            Light terrorButtressPool = terrorButtressPoolGo.AddComponent<Light>();
            terrorButtressPool.type = LightType.Spot;
            terrorButtressPool.useColorTemperature = true;
            terrorButtressPool.colorTemperature = 2700f;
            terrorButtressPool.color = Color.white;
            terrorButtressPool.intensity = 38.0f;
            terrorButtressPool.range = 6.2f;
            terrorButtressPool.spotAngle = 98.0f;
            terrorButtressPool.innerSpotAngle = 44.0f;
            terrorButtressPool.shadows = LightShadows.None;

            Vector3 tBeamCenter = Vector3.Lerp(tUpPos, tUpTarget, 0.38f);
            Quaternion tBeamRot = Quaternion.LookRotation(tUpTarget - tUpPos) * Quaternion.Euler(-90f, 0f, 0f);
            CreateVolumetricBeamCross(
                "ConoVol_Terror_Contrafuerte_Principal",
                conesTerrorRoot.transform,
                tBeamCenter,
                tBeamRot,
                new Vector3(1.80f, 3.7f, 1.80f),
                mats["vol_cone_terror"]
            );

            // 3. Luz de acento fría verde-teal tenue desde dentro de UNA sola ventana (P2 Frontal Sec06) para sugerir presencia sin mostrar nada
            GameObject tealAccentGo = new GameObject("Luz_Acento_Interior_Ventana_Teal_Sec06");
            tealAccentGo.transform.SetParent(lightsTerrorRoot.transform);
            // Colocada en el hueco de la ventana de planta alta (Y=5.75m, Z=-13.85m) apuntando suavemente hacia afuera y hacia el marco/dintel de la ventana para evitar cualquier hot-spot sobre el panel verde
            tealAccentGo.transform.position = new Vector3(xSec06, 5.75f, frontSignZ * (halfZ - 0.12f));
            tealAccentGo.transform.rotation = Quaternion.LookRotation(new Vector3(0f, 0.12f, frontSignZ * 1.0f));
            Light tealAccent = tealAccentGo.AddComponent<Light>();
            tealAccent.type = LightType.Spot;
            tealAccent.useColorTemperature = false;
            tealAccent.color = new Color(0.18f, 0.78f, 0.70f, 1.0f); // Verde-teal frío misterioso
            tealAccent.intensity = 11.5f; // Tenue, baña solo el dintel, los parteluces de la ventana y el plafón del voladizo
            tealAccent.range = 4.2f;
            tealAccent.spotAngle = 95.0f;
            tealAccent.innerSpotAngle = 45.0f;
            tealAccent.shadows = LightShadows.Soft;
            tealAccent.shadowStrength = 0.65f;
            // En total, el Modo TERROR usa solo 5 luces puntuales con sombras + 1 direccional (muy por debajo del límite del atlas 8192)

            // Configurar valores iniciales de RenderSettings (Noche por defecto antes del switch)
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor     = new Color(0.115f, 0.155f, 0.225f);
            RenderSettings.ambientEquatorColor = new Color(0.115f, 0.145f, 0.195f);
            RenderSettings.ambientGroundColor  = new Color(0.110f, 0.132f, 0.178f);

            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Exponential;
            RenderSettings.fogColor = new Color(0.102f, 0.141f, 0.200f, 1.0f);
            RenderSettings.fogDensity = 0.0085f;

            if (mats.ContainsKey("skybox"))
            {
                RenderSettings.skybox = mats["skybox"];
            }

            SetupGIAndProbes(probes);

            fogNightRoot  = CreateGroundFogBanksAndHorizonHills(mats["mist"], mats["distant_hills"]);
            fogTerrorRoot = CreateTerrorGroundFogBanksAndHills(mats["mist_terror"], mats["distant_hills_terror"], frontSignZ);
            return probes;
        }

        private static void SetupGIAndProbes(List<ReflectionProbe> probes)
        {
            GameObject giRoot = new GameObject("GI_APV_y_ReflectionProbes");

            // 1. Configurar LightingSettings con Progressive GPU Lightmapper
            LightingSettings ls = new LightingSettings();
            ls.name = "Exterior_Docencia2_LightingSettings";
            ls.lightmapper = LightingSettings.Lightmapper.ProgressiveGPU;
            ls.realtimeGI = false;
            ls.bakedGI = true;
            ls.mixedBakeMode = MixedLightingMode.Shadowmask;
            ls.lightmapResolution = 16f;
            ls.lightmapMaxSize = 2048;
            ls.ao = true;
            string lsPath = "Assets/Scenes/Exterior_Docencia2_LightingSettings.lighting";
            if (AssetDatabase.LoadAssetAtPath<LightingSettings>(lsPath) != null)
                AssetDatabase.DeleteAsset(lsPath);
            AssetDatabase.CreateAsset(ls, lsPath);
            Lightmapping.lightingSettings = ls;

            // 2. Adaptive Probe Volume (Unity 6) cubriendo el edificio y el terreno (80 x 24 x 55 m)
            GameObject apvObj = new GameObject("Adaptive_Probe_Volume_Docencia2");
            apvObj.transform.SetParent(giRoot.transform);
            apvObj.transform.position = new Vector3(0f, 6.0f, 0f);
            ProbeVolume pv = apvObj.AddComponent<ProbeVolume>();
            pv.mode = ProbeVolume.Mode.Scene;
            pv.size = new Vector3(90.0f, 22.0f, 64.0f);

            // 3. Reflection Probes frente a cada una de las 4 fachadas (Frontal, Trasera, Lateral Izquierdo, Lateral Derecho)
            Vector3[] rpPositions = new Vector3[]
            {
                new Vector3(0f, 4.2f, -22.0f), // Frente a Fachada Frontal
                new Vector3(0f, 4.2f,  22.0f), // Frente a Fachada Trasera
                new Vector3(-35.0f, 4.2f, 0f), // Frente a Fachada Lateral Izquierda
                new Vector3( 35.0f, 4.2f, 0f), // Frente a Fachada Lateral Derecha
            };
            Vector3[] rpSizes = new Vector3[]
            {
                new Vector3(68f, 18f, 32f),
                new Vector3(68f, 18f, 32f),
                new Vector3(32f, 18f, 44f),
                new Vector3(32f, 18f, 44f),
            };
            string[] rpNames = new string[]
            {
                "ReflectionProbe_Fachada_Frontal",
                "ReflectionProbe_Fachada_Trasera",
                "ReflectionProbe_Fachada_Lateral_Izq",
                "ReflectionProbe_Fachada_Lateral_Der"
            };

            for (int i = 0; i < 4; i++)
            {
                GameObject rpGo = new GameObject(rpNames[i]);
                rpGo.transform.SetParent(giRoot.transform);
                rpGo.transform.position = rpPositions[i];
                ReflectionProbe rp = rpGo.AddComponent<ReflectionProbe>();
                rp.mode = ReflectionProbeMode.Realtime;
                rp.refreshMode = ReflectionProbeRefreshMode.OnAwake;
                rp.timeSlicingMode = ReflectionProbeTimeSlicingMode.NoTimeSlicing;
                rp.resolution = 256;
                rp.hdr = true;
                rp.boxProjection = true;
                rp.size = rpSizes[i];
                rp.intensity = 1.15f;
                probes.Add(rp);
            }
        }

        private static GameObject CreateVolumetricBeamCross(string name, Transform parent, Vector3 pos, Quaternion rot, Vector3 scale, Material mat)
        {
            GameObject root = new GameObject(name);
            root.transform.SetParent(parent);
            root.transform.position = pos;
            root.transform.rotation = rot;
            root.transform.localScale = scale;

            for (int i = 0; i < 2; i++)
            {
                GameObject q = GameObject.CreatePrimitive(PrimitiveType.Quad);
                q.name = $"Plano_{i + 1}";
                UnityEngine.Object.DestroyImmediate(q.GetComponent<Collider>());
                q.transform.SetParent(root.transform, false);
                q.transform.localPosition = Vector3.zero;
                q.transform.localRotation = Quaternion.Euler(0f, i * 90f, 0f);
                q.transform.localScale = Vector3.one;
                Renderer r = q.GetComponent<Renderer>();
                r.sharedMaterial = mat;
                r.shadowCastingMode = ShadowCastingMode.Off;
            }
            return root;
        }

        private static GameObject CreateGroundFogBanksAndHorizonHills(Material mistMat, Material hillsMat)
        {
            GameObject fogRoot = new GameObject("Niebla_Volumetrica_Suelo_y_Horizonte");
            UnityEngine.Random.InitState(505);

            // 1. Anillo perimetral de colinas bajas lejanas en silueta oscura (radio 175-195m) que rompe y suaviza la línea recta entre el piso y el cielo sin tapar jamás la fachada ni los conos de luz
            int hillSegments = 36;
            for (int h = 0; h < hillSegments; h++)
            {
                float ang = (h / (float)hillSegments) * Mathf.PI * 2.0f;
                float dist = 182.0f + Mathf.Sin(ang * 3.0f + 1.2f) * 8.0f;
                float height = 6.5f + (Mathf.Sin(ang * 4.0f + 0.5f) * 0.5f + 0.5f) * 7.5f + Mathf.Cos(ang * 9.0f) * 2.2f;
                Vector3 pos = new Vector3(Mathf.Cos(ang) * dist, height * 0.22f - 1.2f, Mathf.Sin(ang) * dist);

                GameObject hill = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                hill.name = $"Colina_Silueta_Horizonte_{h + 1:02d}";
                UnityEngine.Object.DestroyImmediate(hill.GetComponent<Collider>());
                hill.transform.SetParent(fogRoot.transform);
                hill.transform.position = pos;
                hill.transform.LookAt(new Vector3(0f, pos.y, 0f));
                hill.transform.localScale = new Vector3(54.0f, height, 24.0f);
                Renderer hr = hill.GetComponent<Renderer>();
                hr.sharedMaterial = hillsMat;
                hr.shadowCastingMode = ShadowCastingMode.Off;
                hr.receiveShadows = false;
            }

            // 2. Bancos de niebla baja azul-gris tenue (#1A2433) en dos anillos en el horizonte (radio 95-165m) que difuminan suavemente el borde entre el terreno, las colinas bajas y el cielo
            for (int b = 0; b < 36; b++)
            {
                float angle = (b / 36.0f) * Mathf.PI * 2.0f + UnityEngine.Random.Range(-0.04f, 0.04f);
                float dist = (b % 2 == 0) ? UnityEngine.Random.Range(95.0f, 130.0f) : UnityEngine.Random.Range(135.0f, 168.0f);
                float yHeight = UnityEngine.Random.Range(2.2f, 5.2f);
                Vector3 pos = new Vector3(Mathf.Cos(angle) * dist, yHeight, Mathf.Sin(angle) * dist);

                GameObject card = GameObject.CreatePrimitive(PrimitiveType.Quad);
                card.name = $"Bruma_Horizonte_{b + 1:02d}";
                UnityEngine.Object.DestroyImmediate(card.GetComponent<Collider>());
                card.transform.SetParent(fogRoot.transform);
                card.transform.position = pos;
                card.transform.LookAt(new Vector3(0f, pos.y, 0f));
                card.transform.localScale = new Vector3(UnityEngine.Random.Range(55f, 88f), UnityEngine.Random.Range(9.5f, 15.5f), 1f);
                Renderer r = card.GetComponent<Renderer>();
                r.sharedMaterial = mistMat;
                r.shadowCastingMode = ShadowCastingMode.Off;
            }
            return fogRoot;
        }

        private static GameObject CreateTerrorGroundFogBanksAndHills(Material mistTerrorMat, Material hillsTerrorMat, float frontSignZ)
        {
            GameObject fogRoot = new GameObject("Niebla_Volumetrica_Terror_Suelo_y_Horizonte");
            UnityEngine.Random.InitState(909);

            // 1. Anillo perimetral de colinas bajas en silueta oscura (#070D12)
            int hillSegments = 36;
            for (int h = 0; h < hillSegments; h++)
            {
                float ang = (h / (float)hillSegments) * Mathf.PI * 2.0f;
                float dist = 182.0f + Mathf.Sin(ang * 3.0f + 1.2f) * 8.0f;
                float height = 6.5f + (Mathf.Sin(ang * 4.0f + 0.5f) * 0.5f + 0.5f) * 7.5f + Mathf.Cos(ang * 9.0f) * 2.2f;
                Vector3 pos = new Vector3(Mathf.Cos(ang) * dist, height * 0.22f - 1.2f, Mathf.Sin(ang) * dist);

                GameObject hill = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                hill.name = $"Colina_Terror_Horizonte_{h + 1:02d}";
                UnityEngine.Object.DestroyImmediate(hill.GetComponent<Collider>());
                hill.transform.SetParent(fogRoot.transform);
                hill.transform.position = pos;
                hill.transform.LookAt(new Vector3(0f, pos.y, 0f));
                hill.transform.localScale = new Vector3(54.0f, height, 24.0f);
                Renderer hr = hill.GetComponent<Renderer>();
                hr.sharedMaterial = hillsTerrorMat;
                hr.shadowCastingMode = ShadowCastingMode.Off;
                hr.receiveShadows = false;
            }

            // 2. Bancos de niebla en el horizonte (#0F1A1C)
            for (int b = 0; b < 36; b++)
            {
                float angle = (b / 36.0f) * Mathf.PI * 2.0f + UnityEngine.Random.Range(-0.04f, 0.04f);
                float dist = (b % 2 == 0) ? UnityEngine.Random.Range(85.0f, 125.0f) : UnityEngine.Random.Range(130.0f, 165.0f);
                float yHeight = UnityEngine.Random.Range(2.0f, 4.8f);
                Vector3 pos = new Vector3(Mathf.Cos(angle) * dist, yHeight, Mathf.Sin(angle) * dist);

                GameObject card = GameObject.CreatePrimitive(PrimitiveType.Quad);
                card.name = $"Bruma_Terror_Horizonte_{b + 1:02d}";
                UnityEngine.Object.DestroyImmediate(card.GetComponent<Collider>());
                card.transform.SetParent(fogRoot.transform);
                card.transform.position = pos;
                card.transform.LookAt(new Vector3(0f, pos.y, 0f));
                card.transform.localScale = new Vector3(UnityEngine.Random.Range(55f, 88f), UnityEngine.Random.Range(9.5f, 15.0f), 1f);
                Renderer r = card.GetComponent<Renderer>();
                r.sharedMaterial = mistTerrorMat;
                r.shadowCastingMode = ShadowCastingMode.Off;
            }

            // 3. Capas bajas de niebla a ras de suelo alrededor del edificio (más espesa cerca del suelo sin tapar la fachada)
            Vector3[] lowGroundFogPositions = new Vector3[]
            {
                new Vector3(-22.0f, 0.85f, frontSignZ * 21.0f),
                new Vector3( -8.0f, 0.78f, frontSignZ * 23.5f),
                new Vector3( 16.0f, 0.82f, frontSignZ * 22.0f),
                new Vector3( 34.0f, 0.88f, frontSignZ * 14.0f),
                new Vector3( 36.0f, 0.85f,  0.0f),
                new Vector3( 33.0f, 0.90f, -frontSignZ * 14.0f),
                new Vector3(-32.0f, 0.95f, frontSignZ * 12.0f),
                new Vector3(  0.0f, 0.95f, -frontSignZ * 22.0f),
            };

            for (int i = 0; i < lowGroundFogPositions.Length; i++)
            {
                GameObject lowCard = GameObject.CreatePrimitive(PrimitiveType.Quad);
                lowCard.name = $"Niebla_RasDeSuelo_Terror_{i + 1:02d}";
                UnityEngine.Object.DestroyImmediate(lowCard.GetComponent<Collider>());
                lowCard.transform.SetParent(fogRoot.transform);
                lowCard.transform.position = lowGroundFogPositions[i];
                lowCard.transform.LookAt(new Vector3(lowGroundFogPositions[i].x * 1.5f, lowGroundFogPositions[i].y, lowGroundFogPositions[i].z * 1.5f));
                lowCard.transform.localScale = new Vector3(26.0f, 2.15f, 1.0f);
                Renderer lr = lowCard.GetComponent<Renderer>();
                lr.sharedMaterial = mistTerrorMat;
                lr.shadowCastingMode = ShadowCastingMode.Off;
            }

            return fogRoot;
        }

        // ----------------------------------------------------------------------
        // PASO 4 - POST-PROCESADO: Global_Volume_PostProcesado (NOCHE) + Global_Volume_Terror (TERROR)
        // ----------------------------------------------------------------------
        public static void EnsureVolumeProfilesRestored()
        {
            // Restaurar exactamente los sub-assets de Exterior_Docencia2_VolumeProfile.asset y Exterior_Docencia2_Terror_VolumeProfile.asset
            // que quedaron en {fileID: 0} por no usar AssetDatabase.AddObjectToAsset, conservando el mismo GUID del asset.
            string profileNightPath = "Assets/Scenes/Exterior_Docencia2_VolumeProfile.asset";
            VolumeProfile profileNight = AssetDatabase.LoadAssetAtPath<VolumeProfile>(profileNightPath);
            if (profileNight == null)
            {
                profileNight = ScriptableObject.CreateInstance<VolumeProfile>();
                AssetDatabase.CreateAsset(profileNight, profileNightPath);
            }
            profileNight.components.Clear();
            foreach (var sub in AssetDatabase.LoadAllAssetsAtPath(profileNightPath))
            {
                if (sub != null && sub != profileNight)
                    UnityEngine.Object.DestroyImmediate(sub, true);
            }

            Tonemapping tone = profileNight.Add<Tonemapping>(true);
            tone.name = "Tonemapping";
            tone.mode.Override(TonemappingMode.ACES);
            AssetDatabase.AddObjectToAsset(tone, profileNight);

            ColorAdjustments adj = profileNight.Add<ColorAdjustments>(true);
            adj.name = "ColorAdjustments";
            adj.postExposure.Override(1.68f);
            adj.contrast.Override(9.0f);
            adj.colorFilter.Override(Color.white);
            adj.saturation.Override(0.0f);
            AssetDatabase.AddObjectToAsset(adj, profileNight);

            SplitToning split = profileNight.Add<SplitToning>(true);
            split.name = "SplitToning";
            split.shadows.Override(new Color(0.20f, 0.27f, 0.34f, 1.0f));
            split.highlights.Override(new Color(1.00f, 0.80f, 0.54f, 1.0f));
            split.balance.Override(-5.0f);
            AssetDatabase.AddObjectToAsset(split, profileNight);

            Bloom bloom = profileNight.Add<Bloom>(true);
            bloom.name = "Bloom";
            bloom.threshold.Override(1.15f);
            bloom.intensity.Override(0.30f);
            bloom.scatter.Override(0.65f);
            AssetDatabase.AddObjectToAsset(bloom, profileNight);

            Vignette vig = profileNight.Add<Vignette>(true);
            vig.name = "Vignette";
            vig.intensity.Override(0.20f);
            vig.smoothness.Override(0.42f);
            AssetDatabase.AddObjectToAsset(vig, profileNight);

            FilmGrain grain = profileNight.Add<FilmGrain>(true);
            grain.name = "FilmGrain";
            grain.type.Override(FilmGrainLookup.Thin1);
            grain.intensity.Override(0.04f);
            AssetDatabase.AddObjectToAsset(grain, profileNight);

            EditorUtility.SetDirty(profileNight);

            string profileTerrorPath = "Assets/Scenes/Exterior_Docencia2_Terror_VolumeProfile.asset";
            VolumeProfile profileTerror = AssetDatabase.LoadAssetAtPath<VolumeProfile>(profileTerrorPath);
            if (profileTerror == null)
            {
                profileTerror = ScriptableObject.CreateInstance<VolumeProfile>();
                AssetDatabase.CreateAsset(profileTerror, profileTerrorPath);
            }
            profileTerror.components.Clear();
            foreach (var sub in AssetDatabase.LoadAllAssetsAtPath(profileTerrorPath))
            {
                if (sub != null && sub != profileTerror)
                    UnityEngine.Object.DestroyImmediate(sub, true);
            }

            Tonemapping toneT = profileTerror.Add<Tonemapping>(true);
            toneT.name = "Tonemapping";
            toneT.mode.Override(TonemappingMode.ACES);
            AssetDatabase.AddObjectToAsset(toneT, profileTerror);

            ColorAdjustments adjT = profileTerror.Add<ColorAdjustments>(true);
            adjT.name = "ColorAdjustments";
            adjT.postExposure.Override(1.18f);
            adjT.contrast.Override(14.0f);
            adjT.colorFilter.Override(Color.white);
            adjT.saturation.Override(-28.0f);
            AssetDatabase.AddObjectToAsset(adjT, profileTerror);

            SplitToning splitT = profileTerror.Add<SplitToning>(true);
            splitT.name = "SplitToning";
            splitT.shadows.Override(new Color(0.13f, 0.23f, 0.24f, 1.0f));
            splitT.highlights.Override(new Color(0.92f, 0.72f, 0.46f, 1.0f));
            splitT.balance.Override(-10.0f);
            AssetDatabase.AddObjectToAsset(splitT, profileTerror);

            Bloom bloomT = profileTerror.Add<Bloom>(true);
            bloomT.name = "Bloom";
            bloomT.threshold.Override(1.18f);
            bloomT.intensity.Override(0.22f);
            bloomT.scatter.Override(0.62f);
            AssetDatabase.AddObjectToAsset(bloomT, profileTerror);

            Vignette vigT = profileTerror.Add<Vignette>(true);
            vigT.name = "Vignette";
            vigT.intensity.Override(0.34f);
            vigT.smoothness.Override(0.45f);
            AssetDatabase.AddObjectToAsset(vigT, profileTerror);

            FilmGrain grainT = profileTerror.Add<FilmGrain>(true);
            grainT.name = "FilmGrain";
            grainT.type.Override(FilmGrainLookup.Thin1);
            grainT.intensity.Override(0.12f);
            grainT.response.Override(0.80f);
            AssetDatabase.AddObjectToAsset(grainT, profileTerror);

            EditorUtility.SetDirty(profileTerror);
            AssetDatabase.SaveAssets();

            GameObject volNightGo = GameObject.Find("Global_Volume_PostProcesado");
            if (volNightGo != null && volNightGo.GetComponent<Volume>() != null)
            {
                volNightGo.GetComponent<Volume>().sharedProfile = profileNight;
                EditorUtility.SetDirty(volNightGo.GetComponent<Volume>());
            }
            GameObject volTerrorGo = GameObject.Find("Global_Volume_Terror");
            if (volTerrorGo != null && volTerrorGo.GetComponent<Volume>() != null)
            {
                volTerrorGo.GetComponent<Volume>().sharedProfile = profileTerror;
                EditorUtility.SetDirty(volTerrorGo.GetComponent<Volume>());
            }
        }

        private static void SetupPostProcessing(out GameObject volNightObj, out GameObject volTerrorObj)
        {
            volNightObj = new GameObject("Global_Volume_PostProcesado");
            Volume volNight = volNightObj.AddComponent<Volume>();
            volNight.isGlobal = true;

            volTerrorObj = new GameObject("Global_Volume_Terror");
            Volume volTerror = volTerrorObj.AddComponent<Volume>();
            volTerror.isGlobal = true;

            EnsureVolumeProfilesRestored();
            volNight.sharedProfile = AssetDatabase.LoadAssetAtPath<VolumeProfile>("Assets/Scenes/Exterior_Docencia2_VolumeProfile.asset");
            volTerror.sharedProfile = AssetDatabase.LoadAssetAtPath<VolumeProfile>("Assets/Scenes/Exterior_Docencia2_Terror_VolumeProfile.asset");
        }

        // ----------------------------------------------------------------------
        // LAS 3 CÁMARAS EN UNITY (POSICIONES Y ÁNGULOS CONSERVADOS INTACTOS)
        // ----------------------------------------------------------------------
        private static void SetupReferenceCameras(out Camera cam1, out Camera cam2, out Camera cam3)
        {
            GameObject camsRoot = new GameObject("Camaras_Referencia_Docencia2");

            GameObject doorObj = GameObject.Find("Vidrio_Entrada_Total");
            float fz = -1.0f;
            float fx = 1.0f;
            if (doorObj != null && doorObj.GetComponent<Renderer>() != null)
            {
                Vector3 c = doorObj.GetComponent<Renderer>().bounds.center;
                fz = Mathf.Sign(c.z);
                fx = Mathf.Sign(c.x);
            }

            // (1) Cámara 1: Frontal Baja
            cam1 = CreateCinCamera(
                "Camara_01_Frontal_Baja",
                camsRoot.transform,
                new Vector3(fx * 37.5f, 1.95f, fz * 36.5f),
                new Vector3(fx * 1.5f, 3.80f, fz * 10.0f),
                38.0f
            );

            // (2) Cámara 2: Aérea Azotea
            cam2 = CreateCinCamera(
                "Camara_02_Aerea_Azotea",
                camsRoot.transform,
                new Vector3(fx * 44.5f, 25.5f, fz * 41.5f),
                new Vector3(fx * 0.5f, 3.2f, fz * 0.5f),
                35.0f
            );

            // (3) Cámara 3: Esquina Contrafuerte
            cam3 = CreateCinCamera(
                "Camara_03_Esquina_Contrafuerte",
                camsRoot.transform,
                new Vector3(fx * 43.5f, 2.95f, fz * 28.0f),
                new Vector3(fx * 8.5f, 3.90f, fz * 3.5f),
                36.0f
            );

            cam1.tag = "MainCamera";
        }

        private static Camera CreateCinCamera(string name, Transform parent, Vector3 pos, Vector3 lookTarget, float fov)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent);
            go.transform.position = pos;
            go.transform.LookAt(lookTarget, Vector3.up);

            Camera cam = go.AddComponent<Camera>();
            cam.fieldOfView = fov;
            cam.nearClipPlane = 0.2f;
            cam.farClipPlane = 450.0f;
            cam.clearFlags = CameraClearFlags.Skybox;
            cam.backgroundColor = new Color(0.102f, 0.141f, 0.200f, 1.0f); // #1A2433

            var camData = go.AddComponent<UniversalAdditionalCameraData>();
            camData.renderPostProcessing = true;
            camData.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            camData.antialiasingQuality = AntialiasingQuality.High;

            return cam;
        }

        private static void RenderSingleCameraClean(Camera targetCam, Camera[] allCams, string filePath, int width, int height)
        {
            foreach (Camera c in allCams)
            {
                if (c != null) c.enabled = (c == targetCam);
            }

            RenderTexture rt = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
            rt.antiAliasing = 4;
            RenderTexture prevRT = RenderTexture.active;
            RenderTexture prevCamRT = targetCam.targetTexture;

            targetCam.targetTexture = rt;
            targetCam.Render();
            targetCam.Render();

            RenderTexture.active = rt;
            Texture2D tex = new Texture2D(width, height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            tex.Apply();

            targetCam.targetTexture = prevCamRT;
            RenderTexture.active = prevRT;
            UnityEngine.Object.DestroyImmediate(rt);

            File.WriteAllBytes(filePath, tex.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(tex);
            Debug.Log($"[Docencia2] Captura Game View 1920x1080 guardada: {filePath} (pos={targetCam.transform.position})");
        }
    }
}
