using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.Recorder;
using UnityEditor.Recorder.Encoder;
using UnityEditor.Recorder.Input;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.Timeline;
using UnityEngine.UI;
using Unity.Cinemachine;
using Vaelith.Docencia2;

namespace Vaelith.Trailer.Editor
{
    /// <summary>
    /// Constructor y renderizador del borrador Paso 2 exclusivamente sobre Trailer_Docencia2.unity.
    /// Sin [MenuItem] para conservar VAELITH/ con un único elemento.
    /// </summary>
    public static class TrailerStep2Builder
    {
        public const string ApprovedScenePath = "Assets/Scenes/Exterior_Docencia2.unity";
        public const string TrailerScenePath = "Assets/Scenes/Trailer_Docencia2.unity";
        public const string TrailerRootDir = "Assets/Trailer";
        public const string ClipsDir = "Assets/Trailer/Clips";
        public const string TimelinePath = "Assets/Trailer/VAELITH_Trailer.playable";
        public const string TrailerRpPath = "Assets/Trailer/Trailer_PC_RPAsset.asset";
        public const string TrailerRendererPath = "Assets/Trailer/Trailer_PC_Renderer.asset";
        public const string TrailerProfilePath = "Assets/Trailer/Trailer_PostProcess_Profile.asset";
        public const string DustTexPath = "Assets/Trailer/Textures/Trailer_DustMote.png";
        public const string DustMatPath = "Assets/Trailer/Trailer_DustMat.mat";
        public const string BorradorOutDir = "Trailer_Borrador_540p";
        public const string UnityBaseOutDir = "Unity_Base";

        private static bool _isPlaySessionRunning = false;
        private static RecorderController _activeRecorder;
        private static PlayableDirector _playDirector;
        private static TrailerDirectorController _playController;
        private static TrailerHandPoseFixer _playHandFixer;
        private static string _borradorLogPath;
        private static readonly List<(double time, string label)> _keyframeSchedule = new List<(double, string)>();
        private static int _nextKeyframeIdx = 0;
        private static bool _keyframeCapturePending = false;
        private static DateTime _playSessionStartUtc;
        private static List<TrailerShotDefinition> _baseShots;
        private static int _baseShotIdx = 0;
        private static int _baseCooldownTicks = 0;
        private static string _baseOutDir = "";

        public static void BuildTrailerSceneAndTimeline()
        {
            Directory.CreateDirectory(TrailerRootDir);
            Directory.CreateDirectory(ClipsDir);
            Directory.CreateDirectory(TrailerRootDir + "/Textures");
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

            // 1. Clonar RPAsset y Renderer exclusivos de Trailer_Docencia2 (NUNCA modificar PC_RPAsset ni PC_Renderer)
            var trailerRp = BuildClonedTrailerUrpAssets();
            var trailerProfile = BuildTrailerPostProcessProfile();
            var dustMat = BuildDustParticleMaterial();

            // 2. Abrir Trailer_Docencia2.unity (jamás Exterior_Docencia2.unity)
            var scene = EditorSceneManager.OpenScene(TrailerScenePath, OpenSceneMode.Single);
            if (!scene.IsValid())
                throw new Exception("No se pudo abrir Assets/Scenes/Trailer_Docencia2.unity");

            // Limpiar __TRAILER_SYSTEM__ previo si existiera
            foreach (var root in scene.GetRootGameObjects())
            {
                if (root.name == "__TRAILER_SYSTEM__")
                    UnityEngine.Object.DestroyImmediate(root);
            }

            // Asegurar modo Terror en Trailer_Docencia2 (no guardado en Exterior_Docencia2)
            var switcher = UnityEngine.Object.FindAnyObjectByType<Docencia2ModeSwitcher>();
            if (switcher != null)
            {
                switcher.ApplyMode(Docencia2VisualMode.Terror, false);
            }

            var shots = TrailerShotCatalog.BuildCanon20Shots();

            // 3. Crear jerarquía __TRAILER_SYSTEM__
            var sysRoot = new GameObject("__TRAILER_SYSTEM__");
            var directorObj = new GameObject("Trailer_Director");
            directorObj.transform.SetParent(sysRoot.transform, false);

            var director = directorObj.AddComponent<PlayableDirector>();
            director.playOnAwake = true;
            director.extrapolationMode = DirectorWrapMode.Hold;
            director.timeUpdateMode = DirectorUpdateMode.GameTime;

            var ctrl = directorObj.AddComponent<TrailerDirectorController>();
            ctrl.playableDirector = director;
            ctrl.trailerQualityRpAsset = trailerRp;
            ctrl.shots = shots;

            var handFixer = directorObj.AddComponent<TrailerHandPoseFixer>();
            handFixer.directorController = ctrl;
            handFixer.writeDiagnosticLog = true;
            handFixer.diagnosticLogPath = Path.Combine(Path.GetFullPath(BorradorOutDir), "HandPose_Diag.txt");

            // 4. Cámara maestra con CinemachineBrain + SMAA High + HDR + PostProcessing (sin AudioListener ni MainCamera duplicados)
            foreach (var al in UnityEngine.Object.FindObjectsByType<AudioListener>(FindObjectsInactive.Include))
            {
                if (al != null) al.enabled = false;
            }
            foreach (var existingCam in UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsInactive.Include))
            {
                if (existingCam == null) continue;
                existingCam.enabled = false;
                if (existingCam.gameObject.CompareTag("MainCamera"))
                    existingCam.gameObject.tag = "Untagged";
            }

            var camGo = new GameObject("Trailer_MainCamera") { tag = "MainCamera" };
            camGo.transform.SetParent(sysRoot.transform, false);
            var mainCam = camGo.AddComponent<Camera>();
            mainCam.clearFlags = CameraClearFlags.Skybox;
            mainCam.nearClipPlane = 0.06f;
            mainCam.farClipPlane = 350f;
            mainCam.fieldOfView = 50f;
            mainCam.depth = 10f;
            mainCam.allowHDR = true;
            mainCam.allowMSAA = false;
            mainCam.cullingMask = ~(1 << Docencia2PlayerRigDriver.HeadMirrorOnlyLayer);

            var uacd = camGo.AddComponent<UniversalAdditionalCameraData>();
            uacd.renderPostProcessing = true;
            uacd.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            uacd.antialiasingQuality = AntialiasingQuality.High;

            var brain = camGo.AddComponent<CinemachineBrain>();
            brain.ShowDebugText = false;
            brain.ShowCameraFrustum = false;
            brain.UpdateMethod = CinemachineBrain.UpdateMethods.ManualUpdate;
            brain.DefaultBlend = new CinemachineBlendDefinition(CinemachineBlendDefinition.Styles.Cut, 0f);

            camGo.AddComponent<AudioListener>();
            ctrl.trailerMainCamera = mainCam;
            ctrl.cinemachineBrain = brain;

            // 5. Volumen de Post-Procesado exclusivo del tráiler
            var volGo = new GameObject("Trailer_PostProcess_Volume");
            volGo.transform.SetParent(sysRoot.transform, false);
            var vol = volGo.AddComponent<Volume>();
            vol.isGlobal = true;
            vol.priority = 50f;
            vol.weight = 1.0f;
            vol.sharedProfile = trailerProfile;
            ctrl.trailerPostProcessVolume = vol;

            // 6. Sistema de polvo interior sutil
            var dustGo = BuildInteriorDustParticleSystem(sysRoot.transform, dustMat);
            ctrl.interiorDustParticles = dustGo;

            // 7. Rig de luces de acento interiores (desactivado en todo exterior)
            var lightRigRoot = BuildInteriorAccentLightingRig(sysRoot.transform);
            ctrl.interiorAccentRigRoot = lightRigRoot;

