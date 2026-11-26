using System;
using UnityEngine;

namespace Vaelith.Docencia2
{
    public class Docencia2InteractiveDoor : MonoBehaviour
    {
        public Quaternion closedRotation = Quaternion.identity;
        public float inwardAngle = 90f;
        public bool doubleSwing;
        public bool isOpen;
        public bool isLocked = false;

        [Header("Animación Suave (Paso 1)")]
        public float animationSpeed = 240f; // grados por segundo (~0.375s para 90°)

        private Quaternion _targetRotation = Quaternion.identity;
        private bool _animating = false;

        public static event Action<Docencia2InteractiveDoor, bool> OnDoorToggled;

        private void Awake()
        {
            _targetRotation = transform.localRotation;
        }

        private void Update()
        {
            if (!_animating) return;

            transform.localRotation = Quaternion.RotateTowards(
                transform.localRotation,
                _targetRotation,
                animationSpeed * Time.deltaTime
            );
            Physics.SyncTransforms();

            if (Quaternion.Angle(transform.localRotation, _targetRotation) < 0.05f)
            {
                transform.localRotation = _targetRotation;
                _animating = false;
                Physics.SyncTransforms();
            }
        }

        public void SetOpen(bool open, bool reverse = false)
        {
            if (isLocked && open) open = false;
            isOpen = open;
            _animating = false;
            _targetRotation = closedRotation * Quaternion.Euler(0f, open ? inwardAngle * (doubleSwing && reverse ? -1f : 1f) : 0f, 0f);
            transform.localRotation = _targetRotation;
            Physics.SyncTransforms();
        }

        public void SetOpenAnimated(bool open, bool reverse = false)
        {
            if (isLocked && open) return;
            isOpen = open;
            _targetRotation = closedRotation * Quaternion.Euler(0f, open ? inwardAngle * (doubleSwing && reverse ? -1f : 1f) : 0f, 0f);
            if (!Application.isPlaying)
            {
                transform.localRotation = _targetRotation;
                _animating = false;
                Physics.SyncTransforms();
            }
            else
            {
                _animating = true;
            }
            OnDoorToggled?.Invoke(this, open);
        }

        public void Toggle(Vector3 viewer)
        {
            if (isLocked) return;
            bool reverse = false;
            if (doubleSwing)
            {
                var col = GetComponent<BoxCollider>();
                if (col != null)
                {
                    var v = transform.TransformVector(col.center);
                    v.y = 0f;
                    var toward = viewer - transform.position;
                    toward.y = 0f;
                    var next = Quaternion.AngleAxis(inwardAngle, Vector3.up) * v;
                    reverse = Vector3.Dot(next, toward) > 0f;
                }
            }
            SetOpenAnimated(!isOpen, reverse);
        }
    }
}
