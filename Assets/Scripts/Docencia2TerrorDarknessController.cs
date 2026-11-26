using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Vaelith.Docencia2
{
    /// <summary>
    /// Controlador de OSCURIDAD EXTREMA y TENSIÓN SIN LUZ (exclusivo del modo TERROR en Play Mode):
    /// - Interior casi en oscuridad: ambiente base ~0.015 (ajustable con tecla B y slider en Pausa),
    ///   exposición fija (sin auto-exposure), sin rellenos ni probes que aclaren.
    /// - Solo 2-3 lámparas de emergencia ámbar tenues y 1 fluorescente moribundo irregular;
    ///   resplandor lunar muy tenue en ventanas y luz de silueta cercana (~2.8 m) para que se lea
    ///   el borde de puertas y silueta de pasillos y nunca quede todo 100 % negro puro.
    /// - Modo Prueba de Puertas (tecla G): enciende piso de luz de inspección suficiente para ver puertas
    ///   y geometría, y lo apaga automáticamente al salir de G.
    /// - Tensión sonora sin luz: ambiente grave constante, respiración y pasos audibles, zumbido en lámparas,
    ///   goteras lejanas, crujidos de puertas y de plafón, silencios súbitos, y eventos de ambiente cuya
    ///   frecuencia aumenta cuanto más tiempo permanece el jugador en una misma zona (SIN sustos bruscos,
    ///   enemigos ni siluetas).
    /// </summary>
    public class Docencia2TerrorDarknessController : MonoBehaviour
    {
        public static Docencia2TerrorDarknessController Instance { get; private set; }

        [Header("Referencias")]
        public Docencia2ModeSwitcher switcher;
        public Docencia2FirstPersonPlayer player;
        public Volume interiorTerrorVolume;
        public GameObject terrorAtmosphereRoot;
        public Light nearSilhouetteLight;
        public Light doorTestInspectionLight;

        [Header("Oscuridad Extrema y Control de Brillo (Tecla B / Menú Pausa)")]
        [Tooltip("Nivel de piso mínimo de luz ambiente (0 = Oscuridad Extrema ~0.002, 1 = Alto)")]
        [Range(0f, 1f)]
        public float brightnessLevel = 0.04f; // Por defecto, casi negro total (penumbra abismal salvo por la linterna)

        private readonly float[] _brightnessPresets = new float[] { 0.02f, 0.04f, 0.12f, 0.25f, 0.50f };
        private int _brightnessPresetIndex = 1;

        [Header("Audio de Terror (Sin ruidos ni zumbidos molestos)")]
        [Tooltip("Si está desactivado, deshabilita los zumbidos/drones continuos para no cansar el oído.")]
        public bool enableContinuousDroneAndHum = false;

        [Header("Estado de Zona y Tensión Sonora")]
        public string currentZoneName = "Vestibulo_PB";
        public float timeInCurrentZone = 0f;
        public bool isSuddenSilenceActive = false;

        // Respaldo de valores originales para no alterar jamás el modo NOCHE ni las cámaras exteriores
        private AmbientMode _origAmbientMode;
        private Color _origSky;
        private Color _origEquator;
        private Color _origGround;
        private Color _origFogColor;
        private float _origFogDensity;
        private ReflectionProbeUsage[] _cachedProbeUsages;
        private ShadowCastingMode[] _cachedShadowModes;
        private Renderer[] _cachedInteriorRenderers;
        private bool _darknessApplied = false;
        private bool _lastShadowsSuppressed = false;
        private bool _inSync = false;

        // Luces originales que se apagan cuando el jugador está activo en modo TERROR extremo
        private List<Light> _suppressedLegacyTerrorLights = new List<Light>();
        private List<Docencia2InteriorModeTracker> _suppressedTrackers = new List<Docencia2InteriorModeTracker>();
        private Dictionary<Light, int> _cachedExteriorLightMasks = new Dictionary<Light, int>();
        private List<Renderer> _emissivePanelRenderers = new List<Renderer>();
        private MaterialPropertyBlock _darkPanelMpb;
        private MaterialPropertyBlock _amberPanelMpb;
        private MaterialPropertyBlock _dyingFluorescentMpb;

        // Luces del modo TERROR extremo
        public List<Light> emergencyAmberLights = new List<Light>();
        public Light dyingFluorescentLight;
        public List<Light> windowMoonGlowLights = new List<Light>();
        public List<Renderer> emergencyPanelRenderers = new List<Renderer>();
        public Renderer dyingFluorescentRenderer;

        // Audio de Tensión sin Luz (SUPUESTO: sintetizado proceduralmente por ausencia de archivos de audio)
        private AudioSource _droneSource;
        private AudioSource _breathSource;
        private AudioSource _spatialEventSource;
        private AudioSource[] _lampHumSources;
        private AudioClip _droneClip;
        private AudioClip _breathClip;
        private AudioClip _lampHumClip;
        private AudioClip _waterDripClip;
        private AudioClip _ceilingCreakClip;
        private AudioClip _distantDoorCloseClip;
        private AudioClip _otherFloorThudClip;
        private AudioClip _bulbPopClip;

        private float _nextAmbientEventTime = 8f;
        private float _nextDripTime = 4.5f;
        private float _silenceEndTime = -1f;
        private float _nextSilenceAllowedTime = 25f;
        private Vector2 _lastZoneAnchorPlan = new Vector2(24.8f, 19.3f);
        private int _lastFloor = 0;

        private void Awake()
        {
            Instance = this;
        }

        private void OnEnable()
        {
            Instance = this;
        }

        private void OnDisable()
        {
            if (Instance == this) Instance = null;
            RestoreOriginalEnvironment();
        }

        private void Start()
        {
            if (switcher == null) switcher = FindAnyObjectByType<Docencia2ModeSwitcher>();
            if (player == null) player = Docencia2FirstPersonPlayer.Instance ?? FindAnyObjectByType<Docencia2FirstPersonPlayer>();

            // Silenciar cualquier AudioSource ajeno al jugador en la escena que pudiera emitir ruido molesto
            foreach (var src in FindObjectsByType<AudioSource>(FindObjectsInactive.Include))
            {
                if (src != null && player != null && !src.transform.IsChildOf(player.transform) && !src.transform.IsChildOf(transform))
                {
                    src.Stop();
                    src.playOnAwake = false;
                }
            }

            CacheInteriorRenderersAndPanels();
            InitializeProceduralTensionAudio();
            SyncState();
        }

        private void CacheInteriorRenderersAndPanels()
        {
            var renderers = new List<Renderer>();
            foreach (string rootName in new[] { "PB_Interior", "PA_Interior" })
            {
                var rGo = GameObject.Find(rootName);
                if (rGo == null) continue;
                foreach (var r in rGo.GetComponentsInChildren<Renderer>(true))
                {
                    if (r == null) continue;
                    renderers.Add(r);
                    if (r.name.Contains("Difusor") || r.name.Contains("Comb_Paneles_LED_PB"))
                    {
                        _emissivePanelRenderers.Add(r);
                    }
                }
            }
            _cachedInteriorRenderers = renderers.ToArray();
            _cachedProbeUsages = _cachedInteriorRenderers.Select(r => r.reflectionProbeUsage).ToArray();
            _cachedShadowModes = _cachedInteriorRenderers.Select(r => r.shadowCastingMode).ToArray();

            _darkPanelMpb = new MaterialPropertyBlock();
            _darkPanelMpb.SetColor("_BaseColor", new Color(0.16f, 0.19f, 0.21f, 1f));
            _darkPanelMpb.SetColor("_EmissionColor", new Color(0.015f, 0.022f, 0.026f, 1f));

            _amberPanelMpb = new MaterialPropertyBlock();
            _amberPanelMpb.SetColor("_BaseColor", new Color(0.55f, 0.36f, 0.16f, 1f));
            _amberPanelMpb.SetColor("_EmissionColor", new Color(0.85f, 0.48f, 0.14f, 1f) * 0.65f);

            _dyingFluorescentMpb = new MaterialPropertyBlock();
            _dyingFluorescentMpb.SetColor("_BaseColor", new Color(0.45f, 0.58f, 0.56f, 1f));
            _dyingFluorescentMpb.SetColor("_EmissionColor", new Color(0.42f, 0.65f, 0.60f, 1f) * 0.55f);
        }

        private void Update()
        {
            if (!Application.isPlaying) return;

            var kb = Keyboard.current;
            bool allowShortcuts = player == null || player.showDebugHudAndShortcuts;
            if (allowShortcuts && kb != null && kb.bKey.wasPressedThisFrame)
            {
                CycleBrightnessPreset();
            }

            SyncState();

            bool isTerror = switcher != null && switcher.modoActual == Docencia2VisualMode.Terror;
            if (isTerror && _darknessApplied)
            {
                UpdateDyingFluorescentAndEmergencyLights(Time.time);
                UpdateZoneTrackingAndTensionEvents(Time.time, Time.deltaTime);
            }
        }

        public bool IsViewerOutdoor()
        {
            Vector3 viewerWorldPos = player != null ? player.transform.position : Vector3.zero;
            Camera activeCam = null;

            if (player != null && player.playerCamera != null && player.playerCamera.enabled &&
                player.playerCamera.gameObject.activeInHierarchy && player.playerCamera.targetTexture == null)
            {
                activeCam = player.playerCamera;
            }
            else
            {
                float bestDepth = float.NegativeInfinity;
                foreach (var cam in Camera.allCameras)
                {
                    if (cam == null || !cam.enabled || !cam.gameObject.activeInHierarchy || cam.targetTexture != null)
                        continue;
                    if (cam.depth >= bestDepth)
                    {
                        bestDepth = cam.depth;
                        activeCam = cam;
                    }
                }
            }

            if (activeCam != null)
            {
                viewerWorldPos = activeCam.transform.position;
            }

            Vector2 plan = Docencia2FirstPersonPlayer.UnityToPlanXZ(viewerWorldPos);
            bool insideBuilding =
                plan.x >= 0.10f && plan.x <= 28.38f &&
                plan.y >= 0.10f && plan.y <= 53.82f &&
                viewerWorldPos.y >= -0.60f && viewerWorldPos.y <= 7.05f;
            return !insideBuilding;
        }

        public void CycleBrightnessPreset()
        {
            _brightnessPresetIndex = (_brightnessPresetIndex + 1) % _brightnessPresets.Length;
            brightnessLevel = _brightnessPresets[_brightnessPresetIndex];
            ApplyCurrentAmbientAndInspectionFloor();
        }

        public void SetBrightnessLevel(float level)
        {
            brightnessLevel = Mathf.Clamp01(level);
            ApplyCurrentAmbientAndInspectionFloor();
        }

        public void SyncState()
        {
            if (_inSync || !isActiveAndEnabled || !gameObject.scene.isLoaded) return;
            _inSync = true;
            try
            {
                if (switcher == null) switcher = FindAnyObjectByType<Docencia2ModeSwitcher>();
                if (player == null) player = Docencia2FirstPersonPlayer.Instance ?? FindAnyObjectByType<Docencia2FirstPersonPlayer>();
                if (switcher == null) return;

                bool isTerror = (switcher.modoActual == Docencia2VisualMode.Terror);
                bool isOutdoor = IsViewerOutdoor();
                bool shouldApplyInteriorDarkness = isTerror && !isOutdoor;

                if (shouldApplyInteriorDarkness && !_darknessApplied)
                {
                    EnterExtremeTerrorDarkness();
                }
                else if (!shouldApplyInteriorDarkness && _darknessApplied)
                {
                    RestoreOriginalEnvironment();
                }

                if (shouldApplyInteriorDarkness)
                {
                    // Mantener bloqueadas todas las luces generales del interior y el derrame de luces exteriores cada cuadro
                    // (para que si el usuario o algún script toca el switcher en vivo, nunca se vuelvan a prender las luces en TERROR interior)
                    EnforceInteriorLightsSuppressedEveryFrame();
                    ApplyCurrentAmbientAndInspectionFloor();
                }

                if (player != null && shouldApplyInteriorDarkness != _lastShadowsSuppressed)
                {
                    player.SyncSceneLightShadows(shouldApplyInteriorDarkness);
                    _lastShadowsSuppressed = shouldApplyInteriorDarkness;
                }

                UpdateAudioState(shouldApplyInteriorDarkness);
            }
            finally
            {
                _inSync = false;
            }
        }

        private void EnforceInteriorLightsSuppressedEveryFrame()
        {
            if (switcher != null)
            {
                if (switcher.grupoLucesInteriorNoche != null && switcher.grupoLucesInteriorNoche.activeSelf)
                    switcher.grupoLucesInteriorNoche.SetActive(false);
                if (switcher.grupoLucesInteriorTerror != null && switcher.grupoLucesInteriorTerror.activeSelf)
                    switcher.grupoLucesInteriorTerror.SetActive(false);
            }

            for (int i = 0; i < _suppressedTrackers.Count; i++)
            {
                var tr = _suppressedTrackers[i];
                if (tr == null) continue;
                if (tr.enabled) tr.enabled = false;
                if (tr.nightGroup != null && tr.nightGroup.activeSelf) tr.nightGroup.SetActive(false);
                if (tr.terrorGroup != null && tr.terrorGroup.activeSelf) tr.terrorGroup.SetActive(false);
            }

            for (int i = 0; i < _suppressedLegacyTerrorLights.Count; i++)
            {
                var l = _suppressedLegacyTerrorLights[i];
                if (l != null && l.enabled) l.enabled = false;
            }
        }

        private void EnterExtremeTerrorDarkness()
        {
            _origAmbientMode = RenderSettings.ambientMode;
            _origSky = RenderSettings.ambientSkyColor;
            _origEquator = RenderSettings.ambientEquatorColor;
            _origGround = RenderSettings.ambientGroundColor;
            _origFogColor = RenderSettings.fogColor;
            _origFogDensity = RenderSettings.fogDensity;

            // 1. Apagar TODAS las luces interiores generales en TERROR (tanto de PB como de PA, en grupos NOCHE, TERROR, aulas, baños y auditorio)
            //    y forzar que Docencia2InteriorModeTracker no reactive los grupos de luces generales mientras el jugador esté en Oscuridad Extrema:
            _suppressedLegacyTerrorLights.Clear();
            _suppressedTrackers.Clear();
            foreach (var tracker in FindObjectsByType<Docencia2InteriorModeTracker>(FindObjectsInactive.Include))
            {
                if (tracker != null && tracker.enabled)
                {
                    tracker.enabled = false;
                    _suppressedTrackers.Add(tracker);
                }
                if (tracker != null && tracker.nightGroup != null) tracker.nightGroup.SetActive(false);
                if (tracker != null && tracker.terrorGroup != null) tracker.terrorGroup.SetActive(false);
            }

            foreach (string rootName in new[] { "PB_Interior", "PA_Interior" })
            {
                var rGo = GameObject.Find(rootName);
                if (rGo == null) continue;
                foreach (var l in rGo.GetComponentsInChildren<Light>(true))
                {
                    if (l != null)
                    {
                        if (l.enabled) l.enabled = false;
                        _suppressedLegacyTerrorLights.Add(l);
                    }
                }
            }

            // 1b. Evitar que CUALQUIER luz exterior ("Iluminacion_Terror_Docencia2", "Iluminacion_Cinematografica_Docencia2" o Directional Light exterior)
            //     atraviese los muros hacia el interior en modo TERROR (quitando las capas interiores 8 y 30 de su cullingMask en Play Mode)
            _cachedExteriorLightMasks.Clear();
            foreach (var l in FindObjectsByType<Light>(FindObjectsInactive.Include))
            {
                if (l == null) continue;
                if (player != null && l.transform.IsChildOf(player.transform)) continue;
                if (terrorAtmosphereRoot != null && l.transform.IsChildOf(terrorAtmosphereRoot.transform)) continue;
                _cachedExteriorLightMasks[l] = l.cullingMask;
                l.cullingMask &= ~((1 << 8) | (1 << 30));
            }

            // 2. Oscurecer los paneles LED emisivos generales para que no brillen como si estuvieran encendidos
            if (_emissivePanelRenderers != null)
            {
                foreach (var r in _emissivePanelRenderers)
                {
                    if (r != null) r.SetPropertyBlock(_darkPanelMpb);
                }
            }
            foreach (var r in emergencyPanelRenderers)
            {
                if (r != null) r.SetPropertyBlock(_amberPanelMpb);
            }
            if (dyingFluorescentRenderer != null)
            {
                dyingFluorescentRenderer.SetPropertyBlock(_dyingFluorescentMpb);
            }

            // 3. Desactivar Reflection Probes en los renderers interiores para que ningún probe aclare el interior en modo TERROR
            if (_cachedInteriorRenderers != null)
            {
                for (int i = 0; i < _cachedInteriorRenderers.Length; i++)
                {
                    var r = _cachedInteriorRenderers[i];
                    if (r == null) continue;
                    r.reflectionProbeUsage = ReflectionProbeUsage.Off;

                    // En modo TERROR, activar proyección de sombras en todos los muros interiores y columnas
                    // para que la linterna del celular nunca atraviese los muros hacia los cuartos traseros:
                    string n = r.gameObject.name;
                    if (!n.StartsWith("Piso_") && !n.StartsWith("Plafon_") && !n.Contains("Panel") && !n.Contains("Difusor"))
                    {
                        r.shadowCastingMode = ShadowCastingMode.TwoSided;
                    }
                }
            }

            if (terrorAtmosphereRoot != null) terrorAtmosphereRoot.SetActive(true);
            if (interiorTerrorVolume != null)
            {
                interiorTerrorVolume.gameObject.SetActive(true);
                TuneInteriorTerrorVolumeProfile();
            }
            if (nearSilhouetteLight != null) nearSilhouetteLight.enabled = true;

            _darknessApplied = true;
            ApplyCurrentAmbientAndInspectionFloor();
        }

        void TuneInteriorTerrorVolumeProfile()
        {
            if (interiorTerrorVolume == null) return;
            var prof = Application.isPlaying ? interiorTerrorVolume.profile : interiorTerrorVolume.sharedProfile;
            if (prof == null) return;

            if (prof.TryGet<ColorAdjustments>(out var colorAdj))
            {
                colorAdj.postExposure.Override(-0.10f);
                colorAdj.contrast.Override(22f);
                colorAdj.colorFilter.Override(new Color(0.88f, 0.93f, 0.98f, 1f));
                colorAdj.saturation.Override(-28f);
            }

            if (prof.TryGet<LiftGammaGain>(out var lgg))
            {
                lgg.lift.Override(new Vector4(1.00f, 1.01f, 1.02f, 0.004f));
                lgg.gamma.Override(new Vector4(1.00f, 1.00f, 1.01f, 0.0f));
            }

            if (!prof.TryGet<Bloom>(out var bloom))
                bloom = prof.Add<Bloom>(true);
            if (bloom != null)
            {
                bloom.active = true;
                bloom.threshold.Override(1.38f);
                bloom.intensity.Override(0.22f);
                bloom.scatter.Override(0.55f);
            }
        }

        public void RestoreOriginalEnvironment()
        {
            if (!_darknessApplied) return;

            foreach (var kv in _cachedExteriorLightMasks)
            {
                if (kv.Key != null) kv.Key.cullingMask = kv.Value;
            }
            _cachedExteriorLightMasks.Clear();

            foreach (var tr in _suppressedTrackers)
            {
                if (tr != null) tr.enabled = true;
            }
            _suppressedTrackers.Clear();

            foreach (var l in _suppressedLegacyTerrorLights)
            {
                if (l != null) l.enabled = true;
            }
            _suppressedLegacyTerrorLights.Clear();

            if (_emissivePanelRenderers != null)
            {
                foreach (var r in _emissivePanelRenderers)
                {
                    if (r != null) r.SetPropertyBlock(null);
                }
            }

            if (_cachedInteriorRenderers != null && _cachedProbeUsages != null)
            {
                int count = Mathf.Min(_cachedInteriorRenderers.Length, _cachedProbeUsages.Length);
                for (int i = 0; i < count; i++)
                {
                    if (_cachedInteriorRenderers[i] != null)
                    {
                        _cachedInteriorRenderers[i].reflectionProbeUsage = _cachedProbeUsages[i];
                        if (_cachedShadowModes != null && i < _cachedShadowModes.Length)
                            _cachedInteriorRenderers[i].shadowCastingMode = _cachedShadowModes[i];
                    }
                }
            }

            if (terrorAtmosphereRoot != null) terrorAtmosphereRoot.SetActive(false);
            if (interiorTerrorVolume != null) interiorTerrorVolume.gameObject.SetActive(false);
            if (nearSilhouetteLight != null) nearSilhouetteLight.enabled = false;
            if (doorTestInspectionLight != null) doorTestInspectionLight.enabled = false;

            _darknessApplied = false;

            bool prevInSync = _inSync;
            _inSync = true;
            try
            {
                if (switcher != null && gameObject.scene.isLoaded)
                {
                    switcher.ApplyMode(switcher.modoActual, false);
                    foreach (var tracker in FindObjectsByType<Docencia2InteriorModeTracker>(FindObjectsInactive.Include))
                    {
                        if (tracker != null && tracker.gameObject.activeInHierarchy)
                            tracker.SendMessage("Update", SendMessageOptions.DontRequireReceiver);
                    }
                }
                else
                {
                    RenderSettings.ambientMode = _origAmbientMode;
                    RenderSettings.ambientSkyColor = _origSky;
                    RenderSettings.ambientEquatorColor = _origEquator;
                    RenderSettings.ambientGroundColor = _origGround;
                    RenderSettings.fogColor = _origFogColor;
                    RenderSettings.fogDensity = _origFogDensity;
                }
            }
            finally
            {
                _inSync = prevInSync;
            }
        }

        public void ApplyCurrentAmbientAndInspectionFloor()
        {
            if (!_darknessApplied) return;

            bool doorTestOpen = player != null && player.IsDoorPanelOpen;

            if (doorTestOpen)
            {
                // MODO PRUEBA (tecla G): enciende piso de luz suficiente para ver las puertas y la geometría, y lo apaga al salir
                RenderSettings.ambientMode = AmbientMode.Trilight;
                RenderSettings.ambientSkyColor = new Color(0.20f, 0.25f, 0.30f, 1f);
                RenderSettings.ambientEquatorColor = new Color(0.18f, 0.22f, 0.27f, 1f);
                RenderSettings.ambientGroundColor = new Color(0.15f, 0.19f, 0.23f, 1f);
                if (doorTestInspectionLight != null) doorTestInspectionLight.enabled = true;
            }
            else
            {
                if (doorTestInspectionLight != null) doorTestInspectionLight.enabled = false;

                // OSCURIDAD EXTREMA REAL: ambient intensity ~0.002–0.005 por defecto, escalable con el control de brillo (B / Pausa)
                // Si la linterna está apagada, las zonas quedan en penumbra abismal casi negra (apenas se distingue la silueta a 2 m sin caer en RGB 0,0,0)
                float ambScalar = Mathf.Lerp(0.0022f, 0.028f, brightnessLevel);
                RenderSettings.ambientMode = AmbientMode.Trilight;
                RenderSettings.ambientSkyColor = new Color(ambScalar * 0.72f, ambScalar * 0.96f, ambScalar * 1.15f, 1f);
                RenderSettings.ambientEquatorColor = new Color(ambScalar * 0.78f, ambScalar * 1.02f, ambScalar * 1.20f, 1f);
                RenderSettings.ambientGroundColor = new Color(ambScalar * 0.65f, ambScalar * 0.88f, ambScalar * 1.04f, 1f);

                // Niebla interior fría y densa que engulle el fondo de los pasillos más allá de los 7 m de la linterna
                RenderSettings.fog = true;
                RenderSettings.fogMode = FogMode.ExponentialSquared;
                RenderSettings.fogColor = new Color(0.002f + ambScalar * 0.08f, 0.003f + ambScalar * 0.12f, 0.005f + ambScalar * 0.16f, 1f);
                RenderSettings.fogDensity = 0.048f;

                if (nearSilhouetteLight != null)
                {
                    // Luz mínima no direccional de 2.2 m alrededor del jugador solo para leer bordes inmediatos de puertas sin aclarar el pasillo
                    nearSilhouetteLight.range = 2.2f;
                    nearSilhouetteLight.intensity = Mathf.Lerp(0.008f, 0.06f, brightnessLevel);
                }
            }
        }

        private void UpdateDyingFluorescentAndEmergencyLights(float time)
        {
            // Fluorescente moribundo con parpadeo irregular y micro-cortes marcados
            if (dyingFluorescentLight != null)
            {
                float n1 = Mathf.PerlinNoise(time * 2.4f, 13.7f);
                float n2 = Mathf.PerlinNoise(time * 18.5f, 4.3f);
                float sputter = (n1 > 0.40f) ? (n2 > 0.34f ? Mathf.Lerp(0.10f, 0.42f, n1 * n2) : 0.022f) : 0.014f;
                dyingFluorescentLight.intensity = sputter;

                if (dyingFluorescentRenderer != null && _dyingFluorescentMpb != null)
                {
                    Color em = new Color(0.42f, 0.65f, 0.60f, 1f) * (sputter * 1.5f);
                    _dyingFluorescentMpb.SetColor("_EmissionColor", em);
                    dyingFluorescentRenderer.SetPropertyBlock(_dyingFluorescentMpb);
                }
            }

            // Lámparas de emergencia ámbar tenues con leve ondulación eléctrica
            for (int i = 0; i < emergencyAmberLights.Count; i++)
            {
                var l = emergencyAmberLights[i];
                if (l == null) continue;
                float wave = 0.88f + (Mathf.PerlinNoise(time * 1.9f + i * 7.1f, 2.3f) - 0.5f) * 0.26f;
                l.intensity = 0.07f * wave;
            }
        }

        // ====================================================================
        // TENSIÓN SIN LUZ: Seguimiento de Zona + Eventos Progresivos + Silencios Súbitos
        // ====================================================================
        private void UpdateZoneTrackingAndTensionEvents(float time, float dt)
        {
            if (player == null) return;

            Vector2 planXZ = Docencia2FirstPersonPlayer.UnityToPlanXZ(player.transform.position);
            int floor = player.transform.position.y > 2.0f ? 1 : 0;
            string detectedZone = ClassifyZone(planXZ, floor);

            if (detectedZone != currentZoneName || Vector2.Distance(planXZ, _lastZoneAnchorPlan) > 9.5f || floor != _lastFloor)
            {
                currentZoneName = detectedZone;
                _lastZoneAnchorPlan = planXZ;
                _lastFloor = floor;
                timeInCurrentZone = Mathf.Max(0f, timeInCurrentZone * 0.35f); // Reduce tensión al cambiar de zona
            }
            else
            {
                timeInCurrentZone += dt;
            }

            // Silencios súbitos (el sonido grave se corta de golpe por 2.5 - 4.0 s antes de un crujido o evento)
            isSuddenSilenceActive = (time < _silenceEndTime);
            if (!isSuddenSilenceActive && time > _nextSilenceAllowedTime && timeInCurrentZone > 10f)
            {
                if (Mathf.PerlinNoise(time * 0.41f, 19.3f) > 0.76f)
                {
                    _silenceEndTime = time + Mathf.Lerp(2.4f, 4.1f, Mathf.PerlinNoise(time, 3.7f));
                    _nextSilenceAllowedTime = time + Mathf.Lerp(22f, 38f, Mathf.PerlinNoise(time * 0.2f, 8.1f));
                }
            }

            // Goteras lejanas suaves (intervalo más espaciado y volumen discreto para que nunca sea molesto)
            if (time > _nextDripTime && !isSuddenSilenceActive)
            {
                float dripInterval = Mathf.Lerp(11.0f, 6.5f, Mathf.Clamp01(timeInCurrentZone / 55f));
                _nextDripTime = time + dripInterval * Mathf.Lerp(0.85f, 1.35f, Mathf.PerlinNoise(time * 0.9f, 5.2f));
                PlaySpatialTensionClip(_waterDripClip, GetNearbyOffsetPosition(player.transform.position, 6f, 12f, 0.2f), 0.11f, 0.88f + Mathf.PerlinNoise(time, 1.1f) * 0.12f);
            }

            // Eventos de ambiente suaves por zona: aparecen más seguidos cuanto más tiempo pasa el jugador en una misma zona
            if (time > _nextAmbientEventTime)
            {
                float stayFactor = Mathf.Clamp01(timeInCurrentZone / 55f); // De 0 a 55 segundos en la misma zona
                float minWait = Mathf.Lerp(16.0f, 6.0f, stayFactor);
                float maxWait = Mathf.Lerp(26.0f, 10.5f, stayFactor);
                _nextAmbientEventTime = time + Mathf.Lerp(minWait, maxWait, Mathf.PerlinNoise(time * 0.63f, 11.4f));

                TriggerSoftAmbientZoneEvent(time, floor);
            }
        }

        private void TriggerSoftAmbientZoneEvent(float time, int playerFloor)
        {
            if (player == null) return;
            int eventType = Mathf.Abs((int)(time * 13.7f + timeInCurrentZone * 3.1f)) % 4;

            switch (eventType)
            {
                case 0:
                    // 1. Crujido suave de plafón o estructura arriba del jugador
                    PlaySpatialTensionClip(
                        _ceilingCreakClip,
                        player.transform.position + new Vector3(Mathf.Sin(time) * 3.5f, 1.4f, Mathf.Cos(time) * 3.5f),
                        0.15f,
                        0.88f + (Mathf.PerlinNoise(time, 2.1f) * 0.12f)
                    );
                    break;

                case 1:
                    // 2. Pasos apagados lejanos en la OTRA planta
                    float otherFloorY = (playerFloor == 0) ? 3.8f : 0.3f;
                    Vector3 otherFloorPos = new Vector3(
                        player.transform.position.x + Mathf.Sin(time * 1.7f) * 4.5f,
                        otherFloorY,
                        player.transform.position.z + Mathf.Cos(time * 1.3f) * 4.5f
                    );
                    PlaySpatialTensionClip(_otherFloorThudClip, otherFloorPos, 0.18f, 0.86f);
                    break;

                case 2:
                    // 3. Una puerta que se cierra o entreabre sola a lo lejos (a 8 - 18 m del jugador, fuera de su cara)
                    var distantDoor = FindDistantDoor(player.transform.position, 8.0f, 20.0f);
                    if (distantDoor != null)
                    {
                        if (distantDoor.isOpen)
                        {
                            distantDoor.SetOpenAnimated(false, false);
                        }
                        PlaySpatialTensionClip(_distantDoorCloseClip, distantDoor.transform.position, 0.20f, 0.92f);
                    }
                    else
                    {
                        PlaySpatialTensionClip(_distantDoorCloseClip, GetNearbyOffsetPosition(player.transform.position, 9f, 15f, 0f), 0.16f, 0.90f);
                    }
                    break;

                case 3:
                    // 4. Luz de emergencia que baja momentáneamente de intensidad (sin chispazo estridente)
                    if (emergencyAmberLights.Count > 0)
                    {
                        var l = emergencyAmberLights[Mathf.Abs((int)(time * 7f)) % emergencyAmberLights.Count];
                        if (l != null)
                        {
                            l.intensity = 0.02f;
                        }
                    }
                    break;
            }
        }

        private Docencia2InteractiveDoor FindDistantDoor(Vector3 playerPos, float minDist, float maxDist)
        {
            var all = FindObjectsByType<Docencia2InteractiveDoor>(FindObjectsInactive.Exclude);
            foreach (var d in all)
            {
                if (d == null) continue;
                float dist = Vector3.Distance(playerPos, d.transform.position);
                if (dist >= minDist && dist <= maxDist)
                    return d;
            }
            return null;
        }

        private static Vector3 GetNearbyOffsetPosition(Vector3 origin, float minR, float maxR, float yOffset)
        {
            float angle = Time.time * 2.37f;
            float r = Mathf.Lerp(minR, maxR, Mathf.PerlinNoise(Time.time * 0.5f, 4.4f));
            return origin + new Vector3(Mathf.Cos(angle) * r, yOffset, Mathf.Sin(angle) * r);
        }

        private static string ClassifyZone(Vector2 p, int floor)
        {
            string prefix = floor > 0 ? "PA_" : "PB_";
            if (p.y < 7.8f) return prefix + "Cubiculos_y_Banos_Norte";
            if (p.y < 15.5f) return prefix + "Pasillo_Canceles_Sec06";
            if (p.y < 23.2f) return p.x < 10.5f ? prefix + "Escalera" : prefix + "Vestibulo_Entrada";
            if (p.y < 38.6f) return p.x > 17.3f ? prefix + "Auditorio_AulasEste" : prefix + "Pasillo_Central";
            return prefix + "Plaza_Sur_y_Aulas";
        }

        // ====================================================================
        // SÍNTESIS DE AUDIO DE TENSIÓN SIN LUZ (SUPUESTO)
        // ====================================================================
        private void InitializeProceduralTensionAudio()
        {
            if (_droneClip == null) _droneClip = CreateDeepDroneClip();
            if (_breathClip == null) _breathClip = CreateBreathingClip();
            if (_lampHumClip == null) _lampHumClip = CreateLampHumClip();
            if (_waterDripClip == null) _waterDripClip = CreateWaterDripClip();
            if (_ceilingCreakClip == null) _ceilingCreakClip = CreateStructuralCreakClip();
            if (_distantDoorCloseClip == null) _distantDoorCloseClip = CreateDistantDoorThudClip();
            if (_otherFloorThudClip == null) _otherFloorThudClip = CreateOtherFloorStepsClip();
            if (_bulbPopClip == null) _bulbPopClip = CreateElectricFlickerBuzzClip();

            if (_droneSource == null)
            {
                var go = new GameObject("Audio_Ambiente_Grave_Terror");
                go.transform.SetParent(transform, false);
                _droneSource = go.AddComponent<AudioSource>();
                _droneSource.clip = _droneClip;
                _droneSource.loop = true;
                _droneSource.spatialBlend = 0f;
                _droneSource.volume = 0f;
                _droneSource.Play();
            }

            if (_breathSource == null)
            {
                var go = new GameObject("Audio_Respiracion_Jugador_Terror");
                go.transform.SetParent(transform, false);
                _breathSource = go.AddComponent<AudioSource>();
                _breathSource.clip = _breathClip;
                _breathSource.loop = true;
                _breathSource.spatialBlend = 0f;
                _breathSource.volume = 0f;
                _breathSource.Play();
            }

            if (_spatialEventSource == null)
            {
                var go = new GameObject("Audio_Eventos_Espaciales_Terror");
                go.transform.SetParent(transform, false);
                _spatialEventSource = go.AddComponent<AudioSource>();
                _spatialEventSource.spatialBlend = 0.85f;
                _spatialEventSource.minDistance = 2.0f;
                _spatialEventSource.maxDistance = 24.0f;
                _spatialEventSource.playOnAwake = false;
            }

            // Zumbido espacial en lámparas desactivado por defecto para eliminar cualquier pitido/zumbido que moleste el oído
            var humTargets = new List<Light>();
            if (dyingFluorescentLight != null) humTargets.Add(dyingFluorescentLight);
            humTargets.AddRange(emergencyAmberLights.Where(l => l != null));
            _lampHumSources = new AudioSource[humTargets.Count];
            for (int i = 0; i < humTargets.Count; i++)
            {
                var src = humTargets[i].gameObject.GetComponent<AudioSource>();
                if (src == null) src = humTargets[i].gameObject.AddComponent<AudioSource>();
                src.playOnAwake = false;
                src.clip = _lampHumClip;
                src.loop = true;
                src.spatialBlend = 1.0f;
                src.minDistance = 1.0f;
                src.maxDistance = 6.0f;
                src.volume = 0f;
                src.Stop();
                _lampHumSources[i] = src;
            }
        }

        private void UpdateAudioState(bool isTerror)
        {
            if (_droneSource != null)
            {
                if (!enableContinuousDroneAndHum || !isTerror || isSuddenSilenceActive)
                {
                    _droneSource.volume = 0f;
                    if (_droneSource.isPlaying) _droneSource.Stop();
                }
                else
                {
                    if (!_droneSource.isPlaying) _droneSource.Play();
                    float targetDroneVol = Mathf.Lerp(0.06f, 0.12f, Mathf.Clamp01(timeInCurrentZone / 45f));
                    _droneSource.volume = Mathf.MoveTowards(_droneSource.volume, targetDroneVol, Time.deltaTime * 0.5f);
                }
            }

            if (_breathSource != null)
            {
                if (!enableContinuousDroneAndHum || !isTerror)
                {
                    _breathSource.volume = 0f;
                    if (_breathSource.isPlaying) _breathSource.Stop();
                }
                else
                {
                    if (!_breathSource.isPlaying) _breathSource.Play();
                    bool flashlightDark = player != null && !player.flashlightOn;
                    float targetBreathVol = flashlightDark ? 0.10f : 0.04f;
                    _breathSource.volume = Mathf.MoveTowards(_breathSource.volume, targetBreathVol, Time.deltaTime * 0.5f);
                }
            }

            if (_lampHumSources != null)
            {
                foreach (var src in _lampHumSources)
                {
                    if (src != null)
                    {
                        if (!enableContinuousDroneAndHum || !isTerror || isSuddenSilenceActive)
                        {
                            src.mute = true;
                            if (src.isPlaying) src.Stop();
                        }
                        else
                        {
                            src.mute = false;
                            src.volume = 0.04f;
                            if (src.gameObject.activeInHierarchy && src.enabled && !src.isPlaying)
                            {
                                src.Play();
                            }
                        }
                    }
                }
            }
        }

        private void PlaySpatialTensionClip(AudioClip clip, Vector3 worldPos, float volume, float pitch)
        {
            if (clip == null || _spatialEventSource == null) return;
            _spatialEventSource.transform.position = worldPos;
            _spatialEventSource.pitch = pitch;
            _spatialEventSource.PlayOneShot(clip, volume);
        }

        private static AudioClip CreateDeepDroneClip()
        {
            int sr = 22050;
            int len = sr * 6; // 6 s loopable con fundido cruzado suave en los bordes (cero clics de loop)
            float[] data = new float[len];
            for (int i = 0; i < len; i++)
            {
                float t = i / (float)sr;
                // Sub-bajo muy suave (44 Hz + 66 Hz) sin ruido de alta frecuencia
                float sub = Mathf.Sin(2f * Mathf.PI * 44f * t) * 0.45f;
                float fifth = Mathf.Sin(2f * Mathf.PI * 66f * t) * 0.20f;
                float edgeFade = 1.0f;
                if (t < 0.25f) edgeFade = t / 0.25f;
                else if (t > 5.75f) edgeFade = (6.0f - t) / 0.25f;
                data[i] = Mathf.Clamp((sub + fifth) * 0.28f * edgeFade, -1f, 1f);
            }
            var c = AudioClip.Create("Terror_SubDrone_SUPUESTO", len, 1, sr, false);
            c.SetData(data, 0);
            return c;
        }

        private static AudioClip CreateBreathingClip()
        {
            int sr = 22050;
            int len = sr * 4;
            float[] data = new float[len];
            float prev = 0f;
            for (int i = 0; i < len; i++)
            {
                float t = (i / (float)sr) % 4.0f;
                float env = 0f;
                if (t < 1.35f) env = Mathf.Sin((t / 1.35f) * Mathf.PI) * 0.65f;
                else if (t > 1.75f && t < 3.45f) env = Mathf.Sin(((t - 1.75f) / 1.70f) * Mathf.PI) * 0.85f;

                // Aire filtrado en paso-bajo sin ningún pitido senoidal (0 Hz de tono puro)
                float rawAir = (Mathf.PerlinNoise(i * 0.08f, 3.3f) - 0.5f) * 0.35f;
                prev = prev * 0.88f + rawAir * 0.12f;
                data[i] = Mathf.Clamp(prev * env * 0.30f, -1f, 1f);
            }
            var c = AudioClip.Create("Terror_Respiracion_SUPUESTO", len, 1, sr, false);
            c.SetData(data, 0);
            return c;
        }

        private static AudioClip CreateLampHumClip()
        {
            int sr = 22050;
            int len = sr * 2;
            float[] data = new float[len];
            for (int i = 0; i < len; i++)
            {
                float t = i / (float)sr;
                float h60 = Mathf.Sin(2f * Mathf.PI * 60f * t) * 0.18f;
                data[i] = Mathf.Clamp(h60, -1f, 1f);
            }
            var c = AudioClip.Create("Terror_LampHum_SUPUESTO", len, 1, sr, false);
            c.SetData(data, 0);
            return c;
        }

        private static AudioClip CreateWaterDripClip()
        {
            int sr = 22050;
            int len = (int)(sr * 0.30f);
            float[] data = new float[len];
            for (int i = 0; i < len; i++)
            {
                float t = i / (float)sr;
                // Gota de agua suave y grave (420 -> 580 Hz, sin agudos que lastimen)
                float freq = Mathf.Lerp(420f, 580f, Mathf.Clamp01(t / 0.08f));
                float env = Mathf.Exp(-t * 32f);
                data[i] = Mathf.Clamp(Mathf.Sin(2f * Mathf.PI * freq * t) * env * 0.28f, -1f, 1f);
            }
            var c = AudioClip.Create("Terror_Gotera_SUPUESTO", len, 1, sr, false);
            c.SetData(data, 0);
            return c;
        }

        private static AudioClip CreateStructuralCreakClip()
        {
            int sr = 22050;
            int len = (int)(sr * 0.85f);
            float[] data = new float[len];
            for (int i = 0; i < len; i++)
            {
                float t = i / (float)sr;
                float env = Mathf.Sin(Mathf.Clamp01(t / 0.85f) * Mathf.PI);
                float f = 105f + Mathf.Sin(t * 19f) * 28f;
                float pulse = Mathf.Sin(2f * Mathf.PI * f * t) * (0.5f + 0.5f * Mathf.Sin(t * 62f));
                data[i] = Mathf.Clamp(pulse * env * 0.48f, -1f, 1f);
            }
            var c = AudioClip.Create("Terror_CrujidoPlafon_SUPUESTO", len, 1, sr, false);
            c.SetData(data, 0);
            return c;
        }

        private static AudioClip CreateDistantDoorThudClip()
        {
            int sr = 22050;
            int len = (int)(sr * 0.55f);
            float[] data = new float[len];
            for (int i = 0; i < len; i++)
            {
                float t = i / (float)sr;
                float creak = t < 0.22f ? Mathf.Sin(2f * Mathf.PI * (185f - t * 140f) * t) * 0.28f : 0f;
                float thudT = Mathf.Max(0f, t - 0.20f);
                float thud = t >= 0.20f ? Mathf.Sin(2f * Mathf.PI * 78f * thudT) * Mathf.Exp(-thudT * 22f) * 0.75f : 0f;
                data[i] = Mathf.Clamp((creak + thud) * 0.65f, -1f, 1f);
            }
            var c = AudioClip.Create("Terror_PuertaLejana_SUPUESTO", len, 1, sr, false);
            c.SetData(data, 0);
            return c;
        }

        private static AudioClip CreateOtherFloorStepsClip()
        {
            int sr = 22050;
            int len = (int)(sr * 1.35f);
            float[] data = new float[len];
            float[] stepTimes = { 0.05f, 0.52f, 0.98f };
            for (int i = 0; i < len; i++)
            {
                float t = i / (float)sr;
                float sample = 0f;
                foreach (float st in stepTimes)
                {
                    if (t >= st)
                    {
                        float dt = t - st;
                        sample += Mathf.Sin(2f * Mathf.PI * 68f * dt) * Mathf.Exp(-dt * 26f) * 0.55f;
                    }
                }
                data[i] = Mathf.Clamp(sample, -1f, 1f);
            }
            var c = AudioClip.Create("Terror_PasosOtraPlanta_SUPUESTO", len, 1, sr, false);
            c.SetData(data, 0);
            return c;
        }

        private static AudioClip CreateElectricFlickerBuzzClip()
        {
            int sr = 22050;
            int len = (int)(sr * 0.25f);
            float[] data = new float[len];
            for (int i = 0; i < len; i++)
            {
                float t = i / (float)sr;
                float env = Mathf.Exp(-t * 18f);
                float spark = (Mathf.PerlinNoise(i * 0.45f, 9.2f) - 0.5f) * 0.7f;
                float hum = Mathf.Sin(2f * Mathf.PI * 180f * t) * 0.4f;
                data[i] = Mathf.Clamp((spark + hum) * env * 0.55f, -1f, 1f);
            }
            var c = AudioClip.Create("Terror_FalloLuz_SUPUESTO", len, 1, sr, false);
            c.SetData(data, 0);
            return c;
        }
    }
}