            // 8. Canvas UI ScreenSpaceCamera para fade a negro y título T20 (visible por Unity Recorder CameraInput)
            var (fadeGroup, titleGroup) = BuildTrailerScreenSpaceUi(sysRoot.transform, mainCam);
            ctrl.fadeBlackCanvasGroup = fadeGroup;
            ctrl.titleCardCanvasGroup = titleGroup;

            // 9. Crear 20 CinemachineCameras + TimelineAsset con 20 pistas propias (una por toma)
            if (File.Exists(TimelinePath)) AssetDatabase.DeleteAsset(TimelinePath);
            var timeline = ScriptableObject.CreateInstance<TimelineAsset>();
            timeline.editorSettings.frameRate = 24.0;
            AssetDatabase.CreateAsset(timeline, TimelinePath);

            var camsParent = new GameObject("Trailer_Cameras");
            camsParent.transform.SetParent(sysRoot.transform, false);

            director.playableAsset = timeline;

            for (int i = 0; i < shots.Count; i++)
            {
                var s = shots[i];
                var vcamGo = new GameObject($"CM_{s.id}");
                vcamGo.transform.SetParent(camsParent.transform, false);
                vcamGo.transform.position = Vector3.zero;
                vcamGo.transform.rotation = Quaternion.identity;

                var vcamAnim = vcamGo.AddComponent<Animator>();
                vcamAnim.applyRootMotion = false;

                var vcam = vcamGo.AddComponent<CinemachineCamera>();
                vcam.Lens = new LensSettings
                {
                    FieldOfView = s.fovStart,
                    NearClipPlane = s.nearClip,
                    FarClipPlane = s.farClip,
                    Dutch = s.dutchAngle
                };

                // Cada toma en su propia pista (GroupTrack -> CinemachineTrack + AnimationTrack)
                var groupTrack = timeline.CreateTrack<GroupTrack>(null, s.trackTitle);
                var cmTrack = timeline.CreateTrack<CinemachineTrack>(groupTrack, $"{s.id}_Camara");
                cmTrack.TrackPriority = i + 1;
                director.SetGenericBinding(cmTrack, brain);

                var cmClip = cmTrack.CreateClip<CinemachineShot>();
                cmClip.start = s.startTime;
                cmClip.duration = s.duration;
                cmClip.displayName = s.id;
                var cmShotAsset = cmClip.asset as CinemachineShot;
                if (cmShotAsset != null)
                {
                    cmShotAsset.DisplayName = s.id;
                    cmShotAsset.VirtualCamera.exposedName = GUID.Generate().ToString();
                    director.SetReferenceValue(cmShotAsset.VirtualCamera.exposedName, vcam);
                }

                var animTrack = timeline.CreateTrack<AnimationTrack>(groupTrack, $"{s.id}_Recorrido");
                animTrack.trackOffset = TrackOffset.ApplyTransformOffsets;
                director.SetGenericBinding(animTrack, vcamAnim);
                var animClip = BuildCameraAnimationClip(s);
                string clipAssetPath = $"{ClipsDir}/Anim_{s.id}.anim";
                if (File.Exists(clipAssetPath)) AssetDatabase.DeleteAsset(clipAssetPath);
                AssetDatabase.CreateAsset(animClip, clipAssetPath);

                var animTimelineClip = animTrack.CreateClip(animClip);
                animTimelineClip.start = s.startTime;
                animTimelineClip.duration = s.duration;
                animTimelineClip.displayName = $"Ruta_{s.id}";
            }

            // 10. Pista de audio maestro sincronizada (voces canónicas PROVISIONAL/SUPUESTO + SFX + música final T20)
            const string masterWavAsset = "Assets/Trailer/Audio/VAELITH_Master_Audio.wav";
            if (File.Exists(masterWavAsset))
            {
                AssetDatabase.ImportAsset(masterWavAsset, ImportAssetOptions.ForceSynchronousImport);
                var masterClip = AssetDatabase.LoadAssetAtPath<AudioClip>(masterWavAsset);
                if (masterClip != null)
                {
                    var audioSource = directorObj.GetComponent<AudioSource>();
                    if (audioSource == null) audioSource = directorObj.AddComponent<AudioSource>();
                    audioSource.playOnAwake = false;
                    audioSource.spatialBlend = 0f;
                    audioSource.volume = 1.0f;

                    var audioTrack = timeline.CreateTrack<AudioTrack>(null, "Master_Audio_Voz_y_SFX");
                    director.SetGenericBinding(audioTrack, audioSource);
                    var audioTimelineClip = audioTrack.CreateClip(masterClip);
                    audioTimelineClip.start = 0.0;
                    audioTimelineClip.duration = masterClip.length;
                    audioTimelineClip.displayName = "VAELITH_Master_Audio";
                }
            }

            EditorUtility.SetDirty(timeline);
            EditorUtility.SetDirty(director);
            EditorUtility.SetDirty(ctrl);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, TrailerScenePath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

            Debug.Log($"[TrailerStep2Builder] Trailer_Docencia2.unity + Timeline (20 tomas en pistas individuales, 120 s @ 24 fps) guardados correctamente.");
        }

        private static AnimationClip BuildCameraAnimationClip(TrailerShotDefinition s)
        {
            var clip = new AnimationClip { name = $"Anim_{s.id}", frameRate = 24f };
            int steps = Mathf.Max(8, Mathf.RoundToInt((float)s.duration * 6f));
            var kx = new List<Keyframe>(steps + 1);
            var ky = new List<Keyframe>(steps + 1);
            var kz = new List<Keyframe>(steps + 1);
            var qx = new List<Keyframe>(steps + 1);
            var qy = new List<Keyframe>(steps + 1);
            var qz = new List<Keyframe>(steps + 1);
            var qw = new List<Keyframe>(steps + 1);

            for (int i = 0; i <= steps; i++)
            {
                float u = (float)i / steps;
                float tSec = u * (float)s.duration;
                Vector3 pos;
                Vector3 target;

                if (s.id == "T10")
                {
                    // T10: mirar espejo (0..3.45s), girar 170° suavemente (3.45..5.25s), sostener baño vacío (5.25..6.0s)
                    pos = Vector3.Lerp(s.camPosStart, s.camPosEnd, u);
                    if (tSec <= 3.45f)
                    {
                        target = s.lookTargetStart;
                    }
                    else if (tSec < 5.25f)
                    {
                        float turnU = Mathf.SmoothStep(0f, 1f, (tSec - 3.45f) / 1.80f);
                        Vector3 midTurnTarget = new Vector3(22.95f, 1.64f, 1.50f);
                        target = turnU < 0.5f
                            ? Vector3.Lerp(s.lookTargetStart, midTurnTarget, turnU * 2f)
                            : Vector3.Lerp(midTurnTarget, s.lookTargetEnd, (turnU - 0.5f) * 2f);
                    }
                    else
                    {
                        target = s.lookTargetEnd;
                    }
                }
                else
                {
                    float ease = s.isThirdPerson ? u : Mathf.SmoothStep(0f, 1f, u);
                    pos = s.useCamMid
                        ? QuadBezier(s.camPosStart, s.camPosMid, s.camPosEnd, ease)
                        : Vector3.Lerp(s.camPosStart, s.camPosEnd, ease);
                    target = s.useLookMid
                        ? QuadBezier(s.lookTargetStart, s.lookTargetMid, s.lookTargetEnd, ease)
                        : Vector3.Lerp(s.lookTargetStart, s.lookTargetEnd, ease);
                }

                // Micro-vibración orgánica discreta en tomas cámara en mano 1ª persona
                if (s.handheldPosAmp > 0f)
                {
                    pos.x += (Mathf.PerlinNoise(tSec * 1.4f, 2.3f) - 0.5f) * s.handheldPosAmp;
                    pos.y += (Mathf.PerlinNoise(tSec * 1.9f, 7.1f) - 0.5f) * s.handheldPosAmp;
                }

                Vector3 dir = (target - pos);
                if (dir.sqrMagnitude < 0.0001f) dir = Vector3.forward;
                Quaternion rot = Quaternion.LookRotation(dir.normalized, Vector3.up);
                if (s.handheldRotAmp > 0f)
                {
                    float rp = (Mathf.PerlinNoise(tSec * 1.6f, 11.4f) - 0.5f) * s.handheldRotAmp;
                    float ry = (Mathf.PerlinNoise(tSec * 1.3f, 17.8f) - 0.5f) * s.handheldRotAmp;
                    rot *= Quaternion.Euler(rp, ry, 0f);
                }

                kx.Add(new Keyframe(tSec, pos.x));
                ky.Add(new Keyframe(tSec, pos.y));
                kz.Add(new Keyframe(tSec, pos.z));
                qx.Add(new Keyframe(tSec, rot.x));
                qy.Add(new Keyframe(tSec, rot.y));
                qz.Add(new Keyframe(tSec, rot.z));
                qw.Add(new Keyframe(tSec, rot.w));
            }

            clip.SetCurve("", typeof(Transform), "localPosition.x", new AnimationCurve(kx.ToArray()));
            clip.SetCurve("", typeof(Transform), "localPosition.y", new AnimationCurve(ky.ToArray()));
            clip.SetCurve("", typeof(Transform), "localPosition.z", new AnimationCurve(kz.ToArray()));
            clip.SetCurve("", typeof(Transform), "localRotation.x", new AnimationCurve(qx.ToArray()));
            clip.SetCurve("", typeof(Transform), "localRotation.y", new AnimationCurve(qy.ToArray()));
            clip.SetCurve("", typeof(Transform), "localRotation.z", new AnimationCurve(qz.ToArray()));
            clip.SetCurve("", typeof(Transform), "localRotation.w", new AnimationCurve(qw.ToArray()));
            clip.EnsureQuaternionContinuity();
            return clip;
        }

