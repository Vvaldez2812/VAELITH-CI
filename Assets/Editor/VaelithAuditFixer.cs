using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Vaelith.Docencia2;

namespace Vaelith.Docencia2.Editor
{
    public static class VaelithAuditFixer
    {
        private const string MainScenePath = "Assets/Scenes/Exterior_Docencia2.unity";
        private const string InteriorTerrorProfilePath = "Assets/Settings/Docencia2_Interior_Terror_OscuridadExtrema_Profile.asset";

        public static void RunAllAuditFixesBatch()
        {
            var scene = EditorSceneManager.OpenScene(MainScenePath, OpenSceneMode.Single);
            if (!scene.IsValid())
            {
                Debug.LogError("[VaelithAuditFixer] No se pudo abrir Exterior_Docencia2.unity");
                return;
            }

            Debug.Log("=== [VaelithAuditFixer] INICIO DE CORRECCIONES ===");

            FixDuplicateTerrorAtmosphereGroups();
            RemoveMisplacedEmergencyAmberLights();
            RebuildCleanInteriorTerrorProfile();
            ConfigurePlayerInspectorSettings();
            AssignPAInteriorLayerToAllChildren();
            LockDirectorP29Door();
            CloseAuditorioDoorLintelGaps();
            RepositionPAGlassExtintores();
            AddStairAndGuardrailCollidersAndCheckSeam();
            FixInteriorFloorUVContinuity();
            FixDuplicateWallAndCabinetOverlap();
            DeduplicateBathroomCabinsAndDoors();
            SoftenInteriorCeilingLightsNoche();
            RespaceAllClassroomsAndAuditorio();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("=== [VaelithAuditFixer] CORRECCIONES GUARDADAS EXITOSAMENTE ===");
        }

        // H03: Mantener una sola copia de Terror_OscuridadExtrema_Interior_Grupo y destruir las 13 copias huérfanas
        private static void FixDuplicateTerrorAtmosphereGroups()
        {
            var darkness = UnityEngine.Object.FindAnyObjectByType<Docencia2TerrorDarknessController>();
            GameObject activeRoot = darkness != null ? darkness.terrorAtmosphereRoot : null;

            var roots = UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects()
                .Where(go => go.name == "Terror_OscuridadExtrema_Interior_Grupo")
                .ToList();

            if (activeRoot == null && roots.Count > 0)
            {
                activeRoot = roots[0];
                if (darkness != null) darkness.terrorAtmosphereRoot = activeRoot;
            }

            int removed = 0;
            foreach (var go in roots)
            {
                if (go == activeRoot)
                {
                    go.SetActive(false); // Inactivo en EditMode/NOCHE; el controlador lo enciende en TERROR interior
                    continue;
                }
                UnityEngine.Object.DestroyImmediate(go);
                removed++;
            }

            // Reparar referencias en darkness controller si alguna apuntara fuera del activeRoot
            if (darkness != null && activeRoot != null)
            {
                darkness.emergencyAmberLights.RemoveAll(l => l == null);
                darkness.windowMoonGlowLights.RemoveAll(l => l == null);
                EditorUtility.SetDirty(darkness);
            }

            Debug.Log($"[H03] Terror_OscuridadExtrema_Interior_Grupo: conservada 1 raíz, eliminadas {removed} copias duplicadas.");
        }

        // Limpiar los 5 {fileID: 0} rotos del VolumeProfile interior de TERROR y dejar valores anti-destello
        private static void RebuildCleanInteriorTerrorProfile()
        {
            var darkness = UnityEngine.Object.FindAnyObjectByType<Docencia2TerrorDarknessController>();
            VolumeProfile profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(InteriorTerrorProfilePath);
            if (profile == null && darkness != null && darkness.interiorTerrorVolume != null)
            {
                profile = darkness.interiorTerrorVolume.sharedProfile;
            }
            if (profile == null)
            {
                Debug.LogWarning("[Volume] No se encontró Docencia2_Interior_Terror_OscuridadExtrema_Profile.asset");
                return;
            }

            // Remover sub-assets nulos o reconstruir limpiamente los componentes
            for (int i = profile.components.Count - 1; i >= 0; i--)
            {
                if (profile.components[i] == null)
                {
                    profile.components.RemoveAt(i);
                }
            }

            if (!profile.TryGet<ColorAdjustments>(out var colorAdj))
            {
                colorAdj = profile.Add<ColorAdjustments>(true);
            }
            colorAdj.postExposure.Override(-0.10f);
            colorAdj.contrast.Override(22f);
            colorAdj.colorFilter.Override(new Color(0.88f, 0.93f, 0.98f, 1f));
            colorAdj.saturation.Override(-28f);

            if (!profile.TryGet<LiftGammaGain>(out var lgg))
            {
                lgg = profile.Add<LiftGammaGain>(true);
            }
            lgg.lift.Override(new Vector4(1.00f, 1.01f, 1.02f, 0.004f));
            lgg.gamma.Override(new Vector4(1.00f, 1.00f, 1.01f, 0.0f));

            if (!profile.TryGet<Bloom>(out var bloom))
            {
                bloom = profile.Add<Bloom>(true);
            }
            bloom.active = true;
            bloom.threshold.Override(1.38f);
            bloom.intensity.Override(0.22f);
            bloom.scatter.Override(0.55f);

            // Asegurar que los componentes creados queden embebidos en el asset
            string profPath = AssetDatabase.GetAssetPath(profile);
            if (!string.IsNullOrEmpty(profPath))
            {
                var existingSubs = AssetDatabase.LoadAllAssetsAtPath(profPath);
                foreach (var comp in profile.components)
                {
                    if (comp != null && !existingSubs.Contains(comp))
                    {
                        AssetDatabase.AddObjectToAsset(comp, profile);
                    }
                    if (comp != null) EditorUtility.SetDirty(comp);
                }
            }

            EditorUtility.SetDirty(profile);
            Debug.Log($"[Volume] Profile '{profile.name}' reparado con {profile.components.Count} componentes válidos (Bloom threshold 1.38).");
        }

