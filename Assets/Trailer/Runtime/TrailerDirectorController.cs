using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;
using Unity.Cinemachine;
using Vaelith.Docencia2;

namespace Vaelith.Trailer
{
    /// <summary>
    /// Orquestador exclusivo de la escena duplicada Trailer_Docencia2.unity.
    /// Sincroniza a Jeid (1.599 m), La Testigo (2.302 m), puertas, parpadeos de luz, polvo interior,
    /// Post-Procesado y calidad URP con el tiempo exacto de Timeline/Cinemachine sin modificar
    /// la escena aprobada Exterior_Docencia2.unity ni scripts de juego.
    /// </summary>
    [DefaultExecutionOrder(-20)]
    public class TrailerDirectorController : MonoBehaviour
    {
        [Header("Timeline y Cinemachine")]
        public PlayableDirector playableDirector;
        public Camera trailerMainCamera;
        public CinemachineBrain cinemachineBrain;

        [Header("Calidad y Post-Proceso exclusivos del Tráiler")]
        public RenderPipelineAsset trailerQualityRpAsset;
        public Volume trailerPostProcessVolume;
        public ParticleSystem interiorDustParticles;
        public Transform interiorAccentRigRoot;
        public CanvasGroup titleCardCanvasGroup;
        public CanvasGroup fadeBlackCanvasGroup;

        [Header("Guion de 20 Tomas (120 s)")]
        public List<TrailerShotDefinition> shots = new List<TrailerShotDefinition>();

        private RenderPipelineAsset _prevQualityRp;
        private Docencia2ModeSwitcher _modeSwitcher;
        private Docencia2TerrorDarknessController _darknessCtrl;
        private Docencia2FirstPersonPlayer _player;
        private Docencia2PlayerRigDriver _rigDriver;
        private GameObject _testigoGo;

        private Docencia2InteractiveDoor _mainPortalLeft;
        private Docencia2InteractiveDoor _mainPortalRight;
        private Docencia2InteractiveDoor _banoMujeresDoor;
        private Docencia2InteractiveDoor _aula3Door;

        private readonly List<Light> _accentLights = new List<Light>();
        private readonly Dictionary<int, List<Light>> _accentGroups = new Dictionary<int, List<Light>>();
        private readonly List<Light> _exteriorLights = new List<Light>();
        private readonly Dictionary<Light, int> _origExteriorMasks = new Dictionary<Light, int>();
        private readonly List<Renderer> _interiorRenderers = new List<Renderer>();
        private readonly List<Renderer> _emissivePanelRenderers = new List<Renderer>();

        private Light _emergencyCorridorLight1;
        private Light _emergencyCorridorLight2;
        private Light _emergencyCorridorLight3;
        private Light _dyingFluorescentLight;
        private Light _closePhoneScreenFill;

        private Color _origAmbSky, _origAmbEq, _origAmbGr, _origFogColor;
        private AmbientMode _origAmbMode;
        private FogMode _origFogMode;
        private float _origFogDensity;
        private bool _origFogEnabled;
        private MaterialPropertyBlock _darkPanelMpb;
        private MaterialPropertyBlock _amberPanelMpb;

        private FieldInfo _animSpeedField;
        private MethodInfo _applyFlashMethod;
        private Vector3 _origPhoneHandLocalPos = new Vector3(0.180f, -0.130f, 0.335f);
        private Quaternion _origPhoneHandLocalRot = Quaternion.Euler(4f, -14f, 3f);

        private DepthOfField _dof;
        private ColorAdjustments _colorAdj;
        private double _manualEvalTime = -1.0;
        private bool _useManualEvalTime = false;
        private string _lastShotId = "";
        private int _steppedFrameCount = -1;
        private double _steppedShotStart = 0.0;
        private double _steppedShotDuration = 0.0;
        private int _steppedWarmupFrames = 0;

        public void BeginSteppedShot(double shotStartTime, double shotDuration, int warmupFrames)
        {
            _steppedFrameCount = 0;
            _steppedShotStart = shotStartTime;
            _steppedShotDuration = shotDuration;
            _steppedWarmupFrames = warmupFrames;
            _useManualEvalTime = true;
            _manualEvalTime = shotStartTime + 0.002;
        }

        public void StopSteppedShot()
        {
            _steppedFrameCount = -1;
            _useManualEvalTime = false;
        }

        public void SetManualEvaluationTime(double timeSeconds)
        {
            _useManualEvalTime = true;
            _manualEvalTime = timeSeconds;
        }