        private static Vector3 QuadBezier(Vector3 a, Vector3 b, Vector3 c, float t)
        {
            float omt = 1f - t;
            return omt * omt * a + 2f * omt * t * b + t * t * c;
        }

        private static UniversalRenderPipelineAsset BuildClonedTrailerUrpAssets()
        {
            const string srcRp = "Assets/Settings/PC_RPAsset.asset";
            const string srcRend = "Assets/Settings/PC_Renderer.asset";

            if (File.Exists(TrailerRendererPath)) AssetDatabase.DeleteAsset(TrailerRendererPath);
            if (File.Exists(TrailerRpPath)) AssetDatabase.DeleteAsset(TrailerRpPath);

            AssetDatabase.CopyAsset(srcRend, TrailerRendererPath);
            AssetDatabase.CopyAsset(srcRp, TrailerRpPath);
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

            var rend = AssetDatabase.LoadAssetAtPath<ScriptableRendererData>(TrailerRendererPath);
            var rp = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(TrailerRpPath);
            if (rp != null)
            {
                var so = new SerializedObject(rp);
                if (rend != null)
                {
                    var listProp = so.FindProperty("m_RendererDataList");
                    if (listProp != null && listProp.arraySize > 0)
                        listProp.GetArrayElementAtIndex(0).objectReferenceValue = rend;
                }
                SetIntProp(so, "m_MainLightShadowmapResolution", 4096);
                SetIntProp(so, "m_AdditionalLightsShadowmapResolution", 8192);
                SetIntProp(so, "m_AdditionalLightsShadowResolutionTierHigh", 2048);
                SetIntProp(so, "m_AdditionalLightsShadowResolutionTierMedium", 1024);
                SetIntProp(so, "m_SoftShadowQuality", 3);
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(rp);
            }
            return rp;
        }

        private static void SetIntProp(SerializedObject so, string name, int val)
        {
            var p = so.FindProperty(name);
            if (p != null) p.intValue = val;
        }

        private static VolumeProfile BuildTrailerPostProcessProfile()
        {
            if (File.Exists(TrailerProfilePath)) AssetDatabase.DeleteAsset(TrailerProfilePath);
            var prof = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(prof, TrailerProfilePath);

            var tone = prof.Add<Tonemapping>(true);
            tone.mode.Override(TonemappingMode.ACES);

            var col = prof.Add<ColorAdjustments>(true);
            // No sobrescribir postExposure global: hereda 1.18 exacto de Global_Volume_Terror (exterior fiel)
            col.postExposure.overrideState = false;
            col.contrast.Override(14f);
            col.saturation.Override(-22f);
            col.colorFilter.Override(new Color(0.95f, 0.98f, 1.0f, 1f));

            var split = prof.Add<SplitToning>(true);
            split.shadows.Override(new Color(0.14f, 0.24f, 0.26f, 1f));
            split.highlights.Override(new Color(0.95f, 0.78f, 0.54f, 1f));
            split.balance.Override(-8f);

            var bloom = prof.Add<Bloom>(true);
            bloom.threshold.Override(1.22f);
            bloom.intensity.Override(0.24f);
            bloom.scatter.Override(0.62f);

            var vig = prof.Add<Vignette>(true);
            vig.intensity.Override(0.28f);
            vig.smoothness.Override(0.48f);

            var grain = prof.Add<FilmGrain>(true);
            grain.type.Override(FilmGrainLookup.Thin1);
            grain.intensity.Override(0.12f);
            grain.response.Override(0.8f);

            var mb = prof.Add<MotionBlur>(true);
            mb.mode.Override(MotionBlurMode.CameraOnly);
            mb.quality.Override(MotionBlurQuality.Medium);
            mb.intensity.Override(0.16f);
            mb.clamp.Override(0.03f);

            var dof = prof.Add<DepthOfField>(true);
            dof.active = false;

            EditorUtility.SetDirty(prof);
            return prof;
        }

        private static Material BuildDustParticleMaterial()
        {
            var tex = new Texture2D(64, 64, TextureFormat.RGBA32, false);
            for (int y = 0; y < 64; y++)
            {
                for (int x = 0; x < 64; x++)
                {
                    float dx = (x - 31.5f) / 31.5f;
                    float dy = (y - 31.5f) / 31.5f;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    float a = Mathf.Pow(Mathf.Clamp01(1f - d), 2.4f);
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                }
            }
            tex.Apply();
            File.WriteAllBytes(DustTexPath, tex.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(DustTexPath, ImportAssetOptions.ForceSynchronousImport);
            var importedTex = AssetDatabase.LoadAssetAtPath<Texture2D>(DustTexPath);

            Shader sh = Shader.Find("Universal Render Pipeline/Particles/Unlit") ?? Shader.Find("Particles/Standard Unlit") ?? Shader.Find("Sprites/Default");
            var mat = new Material(sh) { name = "Trailer_DustMat" };
            if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", importedTex);
            else mat.mainTexture = importedTex;
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", new Color(0.82f, 0.90f, 0.96f, 0.08f));
            if (mat.HasProperty("_Surface")) mat.SetFloat("_Surface", 1f);
            if (mat.HasProperty("_Blend")) mat.SetFloat("_Blend", 2f);
            mat.renderQueue = 3200;

            if (File.Exists(DustMatPath)) AssetDatabase.DeleteAsset(DustMatPath);
            AssetDatabase.CreateAsset(mat, DustMatPath);
            return AssetDatabase.LoadAssetAtPath<Material>(DustMatPath);
        }

        private static ParticleSystem BuildInteriorDustParticleSystem(Transform parent, Material mat)
        {
            var go = new GameObject("Trailer_DustParticles");
            go.transform.SetParent(parent, false);
            var ps = go.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.loop = true;
            main.startLifetime = 4.5f;
            main.startSpeed = 0.06f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.008f, 0.022f);
            main.startColor = new Color(0.78f, 0.88f, 0.94f, 0.07f);
            main.maxParticles = 90;
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            var emit = ps.emission;
            emit.rateOverTime = 20f;

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(3.6f, 2.2f, 4.5f);

            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
            return ps;
        }