        // H04 & Decisión #7: Calibrar linterna en escena y ocultar HUD/atajos QA por defecto
        private static void ConfigurePlayerInspectorSettings()
        {
            var player = UnityEngine.Object.FindAnyObjectByType<Docencia2FirstPersonPlayer>();
            if (player == null)
            {
                Debug.LogWarning("[Player] No se encontró Docencia2FirstPersonPlayer.");
                return;
            }

            player.showDebugHudAndShortcuts = false;
            player.flashlightBaseIntensity = 38.0f;
            player.flashlightRange = 8.5f;
            player.flashlightOuterAngle = 34.0f;
            player.flashlightInnerAngle = 10.0f;
            player.flashlightSpillBaseIntensity = 1.15f;
            player.volumetricBeamBaseAlpha = 0.018f;

            if (player.phoneSpotLight != null)
            {
                player.phoneSpotLight.intensity = 38.0f;
                player.phoneSpotLight.range = 8.5f;
                player.phoneSpotLight.spotAngle = 34.0f;
                player.phoneSpotLight.innerSpotAngle = 10.0f;
                player.phoneSpotLight.shadows = LightShadows.Soft;
                EditorUtility.SetDirty(player.phoneSpotLight);
            }

            if (player.flashlightSpillLight != null)
            {
                player.flashlightSpillLight.intensity = 1.15f;
                EditorUtility.SetDirty(player.flashlightSpillLight);
            }

            EditorUtility.SetDirty(player);
            Debug.Log("[H04/#7] Jugador actualizado: showDebugHudAndShortcuts=false, linterna intensity=38.0, range=8.5, spill=1.15.");
        }

        private static GameObject FindEvenInactive(string path)
        {
            if (string.IsNullOrEmpty(path)) return null;
            string[] parts = path.Split('/');
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (!scene.IsValid()) return null;

            Transform cur = null;
            foreach (var root in scene.GetRootGameObjects())
            {
                if (root.name == parts[0])
                {
                    cur = root.transform;
                    break;
                }
            }
            if (cur == null) return null;

            for (int i = 1; i < parts.Length; i++)
            {
                cur = cur.Find(parts[i]);
                if (cur == null) return null;
            }
            return cur.gameObject;
        }

        // H06: Reasignar los 19 objetos de PA_Interior en Default (0) hacia capa 30 (PA_Interior)
        private static void AssignPAInteriorLayerToAllChildren()
        {
            int paLayer = LayerMask.NameToLayer("PA_Interior");
            if (paLayer < 0)
            {
                Debug.LogWarning("[H06] Capa 'PA_Interior' no encontrada en TagManager; usando capa 30.");
                paLayer = 30;
            }

            var paRoot = FindEvenInactive("PA_Interior");
            if (paRoot == null) return;

            int changed = 0;
            foreach (var t in paRoot.GetComponentsInChildren<Transform>(true))
            {
                if (t.gameObject.layer == 0)
                {
                    t.gameObject.layer = paLayer;
                    EditorUtility.SetDirty(t.gameObject);
                    changed++;
                }
            }
            Debug.Log($"[H06] PA_Interior: {changed} objetos reasignados de Default(0) a capa {paLayer} ('{LayerMask.LayerToName(paLayer)}').");
        }

        // Decisión #3 / H10: Mantener Cubículo del Director (P29) permanentemente cerrado con llave
        private static void LockDirectorP29Door()
        {
            var dirGo = GameObject.Find("PA_Interior/Director/Madera_Division/P29_Hueco096_Abre_este/Bisagra_Giro90_Interior");
            if (dirGo == null)
            {
                Debug.LogWarning("[#3] No se encontró PA_Interior/Director/.../P29_Hueco096_Abre_este/Bisagra_Giro90_Interior.");
                return;
            }

            var door = dirGo.GetComponent<Docencia2InteractiveDoor>();
            if (door != null)
            {
                door.SetOpen(false, false);
                door.isLocked = true;
                EditorUtility.SetDirty(door);
                EditorUtility.SetDirty(dirGo);
                Debug.Log("[#3] P29_Director bloqueada permanentemente (isLocked=true, isOpen=false).");
            }
        }

        // H05: Cerrar la rendija abierta de 20 cm (Y=2.30..2.50) arriba de P24 y P25 del Auditorio PB
        private static void CloseAuditorioDoorLintelGaps()
        {
            string[] names =
            {
                "PB_Interior/24_Auditorio/Muro_Div_Vestibulo_Auditorio_z23.14_Dintel_P24_Auditorio_Norte_A",
                "PB_Interior/24_Auditorio/Muro_Div_Vestibulo_Auditorio_z23.14_Dintel_P25_Auditorio_Norte_B"
            };

            foreach (string path in names)
            {
                var go = GameObject.Find(path);
                if (go == null)
                {
                    Debug.LogWarning($"[H05] No encontrado {path}");
                    continue;
                }
                var mr = go.GetComponent<MeshRenderer>();
                if (mr == null) continue;

                Bounds b = mr.bounds;
                float currentH = b.size.y;
                float targetMinY = 2.30f;
                float targetMaxY = b.max.y; // ~3.20
                if (b.min.y > targetMinY + 0.01f && currentH > 0.05f)
                {
                    float targetH = targetMaxY - targetMinY;
                    float targetCenterY = (targetMinY + targetMaxY) * 0.5f;
                    Vector3 s = go.transform.localScale;
                    s.y *= (targetH / currentH);
                    go.transform.localScale = s;

                    Vector3 wp = go.transform.position;
                    wp.y += (targetCenterY - b.center.y);
                    go.transform.position = wp;

                    EditorUtility.SetDirty(go.transform);
                    Physics.SyncTransforms();
                    Debug.Log($"[H05] {go.name} ajustado a Y=[{mr.bounds.min.y:F3}..{mr.bounds.max.y:F3}].");
                }
                else
                {
                    Debug.Log($"[H05] {go.name} ya estaba cerrado en Y=[{b.min.y:F3}..{b.max.y:F3}].");
                }
            }
        }