        public void ClearManualEvaluationTime()
        {
            _useManualEvalTime = false;
        }

        public double GetEffectiveTime()
        {
            return _useManualEvalTime ? _manualEvalTime : (playableDirector != null ? playableDirector.time : 0.0);
        }

        public void RunAtEndOfFrame(Action callback)
        {
            if (callback != null && gameObject.activeInHierarchy)
            {
                StartCoroutine(WaitAndInvokeEndOfFrame(callback));
            }
        }

        private System.Collections.IEnumerator WaitAndInvokeEndOfFrame(Action callback)
        {
            yield return new WaitForEndOfFrame();
            callback?.Invoke();
        }

        private void OnEnable()
        {
            if (trailerQualityRpAsset != null)
            {
                _prevQualityRp = QualitySettings.renderPipeline;
                QualitySettings.renderPipeline = trailerQualityRpAsset;
            }
            if (trailerMainCamera != null)
            {
                trailerMainCamera.gameObject.tag = "MainCamera";
                trailerMainCamera.enabled = true;
                foreach (var c in FindObjectsByType<Camera>(FindObjectsInactive.Include))
                {
                    if (c == null || c == trailerMainCamera) continue;
                    if (c.gameObject.CompareTag("MainCamera"))
                    {
                        c.gameObject.tag = "Untagged";
                        c.enabled = false;
                    }
                }
            }
        }

        private void OnDisable()
        {
            RestoreVisualState();
        }

        private void Awake()
        {
            if (shots == null || shots.Count == 0)
            {
                shots = TrailerShotCatalog.BuildCanon20Shots();
            }

            CacheSceneReferences();
        }

        private void Start()
        {
            ApplySceneTerrorBaseline();
            if (playableDirector != null && Application.isPlaying && !_useManualEvalTime)
            {
                playableDirector.time = 0.0;
                playableDirector.Play();
            }
            ApplyFrameAtTime(0.0);
        }

