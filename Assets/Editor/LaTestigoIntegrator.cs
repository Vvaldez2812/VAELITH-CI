using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using Vaelith.Docencia2;

public static class LaTestigoIntegrator
{
    private const string FbxPath = "Assets/Models/LaTestigo/La_Testigo.fbx";
    private const string TexDir = "Assets/Models/LaTestigo/Textures";
    private const string MatPath = "Assets/Models/LaTestigo/Mat_LaTestigo.mat";
    private const string CtrlPath = "Assets/Animations/LaTestigo_Controller.controller";
    private const string PrefabPath = "Assets/Prefabs/LaTestigo.prefab";
    private const string ScenePath = "Assets/Scenes/Exterior_Docencia2.unity";

    public static void IntegrateAll()
    {
        EnsureDir("Assets/Animations");
        EnsureDir("Assets/Prefabs");

        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

        // 1. Configure PBR textures cleanly (Tripo 4K BaseColor + 2K Normal + 2K MetallicSmoothness)
        Texture2D texBase = ConfigureTexture(
            $"{TexDir}/LaTestigo_BaseColor.png",
            TextureImporterType.Default, sRgb: true, maxSize: 4096, alphaFromInput: false);
        Texture2D texNormal = ConfigureTexture(
            $"{TexDir}/LaTestigo_Normal.png",
            TextureImporterType.NormalMap, sRgb: false, maxSize: 2048, alphaFromInput: false);
        Texture2D texMetalSmooth = ConfigureTexture(
            $"{TexDir}/LaTestigo_MetallicSmoothness.png",
            TextureImporterType.Default, sRgb: false, maxSize: 2048, alphaFromInput: true);

        // 2. Build URP Lit material matched to baked horror character
        Material mat = AssetDatabase.LoadAssetAtPath<Material>(MatPath);
        Shader urpLit = Shader.Find("Universal Render Pipeline/Lit");
        if (urpLit == null)
            throw new Exception("No se encontro el shader Universal Render Pipeline/Lit.");

        if (mat == null)
        {
            mat = new Material(urpLit) { name = "Mat_LaTestigo" };
            AssetDatabase.CreateAsset(mat, MatPath);
        }
        else
        {
            mat.shader = urpLit;
        }

        mat.SetTexture("_BaseMap", texBase);
        mat.SetColor("_BaseColor", new Color(0.94f, 0.94f, 0.94f, 1f));
        mat.SetTexture("_BumpMap", texNormal);
        mat.SetFloat("_BumpScale", 0.75f);
        mat.EnableKeyword("_NORMALMAP");

        mat.SetTexture("_MetallicGlossMap", texMetalSmooth);
        mat.SetFloat("_Metallic", 0.0f);
        mat.SetFloat("_Smoothness", 0.85f);
        mat.EnableKeyword("_METALLICSPECGLOSSMAP");

        mat.SetFloat("_SpecularHighlights", 1f);
        mat.SetFloat("_EnvironmentReflections", 1f);
        EditorUtility.SetDirty(mat);
        AssetDatabase.SaveAssets();

        // 3. Configure FBX ModelImporter: Humanoid Avatar + Material Remap + Looping Idle Clip
        var importer = AssetImporter.GetAtPath(FbxPath) as ModelImporter;
        if (importer == null)
            throw new Exception($"No se pudo cargar ModelImporter para {FbxPath}");

        importer.globalScale = 1.0f;
        importer.useFileScale = true;
        importer.importBlendShapes = false;
        importer.importVisibility = false;
        importer.importCameras = false;
        importer.importLights = false;
        importer.isReadable = false;
        importer.meshCompression = ModelImporterMeshCompression.Off;
        // Clear any stale cached human bone mappings (e.g. if FBX skeleton changed from custom rig to Mixamo)
        importer.animationType = ModelImporterAnimationType.None;
        var cleanHd = importer.humanDescription;
        cleanHd.human = Array.Empty<HumanBone>();
        cleanHd.skeleton = Array.Empty<SkeletonBone>();
        importer.humanDescription = cleanHd;
        importer.SaveAndReimport();

        importer.animationType = ModelImporterAnimationType.Human;
        importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
        importer.importAnimation = true;
        importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
        importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), "Mat_LaTestigo"), mat);
        importer.SaveAndReimport();

        // Configure looping clip if available
        var clips = importer.defaultClipAnimations;
        if (clips != null && clips.Length > 0)
        {
            foreach (var c in clips)
            {
                c.loopTime = true;
                c.loopPose = true;
                c.lockRootRotation = true;
                c.lockRootHeightY = true;
                c.lockRootPositionXZ = true;
            }
            importer.clipAnimations = clips;
            importer.SaveAndReimport();
        }

        var loadedAssets = AssetDatabase.LoadAllAssetsAtPath(FbxPath);
        Avatar avatar = loadedAssets.OfType<Avatar>().FirstOrDefault();
        AnimationClip idleClip = loadedAssets.OfType<AnimationClip>()
            .FirstOrDefault(c => !c.name.StartsWith("__preview__", StringComparison.Ordinal));

        Debug.Log($"[LaTestigoIntegrator] Avatar={(avatar != null ? avatar.name : "NULL")} isValid={avatar != null && avatar.isValid} isHuman={avatar != null && avatar.isHuman} IdleClip={(idleClip != null ? idleClip.name : "NULL")}");

        if (avatar == null || !avatar.isValid || !avatar.isHuman)
            throw new Exception("La_Testigo Avatar Humanoid no es valido o no se mapeo correctamente.");

        // 4. Build Animator Controller with CreepyIdle default state
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(CtrlPath);
        if (controller != null)
            AssetDatabase.DeleteAsset(CtrlPath);
        controller = AnimatorController.CreateAnimatorControllerAtPath(CtrlPath);
        if (idleClip != null)
        {
            var sm = controller.layers[0].stateMachine;
            var st = sm.AddState("CreepyIdle");
            st.motion = idleClip;
            sm.defaultState = st;
        }
        EditorUtility.SetDirty(controller);
        AssetDatabase.SaveAssets();

        // 5. Build Prefab Assets/Prefabs/LaTestigo.prefab
        GameObject fbxPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(FbxPath);
        if (fbxPrefab == null)
            throw new Exception($"Falta GameObject en {FbxPath}");

        int pbLayer = LayerMask.NameToLayer("PB_Interior");
        if (pbLayer < 0) pbLayer = 8;

        GameObject tempRoot = (GameObject)PrefabUtility.InstantiatePrefab(fbxPrefab);
        tempRoot.name = "LaTestigo";
        SetLayerRecursively(tempRoot, pbLayer);

        var anim = tempRoot.GetComponent<Animator>();
        if (anim == null) anim = tempRoot.AddComponent<Animator>();
        anim.avatar = avatar;
        anim.runtimeAnimatorController = controller;
        anim.applyRootMotion = false;
        anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;

        var cap = tempRoot.GetComponent<CapsuleCollider>();
        if (cap == null) cap = tempRoot.AddComponent<CapsuleCollider>();
        cap.center = new Vector3(0f, 1.15f, 0f);
        cap.height = 2.30f;
        cap.radius = 0.33f;

        foreach (var smr in tempRoot.GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            smr.sharedMaterial = mat;
            smr.updateWhenOffscreen = true;
            smr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.TwoSided;
            smr.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
        }

        PrefabUtility.SaveAsPrefabAsset(tempRoot, PrefabPath);
        UnityEngine.Object.DestroyImmediate(tempRoot);

        // 6. Place cleanly into Exterior_Docencia2.unity inside PB_Interior Pasillo Central
        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        GameObject pbRoot = GameObject.Find("PB_Interior");

        // Remove any prior LaTestigo instance cleanly
        foreach (var existing in Resources.FindObjectsOfTypeAll<GameObject>()
            .Where(g => g.scene.IsValid() && g.name == "LaTestigo").ToArray())
        {
            UnityEngine.Object.DestroyImmediate(existing);
        }

        GameObject prefabAsset = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefabAsset, scene);
        instance.name = "LaTestigo";
        if (pbRoot != null)
            instance.transform.SetParent(pbRoot.transform, worldPositionStays: true);

        // Pasillo Central Sec04 (flanked by Aula 7 & Auditorio walls): planX = 14.24, planZ = 24.80, facing +X (North toward Vestibulo/Player!)
        Vector3 spawnPos = Docencia2FirstPersonPlayer.PlanToUnityPos(14.24f, 0.20f, 24.80f);
        if (Physics.Raycast(spawnPos + Vector3.up * 1.4f, Vector3.down, out RaycastHit hit, 3.0f, ~0, QueryTriggerInteraction.Ignore))
        {
            spawnPos.y = hit.point.y;
        }
        instance.transform.position = spawnPos;
        instance.transform.rotation = Quaternion.Euler(0f, 90f, 0f);
        SetLayerRecursively(instance, pbLayer);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);

        var bodySmr = instance.GetComponentInChildren<SkinnedMeshRenderer>(true);
        int triCount = bodySmr != null && bodySmr.sharedMesh != null ? bodySmr.sharedMesh.triangles.Length / 3 : -1;
        float trueHeight = 0f;
        if (bodySmr != null)
        {
            Mesh bm = new Mesh();
            bodySmr.BakeMesh(bm);
            var vs = bm.vertices;
            float mnY = float.MaxValue, mxY = float.MinValue;
            Matrix4x4 l2w = bodySmr.transform.localToWorldMatrix;
            for (int vi = 0; vi < vs.Length; vi++)
            {
                float wy = l2w.MultiplyPoint3x4(vs[vi]).y;
                if (wy < mnY) mnY = wy;
                if (wy > mxY) mxY = wy;
            }
            UnityEngine.Object.DestroyImmediate(bm);
            if (mxY > mnY) trueHeight = mxY - mnY;
        }
        Debug.Log($"[LaTestigoIntegrator] Integracion COMPLETA -> Pos={spawnPos}, AlturaReal={trueHeight:F3} m, Tris={triCount}, Layer={LayerMask.LayerToName(instance.layer)}");
    }

    private static Texture2D ConfigureTexture(string path, TextureImporterType type, bool sRgb, int maxSize, bool alphaFromInput)
    {
        var ti = AssetImporter.GetAtPath(path) as TextureImporter;
        if (ti == null)
            throw new Exception($"No se encontro textura en {path}");
        ti.textureType = type;
        ti.sRGBTexture = sRgb;
        ti.maxTextureSize = maxSize;
        ti.textureCompression = TextureImporterCompression.CompressedHQ;
        ti.alphaSource = alphaFromInput ? TextureImporterAlphaSource.FromInput : TextureImporterAlphaSource.None;
        ti.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    private static void SetLayerRecursively(GameObject go, int layer)
    {
        go.layer = layer;
        foreach (Transform child in go.transform)
            SetLayerRecursively(child.gameObject, layer);
    }

    private static void EnsureDir(string assetFolder)
    {
        if (!Directory.Exists(assetFolder))
            Directory.CreateDirectory(assetFolder);
    }
}
