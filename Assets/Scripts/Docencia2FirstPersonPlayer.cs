using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Vaelith.Docencia2
{
    /// <summary>
    /// PASO 1: Personaje en primera persona con linterna de celular, interacción E con puertas,
    /// teletransportes de depuración, audio procedural de pasos/puertas (SUPUESTO) y panel de prueba de puertas (tecla G).
    /// Usa exclusivamente el nuevo Input System (Keyboard.current / Mouse.current).
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class Docencia2FirstPersonPlayer : MonoBehaviour
    {
        public static Docencia2FirstPersonPlayer Instance { get; private set; }

        [Header("Dimensiones y Movimiento (Paso 1)")]
        public float standingHeight = 1.75f;
        public float crouchingHeight = 1.15f;
        public float controllerRadius = 0.30f;
        public float controllerStepOffset = 0.30f;
        public float standingCameraY = 1.65f;
        public float crouchingCameraY = 1.05f;
        public float walkSpeed = 3.6f;
        public float runSpeed = 5.8f;
        public float crouchSpeed = 2.0f;
        public float mouseSensitivity = 0.14f;
        public float gravity = -18.0f;

        [Header("Cabeceo Sutil de Caminata")]
        public float bobFrequencyWalk = 8.5f;
        public float bobFrequencyRun = 12.0f;
        public float bobAmplitudeY = 0.022f;
        public float bobAmplitudeX = 0.012f;

        [Header("Referencias de Cámara y Celular")]
        public Camera playerCamera;
        public Transform phoneHandRoot;
        public Light phoneSpotLight;
        public Renderer phoneFlashLedRenderer;

        [Header("Linterna de Celular (Tecla F - Óptica Realista 28° y Alcance ~7 m)")]
        public bool flashlightOn = true;
        public float flashlightBaseIntensity = 95.0f;
        public float flashlightRange = 7.8f;
        public float flashlightOuterAngle = 30.0f;
        public float flashlightInnerAngle = 12.0f;
        [Range(0.4f, 2.0f)]
        public float flashlightPowerMultiplier = 1.0f;
        public Light flashlightSpillLight;
        public float flashlightSpillBaseIntensity = 3.8f;
        public Renderer volumetricBeamRenderer;
        public float volumetricBeamBaseAlpha = 0.032f;

        [Header("Batería de Celular (Opcional: 6 a 8 minutos)")]
        public bool enableBatteryDrain = true;
        [Tooltip("Duración total de la batería en segundos (420 s = 7 minutos)")]
        public float batteryTotalDurationSeconds = 420.0f;
        public float batteryRemainingSeconds = 420.0f;
        private bool _batteryShutoffLockout = false;
        private float _batteryLockoutEndTime = -1f;

        public bool IsDoorPanelOpen { get => _showDoorPanel; set => _showDoorPanel = value; }
        public bool IsPaused { get => _isPaused; set => _isPaused = value; }
        public float BatteryPercent01 => batteryTotalDurationSeconds > 0.01f ? Mathf.Clamp01(batteryRemainingSeconds / batteryTotalDurationSeconds) : 1.0f;

        [Header("Interacción con Puertas (Tecla E)")]
        public float interactionDistance = 3.0f;

        private CharacterController _cc;
        private float _pitch = 0f;
        private float _verticalVelocity = 0f;
        private float _bobTimer = 0f;
        private float _currentHeight = 1.75f;
        private float _currentCamY = 1.65f;
        private Vector3 _phoneBaseLocalPos = new Vector3(0.24f, -0.21f, 0.38f);
        private Quaternion _phoneBaseLocalRot = Quaternion.Euler(6f, -8f, 2f);

        private bool _isPaused = false;
        private bool _showDoorPanel = false;
        private Vector2 _doorPanelScroll = Vector2.zero;
        private string _lastReportMessage = "";
        private Docencia2InteractiveDoor _hoveredDoor;
        private string _hoveredDoorLabel = "";

        // Audio procedural (SUPUESTO por ausencia de archivos de audio en el proyecto)
        private AudioSource _footstepSource;
        private AudioSource _sfxSource;
        private AudioClip[] _tileStepClips;
        private AudioClip _doorOpenClip;
        private AudioClip _doorCloseClip;
        private AudioClip _phoneClickClip;
        private float _stepDistanceAccumulator = 0f;

        // Estado del parpadeo muy ocasional de la linterna y balanceo físico de mano
        private float _nextFlickerTime = 14f;
        private float _flickerEndTime = -1f;
        private Vector2 _smoothedLookSway = Vector2.zero;
        private MaterialPropertyBlock _volumetricMpb;
        private static Texture2D _runtimeCookieTex;
        private static Texture2D _runtimeVolumetricTex;

        // Lista de puertas para el panel de prueba (Tecla G)
        [Serializable]
        public class DoorTestEntry
        {
            public string id;
            public string hierarchyPath;
            public Docencia2InteractiveDoor door;
            public string status;
            public bool passed;
            public Vector3 worldPos;
            public Vector2 planPos;
        }
        public List<DoorTestEntry> doorTestEntries = new List<DoorTestEntry>();

        // Puntos de teletransporte (Atajos 1..8)
        private struct TeleportTarget
        {
            public Key key;
            public string label;
            public float planX;
            public float y;
            public float planZ;
            public float yaw;
        }

        private readonly TeleportTarget[] _teleports = new TeleportTarget[]
        {
            new TeleportTarget { key = Key.Digit1, label = "1: Entrada Principal (PB)", planX = 24.50f, y = 0.20f, planZ = 19.29f, yaw = -90f },
            new TeleportTarget { key = Key.Digit2, label = "2: Pasillo Central Norte / Canceles (PB)", planX = 14.24f, y = 0.20f, planZ = 14.50f, yaw = -90f },
            new TeleportTarget { key = Key.Digit3, label = "3: Plaza Sur (PB)", planX = 14.24f, y = 0.20f, planZ = 42.00f, yaw = 90f },
            new TeleportTarget { key = Key.Digit4, label = "4: Auditorio (PB)", planX = 22.00f, y = 0.20f, planZ = 26.50f, yaw = 90f },
            new TeleportTarget { key = Key.Digit5, label = "5: Escalera PB (Pie de Escalera)", planX = 9.20f, y = 0.20f, planZ = 19.29f, yaw = 180f },
            new TeleportTarget { key = Key.Digit6, label = "6: Desembarque Escalera (PA)", planX = 12.50f, y = 3.606f, planZ = 19.29f, yaw = -90f },
            new TeleportTarget { key = Key.Digit7, label = "7: Pasillo Central Norte / Canceles (PA)", planX = 14.24f, y = 3.606f, planZ = 14.50f, yaw = -90f },
            new TeleportTarget { key = Key.Digit8, label = "8: Plaza Sur / Aulas (PA)", planX = 14.24f, y = 3.606f, planZ = 42.00f, yaw = 90f }
        };

        public static Vector3 PlanToUnityPos(float xPlan, float yHeight, float zPlan)
        {
            float ux = 27.00f - zPlan * (54.00f / 53.92f);
            float uz = 14.25f - xPlan * (28.50f / 28.48f);
            return new Vector3(ux, yHeight, uz);
        }

        public static Vector2 UnityToPlanXZ(Vector3 worldPos)
        {
            float px = (14.25f - worldPos.z) / (28.50f / 28.48f);
            float pz = (27.00f - worldPos.x) / (54.00f / 53.92f);
            return new Vector2(px, pz);
        }

        private void Awake()
        {
            Instance = this;
            _cc = GetComponent<CharacterController>();
            ConfigureCharacterController();
            EnsureAudioInitialized();
        }

        private void OnEnable()
        {
            Instance = this;
            Docencia2InteractiveDoor.OnDoorToggled += HandleDoorSound;
        }

        private void OnDisable()
        {
            Docencia2InteractiveDoor.OnDoorToggled -= HandleDoorSound;
            if (Instance == this) Instance = null;
        }

        private void Start()
        {
            ConfigureCharacterController();
            EnsureRealisticFlashlightRig();
            if (phoneSpotLight != null)
            {
                // Garantizar que ninguna otra luz de la escena tenga sombras activas para que la linterna sea la ÚNICA luz con sombras
                foreach (var l in FindObjectsByType<Light>(FindObjectsInactive.Include))
                {
                    if (l != phoneSpotLight && l.shadows != LightShadows.None)
                    {
                        l.shadows = LightShadows.None;
                    }
                }
                SetFlashlightState(flashlightOn, false);
            }

            // Desactivar las cámaras fijas de referencia en Play para que la vista sea la del jugador (sin alterar sus componentes ni posiciones para renders de regresión)
            var refCams = GameObject.Find("Camaras_Referencia_Docencia2");
            if (refCams != null)
            {
                foreach (var c in refCams.GetComponentsInChildren<Camera>(true))
                {
                    if (c.targetTexture == null)
                        c.enabled = false;
                }
            }

            if (playerCamera != null)
            {
                playerCamera.enabled = true;
            }

            LockCursor(true);
            RunDoorAuditAndPopulateList(saveFiles: true);
        }

        public void ConfigureCharacterController()
        {
            if (_cc == null) _cc = GetComponent<CharacterController>();
            _cc.height = standingHeight;
            _cc.radius = controllerRadius;
            _cc.stepOffset = controllerStepOffset;
            _cc.slopeLimit = 50f;
            _cc.skinWidth = 0.03f;
            _cc.center = new Vector3(0f, standingHeight * 0.5f, 0f);
            _currentHeight = standingHeight;
            _currentCamY = standingCameraY;
        }

        private void Update()
        {
            var kb = Keyboard.current;
            var mouse = Mouse.current;
            if (kb == null) return;

            // 1. Pausa con Esc
            if (kb.escapeKey.wasPressedThisFrame)
            {
                _isPaused = !_isPaused;
                LockCursor(!_isPaused && !_showDoorPanel);
            }

            // 2. Panel de prueba de puertas con tecla G
            if (kb.gKey.wasPressedThisFrame)
            {
                _showDoorPanel = !_showDoorPanel;
                if (_showDoorPanel)
                {
                    RunDoorAuditAndPopulateList(saveFiles: true);
                }
                if (Docencia2TerrorDarknessController.Instance != null)
                {
                    Docencia2TerrorDarknessController.Instance.ApplyCurrentAmbientAndInspectionFloor();
                }
                LockCursor(!_isPaused && !_showDoorPanel);
            }

            // 3. Linterna con tecla F (si se apagó por batería, tras 1 s de oscuridad vuelve a encender con F)
            if (kb.fKey.wasPressedThisFrame)
            {
                if (_batteryShutoffLockout && Time.time < _batteryLockoutEndTime)
                {
                    // Sigue en el segundo de oscuridad casi total tras apagarse
                }
                else
                {
                    if (_batteryShutoffLockout || batteryRemainingSeconds <= 0.5f)
                    {
                        _batteryShutoffLockout = false;
                        batteryRemainingSeconds = batteryTotalDurationSeconds; // Recarga al volver a encender con F
                        SetFlashlightState(true, true);
                    }
                    else
                    {
                        SetFlashlightState(!flashlightOn, true);
                    }
                }
            }

            // 4. Atajos de teletransporte (Teclas 1..8)
            foreach (var tp in _teleports)
            {
                if (kb[tp.key].wasPressedThisFrame)
                {
                    if (tp.y > 2.0f && GameObject.Find("PA_Interior") == null) continue;
                    TeleportToPlan(tp.planX, tp.y, tp.planZ, tp.yaw);
                }
            }

            // 5. Actualizar detección y resaltado de puerta frente al jugador + tecla E
            UpdateDoorHoverAndInteraction(kb);

            // 6. Actualizar temblor de mano y parpadeo ocasional de la linterna
            UpdateHandAndFlashlight(Time.time, Time.deltaTime);

            if (_isPaused) return;

            // 7. Mirar con el mouse (cuando el cursor está bloqueado)
            if (mouse != null && !_showDoorPanel)
            {
                if (mouse.leftButton.wasPressedThisFrame && Cursor.lockState != CursorLockMode.Locked)
                {
                    LockCursor(true);
                }

                if (Cursor.lockState == CursorLockMode.Locked)
                {
                    Vector2 delta = mouse.delta.ReadValue();
                    float yawDelta = delta.x * mouseSensitivity;
                    float pitchDelta = delta.y * mouseSensitivity;

                    transform.Rotate(0f, yawDelta, 0f);
                    _pitch = Mathf.Clamp(_pitch - pitchDelta, -82f, 82f);
                    if (playerCamera != null)
                    {
                        playerCamera.transform.localRotation = Quaternion.Euler(_pitch, 0f, 0f);
                    }
                }
            }

            // 8. Movimiento WASD + Shift (correr) + Ctrl (agacharse)
            UpdateMovement(kb, Time.deltaTime);
        }

        private void UpdateMovement(Keyboard kb, float dt)
        {
            bool wantsCrouch = kb.leftCtrlKey.isPressed || kb.rightCtrlKey.isPressed;
            bool wantsRun = (kb.leftShiftKey.isPressed || kb.rightShiftKey.isPressed) && !wantsCrouch;

            float targetH = wantsCrouch ? crouchingHeight : standingHeight;
            float targetCamY = wantsCrouch ? crouchingCameraY : standingCameraY;

            _currentHeight = Mathf.Lerp(_currentHeight, targetH, dt * 10f);
            _currentCamY = Mathf.Lerp(_currentCamY, targetCamY, dt * 10f);

            _cc.height = _currentHeight;
            _cc.center = new Vector3(0f, _currentHeight * 0.5f, 0f);

            Vector2 input = Vector2.zero;
            if (kb.wKey.isPressed || kb.upArrowKey.isPressed) input.y += 1f;
            if (kb.sKey.isPressed || kb.downArrowKey.isPressed) input.y -= 1f;
            if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) input.x += 1f;
            if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) input.x -= 1f;
            if (input.sqrMagnitude > 1f) input.Normalize();

            float speed = wantsCrouch ? crouchSpeed : (wantsRun ? runSpeed : walkSpeed);
            Vector3 moveHorizontal = (transform.right * input.x + transform.forward * input.y) * speed;

            if (_cc.isGrounded && _verticalVelocity < 0f)
            {
                _verticalVelocity = -2.5f; // Mantiene pegado al piso y peldaños al bajar/subir
            }
            _verticalVelocity += gravity * dt;

            Vector3 velocity = moveHorizontal + Vector3.up * _verticalVelocity;
            Vector3 prevPos = transform.position;
            _cc.Move(velocity * dt);
            Vector3 actualDisp = transform.position - prevPos;
            Vector2 horizDisp = new Vector2(actualDisp.x, actualDisp.z);
            float horizSpeed = dt > 0.0001f ? horizDisp.magnitude / dt : 0f;

            // Cabeceo sutil de caminata (sin mareo) y sonido de pasos sobre loseta
            if (playerCamera != null)
            {
                if (_cc.isGrounded && input.sqrMagnitude > 0.05f && horizSpeed > 0.4f)
                {
                    float freq = wantsRun ? bobFrequencyRun : bobFrequencyWalk;
                    _bobTimer += dt * freq;
                    float bobY = Mathf.Sin(_bobTimer) * bobAmplitudeY * (wantsCrouch ? 0.5f : 1f);
                    float bobX = Mathf.Cos(_bobTimer * 0.5f) * bobAmplitudeX * (wantsCrouch ? 0.5f : 1f);
                    playerCamera.transform.localPosition = new Vector3(bobX, _currentCamY + bobY, 0f);

                    _stepDistanceAccumulator += horizDisp.magnitude;
                    float stepThreshold = wantsRun ? 1.55f : (wantsCrouch ? 1.15f : 1.30f);
                    if (_stepDistanceAccumulator >= stepThreshold)
                    {
                        _stepDistanceAccumulator = 0f;
                        PlayFootstepSound(wantsRun ? 0.32f : 0.22f);
                    }
                }
                else
                {
                    _bobTimer = 0f;
                    playerCamera.transform.localPosition = Vector3.Lerp(
                        playerCamera.transform.localPosition,
                        new Vector3(0f, _currentCamY, 0f),
                        dt * 10f
                    );
                }
            }
        }

        // ====================================================================
        // ÓPTICA REALISTA DE LINTERNA: Cookie lineal 512x512 + Spill + Cono Volumétrico
        // ====================================================================
        public static Color[] BuildRealisticFlashlightCookiePixels(int size)
        {
            Color[] pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float nx = (x / (float)(size - 1)) * 2f - 1f;
                    float ny = (y / (float)(size - 1)) * 2f - 1f;

                    float angle = Mathf.Atan2(ny, nx);
                    float distRaw = Mathf.Sqrt(nx * nx * 0.96f + ny * ny * 1.04f);

                    // Micro-pétalos ópticos de lente TIR/Fresnel de celular y leve irregularidad orgánica en el borde exterior
                    float petalRipple = Mathf.Cos(angle * 6f + 0.35f) * 0.022f + Mathf.Sin(angle * 3f - 0.8f) * 0.015f;
                    float orgNoise = (Mathf.PerlinNoise(Mathf.Cos(angle) * 2.4f + 4.1f, Mathf.Sin(angle) * 2.4f + 6.3f) - 0.5f) * 0.085f;
                    float microLens = (Mathf.PerlinNoise(nx * 6.5f + 8.3f, ny * 6.5f + 2.1f) - 0.5f) * 0.032f;

                    float outerWeight = Mathf.SmoothStep(0.32f, 0.88f, distRaw);
                    float r = Mathf.Max(0f, distRaw + (petalRipple + orgNoise + microLens) * outerWeight);

                    // 1) Hotspot central realista tipo LED (gaussiano concentrado y brillante en el centro)
                    float hotspot = Mathf.Exp(-(r * r) / (0.24f * 0.24f));
                    // 2) Corona suave del reflector (cae progresivamente hacia los bordes sin aros duros artificiales)
                    float corona = Mathf.Pow(Mathf.Clamp01(1.0f - Mathf.SmoothStep(0.06f, 0.95f, r)), 1.55f);
                    // 3) Fundido limpio al borde exterior
                    float edgeFade = Mathf.Pow(Mathf.Clamp01((0.97f - r) / 0.26f), 1.25f);

                    float val = Mathf.Clamp01((hotspot * 0.58f + corona * 0.42f) * edgeFade);
                    pixels[y * size + x] = new Color(val, val, val, val);
                }
            }
            return pixels;
        }

        public static Texture2D CreateLinearFlashlightCookieRuntime(int size = 512)
        {
            if (_runtimeCookieTex != null && _runtimeCookieTex.width == size)
                return _runtimeCookieTex;

            var tex = new Texture2D(size, size, TextureFormat.RGBA32, true, true);
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.filterMode = FilterMode.Bilinear;
            tex.SetPixels(BuildRealisticFlashlightCookiePixels(size));
            tex.Apply(true, false);
            _runtimeCookieTex = tex;
            return tex;
        }

        private static Texture2D GetOrCreateVolumetricConeTexture()
        {
            if (_runtimeVolumetricTex != null) return _runtimeVolumetricTex;

            int w = 64, h = 128;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false, true);
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.filterMode = FilterMode.Bilinear;
            Color[] cols = new Color[w * h];
            for (int y = 0; y < h; y++)
            {
                float v = y / (float)(h - 1); // 0 = cerca de la lente, 1 = extremo lejano del cono
                float fadeIn = Mathf.SmoothStep(0.0f, 0.14f, v);
                float fadeOut = Mathf.Pow(1.0f - Mathf.SmoothStep(0.20f, 1.0f, v), 1.8f);
                float a = fadeIn * fadeOut;
                for (int x = 0; x < w; x++)
                {
                    float streak = 0.88f + 0.12f * Mathf.Sin((x / (float)w) * Mathf.PI * 6f);
                    float c = a * streak;
                    cols[y * w + x] = new Color(0.86f, 0.94f, 1.0f, c);
                }
            }
            tex.SetPixels(cols);
            tex.Apply(false, false);
            _runtimeVolumetricTex = tex;
            return tex;
        }

        private static Mesh BuildOpenConeMesh(float r0, float r1, float length, int segments = 24)
        {
            var mesh = new Mesh { name = "Mesh_HazVolumetrico_Linterna" };
            int rings = 6;
            var verts = new Vector3[(rings + 1) * (segments + 1)];
            var uvs = new Vector2[verts.Length];
            var tris = new int[rings * segments * 6];

            for (int r = 0; r <= rings; r++)
            {
                float t = r / (float)rings;
                float z = t * length;
                float radius = Mathf.Lerp(r0, r1, t);
                for (int s = 0; s <= segments; s++)
                {
                    float u = s / (float)segments;
                    float ang = u * Mathf.PI * 2f;
                    int idx = r * (segments + 1) + s;
                    verts[idx] = new Vector3(Mathf.Cos(ang) * radius, Mathf.Sin(ang) * radius, z);
                    uvs[idx] = new Vector2(u, t);
                }
            }

            int ti = 0;
            for (int r = 0; r < rings; r++)
            {
                for (int s = 0; s < segments; s++)
                {
                    int i0 = r * (segments + 1) + s;
                    int i1 = i0 + 1;
                    int i2 = i0 + (segments + 1);
                    int i3 = i2 + 1;

                    tris[ti++] = i0; tris[ti++] = i2; tris[ti++] = i1;
                    tris[ti++] = i1; tris[ti++] = i2; tris[ti++] = i3;
                }
            }

            mesh.vertices = verts;
            mesh.uv = uvs;
            mesh.triangles = tris;
            mesh.RecalculateNormals();
            return mesh;
        }

        public void EnsureRealisticFlashlightRig()
        {
            if (phoneSpotLight == null) return;

            // Calibrar parámetros ópticos realistas si venía de una escena anterior con valores bajos
            if (flashlightBaseIntensity < 85f) flashlightBaseIntensity = 95.0f;
            if (flashlightRange < 7.2f) flashlightRange = 7.8f;
            flashlightOuterAngle = 30.0f;
            flashlightInnerAngle = 12.0f;
            flashlightPowerMultiplier = Mathf.Clamp(flashlightPowerMultiplier, 0.4f, 2.0f);

            phoneSpotLight.type = LightType.Spot;
            phoneSpotLight.color = new Color(0.92f, 0.96f, 1.0f, 1f);
            phoneSpotLight.range = flashlightRange;
            phoneSpotLight.spotAngle = flashlightOuterAngle;
            phoneSpotLight.innerSpotAngle = flashlightInnerAngle;
            phoneSpotLight.shadows = LightShadows.Soft;
            phoneSpotLight.shadowStrength = 0.96f;
            phoneSpotLight.shadowBias = 0.015f;
            phoneSpotLight.shadowNormalBias = 0.16f;
            phoneSpotLight.shadowNearPlane = 0.10f;
            phoneSpotLight.cookie = CreateLinearFlashlightCookieRuntime(512);

            var uld = phoneSpotLight.GetComponent<UniversalAdditionalLightData>();
            if (uld != null) uld.softShadowQuality = SoftShadowQuality.High;

            // Converger exactamente al centro de la mirada a ~4.4 m frente a la cámara
            if (playerCamera != null && phoneSpotLight.transform.parent != null)
            {
                Vector3 targetLocalToCam = new Vector3(0f, -0.02f, 4.4f);
                Vector3 targetWorld = playerCamera.transform.TransformPoint(targetLocalToCam);
                Vector3 dirWorld = (targetWorld - phoneSpotLight.transform.position).normalized;
                phoneSpotLight.transform.rotation = Quaternion.LookRotation(dirWorld, playerCamera.transform.up);
            }

            // 1) Luz secundaria de rebote/dispersión suave (sin sombras, alcance corto) para transición natural sin recorte circular duro
            Transform spillT = phoneSpotLight.transform.Find("Luz_Linterna_Rebote_Suave");
            if (spillT == null)
            {
                var spillGo = new GameObject("Luz_Linterna_Rebote_Suave");
                spillGo.transform.SetParent(phoneSpotLight.transform, false);
                spillT = spillGo.transform;
            }
            var spillLight = spillT.GetComponent<Light>();
            if (spillLight == null) spillLight = spillT.gameObject.AddComponent<Light>();
            flashlightSpillLight = spillLight;
            flashlightSpillLight.type = LightType.Spot;
            flashlightSpillLight.color = new Color(0.76f, 0.87f, 0.98f, 1f);
            flashlightSpillLight.spotAngle = 62.0f;
            flashlightSpillLight.innerSpotAngle = 8.0f;
            flashlightSpillLight.range = 4.6f;
            flashlightSpillLight.intensity = flashlightSpillBaseIntensity;
            flashlightSpillLight.shadows = LightShadows.None;
            flashlightSpillLight.cullingMask = (1 << 8) | (1 << 30);
            var spillUld = spillT.GetComponent<UniversalAdditionalLightData>();
            if (spillUld == null) spillUld = spillT.gameObject.AddComponent<UniversalAdditionalLightData>();
            spillUld.renderingLayers = uint.MaxValue;

            // 2) Haz volumétrico sutil en el aire frente al celular (muy tenue para dar profundidad sin tapar la vista)
            Transform beamT = phoneSpotLight.transform.Find("Haz_Volumetrico_Linterna");
            if (beamT == null)
            {
                var beamGo = new GameObject("Haz_Volumetrico_Linterna");
                beamGo.transform.SetParent(phoneSpotLight.transform, false);
                beamGo.transform.localPosition = new Vector3(0f, 0f, 0.06f);
                var mf = beamGo.AddComponent<MeshFilter>();
                mf.sharedMesh = BuildOpenConeMesh(0.016f, 0.68f, 2.9f);
                var mr = beamGo.AddComponent<MeshRenderer>();
                Shader unlitShader = Shader.Find("Universal Render Pipeline/Unlit");
                if (unlitShader == null) unlitShader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
                if (unlitShader == null) unlitShader = Shader.Find("Unlit/Transparent");
                if (unlitShader == null) unlitShader = Shader.Find("Sprites/Default");
                if (unlitShader != null)
                {
                    var beamMat = new Material(unlitShader);
                    var coneTex = GetOrCreateVolumetricConeTexture();
                    if (beamMat.HasProperty("_BaseMap")) beamMat.SetTexture("_BaseMap", coneTex);
                    else beamMat.mainTexture = coneTex;
                    Color initBeamCol = new Color(0.86f, 0.94f, 1.0f, volumetricBeamBaseAlpha);
                    if (beamMat.HasProperty("_BaseColor")) beamMat.SetColor("_BaseColor", initBeamCol);
                    if (beamMat.HasProperty("_Color")) beamMat.SetColor("_Color", initBeamCol);
                    if (beamMat.HasProperty("_Surface")) beamMat.SetFloat("_Surface", 1f);
                    if (beamMat.HasProperty("_Blend")) beamMat.SetFloat("_Blend", 2f); // Additive
                    if (beamMat.HasProperty("_SrcBlend")) beamMat.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
                    if (beamMat.HasProperty("_DstBlend")) beamMat.SetFloat("_DstBlend", (float)BlendMode.One);
                    if (beamMat.HasProperty("_ZWrite")) beamMat.SetFloat("_ZWrite", 0f);
                    if (beamMat.HasProperty("_Cull")) beamMat.SetFloat("_Cull", 0f);
                    beamMat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                    beamMat.renderQueue = 3100;
                    mr.sharedMaterial = beamMat;
                    mr.shadowCastingMode = ShadowCastingMode.Off;
                    mr.receiveShadows = false;
                    volumetricBeamRenderer = mr;
                }
                else
                {
                    mr.enabled = false;
                    volumetricBeamRenderer = null;
                }
            }
            else
            {
                volumetricBeamRenderer = beamT.GetComponent<Renderer>();
            }
        }

        private void ApplyFlashlightOutputs(float beamFactor)
        {
            float eff = beamFactor * flashlightPowerMultiplier;
            if (phoneSpotLight != null)
            {
                phoneSpotLight.enabled = flashlightOn && eff > 0.001f;
                phoneSpotLight.intensity = flashlightOn ? flashlightBaseIntensity * eff : 0f;
            }
            if (flashlightSpillLight != null)
            {
                flashlightSpillLight.enabled = flashlightOn && eff > 0.001f;
                flashlightSpillLight.intensity = flashlightOn ? flashlightSpillBaseIntensity * eff : 0f;
            }
            if (volumetricBeamRenderer != null)
            {
                bool showBeam = flashlightOn && eff > 0.02f;
                volumetricBeamRenderer.enabled = showBeam;
                if (showBeam)
                {
                    if (_volumetricMpb == null) _volumetricMpb = new MaterialPropertyBlock();
                    volumetricBeamRenderer.GetPropertyBlock(_volumetricMpb);
                    Color beamCol = new Color(0.86f, 0.94f, 1.0f, volumetricBeamBaseAlpha * Mathf.Clamp(eff, 0.1f, 1.5f));
                    _volumetricMpb.SetColor("_BaseColor", beamCol);
                    _volumetricMpb.SetColor("_Color", beamCol);
                    volumetricBeamRenderer.SetPropertyBlock(_volumetricMpb);
                }
            }
            if (phoneFlashLedRenderer != null && phoneFlashLedRenderer.sharedMaterial != null)
            {
                var mpb = new MaterialPropertyBlock();
                phoneFlashLedRenderer.GetPropertyBlock(mpb);
                Color c = flashlightOn ? new Color(0.92f, 0.96f, 1.0f, 1f) * (3.2f * Mathf.Clamp(eff, 0.15f, 1.5f)) : new Color(0.15f, 0.16f, 0.18f, 1f);
                mpb.SetColor("_BaseColor", c);
                mpb.SetColor("_EmissionColor", c);
                phoneFlashLedRenderer.SetPropertyBlock(mpb);
            }
        }

        private void UpdateHandAndFlashlight(float time, float dt)
        {
            if (phoneHandRoot != null)
            {
                // Inercia física realista de la mano al girar la cámara + balanceo sutil de respiración/paso
                Vector2 rawDelta = (Mouse.current != null && !_isPaused && !_showDoorPanel && Cursor.lockState == CursorLockMode.Locked)
                    ? Mouse.current.delta.ReadValue()
                    : Vector2.zero;
                Vector2 targetSway = new Vector2(
                    Mathf.Clamp(rawDelta.x * 0.055f, -3.2f, 3.2f),
                    Mathf.Clamp(rawDelta.y * 0.055f, -2.4f, 2.4f)
                );
                _smoothedLookSway = Vector2.Lerp(_smoothedLookSway, targetSway, 1f - Mathf.Exp(-dt * 11f));

                float swayX = (Mathf.PerlinNoise(time * 1.4f, 1.7f) - 0.5f) * 0.008f - _smoothedLookSway.x * 0.0045f;
                float swayY = (Mathf.PerlinNoise(time * 1.8f, 5.3f) - 0.5f) * 0.010f - _smoothedLookSway.y * 0.0040f;
                float rotPitch = (Mathf.PerlinNoise(time * 2.1f, 9.1f) - 0.5f) * 1.4f + _smoothedLookSway.y * 1.15f;
                float rotYaw = (Mathf.PerlinNoise(time * 1.6f, 13.4f) - 0.5f) * 1.2f - _smoothedLookSway.x * 1.25f;
                float rotRoll = -_smoothedLookSway.x * 0.9f;

                float walkBobY = Mathf.Sin(_bobTimer) * 0.009f;
                float walkBobX = Mathf.Cos(_bobTimer * 0.5f) * 0.005f;
                phoneHandRoot.localPosition = _phoneBaseLocalPos + new Vector3(swayX + walkBobX, swayY + walkBobY, 0f);
                phoneHandRoot.localRotation = _phoneBaseLocalRot * Quaternion.Euler(rotPitch, rotYaw, rotRoll);
            }

            if (phoneSpotLight != null && flashlightOn)
            {
                // Drenaje opcional de batería (6 a 8 minutos = 420 s): cuando baja, se debilita antes de apagarse
                float batteryFactor = 1.0f;
                if (enableBatteryDrain && !_isPaused && !_showDoorPanel)
                {
                    batteryRemainingSeconds = Mathf.Max(0f, batteryRemainingSeconds - dt);
                    float pct = BatteryPercent01;
                    if (pct <= 0f)
                    {
                        // Se apaga dejando 1 segundo de oscuridad casi total, y permite volver a encender con F
                        _batteryShutoffLockout = true;
                        _batteryLockoutEndTime = time + 1.05f;
                        SetFlashlightState(false, true);
                        return;
                    }
                    else if (pct < 0.20f)
                    {
                        // En el último 20% (~80 s), se debilita progresivamente y parpadea más seguido antes de apagarse
                        float tLow = pct / 0.20f;
                        batteryFactor = Mathf.Lerp(0.28f, 1.0f, tLow);
                        if (Mathf.PerlinNoise(time * 9.5f, 11.2f) > Mathf.Lerp(0.45f, 0.85f, tLow))
                        {
                            batteryFactor *= 0.42f;
                        }
                    }
                }

                // Parpadeo muy ocasional del flash del teléfono
                if (time > _nextFlickerTime)
                {
                    _nextFlickerTime = time + Mathf.Lerp(12f, 26f, Mathf.PerlinNoise(time * 0.37f, 2.9f));
                    _flickerEndTime = time + 0.14f;
                }

                float factor = 1.0f;
                if (time < _flickerEndTime)
                {
                    float pulse = Mathf.PerlinNoise(time * 42f, 7.7f);
                    factor = pulse > 0.45f ? 0.35f : 0.85f;
                }
                else
                {
                    factor = 0.96f + (Mathf.PerlinNoise(time * 3.2f, 4.1f) - 0.5f) * 0.08f;
                }

                ApplyFlashlightOutputs(factor * batteryFactor);
            }
        }

        private void UpdateDoorHoverAndInteraction(Keyboard kb)
        {
            _hoveredDoor = null;
            _hoveredDoorLabel = "";

            if (playerCamera == null) return;

            Ray ray = playerCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
            if (Physics.Raycast(ray, out RaycastHit hit, interactionDistance, ~0, QueryTriggerInteraction.Ignore))
            {
                var door = hit.collider.GetComponentInParent<Docencia2InteractiveDoor>();
                if (door != null)
                {
                    _hoveredDoor = door;
                    string shortName = FormatShortDoorName(door.transform);
                    string action = door.isOpen ? "Cerrar" : "Abrir";
                    _hoveredDoorLabel = $"[E] {action} {shortName}";

                    if (kb.eKey.wasPressedThisFrame)
                    {
                        door.Toggle(playerCamera.transform.position);
                    }
                }
            }
        }

        public void SetFlashlightState(bool on, bool playSound)
        {
            flashlightOn = on;
            ApplyFlashlightOutputs(on ? 1.0f : 0f);
            if (playSound && _sfxSource != null && _phoneClickClip != null)
            {
                _sfxSource.PlayOneShot(_phoneClickClip, 0.25f);
            }
        }

        public void TeleportToPlan(float planX, float yHeight, float planZ, float yawDegrees)
        {
            if (_cc == null) _cc = GetComponent<CharacterController>();
            bool wasEnabled = _cc.enabled;
            _cc.enabled = false;
            transform.position = PlanToUnityPos(planX, yHeight + 0.02f, planZ);
            transform.rotation = Quaternion.Euler(0f, yawDegrees, 0f);
            _pitch = 0f;
            if (playerCamera != null) playerCamera.transform.localRotation = Quaternion.identity;
            _verticalVelocity = 0f;
            _cc.enabled = wasEnabled;
            Physics.SyncTransforms();
        }

        private void LockCursor(bool locked)
        {
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !locked;
        }

        // ====================================================================
        // MODO PRUEBA DE PUERTAS (Tecla G) + Generación de Reporte
        // ====================================================================
        public void RunDoorAuditAndPopulateList(bool saveFiles)
        {
            doorTestEntries.Clear();
            var allDoors = FindObjectsByType<Docencia2InteractiveDoor>(FindObjectsInactive.Include)
                .OrderBy(d => GetHierarchyPath(d.transform))
                .ToArray();

            var csvLines = new List<string>
            {
                "id_puerta,ruta_jerarquia,planta,plan_x,plan_z,abre_90,vaiven,estado,interferencias"
            };
            var failedLines = new List<string>();

            int index = 1;
            foreach (var d in allDoors)
            {
                string path = GetHierarchyPath(d.transform);
                string shortId = FormatShortDoorName(d.transform);
                var col = d.GetComponent<BoxCollider>();
                bool prevOpen = d.isOpen;
                Quaternion prevRot = d.transform.localRotation;

                var blockers = new HashSet<string>();
                bool angleOk = true;

                if (col != null)
                {
                    foreach (bool rev in d.doubleSwing ? new[] { false, true } : new[] { false })
                    {
                        for (int step = 0; step <= 9; step++)
                        {
                            d.transform.localRotation = d.closedRotation * Quaternion.Euler(0f, d.inwardAngle * (rev ? -1f : 1f) * (step / 9f), 0f);
                            Physics.SyncTransforms();

                            Vector3 worldCenter = d.transform.TransformPoint(col.center);
                            Vector3 halfExtents = Vector3.Scale(col.size * 0.5f, d.transform.lossyScale);
                            Collider[] hits = Physics.OverlapBox(worldCenter, halfExtents, d.transform.rotation, ~0, QueryTriggerInteraction.Ignore);

                            foreach (var other in hits)
                            {
                                if (other == col || other == _cc || other.transform.IsChildOf(transform)) continue;
                                if (other.transform.IsChildOf(d.transform.parent) || !other.enabled) continue;

                                if (Physics.ComputePenetration(
                                    col, col.transform.position, col.transform.rotation,
                                    other, other.transform.position, other.transform.rotation,
                                    out _, out float dist) && dist > 0.02f)
                                {
                                    blockers.Add(other.name);
                                }
                            }
                        }

                        d.SetOpen(true, rev);
                        angleOk &= Mathf.Abs(Quaternion.Angle(d.closedRotation, d.transform.localRotation) - 90f) < 0.1f;
                    }
                }
                else
                {
                    angleOk = false;
                    blockers.Add("Sin BoxCollider");
                }

                // Restaurar estado previo
                d.isOpen = prevOpen;
                d.transform.localRotation = prevRot;
                Physics.SyncTransforms();

                Vector3 wPos = d.transform.position;
                Vector2 pPos = UnityToPlanXZ(wPos);
                string floor = wPos.y > 2.0f ? "PA" : "PB";

                bool passed = angleOk && blockers.Count == 0;
                string statusText = passed
                    ? (d.doubleSwing ? "ABRE OK (Vaivén ±90°)" : "ABRE OK (90° Interior)")
                    : (!angleOk ? "NO ABRE 90°" : "ATRAVIESA / SE TRABA: " + string.Join(" | ", blockers));

                doorTestEntries.Add(new DoorTestEntry
                {
                    id = $"{index:00}_{shortId}",
                    hierarchyPath = path,
                    door = d,
                    status = statusText,
                    passed = passed,
                    worldPos = wPos,
                    planPos = pPos
                });

                csvLines.Add(FormattableString.Invariant(
                    $"\"{shortId}\",\"{path}\",{floor},{pPos.x:F2},{pPos.y:F2},{angleOk},{d.doubleSwing},\"{statusText}\",\"{string.Join(" | ", blockers)}\""));

                if (!passed)
                {
                    failedLines.Add($"{shortId} ({floor} x={pPos.x:F2}, z={pPos.y:F2}): {statusText}");
                }
                index++;
            }

            if (saveFiles)
            {
                try
                {
                    string outDir = Path.Combine(Directory.GetCurrentDirectory(), "FaseA_Correcciones/Paso1_Jugador");
                    Directory.CreateDirectory(outDir);
                    File.WriteAllLines(Path.Combine(outDir, "reporte_puertas_paso1.csv"), csvLines);

                    var summary = new List<string>
                    {
                        "=== REPORTE DE PRUEBA DE PUERTAS (PASO 1 - MODO G) ===",
                        $"Fecha: {DateTime.Now:yyyy-MM-dd HH:mm:ss}",
                        $"Total de puertas evaluadas (PB + PA): {doorTestEntries.Count}",
                        $"Puertas que abren a 90° sin trabarse ni atravesar nada: {doorTestEntries.Count(e => e.passed)}",
                        $"Puertas que fallaron: {failedLines.Count}",
                        ""
                    };
                    if (failedLines.Count == 0)
                        summary.Add("RESULTADO: 0 PUERTAS FALLARON. Las 70 puertas abren y cierran libremente.");
                    else
                        summary.AddRange(failedLines);

                    File.WriteAllLines(Path.Combine(outDir, "reporte_puertas_fallidas.txt"), summary);
                    _lastReportMessage = $"Reporte guardado ({doorTestEntries.Count(e => e.passed)}/{doorTestEntries.Count} OK, {failedLines.Count} fallos)";
                }
                catch (Exception ex)
                {
                    _lastReportMessage = "Error guardando reporte: " + ex.Message;
                }
            }
        }

        private static string GetHierarchyPath(Transform t)
        {
            return t.parent ? GetHierarchyPath(t.parent) + "/" + t.name : t.name;
        }

        private static string FormatShortDoorName(Transform t)
        {
            string floor = t.position.y > 2.0f ? "PA" : "PB";
            if (t.parent != null)
            {
                string pName = t.parent.name;
                if (pName.StartsWith("Puerta_Cabina") || pName.StartsWith("P"))
                    return $"{floor}_{pName}";
            }
            return $"{floor}_{t.name}";
        }

        // ====================================================================
        // INTERFAZ EN PANTALLA CON ESCALADO AUTOMÁTICO HD/4K (Letras y Menú Grandes)
        // ====================================================================
        private static Texture2D _panelBgTex;
        private static Texture2D _barBgTex;
        private static Texture2D _btnBgTex;
        private static Texture2D _btnHoverBgTex;

        private static void EnsureGuiTextures()
        {
            if (_panelBgTex == null) _panelBgTex = MakeSolidTex(new Color(0.05f, 0.08f, 0.11f, 0.94f));
            if (_barBgTex == null) _barBgTex = MakeSolidTex(new Color(0.04f, 0.07f, 0.10f, 0.88f));
            if (_btnBgTex == null) _btnBgTex = MakeSolidTex(new Color(0.14f, 0.22f, 0.29f, 0.96f));
            if (_btnHoverBgTex == null) _btnHoverBgTex = MakeSolidTex(new Color(0.22f, 0.36f, 0.46f, 0.98f));
        }

        private static Texture2D MakeSolidTex(Color col)
        {
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            tex.SetPixels(new[] { col, col, col, col });
            tex.Apply();
            return tex;
        }

        private void OnGUI()
        {
            EnsureGuiTextures();

            // Escalado automático para que en pantallas 4K (3840x2160) o 1080p el menú y las letras siempre se vean grandes, claros y nítidos:
            // Usamos una altura virtual de 900 px como referencia (en 4K multiplica x2.4, haciendo el texto y botones más del doble de grandes)
            Matrix4x4 prevMatrix = GUI.matrix;
            float uiScale = Mathf.Clamp(Screen.height / 900f, 1.0f, 3.2f);
            GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(uiScale, uiScale, 1f));

            float vw = Screen.width / uiScale;
            float vh = Screen.height / uiScale;
            float cx = vw * 0.5f;
            float cy = vh * 0.5f;

            var switcher = FindAnyObjectByType<Docencia2ModeSwitcher>();
            var darkness = Docencia2TerrorDarknessController.Instance;
            bool isTerrorMode = switcher != null && switcher.modoActual == Docencia2VisualMode.Terror;
            string modeName = isTerrorMode ? "TERROR (OSCURIDAD EXTREMA)" : "NOCHE";
            Vector2 pxz = UnityToPlanXZ(transform.position);
            string planta = transform.position.y > 2.0f ? "PA" : "PB";
            int batPct = Mathf.RoundToInt(BatteryPercent01 * 100f);
            string brightPct = darkness != null ? $"{Mathf.RoundToInt(darkness.brightnessLevel * 100f)}%" : "Base";

            // Estilos grandes y legibles
            GUIStyle topBarStyle = new GUIStyle(GUI.skin.box)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 19,
                fontStyle = FontStyle.Bold,
                normal = { background = _barBgTex, textColor = isTerrorMode ? new Color(1.0f, 0.78f, 0.42f, 1f) : new Color(0.90f, 0.96f, 1.0f, 1f) }
            };

            GUIStyle panelBoxStyle = new GUIStyle(GUI.skin.box)
            {
                alignment = TextAnchor.UpperCenter,
                fontSize = 24,
                fontStyle = FontStyle.Bold,
                padding = new RectOffset(16, 16, 14, 14),
                normal = { background = _panelBgTex, textColor = new Color(0.95f, 0.98f, 1.0f, 1f) }
            };

            GUIStyle labelBigStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleLeft,
                fontSize = 19,
                fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(0.92f, 0.96f, 1.0f, 1f) }
            };

            GUIStyle toggleBigStyle = new GUIStyle(GUI.skin.toggle)
            {
                fontSize = 18,
                fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(0.90f, 0.95f, 1.0f, 1f) },
                onNormal = { textColor = new Color(0.65f, 1.0f, 0.78f, 1f) }
            };

            GUIStyle buttonBigStyle = new GUIStyle(GUI.skin.button)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 19,
                fontStyle = FontStyle.Bold,
                normal = { background = _btnBgTex, textColor = Color.white },
                hover = { background = _btnHoverBgTex, textColor = new Color(1f, 0.92f, 0.65f, 1f) },
                active = { background = _btnHoverBgTex, textColor = Color.white }
            };

            // 1. Punto central y aviso de puerta al apuntar
            GUIStyle dotStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = _hoveredDoor != null ? 26 : 18,
                fontStyle = FontStyle.Bold,
                normal = { textColor = _hoveredDoor != null ? new Color(0.55f, 1.0f, 0.70f, 1f) : new Color(1f, 1f, 1f, 0.55f) }
            };
            GUI.Label(new Rect(cx - 20f, cy - 20f, 40f, 40f), _hoveredDoor != null ? "◉" : "•", dotStyle);

            if (_hoveredDoor != null && !string.IsNullOrEmpty(_hoveredDoorLabel))
            {
                GUIStyle promptStyle = new GUIStyle(GUI.skin.box)
                {
                    alignment = TextAnchor.MiddleCenter,
                    fontSize = 21,
                    fontStyle = FontStyle.Bold,
                    normal = { background = _panelBgTex, textColor = new Color(0.75f, 1.0f, 0.82f, 1f) }
                };
                GUI.Box(new Rect(cx - 230f, cy + 26f, 460f, 42f), _hoveredDoorLabel, promptStyle);
            }

            // 2. Barra superior de controles grande y clara
            float barW = Mathf.Min(vw - 24f, 1520f);
            GUI.Box(
                new Rect((vw - barW) * 0.5f, 10f, barW, 42f),
                $"Modo: {modeName} [T]  |  Linterna: {(flashlightOn ? "ENCENDIDA" : "APAGADA")} ({batPct}%) [F]  |  Brillo: {brightPct} [B]  |  Puerta [E]  |  Puertas [G]  |  Pausa [Esc]  |  {planta} ({pxz.x:F1}, {pxz.y:F1})",
                topBarStyle
            );

            // 3. Menú de Pausa (Esc) Grande y Legible
            if (_isPaused)
            {
                float mw = 680f;
                float mh = 515f;
                Rect pauseRect = new Rect(cx - mw * 0.5f, cy - mh * 0.5f, mw, mh);
                GUI.Box(pauseRect, "PAUSA — VAELITH DOCENCIA 2", panelBoxStyle);

                float xLeft = pauseRect.x + 34f;
                float contentW = mw - 68f;
                float yCur = pauseRect.y + 58f;

                if (darkness != null)
                {
                    GUI.Label(
                        new Rect(xLeft, yCur, contentW, 28f),
                        $"Brillo ambiente [Tecla B]:  {Mathf.RoundToInt(darkness.brightnessLevel * 100f)}%",
                        labelBigStyle
                    );
                    yCur += 30f;

                    if (GUI.Button(new Rect(xLeft, yCur, 52f, 34f), " - ", buttonBigStyle))
                    {
                        darkness.SetBrightnessLevel(darkness.brightnessLevel - 0.08f);
                    }
                    float newBright = GUI.HorizontalSlider(new Rect(xLeft + 64f, yCur + 9f, contentW - 128f, 26f), darkness.brightnessLevel, 0.0f, 1.0f);
                    if (Mathf.Abs(newBright - darkness.brightnessLevel) > 0.001f)
                    {
                        darkness.SetBrightnessLevel(newBright);
                    }
                    if (GUI.Button(new Rect(xLeft + contentW - 52f, yCur, 52f, 34f), " + ", buttonBigStyle))
                    {
                        darkness.SetBrightnessLevel(darkness.brightnessLevel + 0.08f);
                    }
                    yCur += 46f;
                }

                // Control en vivo de potencia de la linterna realista para ir probando
                GUI.Label(
                    new Rect(xLeft, yCur, contentW, 28f),
                    $"Potencia de Linterna Celular:  {Mathf.RoundToInt(flashlightPowerMultiplier * 100f)}%",
                    labelBigStyle
                );
                yCur += 30f;
                if (GUI.Button(new Rect(xLeft, yCur, 52f, 34f), " - ", buttonBigStyle))
                {
                    flashlightPowerMultiplier = Mathf.Clamp(flashlightPowerMultiplier - 0.15f, 0.4f, 2.0f);
                }
                float newFlashPow = GUI.HorizontalSlider(new Rect(xLeft + 64f, yCur + 9f, contentW - 128f, 26f), flashlightPowerMultiplier, 0.4f, 2.0f);
                if (Mathf.Abs(newFlashPow - flashlightPowerMultiplier) > 0.005f)
                {
                    flashlightPowerMultiplier = newFlashPow;
                }
                if (GUI.Button(new Rect(xLeft + contentW - 52f, yCur, 52f, 34f), " + ", buttonBigStyle))
                {
                    flashlightPowerMultiplier = Mathf.Clamp(flashlightPowerMultiplier + 0.15f, 0.4f, 2.0f);
                }
                yCur += 46f;

                enableBatteryDrain = GUI.Toggle(
                    new Rect(xLeft, yCur, contentW, 30f),
                    enableBatteryDrain,
                    $"  Activar desgaste de batería del celular (7 min — actual: {batPct}%)",
                    toggleBigStyle
                );
                yCur += 32f;

                if (darkness != null)
                {
                    darkness.enableContinuousDroneAndHum = GUI.Toggle(
                        new Rect(xLeft, yCur, contentW, 30f),
                        darkness.enableContinuousDroneAndHum,
                        "  Sonido ambiente continuo de fondo (Apagado por defecto)",
                        toggleBigStyle
                    );
                    yCur += 36f;
                }
                else
                {
                    yCur += 14f;
                }

                if (GUI.Button(new Rect(xLeft, yCur, contentW, 46f), $"Cambiar Modo Visual (Actual: {(isTerrorMode ? "TERROR" : "NOCHE")}) [Tecla T]", buttonBigStyle))
                {
                    if (switcher != null) switcher.ToggleMode();
                    if (darkness != null) darkness.SyncState();
                }
                yCur += 56f;

                if (GUI.Button(new Rect(xLeft, yCur, contentW, 46f), "Abrir / Cerrar Panel de Prueba de Puertas [Tecla G]", buttonBigStyle))
                {
                    _showDoorPanel = !_showDoorPanel;
                    if (darkness != null) darkness.ApplyCurrentAmbientAndInspectionFloor();
                }
                yCur += 56f;

                if (GUI.Button(new Rect(xLeft, yCur, contentW, 48f), "Reanudar Partida [Esc]", buttonBigStyle))
                {
                    _isPaused = false;
                    LockCursor(!_showDoorPanel);
                }
            }

            // 4. Panel de Prueba de Puertas (Tecla G) Grande y Legible
            if (_showDoorPanel)
            {
                float pw = Mathf.Min(1160f, vw - 40f);
                float ph = Mathf.Min(720f, vh - 80f);
                Rect panelRect = new Rect((vw - pw) * 0.5f, 60f, pw, ph);
                GUI.Box(panelRect, $"MODO PRUEBA DE PUERTAS [Tecla G] — Total: {doorTestEntries.Count} puertas | {_lastReportMessage}", panelBoxStyle);

                float topBtnY = panelRect.y + 56f;
                if (GUI.Button(new Rect(panelRect.x + 20f, topBtnY, 310f, 40f), "Re-evaluar y Guardar Reporte", buttonBigStyle))
                {
                    RunDoorAuditAndPopulateList(saveFiles: true);
                }
                if (GUI.Button(new Rect(panelRect.x + 344f, topBtnY, 210f, 40f), "Abrir Todas", buttonBigStyle))
                {
                    foreach (var e in doorTestEntries) if (e.door) e.door.SetOpen(true);
                }
                if (GUI.Button(new Rect(panelRect.x + 568f, topBtnY, 210f, 40f), "Cerrar Todas", buttonBigStyle))
                {
                    foreach (var e in doorTestEntries) if (e.door) e.door.SetOpen(false);
                }
                if (GUI.Button(new Rect(panelRect.xMax - 180f, topBtnY, 160f, 40f), "Cerrar [G]", buttonBigStyle))
                {
                    _showDoorPanel = false;
                    if (darkness != null) darkness.ApplyCurrentAmbientAndInspectionFloor();
                    LockCursor(!_isPaused);
                }

                float rowH = 38f;
                Rect scrollOuter = new Rect(panelRect.x + 16f, panelRect.y + 108f, pw - 32f, ph - 124f);
                Rect scrollInner = new Rect(0f, 0f, pw - 60f, doorTestEntries.Count * rowH + 16f);
                _doorPanelScroll = GUI.BeginScrollView(scrollOuter, _doorPanelScroll, scrollInner);

                GUIStyle rowLabelStyle = new GUIStyle(GUI.skin.label)
                {
                    alignment = TextAnchor.MiddleLeft,
                    fontSize = 17,
                    fontStyle = FontStyle.Bold,
                    normal = { textColor = new Color(0.92f, 0.96f, 1.0f, 1f) }
                };

                for (int i = 0; i < doorTestEntries.Count; i++)
                {
                    var entry = doorTestEntries[i];
                    float ry = i * rowH;
                    GUI.Label(new Rect(8f, ry, 340f, 34f), entry.id, rowLabelStyle);
                    GUI.Label(new Rect(355f, ry, 235f, 34f), $"Plan({entry.planPos.x:F1}, {entry.planPos.y:F1}) Y={entry.worldPos.y:F1}", rowLabelStyle);
                    GUI.Label(new Rect(595f, ry, 290f, 34f), entry.status, rowLabelStyle);

                    if (GUI.Button(new Rect(895f, ry + 2f, 105f, 32f), entry.door != null && entry.door.isOpen ? "Cerrar" : "Abrir", buttonBigStyle))
                    {
                        if (entry.door != null) entry.door.Toggle(transform.position);
                    }
                    if (GUI.Button(new Rect(1010f, ry + 2f, 95f, 32f), "Ir ahí", buttonBigStyle))
                    {
                        float floorY = entry.worldPos.y > 2.0f ? 3.606f : 0.20f;
                        TeleportToPlan(Mathf.Clamp(entry.planPos.x, 1.0f, 27.4f), floorY, Mathf.Clamp(entry.planPos.y + 1.1f, 1.0f, 52.5f), 0f);
                    }
                }

                GUI.EndScrollView();
            }

            GUI.matrix = prevMatrix;
        }

        // ====================================================================
        // AUDIO PROCEDURAL POR CÓDIGO (SUPUESTO — Sin archivos externos)
        // ====================================================================
        private void EnsureAudioInitialized()
        {
            if (_footstepSource == null)
            {
                _footstepSource = gameObject.AddComponent<AudioSource>();
                _footstepSource.spatialBlend = 0f;
                _footstepSource.playOnAwake = false;
                _footstepSource.volume = 0.25f;
            }
            if (_sfxSource == null)
            {
                _sfxSource = gameObject.AddComponent<AudioSource>();
                _sfxSource.spatialBlend = 0f;
                _sfxSource.playOnAwake = false;
                _sfxSource.volume = 0.30f;
            }

            if (_tileStepClips == null || _tileStepClips.Length == 0)
            {
                _tileStepClips = new AudioClip[4];
                for (int i = 0; i < 4; i++)
                {
                    _tileStepClips[i] = CreateProceduralTileStepClip(i);
                }
            }
            if (_doorOpenClip == null) _doorOpenClip = CreateProceduralDoorClip(true);
            if (_doorCloseClip == null) _doorCloseClip = CreateProceduralDoorClip(false);
            if (_phoneClickClip == null) _phoneClickClip = CreateProceduralPhoneClickClip();
        }

        private void PlayFootstepSound(float volume)
        {
            if (_footstepSource == null || _tileStepClips == null || _tileStepClips.Length == 0) return;
            int idx = Mathf.Abs( (int)(_bobTimer * 10f) ) % _tileStepClips.Length;
            _footstepSource.pitch = 0.94f + (idx * 0.04f);
            _footstepSource.PlayOneShot(_tileStepClips[idx], volume);
        }

        private void HandleDoorSound(Docencia2InteractiveDoor door, bool opened)
        {
            if (_sfxSource == null) return;
            AudioClip clip = opened ? _doorOpenClip : _doorCloseClip;
            if (clip != null)
            {
                _sfxSource.pitch = opened ? 1.02f : 0.95f;
                _sfxSource.PlayOneShot(clip, 0.30f);
            }
        }

        private static AudioClip CreateProceduralTileStepClip(int variant)
        {
            int sampleRate = 22050;
            int samples = sampleRate / 8; // 125 ms
            float[] data = new float[samples];
            float baseFreq = 165f + variant * 18f;

            for (int i = 0; i < samples; i++)
            {
                float t = i / (float)sampleRate;
                float env = Mathf.Exp(-t * 38f);
                // Golpe seco cerámico + leve resonancia de suela sobre loseta
                float click = Mathf.Sin(2f * Mathf.PI * (baseFreq * 2.4f) * t) * Mathf.Exp(-t * 95f);
                float thud = Mathf.Sin(2f * Mathf.PI * baseFreq * t) * env;
                float noise = (Mathf.PerlinNoise(i * 0.37f, variant * 3.1f) - 0.5f) * 0.45f * Mathf.Exp(-t * 65f);
                data[i] = Mathf.Clamp((click * 0.45f + thud * 0.40f + noise) * 0.6f, -1f, 1f);
            }

            AudioClip clip = AudioClip.Create("Step_Loseta_SUPUESTO_" + variant, samples, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private static AudioClip CreateProceduralDoorClip(bool open)
        {
            int sampleRate = 22050;
            int samples = (int)(sampleRate * 0.28f);
            float[] data = new float[samples];

            for (int i = 0; i < samples; i++)
            {
                float t = i / (float)sampleRate;
                // Picaporte inicial + leve giro de bisagra
                float latchEnv = Mathf.Exp(-t * 55f);
                float latch = Mathf.Sin(2f * Mathf.PI * (open ? 420f : 310f) * t) * latchEnv;
                float creakEnv = Mathf.Sin(Mathf.Clamp01(t / 0.28f) * Mathf.PI) * 0.35f;
                float creakFreq = (open ? 240f : 190f) + Mathf.Sin(t * 28f) * 22f;
                float creak = Mathf.Sin(2f * Mathf.PI * creakFreq * t) * creakEnv;
                data[i] = Mathf.Clamp((latch * 0.55f + creak * 0.35f) * 0.6f, -1f, 1f);
            }

            AudioClip clip = AudioClip.Create(open ? "Door_Open_SUPUESTO" : "Door_Close_SUPUESTO", samples, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private static AudioClip CreateProceduralPhoneClickClip()
        {
            int sampleRate = 22050;
            int samples = sampleRate / 20; // 50 ms
            float[] data = new float[samples];
            for (int i = 0; i < samples; i++)
            {
                float t = i / (float)sampleRate;
                data[i] = Mathf.Sin(2f * Mathf.PI * 880f * t) * Mathf.Exp(-t * 110f) * 0.4f;
            }
            AudioClip clip = AudioClip.Create("Phone_Click_SUPUESTO", samples, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
