using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using Vaelith.Docencia2;

namespace Vaelith.Trailer
{
    /// <summary>
    /// Exclusivo de la escena duplicada Trailer_Docencia2.unity.
    /// Se ejecuta estrictamente DESPUÉS de Docencia2PlayerRigDriver (order 0) para:
    /// 1) Reorientar Celular_Pro dentro de la palma derecha con ergonomía anatómica real
    ///    (pantalla +Y hacia la palma, tope +Z hacia pulgar/índice, dorso LED -Y hacia atrás),
    ///    evitando el quiebre de muñeca de 90°.
    /// 2) Resolver IK analítico de 2 huesos (Hombro -> Antebrazo -> Muñeca) con polo de codo
    ///    natural abajo-derecha y reparto de pronación en el antebrazo.
    /// 3) Envolver los 4 dedos alrededor del bisel izquierdo (-X) y el pulgar por el bisel
    ///    derecho (+X), sin huecos ni penetración en pantalla.
    /// </summary>
    [DefaultExecutionOrder(10)]
    public class TrailerHandPoseFixer : MonoBehaviour
    {
        public TrailerDirectorController directorController;
        public bool writeDiagnosticLog = false;
        public string diagnosticLogPath = "";

        private Docencia2FirstPersonPlayer _player;
        private Docencia2PlayerRigDriver _rigDriver;
        private Animator _anim;

        private Transform _upperArmBone;
        private Transform _foreArmBone;
        private Transform _rightHandBone;

        private Transform _thumbProx, _thumbInt, _thumbDist;
        private Transform _indexProx, _indexInt, _indexDist;
        private Transform _middleProx, _middleInt, _middleDist;
        private Transform _ringProx, _ringInt, _ringDist;
        private Transform _littleProx, _littleInt, _littleDist;
        private Light _flashlightSpot;
        private Light _flashlightSpillLight;

        private HumanPoseHandler _humanPoseHandler;
        private HumanPose _humanPose;
        private readonly List<Transform> _savedBodyBones = new List<Transform>(64);
        private readonly List<Vector3> _savedBodyLocalPos = new List<Vector3>(64);
        private readonly List<Quaternion> _savedBodyLocalRot = new List<Quaternion>(64);

        private bool _mountPoseInitialized = false;
        private Vector3 _targetMountLocalPos;
        private Quaternion _targetMountLocalRot;

        private readonly HashSet<string> _loggedShots = new HashSet<string>();
        private readonly StringBuilder _diagSb = new StringBuilder();

        private void Awake()
        {
            ResolveRefs();
        }

        private void OnDisable()
        {
            FlushDiagLog();
        }

        private void OnDestroy()
        {
            FlushDiagLog();
        }

        public void FlushDiagLog()
        {
            if (!writeDiagnosticLog || string.IsNullOrEmpty(diagnosticLogPath) || _diagSb.Length == 0)
                return;

            try
            {
                string dir = Path.GetDirectoryName(diagnosticLogPath);
                if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
                File.WriteAllText(diagnosticLogPath, _diagSb.ToString(), Encoding.UTF8);
            }
            catch { }
        }

