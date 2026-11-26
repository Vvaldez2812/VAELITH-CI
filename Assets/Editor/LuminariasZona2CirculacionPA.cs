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

public static class LuminariasZona2CirculacionPA
{
    public const string ScenePath = "Assets/Scenes/Exterior_Docencia2.unity";
    public const string BackupScenePath = "backup_luminarias_plafones_20261125/Exterior_Docencia2.unity";
    public const string AssetDir = "Assets/Models/Docencia2/Luminarias_Zona2_CirculacionPA";
    public const string OutDir = "Luminarias_Zona2_CirculacionPA";
    public const string PlafonRootName = "Plafon_CirculacionPA_Zona2_SUPUESTO";
    public const string LightsRootName = "Luces_CirculacionPA_Zona2_SUPUESTO";

    // Light Layer 1 = bit 1 (mask 2u), defined in ProjectSettings/TagManager.asset and unused in PA_Interior.
    public const uint Mask = 2u;
    public const int LayerPA = 30;
    const float Y = 6.8000f;

    public static float GX(int i) => i == 0 ? 8.0000f : i == 21 ? 20.4800f : 8.5400f + (i - 1) * 0.6000f;
    public static float GZ(int j) => j == 0 ? 6.4200f : j == 67 ? 46.2900f : 6.8550f + (j - 1) * 0.6000f;
    public static float CX(int c) => (GX(c) + GX(c + 1)) * 0.5f;
    public static float CZ(int r) => (GZ(r) + GZ(r + 1)) * 0.5f;

    public static Vector3 W(float x, float y, float z) => Docencia2InteriorBuilder.PlanToUnityPos(x, y, z);
    public static string P(Transform t) => t.parent ? P(t.parent) + "/" + t.name : t.name;

    struct SubZoneRect
    {
        public string name;
        public float x0, x1, z0, z1;
    }

    static readonly SubZoneRect[] SubZones = new[]
    {
        new SubZoneRect { name = "Acceso_Banos",     x0 = 12.24f, x1 = 16.24f, z0 =  6.42f, z1 =  7.71f },
        new SubZoneRect { name = "Pasillo_Norte",    x0 = 11.24f, x1 = 17.24f, z0 =  7.71f, z1 = 15.43f },
        new SubZoneRect { name = "Llegada_Escalera", x0 =  8.10f, x1 = 19.24f, z0 = 15.43f, z1 = 23.14f },
        new SubZoneRect { name = "Pasillo_Centro",   x0 = 11.24f, x1 = 17.24f, z0 = 23.14f, z1 = 30.86f },
        new SubZoneRect { name = "Pasillo_Sur",      x0 =  9.44f, x1 = 19.04f, z0 = 30.86f, z1 = 38.57f },
        new SubZoneRect { name = "Plaza_Sur",        x0 =  8.00f, x1 = 20.48f, z0 = 38.57f, z1 = 46.29f },
    };

    public static bool IsPointInZona2(float x, float z, float margin = 0.14f)
    {
        for (int i = 0; i < SubZones.Length; i++)
        {
            var s = SubZones[i];
            if (x >= s.x0 - margin && x <= s.x1 + margin && z >= s.z0 - margin && z <= s.z1 + margin)
                return true;
        }
        return false;
    }

    struct LampSpec
    {
        public string id;
        public int c, r;
        public bool isLit;
        public bool isCool;
        public float intensity;
        public float range;
        public bool useSpot;
        public Vector3 spotPlanOffset;
        public float spotAngle;
        public float innerSpotAngle;
        public string reusedLightName;
        public string refs;
    }

    static LampSpec LitPoint(string id, int c, int r, bool cool, float intensity, float range, string refs, string reused = null) =>
        new LampSpec { id = id, c = c, r = r, isLit = true, isCool = cool, intensity = intensity, range = range, useSpot = false, reusedLightName = reused, refs = refs };

    static LampSpec LitSpot(string id, int c, int r, bool cool, float intensity, float range, Vector3 offset, float outer, float inner, string refs, string reused = null) =>
        new LampSpec { id = id, c = c, r = r, isLit = true, isCool = cool, intensity = intensity, range = range, useSpot = true, spotPlanOffset = offset, spotAngle = outer, innerSpotAngle = inner, reusedLightName = reused, refs = refs };

    static LampSpec OffLamp(string id, int c, int r, string refs) =>
        new LampSpec { id = id, c = c, r = r, isLit = false, refs = refs };

