using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.Rendering;
using Vaelith.Docencia2;
using Object = UnityEngine.Object;

public static class FaseACancelesBronce
{
    static Vector3 W(float x, float y, float z) => Docencia2InteriorBuilder.PlanToUnityPos(x, y, z);
    static Material M(string name) => AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Docencia2/" + name + ".mat");
    static string PathOf(Transform t) => t.parent ? PathOf(t.parent) + "/" + t.name : t.name;

    const float Y0_PA = 3.60f;
    const float Y1_PA = 6.80f;
    const float Y_DOOR_TOP_PA = 3.60f + 2.10f; // 5.70 m (igual que PB: 0.20 + 2.10 = 2.30 m)
    const int LAYER_PA = 30;
    const uint MASK_PA = 17u; // 1u | 16u

    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new Exception("Construcción requiere Edit mode.");

        string outDir = FaseARevision.Output;
        Directory.CreateDirectory(outDir);

        var scene = EditorSceneManager.OpenScene("Assets/Scenes/Exterior_Docencia2.unity");
        EditorSceneManager.SaveScene(scene, outDir + "/Antes_Canceles_Bronce_PB_PA.unity", true);

        // Capturas Antes en PA y PB
        FaseARevision.Shot("PB_Canceles_Antes_BronceFix", 14.24f, 1.85f, 15.0f, 14.24f, 1.6f, 8.0f);
        FaseARevision.Shot("PA_Canceles_Antes_BronceFix", 14.24f, 5.25f, 15.0f, 14.24f, 5.0f, 8.0f);

        Material mBronze = M("Mat_Int_Marco_Puerta_Bronce");
        Material mGlassClaro = M("Mat_Int_Vidrio_Interior_Claro");
        Material mGlassTransp = M("Mat_FaseA_Vidrio_Cancel_Transparente") ?? mGlassClaro;
        Material mGreen = M("Mat_Int_Franja_Verde_Institucional");
        Material mGuinda = M("Mat_Int_Linea_Guinda_Institucional");
        Material mSteel = M("Mat_Int_Chapa_Acero_Inox");

        if (!mBronze || !mGlassClaro || !mGreen || !mGuinda || !mSteel)
            throw new Exception("Faltan materiales de cancelería en Assets/Materials/Docencia2/");

        // ====================================================================
        // 1. PLANTA BAJA (Sec 06: Alcoba Oeste x=11.24 y Local Este x=17.24)
        //    Regresar el color del marco a bronce (Mat_Int_Marco_Puerta_Bronce)
        //    como estaba en PB_Canceles_Antes.png, conservando el vidrio claro.
        // ====================================================================
        Transform pbCancels = GameObject.Find("PB_Interior/03_Fase3_Carpinteria_y_Vidrios/Canceleria_Interior_Vidrio_Verde").transform;
        foreach (MeshRenderer r in pbCancels.GetComponentsInChildren<MeshRenderer>(true))
        {
            string path = PathOf(r.transform);
            if (path.Contains("Cancel_Vidrio_Sec06"))
            {
                if (r.name.Contains("Marco_Puerta_Bronce") || (r.sharedMaterial && r.sharedMaterial.name.Contains("Marco_Ventana_Claro")))
                {
                    r.sharedMaterial = mBronze;
                }
                else if (r.sharedMaterial && r.sharedMaterial.name.Contains("Vidrio"))
                {
                    r.sharedMaterial = mGlassClaro;
                }
            }
            else if (r.name.StartsWith("Comb_Canceleria_PB"))
            {
                if (r.name.Contains("Marco_Puerta_Bronce"))
                {
                    var mats = r.sharedMaterials;
                    for (int i = 0; i < mats.Length; i++) mats[i] = mBronze;
                    r.sharedMaterials = mats;
                }
                else if (r.name.Contains("Vidrio_Interior_Claro"))
                {
                    var mats = r.sharedMaterials;
                    for (int i = 0; i < mats.Length; i++) mats[i] = mGlassClaro;
                    r.sharedMaterials = mats;
                }
            }
        }

        // ====================================================================
        // 2. PLANTA ALTA (Sec 06: z = 7.71..15.43, x = 11.24 y x = 17.24)
        //    Construir los canceles con el MISMO diseño y color bronce que PB:
        //    - Lado Este (Sala_Maestros/Cancel_Oeste, x=17.24, z=7.71..15.43, P17 en z=8.83..9.79)
        //    - Lado Oeste Norte (Pasillo_Junto_Redes/Entrada_P16, x=11.24, z=7.71..10.29, P16 en z=7.86..8.82 junto a baños)
        //    - Lado Oeste Sur (Redes_SITE/Cancel_Este, x=11.24, z=10.29..15.43)
        // ====================================================================

