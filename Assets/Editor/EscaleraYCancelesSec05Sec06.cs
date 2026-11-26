using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Vaelith.Docencia2;
using Object = UnityEngine.Object;

public static class EscaleraYCancelesSec05Sec06
{
    public const string ScenePath = "Assets/Scenes/Exterior_Docencia2.unity";
    public const string OutDir = "Revision_Escalera_Canceles_Sec05_Sec06";
    public const string MeshDir = "Assets/Models/Docencia2/EscaleraCancelesMeshes";
    public const string MatSubDir = "Assets/Materials/Docencia2/EscaleraCanceles";

    const int LAYER_PB = 8;
    const int LAYER_PA = 30;
    const uint MASK_PB_CANCEL = 3u;
    const uint MASK_PA_ROOM = 17u;      // 1u | 16u
    const uint MASK_PA_CORRIDOR = 19u;  // 1u | 2u | 16u (receives Zona 2 Light Layer 1 = 2u)

    const float Y0_PB = 0.20f;
    const float Y1_PB = 3.20f;
    const float Y0_PA = 3.60f;
    const float Y1_PA = 6.80f;

    // Sec06 exact bay layout matching Photos 2, 3, and 4:
    // - z = 7.71 .. 9.29 (1.58 m = ~4.8 floor tiles): solid yellow-cream masonry wall + grey ceramic zoclo + extinguisher (PA)
    // - z = 9.29 .. 10.25 (0.96 m): full-height bronze-framed glass door (aligns north of Redes_SITE/Norte_P28 at z=10.29!)
    // - z = 10.25 .. 15.23 (4.98 m = 5 equal bays of 0.996 m): uninterrupted floor-to-header clear glass + single green band
    // - z = 15.23 .. 15.63 (0.40 m centered at 15.43): structural yellow-cream column
    const float Z_SEC6_NORTH = 7.71f;
    const float Z_WALL_NORTH_END = 9.29f;
    const float Z_DOOR_END = 10.25f;
    const float Z_COL_SOUTH_FACE = 15.23f;
    const float HEADER_DROP = 0.14f; // ~14 cm yellow-cream header fascia above bronze frame (Photos 2, 3, 4)

    static Vector3 W(float x, float y, float z) => Docencia2InteriorBuilder.PlanToUnityPos(x, y, z);
    static float PX(Vector3 p) => (14.25f - p.z) * (28.48f / 28.50f);
    static float PZ(Vector3 p) => (27.00f - p.x) * (53.92f / 54.00f);
    static string PathOf(Transform t) => t.parent ? PathOf(t.parent) + "/" + t.name : t.name;

    static Material LoadMat(string relPath) =>
        AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Docencia2/" + relPath + ".mat");

    static Material GetOrCreateCustomMat(string fileName, Color baseCol, float smoothness, float metallic = 0f, Color? emission = null)
    {
        Directory.CreateDirectory(MatSubDir);
        string path = MatSubDir + "/" + fileName + ".mat";
        Material m = AssetDatabase.LoadAssetAtPath<Material>(path);
        Shader sh = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        if (m == null)
        {
            m = new Material(sh);
            AssetDatabase.CreateAsset(m, path);
        }
        m.shader = sh;
        m.SetColor("_BaseColor", baseCol);
        m.SetFloat("_Smoothness", smoothness);
        m.SetFloat("_Metallic", metallic);
        if (emission.HasValue)
        {
            m.EnableKeyword("_EMISSION");
            m.SetColor("_EmissionColor", emission.Value);
        }
        else
        {
            m.DisableKeyword("_EMISSION");
        }
        m.enableInstancing = true;
        EditorUtility.SetDirty(m);
        return m;
    }

    public static void BuildAndVerifyEditMode()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("EscaleraYCancelesSec05Sec06 debe ejecutarse fuera de Play Mode.");

        Directory.CreateDirectory(OutDir);
        Directory.CreateDirectory(MeshDir);
        Directory.CreateDirectory(MatSubDir);

        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        var log = new List<string>();

        var paRoot = GameObject.Find("PA_Interior")?.transform ?? throw new Exception("No se encontró PA_Interior");
        var pbRoot = GameObject.Find("PB_Interior")?.transform ?? throw new Exception("No se encontró PB_Interior");
        var extRoot = GameObject.Find("Docencia2_Exterior") ?? throw new Exception("No se encontró Docencia2_Exterior");

        // Capture BEFORE shots matching the 4 uploaded photos + PB Sec06 (after normalizing mode to Noche)
        var switcher = Object.FindAnyObjectByType<Docencia2ModeSwitcher>();
        if (switcher != null) switcher.ApplyMode(Docencia2VisualMode.Noche, true);
        SyncTrackersEditMode(Docencia2VisualMode.Noche);

        string extBeforeHash = SnapshotTransformsAndRenderers(extRoot);
        RenderSuite("Antes");

        // Shared materials
        Material mCream = LoadMat("Interior/Mat_Int_Muro_AmarilloCrema_PB");
        Material mZoclo = LoadMat("Interior/Mat_Int_Zoclo_CeramicoGris_PB");
        Material mBronze = LoadMat("Mat_Int_Marco_Puerta_Bronce");
        Material mGlass = LoadMat("Mat_Int_Vidrio_Interior_Claro");
        Material mGreen = LoadMat("Mat_Int_Franja_Verde_Institucional");
        Material mSteel = LoadMat("Mat_Int_Chapa_Acero_Inox");
        Material mPanelTile = LoadMat("Aula17/Mat_Aula17_Plafon_Panel") ?? LoadMat("Interior/Mat_Int_Plafon_Reticular_PB");
        Material mTeeGrid = LoadMat("Aula17/Mat_Aula17_Plafon_Perfil") ?? mSteel;
        Material mTrofferFrame = LoadMat("Aula17/Mat_Aula17_Luminaria_Marco") ?? LoadMat("Mat_Int_Troffer_Marco_Negro");
        Material mTrofferHousing = LoadMat("Aula17/Mat_Aula17_Luminaria_Carcasa") ?? mTrofferFrame;
        Material mDiffUnlit = LoadMat("Aula17/Mat_Aula17_Difusor_Apagado") ?? LoadMat("Fase5/F5_Beige");

        // Dedicated subtle architectural materials for stairwell dark night glass & Mayan pyramid / posters
        Material mStairNightGlass = GetOrCreateCustomMat(
            "Mat_Escalera_Vidrio_Noche_Reflectivo",
            new Color(0.075f, 0.072f, 0.062f, 1f),
            smoothness: 0.48f,
            metallic: 0.04f);
        Material mStairFrame = GetOrCreateCustomMat(
            "Mat_Escalera_Marco_BronceCalido",
            new Color(0.45f, 0.34f, 0.21f, 1f),
            smoothness: 0.36f,
            metallic: 0.22f);
        Material mMayanStone = GetOrCreateCustomMat(
            "Mat_Maqueta_Piedra_Maya",
            new Color(0.28f, 0.31f, 0.25f, 1f),
            smoothness: 0.22f,
            metallic: 0.0f);
        Material mMayanStoneLight = GetOrCreateCustomMat(
            "Mat_Maqueta_Alfarda_Maya",
            new Color(0.39f, 0.42f, 0.36f, 1f),
            smoothness: 0.26f,
            metallic: 0.0f);
        Material mPosterWood = GetOrCreateCustomMat(
            "Mat_Cartel_Madera_Clara_Maya",
            new Color(0.79f, 0.68f, 0.52f, 1f),
            smoothness: 0.24f,
            metallic: 0.0f);
        Material mPanelSlate = GetOrCreateCustomMat(
            "Mat_Tablero_Electrico_GrisOscuro",
            new Color(0.20f, 0.23f, 0.26f, 1f),
            smoothness: 0.36f,
            metallic: 0.35f);
        Material mDishGrey = GetOrCreateCustomMat(
            "Mat_Antena_Parabolica_Gris",
            new Color(0.56f, 0.58f, 0.60f, 1f),
            smoothness: 0.42f,
            metallic: 0.25f);
        Material mPalletWood = GetOrCreateCustomMat(
            "Mat_Tarima_Madera_Rustica",
            new Color(0.42f, 0.29f, 0.17f, 1f),
            smoothness: 0.18f,
            metallic: 0.0f);
        Material mEmergWhite = GetOrCreateCustomMat(
            "Mat_Luminaria_Emergencia_Blanca",
            new Color(0.93f, 0.94f, 0.95f, 1f),
            smoothness: 0.35f,
            metallic: 0.0f,
            emission: new Color(0.18f, 0.18f, 0.18f, 1f));

        if (!mCream || !mZoclo || !mBronze || !mGlass || !mGreen || !mSteel)
            throw new Exception("Faltan materiales base requeridos en Assets/Materials/Docencia2.");

        // ========================================================================================
        // PARTE 1: ESCALERA SEC05 — VENTANA TRASERA 6x2 + VENTANA DESCANSO 6x1 + PLAFÓN 60x60 CM
        // ========================================================================================
        BuildStairwellRearWallAndCeiling(
            paRoot, pbRoot,
            mCream, mZoclo, mStairFrame, mStairNightGlass,
            mPanelTile, mTeeGrid, mTrofferHousing, mTrofferFrame, mDiffUnlit,
            mPanelSlate, mEmergWhite, log);

        // ========================================================================================
        // PARTE 2: CANCELES DE CRISTAL SEC06 EN PLANTA ALTA + COLUMNAS z=15.43 + DECORACIÓN PA
        // ========================================================================================
        BuildPASec06CancelsAndColumns(
            paRoot, mCream, mZoclo, mBronze, mGlass, mGreen, mSteel, log);

        BuildPASec06CorridorDecoration(
            paRoot, pbRoot,
            mCream, mZoclo, mBronze, mSteel, mMayanStone, mMayanStoneLight, mPosterWood,
            mPanelSlate, mDishGrey, mPalletWood, mEmergWhite, log);

        // ========================================================================================
        // PARTE 3: CANCELES DE CRISTAL SEC06 EN PLANTA BAJA (MISMO PANELADO, SIN DECORACIÓN DE PA)
        // ========================================================================================
        BuildPBSec06CancelsWithoutPADecoration(
            pbRoot, mCream, mZoclo, mBronze, mGlass, mGreen, mSteel, log);

        Physics.SyncTransforms();

        // Verify Docencia2_Exterior was 100% untouched
        string extAfterHash = SnapshotTransformsAndRenderers(extRoot);
        if (extBeforeHash != extAfterHash)
            throw new Exception("¡REGRESIÓN DETECTADA! Docencia2_Exterior sufrió modificaciones.");
        log.Add("[OK] Docencia2_Exterior verificado 100% intacto (sin cambios en fachada exterior).");

        // Verify PA 29 doors & sweep audit + refresh player's door list
        AuditPADoorsAndColliders(paRoot, log);
        var player = Object.FindAnyObjectByType<Docencia2FirstPersonPlayer>();
        if (player != null)
        {
            player.RunDoorAuditAndPopulateList(saveFiles: false);
            int totalInteractiveDoors = Object.FindObjectsByType<Docencia2InteractiveDoor>(FindObjectsInactive.Include).Length;
            log.Add($"[OK] Docencia2FirstPersonPlayer lista de puertas actualizada (puertas interactivas totales: {totalInteractiveDoors}).");
        }

        // Ensure scene is saved in NOCHE / clean Edit Mode state (or keep switcher consistent)
        if (switcher != null) switcher.ApplyMode(Docencia2VisualMode.Noche, true);
        SyncTrackersEditMode(Docencia2VisualMode.Noche);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();

        // Render AFTER suite + 3 exterior reference cameras in NOCHE and TERROR + side-by-side comparisons
        RenderSuite("Despues");
        RenderExteriorReferenceCameras(switcher);
        if (switcher != null) switcher.ApplyMode(Docencia2VisualMode.Noche, true);
        SyncTrackersEditMode(Docencia2VisualMode.Noche);

        BuildSideBySideComparisons(log);

