using System;
using System.IO;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Vaelith.Docencia2;
using Object = UnityEngine.Object;

public static class LuminariasZona1Aula16
{
    public const string ScenePath = "Assets/Scenes/Exterior_Docencia2.unity";
    public const string BackupScenePath = "backup_luminarias_plafones_20261125/Exterior_Docencia2.unity";
    public const string AssetDir = "Assets/Models/Docencia2/Luminarias_Zona1_Aula16";
    public const string OutDir = "Luminarias_Zona1_Aula16";
    public const string PlafonRootName = "Plafon_Aula16_Zona1_SUPUESTO";
    public const string LightsRootName = "Luces_Aula16_Zona1_SUPUESTO";

    // Light Layer 3 = bit 3 (mask 8u), defined in ProjectSettings/TagManager.asset.
    public const uint Mask = 8u;
    public const int LayerPA = 30;

    //PA_Interior/Aula_16_Taller slab & clear interior bounds:
    // Clear interior faces: x = 0.3600 .. 9.3650 (dx = 9.0050 m), z = 30.9350 .. 38.4950 (dz = 7.5600 m), Y = 6.80 m.
    // Centered 60x60 grid: 16 cols (14 full 0.60 + 2 visible border cuts of 0.3025 m) x 13 rows (11 full 0.60 + 2 visible border cuts of 0.4800 m).
    const float X0 = 0.3600f, X1 = 9.4400f, Z0 = 30.8600f, Z1 = 38.5700f, Y = 6.8000f;

    static float BX(int i) => i == 0 ? X0 : i == 16 ? X1 : 0.6625f + (i - 1) * 0.60f;
    static float BZ(int i) => i == 0 ? Z0 : i == 13 ? Z1 : 31.4150f + (i - 1) * 0.60f;
    static float CX(int c) => (BX(c) + BX(c + 1)) * 0.5f;
    static float CZ(int r) => (BZ(r) + BZ(r + 1)) * 0.5f;

    public static Vector3 W(float x, float y, float z) => Docencia2InteriorBuilder.PlanToUnityPos(x, y, z);
    public static string P(Transform t) => t.parent ? P(t.parent) + "/" + t.name : t.name;

    struct LampSpec
    {
        public string id;
        public int c;
        public int r;
        public bool isLit;
        public bool isCool;
        public float intensity;
        public float range;
        public bool directionalNicheGuard;
        public string refs;
    }

    static readonly LampSpec[] Specs = new[]
    {
        new LampSpec { id = "A16_L01_Encendido",      c = 12, r = 1,  isLit = true,  isCool = false, intensity = 2.25f, range = 4.55f, directionalNicheGuard = true,  refs = "F08,F09,F10,F11,F14" },
        new LampSpec { id = "A16_L02_Encendido",      c = 2,  r = 1,  isLit = true,  isCool = false, intensity = 2.00f, range = 4.60f, directionalNicheGuard = false, refs = "F08,F09,F10,F11,F14" },
        new LampSpec { id = "A16_L03_Encendido",      c = 12, r = 5,  isLit = true,  isCool = false, intensity = 1.85f, range = 3.70f, directionalNicheGuard = false, refs = "F07" },
        new LampSpec { id = "A16_L04_Encendido",      c = 8,  r = 5,  isLit = true,  isCool = false, intensity = 1.45f, range = 4.45f, directionalNicheGuard = false, refs = "F07,F14" },
        new LampSpec { id = "A16_L05_Encendido",      c = 5,  r = 5,  isLit = true,  isCool = false, intensity = 1.45f, range = 4.45f, directionalNicheGuard = false, refs = "F08,F09,F14" },
        new LampSpec { id = "A16_L06_Encendido",      c = 2,  r = 5,  isLit = true,  isCool = false, intensity = 1.65f, range = 4.50f, directionalNicheGuard = false, refs = "F08,F09,F14" },
        new LampSpec { id = "A16_L07_Encendido_Frio", c = 12, r = 10, isLit = true,  isCool = true,  intensity = 2.10f, range = 4.65f, directionalNicheGuard = false, refs = "F07" },
        new LampSpec { id = "A16_L08_Apagado",        c = 5,  r = 1,  isLit = false, isCool = false, intensity = 0.00f, range = 0.00f, directionalNicheGuard = false, refs = "F08,F09" },
        new LampSpec { id = "A16_L09_Apagado",        c = 8,  r = 10, isLit = false, isCool = false, intensity = 0.00f, range = 0.00f, directionalNicheGuard = false, refs = "F07" },
        new LampSpec { id = "A16_L10_Apagado",        c = 5,  r = 10, isLit = false, isCool = false, intensity = 0.00f, range = 0.00f, directionalNicheGuard = false, refs = "F07,F14" },
    };

    static Transform aula, plafonRoot, lightsRoot, nightGroup, terrorGroup, oldLightsRoot, workshopFurniture, westForros;
    static MeshRenderer sharedNorthWallRenderer;
    static Docencia2ModeSwitcher switcher;
    static Light reusedNorthWest, reusedNorthEast;
    static GameObject[] obsoleteVisuals;
    static HashSet<Object> allowed = new HashSet<Object>();
    static Material matCeiling, matDark, matLitWarm, matLitCool, matOff, matTerror;
    static Mesh panelUvMesh;