        // H07: Mover los 2 Extintor_PA_FOTO_SUPUESTO fuera de los canceles de vidrio hacia muro sólido adyacente
        private static void RepositionPAGlassExtintores()
        {
            var container = GameObject.Find("PA_Interior/Detalles_Fotos_Revision4/Mobiliario_PA_SUPUESTO/Pasillos_F02_F03_F23_F35_F44");
            if (container == null) return;

            var redesWall = GameObject.Find("PA_Interior/Redes_SITE/Sur/Muro");
            var maestrosWall = GameObject.Find("PA_Interior/Sala_Maestros/Sur/Muro");
            Bounds? rwB = redesWall != null ? redesWall.GetComponent<Renderer>()?.bounds : null;
            Bounds? mwB = maestrosWall != null ? maestrosWall.GetComponent<Renderer>()?.bounds : null;
            Debug.Log($"[H07] Muros referencia PA z=15: RedesSur={(rwB.HasValue ? rwB.Value.ToString() : "null")}, MaestrosSur={(mwB.HasValue ? mwB.Value.ToString() : "null")}");

            var extintores = new List<Transform>();
            foreach (Transform c in container.transform)
            {
                if (c.name == "Extintor_PA_FOTO_SUPUESTO")
                    extintores.Add(c);
            }

            foreach (var ext in extintores)
            {
                var rends = ext.GetComponentsInChildren<Renderer>(true);
                if (rends.Length == 0) continue;
                Bounds eb = rends[0].bounds;
                for (int i = 1; i < rends.Length; i++) eb.Encapsulate(rends[i].bounds);

                Vector2 planCenter = Docencia2FirstPersonPlayer.UnityToPlanXZ(eb.center);
                if (planCenter.y < 14.0f || planCenter.y > 16.5f) continue;

                // Colocar ambos extintores sobre la cara SUR sólida (Z=15.488 -> muro foyer abierto Z>15.49)
                // justo afuera de los canceles de vidrio de Redes_SITE (X<=11.24) y Sala_Maestros (X>=17.24):
                bool isWest = planCenter.x < 14.24f;
                float targetPlanX = isWest ? 10.65f : 17.83f;
                float targetPlanZ = 15.60f; // respaldo pegado al muro sólido Z=15.488 mirando hacia el sur al pasillo central Z>15.5

                // Rotar para que la etiqueta (F5_Papel) mire al sur (+Z_plan = -X_unity)
                Transform rojoT = ext.Find("F5_Rojo");
                Transform papelT = ext.Find("F5_Papel");
                if (rojoT != null && papelT != null)
                {
                    Vector3 curFrontUnity = papelT.GetComponent<Renderer>().bounds.center - rojoT.GetComponent<Renderer>().bounds.center;
                    curFrontUnity.y = 0f;
                    if (curFrontUnity.sqrMagnitude > 0.0001f)
                    {
                        float yawDelta = Vector3.SignedAngle(curFrontUnity.normalized, Vector3.left, Vector3.up); // Vector3.left = -X_unity = +Z_plan
                        ext.Rotate(0f, yawDelta, 0f, Space.World);
                        Physics.SyncTransforms();
                    }
                }

                Bounds nbRot = rends[0].bounds;
                for (int i = 1; i < rends.Length; i++) nbRot.Encapsulate(rends[i].bounds);

                Vector3 desiredUnityCenter = Docencia2FirstPersonPlayer.PlanToUnityPos(targetPlanX, nbRot.center.y, targetPlanZ);
                ext.position += (desiredUnityCenter - nbRot.center);
                EditorUtility.SetDirty(ext);

                Bounds nb = rends[0].bounds;
                for (int i = 1; i < rends.Length; i++) nb.Encapsulate(rends[i].bounds);
                Vector2 newPlan = Docencia2FirstPersonPlayer.UnityToPlanXZ(nb.center);
                Debug.Log($"[H07] Movido {ext.name} ({(isWest ? "Oeste" : "Este")}): plan({planCenter.x:F2},{planCenter.y:F2}) -> plan({newPlan.x:F2},{newPlan.y:F2}).");
            }
        }

        // H08 / Decisión #1: NO mover escalera; añadir MeshCollider a muretes inclinados/rampas/barandales y sellar posible junta en Tramo 1/Descanso
        private static void AddStairAndGuardrailCollidersAndCheckSeam()
        {
            var escRoot = GameObject.Find("PB_Interior/22_Cubo_Escaleras_PB/Escalera_En_U_Concreto_y_Barandal");
            int added = 0;
            if (escRoot != null)
            {
                foreach (var mf in escRoot.GetComponentsInChildren<MeshFilter>(true))
                {
                    if (mf.sharedMesh == null) continue;
                    string n = mf.gameObject.name;
                    if (n.StartsWith("Murete_Gualdera_") ||
                        n.StartsWith("Rampa_Estructural_") ||
                        n.Contains("Barandal_"))
                    {
                        if (mf.GetComponent<Collider>() == null)
                        {
                            var mc = mf.gameObject.AddComponent<MeshCollider>();
                            mc.sharedMesh = mf.sharedMesh;
                            mc.convex = false;
                            EditorUtility.SetDirty(mf.gameObject);
                            added++;
                        }
                    }
                }

                // Verificar unión Tramo 1 paso superior / Descanso (línea brillante de 3 cm entre Y=1.730 y Y=1.760 en Escalera_TERROR_19.png)
                var descanso = escRoot.transform.Find("Descanso_Intermedio_Y1.90_x1.36_4.40");
                if (descanso != null)
                {
                    var dMr = descanso.GetComponent<MeshRenderer>();
                    if (dMr != null && dMr.bounds.min.y > 1.725f)
                    {
                        Bounds db = dMr.bounds;
                        float targetMinY = 1.720f;
                        float targetMaxY = db.max.y; // 1.900 exacto sin alterar altura de pisada
                        float newH = targetMaxY - targetMinY;
                        float newCenterY = (targetMinY + targetMaxY) * 0.5f;
                        Vector3 s = descanso.localScale;
                        s.y *= (newH / db.size.y);
                        descanso.localScale = s;
                        Vector3 wp = descanso.position;
                        wp.y += (newCenterY - db.center.y);
                        descanso.position = wp;
                        EditorUtility.SetDirty(descanso);
                        Physics.SyncTransforms();
                        Debug.Log($"[H08] Descanso_Intermedio sellado inferiormente a Y=[{dMr.bounds.min.y:F3}..{dMr.bounds.max.y:F3}] (sin mover pisada Y={dMr.bounds.max.y:F3}).");
                    }
                }
            }

            var paLlegada = GameObject.Find("PA_Interior/Llegada_Escalera/Cierre_Losa_y_Barandales_Hueco_Escalera_PA");
            if (paLlegada != null)
            {
                foreach (var mf in paLlegada.GetComponentsInChildren<MeshFilter>(true))
                {
                    if (mf.sharedMesh == null) continue;
                    string n = mf.gameObject.name;
                    if (n.StartsWith("Barandal_") || n.StartsWith("Poste_") || n.StartsWith("Murete_"))
                    {
                        if (mf.GetComponent<Collider>() == null)
                        {
                            var mc = mf.gameObject.AddComponent<MeshCollider>();
                            mc.sharedMesh = mf.sharedMesh;
                            mc.convex = false;
                            EditorUtility.SetDirty(mf.gameObject);
                            added++;
                        }
                    }
                }
            }

            Debug.Log($"[H08/#1] Colliders de escalera y barandales PA agregados: {added} (sin mover la escalera).");
        }