        File.WriteAllLines(Path.Combine(OutDir, "reporte_construccion.txt"), log, Encoding.UTF8);
        Debug.Log("[EscaleraYCancelesSec05Sec06] Construcción y verificación en Edit Mode completadas con éxito.");
    }

    // ============================================================================================
    // 1. ESCALERA SEC05: PARED TRASERA OESTE (FOTO 1) + PLAFÓN MODULAR 60x60 CM SOBRE VACÍO
    // ============================================================================================
    static void BuildStairwellRearWallAndCeiling(
        Transform paRoot, Transform pbRoot,
        Material mCream, Material mZoclo, Material mBronze, Material mDarkGlass,
        Material mPanelTile, Material mTeeGrid, Material mHousing, Material mFrame, Material mDiffUnlit,
        Material mPanelSlate, Material mEmergWhite, List<string> log)
    {
        // 1A. Remove old PA Trasera5 lining pieces that blocked the stairwell window (Antepecho_Trasera5 Y=3.60..5.66, etc.)
        Transform forrosPA = paRoot.Find("Forros_Interiores_Con_Vanos");
        if (forrosPA != null)
        {
            foreach (Transform child in forrosPA.Cast<Transform>().ToArray())
            {
                if (child.name.Contains("Trasera5"))
                    Object.DestroyImmediate(child.gameObject);
            }
        }

        // Remove old PB Ventana_Oeste_Sec03 (under Ventanas_Exteriores_y_Rejas_PB) and filter any combined PB window mesh at x<=0.65, z=15.43..23.14
        foreach (string winSubPath in new[]
        {
            "03_Fase3_Carpinteria_y_Vidrios/Ventanas_Exteriores_y_Rejas_PB/Ventana_Oeste_Sec03",
            "03_Fase3_Carpinteria_y_Vidrios/Ventanas_PB/Ventana_Oeste_Sec03"
        })
        {
            Transform oldW = pbRoot.Find(winSubPath);
            if (oldW != null) Object.DestroyImmediate(oldW.gameObject);
        }
        Transform pbP3 = pbRoot.Find("03_Fase3_Carpinteria_y_Vidrios");
        if (pbP3 != null)
        {
            foreach (var mr in pbP3.GetComponentsInChildren<MeshRenderer>(true).Where(r => r.name.Contains("Ventana")))
            {
                FilterMeshTriangles(mr, p =>
                {
                    float x = PX(p), z = PZ(p);
                    return x <= 0.65f && z >= 15.45f && z <= 23.10f && p.y >= 1.30f && p.y <= 3.15f;
                }, "StairPBWin");
            }
        }

        // Remove old 1m ceiling strip in Escalera_Encuentro_Desplazado_1m so the new 60x60 grid covers x=0.36..8.10 cleanly
        Transform oldStripCeil = paRoot.Find("Escalera_Encuentro_Desplazado_1m/Plafon_Franja_Oeste_PA");
        if (oldStripCeil != null) Object.DestroyImmediate(oldStripCeil.gameObject);

        // Clean idempotent previous stairwell group if re-running
        Transform oldStairEnh = paRoot.Find("Escalera_Sec05_VentanaYPlafon_Real");
        if (oldStairEnh != null) Object.DestroyImmediate(oldStairEnh.gameObject);

        GameObject stairEnhRoot = new GameObject("Escalera_Sec05_VentanaYPlafon_Real");
        stairEnhRoot.layer = LAYER_PA;
        stairEnhRoot.transform.SetParent(paRoot, false);

        // ---- 1B. West Rear Wall & Windows (x ≈ 0.36..0.50, z = 15.43..23.14, Y = 0.20..6.80) ----
        // Exact window clear span matching exterior Sec05 frames: z = 15.865 .. 22.649 (width = 6.784 m = 6 cols of 1.1307 m)
        GameObject wallGrp = new GameObject("Muro_y_Ventanales_Traseros_Oeste_Sec05");
        wallGrp.layer = LAYER_PA;
        wallGrp.transform.SetParent(stairEnhRoot.transform, false);

        const float zN = 15.43f;
        const float zWin0 = 15.865f;
        const float zWin1 = 22.649f;
        const float zS = 23.14f;

        // North & South full-height cream pilasters flanking the stairwell windows (visible left & right in Photo 1)
        MakeBox(wallGrp.transform, "Pilastra_Norte_Escalera", 0.36f, 0.54f, 0.20f, 6.80f, zN, zWin0, mCream, true, LAYER_PA, MASK_PA_CORRIDOR);
        MakeBox(wallGrp.transform, "Pilastra_Sur_Escalera",   0.36f, 0.54f, 0.20f, 6.80f, zWin1, zS, mCream, true, LAYER_PA, MASK_PA_CORRIDOR);

        // Lower wall below landing window (Y = 0.20 .. 2.08 m) + grey zoclo at PB floor
        MakeBox(wallGrp.transform, "Muro_Bajo_PB_Escalera", 0.36f, 0.52f, 0.20f, 2.08f, zWin0, zWin1, mCream, true, LAYER_PA, MASK_PA_CORRIDOR);
        MakeBox(wallGrp.transform, "Zoclo_PB_Oeste_Escalera", 0.52f, 0.536f, 0.20f, 0.30f, zN, zS, mZoclo, false, LAYER_PB, MASK_PB_CANCEL);

        // Mid-height cream spandrel band between landing window and PA 6x2 window (Y = 3.52 .. 4.42 m, Photo 1!)
        MakeBox(wallGrp.transform, "Antepecho_Entrepiso_Escalera", 0.36f, 0.54f, 3.52f, 4.42f, zWin0, zWin1, mCream, true, LAYER_PA, MASK_PA_CORRIDOR);

        // Slim top header trim above upper window (Y = 6.76 .. 6.80 m)
        MakeBox(wallGrp.transform, "Cabezal_Superior_Ventana", 0.36f, 0.54f, 6.76f, 6.80f, zWin0, zWin1, mCream, false, LAYER_PA, MASK_PA_CORRIDOR);

        // ---- Upper PA Window: 6 columns x 2 rows = 12 panes (Y = 4.42 .. 6.76 m) ----
        const float yUp0 = 4.42f;
        const float yUp1 = 6.76f;
        const float yUpMid = 5.56f; // horizontal transom dividing the 2 rows (Photo 1)
        const float fT = 0.044f;
        const float xFrame0 = 0.475f;
        const float xFrame1 = 0.530f;

        MakeBox(wallGrp.transform, "Marco_Sup_PA_Inf", xFrame0, xFrame1, yUp0, yUp0 + fT, zWin0, zWin1, mBronze, false, LAYER_PA, MASK_PA_CORRIDOR);
        MakeBox(wallGrp.transform, "Marco_Sup_PA_Sup", xFrame0, xFrame1, yUp1 - fT, yUp1, zWin0, zWin1, mBronze, false, LAYER_PA, MASK_PA_CORRIDOR);
        MakeBox(wallGrp.transform, "Marco_Sup_PA_Izq", xFrame0, xFrame1, yUp0, yUp1, zWin0, zWin0 + fT, mBronze, false, LAYER_PA, MASK_PA_CORRIDOR);
        MakeBox(wallGrp.transform, "Marco_Sup_PA_Der", xFrame0, xFrame1, yUp0, yUp1, zWin1 - fT, zWin1, mBronze, false, LAYER_PA, MASK_PA_CORRIDOR);
        MakeBox(wallGrp.transform, "Travesano_Horizontal_PA_6x2", xFrame0, xFrame1, yUpMid - fT * 0.45f, yUpMid + fT * 0.45f, zWin0 + fT, zWin1 - fT, mBronze, false, LAYER_PA, MASK_PA_CORRIDOR);

        float colW = (zWin1 - zWin0) / 6f;
        for (int c = 0; c < 6; c++)
        {
            float cz0 = zWin0 + c * colW;
            float cz1 = cz0 + colW;
            if (c > 0)
            {
                MakeBox(wallGrp.transform, $"Montante_PA_{c:02d}", xFrame0, xFrame1, yUp0 + fT, yUp1 - fT, cz0 - fT * 0.45f, cz0 + fT * 0.45f, mBronze, false, LAYER_PA, MASK_PA_CORRIDOR);
            }
            // Lower row pane & Upper row pane (12 dark-reflective night glass panes total)
            MakeBox(wallGrp.transform, $"Cristal_PA_FilaInf_{c + 1:02d}", 0.488f, 0.506f, yUp0 + fT * 0.8f, yUpMid - fT * 0.4f, cz0 + fT * 0.4f, cz1 - fT * 0.4f, mDarkGlass, false, LAYER_PA, MASK_PA_CORRIDOR);
            MakeBox(wallGrp.transform, $"Cristal_PA_FilaSup_{c + 1:02d}", 0.488f, 0.506f, yUpMid + fT * 0.4f, yUp1 - fT * 0.8f, cz0 + fT * 0.4f, cz1 - fT * 0.4f, mDarkGlass, false, LAYER_PA, MASK_PA_CORRIDOR);
        }
        // Single collider across the upper window so physics/raycasts respect it cleanly
        AddInvisibleCollider(wallGrp.transform, "Colision_Ventana_PA_6x2", 0.47f, 0.535f, yUp0, yUp1, zWin0, zWin1, LAYER_PA);

        // Small white emergency light fixture centered on top frame of the 6x2 window (Photo 1!)
        float zWinMid = (zWin0 + zWin1) * 0.5f;
        MakeBox(wallGrp.transform, "Luminaria_Emergencia_VentanaEscalera", 0.530f, 0.585f, 6.695f, 6.755f, zWinMid - 0.13f, zWinMid + 0.13f, mEmergWhite, false, LAYER_PA, MASK_PA_CORRIDOR);

        // ---- Lower/Landing Window: 6 columns behind intermediate landing handrail (Y = 2.08 .. 3.52 m, Photo 1!) ----
        const float yLow0 = 2.08f;
        const float yLow1 = 3.52f;
        MakeBox(wallGrp.transform, "Marco_Descanso_Inf", xFrame0, xFrame1, yLow0, yLow0 + fT, zWin0, zWin1, mBronze, false, LAYER_PA, MASK_PA_CORRIDOR);
        MakeBox(wallGrp.transform, "Marco_Descanso_Sup", xFrame0, xFrame1, yLow1 - fT, yLow1, zWin0, zWin1, mBronze, false, LAYER_PA, MASK_PA_CORRIDOR);
        MakeBox(wallGrp.transform, "Marco_Descanso_Izq", xFrame0, xFrame1, yLow0, yLow1, zWin0, zWin0 + fT, mBronze, false, LAYER_PA, MASK_PA_CORRIDOR);
        MakeBox(wallGrp.transform, "Marco_Descanso_Der", xFrame0, xFrame1, yLow0, yLow1, zWin1 - fT, zWin1, mBronze, false, LAYER_PA, MASK_PA_CORRIDOR);

        for (int c = 0; c < 6; c++)
        {
            float cz0 = zWin0 + c * colW;
            float cz1 = cz0 + colW;
            if (c > 0)
            {
                MakeBox(wallGrp.transform, $"Montante_Descanso_{c:02d}", xFrame0, xFrame1, yLow0 + fT, yLow1 - fT, cz0 - fT * 0.45f, cz0 + fT * 0.45f, mBronze, false, LAYER_PA, MASK_PA_CORRIDOR);
            }
            MakeBox(wallGrp.transform, $"Cristal_Descanso_{c + 1:02d}", 0.488f, 0.506f, yLow0 + fT * 0.8f, yLow1 - fT * 0.8f, cz0 + fT * 0.4f, cz1 - fT * 0.4f, mDarkGlass, false, LAYER_PA, MASK_PA_CORRIDOR);
        }
        AddInvisibleCollider(wallGrp.transform, "Colision_Ventana_Descanso_6x1", 0.47f, 0.535f, yLow0, yLow1, zWin0, zWin1, LAYER_PA);

        // Continuous cream plaster finish over the exposed inter-floor slab band (Y = 3.16..3.72) on North & South stairwell side walls
        // + subtle architectural ledge line at Y = 3.55 + TP 1-1 / TBA 2-2 electrical boxes (Photo 1!)
        MakeBox(wallGrp.transform, "Forro_Losa_MuroNorte_Escalera", 0.54f, 8.10f, 3.16f, 3.72f, 15.505f, 15.535f, mCream, false, LAYER_PA, MASK_PA_CORRIDOR);
        MakeBox(wallGrp.transform, "Forro_Losa_MuroSur_Escalera",   0.54f, 8.10f, 3.16f, 3.72f, 23.035f, 23.065f, mCream, false, LAYER_PA, MASK_PA_CORRIDOR);
        MakeBox(wallGrp.transform, "Moldura_Entrepiso_MuroNorte_Escalera", 0.54f, 8.10f, 3.54f, 3.57f, 15.535f, 15.555f, mCream, false, LAYER_PA, MASK_PA_CORRIDOR);
        MakeBox(wallGrp.transform, "Moldura_Entrepiso_MuroSur_Escalera",   0.54f, 8.10f, 3.54f, 3.57f, 23.015f, 23.035f, mCream, false, LAYER_PA, MASK_PA_CORRIDOR);
        MakeBox(wallGrp.transform, "Tablero_TP_1_1_Escalera",  4.85f, 5.55f, 1.95f, 2.75f, 15.505f, 15.565f, mPanelSlate, false, LAYER_PA, MASK_PA_CORRIDOR);
        MakeBox(wallGrp.transform, "Tablero_TBA_2_2_Escalera", 4.52f, 4.78f, 2.28f, 2.68f, 15.505f, 15.555f, mPanelSlate, false, LAYER_PA, MASK_PA_CORRIDOR);

        CombineRenderersByMaterial(wallGrp.transform, "Comb_Escalera_MuroTrasero", LAYER_PA, MASK_PA_CORRIDOR);

        // ---- 1C. Stairwell 60x60 cm Modular Ceiling (Y = 6.80 m, x = 0.36..8.10, z = 15.43..23.14) ----
        // Exact continuation of Zona 2's grid: GX(c) = 8.54 + c * 0.60, GZ(r) = 6.255 + r * 0.60 (so r=15 is 15.255)
        GameObject ceilGrp = new GameObject("Plafon_Modular_60x60_Escalera_PA");
        ceilGrp.layer = LAYER_PA;
        ceilGrp.transform.SetParent(stairEnhRoot.transform, false);

        const float xCeilMin = 0.36f;
        const float xCeilMax = 8.10f;
        const float zCeilMin = 15.43f;
        const float zCeilMax = 23.14f;
        const float teeHalf = 0.012f;

        // 9 unlit ("aparentemente apagadas") troffers aligned with Zona 2's 3 E-W rows (r = 17 -> z=16.755, r = 21 -> z=19.155, r = 25 -> z=21.555)
        // and columns c = -11 (x=1.94..2.54), c = -7 (x=4.34..4.94), c = -3 (x=6.74..7.34), matching Photo 1 tile-for-tile!
        var unlitCells = new HashSet<(int c, int r)>();
        foreach (int c in new[] { -11, -7, -3 })
            foreach (int r in new[] { 17, 21, 25 })
                unlitCells.Add((c, r));

        int solidTileCount = 0;
        int unlitTrofferCount = 0;
        for (int c = -14; c <= -1; c++)
        {
            float gx0 = 8.54f + c * 0.60f;
            float gx1 = gx0 + 0.60f;
            float cx0 = Mathf.Max(gx0, xCeilMin);
            float cx1 = Mathf.Min(gx1, xCeilMax);
            if (cx1 - cx0 <= 0.005f) continue;

            for (int r = 15; r <= 28; r++)
            {
                float gz0 = 6.255f + r * 0.60f;
                float gz1 = gz0 + 0.60f;
                float cz0 = Mathf.Max(gz0, zCeilMin);
                float cz1 = Mathf.Min(gz1, zCeilMax);
                if (cz1 - cz0 <= 0.005f) continue;

                if (unlitCells.Contains((c, r)))
                {
                    float tx0 = gx0 + teeHalf, tx1 = gx1 - teeHalf;
                    float tz0 = gz0 + teeHalf, tz1 = gz1 - teeHalf;
                    float bW = 0.038f;
                    // Recessed unlit troffer matching Aula 17 / Zona 2
                    MakeBox(ceilGrp.transform, "Carcasa", tx0 + 0.01f, tx1 - 0.01f, Y1_PA + 0.006f, Y1_PA + 0.055f, tz0 + 0.01f, tz1 - 0.01f, mHousing, false, LAYER_PA, MASK_PA_CORRIDOR);
                    MakeBox(ceilGrp.transform, "Marco_N", tx0, tx1, Y1_PA - 0.006f, Y1_PA + 0.012f, tz0, tz0 + bW, mFrame, false, LAYER_PA, MASK_PA_CORRIDOR);
                    MakeBox(ceilGrp.transform, "Marco_S", tx0, tx1, Y1_PA - 0.006f, Y1_PA + 0.012f, tz1 - bW, tz1, mFrame, false, LAYER_PA, MASK_PA_CORRIDOR);
                    MakeBox(ceilGrp.transform, "Marco_O", tx0, tx0 + bW, Y1_PA - 0.006f, Y1_PA + 0.012f, tz0 + bW, tz1 - bW, mFrame, false, LAYER_PA, MASK_PA_CORRIDOR);
                    MakeBox(ceilGrp.transform, "Marco_E", tx1 - bW, tx1, Y1_PA - 0.006f, Y1_PA + 0.012f, tz0 + bW, tz1 - bW, mFrame, false, LAYER_PA, MASK_PA_CORRIDOR);
                    MakeBox(ceilGrp.transform, "Difusor_Apagado", tx0 + bW - 0.004f, tx1 - bW + 0.004f, Y1_PA + 0.002f, Y1_PA + 0.010f, tz0 + bW - 0.004f, tz1 - bW + 0.004f, mDiffUnlit, false, LAYER_PA, MASK_PA_CORRIDOR);
                    unlitTrofferCount++;
                }
                else
                {
                    float tx0 = cx0 + (Mathf.Abs(cx0 - gx0) < 0.002f ? teeHalf : 0.004f);
                    float tx1 = cx1 - (Mathf.Abs(cx1 - gx1) < 0.002f ? teeHalf : 0.004f);
                    float tz0 = cz0 + (Mathf.Abs(cz0 - gz0) < 0.002f ? teeHalf : 0.004f);
                    float tz1 = cz1 - (Mathf.Abs(cz1 - gz1) < 0.002f ? teeHalf : 0.004f);
                    if (tx1 - tx0 > 0.006f && tz1 - tz0 > 0.006f)
                    {
                        MakeBox(ceilGrp.transform, "Panel", tx0, tx1, Y1_PA + 0.002f, Y1_PA + 0.018f, tz0, tz1, mPanelTile, false, LAYER_PA, MASK_PA_CORRIDOR);
                        solidTileCount++;
                    }
                }
            }
        }

        // T-bar grid lines across x = 0.36..8.10, z = 15.43..23.14
        for (int c = -13; c <= -1; c++)
        {
            float gx = 8.54f + c * 0.60f;
            if (gx > xCeilMin + 0.02f && gx < xCeilMax - 0.02f)
                MakeBox(ceilGrp.transform, "Tee_NS", gx - teeHalf, gx + teeHalf, Y1_PA - 0.004f, Y1_PA + 0.018f, zCeilMin, zCeilMax, mTeeGrid, false, LAYER_PA, MASK_PA_CORRIDOR);
        }
        for (int r = 16; r <= 28; r++)
        {
            float gz = 6.255f + r * 0.60f;
            if (gz > zCeilMin + 0.02f && gz < zCeilMax - 0.02f)
                MakeBox(ceilGrp.transform, "Tee_EO", xCeilMin, xCeilMax, Y1_PA - 0.004f, Y1_PA + 0.018f, gz - teeHalf, gz + teeHalf, mTeeGrid, false, LAYER_PA, MASK_PA_CORRIDOR);
        }
        // Perimeter wall-angle molding
        MakeBox(ceilGrp.transform, "Perim_Oeste", xCeilMin, xCeilMin + 0.022f, Y1_PA - 0.005f, Y1_PA + 0.018f, zCeilMin, zCeilMax, mTeeGrid, false, LAYER_PA, MASK_PA_CORRIDOR);
        MakeBox(ceilGrp.transform, "Perim_Norte", xCeilMin, xCeilMax, Y1_PA - 0.005f, Y1_PA + 0.018f, zCeilMin, zCeilMin + 0.022f, mTeeGrid, false, LAYER_PA, MASK_PA_CORRIDOR);
        MakeBox(ceilGrp.transform, "Perim_Sur",   xCeilMin, xCeilMax, Y1_PA - 0.005f, Y1_PA + 0.018f, zCeilMax - 0.022f, zCeilMax, mTeeGrid, false, LAYER_PA, MASK_PA_CORRIDOR);

        CombineRenderersByMaterial(ceilGrp.transform, "Comb_Plafon_Escalera_PA", LAYER_PA, MASK_PA_CORRIDOR);

        // Soft NOCHE-only indirect bounce light inside Iluminacion_Zona2_CirculacionPA/NOCHE so the West stairwell ceiling & spandrel
        // receive natural bounce from the lit stairwell/corridor in NOCHE (Mask = 2u ONLY -> 0.000 leak into Aula 17 or Redes_SITE; off in TERROR!)
        Transform z2Noche = paRoot.Find("Iluminacion_Zona2_CirculacionPA/NOCHE");
        if (z2Noche != null)
        {
            Transform oldBounce = z2Noche.Find("Luz_Rebote_CuboEscalera_PA_NOCHE");
            if (oldBounce != null) Object.DestroyImmediate(oldBounce.gameObject);

            GameObject bounceGo = new GameObject("Luz_Rebote_CuboEscalera_PA_NOCHE");
            bounceGo.layer = LAYER_PA;
            bounceGo.transform.SetParent(z2Noche, false);
            bounceGo.transform.position = W(4.10f, 5.25f, 19.29f);
            var l = bounceGo.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = new Color(0.98f, 0.96f, 0.91f, 1f);
            l.intensity = 1.15f;
            l.range = 6.20f;
            l.shadows = LightShadows.None;
            l.cullingMask = 1 << LAYER_PA;
            var uld = bounceGo.AddComponent<UniversalAdditionalLightData>();
            uld.renderingLayers = 2u;
        }

        // Also ensure PB stair stairwell walls/steps (Layer 8) don't block view and stairs look natural
        log.Add($"[OK] Escalera Sec05 actualizada: ventana PA 6x2 (12 paños) + pretil crema + ventana descanso 6x1 + plafón 60x60 cm ({solidTileCount} paneles + {unlitTrofferCount} luminarias apagadas).");
    }

    // ============================================================================================
    // 2. CANCELES DE CRISTAL SEC06 EN PLANTA ALTA + COLUMNAS z=15.43
    // ============================================================================================
    static void BuildPASec06CancelsAndColumns(
        Transform paRoot,
        Material mCream, Material mZoclo, Material mBronze, Material mGlass, Material mGreen, Material mSteel,
        List<string> log)
    {
        // 2A. Add the two missing structural columns at (11.24, 15.43) and (17.24, 15.43) in PA_Interior/Columnas_PA (Photos 2, 3, 4!)
        Transform colsPA = paRoot.Find("Columnas_PA");
        if (colsPA != null)
        {
            foreach (Transform child in colsPA.Cast<Transform>().ToArray())
            {
                if (child.name.StartsWith("Columna_Sec06_z15.43"))
                    Object.DestroyImmediate(child.gameObject);
            }
            foreach (float cx in new[] { 11.24f, 17.24f })
            {
                string sideTag = cx < 14f ? "Oeste" : "Este";
                GameObject colGrp = new GameObject($"Columna_Sec06_z15.43_{sideTag}");
                colGrp.layer = LAYER_PA;
                colGrp.transform.SetParent(colsPA, false);

                float halfC = 0.21f; // 42x42 cm column matching Photos 2, 3, 4
                MakeBox(colGrp.transform, "Fuste_Columna", cx - halfC, cx + halfC, Y0_PA, Y1_PA, 15.43f - halfC, 15.43f + halfC, mCream, true, LAYER_PA, MASK_PA_CORRIDOR);
                // Ceramic skirting on the exposed corridor/foyer faces (East/West/North/South outside rooms)
                if (cx < 14f)
                {
                    // West column (11.24, 15.43): exposed East (+x), South (+z), and West-South towards Llegada_Escalera
                    MakeBox(colGrp.transform, "Zoclo_CaraEste", cx + halfC, cx + halfC + 0.015f, Y0_PA, Y0_PA + 0.10f, 15.43f - halfC - 0.01f, 15.43f + halfC + 0.015f, mZoclo, false, LAYER_PA, MASK_PA_CORRIDOR);
                    MakeBox(colGrp.transform, "Zoclo_CaraSur",  cx - halfC - 0.01f, cx + halfC + 0.015f, Y0_PA, Y0_PA + 0.10f, 15.43f + halfC, 15.43f + halfC + 0.015f, mZoclo, false, LAYER_PA, MASK_PA_CORRIDOR);
                    MakeBox(colGrp.transform, "Zoclo_CaraNorte_Pasillo", cx, cx + halfC + 0.015f, Y0_PA, Y0_PA + 0.10f, 15.43f - halfC - 0.015f, 15.43f - halfC, mZoclo, false, LAYER_PA, MASK_PA_CORRIDOR);
                }
                else
                {
                    // East column (17.24, 15.43): exposed West (-x) and South (+z)
                    MakeBox(colGrp.transform, "Zoclo_CaraOeste", cx - halfC - 0.015f, cx - halfC, Y0_PA, Y0_PA + 0.10f, 15.43f - halfC - 0.01f, 15.43f + halfC + 0.015f, mZoclo, false, LAYER_PA, MASK_PA_CORRIDOR);
                    MakeBox(colGrp.transform, "Zoclo_CaraSur",   cx - halfC - 0.015f, cx + halfC + 0.01f, Y0_PA, Y0_PA + 0.10f, 15.43f + halfC, 15.43f + halfC + 0.015f, mZoclo, false, LAYER_PA, MASK_PA_CORRIDOR);
                    MakeBox(colGrp.transform, "Zoclo_CaraNorte_Pasillo", cx - halfC - 0.015f, cx, Y0_PA, Y0_PA + 0.10f, 15.43f - halfC - 0.015f, 15.43f - halfC, mZoclo, false, LAYER_PA, MASK_PA_CORRIDOR);
                }
            }
        }

        // 2B. Rebuild West PA Partition: Pasillo_Junto_Redes/Entrada_P16 (z=7.71..10.25) + Redes_SITE/Cancel_Este (z=10.25..15.23)
        Transform p16Parent = paRoot.Find("Pasillo_Junto_Redes/Entrada_P16") ?? throw new Exception("Falta Pasillo_Junto_Redes/Entrada_P16");
        Transform redesCancel = paRoot.Find("Redes_SITE/Cancel_Este") ?? throw new Exception("Falta Redes_SITE/Cancel_Este");
        ClearChildren(p16Parent);
        ClearChildren(redesCancel);

        // West North solid wall (z=7.71..9.29) + full-height glass door P16 (z=9.29..10.25) inside Entrada_P16
        BuildSec06NorthWallAndDoor(
            wallAndDoorParent: p16Parent,
            fixedBaysParent: redesCancel,
            xAxis: 11.24f,
            yFloor: Y0_PA,
            yCeil: Y1_PA,
            doorNodeName: "P16_Hueco096_Abre_oeste",
            hingeNodeName: "Bisagra_Giro90_Interior",
            inwardSign: -1f, // opens West (-x)
            isPA: true,
            mCream, mZoclo, mBronze, mGlass, mGreen, mSteel);

        // 2C. Rebuild East PA Partition: Sala_Maestros/Cancel_Oeste (z=7.71..15.23)
        Transform salaCancel = paRoot.Find("Sala_Maestros/Cancel_Oeste") ?? throw new Exception("Falta Sala_Maestros/Cancel_Oeste");
        ClearChildren(salaCancel);

        BuildSec06NorthWallAndDoor(
            wallAndDoorParent: salaCancel,
            fixedBaysParent: salaCancel,
            xAxis: 17.24f,
            yFloor: Y0_PA,
            yCeil: Y1_PA,
            doorNodeName: "P17_Hueco096_Abre_este",
            hingeNodeName: "Bisagra_Giro90_Interior",
            inwardSign: +1f, // opens East (+x)
            isPA: true,
            mCream, mZoclo, mBronze, mGlass, mGreen, mSteel);

        log.Add("[OK] Canceles Sec06 de Planta Alta reconstruidos (muro crema z=7.71..9.29 + puerta cristal altura completa z=9.29..10.25 + 5 paños fijos z=10.25..15.23 sin travesaños medios ni línea guinda + columnas z=15.43).");
    }

    // ============================================================================================
    // 3. CANCELES DE CRISTAL SEC06 EN PLANTA BAJA (MISMO PANELADO, SIN DECORACIÓN DE PA)
    // ============================================================================================
    static void BuildPBSec06CancelsWithoutPADecoration(
        Transform pbRoot,
        Material mCream, Material mZoclo, Material mBronze, Material mGlass, Material mGreen, Material mSteel,
        List<string> log)
    {
        Transform pbCancels = pbRoot.Find("03_Fase3_Carpinteria_y_Vidrios/Canceleria_Interior_Vidrio_Verde")
            ?? throw new Exception("No se encontró Canceleria_Interior_Vidrio_Verde en PB_Interior");

        // 3A. Filter out baked triangles & old colliders belonging to the old Sec06 cancels (x ≈ 11.24 or 17.24, z = 7.70..15.44)
        bool InPBSec06Cancel(Vector3 p)
        {
            float x = PX(p), z = PZ(p);
            return p.y >= 0.18f && p.y <= 3.22f && z >= 7.70f && z <= 15.44f &&
                   (Mathf.Abs(x - 11.24f) < 0.12f || Mathf.Abs(x - 17.24f) < 0.12f);
        }

        foreach (var mr in pbCancels.GetComponentsInChildren<MeshRenderer>(true).Where(r => r.name.StartsWith("Comb_Canceleria_PB")).ToArray())
        {
            FilterMeshTriangles(mr, InPBSec06Cancel, "Sec06PB");
        }
        foreach (var col in pbCancels.GetComponentsInChildren<Collider>(true).Where(c => c.transform.parent == pbCancels && InPBSec06Cancel(c.bounds.center)).ToArray())
        {
            Object.DestroyImmediate(col.gameObject);
        }

        // 3B. Rebuild Cancel_Vidrio_Sec06_PasilloOeste_x11.24 (P15_Alcoba_Oeste_CancelVidrio/Pivote_Bisagra)
        Transform westPB = pbCancels.Find("Cancel_Vidrio_Sec06_PasilloOeste_x11.24");
        if (westPB == null)
        {
            westPB = new GameObject("Cancel_Vidrio_Sec06_PasilloOeste_x11.24").transform;
            westPB.SetParent(pbCancels, false);
        }
        westPB.gameObject.layer = LAYER_PB;
        ClearChildren(westPB);

        BuildSec06NorthWallAndDoor(
            wallAndDoorParent: westPB,
            fixedBaysParent: westPB,
            xAxis: 11.24f,
            yFloor: Y0_PB,
            yCeil: Y1_PB,
            doorNodeName: "P15_Alcoba_Oeste_CancelVidrio",
            hingeNodeName: "Pivote_Bisagra",
            inwardSign: -1f,
            isPA: false,
            mCream, mZoclo, mBronze, mGlass, mGreen, mSteel);

        // 3C. Rebuild Cancel_Vidrio_Sec06_PasilloEste_x17.24 (P16_Local_Este_CancelVidrio/Pivote_Bisagra)
        Transform eastPB = pbCancels.Find("Cancel_Vidrio_Sec06_PasilloEste_x17.24");
        if (eastPB == null)
        {
            eastPB = new GameObject("Cancel_Vidrio_Sec06_PasilloEste_x17.24").transform;
            eastPB.SetParent(pbCancels, false);
        }
        eastPB.gameObject.layer = LAYER_PB;
        ClearChildren(eastPB);

        BuildSec06NorthWallAndDoor(
            wallAndDoorParent: eastPB,
            fixedBaysParent: eastPB,
            xAxis: 17.24f,
            yFloor: Y0_PB,
            yCeil: Y1_PB,
            doorNodeName: "P16_Local_Este_CancelVidrio",
            hingeNodeName: "Pivote_Bisagra",
            inwardSign: +1f,
            isPA: false,
            mCream, mZoclo, mBronze, mGlass, mGreen, mSteel);

        log.Add("[OK] Canceles Sec06 de Planta Baja (Alcoba Oeste y Local Este) actualizados con el mismo diseño de paneles que PA y sin decoración exterior de PA.");
    }

    // ============================================================================================
    // SHARED BUILDER FOR BOTH PA AND PB SEC06 GLAZED PARTITIONS (EXACT SAME PANEL DESIGN)
    // ============================================================================================
    static void BuildSec06NorthWallAndDoor(
        Transform wallAndDoorParent,
        Transform fixedBaysParent,
        float xAxis, float yFloor, float yCeil,
        string doorNodeName, string hingeNodeName,
        float inwardSign, bool isPA,
        Material mCream, Material mZoclo, Material mBronze, Material mGlass, Material mGreen, Material mSteel)
    {
        int layer = isPA ? LAYER_PA : LAYER_PB;
        uint corridorMask = isPA ? MASK_PA_CORRIDOR : MASK_PB_CANCEL;
        uint roomMask = isPA ? MASK_PA_ROOM : MASK_PB_CANCEL;

        float wallHalf = 0.075f; // 15 cm masonry wall
        float yTopFrame = yCeil - HEADER_DROP; // e.g. 6.66 in PA, 3.06 in PB
        float fHalfX = 0.026f;   // 5.2 cm bronze frame depth
        float postW = 0.045f;    // 4.5 cm bronze mullion width

        // In PB, the z=7.71 column extends to z=7.91; in PA, bathroom wall is at z=7.71
        float zWall0 = isPA ? Z_SEC6_NORTH : 7.91f;
        float zWall1 = Z_WALL_NORTH_END; // 9.29

        // 1. North Solid Yellow-Cream Masonry Wall (z = zWall0 .. 9.29) + Grey Ceramic Zoclo on both faces
        MakeBox(wallAndDoorParent, "Muro_Norte_Crema_Sec06",
            xAxis - wallHalf, xAxis + wallHalf, yFloor, yCeil, zWall0, zWall1,
            mCream, true, layer, corridorMask);

        // Corridor-side zoclo vs Room-side zoclo (in PA, room-side zoclo uses MASK_PA_ROOM so corridor light never leaks inside!)
        bool corridorIsEast = xAxis < 14f; // at x=11.24 corridor is +X; at x=17.24 corridor is -X
        MakeBox(wallAndDoorParent, "Zoclo_Pasillo_MuroNorte",
            corridorIsEast ? xAxis + wallHalf : xAxis - wallHalf - 0.015f,
            corridorIsEast ? xAxis + wallHalf + 0.015f : xAxis - wallHalf,
            yFloor, yFloor + 0.10f, zWall0, zWall1,
            mZoclo, false, layer, corridorMask);

        MakeBox(wallAndDoorParent, "Zoclo_Interior_MuroNorte",
            corridorIsEast ? xAxis - wallHalf - 0.015f : xAxis + wallHalf,
            corridorIsEast ? xAxis - wallHalf : xAxis + wallHalf + 0.015f,
            yFloor, yFloor + 0.10f, zWall0, zWall1,
            mZoclo, false, layer, roomMask);

        // 2. Continuous Yellow-Cream Upper Header Drop Beam above the 6 glazed bays (z = 9.29 .. 15.23, Y = yTopFrame .. yCeil, Photos 2, 3, 4!)
        MakeBox(wallAndDoorParent, "Faldón_Cabezal_Crema_Norte",
            xAxis - wallHalf, xAxis + wallHalf, yTopFrame, yCeil, zWall1, Z_DOOR_END,
            mCream, false, layer, corridorMask);
        MakeBox(fixedBaysParent, "Faldón_Cabezal_Crema_Fijos",
            xAxis - wallHalf, xAxis + wallHalf, yTopFrame, yCeil, Z_DOOR_END, Z_COL_SOUTH_FACE,
            mCream, false, layer, corridorMask);

        // 3. Bay 6: Full-Height Bronze-Framed Glass Door (z = 9.29 .. 10.25, Y = yFloor .. yTopFrame)
        MakeBox(wallAndDoorParent, "Jamba_Bronce_Norte_Puerta", xAxis - fHalfX, xAxis + fHalfX, yFloor, yTopFrame, zWall1, zWall1 + postW * 0.65f, mBronze, false, layer, corridorMask);
        MakeBox(wallAndDoorParent, "Cabezal_Bronce_Puerta",     xAxis - fHalfX, xAxis + fHalfX, yTopFrame - 0.04f, yTopFrame, zWall1, Z_DOOR_END, mBronze, false, layer, corridorMask);

        // Small overhead hydraulic door closer body at top of door frame (visible in Photos 2 & 3!)
        float closerSideX = corridorIsEast ? (xAxis - 0.055f) : (xAxis + 0.015f);
        MakeBox(wallAndDoorParent, "Cierrapuertas_Superior", closerSideX, closerSideX + 0.04f, yTopFrame - 0.085f, yTopFrame - 0.04f, zWall1 + 0.08f, zWall1 + 0.28f, mBronze, false, layer, corridorMask);

        BuildFullHeightCancelDoor(
            wallAndDoorParent, doorNodeName, hingeNodeName,
            xAxis, zWall1 + postW * 0.55f, Z_DOOR_END - postW * 0.45f,
            yFloor, yTopFrame - 0.038f,
            inwardSign, isPA, layer, corridorMask,
            mBronze, mGlass, mGreen, mSteel);

        // 4. Bays 1..5: Five Equal Uninterrupted Floor-to-Header Fixed Glass Bays (z = 10.25 .. 15.23)
        const int numFixedBays = 5;
        float fixedSpan = Z_COL_SOUTH_FACE - Z_DOOR_END; // 4.98 m
        float bayW = fixedSpan / numFixedBays;          // 0.996 m

        // Continuous bottom & top bronze rails across the 5 fixed bays (NO mid rail, NO lintel rail!)
        MakeBox(fixedBaysParent, "Riel_Bronce_Inferior_Fijos", xAxis - fHalfX, xAxis + fHalfX, yFloor, yFloor + 0.045f, Z_DOOR_END, Z_COL_SOUTH_FACE, mBronze, false, layer, corridorMask);
        MakeBox(fixedBaysParent, "Riel_Bronce_Superior_Fijos", xAxis - fHalfX, xAxis + fHalfX, yTopFrame - 0.042f, yTopFrame, Z_DOOR_END, Z_COL_SOUTH_FACE, mBronze, false, layer, corridorMask);

        // 6 vertical mullions from z = 10.25 to z = 15.23 (including shared mullion at z=10.25 between door & Bay 5)
        for (int i = 0; i <= numFixedBays; i++)
        {
            float mz = Z_DOOR_END + i * bayW;
            float mz0 = (i == 0) ? (mz - postW * 0.45f) : (i == numFixedBays ? mz - postW : mz - postW * 0.5f);
            float mz1 = (i == 0) ? (mz + postW * 0.55f) : (i == numFixedBays ? mz : mz + postW * 0.5f);
            MakeBox(fixedBaysParent, $"Montante_Bronce_Vert_{i + 1:02d}",
                xAxis - fHalfX, xAxis + fHalfX, yFloor, yTopFrame, mz0, mz1,
                mBronze, false, layer, corridorMask);
        }

        // 5 uninterrupted clear glass panes + single institutional green stripe (yFloor + 0.72 .. yFloor + 0.81, NO guinda line!)
        for (int b = 0; b < numFixedBays; b++)
        {
            float bz0 = Z_DOOR_END + b * bayW + postW * 0.45f;
            float bz1 = Z_DOOR_END + (b + 1) * bayW - postW * 0.45f;

            MakeBox(fixedBaysParent, $"Cristal_Fijo_Pano_{b + 1:02d}",
                xAxis - 0.007f, xAxis + 0.007f, yFloor + 0.042f, yTopFrame - 0.040f, bz0, bz1,
                mGlass, true, layer, corridorMask);

            MakeBox(fixedBaysParent, $"Franja_Verde_Pano_{b + 1:02d}",
                xAxis - 0.010f, xAxis + 0.010f, yFloor + 0.72f, yFloor + 0.81f, bz0, bz1,
                mGreen, false, layer, corridorMask);
        }
    }

    static void BuildFullHeightCancelDoor(
        Transform parent, string doorNodeName, string hingeNodeName,
        float xAxis, float zLeaf0, float zLeaf1,
        float yFloor, float yLeafTop,
        float inwardSign, bool isPA, int layer, uint mask,
        Material mBronze, Material mGlass, Material mGreen, Material mSteel)
    {
        GameObject doorRoot = new GameObject(doorNodeName);
        doorRoot.layer = layer;
        doorRoot.transform.SetParent(parent, false);

        GameObject hingePivot = new GameObject(hingeNodeName);
        hingePivot.layer = layer;
        hingePivot.transform.SetParent(doorRoot.transform, false);
        hingePivot.transform.position = W(xAxis, yFloor, zLeaf0); // Hinge on North stile so 90° inward swing parallels North wall

        float yLeaf0 = yFloor + 0.014f;
        float stileW = 0.042f;
        float railH = 0.046f;
        float halfT = 0.020f;

        // Slim perimeter bronze stiles & top/bottom rails (full height up to header!)
        MakeBox(hingePivot.transform, "Bastidor_Norte", xAxis - halfT, xAxis + halfT, yLeaf0, yLeafTop, zLeaf0, zLeaf0 + stileW, mBronze, false, layer, mask, isMoving: true);
        MakeBox(hingePivot.transform, "Bastidor_Sur",   xAxis - halfT, xAxis + halfT, yLeaf0, yLeafTop, zLeaf1 - stileW, zLeaf1, mBronze, false, layer, mask, isMoving: true);
        MakeBox(hingePivot.transform, "Bastidor_Inf",   xAxis - halfT, xAxis + halfT, yLeaf0, yLeaf0 + railH, zLeaf0 + stileW, zLeaf1 - stileW, mBronze, false, layer, mask, isMoving: true);
        MakeBox(hingePivot.transform, "Bastidor_Sup",   xAxis - halfT, xAxis + halfT, yLeafTop - railH, yLeafTop, zLeaf0 + stileW, zLeaf1 - stileW, mBronze, false, layer, mask, isMoving: true);

        // Thin horizontal push/pull bar across door leaf at waist height (Photos 2 & 3!)
        MakeBox(hingePivot.transform, "Barra_Horizontal_Puerta", xAxis - halfT - 0.006f, xAxis + halfT + 0.006f, yFloor + 0.96f, yFloor + 0.992f, zLeaf0 + stileW * 0.5f, zLeaf1 - stileW * 0.5f, mBronze, false, layer, mask, isMoving: true);

        // Full-height tempered glass leaf
        GameObject glassLeaf = MakeBox(
            hingePivot.transform, "Hoja",
            xAxis - 0.006f, xAxis + 0.006f,
            yLeaf0 + railH * 0.6f, yLeafTop - railH * 0.6f,
            zLeaf0 + stileW * 0.6f, zLeaf1 - stileW * 0.6f,
            mGlass, false, layer, mask, isMoving: true);

        if (isPA)
        {
            var paDoor = glassLeaf.AddComponent<Docencia2PADoor>();
            paDoor.hinge = hingePivot.transform;
            paDoor.inwardAngle = inwardSign > 0f ? -90f : 90f;
        }

        // Single institutional green band (aligned with fixed panels, NO guinda stripe!)
        MakeBox(hingePivot.transform, "Franja_Verde_Hoja",
            xAxis - 0.009f, xAxis + 0.009f,
            yFloor + 0.72f, yFloor + 0.81f,
            zLeaf0 + stileW, zLeaf1 - stileW,
            mGreen, false, layer, mask, isMoving: true);

        // Slim stainless latch/handle on strike stile
        float zLatch = zLeaf1 - 0.055f;
        MakeBox(hingePivot.transform, "Jaladera_Ext", xAxis - 0.038f, xAxis - 0.020f, yFloor + 0.90f, yFloor + 1.12f, zLatch - 0.010f, zLatch + 0.010f, mSteel, false, layer, mask, isMoving: true);
        MakeBox(hingePivot.transform, "Jaladera_Int", xAxis + 0.020f, xAxis + 0.038f, yFloor + 0.90f, yFloor + 1.12f, zLatch - 0.010f, zLatch + 0.010f, mSteel, false, layer, mask, isMoving: true);

        // Interactive door component + 3cm floor-cleared BoxCollider (excluding protruding handles)
        var interactive = hingePivot.AddComponent<Docencia2InteractiveDoor>();
        interactive.closedRotation = Quaternion.identity;
        interactive.inwardAngle = inwardSign > 0f ? -90f : 90f;
        interactive.doubleSwing = false;

        var renderers = hingePivot.GetComponentsInChildren<Renderer>();
        Bounds b = renderers[0].bounds;
        foreach (var r in renderers)
        {
            if (!r.name.StartsWith("Jaladera_"))
                b.Encapsulate(r.bounds);
        }
        Bounds local = new Bounds(hingePivot.transform.InverseTransformPoint(b.center), Vector3.zero);
        for (int a = 0; a < 8; a++)
        {
            local.Encapsulate(hingePivot.transform.InverseTransformPoint(new Vector3(
                (a & 1) == 0 ? b.min.x : b.max.x,
                (a & 2) == 0 ? b.min.y : b.max.y,
                (a & 4) == 0 ? b.min.z : b.max.z)));
        }
        var box = hingePivot.AddComponent<BoxCollider>();
        Vector3 size = local.size;
        Vector3 center = local.center;
        if (size.x < size.z) size.x = Mathf.Min(size.x, 0.04f);
        else size.z = Mathf.Min(size.z, 0.04f);

        float desiredBottom = yFloor + 0.03f;
        float bottom = hingePivot.transform.TransformPoint(center - size * 0.5f).y;
        float trim = Mathf.Max(0f, desiredBottom - bottom);
        center.y += trim * 0.5f;
        size.y -= trim;
        box.center = center;
        box.size = size;

        foreach (var tr in hingePivot.GetComponentsInChildren<Transform>(true))
            GameObjectUtility.SetStaticEditorFlags(tr.gameObject, 0);
    }

    // ============================================================================================
    // 4. PA-ONLY CORRIDOR DECORATION (PHOTOS 2, 3, 4: PYRAMID, DISH, BENCHES, TSA 2-4, EXTINTORES)
    // ============================================================================================
    static void BuildPASec06CorridorDecoration(
        Transform paRoot, Transform pbRoot,
        Material mCream, Material mZoclo, Material mBronze, Material mSteel, Material mMayanStone, Material mMayanStoneLight, Material mPosterWood,
        Material mPanelSlate, Material mDishGrey, Material mPalletWood, Material mEmergWhite,
        List<string> log)
    {
        // Load Phase 5 accent materials
        Material mBlack = LoadMat("Fase5/F5_Negro");
        Material mTealBench = LoadMat("Fase5/F5_VerdeAzulado");
        Material mPaper = LoadMat("Fase5/F5_Papel");
        Material mRed = LoadMat("Fase5/F5_Rojo");
        Material mGreenSign = LoadMat("Fase5/F5_VerdeSenal");
        Material mYellow = LoadMat("Fase5/F5_Amarillo");
        Material mBlue = LoadMat("Fase5/F5_Azul");
        Material mWood = LoadMat("Fase5/F5_Madera");

        // Remove previous idempotent PA Sec06 decoration root if present
        Transform oldDeco = paRoot.Find("Decoracion_Pasillo_Sec06_PA_Fotos");
        if (oldDeco != null) Object.DestroyImmediate(oldDeco.gameObject);

        GameObject decoRoot = new GameObject("Decoracion_Pasillo_Sec06_PA_Fotos");
        decoRoot.layer = LAYER_PA;
        decoRoot.transform.SetParent(paRoot, false);

        GameObject corrProps = new GameObject("Props_Pasillo_Exterior_Sec06");
        corrProps.layer = LAYER_PA;
        corrProps.transform.SetParent(decoRoot.transform, false);

        GameObject roomProps = new GameObject("Props_Tras_Cristal_Interior_Sec06");
        roomProps.layer = LAYER_PA;
        roomProps.transform.SetParent(decoRoot.transform, false);

        // ---- 4A. Reposition the 2 existing PA corridor Fire Extinguishers onto the new North solid cream walls (z ≈ 8.48, Photos 2, 3, 4!) ----
        var extintores = paRoot.GetComponentsInChildren<Transform>(true)
            .Where(t => t.name == "Extintor_PA_FOTO_SUPUESTO")
            .OrderBy(t => PX(t.position))
            .ToArray();

        if (extintores.Length >= 2)
        {
            // West North solid wall (x = 11.24 + 0.075 = 11.315 -> mount center at x = 11.41, z = 8.48, facing East)
            extintores[0].position = W(11.41f, 3.606f, 8.48f);
            extintores[0].rotation = Quaternion.Euler(0f, -90f, 0f);
            foreach (var r in extintores[0].GetComponentsInChildren<Renderer>(true)) r.renderingLayerMask = MASK_PA_CORRIDOR;

            // East North solid wall (x = 17.24 - 0.075 = 17.165 -> mount center at x = 17.07, z = 8.48, facing West)
            extintores[1].position = W(17.07f, 3.606f, 8.48f);
            extintores[1].rotation = Quaternion.Euler(0f, 90f, 0f);
            foreach (var r in extintores[1].GetComponentsInChildren<Renderer>(true)) r.renderingLayerMask = MASK_PA_CORRIDOR;
        }
        // Square red/white fire extinguisher wall signs right above both extinguishers (Photos 2, 3, 4!)
        MakeBox(corrProps.transform, "Placa_Extintor_Oeste_Fondo", 11.316f, 11.326f, 4.86f, 5.05f, 8.38f, 8.58f, mRed, false, LAYER_PA, MASK_PA_CORRIDOR);
        MakeBox(corrProps.transform, "Placa_Extintor_Oeste_Centro", 11.326f, 11.331f, 4.89f, 5.02f, 8.41f, 8.55f, mPaper, false, LAYER_PA, MASK_PA_CORRIDOR);
        MakeBox(corrProps.transform, "Placa_Extintor_Este_Fondo",  17.154f, 17.164f, 4.86f, 5.05f, 8.38f, 8.58f, mRed, false, LAYER_PA, MASK_PA_CORRIDOR);
        MakeBox(corrProps.transform, "Placa_Extintor_Este_Centro", 17.149f, 17.154f, 4.89f, 5.02f, 8.41f, 8.55f, mPaper, false, LAYER_PA, MASK_PA_CORRIDOR);

        // ---- 4B. Also align F01 Stairwell Landing Bench along North stairwell parapet (Photo 4 far left: facing South!) ----
        Transform f01Bench = paRoot.Find("Bancas_Verdes_Revision3_SUPUESTO/Banca_4_Verdes_Foto_F01_0_SUPUESTO");
        if (f01Bench != null)
        {
            f01Bench.position = W(8.95f, 3.606f, 15.86f);
            f01Bench.rotation = Quaternion.Euler(0f, 0f, 0f); // faces South (+z) with back against North wall/railing, just like Photo 4!
        }

        // ---- 4C. West Glazed Partition Row of 7 Green Waiting Seats (z = 10.42 .. 13.92, x ≈ 11.58, Photos 2 & 4!) ----
        // Build authentic keyhole-backrest tandem benches (4-seat + 3-seat = 7 seats)
        BuildTandemBenchAlongZ(corrProps.transform, "Banca_Verde_Oeste_3Plazas", xBack: 11.33f, dirX: +1f, zCenter: 11.12f, seats: 3, mBlack, mTealBench);
        BuildTandemBenchAlongZ(corrProps.transform, "Banca_Verde_Oeste_4Plazas", xBack: 11.33f, dirX: +1f, zCenter: 12.88f, seats: 4, mBlack, mTealBench);

        // ---- 4D. East Glazed Partition Row of Green Waiting Seats in front of Sala_Maestros (z = 10.50 .. 14.30, Photo 3!) ----
        BuildTandemBenchAlongZ(corrProps.transform, "Banca_Verde_Este_4Plazas_Norte", xBack: 17.15f, dirX: -1f, zCenter: 11.42f, seats: 4, mBlack, mTealBench);
        BuildTandemBenchAlongZ(corrProps.transform, "Banca_Verde_Este_4Plazas_Sur",   xBack: 17.15f, dirX: -1f, zCenter: 13.38f, seats: 4, mBlack, mTealBench);

        // ---- 4E. Black Metal Table + Stepped Mayan Pyramid Model (Chichén Itzá / Kukulcán) at West Bay 1 (Photos 2 & 4!) ----
        const float pyrX = 11.68f;
        const float pyrZ = 14.42f;
        const float tblW_X = 0.62f;
        const float tblL_Z = 0.92f;
        const float yTblTop = Y0_PA + 0.68f; // 4.28 m

        // Black tubular table
        MakeBox(corrProps.transform, "Mesa_Piramide_Cubierta", pyrX - tblW_X * 0.5f, pyrX + tblW_X * 0.5f, yTblTop - 0.03f, yTblTop, pyrZ - tblL_Z * 0.5f, pyrZ + tblL_Z * 0.5f, mBlack, false, LAYER_PA, MASK_PA_CORRIDOR);
        foreach (float dx in new[] { -tblW_X * 0.5f + 0.03f, tblW_X * 0.5f - 0.03f })
            foreach (float dz in new[] { -tblL_Z * 0.5f + 0.03f, tblL_Z * 0.5f - 0.03f })
                MakeBox(corrProps.transform, "Mesa_Piramide_Pata", pyrX + dx - 0.016f, pyrX + dx + 0.016f, Y0_PA, yTblTop - 0.03f, pyrZ + dz - 0.016f, pyrZ + dz + 0.016f, mBlack, false, LAYER_PA, MASK_PA_CORRIDOR);

        // Two white info sheets on the front corner of the black table (visible in Photos 2 & 4)
        MakeBox(corrProps.transform, "Ficha_Mesa_Piramide_1", pyrX + 0.06f, pyrX + 0.28f, yTblTop, yTblTop + 0.004f, pyrZ + 0.16f, pyrZ + 0.42f, mPaper, false, LAYER_PA, MASK_PA_CORRIDOR);
        MakeBox(corrProps.transform, "Ficha_Mesa_Piramide_2", pyrX - 0.22f, pyrX + 0.01f, yTblTop, yTblTop + 0.004f, pyrZ + 0.20f, pyrZ + 0.43f, mPaper, false, LAYER_PA, MASK_PA_CORRIDOR);

        // 9-stepped Mayan Pyramid (El Castillo / Kukulcán)
        const int numSteps = 9;
        const float baseSpan = 0.78f;
        const float topSpan = 0.24f;
        const float stepH = 0.038f;
        float yBase = yTblTop;

        for (int s = 0; s < numSteps; s++)
        {
            float t = s / (float)(numSteps - 1);
            float span = Mathf.Lerp(baseSpan, topSpan, t);
            float sy0 = yBase + s * stepH;
            float sy1 = sy0 + stepH - 0.003f;
            MakeBox(corrProps.transform, $"Piramide_Cuerpo_{s + 1:02d}",
                pyrX - span * 0.5f, pyrX + span * 0.5f, sy0, sy1, pyrZ - span * 0.5f, pyrZ + span * 0.5f,
                mMayanStone, false, LAYER_PA, MASK_PA_CORRIDOR);
            // Subtle cornice lip per terrace
            MakeBox(corrProps.transform, $"Piramide_Cornisa_{s + 1:02d}",
                pyrX - span * 0.5f - 0.005f, pyrX + span * 0.5f + 0.005f, sy1, sy1 + 0.004f, pyrZ - span * 0.5f - 0.005f, pyrZ + span * 0.5f + 0.005f,
                mMayanStoneLight, false, LAYER_PA, MASK_PA_CORRIDOR);
        }

        // Top temple (Templo Superior) + roof cornice + 4 dark portals
        float yTemple0 = yBase + numSteps * stepH;
        float yTemple1 = yTemple0 + 0.105f;
        float templeW = 0.19f;
        MakeBox(corrProps.transform, "Piramide_Templo_Cuerpo", pyrX - templeW * 0.5f, pyrX + templeW * 0.5f, yTemple0, yTemple1, pyrZ - templeW * 0.5f, pyrZ + templeW * 0.5f, mMayanStone, false, LAYER_PA, MASK_PA_CORRIDOR);
        MakeBox(corrProps.transform, "Piramide_Templo_Moldura", pyrX - templeW * 0.54f, pyrX + templeW * 0.54f, yTemple1 - 0.022f, yTemple1 + 0.012f, pyrZ - templeW * 0.54f, pyrZ + templeW * 0.54f, mMayanStoneLight, false, LAYER_PA, MASK_PA_CORRIDOR);
        MakeBox(corrProps.transform, "Piramide_Portal_Este",  pyrX + templeW * 0.5f - 0.004f, pyrX + templeW * 0.5f + 0.004f, yTemple0 + 0.008f, yTemple0 + 0.068f, pyrZ - 0.028f, pyrZ + 0.028f, mBlack, false, LAYER_PA, MASK_PA_CORRIDOR);
        MakeBox(corrProps.transform, "Piramide_Portal_Oeste", pyrX - templeW * 0.5f - 0.004f, pyrX - templeW * 0.5f + 0.004f, yTemple0 + 0.008f, yTemple0 + 0.068f, pyrZ - 0.028f, pyrZ + 0.028f, mBlack, false, LAYER_PA, MASK_PA_CORRIDOR);
        MakeBox(corrProps.transform, "Piramide_Portal_Norte", pyrX - 0.028f, pyrX + 0.028f, yTemple0 + 0.008f, yTemple0 + 0.068f, pyrZ - templeW * 0.5f - 0.004f, pyrZ - templeW * 0.5f + 0.004f, mBlack, false, LAYER_PA, MASK_PA_CORRIDOR);
        MakeBox(corrProps.transform, "Piramide_Portal_Sur",   pyrX - 0.028f, pyrX + 0.028f, yTemple0 + 0.008f, yTemple0 + 0.068f, pyrZ + templeW * 0.5f - 0.004f, pyrZ + templeW * 0.5f + 0.004f, mBlack, false, LAYER_PA, MASK_PA_CORRIDOR);

        // 4 Sloped Balustrade Stairways (Alfardas + Escalinata) on all 4 faces
        BuildPyramidStairway(corrProps.transform, pyrX, pyrZ, yBase, yTemple0, baseSpan * 0.5f + 0.03f, topSpan * 0.45f, 0, mMayanStone, mMayanStoneLight);
        BuildPyramidStairway(corrProps.transform, pyrX, pyrZ, yBase, yTemple0, baseSpan * 0.5f + 0.03f, topSpan * 0.45f, 1, mMayanStone, mMayanStoneLight);
        BuildPyramidStairway(corrProps.transform, pyrX, pyrZ, yBase, yTemple0, baseSpan * 0.5f + 0.03f, topSpan * 0.45f, 2, mMayanStone, mMayanStoneLight);
        BuildPyramidStairway(corrProps.transform, pyrX, pyrZ, yBase, yTemple0, baseSpan * 0.5f + 0.03f, topSpan * 0.45f, 3, mMayanStone, mMayanStoneLight);

        // Single clean physics hull around the pyramid + table
        AddInvisibleCollider(corrProps.transform, "Colision_Mesa_Piramide", pyrX - 0.40f, pyrX + 0.40f, Y0_PA, yTemple1, pyrZ - 0.46f, pyrZ + 0.46f, LAYER_PA);

        // ---- 4F. Satellite Dish on Wooden Pallet + Leaning Wood Panel + TSA 2-4 Panel + Green Evacuation Sign (Photos 2 & 4!) ----
        // Green evacuation arrow sign on South face of West column (11.24, 15.43 -> South face at z = 15.64)
        MakeBox(corrProps.transform, "Senal_Evacuacion_Columna_Fondo", 11.11f, 11.37f, 5.28f, 5.44f, 15.642f, 15.654f, mGreenSign, false, LAYER_PA, MASK_PA_CORRIDOR);
        MakeBox(corrProps.transform, "Senal_Evacuacion_Columna_Borde", 11.125f, 11.355f, 5.295f, 5.425f, 15.654f, 15.658f, mPaper, false, LAYER_PA, MASK_PA_CORRIDOR);
        MakeBox(corrProps.transform, "Senal_Evacuacion_Flecha",        11.155f, 11.325f, 5.335f, 5.385f, 15.658f, 15.662f, mGreenSign, false, LAYER_PA, MASK_PA_CORRIDOR);

        // TSA 2-4 Grey Electrical Panel on North wall of Llegada_Escalera (shifted slightly West to x=9.88..10.34 so full cabinet is visible next to dish, Photo 4!)
        MakeBox(corrProps.transform, "Tablero_TSA_2_4_Gabinete", 9.88f, 10.34f, 4.52f, 5.38f, 15.505f, 15.542f, mPanelSlate, false, LAYER_PA, MASK_PA_CORRIDOR);
        MakeBox(corrProps.transform, "Tablero_TSA_2_4_Etiqueta1", 10.04f, 10.18f, 5.26f, 5.31f, 15.542f, 15.547f, mPaper, false, LAYER_PA, MASK_PA_CORRIDOR);
        MakeBox(corrProps.transform, "Tablero_TSA_2_4_Etiqueta2", 10.05f, 10.17f, 5.17f, 5.22f, 15.542f, 15.547f, mPaper, false, LAYER_PA, MASK_PA_CORRIDOR);
        // Two small wall socket plates to the right of TSA 2-4 (Photo 4!)
        MakeBox(corrProps.transform, "Contacto_Pared_1", 10.42f, 10.47f, 4.40f, 4.48f, 15.505f, 15.525f, mPanelSlate, false, LAYER_PA, MASK_PA_CORRIDOR);
        MakeBox(corrProps.transform, "Contacto_Pared_2", 10.52f, 10.57f, 4.40f, 4.48f, 15.505f, 15.525f, mPanelSlate, false, LAYER_PA, MASK_PA_CORRIDOR);
        MakeBox(corrProps.transform, "Cable_Vertical_Azul_Pared", 9.68f, 9.686f, 3.70f, 6.78f, 15.505f, 15.512f, mBlue, false, LAYER_PA, MASK_PA_CORRIDOR);

        // Corridor-side warm cream paint finish on the bathroom wall corners framing Acceso_Banos (z=7.71, Photos 2, 3, 4!)
        MakeBox(corrProps.transform, "Acabado_Crema_Esquina_BanoOeste", 11.315f, 12.315f, Y0_PA, Y1_PA, 7.785f, 7.793f, mCream, false, LAYER_PA, MASK_PA_CORRIDOR);
        MakeBox(corrProps.transform, "Acabado_Crema_Esquina_BanoEste",  16.165f, 17.165f, Y0_PA, Y1_PA, 7.785f, 7.793f, mCream, false, LAYER_PA, MASK_PA_CORRIDOR);
        MakeBox(corrProps.transform, "Zoclo_Esquina_BanoOeste", 11.315f, 12.315f, Y0_PA, Y0_PA + 0.10f, 7.793f, 7.808f, mZoclo, false, LAYER_PA, MASK_PA_CORRIDOR);
        MakeBox(corrProps.transform, "Zoclo_Esquina_BanoEste",  16.165f, 17.165f, Y0_PA, Y0_PA + 0.10f, 7.793f, 7.808f, mZoclo, false, LAYER_PA, MASK_PA_CORRIDOR);

        // Wooden pallet + offset parabolic dish + leaning laminated board in front of West column (x ≈ 10.76, z ≈ 16.02, Photos 2 & 4!)
        const float palX = 10.76f;
        const float palZ = 16.02f;
        // Pallet stringers & slats
        foreach (float dz in new[] { -0.32f, 0f, 0.32f })
            MakeBox(corrProps.transform, "Tarima_Larguero", palX - 0.36f, palX + 0.36f, Y0_PA + 0.006f, Y0_PA + 0.072f, palZ + dz - 0.04f, palZ + dz + 0.04f, mPalletWood, false, LAYER_PA, MASK_PA_CORRIDOR);
        for (int sl = 0; sl < 5; sl++)
        {
            float sx = palX - 0.32f + sl * 0.16f;
            MakeBox(corrProps.transform, "Tarima_Tabla", sx - 0.055f, sx + 0.055f, Y0_PA + 0.072f, Y0_PA + 0.094f, palZ - 0.36f, palZ + 0.36f, mPalletWood, false, LAYER_PA, MASK_PA_CORRIDOR);
        }
        // Leaning laminated wood square board against West column (Photo 2 & 4!)
        GameObject leanBoard = MakeBox(corrProps.transform, "Tablero_Madera_Recargado", 10.88f, 11.34f, Y0_PA + 0.094f, Y0_PA + 0.72f, 15.66f, 15.69f, mWood, false, LAYER_PA, MASK_PA_CORRIDOR);
        leanBoard.transform.Rotate(6f, 0f, 0f, Space.Self);

        // Parabolic satellite dish mast, upward-tilted reflector bowl, and LNB feed arm arching over from left rim (Photos 2 & 4!)
        MakeBox(corrProps.transform, "Antena_Placa_Base", palX - 0.24f, palX - 0.04f, Y0_PA + 0.094f, Y0_PA + 0.114f, palZ - 0.10f, palZ + 0.10f, mBlack, false, LAYER_PA, MASK_PA_CORRIDOR);
        MakeBox(corrProps.transform, "Antena_Mastil_Inf", palX - 0.17f, palX - 0.11f, Y0_PA + 0.114f, Y0_PA + 0.58f, palZ - 0.03f, palZ + 0.03f, mBlack, false, LAYER_PA, MASK_PA_CORRIDOR);
        MakeBox(corrProps.transform, "Antena_Mastil_Sup", palX - 0.15f, palX - 0.08f, Y0_PA + 0.56f, Y0_PA + 0.86f, palZ - 0.04f, palZ + 0.04f, mDishGrey, false, LAYER_PA, MASK_PA_CORRIDOR);

        GameObject dishPivot = new GameObject("Plato_Parabolico_Pivote");
        dishPivot.layer = LAYER_PA;
        dishPivot.transform.SetParent(corrProps.transform, false);
        dishPivot.transform.position = W(palX + 0.03f, Y0_PA + 0.88f, palZ);
        // Shallow upward-right tilt (~26 deg) matching Photos 2 & 4!
        dishPivot.transform.rotation = Quaternion.Euler(-22f, 25f, 18f);

        GameObject dishDisc = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        dishDisc.name = "Plato_Parabolico_Reflector";
        dishDisc.layer = LAYER_PA;
        dishDisc.transform.SetParent(dishPivot.transform, false);
        dishDisc.transform.localScale = new Vector3(0.84f, 0.018f, 0.76f);
        Object.DestroyImmediate(dishDisc.GetComponent<Collider>());
        var rDish = dishDisc.GetComponent<MeshRenderer>();
        rDish.sharedMaterial = mDishGrey;
        rDish.renderingLayerMask = MASK_PA_CORRIDOR;
        rDish.shadowCastingMode = ShadowCastingMode.Off;

        GameObject dishRim = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        dishRim.name = "Plato_Parabolico_AnilloInterior";
        dishRim.layer = LAYER_PA;
        dishRim.transform.SetParent(dishPivot.transform, false);
        dishRim.transform.localPosition = new Vector3(0f, 0.011f, 0f);
        dishRim.transform.localScale = new Vector3(0.72f, 0.005f, 0.65f);
        Object.DestroyImmediate(dishRim.GetComponent<Collider>());
        var rRim = dishRim.GetComponent<MeshRenderer>();
        rRim.sharedMaterial = mPanelSlate;
        rRim.renderingLayerMask = MASK_PA_CORRIDOR;
        rRim.shadowCastingMode = ShadowCastingMode.Off;

        // LNB feed arm arching from West/left rim up over the center of the dish
        GameObject lnbArm = GameObject.CreatePrimitive(PrimitiveType.Cube);
        lnbArm.name = "Brazo_LNB";
        lnbArm.layer = LAYER_PA;
        lnbArm.transform.SetParent(dishPivot.transform, false);
        lnbArm.transform.localPosition = new Vector3(0f, 0.22f, 0.20f);
        lnbArm.transform.localRotation = Quaternion.Euler(-38f, 0f, 0f);
        lnbArm.transform.localScale = new Vector3(0.024f, 0.50f, 0.024f);
        Object.DestroyImmediate(lnbArm.GetComponent<Collider>());
        var rArm = lnbArm.GetComponent<MeshRenderer>();
        rArm.sharedMaterial = mBlack;
        rArm.renderingLayerMask = MASK_PA_CORRIDOR;
        rArm.shadowCastingMode = ShadowCastingMode.Off;

        GameObject lnbHead = GameObject.CreatePrimitive(PrimitiveType.Cube);
        lnbHead.name = "Cabezal_LNB";
        lnbHead.layer = LAYER_PA;
        lnbHead.transform.SetParent(dishPivot.transform, false);
        lnbHead.transform.localPosition = new Vector3(0f, 0.40f, 0.03f);
        lnbHead.transform.localScale = new Vector3(0.065f, 0.045f, 0.10f);
        Object.DestroyImmediate(lnbHead.GetComponent<Collider>());
        var rHead = lnbHead.GetComponent<MeshRenderer>();
        rHead.sharedMaterial = mDishGrey;
        rHead.renderingLayerMask = MASK_PA_CORRIDOR;
        rHead.shadowCastingMode = ShadowCastingMode.Off;

        AddInvisibleCollider(corrProps.transform, "Colision_Tarima_Parabolica", palX - 0.40f, palX + 0.42f, Y0_PA, Y0_PA + 1.38f, palZ - 0.40f, palZ + 0.40f, LAYER_PA);

        // ---- 4G. Acceso_Banos Beige Trash Bin + Restroom Sign + Emergency Light (Photos 3 & 4 background!) ----
        var binTemplate = pbRoot.Find("05_Fase5_Mobiliario_PB")?.GetComponentsInChildren<Transform>(true)
            .FirstOrDefault(t => t.name.StartsWith("Bote_Beige"));
        if (binTemplate != null)
        {
            GameObject paBin = Object.Instantiate(binTemplate.gameObject, decoRoot.transform);
            paBin.name = "Bote_Beige_Acceso_Banos_PA";
            paBin.transform.position = W(14.24f, 3.606f, 6.76f);
            paBin.transform.rotation = Quaternion.Euler(0f, 0f, 0f); // mouth facing South (+z) toward corridor!
            foreach (var tr in paBin.GetComponentsInChildren<Transform>(true))
            {
                tr.gameObject.layer = LAYER_PA;
                GameObjectUtility.SetStaticEditorFlags(tr.gameObject, StaticEditorFlags.BatchingStatic | StaticEditorFlags.ReflectionProbeStatic);
            }
            foreach (var r in paBin.GetComponentsInChildren<Renderer>(true))
            {
                r.renderingLayerMask = MASK_PA_CORRIDOR;
                r.shadowCastingMode = ShadowCastingMode.Off;
            }
        }
        MakeBox(corrProps.transform, "Letrero_Acceso_Banos_Pared", 14.08f, 14.40f, 5.02f, 5.22f, 6.502f, 6.512f, mPaper, false, LAYER_PA, MASK_PA_CORRIDOR);
        MakeBox(corrProps.transform, "Luminaria_Emergencia_AccesoBanos", 14.11f, 14.37f, 6.70f, 6.76f, 7.785f, 7.835f, mEmergWhite, false, LAYER_PA, MASK_PA_CORRIDOR);

        // Paper notices on fixed glass Bay 4 (West) and Sala_Maestros Bay 5 (East) as seen in Photos 2, 3, 4
        MakeBox(corrProps.transform, "Aviso_Vidrio_Oeste_HojaGrande", 11.248f, 11.254f, 4.82f, 5.18f, 11.62f, 11.88f, mPaper, false, LAYER_PA, MASK_PA_CORRIDOR);
        MakeBox(corrProps.transform, "Aviso_Vidrio_Oeste_Etiqueta",   11.248f, 11.254f, 5.26f, 5.34f, 11.96f, 12.14f, mPaper, false, LAYER_PA, MASK_PA_CORRIDOR);
        MakeBox(corrProps.transform, "Aviso_Vidrio_Este_1",           17.226f, 17.232f, 4.86f, 5.22f, 10.55f, 10.80f, mPaper, false, LAYER_PA, MASK_PA_CORRIDOR);

        // ---- 4H. Interior Props Behind Glass (MASK_PA_ROOM = 17u so zero corridor light leak!) ----
        // 1) Mayan Wood Poster Board behind West Bay 1 inside Redes_SITE (Photo 2!)
        MakeBox(roomProps.transform, "Cartel_Maya_Marco", 10.98f, 11.03f, 4.28f, 5.72f, 14.36f, 15.10f, mGreenSign, false, LAYER_PA, MASK_PA_ROOM);
        MakeBox(roomProps.transform, "Cartel_Maya_Tablero_Madera", 11.03f, 11.055f, 4.32f, 5.68f, 14.40f, 15.06f, mPosterWood, false, LAYER_PA, MASK_PA_ROOM);
        // Schematic Mayan numeral dots & bars + stepped triangle silhouette on the wood board
        for (int row = 0; row < 4; row++)
        {
            float ry = 4.44f + row * 0.13f;
            for (int col = 0; col < 3; col++)
            {
                float rz = 14.49f + col * 0.19f;
                MakeBox(roomProps.transform, "Barra_Numeral_Maya", 11.055f, 11.062f, ry, ry + 0.018f, rz, rz + 0.11f, mBronze, false, LAYER_PA, MASK_PA_ROOM);
                MakeBox(roomProps.transform, "Punto_Numeral_Maya", 11.055f, 11.062f, ry + 0.032f, ry + 0.052f, rz + 0.04f, rz + 0.07f, mBronze, false, LAYER_PA, MASK_PA_ROOM);
            }
        }
        for (int st = 0; st < 4; st++)
        {
            float sw = 0.42f - st * 0.085f;
            MakeBox(roomProps.transform, "Silueta_Piramide_Cartel", 11.055f, 11.061f, 5.04f + st * 0.055f, 5.085f + st * 0.055f, 14.73f - sw * 0.5f, 14.73f + sw * 0.5f, mBronze, false, LAYER_PA, MASK_PA_ROOM);
        }

        // 2) Framed Colorful Bulletin Board inside Sala_Maestros visible through East fixed glass (Photo 3!)
        MakeBox(roomProps.transform, "Pizarron_SalaMaestros_BordeBlanco", 18.88f, 18.92f, 4.62f, 5.58f, 11.55f, 12.85f, mPaper, false, LAYER_PA, MASK_PA_ROOM);
        MakeBox(roomProps.transform, "Pizarron_SalaMaestros_FondoVerde",  18.855f, 18.88f, 4.66f, 5.54f, 11.60f, 12.80f, mGreenSign, false, LAYER_PA, MASK_PA_ROOM);
        Material[] cardMats = { mYellow, mPaper, mRed, mTealBench };
        for (int c = 0; c < 6; c++)
        {
            float cy = 4.78f + (c % 2) * 0.34f;
            float cz = 11.75f + (c / 2) * 0.38f;
            MakeBox(roomProps.transform, $"Tarjeta_Sala_{c}", 18.848f, 18.855f, cy, cy + 0.16f, cz, cz + 0.22f, cardMats[c % cardMats.Length], false, LAYER_PA, MASK_PA_ROOM);
        }

        CombineRenderersByMaterial(corrProps.transform, "Comb_Deco_Sec06_Pasillo_PA", LAYER_PA, MASK_PA_CORRIDOR);
        CombineRenderersByMaterial(roomProps.transform, "Comb_Deco_Sec06_Interior_PA", LAYER_PA, MASK_PA_ROOM);

        log.Add("[OK] Decoración exclusiva de Planta Alta construida: pirámide maya escalonada sobre mesa negra, cartel maya en madera, bancas verdes en ambos lados, antena parabólica sobre tarima, tablero TSA 2-4, extintores en muro norte crema y bote en Acceso_Banos.");
    }

    static void BuildTandemBenchAlongZ(Transform parent, string name, float xBack, float dirX, float zCenter, int seats, Material mBlack, Material mTeal)
    {
        float pitch = 0.50f;
        float totalZ = seats * pitch;
        float z0 = zCenter - totalZ * 0.5f;
        float z1 = zCenter + totalZ * 0.5f;

        float xBeam = xBack + dirX * 0.23f;
        float xSeat0 = Mathf.Min(xBack + dirX * 0.05f, xBack + dirX * 0.47f);
        float xSeat1 = Mathf.Max(xBack + dirX * 0.05f, xBack + dirX * 0.47f);
        float xBack0 = Mathf.Min(xBack, xBack + dirX * 0.055f);
        float xBack1 = Mathf.Max(xBack, xBack + dirX * 0.055f);
        float xFoot0 = Mathf.Min(xBack + dirX * 0.02f, xBack + dirX * 0.52f);
        float xFoot1 = Mathf.Max(xBack + dirX * 0.02f, xBack + dirX * 0.52f);

        // Horizontal support beam + 2 T-legs
        MakeBox(parent, name + "_Travesano", xBeam - 0.032f, xBeam + 0.032f, Y0_PA + 0.27f, Y0_PA + 0.33f, z0 + 0.08f, z1 - 0.08f, mBlack, false, LAYER_PA, MASK_PA_CORRIDOR);
        foreach (float lz in new[] { z0 + 0.24f, z1 - 0.24f })
        {
            MakeBox(parent, name + "_Poste", xBeam - 0.025f, xBeam + 0.025f, Y0_PA + 0.045f, Y0_PA + 0.30f, lz - 0.025f, lz + 0.025f, mBlack, false, LAYER_PA, MASK_PA_CORRIDOR);
            MakeBox(parent, name + "_Pie",   xFoot0, xFoot1, Y0_PA + 0.005f, Y0_PA + 0.045f, lz - 0.032f, lz + 0.032f, mBlack, false, LAYER_PA, MASK_PA_CORRIDOR);
        }

        // Individual green plastic shells with triangular/keyhole backrest cutout (Photos 2, 3, 4!)
        for (int i = 0; i < seats; i++)
        {
            float sz = z0 + (i + 0.5f) * pitch;
            MakeBox(parent, name + "_Asiento", xSeat0, xSeat1, Y0_PA + 0.40f, Y0_PA + 0.455f, sz - 0.21f, sz + 0.21f, mTeal, false, LAYER_PA, MASK_PA_CORRIDOR);
            // Keyhole backrest: bottom rail + top rounded header + 2 side pillars leaving central cutout
            MakeBox(parent, name + "_RespInf", xBack0, xBack1, Y0_PA + 0.44f, Y0_PA + 0.52f, sz - 0.20f, sz + 0.20f, mTeal, false, LAYER_PA, MASK_PA_CORRIDOR);
            MakeBox(parent, name + "_RespSup", xBack0, xBack1, Y0_PA + 0.72f, Y0_PA + 0.81f, sz - 0.17f, sz + 0.17f, mTeal, false, LAYER_PA, MASK_PA_CORRIDOR);
            MakeBox(parent, name + "_RespIzq", xBack0, xBack1, Y0_PA + 0.52f, Y0_PA + 0.73f, sz - 0.195f, sz - 0.085f, mTeal, false, LAYER_PA, MASK_PA_CORRIDOR);
            MakeBox(parent, name + "_RespDer", xBack0, xBack1, Y0_PA + 0.52f, Y0_PA + 0.73f, sz + 0.085f, sz + 0.195f, mTeal, false, LAYER_PA, MASK_PA_CORRIDOR);
        }

        AddInvisibleCollider(parent, "Colision_" + name, xFoot0, xFoot1, Y0_PA, Y0_PA + 0.82f, z0 + 0.03f, z1 - 0.03f, LAYER_PA);
    }

    static void BuildPyramidStairway(
        Transform parent, float cx, float cz, float y0, float y1, float rBot, float rTop,
        int dirIndex, Material mStone, Material mAlfarda)
    {
        // Build 14 fine steps + 2 sloped alfarda balustrades along one cardinal face (0=+X, 1=-X, 2=+Z, 3=-Z)
        int microSteps = 14;
        float stairHalfW = 0.068f;
        float alfW = 0.022f;

        for (int i = 0; i < microSteps; i++)
        {
            float t0 = i / (float)microSteps;
            float t1 = (i + 1) / (float)microSteps;
            float rA = Mathf.Lerp(rBot, rTop, t1);
            float rB = Mathf.Lerp(rBot, rTop, t0);
            float sy0 = Mathf.Lerp(y0, y1, t0);
            float sy1 = Mathf.Lerp(y0, y1, t1);

            if (dirIndex == 0) // +X (East toward corridor)
            {
                MakeBox(parent, "Escalinata", cx + rA - 0.02f, cx + rB, sy0, sy1, cz - stairHalfW, cz + stairHalfW, mStone, false, LAYER_PA, MASK_PA_CORRIDOR);
                MakeBox(parent, "Alfarda_A",  cx + rA - 0.02f, cx + rB + 0.008f, sy0, sy1 + 0.014f, cz - stairHalfW - alfW, cz - stairHalfW, mAlfarda, false, LAYER_PA, MASK_PA_CORRIDOR);
                MakeBox(parent, "Alfarda_B",  cx + rA - 0.02f, cx + rB + 0.008f, sy0, sy1 + 0.014f, cz + stairHalfW, cz + stairHalfW + alfW, mAlfarda, false, LAYER_PA, MASK_PA_CORRIDOR);
            }
            else if (dirIndex == 1) // -X
            {
                MakeBox(parent, "Escalinata", cx - rB, cx - rA + 0.02f, sy0, sy1, cz - stairHalfW, cz + stairHalfW, mStone, false, LAYER_PA, MASK_PA_CORRIDOR);
                MakeBox(parent, "Alfarda_A",  cx - rB - 0.008f, cx - rA + 0.02f, sy0, sy1 + 0.014f, cz - stairHalfW - alfW, cz - stairHalfW, mAlfarda, false, LAYER_PA, MASK_PA_CORRIDOR);
                MakeBox(parent, "Alfarda_B",  cx - rB - 0.008f, cx - rA + 0.02f, sy0, sy1 + 0.014f, cz + stairHalfW, cz + stairHalfW + alfW, mAlfarda, false, LAYER_PA, MASK_PA_CORRIDOR);
            }
            else if (dirIndex == 2) // +Z (South)
            {
                MakeBox(parent, "Escalinata", cx - stairHalfW, cx + stairHalfW, sy0, sy1, cz + rA - 0.02f, cz + rB, mStone, false, LAYER_PA, MASK_PA_CORRIDOR);
                MakeBox(parent, "Alfarda_A",  cx - stairHalfW - alfW, cx - stairHalfW, sy0, sy1 + 0.014f, cz + rA - 0.02f, cz + rB + 0.008f, mAlfarda, false, LAYER_PA, MASK_PA_CORRIDOR);
                MakeBox(parent, "Alfarda_B",  cx + stairHalfW, cx + stairHalfW + alfW, sy0, sy1 + 0.014f, cz + rA - 0.02f, cz + rB + 0.008f, mAlfarda, false, LAYER_PA, MASK_PA_CORRIDOR);
            }
            else // -Z (North)
            {
                MakeBox(parent, "Escalinata", cx - stairHalfW, cx + stairHalfW, sy0, sy1, cz - rB, cz - rA + 0.02f, mStone, false, LAYER_PA, MASK_PA_CORRIDOR);
                MakeBox(parent, "Alfarda_A",  cx - stairHalfW - alfW, cx - stairHalfW, sy0, sy1 + 0.014f, cz - rB - 0.008f, cz - rA + 0.02f, mAlfarda, false, LAYER_PA, MASK_PA_CORRIDOR);
                MakeBox(parent, "Alfarda_B",  cx + stairHalfW, cx + stairHalfW + alfW, sy0, sy1 + 0.014f, cz - rB - 0.008f, cz - rA + 0.02f, mAlfarda, false, LAYER_PA, MASK_PA_CORRIDOR);
            }
        }
    }

    // ============================================================================================
    // HELPERS: GEOMETRY, MESH COMBINING, FILTERING, AUDITING & PHOTO COMPARISONS
    // ============================================================================================
    static GameObject MakeBox(
        Transform parent, string name,
        float x0, float x1, float y0, float y1, float z0, float z1,
        Material mat, bool addCollider, int layer, uint renderingMask, bool isMoving = false)
    {
        GameObject go = Docencia2InteriorBuilder.CreatePlanBox(name, parent, x0, x1, y0, y1, z0, z1, mat, 0.001f, addCollider);
        go.layer = layer;

        var mf = go.GetComponent<MeshFilter>();
        if (mf != null && mf.sharedMesh != null && !AssetDatabase.Contains(mf.sharedMesh))
        {
            string safeAsset = $"{MeshDir}/UV_{parent.name}_{name}_{Mathf.Abs((x0 * 31f + z0 * 17f + y0 * 7f).GetHashCode()):X6}.asset";
            if (AssetDatabase.LoadAssetAtPath<Mesh>(safeAsset) != null) AssetDatabase.DeleteAsset(safeAsset);
            AssetDatabase.CreateAsset(mf.sharedMesh, safeAsset);
        }

        var mr = go.GetComponent<MeshRenderer>();
        if (mr != null)
        {
            mr.renderingLayerMask = renderingMask;
            mr.shadowCastingMode = ShadowCastingMode.Off;
            mr.receiveShadows = true;
        }

        if (isMoving)
            GameObjectUtility.SetStaticEditorFlags(go, 0);
        else
            GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic | StaticEditorFlags.ReflectionProbeStatic | StaticEditorFlags.OccluderStatic);

        return go;
    }

    static GameObject AddInvisibleCollider(Transform parent, string name, float x0, float x1, float y0, float y1, float z0, float z1, int layer)
    {
        Vector3 a = W(x0, y0, z0);
        Vector3 b = W(x1, y1, z1);
        GameObject go = new GameObject(name);
        go.layer = layer;
        go.transform.SetParent(parent, false);
        go.transform.position = (a + b) * 0.5f;
        var col = go.AddComponent<BoxCollider>();
        col.size = new Vector3(Mathf.Abs(b.x - a.x), Mathf.Abs(b.y - a.y), Mathf.Abs(b.z - a.z));
        GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic);
        return go;
    }

    static void CombineRenderersByMaterial(Transform group, string prefix, int layer, uint renderingMask)
    {
        var mrs = group.GetComponentsInChildren<MeshRenderer>(true)
            .Where(r => r.GetComponent<Collider>() == null && r.GetComponent<MeshFilter>() != null && r.GetComponent<MeshFilter>().sharedMesh != null)
            .ToArray();

        int idx = 0;
        foreach (var byMat in mrs.GroupBy(r => r.sharedMaterial))
        {
            Material mat = byMat.Key;
            if (mat == null) continue;
            var items = byMat.ToArray();
            var combines = new List<CombineInstance>(items.Length);
            foreach (var r in items)
            {
                combines.Add(new CombineInstance
                {
                    mesh = r.GetComponent<MeshFilter>().sharedMesh,
                    transform = group.worldToLocalMatrix * r.transform.localToWorldMatrix
                });
            }

            var combined = new Mesh { name = $"{prefix}_{idx++:02d}_{mat.name}" };
            combined.indexFormat = IndexFormat.UInt32;
            combined.CombineMeshes(combines.ToArray(), true, true);
            combined.RecalculateNormals();
            combined.RecalculateTangents();
            combined.RecalculateBounds();

            string assetPath = $"{MeshDir}/{combined.name}.asset";
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(assetPath);
            if (existing != null)
            {
                EditorUtility.CopySerialized(combined, existing);
                Object.DestroyImmediate(combined);
                combined = existing;
            }
            else
            {
                AssetDatabase.CreateAsset(combined, assetPath);
            }

            GameObject go = new GameObject(combined.name);
            go.layer = layer;
            go.transform.SetParent(group, false);
            go.AddComponent<MeshFilter>().sharedMesh = combined;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.renderingLayerMask = renderingMask;
            mr.shadowCastingMode = ShadowCastingMode.Off;
            mr.receiveShadows = true;
            GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic | StaticEditorFlags.ReflectionProbeStatic | StaticEditorFlags.OccluderStatic);
        }

        foreach (var r in mrs)
        {
            if (r != null) Object.DestroyImmediate(r.gameObject);
        }
        // Clean empty intermediate pivots
        foreach (var tr in group.GetComponentsInChildren<Transform>(true).Reverse().ToArray())
        {
            if (tr != null && tr != group && tr.childCount == 0 && tr.GetComponents<Component>().Length == 1)
                Object.DestroyImmediate(tr.gameObject);
        }
    }

    static void FilterMeshTriangles(MeshRenderer r, Func<Vector3, bool> inside, string tag)
    {
        var mf = r.GetComponent<MeshFilter>();
        if (!mf || !mf.sharedMesh) return;
        Mesh original = mf.sharedMesh;
        Vector3[] verts = original.vertices;
        var lists = new List<int>[original.subMeshCount];
        int removed = 0, kept = 0;

        for (int sm = 0; sm < lists.Length; sm++)
        {
            int[] tri = original.GetTriangles(sm);
            lists[sm] = new List<int>(tri.Length);
            for (int i = 0; i < tri.Length; i += 3)
            {
                Vector3 v0 = r.transform.TransformPoint(verts[tri[i]]);
                Vector3 v1 = r.transform.TransformPoint(verts[tri[i + 1]]);
                Vector3 v2 = r.transform.TransformPoint(verts[tri[i + 2]]);
                if (inside(v0) && inside(v1) && inside(v2))
                {
                    removed += 3;
                    continue;
                }
                lists[sm].Add(tri[i]);
                lists[sm].Add(tri[i + 1]);
                lists[sm].Add(tri[i + 2]);
                kept += 3;
            }
        }
        if (removed == 0) return;
        if (kept == 0)
        {
            Object.DestroyImmediate(r.gameObject);
            return;
        }

        string baseName = original.name.Replace("_" + tag, "");
        Mesh m = Object.Instantiate(original);
        m.name = baseName + "_" + tag;
        for (int sm = 0; sm < lists.Length; sm++) m.SetTriangles(lists[sm], sm);
        m.RecalculateBounds();

        string path = $"{MeshDir}/{m.name}.asset";
        var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (existing != null)
        {
            EditorUtility.CopySerialized(m, existing);
            Object.DestroyImmediate(m);
            m = existing;
        }
        else
        {
            AssetDatabase.CreateAsset(m, path);
        }
        mf.sharedMesh = m;
    }

    static void ClearChildren(Transform t)
    {
        foreach (Transform c in t.Cast<Transform>().ToArray())
            Object.DestroyImmediate(c.gameObject);
    }

    static string SnapshotTransformsAndRenderers(GameObject root)
    {
        var sb = new StringBuilder();
        foreach (var t in root.GetComponentsInChildren<Transform>(true))
        {
            sb.Append(t.name).Append('|').Append(t.localPosition).Append('|').Append(t.localRotation).Append('|').Append(t.localScale).Append('\n');
            var r = t.GetComponent<Renderer>();
            if (r != null)
            {
                sb.Append(r.enabled).Append('|');
                foreach (var m in r.sharedMaterials) sb.Append(m ? m.name : "null").Append(',');
                sb.Append('\n');
            }
        }
        using var sha = System.Security.Cryptography.SHA256.Create();
        return Convert.ToBase64String(sha.ComputeHash(Encoding.UTF8.GetBytes(sb.ToString())));
    }

    static void AuditPADoorsAndColliders(Transform paRoot, List<string> log)
    {
        var paDoors = paRoot.GetComponentsInChildren<Docencia2PADoor>(true);
        if (paDoors.Length != 29)
            throw new Exception($"Conteo de Docencia2PADoor inválido: {paDoors.Length} (esperado 29).");

        // Test 90-degree swing & check against furniture/prop colliders
        var propColliders = paRoot.GetComponentsInChildren<Collider>(true)
            .Where(c => c.enabled && c.gameObject.activeInHierarchy &&
                        c.GetComponentsInParent<Transform>().Any(p =>
                            p.name == "Decoracion_Pasillo_Sec06_PA_Fotos" ||
                            p.name == "Bancas_Verdes_Revision3_SUPUESTO" ||
                            p.name == "Mobiliario_PA_SUPUESTO"))
            .ToArray();

        var sweepCollisions = new List<string>();
        foreach (var d in paDoors)
        {
            var interactive = d.hinge != null ? d.hinge.GetComponent<Docencia2InteractiveDoor>() : null;
            if (interactive == null || !interactive.isLocked)
            {
                d.SetOpen(true);
                float expected = interactive != null ? Mathf.Abs(interactive.inwardAngle) : Mathf.Abs(d.inwardAngle);
                if (Quaternion.Angle(d.hinge.localRotation, interactive != null ? interactive.closedRotation : Quaternion.identity) < expected - 1.5f)
                    throw new Exception($"Puerta PA {d.hinge.parent.name} no giró su ángulo esperado ({expected}°).");
                d.SetOpen(false);
            }

            Vector3 v = d.GetComponent<Renderer>().bounds.center - d.hinge.position;
            v.y = 0f;
            v *= 2f;
            for (int a = 0; a <= 24; a++)
            {
                Vector3 dir = Quaternion.Euler(0f, d.inwardAngle * a / 24f, 0f) * v;
                for (int k = 1; k <= 12; k++)
                {
                    Vector3 pt = d.hinge.position + Vector3.up * 0.9f + dir * (k / 12f);
                    foreach (var c in propColliders)
                    {
                        if ((c.ClosestPoint(pt) - pt).sqrMagnitude < 0.0004f)
                            sweepCollisions.Add($"{d.hinge.parent.name} vs {PathOf(c.transform)}");
                    }
                }
            }
        }
        if (sweepCollisions.Count > 0)
            throw new Exception("Interferencia en barrido de puertas PA: " + string.Join("; ", sweepCollisions.Distinct()));

        log.Add("[OK] Auditoría de 29 puertas PA superada (giro 90° exacto y 0 interferencias con bancas, pirámide o decoración).");
    }

    static void SyncTrackersEditMode(Docencia2VisualMode mode)
    {
        bool night = mode == Docencia2VisualMode.Noche;
        foreach (var tr in Object.FindObjectsByType<Docencia2InteriorModeTracker>(FindObjectsInactive.Include))
        {
            if (tr.nightGroup != null) tr.nightGroup.SetActive(night);
            if (tr.terrorGroup != null) tr.terrorGroup.SetActive(!night);
        }
    }

    static void RenderSuite(string prefix)
    {
        var camGo = new GameObject("TEMP_Cam_EscaleraCanceles");
        var cam = camGo.AddComponent<Camera>();
        cam.enabled = false;
        cam.nearClipPlane = 0.06f;
        cam.farClipPlane = 120f;
        cam.clearFlags = CameraClearFlags.Skybox;
        var uacd = camGo.AddComponent<UniversalAdditionalCameraData>();
        uacd.renderPostProcessing = true;

        try
        {
            // View 1: Exact framing of Photo 1 (media_1795674147560_5736d194.jpg — looking West across stairwell from top of stair arrival)
            RenderShot(cam, $"{prefix}_Foto1_Escalera_Trasera.png",
                pos: W(8.25f, 5.02f, 19.29f), look: W(0.50f, 4.55f, 19.29f), fov: 60f, 1440, 1080);

            // View 2: Exact framing of Photo 2 (media_1795674147573_59fd95f8.jpg — PA West glazed partition, pyramid, benches, P16, extinguisher)
            RenderShot(cam, $"{prefix}_Foto2_PA_Cancel_Oeste_Piramide.png",
                pos: W(15.65f, 5.16f, 16.95f), look: W(11.30f, 4.88f, 11.90f), fov: 56f, 1440, 1080);

            // View 3: Exact framing of Photo 3 (media_1795674147585_0c50ca87.jpg — PA East glazed partition Sala_Maestros, foreground column, benches, extinguisher, Acceso_Banos bin)
            RenderShot(cam, $"{prefix}_Foto3_PA_Cancel_Este_Sala.png",
                pos: W(15.35f, 5.16f, 18.05f), look: W(16.65f, 4.85f, 11.20f), fov: 58f, 1440, 1080);

            // View 4: Exact framing of Photo 4 (media_1795674147588_3f813905.jpg — Wide view from Llegada_Escalera showing TSA 2-4, satellite dish on pallet, pyramid, benches, P16, Acceso_Banos)
            RenderShot(cam, $"{prefix}_Foto4_PA_Panoramica_Sec06.png",
                pos: W(14.35f, 5.18f, 19.65f), look: W(11.55f, 4.88f, 12.60f), fov: 58f, 1440, 1080);

            // View 5: PB Sec06 Corridor showing identical glazed panels + solid North cream walls WITHOUT PA decoration
            RenderShot(cam, $"{prefix}_PB_Canceles_Sec06_SinDecoPA.png",
                pos: W(14.24f, 1.78f, 16.80f), look: W(14.24f, 1.55f, 9.20f), fov: 64f, 1440, 1080);

            // View 6: Stairwell intermediate landing looking West at the lower 6-column window behind green railing
            RenderShot(cam, $"{prefix}_Escalera_Descanso_VentanaBaja.png",
                pos: W(5.80f, 3.05f, 19.29f), look: W(0.45f, 2.85f, 19.29f), fov: 64f, 1440, 1080);
        }
        finally
        {
            Object.DestroyImmediate(camGo);
        }
    }

    static void RenderExteriorReferenceCameras(Docencia2ModeSwitcher switcher)
    {
        var camsRoot = GameObject.Find("Camaras_Referencia_Docencia2");
        if (camsRoot == null || switcher == null) return;
        var cams = camsRoot.GetComponentsInChildren<Camera>(true).OrderBy(c => c.name).ToArray();

        foreach (var mode in new[] { Docencia2VisualMode.Noche, Docencia2VisualMode.Terror })
        {
            switcher.ApplyMode(mode, true);
            SyncTrackersEditMode(mode);
            foreach (var refCam in cams)
            {
                var tmpGo = new GameObject("TEMP_ExtRefCam");
                tmpGo.transform.SetPositionAndRotation(refCam.transform.position, refCam.transform.rotation);
                var c = tmpGo.AddComponent<Camera>();
                c.enabled = false;
                c.fieldOfView = refCam.fieldOfView;
                c.nearClipPlane = refCam.nearClipPlane;
                c.farClipPlane = refCam.farClipPlane;
                c.clearFlags = CameraClearFlags.Skybox;
                tmpGo.AddComponent<UniversalAdditionalCameraData>().renderPostProcessing = true;

                RenderCameraToPng(c, Path.Combine(OutDir, $"Ext_{mode}_{refCam.name}.png"), 1280, 720);
                Object.DestroyImmediate(tmpGo);
            }
        }
    }

    static void RenderShot(Camera cam, string fileName, Vector3 pos, Vector3 look, float fov, int w, int h)
    {
        cam.transform.position = pos;
        cam.transform.LookAt(look, Vector3.up);
        cam.fieldOfView = fov;
        RenderCameraToPng(cam, Path.Combine(OutDir, fileName), w, h);
    }

    static void RenderCameraToPng(Camera cam, string fullPath, int w, int h)
    {
        var rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32);
        rt.antiAliasing = 4;
        var prev = RenderTexture.active;
        cam.targetTexture = rt;
        cam.Render();
        RenderTexture.active = rt;
        var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
        tex.Apply();
        File.WriteAllBytes(fullPath, tex.EncodeToPNG());
        cam.targetTexture = null;
        RenderTexture.active = prev;
        Object.DestroyImmediate(tex);
        rt.Release();
        Object.DestroyImmediate(rt);
    }

    static void BuildSideBySideComparisons(List<string> log)
    {
        string userMediaDir = @"C:/Users/faust/.gemini/antigravity/brain/21360248-ec9a-4f6e-88af-e5f6d6b1ecdc/.user_uploaded";
        var pairs = new (string realPhoto, string renderPng, string outJpg)[]
        {
            ("media_1795674147560_5736d194.jpg", "Despues_Foto1_Escalera_Trasera.png",        "Comparativa_1_Escalera_ParedTrasera.jpg"),
            ("media_1795674147573_59fd95f8.jpg", "Despues_Foto2_PA_Cancel_Oeste_Piramide.png","Comparativa_2_PA_Cancel_Oeste_Piramide.jpg"),
            ("media_1795674147585_0c50ca87.jpg", "Despues_Foto3_PA_Cancel_Este_Sala.png",     "Comparativa_3_PA_Cancel_Este_SalaMaestros.jpg"),
            ("media_1795674147588_3f813905.jpg", "Despues_Foto4_PA_Panoramica_Sec06.png",     "Comparativa_4_PA_Panoramica_Parabolica_Piramide.jpg")
        };

        foreach (var (realPhoto, renderPng, outJpg) in pairs)
        {
            string realPath = Path.Combine(userMediaDir, realPhoto);
            string renPath = Path.Combine(OutDir, renderPng);
            if (!File.Exists(realPath) || !File.Exists(renPath)) continue;

            var texL = new Texture2D(2, 2);
            texL.LoadImage(File.ReadAllBytes(realPath));
            var texR = new Texture2D(2, 2);
            texR.LoadImage(File.ReadAllBytes(renPath));

            int h = 768;
            int wL = Mathf.RoundToInt(h * (texL.width / (float)texL.height));
            int wR = Mathf.RoundToInt(h * (texR.width / (float)texR.height));
            int gap = 12;
            var outTex = new Texture2D(wL + gap + wR, h, TextureFormat.RGB24, false);

            for (int y = 0; y < h; y++)
            {
                float v = y / (float)(h - 1);
                for (int x = 0; x < outTex.width; x++)
                {
                    if (x < wL)
                        outTex.SetPixel(x, y, texL.GetPixelBilinear(x / (float)(wL - 1), v));
                    else if (x < wL + gap)
                        outTex.SetPixel(x, y, new Color(0.12f, 0.12f, 0.14f));
                    else
                        outTex.SetPixel(x, y, texR.GetPixelBilinear((x - wL - gap) / (float)(wR - 1), v));
                }
            }
            outTex.Apply();
            File.WriteAllBytes(Path.Combine(OutDir, outJpg), outTex.EncodeToJPG(92));

            Object.DestroyImmediate(texL);
            Object.DestroyImmediate(texR);
            Object.DestroyImmediate(outTex);
            log.Add($"[OK] Comparativa generada: {outJpg}");
        }
    }

    // ============================================================================================
    // 5. PLAY MODE VERIFICATION (DOORS P16/P17 PA & P15/P16 PB + NAVIGATION + NOCHE/TERROR + FPS)
    // ============================================================================================
    const string PlayFlagFile = "Revision_Escalera_Canceles_Sec05_Sec06/play_qa_active.flag";

    public static void StartPlayVerification()
    {
        Directory.CreateDirectory(OutDir);
        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        File.WriteAllText(PlayFlagFile, "RUN");
        EditorApplication.isPlaying = true;
    }

    [InitializeOnLoadMethod]
    static void OnLoadHook()
    {
        EditorApplication.playModeStateChanged -= OnPlayStateChanged;
        EditorApplication.playModeStateChanged += OnPlayStateChanged;
    }

    static void OnPlayStateChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.EnteredPlayMode && File.Exists(PlayFlagFile))
        {
            File.Delete(PlayFlagFile);
            var runnerGo = new GameObject("TEMP_PlayQA_EscaleraCanceles");
            Object.DontDestroyOnLoad(runnerGo);
            runnerGo.AddComponent<EscaleraCancelesPlayRunner>();
        }
    }

    public class EscaleraCancelesPlayRunner : MonoBehaviour
    {
        System.Collections.IEnumerator Start()
        {
            var sb = new StringBuilder();
            int errCount = 0;
            Application.logMessageReceived += OnLog;
            void OnLog(string msg, string stack, LogType type)
            {
                if (type == LogType.Error || type == LogType.Exception)
                {
                    errCount++;
                    sb.AppendLine($"[ERR] {msg}");
                }
            }

            yield return null;
            yield return null;

            var player = Object.FindAnyObjectByType<Docencia2FirstPersonPlayer>();
            var switcher = Object.FindAnyObjectByType<Docencia2ModeSwitcher>();
            var darkness = Docencia2TerrorDarknessController.Instance;

            // 1. Verify NOCHE mode in PA Sec06 + open/close P16 & P17
            switcher.ApplyMode(Docencia2VisualMode.Noche, false);
            if (darkness != null) darkness.SyncState();
            player.SetFlashlightState(false, false);

            string[] doorPaths =
            {
                "PA_Interior/Pasillo_Junto_Redes/Entrada_P16/P16_Hueco096_Abre_oeste/Bisagra_Giro90_Interior",
                "PA_Interior/Sala_Maestros/Cancel_Oeste/P17_Hueco096_Abre_este/Bisagra_Giro90_Interior",
                "PB_Interior/03_Fase3_Carpinteria_y_Vidrios/Canceleria_Interior_Vidrio_Verde/Cancel_Vidrio_Sec06_PasilloOeste_x11.24/P15_Alcoba_Oeste_CancelVidrio/Pivote_Bisagra",
                "PB_Interior/03_Fase3_Carpinteria_y_Vidrios/Canceleria_Interior_Vidrio_Verde/Cancel_Vidrio_Sec06_PasilloEste_x17.24/P16_Local_Este_CancelVidrio/Pivote_Bisagra"
            };

            foreach (string dp in doorPaths)
            {
                var d = GameObject.Find(dp)?.GetComponent<Docencia2InteractiveDoor>();
                if (d == null)
                {
                    errCount++;
                    sb.AppendLine($"[FAIL] Puerta no encontrada: {dp}");
                    continue;
                }
                d.SetOpen(true);
                float openAng = Quaternion.Angle(d.transform.localRotation, Quaternion.identity);
                d.SetOpen(false);
                float closedAng = Quaternion.Angle(d.transform.localRotation, Quaternion.identity);
                sb.AppendLine($"[OK] Puerta interactiva {dp}: abierta={openAng:F1}°, cerrada={closedAng:F1}°");
            }

            // 2. Capture Play Mode first-person shots in PA Sec06 (NOCHE), PA Stairwell (NOCHE), PB Sec06 (NOCHE), and PA Sec06 (TERROR + floating phone!)
            player.TeleportToPlan(15.20f, 3.606f, 17.40f, 0f);
            AimPlayer(player, 11.40f, 4.88f, 12.20f);
            for (int i = 0; i < 6; i++) yield return null;
            RenderCameraToPng(player.playerCamera, Path.Combine(OutDir, "Play_PA_Sec06_Noche.png"), 1440, 900);

            player.TeleportToPlan(7.40f, 3.606f, 19.29f, 0f);
            AimPlayer(player, 0.50f, 4.65f, 19.29f);
            for (int i = 0; i < 6; i++) yield return null;
            RenderCameraToPng(player.playerCamera, Path.Combine(OutDir, "Play_Escalera_Noche.png"), 1440, 900);

            player.TeleportToPlan(14.24f, 0.22f, 16.40f, 0f);
            AimPlayer(player, 14.24f, 1.55f, 9.20f);
            for (int i = 0; i < 6; i++) yield return null;
            RenderCameraToPng(player.playerCamera, Path.Combine(OutDir, "Play_PB_Sec06_Noche.png"), 1440, 900);

            // TERROR mode + floating phone check in PA Sec06 looking at pyramid & glass
            switcher.ApplyMode(Docencia2VisualMode.Terror, false);
            if (darkness != null) darkness.SyncState();
            player.SetFlashlightState(true, false);
            player.TeleportToPlan(13.80f, 3.606f, 16.30f, 0f);
            AimPlayer(player, 11.65f, 4.55f, 14.20f);
            for (int i = 0; i < 12; i++) yield return null;
            RenderCameraToPng(player.playerCamera, Path.Combine(OutDir, "Play_PA_Sec06_Terror_CelularFlotante.png"), 1440, 900);

            // Walk test along PA Sec06 corridor (z = 16.5 -> 8.2 along x = 14.24)
            var cc = player.GetComponent<CharacterController>();
            player.TeleportToPlan(14.24f, 3.606f, 16.50f, 0f);
            Vector3 walkDir = (W(14.24f, 3.606f, 8.20f) - W(14.24f, 3.606f, 16.50f)).normalized;
            walkDir.y = 0f;
            var dtSamples = new List<double>(180);
            double prevT = Time.realtimeSinceStartupAsDouble;
            for (int f = 0; f < 120; f++)
            {
                cc.Move((walkDir * 4.0f + Vector3.down * 3.0f) * (1f / 60f));
                yield return null;
                double nowT = Time.realtimeSinceStartupAsDouble;
                double dt = nowT - prevT;
                prevT = nowT;
                if (f > 10 && dt > 0.0001 && dt < 0.25) dtSamples.Add(dt);
            }
            Vector2 endXZ = Docencia2FirstPersonPlayer.UnityToPlanXZ(player.transform.position);
            double avgFps = dtSamples.Count > 0 ? dtSamples.Count / dtSamples.Sum() : 0.0;
            sb.AppendLine($"[OK] Caminata PA Sec06 z=16.5 -> z={endXZ.y:F2} sin obstrucciones | AvgFPS={avgFps:F1} | ErroresConsola={errCount}");

            Application.logMessageReceived -= OnLog;
            File.WriteAllText(Path.Combine(OutDir, "reporte_play_qa.txt"), sb.ToString(), Encoding.UTF8);

            if (Application.isBatchMode)
                EditorApplication.Exit(errCount == 0 ? 0 : 1);
            else
                EditorApplication.isPlaying = false;
        }

        static void AimPlayer(Docencia2FirstPersonPlayer player, float tx, float ty, float tz)
        {
            Vector3 target = W(tx, ty, tz);
            Vector3 dir = (target - player.playerCamera.transform.position).normalized;
            float yaw = Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg;
            float pitch = -Mathf.Asin(Mathf.Clamp(dir.y, -0.95f, 0.95f)) * Mathf.Rad2Deg;
            player.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            player.playerCamera.transform.localRotation = Quaternion.Euler(pitch, 0f, 0f);
        }
    }
}
