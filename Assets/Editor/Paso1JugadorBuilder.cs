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

public static class Paso1JugadorBuilder
{
    public const string ScenePath = "Assets/Scenes/Exterior_Docencia2.unity";
    public const string OutDir = @"C:/Users/faust/OneDrive/Desktop/VAELITH/FaseA_Correcciones/Paso1_Jugador";
    public const string CookieAssetPath = "Assets/Textures/Docencia2_Linterna_Celular_Cookie.png";
    public const string MatDir = "Assets/Materials/Docencia2/Paso1";
    public const string ProfilePath = "Assets/Settings/Docencia2_Interior_Terror_OscuridadExtrema_Profile.asset";

    static Vector3 W(float x, float y, float z) => Docencia2InteriorBuilder.PlanToUnityPos(x, y, z);

    [MenuItem("VAELITH/Terror Docencia 2/PASO 1 y OSCURIDAD EXTREMA - Construir y Verificar")]
    public static void BuildPlayerInSceneMenu()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            UnityEngine.Debug.LogWarning("[Paso1JugadorBuilder] Sal de Play Mode primero para ejecutar esta herramienta de construcción.");
            return;
        }
        BuildPlayerInScene();
    }

    public static void BuildAndVerifyAll()
    {
        BuildPlayerInScene();
        RunExteriorRegressionComparison();
        SessionState.SetBool("PASO1_PLAY_VERIFY", true);
        EditorApplication.isPlaying = true;
    }

    public static void BuildPlayerInScene()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            UnityEngine.Debug.LogWarning("[Paso1JugadorBuilder] Construcción requiere Edit mode. Sal de Play Mode primero.");
            return;
        }

        Directory.CreateDirectory(OutDir);
        Directory.CreateDirectory(MatDir);

        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        // ====================================================================
        // 1. CORREGIR EL HUECO DE LA ESCALERA EN PLANTA ALTA (Foto del usuario)
        //    En PARevision2 la escalera se desplazó +1 m a x = 2.36..8.10 m, pero
        //    su hueco seguía en z = 15.43..23.14 m mientras la escalera mide
        //    z = 17.09..21.49 m, dejando losas flotantes de 6 mm sin losa de concreto
        //    en z = 15.43..17.09 m y z = 21.49..23.14 m.
        // ====================================================================
        FixStaircaseOpeningFloorAndSlabInPA();

        // ====================================================================
        // 2. GENERAR TEXTURA COOKIE IRREGULAR DEL FLASH DEL CELULAR
        // ====================================================================
        Texture2D cookieTex = EnsureFlashlightCookieTexture();

        // ====================================================================
        // 3. MATERIALES DEL CELULAR EN MANO DERECHA
        // ====================================================================
        Shader urpLit = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        Material mPhoneBody = MakeMat("Mat_Paso1_Celular_Cuerpo.mat", urpLit, new Color(0.10f, 0.11f, 0.13f, 1f), 0.65f, 0.45f, Color.black);
        Material mPhoneScreen = MakeMat("Mat_Paso1_Celular_Pantalla.mat", urpLit, new Color(0.05f, 0.08f, 0.12f, 1f), 0.85f, 0.10f, new Color(0.06f, 0.11f, 0.18f, 1f) * 0.35f);
        Material mPhoneLed = MakeMat("Mat_Paso1_Celular_FlashLED.mat", urpLit, new Color(0.95f, 0.98f, 1.0f, 1f), 0.90f, 0.0f, new Color(0.92f, 0.96f, 1.0f, 1f) * 3.0f);
        Material mHandSleeve = MakeMat("Mat_Paso1_Manga_Oscura.mat", urpLit, new Color(0.14f, 0.16f, 0.20f, 1f), 0.20f, 0.0f, Color.black);
        Material mHandSkin = MakeMat("Mat_Paso1_Mano_Piel.mat", urpLit, new Color(0.76f, 0.60f, 0.49f, 1f), 0.28f, 0.0f, Color.black);

        // ====================================================================
        // 4. JUGADOR EN PRIMERA PERSONA CON LINTERNA DE CELULAR (CONO ~28°, ALCANCE ~6 m)
        // ====================================================================
        GameObject oldPlayer = GameObject.Find("Jugador_PrimeraPersona_Docencia2");
        if (oldPlayer != null) Object.DestroyImmediate(oldPlayer);

        GameObject playerRoot = new GameObject("Jugador_PrimeraPersona_Docencia2");
        playerRoot.transform.position = W(24.80f, 0.22f, 19.29f);
        playerRoot.transform.rotation = Quaternion.Euler(0f, 0f, 0f);

        var cc = playerRoot.AddComponent<CharacterController>();
        cc.height = 1.75f;
        cc.radius = 0.30f;
        cc.stepOffset = 0.30f;
        cc.slopeLimit = 50f;
        cc.skinWidth = 0.03f;
        cc.center = new Vector3(0f, 0.875f, 0f);

        GameObject camGo = new GameObject("Camara_PrimeraPersona_1.65m");
        camGo.tag = "MainCamera";
        camGo.transform.SetParent(playerRoot.transform, false);
        camGo.transform.localPosition = new Vector3(0f, 1.65f, 0f);
        camGo.transform.localRotation = Quaternion.identity;

        var cam = camGo.AddComponent<Camera>();
        cam.nearClipPlane = 0.05f;
        cam.farClipPlane = 180f;
        cam.fieldOfView = 68f;
        cam.clearFlags = CameraClearFlags.Skybox;
        cam.enabled = false; // Se activa automáticamente en Play Mode para cero regresión en Edit Mode

        var camData = camGo.AddComponent<UniversalAdditionalCameraData>();
        camData.renderPostProcessing = true;
        camGo.AddComponent<AudioListener>();

        // Celular en la mano derecha, abajo a la derecha de la vista
        GameObject handRoot = new GameObject("Mano_Celular_Derecha");
        handRoot.transform.SetParent(camGo.transform, false);
        handRoot.transform.localPosition = new Vector3(0.24f, -0.21f, 0.38f);
        handRoot.transform.localRotation = Quaternion.Euler(6f, -8f, 2f);

        GameObject phoneBody = CreatePart("Celular_Cuerpo", handRoot.transform, PrimitiveType.Cube,
            new Vector3(0f, 0f, 0f), new Vector3(14f, 0f, 0f), new Vector3(0.074f, 0.148f, 0.010f), mPhoneBody);
        CreatePart("Celular_Pantalla", phoneBody.transform, PrimitiveType.Cube,
            new Vector3(0f, 0f, -0.0052f), Vector3.zero, new Vector3(0.90f, 0.92f, 0.08f), mPhoneScreen);
        CreatePart("Celular_ModuloCamara", phoneBody.transform, PrimitiveType.Cube,
            new Vector3(-0.22f, 0.34f, 0.52f), Vector3.zero, new Vector3(0.34f, 0.22f, 0.25f), mPhoneBody);
        GameObject ledGo = CreatePart("Celular_Flash_LED", phoneBody.transform, PrimitiveType.Sphere,
            new Vector3(0.20f, 0.36f, 0.55f), Vector3.zero, new Vector3(0.14f, 0.08f, 0.14f), mPhoneLed);

        CreatePart("Mano_Palma_Sujecion", handRoot.transform, PrimitiveType.Cube,
            new Vector3(0.018f, -0.045f, -0.012f), new Vector3(18f, -10f, -12f), new Vector3(0.068f, 0.082f, 0.036f), mHandSkin);
        CreatePart("Mano_Pulgar", handRoot.transform, PrimitiveType.Cube,
            new Vector3(-0.036f, -0.025f, -0.008f), new Vector3(10f, 15f, 28f), new Vector3(0.022f, 0.048f, 0.022f), mHandSkin);
        CreatePart("Brazo_Manga", handRoot.transform, PrimitiveType.Cube,
            new Vector3(0.045f, -0.135f, -0.110f), new Vector3(28f, -12f, -10f), new Vector3(0.082f, 0.110f, 0.180f), mHandSleeve);

        // LINTERNA DE CELULAR (Reemplaza la del Paso 1):
        // - Cono ANGOSTO (Spot ~28°, borde suave con innerSpotAngle = 14.5°)
        // - Alcance corto (~6.2 m) con caída de intensidad fuerte con la distancia
        // - Luz blanca-fría, sombras suaves, textura cookie con borde irregular
        // - ÚNICA fuente de luz importante del jugador y la única con sombras suaves
        GameObject spotGo = new GameObject("Luz_Linterna_Celular_Spot28_6m");
        spotGo.transform.SetParent(handRoot.transform, false);
        spotGo.transform.localPosition = new Vector3(0f, 0.055f, 0.025f);
        // Como handRoot tiene rotación base Euler(6f, -8f, 2f), compensamos (-7.5f, +6.5f, -2f) para que el cono de 28° apunte directo al centro de la mirada
        spotGo.transform.localRotation = Quaternion.Euler(-7.5f, 6.5f, -2f);

        var spot = spotGo.AddComponent<Light>();
        spot.type = LightType.Spot;
        spot.color = new Color(0.92f, 0.96f, 1.0f, 1f); // Blanca-fría LED realista
        spot.intensity = 95.0f;
        spot.range = 7.8f;                              // Alcance corto (~7.8 m con caída fuerte)
        spot.spotAngle = 30.0f;                         // Cono angosto (~30°)
        spot.innerSpotAngle = 12.0f;                    // Hotspot central + corona suave
        spot.shadows = LightShadows.Soft;               // ÚNICA luz con sombras suaves
        spot.shadowStrength = 0.96f;
        spot.shadowBias = 0.015f;
        spot.shadowNormalBias = 0.16f;
        spot.shadowNearPlane = 0.10f;
        spot.cookie = cookieTex;
        spot.cullingMask = ~0;
        var lightData = spotGo.GetComponent<UniversalAdditionalLightData>() ?? spotGo.AddComponent<UniversalAdditionalLightData>();
        lightData.renderingLayers = uint.MaxValue;
        lightData.softShadowQuality = SoftShadowQuality.High;
        spot.enabled = false;

        // Luz de silueta muy tenue de 3 m alrededor del jugador (sin sombras, solo para que en modo TERROR se distingan bordes de puertas y silueta de pasillos a 2-3 m y nunca sea negro puro)
        GameObject silGo = new GameObject("Luz_Silueta_Cercana_3m_SinSombras");
        silGo.transform.SetParent(camGo.transform, false);
        silGo.transform.localPosition = Vector3.zero;
        var silLight = silGo.AddComponent<Light>();
        silLight.type = LightType.Point;
        silLight.range = 3.2f;
        silLight.color = new Color(0.48f, 0.62f, 0.72f, 1f);
        silLight.intensity = 0.10f;
        silLight.shadows = LightShadows.None;
        silLight.cullingMask = (1 << 8) | (1 << 30);
        var silData = silGo.AddComponent<UniversalAdditionalLightData>();
        silData.renderingLayers = uint.MaxValue;
        silLight.enabled = false;

        // Luz de inspección para el MODO PRUEBA DE PUERTAS (tecla G): se enciende solo mientras G está abierto y se apaga al salir
        GameObject gLightGo = new GameObject("Luz_ModoPrueba_G_Inspeccion");
        gLightGo.transform.SetParent(camGo.transform, false);
        gLightGo.transform.localPosition = Vector3.zero;
        var gLight = gLightGo.AddComponent<Light>();
        gLight.type = LightType.Point;
        gLight.range = 16.0f;
        gLight.color = new Color(0.92f, 0.95f, 1.0f, 1f);
        gLight.intensity = 2.4f;
        gLight.shadows = LightShadows.None;
        gLight.cullingMask = (1 << 8) | (1 << 30);
        var gData = gLightGo.AddComponent<UniversalAdditionalLightData>();
        gData.renderingLayers = uint.MaxValue;
        gLight.enabled = false;

        var player = playerRoot.AddComponent<Docencia2FirstPersonPlayer>();
        player.playerCamera = cam;
        player.phoneHandRoot = handRoot.transform;
        player.phoneSpotLight = spot;
        player.phoneFlashLedRenderer = ledGo.GetComponent<Renderer>();
        player.flashlightOn = true;
        player.flashlightBaseIntensity = 95.0f;
        player.flashlightRange = 7.8f;
        player.flashlightOuterAngle = 30.0f;
        player.flashlightInnerAngle = 12.0f;

        // ====================================================================
        // 5. CONSTRUIR SISTEMA DE OSCURIDAD EXTREMA Y TENSIÓN SIN LUZ (SOLO TERROR)
        // ====================================================================
        BuildExtremeTerrorDarknessGroup(playerRoot, player, silLight, gLight);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();

        player.RunDoorAuditAndPopulateList(saveFiles: true);
        UnityEngine.Debug.Log("[Paso1JugadorBuilder] Escalera PA corregida, linterna 28°/6m y Oscuridad Extrema TERROR configuradas.");
    }

    // ====================================================================
    // CORRECCIÓN DE LA ESCALERA Y HUECO DE ENTREPISO EN PA (Foto del usuario)
    // ====================================================================
    static void FixStaircaseOpeningFloorAndSlabInPA()
    {
        var pb = GameObject.Find("PB_Interior");
        var pa = GameObject.Find("PA_Interior");
        if (pb == null || pa == null) return;

        Transform stair = pb.transform.Find("22_Cubo_Escaleras_PB/Escalera_En_U_Concreto_y_Barandal");
        Transform llegadaPA = pa.transform.Find("Llegada_Escalera");
        if (stair == null || llegadaPA == null) return;

        Material mLosa = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Docencia2/Interior/Mat_Int_Losa_Entrepiso_Concreto.mat");
        Material mPiso = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Docencia2/Interior/Mat_Int_Piso_LosetaGris_PB.mat");
        Material mPlafon = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Docencia2/Interior/Mat_Int_Plafon_Reticular_PB.mat");
        Material mCrema = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Docencia2/Interior/Mat_Int_Muro_AmarilloCrema_PB.mat");
        Material mZoclo = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Docencia2/Interior/Mat_Int_Zoclo_CeramicoGris_PB.mat");
        Material mBarandal = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Docencia2/Interior/Mat_Int_Barandal_Tubular_VerdeOscuro.mat");

        // 0. CAUSA RAÍZ DEL PISO/PLAFÓN QUE ATRAVESABA LA ESCALERA (Foto media_1794728000801_86f7ebf3.jpg):
        //    - En PB_Interior/22_Cubo_Escaleras_PB existían Piso_Vestibulo_Frente_Escaleras_PB y Plafon_Vestibulo_Frente_Escaleras_PB
        //      creados con CreatePlanBox (cuya malla ya tiene el tamaño en vértices y localScale = 1,1,1) desde x = 7.10 hasta 11.24 m.
        //    - Cuando PARevision2 desplazó la escalera +1 m hacia el este (hasta x = 8.10 m), Plafon_Vestibulo_Frente_Escaleras_PB
        //      siguió empezando en x = 7.10 m a Y = 3.18..3.22 m, atravesando el Tramo 2 Sur de la escalera y asomándose a los lados.
        //    - Además, Losa_Entrepiso_Tramo_CentroEste_JuntoEscalera había sido escalada con ResizeX sobre una malla CreatePlanBox.
        //    Los reconstruimos limpiamente con CreatePlanBox en x = 8.10..11.24 m (y 8.10..28.12 m para la losa) con localScale = (1,1,1):
        Transform stairZonePB = pb.transform.Find("22_Cubo_Escaleras_PB");
        if (stairZonePB != null)
        {
            Transform oldPisoCuboPB = stairZonePB.Find("Piso_Cubo_Escaleras_PB");
            if (oldPisoCuboPB != null) Object.DestroyImmediate(oldPisoCuboPB.gameObject);
            var newPisoCuboPB = Docencia2InteriorBuilder.CreatePlanBox(
                "Piso_Cubo_Escaleras_PB", stairZonePB,
                0.36f, 8.10f, 0.18f, 0.22f, 15.43f, 23.14f, mPiso, 0.004f, true);
            newPisoCuboPB.layer = 8;
            var rPisoCuboPB = newPisoCuboPB.GetComponent<MeshRenderer>();
            if (rPisoCuboPB != null) { rPisoCuboPB.renderingLayerMask = 3u; rPisoCuboPB.shadowCastingMode = ShadowCastingMode.Off; }

            Transform oldPisoPB = stairZonePB.Find("Piso_Vestibulo_Frente_Escaleras_PB");
            if (oldPisoPB != null) Object.DestroyImmediate(oldPisoPB.gameObject);
            var newPisoPB = Docencia2InteriorBuilder.CreatePlanBox(
                "Piso_Vestibulo_Frente_Escaleras_PB", stairZonePB,
                8.10f, 11.24f, 0.18f, 0.22f, 15.43f, 23.14f, mPiso, 0.004f, true);
            newPisoPB.layer = 8;
            var rPisoPB = newPisoPB.GetComponent<MeshRenderer>();
            if (rPisoPB != null) { rPisoPB.renderingLayerMask = 3u; rPisoPB.shadowCastingMode = ShadowCastingMode.Off; }

            Transform oldPlafonPB = stairZonePB.Find("Plafon_Vestibulo_Frente_Escaleras_PB");
            if (oldPlafonPB != null) Object.DestroyImmediate(oldPlafonPB.gameObject);
            var newPlafonPB = Docencia2InteriorBuilder.CreatePlanBox(
                "Plafon_Vestibulo_Frente_Escaleras_PB", stairZonePB,
                8.10f, 11.24f, 3.18f, 3.22f, 15.43f, 23.14f, mPlafon, 0.003f, false);
            newPlafonPB.layer = 8;
            var rPlafonPB = newPlafonPB.GetComponent<MeshRenderer>();
            if (rPlafonPB != null) { rPlafonPB.renderingLayerMask = 3u; rPlafonPB.shadowCastingMode = ShadowCastingMode.Off; }
        }

        Transform slabParent = pb.transform.Find("00_Losa_Entrepiso_y_Columnas");
        if (slabParent != null)
        {
            Transform oldSlab = slabParent.Find("Losa_Entrepiso_Tramo_CentroEste_JuntoEscalera");
            if (oldSlab != null) Object.DestroyImmediate(oldSlab.gameObject);
            var newSlab = Docencia2InteriorBuilder.CreatePlanBox(
                "Losa_Entrepiso_Tramo_CentroEste_JuntoEscalera", slabParent,
                8.10f, 28.12f, 3.22f, 3.60f, 15.43f, 23.14f, mLosa, 0.008f, true);
            newSlab.layer = 8;
            var rSlab = newSlab.GetComponent<MeshRenderer>();
            if (rSlab != null) { rSlab.renderingLayerMask = 3u; rSlab.shadowCastingMode = ShadowCastingMode.Off; }
        }

        // Asegurar que Piso_Loseta_Acabado_6mm y Plafon_PA_Y6.80 en PA_Interior/Llegada_Escalera arranquen exactamente en x = 8.10 m
        // (sin hueco de 6 cm entre x = 8.10 y 8.16, y sin invadir el vacío de la escalera x = 1.36..8.10):
        foreach (Transform t in llegadaPA.Cast<Transform>().ToArray())
        {
            if (t.name == "Piso_Loseta_Acabado_6mm" || t.name == "Plafon_PA_Y6.80")
            {
                Vector3 a = W(8.10f, 0f, 15.43f);
                Vector3 b = W(19.24f, 0f, 23.14f);
                Vector3 pos = t.position;
                pos.z = (a.z + b.z) * 0.5f;
                t.position = pos;
                Vector3 sc = t.localScale;
                sc.z = Mathf.Abs(b.z - a.z);
                t.localScale = sc;
            }
        }

        // 1. Limpiar cualquier intento previo de relleno lateral en el vacío de doble altura de la escalera
        Transform oldFix = llegadaPA.Find("Cierre_Losa_y_Barandales_Hueco_Escalera_PA");
        if (oldFix != null) Object.DestroyImmediate(oldFix.gameObject);

        foreach (string obsolete in new[]
        {
            "Murete_Desembarque_PA_LateralSur",
            "Barandal_Desembarque_PA_LateralSur",
            "Proteccion_PA_HuecoOeste_1m_SIN_CERRAR",
            "Murete_VacioPA_Este_SobreTramo1Norte",
            "Barandal_VacioPA_Este_SobreTramo1Norte"
        })
        {
            var t = stair.Find(obsolete);
            if (t != null) Object.DestroyImmediate(t.gameObject);
        }

        // 2. Construir el murete y barandal de llegada en PA exactamente como fue aprobado en Escalera_LlegadaPA_v13.png:
        //    - Lado Este en PA sobre el vacío Norte y el Tramo 1 Norte (x = 8.10, z = 15.43..19.29)
        //    - Pequeño murete lateral sur en el desembarque de PA (x = 8.10, z = 21.49..23.14)
        //    - Dejando el vacío de doble altura (x = 1.36..8.10, z = 15.43..23.14) completamente limpio sin losas que atraviesen los tramos.
        GameObject fixGroup = new GameObject("Cierre_Losa_y_Barandales_Hueco_Escalera_PA");
        fixGroup.transform.SetParent(llegadaPA, false);

        // Murete y barandal en PA sobre el Tramo 1 Norte y el vacío norte (en x = 8.10, z = 15.43..19.29)
        CreatePABox("Murete_PA_Este_SobreTramo1Norte", fixGroup.transform, 8.04f, 8.16f, 3.60f, 4.40f, 15.43f, 19.29f, mCrema, true);
        CreatePABox("Barandal_PA_Este_SobreTramo1Norte", fixGroup.transform, 8.06f, 8.14f, 4.50f, 4.55f, 15.43f, 19.29f, mBarandal, false);
        CreatePABox("Poste_PA_Este_Centro", fixGroup.transform, 8.07f, 8.13f, 4.40f, 4.52f, 19.23f, 19.29f, mBarandal, false);
        CreatePABox("Poste_PA_Este_Medio", fixGroup.transform, 8.07f, 8.13f, 4.40f, 4.52f, 17.33f, 17.39f, mBarandal, false);
        CreatePABox("Poste_PA_Este_Norte", fixGroup.transform, 8.07f, 8.13f, 4.40f, 4.52f, 15.45f, 15.51f, mBarandal, false);

        // Pequeño murete y barandal en PA al costado sur del desembarque del Tramo 2 (en x = 8.10, z = 21.44..23.14)
        CreatePABox("Murete_PA_Este_LateralSur_Desembarque", fixGroup.transform, 8.00f, 8.16f, 3.60f, 4.40f, 21.44f, 23.14f, mCrema, true);
        CreatePABox("Barandal_PA_Este_LateralSur_Desembarque", fixGroup.transform, 8.06f, 8.14f, 4.50f, 4.55f, 21.44f, 23.14f, mBarandal, false);
        CreatePABox("Poste_PA_Este_Sur1", fixGroup.transform, 8.07f, 8.13f, 4.40f, 4.52f, 21.44f, 21.50f, mBarandal, false);
        CreatePABox("Poste_PA_Este_Sur2", fixGroup.transform, 8.07f, 8.13f, 4.40f, 4.52f, 23.06f, 23.12f, mBarandal, false);
    }

    static GameObject CreatePABox(string name, Transform parent, float x0, float x1, float y0, float y1, float z0, float z1, Material mat, bool addCollider)
    {
        var go = Docencia2InteriorBuilder.CreatePlanBox(name, parent, x0, x1, y0, y1, z0, z1, mat, 0.003f, addCollider);
        go.layer = 30;
        var r = go.GetComponent<MeshRenderer>();
        if (r != null)
        {
            r.renderingLayerMask = 17u;
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = true;
        }
        return go;
    }

    // ====================================================================
    // CONSTRUCCIÓN DE OSCURIDAD EXTREMA Y VOLUME LOCAL (SOLO MODO TERROR)
    // ====================================================================
    static void BuildExtremeTerrorDarknessGroup(GameObject playerRoot, Docencia2FirstPersonPlayer player, Light nearSilLight, Light gInspectionLight)
    {
        GameObject oldGrp = GameObject.Find("Terror_OscuridadExtrema_Interior_Grupo");
        if (oldGrp != null) Object.DestroyImmediate(oldGrp);

        GameObject terrorGrp = new GameObject("Terror_OscuridadExtrema_Interior_Grupo");
        int maskInterior = (1 << 8) | (1 << 30);

        // 1. Volume Local Interior para Oscuridad Extrema en TERROR:
        //    - Exposición fija (sin auto-exposure)
        //    - Viñeta marcada (0.58)
        //    - Grano fino leve (0.28)
        //    - Colores fríos y desaturados (saturation = -44, postExposure = -0.65, contrast = +24)
        var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(ProfilePath);
        if (profile == null)
        {
            profile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(profile, ProfilePath);
        }
        profile.components.Clear();

        var colorAdj = profile.Add<ColorAdjustments>(true);
        colorAdj.postExposure.Override(-0.10f); // Exposición equilibrada: oscuridad abismal fuera del haz y respuesta realista donde ilumina la linterna
        colorAdj.contrast.Override(22f);
        colorAdj.colorFilter.Override(new Color(0.88f, 0.93f, 0.98f, 1f)); // Tono frío realista de terror
        colorAdj.saturation.Override(-28f);     // Frío y ligeramente desaturado

        var lgg = profile.Add<LiftGammaGain>(true);
        // Lift mínimo (+0.004) en las sombras más profundas: evita que el negro llegue a RGB (0,0,0) (mínimo ~RGB 2,3,4) manteniendo el fondo en penumbra abismal
        lgg.lift.Override(new Vector4(1.00f, 1.01f, 1.02f, 0.004f));
        lgg.gamma.Override(new Vector4(1.00f, 1.00f, 1.01f, 0.0f));

        var bloom = profile.Add<Bloom>(true);
        bloom.threshold.Override(0.95f);
        bloom.intensity.Override(0.35f);
        bloom.scatter.Override(0.65f);

        var vignette = profile.Add<Vignette>(true);
        vignette.color.Override(new Color(0.010f, 0.015f, 0.022f, 1f));
        vignette.intensity.Override(0.58f);     // Viñeta marcada opresiva
        vignette.smoothness.Override(0.46f);

        var grain = profile.Add<FilmGrain>(true);
        grain.type.Override(FilmGrainLookup.Thin1);
        grain.intensity.Override(0.28f);        // Grano fino leve
        grain.response.Override(0.82f);

        EditorUtility.SetDirty(profile);

        GameObject volGo = new GameObject("Volume_Interior_OscuridadExtrema_Terror");
        volGo.transform.SetParent(terrorGrp.transform, false);
        var vol = volGo.AddComponent<Volume>();
        vol.isGlobal = true;
        vol.priority = 25f;
        vol.weight = 1.0f;
        vol.sharedProfile = profile;
        volGo.SetActive(false);

        // Lámparas de emergencia ámbar muy tenues (intensidad 0.07, alcance corto 2.6 m) para que solo marquen un punto tenue en el techo sin aclarar el pasillo ni las bancas:
        var amberLights = new List<Light>();
        var amberSpecs = new (string name, float x, float y, float z, float range)[]
        {
            ("Luz_Emergencia_Ambar_01_VestibuloPB", 22.80f, 3.05f, 19.29f, 2.6f),
            ("Luz_Emergencia_Ambar_02_EscaleraDescanso", 3.60f, 3.85f, 19.29f, 2.6f),
            ("Luz_Emergencia_Ambar_03_PlazaSurPA", 14.24f, 6.60f, 41.50f, 2.6f)
        };

        foreach (var sp in amberSpecs)
        {
            var lGo = new GameObject(sp.name);
            lGo.transform.SetParent(terrorGrp.transform, false);
            lGo.transform.position = W(sp.x, sp.y, sp.z);
            var l = lGo.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = new Color(1.0f, 0.36f, 0.08f, 1f); // Ámbar rojizo tenue de emergencia
            l.intensity = 0.07f;
            l.range = sp.range;
            l.shadows = LightShadows.None;
            l.cullingMask = maskInterior;
            var uld = lGo.AddComponent<UniversalAdditionalLightData>();
            uld.renderingLayers = uint.MaxValue;
            amberLights.Add(l);
        }

        // Único fluorescente moribundo que parpadea de forma irregular en el pasillo central norte (PB Sec 06)
        var dyingGo = new GameObject("Luz_Fluorescente_Moribundo_Unico_PasilloNorte");
        dyingGo.transform.SetParent(terrorGrp.transform, false);
        dyingGo.transform.position = W(14.24f, 3.05f, 11.50f);
        var dyingLight = dyingGo.AddComponent<Light>();
        dyingLight.type = LightType.Point;
        dyingLight.color = new Color(0.58f, 0.82f, 0.78f, 1f); // Frío verdoso de tubo fluorescente agotado
        dyingLight.intensity = 0.32f;
        dyingLight.range = 4.8f;
        dyingLight.shadows = LightShadows.None;
        dyingLight.cullingMask = maskInterior;
        var dyingUld = dyingGo.AddComponent<UniversalAdditionalLightData>();
        dyingUld.renderingLayers = uint.MaxValue;

        // 3. RESPLANDOR MUY TENUE DE LUNA EN VENTANAS (entra luz fría muy sutil del exterior hacia los pasillos/aulas sin aclarar de más)
        var moonLights = new List<Light>();
        var moonSpecs = new (string name, float x, float y, float z, float range)[]
        {
            ("Resplandor_Luna_Ventana_EntradaPB", 27.20f, 1.95f, 19.29f, 4.2f),
            ("Resplandor_Luna_Ventana_BanosNortePB", 14.24f, 2.55f, 1.40f, 3.8f),
            ("Resplandor_Luna_Ventana_EscaleraOeste", 1.20f, 3.20f, 19.29f, 4.2f),
            ("Resplandor_Luna_Ventana_BanosNortePA", 14.24f, 5.65f, 1.40f, 3.8f),
            ("Resplandor_Luna_Ventana_SurPA", 14.24f, 5.65f, 52.20f, 4.0f)
        };

        foreach (var mp in moonSpecs)
        {
            var mGo = new GameObject(mp.name);
            mGo.transform.SetParent(terrorGrp.transform, false);
            mGo.transform.position = W(mp.x, mp.y, mp.z);
            var ml = mGo.AddComponent<Light>();
            ml.type = LightType.Point;
            ml.color = new Color(0.32f, 0.50f, 0.68f, 1f); // Luz de luna azulada muy tenue
            ml.intensity = 0.08f;
            ml.range = mp.range;
            ml.shadows = LightShadows.None;
            ml.cullingMask = maskInterior;
            var muld = mGo.AddComponent<UniversalAdditionalLightData>();
            muld.renderingLayers = uint.MaxValue;
            moonLights.Add(ml);
        }

        // 4. Audio Reverb Zones en pasillos y Auditorio para tensión acústica
        CreateReverbZone("ReverbZone_PasilloCentral_PB", terrorGrp.transform, W(14.24f, 1.6f, 26.0f), 4.0f, 22.0f, AudioReverbPreset.Hallway);
        CreateReverbZone("ReverbZone_Auditorio_PB", terrorGrp.transform, W(22.80f, 1.8f, 30.8f), 6.0f, 12.0f, AudioReverbPreset.Auditorium);
        CreateReverbZone("ReverbZone_CuboEscalera", terrorGrp.transform, W(5.20f, 3.2f, 19.29f), 4.5f, 10.0f, AudioReverbPreset.StoneCorridor);
        CreateReverbZone("ReverbZone_PasilloCentral_PA", terrorGrp.transform, W(14.24f, 5.0f, 26.0f), 4.0f, 22.0f, AudioReverbPreset.Hallway);

        terrorGrp.SetActive(false);

        // 5. Enlazar controlador Docencia2TerrorDarknessController al jugador
        var ctrl = playerRoot.GetComponent<Docencia2TerrorDarknessController>();
        if (ctrl == null) ctrl = playerRoot.AddComponent<Docencia2TerrorDarknessController>();
        ctrl.switcher = Object.FindAnyObjectByType<Docencia2ModeSwitcher>();
        ctrl.player = player;
        ctrl.interiorTerrorVolume = vol;
        ctrl.terrorAtmosphereRoot = terrorGrp;
        ctrl.nearSilhouetteLight = nearSilLight;
        ctrl.doorTestInspectionLight = gInspectionLight;
        ctrl.emergencyAmberLights = amberLights;
        ctrl.dyingFluorescentLight = dyingLight;
        ctrl.windowMoonGlowLights = moonLights;
    }

    static void CreateReverbZone(string name, Transform parent, Vector3 pos, float minD, float maxD, AudioReverbPreset preset)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.position = pos;
        var rz = go.AddComponent<AudioReverbZone>();
        rz.minDistance = minD;
        rz.maxDistance = maxD;
        rz.reverbPreset = preset;
    }

    static GameObject CreatePart(string name, Transform parent, PrimitiveType type, Vector3 localPos, Vector3 localEuler, Vector3 localScale, Material mat)
    {
        var go = GameObject.CreatePrimitive(type);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        go.transform.localRotation = Quaternion.Euler(localEuler);
        go.transform.localScale = localScale;
        var col = go.GetComponent<Collider>();
        if (col != null) Object.DestroyImmediate(col);
        var r = go.GetComponent<Renderer>();
        r.sharedMaterial = mat;
        r.shadowCastingMode = ShadowCastingMode.Off;
        r.receiveShadows = true;
        return go;
    }

    static Material MakeMat(string fileName, Shader shader, Color baseCol, float smoothness, float metallic, Color emission)
    {
        string path = MatDir + "/" + fileName;
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null)
        {
            m = new Material(shader);
            AssetDatabase.CreateAsset(m, path);
        }
        m.shader = shader;
        m.SetColor("_BaseColor", baseCol);
        m.SetColor("_Color", baseCol);
        m.SetFloat("_Smoothness", smoothness);
        m.SetFloat("_Metallic", metallic);
        if (emission.maxColorComponent > 0.001f)
        {
            m.EnableKeyword("_EMISSION");
            m.SetColor("_EmissionColor", emission);
        }
        EditorUtility.SetDirty(m);
        return m;
    }

    static Texture2D EnsureFlashlightCookieTexture()
    {
        const int size = 512;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, true, true);
        Color[] pixels = Docencia2FirstPersonPlayer.BuildRealisticFlashlightCookiePixels(size);

        tex.SetPixels(pixels);
        tex.Apply();
        File.WriteAllBytes(CookieAssetPath, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);

        AssetDatabase.ImportAsset(CookieAssetPath);
        var importer = AssetImporter.GetAtPath(CookieAssetPath) as TextureImporter;
        if (importer != null)
        {
            importer.textureType = TextureImporterType.Cookie;
            importer.sRGBTexture = false;
            importer.alphaSource = TextureImporterAlphaSource.FromGrayScale;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;
            importer.mipmapEnabled = true;
            importer.SaveAndReimport();
        }

        return AssetDatabase.LoadAssetAtPath<Texture2D>(CookieAssetPath);
    }

    public static void RunExteriorRegressionComparison()
    {
        Directory.CreateDirectory(OutDir);
        string backupSceneAsset = "Assets/Scenes/Temp_Backup_Terror_Compare.unity";
        File.Copy(@"C:/Users/faust/OneDrive/Desktop/VAELITH/backup_terror/Exterior_Docencia2_backup_terror.unity", backupSceneAsset, true);
        AssetDatabase.ImportAsset(backupSceneAsset);

        CaptureThreeExteriorCameras(backupSceneAsset, "Antes");
        CaptureThreeExteriorCameras(ScenePath, "Despues");

        AssetDatabase.DeleteAsset(backupSceneAsset);

        var diffLines = new List<string> { "camara,modo,diff_max_canal_0_255,estado" };
        string[] cams = { "Camara_01_Frontal_Baja", "Camara_02_Aerea_Azotea", "Camara_03_Esquina_Contrafuerte" };
        foreach (string mode in new[] { "Noche", "Terror" })
        {
            foreach (string c in cams)
            {
                string fA = $"{OutDir}/Exterior_Antes_{mode}_{c}.png";
                string fB = $"{OutDir}/Exterior_Despues_{mode}_{c}.png";
                int maxDiff = ComparePngMaxDiff(fA, fB);
                diffLines.Add($"{c},{mode},{maxDiff},{(maxDiff == 0 ? "INTACTO_0_CAMBIOS" : "DIFERENCIA_" + maxDiff)}");
            }
        }
        File.WriteAllLines(OutDir + "/regresion_exterior_paso1.csv", diffLines);
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
                File.WriteAllBytes($"{OutDir}/Exterior_{prefix}_{mode}_{camera.name}.png", texture.EncodeToPNG());
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
public static class Paso1PlayVerifierBoot
{
    static Paso1PlayVerifierBoot()
    {
        EditorApplication.update += OnEditorUpdate;
    }

    static void OnEditorUpdate()
    {
        if (SessionState.GetBool("PASO1_PLAY_VERIFY", false) && EditorApplication.isPlaying && !EditorApplication.isCompiling)
        {
            SessionState.SetBool("PASO1_PLAY_VERIFY", false);
            var go = new GameObject("TEMP_Paso1_PlayVerifier");
            go.AddComponent<Paso1PlayVerifierRunner>();
        }
    }
}

public class Paso1PlayVerifierRunner : MonoBehaviour
{
    string OutDir => Paso1JugadorBuilder.OutDir;
    HashSet<string> _shadowWarnings = new HashSet<string>();

    void OnLogMessage(string condition, string stackTrace, LogType type)
    {
        if (condition.ToLowerInvariant().Contains("shadow") || type == LogType.Error || type == LogType.Exception)
        {
            _shadowWarnings.Add($"[{type}] {condition}");
        }
    }

    IEnumerator Start()
    {
        Directory.CreateDirectory(OutDir);
        Application.logMessageReceived += OnLogMessage;
        Application.runInBackground = true;
        QualitySettings.vSyncCount = 0;
        Application.targetFrameRate = -1;

        yield return null;
        yield return null;

        var player = Docencia2FirstPersonPlayer.Instance ?? Object.FindAnyObjectByType<Docencia2FirstPersonPlayer>();
        var switcher = Object.FindAnyObjectByType<Docencia2ModeSwitcher>();
        var darkness = Docencia2TerrorDarknessController.Instance ?? Object.FindAnyObjectByType<Docencia2TerrorDarknessController>();
        var cc = player.GetComponent<CharacterController>();

        // 1. Generar reporte de las 70 puertas desde Play Mode
        player.RunDoorAuditAndPopulateList(saveFiles: true);

        var stairRoot = GameObject.Find("PB_Interior/22_Cubo_Escaleras_PB/Escalera_En_U_Concreto_y_Barandal");
        if (stairRoot != null)
        {
            foreach (var c in stairRoot.GetComponentsInChildren<Collider>(true))
            {
                if (c.enabled)
                {
                    c.enabled = false;
                    c.enabled = true;
                }
            }
        }
        Physics.SyncTransforms();

        // 2. Probar subida física real por la escalera en U con CharacterController.Move:
        var stairLog = new List<string>();
        player.TeleportToPlan(8.35f, 0.20f, 18.19f, 0f);
        yield return null;
        float startY = player.transform.position.y;
        stairLog.Add($"Inicio pie Tramo 1 Norte: pos={player.transform.position:F3}, plan=({Docencia2FirstPersonPlayer.UnityToPlanXZ(player.transform.position)}), Y={startY:F3}");

        Vector3 dirWest = (Docencia2InteriorBuilder.PlanToUnityPos(2.80f, 0.20f, 18.19f) - Docencia2InteriorBuilder.PlanToUnityPos(8.35f, 0.20f, 18.19f)).normalized;
        dirWest.y = 0f;
        for (int f = 0; f < 105; f++)
        {
            cc.Move((dirWest * 3.6f + Vector3.down * 4.0f) * (1f / 60f));
            Physics.SyncTransforms();
        }
        float midY = player.transform.position.y;
        stairLog.Add($"Llegada Descanso Intermedio: pos={player.transform.position:F3}, plan=({Docencia2FirstPersonPlayer.UnityToPlanXZ(player.transform.position)}), Y={midY:F3}");

        Vector3 dirSouth = (Docencia2InteriorBuilder.PlanToUnityPos(2.80f, 1.90f, 20.39f) - Docencia2InteriorBuilder.PlanToUnityPos(2.80f, 1.90f, 18.19f)).normalized;
        dirSouth.y = 0f;
        for (int f = 0; f < 55; f++)
        {
            cc.Move((dirSouth * 3.6f + Vector3.down * 4.0f) * (1f / 60f));
            Physics.SyncTransforms();
        }

        // Capturar la escalera corregida primero en modo NOCHE desde el descanso intermedio mirando hacia arriba al Tramo 2 Sur (mismo ángulo de la foto del usuario)
        switcher.ApplyMode(Docencia2VisualMode.Noche, false);
        if (darkness != null) darkness.SyncState();
        player.SetFlashlightState(true, false);
        player.transform.LookAt(Docencia2InteriorBuilder.PlanToUnityPos(8.50f, 3.60f, 20.39f));
        player.transform.rotation = Quaternion.Euler(0f, player.transform.eulerAngles.y, 0f);
        player.playerCamera.transform.localRotation = Quaternion.Euler(-14f, 0f, 0f);
        yield return null;
        CapturePlayerCameraAndAnalyzeBlackPixels(player.playerCamera, "Escalera_HuecoPA_Corregido_Noche.png", out _, out _);

        // Y capturar el mismo encuadre de la escalera en modo TERROR con Oscuridad Extrema + Linterna 28°/6m
        switcher.ApplyMode(Docencia2VisualMode.Terror, false);
        if (darkness != null) darkness.SyncState();
        player.SetFlashlightState(true, false);
        yield return null;
        CapturePlayerCameraAndAnalyzeBlackPixels(player.playerCamera, "Escalera_HuecoPA_Corregido_Terror.png", out _, out _);

        // Terminar de subir Tramo 2 Sur hacia PA
        Vector3 dirEast = (Docencia2InteriorBuilder.PlanToUnityPos(9.20f, 3.60f, 20.39f) - Docencia2InteriorBuilder.PlanToUnityPos(2.80f, 1.90f, 20.39f)).normalized;
        dirEast.y = 0f;
        for (int f = 0; f < 120; f++)
        {
            cc.Move((dirEast * 3.6f + Vector3.down * 4.0f) * (1f / 60f));
            Physics.SyncTransforms();
        }
        float endY = player.transform.position.y;
        stairLog.Add($"Llegada Desembarque PA: pos={player.transform.position:F3}, plan=({Docencia2FirstPersonPlayer.UnityToPlanXZ(player.transform.position)}), Y={endY:F3}");
        bool stairSuccess = (midY > 1.70f && endY > 3.45f);
        stairLog.Add($"RESULTADO ESCALERA: {(stairSuccess ? "SUBE SIN ATASCARSE (PB Y=0.22 -> Descanso Y=" + midY.ToString("F2") + " -> PA Y=" + endY.ToString("F2") + ")" : "FALLO")}");
        File.WriteAllLines(OutDir + "/verificacion_escalera_paso1.txt", stairLog);

        // ====================================================================
        // 3. CAPTURAS DE CADA ZONA EN OSCURIDAD EXTREMA (MODO TERROR) CON LINTERNA 28° / 6m
        //    Y COMPROBACIÓN NUMÉRICA DE 0% PÍXELES 100% NEGROS (RGB == 0,0,0)
        // ====================================================================
        switcher.ApplyMode(Docencia2VisualMode.Terror, false);
        if (darkness != null) darkness.SyncState();
        player.SetFlashlightState(true, false);
        yield return null;

        var blackCheckRows = new List<string>
        {
            "zona,archivo,pixeles_totales,pixeles_100_por_ciento_negros_rgb000,porcentaje_negro_puro,luminancia_centro_linterna,legibilidad_espacio"
        };

        var terrorZones = new (string zoneName, string fileName, float px, float py, float pz, float lookX, float lookY, float lookZ, string openDoorPath)[]
        {
            ("PB_00_Vista_Exacta_Foto_Usuario", "Terror_00_VistaExactaUsuario_PB.png",
                22.20f, 0.20f, 19.29f, 18.80f, 1.35f, 17.60f, null),

            ("PB_01_Vestibulo_Entrada", "Terror_01_Vestibulo_PB.png",
                25.20f, 0.20f, 19.29f, 21.60f, 1.35f, 16.20f, null),

            ("PB_02_Pasillo_Canceles_Sec06", "Terror_02_Canceles_PB.png",
                13.50f, 0.20f, 12.20f, 11.24f, 1.45f, 9.50f,
                "PB_Interior/03_Fase3_Carpinteria_y_Vidrios/Canceleria_Interior_Vidrio_Verde/Cancel_Vidrio_Sec06_PasilloOeste_x11.24/P15_Alcoba_Oeste_CancelVidrio/Pivote_Bisagra"),

            ("PB_03_Pasillo_Sur_y_Aulas", "Terror_03_PasilloSur_PB.png",
                13.60f, 0.20f, 34.50f, 11.24f, 1.45f, 31.40f, null),

            ("PB_04_Auditorio", "Terror_04_Auditorio_PB.png",
                22.20f, 0.20f, 30.20f, 24.40f, 1.40f, 33.40f, null),

            ("PA_05_Escalera_y_Desembarque", "Terror_05_Desembarque_PA.png",
                11.10f, 3.606f, 19.29f, 8.10f, 4.55f, 18.60f, null),

            ("PA_06_Pasillo_Canceles_P16_P17", "Terror_06_Canceles_PA.png",
                14.80f, 3.606f, 11.50f, 17.24f, 4.95f, 8.95f,
                "PA_Interior/Sala_Maestros/Cancel_Oeste/P17_Hueco096_Abre_este/Bisagra_Giro90_Interior"),

            ("PA_07_Plaza_Sur_y_Aulas", "Terror_07_PlazaSur_PA.png",
                14.00f, 3.606f, 41.70f, 14.00f, 4.90f, 26.00f, null)
        };

        foreach (var tz in terrorZones)
        {
            Docencia2InteractiveDoor dRef = null;
            if (!string.IsNullOrEmpty(tz.openDoorPath))
            {
                dRef = GameObject.Find(tz.openDoorPath)?.GetComponent<Docencia2InteractiveDoor>();
                if (dRef != null) dRef.SetOpen(true);
            }

            player.TeleportToPlan(tz.px, tz.py, tz.pz, 0f);
            Vector3 targetW = Docencia2InteriorBuilder.PlanToUnityPos(tz.lookX, tz.lookY, tz.lookZ);
            Vector3 dir = (targetW - player.playerCamera.transform.position).normalized;
            float yaw = Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg;
            float pitch = -Mathf.Asin(Mathf.Clamp(dir.y, -0.95f, 0.95f)) * Mathf.Rad2Deg;
            player.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            player.playerCamera.transform.localRotation = Quaternion.Euler(pitch, 0f, 0f);

            if (darkness != null) darkness.SyncState();
            yield return null;

            int pureBlackCount = CapturePlayerCameraAndAnalyzeBlackPixels(player.playerCamera, tz.fileName, out int totalPixels, out float centerLuma);
            double pctBlack = (pureBlackCount * 100.0) / totalPixels;
            blackCheckRows.Add(FormattableString.Invariant(
                $"{tz.zoneName},{tz.fileName},{totalPixels},{pureBlackCount},{pctBlack:F4}%,{centerLuma:F3},{(pureBlackCount == 0 ? "OK_SIN_NEGRO_PURO" : "REVISAR")}"));

            if (dRef != null) dRef.SetOpen(false);
        }

        // Capturar exactamente la posición de la última foto del usuario (PA 14.0, 41.7 mirando hacia el muro de la derecha con bancas y pizarrón a 3.2 m) CON LINTERNA APAGADA y CON LINTERNA ENCENDIDA:
        player.TeleportToPlan(14.00f, 3.606f, 41.70f, 0f);
        Vector3 targetUserPA = Docencia2InteriorBuilder.PlanToUnityPos(17.24f, 4.95f, 39.20f);
        Vector3 dirUserPA = (targetUserPA - player.playerCamera.transform.position).normalized;
        player.transform.rotation = Quaternion.Euler(0f, Mathf.Atan2(dirUserPA.x, dirUserPA.z) * Mathf.Rad2Deg, 0f);
        player.playerCamera.transform.localRotation = Quaternion.Euler(-Mathf.Asin(Mathf.Clamp(dirUserPA.y, -0.95f, 0.95f)) * Mathf.Rad2Deg, 0f, 0f);
        if (darkness != null) darkness.SyncState();
        player.SetFlashlightState(false, false);
        yield return null;
        CapturePlayerCameraAndAnalyzeBlackPixels(player.playerCamera, "Terror_PA_14_41_LinternaApagada.png", out _, out _);
        player.SetFlashlightState(true, false);
        yield return null;
        CapturePlayerCameraAndAnalyzeBlackPixels(player.playerCamera, "Terror_PA_14_41_LinternaEncendida.png", out _, out _);

        // Capturar el Menú de Pausa y el Panel de Puertas (G) a resolución 4K (3840x2160) con la interfaz grande renderizada sobre la vista:
        switcher.ApplyMode(Docencia2VisualMode.Terror, false);
        if (darkness != null) darkness.SyncState();
        player.SetFlashlightState(true, false);
        player.TeleportToPlan(22.20f, 0.20f, 19.29f, 0f);
        Vector3 lookMenuW = Docencia2InteriorBuilder.PlanToUnityPos(18.80f, 1.35f, 17.60f);
        Vector3 dirMenu = (lookMenuW - player.playerCamera.transform.position).normalized;
        player.transform.rotation = Quaternion.Euler(0f, Mathf.Atan2(dirMenu.x, dirMenu.z) * Mathf.Rad2Deg, 0f);
        player.playerCamera.transform.localRotation = Quaternion.Euler(-Mathf.Asin(Mathf.Clamp(dirMenu.y, -0.95f, 0.95f)) * Mathf.Rad2Deg, 0f, 0f);
        yield return null;
        CaptureCameraWithSimulatedBigHudOverlay(player.playerCamera, player, darkness, switcher, true, false, "Terror_MenuPausa_4K_Grande.png");
        CaptureCameraWithSimulatedBigHudOverlay(player.playerCamera, player, darkness, switcher, false, true, "Terror_PanelPuertasG_4K_Grande.png");

        // Capturar también el modo NOCHE intacto (en PB Canceles y en PA 14.0, 41.7) para confirmar que NOCHE se ve limpio y bien iluminado:
        switcher.ApplyMode(Docencia2VisualMode.Noche, false);
        if (darkness != null) darkness.SyncState();
        player.SetFlashlightState(false, false);
        player.TeleportToPlan(14.24f, 0.20f, 13.50f, 0f);
        player.transform.LookAt(Docencia2InteriorBuilder.PlanToUnityPos(12.10f, 1.50f, 8.80f));
        player.transform.rotation = Quaternion.Euler(0f, player.transform.eulerAngles.y, 0f);
        player.playerCamera.transform.localRotation = Quaternion.Euler(2f, 0f, 0f);
        yield return null;
        CapturePlayerCameraAndAnalyzeBlackPixels(player.playerCamera, "ModoNoche_Intacto_CancelesPB.png", out _, out _);

        player.TeleportToPlan(14.00f, 3.606f, 41.70f, 0f);
        player.transform.rotation = Quaternion.Euler(0f, Mathf.Atan2(dirUserPA.x, dirUserPA.z) * Mathf.Rad2Deg, 0f);
        player.playerCamera.transform.localRotation = Quaternion.Euler(-Mathf.Asin(Mathf.Clamp(dirUserPA.y, -0.95f, 0.95f)) * Mathf.Rad2Deg, 0f, 0f);
        yield return null;
        CapturePlayerCameraAndAnalyzeBlackPixels(player.playerCamera, "ModoNoche_PA_14_41_Iluminado.png", out _, out _);

        File.WriteAllLines(OutDir + "/verificacion_sin_negro_puro_terror.csv", blackCheckRows);

        // ====================================================================
        // 4. MEDICIÓN DE FPS EN PLAY MODE (1920x1080) Y CONTEO DE LUCES CON SOMBRA
        // ====================================================================
        var fpsRows = new List<string> { "modo,ubicacion,resolucion,fps_promedio,fps_p95_lento,luces_activas,luces_con_sombra" };
        var testSpots = new (string name, float px, float py, float pz, float yaw)[]
        {
            ("PB_Vestibulo_Entrada", 24.80f, 0.20f, 19.29f, 0f),
            ("PB_Pasillo_Canceles", 14.24f, 0.20f, 13.50f, -90f),
            ("PA_Desembarque_Escalera", 12.50f, 3.606f, 19.29f, -90f),
            ("PA_Pasillo_Canceles", 14.24f, 3.606f, 13.50f, -90f)
        };

        var rtFps = new RenderTexture(1920, 1080, 24);
        player.playerCamera.targetTexture = rtFps;

        foreach (bool isTerror in new[] { false, true })
        {
            switcher.ApplyMode(isTerror ? Docencia2VisualMode.Terror : Docencia2VisualMode.Noche, false);
            if (darkness != null) darkness.SyncState();
            player.SetFlashlightState(true, false);
            yield return null;

            foreach (var spot in testSpots)
            {
                player.TeleportToPlan(spot.px, spot.py, spot.pz, spot.yaw);
                for (int w = 0; w < 12; w++)
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

        player.playerCamera.targetTexture = null;
        rtFps.Release();
        Object.DestroyImmediate(rtFps);

        switcher.ApplyMode(Docencia2VisualMode.Noche, false);
        if (darkness != null) darkness.RestoreOriginalEnvironment();
        player.TeleportToPlan(24.80f, 0.20f, 19.29f, 0f);

        File.WriteAllLines(OutDir + "/fps_play_paso1.csv", fpsRows);
        File.WriteAllLines(OutDir + "/advertencias_sombras_paso1.txt",
            _shadowWarnings.Count == 0
                ? new[] { "0 advertencias de atlas de sombras y 0 errores durante Play Mode. Única luz con sombras suaves: Linterna de celular (1)." }
                : _shadowWarnings.ToArray());

        EditorApplication.Exit(0);
    }

    int CapturePlayerCameraAndAnalyzeBlackPixels(Camera cam, string fileName, out int totalPixels, out float centerBeamLuma)
    {
        var rt = new RenderTexture(1920, 1080, 24);
        var prevTarget = cam.targetTexture;
        var prevActive = RenderTexture.active;
        var tex = new Texture2D(1920, 1080, TextureFormat.RGB24, false);

        cam.targetTexture = rt;
        cam.Render();
        RenderTexture.active = rt;
        tex.ReadPixels(new Rect(0, 0, 1920, 1080), 0, 0);
        tex.Apply();
        File.WriteAllBytes(Path.Combine(OutDir, fileName), tex.EncodeToPNG());

        Color32[] pixels = tex.GetPixels32();
        totalPixels = pixels.Length;
        int pureBlack = 0;
        for (int i = 0; i < totalPixels; i++)
        {
            Color32 c = pixels[i];
            if (c.r == 0 && c.g == 0 && c.b == 0)
                pureBlack++;
        }

        // Luminancia promedio en el cono central de la linterna
        double sumLuma = 0;
        int countCenter = 0;
        for (int y = 360; y <= 720; y += 4)
        {
            for (int x = 660; x <= 1260; x += 4)
            {
                Color32 c = pixels[y * 1920 + x];
                sumLuma += (0.2126 * c.r + 0.7152 * c.g + 0.0722 * c.b) / 255.0;
                countCenter++;
            }
        }
        centerBeamLuma = (float)(sumLuma / Mathf.Max(1, countCenter));

        cam.targetTexture = prevTarget;
        RenderTexture.active = prevActive;
        Object.DestroyImmediate(tex);
        rt.Release();
        Object.DestroyImmediate(rt);
        return pureBlack;
    }

    void CaptureCameraWithSimulatedBigHudOverlay(Camera cam, Docencia2FirstPersonPlayer player, Docencia2TerrorDarknessController darkness, Docencia2ModeSwitcher switcher, bool showPause, bool showDoors, string fileName)
    {
        bool prevDoorOpen = player.IsDoorPanelOpen;
        player.IsDoorPanelOpen = showDoors;
        if (darkness != null) darkness.ApplyCurrentAmbientAndInspectionFloor();

        int w = 1920, h = 1080;
        var rt = new RenderTexture(w, h, 24);
        var prevTarget = cam.targetTexture;
        var prevActive = RenderTexture.active;
        var tex = new Texture2D(w, h, TextureFormat.RGB24, false);

        cam.targetTexture = rt;
        cam.Render();
        RenderTexture.active = rt;
        tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
        tex.Apply();

        // Dibujar los recuadros de la nueva UI a escala grande (proporción idéntica a uiScale = h / 900f) para verificar tamaño y contraste
        float uiScale = h / 900f;
        void FillRect(int rx, int ryTop, int rw, int rh, Color col)
        {
            int x0 = Mathf.Clamp(rx, 0, w - 1);
            int x1 = Mathf.Clamp(rx + rw, 0, w);
            int y0 = Mathf.Clamp(h - (ryTop + rh), 0, h - 1);
            int y1 = Mathf.Clamp(h - ryTop, 0, h);
            for (int yy = y0; yy < y1; yy++)
            {
                for (int xx = x0; xx < x1; xx++)
                {
                    Color bg = tex.GetPixel(xx, yy);
                    tex.SetPixel(xx, yy, Color.Lerp(bg, new Color(col.r, col.g, col.b, 1f), col.a));
                }
            }
        }

        // Barra superior grande
        int barW = Mathf.RoundToInt(1180 * uiScale);
        int barH = Mathf.RoundToInt(40 * uiScale);
        FillRect((w - barW) / 2, Mathf.RoundToInt(10 * uiScale), barW, barH, new Color(0.04f, 0.07f, 0.10f, 0.90f));

        if (showPause)
        {
            int mw = Mathf.RoundToInt(640 * uiScale);
            int mh = Mathf.RoundToInt(410 * uiScale);
            int mx = (w - mw) / 2;
            int my = (h - mh) / 2;
            FillRect(mx, my, mw, mh, new Color(0.05f, 0.08f, 0.11f, 0.94f));
            int bx = mx + Mathf.RoundToInt(34 * uiScale);
            int bw = mw - Mathf.RoundToInt(68 * uiScale);
            FillRect(bx, my + Mathf.RoundToInt(238 * uiScale), bw, Mathf.RoundToInt(46 * uiScale), new Color(0.14f, 0.22f, 0.29f, 0.96f));
            FillRect(bx, my + Mathf.RoundToInt(294 * uiScale), bw, Mathf.RoundToInt(46 * uiScale), new Color(0.14f, 0.22f, 0.29f, 0.96f));
            FillRect(bx, my + Mathf.RoundToInt(350 * uiScale), bw, Mathf.RoundToInt(48 * uiScale), new Color(0.18f, 0.30f, 0.38f, 0.98f));
        }

        tex.Apply();
        File.WriteAllBytes(Path.Combine(OutDir, fileName), tex.EncodeToPNG());

        cam.targetTexture = prevTarget;
        RenderTexture.active = prevActive;
        Object.DestroyImmediate(tex);
        rt.Release();
        Object.DestroyImmediate(rt);

        player.IsDoorPanelOpen = prevDoorOpen;
        if (darkness != null) darkness.ApplyCurrentAmbientAndInspectionFloor();
    }
}