        // H10 & H11: Eliminar muro duplicado en Cubiculo_A3/Cierre_Sec7 y levantar 2 mm Gabinete_Alto_Claro en Aula_16_Taller
        private static void FixDuplicateWallAndCabinetOverlap()
        {
            var dupWall = GameObject.Find("PA_Interior/Cubiculo_A3/Cierre_Sec7");
            if (dupWall != null)
            {
                UnityEngine.Object.DestroyImmediate(dupWall);
                Debug.Log("[H11] Eliminado muro duplicado PA_Interior/Cubiculo_A3/Cierre_Sec7.");
            }

            var gab = GameObject.Find("PA_Interior/Detalles_Fotos_Revision4/Mobiliario_PA_SUPUESTO/Taller16_F07_F14/Gabinete_Alto_Claro");
            if (gab != null)
            {
                var mr = gab.GetComponent<Renderer>();
                if (mr != null && mr.bounds.min.y < 3.6065f)
                {
                    float lift = 3.6068f - mr.bounds.min.y;
                    gab.transform.position += Vector3.up * lift;
                    EditorUtility.SetDirty(gab.transform);
                    Debug.Log($"[H10] Gabinete_Alto_Claro elevado +{lift:F4} m para eliminar solape de 1 mm con piso PA.");
                }
            }
        }

        // H11: Separar mallas fijas vs hojas móviles en cabinas de baño (PB & PA) con bounds compactados sin solape
        private static void DeduplicateBathroomCabinsAndDoors()
        {
            string[] bathRoots =
            {
                "PB_Interior/05_Fase5_Mobiliario_PB/Bano_Mujeres",
                "PB_Interior/05_Fase5_Mobiliario_PB/Bano_Hombres",
                "PA_Interior/Bano_Mujeres/Mobiliario_PA_SUPUESTO",
                "PA_Interior/Bano_Hombres/Mobiliario_PA_SUPUESTO"
            };

            int fixedCount = 0;
            foreach (string rootPath in bathRoots)
            {
                var rootGo = GameObject.Find(rootPath);
                if (rootGo == null) continue;

                bool isPA = rootPath.StartsWith("PA_");
                bool isMuj = rootPath.Contains("Bano_Mujeres");
                string prefix = (isPA ? "PA_" : "PB_") + (isMuj ? "M_" : "H_");

                // 1) Compactar cada hoja móvil (Hoja_F5_Crema / Hoja_F5_CromoBano) usando solo vértices referenciados por sus triángulos
                //    y en PB reasignar Cabina_k_SUPUESTO a FaseA_Cabina_Fija_PB_* compactada si existe.
                for (int k = 1; k <= (isMuj ? 3 : 1); k++)
                {
                    Transform cabT = rootGo.transform.Find($"Cabina_{k}_SUPUESTO");
                    Transform doorRoot = rootGo.transform.Find($"Puerta_Cabina_{prefix}{k}");
                    if (cabT == null || doorRoot == null) continue;

                    Transform hingeT = doorRoot.Find("Bisagra_Cabina_90");
                    if (hingeT == null) continue;

                    // Obtener límites reales usados por la hoja de puerta en mundo
                    Bounds? doorUsedWorldBounds = null;
                    foreach (string leafPart in new[] { "Hoja_F5_Crema", "Hoja_F5_CromoBano" })
                    {
                        var leafT = hingeT.Find(leafPart);
                        var leafMf = leafT != null ? leafT.GetComponent<MeshFilter>() : null;
                        if (leafMf != null && leafMf.sharedMesh != null)
                        {
                            Mesh compactLeaf = BuildCompactedMeshAssetIfNeeded(leafMf.sharedMesh, $"Assets/Models/Docencia2/Cleaned_{prefix}{k}_{leafPart}.asset");
                            if (compactLeaf != null && compactLeaf != leafMf.sharedMesh)
                            {
                                leafMf.sharedMesh = compactLeaf;
                                EditorUtility.SetDirty(leafMf);
                            }
                            var lMr = leafT.GetComponent<MeshRenderer>();
                            if (lMr != null)
                            {
                                doorUsedWorldBounds = doorUsedWorldBounds.HasValue
                                    ? Encapsulate(doorUsedWorldBounds.Value, lMr.bounds)
                                    : lMr.bounds;
                            }
                        }
                    }

                    // Para PB, cambiar de F5_CabinaBano completo a FaseA_Cabina_Fija_PB_* o recortar triángulos que caigan dentro de la hoja móvil
                    foreach (string fixedPart in new[] { "F5_Crema", "F5_CromoBano" })
                    {
                        var partT = cabT.Find(fixedPart);
                        var partMf = partT != null ? partT.GetComponent<MeshFilter>() : null;
                        if (partMf == null || partMf.sharedMesh == null) continue;

                        Mesh sourceMesh = partMf.sharedMesh;
                        if (!isPA)
                        {
                            string candidatePath = $"Assets/Models/Docencia2/FaseA_Cabina_Fija_{prefix}{k}_{fixedPart}.asset";
                            var candidate = AssetDatabase.LoadAssetAtPath<Mesh>(candidatePath);
                            if (candidate != null)
                            {
                                sourceMesh = candidate;
                            }
                        }

                        // Compactar vértices (y en caso de que aún contuviera triángulos superpuestos a la hoja móvil en >95% de coincidencia, filtrarlos)
                        Mesh cleanedFixed = BuildFilteredFixedCabinMesh(
                            sourceMesh,
                            partT,
                            hingeT.Find("Hoja_F5_Crema")?.GetComponent<MeshFilter>(),
                            hingeT.Find("Hoja_F5_CromoBano")?.GetComponent<MeshFilter>(),
                            $"Assets/Models/Docencia2/Cleaned_CabinaFija_{prefix}{k}_{fixedPart}.asset");

                        if (cleanedFixed != null)
                        {
                            partMf.sharedMesh = cleanedFixed;
                            EditorUtility.SetDirty(partMf);
                            fixedCount++;
                        }
                    }

                    // Reportar resultado exacto de bounds tras compactado
                    var cabMr = cabT.Find("F5_Crema")?.GetComponent<MeshRenderer>();
                    var hojaMr = hingeT.Find("Hoja_F5_Crema")?.GetComponent<MeshRenderer>();
                    if (cabMr != null && hojaMr != null)
                    {
                        Debug.Log($"[H11] {prefix}{k}: CabinaFija bounds={cabMr.bounds} vs HojaPuerta bounds={hojaMr.bounds}");
                    }
                }
            }

            Debug.Log($"[H11] Mallas de cabinas de baño separadas/compactadas: {fixedCount} sub-mallas.");
        }

