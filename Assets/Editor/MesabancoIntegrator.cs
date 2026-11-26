using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Vaelith.Docencia2.Editor;

/// <summary>
/// Restaura y mantiene el modelo original limpio de pupitre/mesabanco escolar (F5_Pupitre:
/// asiento/respaldo verde azulado, paleta crema, estructura metálica negra) en todas las
/// aulas de Planta Baja (Aula_1..7) y Planta Alta (Aula_8..15), conservando rotación,
/// capas, renderingLayerMask y pasillos caminables >= 0.74 m sin errores de malla.
/// </summary>
public static class MesabancoIntegrator
{
    private const string MeshDir = "Assets/Models/Docencia2/Fase5";
    private const string MatDir = "Assets/Materials/Docencia2/Fase5";
    private const string PrefabPath = "Assets/Prefabs/Mesabanco.prefab";
    private const string ScenePath = "Assets/Scenes/Exterior_Docencia2.unity";

    private static readonly (string meshName, string matName)[] OriginalParts =
    {
        ("F5_Pupitre_F5_VerdeAzulado", "F5_VerdeAzulado"),
        ("F5_Pupitre_F5_Crema", "F5_Crema"),
        ("F5_Pupitre_F5_Negro", "F5_Negro"),
    };

    public static void IntegrateAll()
    {
        EnsureDir("Assets/Prefabs");
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

        // 1. Reconstruir Assets/Prefabs/Mesabanco.prefab con las 3 mallas originales limpias F5_Pupitre
        GameObject tempRoot = new GameObject("Mesabanco");
        tempRoot.transform.position = Vector3.zero;
        tempRoot.transform.rotation = Quaternion.identity;
        tempRoot.transform.localScale = Vector3.one;

        int builtParts = 0;
        foreach (var (meshName, matName) in OriginalParts)
        {
            string meshPath = $"{MeshDir}/{meshName}.asset";
            string matPath = $"{MatDir}/{matName}.mat";
            Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (mesh == null)
                throw new FileNotFoundException($"No se encontro la malla original del pupitre en {meshPath}");
            if (mat == null)
                throw new FileNotFoundException($"No se encontro el material original del pupitre en {matPath}");

            mat.enableInstancing = true;
            EditorUtility.SetDirty(mat);

            var child = new GameObject(matName);
            child.transform.SetParent(tempRoot.transform, false);
            child.transform.localPosition = Vector3.zero;
            child.transform.localRotation = Quaternion.identity;
            child.transform.localScale = Vector3.one;

            var mf = child.AddComponent<MeshFilter>();
            mf.sharedMesh = mesh;

            var mr = child.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = ShadowCastingMode.Off;
            mr.receiveShadows = true;
            builtParts++;
        }

        Bounds localBounds = ComputeRendererBounds(tempRoot.transform)
            ?? new Bounds(new Vector3(-0.04f, 0.44f, 0f), new Vector3(0.87f, 0.88f, 0.70f));

        // BoxCollider exacto a las dimensiones del pupitre original
        var box = tempRoot.AddComponent<BoxCollider>();
        box.center = localBounds.center;
        box.size = localBounds.size;

        PrefabUtility.SaveAsPrefabAsset(tempRoot, PrefabPath);
        UnityEngine.Object.DestroyImmediate(tempRoot);
        AssetDatabase.SaveAssets();

        // 2. Reemplazar todos los Pupitre_* en PB_Interior y PA_Interior por el prefab original limpio
        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        GameObject prefabAsset = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);

        var targets = new List<(Transform t, bool isPA)>();
        foreach (string rootName in new[] { "PB_Interior", "PA_Interior" })
        {
            GameObject rootObj = FindRootByName(scene, rootName);
            if (rootObj == null) continue;
            bool isPA = rootName == "PA_Interior";

            foreach (Transform t in rootObj.GetComponentsInChildren<Transform>(true))
            {
                if (t != null && t.name.StartsWith("Pupitre_", StringComparison.Ordinal))
                    targets.Add((t, isPA));
            }
        }

