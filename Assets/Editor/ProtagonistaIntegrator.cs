using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
using UnityEngine.Rendering;
using Vaelith.Docencia2;

/// <summary>
/// Configura e integra el modelo rigged Tripo de la Protagonista (1.60 m exactos)
/// como cuerpo jugable animado Humanoid con brazo IK sosteniendo el celular-linterna
/// y alternancia de vista 1ª / 3ª persona (tecla V).
/// </summary>
public static class ProtagonistaIntegrator
{
    private const string FbxPath = "Assets/Models/Protagonista/Protagonista.fbx";
    private const string TexDir = "Assets/Models/Protagonista/Textures";
    private const string MatPath = "Assets/Models/Protagonista/Mat_Protagonista.mat";
    private const string CtrlPath = "Assets/Animations/Protagonista_Controller.controller";
    private const string MaskPath = "Assets/Animations/Protagonista_RightArmMask.mask";
    private const string PrefabPath = "Assets/Prefabs/Protagonista.prefab";
    private const string ScenePath = "Assets/Scenes/Exterior_Docencia2.unity";

    public static void IntegrateAll()
    {
        EnsureDir("Assets/Animations");
        EnsureDir("Assets/Prefabs");

        GameObject celularPrefab = CelularProIntegrator.EnsureCelularProPrefab();

        if (!File.Exists(FbxPath))
            throw new FileNotFoundException($"Falta el modelo FBX de la protagonista en {FbxPath}");

        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

        // 1. Configurar texturas PBR (4K BaseColor + 2K Normal + 2K MetallicSmoothness)
        Texture2D texBase = ConfigureTexture(
            $"{TexDir}/Protagonista_BaseColor.png",
            TextureImporterType.Default, sRgb: true, maxSize: 4096, alphaFromInput: false);
        Texture2D texNormal = ConfigureTexture(
            $"{TexDir}/Protagonista_Normal.png",
            TextureImporterType.NormalMap, sRgb: false, maxSize: 2048, alphaFromInput: false);
        Texture2D texMetalSmooth = ConfigureTexture(
            $"{TexDir}/Protagonista_MetallicSmoothness.png",
            TextureImporterType.Default, sRgb: false, maxSize: 2048, alphaFromInput: true);

        // 2. Material URP Lit limpio (ropa mate realista + detalles de lentes/cremalleras)
        Shader urpLit = Shader.Find("Universal Render Pipeline/Lit");
        if (urpLit == null)
            throw new Exception("No se encontro el shader Universal Render Pipeline/Lit.");

        Material mat = AssetDatabase.LoadAssetAtPath<Material>(MatPath);
        if (mat == null)
        {
            mat = new Material(urpLit) { name = "Mat_Protagonista" };
            AssetDatabase.CreateAsset(mat, MatPath);
        }
        else
        {
            mat.shader = urpLit;
        }

        mat.SetTexture("_BaseMap", texBase);
        mat.SetColor("_BaseColor", Color.white);
        mat.SetTexture("_BumpMap", texNormal);
        mat.SetFloat("_BumpScale", 0.45f);
        mat.EnableKeyword("_NORMALMAP");

        mat.SetTexture("_MetallicGlossMap", texMetalSmooth);
        mat.SetFloat("_Metallic", 0.0f);
        mat.SetFloat("_Smoothness", 0.90f);
        mat.EnableKeyword("_METALLICSPECGLOSSMAP");

        mat.SetFloat("_SpecularHighlights", 1f);
        mat.SetFloat("_EnvironmentReflections", 1f);
        EditorUtility.SetDirty(mat);
        AssetDatabase.SaveAssets();

        // 3. Configurar ModelImporter FBX -> Humanoid Avatar + Clips + Remap material
        var importer = AssetImporter.GetAtPath(FbxPath) as ModelImporter;
        if (importer == null)
            throw new Exception($"No se pudo cargar ModelImporter para {FbxPath}");

        importer.globalScale = 1.0f;
        importer.useFileScale = true;
        importer.importBlendShapes = false;
        importer.importVisibility = true;
        importer.importCameras = false;
        importer.importLights = false;
        importer.isReadable = false;
        importer.meshCompression = ModelImporterMeshCompression.Off;

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
        importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), "Mat_Protagonista"), mat);
        importer.SaveAndReimport();

        var defaultClips = importer.defaultClipAnimations;
        if (defaultClips != null && defaultClips.Length > 0)
        {
            foreach (var c in defaultClips)
            {
                c.loopTime = true;
                c.loopPose = true;
                c.lockRootRotation = true;
                c.lockRootPositionXZ = true;
                c.lockRootHeightY = c.name.IndexOf("Crouch", StringComparison.OrdinalIgnoreCase) < 0;
            }
            importer.clipAnimations = defaultClips;
            importer.SaveAndReimport();
        }

        var loadedAssets = AssetDatabase.LoadAllAssetsAtPath(FbxPath);
        Avatar avatar = loadedAssets.OfType<Avatar>().FirstOrDefault();
        AnimationClip[] clips = loadedAssets.OfType<AnimationClip>()
            .Where(c => !c.name.StartsWith("__preview__", StringComparison.Ordinal))
            .ToArray();

        if (avatar == null || !avatar.isValid || !avatar.isHuman)
            throw new Exception("Protagonista Avatar Humanoid no es valido o no se mapeo correctamente.");

        AnimationClip FindClip(string key) =>
            clips.FirstOrDefault(c => c.name.IndexOf(key, StringComparison.OrdinalIgnoreCase) >= 0);

        AnimationClip idleClip = FindClip("Prota_Idle") ?? clips.FirstOrDefault();
        AnimationClip walkClip = FindClip("Prota_Walk") ?? idleClip;
        AnimationClip runClip = FindClip("Prota_Run") ?? walkClip;
        AnimationClip crouchClip = FindClip("Prota_Crouch") ?? idleClip;
        AnimationClip holdPhoneClip = FindClip("Prota_HoldPhone") ?? idleClip;

        Debug.Log($"[ProtagonistaIntegrator] Avatar={avatar.name} valid={avatar.isValid} human={avatar.isHuman} clips={string.Join(",", clips.Select(c => c.name))}");

        // 4. AvatarMask brazo/mano derecha + Animator Controller (Layer 0 Locomotion + Layer 1 RightArmPhone)
        if (AssetDatabase.LoadAssetAtPath<AvatarMask>(MaskPath) != null)
            AssetDatabase.DeleteAsset(MaskPath);
        var armMask = new AvatarMask();
        for (int i = 0; i < (int)AvatarMaskBodyPart.LastBodyPart; i++)
            armMask.SetHumanoidBodyPartActive((AvatarMaskBodyPart)i, false);
        armMask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.RightArm, true);
        armMask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.RightFingers, true);
        armMask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.RightHandIK, true);
        AssetDatabase.CreateAsset(armMask, MaskPath);

        if (AssetDatabase.LoadAssetAtPath<AnimatorController>(CtrlPath) != null)
            AssetDatabase.DeleteAsset(CtrlPath);

        var controller = AnimatorController.CreateAnimatorControllerAtPath(CtrlPath);
        controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
        controller.AddParameter("IsCrouching", AnimatorControllerParameterType.Bool);
        controller.AddLayer("RightArmPhone");

        var layers = controller.layers;
        layers[0].iKPass = false;
        layers[1].avatarMask = armMask;
        layers[1].defaultWeight = 1.0f;
        layers[1].iKPass = true;
        layers[1].blendingMode = AnimatorLayerBlendingMode.Override;
        controller.layers = layers;

        var sm = controller.layers[0].stateMachine;
        AnimatorState locState = controller.CreateBlendTreeInController("Locomotion", out BlendTree blendTree, 0);
        blendTree.blendType = BlendTreeType.Simple1D;
        blendTree.blendParameter = "Speed";
        blendTree.useAutomaticThresholds = false;
        if (idleClip != null) blendTree.AddChild(idleClip, 0.0f);
        if (walkClip != null) blendTree.AddChild(walkClip, 0.58f);
        if (runClip != null) blendTree.AddChild(runClip, 1.0f);
        sm.defaultState = locState;

        AnimatorState crouchState = sm.AddState("Crouch");
        crouchState.motion = crouchClip;

        var toCrouch = locState.AddTransition(crouchState);
        toCrouch.hasExitTime = false;
        toCrouch.duration = 0.14f;
        toCrouch.AddCondition(AnimatorConditionMode.If, 0f, "IsCrouching");

        var toLoc = crouchState.AddTransition(locState);
        toLoc.hasExitTime = false;
        toLoc.duration = 0.14f;
        toLoc.AddCondition(AnimatorConditionMode.IfNot, 0f, "IsCrouching");

        var armSm = controller.layers[1].stateMachine;
        AnimatorState holdState = armSm.AddState("HoldPhone");
        holdState.motion = holdPhoneClip;
        armSm.defaultState = holdState;

        EditorUtility.SetDirty(armMask);
        EditorUtility.SetDirty(controller);
        AssetDatabase.SaveAssets();

        // 5. Calcular agarre anatómico EXACTO e invariante a pose en el espacio local de RightHand
        // usando los nudillos rígidos hijos (Index1, Middle1, Pinky1):
        //   +X_phone = Middle1 -> Wrist (borde derecho/muñeca)
        //   +Y_phone = Pinky1  -> Index1 (arriba del teléfono, cámara arriba)
        //   +Z_phone = Cross(+X_phone, +Y_phone) (dorso de la mano -> lentes y LED hacia adelante; -Z_phone = pantalla hacia la palma/ojos)
        GameObject fbxPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(FbxPath);
        if (fbxPrefab == null)
            throw new Exception($"Falta GameObject en {FbxPath}");

        Vector3 celLocalPosInHand = new Vector3(0.0f, 0.055f, 0.022f);
        Quaternion celLocalRotInHand = Quaternion.identity;
        Vector3 handOffsetInPhoneSpace = new Vector3(0.045f, -0.032f, 0.022f);

        GameObject sampleGo = (GameObject)PrefabUtility.InstantiatePrefab(fbxPrefab);
        sampleGo.transform.position = Vector3.zero;
        sampleGo.transform.rotation = Quaternion.identity;
        var sampleAnim = sampleGo.GetComponent<Animator>() ?? sampleGo.AddComponent<Animator>();
        sampleAnim.avatar = avatar;
        sampleAnim.applyRootMotion = false;

        Transform sampleRightHand = sampleAnim.GetBoneTransform(HumanBodyBones.RightHand);
        Transform idx1 = sampleAnim.GetBoneTransform(HumanBodyBones.RightIndexProximal);
        Transform mid1 = sampleAnim.GetBoneTransform(HumanBodyBones.RightMiddleProximal);
        Transform pinky1 = sampleAnim.GetBoneTransform(HumanBodyBones.RightLittleProximal);

        if (sampleRightHand != null && idx1 != null && mid1 != null && pinky1 != null)
        {
            Vector3 midL = sampleRightHand.InverseTransformPoint(mid1.position);
            Vector3 idxL = sampleRightHand.InverseTransformPoint(idx1.position);
            Vector3 pkyL = sampleRightHand.InverseTransformPoint(pinky1.position);

            Vector3 palmAlong = midL.normalized; // muñeca -> nudillo medio (hacia borde izquierdo del teléfono)
            Vector3 xPhone = -palmAlong;         // borde derecho del teléfono (+X_phone)
            Vector3 yRaw = (idxL - pkyL);        // meñique -> índice (+Y_phone = arriba del teléfono)
            Vector3 yPhone = (yRaw - Vector3.Dot(yRaw, xPhone) * xPhone).normalized;
            Vector3 zPhone = Vector3.Cross(xPhone, yPhone).normalized; // dorso mano (+Z_phone = cámaras/flash); -zPhone = palma/pantalla

            // Orientación vertical exacta paralela a la palma (0 deg roll)
            celLocalRotInHand = Quaternion.LookRotation(zPhone, yPhone);

            // Centro del teléfono apoyado en la palma/nudillos, elevado +3.2 cm para que los dedos nunca tapen las cámaras ni el pulgar cruce la pantalla
            float palmLen = midL.magnitude;
            celLocalPosInHand = palmAlong * (palmLen * 0.84f)
                              + (-zPhone) * 0.028f
                              + yPhone * 0.032f;

            handOffsetInPhoneSpace = Quaternion.Inverse(celLocalRotInHand) * (-celLocalPosInHand);

            Debug.Log($"[ProtagonistaIntegrator] GripAnatomico palmLen={palmLen:F4} m | celLocalPos={celLocalPosInHand:F4} (dist={celLocalPosInHand.magnitude:F4} m) celLocalEuler={celLocalRotInHand.eulerAngles:F1} | handOff={handOffsetInPhoneSpace:F4}");
        }
        else
        {
            Debug.LogWarning("[ProtagonistaIntegrator] No se encontraron huesos de dedos en RightHand; usando fallback de agarre.");
        }
        UnityEngine.Object.DestroyImmediate(sampleGo);

        // 6. Construir Prefab limpio Assets/Prefabs/Protagonista.prefab con Celular_Pro sujeto en mano derecha
        GameObject tempRoot = (GameObject)PrefabUtility.InstantiatePrefab(fbxPrefab);
        tempRoot.name = "Protagonista";

        var anim = tempRoot.GetComponent<Animator>() ?? tempRoot.AddComponent<Animator>();
        anim.avatar = avatar;
        anim.runtimeAnimatorController = controller;
        anim.applyRootMotion = false;
        anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;

        SkinnedMeshRenderer bodySmr = null;
        SkinnedMeshRenderer headSmr = null;
        foreach (var smr in tempRoot.GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            smr.sharedMaterial = mat;
            smr.updateWhenOffscreen = true;
            smr.reflectionProbeUsage = ReflectionProbeUsage.BlendProbes;
            if (smr.name.IndexOf("Cabeza", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                smr.gameObject.layer = Docencia2PlayerRigDriver.HeadMirrorOnlyLayer;
                smr.shadowCastingMode = ShadowCastingMode.On;
                headSmr = smr;
            }
            else
            {
                smr.gameObject.layer = 0;
                smr.shadowCastingMode = ShadowCastingMode.On;
                bodySmr = smr;
            }
        }

        Transform rightHandBone = anim.GetBoneTransform(HumanBodyBones.RightHand);
        Transform mountedPhone = null;
        Transform mountedFlashAnchor = null;
        if (rightHandBone != null && celularPrefab != null)
        {
            for (int i = rightHandBone.childCount - 1; i >= 0; i--)
            {
                var ch = rightHandBone.GetChild(i);
                if (ch.name.StartsWith("Celular", StringComparison.OrdinalIgnoreCase))
                    UnityEngine.Object.DestroyImmediate(ch.gameObject);
            }
            GameObject celGo = (GameObject)PrefabUtility.InstantiatePrefab(celularPrefab);
            celGo.name = "Celular_Pro";
            celGo.transform.SetParent(rightHandBone, false);
            celGo.transform.localPosition = celLocalPosInHand;
            celGo.transform.localRotation = celLocalRotInHand;
            celGo.transform.localScale = Vector3.one;
            mountedPhone = celGo.transform;
            mountedFlashAnchor = mountedPhone.Find("Flash_Anchor");
        }

        var driver = tempRoot.GetComponent<Docencia2PlayerRigDriver>() ?? tempRoot.AddComponent<Docencia2PlayerRigDriver>();
        driver.bodyRenderer = bodySmr;
        driver.headRenderer = headSmr;
        driver.phoneVisualMount = mountedPhone;
        driver.phoneFlashAnchor = mountedFlashAnchor;

        PrefabUtility.SaveAsPrefabAsset(tempRoot, PrefabPath);
        UnityEngine.Object.DestroyImmediate(tempRoot);

        // 7. Integrar sobre Jugador_PrimeraPersona_Docencia2 en Exterior_Docencia2.unity
        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        var player = UnityEngine.Object.FindAnyObjectByType<Docencia2FirstPersonPlayer>();
        if (player == null)
            throw new Exception("No se encontro Docencia2FirstPersonPlayer en Exterior_Docencia2.unity");

        player.standingHeight = 1.60f;
        player.crouchingHeight = 1.05f;
        player.standingCameraY = 1.48f;
        player.crouchingCameraY = 0.96f;
        player.isThirdPersonView = false;

        var cc = player.GetComponent<CharacterController>();
        if (cc != null)
        {
            cc.height = 1.60f;
            cc.radius = 0.30f;
            cc.center = new Vector3(0f, 0.80f, 0f);
            EditorUtility.SetDirty(cc);
        }

        if (player.playerCamera != null)
        {
            player.playerCamera.nearClipPlane = 0.04f;
            player.playerCamera.transform.localPosition = new Vector3(0f, 1.48f, 0.065f);
            player.playerCamera.cullingMask &= ~(1 << Docencia2PlayerRigDriver.HeadMirrorOnlyLayer);
            EditorUtility.SetDirty(player.playerCamera.transform);
            EditorUtility.SetDirty(player.playerCamera);
        }

        // Configurar phoneHandRoot como objetivo espacial de la mano derecha + Celular_Pro (ocultando mallas provisionales antiguas)
        Transform gripTarget = null;
        if (player.phoneHandRoot != null)
        {
            player.phoneHandRoot.localPosition = new Vector3(0.180f, -0.130f, 0.335f);
            player.phoneHandRoot.localRotation = Quaternion.Euler(4f, -14f, 3f);
            EditorUtility.SetDirty(player.phoneHandRoot);

            foreach (string legacyPart in new[]
            {
                "Brazo_Manga", "Mano_Palma_Sujecion", "Mano_Pulgar",
                "Celular_Cuerpo", "Celular_Pantalla", "Celular_ModuloCamara", "Celular_Flash_LED"
            })
            {
                Transform lt = player.phoneHandRoot.Find(legacyPart);
                if (lt != null)
                {
                    if (lt.GetComponentInChildren<Light>(true) != null)
                    {
                        foreach (var r in lt.GetComponentsInChildren<Renderer>(true))
                        {
                            r.enabled = false;
                            EditorUtility.SetDirty(r);
                        }
                    }
                    else
                    {
                        lt.gameObject.SetActive(false);
                        EditorUtility.SetDirty(lt.gameObject);
                    }
                }
            }

            gripTarget = player.phoneHandRoot.Find("PhoneGripTarget");
            if (gripTarget == null)
            {
                var gt = new GameObject("PhoneGripTarget");
                gt.transform.SetParent(player.phoneHandRoot, false);
                gripTarget = gt.transform;
            }
            gripTarget.localPosition = handOffsetInPhoneSpace;
            gripTarget.localRotation = Quaternion.identity;
            EditorUtility.SetDirty(gripTarget);
        }

        // Eliminar instancia previa de Protagonista si existe
        for (int i = player.transform.childCount - 1; i >= 0; i--)
        {
            Transform ch = player.transform.GetChild(i);
            if (ch.name.StartsWith("Protagonista", StringComparison.Ordinal))
                UnityEngine.Object.DestroyImmediate(ch.gameObject);
        }

        GameObject prefabAsset = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefabAsset, scene);
        instance.name = "Protagonista";
        instance.transform.SetParent(player.transform, false);
        instance.transform.localPosition = Vector3.zero;
        instance.transform.localRotation = Quaternion.identity;
        instance.transform.localScale = Vector3.one;

        var instDriver = instance.GetComponent<Docencia2PlayerRigDriver>() ?? instance.AddComponent<Docencia2PlayerRigDriver>();
        instDriver.player = player;
        instDriver.rightHandHoldTarget = gripTarget;
        instDriver.InitializeRefs();
        instDriver.SyncRendererVisibility(force: true);

        if (instDriver.phoneVisualMount != null)
        {
            foreach (var mr in instDriver.phoneVisualMount.GetComponentsInChildren<MeshRenderer>(true))
            {
                if (mr.name.IndexOf("Flash", StringComparison.OrdinalIgnoreCase) >= 0
                    || mr.name.IndexOf("LED", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    instDriver.phoneFlashLedRenderer = mr;
                    player.phoneFlashLedRenderer = mr;
                    Debug.Log($"[ProtagonistaIntegrator] Vinculado phoneFlashLedRenderer -> {mr.name}");
                    break;
                }
            }
        }

        player.rigDriver = instDriver;
        EditorUtility.SetDirty(instDriver);
        EditorUtility.SetDirty(player);

        // Medir altura real exacta por vertices (BakeMesh) del cuerpo completo en escena
        float minY = float.MaxValue;
        float maxY = float.MinValue;
        Mesh tempBaked = new Mesh();
        foreach (var r in instance.GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            if (r == null) continue;
            r.BakeMesh(tempBaked);
            var verts = tempBaked.vertices;
            Matrix4x4 l2w = r.transform.localToWorldMatrix;
            for (int vi = 0; vi < verts.Length; vi++)
            {
                float wy = l2w.MultiplyPoint3x4(verts[vi]).y;
                if (wy < minY) minY = wy;
                if (wy > maxY) maxY = wy;
            }
        }
        UnityEngine.Object.DestroyImmediate(tempBaked);
        float trueHeight = (maxY > minY) ? (maxY - minY) : 0f;

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);

        Debug.Log($"[ProtagonistaIntegrator] Integracion COMPLETA -> AlturaReal={trueHeight:F3} m (Y=[{minY:F3}, {maxY:F3}]), StandingCamY={player.standingCameraY:F2} m");
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

    private static void EnsureDir(string assetFolder)
    {
        if (!Directory.Exists(assetFolder))
            Directory.CreateDirectory(assetFolder);
    }
}