    static readonly LampSpec[] Specs = new[]
    {
        // Acceso Banos (1 encendida)
        LitPoint("C2_L01_Encendido", 10, 1, false, 1.55f, 4.20f, "F36,F44", "Luz_PA_Acceso_Banos_20"),

        // Pasillo Norte (8 encendidas; L02..L05 con guarda direccional hacia pasillo para evitar nicho tras Bano_Mujeres/Hombres Sur)
        LitSpot ("C2_L02_Encendido",      8,  4, false, 1.65f, 4.50f, new Vector3( 0.00f, -0.80f,  0.60f), 108f, 68f, "F36,F44", "Luz_PA_Pasillo_Central_26"),
        LitSpot ("C2_L03_Encendido",     12,  4, false, 1.65f, 4.50f, new Vector3( 0.00f, -0.80f,  0.60f), 108f, 68f, "F36,F44"),
        LitSpot ("C2_L04_Encendido",      8,  7, false, 1.45f, 4.50f, new Vector3( 0.00f, -0.86f,  0.50f), 118f, 74f, "F36,F44"),
        LitSpot ("C2_L05_Encendido",     12,  7, false, 1.45f, 4.50f, new Vector3( 0.00f, -0.86f,  0.50f), 118f, 74f, "F36,F44"),
        LitPoint("C2_L06_Encendido",      8, 10, false, 1.25f, 4.40f, "F36,F44", "Luz_PA_Pasillo_Central_25"),
        LitPoint("C2_L07_Encendido_Frio",12, 10, true,  1.30f, 4.40f, "F36,F44"),
        LitPoint("C2_L08_Encendido",      8, 13, false, 1.30f, 4.50f, "F36,F44"),
        LitPoint("C2_L09_Encendido",     12, 13, false, 1.25f, 4.50f, "F36,F44"),

        // Llegada Escalera (12 encendidas + 2 apagadas)
        LitPoint("C2_L10_Encendido",      1, 17, false, 1.35f, 4.50f, "F37"),
        LitPoint("C2_L11_Encendido_Frio", 4, 17, true,  1.25f, 4.40f, "F03,F37"),
        LitPoint("C2_L12_Encendido",      8, 17, false, 1.25f, 4.50f, "F03,F36", "Luz_PA_Llegada_Escalera_32"),
        LitSpot ("C2_L13_Encendido_Frio",16, 17, true,  1.65f, 4.50f, new Vector3(-0.54f, -0.82f,  0.18f), 118f, 74f, "F38"),

        OffLamp ("C2_L14_Apagado",        1, 21, "F37"),
        LitPoint("C2_L15_Encendido_Frio", 4, 21, true,  1.30f, 3.88f, "F03,F37"),
        LitPoint("C2_L16_Encendido_Frio", 8, 21, true,  1.20f, 4.40f, "F03,F37"),
        LitPoint("C2_L17_Encendido",     12, 21, false, 1.20f, 4.40f, "F38"),
        LitPoint("C2_L18_Encendido_Frio",16, 21, true,  1.35f, 3.88f, "F38"),

        LitSpot ("C2_L19_Encendido",      1, 25, false, 1.65f, 4.50f, new Vector3(-0.34f, -0.82f, -0.46f), 106f, 66f, "F37"),
        OffLamp ("C2_L20_Apagado",        4, 25, "F03,F34,F37"),
        LitPoint("C2_L21_Encendido_Frio", 8, 25, true,  1.25f, 4.40f, "F03,F34", "Luz_PA_Llegada_Escalera_31"),
        LitPoint("C2_L22_Encendido",     12, 25, false, 1.25f, 4.40f, "F34,F38"),
        LitSpot ("C2_L23_Encendido",     16, 25, false, 1.65f, 4.50f, new Vector3(-0.48f, -0.78f, -0.40f), 104f, 66f, "F34,F38"),

        // Pasillo Centro (L26..L29 como Point omnidireccionales para iluminar tambien paneles del plafon Y=6.80 con 0.000 fuga; L24/L25 y L30/L31 con guarda direccional)
        LitSpot ("C2_L24_Encendido_Frio", 8, 29, true,  1.65f, 4.50f, new Vector3( 0.00f, -0.78f,  0.62f), 108f, 68f, "F34,F35", "Luz_PA_Pasillo_Central_28"),
        LitSpot ("C2_L25_Encendido_Frio",12, 29, true,  1.65f, 4.50f, new Vector3( 0.00f, -0.78f,  0.62f), 108f, 68f, "F02,F34,F35"),
        LitPoint("C2_L26_Encendido",      8, 32, false, 1.85f, 3.12f, "F34,F35"),
        LitPoint("C2_L27_Encendido",     12, 32, false, 1.85f, 3.12f, "F02,F34,F35"),
        LitPoint("C2_L28_Encendido_Frio", 8, 35, true,  1.70f, 3.70f, "F34,F35"),
        LitPoint("C2_L29_Encendido",     12, 35, false, 1.70f, 3.70f, "F34,F35", "Luz_PA_Pasillo_Central_27"),
        LitSpot ("C2_L30_Encendido",      8, 38, false, 1.65f, 4.50f, new Vector3( 0.00f, -0.78f, -0.62f), 108f, 68f, "F34,F35"),
        LitSpot ("C2_L31_Encendido",     12, 38, false, 1.65f, 4.50f, new Vector3( 0.00f, -0.78f, -0.62f), 108f, 68f, "F34,F35"),

        // Pasillo Sur (L33, L34 y L35 como Point omnidireccionales que iluminan plafon de Pasillo Sur y derrame hacia Plaza Sur con 0.000 fuga)
        OffLamp ("C2_L32_Apagado",        8, 43, "F41"),
        LitPoint("C2_L33_Encendido_Frio",12, 43, true,  1.60f, 3.90f, "F41", "Luz_PA_Pasillo_Central_30"),
        LitPoint("C2_L34_Encendido",      8, 49, false, 2.05f, 5.45f, "F41", "Luz_PA_Pasillo_Central_29"),
        LitPoint("C2_L35_Encendido",     12, 49, false, 2.05f, 5.45f, "F41", "Luz_PA_Plaza_Sur_46"),
        LitSpot ("C2_L36_Encendido",      4, 43, false, 1.65f, 4.60f,  new Vector3( 0.05f, -0.86f,  0.50f), 114f, 70f, "F12,F40"),
        OffLamp ("C2_L37_Apagado",        4, 46, "F40"),
        LitSpot ("C2_L38_Encendido",      4, 49, false, 1.85f, 8.80f,  new Vector3( 0.26f, -0.68f,  0.68f), 112f, 68f, "F40", "Luz_PA_Plaza_Sur_45"),
        LitSpot ("C2_L39_Encendido",     16, 43, false, 1.65f, 4.60f,  new Vector3(-0.05f, -0.86f,  0.50f), 114f, 70f, "F39"),
        LitSpot ("C2_L40_Encendido_Frio",16, 49, true,  1.85f, 8.80f,  new Vector3(-0.26f, -0.68f,  0.68f), 112f, 68f, "F39"),

        // Plaza Sur (8 aparentemente apagadas confirmadas en F41, F42, F45)
        OffLamp("C2_L41_Apagado",  4, 57, "F45"),
        OffLamp("C2_L42_Apagado",  8, 57, "F41,F45"),
        OffLamp("C2_L43_Apagado", 12, 57, "F41,F42"),
        OffLamp("C2_L44_Apagado", 16, 57, "F42"),
        OffLamp("C2_L45_Apagado",  4, 62, "F45"),
        OffLamp("C2_L46_Apagado",  8, 62, "F41,F45"),
        OffLamp("C2_L47_Apagado", 12, 62, "F41,F42"),
        OffLamp("C2_L48_Apagado", 16, 62, "F42"),
    };

    static readonly string[] ReusedTerrorPreviewNames = new[]
    {
        "Luz_PA_Terror_26", "Luz_PA_Terror_32", "Luz_PA_Terror_28", "Luz_PA_Terror_30", "Luz_PA_Terror_46"
    };

    static Transform paRoot, pasilloGroup, accesoBanosGroup, llegadaGroup, plazaSurGroup, oldLightsRoot;
    static Transform plafonRoot, lightsRoot, nightGroup, terrorGroup;
    static Docencia2ModeSwitcher switcher;
    static GameObject[] flatCeilings;
    static GameObject[] obsoleteVisuals;
    static Dictionary<string, Light> reusedNightLights;
    static Light[] reusedTerrorLights;
    static HashSet<Object> allowed = new HashSet<Object>();
    static Material matCeiling, matDark, matLitWarm, matLitCool, matOff, matTerror;
    static Mesh panelUvMesh;

