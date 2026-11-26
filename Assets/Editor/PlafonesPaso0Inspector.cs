// Paso 0 exclusivamente: inspeccion de lectura y capturas ANTES sin modificar ni guardar la escena.
// Sin [MenuItem] y sin GetInstanceID.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Vaelith.Docencia2;
using Object = UnityEngine.Object;

public static class PlafonesPaso0Inspector
{
    private const string ScenePath = "Assets/Scenes/Exterior_Docencia2.unity";
    private const string OutDir = "Paso0_Inspeccion_Plafones";

    private static float ToPlanX(float unityZ) => (14.25f - unityZ) * (28.48f / 28.50f);
    private static float ToPlanZ(float unityX) => (27.0f - unityX) * (53.92f / 54.0f);
    private static Vector3 W(float xPlan, float y, float zPlan) => Docencia2InteriorBuilder.PlanToUnityPos(xPlan, y, zPlan);
    private static string PathOf(Transform t) => t.parent == null ? t.name : PathOf(t.parent) + "/" + t.name;
    private static string F(float v) => v.ToString("F4", CultureInfo.InvariantCulture);

    public static void Run()
    {
        try
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new Exception("Paso 0 debe ejecutarse fuera de Play.");

            Directory.CreateDirectory(OutDir);
            Directory.CreateDirectory(OutDir + "/capturas_antes");

            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var roots = scene.GetRootGameObjects();

            DumpHierarchySummary(roots);
            DumpCeilingsSlabsAndRoofs(roots);
            DumpInteriorZonesAndGridAnalysis(roots);
            DumpLuminairesAndControllers(roots);
            CaptureReadOnlyZoneViews();

            // Reabrir sin guardar absolutamente nada para garantizar 0 cambios en memoria/disco
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            File.WriteAllText(OutDir + "/inspeccion_ok.txt", "Inspeccion Paso 0 completada sin guardar cambios en la escena.\n" + DateTime.Now.ToString("O"), Encoding.UTF8);
            EditorApplication.Exit(0);
        }
        catch (Exception ex)
        {
            Directory.CreateDirectory(OutDir);
            File.WriteAllText(OutDir + "/error.txt", ex.ToString(), Encoding.UTF8);
            Debug.LogException(ex);
            EditorApplication.Exit(1);
        }
    }

    private static void DumpHierarchySummary(GameObject[] roots)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# Raíces y grupos de primer/segundo nivel en Exterior_Docencia2.unity");
        sb.AppendLine();
        foreach (var r in roots)
        {
            int trCount = r.GetComponentsInChildren<Transform>(true).Length;
            int renCount = r.GetComponentsInChildren<Renderer>(true).Length;
            int lightCount = r.GetComponentsInChildren<Light>(true).Length;
            sb.AppendLine($"ROOT: {r.name} (active={r.activeSelf}, layer={r.layer}, transforms={trCount}, renderers={renCount}, lights={lightCount})");
            foreach (Transform c1 in r.transform)
            {
                int cTr = c1.GetComponentsInChildren<Transform>(true).Length;
                int cRen = c1.GetComponentsInChildren<Renderer>(true).Length;
                int cLi = c1.GetComponentsInChildren<Light>(true).Length;
                sb.AppendLine($"  - {c1.name} (active={c1.gameObject.activeSelf}, layer={c1.gameObject.layer}, tr={cTr}, ren={cRen}, lights={cLi})");
            }
        }
        File.WriteAllText(OutDir + "/escena_raices_y_jerarquia.txt", sb.ToString(), Encoding.UTF8);
    }

    private static string ClassifyHorizontalObject(string path, string matNames, Bounds b)
    {
        string pLow = path.ToLowerInvariant();
        if (pLow.Contains("aula_17/plafon_aula17_fase2a"))
            return "PLAFON_CUADRADO_YA_RESUELTO_AULA17";
        if (pLow.StartsWith("docencia2_exterior/"))
        {
            if (pLow.Contains("azotea") || pLow.Contains("cubierta") || pLow.Contains("pretil"))
                return "AZOTEA_EXTERIOR";
            if (pLow.Contains("voladizo") || pLow.Contains("goteron") || pLow.Contains("marquesina") || pLow.Contains("plafon_ext"))
                return "PLAFON_O_VOLADIZO_EXTERIOR";
            return "ESTRUCTURA_O_FACHADA_EXTERIOR";
        }
        if (pLow.Contains("losa_entrepiso") || pLow.Contains("losa_franja"))
            return "LOSA_ESTRUCTURAL_ENTREPISO";
        if (pLow.Contains("plafon"))
            return "PLAFON_INTERIOR";
        if (pLow.Contains("piso_") || pLow.Contains("acabado_piso") || pLow.Contains("grada_") || pLow.Contains("descanso"))
            return "PISO_O_ESCALERA";
        if (matNames.Contains("Plafon_Reticular"))
            return "PLAFON_INTERIOR_POR_MATERIAL";
        return "OTRO_HORIZONTAL";
    }

    private static void DumpCeilingsSlabsAndRoofs(GameObject[] roots)
    {
        var lines = new List<string>
        {
            "categoria|path|activeInHierarchy|layer|renderingLayerMask|isStatic|hasCollider|submeshes|verts|tris|materials|xPlanMin|xPlanMax|dxPlan|zPlanMin|zPlanMax|dzPlan|yMin|yMax|dy|uvMin|uvMax"
        };

        foreach (var root in roots)
        {
            foreach (var ren in root.GetComponentsInChildren<Renderer>(true))
            {
                if (ren == null) continue;
                string path = PathOf(ren.transform);
                string pLow = path.ToLowerInvariant();
                Bounds b = ren.bounds;
                string mats = string.Join(";", (ren.sharedMaterials ?? Array.Empty<Material>()).Select(m => m ? m.name : "null"));

                bool nameMatch = pLow.Contains("plafon") || pLow.Contains("losa") || pLow.Contains("azotea") ||
                                 pLow.Contains("cubierta") || pLow.Contains("voladizo") || pLow.Contains("techo") ||
                                 mats.Contains("Plafon_Reticular");
                bool geomMatch = b.size.y <= 0.48f && Mathf.Max(b.size.x, b.size.z) >= 0.90f &&
                                 ((b.min.y >= 2.50f && b.min.y <= 3.70f) || b.min.y >= 6.20f);

                if (!nameMatch && !geomMatch) continue;
                // Evitar llenar la tabla de plafones con cientos de pisos individuales si no son losa/plafón/azotea
                if (!nameMatch && (pLow.Contains("piso_") || pLow.Contains("zoclo") || pLow.Contains("mesa") || pLow.Contains("estante")))
                    continue;

                var mf = ren.GetComponent<MeshFilter>();
                Mesh m = mf ? mf.sharedMesh : null;
                int verts = m ? m.vertexCount : 0;
                int sub = m ? m.subMeshCount : 0;
                int tris = 0;
                string uvMinStr = "", uvMaxStr = "";
                if (m != null)
                {
                    for (int s = 0; s < sub; s++) tris += (int)m.GetIndexCount(s) / 3;
                    var uvs = m.uv;
                    if (uvs != null && uvs.Length > 0)
                    {
                        Vector2 uMin = new Vector2(float.MaxValue, float.MaxValue);
                        Vector2 uMax = new Vector2(float.MinValue, float.MinValue);
                        for (int i = 0; i < uvs.Length; i++)
                        {
                            uMin = Vector2.Min(uMin, uvs[i]);
                            uMax = Vector2.Max(uMax, uvs[i]);
                        }
                        uvMinStr = $"{F(uMin.x)},{F(uMin.y)}";
                        uvMaxStr = $"{F(uMax.x)},{F(uMax.y)}";
                    }
                }

                float x0 = ToPlanX(b.max.z);
                float x1 = ToPlanX(b.min.z);
                float z0 = ToPlanZ(b.max.x);
                float z1 = ToPlanZ(b.min.x);

                string cat = ClassifyHorizontalObject(path, mats, b);
                bool hasCol = ren.GetComponent<Collider>() != null;

                lines.Add(string.Join("|", new[]
                {
                    cat,
                    path,
                    ren.gameObject.activeInHierarchy.ToString(),
                    ren.gameObject.layer.ToString(),
                    ren.renderingLayerMask.ToString(),
                    ren.gameObject.isStatic.ToString(),
                    hasCol.ToString(),
                    sub.ToString(),
                    verts.ToString(),
                    tris.ToString(),
                    mats,
                    F(x0), F(x1), F(x1 - x0),
                    F(z0), F(z1), F(z1 - z0),
                    F(b.min.y), F(b.max.y), F(b.size.y),
                    uvMinStr, uvMaxStr
                }));
            }
        }

        File.WriteAllLines(OutDir + "/plafones_losas_azotea.csv", lines, Encoding.UTF8);
    }

    private static (int fullCount, float cutEach, float firstLineOffset) ComputeBalancedGrid(float clearSpan)
    {
        if (clearSpan < 0.40f)
            return (0, clearSpan * 0.5f, clearSpan * 0.5f);

        int q = Mathf.FloorToInt((clearSpan + 0.0001f) / 0.60f);
        float rem = clearSpan - q * 0.60f;
        if (rem < 0.001f)
        {
            // Múltiplo exacto de 0.60 m -> módulos completos de 0.60 m en bordes
            return (q, 0.60f, 0.60f);
        }
        if (rem >= 0.3999f)
        {
            // Dos recortes simétricos >= 0.20 m
            float cut = rem * 0.5f;
            return (q, cut, cut);
        }
        else
        {
            // Desfasar medio módulo (q - 1 completos) para obtener recortes simétricos en (0.30, 0.50) m >= 0.20 m
            int full = Mathf.Max(0, q - 1);
            float cut = (clearSpan - full * 0.60f) * 0.5f;
            return (full, cut, cut);
        }
    }

    private static void DumpInteriorZonesAndGridAnalysis(GameObject[] roots)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# Análisis detallado de plafones interiores por recinto (PB y PA)");
        sb.AppendLine();

        var csv = new List<string>
        {
            "nivel|zona_padre|objeto_plafon|estado|y_cara_inf|y_cara_sup|x0_plafon|x1_plafon|dx_plafon|z0_plafon|z1_plafon|dz_plafon|x0_libre_est|x1_libre_est|dx_libre|z0_libre_est|z1_libre_est|dz_libre|cols_60|corte_OE_m|filas_60|corte_NS_m|cumple_20cm|layer|rendMask|material|obstaculos_cerca_techo"
        };

        // Recopilar todos los muros/forros/canceles/columnas/soportes para medir caras libres e interferencias junto al techo
        var allRenderers = roots.SelectMany(r => r.GetComponentsInChildren<Renderer>(true)).Where(r => r != null && r.gameObject.activeInHierarchy).ToArray();

        foreach (string rootName in new[] { "PB_Interior", "PA_Interior" })
        {
            var rGo = roots.FirstOrDefault(g => g.name == rootName);
            if (rGo == null) continue;
            string nivel = rootName == "PB_Interior" ? "PB" : "PA";

            // Buscar también cualquier plafón combinado o por zona
            var ceilRenderers = rGo.GetComponentsInChildren<Renderer>(true)
                .Where(r =>
                {
                    string n = r.name;
                    string p = PathOf(r.transform);
                    if (p.Contains("Aula_17/Plafon_Aula17_Fase2a_SUPUESTO"))
                        return n == "Respaldo_Continuo_Sin_Huecos_Completos";
                    return n.StartsWith("Plafon_") || n.Contains("Comb_Plafon");
                })
                .OrderBy(r => PathOf(r.transform))
                .ToArray();

            sb.AppendLine($"## {rootName} — Renderers de plafón detectados: {ceilRenderers.Length}");
            foreach (var cr in ceilRenderers)
            {
                string path = PathOf(cr.transform);
                string zoneName = cr.transform.parent ? cr.transform.parent.name : rootName;
                Bounds b = cr.bounds;
                float x0 = ToPlanX(b.max.z);
                float x1 = ToPlanX(b.min.z);
                float z0 = ToPlanZ(b.max.x);
                float z1 = ToPlanZ(b.min.x);
                float yBot = b.min.y;
                float yTop = b.max.y;

                // Detectar muros/columnas/canceles/proyectores cercanos a la cota del plafón dentro o en el borde de esta huella
                float cx0 = x0, cx1 = x1, cz0 = z0, cz1 = z1;
                var obstacles = new List<string>();

                foreach (var wr in allRenderers)
                {
                    if (wr == cr) continue;
                    string wp = PathOf(wr.transform);
                    if (wp.Contains("Aula_17/Plafon_Aula17_Fase2a")) continue;
                    if (wp.StartsWith("Docencia2_Exterior/") && !wp.Contains("Columna") && !wp.Contains("Pilar")) continue;

                    Bounds wb = wr.bounds;
                    if (wb.max.y < yBot - 0.35f || wb.min.y > yTop + 0.05f) continue;

                    float wx0 = ToPlanX(wb.max.z);
                    float wx1 = ToPlanX(wb.min.z);
                    float wz0 = ToPlanZ(wb.max.x);
                    float wz1 = ToPlanZ(wb.min.x);

                    // Comprobar solape horizontal con margen de 0.12 m
                    if (wx1 < x0 - 0.12f || wx0 > x1 + 0.12f || wz1 < z0 - 0.12f || wz0 > z1 + 0.12f)
                        continue;

                    string wn = wr.name;
                    bool isWallOrLining = wn.Contains("Muro") || wn.Contains("Forro") || wn.Contains("Tabique") || wn.Contains("Mampara") || wn.Contains("Cancel") || wn.Contains("Vidrio_Al_Plafon") || wn.Contains("Riel_Sup_Plafon");
                    bool isPerimeterXWall = isWallOrLining && (wx1 - wx0) <= 0.45f && (Mathf.Min(z1, wz1) - Mathf.Max(z0, wz0)) > (z1 - z0) * 0.45f;
                    bool isPerimeterZWall = isWallOrLining && (wz1 - wz0) <= 0.45f && (Mathf.Min(x1, wx1) - Mathf.Max(x0, wx0)) > (x1 - x0) * 0.45f;

                    if (isPerimeterXWall)
                    {
                        if (Mathf.Abs(wx0 - x0) < 0.25f || Mathf.Abs(wx1 - x0) < 0.25f)
                            cx0 = Mathf.Max(cx0, wx1);
                        else if (Mathf.Abs(wx1 - x1) < 0.25f || Mathf.Abs(wx0 - x1) < 0.25f)
                            cx1 = Mathf.Min(cx1, wx0);
                    }
                    else if (isPerimeterZWall)
                    {
                        if (Mathf.Abs(wz0 - z0) < 0.25f || Mathf.Abs(wz1 - z0) < 0.25f)
                            cz0 = Mathf.Max(cz0, wz1);
                        else if (Mathf.Abs(wz1 - z1) < 0.25f || Mathf.Abs(wz0 - z1) < 0.25f)
                            cz1 = Mathf.Min(cz1, wz0);
                    }

                    // Registrar columnas, soportes de proyector, rieles de cortina, marcos de luminaria o divisiones interiores que toquen cerca del plafón
                    if (wn.Contains("Columna") || wn.Contains("Pilar") || wn.Contains("Soporte_Techo") || wn.Contains("Cortina") ||
                        wn.Contains("Riel_Sup_Plafon") || wn.Contains("Retorno") || wn.Contains("Oblicuo") ||
                        (isWallOrLining && wx0 > x0 + 0.30f && wx1 < x1 - 0.30f && wz0 > z0 + 0.30f && wz1 < z1 - 0.30f))
                    {
                        if (obstacles.Count < 12)
                            obstacles.Add($"{wn}[x={wx0:F2}..{wx1:F2},z={wz0:F2}..{wz1:F2},yMax={wb.max.y:F2}]");
                    }
                }

                float dxClear = Mathf.Max(0.01f, cx1 - cx0);
                float dzClear = Mathf.Max(0.01f, cz1 - cz0);
                var gx = ComputeBalancedGrid(dxClear);
                var gz = ComputeBalancedGrid(dzClear);
                bool ok20 = gx.cutEach >= 0.199f && gz.cutEach >= 0.199f;
                string estado = path.Contains("Aula_17") ? "YA_RESUELTO_FASE2A" : (cr.gameObject.activeInHierarchy ? "ACTIVO_PLANO_TEXTURA" : "INACTIVO");
                string matName = cr.sharedMaterial ? cr.sharedMaterial.name : "null";
                string obsStr = string.Join("; ", obstacles);

                csv.Add(string.Join("|", new[]
                {
                    nivel, zoneName, cr.name, estado,
                    F(yBot), F(yTop),
                    F(x0), F(x1), F(x1 - x0),
                    F(z0), F(z1), F(z1 - z0),
                    F(cx0), F(cx1), F(dxClear),
                    F(cz0), F(cz1), F(dzClear),
                    gx.fullCount.ToString(), F(gx.cutEach),
                    gz.fullCount.ToString(), F(gz.cutEach),
                    ok20.ToString(),
                    cr.gameObject.layer.ToString(),
                    cr.renderingLayerMask.ToString(),
                    matName,
                    obsStr
                }));

                sb.AppendLine($"- `{path}` [{estado}] | Y={F(yBot)}..{F(yTop)} | Plafón x={F(x0)}..{F(x1)} ({F(x1 - x0)}m), z={F(z0)}..{F(z1)} ({F(z1 - z0)}m) | Libre est. x={F(cx0)}..{F(cx1)} ({F(dxClear)}m -> {gx.fullCount}×0.60 + 2×{F(gx.cutEach)}m), z={F(cz0)}..{F(cz1)} ({F(dzClear)}m -> {gz.fullCount}×0.60 + 2×{F(gz.cutEach)}m) | >=20cm={ok20} | layer={cr.gameObject.layer} mask={cr.renderingLayerMask} mat={matName}");
                if (obstacles.Count > 0)
                    sb.AppendLine($"    Interferencias/encuentros techo: {obsStr}");
            }
            sb.AppendLine();
        }

        File.WriteAllLines(OutDir + "/recintos_y_caras_libres.csv", csv, Encoding.UTF8);
        File.WriteAllText(OutDir + "/recintos_resumen.md", sb.ToString(), Encoding.UTF8);
    }

    private static void DumpLuminairesAndControllers(GameObject[] roots)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# Luminarias interiores, difusores y relación con NOCHE / TERROR");
        sb.AppendLine();

        var switcher = Object.FindAnyObjectByType<Docencia2ModeSwitcher>();
        if (switcher != null)
        {
            sb.AppendLine("## Docencia2ModeSwitcher");
            sb.AppendLine($"- Objeto: {PathOf(switcher.transform)} | modoActual guardado: {switcher.modoActual} | tecla: {switcher.toggleKey}");
            sb.AppendLine($"- grupoLucesInteriorNoche: {(switcher.grupoLucesInteriorNoche ? PathOf(switcher.grupoLucesInteriorNoche.transform) + " (activeSelf=" + switcher.grupoLucesInteriorNoche.activeSelf + ")" : "null")}");
            sb.AppendLine($"- grupoLucesInteriorTerror: {(switcher.grupoLucesInteriorTerror ? PathOf(switcher.grupoLucesInteriorTerror.transform) + " (activeSelf=" + switcher.grupoLucesInteriorTerror.activeSelf + ")" : "null")}");
            sb.AppendLine();
        }

        var terrorCtrl = Object.FindAnyObjectByType<Docencia2TerrorDarknessController>();
        if (terrorCtrl != null)
        {
            sb.AppendLine("## Docencia2TerrorDarknessController (referencias serializadas protegidas)");
            sb.AppendLine($"- Objeto: {PathOf(terrorCtrl.transform)}");
            sb.AppendLine($"- terrorAtmosphereRoot: {(terrorCtrl.terrorAtmosphereRoot ? PathOf(terrorCtrl.terrorAtmosphereRoot.transform) : "null")}");
            sb.AppendLine($"- dyingFluorescentLight: {(terrorCtrl.dyingFluorescentLight ? PathOf(terrorCtrl.dyingFluorescentLight.transform) : "null")}");
            sb.AppendLine($"- dyingFluorescentRenderer: {(terrorCtrl.dyingFluorescentRenderer ? PathOf(terrorCtrl.dyingFluorescentRenderer.transform) : "null")}");
            sb.AppendLine($"- emergencyAmberLights ({terrorCtrl.emergencyAmberLights?.Count ?? 0}): {string.Join(", ", (terrorCtrl.emergencyAmberLights ?? new List<Light>()).Where(x => x).Select(x => PathOf(x.transform)))}");
            sb.AppendLine($"- emergencyPanelRenderers ({terrorCtrl.emergencyPanelRenderers?.Count ?? 0}): {string.Join(", ", (terrorCtrl.emergencyPanelRenderers ?? new List<Renderer>()).Where(x => x).Select(x => PathOf(x.transform)))}");
            sb.AppendLine($"- windowMoonGlowLights ({terrorCtrl.windowMoonGlowLights?.Count ?? 0}): {string.Join(", ", (terrorCtrl.windowMoonGlowLights ?? new List<Light>()).Where(x => x).Select(x => PathOf(x.transform)))}");
            sb.AppendLine();
        }

        sb.AppendLine("## Docencia2InteriorModeTracker en escena");
        foreach (var tr in Object.FindObjectsByType<Docencia2InteriorModeTracker>(FindObjectsInactive.Include).OrderBy(t => PathOf(t.transform)))
        {
            sb.AppendLine($"- {PathOf(tr.transform)} | night={(tr.nightGroup ? PathOf(tr.nightGroup.transform) : "null")} | terror={(tr.terrorGroup ? PathOf(tr.terrorGroup.transform) : "null")}");
        }
        sb.AppendLine();

        sb.AppendLine("## Todas las luces interiores (PB_Interior y PA_Interior)");
        var lightCsv = new List<string> { "nivel|path|activeSelf|type|xPlan|y|zPlan|intensity|range|shadows|cullingMask|renderingLayers" };
        foreach (string rName in new[] { "PB_Interior", "PA_Interior" })
        {
            var rGo = roots.FirstOrDefault(g => g.name == rName);
            if (rGo == null) continue;
            foreach (var l in rGo.GetComponentsInChildren<Light>(true).OrderBy(x => PathOf(x.transform)))
            {
                Vector3 p = l.transform.position;
                var uld = l.GetComponent<UniversalAdditionalLightData>();
                uint rLayers = uld ? uld.renderingLayers : (uint)l.renderingLayerMask;
                lightCsv.Add(string.Join("|", new[]
                {
                    rName == "PB_Interior" ? "PB" : "PA",
                    PathOf(l.transform),
                    l.gameObject.activeSelf.ToString(),
                    l.type.ToString(),
                    F(ToPlanX(p.z)), F(p.y), F(ToPlanZ(p.x)),
                    F(l.intensity), F(l.range), l.shadows.ToString(),
                    l.cullingMask.ToString(), rLayers.ToString()
                }));
            }
        }
        File.WriteAllLines(OutDir + "/luces_interiores_detalle.csv", lightCsv, Encoding.UTF8);

        sb.AppendLine("## Carcasas / Marcos / Difusores de luminarias en techo (PB_Interior y PA_Interior)");
        var fixCsv = new List<string> { "nivel|path|activeSelf|layer|renderingLayers|material|xPlanCenter|yMin|yMax|zPlanCenter|dxPlan|dzPlan" };
        foreach (string rName in new[] { "PB_Interior", "PA_Interior" })
        {
            var rGo = roots.FirstOrDefault(g => g.name == rName);
            if (rGo == null) continue;
            foreach (var r in rGo.GetComponentsInChildren<Renderer>(true).OrderBy(x => PathOf(x.transform)))
            {
                string p = PathOf(r.transform);
                string n = r.name;
                if (!(n.Contains("Panel") || n.Contains("Troffer") || n.Contains("Difusor") || n.Contains("LED") || n.Contains("Luminaria") || p.Contains("Paneles_LED_Plafon") || p.Contains("Iluminacion_PA_Mismo_Criterio_PB")))
                    continue;
                if (p.Contains("Aula_17/Plafon_Aula17_Fase2a_SUPUESTO/Paneles_60x60"))
                    continue;

                Bounds b = r.bounds;
                float x0 = ToPlanX(b.max.z), x1 = ToPlanX(b.min.z);
                float z0 = ToPlanZ(b.max.x), z1 = ToPlanZ(b.min.x);
                fixCsv.Add(string.Join("|", new[]
                {
                    rName == "PB_Interior" ? "PB" : "PA",
                    p,
                    r.gameObject.activeSelf.ToString(),
                    r.gameObject.layer.ToString(),
                    r.renderingLayerMask.ToString(),
                    r.sharedMaterial ? r.sharedMaterial.name : "null",
                    F((x0 + x1) * 0.5f), F(b.min.y), F(b.max.y), F((z0 + z1) * 0.5f),
                    F(x1 - x0), F(z1 - z0)
                }));
            }
        }
        File.WriteAllLines(OutDir + "/carcasas_luminarias_techo.csv", fixCsv, Encoding.UTF8);
        sb.AppendLine($"Total luces interiores volcadas: {lightCsv.Count - 1}");
        sb.AppendLine($"Total carcasas/difusores volcados: {fixCsv.Count - 1}");
        File.WriteAllText(OutDir + "/luminarias_y_modos.md", sb.ToString(), Encoding.UTF8);
    }

    private static void CaptureReadOnlyZoneViews()
    {
        // Luz y cámara temporales solo para lectura geométrica del estado ANTES (igual que Aula17CeilingPhase2A.Capture),
        // destruidas en finally y sin guardar jamás la escena.
        var origAmbMode = RenderSettings.ambientMode;
        var origSky = RenderSettings.ambientSkyColor;
        var origEq = RenderSettings.ambientEquatorColor;
        var origGr = RenderSettings.ambientGroundColor;
        bool origFog = RenderSettings.fog;

        var camGo = new GameObject("TEMP_Paso0_Camara_Inspeccion");
        var cam = camGo.AddComponent<Camera>();
        cam.enabled = false;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.14f, 0.15f, 0.17f, 1f);
        cam.nearClipPlane = 0.04f;
        cam.farClipPlane = 90f;
        cam.fieldOfView = 82f;
        var uacd = camGo.AddComponent<UniversalAdditionalCameraData>();
        uacd.renderPostProcessing = false;
        uacd.renderShadows = false;

        var lampGo = new GameObject("TEMP_Paso0_Luz_Inspeccion");
        var light = lampGo.AddComponent<Light>();
        light.type = LightType.Directional;
        light.intensity = 1.05f;
        light.color = new Color(0.98f, 0.97f, 0.95f, 1f);
        light.shadows = LightShadows.None;
        light.transform.rotation = Quaternion.LookRotation((Vector3.up * 0.85f + new Vector3(0.2f, 0f, 0.2f)).normalized);

        var rt = new RenderTexture(1440, 900, 24, RenderTextureFormat.ARGB32) { antiAliasing = 2 };
        var prevRt = RenderTexture.active;

        var shots = new (string file, float cx, float cy, float cz, float tx, float ty, float tz)[]
        {
            // Referencia aprobada Aula 17
            ("00_Ref_PA_Aula17_YaResuelta",          1.20f, 5.15f, 29.80f,  6.50f, 6.55f, 25.80f),
            // Planta Alta (PA, Y=6.80)
            ("01_PA_AulasOeste_Aula16_y_15",         1.20f, 5.15f, 37.80f,  7.20f, 6.55f, 33.50f),
            ("02_PA_AulasSur_Aula13_Centro",        10.50f, 5.15f, 47.20f, 15.80f, 6.55f, 52.00f),
            ("03_PA_AulasEste_Aula10_y_9",          26.80f, 5.15f, 37.50f, 20.50f, 6.55f, 33.20f),
            ("04_PA_PasilloCentral_y_PlazaSur",     14.24f, 5.20f, 24.50f, 14.24f, 6.55f, 38.00f),
            ("05_PA_LlegadaEscalera_y_Hueco",       15.80f, 5.20f, 19.30f,  5.00f, 6.45f, 19.30f),
            ("06_PA_Sec06_Salas_y_Canceles",        14.24f, 5.20f, 14.80f, 14.24f, 6.55f,  8.50f),
            ("07_PA_Norte_Banos_y_Cubiculos",       13.50f, 5.15f,  6.10f, 10.50f, 6.55f,  1.80f),
            // Planta Baja (PB, Y=3.20)
            ("08_PB_AulasOeste_Aula7_y_6",           1.50f, 1.65f, 29.80f,  7.50f, 2.98f, 25.50f),
            ("09_PB_AulasSur_Aula3_y_Aula5",        10.50f, 1.65f, 47.20f, 15.80f, 2.98f, 52.00f),
            ("10_PB_Auditorio_Plafon_y_Luces",      18.50f, 1.95f, 25.00f, 23.50f, 2.95f, 34.50f),
            ("11_PB_Vestibulo_y_PasilloCentral",    26.20f, 1.68f, 19.30f, 14.24f, 2.98f, 19.30f),
            ("12_PB_PasilloCentral_hacia_PlazaSur", 14.24f, 1.68f, 23.50f, 14.24f, 2.98f, 39.50f),
            ("13_PB_Sec06_y_Cubiculos_BanosNorte",  14.24f, 1.68f, 14.80f, 14.24f, 2.98f,  7.50f)
        };

        try
        {
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientSkyColor = new Color(0.36f, 0.37f, 0.39f, 1f);
            RenderSettings.fog = false;
            cam.targetTexture = rt;

            foreach (var s in shots)
            {
                camGo.transform.position = W(s.cx, s.cy, s.cz);
                camGo.transform.LookAt(W(s.tx, s.ty, s.tz));
                var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
                for (int w = 0; w < 4; w++)
                {
                    cam.Render();
                    RenderTexture.active = rt;
                    tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
                    tex.Apply();
                }
                File.WriteAllBytes(OutDir + "/capturas_antes/" + s.file + ".png", tex.EncodeToPNG());
                Object.DestroyImmediate(tex);
            }
        }
        finally
        {
            RenderSettings.ambientMode = origAmbMode;
            RenderSettings.ambientSkyColor = origSky;
            RenderSettings.ambientEquatorColor = origEq;
            RenderSettings.ambientGroundColor = origGr;
            RenderSettings.fog = origFog;

            cam.targetTexture = null;
            RenderTexture.active = prevRt;
            rt.Release();
            Object.DestroyImmediate(rt);
            Object.DestroyImmediate(camGo);
            Object.DestroyImmediate(lampGo);
        }
    }
}
