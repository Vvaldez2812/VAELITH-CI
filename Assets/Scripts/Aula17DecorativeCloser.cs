using UnityEngine;

// Visual linkage only. Never opens, closes, locks or modifies the approved door.
namespace Vaelith.Docencia2
{
    public class Aula17DecorativeCloser : MonoBehaviour
    {
        public Transform movingPin, fixedPin, armA, armB, elbow;
        public float armLength = .24f;
        public float bendSign = 1f;
        public void RefreshVisual()
        {
            if (!movingPin || !fixedPin || !armA || !armB || !elbow) return;
            Vector3 a = movingPin.position, b = fixedPin.position;
            b.y = a.y;
            Vector3 delta = b - a;
            float distance = delta.magnitude;
            if (distance < .0001f) return;
            Vector3 side = Vector3.Cross(delta / distance, Vector3.up) * bendSign;
            float height = Mathf.Sqrt(Mathf.Max(0, armLength * armLength - distance * distance * .25f));
            Vector3 joint = (a + b) * .5f + side * height;
            elbow.position = joint;
            Place(armA, a, joint);
            Place(armB, joint, b);
        }
        static void Place(Transform arm, Vector3 a, Vector3 b)
        {
            arm.position = (a + b) * .5f;
            arm.rotation = Quaternion.LookRotation(b - a, Vector3.up);
            arm.localScale = new Vector3(.014f, .012f, Vector3.Distance(a, b));
        }
        void LateUpdate() { RefreshVisual(); }
    }
}