        private static Transform BuildInteriorAccentLightingRig(Transform parent)
        {
            var root = new GameObject("Trailer_ShotLightingRig");
            root.transform.SetParent(parent, false);

            Light MakeLight(Transform grp, string name, LightType type, Vector3 pos, Vector3 euler, Color col, float intensity, float range, float spotAngle, bool shadows)
            {
                var g = new GameObject(name);
                g.transform.SetParent(grp, false);
                g.transform.position = pos;
                g.transform.rotation = Quaternion.Euler(euler);
                var l = g.AddComponent<Light>();
                l.type = type;
                l.color = col;
                l.intensity = intensity;
                l.range = range;
                l.spotAngle = spotAngle;
                l.innerSpotAngle = spotAngle * 0.45f;
                l.shadows = shadows ? LightShadows.Soft : LightShadows.None;
                l.shadowStrength = 0.88f;
                l.cullingMask = ~0;
                var u = g.AddComponent<UniversalAdditionalLightData>();
                u.renderingLayers = uint.MaxValue;
                u.softShadowQuality = SoftShadowQuality.High;
                l.enabled = false;
                return l;
            }

            Transform Grp(int id)
            {
                var g = new GameObject($"Group_{id}");
                g.transform.SetParent(root.transform, false);
                return g.transform;
            }

            // Group 1: Pasillo Central PB (T08, T11)
            var g1 = Grp(1);
            MakeLight(g1, "G1_Corridor_Emergency", LightType.Spot, new Vector3(13.0f, 3.00f, 0.0f), new Vector3(58f, 270f, 0f), new Color(1.0f, 0.66f, 0.30f), 11f, 18f, 92f, true);
            MakeLight(g1, "G1_Corridor_MoonFill", LightType.Point, new Vector3(18.5f, 2.75f, 0.0f), Vector3.zero, new Color(0.55f, 0.78f, 0.88f), 3.6f, 11f, 60f, true);

            // Group 2: Baño Mujeres PB (T09, T10 — refleja a Jeid + La Testigo en espejo real)
            var g2 = Grp(2);
            MakeLight(g2, "G2_Bano_Fluorescent", LightType.Point, new Vector3(21.79f, 2.95f, 2.15f), Vector3.zero, new Color(0.76f, 0.90f, 0.92f), 6.0f, 8.0f, 60f, true);
            MakeLight(g2, "G2_Bano_MirrorFaceRim", LightType.Point, new Vector3(21.79f, 2.15f, 0.85f), Vector3.zero, new Color(0.84f, 0.94f, 0.96f), 2.2f, 3.8f, 60f, false);

            // Group 3: Escalera en U PB (T12 hasta descanso Y=1.90)
            var g3 = Grp(3);
            MakeLight(g3, "G3_Stair_LandingAmber", LightType.Spot, new Vector3(8.50f, 3.30f, 10.60f), new Vector3(56f, 180f, 0f), new Color(1.0f, 0.68f, 0.32f), 14f, 13f, 84f, true);
            MakeLight(g3, "G3_Stair_BaseCool", LightType.Point, new Vector3(8.80f, 2.60f, 5.40f), Vector3.zero, new Color(0.52f, 0.74f, 0.86f), 4.2f, 8.5f, 60f, true);

            Vector3 Aim(Vector3 from, Vector3 to) => Quaternion.LookRotation((to - from).normalized, Vector3.up).eulerAngles;

            // Group 4: Aula 3 PB (T13 pupitres originales F5_Pupitre)
            var g4 = Grp(4);
            Vector3 g4MoonPos = TrailerShotCatalog.Plan(16.8f, 2.65f, 52.6f);
            Vector3 g4MoonTarget = TrailerShotCatalog.Plan(14.2f, 0.85f, 49.0f);
            MakeLight(g4, "G4_Aula3_WindowMoon", LightType.Spot, g4MoonPos, Aim(g4MoonPos, g4MoonTarget), new Color(0.62f, 0.80f, 0.95f), 34f, 18f, 105f, true);
            MakeLight(g4, "G4_Aula3_DeskFill", LightType.Point, TrailerShotCatalog.Plan(14.5f, 2.75f, 49.8f), Vector3.zero, new Color(0.54f, 0.72f, 0.86f), 2.6f, 12f, 60f, false);
            MakeLight(g4, "G4_Aula3_DoorAmber", LightType.Point, TrailerShotCatalog.Plan(10.8f, 2.55f, 47.8f), Vector3.zero, new Color(0.95f, 0.64f, 0.30f), 4.2f, 9.5f, 60f, false);

            // Group 5: Auditorio PB (T14 80 butacas)
            var g5 = Grp(5);
            MakeLight(g5, "G5_Auditorio_StageMoon", LightType.Spot, TrailerShotCatalog.Plan(24.5f, 2.95f, 35.8f), new Vector3(36f, 315f, 0f), new Color(0.64f, 0.82f, 0.96f), 28f, 22f, 102f, true);
            MakeLight(g5, "G5_Auditorio_AisleAmber", LightType.Point, TrailerShotCatalog.Plan(19.0f, 2.85f, 26.5f), Vector3.zero, new Color(1.0f, 0.68f, 0.34f), 4.8f, 12f, 60f, false);

            // Group 6: Plaza Sur PB (T15 apagón en cascada de 3 luces)
            var g6 = Grp(6);
            MakeLight(g6, "G6_Plaza_Lamp1", LightType.Spot, new Vector3(-11.0f, 3.00f, 0.0f), new Vector3(90f, 0f, 0f), new Color(1.0f, 0.68f, 0.32f), 11f, 10f, 88f, true);
            MakeLight(g6, "G6_Plaza_Lamp2", LightType.Spot, new Vector3(-14.5f, 3.00f, 0.0f), new Vector3(90f, 0f, 0f), new Color(1.0f, 0.68f, 0.32f), 11f, 10f, 88f, true);
            MakeLight(g6, "G6_Plaza_Lamp3", LightType.Spot, new Vector3(-18.2f, 3.00f, 0.0f), new Vector3(90f, 0f, 0f), new Color(1.0f, 0.68f, 0.32f), 11f, 10f, 88f, true);
            MakeLight(g6, "G6_Plaza_BaseMoon", LightType.Point, new Vector3(-13.5f, 2.75f, 2.0f), Vector3.zero, new Color(0.46f, 0.68f, 0.82f), 2.4f, 12f, 60f, false);

            // Group 7: Clímax Pasillo Central PB con La Testigo (T16–T19)
            var g7 = Grp(7);
            MakeLight(g7, "G7_Testigo_BackAmber", LightType.Spot, new Vector3(-1.20f, 3.00f, 0.00f), new Vector3(68f, 90f, 0f), new Color(1.0f, 0.62f, 0.24f), 9.0f, 11f, 72f, true);
            MakeLight(g7, "G7_Corridor_DimCool", LightType.Point, new Vector3(5.80f, 2.80f, 0.00f), Vector3.zero, new Color(0.55f, 0.78f, 0.90f), 1.4f, 11f, 60f, false);

            // Relleno mínimo de pantalla OLED solo para T01
            var fillGo = new GameObject("ClosePhoneScreenFill");
            fillGo.transform.SetParent(root.transform, false);
            var fl = fillGo.AddComponent<Light>();
            fl.type = LightType.Point;
            fl.color = new Color(0.72f, 0.88f, 1.0f);
            fl.intensity = 0.018f;
            fl.range = 0.75f;
            fl.shadows = LightShadows.None;
            fl.cullingMask = ~0;
            fl.enabled = false;

            return root.transform;
        }

