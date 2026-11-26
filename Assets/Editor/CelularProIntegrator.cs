using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

public static class CelularProIntegrator
{
    public const string FbxPath = "Assets/Models/Celular/Celular_Pro.fbx";
    public const string UiTexPath = "Assets/Models/Celular/Textures/Celular_Pantalla_UI.png";
    public const string MatDir = "Assets/Materials/Celular";
    public const string PrefabPath = "Assets/Prefabs/Celular_Pro.prefab";

    public static GameObject EnsureCelularProPrefab()
    {
        if (!File.Exists(FbxPath))
            throw new Exception($"No existe {FbxPath}. Ejecuta primero el generador Blender.");

        EnsureFolder(MatDir);
        EnsureFolder("Assets/Prefabs");

        // 1. Configurar textura OLED UI
        Texture2D uiTex = null;
        if (File.Exists(UiTexPath))
        {
            var ti = AssetImporter.GetAtPath(UiTexPath) as TextureImporter;
            if (ti != null)
            {
                bool dirty = false;
                if (!ti.sRGBTexture) { ti.sRGBTexture = true; dirty = true; }
                if (ti.maxTextureSize != 1024) { ti.maxTextureSize = 1024; dirty = true; }
                if (ti.filterMode != FilterMode.Bilinear) { ti.filterMode = FilterMode.Bilinear; dirty = true; }
                if (ti.wrapMode != TextureWrapMode.Clamp) { ti.wrapMode = TextureWrapMode.Clamp; dirty = true; }
                if (dirty) ti.SaveAndReimport();
            }
            uiTex = AssetDatabase.LoadAssetAtPath<Texture2D>(UiTexPath);
        }

        Shader urpLit = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");

        var matBody = UpsertMat($"{MatDir}/Mat_Celular_Cuerpo.mat", urpLit, m =>
        {
            m.SetColor("_BaseColor", new Color(0.11f, 0.13f, 0.16f, 1f));
            m.SetFloat("_Metallic", 0.88f);
            m.SetFloat("_Smoothness", 0.72f);
        });

        var matScreen = UpsertMat($"{MatDir}/Mat_Celular_Pantalla.mat", urpLit, m =>
        {
            m.SetColor("_BaseColor", Color.white);
            if (uiTex != null) m.SetTexture("_BaseMap", uiTex);
            m.SetFloat("_Metallic", 0.0f);
            m.SetFloat("_Smoothness", 0.35f);
            m.SetFloat("_SpecularHighlights", 0f);
            m.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");
            m.EnableKeyword("_EMISSION");
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            if (uiTex != null) m.SetTexture("_EmissionMap", uiTex);
            m.SetColor("_EmissionColor", new Color(0.85f, 0.94f, 1.0f, 1f) * 0.62f);
        });

        var matIsland = UpsertMat($"{MatDir}/Mat_Celular_IslaCam.mat", urpLit, m =>
        {
            m.SetColor("_BaseColor", new Color(0.035f, 0.04f, 0.05f, 1f));
            m.SetFloat("_Metallic", 0.70f);
            m.SetFloat("_Smoothness", 0.78f);
        });

        var matLens = UpsertMat($"{MatDir}/Mat_Celular_Lente.mat", urpLit, m =>
        {
            m.SetColor("_BaseColor", new Color(0.02f, 0.06f, 0.13f, 1f));
            m.SetFloat("_Metallic", 0.15f);
            m.SetFloat("_Smoothness", 0.96f);
        });

        var matRing = UpsertMat($"{MatDir}/Mat_Celular_Aro.mat", urpLit, m =>
        {
            m.SetColor("_BaseColor", new Color(0.68f, 0.72f, 0.78f, 1f));
            m.SetFloat("_Metallic", 0.95f);
            m.SetFloat("_Smoothness", 0.86f);
        });

        var matLed = UpsertMat($"{MatDir}/Mat_Celular_FlashLED.mat", urpLit, m =>
        {
            m.SetColor("_BaseColor", new Color(0.92f, 0.96f, 1.0f, 1f));
            m.SetFloat("_Metallic", 0.0f);
            m.SetFloat("_Smoothness", 0.85f);
            m.EnableKeyword("_EMISSION");
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            m.SetColor("_EmissionColor", new Color(0.92f, 0.96f, 1.0f, 1f) * 1.3f);
        });

        var map = new Dictionary<string, Material>
        {
            { "Mat_Celular_Cuerpo", matBody },
            { "Mat_Celular_Pantalla", matScreen },
            { "Mat_Celular_IslaCam", matIsland },
            { "Mat_Celular_Lente", matLens },
            { "Mat_Celular_Aro", matRing },
            { "Mat_Celular_FlashLED", matLed }
        };

        // 2. Configurar ModelImporter FBX y remapear materiales
        var importer = AssetImporter.GetAtPath(FbxPath) as ModelImporter;
        if (importer == null)
            throw new Exception($"No se pudo cargar ModelImporter para {FbxPath}");

        importer.globalScale = 1.0f;
        importer.useFileScale = true;
        importer.importAnimation = false;
        importer.importCameras = false;
        importer.importLights = false;
        importer.isReadable = false;
        importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
        foreach (var kv in map)
        {
            importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), kv.Key), kv.Value);
        }
        importer.SaveAndReimport();

        // 3. Instanciar modelo limpio -> Prefab Celular_Pro.prefab con Flash_Anchor y sin Colliders
        GameObject fbxAsset = AssetDatabase.LoadAssetAtPath<GameObject>(FbxPath);
        if (fbxAsset == null)
            throw new Exception($"No se encontro GameObject en {FbxPath}");

        GameObject temp = (GameObject)PrefabUtility.InstantiatePrefab(fbxAsset);
        temp.name = "Celular_Pro";

        foreach (var col in temp.GetComponentsInChildren<Collider>(true))
            UnityEngine.Object.DestroyImmediate(col);

        foreach (var mr in temp.GetComponentsInChildren<MeshRenderer>(true))
        {
            mr.shadowCastingMode = ShadowCastingMode.On;
            mr.receiveShadows = true;

            if (mr.name.IndexOf("Pantalla", StringComparison.OrdinalIgnoreCase) >= 0)
                mr.sharedMaterial = matScreen;
            else if (mr.name.IndexOf("Flash", StringComparison.OrdinalIgnoreCase) >= 0)
                mr.sharedMaterial = matLed;
            else if (mr.name.IndexOf("Isla", StringComparison.OrdinalIgnoreCase) >= 0)
                mr.sharedMaterial = matIsland;
            else if (mr.name.IndexOf("Lente", StringComparison.OrdinalIgnoreCase) >= 0)
                mr.sharedMaterial = matLens;
            else if (mr.name.IndexOf("Aro", StringComparison.OrdinalIgnoreCase) >= 0)
                mr.sharedMaterial = matRing;
            else
                mr.sharedMaterial = matBody;
        }

        Transform flashAnchor = temp.transform.Find("Flash_Anchor");
        if (flashAnchor == null)
        {
            var faGo = new GameObject("Flash_Anchor");
            faGo.transform.SetParent(temp.transform, false);
            flashAnchor = faGo.transform;
        }
        // Ubicado justo sobre Celular_Flash_LED (-Z = pantalla, +Z = lentes/flash trasero)
        flashAnchor.localPosition = new Vector3(0.006f, 0.058f, 0.015f);
        flashAnchor.localRotation = Quaternion.identity;

        GameObject savedPrefab = PrefabUtility.SaveAsPrefabAsset(temp, PrefabPath);
        UnityEngine.Object.DestroyImmediate(temp);
        AssetDatabase.SaveAssets();

        Debug.Log($"[CelularProIntegrator] Prefab generado y remapeado: {PrefabPath}");
        return savedPrefab;
    }

    private static Material UpsertMat(string path, Shader shader, Action<Material> configure)
    {
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null)
        {
            mat = new Material(shader);
            configure(mat);
            AssetDatabase.CreateAsset(mat, path);
        }
        else
        {
            if (mat.shader != shader && shader != null) mat.shader = shader;
            configure(mat);
            EditorUtility.SetDirty(mat);
        }
        return mat;
    }

    private static void EnsureFolder(string assetFolderPath)
    {
        string[] parts = assetFolderPath.Split('/');
        string acc = parts[0];
        for (int i = 1; i < parts.Length; i++)
        {
            string next = acc + "/" + parts[i];
            if (!AssetDatabase.IsValidFolder(next))
                AssetDatabase.CreateFolder(acc, parts[i]);
            acc = next;
        }
    }
}