    static IEnumerable<Transform> AllSceneTransforms() =>
        aula.gameObject.scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Transform>(true));

    static Transform MakeGroup(string name, Transform parent)
    {
        var go = new GameObject(name);
        go.layer = LayerPA;
        go.transform.SetParent(parent, false);
        return go.transform;
    }

    static void PermitObjectAndComponents(GameObject go)
    {
        allowed.Add(go);
        foreach (var c in go.GetComponents<Component>())
            if (c) allowed.Add(c);
    }

    static bool IsOuterSkirting(Renderer r)
    {
        if (r.name != "Zoclo_B") return false;
        string path = P(r.transform);
        return path.Contains("Aula_16_Taller/Este_P19/Zoclo_B") || path.Contains("Aula_16_Taller/Sur/Zoclo_B");
    }

    static List<Renderer> GetAuthorizedReceivers()
    {
        var list = new List<Renderer>();
        foreach (var r in aula.GetComponentsInChildren<Renderer>(true))
        {
            if (IsOuterSkirting(r)) continue;
            list.Add(r);
        }
        if (workshopFurniture)
            list.AddRange(workshopFurniture.GetComponentsInChildren<Renderer>(true));
        if (westForros)
        {
            foreach (var r in westForros.GetComponentsInChildren<Renderer>(true))
                if (r.name.Contains("Trasera3")) list.Add(r);
        }
        if (sharedNorthWallRenderer)
            list.Add(sharedNorthWallRenderer);
        return list.Distinct().ToList();
    }

    static Dictionary<string, string> Snapshot()
    {
        var dict = new Dictionary<string, string>();
        foreach (var t in AllSceneTransforms())
        {
            if (plafonRoot && (t == plafonRoot || t.IsChildOf(plafonRoot))) continue;
            if (lightsRoot && (t == lightsRoot || t.IsChildOf(lightsRoot))) continue;
            foreach (var o in new Object[] { t.gameObject }.Concat(t.GetComponents<Component>().Cast<Object>()))
            {
                if (!o || allowed.Contains(o)) continue;
                dict[GlobalObjectId.GetGlobalObjectIdSlow(o).ToString()] = EditorJsonUtility.ToJson(o);
            }
        }
        return dict;
    }

    static void CheckSnapshot(Dictionary<string, string> before, string tag)
    {
        var now = Snapshot();
        var diffs = before.Keys.Union(now.Keys)
            .Where(k => !before.ContainsKey(k) || !now.ContainsKey(k) || before[k] != now[k])
            .ToArray();
        File.WriteAllText(
            Path.Combine(OutDir, "auditoria_ambito_" + tag + ".txt"),
            $"Objetos/componentes protegidos fuera del ambito autorizado: {before.Count}\nDiferencias detectadas: {diffs.Length}\n" + string.Join("\n", diffs)
        );
        if (diffs.Length > 0)
            throw new Exception($"Se detectaron {diffs.Length} cambios fuera del ambito autorizado en '{tag}'. No se guarda.");
    }

    static Material CreateOrUpdateMaterial(string name, Material source, Color baseColor, float smoothness, Color? emission = null)
    {
        string path = AssetDir + "/" + name + ".mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (!m)
        {
            m = new Material(source);
            AssetDatabase.CreateAsset(m, path);
        }
        m.name = name;
        m.SetColor("_BaseColor", baseColor);
        m.SetColor("_Color", baseColor);
        m.SetTexture("_BaseMap", null);
        m.SetTexture("_MainTex", null);
        m.SetFloat("_Smoothness", smoothness);
        m.enableInstancing = true;
        m.globalIlluminationFlags = emission.HasValue ? MaterialGlobalIlluminationFlags.RealtimeEmissive : MaterialGlobalIlluminationFlags.None;
        if (emission.HasValue)
        {
            m.EnableKeyword("_EMISSION");
            m.SetColor("_EmissionColor", emission.Value);
        }
        else
        {
            m.DisableKeyword("_EMISSION");
            m.SetColor("_EmissionColor", Color.black);
        }
        EditorUtility.SetDirty(m);
        return m;
    }

    static Mesh PersistMesh(Mesh mesh, string name)
    {
        string path = AssetDir + "/" + name + ".asset";
        var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        mesh.name = name;
        if (existing)
        {
            EditorUtility.CopySerialized(mesh, existing);
            Object.DestroyImmediate(mesh);
            EditorUtility.SetDirty(existing);
            return existing;
        }
        AssetDatabase.CreateAsset(mesh, path);
        return mesh;
    }

    static GameObject MakeBox(string name, Transform parent, float x0, float x1, float y0, float y1, float z0, float z1, Material mat, bool usePanelUv = false)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        go.layer = LayerPA;
        go.transform.SetParent(parent, false);
        Object.DestroyImmediate(go.GetComponent<Collider>());
        Vector3 a = W(x0, y0, z0), b = W(x1, y1, z1);
        go.transform.position = (a + b) * 0.5f;
        go.transform.localScale = new Vector3(Mathf.Abs(a.x - b.x), y1 - y0, Mathf.Abs(a.z - b.z));
        if (usePanelUv)
            go.GetComponent<MeshFilter>().sharedMesh = panelUvMesh;
        var r = go.GetComponent<MeshRenderer>();
        r.sharedMaterial = mat;
        r.shadowCastingMode = ShadowCastingMode.Off;
        r.receiveShadows = true;
        r.renderingLayerMask = 17u | Mask;
        return go;
    }

    static void CombineStaticGroup(Transform parent, string combinedName, Func<MeshFilter, bool> filter)
    {
        var sources = parent.GetComponentsInChildren<MeshFilter>(true).Where(filter).ToArray();
        if (sources.Length == 0) return;
        var sharedMat = sources[0].GetComponent<Renderer>().sharedMaterial;
        if (sources.Any(s => s.GetComponent<Renderer>().sharedMaterial != sharedMat))
            throw new Exception("Intento de combinar mallas con materiales distintos en " + combinedName);

        var combined = new Mesh { indexFormat = IndexFormat.UInt32 };
        var instances = sources.Select(s => new CombineInstance
        {
            mesh = s.sharedMesh,
            transform = parent.worldToLocalMatrix * s.transform.localToWorldMatrix
        }).ToArray();
        combined.CombineMeshes(instances, true, true, false);
        combined.RecalculateBounds();

        var go = new GameObject(combinedName, typeof(MeshFilter), typeof(MeshRenderer));
        go.layer = LayerPA;
        go.isStatic = true;
        go.transform.SetParent(parent, false);
        go.GetComponent<MeshFilter>().sharedMesh = PersistMesh(combined, combinedName);
        var mr = go.GetComponent<MeshRenderer>();
        mr.sharedMaterial = sharedMat;
        mr.shadowCastingMode = ShadowCastingMode.Off;
        mr.receiveShadows = true;
        mr.renderingLayerMask = 17u | Mask;

        foreach (var s in sources)
            Object.DestroyImmediate(s.gameObject);
    }

    static void ConfigureAula16Light(Light l, LampSpec spec)
    {
        float cx = CX(spec.c), cz = CZ(spec.r);
        Vector3 pos = W(cx, 6.32f, cz);
        l.transform.position = pos;

        if (spec.directionalNicheGuard)
        {
            // Wide downward cone tilted slightly SW so x >= 9.515, z = 30.935 in corridor alcove is strictly outside the cone (0.000 lux).
            Vector3 target = W(cx - 0.52f, 6.32f - 0.84f, cz + 0.15f);
            l.transform.rotation = Quaternion.LookRotation(target - pos, Vector3.forward);
            l.type = LightType.Spot;
            l.spotAngle = 136f;
            l.innerSpotAngle = 82f;
        }
        else
        {
            l.transform.rotation = Quaternion.LookRotation(Vector3.down, Vector3.forward);
            l.type = LightType.Point;
        }

        l.range = spec.range;
        l.intensity = spec.intensity;
        l.color = spec.isCool ? new Color(0.90f, 0.96f, 1.00f) : new Color(1.00f, 0.94f, 0.84f);
        l.shadows = LightShadows.None;
        l.bounceIntensity = 0f;
        l.lightmapBakeType = LightmapBakeType.Realtime;
        l.cullingMask = 1 << LayerPA;
        l.renderMode = LightRenderMode.ForcePixel;
        l.enabled = true;

        var uld = l.GetComponent<UniversalAdditionalLightData>();
        if (!uld) uld = l.gameObject.AddComponent<UniversalAdditionalLightData>();
        uld.renderingLayers = Mask;
        uld.customShadowLayers = false;
    }

    public static void ApplyModeAndSync(Docencia2VisualMode mode)
    {
        var sw = Object.FindAnyObjectByType<Docencia2ModeSwitcher>();
        sw.ApplyMode(mode, false);
        foreach (var tr in Object.FindObjectsByType<Docencia2InteriorModeTracker>(FindObjectsInactive.Include))
            if (tr.enabled) tr.SendMessage("Update", SendMessageOptions.DontRequireReceiver);
    }

    public static void SetDoorP19Open(Transform aulaTransform, bool open)
    {
        var idoor = aulaTransform.GetComponentInChildren<Docencia2InteractiveDoor>(true);
        if (idoor) { idoor.SetOpen(open); return; }
        var padoor = aulaTransform.GetComponentInChildren<Docencia2PADoor>(true);
        if (padoor) padoor.SetOpen(open);
    }

    public static void RenderCameraToPng(Camera c, string outPath, int w, int h)
    {
        var rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGBHalf);
        rt.antiAliasing = 2;
        var prev = RenderTexture.active;
        var oldTarget = c.targetTexture;
        c.targetTexture = rt;
        var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
        try
        {
            for (int i = 0; i < 6; i++)
            {
                c.Render();
                RenderTexture.active = rt;
                tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
                tex.Apply();
            }
            File.WriteAllBytes(outPath, tex.EncodeToPNG());
        }
        finally
        {
            c.targetTexture = oldTarget;
            RenderTexture.active = prev;
            Object.DestroyImmediate(tex);
            rt.Release();
            Object.DestroyImmediate(rt);
        }
    }

    static void CaptureSuite(string prefix)
    {
        string sub = Path.Combine(OutDir, "capturas");
        Directory.CreateDirectory(sub);

        var originalMode = switcher.modoActual;
        var go = new GameObject("TEMP_Cam_Zona1_Aula16");
        var cam = go.AddComponent<Camera>();
        cam.enabled = false;
        cam.nearClipPlane = 0.04f;
        cam.farClipPlane = 100f;
        cam.allowHDR = true;
        cam.clearFlags = CameraClearFlags.SolidColor;
        var ucd = go.AddComponent<UniversalAdditionalCameraData>();
        ucd.renderPostProcessing = true;
        ucd.renderShadows = true;

        try
        {
            ApplyModeAndSync(Docencia2VisualMode.Noche);
            cam.backgroundColor = switcher.nightFogColor;

            void Shot(string name, float x, float y, float z, float tx, float ty, float tz, float fov, int w = 1440, int h = 1080)
            {
                cam.orthographic = false;
                cam.fieldOfView = fov;
                go.transform.position = W(x, y, z);
                go.transform.LookAt(W(tx, ty, tz));
                RenderCameraToPng(cam, Path.Combine(sub, prefix + "_" + name + ".png"), w, h);
            }

            // Photo-matched views (4:3 aspect matching 2191x1644 / 960x720 phone photos)
            Shot("F07_Vista_Sur",     8.65f, 5.15f, 32.55f, 5.20f, 5.15f, 38.00f, 72f);
            Shot("F09_Vista_Noroeste",9.12f, 5.15f, 32.42f, 1.50f, 5.20f, 31.85f, 74f);
            Shot("F14_Vista_General", 8.95f, 5.20f, 32.55f, 2.20f, 5.10f, 34.45f, 78f);

            // F11 looks from corridor through open P19; open temporarily and restore right after.
            SetDoorP19Open(aula, true);
            Shot("F11_Desde_Pasillo_P19", 11.15f, 5.10f, 32.35f, 3.50f, 5.12f, 32.20f, 62f);
            SetDoorP19Open(aula, false);

            // Wide 16:9 room & ceiling & closed-door corridor leak check
            Shot("05_General_SO_NOCHE", 1.15f, 5.25f, 37.80f, 7.20f, 5.25f, 32.40f, 78f, 1600, 1000);
            Shot("06_Plafon_Contrapicado_NOCHE", 4.85f, 4.05f, 36.60f, 4.85f, 6.80f, 33.80f, 88f, 1600, 1000);
            Shot("07_Pasillo_P19_Cerrada_NOCHE", 13.80f, 5.15f, 34.50f, 9.40f, 5.15f, 32.20f, 68f, 1600, 1000);

            // Editor TERROR preview
            ApplyModeAndSync(Docencia2VisualMode.Terror);
            cam.backgroundColor = switcher.terrorFogColor;
            Shot("05_General_SO_TERROR", 1.15f, 5.25f, 37.80f, 7.20f, 5.25f, 32.40f, 78f, 1600, 1000);
            Shot("F14_Vista_General_TERROR", 8.95f, 5.20f, 32.55f, 2.20f, 5.10f, 34.45f, 78f);
        }
        finally
        {
            SetDoorP19Open(aula, false);
            ApplyModeAndSync(originalMode);
            Object.DestroyImmediate(go);
        }
    }

    static double ComputePointOrSpotIrradiance(Light l, Vector3 point, Vector3 normal)
    {
        Vector3 toLight = l.transform.position - point;
        float d2 = toLight.sqrMagnitude;
        float r2 = l.range * l.range;
        if (d2 >= r2 || d2 < 1e-6f) return 0.0;
        float dist = Mathf.Sqrt(d2);
        Vector3 dir = toLight / dist;
        float ndotl = Vector3.Dot(normal, dir);
        if (ndotl <= 0f) return 0.0;

        float spotAtten = 1f;
        if (l.type == LightType.Spot)
        {
            float cosOuter = Mathf.Cos(l.spotAngle * 0.5f * Mathf.Deg2Rad);
            float cosTheta = Vector3.Dot(-dir, l.transform.forward);
            if (cosTheta <= cosOuter) return 0.0;
            float cosInner = Mathf.Cos(l.innerSpotAngle * 0.5f * Mathf.Deg2Rad);
            spotAtten = Mathf.Clamp01((cosTheta - cosOuter) / Mathf.Max(1e-4f, cosInner - cosOuter));
            spotAtten *= spotAtten;
        }

        float factor = d2 / r2;
        float rangeWin = Mathf.Max(0f, 1f - factor * factor);
        rangeWin *= rangeWin;
        return l.intensity * (rangeWin / Mathf.Max(0.01f, d2)) * spotAtten * ndotl;
    }

    static void AuditAndWriteReports()
    {
        var allAula16Lights = lightsRoot.GetComponentsInChildren<Light>(true)
            .Concat(new[] { reusedNorthEast, reusedNorthWest })
            .ToArray();

        if (allAula16Lights.Length != 7)
            throw new Exception($"Se esperaban exactamente 7 luces funcionales en NOCHE para Aula 16, hay {allAula16Lights.Length}.");

        if (terrorGroup.GetComponentsInChildren<Light>(true).Length != 0)
            throw new Exception("No debe haber excepciones de luces encendidas en TERROR dentro de Aula 16.");

        if (allAula16Lights.Any(l => l.shadows != LightShadows.None))
            throw new Exception("Ninguna luz de Aula 16 debe solicitar mapas de sombras adicionales.");

        if (plafonRoot.GetComponentsInChildren<Collider>(true).Length != 0)
            throw new Exception("El plafon modular no debe agregar colliders innecesarios.");

        var authorizedSet = new HashSet<Renderer>(GetAuthorizedReceivers());
        var foreignMaskPA = AllSceneTransforms()
            .SelectMany(t => t.GetComponents<Renderer>())
            .Where(r => r.gameObject.layer == LayerPA && (r.renderingLayerMask & Mask) != 0 && !authorizedSet.Contains(r))
            .ToArray();

        if (foreignMaskPA.Length != 0)
            throw new Exception("Existen renderers ajenos en PA con bit de mascara 8: " + string.Join(", ", foreignMaskPA.Select(r => P(r.transform))));

        // Mathematical leak verification at corridor alcove (x=9.515..11.20, z=30.935), Aula 17 (z=30.785), and Aula 15 (z=38.645):
        Vector3 nSouthInPlan = (W(5f, 5f, 32f) - W(5f, 5f, 31f)).normalized;
        Vector3 nNorthInPlan = -nSouthInPlan;
        double maxCorridorAlcove = 0.0, maxAula17 = 0.0, maxAula15 = 0.0;

        for (float x = 9.52f; x <= 11.20f; x += 0.15f)
            for (float y = 3.62f; y <= 6.78f; y += 0.25f)
                foreach (var l in allAula16Lights)
                    maxCorridorAlcove = Math.Max(maxCorridorAlcove, ComputePointOrSpotIrradiance(l, W(x, y, 30.935f), nSouthInPlan));

        for (float x = 0.45f; x <= 11.15f; x += 0.35f)
            for (float y = 3.62f; y <= 6.78f; y += 0.35f)
                foreach (var l in allAula16Lights)
                    maxAula17 = Math.Max(maxAula17, ComputePointOrSpotIrradiance(l, W(x, y, 30.785f), nNorthInPlan));

        for (float x = 0.45f; x <= 7.95f; x += 0.35f)
            for (float y = 3.62f; y <= 6.78f; y += 0.35f)
                foreach (var l in allAula16Lights)
                    maxAula15 = Math.Max(maxAula15, ComputePointOrSpotIrradiance(l, W(x, y, 38.645f), nSouthInPlan));

        if (maxCorridorAlcove > 1e-6 || maxAula17 > 1e-6 || maxAula15 > 1e-6)
            throw new Exception($"Fuga matematica detectada: pasillo={maxCorridorAlcove:E3}, aula17={maxAula17:E3}, aula15={maxAula15:E3}");

        var lightRows = new List<string>
        {
            "codigo|tipo_luz|objeto_luz|reutilizada|col_1based|fila_1based|x_plano|z_plano|x_unity|y_unity|z_unity|intensidad|alcance|spot_angle|rendering_mask|sombras|fotos"
        };
        for (int i = 0; i < Specs.Length; i++)
        {
            var s = Specs[i];
            if (!s.isLit)
            {
                Vector3 wpOff = W(CX(s.c), Y, CZ(s.r));
                lightRows.Add(string.Format(CultureInfo.InvariantCulture,
                    "{0}|SIN_LUZ_APAGADA|Difusor_Apagados_Combinados|False|{1}|{2}|{3:F4}|{4:F4}|{5:F4}|{6:F4}|{7:F4}|0.00|0.00|0|25|None|{8}",
                    s.id, s.c + 1, s.r + 1, CX(s.c), CZ(s.r), wpOff.x, wpOff.y, wpOff.z, s.refs));
                continue;
            }
            Light l = i == 0 ? reusedNorthEast : i == 1 ? reusedNorthWest : nightGroup.Find(s.id).GetComponentInChildren<Light>(true);
            Vector3 wp = l.transform.position;
            lightRows.Add(string.Format(CultureInfo.InvariantCulture,
                "{0}|{1}|{2}|{3}|{4}|{5}|{6:F4}|{7:F4}|{8:F4}|{9:F4}|{10:F4}|{11:F2}|{12:F2}|{13:F0}|{14}|None|{15}",
                s.id, l.type, P(l.transform), i < 2, s.c + 1, s.r + 1, CX(s.c), CZ(s.r), wp.x, wp.y, wp.z,
                l.intensity, l.range, l.type == LightType.Spot ? l.spotAngle : 0f, Mask, s.refs));
        }
        File.WriteAllLines(Path.Combine(OutDir, "Luces_Colocadas_Zona1_Aula16.csv"), lightRows);

        var summary = new List<string>
        {
            $"Fecha: {DateTime.Now:O}",
            $"Zona: PA_Interior/Aula_16_Taller (Y = {Y:F2} m)",
            $"Reticula 60x60 centrada: 16 columnas x 13 filas = 208 celdas (cortes visibles O/E = 0.3025 m, N/S = 0.4800 m; todos >= 0.20 m).",
            $"Paneles solidos 60x60 combinados: 198 -> 1 MeshRenderer Static (Plafon_Paneles_Estaticos_Combinados).",
            $"Respaldo continuo opaco: 1 MeshRenderer Static (Plafon_Respaldo_Continuo_Sin_Huecos).",
            $"Perfiles T de 18 mm combinados: 31 -> 1 MeshRenderer Static (Plafon_Perfiles_Estaticos_Combinados).",
            $"Carcasas de troffer combinadas: 10 -> 1 MeshRenderer Static (Plafon_Troffers_Cuerpos_Combinados).",
            $"Difusores apagados combinados: 3 -> 1 MeshRenderer Static (Difusor_Apagados_Combinados).",
            $"Difusores encendidos por modo: 7 en NOCHE + 7 en TERROR.",
            $"Piezas antiguas retiradas de Iluminacion_PA_Mismo_Criterio_PB: 6 (2 Marco_Aula_16_Taller, 2 Difusor_Aula_16_Taller, 2 Difusor_Terror_Aula_16_Taller).",
            $"Luces originales reutilizadas: 2 (Luz_PA_Aula_16_Taller_40 en L01 y Luz_PA_Aula_16_Taller_39 en L02).",
            $"Luces puntuales nuevas en NOCHE: 5 (L03..L07). Total luces funcionales NOCHE en Aula 16: 7. Total luces TERROR en Aula 16: 0.",
            $"Mapas de sombras adicionales solicitados: 0.",
            $"Rendering Layer Mask local: {Mask} (Light Layer 3 definida en TagManager.asset), cullingMask = 1<<30.",
            $"Renderers ajenos en PA con bit {Mask}: {foreignMaskPA.Length}.",
            $"Irradiancia directa maxima en nicho pasillo (x>=9.52, z=30.935): {maxCorridorAlcove:F6}.",
            $"Irradiancia directa maxima en cara norte Aula 17 (z=30.785): {maxAula17:F6}.",
            $"Irradiancia directa maxima en cara sur Aula 15 (z=38.645): {maxAula15:F6}.",
            $"Transform totales en Plafon_Aula16_Zona1_SUPUESTO: {plafonRoot.GetComponentsInChildren<Transform>(true).Length} ({plafonRoot.GetComponentsInChildren<Renderer>(true).Length} renderers).",
            $"Transform totales en Luces_Aula16_Zona1_SUPUESTO: {lightsRoot.GetComponentsInChildren<Transform>(true).Length} ({lightsRoot.GetComponentsInChildren<Renderer>(true).Length} renderers)."
        };
        File.WriteAllLines(Path.Combine(OutDir, "auditoria_zona1_aula16.txt"), summary);
    }

    static void RunExteriorRegression()
    {
        const string tempScene = "Assets/Scenes/TEMP_Zona1_Baseline_Check.unity";
        string extDir = Path.Combine(OutDir, "exterior");
        Directory.CreateDirectory(extDir);
        try
        {
            if (!File.Exists(BackupScenePath))
                throw new Exception("No se encontro respaldo base en " + BackupScenePath);
            File.Copy(BackupScenePath, tempScene, true);
            AssetDatabase.ImportAsset(tempScene);

            foreach (var entry in new[] { (tempScene, "Antes"), (ScenePath, "Actual") })
            {
                EditorSceneManager.OpenScene(entry.Item1, OpenSceneMode.Single);
                foreach (var mode in new[] { Docencia2VisualMode.Noche, Docencia2VisualMode.Terror })
                {
                    ApplyModeAndSync(mode);
                    var cams = GameObject.Find("Camaras_Referencia_Docencia2").GetComponentsInChildren<Camera>(true);
                    if (cams.Length != 3)
                        throw new Exception("Se esperaban las 3 camaras exteriores de referencia.");
                    foreach (var c in cams)
                        RenderCameraToPng(c, Path.Combine(extDir, $"{entry.Item2}_{mode}_{c.name}.png"), 1280, 720);
                }
            }
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        }
        finally
        {
            if (File.Exists(tempScene))
                AssetDatabase.DeleteAsset(tempScene);
        }
    }

    public static void Build()
    {
        try
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new Exception("Debe ejecutarse fuera de Play Mode.");

            Directory.CreateDirectory(OutDir);
            Directory.CreateDirectory(AssetDir);
            AssetDatabase.Refresh();

            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            aula = GameObject.Find("PA_Interior/Aula_16_Taller")?.transform;
            if (!aula) throw new Exception("No se encontro PA_Interior/Aula_16_Taller.");
            switcher = Object.FindAnyObjectByType<Docencia2ModeSwitcher>();
            if (!switcher) throw new Exception("No se encontro Docencia2ModeSwitcher.");

            // Capture ANTES views if building from original flat ceiling, then reload clean scene from disk
            if (aula.Find("Plafon_PA_Y6.80") != null)
            {
                CaptureSuite("Antes");
                scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
                aula = GameObject.Find("PA_Interior/Aula_16_Taller")?.transform;
                switcher = Object.FindAnyObjectByType<Docencia2ModeSwitcher>();
            }

            oldLightsRoot = GameObject.Find("PA_Interior/Iluminacion_PA_Mismo_Criterio_PB")?.transform;
            if (!oldLightsRoot) throw new Exception("No se encontro PA_Interior/Iluminacion_PA_Mismo_Criterio_PB.");

            workshopFurniture = GameObject.Find("PA_Interior/Detalles_Fotos_Revision4/Mobiliario_PA_SUPUESTO/Taller16_F07_F14")?.transform;
            westForros = GameObject.Find("PA_Interior/Forros_Interiores_Con_Vanos")?.transform;
            sharedNorthWallRenderer = GameObject.Find("PA_Interior/Aula_17/Sur/Muro")?.GetComponent<MeshRenderer>();
            if (!sharedNorthWallRenderer) throw new Exception("No se encontro PA_Interior/Aula_17/Sur/Muro.");

            var savedMode = switcher.modoActual;

            var rp = QualitySettings.renderPipeline as UniversalRenderPipelineAsset;
            if (!rp || !rp.useRenderingLayers)
                throw new Exception("URP Rendering Layers deben estar activas.");

            reusedNorthWest = oldLightsRoot.GetComponentsInChildren<Light>(true).FirstOrDefault(l => l.name == "Luz_PA_Aula_16_Taller_39");
            reusedNorthEast = oldLightsRoot.GetComponentsInChildren<Light>(true).FirstOrDefault(l => l.name == "Luz_PA_Aula_16_Taller_40");
            if (!reusedNorthWest || !reusedNorthEast)
                throw new Exception("No se encontraron Luz_PA_Aula_16_Taller_39 y 40 para reutilizar.");

            obsoleteVisuals = oldLightsRoot.GetComponentsInChildren<Transform>(true)
                .Where(t => t.name == "Marco_Aula_16_Taller" || t.name == "Difusor_Aula_16_Taller" || t.name == "Difusor_Terror_Aula_16_Taller")
                .Select(t => t.gameObject)
                .ToArray();

            var flatCeiling = aula.Find("Plafon_PA_Y6.80")?.gameObject;
            plafonRoot = aula.Find(PlafonRootName);
            lightsRoot = aula.Find(LightsRootName);

            if (!flatCeiling && !plafonRoot)
                throw new Exception("No se encontro Plafon_PA_Y6.80 ni Plafon_Aula16_Zona1_SUPUESTO.");
            if (flatCeiling && obsoleteVisuals.Length != 6)
                throw new Exception($"Se esperaban 6 piezas visuales antiguas de Aula_16_Taller, se hallaron {obsoleteVisuals.Length}.");

            // Define strict scope whitelist for Snapshot() verification
            allowed.Clear();
            allowed.Add(aula);
            if (flatCeiling) PermitObjectAndComponents(flatCeiling);
            foreach (var g in obsoleteVisuals)
            {
                PermitObjectAndComponents(g);
                if (g.transform.parent) allowed.Add(g.transform.parent);
            }
            PermitObjectAndComponents(reusedNorthWest.gameObject);
            PermitObjectAndComponents(reusedNorthEast.gameObject);
            foreach (var r in GetAuthorizedReceivers())
                allowed.Add(r);

            var before = Snapshot();

            // Load approved base materials & shared UV panel mesh
            matCeiling = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Docencia2/Interior/Mat_Int_Plafon_Reticular_PB.mat");
            matDark = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Docencia2/Mat_Int_Troffer_Marco_Negro.mat");
            if (!matCeiling || !matDark)
                throw new Exception("No se encontraron los materiales base aprobados del plafon.");

            panelUvMesh = AssetDatabase.LoadAssetAtPath<Mesh>("Assets/Models/Docencia2/Aula17_Fase2a/Panel_UV_Material_Aprobado.asset");
            if (!panelUvMesh)
            {
                var tmp = GameObject.CreatePrimitive(PrimitiveType.Cube);
                var m = Object.Instantiate(tmp.GetComponent<MeshFilter>().sharedMesh);
                Object.DestroyImmediate(tmp);
                m.uv = m.uv.Select(uv => new Vector2(0.025f + uv.x * 0.2f, 0.025f + uv.y * 0.2f)).ToArray();
                panelUvMesh = PersistMesh(m, "Panel_UV_Material_Aprobado_A16");
            }

            matLitWarm = CreateOrUpdateMaterial("A16_Difusor_Encendido_Calido", matCeiling, new Color(0.88f, 0.88f, 0.80f), 0.12f, new Color(2.20f, 2.05f, 1.65f));
            matLitCool = CreateOrUpdateMaterial("A16_Difusor_Encendido_Frio",   matCeiling, new Color(0.86f, 0.91f, 0.96f), 0.12f, new Color(1.90f, 2.15f, 2.38f));
            matOff     = CreateOrUpdateMaterial("A16_Difusor_Apagado",          matCeiling, new Color(0.76f, 0.75f, 0.70f), 0.24f, null);
            matTerror  = CreateOrUpdateMaterial("A16_Difusor_Terror",           matCeiling, new Color(0.30f, 0.33f, 0.34f), 0.14f, new Color(0.50f, 0.65f, 0.68f) * 0.025f);

            // Remove only replaced Aula 16 flat ceiling & Aula 16 old visual troffers
            if (flatCeiling) Object.DestroyImmediate(flatCeiling);
            if (plafonRoot) Object.DestroyImmediate(plafonRoot.gameObject);
            if (lightsRoot) Object.DestroyImmediate(lightsRoot.gameObject);
            foreach (var g in obsoleteVisuals)
                Object.DestroyImmediate(g);

            // Build Plafon_Aula16_Zona1_SUPUESTO & Luces_Aula16_Zona1_SUPUESTO
            plafonRoot = MakeGroup(PlafonRootName, aula);
            var panelsGroup = MakeGroup("Paneles_60x60", plafonRoot);
            var barsGroup   = MakeGroup("Reticula", plafonRoot);
            var lampsGroup  = MakeGroup("Troffers_7_encendidos_3_apagados", plafonRoot);

            lightsRoot  = MakeGroup(LightsRootName, aula);
            nightGroup  = MakeGroup("NOCHE", lightsRoot);
            terrorGroup = MakeGroup("TERROR", lightsRoot);

            var specByCell = Specs.ToDictionary(s => new Vector2Int(s.c, s.r), s => s);

            for (int c = 0; c < 16; c++)
            {
                for (int r = 0; r < 13; r++)
                {
                    float x0 = BX(c), x1 = BX(c + 1), z0 = BZ(r), z1 = BZ(r + 1);
                    if (specByCell.TryGetValue(new Vector2Int(c, r), out var spec))
                    {
                        MakeBox("Cuerpo_" + spec.id, lampsGroup, x0 + 0.008f, x1 - 0.008f, Y, Y + 0.025f, z0 + 0.008f, z1 - 0.008f, matDark, false);
                        if (spec.isLit)
                        {
                            var hn = MakeGroup(spec.id, nightGroup);
                            var ht = MakeGroup(spec.id, terrorGroup);
                            var dn = MakeBox("Difusor_NOCHE_" + spec.id, hn, x0 + 0.022f, x1 - 0.022f, Y - 0.002f, Y + 0.006f, z0 + 0.022f, z1 - 0.022f, spec.isCool ? matLitCool : matLitWarm, false);
                            var dt = MakeBox("Difusor_TERROR_" + spec.id, ht, x0 + 0.022f, x1 - 0.022f, Y - 0.002f, Y + 0.006f, z0 + 0.022f, z1 - 0.022f, matTerror, false);
                            dn.isStatic = false;
                            dt.isStatic = false;
                        }
                        else
                        {
                            MakeBox("Difusor_Apagado_" + spec.id, lampsGroup, x0 + 0.022f, x1 - 0.022f, Y - 0.002f, Y + 0.006f, z0 + 0.022f, z1 - 0.022f, matOff, false);
                        }
                    }
                    else
                    {
                        MakeBox($"Panel_C{c + 1:00}_F{r + 1:00}", panelsGroup, x0 + 0.005f, x1 - 0.005f, Y, Y + 0.025f, z0 + 0.005f, z1 - 0.005f, matCeiling, true);
                    }
                }
            }

            var backing = MakeBox("Plafon_Respaldo_Continuo_Sin_Huecos", panelsGroup, X0, X1, Y + 0.027f, Y + 0.037f, Z0, Z1, matCeiling, true);
            backing.isStatic = true;

            for (int c = 0; c <= 16; c++)
            {
                float x = BX(c);
                MakeBox("Perfil_X_" + c, barsGroup, Mathf.Max(X0, x - 0.009f), Mathf.Min(X1, x + 0.009f), Y - 0.004f, Y + 0.009f, Z0, Z1, matDark, false);
            }
            for (int r = 0; r <= 13; r++)
            {
                float z = BZ(r);
                MakeBox("Perfil_Z_" + r, barsGroup, X0, X1, Y - 0.004f, Y + 0.009f, Mathf.Max(Z0, z - 0.009f), Mathf.Min(Z1, z + 0.009f), matDark, false);
            }

            // Combine static ceiling geometry by material
            plafonRoot.gameObject.isStatic = true;
            panelsGroup.gameObject.isStatic = true;
            barsGroup.gameObject.isStatic = true;
            lampsGroup.gameObject.isStatic = true;

            CombineStaticGroup(panelsGroup, "Plafon_Paneles_Estaticos_Combinados", mf => mf.name.StartsWith("Panel_"));
            CombineStaticGroup(barsGroup,   "Plafon_Perfiles_Estaticos_Combinados", mf => true);
            CombineStaticGroup(lampsGroup,  "Plafon_Troffers_Cuerpos_Combinados", mf => mf.name.StartsWith("Cuerpo_"));
            CombineStaticGroup(lampsGroup,  "Difusor_Apagados_Combinados", mf => mf.name.StartsWith("Difusor_Apagado_"));

            // Configure the 7 functional NOCHE lights (2 reused + 5 new)
            for (int i = 0; i < Specs.Length; i++)
            {
                var spec = Specs[i];
                if (!spec.isLit) continue;
                Light l;
                if (i == 0) l = reusedNorthEast; // Luz_PA_Aula_16_Taller_40 -> A16_L01
                else if (i == 1) l = reusedNorthWest; // Luz_PA_Aula_16_Taller_39 -> A16_L02
                else
                {
                    var holder = nightGroup.Find(spec.id);
                    var lgo = MakeGroup("Luz_NOCHE_" + spec.id, holder).gameObject;
                    l = lgo.AddComponent<Light>();
                }
                ConfigureAula16Light(l, spec);
            }

            // Assign receiver bit Mask (8u) ONLY to authorized Aula 16 interior surfaces
            foreach (var r in GetAuthorizedReceivers())
                r.renderingLayerMask |= Mask;

            // Mode tracker linked to existing Docencia2ModeSwitcher (sync only our new tracker so saved scene state is untouched)
            var tracker = lightsRoot.gameObject.AddComponent<Docencia2InteriorModeTracker>();
            tracker.nightGroup = nightGroup.gameObject;
            tracker.terrorGroup = terrorGroup.gameObject;
            tracker.switcher = switcher;
            bool startTerror = savedMode == Docencia2VisualMode.Terror;
            nightGroup.gameObject.SetActive(!startTerror);
            terrorGroup.gameObject.SetActive(startTerror);

            CheckSnapshot(before, "post_build");
            AuditAndWriteReports();

            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            // Capture DESPUES views, then reopen saved scene cleanly from disk without saving temporary state
            CaptureSuite("Despues");
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            // Exterior regression check (3 reference cameras x 2 modes vs dated backup)
            RunExteriorRegression();

            File.WriteAllText(Path.Combine(OutDir, "build_completado.txt"), $"Zona 1 (PA_Interior/Aula_16_Taller) construida, auditada y guardada fuera de Play Mode.\n{DateTime.Now:O}\n");
            Debug.Log("ZONA1_AULA16_BUILD_OK");
            EditorApplication.Exit(0);
        }
        catch (Exception e)
        {
            Directory.CreateDirectory(OutDir);
            File.WriteAllText(Path.Combine(OutDir, "error_build.txt"), e.ToString());
            Debug.LogException(e);
            EditorApplication.Exit(1);
        }
    }

    public static void PlayVerify()
    {
        try
        {
            Directory.CreateDirectory(OutDir);
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            SessionState.SetBool("ZONA1_AULA16_PLAY_VERIFY", true);
            EditorApplication.isPlaying = true;
        }
        catch (Exception e)
        {
            Directory.CreateDirectory(OutDir);
            File.WriteAllText(Path.Combine(OutDir, "error_play.txt"), e.ToString());
            Debug.LogException(e);
            EditorApplication.Exit(1);
        }
    }
}

