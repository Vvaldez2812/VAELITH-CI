using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Vaelith.Docencia2
{
    // Opt-in presentation for the gameplay player; floating phone in 1st person without hand clipping.
    [DefaultExecutionOrder(20)]
    public sealed class Docencia2PhonePresentation : MonoBehaviour
    {
        public Vector3 phoneCameraPosition = new Vector3(.16f, -.16f, .32f);
        public float gripFlip = 180f;
        public float screenFlip = 0f;
        public float phoneRoll = -9f;
        public float beamIntensity = 5.5f;
        public float beamAngle = 52f;
        public float beamInnerAngle = 16f;
        public float spillIntensity = .40f;
        public float beamRange = 8.5f;
        public float maxGripError;
        public bool floatingWithoutHand = true;

        Docencia2FirstPersonPlayer player;
        Docencia2PlayerRigDriver rig;
        Animator animator;
        Transform shoulder, elbow, hand;
        HumanPoseHandler handler;
        HumanPose pose;
        readonly List<Transform> preserved = new List<Transform>();
        readonly List<Vector3> positions = new List<Vector3>();
        readonly List<Quaternion> rotations = new List<Quaternion>();
        readonly RaycastHit[] hits = new RaycastHit[32];
        Texture2D cookie;
        Quaternion mountRotation;
        Vector3 mountPosition = new Vector3(-.0393f, .0735f, .0131f);
        Transform _initialMountParent;
        Vector3 _initialMountWorldScale = Vector3.one;
        float _flashPoseBlend = 1f;

        static readonly float[] FingerValues = { .50f, -.08f, .60f, .30f, -.34f, .16f, -.30f, -.18f, -.40f, .04f, -.36f, -.24f, -.44f, -.05f, -.40f, -.26f, -.45f, -.14f, -.36f, -.22f };

        void Start()
        {
            player = GetComponent<Docencia2FirstPersonPlayer>();
            if (!player || !player.rigDriver) { enabled = false; return; }
            rig = player.rigDriver;
            animator = rig.GetComponent<Animator>();
            if (!animator || !animator.isHuman || !rig.phoneVisualMount) { enabled = false; return; }

            shoulder = animator.GetBoneTransform(HumanBodyBones.RightUpperArm);
            elbow = animator.GetBoneTransform(HumanBodyBones.RightLowerArm);
            hand = animator.GetBoneTransform(HumanBodyBones.RightHand);
            _initialMountParent = rig.phoneVisualMount.parent;
            _initialMountWorldScale = rig.phoneVisualMount.lossyScale;
            _flashPoseBlend = player.flashlightOn ? 1f : 0f;

            mountRotation = Quaternion.LookRotation(new Vector3(-.75f, .24f, -.61f).normalized, new Vector3(-.63f, .16f, .76f).normalized);
            handler = new HumanPoseHandler(animator.avatar, animator.transform);
            pose.muscles = new float[HumanTrait.MuscleCount];
            for (int i = 0; i < (int)HumanBodyBones.LastBone; i++)
            {
                var b = (HumanBodyBones)i;
                if (b >= HumanBodyBones.RightThumbProximal && b <= HumanBodyBones.RightLittleDistal) continue;
                var t = animator.GetBoneTransform(b);
                if (t && !preserved.Contains(t))
                {
                    preserved.Add(t);
                    positions.Add(t.localPosition);
                    rotations.Add(t.localRotation);
                }
            }

            cookie = MakeCookie();
            foreach (var r in rig.phoneVisualMount.GetComponentsInChildren<Renderer>(true))
            {
                if (r.gameObject.layer != 0) r.gameObject.layer = 0;
                if (r.name.Contains("Cuerpo"))
                {
                    var block = new MaterialPropertyBlock();
                    r.GetPropertyBlock(block);
                    block.SetColor("_BaseColor", new Color(.26f, .28f, .31f));
                    block.SetFloat("_Metallic", .35f);
                    block.SetFloat("_Smoothness", .48f);
                    r.SetPropertyBlock(block);
                }
            }
        }

        Texture2D MakeCookie()
        {
            const int size = 256;
            var t = new Texture2D(size, size, TextureFormat.RGBA32, true, true)
            {
                name = "Celular_Haz_Suave_Runtime",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };
            var pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float nx = 2f * x / (size - 1) - 1f, ny = 2f * y / (size - 1) - 1f, r = Mathf.Sqrt(nx * nx + ny * ny);
                    float core = Mathf.Exp(-r * r / .30f);
                    float edge = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(.55f, 1f, r));
                    float v = (.20f + .65f * core) * edge;
                    pixels[y * size + x] = new Color(v, v, v, v);
                }
            }
            t.SetPixels(pixels);
            t.Apply(true, false);
            return t;
        }

        void LateUpdate()
        {
            if (!player || !rig || !rig.phoneVisualMount || !player.playerCamera) return;
            var mount = rig.phoneVisualMount;
            var cam = player.playerCamera.transform;

            if (floatingWithoutHand && !player.isThirdPersonView)
            {
                if (mount.parent != cam)
                {
                    mount.SetParent(cam, false);
                    mount.localScale = _initialMountWorldScale;
                }

                _flashPoseBlend = Mathf.MoveTowards(_flashPoseBlend, player.flashlightOn ? 1f : 0f, Time.deltaTime * 7f);

                Vector3 sway = Vector3.zero;
                if (player.phoneHandRoot)
                    sway = player.phoneHandRoot.localPosition - new Vector3(.18f, -.13f, .335f);

                Vector3 offDelta = Vector3.Lerp(new Vector3(.010f, -.028f, -.010f), Vector3.zero, _flashPoseBlend);
                Vector3 localPos = phoneCameraPosition + offDelta + sway * .50f;

                // FBX: +Z is the top edge, +Y is the screen normal, -Y is the rear camera/flash LED.
                Quaternion localRot = Quaternion.AngleAxis(phoneRoll, Vector3.forward)
                    * Quaternion.LookRotation(new Vector3(0f, .985f, .17f), new Vector3(.12f, .17f, -.985f))
                    * Quaternion.AngleAxis(screenFlip, Vector3.forward)
                    * Quaternion.Euler(7f * (1f - _flashPoseBlend), 0f, -2.5f * (1f - _flashPoseBlend));

                mount.localPosition = localPos;
                mount.localRotation = localRot;
                maxGripError = 0f;
                UpdateBeam(cam);
                return;
            }

            // Third-person fallback: reattach to RightHand bone
            if (hand && mount.parent != _initialMountParent && _initialMountParent != null)
            {
                mount.SetParent(_initialMountParent, false);
                mount.localScale = Vector3.one;
            }

            if (!hand) return;
            var calibratedMount = mountRotation * Quaternion.AngleAxis(gripFlip, Vector3.forward);
            var calibratedPosition = mountPosition + calibratedMount * new Vector3(0, 0, .035f);
            mount.localPosition = calibratedPosition;
            mount.localRotation = calibratedMount;
            Fingers();

            Quaternion basis = player.isThirdPersonView ? player.transform.rotation : cam.rotation;
            Vector3 offset = phoneCameraPosition;
            if (!player.flashlightOn) offset += new Vector3(.008f, -.025f, -.008f);
            Vector3 eye = player.isThirdPersonView
                ? player.transform.position + Vector3.up * (player.IsCrouchingState ? .96f : 1.48f)
                : cam.position;
            Vector3 desiredPosition = eye + basis * offset;
            Quaternion desiredRotation = basis
                * Quaternion.AngleAxis(phoneRoll, Vector3.forward)
                * Quaternion.LookRotation(new Vector3(0, .985f, .17f), new Vector3(.12f, .17f, -.985f))
                * Quaternion.AngleAxis(screenFlip, Vector3.forward);

            Quaternion wristRotation = desiredRotation * Quaternion.Inverse(calibratedMount);
            Vector3 wristPosition = desiredPosition - wristRotation * Vector3.Scale(calibratedPosition, hand.lossyScale);
            SolveArm(wristPosition, wristRotation, basis);
            maxGripError = Mathf.Max(maxGripError, Vector3.Distance(mount.position, desiredPosition));
            UpdateBeam(cam);
        }

        void Fingers()
        {
            if (handler == null) return;
            Vector3 ap = animator.transform.localPosition;
            Quaternion ar = animator.transform.localRotation;
            for (int i = 0; i < preserved.Count; i++) { positions[i] = preserved[i].localPosition; rotations[i] = preserved[i].localRotation; }
            handler.GetHumanPose(ref pose);
            for (int i = 0; i < FingerValues.Length; i++) pose.muscles[75 + i] = FingerValues[i];
            handler.SetHumanPose(ref pose);
            animator.transform.localPosition = ap;
            animator.transform.localRotation = ar;
            for (int i = 0; i < preserved.Count; i++) { preserved[i].localPosition = positions[i]; preserved[i].localRotation = rotations[i]; }
        }

        void SolveArm(Vector3 target, Quaternion rotation, Quaternion basis)
        {
            if (!shoulder || !elbow || !hand) return;
            Vector3 s = shoulder.position;
            float a = Vector3.Distance(s, elbow.position), b = Vector3.Distance(elbow.position, hand.position);
            if (a < .05f || b < .05f) return;
            Vector3 dir = target - s;
            float d = Mathf.Clamp(dir.magnitude, Mathf.Abs(a - b) + .02f, (a + b) * .992f);
            dir.Normalize();
            Vector3 bend = Vector3.ProjectOnPlane(basis * new Vector3(.24f, -.42f, .10f), dir).normalized;
            float cos = Mathf.Clamp((a * a + d * d - b * b) / (2 * a * d), -1, 1);
            Vector3 e = s + dir * (a * cos) + bend * (a * Mathf.Sqrt(1 - cos * cos));
            shoulder.rotation = Quaternion.FromToRotation(elbow.position - s, e - s) * shoulder.rotation;
            elbow.rotation = Quaternion.FromToRotation(hand.position - elbow.position, s + dir * d - elbow.position) * elbow.rotation;
            Vector3 axis = (hand.position - elbow.position).normalized;
            Quaternion diff = rotation * Quaternion.Inverse(hand.rotation);
            Vector3 q = Vector3.Project(new Vector3(diff.x, diff.y, diff.z), axis);
            Quaternion twist = new Quaternion(q.x, q.y, q.z, diff.w).normalized;
            twist.ToAngleAxis(out float angle, out Vector3 twistAxis);
            if (angle > 180) angle -= 360;
            if (Mathf.Abs(angle) > .1f && twistAxis.sqrMagnitude > .001f) elbow.rotation = Quaternion.AngleAxis(angle * .58f, twistAxis) * elbow.rotation;
            hand.rotation = rotation;
        }

        void UpdateBeam(Transform cam)
        {
            var light = player.phoneSpotLight;
            if (!light) return;
            float factor = light.intensity / Mathf.Max(.001f, player.flashlightBaseIntensity);
            player.flashlightBaseIntensity = beamIntensity;
            player.flashlightSpillBaseIntensity = spillIntensity;
            light.intensity = beamIntensity * factor;
            if (player.flashlightSpillLight) player.flashlightSpillLight.intensity = spillIntensity * factor;
            light.cookie = cookie;
            light.range = beamRange;
            light.spotAngle = beamAngle;
            light.innerSpotAngle = beamInnerAngle;

            Vector3 origin = rig.phoneFlashLedRenderer
                ? rig.phoneFlashLedRenderer.bounds.center
                : (rig.phoneFlashAnchor ? rig.phoneFlashAnchor.position : rig.phoneVisualMount.position);
            Vector3 target = cam.position + cam.forward * 5f;
            int count = Physics.RaycastNonAlloc(cam.position, cam.forward, hits, 5f, ~0, QueryTriggerInteraction.Ignore);
            float nearest = 5f;
            for (int i = 0; i < count; i++)
            {
                if (!hits[i].transform.IsChildOf(player.transform) && hits[i].distance > .35f && hits[i].distance < nearest)
                {
                    nearest = hits[i].distance;
                    target = hits[i].point;
                }
            }
            light.transform.position = origin + cam.forward * .025f;
            light.transform.rotation = Quaternion.LookRotation(target - light.transform.position, cam.up);

            if (player.flashlightSpillLight)
            {
                player.flashlightSpillLight.transform.localPosition = Vector3.zero;
                player.flashlightSpillLight.transform.localRotation = Quaternion.identity;
                player.flashlightSpillLight.spotAngle = 72;
                player.flashlightSpillLight.innerSpotAngle = 20;
                player.flashlightSpillLight.range = 4.6f;
            }
            if (player.phoneScreenLight) player.phoneScreenLight.intensity = 0;
            if (player.volumetricBeamRenderer) player.volumetricBeamRenderer.enabled = false;
        }

        void OnDestroy()
        {
            handler?.Dispose();
            if (cookie) Destroy(cookie);
        }
    }
}