        private void ResolveRefs()
        {
            if (_player == null)
                _player = FindAnyObjectByType<Docencia2FirstPersonPlayer>();
            if (_player == null) return;

            if (_rigDriver == null)
                _rigDriver = _player.rigDriver != null
                    ? _player.rigDriver
                    : _player.GetComponentInChildren<Docencia2PlayerRigDriver>(true);

            if (_anim == null && _rigDriver != null)
                _anim = _rigDriver.GetComponent<Animator>();

            if (_anim != null && _anim.isHuman && _rightHandBone == null)
            {
                _upperArmBone = _anim.GetBoneTransform(HumanBodyBones.RightUpperArm);
                _foreArmBone = _anim.GetBoneTransform(HumanBodyBones.RightLowerArm);
                _rightHandBone = _anim.GetBoneTransform(HumanBodyBones.RightHand);

                _thumbProx = _anim.GetBoneTransform(HumanBodyBones.RightThumbProximal);
                _thumbInt = _anim.GetBoneTransform(HumanBodyBones.RightThumbIntermediate);
                _thumbDist = _anim.GetBoneTransform(HumanBodyBones.RightThumbDistal);

                _indexProx = _anim.GetBoneTransform(HumanBodyBones.RightIndexProximal);
                _indexInt = _anim.GetBoneTransform(HumanBodyBones.RightIndexIntermediate);
                _indexDist = _anim.GetBoneTransform(HumanBodyBones.RightIndexDistal);

                _middleProx = _anim.GetBoneTransform(HumanBodyBones.RightMiddleProximal);
                _middleInt = _anim.GetBoneTransform(HumanBodyBones.RightMiddleIntermediate);
                _middleDist = _anim.GetBoneTransform(HumanBodyBones.RightMiddleDistal);

                _ringProx = _anim.GetBoneTransform(HumanBodyBones.RightRingProximal);
                _ringInt = _anim.GetBoneTransform(HumanBodyBones.RightRingIntermediate);
                _ringDist = _anim.GetBoneTransform(HumanBodyBones.RightRingDistal);

                _littleProx = _anim.GetBoneTransform(HumanBodyBones.RightLittleProximal);
                _littleInt = _anim.GetBoneTransform(HumanBodyBones.RightLittleIntermediate);
                _littleDist = _anim.GetBoneTransform(HumanBodyBones.RightLittleDistal);

                if (_anim.avatar != null && _humanPoseHandler == null)
                {
                    _humanPoseHandler = new HumanPoseHandler(_anim.avatar, _anim.transform);
                    _humanPose.muscles = new float[HumanTrait.MuscleCount];
                    _savedBodyBones.Clear();
                    _savedBodyLocalPos.Clear();
                    _savedBodyLocalRot.Clear();
                    for (int i = 0; i < (int)HumanBodyBones.LastBone; i++)
                    {
                        var hb = (HumanBodyBones)i;
                        if (hb >= HumanBodyBones.RightThumbProximal && hb <= HumanBodyBones.RightLittleDistal)
                            continue;
                        Transform bt = _anim.GetBoneTransform(hb);
                        if (bt != null && !_savedBodyBones.Contains(bt))
                        {
                            _savedBodyBones.Add(bt);
                            _savedBodyLocalPos.Add(bt.localPosition);
                            _savedBodyLocalRot.Add(bt.localRotation);
                        }
                    }
                }
            }

            if (_rigDriver != null && _rigDriver.phoneVisualMount != null && !_mountPoseInitialized)
            {
                // En espacio local de RightHand:
                //   Posición calibrada para que los nudillos (MiddleProx) queden detrás del dorso
                //   (-0.026, -0.021) y el pulgar libre frente al bisel derecho (+0.048, -0.015),
                //   permitiendo que los dedos envuelvan de forma anatómica sin flotar a la izquierda.
                Vector3 topInRH = new Vector3(-0.75f, 0.24f, -0.61f).normalized;
                Vector3 screenInRH = new Vector3(-0.63f, 0.16f, 0.76f).normalized;
                _targetMountLocalRot = Quaternion.LookRotation(topInRH, screenInRH);
                _targetMountLocalPos = new Vector3(-0.0393f, 0.0735f, 0.0131f);

                _rigDriver.phoneVisualMount.localPosition = _targetMountLocalPos;
                _rigDriver.phoneVisualMount.localRotation = _targetMountLocalRot;
                _mountPoseInitialized = true;
            }

            if (_flashlightSpot == null)
            {
                foreach (var l in _player.GetComponentsInChildren<Light>(true))
                {
                    if (l != null && l.type == LightType.Spot)
                    {
                        _flashlightSpot = l;
                        break;
                    }
                }
                if (_flashlightSpot == null && directorController != null && directorController.trailerMainCamera != null)
                {
                    foreach (var l in directorController.trailerMainCamera.GetComponentsInChildren<Light>(true))
                    {
                        if (l != null && l.type == LightType.Spot)
                        {
                            _flashlightSpot = l;
                            break;
                        }
                    }
                }
                if (_flashlightSpot != null)
                {
                    Transform spillT = _flashlightSpot.transform.Find("Luz_Linterna_Rebote_Suave");
                    if (spillT != null)
                        _flashlightSpillLight = spillT.GetComponent<Light>();
                }
            }
        }