        // 2A. Sala de Maestros (x = 17.24, z = 7.71..15.43, P17 alineada con P16 de enfrente en z = 7.86..8.82 junto a baños, abre al este)
        Transform salaCancel = GameObject.Find("PA_Interior/Sala_Maestros/Cancel_Oeste").transform;
        ClearChildren(salaCancel);
        BuildPACancelWithDoor(
            salaCancel,
            xAxis: 17.24f,
            zStart: 7.71f,
            zEnd: 15.43f,
            doorZ0: 7.86f,
            doorZ1: 8.82f,
            doorNodeName: "P17_Hueco096_Abre_este",
            inwardSign: +1f,
            postZs: new float[] { 7.73f, 7.86f, 8.82f, 10.27f, 11.66f, 13.53f, 15.41f },
            mBronze, mGlassClaro, mGreen, mGuinda, mSteel
        );

        // 2B. Pasillo junto a Redes (x = 11.24, z = 7.71..10.29, P16 confirmada en z = 7.86..8.82 abre al oeste)
        Transform p16Cancel = GameObject.Find("PA_Interior/Pasillo_Junto_Redes/Entrada_P16").transform;
        ClearChildren(p16Cancel);
        BuildPACancelWithDoor(
            p16Cancel,
            xAxis: 11.24f,
            zStart: 7.71f,
            zEnd: 10.29f,
            doorZ0: 7.86f,
            doorZ1: 8.82f,
            doorNodeName: "P16_Hueco096_Abre_oeste",
            inwardSign: -1f,
            postZs: new float[] { 7.73f, 7.86f, 8.82f, 10.27f },
            mBronze, mGlassClaro, mGreen, mGuinda, mSteel
        );

        // 2C. Redes / SITE (x = 11.24, z = 10.29..15.43, tramo fijo continuo con mismo diseño que PB)
        Transform redesCancel = GameObject.Find("PA_Interior/Redes_SITE/Cancel_Este").transform;
        ClearChildren(redesCancel);
        BuildPACancelFixedSpan(
            redesCancel,
            xAxis: 11.24f,
            zStart: 10.29f,
            zEnd: 15.43f,
            postZs: new float[] { 11.66f, 13.53f, 15.41f },
            mBronze, mGlassClaro, mGreen, mGuinda
        );

        // Asegurar que las puertas interactivas y sus colliders queden limpios y refinados
        Physics.SyncTransforms();

        // Guardar escena y assets
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();

        // Ejecutar auditoría final para actualizar conteos sin errores
        FaseAAuditFinal.Run();

        // Renderizar capturas de verificación comparativas en PB y PA
        FaseARevision.Shot("PB_Canceles_Bronce_Restaurado", 14.24f, 1.85f, 15.0f, 14.24f, 1.6f, 8.0f);
        FaseARevision.Shot("PA_Canceles_Bronce_DisenoIgualPB", 14.24f, 5.25f, 15.0f, 14.24f, 5.0f, 8.0f);
        FaseARevision.Shot("PA_P16_Bronce_Corregida", 14.0f, 5.2f, 11.0f, 10.7f, 5.1f, 8.5f);
        FaseARevision.Shot("PA_Sala_Bronce_Corregida", 14.0f, 5.2f, 12.0f, 18.0f, 5.0f, 10.0f);

