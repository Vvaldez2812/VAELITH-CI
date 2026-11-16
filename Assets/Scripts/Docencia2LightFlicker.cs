using UnityEngine;

namespace Vaelith.Docencia2
{
    /// <summary>
    /// Controlador de parpadeo orgánico e irregular para lámparas cálidas en el modo TERROR.
    /// Sin patrón repetitivo evidente: combina ruido Perlin multi-frecuencia con micro-caídas y apagones cortos aleatorios.
    /// </summary>
    [ExecuteAlways]
    public class Docencia2LightFlicker : MonoBehaviour
    {
        [Tooltip("Luz principal controlada por este parpadeo")]
        public Light targetLight;

        [Tooltip("Luz secundaria opcional (charco en el suelo asociado)")]
        public Light secondaryPoolLight;

        [Tooltip("Cono volumétrico asociado para modular su opacidad junto con la lámpara")]
        public GameObject volumetricConeRoot;

        [Tooltip("Intensidad base de la lámpara principal en modo Terror")]
        public float baseIntensity = 42.0f;

        [Tooltip("Intensidad base del charco de suelo asociado en modo Terror")]
        public float baseSecondaryIntensity = 18.0f;

        [Tooltip("Si es true, actúa como lámpara casi muerta con luz débil y tintineo inestable")]
        public bool isDyingLamp = false;

        [Tooltip("Semilla única para desfasar el parpadeo entre lámparas")]
        public float noiseSeed = 17.3f;

        [Tooltip("Factor estático determinista (0..1) usado en modo Editor fuera de Play mode para capturas limpias")]
        public float editorPreviewFactor = 0.86f;

        private float _blackoutEndTime = -1f;
        private float _nextBlackoutCheckTime = 2.0f;
        private MaterialPropertyBlock _mpb;

        private void OnEnable()
        {
            if (targetLight == null) targetLight = GetComponent<Light>();
            ApplyFactor(Application.isPlaying ? ComputeLiveFactor(Time.time) : editorPreviewFactor);
        }

        private void Update()
        {
            if (!Application.isPlaying)
            {
                ApplyFactor(editorPreviewFactor);
                return;
            }

            float factor = ComputeLiveFactor(Time.time);
            ApplyFactor(factor);
        }

        public float ComputeLiveFactor(float t)
        {
            // Ruido orgánico no periódico de 3 octavas inarmónicas
            float nSlow = Mathf.PerlinNoise(t * 1.37f + noiseSeed, noiseSeed * 0.73f);
            float nMid  = Mathf.PerlinNoise(t * 5.83f + noiseSeed * 2.1f, t * 0.91f + 4.7f);
            float nFast = Mathf.PerlinNoise(t * 14.9f + noiseSeed * 3.7f, 11.3f);

            if (isDyingLamp)
            {
                // Lámpara casi muerta: permanece débil (18% a 55% de su baja intensidad) con tintineo irregular y breves micro-cortes
                if (t > _nextBlackoutCheckTime)
                {
                    _nextBlackoutCheckTime = t + Mathf.Lerp(0.9f, 3.4f, Mathf.PerlinNoise(t * 0.7f, noiseSeed));
                    if (Mathf.PerlinNoise(t * 2.9f, noiseSeed + 9.1f) > 0.58f)
                    {
                        _blackoutEndTime = t + Mathf.Lerp(0.06f, 0.24f, nFast);
                    }
                }

                if (t < _blackoutEndTime)
                {
                    return 0.04f; // Casi apagada durante el micro-corte
                }

                float sputter = Mathf.Clamp01(nSlow * 0.45f + nMid * 0.35f + nFast * 0.20f);
                return Mathf.Lerp(0.22f, 0.78f, sputter);
            }
            else
            {
                // Lámpara con variación irregular sutil y algún apagón corto ocasional
                if (t > _nextBlackoutCheckTime)
                {
                    _nextBlackoutCheckTime = t + Mathf.Lerp(2.5f, 7.5f, Mathf.PerlinNoise(t * 0.43f, noiseSeed));
                    if (Mathf.PerlinNoise(t * 1.9f, noiseSeed + 5.4f) > 0.68f)
                    {
                        _blackoutEndTime = t + Mathf.Lerp(0.05f, 0.16f, nFast);
                    }
                }

                if (t < _blackoutEndTime)
                {
                    return 0.12f; // Apagón corto aleatorio
                }

                float organic = Mathf.Clamp01(0.62f + (nSlow - 0.5f) * 0.35f + (nMid - 0.5f) * 0.28f + (nFast - 0.5f) * 0.16f);
                return Mathf.Clamp(organic, 0.48f, 1.05f);
            }
        }

        public void ApplyFactor(float factor)
        {
            if (targetLight != null)
            {
                targetLight.intensity = baseIntensity * factor;
            }
            if (secondaryPoolLight != null)
            {
                secondaryPoolLight.intensity = baseSecondaryIntensity * factor;
            }
            if (volumetricConeRoot != null)
            {
                if (_mpb == null) _mpb = new MaterialPropertyBlock();
                Renderer[] renderers = volumetricConeRoot.GetComponentsInChildren<Renderer>(true);
                float alpha = (isDyingLamp ? 0.042f : 0.068f) * factor;
                Color c = new Color(1.0f, 0.72f, 0.36f, alpha);
                foreach (Renderer r in renderers)
                {
                    if (r == null) continue;
                    r.GetPropertyBlock(_mpb);
                    _mpb.SetColor("_BaseColor", c);
                    r.SetPropertyBlock(_mpb);
                }
            }
        }
    }
}