        private void LateUpdate()
        {
            if (!Application.isPlaying) return;
            ResolveRefs();
            if (_player == null || !_player.gameObject.activeInHierarchy)
                return;
            if (_rigDriver == null || _rigDriver.phoneVisualMount == null || _player.phoneHandRoot == null || _rightHandBone == null)
                return;

            Transform mount = _rigDriver.phoneVisualMount;
            mount.localPosition = _targetMountLocalPos;
            mount.localRotation = _targetMountLocalRot;

            TrailerShotDefinition shot = null;
            float localT = 0f;
            if (directorController != null)
            {
                double t = directorController.GetEffectiveTime();
                shot = directorController.GetShotAtTime(t, out localT);
            }

            // En T01 (1ª persona primer plano 03:17), encuadre claro y cercano de teléfono + mano
            if (!_player.isThirdPersonView && shot != null && shot.useClosePhoneFraming && directorController.trailerMainCamera != null)
            {
                Transform handParent = _player.phoneHandRoot.parent;
                if (handParent != null)
                {
                    handParent.SetPositionAndRotation(
                        directorController.trailerMainCamera.transform.position,
                        directorController.trailerMainCamera.transform.rotation);
                }
                _player.phoneHandRoot.localPosition = new Vector3(0.068f, -0.015f, 0.345f);
                _player.phoneHandRoot.localRotation = Quaternion.Euler(0f, -18f, -5f) * Quaternion.Euler(-76f, 0f, 0f);
            }

            // 1. Aplicar músculos anatómicos calibrados en los 5 dedos (sin torsión axial ni pinzamiento)
            ApplyCleanHumanFingerPose();

            // 2. Calcular rotación y posición deseadas de RightHand para que mount == phoneHandRoot
            Quaternion targetHandRot = _player.phoneHandRoot.rotation * Quaternion.Inverse(_targetMountLocalRot);
            Vector3 targetWristPos = _player.phoneHandRoot.position - targetHandRot * _targetMountLocalPos;

            // 3. Resolver brazo completo (Hombro -> Codo -> Muñeca) con polo anatómico sin quiebre en muñeca
            SolveNaturalRightArm(targetWristPos, targetHandRot);

            // 4. Reafirmar orientación exacta de muñeca tras ajuste del brazo
            Quaternion remDelta = _player.phoneHandRoot.rotation * Quaternion.Inverse(mount.rotation);
            remDelta.ToAngleAxis(out float remDeg, out Vector3 remAxis);
            if (remDeg > 180f) remDeg -= 360f;
            if (Mathf.Abs(remDeg) > 0.05f && remAxis.sqrMagnitude > 0.0001f)
            {
                _rightHandBone.rotation = Quaternion.AngleAxis(remDeg, remAxis.normalized) * _rightHandBone.rotation;
            }

            // 5. Reajustar origen del haz y derrame de linterna por delante de los nudillos
            //    y suavizar OLED para evitar sobreexposición blanca artificial en los dedos
            if (_flashlightSpot != null)
            {
                _flashlightSpot.transform.position = mount.TransformPoint(new Vector3(0.0066f, -0.085f, 0.0654f));
                if (!_player.isThirdPersonView && directorController != null && directorController.trailerMainCamera != null)
                {
                    _flashlightSpot.transform.rotation = directorController.trailerMainCamera.transform.rotation;
                }
                else
                {
                    _flashlightSpot.transform.rotation = _player.transform.rotation;
                }
            }
            if (_flashlightSpillLight != null)
            {
                _flashlightSpillLight.transform.localPosition = new Vector3(0f, 0f, 0.14f);
                _flashlightSpillLight.spotAngle = 42f;
                _flashlightSpillLight.intensity = 0.45f;
            }
            if (_player.phoneScreenLight != null)
            {
                _player.phoneScreenLight.intensity = 0.005f;
            }

            // 6. Diagnóstico puntual para verificación geométrica
            if (writeDiagnosticLog && shot != null && localT >= 0.4f && !_loggedShots.Contains(shot.id))
            {
                _loggedShots.Add(shot.id);
                LogDiagnostic(shot.id, mount);
            }
        }