        private static Bounds Encapsulate(Bounds a, Bounds b)
        {
            a.Encapsulate(b);
            return a;
        }

        private static Mesh BuildCompactedMeshAssetIfNeeded(Mesh src, string outPath)
        {
            if (src == null) return null;
            int[] tris = src.triangles;
            Vector3[] verts = src.vertices;
            var used = new HashSet<int>(tris);
            if (used.Count == verts.Length) return src; // Ya compacto

            return CreateCompactedMeshAsset(src, tris, outPath);
        }

        private static Mesh BuildFilteredFixedCabinMesh(Mesh src, Transform fixedT, MeshFilter leafA, MeshFilter leafB, string outPath)
        {
            if (src == null) return null;

            // Recopilar centroides de triángulos de las mallas de hoja móvil en coordenadas mundo
            var leafCentroids = new List<Vector3>();
            foreach (var lmf in new[] { leafA, leafB })
            {
                if (lmf == null || lmf.sharedMesh == null) continue;
                Vector3[] lv = lmf.sharedMesh.vertices;
                int[] lt = lmf.sharedMesh.triangles;
                for (int i = 0; i < lt.Length; i += 3)
                {
                    Vector3 c = (lv[lt[i]] + lv[lt[i + 1]] + lv[lt[i + 2]]) / 3f;
                    leafCentroids.Add(lmf.transform.TransformPoint(c));
                }
            }

            Vector3[] sv = src.vertices;
            int[] st = src.triangles;
            var keptTris = new List<int>(st.Length);
            int removedTris = 0;

            for (int i = 0; i < st.Length; i += 3)
            {
                int i0 = st[i], i1 = st[i + 1], i2 = st[i + 2];
                Vector3 wc = fixedT.TransformPoint((sv[i0] + sv[i1] + sv[i2]) / 3f);
                bool matchesDoorLeaf = false;
                for (int j = 0; j < leafCentroids.Count; j++)
                {
                    if ((wc - leafCentroids[j]).sqrMagnitude < 0.0001f) // < 1 cm exact duplicate triangle
                    {
                        matchesDoorLeaf = true;
                        break;
                    }
                }
                if (matchesDoorLeaf)
                {
                    removedTris++;
                }
                else
                {
                    keptTris.Add(i0);
                    keptTris.Add(i1);
                    keptTris.Add(i2);
                }
            }

            if (removedTris == 0 && new HashSet<int>(st).Count == sv.Length)
            {
                return src;
            }

            if (keptTris.Count == 0)
            {
                Debug.LogWarning($"[H11] {fixedT.name}: Todos los triángulos coinciden con la hoja; se conservan los no filtrados.");
                keptTris.AddRange(st);
            }

            return CreateCompactedMeshAsset(src, keptTris.ToArray(), outPath);
        }

        private static Mesh CreateCompactedMeshAsset(Mesh src, int[] tris, string outPath)
        {
            Vector3[] oldV = src.vertices;
            Vector3[] oldN = src.normals;
            Vector2[] oldUv = src.uv;
            Vector4[] oldTan = src.tangents;

            bool hasN = oldN != null && oldN.Length == oldV.Length;
            bool hasUv = oldUv != null && oldUv.Length == oldV.Length;
            bool hasTan = oldTan != null && oldTan.Length == oldV.Length;

            var remap = new Dictionary<int, int>();
            var newV = new List<Vector3>();
            var newN = new List<Vector3>();
            var newUv = new List<Vector2>();
            var newTan = new List<Vector4>();
            int[] newTris = new int[tris.Length];

            for (int i = 0; i < tris.Length; i++)
            {
                int o = tris[i];
                if (!remap.TryGetValue(o, out int n))
                {
                    n = newV.Count;
                    remap[o] = n;
                    newV.Add(oldV[o]);
                    if (hasN) newN.Add(oldN[o]);
                    if (hasUv) newUv.Add(oldUv[o]);
                    if (hasTan) newTan.Add(oldTan[o]);
                }
                newTris[i] = n;
            }

            var mesh = new Mesh { name = System.IO.Path.GetFileNameWithoutExtension(outPath) };
            mesh.SetVertices(newV);
            if (hasN) mesh.SetNormals(newN);
            if (hasUv) mesh.SetUVs(0, newUv);
            if (hasTan) mesh.SetTangents(newTan);
            mesh.SetTriangles(newTris, 0);
            mesh.RecalculateBounds();
            if (!hasN) mesh.RecalculateNormals();

            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(outPath);
            if (existing != null)
            {
                EditorUtility.CopySerialized(mesh, existing);
                EditorUtility.SetDirty(existing);
                return existing;
            }
            AssetDatabase.CreateAsset(mesh, outPath);
            return mesh;
        }

        // H04 / H12: Bajar levemente luces de plafón PB y PA en NOCHE para evitar halos blancos quemados en techos/muros
        private static void SoftenInteriorCeilingLightsNoche()
        {
            int adjusted = 0;

            var pbGroup = FindEvenInactive("PB_Interior/04_Fase4_Acabados_e_Iluminacion/Luces_Interiores_PB_Noche");
            if (pbGroup != null)
            {
                foreach (var l in pbGroup.GetComponentsInChildren<Light>(true))
                {
                    if (l.type != LightType.Point) continue;
                    if (l.transform.position.y > 2.94f && l.transform.position.y < 3.25f)
                    {
                        Vector3 p = l.transform.position;
                        p.y = 2.86f; // 32 cm abajo del plafón Y=3.18
                        l.transform.position = p;
                    }
                    if (l.intensity > 2.65f)
                    {
                        l.intensity *= 0.72f;
                    }
                    EditorUtility.SetDirty(l.transform);
                    EditorUtility.SetDirty(l);
                    adjusted++;
                }
            }

            var paNoche = FindEvenInactive("PA_Interior/Iluminacion_PA_Mismo_Criterio_PB/NOCHE");
            if (paNoche != null)
            {
                foreach (var l in paNoche.GetComponentsInChildren<Light>(true))
                {
                    if (l.type != LightType.Point) continue;
                    if (l.transform.position.y > 6.42f && l.transform.position.y < 6.80f)
                    {
                        Vector3 p = l.transform.position;
                        p.y = 6.32f; // 48 cm abajo del plafón Y=6.80
                        l.transform.position = p;
                    }
                    if (l.intensity > 2.3f)
                    {
                        l.intensity = l.intensity >= 3.0f ? 2.15f : 1.85f;
                    }
                    EditorUtility.SetDirty(l.transform);
                    EditorUtility.SetDirty(l);
                    adjusted++;
                }
            }

            Debug.Log($"[H04/H12] Ajustadas {adjusted} luminarias interiores NOCHE (PB y PA) para eliminar quemado blanco en plafones.");
        }