        private void CacheSceneReferences()
        {
            _origAmbMode = RenderSettings.ambientMode;
            _origAmbSky = RenderSettings.ambientSkyColor;
            _origAmbEq = RenderSettings.ambientEquatorColor;
            _origAmbGr = RenderSettings.ambientGroundColor;
            _origFogEnabled = RenderSettings.fog;
            _origFogMode = RenderSettings.fogMode;
            _origFogColor = RenderSettings.fogColor;
            _origFogDensity = RenderSettings.fogDensity;

            _modeSwitcher = FindAnyObjectByType<Docencia2ModeSwitcher>();
            _darknessCtrl = FindAnyObjectByType<Docencia2TerrorDarknessController>();
            if (_darknessCtrl != null)
            {
                _dyingFluorescentLight = _darknessCtrl.dyingFluorescentLight;
                _darknessCtrl.enabled = false;
            }

            _player = FindAnyObjectByType<Docencia2FirstPersonPlayer>();
            if (_player != null)
            {
                if (_player.playerCamera != null && trailerMainCamera != null && _player.playerCamera != trailerMainCamera)
                {
                    _player.playerCamera.enabled = false;
                    var existingListener = _player.playerCamera.GetComponent<AudioListener>();
                    if (existingListener != null) existingListener.enabled = false;
                    _player.playerCamera.gameObject.tag = "Untagged";
                }

                var cc = _player.GetComponent<CharacterController>();
                if (cc != null) cc.enabled = false;
                _player.enabled = false;

                _player.EnsureRealisticFlashlightRig();
                _player.CacheOriginalSceneLightShadows();
                if (_player.phoneHandRoot != null)
                {
                    _origPhoneHandLocalPos = _player.phoneHandRoot.localPosition;
                    _origPhoneHandLocalRot = _player.phoneHandRoot.localRotation;
                }

                _rigDriver = _player.rigDriver != null ? _player.rigDriver : _player.GetComponentInChildren<Docencia2PlayerRigDriver>(true);
                if (trailerMainCamera != null)
                {
                    _player.playerCamera = trailerMainCamera;
                }

                _animSpeedField = typeof(Docencia2FirstPersonPlayer).GetField("_currentAnimSpeed01", BindingFlags.Instance | BindingFlags.NonPublic);
                _applyFlashMethod = typeof(Docencia2FirstPersonPlayer).GetMethod("ApplyFlashlightOutputs", BindingFlags.Instance | BindingFlags.NonPublic);
            }

            _testigoGo = GameObject.Find("PB_Interior/LaTestigo") ?? GameObject.Find("LaTestigo");

            _mainPortalLeft = FindDoor("Pivote_Vaiven_Modulo_2");
            _mainPortalRight = FindDoor("Pivote_Vaiven_Modulo_3");
            _banoMujeresDoor = FindDoor("P13_Bano_Mujeres/Pivote_Bisagra");
            _aula3Door = FindDoor("P21_Aula_3/Pivote_Bisagra");

            // Recolectar luces exteriores e interiores existentes sin tocar assets compartidos
            _exteriorLights.Clear();
            _origExteriorMasks.Clear();
            foreach (var l in FindObjectsByType<Light>(FindObjectsInactive.Include))
            {
                if (l == null) continue;
                if (_player != null && l.transform.IsChildOf(_player.transform)) continue;
                if (interiorAccentRigRoot != null && l.transform.IsChildOf(interiorAccentRigRoot)) continue;

                if (IsInsidePBorPA(l.transform.position))
                {
                    l.enabled = false; // Las luces generales interiores permanecen apagadas en Modo Terror
                }
                else
                {
                    _exteriorLights.Add(l);
                    _origExteriorMasks[l] = l.cullingMask;
                }
            }

            // Recolectar renderers interiores para mantener sombras de muros TwoSided (sin traspasar luces)
            _interiorRenderers.Clear();
            _emissivePanelRenderers.Clear();
            foreach (string rootName in new[] { "PB_Interior", "PA_Interior" })
            {
                var rGo = GameObject.Find(rootName);
                if (rGo == null) continue;
                foreach (var r in rGo.GetComponentsInChildren<Renderer>(true))
                {
                    if (r == null) continue;
                    _interiorRenderers.Add(r);
                    if (r.name.Contains("Difusor") || r.name.Contains("Comb_Paneles_LED"))
                        _emissivePanelRenderers.Add(r);
                }
            }

            _darkPanelMpb = new MaterialPropertyBlock();
            _darkPanelMpb.SetColor("_BaseColor", new Color(0.14f, 0.17f, 0.19f, 1f));
            _darkPanelMpb.SetColor("_EmissionColor", new Color(0.01f, 0.015f, 0.018f, 1f));

            _amberPanelMpb = new MaterialPropertyBlock();
            _amberPanelMpb.SetColor("_BaseColor", new Color(0.55f, 0.36f, 0.16f, 1f));
            _amberPanelMpb.SetColor("_EmissionColor", new Color(0.85f, 0.48f, 0.14f, 1f) * 0.55f);

            // Cachear grupo de acentos interiores exclusivos de Trailer_Docencia2
            _accentLights.Clear();
            _accentGroups.Clear();
            if (interiorAccentRigRoot != null)
            {
                foreach (Transform group in interiorAccentRigRoot)
                {
                    if (int.TryParse(group.name.Replace("Group_", ""), out int gid))
                    {
                        var list = new List<Light>(group.GetComponentsInChildren<Light>(true));
                        _accentGroups[gid] = list;
                        _accentLights.AddRange(list);
                        if (gid == 6 && list.Count >= 3)
                        {
                            _emergencyCorridorLight1 = list[0];
                            _emergencyCorridorLight2 = list[1];
                            _emergencyCorridorLight3 = list[2];
                        }
                    }
                    else if (group.name == "ClosePhoneScreenFill")
                    {
                        _closePhoneScreenFill = group.GetComponent<Light>();
                    }
                }
            }

            if (trailerPostProcessVolume != null && trailerPostProcessVolume.sharedProfile != null)
            {
                trailerPostProcessVolume.sharedProfile.TryGet(out _dof);
                trailerPostProcessVolume.sharedProfile.TryGet(out _colorAdj);
            }

            if (cinemachineBrain != null)
            {
                cinemachineBrain.UpdateMethod = CinemachineBrain.UpdateMethods.ManualUpdate;
            }
        }

        private void ApplySceneTerrorBaseline()
        {
            if (_modeSwitcher != null)
            {
                _modeSwitcher.ApplyMode(Docencia2VisualMode.Terror, false);
            }

            foreach (var r in _emissivePanelRenderers)
            {
                if (r != null) r.SetPropertyBlock(_darkPanelMpb);
            }

            foreach (var r in _interiorRenderers)
            {
                if (r == null) continue;
                string n = r.gameObject.name;
                if (!n.StartsWith("Piso_") && !n.StartsWith("Plafon_") && !n.Contains("Panel") && !n.Contains("Difusor"))
                {
                    r.shadowCastingMode = ShadowCastingMode.TwoSided;
                }
            }
        }