        private void SolveNaturalRightArm(Vector3 targetWristPos, Quaternion targetHandRot)
        {
            if (_upperArmBone == null || _foreArmBone == null || _rightHandBone == null)
                return;

            Vector3 s = _upperArmBone.position;
            Vector3 e = _foreArmBone.position;
            Vector3 h = _rightHandBone.position;

            float l1 = Vector3.Distance(s, e);
            float l2 = Vector3.Distance(e, h);
            if (l1 < 0.05f || l2 < 0.05f) return;

            Vector3 polePos;
            if (!_player.isThirdPersonView && directorController != null && directorController.trailerMainCamera != null)
            {
                Transform c = directorController.trailerMainCamera.transform;
                polePos = s + c.right * 0.24f - c.up * 0.42f + c.forward * 0.10f;
            }
            else
            {
                Transform pt = _player.transform;
                polePos = s + pt.right * 0.24f - pt.up * 0.36f + pt.forward * 0.12f;
            }

            Vector3 st = targetWristPos - s;
            float dist = st.magnitude;
            float maxReach = (l1 + l2) * 0.992f;
            float minReach = Mathf.Abs(l1 - l2) + 0.02f;
            dist = Mathf.Clamp(dist, minReach, maxReach);
            Vector3 toT = st.sqrMagnitude > 0.0001f ? st.normalized : Vector3.forward;
            Vector3 clampedTarget = s + toT * dist;

            float cosA = Mathf.Clamp((l1 * l1 + dist * dist - l2 * l2) / (2f * l1 * dist), -1f, 1f);
            float sinA = Mathf.Sqrt(Mathf.Max(0f, 1f - cosA * cosA));

            Vector3 bendDir = Vector3.ProjectOnPlane(polePos - s, toT);
            if (bendDir.sqrMagnitude < 0.0001f) bendDir = Vector3.down;
            bendDir.Normalize();

            Vector3 eTarget = s + toT * (l1 * cosA) + bendDir * (l1 * sinA);

            Vector3 curUpperDir = (_foreArmBone.position - _upperArmBone.position).normalized;
            Vector3 wantUpperDir = (eTarget - _upperArmBone.position).normalized;
            _upperArmBone.rotation = Quaternion.FromToRotation(curUpperDir, wantUpperDir) * _upperArmBone.rotation;

            Vector3 curForeDir = (_rightHandBone.position - _foreArmBone.position).normalized;
            Vector3 wantForeDir = (clampedTarget - _foreArmBone.position).normalized;
            _foreArmBone.rotation = Quaternion.FromToRotation(curForeDir, wantForeDir) * _foreArmBone.rotation;

            // Repartir suavemente el giro longitudinal (pronación) a lo largo del antebrazo
            // para eliminar el pliegue/pinzado brusco en la unión de la muñeca
            Vector3 foreAxis = (_rightHandBone.position - _foreArmBone.position).normalized;
            Quaternion wristDiff = targetHandRot * Quaternion.Inverse(_rightHandBone.rotation);
            Quaternion twistPart = ExtractTwistAroundAxis(wristDiff, foreAxis);
            twistPart.ToAngleAxis(out float twistDeg, out Vector3 twistAx);
            if (twistDeg > 180f) twistDeg -= 360f;
            if (Mathf.Abs(twistDeg) > 0.1f && twistAx.sqrMagnitude > 0.0001f)
            {
                _foreArmBone.rotation = Quaternion.AngleAxis(twistDeg * 0.58f, twistAx.normalized) * _foreArmBone.rotation;
            }

            _rightHandBone.rotation = targetHandRot;
        }

        private static Quaternion ExtractTwistAroundAxis(Quaternion q, Vector3 axis)
        {
            Vector3 ra = new Vector3(q.x, q.y, q.z);
            Vector3 proj = Vector3.Project(ra, axis);
            Quaternion twist = new Quaternion(proj.x, proj.y, proj.z, q.w);
            float mag = Mathf.Sqrt(twist.x * twist.x + twist.y * twist.y + twist.z * twist.z + twist.w * twist.w);
            if (mag < 0.0001f) return Quaternion.identity;
            return new Quaternion(twist.x / mag, twist.y / mag, twist.z / mag, twist.w / mag);
        }

