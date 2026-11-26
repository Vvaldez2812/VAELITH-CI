using UnityEngine;
using UnityEngine.Rendering;

namespace Vaelith.Docencia2
{
    /// <summary>
    /// Conduce el Animator Humanoid de la protagonista de 1.60 m:
    /// - Sincroniza Speed e IsCrouching con el movimiento real.
    /// - Mantiene Protagonista_Cabeza en Layer 28 (visible siempre en espejos y 3ª persona; oculta solo para la cámara de 1ª persona para evitar clipping).
    /// - Mantiene el smartphone Celular_Pro siempre sujeto en la mano derecha mediante Layer 1 + IK + alineación de muñeca en LateUpdate.
    /// </summary>
    [RequireComponent(typeof(Animator))]
    public class Docencia2PlayerRigDriver : MonoBehaviour
    {
        public const int HeadMirrorOnlyLayer = 28;

        public Docencia2FirstPersonPlayer player;
        public Transform rightHandHoldTarget;
        public Transform phoneVisualMount;
        public Transform phoneFlashAnchor;
        public Renderer phoneFlashLedRenderer;
        public Renderer bodyRenderer;
        public Renderer headRenderer;

        [Header("Modo Teléfono Flotante en 1ª Persona")]
        public bool floatingPhoneInFirstPerson = true;

        [Header("Suavizado")]
        public float animDampTime = 0.09f;
        public float ikBlendSpeed = 12.0f;

        private Animator _animator;
        private int _speedHash;
        private int _crouchHash;
        private bool _hasSpeedParam;
        private bool _hasCrouchParam;
        private float _ikWeight = 1.0f;
        private float _lookWeight = 0.0f;
        private bool _lastThirdPersonState = false;
        private bool _headModeInitialized = false;
        private int _ikTargetLayer = 0;

        private void Awake()
        {
            InitializeRefs();
        }

        private void OnEnable()
        {
            InitializeRefs();
            SyncRendererVisibility(force: true);
        }

        public void InitializeRefs()
        {
            if (_animator == null) _animator = GetComponent<Animator>();
            if (player == null) player = GetComponentInParent<Docencia2FirstPersonPlayer>();

            if (bodyRenderer == null)
            {
                Transform t = transform.Find("Protagonista_Cuerpo");
                if (t != null) bodyRenderer = t.GetComponent<Renderer>();
            }
            if (headRenderer == null)
            {
                Transform t = transform.Find("Protagonista_Cabeza");
                if (t != null) headRenderer = t.GetComponent<Renderer>();
            }

            if (_animator != null && _animator.isHuman)
            {
                Transform rightHandBone = _animator.GetBoneTransform(HumanBodyBones.RightHand);
                if (rightHandBone != null)
                {
                    if (phoneVisualMount == null)
                    {
                        Transform t = rightHandBone.Find("Celular_Pro");
                        if (t != null) phoneVisualMount = t;
                    }
                    if (phoneVisualMount != null)
                    {
                        if (phoneFlashAnchor == null)
                        {
                            Transform fa = phoneVisualMount.Find("Flash_Anchor");
                            if (fa != null) phoneFlashAnchor = fa;
                        }
                        if (phoneFlashLedRenderer == null)
                        {
                            foreach (var r in phoneVisualMount.GetComponentsInChildren<Renderer>(true))
                            {
                                if (r.name.IndexOf("Flash", System.StringComparison.OrdinalIgnoreCase) >= 0
                                    || r.name.IndexOf("LED", System.StringComparison.OrdinalIgnoreCase) >= 0)
                                {
                                    phoneFlashLedRenderer = r;
                                    break;
                                }
                            }
                        }
                        if (player != null && phoneFlashLedRenderer != null)
                        {
                            player.phoneFlashLedRenderer = phoneFlashLedRenderer;
                        }
                    }
                }
                _ikTargetLayer = _animator.layerCount > 1 ? 1 : 0;
            }

            _speedHash = Animator.StringToHash("Speed");
            _crouchHash = Animator.StringToHash("IsCrouching");
            _hasSpeedParam = false;
            _hasCrouchParam = false;

            if (_animator != null && _animator.runtimeAnimatorController != null)
            {
                foreach (var p in _animator.parameters)
                {
                    if (p.nameHash == _speedHash) _hasSpeedParam = true;
                    else if (p.nameHash == _crouchHash) _hasCrouchParam = true;
                }
            }
        }