        private static (CanvasGroup fadeBlack, CanvasGroup titleCard) BuildTrailerScreenSpaceUi(Transform parent, Camera cam)
        {
            var uiRoot = new GameObject("Trailer_UI_Canvas");
            uiRoot.transform.SetParent(parent, false);
            var canvas = uiRoot.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = cam;
            canvas.planeDistance = 0.8f;
            canvas.sortingOrder = 100;

            var scaler = uiRoot.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            uiRoot.AddComponent<GraphicRaycaster>();

            CanvasGroup CreateFullOverlay(string name, bool blackBg)
            {
                var g = new GameObject(name);
                g.transform.SetParent(uiRoot.transform, false);
                var rt = g.AddComponent<RectTransform>();
                rt.anchorMin = Vector2.zero;
                rt.anchorMax = Vector2.one;
                rt.offsetMin = Vector2.zero;
                rt.offsetMax = Vector2.zero;
                if (blackBg)
                {
                    var img = g.AddComponent<Image>();
                    img.color = Color.black;
                }
                var cg = g.AddComponent<CanvasGroup>();
                cg.alpha = 0f;
                return cg;
            }

            var fadeBlack = CreateFullOverlay("FadeBlackOverlay", true);
            var titleGroup = CreateFullOverlay("TitleCardOverlay", false);

            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (font == null) font = Resources.GetBuiltinResource<Font>("Arial.ttf");

            void AddLabel(string name, string text, int size, Vector2 pos, float alpha)
            {
                var go = new GameObject(name);
                go.transform.SetParent(titleGroup.transform, false);
                var rt = go.AddComponent<RectTransform>();
                rt.anchorMin = new Vector2(0.5f, 0.5f);
                rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.sizeDelta = new Vector2(1400f, 160f);
                rt.anchoredPosition = pos;
                var txt = go.AddComponent<Text>();
                txt.text = text;
                txt.font = font;
                txt.fontSize = size;
                txt.alignment = TextAnchor.MiddleCenter;
                txt.color = new Color(0.94f, 0.96f, 0.98f, alpha);
            }

            AddLabel("Title_Main", "V A E L I T H", 112, new Vector2(0f, 65f), 0.96f);
            AddLabel("Title_Sub", "A F T E R   Y O U   W A K E", 30, new Vector2(0f, -20f), 0.72f);
            AddLabel("Title_Soon", "P R Ó X I M A M E N T E", 26, new Vector2(0f, -155f), 0.65f);

            return (fadeBlack, titleGroup);
        }

        // =========================================================================
        // AUDITORÍA GEOMÉTRICA Y RENDER DEL BORRADOR 960 x 540 (PARA EN PASO 2)
        // =========================================================================
        public static void RunAuditAndRenderBorrador540p()
        {
            BuildTrailerSceneAndTimeline();

            var scene = EditorSceneManager.OpenScene(TrailerScenePath, OpenSceneMode.Single);
            string fullOutDir = Path.GetFullPath(BorradorOutDir);
            string kfDir = Path.Combine(fullOutDir, "Keyframes");
            Directory.CreateDirectory(fullOutDir);
            Directory.CreateDirectory(kfDir);

            // 1. Verificar SHA-256 de Exterior_Docencia2.unity sigue 100 % intacto
            var shots = TrailerShotCatalog.BuildCanon20Shots();
            RunGeometryDefectAudit(shots, Path.Combine(fullOutDir, "Borrador_Audit_Report.txt"));

            // 2. Renderizar borrador continuo 960 x 540 @ 24 fps con Unity Recorder en Play Mode
            _borradorLogPath = Path.Combine(fullOutDir, "Borrador_Audit_Report.txt");
            _keyframeSchedule.Clear();
            foreach (var s in shots)
            {
                _keyframeSchedule.Add((s.startTime + 0.15, $"{s.id}_0_Start"));
                if (s.id == "T10")
                {
                    _keyframeSchedule.Add((s.startTime + 1.20, "T10_1_EspejoSolo"));
                    _keyframeSchedule.Add((s.startTime + 2.65, "T10_2_EspejoLaTestigo"));
                    _keyframeSchedule.Add((s.startTime + 5.45, "T10_3_GiroVacio"));
                }
                else if (s.id == "T17")
                {
                    _keyframeSchedule.Add((s.startTime + 1.40, "T17_1_AntesApagon"));
                    _keyframeSchedule.Add((s.startTime + 2.28, "T17_2_DuranteApagon"));
                    _keyframeSchedule.Add((s.startTime + 3.60, "T17_3_DespuesAvance3m"));
                }
                else
                {
                    _keyframeSchedule.Add((s.startTime + s.duration * 0.50, $"{s.id}_1_Mid"));
                    _keyframeSchedule.Add((s.startTime + s.duration - 0.15, $"{s.id}_2_End"));
                }
            }
            _nextKeyframeIdx = 0;
            _keyframeCapturePending = false;

            EditorApplication.playModeStateChanged -= OnPlayModeChangedForBorrador;
            EditorApplication.playModeStateChanged += OnPlayModeChangedForBorrador;
            EditorApplication.EnterPlaymode();
        }

        public static void RenderUnityBase1080p()
        {
            BuildTrailerSceneAndTimeline();

            EditorSceneManager.OpenScene(TrailerScenePath, OpenSceneMode.Single);
            string fullOutDir = Path.GetFullPath(UnityBaseOutDir);
            Directory.CreateDirectory(fullOutDir);
            foreach (var oldMp4 in Directory.GetFiles(fullOutDir, "*.mp4"))
            {
                try { File.Delete(oldMp4); } catch { }
            }

            var shots = TrailerShotCatalog.BuildCanon20Shots();
            int defects = RunGeometryDefectAudit(shots, Path.Combine(fullOutDir, "Unity_Base_Audit_Report.txt"));
            if (defects > 0)
            {
                throw new Exception($"Abortado RenderUnityBase1080p: se detectaron {defects} defectos en Unity_Base_Audit_Report.txt.");
            }

            EditorApplication.playModeStateChanged -= OnPlayModeChangedForUnityBase;
            EditorApplication.playModeStateChanged += OnPlayModeChangedForUnityBase;
            EditorApplication.EnterPlaymode();
        }

        private static RecorderController CreateShotRecorder(
            string recName,
            string outputFileNoExt,
            int width,
            int height,
            CoreEncoderSettings.VideoEncodingQuality quality,
            int startFrame,
            int endFrame)
        {
            var ctrlSettings = ScriptableObject.CreateInstance<RecorderControllerSettings>();
            ctrlSettings.FrameRatePlayback = FrameRatePlayback.Constant;
            ctrlSettings.FrameRate = 24.0f;
            ctrlSettings.CapFrameRate = false;
            ctrlSettings.ExitPlayMode = false;
            ctrlSettings.SetRecordModeToFrameInterval(Mathf.Max(0, startFrame), Mathf.Max(startFrame, endFrame));

            var movieSettings = ScriptableObject.CreateInstance<MovieRecorderSettings>();
            movieSettings.name = recName;
            movieSettings.Enabled = true;
            movieSettings.CaptureAudio = false;
            movieSettings.FrameRate = 24.0f;
            movieSettings.OutputFile = outputFileNoExt;

            var camInput = new CameraInputSettings
            {
                Source = ImageSource.MainCamera,
                OutputWidth = width,
                OutputHeight = height,
                CaptureUI = false
            };
            movieSettings.ImageInputSettings = camInput;
            movieSettings.EncoderSettings = new CoreEncoderSettings
            {
                Codec = CoreEncoderSettings.OutputCodec.MP4,
                EncodingQuality = quality
            };

            ctrlSettings.AddRecorderSettings(movieSettings);
            return new RecorderController(ctrlSettings);
        }

