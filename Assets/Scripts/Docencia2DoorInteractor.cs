using UnityEngine;
using UnityEngine.InputSystem;

namespace Vaelith.Docencia2
{
    // Mantiene compatibilidad de escena; si existe Docencia2FirstPersonPlayer activo, delega la interacción E/resaltado a este.
    public class Docencia2DoorInteractor : MonoBehaviour
    {
        public float interactionDistance = 3f;

        void Update()
        {
            if (Docencia2FirstPersonPlayer.Instance != null && Docencia2FirstPersonPlayer.Instance.isActiveAndEnabled)
                return;

            bool key = Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame;
            bool click = Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame;
            if (!key && !click) return;

            Camera camera = Camera.main;
            if (!camera || !camera.isActiveAndEnabled)
            {
                foreach (var c in Camera.allCameras)
                {
                    if (c.isActiveAndEnabled && c.targetTexture == null)
                    {
                        camera = c;
                        break;
                    }
                }
            }
            if (!camera) return;

            var ray = key
                ? camera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f))
                : camera.ScreenPointToRay(Mouse.current.position.ReadValue());

            if (Physics.Raycast(ray, out var hit, interactionDistance, ~0, QueryTriggerInteraction.Ignore))
            {
                var door = hit.collider.GetComponentInParent<Docencia2InteractiveDoor>();
                if (door) door.Toggle(camera.transform.position);
            }
        }
    }
}
