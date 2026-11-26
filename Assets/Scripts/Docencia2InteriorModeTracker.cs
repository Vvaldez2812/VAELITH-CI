// Assets/Scripts/Docencia2InteriorModeTracker.cs
using UnityEngine;

namespace Vaelith.Docencia2
{
    /// <summary>
    /// Componente de runtime para coordinar el encendido/apagado interior con el Docencia2ModeSwitcher
    /// </summary>
    [ExecuteAlways]
    public class Docencia2InteriorModeTracker : MonoBehaviour
    {
        public GameObject nightGroup;
        public GameObject terrorGroup;
        public Docencia2ModeSwitcher switcher;

        private Docencia2VisualMode _lastMode = (Docencia2VisualMode)(-1);

        private void OnEnable()
        {
            _lastMode = (Docencia2VisualMode)(-1);
        }

        private void Update()
        {
            if (switcher == null) switcher = Object.FindAnyObjectByType<Docencia2ModeSwitcher>();
            if (switcher == null) return;

            bool isTerror = (switcher.modoActual == Docencia2VisualMode.Terror);
            bool needsSync = (switcher.modoActual != _lastMode)
                || (nightGroup != null && nightGroup.activeSelf != !isTerror)
                || (terrorGroup != null && terrorGroup.activeSelf != isTerror);

            if (needsSync)
            {
                _lastMode = switcher.modoActual;
                if (nightGroup != null && nightGroup.activeSelf != !isTerror) nightGroup.SetActive(!isTerror);
                if (terrorGroup != null && terrorGroup.activeSelf != isTerror) terrorGroup.SetActive(isTerror);
            }
        }
    }
}