        Debug.Log("[FaseACancelesBronce] Canceles PB y PA actualizados a diseño y color bronce exitosamente.");
    }

    static void ClearChildren(Transform parent)
    {
        foreach (Transform child in parent.Cast<Transform>().ToArray())
        {
            Object.DestroyImmediate(child.gameObject);
        }
    }

    static GameObject CreateBoxPA(
        string name, Transform parent,
        float x0, float x1, float y0, float y1, float z0, float z1,
        Material mat, float bevel, bool addCollider, bool isMoving = false)
    {
        GameObject go = Docencia2InteriorBuilder.CreatePlanBox(name, parent, x0, x1, y0, y1, z0, z1, mat, bevel, addCollider);
        go.layer = LAYER_PA;
        var mr = go.GetComponent<MeshRenderer>();
        if (mr != null)
        {
            mr.renderingLayerMask = MASK_PA;
            mr.shadowCastingMode = ShadowCastingMode.Off;
            mr.receiveShadows = true;
        }
        if (isMoving)
        {
            GameObjectUtility.SetStaticEditorFlags(go, 0);
        }
        else
        {
            GameObjectUtility.SetStaticEditorFlags(
                go,
                StaticEditorFlags.BatchingStatic |
                StaticEditorFlags.ReflectionProbeStatic |
                StaticEditorFlags.OccluderStatic
            );
        }
        return go;
    }

    static void BuildPACancelWithDoor(
        Transform cancelGrp,
        float xAxis, float zStart, float zEnd,
        float doorZ0, float doorZ1,
        string doorNodeName, float inwardSign,
        float[] postZs,
        Material mBronzeFrame, Material mGlass, Material mGreen, Material mGuinda, Material mSteel)
    {
        float y0 = Y0_PA;
        float yDoorTop = Y_DOOR_TOP_PA;
        float yCeil = Y1_PA;
        float fHalfX = 0.025f;

        // 1. Riel inferior de aluminio bronce en los tramos fijos (fuera de la puerta)
        if (doorZ0 - zStart > 0.02f)
        {
            CreateBoxPA("Riel_Inf_Norte", cancelGrp, xAxis - fHalfX, xAxis + fHalfX, y0, y0 + 0.05f, zStart, doorZ0, mBronzeFrame, 0.002f, false);
            CreateBoxPA("Riel_Medio_Norte", cancelGrp, xAxis - fHalfX, xAxis + fHalfX, y0 + 0.98f, y0 + 1.015f, zStart, doorZ0, mBronzeFrame, 0.002f, false);
            CreateBoxPA("Cristal_Fijo_Norte", cancelGrp, xAxis - 0.008f, xAxis + 0.008f, y0 + 0.04f, yDoorTop, zStart + 0.02f, doorZ0 - 0.02f, mGlass, 0.001f, true);
            CreateBoxPA("Franja_Verde_Norte", cancelGrp, xAxis - 0.011f, xAxis + 0.011f, y0 + 0.72f, y0 + 0.81f, zStart + 0.02f, doorZ0 - 0.02f, mGreen, 0.001f, false);
            CreateBoxPA("Linea_Guinda_Norte", cancelGrp, xAxis - 0.011f, xAxis + 0.011f, y0 + 0.66f, y0 + 0.69f, zStart + 0.02f, doorZ0 - 0.02f, mGuinda, 0.001f, false);
        }

        if (zEnd - doorZ1 > 0.02f)
        {
            CreateBoxPA("Riel_Inf_Sur", cancelGrp, xAxis - fHalfX, xAxis + fHalfX, y0, y0 + 0.05f, doorZ1, zEnd, mBronzeFrame, 0.002f, false);
            CreateBoxPA("Riel_Medio_Sur", cancelGrp, xAxis - fHalfX, xAxis + fHalfX, y0 + 0.98f, y0 + 1.015f, doorZ1, zEnd, mBronzeFrame, 0.002f, false);
            CreateBoxPA("Cristal_Fijo_Sur", cancelGrp, xAxis - 0.008f, xAxis + 0.008f, y0 + 0.04f, yDoorTop, doorZ1 + 0.02f, zEnd - 0.02f, mGlass, 0.001f, true);
            CreateBoxPA("Franja_Verde_Sur", cancelGrp, xAxis - 0.011f, xAxis + 0.011f, y0 + 0.72f, y0 + 0.81f, doorZ1 + 0.02f, zEnd - 0.02f, mGreen, 0.001f, false);
            CreateBoxPA("Linea_Guinda_Sur", cancelGrp, xAxis - 0.011f, xAxis + 0.011f, y0 + 0.66f, y0 + 0.69f, doorZ1 + 0.02f, zEnd - 0.02f, mGuinda, 0.001f, false);
        }

        // 2. Travesaño horizontal a nivel de dintel de puerta (2.10 m sobre piso PA) y riel superior en plafón
        CreateBoxPA("Riel_Dintel_Continuo", cancelGrp, xAxis - fHalfX, xAxis + fHalfX, yDoorTop - 0.02f, yDoorTop + 0.02f, zStart, zEnd, mBronzeFrame, 0.002f, false);
        CreateBoxPA("Riel_Superior_Plafon", cancelGrp, xAxis - fHalfX, xAxis + fHalfX, yCeil - 0.04f, yCeil, zStart, zEnd, mBronzeFrame, 0.002f, false);

        // 3. Montantes verticales de piso a plafón
        for (int i = 0; i < postZs.Length; i++)
        {
            float pz = postZs[i];
            CreateBoxPA($"Montante_Vert_{i + 1:02d}", cancelGrp, xAxis - fHalfX, xAxis + fHalfX, y0, yCeil, pz - 0.02f, pz + 0.02f, mBronzeFrame, 0.002f, false);
        }

        // 4. Montaje de cristal superior (montante sobre puerta y paneles fijos: Y = yDoorTop..yCeil)
        CreateBoxPA("Cristal_Montante_Superior", cancelGrp, xAxis - 0.008f, xAxis + 0.008f, yDoorTop + 0.01f, yCeil - 0.03f, zStart + 0.02f, zEnd - 0.02f, mGlass, 0.001f, true);

        // 5. Hoja de puerta de cristal con marco perimetral bronce, franja verde/guinda y bisagra interactiva 90°
        BuildPADoorInCancel(
            cancelGrp, doorNodeName, xAxis, doorZ0, doorZ1,
            y0, yDoorTop, inwardSign,
            mBronzeFrame, mGlass, mGreen, mGuinda, mSteel
        );
    }

    static void BuildPACancelFixedSpan(
        Transform cancelGrp,
        float xAxis, float zStart, float zEnd,
        float[] postZs,
        Material mBronzeFrame, Material mGlass, Material mGreen, Material mGuinda)
    {
        float y0 = Y0_PA;
        float yDoorTop = Y_DOOR_TOP_PA;
        float yCeil = Y1_PA;
        float fHalfX = 0.025f;

        CreateBoxPA("Riel_Inf_Continuo", cancelGrp, xAxis - fHalfX, xAxis + fHalfX, y0, y0 + 0.05f, zStart, zEnd, mBronzeFrame, 0.002f, false);
        CreateBoxPA("Riel_Medio_Continuo", cancelGrp, xAxis - fHalfX, xAxis + fHalfX, y0 + 0.98f, y0 + 1.015f, zStart, zEnd, mBronzeFrame, 0.002f, false);
        CreateBoxPA("Riel_Dintel_Continuo", cancelGrp, xAxis - fHalfX, xAxis + fHalfX, yDoorTop - 0.02f, yDoorTop + 0.02f, zStart, zEnd, mBronzeFrame, 0.002f, false);
        CreateBoxPA("Riel_Superior_Plafon", cancelGrp, xAxis - fHalfX, xAxis + fHalfX, yCeil - 0.04f, yCeil, zStart, zEnd, mBronzeFrame, 0.002f, false);

        for (int i = 0; i < postZs.Length; i++)
        {
            float pz = postZs[i];
            CreateBoxPA($"Montante_Vert_{i + 1:02d}", cancelGrp, xAxis - fHalfX, xAxis + fHalfX, y0, yCeil, pz - 0.02f, pz + 0.02f, mBronzeFrame, 0.002f, false);
        }

        CreateBoxPA("Cristal_Fijo_Inferior", cancelGrp, xAxis - 0.008f, xAxis + 0.008f, y0 + 0.04f, yDoorTop, zStart + 0.01f, zEnd - 0.02f, mGlass, 0.001f, true);
        CreateBoxPA("Cristal_Montante_Superior", cancelGrp, xAxis - 0.008f, xAxis + 0.008f, yDoorTop + 0.01f, yCeil - 0.03f, zStart + 0.01f, zEnd - 0.02f, mGlass, 0.001f, true);
        CreateBoxPA("Franja_Verde_Continuo", cancelGrp, xAxis - 0.011f, xAxis + 0.011f, y0 + 0.72f, y0 + 0.81f, zStart + 0.01f, zEnd - 0.02f, mGreen, 0.001f, false);
        CreateBoxPA("Linea_Guinda_Continuo", cancelGrp, xAxis - 0.011f, xAxis + 0.011f, y0 + 0.66f, y0 + 0.69f, zStart + 0.01f, zEnd - 0.02f, mGuinda, 0.001f, false);
    }

    static void BuildPADoorInCancel(
        Transform parent, string doorName,
        float xAxis, float z0, float z1,
        float yBottom, float yTop, float inwardSign,
        Material mBronzeFrame, Material mGlass, Material mGreen, Material mGuinda, Material mSteel)
    {
        GameObject doorRoot = new GameObject(doorName);
        doorRoot.layer = LAYER_PA;
        doorRoot.transform.SetParent(parent, false);

        float zLeaf0 = z0 + 0.025f;
        float zLeaf1 = z1 - 0.025f;
        float yLeaf0 = yBottom + 0.015f;
        float yLeaf1 = yTop - 0.02f;

        GameObject hingePivot = new GameObject("Bisagra_Giro90_Interior");
        hingePivot.layer = LAYER_PA;
        hingePivot.transform.SetParent(doorRoot.transform, false);
        hingePivot.transform.position = W(xAxis, yBottom, zLeaf0);

        float stileW = 0.045f;
        float railH = 0.05f;
        float leafHalfT = 0.02f;

        // Marco perimetral de aluminio bronce de la hoja
        CreateBoxPA("Bastidor_Izq", hingePivot.transform, xAxis - leafHalfT, xAxis + leafHalfT, yLeaf0, yLeaf1, zLeaf0, zLeaf0 + stileW, mBronzeFrame, 0.002f, false, true);
        CreateBoxPA("Bastidor_Der", hingePivot.transform, xAxis - leafHalfT, xAxis + leafHalfT, yLeaf0, yLeaf1, zLeaf1 - stileW, zLeaf1, mBronzeFrame, 0.002f, false, true);
        CreateBoxPA("Bastidor_Sup", hingePivot.transform, xAxis - leafHalfT, xAxis + leafHalfT, yLeaf1 - railH, yLeaf1, zLeaf0 + stileW, zLeaf1 - stileW, mBronzeFrame, 0.002f, false, true);
        CreateBoxPA("Bastidor_Inf", hingePivot.transform, xAxis - leafHalfT, xAxis + leafHalfT, yLeaf0, yLeaf0 + railH, zLeaf0 + stileW, zLeaf1 - stileW, mBronzeFrame, 0.002f, false, true);
        CreateBoxPA("Bastidor_Medio", hingePivot.transform, xAxis - leafHalfT, xAxis + leafHalfT, yBottom + 0.98f, yBottom + 1.015f, zLeaf0 + stileW, zLeaf1 - stileW, mBronzeFrame, 0.002f, false, true);

        // Cristal templado de la hoja (con Docencia2PADoor para compatibilidad total con validadores de PA)
        GameObject glassLeaf = CreateBoxPA(
            "Hoja", hingePivot.transform,
            xAxis - 0.007f, xAxis + 0.007f,
            yLeaf0 + railH * 0.5f, yLeaf1 - railH * 0.5f,
            zLeaf0 + stileW * 0.5f, zLeaf1 - stileW * 0.5f,
            mGlass, 0.001f, false, true
        );
        var paDoor = glassLeaf.AddComponent<Docencia2PADoor>();
        paDoor.hinge = hingePivot.transform;
        paDoor.inwardAngle = inwardSign > 0f ? -90f : 90f;

        // Franja verde y línea guinda en la hoja de la puerta
        CreateBoxPA("Franja_Verde_Hoja", hingePivot.transform, xAxis - 0.010f, xAxis + 0.010f, yBottom + 0.72f, yBottom + 0.81f, zLeaf0 + stileW, zLeaf1 - stileW, mGreen, 0.001f, false, true);
        CreateBoxPA("Linea_Guinda_Hoja", hingePivot.transform, xAxis - 0.010f, xAxis + 0.010f, yBottom + 0.66f, yBottom + 0.69f, zLeaf0 + stileW, zLeaf1 - stileW, mGuinda, 0.001f, false, true);

        // Jaladera tubular doble de acero inoxidable
        float zHandle = zLeaf1 - 0.07f;
        CreateBoxPA("Jaladera_Ext", hingePivot.transform, xAxis - 0.045f, xAxis - 0.025f, yBottom + 0.90f, yBottom + 1.20f, zHandle - 0.012f, zHandle + 0.012f, mSteel, 0.002f, false, true);
        CreateBoxPA("Jaladera_Int", hingePivot.transform, xAxis + 0.025f, xAxis + 0.045f, yBottom + 0.90f, yBottom + 1.20f, zHandle - 0.012f, zHandle + 0.012f, mSteel, 0.002f, false, true);

        // Componente Docencia2InteractiveDoor y BoxCollider en la bisagra (con despeje inferior de 3 cm al piso PA = 3.63 m, sin englobar jaladera)
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
                (a & 4) == 0 ? b.min.z : b.max.z
            )));
        }
        var box = hingePivot.AddComponent<BoxCollider>();
        Vector3 size = local.size;
        Vector3 center = local.center;
        if (size.x < size.z) size.x = Mathf.Min(size.x, 0.04f);
        else size.z = Mathf.Min(size.z, 0.04f);

        float desiredBottom = 3.63f;
        float bottom = hingePivot.transform.TransformPoint(center - size * 0.5f).y;
        float trim = Mathf.Max(0f, desiredBottom - bottom);
        center.y += trim * 0.5f;
        size.y -= trim;
        box.center = center;
        box.size = size;

        foreach (var tr in hingePivot.GetComponentsInChildren<Transform>(true))
        {
            GameObjectUtility.SetStaticEditorFlags(tr.gameObject, 0);
        }
    }
}