[InitializeOnLoad]
public static class LuminariasZona1Aula16PlayBoot
{
    static LuminariasZona1Aula16PlayBoot()
    {
        EditorApplication.update += OnEditorUpdate;
    }

    static void OnEditorUpdate()
    {
        if (SessionState.GetBool("ZONA1_AULA16_PLAY_VERIFY", false) && EditorApplication.isPlaying && !EditorApplication.isCompiling)
        {
            SessionState.SetBool("ZONA1_AULA16_PLAY_VERIFY", false);
            new GameObject("TEMP_Zona1_Aula16_PlayVerifier").AddComponent<LuminariasZona1Aula16Runtime>();
        }
    }
}

public class LuminariasZona1Aula16Runtime : MonoBehaviour
{
    readonly List<string> consoleIssues = new List<string>();

    void OnLog(string condition, string stackTrace, LogType type)
    {
        string low = condition.ToLowerInvariant();
        if (type == LogType.Error || type == LogType.Exception || (low.Contains("shadow") && low.Contains("atlas")))
            consoleIssues.Add($"{type}: {condition}");
    }

    IEnumerator Start()
    {
        Application.logMessageReceived += OnLog;
        Application.runInBackground = true;
        QualitySettings.vSyncCount = 0;
        Application.targetFrameRate = -1;

        yield return null;
        yield return null;

        string playDir = Path.Combine(LuminariasZona1Aula16.OutDir, "play");
        Directory.CreateDirectory(playDir);

        var player = Docencia2FirstPersonPlayer.Instance ?? Object.FindAnyObjectByType<Docencia2FirstPersonPlayer>();
        var switcher = Object.FindAnyObjectByType<Docencia2ModeSwitcher>();
        var aula = GameObject.Find("PA_Interior/Aula_16_Taller")?.transform;
        var oldLightsRoot = GameObject.Find("PA_Interior/Iluminacion_PA_Mismo_Criterio_PB")?.transform;

        if (!player || !switcher || !aula || !oldLightsRoot)
        {
            File.WriteAllText(Path.Combine(LuminariasZona1Aula16.OutDir, "error_play.txt"), "Objetos base ausentes en Play Mode.");
            EditorApplication.Exit(1);
            yield break;
        }

        var cc = player.GetComponent<CharacterController>();
        var cam = player.playerCamera;
        var rt = new RenderTexture(1280, 720, 24, RenderTextureFormat.ARGBHalf);
        cam.targetTexture = rt;
        player.enableBatteryDrain = false;
        player.showDebugHudAndShortcuts = true;

        var report = new List<string>
        {
            $"Jugador real: {LuminariasZona1Aula16.P(player.transform)} (height={cc.height:F2}, radius={cc.radius:F2}, stepOffset={cc.stepOffset:F2})",
            $"Tecla cambio de modo configurada en Docencia2ModeSwitcher: {switcher.toggleKey}"
        };

        Light[] GetAula16Lights() =>
            aula.GetComponentsInChildren<Light>(true)
                .Concat(oldLightsRoot.GetComponentsInChildren<Light>(true)
                    .Where(l => l.name == "Luz_PA_Aula_16_Taller_39" || l.name == "Luz_PA_Aula_16_Taller_40"))
                .ToArray();

        // 1. Test live mode switching (ToggleMode equivalent to T key)
        LuminariasZona1Aula16.ApplyModeAndSync(Docencia2VisualMode.Noche);
        for (int i = 0; i < 15; i++) yield return null;
        int activeNoche1 = GetAula16Lights().Count(l => l.enabled && l.gameObject.activeInHierarchy);
        switcher.ToggleMode();
        for (int i = 0; i < 15; i++) yield return null;
        int activeTerrorAfterToggle = GetAula16Lights().Count(l => l.enabled && l.gameObject.activeInHierarchy);
        switcher.ToggleMode();
        for (int i = 0; i < 15; i++) yield return null;
        int activeNocheAfterToggle = GetAula16Lights().Count(l => l.enabled && l.gameObject.activeInHierarchy);

        report.Add($"Prueba ToggleMode (tecla T): NOCHE={activeNoche1} luces activas -> TERROR={activeTerrorAfterToggle} luces activas -> NOCHE={activeNocheAfterToggle} luces activas.");

        // 2. CharacterController navigation through P19 into Aula 16 and back out
        var idoor = aula.GetComponentInChildren<Docencia2InteractiveDoor>(true);
        var padoor = aula.GetComponentInChildren<Docencia2PADoor>(true);

        player.TeleportToPlan(10.60f, 3.606f, 32.35f, 0f);
        player.enabled = false;
        if (idoor) idoor.SetOpenAnimated(true);
        else if (padoor) padoor.SetOpen(true);
        yield return new WaitForSeconds(0.6f);

        float[][] waypoints = new[]
        {
            new[] { 8.65f, 32.35f },
            new[] { 7.55f, 33.35f },
            new[] { 7.55f, 35.80f },
            new[] { 3.10f, 35.80f },
            new[] { 3.10f, 33.45f },
            new[] { 7.55f, 33.35f },
            new[] { 8.65f, 32.35f },
            new[] { 10.60f, 32.35f }
        };

        for (int i = 0; i < waypoints.Length; i++)
        {
            var wp = waypoints[i];
            Vector3 target = LuminariasZona1Aula16.W(wp[0], 3.626f, wp[1]);
            float elapsed = 0f;
            while (elapsed < 7.0f)
            {
                Vector3 delta = target - player.transform.position;
                delta.y = 0f;
                if (delta.magnitude < 0.05f) break;
                cc.Move(Vector3.ClampMagnitude(delta, 2.5f * Time.deltaTime) + Vector3.down * 2.0f * Time.deltaTime);
                elapsed += Time.deltaTime;
                yield return null;
            }
            Vector3 rem = target - player.transform.position;
            rem.y = 0f;
            bool ok = rem.magnitude < 0.09f && Mathf.Abs(player.transform.position.y - 3.636f) < 0.12f;
            report.Add(string.Format(CultureInfo.InvariantCulture,
                "Tramo {0}: destino ({1:F2},{2:F2}), error={3:F3}m, Y={4:F3} -> {5}",
                i + 1, wp[0], wp[1], rem.magnitude, player.transform.position.y, ok ? "PASS" : "REVISAR"));
        }

        if (idoor) idoor.SetOpenAnimated(false);
        else if (padoor) padoor.SetOpen(false);
        yield return new WaitForSeconds(0.6f);
        player.enabled = true;

        // 3. FPS & Play captures across 3 views in NOCHE and TERROR (>= 8 seconds per view at 1280x720)
        var fpsRows = new List<string>
        {
            "modo|vista|resolucion|muestras|fps_promedio|fps_p95|ms_max|luces_aula16_activas|linterna"
        };

        float[][] spots = new[]
        {
            new[] { 8.65f, 32.55f, 2.40f, 34.45f }, // Vista 1: Desde acceso P19 hacia taller y ventanas (tipo F14)
            new[] { 8.65f, 32.60f, 5.20f, 37.90f }, // Vista 2: Hacia sur y luminaria fria L07 (tipo F07)
            new[] { 1.35f, 37.40f, 7.40f, 32.40f }  // Vista 3: Contravista desde SO hacia puerta P19 y pizarron
        };

        foreach (var mode in new[] { Docencia2VisualMode.Noche, Docencia2VisualMode.Terror })
        {
            if (switcher.modoActual != mode) switcher.ToggleMode();
            for (int w = 0; w < 20; w++) yield return null;
            player.SetFlashlightState(mode == Docencia2VisualMode.Terror, false);

            for (int v = 0; v < spots.Length; v++)
            {
                var s = spots[v];
                player.TeleportToPlan(s[0], 3.606f, s[1], 0f);
                Vector3 fwd = LuminariasZona1Aula16.W(s[2], 0f, s[3]) - LuminariasZona1Aula16.W(s[0], 0f, s[1]);
                player.transform.rotation = Quaternion.LookRotation(fwd);
                cam.transform.localRotation = Quaternion.Euler(-8f, 0f, 0f); // slight upward pitch to include ceiling + furniture + floor

                for (int w = 0; w < 45; w++) yield return null;

                var dtSamples = new List<double>(2048);
                double last = Time.realtimeSinceStartupAsDouble;
                double start = last;
                for (int i = 0; i < 6000 && (i < 300 || last - start < 8.0); i++)
                {
                    yield return null;
                    double now = Time.realtimeSinceStartupAsDouble;
                    dtSamples.Add(now - last);
                    last = now;
                }

                dtSamples.Sort();
                double avgFps = dtSamples.Count / dtSamples.Sum();
                double p95Fps = 1.0 / dtSamples[(int)(dtSamples.Count * 0.95)];
                double maxMs = dtSamples.Last() * 1000.0;
                int activeCount = GetAula16Lights().Count(l => l.enabled && l.gameObject.activeInHierarchy);

                fpsRows.Add(string.Format(CultureInfo.InvariantCulture,
                    "{0}|Vista{1}|1280x720|{2}|{3:F2}|{4:F2}|{5:F2}|{6}|{7}",
                    mode, v + 1, dtSamples.Count, avgFps, p95Fps, maxMs, activeCount, player.flashlightOn));

                LuminariasZona1Aula16.RenderCameraToPng(cam, Path.Combine(playDir, $"Play_{mode}_Vista{v + 1}.png"), 1280, 720);
            }
        }

        // 4. Flashlight ON vs OFF check in Play TERROR inside Aula 16
        if (switcher.modoActual != Docencia2VisualMode.Terror) switcher.ToggleMode();
        player.TeleportToPlan(7.40f, 3.606f, 35.60f, 0f);
        player.transform.rotation = Quaternion.LookRotation(LuminariasZona1Aula16.W(9.30f, 0f, 35.60f) - LuminariasZona1Aula16.W(7.40f, 0f, 35.60f));
        cam.transform.localRotation = Quaternion.identity;

        foreach (bool on in new[] { false, true })
        {
            player.SetFlashlightState(on, false);
            for (int w = 0; w < 40; w++) yield return null;
            LuminariasZona1Aula16.RenderCameraToPng(cam, Path.Combine(playDir, $"Play_TERROR_Linterna_{(on ? "ON" : "OFF")}.png"), 1280, 720);
        }

        report.Add($"GPU={SystemInfo.graphicsDeviceName} | CPU={SystemInfo.processorType} | RAM={SystemInfo.systemMemorySize}MB | Unity={Application.unityVersion}");
        report.Add($"Errores/excepciones/advertencias shadow atlas en Play: {consoleIssues.Count}");

        File.WriteAllLines(Path.Combine(LuminariasZona1Aula16.OutDir, "recorrido_play.txt"), report);
        File.WriteAllLines(Path.Combine(LuminariasZona1Aula16.OutDir, "fps_play.csv"), fpsRows);
        File.WriteAllLines(Path.Combine(LuminariasZona1Aula16.OutDir, "consola_play.txt"), consoleIssues);

        cam.targetTexture = null;
        rt.Release();
        Object.Destroy(rt);
        Application.logMessageReceived -= OnLog;

        File.WriteAllText(Path.Combine(LuminariasZona1Aula16.OutDir, "play_completado.txt"), $"Verificacion en Play Mode completada sin guardar estado de runtime.\n{DateTime.Now:O}\n");
        EditorApplication.Exit(0);
    }
}