        // H09 & Decisión #5: Abrir pasillos >= 0.74 m en Aulas 1..15 y pasillo central >= 1.4 m en Auditorio PB
        public static void RespaceAllClassroomsAndAuditorio()
        {
            RespaceAuditorioButacas();

            for (int num = 1; num <= 15; num++)
            {
                RespaceClassroom(num);
            }
        }

        private static void RespaceAuditorioButacas()
        {
            var container = GameObject.Find("PB_Interior/06_Auditorio_PB/Butacas_Centrales_80_SUPUESTO");
            if (container == null) return;

            // Gradas X interior: 17.382 .. 27.990 (ancho libre 10.608 m)
            // Mantener los 10 asientos por fila (80 totales) en sus 8 gradas exactas, divididos en bloque Izq (5) + Pasillo Central + bloque Der (5)
            // Cada butaca tiene ancho 0.66 m (semiancho 0.33 m).
            float[] blockX = { 18.76f, 19.46f, 20.16f, 20.86f, 21.56f, 23.81f, 24.51f, 25.21f, 25.91f, 26.61f };
            // Pasillo Oeste = 18.43 - 17.38 = 1.05 m | Pasillo Central = 23.48 - 21.89 = 1.59 m | Pasillo Este = 27.99 - 26.94 = 1.05 m!

            int moved = 0;
            for (int f = 1; f <= 8; f++)
            {
                for (int c = 1; c <= 10; c++)
                {
                    var chairT = container.transform.Find($"Butaca_F{f}_C{c}_SUPUESTO");
                    if (chairT == null) continue;

                    var rends = chairT.GetComponentsInChildren<Renderer>(true);
                    if (rends.Length == 0) continue;
                    Bounds cb = rends[0].bounds;
                    for (int i = 1; i < rends.Length; i++) cb.Encapsulate(rends[i].bounds);

                    Vector2 curPlan = Docencia2FirstPersonPlayer.UnityToPlanXZ(cb.center);
                    Vector3 targetWorld = Docencia2FirstPersonPlayer.PlanToUnityPos(blockX[c - 1], cb.center.y, curPlan.y);
                    chairT.position += (targetWorld - cb.center);
                    EditorUtility.SetDirty(chairT);
                    moved++;
                }
            }
            Debug.Log($"[H09/#5] Auditorio PB: {moved} butacas reacomodadas (5 izq + pasillo central 1.59 m + 5 der, pasillos laterales 1.05 m).");
        }