        private void LateUpdate()
        {
            if (player == null) return;

            SyncRendererVisibility(force: false);

            if (_animator != null && _animator.runtimeAnimatorController != null)
            {
                if (_hasSpeedParam)
                {
                    _animator.SetFloat(_speedHash, player.CurrentAnimSpeed01, animDampTime, Time.deltaTime);
                }
                if (_hasCrouchParam)
                {
                    _animator.SetBool(_crouchHash, player.IsCrouchingState);
                }
                if (_animator.layerCount > 1)
                {
                    float armLayerWeight = (!player.isThirdPersonView && floatingPhoneInFirstPerson) ? 0f : 1f;
                    _animator.SetLayerWeight(1, armLayerWeight);
                }
            }

            // Solo alinear la muñeca derecha con el teléfono cuando se esté en 3ª persona o con mano activa
            bool useHandGrip = player.isThirdPersonView || !floatingPhoneInFirstPerson;
            if (useHandGrip && _animator != null && _animator.isHuman && player.phoneHandRoot != null && phoneVisualMount != null)
            {
                Transform rightHandBone = _animator.GetBoneTransform(HumanBodyBones.RightHand);
                if (rightHandBone != null)
                {
                    Quaternion rotDelta = player.phoneHandRoot.rotation * Quaternion.Inverse(phoneVisualMount.rotation);
                    rotDelta.ToAngleAxis(out float angDeg, out Vector3 axis);
                    if (angDeg > 180f) angDeg -= 360f;
                    float clampedDeg = Mathf.Clamp(angDeg, -95f, 95f);
                    if (Mathf.Abs(clampedDeg) > 0.05f && axis.sqrMagnitude > 0.0001f)
                    {
                        rightHandBone.rotation = Quaternion.AngleAxis(clampedDeg, axis.normalized) * rightHandBone.rotation;
                    }
                }
            }
            else if (!useHandGrip && phoneVisualMount != null && player.playerCamera != null && player.GetComponent<Docencia2PhonePresentation>() == null)
            {
                // Respaldo flotante directo si Docencia2PhonePresentation no estuviera presente
                Transform cam = player.playerCamera.transform;
                Vector3 localPos = player.flashlightOn ? new Vector3(0.16f, -0.155f, 0.325f) : new Vector3(0.17f, -0.185f, 0.31f);
                Quaternion localRot = Quaternion.AngleAxis(-9f, Vector3.forward)
                    * Quaternion.LookRotation(new Vector3(0f, 0.985f, 0.17f), new Vector3(0.12f, 0.17f, -0.985f));
                phoneVisualMount.SetPositionAndRotation(cam.position + cam.rotation * localPos, cam.rotation * localRot);
            }

            // Mantener el foco/cono de linterna anclado al módulo LED físico de Celular_Pro (si Docencia2PhonePresentation no lo gestiona)
            if (phoneFlashAnchor != null && player.phoneSpotLight != null && player.playerCamera != null && player.GetComponent<Docencia2PhonePresentation>() == null)
            {
                player.phoneSpotLight.transform.position = phoneFlashAnchor.position;
                Vector3 aimWorld = player.playerCamera.transform.TransformPoint(new Vector3(0f, -0.02f, 4.4f));
                Vector3 dirWorld = aimWorld - phoneFlashAnchor.position;
                if (dirWorld.sqrMagnitude > 0.001f)
                {
                    player.phoneSpotLight.transform.rotation = Quaternion.LookRotation(dirWorld.normalized, player.playerCamera.transform.up);
                }
            }
        }

