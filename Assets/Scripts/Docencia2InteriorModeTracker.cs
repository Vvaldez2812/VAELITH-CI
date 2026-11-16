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

        private void Update()
        {
            if (switcher == null) switcher = Object.FindAnyObjectByType<Docencia2ModeSwitcher>();
            if (switcher == null) return;

            if (switcher.modoActual != _lastMode)
            {
                _lastMode = switcher.modoActual;
                bool isTerror = (_lastMode == Docencia2VisualMode.Terror);
                if (nightGroup != null) nightGroup.SetActive(!isTerror);
                if (terrorGroup != null) terrorGroup.SetActive(isTerror);
            }
        }
    }
}