        private static void RespaceClassroom(int num)
        {
            bool isPB = num <= 7;
            GameObject parentObj = isPB
                ? GameObject.Find($"PB_Interior/05_Fase5_Mobiliario_PB/Aula_{num}")
                : GameObject.Find($"PA_Interior/Aula_{num}/Mobiliario_PA_SUPUESTO");

            if (parentObj == null)
            {
                Debug.LogWarning($"[H09] No encontrada carpeta de mobiliario para Aula_{num}.");
                return;
            }

            // Obtener límites de piso del aula en Plan XZ
            Bounds floorBounds;
            if (!TryGetClassroomFloorBounds(num, out floorBounds))
            {
                Debug.LogWarning($"[H09] No se pudo determinar piso de Aula_{num}.");
                return;
            }

            Vector2 fMinPlan = Docencia2FirstPersonPlayer.UnityToPlanXZ(new Vector3(floorBounds.max.x, 0f, floorBounds.max.z)); // Note: UnityToPlanXZ inverts axes!
            Vector2 fMaxPlan = Docencia2FirstPersonPlayer.UnityToPlanXZ(new Vector3(floorBounds.min.x, 0f, floorBounds.min.z));
            float wallMinX = Mathf.Min(fMinPlan.x, fMaxPlan.x) + 0.08f;
            float wallMaxX = Mathf.Max(fMinPlan.x, fMaxPlan.x) - 0.08f;
            float wallMinZ = Mathf.Min(fMinPlan.y, fMaxPlan.y) + 0.08f;
            float wallMaxZ = Mathf.Max(fMinPlan.y, fMaxPlan.y) - 0.08f;

            // Recoger pupitres ordenados
            var desks = new List<Transform>();
            foreach (Transform ch in parentObj.transform)
            {
                if (ch.name.StartsWith("Pupitre_")) desks.Add(ch);
            }
            if (desks.Count == 0) return;

            // Medir tamaño real en planta del primer pupitre y determinar hacia qué pared mira el pizarrón
            Bounds firstDeskB = GetRendererBounds(desks[0]);
            Vector2 dPlanMin = Docencia2FirstPersonPlayer.UnityToPlanXZ(new Vector3(firstDeskB.max.x, 0f, firstDeskB.max.z));
            Vector2 dPlanMax = Docencia2FirstPersonPlayer.UnityToPlanXZ(new Vector3(firstDeskB.min.x, 0f, firstDeskB.min.z));
            float sizeX = Mathf.Abs(dPlanMax.x - dPlanMin.x); // ~0.70 o ~0.87
            float sizeZ = Mathf.Abs(dPlanMax.y - dPlanMin.y); // ~0.87 o ~0.70

            // Consultar dónde está Pizarron_Pared_Acceso_SUPUESTO
            Transform boardT = parentObj.transform.Find("Pizarron_Pared_Acceso_SUPUESTO");
            Vector2 boardPlan = boardT != null
                ? Docencia2FirstPersonPlayer.UnityToPlanXZ(GetRendererBounds(boardT).center)
                : new Vector2((wallMinX + wallMaxX) * 0.5f, wallMinZ);

            // Elegir pared frontal más cercana al pizarrón
            float dNorth = Mathf.Abs(boardPlan.y - wallMinZ);
            float dSouth = Mathf.Abs(boardPlan.y - wallMaxZ);
            float dWest = Mathf.Abs(boardPlan.x - wallMinX);
            float dEast = Mathf.Abs(boardPlan.x - wallMaxX);
            float minD = Mathf.Min(Mathf.Min(dNorth, dSouth), Mathf.Min(dWest, dEast));

            // Márgenes mínimos para caminar por frente (pizarrón/puerta/profesor), atrás y laterales:
            const float frontReserve = 1.90f;
            const float backReserve = 0.90f;
            const float sideReserve = 0.92f;
            const float minGap = 0.74f; // > 0.70 m requerido en #5 y >= radio+diámetro jugador (0.60 m)

            float useMinX = wallMinX + sideReserve;
            float useMaxX = wallMaxX - sideReserve;
            float useMinZ = wallMinZ + sideReserve;
            float useMaxZ = wallMaxZ - sideReserve;

            bool depthAlongZ;
            if (Mathf.Approximately(minD, dNorth))
            {
                useMinZ = wallMinZ + frontReserve;
                useMaxZ = wallMaxZ - backReserve;
                depthAlongZ = true;
            }
            else if (Mathf.Approximately(minD, dSouth))
            {
                useMinZ = wallMinZ + backReserve;
                useMaxZ = wallMaxZ - frontReserve;
                depthAlongZ = true;
            }
            else if (Mathf.Approximately(minD, dWest))
            {
                useMinX = wallMinX + frontReserve;
                useMaxX = wallMaxX - backReserve;
                depthAlongZ = false;
            }
            else
            {
                useMinX = wallMinX + backReserve;
                useMaxX = wallMaxX - frontReserve;
                depthAlongZ = false;
            }

            float availX = Mathf.Max(sizeX, useMaxX - useMinX);
            float availZ = Mathf.Max(sizeZ, useMaxZ - useMinZ);

            float reqPitchX = sizeX + minGap;
            float reqPitchZ = sizeZ + minGap;

            int colsX = Mathf.Max(2, Mathf.FloorToInt((availX + minGap) / reqPitchX));
            int rowsZ = Mathf.Max(2, Mathf.FloorToInt((availZ + minGap) / reqPitchZ));

            // Limitar al número máximo disponible (sin crear pupitres nuevos)
            while (colsX * rowsZ > desks.Count)
            {
                if (depthAlongZ && colsX > rowsZ) colsX--;
                else if (!depthAlongZ && rowsZ > colsX) rowsZ--;
                else if (colsX >= rowsZ && colsX > 2) colsX--;
                else rowsZ--;
            }

            float actualSpanX = colsX * sizeX + (colsX - 1) * minGap;
            float actualSpanZ = rowsZ * sizeZ + (rowsZ - 1) * minGap;
            float gapX = colsX > 1 ? Mathf.Max(minGap, (availX - colsX * sizeX) / (colsX - 1)) : 0f;
            float gapZ = rowsZ > 1 ? Mathf.Max(minGap, (availZ - rowsZ * sizeZ) / (rowsZ - 1)) : 0f;
            // Evitar sobre-estirar demasiado en aulas enormes: tope estético 1.15 m de pasillo, centrado dentro de zona útil
            gapX = Mathf.Min(gapX, 1.05f);
            gapZ = Mathf.Min(gapZ, 1.05f);

            float usedX = colsX * sizeX + (colsX - 1) * gapX;
            float usedZ = rowsZ * sizeZ + (rowsZ - 1) * gapZ;
            float startCenterX = (useMinX + useMaxX) * 0.5f - usedX * 0.5f + sizeX * 0.5f;
            float startCenterZ = (useMinZ + useMaxZ) * 0.5f - usedZ * 0.5f + sizeZ * 0.5f;

            // Posicionar los primeros colsX * rowsZ pupitres y retirar el excedente que impedía los pasillos
            int idx = 0;
            for (int rz = 0; rz < rowsZ; rz++)
            {
                for (int cx = 0; cx < colsX; cx++)
                {
                    float targetPlanX = startCenterX + cx * (sizeX + gapX);
                    float targetPlanZ = startCenterZ + rz * (sizeZ + gapZ);

                    Transform dT = desks[idx++];
                    Bounds db = GetRendererBounds(dT);
                    Vector2 curCenterPlan = Docencia2FirstPersonPlayer.UnityToPlanXZ(db.center);
                    Vector3 targetWorldCenter = Docencia2FirstPersonPlayer.PlanToUnityPos(targetPlanX, db.center.y, targetPlanZ);
                    dT.position += (targetWorldCenter - db.center);
                    EditorUtility.SetDirty(dT);
                }
            }

            int removedExtra = 0;
            for (int k = idx; k < desks.Count; k++)
            {
                UnityEngine.Object.DestroyImmediate(desks[k].gameObject);
                removedExtra++;
            }

            Debug.Log($"[H09/#5] Aula_{num:00} ({(isPB ? "PB" : "PA")}): {colsX}x{rowsZ}={idx} pupitres (retirados {removedExtra}), pasilloX={gapX:F2} m, pasilloZ={gapZ:F2} m, libre X=[{(startCenterX - sizeX * 0.5f - wallMinX):F2} / {(wallMaxX - (startCenterX - sizeX * 0.5f + usedX)):F2}], Z=[{(startCenterZ - sizeZ * 0.5f - wallMinZ):F2} / {(wallMaxZ - (startCenterZ - sizeZ * 0.5f + usedZ)):F2}].");
        }

        private static bool TryGetClassroomFloorBounds(int num, out Bounds b)
        {
            b = default;
            if (num <= 7)
            {
                // PB: buscar PB_Interior/*Aula_<num>/Piso_Aula_<num>_*
                var pb = GameObject.Find("PB_Interior");
                if (pb != null)
                {
                    string prefix = $"Piso_Aula_{num}_";
                    foreach (Transform t in pb.GetComponentsInChildren<Transform>(true))
                    {
                        if (t.name.StartsWith(prefix))
                        {
                            var r = t.GetComponent<Renderer>();
                            if (r != null) { b = r.bounds; return true; }
                        }
                    }
                }
            }
            else
            {
                var pisoT = GameObject.Find($"PA_Interior/Aula_{num}/Piso_Loseta_Acabado_6mm");
                if (pisoT != null)
                {
                    var r = pisoT.GetComponent<Renderer>();
                    if (r != null) { b = r.bounds; return true; }
                }
            }
            return false;
        }