        public void SyncRendererVisibility(bool force = false)
        {
            bool tp = player != null && player.isThirdPersonView;
            bool hideBodyFromFirstPerson = !tp && floatingPhoneInFirstPerson;

            if (bodyRenderer != null)
            {
                bodyRenderer.enabled = true;
                bodyRenderer.shadowCastingMode = ShadowCastingMode.On;
                int desiredBodyLayer = hideBodyFromFirstPerson ? HeadMirrorOnlyLayer : 0;
                if (bodyRenderer.gameObject.layer != desiredBodyLayer)
                {
                    bodyRenderer.gameObject.layer = desiredBodyLayer;
                }
            }

            if (phoneVisualMount != null)
            {
                ShadowCastingMode phoneShadows = tp ? ShadowCastingMode.On : ShadowCastingMode.Off;
                foreach (var r in phoneVisualMount.GetComponentsInChildren<Renderer>(true))
                {
                    if (r == null) continue;
                    if (r.gameObject.layer != 0) r.gameObject.layer = 0;
                    r.shadowCastingMode = phoneShadows;
                }
            }

            if (headRenderer != null)
            {
                headRenderer.enabled = true;
                // Siempre encendida con sombra real, pero aislada en Layer 28:
                // - Espejos (Docencia2Mirror) incluyen Layer 28 -> se ve cabeza/cabello/lentes en espejos.
                // - 3ª persona incluye Layer 28 en playerCamera.cullingMask -> se ve cuerpo entero.
                // - 1ª persona excluye Layer 28 en playerCamera.cullingMask -> cero clipping sin perder reflejo.
                headRenderer.shadowCastingMode = ShadowCastingMode.On;
                if (headRenderer.gameObject.layer != HeadMirrorOnlyLayer)
                {
                    headRenderer.gameObject.layer = HeadMirrorOnlyLayer;
                }
            }

            if (!force && _headModeInitialized && tp == _lastThirdPersonState) return;

            _lastThirdPersonState = tp;
            _headModeInitialized = true;

            if (player != null && player.playerCamera != null)
            {
                int mask = player.playerCamera.cullingMask;
                if (tp)
                    mask |= (1 << HeadMirrorOnlyLayer);
                else
                    mask &= ~(1 << HeadMirrorOnlyLayer);
                player.playerCamera.cullingMask = mask;
            }
        }

        private void OnAnimatorIK(int layerIndex)
        {
            if (_animator == null || !_animator.isHuman || player == null) return;
            if (layerIndex != _ikTargetLayer) return;

            bool useHandIk = (player.isThirdPersonView || !floatingPhoneInFirstPerson) && rightHandHoldTarget != null;
            float targetIk = useHandIk ? 1.0f : 0.0f;
            _ikWeight = Mathf.MoveTowards(_ikWeight, targetIk, Time.deltaTime * ikBlendSpeed);

            if (rightHandHoldTarget != null && _ikWeight > 0.005f)
            {
                _animator.SetIKPositionWeight(AvatarIKGoal.RightHand, _ikWeight);
                _animator.SetIKPosition(AvatarIKGoal.RightHand, rightHandHoldTarget.position);
            }
            else
            {
                _animator.SetIKPositionWeight(AvatarIKGoal.RightHand, 0f);
            }
            _animator.SetIKRotationWeight(AvatarIKGoal.RightHand, 0f);

            float targetLook = player.isThirdPersonView ? 0.55f : 0.0f;
            _lookWeight = Mathf.MoveTowards(_lookWeight, targetLook, Time.deltaTime * 6f);
            if (_lookWeight > 0.01f && player.playerCamera != null)
            {
                Vector3 lookTarget = player.playerCamera.transform.position + player.playerCamera.transform.forward * 6.0f;
                _animator.SetLookAtWeight(_lookWeight, 0.14f, 0.65f, 0.25f, 0.55f);
                _animator.SetLookAtPosition(lookTarget);
            }
            else
            {
                _animator.SetLookAtWeight(0f);
            }
        }
    }
}