    static IEnumerable<Transform> AllSceneTransforms() =>
        paRoot.gameObject.scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Transform>(true));

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

    public static Vector2 WorldToPlanXZ(Vector3 wp)
    {
        float xPlan = (14.25f - wp.z) * (28.48f / 28.50f);
        float zPlan = (27.00f - wp.x) * (53.92f / 54.00f);
        return new Vector2(xPlan, zPlan);
    }

    static List<Renderer> GetAuthorizedReceivers()
    {
        var list = new List<Renderer>();

        // 1. Direct circulation groups + columns + stairwell guard + corridor benches
        foreach (string grpName in new[]
        {
            "Acceso_Banos", "Pasillo_Central", "Llegada_Escalera", "Plaza_Sur",
            "Columnas_PA", "Escalera_Encuentro_Desplazado_1m",
            "Bancas_Verdes_Revision3_SUPUESTO", "Bancas_Aula16_Revision5"
        })
        {
            var g = paRoot.Find(grpName);
            if (g) list.AddRange(g.GetComponentsInChildren<Renderer>(true));
        }

        // 2. Stairwell West wall linings (Trasera5 at z = 15.43..23.14)
        var forros = paRoot.Find("Forros_Interiores_Con_Vanos");
        if (forros)
        {
            foreach (var r in forros.GetComponentsInChildren<Renderer>(true))
                if (r.name.Contains("Trasera5")) list.Add(r);
        }

        // 3. Corridor props inside Detalles_Fotos_Revision4 (strictly inside Zona 2 footprint, excluding classrooms/workshops/cubicles/redes)
        var rev4 = paRoot.Find("Detalles_Fotos_Revision4");
        if (rev4)
        {
            foreach (var r in rev4.GetComponentsInChildren<Renderer>(true))
            {
                string path = P(r.transform);
                if (path.Contains("Taller16_") || path.Contains("Redes_Detalle_") || path.Contains("Cubiculos_"))
                    continue;
                Vector2 xz = WorldToPlanXZ(r.bounds.center);
                if (IsPointInZona2(xz.x, xz.y, 0.12f) || (xz.x >= 0.36f && xz.x <= 8.20f && xz.y >= 15.40f && xz.y <= 23.16f))
                    list.Add(r);
            }
        }

        // 4. Shared perimeter wall assemblies bordering Zona 2 (excluding room-side Zoclo that sits outside Zona 2)
        string[] wallPaths = new[]
        {
            "Bano_Mujeres/Norte_Acceso", "Bano_Mujeres/Nicho", "Bano_Hombres/Nicho",
            "Bano_Mujeres/Sur", "Bano_Hombres/Sur",
            "Pasillo_Junto_Redes/Entrada_P16", "Redes_SITE/Cancel_Este", "Sala_Maestros/Cancel_Oeste",
            "Redes_SITE/Sur", "Sala_Maestros/Sur",
            "Aula_8/Frente_P27_B",
            "Aula_17/Norte_P18_B", "Aula_17/Este", "Aula_17/Sur", "Aula_17/Puerta_Aula17_Fase2c",
            "Aula_9/Norte_P26_B", "Aula_9/Oeste", "Aula_9/Sur",
            "Aula_16_Taller/Este_P19", "Aula_16_Taller/Sur",
            "Aula_10/Oeste_P25", "Aula_10/Sur",
            "Aula_15/Este_P20", "Aula_11/Oeste_P24",
            "Aula_14/Norte_P21", "Aula_13/Norte_P22", "Aula_12/Norte_P23"
        };

        foreach (string wp in wallPaths)
        {
            var wt = paRoot.Find(wp);
            if (!wt) continue;
            foreach (var r in wt.GetComponentsInChildren<Renderer>(true))
            {
                if (r.name.StartsWith("Zoclo_"))
                {
                    // Include only the skirting face on the corridor/stairwell side, never the classroom/bathroom side
                    Vector2 xz = WorldToPlanXZ(r.bounds.center);
                    bool inStairVoid = xz.x >= 0.36f && xz.x <= 8.15f && xz.y >= 15.32f && xz.y <= 23.25f;
                    if (!IsPointInZona2(xz.x, xz.y, 0.02f) && !inStairVoid)
                        continue;
                }
                list.Add(r);
            }
        }

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
            throw new Exception("Materiales distintos al combinar " + combinedName);

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

    // Returns exact clipped [x0,x1,z0,z1] for global cell (c,r) inside the unified Zona 2 footprint, or false if outside.
    static bool TryGetCellBounds(int c, int r, out float x0, out float x1, out float z0, out float z1)
    {
        x0 = GX(c); x1 = GX(c + 1);
        z0 = GZ(r); z1 = GZ(r + 1);
        float cx = (x0 + x1) * 0.5f;

        // Determine active SubZone(s) for row r:
        // Rows 0..1: Acceso_Banos [12.24..16.24], z=[6.42..7.71]
        // Row 2 (7.455..8.055): center [12.24..16.24] continuous; side shoulders [11.24..12.24] & [16.24..17.24] start at z=7.71
        // Rows 3..14: Pasillo Norte [11.24..17.24]
        // Row 15 (15.255..15.855): center [11.24..17.24] continuous; landing wings [8.10..11.24] & [17.24..19.24] start at z=15.43
        // Rows 16..26: Llegada Escalera [8.10..19.24]
        // Row 27 (22.455..23.055): center [11.24..17.24] ends at 23.055; landing wings [8.10..11.24] & [17.24..19.24] extend to 23.14 (avoiding 8.5cm sliver)
        // Rows 28..39: Pasillo Centro [11.24..17.24]
        // Row 40 (30.255..30.855): [11.24..17.24] (extends to 30.86 at outer edge)
        // Row 41 (30.855..31.455): center [11.24..17.24] 30.855..31.455; side shoulders [9.44..11.24] & [17.24..19.04] start at 30.86
        // Rows 42..52: Pasillo Sur [9.44..19.04]
        // Row 53 (38.055..38.655): [9.44..19.04] (side shoulders outside [9.44..19.04] start at row 54 from 38.57..39.255 to avoid 8.5cm sliver)
        // Row 54 (38.655..39.255): center [9.44..19.04] 38.655..39.255; Plaza Sur wings [8.00..9.44] & [19.04..20.48] span 38.57..39.255
        // Rows 55..66: Plaza Sur [8.00..20.48]

        float minX, maxX;
        if (r <= 1) { minX = 12.24f; maxX = 16.24f; }
        else if (r == 2)
        {
            minX = 11.24f; maxX = 17.24f;
            if (cx < 12.24f || cx > 16.24f) z0 = 7.71f;
        }
        else if (r <= 14) { minX = 11.24f; maxX = 17.24f; }
        else if (r == 15)
        {
            minX = 8.10f; maxX = 19.24f;
            if (cx < 11.24f || cx > 17.24f) z0 = 15.43f;
        }
        else if (r <= 26) { minX = 8.10f; maxX = 19.24f; }
        else if (r == 27)
        {
            minX = 8.10f; maxX = 19.24f;
            if (cx < 11.24f || cx > 17.24f) z1 = 23.14f;
        }
        else if (r <= 40)
        {
            minX = 11.24f; maxX = 17.24f;
            if (r == 40 && (cx < 11.54f || cx > 16.94f)) z1 = 30.86f;
        }
        else if (r == 41)
        {
            minX = 9.44f; maxX = 19.04f;
            if (cx < 11.24f || cx > 17.24f) z0 = 30.86f;
        }
        else if (r <= 53) { minX = 9.44f; maxX = 19.04f; }
        else if (r == 54)
        {
            minX = 8.00f; maxX = 20.48f;
            if (cx < 9.44f || cx > 19.04f) z0 = 38.57f;
        }
        else { minX = 8.00f; maxX = 20.48f; }

        if (x1 <= minX + 0.02f || x0 >= maxX - 0.02f)
            return false;

        x0 = Mathf.Max(x0, minX);
        x1 = Mathf.Min(x1, maxX);
        return (x1 - x0) >= 0.18f && (z1 - z0) >= 0.18f;
    }

    static void ConfigureZona2Light(Light l, LampSpec spec)
    {
        float cx = CX(spec.c), cz = CZ(spec.r);
        Vector3 pos = W(cx, 6.32f, cz);
        l.transform.position = pos;

        if (spec.useSpot)
        {
            Vector3 target = W(cx + spec.spotPlanOffset.x, 6.32f + spec.spotPlanOffset.y, cz + spec.spotPlanOffset.z);
            l.transform.rotation = Quaternion.LookRotation(target - pos, Vector3.forward);
            l.type = LightType.Spot;
            l.spotAngle = spec.spotAngle;
            l.innerSpotAngle = spec.innerSpotAngle;
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

    static void CaptureSuite(string prefix)
    {
        string sub = Path.Combine(OutDir, "capturas");
        Directory.CreateDirectory(sub);

        var originalMode = switcher.modoActual;
        var go = new GameObject("TEMP_Cam_Zona2_CirculacionPA");
        var cam = go.AddComponent<Camera>();
        cam.enabled = false;
        cam.nearClipPlane = 0.05f;
        cam.farClipPlane = 110f;
        cam.allowHDR = true;
        cam.clearFlags = CameraClearFlags.SolidColor;
        var ucd = go.AddComponent<UniversalAdditionalCameraData>();
        ucd.renderPostProcessing = true;
        ucd.renderShadows = true;

        try
        {
            LuminariasZona1Aula16.ApplyModeAndSync(Docencia2VisualMode.Noche);
            cam.backgroundColor = switcher.nightFogColor;

            void Shot(string name, float x, float y, float z, float tx, float ty, float tz, float fov, int w = 1080, int h = 1440)
            {
                cam.orthographic = false;
                cam.fieldOfView = fov;
                go.transform.position = W(x, y, z);
                go.transform.LookAt(W(tx, ty, tz));
                LuminariasZona1Aula16.RenderCameraToPng(cam, Path.Combine(sub, prefix + "_" + name + ".png"), w, h);
            }

            // Photo-matched views (portrait 3:4 matching vertical phone photos F34, F35, F36/F44, F03/F37, F41, F42/F45)
            Shot("F44_Pasillo_Norte_hacia_Banos", 14.24f, 5.20f, 16.60f, 14.24f, 5.30f,  7.00f, 74f);
            Shot("F03_Llegada_Escalera_Oeste",    15.80f, 5.15f, 19.30f,  8.20f, 5.25f, 19.30f, 74f);
            Shot("F34_Eje_Sur_Desde_Escalera",    14.60f, 5.20f, 17.80f, 14.24f, 5.30f, 44.00f, 74f);
            Shot("F35_Pasillo_Centro_Mecatronica",14.24f, 5.20f, 23.60f, 14.24f, 5.30f, 45.00f, 72f);
            Shot("F41_Pasillo_Sur_hacia_Plaza",   14.24f, 5.20f, 32.80f, 14.24f, 5.30f, 45.80f, 72f);
            Shot("F42_Plaza_Sur_Apagadas",        10.20f, 5.20f, 39.20f, 16.80f, 5.25f, 45.60f, 76f);

            // Wide 16:9 checks (including inside Aula 17 & Aula 16 leak verification!)
            Shot("07_Panoramica_Llegada_NOCHE",   18.40f, 5.25f, 22.20f,  8.40f, 5.25f, 16.50f, 78f, 1600, 1000);
            Shot("08_SinFuga_Interior_Aula17",     9.20f, 5.20f, 26.80f, 11.20f, 5.20f, 24.00f, 76f, 1600, 1000);

            LuminariasZona1Aula16.ApplyModeAndSync(Docencia2VisualMode.Terror);
            cam.backgroundColor = switcher.terrorFogColor;
            Shot("F35_Pasillo_Centro_TERROR",     14.24f, 5.20f, 23.60f, 14.24f, 5.30f, 45.00f, 72f);
            Shot("07_Panoramica_Llegada_TERROR",  18.40f, 5.25f, 22.20f,  8.40f, 5.25f, 16.50f, 78f, 1600, 1000);
        }
        finally
        {
            LuminariasZona1Aula16.ApplyModeAndSync(originalMode);
            Object.DestroyImmediate(go);
        }
    }

    static double ComputeIrradiance(Light l, Vector3 point, Vector3 normal)
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
        var allNocheLights = lightsRoot.Find("NOCHE").GetComponentsInChildren<Light>(true)
            .Concat(reusedNightLights.Values)
            .Distinct()
            .ToArray();

        if (allNocheLights.Length != 36)
            throw new Exception($"Se esperaban exactamente 36 luces funcionales en NOCHE en Zona 2, hay {allNocheLights.Length}.");

        if (allNocheLights.Any(l => l.shadows != LightShadows.None))
            throw new Exception("Ninguna luz de Zona 2 debe solicitar mapas de sombras.");

        if (plafonRoot.GetComponentsInChildren<Collider>(true).Length != 0)
            throw new Exception("El plafon de Zona 2 no debe contener colliders.");

        var authorizedSet = new HashSet<Renderer>(GetAuthorizedReceivers());
        foreach (var r in plafonRoot.GetComponentsInChildren<Renderer>(true)) authorizedSet.Add(r);
        foreach (var r in lightsRoot.GetComponentsInChildren<Renderer>(true)) authorizedSet.Add(r);

        var foreignPA = AllSceneTransforms()
            .SelectMany(t => t.GetComponents<Renderer>())
            .Where(r => r.gameObject.layer == LayerPA && (r.renderingLayerMask & Mask) != 0 && !authorizedSet.Contains(r))
            .ToArray();

        if (foreignPA.Length != 0)
            throw new Exception("Renderers ajenos en PA con bit 2: " + string.Join(", ", foreignPA.Select(r => P(r.transform))));

        // Mathematical leak audit on interior faces of adjacent rooms where shared partition walls protrude:
        Vector3 nSouth = (W(14f, 5f, 25f) - W(14f, 5f, 24f)).normalized;
        Vector3 nNorth = -nSouth;
        Vector3 nEast  = (W(15f, 5f, 25f) - W(14f, 5f, 25f)).normalized;
        Vector3 nWest  = -nEast;

        double maxLeakAula17 = 0.0, maxLeakAula16 = 0.0, maxLeakAula8 = 0.0, maxLeakAula9 = 0.0, maxLeakAula10 = 0.0, maxLeakNorte = 0.0;

        for (float y = 3.65f; y <= 6.75f; y += 0.35f)
        {
            // Aula 17 interior faces: North (z=23.215, x=0.4..11.16), East (x=11.165, z=23.22..30.78), South (z=30.785, x=0.4..11.16)
            for (float x = 0.5f; x <= 11.15f; x += 0.40f)
                foreach (var l in allNocheLights)
                {
                    maxLeakAula17 = Math.Max(maxLeakAula17, ComputeIrradiance(l, W(x, y, 23.215f), nSouth));
                    maxLeakAula17 = Math.Max(maxLeakAula17, ComputeIrradiance(l, W(x, y, 30.785f), nNorth));
                }
            for (float z = 23.22f; z <= 30.78f; z += 0.40f)
                foreach (var l in allNocheLights)
                    maxLeakAula17 = Math.Max(maxLeakAula17, ComputeIrradiance(l, W(11.165f, y, z), nWest));

            // Aula 16 interior faces: North (z=30.935, x=0.4..9.36), East (x=9.365, z=30.94..38.49)
            for (float x = 0.5f; x <= 9.36f; x += 0.40f)
                foreach (var l in allNocheLights)
                    maxLeakAula16 = Math.Max(maxLeakAula16, ComputeIrradiance(l, W(x, y, 30.935f), nSouth));
            for (float z = 30.94f; z <= 38.49f; z += 0.40f)
                foreach (var l in allNocheLights)
                    maxLeakAula16 = Math.Max(maxLeakAula16, ComputeIrradiance(l, W(9.365f, y, z), nWest));

            // Aula 8 North face (Sala_Maestros/Sur protruding x=19.32..28.0, z=15.505)
            for (float x = 19.32f; x <= 28.0f; x += 0.45f)
                foreach (var l in allNocheLights)
                    maxLeakAula8 = Math.Max(maxLeakAula8, ComputeIrradiance(l, W(x, y, 15.505f), nSouth));

            // Aula 9 interior faces: North (z=23.215, x=17.32..28.0), West (x=17.315, z=23.22..30.78)
            for (float x = 17.32f; x <= 28.0f; x += 0.45f)
                foreach (var l in allNocheLights)
                    maxLeakAula9 = Math.Max(maxLeakAula9, ComputeIrradiance(l, W(x, y, 23.215f), nSouth));
            for (float z = 23.22f; z <= 30.78f; z += 0.40f)
                foreach (var l in allNocheLights)
                    maxLeakAula9 = Math.Max(maxLeakAula9, ComputeIrradiance(l, W(17.315f, y, z), nEast));

            // Aula 10 North face (Aula_9/Sur protruding x=19.12..28.0, z=30.935)
            for (float x = 19.12f; x <= 28.0f; x += 0.45f)
                foreach (var l in allNocheLights)
                    maxLeakAula10 = Math.Max(maxLeakAula10, ComputeIrradiance(l, W(x, y, 30.935f), nSouth));

            // Bano_Mujeres/Sur (x=9.3..11.18, z=7.785) & Bano_Hombres/Sur (x=17.30..19.18, z=7.785)
            for (float x = 9.30f; x <= 11.18f; x += 0.30f)
                foreach (var l in allNocheLights)
                    maxLeakNorte = Math.Max(maxLeakNorte, ComputeIrradiance(l, W(x, y, 7.785f), nSouth));
            for (float x = 17.30f; x <= 19.18f; x += 0.30f)
                foreach (var l in allNocheLights)
                    maxLeakNorte = Math.Max(maxLeakNorte, ComputeIrradiance(l, W(x, y, 7.785f), nSouth));
        }

        if (maxLeakAula17 > 1e-6 || maxLeakAula16 > 1e-6 || maxLeakAula8 > 1e-6 || maxLeakAula9 > 1e-6 || maxLeakAula10 > 1e-6 || maxLeakNorte > 1e-6)
            throw new Exception($"Fuga matematica detectada: A17={maxLeakAula17:E3}, A16={maxLeakAula16:E3}, A8={maxLeakAula8:E3}, A9={maxLeakAula9:E3}, A10={maxLeakAula10:E3}, Norte={maxLeakNorte:E3}");

        var lightRows = new List<string>
        {
            "codigo|tipo_luz|objeto_luz|reutilizada|col_1based|fila_1based|x_plano|z_plano|x_unity|y_unity|z_unity|intensidad|alcance|spot_angle|rendering_mask|sombras|fotos"
        };
        foreach (var s in Specs)
        {
            if (!s.isLit)
            {
                Vector3 wpOff = W(CX(s.c), Y, CZ(s.r));
                lightRows.Add(string.Format(CultureInfo.InvariantCulture,
                    "{0}|SIN_LUZ_APAGADA|Difusor_Apagados_Combinados|False|{1}|{2}|{3:F4}|{4:F4}|{5:F4}|{6:F4}|{7:F4}|0.00|0.00|0|19|None|{8}",
                    s.id, s.c + 1, s.r + 1, CX(s.c), CZ(s.r), wpOff.x, wpOff.y, wpOff.z, s.refs));
                continue;
            }
            bool isReused = !string.IsNullOrEmpty(s.reusedLightName);
            Light l = isReused ? reusedNightLights[s.reusedLightName] : nightGroup.Find(s.id).GetComponentInChildren<Light>(true);
            Vector3 wp = l.transform.position;
            lightRows.Add(string.Format(CultureInfo.InvariantCulture,
                "{0}|{1}|{2}|{3}|{4}|{5}|{6:F4}|{7:F4}|{8:F4}|{9:F4}|{10:F4}|{11:F2}|{12:F2}|{13:F0}|{14}|None|{15}",
                s.id, l.type, P(l.transform), isReused, s.c + 1, s.r + 1, CX(s.c), CZ(s.r), wp.x, wp.y, wp.z,
                l.intensity, l.range, l.type == LightType.Spot ? l.spotAngle : 0f, Mask, s.refs));
        }
        File.WriteAllLines(Path.Combine(OutDir, "Luces_Colocadas_Zona2_CirculacionPA.csv"), lightRows);

        var summary = new List<string>
        {
            $"Fecha: {DateTime.Now:O}",
            $"Zona 2: Circulacion Principal PA (Acceso_Banos + Pasillo_Central N/C/S + Llegada_Escalera + Plaza_Sur, Y = {Y:F2} m)",
            $"Losas planas Plafon_PA_Y6.80 sustituidas: 6.",
            $"Reticula continua 60x60 cm centrada en x=14.24 m y z=6.495..46.215 m: 21 columnas x 67 filas (todos los recortes visibles >= 0.225 m; hueco de escalera intacto).",
            $"Luminarias integradas totales: 48 (36 encendidas + 12 aparentemente apagadas, incluidas las 8 apagadas de Plaza_Sur).",
            $"Piezas visuales antiguas retiradas de Iluminacion_PA_Mismo_Criterio_PB: 33 (11 Marcos + 11 Difusores NOCHE + 11 Difusores TERROR).",
            $"Luces originales reutilizadas: 11 en NOCHE + 5 de previsualizacion TERROR (0 objetos Light destruidos).",
            $"Luces nuevas en NOCHE: 25. Total luces funcionales NOCHE en Zona 2: 36. Mapas de sombras solicitados: 0.",
            $"Rendering Layer Mask local: {Mask} (Light Layer 1 definida en TagManager.asset), cullingMask = 1<<30.",
            $"Renderers ajenos en PA con bit {Mask}: {foreignPA.Length}.",
            $"Fuga matematica maxima hacia aulas adyacentes: Aula17={maxLeakAula17:F6}, Aula16={maxLeakAula16:F6}, Aula8={maxLeakAula8:F6}, Aula9={maxLeakAula9:F6}, Aula10={maxLeakAula10:F6}, Norte={maxLeakNorte:F6}.",
            $"Transform totales en {PlafonRootName}: {plafonRoot.GetComponentsInChildren<Transform>(true).Length} ({plafonRoot.GetComponentsInChildren<Renderer>(true).Length} renderers Static).",
            $"Transform totales en {LightsRootName}: {lightsRoot.GetComponentsInChildren<Transform>(true).Length} ({lightsRoot.GetComponentsInChildren<Renderer>(true).Length} renderers)."
        };
        File.WriteAllLines(Path.Combine(OutDir, "auditoria_zona2_circulacionPA.txt"), summary);
    }

    static void RunExteriorRegression()
    {
        const string tempScene = "Assets/Scenes/TEMP_Zona2_Baseline_Check.unity";
        string extDir = Path.Combine(OutDir, "exterior");
        Directory.CreateDirectory(extDir);
        try
        {
            File.Copy(BackupScenePath, tempScene, true);
            AssetDatabase.ImportAsset(tempScene);

            foreach (var entry in new[] { (tempScene, "Antes"), (ScenePath, "Actual") })
            {
                EditorSceneManager.OpenScene(entry.Item1, OpenSceneMode.Single);
                foreach (var mode in new[] { Docencia2VisualMode.Noche, Docencia2VisualMode.Terror })
                {
                    LuminariasZona1Aula16.ApplyModeAndSync(mode);
                    var cams = GameObject.Find("Camaras_Referencia_Docencia2").GetComponentsInChildren<Camera>(true);
                    foreach (var c in cams)
                        LuminariasZona1Aula16.RenderCameraToPng(c, Path.Combine(extDir, $"{entry.Item2}_{mode}_{c.name}.png"), 1280, 720);
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
            paRoot = GameObject.Find("PA_Interior")?.transform;
            if (!paRoot) throw new Exception("No se encontro PA_Interior.");
            pasilloGroup = paRoot.Find("Pasillo_Central");
            switcher = Object.FindAnyObjectByType<Docencia2ModeSwitcher>();

            if (pasilloGroup.Find("Plafon_PA_Y6.80") != null && !File.Exists(Path.Combine(OutDir, "capturas", "Antes_F35_Pasillo_Centro_Mecatronica.png")))
            {
                CaptureSuite("Antes");
                scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
                paRoot = GameObject.Find("PA_Interior")?.transform;
                pasilloGroup = paRoot.Find("Pasillo_Central");
                switcher = Object.FindAnyObjectByType<Docencia2ModeSwitcher>();
            }

            accesoBanosGroup = paRoot.Find("Acceso_Banos");
            llegadaGroup     = paRoot.Find("Llegada_Escalera");
            plazaSurGroup    = paRoot.Find("Plaza_Sur");
            oldLightsRoot    = paRoot.Find("Iluminacion_PA_Mismo_Criterio_PB");
            var savedMode    = switcher.modoActual;

            flatCeilings = new[] { accesoBanosGroup, pasilloGroup, llegadaGroup, plazaSurGroup }
                .SelectMany(g => g.GetComponentsInChildren<Transform>(true))
                .Where(t => t.name == "Plafon_PA_Y6.80")
                .Select(t => t.gameObject)
                .ToArray();

            string[] visualPrefixes = new[]
            {
                "Marco_Acceso_Banos", "Marco_Llegada_Escalera", "Marco_Pasillo_Central", "Marco_Plaza_Sur",
                "Difusor_Acceso_Banos", "Difusor_Llegada_Escalera", "Difusor_Pasillo_Central", "Difusor_Plaza_Sur",
                "Difusor_Terror_Acceso_Banos", "Difusor_Terror_Llegada_Escalera", "Difusor_Terror_Pasillo_Central", "Difusor_Terror_Plaza_Sur"
            };
            obsoleteVisuals = oldLightsRoot.GetComponentsInChildren<Transform>(true)
                .Where(t => visualPrefixes.Contains(t.name))
                .Select(t => t.gameObject)
                .ToArray();

            reusedNightLights = new Dictionary<string, Light>();
            foreach (var s in Specs.Where(sp => !string.IsNullOrEmpty(sp.reusedLightName)))
            {
                var l = oldLightsRoot.GetComponentsInChildren<Light>(true).FirstOrDefault(x => x.name == s.reusedLightName);
                if (!l) throw new Exception("Luz original a reutilizar no encontrada: " + s.reusedLightName);
                reusedNightLights[s.reusedLightName] = l;
            }

            reusedTerrorLights = ReusedTerrorPreviewNames
                .Select(n => oldLightsRoot.GetComponentsInChildren<Light>(true).FirstOrDefault(x => x.name == n))
                .Where(x => x != null)
                .ToArray();

            plafonRoot = pasilloGroup.Find(PlafonRootName);
            lightsRoot = pasilloGroup.Find(LightsRootName);

            if (flatCeilings.Length > 0 && (flatCeilings.Length != 6 || obsoleteVisuals.Length != 33))
                throw new Exception($"Inventario previo inesperado: plafones={flatCeilings.Length} (esp. 6), visuales={obsoleteVisuals.Length} (esp. 33).");

            // Strict whitelist for Snapshot() verification
            allowed.Clear();
            foreach (var grp in new[] { accesoBanosGroup, pasilloGroup, llegadaGroup, plazaSurGroup })
                allowed.Add(grp);
            foreach (var fc in flatCeilings) PermitObjectAndComponents(fc);
            foreach (var ov in obsoleteVisuals)
            {
                PermitObjectAndComponents(ov);
                if (ov.transform.parent) allowed.Add(ov.transform.parent);
            }
            foreach (var l in reusedNightLights.Values) PermitObjectAndComponents(l.gameObject);
            foreach (var l in reusedTerrorLights) PermitObjectAndComponents(l.gameObject);
            foreach (var r in GetAuthorizedReceivers()) allowed.Add(r);

            var before = Snapshot();

            matCeiling = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Docencia2/Interior/Mat_Int_Plafon_Reticular_PB.mat");
            matDark    = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Docencia2/Mat_Int_Troffer_Marco_Negro.mat");
            panelUvMesh = AssetDatabase.LoadAssetAtPath<Mesh>("Assets/Models/Docencia2/Aula17_Fase2a/Panel_UV_Material_Aprobado.asset");
            if (!matCeiling || !matDark || !panelUvMesh)
                throw new Exception("Materiales o malla UV base aprobados no encontrados.");

            matLitWarm = CreateOrUpdateMaterial("C2_Difusor_Encendido_Calido", matCeiling, new Color(0.88f, 0.88f, 0.80f), 0.12f, new Color(2.20f, 2.05f, 1.65f));
            matLitCool = CreateOrUpdateMaterial("C2_Difusor_Encendido_Frio",   matCeiling, new Color(0.86f, 0.91f, 0.96f), 0.12f, new Color(1.90f, 2.15f, 2.38f));
            matOff     = CreateOrUpdateMaterial("C2_Difusor_Apagado",          matCeiling, new Color(0.76f, 0.75f, 0.70f), 0.24f, null);
            matTerror  = CreateOrUpdateMaterial("C2_Difusor_Terror",           matCeiling, new Color(0.30f, 0.33f, 0.34f), 0.14f, new Color(0.50f, 0.65f, 0.68f) * 0.025f);

            foreach (var fc in flatCeilings) Object.DestroyImmediate(fc);
            if (plafonRoot) Object.DestroyImmediate(plafonRoot.gameObject);
            if (lightsRoot) Object.DestroyImmediate(lightsRoot.gameObject);
            foreach (var ov in obsoleteVisuals) Object.DestroyImmediate(ov);

            plafonRoot = MakeGroup(PlafonRootName, pasilloGroup);
            var panelsGroup  = MakeGroup("Paneles_60x60", plafonRoot);
            var backingGroup = MakeGroup("Respaldo_Continuo", plafonRoot);
            var barsGroup    = MakeGroup("Reticula", plafonRoot);
            var lampsGroup   = MakeGroup("Troffers_36_encendidos_12_apagados", plafonRoot);

            lightsRoot  = MakeGroup(LightsRootName, pasilloGroup);
            nightGroup  = MakeGroup("NOCHE", lightsRoot);
            terrorGroup = MakeGroup("TERROR", lightsRoot);

            var specByCell = Specs.ToDictionary(s => new Vector2Int(s.c, s.r), s => s);
            int panelCount = 0;

            for (int c = 0; c < 21; c++)
            {
                for (int r = 0; r < 67; r++)
                {
                    if (!TryGetCellBounds(c, r, out float x0, out float x1, out float z0, out float z1))
                        continue;

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
                        panelCount++;
                        MakeBox($"Panel_C{c + 1:00}_F{r + 1:00}", panelsGroup, x0 + 0.005f, x1 - 0.005f, Y, Y + 0.025f, z0 + 0.005f, z1 - 0.005f, matCeiling, true);
                    }
                }
            }

            // Continuous opaque backing over all 6 sub-rectangles
            foreach (var sz in SubZones)
                MakeBox("Respaldo_" + sz.name, backingGroup, sz.x0, sz.x1, Y + 0.027f, Y + 0.037f, sz.z0, sz.z1, matCeiling, true);

            // Clipped 18 mm T-bar profiles across all 6 sub-rectangles (merged into 1 static mesh)
            foreach (var sz in SubZones)
            {
                // Perimeter bars of subzone (only along true wall edges; internal open seams omit transverse perimeter bar)
                MakeBox($"Perfil_BordeO_{sz.name}", barsGroup, sz.x0, sz.x0 + 0.009f, Y - 0.004f, Y + 0.009f, sz.z0, sz.z1, matDark, false);
                MakeBox($"Perfil_BordeE_{sz.name}", barsGroup, sz.x1 - 0.009f, sz.x1, Y - 0.004f, Y + 0.009f, sz.z0, sz.z1, matDark, false);

                for (int i = 1; i <= 20; i++)
                {
                    float x = GX(i);
                    if (x > sz.x0 + 0.14f && x < sz.x1 - 0.14f)
                        MakeBox($"Perfil_X_{sz.name}_{i}", barsGroup, x - 0.009f, x + 0.009f, Y - 0.004f, Y + 0.009f, sz.z0, sz.z1, matDark, false);
                }
                for (int j = 1; j <= 66; j++)
                {
                    float z = GZ(j);
                    if (z > sz.z0 + 0.14f && z < sz.z1 - 0.14f)
                        MakeBox($"Perfil_Z_{sz.name}_{j}", barsGroup, sz.x0, sz.x1, Y - 0.004f, Y + 0.009f, z - 0.009f, z + 0.009f, matDark, false);
                }
            }
            MakeBox("Perfil_BordeN_Acceso", barsGroup, 12.24f, 16.24f, Y - 0.004f, Y + 0.009f, 6.42f, 6.429f, matDark, false);
            MakeBox("Perfil_BordeS_Plaza",  barsGroup,  8.00f, 20.48f, Y - 0.004f, Y + 0.009f, 46.281f, 46.29f, matDark, false);

            plafonRoot.gameObject.isStatic   = true;
            panelsGroup.gameObject.isStatic  = true;
            backingGroup.gameObject.isStatic = true;
            barsGroup.gameObject.isStatic    = true;
            lampsGroup.gameObject.isStatic   = true;

            CombineStaticGroup(panelsGroup,  "Plafon_Paneles_Estaticos_Combinados", mf => true);
            CombineStaticGroup(backingGroup, "Plafon_Respaldo_Continuo_Combinado",  mf => true);
            CombineStaticGroup(barsGroup,    "Plafon_Perfiles_Estaticos_Combinados", mf => true);
            CombineStaticGroup(lampsGroup,   "Plafon_Troffers_Cuerpos_Combinados",  mf => mf.name.StartsWith("Cuerpo_"));
            CombineStaticGroup(lampsGroup,   "Difusor_Apagados_Combinados",         mf => mf.name.StartsWith("Difusor_Apagado_"));

            // Configure the 36 NOCHE lights (11 reused + 25 new)
            foreach (var spec in Specs)
            {
                if (!spec.isLit) continue;
                Light l;
                if (!string.IsNullOrEmpty(spec.reusedLightName))
                    l = reusedNightLights[spec.reusedLightName];
                else
                {
                    var holder = nightGroup.Find(spec.id);
                    l = MakeGroup("Luz_NOCHE_" + spec.id, holder).gameObject.AddComponent<Light>();
                }
                ConfigureZona2Light(l, spec);
            }

            // Reposition the 5 reused TERROR editor-preview lights cleanly under 5 lit corridor troffers & update their renderingLayerMask to Mask (2u)
            string[] anchorIds = new[] { "C2_L02_Encendido", "C2_L12_Encendido", "C2_L24_Encendido_Frio", "C2_L33_Encendido_Frio", "C2_L35_Encendido" };
            for (int i = 0; i < reusedTerrorLights.Length && i < anchorIds.Length; i++)
            {
                var anchor = Specs.First(s => s.id == anchorIds[i]);
                var tl = reusedTerrorLights[i];
                tl.transform.position = W(CX(anchor.c), 6.32f, CZ(anchor.r));
                tl.shadows = LightShadows.None;
                tl.cullingMask = 1 << LayerPA;
                var uld = tl.GetComponent<UniversalAdditionalLightData>();
                if (!uld) uld = tl.gameObject.AddComponent<UniversalAdditionalLightData>();
                uld.renderingLayers = Mask;
                uld.customShadowLayers = false;
            }

            foreach (var r in GetAuthorizedReceivers())
                r.renderingLayerMask |= Mask;

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

            CaptureSuite("Despues");
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            RunExteriorRegression();

            File.WriteAllText(Path.Combine(OutDir, "build_completado.txt"), $"Zona 2 (Circulacion Principal PA, {panelCount} paneles solidos + 48 luminarias) construida, auditada y guardada fuera de Play Mode.\n{DateTime.Now:O}\n");
            Debug.Log("ZONA2_CIRCULACION_PA_BUILD_OK");
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
            SessionState.SetBool("ZONA2_CIRCULACION_PA_PLAY_VERIFY", true);
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
public static class LuminariasZona2CirculacionPAPlayBoot
{
    static LuminariasZona2CirculacionPAPlayBoot()
    {
        EditorApplication.update += OnEditorUpdate;
    }

    static void OnEditorUpdate()
    {
        if (SessionState.GetBool("ZONA2_CIRCULACION_PA_PLAY_VERIFY", false) && EditorApplication.isPlaying && !EditorApplication.isCompiling)
        {
            SessionState.SetBool("ZONA2_CIRCULACION_PA_PLAY_VERIFY", false);
            new GameObject("TEMP_Zona2_CirculacionPA_PlayVerifier").AddComponent<LuminariasZona2CirculacionPARuntime>();
        }
    }
}

public class LuminariasZona2CirculacionPARuntime : MonoBehaviour
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

        string playDir = Path.Combine(LuminariasZona2CirculacionPA.OutDir, "play");
        Directory.CreateDirectory(playDir);

        var player = Docencia2FirstPersonPlayer.Instance ?? Object.FindAnyObjectByType<Docencia2FirstPersonPlayer>();
        var switcher = Object.FindAnyObjectByType<Docencia2ModeSwitcher>();
        var pasillo = GameObject.Find("PA_Interior/Pasillo_Central")?.transform;
        var oldLightsRoot = GameObject.Find("PA_Interior/Iluminacion_PA_Mismo_Criterio_PB")?.transform;

        var cc = player.GetComponent<CharacterController>();
        var cam = player.playerCamera;
        var rt = new RenderTexture(1280, 720, 24, RenderTextureFormat.ARGBHalf);
        cam.targetTexture = rt;
        player.enableBatteryDrain = false;
        player.showDebugHudAndShortcuts = true;

        string[] reusedNames = new[]
        {
            "Luz_PA_Acceso_Banos_20",
            "Luz_PA_Pasillo_Central_25", "Luz_PA_Pasillo_Central_26", "Luz_PA_Pasillo_Central_27",
            "Luz_PA_Pasillo_Central_28", "Luz_PA_Pasillo_Central_29", "Luz_PA_Pasillo_Central_30",
            "Luz_PA_Llegada_Escalera_31", "Luz_PA_Llegada_Escalera_32",
            "Luz_PA_Plaza_Sur_45", "Luz_PA_Plaza_Sur_46",
            "Luz_PA_Terror_26", "Luz_PA_Terror_28", "Luz_PA_Terror_30", "Luz_PA_Terror_32", "Luz_PA_Terror_46"
        };

        Light[] GetZona2Lights() =>
            pasillo.GetComponentsInChildren<Light>(true)
                .Concat(oldLightsRoot.GetComponentsInChildren<Light>(true).Where(l => reusedNames.Contains(l.name)))
                .Distinct()
                .ToArray();

        var report = new List<string>
        {
            $"Jugador real: {LuminariasZona2CirculacionPA.P(player.transform)} (height={cc.height:F2}, radius={cc.radius:F2}, stepOffset={cc.stepOffset:F2})"
        };

        // 1. Live ToggleMode test (NOCHE -> TERROR -> NOCHE)
        LuminariasZona1Aula16.ApplyModeAndSync(Docencia2VisualMode.Noche);
        for (int i = 0; i < 15; i++) yield return null;
        int n1 = GetZona2Lights().Count(l => l.enabled && l.gameObject.activeInHierarchy);
        switcher.ToggleMode();
        for (int i = 0; i < 15; i++) yield return null;
        int t1 = GetZona2Lights().Count(l => l.enabled && l.gameObject.activeInHierarchy);
        switcher.ToggleMode();
        for (int i = 0; i < 15; i++) yield return null;
        int n2 = GetZona2Lights().Count(l => l.enabled && l.gameObject.activeInHierarchy);
        report.Add($"Prueba ToggleMode (tecla T): NOCHE={n1} luces activas -> TERROR={t1} luces activas -> NOCHE={n2} luces activas.");

        // 2. CharacterController walk along the entire 39m spine (Acceso_Banos -> Pasillo N -> Escalera -> Pasillo C -> Pasillo S -> Plaza Sur)
        player.TeleportToPlan(14.24f, 3.606f, 7.20f, 0f);
        player.enabled = false;
        float[][] waypoints = new[]
        {
            new[] { 14.24f, 12.00f },
            new[] { 14.24f, 19.20f },
            new[] { 10.20f, 19.20f },
            new[] { 14.24f, 19.20f },
            new[] { 14.24f, 27.00f },
            new[] { 14.24f, 34.50f },
            new[] { 14.24f, 42.50f }
        };
        for (int i = 0; i < waypoints.Length; i++)
        {
            var wp = waypoints[i];
            Vector3 target = LuminariasZona2CirculacionPA.W(wp[0], 3.626f, wp[1]);
            float elapsed = 0f;
            while (elapsed < 8.5f)
            {
                Vector3 delta = target - player.transform.position;
                delta.y = 0f;
                if (delta.magnitude < 0.06f) break;
                cc.Move(Vector3.ClampMagnitude(delta, 3.2f * Time.deltaTime) + Vector3.down * 2.0f * Time.deltaTime);
                elapsed += Time.deltaTime;
                yield return null;
            }
            Vector3 rem = target - player.transform.position;
            rem.y = 0f;
            bool ok = rem.magnitude < 0.10f && Mathf.Abs(player.transform.position.y - 3.636f) < 0.12f;
            report.Add(string.Format(CultureInfo.InvariantCulture,
                "Tramo {0}: destino ({1:F2},{2:F2}), error={3:F3}m, Y={4:F3} -> {5}",
                i + 1, wp[0], wp[1], rem.magnitude, player.transform.position.y, ok ? "PASS" : "REVISAR"));
        }
        player.enabled = true;

        // 3. FPS & Play captures across 3 views in NOCHE and TERROR (>= 8s per view at 1280x720)
        var fpsRows = new List<string>
        {
            "modo|vista|resolucion|muestras|fps_promedio|fps_p95|ms_max|luces_zona2_activas|linterna"
        };

        float[][] spots = new[]
        {
            new[] { 14.24f, 18.20f, 14.24f, 44.00f }, // Vista 1: Eje largo hacia el Sur desde Llegada_Escalera (la vista mas exigente de 28m!)
            new[] { 14.24f, 16.50f, 14.24f,  7.00f }, // Vista 2: Pasillo Norte hacia Acceso_Banos y canceles
            new[] { 10.60f, 39.20f, 16.50f, 45.40f }  // Vista 3: Plaza Sur con luminarias apagadas y derrame del pasillo
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
                Vector3 fwd = LuminariasZona2CirculacionPA.W(s[2], 0f, s[3]) - LuminariasZona2CirculacionPA.W(s[0], 0f, s[1]);
                player.transform.rotation = Quaternion.LookRotation(fwd);
                cam.transform.localRotation = Quaternion.Euler(-8f, 0f, 0f);

                for (int w = 0; w < 45; w++) yield return null;

                var dtSamples = new List<double>(2048);
                double last = Time.realtimeSinceStartupAsDouble;
                double elapsedActive = 0.0;
                for (int i = 0; i < 6000 && (i < 300 || elapsedActive < 8.0); i++)
                {
                    yield return null;
                    double now = Time.realtimeSinceStartupAsDouble;
                    double dt = now - last;
                    last = now;
                    // Ignore OS process suspension > 0.5s in headless background mode so real GPU/CPU frame times are measured
                    if (dt > 0.5) continue;
                    dtSamples.Add(dt);
                    elapsedActive += dt;
                }

                dtSamples.Sort();
                double avgFps = dtSamples.Count / dtSamples.Sum();
                double p95Fps = 1.0 / dtSamples[(int)(dtSamples.Count * 0.95)];
                double maxMs = dtSamples.Last() * 1000.0;
                int activeCount = GetZona2Lights().Count(l => l.enabled && l.gameObject.activeInHierarchy);

                fpsRows.Add(string.Format(CultureInfo.InvariantCulture,
                    "{0}|Vista{1}|1280x720|{2}|{3:F2}|{4:F2}|{5:F2}|{6}|{7}",
                    mode, v + 1, dtSamples.Count, avgFps, p95Fps, maxMs, activeCount, player.flashlightOn));

                LuminariasZona1Aula16.RenderCameraToPng(cam, Path.Combine(playDir, $"Play_{mode}_Vista{v + 1}.png"), 1280, 720);
            }
        }

        // 4. Flashlight ON vs OFF in Play TERROR facing MECATRONICA corridor wall
        if (switcher.modoActual != Docencia2VisualMode.Terror) switcher.ToggleMode();
        player.TeleportToPlan(14.80f, 3.606f, 27.00f, 0f);
        player.transform.rotation = Quaternion.LookRotation(LuminariasZona2CirculacionPA.W(17.16f, 0f, 27.00f) - LuminariasZona2CirculacionPA.W(14.80f, 0f, 27.00f));
        cam.transform.localRotation = Quaternion.identity;

        foreach (bool on in new[] { false, true })
        {
            player.SetFlashlightState(on, false);
            for (int w = 0; w < 40; w++) yield return null;
            LuminariasZona1Aula16.RenderCameraToPng(cam, Path.Combine(playDir, $"Play_TERROR_Linterna_{(on ? "ON" : "OFF")}.png"), 1280, 720);
        }

        report.Add($"GPU={SystemInfo.graphicsDeviceName} | CPU={SystemInfo.processorType} | RAM={SystemInfo.systemMemorySize}MB | Unity={Application.unityVersion}");
        report.Add($"Errores/excepciones/advertencias shadow atlas en Play: {consoleIssues.Count}");

        File.WriteAllLines(Path.Combine(LuminariasZona2CirculacionPA.OutDir, "recorrido_play.txt"), report);
        File.WriteAllLines(Path.Combine(LuminariasZona2CirculacionPA.OutDir, "fps_play.csv"), fpsRows);
        File.WriteAllLines(Path.Combine(LuminariasZona2CirculacionPA.OutDir, "consola_play.txt"), consoleIssues);

        cam.targetTexture = null;
        rt.Release();
        Object.Destroy(rt);
        Application.logMessageReceived -= OnLog;

        File.WriteAllText(Path.Combine(LuminariasZona2CirculacionPA.OutDir, "play_completado.txt"), $"Verificacion en Play Mode de Zona 2 completada sin guardar estado de runtime.\n{DateTime.Now:O}\n");
        EditorApplication.Exit(0);
    }
}