        private void RestoreVisualState()
        {
            if (_prevQualityRp != null)
            {
                QualitySettings.renderPipeline = _prevQualityRp;
                _prevQualityRp = null;
            }
            foreach (var kv in _origExteriorMasks)
            {
                if (kv.Key != null) kv.Key.cullingMask = kv.Value;
            }
            RenderSettings.ambientMode = _origAmbMode;
            RenderSettings.ambientSkyColor = _origAmbSky;
            RenderSettings.ambientEquatorColor = _origAmbEq;
            RenderSettings.ambientGroundColor = _origAmbGr;
            RenderSettings.fog = _origFogEnabled;
            RenderSettings.fogMode = _origFogMode;
            RenderSettings.fogColor = _origFogColor;
            RenderSettings.fogDensity = _origFogDensity;
        }

        private void LateUpdate()
        {
            if (!Application.isPlaying) return;
            if (_steppedFrameCount >= 0)
            {
                int activeFrame = Math.Max(0, _steppedFrameCount - _steppedWarmupFrames);
                _manualEvalTime = _steppedShotStart + Math.Min(_steppedShotDuration - 0.002, (activeFrame + 0.05) / 24.0);
                _steppedFrameCount++;
            }
            double time = _useManualEvalTime ? _manualEvalTime : (playableDirector != null ? playableDirector.time : 0.0);
            ApplyFrameAtTime(time);
        }

        public TrailerShotDefinition GetShotAtTime(double time, out float localTime)
        {
            for (int i = 0; i < shots.Count; i++)
            {
                var s = shots[i];
                if (time >= s.startTime && (time < s.startTime + s.duration || i == shots.Count - 1))
                {
                    localTime = Mathf.Clamp((float)(time - s.startTime), 0f, (float)s.duration);
                    return s;
                }
            }
            localTime = 0f;
            return shots.Count > 0 ? shots[0] : null;
        }