        private static void OnPlayModeChangedForBorrador(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.EnteredPlayMode)
            {
                try
                {
                    _isPlaySessionRunning = true;
                    _playSessionStartUtc = DateTime.UtcNow;
                    _playDirector = UnityEngine.Object.FindAnyObjectByType<PlayableDirector>();
                    _playController = UnityEngine.Object.FindAnyObjectByType<TrailerDirectorController>();
                    _playHandFixer = UnityEngine.Object.FindAnyObjectByType<TrailerHandPoseFixer>();

                    _activeRecorder = CreateShotRecorder(
                        "VAELITH_Borrador_540p",
                        Path.Combine(Path.GetFullPath(BorradorOutDir), "VAELITH_Borrador_960x540"),
                        960,
                        540,
                        CoreEncoderSettings.VideoEncodingQuality.Medium,
                        0,
                        2879);
                    _activeRecorder.PrepareRecording();
                    bool started = _activeRecorder.StartRecording();
                    if (!started)
                        throw new Exception("RecorderController.StartRecording devolvio false.");

                    EditorApplication.update -= TickBorradorPlayMode;
                    EditorApplication.update += TickBorradorPlayMode;
                    Debug.Log("[TrailerStep2Builder] Grabación borrador 960x540 @ 24 fps iniciada (2880 cuadros)...");
                }
                catch (Exception ex)
                {
                    Debug.LogError("[TrailerStep2Builder] Error iniciando borrador 540p: " + ex);
                    _isPlaySessionRunning = false;
                    EditorApplication.update -= TickBorradorPlayMode;
                    EditorApplication.playModeStateChanged -= OnPlayModeChangedForBorrador;
                    if (Application.isBatchMode) EditorApplication.Exit(1);
                }
            }
            else if (state == PlayModeStateChange.EnteredEditMode && _isPlaySessionRunning)
            {
                _isPlaySessionRunning = false;
                EditorApplication.update -= TickBorradorPlayMode;
                EditorApplication.playModeStateChanged -= OnPlayModeChangedForBorrador;
                Debug.Log("[TrailerStep2Builder] Borrador 960x540 finalizado. Saliendo limpiamente...");
                if (Application.isBatchMode)
                {
                    EditorApplication.Exit(0);
                }
            }
        }

        private static void TickBorradorPlayMode()
        {
            if (!_isPlaySessionRunning || !Application.isPlaying) return;

            if ((DateTime.UtcNow - _playSessionStartUtc).TotalMinutes > 7.0)
            {
                Debug.LogError("[TrailerStep2Builder] Timeout de seguridad (> 7 min) en borrador 540p.");
                try { _activeRecorder?.StopRecording(); } catch { }
                _playHandFixer?.FlushDiagLog();
                _isPlaySessionRunning = false;
                EditorApplication.update -= TickBorradorPlayMode;
                EditorApplication.ExitPlaymode();
                if (Application.isBatchMode) EditorApplication.Exit(1);
                return;
            }

            if (_playDirector != null && _playController != null && _playController.trailerMainCamera != null && !_keyframeCapturePending)
            {
                double t = _playDirector.time;
                if (_nextKeyframeIdx < _keyframeSchedule.Count && t >= _keyframeSchedule[_nextKeyframeIdx].time)
                {
                    var item = _keyframeSchedule[_nextKeyframeIdx];
                    _nextKeyframeIdx++;
                    _keyframeCapturePending = true;
                    string kfPath = Path.Combine(Path.GetFullPath(BorradorOutDir), "Keyframes", item.label + ".png");
                    _playController.RunAtEndOfFrame(() =>
                    {
                        try
                        {
                            if (_playController != null && _playController.trailerMainCamera != null)
                            {
                                CaptureCameraPng(_playController.trailerMainCamera, 960, 540, kfPath);
                            }
                        }
                        finally
                        {
                            _keyframeCapturePending = false;
                        }
                    });
                }
            }

            if (_activeRecorder != null && !_activeRecorder.IsRecording())
            {
                _activeRecorder.StopRecording();
                _activeRecorder = null;
                _playHandFixer?.FlushDiagLog();
                EditorApplication.update -= TickBorradorPlayMode;
                EditorApplication.ExitPlaymode();
            }
        }

        private static void OnPlayModeChangedForUnityBase(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.EnteredPlayMode)
            {
                try
                {
                    _isPlaySessionRunning = true;
                    _playSessionStartUtc = DateTime.UtcNow;
                    _playDirector = UnityEngine.Object.FindAnyObjectByType<PlayableDirector>();
                    _playController = UnityEngine.Object.FindAnyObjectByType<TrailerDirectorController>();
                    _playHandFixer = UnityEngine.Object.FindAnyObjectByType<TrailerHandPoseFixer>();

                    _baseOutDir = Path.GetFullPath(UnityBaseOutDir);
                    if (_playHandFixer != null)
                    {
                        _playHandFixer.writeDiagnosticLog = true;
                        _playHandFixer.diagnosticLogPath = Path.Combine(_baseOutDir, "HandPose_1080p_Diag.txt");
                    }

                    if (_playDirector != null)
                    {
                        _playDirector.timeUpdateMode = DirectorUpdateMode.Manual;
                        _playDirector.Play();
                    }

                    _baseShots = TrailerShotCatalog.BuildCanon20Shots();
                    _baseShotIdx = 0;
                    _baseCooldownTicks = 0;

                    StartNextUnityBaseShot();
                    EditorApplication.update -= TickUnityBasePlayMode;
                    EditorApplication.update += TickUnityBasePlayMode;
                }
                catch (Exception ex)
                {
                    Debug.LogError("[TrailerStep2Builder] Error iniciando Unity_Base 1080p: " + ex);
                    _isPlaySessionRunning = false;
                    EditorApplication.update -= TickUnityBasePlayMode;
                    EditorApplication.playModeStateChanged -= OnPlayModeChangedForUnityBase;
                    if (Application.isBatchMode) EditorApplication.Exit(1);
                }
            }
            else if (state == PlayModeStateChange.EnteredEditMode && _isPlaySessionRunning)
            {
                _isPlaySessionRunning = false;
                EditorApplication.update -= TickUnityBasePlayMode;
                EditorApplication.playModeStateChanged -= OnPlayModeChangedForUnityBase;
                Debug.Log("[TrailerStep2Builder] Unity_Base 1920x1080 @ 24 fps finalizado. Saliendo limpiamente...");
                if (Application.isBatchMode)
                {
                    EditorApplication.Exit(0);
                }
            }
        }

        private static void StartNextUnityBaseShot()
        {
            if (_baseShots == null || _baseShotIdx >= _baseShots.Count)
            {
                _playController?.StopSteppedShot();
                _playHandFixer?.FlushDiagLog();
                EditorApplication.update -= TickUnityBasePlayMode;
                EditorApplication.ExitPlaymode();
                return;
            }

            var s = _baseShots[_baseShotIdx];
            const int warmupFrames = 3;
            int totalFrames = Mathf.Max(1, Mathf.RoundToInt((float)s.duration * 24f));
            _playController.BeginSteppedShot(s.startTime, s.duration, warmupFrames);

            string outNoExt = Path.Combine(_baseOutDir, s.trackTitle);
            _activeRecorder = CreateShotRecorder(
                s.id,
                outNoExt,
                1920,
                1080,
                CoreEncoderSettings.VideoEncodingQuality.High,
                warmupFrames,
                warmupFrames + totalFrames - 1);
            _activeRecorder.PrepareRecording();
            if (!_activeRecorder.StartRecording())
                throw new Exception($"No se pudo iniciar Recorder para toma {s.id}");

            Debug.Log($"[TrailerStep2Builder] Iniciando toma {_baseShotIdx + 1}/{_baseShots.Count}: {s.trackTitle} ({totalFrames} cuadros @ 1920x1080, 24 fps)...");
        }

        private static void TickUnityBasePlayMode()
        {
            if (!_isPlaySessionRunning || !Application.isPlaying) return;

            if ((DateTime.UtcNow - _playSessionStartUtc).TotalMinutes > 12.0)
            {
                Debug.LogError("[TrailerStep2Builder] Timeout de seguridad (> 12 min) en Unity_Base 1080p.");
                try { _activeRecorder?.StopRecording(); } catch { }
                _activeRecorder = null;
                _playController?.StopSteppedShot();
                _playHandFixer?.FlushDiagLog();
                _isPlaySessionRunning = false;
                EditorApplication.update -= TickUnityBasePlayMode;
                EditorApplication.ExitPlaymode();
                if (Application.isBatchMode) EditorApplication.Exit(1);
                return;
            }

            if (_baseCooldownTicks > 0)
            {
                _baseCooldownTicks--;
                if (_baseCooldownTicks == 0)
                {
                    StartNextUnityBaseShot();
                }
                return;
            }

            if (_activeRecorder != null && !_activeRecorder.IsRecording())
            {
                var finishedShot = _baseShots[_baseShotIdx];
                _activeRecorder.StopRecording();
                _activeRecorder = null;
                _baseShotIdx++;
                _baseCooldownTicks = 2;
                Debug.Log($"[TrailerStep2Builder] Toma {finishedShot.id} finalizada ({_baseShotIdx}/{_baseShots.Count}).");
            }
        }

        private static void CaptureCameraPng(Camera cam, int width, int height, string path)
        {
            if (cam == null) return;
            var prevRt = cam.targetTexture;
            var prevActive = RenderTexture.active;
            var rt = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32) { antiAliasing = 1 };
            try
            {
                cam.targetTexture = rt;
                cam.Render();
                RenderTexture.active = rt;
                var tex = new Texture2D(width, height, TextureFormat.RGB24, false);
                tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                tex.Apply();
                File.WriteAllBytes(path, tex.EncodeToPNG());
                UnityEngine.Object.DestroyImmediate(tex);
            }
            finally
            {
                cam.targetTexture = prevRt;
                RenderTexture.active = prevActive;
                UnityEngine.Object.DestroyImmediate(rt);
            }
        }

        private static int RunGeometryDefectAudit(List<TrailerShotDefinition> shots, string outReportPath)
        {
            var sb = new StringBuilder();
            sb.AppendLine("=== REPORTE AUTOMÁTICO DE DEFECTOS GEOMÉTRICOS (PASO 2 BORRADOR 960x540) ===");
            sb.AppendLine($"Fecha: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");

            var animTracks = new Dictionary<string, AnimationClip>();
            foreach (var s in shots)
            {
                var c = AssetDatabase.LoadAssetAtPath<AnimationClip>($"{ClipsDir}/Anim_{s.id}.anim");
                if (c != null) animTracks[s.id] = c;
            }

            int totalDefects = 0;
            var player = UnityEngine.Object.FindAnyObjectByType<Docencia2FirstPersonPlayer>();
            Transform playerRoot = player != null ? player.transform : null;

            foreach (var s in shots)
            {
                if (s.category == TrailerShotCategory.BlackTitleCard)
                {
                    sb.AppendLine($"[OK] {s.id}: Pantalla negra de título 2D (sin geometría).");
                    continue;
                }

                var anim = animTracks.ContainsKey(s.id) ? animTracks[s.id] : null;
                var go = new GameObject("__AUDIT_PROBE__");
                Vector3 prevPos = Vector3.zero;
                bool hasPrev = false;
                int wallHits = 0;
                int hairRuleViolations = 0;
                int paViolations = 0;
                int layer28Error = 0;
                float minDistFound = float.MaxValue;
                string firstHitDetail = "";

                // Verificar Layer 28 y ausencia de luces interiores en exterior
                bool expectedLayer28 = (s.id == "T06" || s.id == "T11");
                if (s.includeLayer28 != expectedLayer28) layer28Error++;

                bool isExterior = s.category == TrailerShotCategory.ArchitecturalExterior || (s.category == TrailerShotCategory.ThirdPersonJeid && !s.isIndoorFog);
                bool extLightViolation = isExterior && s.interiorAccentId != 0;

                const int samples = 20;
                for (int k = 0; k <= samples; k++)
                {
                    float tSec = ((float)k / samples) * (float)s.duration;
                    if (anim != null) anim.SampleAnimation(go, tSec);
                    Vector3 pos = go.transform.position;
                    Vector3 fwd = go.transform.forward;

                    // 1. Colisión contra muros/objetos (ignorando Layer 28 y colliders del propio jugador)
                    var overlaps = Physics.OverlapSphere(pos, 0.12f, ~((1 << 2) | (1 << 28)), QueryTriggerInteraction.Ignore);
                    foreach (var col in overlaps)
                    {
                        if (col == null) continue;
                        if (playerRoot != null && col.transform.IsChildOf(playerRoot)) continue;
                        // Ignorar puertas que se abren durante la toma
                        if ((s.openMainPortal || s.openBanoMujeresDoor) && col.GetComponentInParent<Docencia2InteractiveDoor>() != null) continue;
                        wallHits++;
                        if (string.IsNullOrEmpty(firstHitDetail))
                            firstHitDetail = $"t={tSec:F2}s toca {col.name} en {pos:F2}";
                    }

                    if (hasPrev && Vector3.Distance(prevPos, pos) > 0.01f)
                    {
                        var hits = Physics.RaycastAll(prevPos, (pos - prevPos).normalized, Vector3.Distance(prevPos, pos), ~((1 << 2) | (1 << 28)), QueryTriggerInteraction.Ignore);
                        foreach (var h in hits)
                        {
                            if (playerRoot != null && h.collider.transform.IsChildOf(playerRoot)) continue;
                            if ((s.openMainPortal || s.openBanoMujeresDoor) && h.collider.GetComponentInParent<Docencia2InteractiveDoor>() != null) continue;
                            if (Vector3.Distance(prevPos, pos) > 0.05f)
                            {
                                wallHits++;
                                if (string.IsNullOrEmpty(firstHitDetail))
                                    firstHitDetail = $"t={tSec:F2}s raycast atraviesa {h.collider.name} en {h.point:F2}";
                            }
                        }
                    }

                    // 2. Comprobar cercanía a geometría para riesgo de Z-fighting / near clip
                    if (Physics.Raycast(pos, fwd, out RaycastHit fwdHit, 3.0f, ~((1 << 2) | (1 << 28)), QueryTriggerInteraction.Ignore))
                    {
                        if (playerRoot == null || !fwdHit.collider.transform.IsChildOf(playerRoot))
                            minDistFound = Mathf.Min(minDistFound, fwdHit.distance);
                    }

                    // 3. Regla Cero Planta Alta (nunca cámara en piso PA >= 3.15 m dentro del edificio)
                    var plan = Docencia2FirstPersonPlayer.UnityToPlanXZ(pos);
                    bool insideFootprint = plan.x >= 0f && plan.x <= 28.5f && plan.y >= 0f && plan.y <= 54.0f;
                    if (insideFootprint && pos.y >= 3.00f)
                        paViolations++;

                    // 4. Regla cabello Jeid (Condición 6): en T06 y T11 nunca mirar arriba debajo de su cabello
                    if (s.isThirdPerson && (pos.y < 1.50f || fwd.y > 0.08f))
                        hairRuleViolations++;

                    prevPos = pos;
                    hasPrev = true;
                }

                UnityEngine.Object.DestroyImmediate(go);

                int sErr = wallHits + hairRuleViolations + paViolations + layer28Error + (extLightViolation ? 1 : 0);
                totalDefects += sErr;
                string status = sErr == 0 ? "[OK]" : "[ALERTA]";
                sb.AppendLine($"{status} {s.id} ({s.trackTitle}): wallHits={wallHits} | PA={paViolations} | ExtLight={extLightViolation} | Layer28Err={layer28Error} | HairPitchErr={hairRuleViolations} | MinClearFwd={(minDistFound < 100f ? minDistFound.ToString("F2") + "m" : ">3m")} {(string.IsNullOrEmpty(firstHitDetail) ? "" : "| " + firstHitDetail)}");
            }

            sb.AppendLine($"TOTAL DEFECTOS DETECTADOS: {totalDefects}");
            File.WriteAllText(outReportPath, sb.ToString(), Encoding.UTF8);
            Debug.Log("[TrailerStep2Builder] Auditoría geométrica completada. Total defectos: " + totalDefects);
            return totalDefects;
        }

        public static void InspectPhoneGeometry()
        {
            var scene = EditorSceneManager.OpenScene(TrailerScenePath, OpenSceneMode.Single);
            var player = UnityEngine.Object.FindAnyObjectByType<Docencia2FirstPersonPlayer>();
            var rigDriver = player.GetComponentInChildren<Docencia2PlayerRigDriver>(true);
            var anim = rigDriver.GetComponent<Animator>();
            var mount = rigDriver.phoneVisualMount;

            var sb = new StringBuilder();
            sb.AppendLine($"player.phoneHandRoot localPos={player.phoneHandRoot.localPosition:F4} localEuler={player.phoneHandRoot.localEulerAngles:F2}");
            sb.AppendLine($"mount={mount.name} localPos={mount.localPosition:F4} localRot={mount.localRotation} localEuler={mount.localEulerAngles:F2}");

            foreach (var mf in mount.GetComponentsInChildren<MeshFilter>(true))
            {
                var mr = mf.GetComponent<MeshRenderer>();
                var mats = mr != null ? string.Join(",", mr.sharedMaterials.Select(m => m != null ? m.name : "null")) : "none";
                sb.AppendLine($"Child MF: {mf.name} relPos={mount.InverseTransformPoint(mf.transform.position):F4} relRot={(Quaternion.Inverse(mount.rotation) * mf.transform.rotation).eulerAngles:F2} mats=[{mats}]");

                var mesh = mf.sharedMesh;
                if (mesh != null)
                {
                    var verts = mesh.vertices;
                    Vector3 min = new Vector3(99, 99, 99), max = new Vector3(-99, -99, -99);
                    for (int i = 0; i < verts.Length; i++)
                    {
                        Vector3 pInMount = mount.InverseTransformPoint(mf.transform.TransformPoint(verts[i]));
                        min = Vector3.Min(min, pInMount);
                        max = Vector3.Max(max, pInMount);
                    }
                    sb.AppendLine($"  Bounds in mount space: min={min:F4} max={max:F4} size={(max - min):F4}");

                    if (mr != null)
                    {
                        for (int sm = 0; sm < mesh.subMeshCount && sm < mr.sharedMaterials.Length; sm++)
                        {
                            var mat = mr.sharedMaterials[sm];
                            if (mat != null && mat.name.Contains("Pantalla"))
                            {
                                var tris = mesh.GetTriangles(sm);
                                var norms = mesh.normals;
                                Vector3 avgN = Vector3.zero;
                                Vector3 avgP = Vector3.zero;
                                for (int k = 0; k < tris.Length; k++)
                                {
                                    avgP += mount.InverseTransformPoint(mf.transform.TransformPoint(verts[tris[k]]));
                                    if (norms != null && tris[k] < norms.Length)
                                        avgN += mount.InverseTransformDirection(mf.transform.TransformDirection(norms[tris[k]]));
                                }
                                if (tris.Length > 0) { avgP /= tris.Length; avgN.Normalize(); }
                                sb.AppendLine($"  PANTALLA submesh[{sm}] centerInMount={avgP:F4} normalInMount={avgN:F3}");
                            }
                        }
                    }
                }
            }

            if (anim != null && anim.isHuman)
            {
                var controller = anim.runtimeAnimatorController;
                AnimationClip holdClip = controller != null ? controller.animationClips.FirstOrDefault(c => c.name.Contains("HoldPhone") || c.name.Contains("Phone")) : null;
                sb.AppendLine($"Clips in controller: {(controller != null ? string.Join(", ", controller.animationClips.Select(c => c.name)) : "null")}");
                if (holdClip != null)
                {
                    holdClip.SampleAnimation(anim.gameObject, 0.2f);
                    var rh = anim.GetBoneTransform(HumanBodyBones.RightHand);
                    void DumpBone(string lbl, HumanBodyBones b)
                    {
                        var t = anim.GetBoneTransform(b);
                        if (t != null) sb.AppendLine($"  {lbl} in mount={mount.InverseTransformPoint(t.position):F4} | in RH={rh.InverseTransformPoint(t.position):F4} | localEuler={t.localEulerAngles:F2}");
                    }
                    DumpBone("RightUpperArm", HumanBodyBones.RightUpperArm);
                    DumpBone("RightForeArm", HumanBodyBones.RightLowerArm);
                    DumpBone("RightHand", HumanBodyBones.RightHand);
                    foreach (var b in new[] {
                        HumanBodyBones.RightThumbProximal, HumanBodyBones.RightThumbIntermediate, HumanBodyBones.RightThumbDistal,
                        HumanBodyBones.RightIndexProximal, HumanBodyBones.RightIndexIntermediate, HumanBodyBones.RightIndexDistal,
                        HumanBodyBones.RightMiddleProximal, HumanBodyBones.RightMiddleIntermediate, HumanBodyBones.RightMiddleDistal,
                        HumanBodyBones.RightRingProximal, HumanBodyBones.RightRingIntermediate, HumanBodyBones.RightRingDistal,
                        HumanBodyBones.RightLittleProximal, HumanBodyBones.RightLittleIntermediate, HumanBodyBones.RightLittleDistal
                    })
                    {
                        DumpBone(b.ToString().Replace("Right", ""), b);
                    }
                    var handler = new HumanPoseHandler(anim.avatar, anim.transform);
                    var hp = new HumanPose();
                    handler.GetHumanPose(ref hp);
                    sb.AppendLine("=== RIGHT HAND / ARM MUSCLES ===");
                    for (int m = 0; m < HumanTrait.MuscleCount && m < hp.muscles.Length; m++)
                    {
                        string mn = HumanTrait.MuscleName[m];
                        if (mn.Contains("Right") && (mn.Contains("Arm") || mn.Contains("Forearm") || mn.Contains("Hand") || mn.Contains("Thumb") || mn.Contains("Index") || mn.Contains("Middle") || mn.Contains("Ring") || mn.Contains("Little")))
                        {
                            sb.AppendLine($"  [{m}] {mn} = {hp.muscles[m]:F3}");
                        }
                    }
                }
            }

            File.WriteAllText(Path.GetFullPath("Trailer_Borrador_540p/PhoneMesh_Diag.txt"), sb.ToString(), Encoding.UTF8);
            Debug.Log("[TrailerStep2Builder] InspectPhoneGeometry written.");
        }
    }
}
