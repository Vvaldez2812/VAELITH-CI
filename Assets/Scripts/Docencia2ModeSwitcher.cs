using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Vaelith.Docencia2
{
    public enum Docencia2VisualMode
    {
        Noche = 0,
        Terror = 1
    }

    /// <summary>
    /// Sistema para alternar entre el modo NOCHE (aprobado intacto) y el modo TERROR atmosférico realista.
    /// - En el Inspector: cambia el enum 'modoActual' (se aplica automáticamente fuera de Play mode vía OnValidate).
    /// - En Play Mode: presiona la tecla T para alternar en vivo entre NOCHE y TERROR.
    /// </summary>
    [ExecuteAlways]
    public class Docencia2ModeSwitcher : MonoBehaviour
    {
        [Serializable]
        public class RendererMaterialPair
        {
            public Renderer targetRenderer;
            public Material nightMaterial;
            public Material terrorMaterial;
            public Material[] nightMaterialsArray;
            public Material[] terrorMaterialsArray;
        }

        [Header("Selector de Modo (Tecla 'T' en Play Mode)")]
        public Docencia2VisualMode modoActual = Docencia2VisualMode.Noche;
        public KeyCode toggleKey = KeyCode.T;

        [Header("Grupos de Luces y Conos")]
        public GameObject grupoLucesNoche;
        public GameObject grupoConosNoche;
        public GameObject grupoNieblaNoche;

        public GameObject grupoLucesTerror;
        public GameObject grupoConosTerror;
        public GameObject grupoNieblaTerror;

        [Header("Grupos de Luces Interiores (PB_Interior)")]
        public GameObject grupoLucesInteriorNoche;
        public GameObject grupoLucesInteriorTerror;

        [Header("Volumes de Post-Procesado")]
        public GameObject volumeNoche;
        public GameObject volumeTerror;

        [Header("Skyboxes")]
        public Material skyboxNoche;
        public Material skyboxTerror;

        [Header("Ambiente y Niebla — Modo NOCHE (Intacto)")]
        public Color nightAmbientSky     = new Color(0.115f, 0.155f, 0.225f, 1.0f);
        public Color nightAmbientEquator = new Color(0.115f, 0.145f, 0.195f, 1.0f);
        public Color nightAmbientGround  = new Color(0.110f, 0.132f, 0.178f, 1.0f);
        public Color nightFogColor       = new Color(0.102f, 0.141f, 0.200f, 1.0f); // #1A2433
        public float nightFogDensity     = 0.0085f;

        [Header("Ambiente y Niebla — Modo TERROR (#070D12 / #0F1A1C)")]
        public Color terrorAmbientSky     = new Color(0.055f, 0.105f, 0.138f, 1.0f); // Proporción #070D12 azul-verdoso frío para que las sombras nunca sean negro puro
        public Color terrorAmbientEquator = new Color(0.072f, 0.128f, 0.155f, 1.0f); // Transición horizonte-fachada legible
        public Color terrorAmbientGround  = new Color(0.065f, 0.112f, 0.132f, 1.0f); // Suelo en sombra profundo pero legible
        public Color terrorFogColor       = new Color(0.059f, 0.102f, 0.110f, 1.0f); // #0F1A1C azul-gris verdosa densa
        public float terrorFogDensity     = 0.025f;                                  // Rango 0.025 - 0.045 (el edificio se pierde un poco con la distancia sin desaparecer)

        [Header("Intercambio de Materiales (Concreto, Suelo, Ventanas, Luminarias)")]
        public List<RendererMaterialPair> materialSwaps = new List<RendererMaterialPair>();

        [Header("Cámaras y Reflection Probes")]
        public Camera[] sceneCameras;
        public ReflectionProbe[] reflectionProbes;

        [Header("SSAO Renderer Feature (Opcional)")]
        public ScriptableRendererFeature ssaoFeature;

        private Docencia2VisualMode _lastAppliedMode = (Docencia2VisualMode)(-1);

        private void OnEnable()
        {
            ApplyMode(modoActual, false);
        }

        private void OnValidate()
        {
            if (_lastAppliedMode != modoActual)
            {
                ApplyMode(modoActual, false);
            }
        }

        private void Update()
        {
            if (!Application.isPlaying) return;

            bool allowShortcuts = Docencia2FirstPersonPlayer.Instance == null || Docencia2FirstPersonPlayer.Instance.showDebugHudAndShortcuts;
            bool pressed = false;
#if ENABLE_INPUT_SYSTEM
            if (allowShortcuts && Keyboard.current != null && Keyboard.current.tKey.wasPressedThisFrame)
            {
                pressed = true;
            }

#endif

            if (pressed)
            {
                ToggleMode();
            }
        }

        [ContextMenu("Alternar NOCHE / TERROR")]
        public void ToggleMode()
        {
            Docencia2VisualMode next = (modoActual == Docencia2VisualMode.Noche)
                ? Docencia2VisualMode.Terror
                : Docencia2VisualMode.Noche;
            ApplyMode(next, true);
        }

        public void ApplyMode(Docencia2VisualMode mode, bool reRenderProbes = true)
        {
            modoActual = mode;
            _lastAppliedMode = mode;
            bool isTerror = (mode == Docencia2VisualMode.Terror);

            // 1. Activar / Desactivar grupos de Luces, Conos Volumétricos y Bancos de Niebla
            if (grupoLucesNoche != null) grupoLucesNoche.SetActive(!isTerror);
            if (grupoConosNoche != null) grupoConosNoche.SetActive(!isTerror);
            if (grupoNieblaNoche != null) grupoNieblaNoche.SetActive(!isTerror);

            if (grupoLucesTerror != null) grupoLucesTerror.SetActive(isTerror);
            if (grupoConosTerror != null) grupoConosTerror.SetActive(isTerror);
            if (grupoNieblaTerror != null) grupoNieblaTerror.SetActive(isTerror);

            if (grupoLucesInteriorNoche != null) grupoLucesInteriorNoche.SetActive(!isTerror);
            if (grupoLucesInteriorTerror != null) grupoLucesInteriorTerror.SetActive(isTerror);

            // 2. Activar / Desactivar Volumes (Global_Volume_PostProcesado vs Global_Volume_Terror)
            if (volumeNoche != null) volumeNoche.SetActive(!isTerror);
            if (volumeTerror != null) volumeTerror.SetActive(isTerror);

            // 3. Ambiente, Niebla y Skybox
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor     = isTerror ? terrorAmbientSky     : nightAmbientSky;
            RenderSettings.ambientEquatorColor = isTerror ? terrorAmbientEquator : nightAmbientEquator;
            RenderSettings.ambientGroundColor  = isTerror ? terrorAmbientGround  : nightAmbientGround;

            RenderSettings.fog        = true;
            RenderSettings.fogMode    = FogMode.Exponential;
            RenderSettings.fogColor   = isTerror ? terrorFogColor   : nightFogColor;
            RenderSettings.fogDensity = isTerror ? terrorFogDensity : nightFogDensity;

            if (isTerror && skyboxTerror != null)
                RenderSettings.skybox = skyboxTerror;
            else if (!isTerror && skyboxNoche != null)
                RenderSettings.skybox = skyboxNoche;

            // 4. Actualizar color de fondo de las cámaras
            if (sceneCameras != null)
            {
                Color bg = isTerror ? terrorFogColor : nightFogColor;
                foreach (Camera c in sceneCameras)
                {
                    if (c != null) c.backgroundColor = bg;
                }
            }

            // 5. Intercambiar materiales (Concreto con más humedad/goteo, Suelo con parches húmedos 0.30-0.45, Ventanas apagadas salvo 2 débiles, Luminarias apagadas salvo las 3 encendidas)
            if (materialSwaps != null)
            {
                for (int i = 0; i < materialSwaps.Count; i++)
                {
                    var pair = materialSwaps[i];
                    if (pair != null && pair.targetRenderer != null)
                    {
                        Material[] targetArr = isTerror ? pair.terrorMaterialsArray : pair.nightMaterialsArray;
                        if (targetArr != null && targetArr.Length > 0)
                        {
                            pair.targetRenderer.sharedMaterials = targetArr;
                        }
                        else
                        {
                            Material targetMat = isTerror ? pair.terrorMaterial : pair.nightMaterial;
                            if (targetMat != null)
                            {
                                pair.targetRenderer.sharedMaterial = targetMat;
                            }
                        }
                    }
                }
            }

            // 6. Asegurar que los flickers en modo Terror tengan su intensidad inicial aplicada
            if (isTerror && grupoLucesTerror != null)
            {
                var flickers = grupoLucesTerror.GetComponentsInChildren<Docencia2LightFlicker>(true);
                foreach (var fl in flickers)
                {
                    if (fl != null)
                    {
                        fl.ApplyFactor(Application.isPlaying ? fl.ComputeLiveFactor(Time.time) : fl.editorPreviewFactor);
                    }
                }
            }

            // 7. Re-renderizar Reflection Probes exteriores (apagando temporalmente las luces interiores para no alterar jamás el look exterior)
            if (reRenderProbes && reflectionProbes != null)
            {
                bool prevNightInt = grupoLucesInteriorNoche != null && grupoLucesInteriorNoche.activeSelf;
                bool prevTerrorInt = grupoLucesInteriorTerror != null && grupoLucesInteriorTerror.activeSelf;
                if (grupoLucesInteriorNoche != null) grupoLucesInteriorNoche.SetActive(false);
                if (grupoLucesInteriorTerror != null) grupoLucesInteriorTerror.SetActive(false);

                foreach (ReflectionProbe rp in reflectionProbes)
                {
                    if (rp != null && rp.gameObject.activeInHierarchy)
                    {
                        rp.RenderProbe();
                    }
                }

                if (grupoLucesInteriorNoche != null) grupoLucesInteriorNoche.SetActive(prevNightInt);
                if (grupoLucesInteriorTerror != null) grupoLucesInteriorTerror.SetActive(prevTerrorInt);
            }

            if (Application.isPlaying && gameObject.scene.isLoaded &&
                Docencia2TerrorDarknessController.Instance != null &&
                Docencia2TerrorDarknessController.Instance.isActiveAndEnabled)
            {
                Docencia2TerrorDarknessController.Instance.SyncState();
            }
        }
    }
}