        public void ApplyFrameAtTime(double time)
        {
            if (shots == null || shots.Count == 0) return;
            var shot = GetShotAtTime(time, out float localT);
            if (shot == null) return;

            float normT = shot.duration > 0.001 ? Mathf.Clamp01(localT / (float)shot.duration) : 0f;
            bool isShotChanged = shot.id != _lastShotId;
            _lastShotId = shot.id;

            // 1. Evaluar Timeline/CinemachineBrain en este instante antes de alinear personaje y luces
            if (playableDirector != null && _useManualEvalTime)
            {
                playableDirector.time = time;
                playableDirector.Evaluate();
            }
            if (cinemachineBrain != null)
            {
                cinemachineBrain.ManualUpdate();
            }

            // 2. Excluir capas interiores de luces exteriores SOLO en interior (para evitar contaminación en exterior!)
            bool isExteriorShot = shot.category == TrailerShotCategory.ArchitecturalExterior || shot.category == TrailerShotCategory.ThirdPersonJeid && !shot.isIndoorFog;
            foreach (var kv in _origExteriorMasks)
            {
                if (kv.Key == null) continue;
                kv.Key.cullingMask = isExteriorShot ? kv.Value : (kv.Value & ~((1 << 8) | (1 << 30)));
            }

            // 3. Ambiente y niebla fieles a Exterior_Docencia2 (exterior intacto vs interior oscuro)
            if (shot.category == TrailerShotCategory.BlackTitleCard)
            {
                RenderSettings.ambientMode = AmbientMode.Flat;
                RenderSettings.ambientLight = Color.black;
                RenderSettings.fog = false;
                if (trailerMainCamera != null)
                {
                    trailerMainCamera.clearFlags = CameraClearFlags.SolidColor;
                    trailerMainCamera.backgroundColor = Color.black;
                }
            }
            else if (isExteriorShot)
            {
                RenderSettings.ambientMode = AmbientMode.Trilight;
                RenderSettings.ambientSkyColor = _modeSwitcher != null ? _modeSwitcher.terrorAmbientSky : new Color(0.055f, 0.105f, 0.138f, 1f);
                RenderSettings.ambientEquatorColor = _modeSwitcher != null ? _modeSwitcher.terrorAmbientEquator : new Color(0.072f, 0.128f, 0.155f, 1f);
                RenderSettings.ambientGroundColor = _modeSwitcher != null ? _modeSwitcher.terrorAmbientGround : new Color(0.065f, 0.112f, 0.132f, 1f);
                RenderSettings.fog = true;
                RenderSettings.fogMode = FogMode.Exponential;
                RenderSettings.fogColor = _modeSwitcher != null ? _modeSwitcher.terrorFogColor : new Color(0.059f, 0.102f, 0.110f, 1f);
                RenderSettings.fogDensity = _modeSwitcher != null ? _modeSwitcher.terrorFogDensity : 0.025f;
                if (trailerMainCamera != null)
                {
                    trailerMainCamera.clearFlags = CameraClearFlags.Skybox;
                    trailerMainCamera.backgroundColor = RenderSettings.fogColor;
                }
            }
            else
            {
                // Interior PB oscuro (#070D12), con niebla sutil que no ciega la perspectiva arquitectónica
                float amb = (shot.category == TrailerShotCategory.ArchitecturalInterior) ? 0.015f : 0.0055f;
                RenderSettings.ambientMode = AmbientMode.Trilight;
                RenderSettings.ambientSkyColor = new Color(amb * 0.72f, amb * 1.05f, amb * 1.22f, 1f);
                RenderSettings.ambientEquatorColor = new Color(amb * 0.78f, amb * 1.08f, amb * 1.24f, 1f);
                RenderSettings.ambientGroundColor = new Color(amb * 0.60f, amb * 0.88f, amb * 1.02f, 1f);
                RenderSettings.fog = true;
                RenderSettings.fogMode = FogMode.ExponentialSquared;
                RenderSettings.fogColor = new Color(0.006f, 0.012f, 0.016f, 1f);
                RenderSettings.fogDensity = shot.isIndoorFog ? shot.customIndoorFogDensity : 0.022f;
                if (trailerMainCamera != null)
                {
                    trailerMainCamera.clearFlags = CameraClearFlags.SolidColor;
                    trailerMainCamera.backgroundColor = RenderSettings.fogColor;
                }
            }

            // 4. Puertas
            SetDoorState(_mainPortalLeft, shot.openMainPortal);
            SetDoorState(_mainPortalRight, shot.openMainPortal);
            SetDoorState(_banoMujeresDoor, shot.openBanoMujeresDoor);
            SetDoorState(_aula3Door, shot.openAula3Door);

            // 5. Máscara de cámara: Layer 28 ACTIVA solo en 3ª persona (T06, T11); OCULTA en 1ª persona
            if (trailerMainCamera != null)
            {
                int mask = ~0;
                if (shot.includeLayer28)
                    mask |= (1 << Docencia2PlayerRigDriver.HeadMirrorOnlyLayer);
                else
                    mask &= ~(1 << Docencia2PlayerRigDriver.HeadMirrorOnlyLayer);
                trailerMainCamera.cullingMask = mask;
            }

            // 6. Jeid (Protagonista 1.599 m): 1ª persona (camara ~1.50 m sobre suelo), 3ª persona, u oculta
            ApplyJeidForShot(shot, localT, normT);

            // 7. La Testigo (2.302 m · CreepyIdle): espejo T10 y pasillo T16–T19
            ApplyTestigoForShot(shot, localT);

            // 8. Acentos de luz interiores (ESTRICTAMENTE APAGADOS EN TODA TOMA EXTERIOR)
            ApplyLightingRigForShot(shot, localT, isExteriorShot);

            // 9. Partículas de polvo sutiles (solo interior)
            if (interiorDustParticles != null && trailerMainCamera != null)
            {
                bool showDust = shot.category == TrailerShotCategory.ArchitecturalInterior || shot.category == TrailerShotCategory.FirstPersonJeid || (shot.category == TrailerShotCategory.ThirdPersonJeid && shot.isIndoorFog);
                if (showDust)
                {
                    interiorDustParticles.transform.position = trailerMainCamera.transform.position + trailerMainCamera.transform.forward * 2.4f;
                    interiorDustParticles.transform.rotation = trailerMainCamera.transform.rotation;
                    if (!interiorDustParticles.gameObject.activeSelf) interiorDustParticles.gameObject.SetActive(true);
                    if (!interiorDustParticles.isPlaying) interiorDustParticles.Play();
                }
                else
                {
                    if (interiorDustParticles.isPlaying) interiorDustParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                    interiorDustParticles.gameObject.SetActive(false);
                }
            }

            // 10. Post-proceso por toma (DoF y apagón/fade final a negro en T19/T20)
            if (_dof != null)
            {
                _dof.active = shot.useDof;
                if (shot.useDof)
                {
                    _dof.mode.Override(DepthOfFieldMode.Bokeh);
                    _dof.focusDistance.Override(shot.dofFocusDistance);
                    _dof.aperture.Override(shot.dofAperture);
                    _dof.focalLength.Override(50f);
                }
            }

            if (fadeBlackCanvasGroup != null)
            {
                float blackAlpha = 0f;
                if (shot.id == "T19" && localT > 4.15f)
                    blackAlpha = Mathf.InverseLerp(4.15f, 4.95f, localT);
                else if (shot.id == "T10" && shot.blackoutStart >= 0f && localT >= shot.blackoutStart && localT <= shot.blackoutEnd)
                    blackAlpha = 0.85f;
                else if (shot.id == "T17" && shot.blackoutStart >= 0f && localT >= shot.blackoutStart && localT <= shot.blackoutEnd)
                    blackAlpha = 0.94f;
                fadeBlackCanvasGroup.alpha = blackAlpha;
            }

            if (titleCardCanvasGroup != null)
            {
                if (shot.category == TrailerShotCategory.BlackTitleCard)
                {
                    float a = 1f;
                    if (localT < 0.6f) a = Mathf.InverseLerp(0.15f, 0.6f, localT);
                    else if (localT > 8.8f) a = 1f - Mathf.InverseLerp(8.8f, 9.8f, localT);
                    titleCardCanvasGroup.alpha = Mathf.Clamp01(a);
                }
                else
                {
                    titleCardCanvasGroup.alpha = 0f;
                }
            }

            if (isShotChanged)
            {
                Physics.SyncTransforms();
            }
        }