        private static Bounds GetRendererBounds(Transform t)
        {
            var rs = t.GetComponentsInChildren<Renderer>(true);
            if (rs.Length == 0) return new Bounds(t.position, Vector3.zero);
            Bounds b = rs[0].bounds;
            for (int i = 1; i < rs.Length; i++) b.Encapsulate(rs[i].bounds);
            return b;
        }

        // Elimina las luces puntuales flotantes naranja/rojizas de emergencia sin luminaria en techo (Vestibulo PB, Escalera, Plaza Sur PA)
        private static void RemoveMisplacedEmergencyAmberLights()
        {
            int removed = 0;
            foreach (var l in UnityEngine.Object.FindObjectsByType<Light>(FindObjectsInactive.Include))
            {
                if (l == null) continue;
                string n = l.gameObject.name;
                bool isMisplacedEmergency = n.StartsWith("Luz_Emergencia_Ambar", StringComparison.OrdinalIgnoreCase);
                if (!isMisplacedEmergency && l.transform.parent != null &&
                    l.transform.parent.name == "Terror_OscuridadExtrema_Interior_Grupo" &&
                    l.type == LightType.Point &&
                    l.color.r > 0.85f && l.color.g < 0.50f && l.color.b < 0.20f)
                {
                    isMisplacedEmergency = true;
                }

                if (isMisplacedEmergency)
                {
                    UnityEngine.Object.DestroyImmediate(l.gameObject);
                    removed++;
                }
            }

            var darkness = UnityEngine.Object.FindAnyObjectByType<Docencia2TerrorDarknessController>();
            if (darkness != null)
            {
                darkness.emergencyAmberLights.Clear();
                EditorUtility.SetDirty(darkness);
            }

            Debug.Log($"[LuzTecho] Eliminadas {removed} luces puntuales rojizas flotantes de emergencia sin luminaria.");
        }

        // Unifica en espacio de mundo las UVs de pisos de loseta (0.66 m) y plafón en zonas reescaladas (PA Llegada_Escalera y Escalera PB)
        private static void FixInteriorFloorUVContinuity()
        {
            int updated = 0;
            foreach (string rootName in new[] { "PB_Interior", "PA_Interior" })
            {
                GameObject rootObj = FindRootObject(rootName);
                if (rootObj == null) continue;

                foreach (var mf in rootObj.GetComponentsInChildren<MeshFilter>(true))
                {
                    if (mf == null || mf.sharedMesh == null) continue;
                    string objName = mf.gameObject.name;
                    var mr = mf.GetComponent<MeshRenderer>();
                    string matName = (mr != null && mr.sharedMaterial != null) ? mr.sharedMaterial.name : "";

                    bool isTileFloor = objName.StartsWith("Piso_", StringComparison.Ordinal)
                        || objName.StartsWith("Descanso_Intermedio", StringComparison.Ordinal)
                        || objName.StartsWith("Tramo1_Norte_Escalon_", StringComparison.Ordinal)
                        || objName.StartsWith("Tramo2_Sur_Escalon_", StringComparison.Ordinal)
                        || matName.IndexOf("Piso_Loseta", StringComparison.OrdinalIgnoreCase) >= 0;

                    bool isResizedLlegadaCeil = objName == "Plafon_PA_Y6.80"
                        && mf.transform.parent != null
                        && mf.transform.parent.name == "Llegada_Escalera";

                    if (!isTileFloor && !isResizedLlegadaCeil) continue;

                    float scaleFactor = isTileFloor ? (1.0f / 0.66f) : (1.0f / 1.20f);
                    Mesh srcMesh = mf.sharedMesh;
                    Vector3[] verts = srcMesh.vertices;
                    Vector3[] norms = srcMesh.normals;
                    if (verts == null || verts.Length == 0 || norms == null || norms.Length != verts.Length) continue;

                    Vector2[] oldUvs = srcMesh.uv;
                    Vector2[] newUvs = new Vector2[verts.Length];
                    float maxDelta = 0f;
                    Transform t = mf.transform;

                    for (int i = 0; i < verts.Length; i++)
                    {
                        Vector3 wp = t.TransformPoint(verts[i]);
                        Vector3 wn = t.TransformDirection(norms[i]).normalized;
                        Vector2 uv;
                        if (Mathf.Abs(wn.y) > 0.5f)
                        {
                            uv = new Vector2(wp.x * scaleFactor, wp.z * scaleFactor);
                        }
                        else if (Mathf.Abs(wn.z) > 0.5f)
                        {
                            uv = new Vector2((wp.x - 11.53f) * scaleFactor, wp.y * scaleFactor);
                        }
                        else
                        {
                            uv = new Vector2((wp.z + 10.62f) * scaleFactor, wp.y * scaleFactor);
                        }
                        newUvs[i] = uv;

                        if (oldUvs == null || i >= oldUvs.Length)
                        {
                            maxDelta = 1f;
                        }
                        else
                        {
                            float d = Mathf.Max(Mathf.Abs(oldUvs[i].x - uv.x), Mathf.Abs(oldUvs[i].y - uv.y));
                            if (d > maxDelta) maxDelta = d;
                        }
                    }

                    if (maxDelta <= 0.0005f) continue;

                    string assetPath = AssetDatabase.GetAssetPath(srcMesh);
                    bool isBuiltIn = !string.IsNullOrEmpty(assetPath) && assetPath.StartsWith("Library/", StringComparison.OrdinalIgnoreCase);
                    if (isBuiltIn)
                    {
                        Mesh cloned = UnityEngine.Object.Instantiate(srcMesh);
                        cloned.name = "MeshUV_" + objName;
                        cloned.uv = newUvs;
                        cloned.RecalculateTangents();
                        mf.sharedMesh = cloned;
                        EditorUtility.SetDirty(mf);
                    }
                    else
                    {
                        srcMesh.uv = newUvs;
                        srcMesh.RecalculateTangents();
                        EditorUtility.SetDirty(srcMesh);
                    }
                    updated++;
                }
            }

            Debug.Log($"[PisoUV] Realineadas UVs en espacio de mundo sobre {updated} superficies continuas de piso/escalera/plafón.");
        }

        private static GameObject FindRootObject(string name)
        {
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (!scene.IsValid()) return null;
            foreach (var go in scene.GetRootGameObjects())
            {
                if (go != null && go.name == name) return go;
            }
            return null;
        }
    }
}