        private void ApplyCleanHumanFingerPose()
        {
            if (_humanPoseHandler == null || _anim == null || _humanPose.muscles == null || _humanPose.muscles.Length <= 94)
                return;

            // Preservar cada hueso no dactilar (tronco, piernas, cabeza, brazo izquierdo)
            Vector3 savedAnimPos = _anim.transform.localPosition;
            Quaternion savedAnimRot = _anim.transform.localRotation;
            for (int i = 0; i < _savedBodyBones.Count; i++)
            {
                Transform bt = _savedBodyBones[i];
                if (bt != null)
                {
                    _savedBodyLocalPos[i] = bt.localPosition;
                    _savedBodyLocalRot[i] = bt.localRotation;
                }
            }

            _humanPoseHandler.GetHumanPose(ref _humanPose);

            // Músculos calibrados [75..94] del Avatar Humanoid para sostener el celular de 78 mm
            // apoyando solo las yemas sobre los biseles laterales y dejando la pantalla despejada
            // Pulgar apoyado limpio sobre el bisel/costado derecho (+X >= +0.039, Y >= +0.006 sin penetrar cristal):
            _humanPose.muscles[75] = 0.98f;   // Thumb 1 Stretched
            _humanPose.muscles[76] = -0.08f;  // Thumb Spread (trae la yema hacia +Y frontal/costado sin hundirla en el cristal)
            _humanPose.muscles[77] = 0.82f;   // Thumb 2 Stretched
            _humanPose.muscles[78] = 0.40f;   // Thumb 3 Stretched

            // Índice..Meñique rodeando el dorso con yemas curvas solo en el borde izquierdo (-X ~ -0.038..-0.044):
            _humanPose.muscles[79] = -0.34f;  // Index 1 Stretched
            _humanPose.muscles[80] = 0.16f;   // Index Spread
            _humanPose.muscles[81] = -0.30f;  // Index 2 Stretched
            _humanPose.muscles[82] = -0.18f;  // Index 3 Stretched

            _humanPose.muscles[83] = -0.40f;  // Middle 1 Stretched
            _humanPose.muscles[84] = 0.04f;   // Middle Spread
            _humanPose.muscles[85] = -0.36f;  // Middle 2 Stretched
            _humanPose.muscles[86] = -0.24f;  // Middle 3 Stretched

            _humanPose.muscles[87] = -0.44f;  // Ring 1 Stretched
            _humanPose.muscles[88] = -0.05f;  // Ring Spread
            _humanPose.muscles[89] = -0.40f;  // Ring 2 Stretched
            _humanPose.muscles[90] = -0.26f;  // Ring 3 Stretched

            _humanPose.muscles[91] = -0.45f;  // Little 1 Stretched
            _humanPose.muscles[92] = -0.14f;  // Little Spread
            _humanPose.muscles[93] = -0.36f;  // Little 2 Stretched
            _humanPose.muscles[94] = -0.22f;  // Little 3 Stretched

            _humanPoseHandler.SetHumanPose(ref _humanPose);

            _anim.transform.localPosition = savedAnimPos;
            _anim.transform.localRotation = savedAnimRot;
            for (int i = 0; i < _savedBodyBones.Count; i++)
            {
                Transform bt = _savedBodyBones[i];
                if (bt != null)
                {
                    bt.localPosition = _savedBodyLocalPos[i];
                    bt.localRotation = _savedBodyLocalRot[i];
                }
            }
        }

        private void LogDiagnostic(string shotId, Transform mount)
        {
            float screenCamDot = (directorController != null && directorController.trailerMainCamera != null)
                ? Vector3.Dot(mount.up, -directorController.trailerMainCamera.transform.forward)
                : 0f;

            Vector3 wristInM = mount.InverseTransformPoint(_rightHandBone.position);
            Vector3 elbowInM = _foreArmBone != null ? mount.InverseTransformPoint(_foreArmBone.position) : Vector3.zero;
            Vector3 tDist = _thumbDist != null ? mount.InverseTransformPoint(_thumbDist.position) : Vector3.zero;
            Vector3 iInt = _indexInt != null ? mount.InverseTransformPoint(_indexInt.position) : Vector3.zero;
            Vector3 iDist = _indexDist != null ? mount.InverseTransformPoint(_indexDist.position) : Vector3.zero;
            Vector3 mDist = _middleDist != null ? mount.InverseTransformPoint(_middleDist.position) : Vector3.zero;
            Vector3 rDist = _ringDist != null ? mount.InverseTransformPoint(_ringDist.position) : Vector3.zero;
            Vector3 lDist = _littleDist != null ? mount.InverseTransformPoint(_littleDist.position) : Vector3.zero;

            _diagSb.AppendLine(
                $"[{shotId}] screenDot={screenCamDot:F3} | wristInM={wristInM:F3} elbowInM={elbowInM:F3} | " +
                $"thumbDist={tDist:F3} indexInt={iInt:F3} indexDist={iDist:F3} midDist={mDist:F3} ringDist={rDist:F3} littleDist={lDist:F3}");
        }
    }
}