        private void ApplyJeidForShot(TrailerShotDefinition shot, float localT, float normT)
        {
            if (_player == null) return;

            bool show = shot.showJeid;
            if (_rigDriver != null)
            {
                if (_rigDriver.gameObject.activeSelf != show)
                    _rigDriver.gameObject.SetActive(show);
            }

            if (!show)
            {
                _player.transform.position = new Vector3(0f, -80f, 0f);
                _player.SetFlashlightState(false, false);
                if (_closePhoneScreenFill != null) _closePhoneScreenFill.enabled = false;
                return;
            }

            float smoothT = shot.isThirdPerson ? normT : Mathf.SmoothStep(0f, 1f, normT);
            Vector3 playerPos = Vector3.Lerp(shot.jeidPosStart, shot.jeidPosEnd, smoothT);
            if (!shot.isIndoorFog && Physics.Raycast(playerPos + Vector3.up * 2.0f, Vector3.down, out RaycastHit hit, 5.0f, ~((1 << 2) | (1 << 28)), QueryTriggerInteraction.Ignore))
            {
                playerPos.y = hit.point.y;
            }

            float jeidAnimSpeed = shot.jeidWalkSpeed;

            // En 1ª persona (T01, T02, T07, T09, T10, T17, T18, T19) colocamos el cuerpo exactamente bajo trailerMainCamera
            if (!shot.isThirdPerson && trailerMainCamera != null)
            {
                Vector3 fwdFlat = Vector3.ProjectOnPlane(trailerMainCamera.transform.forward, Vector3.up);
                if (fwdFlat.sqrMagnitude < 0.001f) fwdFlat = Vector3.forward;
                fwdFlat.Normalize();

                Vector3 camPos = trailerMainCamera.transform.position;
                _player.transform.position = new Vector3(camPos.x - fwdFlat.x * 0.065f, playerPos.y, camPos.z - fwdFlat.z * 0.065f);
                _player.transform.rotation = Quaternion.LookRotation(fwdFlat, Vector3.up);

                // Sincronizar Camara_PrimeraPersona_1.65m (padre de PhoneHandRoot) directamente con trailerMainCamera
                Transform camT = _player.playerCamera != null ? _player.playerCamera.transform : null;
                Transform handParent = _player.phoneHandRoot != null ? _player.phoneHandRoot.parent : camT;
                if (handParent != null)
                {
                    handParent.SetPositionAndRotation(camPos, trailerMainCamera.transform.rotation);
                }

                if (_player.phoneHandRoot != null)
                {
                    if (shot.useClosePhoneFraming)
                    {
                        _player.phoneHandRoot.localPosition = new Vector3(0.068f, -0.015f, 0.345f);
                        _player.phoneHandRoot.localRotation = Quaternion.Euler(0f, -18f, -5f) * Quaternion.Euler(-76f, 0f, 0f);
                    }
                    else
                    {
                        _player.phoneHandRoot.localPosition = new Vector3(0.165f, -0.105f, 0.340f);
                        _player.phoneHandRoot.localRotation = Quaternion.Euler(0f, -14f, 3f) * Quaternion.Euler(-68f, 0f, 0f);
                    }
                }
            }
            else
            {
                // 3ª persona (T06 vestíbulo atrapada frente al portal, T11 pasillo PB)
                float yaw;
                if (shot.id == "T06")
                {
                    if (localT < 1.70f)
                    {
                        playerPos = shot.jeidPosStart;
                        yaw = shot.jeidYawStart;
                        jeidAnimSpeed = 0.04f;
                    }
                    else if (localT < 2.55f)
                    {
                        float turnU = Mathf.SmoothStep(0f, 1f, (localT - 1.70f) / 0.85f);
                        playerPos = shot.jeidPosStart;
                        yaw = Mathf.LerpAngle(shot.jeidYawStart, shot.jeidYawEnd, turnU);
                        jeidAnimSpeed = 0.25f;
                    }
                    else
                    {
                        float walkW = Mathf.SmoothStep(0f, 1f, (localT - 2.55f) / 3.45f);
                        playerPos = Vector3.Lerp(shot.jeidPosStart, shot.jeidPosEnd, walkW);
                        yaw = shot.jeidYawEnd;
                        jeidAnimSpeed = shot.jeidWalkSpeed;
                    }
                }
                else
                {
                    yaw = Mathf.LerpAngle(shot.jeidYawStart, shot.jeidYawEnd, normT);
                }

                _player.transform.position = playerPos;
                _player.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
                if (_player.phoneHandRoot != null)
                {
                    Vector3 eyePivot = _player.transform.TransformPoint(new Vector3(0f, _player.standingCameraY, 0.065f));
                    _player.phoneHandRoot.position = eyePivot + _player.transform.rotation * new Vector3(0.165f, -0.105f, 0.340f);
                    _player.phoneHandRoot.rotation = _player.transform.rotation * (Quaternion.Euler(0f, -14f, 3f) * Quaternion.Euler(-68f, 0f, 0f));
                }
            }

            _player.isThirdPersonView = shot.isThirdPerson;
            _animSpeedField?.SetValue(_player, jeidAnimSpeed);
            if (_rigDriver != null)
            {
                _rigDriver.SyncRendererVisibility(true);
            }

            // Control de linterna por toma
            float power = 1.0f;
            bool flashOn = true;
            switch (shot.flashlightMode)
            {
                case TrailerFlashlightMode.Off:
                    flashOn = false;
                    break;
                case TrailerFlashlightMode.OnAtSecond08:
                    flashOn = localT >= 0.85f;
                    power = flashOn ? 1.15f : 0f;
                    break;
                case TrailerFlashlightMode.FlickerDip:
                    if (shot.blackoutStart >= 0f && localT >= shot.blackoutStart && localT <= shot.blackoutEnd)
                    {
                        flashOn = false;
                        power = 0f;
                    }
                    else
                    {
                        flashOn = true;
                        power = 1.05f;
                    }
                    break;
                case TrailerFlashlightMode.FlickerAndDie:
                    if (localT < 1.8f) { flashOn = true; power = 1.05f; }
                    else if (localT < 2.1f) { flashOn = false; power = 0f; }
                    else if (localT < 2.9f) { flashOn = true; power = 0.72f; }
                    else if (localT < 3.15f) { flashOn = false; power = 0f; }
                    else if (localT < 3.65f) { flashOn = true; power = 0.35f; }
                    else { flashOn = false; power = 0f; }
                    break;
                default:
                    flashOn = true;
                    power = 1.08f;
                    break;
            }

            _player.flashlightPowerMultiplier = Mathf.Clamp(power, 0.4f, 2.0f);
            _player.SetFlashlightState(flashOn, false);
            if (_closePhoneScreenFill != null)
            {
                bool enableCloseScreenFill = shot.useClosePhoneFraming && !flashOn && _player.phoneHandRoot != null;
                _closePhoneScreenFill.enabled = enableCloseScreenFill;
                if (enableCloseScreenFill)
                {
                    Vector3 screenCamDir = trailerMainCamera != null ? -trailerMainCamera.transform.forward : _player.phoneHandRoot.up;
                    _closePhoneScreenFill.transform.position = _player.phoneHandRoot.position + screenCamDir * 0.28f;
                }
            }
        }