        int restored = 0;
        foreach (var (oldT, isPA) in targets)
        {
            if (oldT == null) continue;
            GameObject oldGo = oldT.gameObject;

            Bounds? oldBounds = ComputeRendererBounds(oldT);
            Renderer firstRend = oldT.GetComponentInChildren<Renderer>(true);
            uint renderMask = firstRend != null ? firstRend.renderingLayerMask : (isPA ? 17u : 2u);
            int layer = isPA ? 30 : 8;
            StaticEditorFlags staticFlags = StaticEditorFlags.ContributeGI | StaticEditorFlags.OccludeeStatic | StaticEditorFlags.ReflectionProbeStatic;
            Transform parent = oldT.parent;
            int siblingIdx = oldT.GetSiblingIndex();
            Quaternion rot = oldT.rotation;
            float floorY = isPA ? 3.606f : 0.20f;

            Vector3 targetPos = oldBounds.HasValue
                ? new Vector3(oldBounds.Value.center.x, floorY, oldBounds.Value.center.z)
                : new Vector3(oldT.position.x, floorY, oldT.position.z);
            string origName = oldGo.name;

            GameObject inst = (GameObject)PrefabUtility.InstantiatePrefab(prefabAsset, scene);
            inst.name = origName;
            inst.transform.SetParent(parent, false);
            inst.transform.SetPositionAndRotation(targetPos, rot);
            inst.transform.localScale = Vector3.one;
            inst.transform.SetSiblingIndex(siblingIdx);

            // Asegurar patas exactamente apoyadas en floorY sin flotar ni hundirse
            Bounds? newB = ComputeRendererBounds(inst.transform);
            if (newB.HasValue && Mathf.Abs(newB.Value.min.y - floorY) > 0.001f)
            {
                inst.transform.position += new Vector3(0f, floorY - newB.Value.min.y, 0f);
            }

            SetLayerAndMaskRecursively(inst, layer, renderMask, staticFlags);
            EditorUtility.SetDirty(inst);

            UnityEngine.Object.DestroyImmediate(oldGo);
            restored++;
        }

        // 3. Reacomodar cuadrículas y pasillos caminables (>= 0.74 m) en las 15 aulas con el tamaño exacto del pupitre original
        VaelithAuditFixer.RespaceAllClassroomsAndAuditorio();

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();

        Debug.Log($"[MesabancoIntegrator] Pupitres ORIGINALES restaurados -> Parts={builtParts}, Restaurados={restored} en PB y PA, pasillos reacomodados.");
    }

    private static GameObject FindRootByName(Scene scene, string name)
    {
        foreach (GameObject go in scene.GetRootGameObjects())
        {
            if (go.name == name) return go;
        }
        return null;
    }

    private static Bounds? ComputeRendererBounds(Transform root)
    {
        var rends = root.GetComponentsInChildren<Renderer>(true);
        if (rends == null || rends.Length == 0) return null;
        Bounds b = rends[0].bounds;
        for (int i = 1; i < rends.Length; i++)
        {
            if (rends[i] != null) b.Encapsulate(rends[i].bounds);
        }
        return b;
    }

    private static void SetLayerAndMaskRecursively(GameObject go, int layer, uint renderMask, StaticEditorFlags flags)
    {
        go.layer = layer;
        GameObjectUtility.SetStaticEditorFlags(go, flags);
        var r = go.GetComponent<Renderer>();
        if (r != null) r.renderingLayerMask = renderMask;
        foreach (Transform ch in go.transform)
            SetLayerAndMaskRecursively(ch.gameObject, layer, renderMask, flags);
    }

    private static void EnsureDir(string assetFolder)
    {
        if (!Directory.Exists(assetFolder))
            Directory.CreateDirectory(assetFolder);
    }
}