        private void ApplyTestigoForShot(TrailerShotDefinition shot, float localT)
        {
            if (_testigoGo == null) return;
            if (!shot.showTestigo)
            {
                if (_testigoGo.activeSelf) _testigoGo.SetActive(false);
                return;
            }

            bool vis = true;
            if (shot.testigoAppearWindowStart >= 0f)
            {
                vis = localT >= shot.testigoAppearWindowStart && localT <= shot.testigoAppearWindowEnd;
            }
            if (_testigoGo.activeSelf != vis) _testigoGo.SetActive(vis);
            if (!vis) return;

            Vector3 pos = shot.testigoPosPrimary;
            if (shot.testigoHasBlackoutStep && shot.blackoutEnd > 0f && localT >= (shot.blackoutStart + shot.blackoutEnd) * 0.5f)
            {
                pos = shot.testigoPosAfterBlackout;
            }
            _testigoGo.transform.position = pos;
            _testigoGo.transform.rotation = Quaternion.Euler(0f, shot.testigoYaw, 0f);
        }

        private void ApplyLightingRigForShot(TrailerShotDefinition shot, float localT, bool isExteriorShot)
        {
            // NUNCA encender luces de Trailer_ShotLightingRig durante tomas exteriores!
            int activeGid = isExteriorShot ? 0 : shot.interiorAccentId;

            foreach (var kv in _accentGroups)
            {
                bool active = kv.Key == activeGid;
                foreach (var l in kv.Value)
                {
                    if (l != null) l.enabled = active;
                }
            }

            // En T15 (Plaza Sur) apagar progresivamente las 3 luces de emergencia del corredor
            if (activeGid == 6)
            {
                if (_emergencyCorridorLight1 != null) _emergencyCorridorLight1.enabled = localT < 2.0f || (localT > 2.25f && localT < 2.65f);
                if (_emergencyCorridorLight2 != null) _emergencyCorridorLight2.enabled = localT < 3.6f || (localT > 3.85f && localT < 4.10f);
                if (_emergencyCorridorLight3 != null) _emergencyCorridorLight3.enabled = localT < 4.9f;
            }

            // Oscilar fluorescente moribundo de pasillo durante tomas interiores del corredor/baño
            if (_dyingFluorescentLight != null)
            {
                bool enableFluorescent = !isExteriorShot && (shot.id == "T08" || shot.id == "T10" || shot.id == "T11" || shot.id == "T16" || shot.id == "T17" || shot.id == "T18" || shot.id == "T19");
                _dyingFluorescentLight.enabled = enableFluorescent;
                if (enableFluorescent)
                {
                    bool blackout = shot.blackoutStart >= 0f && localT >= shot.blackoutStart && localT <= shot.blackoutEnd;
                    float n = Mathf.PerlinNoise(localT * 14.5f, 7.1f);
                    _dyingFluorescentLight.intensity = blackout ? 0f : (n > 0.35f ? 0.42f * n : 0.05f);
                }
            }
        }

        private static bool IsInsidePBorPA(Vector3 worldPos)
        {
            var p = Docencia2FirstPersonPlayer.UnityToPlanXZ(worldPos);
            return p.x >= 0.1f && p.x <= 28.4f && p.y >= 0.1f && p.y <= 53.8f && worldPos.y > -0.2f && worldPos.y < 7.2f;
        }

        private static Docencia2InteractiveDoor FindDoor(string partialPath)
        {
            foreach (var d in FindObjectsByType<Docencia2InteractiveDoor>(FindObjectsInactive.Include))
            {
                if (d == null) continue;
                string full = GetHierarchyPath(d.transform);
                if (full.EndsWith(partialPath, StringComparison.OrdinalIgnoreCase))
                    return d;
            }
            return null;
        }

        private static void SetDoorState(Docencia2InteractiveDoor door, bool open)
        {
            if (door != null && door.isOpen != open)
            {
                door.SetOpen(open, false);
            }
        }

        private static string GetHierarchyPath(Transform t)
        {
            string s = t.name;
            while (t.parent != null)
            {
                t = t.parent;
                s = t.name + "/" + s;
            }
            return s;
        }
    }
}
