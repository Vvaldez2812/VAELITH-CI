using System;
using System.IO;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Vaelith.Docencia2
{
    public static class Docencia2InteriorBuilder
    {
        private const string SCENE_PATH = "Assets/Scenes/Exterior_Docencia2.unity";
        private const string BACKUP4_SCENE_PATH = "Assets/Scenes/Exterior_Docencia2_backup4.unity";
        private const string MAT_DIR = "Assets/Materials/Docencia2/Interior";
        private const string MESH_DIR = "Assets/Models/Docencia2/InteriorMeshes";
        private const string RENDER_OUT_DIR = "Renders_Verificacion";

        // Alturas aprobadas en Fase 0 (gobernadas por el modelo exterior en Y = 0..7.00 m)
        public const float Y_PISO_PB = 0.20f;
        public const float Y_PLAFON_PB = 3.20f;
        public const float Y_PISO_PA = 3.60f;
        public const float H_MURO_PB = Y_PLAFON_PB - Y_PISO_PB; // 3.00 m libres de piso a plafón
        public const float H_PUERTA_INT = 2.10f;                // Puertas de 2.10 m (Y = 0.20 a 2.30 m)
        public const float Y_WIN_PB_SILL = 1.72f;               // Repisa de ventana PB exterior
        public const float Y_WIN_PB_TOP = 2.90f;                // Dintel de ventana PB exterior

        // Espesores del plano y cotas seguras de retraído interior (cero cruce con la cara exterior ni ventanas exteriores)
        public const float T_EXT = 0.30f;
        public const float T_INT = 0.15f;
        public const float X_INT_WEST = 0.36f;   // Cara interior del forro oeste (exterior está en x = 0.00..0.30)
        public const float X_INT_EAST = 28.12f;  // Cara interior del forro este (exterior está en x = 28.18..28.48)
        public const float Z_INT_NORTH = 0.36f;  // Cara interior del forro norte (exterior está en z = 0.00..0.30)
        public const float Z_INT_SOUTH = 53.54f; // Cara interior del forro sur (exterior está en z = 53.62..54.00)

        // Sistema de Coordenadas aprobado (Rotación rígida pura 90° sin espejo):
        // X_unity = 27.00 - z_plan * (54.00 / 53.92)  (~ 26.96 - z_plan)
        // Z_unity = 14.25 - x_plan * (28.50 / 28.48)  (~ 14.24 - x_plan)
        public static float PlanZToUnityX(float zPlan)
        {
            return 27.00f - zPlan * (54.00f / 53.92f);
        }

        public static float PlanXToUnityZ(float xPlan)
        {
            return 14.25f - xPlan * (28.50f / 28.48f);
        }

        public static Vector3 PlanToUnityPos(float xPlan, float yHeight, float zPlan)
        {
            return new Vector3(PlanZToUnityX(zPlan), yHeight, PlanXToUnityZ(xPlan));
        }

        private struct OpeningRange
        {
            public float minCoord;
            public float maxCoord;
            public float bottomY;
            public float topY;
            public string label;

            public OpeningRange(float a, float b, float y0, float y1, string lbl = "")
            {
                minCoord = Mathf.Min(a, b);
                maxCoord = Mathf.Max(a, b);
                bottomY = y0;
                topY = y1;
                label = lbl;
            }
        }

        [MenuItem("VAELITH/Interior Docencia 2/FASE 1 - Construir Envolvente Planta Baja (PB_Interior)")]
        public static void BuildPhase1PlantaBajaMenu()
        {
            BuildPhase1PlantaBaja(false);
        }

        [MenuItem("VAELITH/Interior Docencia 2/FASE 2 - Construir Escalera en U, Barandal y Columnas PB")]
        public static void BuildPhase2PlantaBajaMenu()
        {
            BuildPhase2PlantaBaja(false);
        }

        public static void BatchBuildPhase1AndRender()
        {
            BuildPhase1PlantaBaja(true);
        }

        public static void BatchBuildPhase2AndRender()
        {
            BuildPhase2PlantaBaja(true);
        }

        public static void BuildPhase1PlantaBaja(bool renderVerification)
        {
            BuildPBPhase1AndPhase2Internal(includePhase2: false, renderVerification: renderVerification);
        }

        public static void BuildPhase2PlantaBaja(bool renderVerification)
        {
            BuildPBPhase1AndPhase2Internal(includePhase2: true, renderVerification: renderVerification);
        }

        private static void BuildPBPhase1AndPhase2Internal(bool includePhase2, bool renderVerification)
        {
            Debug.Log($"[Docencia2 Interior] Construyendo PB_Interior (Fase 1 + Fase 2={includePhase2})...");

            if (!Directory.Exists(MAT_DIR)) Directory.CreateDirectory(MAT_DIR);
            if (!Directory.Exists(MESH_DIR)) Directory.CreateDirectory(MESH_DIR);
            if (!Directory.Exists(RENDER_OUT_DIR)) Directory.CreateDirectory(RENDER_OUT_DIR);

            // Asegurar copia de seguridad Exterior_Docencia2_backup4
            if (File.Exists(SCENE_PATH) && !File.Exists(BACKUP4_SCENE_PATH))
            {
                AssetDatabase.CopyAsset(SCENE_PATH, BACKUP4_SCENE_PATH);
                Debug.Log("[Docencia2 Interior] Respaldo guardado en: " + BACKUP4_SCENE_PATH);
            }

            var scene = EditorSceneManager.OpenScene(SCENE_PATH, OpenSceneMode.Single);

            // Paso 5c: Restaurar los sub-assets de VolumeProfile (Noche y Terror) que estaban en {fileID: 0}
            Docencia2SceneBuilder.EnsureVolumeProfilesRestored();

            // Asegurar que la escena esté en modo NOCHE como base limpia
            var switcher = UnityEngine.Object.FindAnyObjectByType<Docencia2ModeSwitcher>();
            if (switcher != null)
            {
                switcher.ApplyMode(Docencia2VisualMode.Noche, true);
            }

            // Eliminar o desactivar la placa de concreto sobre la entrada exterior ("ese cubo se mira mal desde afuera")
            GameObject placaObj = GameObject.Find("Placa_SobreEntrada");
            if (placaObj != null)
            {
                placaObj.SetActive(false);
            }

            // Limpiar versión previa de PB_Interior si se re-ejecuta
            GameObject existingPB = GameObject.Find("PB_Interior");
            if (existingPB != null)
            {
                UnityEngine.Object.DestroyImmediate(existingPB);
            }

            Dictionary<string, Material> mats = CreatePhase1And2Materials();

            // Raíz principal de Planta Baja
            GameObject pbRoot = new GameObject("PB_Interior");
            pbRoot.transform.position = Vector3.zero;
            pbRoot.transform.rotation = Quaternion.identity;

            // Crear un grupo jerárquico por cada espacio de Planta Baja (para poder encender/apagar luces y props por zona)
            Dictionary<string, Transform> zones = CreatePBZoneGroups(pbRoot.transform);

            // 1. PISOS Y PLAFONES POR ESPACIO (con vacío de escalera en el techo/losa de entrepiso)
            BuildPBZoneFloorsAndCeilings(zones, mats);

            // 2. MUROS INTERIORES DE 0.15 m CON HUECOS EXACTOS DE PUERTAS (P1..P26 + P_PasilloEste_Supuesto) Y VENTANAS INTERIORES
            BuildPBInteriorWalls(zones, mats);

            // 3. FORRO INTERIOR DE MUROS EXTERIORES CON VANOS DE VENTANAS (V1..V17) Y CORTINAS OSCURAS DE OCLUSIÓN DONDE TABIQUES CHOCAN CON VENTANA EXTERIOR
            BuildPBExteriorWallLiningsAndCurtains(zones, mats);

            // 4. LOSA DE ENTREPISO (0.40 m: Y = 3.20 a 3.60 m) CON EL VACÍO DE LA ESCALERA EN U (x = 0.30..7.10, z = 19.68..26.97)
            BuildEntrepisoSlabWithStairVoid(zones["00_Losa_Entrepiso_y_Columnas"], mats);

            // 5. FASE 2: ESCALERA EN U (20 contrahuellas de 17 cm, huella 0.30 m, descanso Y = 1.90 m, altura libre >= 2.00 m),
            //            MURETE AMARILLO CREMA + BARANDAL METÁLICO TUBULAR VERDE OSCURO, Y COLUMNAS DE 0.40 x 0.40 m
            if (includePhase2)
            {
                BuildPhase2StaircaseAndHandrail(zones["22_Cubo_Escaleras_PB"], mats);
                BuildPhase2StructuralColumns(zones["00_Losa_Entrepiso_y_Columnas"], mats);

                // FASE 3: Puertas, Cancelería interior y Ventanas interiores con rejas
                Docencia2Phase3Builder.BuildPhase3(pbRoot, zones, mats);

                // FASE 4: Acabados finales (Zoclos) e Iluminación Interior PB (Noche / Terror)
                Docencia2Phase4Builder.BuildPhase4(pbRoot, zones, mats);
            }

            // PROTECCIÓN CONTRA REGRESIÓN DEL EXTERIOR:
            // Todos los elementos de PB_Interior están dentro del edificio y detrás de las ventanas emisivas del exterior.
            // Para no proyectar sombras falsas hacia afuera sobre los muros exteriores ni consumir espacio del Shadow Atlas de luces exteriores,
            // desactivamos shadowCastingMode en los forros y losas interiores hacia el exterior, conservando receiveShadows = true.
            ConfigureInteriorShadowModesForZeroExteriorRegression(pbRoot);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();

            Debug.Log($"[Docencia2 Interior] PB_Interior (includePhase2={includePhase2}) construido y guardado exitosamente.");

            if (renderVerification)
            {
                if (includePhase2)
                {
                    RenderPhase2AndExteriorRegressionCaptures(pbRoot, switcher, mats);
                }
                else
                {
                    RenderPhase1VerificationCaptures(pbRoot, mats);
                }
                EditorSceneManager.SaveScene(scene);
            }
        }

        private static void ConfigureInteriorShadowModesForZeroExteriorRegression(GameObject pbRoot)
        {
            // Paso 6: Combinar mallas pequeñas estáticas por zona/material (zoclos, marcos, rejas, paneles LED, columnas, barandales)
            Transform p3Root = pbRoot.transform.Find("03_Fase3_Carpinteria_y_Vidrios");
            if (p3Root != null)
            {
                Transform doorsGrp = p3Root.Find("Puertas_P1_P27");
                if (doorsGrp != null)
                {
                    foreach (Transform doorT in doorsGrp)
                    {
                        Transform piv = doorT.Find("Pivote_Bisagra");
                        if (piv != null) CombineStaticChildrenByMaterial(piv, "Hoja_" + doorT.name, false);
                    }
                    CombineStaticChildrenByMaterial(doorsGrp, "Comb_Marcos_Puertas_P1_P27", false, skipSubtreePrefix: "Pivote_");
                }

                Transform cancelsGrp = p3Root.Find("Canceleria_Interior_Vidrio_Verde");
                if (cancelsGrp != null)
                {
                    foreach (Transform cancelT in cancelsGrp)
                    {
                        foreach (Transform doorOrChild in cancelT)
                        {
                            Transform piv = doorOrChild.Find("Pivote_Bisagra");
                            if (piv != null) CombineStaticChildrenByMaterial(piv, "Hoja_" + doorOrChild.name, false);
                        }
                    }
                    CombineStaticChildrenByMaterial(cancelsGrp, "Comb_Canceleria_PB", false, skipSubtreePrefix: "Pivote_");
                }

                Transform winsGrp = p3Root.Find("Ventanas_Exteriores_y_Rejas_PB");
                if (winsGrp != null)
                {
                    Transform portalT = winsGrp.Find("Portal_Entrada_Principal_Sec05");
                    if (portalT != null)
                    {
                        foreach (Transform ch in portalT)
                        {
                            if (ch.name.StartsWith("Pivote_Vaiven_"))
                            {
                                CombineStaticChildrenByMaterial(ch, "HojaVaiven_" + ch.name, false);
                            }
                        }
                        CombineStaticChildrenByMaterial(portalT, "Comb_Portal_Entrada_Fijos", false, skipSubtreePrefix: "Pivote_");
                    }
                    CombineStaticChildrenByMaterial(winsGrp, "Comb_Ventanas_y_Rejas_PB", false, skipSubtreePrefix: "Portal_");
                }
            }

            Transform p4Root = pbRoot.transform.Find("04_Fase4_Acabados_e_Iluminacion");
            if (p4Root != null)
            {
                Transform zoclosGrp = p4Root.Find("Zoclos_Perimetrales_PB");
                if (zoclosGrp != null) CombineStaticChildrenByMaterial(zoclosGrp, "Comb_Zoclos_PB", false);

                Transform panelsGrp = p4Root.Find("Paneles_LED_Plafon_PB");
                if (panelsGrp != null) CombineStaticChildrenByMaterial(panelsGrp, "Comb_Paneles_LED_PB", false);

                Transform audDoorsGrp = p4Root.Find("Auditorio_2_Puertas_Dobles_Cristal_Norte");
                if (audDoorsGrp != null)
                {
                    foreach (Transform ddT in audDoorsGrp)
                    {
                        Transform pivIzq = ddT.Find("Pivote_Bisagra_Hoja_Izq");
                        if (pivIzq != null) CombineStaticChildrenByMaterial(pivIzq, "HojaIzq_" + ddT.name, false);
                        Transform pivDer = ddT.Find("Pivote_Bisagra_Hoja_Der");
                        if (pivDer != null) CombineStaticChildrenByMaterial(pivDer, "HojaDer_" + ddT.name, false);
                    }
                    CombineStaticChildrenByMaterial(audDoorsGrp, "Comb_Marcos_Auditorio", false, skipSubtreePrefix: "Pivote_");
                }
            }

            Transform colsZone = pbRoot.transform.Find("00_Losa_Entrepiso_y_Columnas");
            if (colsZone != null)
            {
                Transform colsGrp = colsZone.Find("Columnas_Estructurales_0.40x0.40_PB");
                if (colsGrp != null) CombineStaticChildrenByMaterial(colsGrp, "Comb_Columnas_PB", true);
            }

            Transform stairZone = pbRoot.transform.Find("22_Cubo_Escaleras_PB");
            if (stairZone != null)
            {
                Transform stairGrp = stairZone.Find("Escalera_En_U_Concreto_y_Barandal");
                // if (stairGrp != null) CombineStaticChildrenByMaterial(stairGrp, "Comb_Escalera_En_U_PB", true);
            }

            // Paso 5b y 6: Asignar Layer 8 (PB_Interior) y RenderingLayerMask ((1<<0)|(1<<1)) a todo PB_Interior,
            // y desactivar sombras en forros, pisos, plafones, zoclos, marcos, rejas y paneles.
            foreach (Transform t in pbRoot.GetComponentsInChildren<Transform>(true))
            {
                t.gameObject.layer = Docencia2Phase4Builder.LAYER_PB_INTERIOR;
            }

            foreach (MeshRenderer mr in pbRoot.GetComponentsInChildren<MeshRenderer>(true))
            {
                mr.renderingLayerMask = (uint)((1 << 0) | (1 << 1));
                string n = mr.gameObject.name;
                if (n.StartsWith("ForroInterior_") || n.StartsWith("CortinaOclusion_") ||
                    n.StartsWith("Piso_") || n.StartsWith("Plafon_") || n.StartsWith("Losa_Entrepiso") ||
                    n.Contains("Zoclo") || n.Contains("Marco") || n.Contains("Reja") ||
                    n.Contains("Panel") || n.Contains("Comb_Puertas") || n.Contains("Comb_Canceleria") ||
                    n.Contains("Comb_Ventanas") || n.Contains("Comb_Zoclos") || n.Contains("Comb_Paneles"))
                {
                    mr.shadowCastingMode = ShadowCastingMode.Off;
                    mr.receiveShadows = true;
                }
                else
                {
                    mr.shadowCastingMode = ShadowCastingMode.On;
                    mr.receiveShadows = true;
                }
            }
        }

        private static void CombineStaticChildrenByMaterial(Transform groupRoot, string baseMeshName, bool castShadows, string skipSubtreePrefix = null)
        {
            MeshRenderer[] renderers = groupRoot.GetComponentsInChildren<MeshRenderer>(true);
            if (renderers.Length <= 1) return;

            var byMat = new Dictionary<Material, List<CombineInstance>>();
            var toDestroy = new List<GameObject>();

            Matrix4x4 rootWorldToLocal = groupRoot.worldToLocalMatrix;

            foreach (MeshRenderer mr in renderers)
            {
                if (!string.IsNullOrEmpty(skipSubtreePrefix))
                {
                    bool skip = false;
                    Transform cur = mr.transform;
                    while (cur != null && cur != groupRoot)
                    {
                        if (cur.name.StartsWith(skipSubtreePrefix))
                        {
                            skip = true;
                            break;
                        }
                        cur = cur.parent;
                    }
                    if (skip) continue;
                }
                MeshFilter mf = mr.GetComponent<MeshFilter>();
                if (mf == null || mf.sharedMesh == null || mr.sharedMaterial == null) continue;

                // Conservar colliders en un contenedor invisible si el objeto tenía collider activo
                Collider col = mr.GetComponent<Collider>();
                if (col != null && col.enabled && col is BoxCollider bc)
                {
                    GameObject colGo = new GameObject("Col_" + mr.gameObject.name);
                    colGo.transform.SetParent(groupRoot, false);
                    colGo.transform.position = mr.transform.position;
                    colGo.transform.rotation = mr.transform.rotation;
                    colGo.transform.localScale = mr.transform.lossyScale;
                    BoxCollider newBc = colGo.AddComponent<BoxCollider>();
                    newBc.center = bc.center;
                    newBc.size = bc.size;
                }

                Material mat = mr.sharedMaterial;
                if (!byMat.TryGetValue(mat, out var list))
                {
                    list = new List<CombineInstance>();
                    byMat[mat] = list;
                }

                CombineInstance ci = new CombineInstance
                {
                    mesh = mf.sharedMesh,
                    transform = rootWorldToLocal * mf.transform.localToWorldMatrix,
                    subMeshIndex = 0
                };
                list.Add(ci);
                toDestroy.Add(mr.gameObject);
            }

            int matIdx = 0;
            foreach (var kvp in byMat)
            {
                matIdx++;
                Material mat = kvp.Key;
                List<CombineInstance> list = kvp.Value;
                if (list.Count == 0) continue;

                Mesh combined = new Mesh();
                combined.indexFormat = IndexFormat.UInt32;
                combined.name = $"{baseMeshName}_{mat.name}_{matIdx}";
                combined.CombineMeshes(list.ToArray(), true, true);
                combined.RecalculateBounds();

                string assetPath = Path.Combine(MESH_DIR, combined.name + ".asset").Replace("\\", "/");
                if (AssetDatabase.LoadAssetAtPath<Mesh>(assetPath) != null)
                {
                    AssetDatabase.DeleteAsset(assetPath);
                }
                AssetDatabase.CreateAsset(combined, assetPath);

                GameObject combGo = new GameObject($"{baseMeshName}_{mat.name}");
                combGo.transform.SetParent(groupRoot, false);
                combGo.AddComponent<MeshFilter>().sharedMesh = combined;
                MeshRenderer combMr = combGo.AddComponent<MeshRenderer>();
                combMr.sharedMaterial = mat;
                combMr.shadowCastingMode = castShadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
                combMr.receiveShadows = true;

                GameObjectUtility.SetStaticEditorFlags(
                    combGo,
                    StaticEditorFlags.ContributeGI |
                    StaticEditorFlags.OccluderStatic |
                    StaticEditorFlags.BatchingStatic |
                    StaticEditorFlags.ReflectionProbeStatic
                );
            }

            foreach (GameObject go in toDestroy)
            {
                if (go != null) UnityEngine.Object.DestroyImmediate(go);
            }
        }

        private static Dictionary<string, Transform> CreatePBZoneGroups(Transform pbRoot)
        {
            string[] zoneNames = new string[]
            {
                "00_Losa_Entrepiso_y_Columnas",
                "01_Cubiculo_Oeste_A1",
                "02_Cubiculo_Oeste_A2",
                "03_Cubiculo_Oeste_A3",
                "04_Pasillo_Cubiculos_Oeste",
                "05_Cubiculo_Oeste_B1",
                "06_Cubiculo_Oeste_B2",
                "07_Cubiculo_Oeste_B3",
                "08_Bano_Mujeres",
                "09_Bano_Hombres",
                "10_Acceso_a_Banos",
                "11_Cubiculo_Este_C1",
                "12_Cubiculo_Este_C2",
                "13_Cubiculo_Este_C3",
                "14_Pasillo_Cubiculos_Este",
                "15_Cubiculo_Este_D1",
                "16_Cubiculo_Este_D2",
                "17_Cubiculo_Este_D3",
                "18_Alcoba_Oeste",
                "19_Espacio_Oeste",
                "20_Espacio_Este",
                "21_Local_Este_Ajustado",
                "22_Cubo_Escaleras_PB",
                "23_Vestibulo_de_Entrada",
                "24_Auditorio",
                "25_Aula_7",
                "26_Aula_6",
                "27_Aula_5",
                "28_Aula_4",
                "29_Aula_3",
                "30_Aula_2",
                "31_Aula_1",
                "32_Pasillo_Central_y_Plaza_Sur"
            };

            var dict = new Dictionary<string, Transform>();
            foreach (string zn in zoneNames)
            {
                GameObject go = new GameObject(zn);
                go.transform.SetParent(pbRoot, false);
                dict[zn] = go.transform;
            }
            return dict;
        }

        private static Dictionary<string, Material> CreatePhase1And2Materials()
        {
            var dict = new Dictionary<string, Material>();
            Shader urpLit = Shader.Find("Universal Render Pipeline/Lit");
            if (urpLit == null) urpLit = Shader.Find("Standard");

            Material MakeMat(string fileName, Color baseCol, float smoothness, float metallic = 0.0f)
            {
                string path = Path.Combine(MAT_DIR, fileName).Replace("\\", "/");
                Material m = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (m == null)
                {
                    m = new Material(urpLit);
                    AssetDatabase.CreateAsset(m, path);
                }
                m.shader = urpLit;
                m.SetColor("_BaseColor", baseCol);
                m.SetFloat("_Smoothness", smoothness);
                m.SetFloat("_Metallic", metallic);
                m.enableInstancing = true;
                EditorUtility.SetDirty(m);
                return m;
            }

            // Colores base fieles a las fotos reales y con mapas PBR (Albedo + Normal + Smoothness en Alpha) del mismo lenguaje visual que el resto del edificio
            dict["muro_crema"] = MakeMat("Mat_Int_Muro_AmarilloCrema_PB.mat", new Color(0.85f, 0.79f, 0.58f, 1f), 0.18f);
            dict["muro_bano"] = MakeMat("Mat_Int_Muro_AzulejoBeige_PB.mat", new Color(0.76f, 0.69f, 0.58f, 1f), 0.42f);
            dict["piso_loseta"] = MakeMat("Mat_Int_Piso_LosetaGris_PB.mat", new Color(0.94f, 0.94f, 0.92f, 1f), 0.28f);
            dict["piso_bano"] = MakeMat("Mat_Int_Piso_LosetaDiagonalBano_PB.mat", new Color(0.88f, 0.90f, 0.90f, 1f), 0.30f);
            dict["zoclo_gris"] = MakeMat("Mat_Int_Zoclo_CeramicoGris_PB.mat", new Color(0.54f, 0.56f, 0.57f, 1f), 0.25f);
            dict["plafon_blanco"] = MakeMat("Mat_Int_Plafon_Reticular_PB.mat", new Color(0.90f, 0.90f, 0.88f, 1f), 0.12f);
            dict["losa_concreto"] = MakeMat("Mat_Int_Losa_Entrepiso_Concreto.mat", new Color(0.48f, 0.49f, 0.47f, 1f), 0.15f);
            dict["cortina_oclusion"] = MakeMat("Mat_Int_Cortina_Oscura_Oclusion.mat", new Color(0.08f, 0.09f, 0.10f, 1f), 0.10f);

            // Materiales de Fase 2 (Escalera en U con piso de loseta, nariz oscura antiderrapante y barandal metálico tubular verde sobre murete crema)
            dict["escalon_terrazo"] = dict["piso_loseta"]; // Mismo piso de loseta cuadrada en todo el edificio y escalera (según foto real)
            dict["nariz_amarilla"] = MakeMat("Mat_Int_Nariz_Precaucion_Amarilla.mat", new Color(0.28f, 0.24f, 0.21f, 1f), 0.24f, 0.20f);
            dict["barandal_verde"] = MakeMat("Mat_Int_Barandal_Tubular_VerdeOscuro.mat", new Color(0.32f, 0.43f, 0.38f, 1f), 0.46f, 0.35f);
            dict["muro_madera"] = MakeMat("Mat_Int_Muro_Madera_Sec6_PB.mat", new Color(0.95f, 0.72f, 0.45f, 1f), 0.30f, 0.0f);
            dict["liston_madera"] = MakeMat("Mat_Int_Liston_Madera_Oscura_Sec6.mat", new Color(0.38f, 0.24f, 0.14f, 1f), 0.28f, 0.0f);

            void ConfigureNormalImport(string texPath, bool isNormal)
            {
                TextureImporter ti = AssetImporter.GetAtPath(texPath) as TextureImporter;
                if (ti != null)
                {
                    bool dirty = false;
                    var targetType = isNormal ? TextureImporterType.NormalMap : TextureImporterType.Default;
                    if (ti.textureType != targetType) { ti.textureType = targetType; dirty = true; }
                    if (ti.wrapMode != TextureWrapMode.Repeat) { ti.wrapMode = TextureWrapMode.Repeat; dirty = true; }
                    if (!isNormal && ti.alphaIsTransparency) { ti.alphaIsTransparency = false; dirty = true; }
                    if (dirty) ti.SaveAndReimport();
                }
            }

            ConfigureNormalImport("Assets/Textures/Docencia2_Madera_Lambrin_Sec6.png", false);
            ConfigureNormalImport("Assets/Textures/Docencia2_Madera_Lambrin_Sec6_Normal.png", true);
            ConfigureNormalImport("Assets/Textures/Docencia2_Piso_Loseta_Grid.png", false);
            ConfigureNormalImport("Assets/Textures/Docencia2_Piso_Loseta_Grid_Normal.png", true);

            // Textura base de concreto del edificio para que los muros crema y listones compartan exactamente el mismo poro orgánico
            Texture2D texConcAlb = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Textures/Docencia2/Tex_Concreto_Albedo.png");
            Texture2D texConcNorm = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Textures/Docencia2/Tex_Concreto_Normal.png");
            if (texConcAlb != null && texConcNorm != null)
            {
                dict["liston_madera"].SetTexture("_BaseMap", texConcAlb);
                dict["liston_madera"].SetColor("_BaseColor", new Color(0.48f, 0.34f, 0.22f, 1f));
                dict["liston_madera"].SetTexture("_BumpMap", texConcNorm);
                dict["liston_madera"].SetFloat("_BumpScale", 0.30f);
                dict["liston_madera"].EnableKeyword("_NORMALMAP");
                EditorUtility.SetDirty(dict["liston_madera"]);
            }

            Texture2D texWood = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Textures/Docencia2_Madera_Lambrin_Sec6.png");
            Texture2D texWoodNorm = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Textures/Docencia2_Madera_Lambrin_Sec6_Normal.png");
            if (texWood != null)
            {
                dict["muro_madera"].SetTexture("_BaseMap", texWood);
                dict["muro_madera"].SetTextureScale("_BaseMap", new Vector2(1f, 1f));
                if (texWoodNorm != null)
                {
                    dict["muro_madera"].SetTexture("_BumpMap", texWoodNorm);
                    dict["muro_madera"].SetFloat("_BumpScale", 0.42f);
                    dict["muro_madera"].EnableKeyword("_NORMALMAP");
                }
                dict["muro_madera"].SetFloat("_Smoothness", 0.24f);
                EditorUtility.SetDirty(dict["muro_madera"]);
            }

            Texture2D texFloor = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Textures/Docencia2_Piso_Loseta_Grid.png");
            Texture2D texFloorNorm = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Textures/Docencia2_Piso_Loseta_Grid_Normal.png");
            if (texFloor != null)
            {
                dict["piso_loseta"].SetTexture("_BaseMap", texFloor);
                dict["piso_loseta"].SetTextureScale("_BaseMap", new Vector2(1f, 1f));
                dict["piso_loseta"].SetColor("_BaseColor", new Color(0.94f, 0.94f, 0.92f, 1f));
                if (texFloorNorm != null)
                {
                    dict["piso_loseta"].SetTexture("_BumpMap", texFloorNorm);
                    dict["piso_loseta"].SetFloat("_BumpScale", 0.45f);
                    dict["piso_loseta"].EnableKeyword("_NORMALMAP");
                }
                dict["piso_loseta"].SetFloat("_Smoothness", 0.28f);
                EditorUtility.SetDirty(dict["piso_loseta"]);

                dict["piso_bano"].SetTexture("_BaseMap", texFloor);
                dict["piso_bano"].SetTextureScale("_BaseMap", new Vector2(1f, 1f));
                dict["piso_bano"].SetColor("_BaseColor", new Color(0.88f, 0.90f, 0.90f, 1f));
                if (texFloorNorm != null)
                {
                    dict["piso_bano"].SetTexture("_BumpMap", texFloorNorm);
                    dict["piso_bano"].SetFloat("_BumpScale", 0.45f);
                    dict["piso_bano"].EnableKeyword("_NORMALMAP");
                }
                dict["piso_bano"].SetFloat("_Smoothness", 0.28f);
                EditorUtility.SetDirty(dict["piso_bano"]);
            }
            Texture2D texCeil = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Textures/Docencia2_Plafon_Reticular_Grid.png");
            if (texCeil != null)
            {
                dict["plafon_blanco"].mainTexture = texCeil;
                dict["plafon_blanco"].SetTextureScale("_BaseMap", new Vector2(1f, 1f));
                EditorUtility.SetDirty(dict["plafon_blanco"]);
            }

            return dict;
        }

        // ----------------------------------------------------------------------
        // ----------------------------------------------------------------------
        // 1. PISOS Y PLAFONES POR ESPACIO (PLANTA BAJA)
        // Alineación estricta por 7 secciones exteriores modulares de 7.714 m
        // ----------------------------------------------------------------------
        private static void BuildPBZoneFloorsAndCeilings(Dictionary<string, Transform> zones, Dictionary<string, Material> mats)
        {
            var spaces = new List<(float x0, float x1, float z0, float z1, string zoneKey, string label, bool isBath, bool hasCeiling)>
            {
                // ==================================================================
                // SEC 07 (Norte: z = Z_INT_NORTH..7.71 m, recortado a cara interior del forro norte)
                // ==================================================================
                // Cubículos A (A1..A3) fondo 3 x 2.57 m:
                (X_INT_WEST, 3.62f, Z_INT_NORTH, 2.57f, "01_Cubiculo_Oeste_A1", "Cubiculo_A1", false, true),
                (X_INT_WEST, 3.62f, 2.57f, 5.14f, "02_Cubiculo_Oeste_A2", "Cubiculo_A2", false, true),
                (X_INT_WEST, 3.62f, 5.14f, 7.71f, "03_Cubiculo_Oeste_A3", "Cubiculo_A3", false, true),
                (3.62f, 5.62f, Z_INT_NORTH, 7.71f, "04_Pasillo_Cubiculos_Oeste", "Pasillo_Cub_Oeste", false, true),

                // Cubículos B (B1..B3) fondo 3 x 2.57 m:
                (5.62f, 9.24f, Z_INT_NORTH, 2.57f, "05_Cubiculo_Oeste_B1", "Cubiculo_B1", false, true),
                (5.62f, 9.24f, 2.57f, 5.14f, "06_Cubiculo_Oeste_B2", "Cubiculo_B2", false, true),
                (5.62f, 9.24f, 5.14f, 7.71f, "07_Cubiculo_Oeste_B3", "Cubiculo_B3", false, true),

                // Bloque Central Norte: Baño Mujeres, Baño Hombres y Acceso a Baños (z = Z_INT_NORTH..7.71 m)
                (9.24f, 14.24f, Z_INT_NORTH, 6.42f, "08_Bano_Mujeres", "Bano_Mujeres_Cuerpo", true, true),
                (9.24f, 12.24f, 6.42f, 7.71f, "08_Bano_Mujeres", "Bano_Mujeres_Nicho", true, true),
                (14.24f, 19.24f, Z_INT_NORTH, 6.42f, "09_Bano_Hombres", "Bano_Hombres_Cuerpo", true, true),
                (16.24f, 19.24f, 6.42f, 7.71f, "09_Bano_Hombres", "Bano_Hombres_Nicho", true, true),
                (12.24f, 16.24f, 6.42f, 7.71f, "10_Acceso_a_Banos", "Acceso_Banos", false, true),

                // Cubículos C (C1..C3) fondo 3 x 2.57 m:
                (19.24f, 22.86f, Z_INT_NORTH, 2.57f, "11_Cubiculo_Este_C1", "Cubiculo_C1", false, true),
                (19.24f, 22.86f, 2.57f, 5.14f, "12_Cubiculo_Este_C2", "Cubiculo_C2", false, true),
                (19.24f, 22.86f, 5.14f, 7.71f, "13_Cubiculo_Este_C3", "Cubiculo_C3", false, true),
                (22.86f, 24.86f, Z_INT_NORTH, 7.71f, "14_Pasillo_Cubiculos_Este", "Pasillo_Cub_Este", false, true),

                // Cubículos D (D1..D3) fondo 3 x 2.57 m:
                (24.86f, X_INT_EAST, Z_INT_NORTH, 2.57f, "15_Cubiculo_Este_D1", "Cubiculo_D1", false, true),
                (24.86f, X_INT_EAST, 2.57f, 5.14f, "16_Cubiculo_Este_D2", "Cubiculo_D2", false, true),
                (24.86f, X_INT_EAST, 5.14f, 7.71f, "17_Cubiculo_Este_D3", "Cubiculo_D3", false, true),

                // ==================================================================
                // SEC 06 (z = 7.71..15.43 m): Espacio Oeste (3.62 x 7.71), Alcoba Oeste (7.62 x 7.71),
                // Pasillo Central (6.00 m), Local Este (7.62 x 7.71) y Espacio Este (3.62 x 7.71)
                // ==================================================================
                (X_INT_WEST, 3.62f, 7.71f, 15.43f, "19_Espacio_Oeste", "Espacio_Oeste_Sec06", false, true),
                (3.62f, 11.24f, 7.71f, 15.43f, "18_Alcoba_Oeste", "Alcoba_Oeste_Sec06", false, true),
                (11.24f, 17.24f, 7.71f, 15.43f, "32_Pasillo_Central_y_Plaza_Sur", "Pasillo_Central_Sec06", false, true),
                (17.24f, 24.86f, 7.71f, 15.43f, "21_Local_Este_Ajustado", "Local_Este_Sec06", false, true),
                (24.86f, X_INT_EAST, 7.71f, 15.43f, "20_Espacio_Este", "Espacio_Este_Sec06", false, true),

                // ==================================================================
                // SEC 05 (z = 15.43..23.14 m): Escalera en U (Oeste), Pasillo y Vestíbulo (Este)
                // ==================================================================
                // Cubo de Escalera (x = X_INT_WEST..8.10 m, z = 15.43..23.14 m): los tramos ocupan todo el ancho libre entre muros; piso PB, sin plafón (doble altura)
                (X_INT_WEST, 8.10f, 15.43f, 23.14f, "22_Cubo_Escaleras_PB", "Cubo_Escaleras_PB", false, false),
                // Circulación frente a escalera (x = 8.10..11.24 m)
                (8.10f, 11.24f, 15.43f, 23.14f, "22_Cubo_Escaleras_PB", "Vestibulo_Frente_Escaleras_PB", false, true),
                // Pasillo central en Sec05 (x = 11.24..18.24 m)
                (11.24f, 18.24f, 15.43f, 23.14f, "32_Pasillo_Central_y_Plaza_Sur", "Pasillo_Central_Sec05", false, true),
                // Vestíbulo de Entrada (x = 18.24..X_INT_EAST m, 10.24 x 7.71 m = 79.0 m²)
                (18.24f, X_INT_EAST, 15.43f, 23.14f, "23_Vestibulo_de_Entrada", "Vestibulo_Entrada_Sec05", false, true),

                // ==================================================================
                // SEC 04 (z = 23.14..30.86 m): Aula 7 (Oeste), Pasillo Central y Auditorio Tramo Norte (Este)
                // ==================================================================
                (X_INT_WEST, 11.24f, 23.14f, 30.86f, "25_Aula_7", "Aula_7_Sec04", false, true),
                (11.24f, 17.24f, 23.14f, 30.86f, "32_Pasillo_Central_y_Plaza_Sur", "Pasillo_Central_Sec04", false, true),
                (17.24f, X_INT_EAST, 23.14f, 30.86f, "24_Auditorio", "Auditorio_TramoNorte_Sec04", false, true),

                // ==================================================================
                // SEC 03 (z = 30.86..38.57 m): Aula 6 (Oeste), Pasillo Central y Auditorio Tramo Sur (Este)
                // ==================================================================
                (X_INT_WEST, 11.24f, 30.86f, 38.57f, "26_Aula_6", "Aula_6_Sec03", false, true),
                (11.24f, 17.24f, 30.86f, 38.57f, "32_Pasillo_Central_y_Plaza_Sur", "Pasillo_Central_Sec03", false, true),
                (17.24f, X_INT_EAST, 30.86f, 38.57f, "24_Auditorio", "Auditorio_TramoSur_Sec03", false, true),

                // ==================================================================
                // SEC 02 (z = 38.57..46.29 m): Aula 5 (Oeste), Plaza Sur Central y Aula 1 (Este)
                // ==================================================================
                (X_INT_WEST, 8.00f, 38.57f, 46.29f, "27_Aula_5", "Aula_5_Sec02", false, true),
                (8.00f, 20.48f, 38.57f, 46.29f, "32_Pasillo_Central_y_Plaza_Sur", "Plaza_Sur_Sec02", false, true),
                (20.48f, X_INT_EAST, 38.57f, 46.29f, "31_Aula_1", "Aula_1_Sec02", false, true),

                // ==================================================================
                // SEC 01 (Sur: z = 46.29..Z_INT_SOUTH m): Aula 4 (Oeste), Aula 3 (Centro) y Aula 2 (Este)
                // ==================================================================
                (X_INT_WEST, 9.49f, 46.29f, Z_INT_SOUTH, "28_Aula_4", "Aula_4_Sec01", false, true),
                (9.49f, 18.99f, 46.29f, Z_INT_SOUTH, "29_Aula_3", "Aula_3_Sec01", false, true),
                (18.99f, X_INT_EAST, 46.29f, Z_INT_SOUTH, "30_Aula_2", "Aula_2_Sec01", false, true)
            };

            float floorThick = 0.04f;
            float ceilThick = 0.04f;

            foreach (var sp in spaces)
            {
                Transform parent = zones[sp.zoneKey];
                Material floorMat = sp.isBath ? mats["piso_bano"] : mats["piso_loseta"];

                CreatePlanBox(
                    $"Piso_{sp.label}",
                    parent,
                    sp.x0, sp.x1,
                    Y_PISO_PB - floorThick * 0.5f, Y_PISO_PB + floorThick * 0.5f,
                    sp.z0, sp.z1,
                    floorMat,
                    bevel: 0.004f,
                    addCollider: true
                );

                if (sp.hasCeiling)
                {
                    CreatePlanBox(
                        $"Plafon_{sp.label}",
                        parent,
                        sp.x0, sp.x1,
                        Y_PLAFON_PB - ceilThick * 0.5f, Y_PLAFON_PB + ceilThick * 0.5f,
                        sp.z0, sp.z1,
                        mats["plafon_blanco"],
                        bevel: 0.003f,
                        addCollider: false
                    );
                }
            }
        }

        // ----------------------------------------------------------------------
        // 2. MUROS INTERIORES DE 0.15 m CON HUECOS EXACTOS DE PUERTAS Y VANOS
        // Alineación estricta por secciones y posiciones exactas aprobadas en PDF
        // ----------------------------------------------------------------------
        private static void BuildPBInteriorWalls(Dictionary<string, Transform> zones, Dictionary<string, Material> mats)
        {
            Material mCrema = mats["muro_crema"];
            Material mBano = mats["muro_bano"];
            Material mMadera = mats["muro_madera"];
            float y0 = Y_PISO_PB;
            float y1 = Y_PLAFON_PB;
            float doorTop = Y_PISO_PB + H_PUERTA_INT; // 2.30 m

            // ==================================================================
            // A. BLOQUE NORTE OESTE (Sec 07: Cubículos A1-A3, Pasillo Oeste, Cubículos B1-B3)
            //    ACTUALIZACIÓN SEGÚN FOTO REAL DE CUBÍCULOS (media_1794307179904_77efc63d.jpg):
            //    - Todos los cubículos son de lambrín de madera + cancel con antepecho de madera abajo, ventana de cristal arriba y puerta de cristal en el extremo derecho mirando el cubículo de frente (todo construido en Fase 3).
            //    - Las divisiones internas entre cubículos (z = 2.57 y z = 5.14) son muros de madera (mMadera) con sus tapajuntas.
            // ==================================================================
            BuildWallAlongX("Muro_Div_A1_A2_z2.57", zones["01_Cubiculo_Oeste_A1"], 2.57f, 0.10f, X_INT_WEST, 3.62f, y0, y1, mMadera, null);
            BuildWallAlongX("Muro_Div_A2_A3_z5.14", zones["02_Cubiculo_Oeste_A2"], 5.14f, 0.10f, X_INT_WEST, 3.62f, y0, y1, mMadera, null);

            BuildWallAlongX("Muro_Div_B1_B2_z2.57", zones["05_Cubiculo_Oeste_B1"], 2.57f, 0.10f, 5.62f, 9.24f, y0, y1, mMadera, null);
            BuildWallAlongX("Muro_Div_B2_B3_z5.14", zones["06_Cubiculo_Oeste_B2"], 5.14f, 0.10f, 5.62f, 9.24f, y0, y1, mMadera, null);

            // En z = 7.71 (muro sur de Cubículos A3 y B3 hacia Alcoba Oeste):
            // A3 (x = X_INT_WEST..3.62) lleva muro de madera; B3 (x = 5.62..9.24) lleva muro de lambrín de madera (con tapajuntas); y el pasillo oeste (x = 3.62..5.62) lleva cancel con antepecho de madera a la izquierda y puerta de cristal a la derecha (en Fase 3).
            BuildWallAlongX("Muro_Sur_Cubiculo_A3_z7.71", zones["03_Cubiculo_Oeste_A3"], 7.71f, 0.10f, X_INT_WEST, 3.62f, y0, y1, mMadera, null);
            BuildWallAlongX("Muro_Sur_Cubiculo_B3_z7.71", zones["07_Cubiculo_Oeste_B3"], 7.71f, 0.10f, 5.62f, 9.24f, y0, y1, mMadera, null);
            BuildWoodWallBattensAlongX("Tapajuntas_Madera_Sur_B3_z7.71", zones["07_Cubiculo_Oeste_B3"], 7.71f, 0.10f, 5.62f, 9.24f, y0, y1, mats["liston_madera"]);

            // ==================================================================
            // B. BLOQUE CENTRAL DE BAÑOS (Sec 07: Baño Mujeres, Baño Hombres, Nicho Acceso z = Z_INT_NORTH..7.71)
            //    La pared del baño (x = 9.24..12.24 y x = 16.24..19.24 en z = 7.71) se queda como está en muro crema, sin columnas intermedias.
            // ==================================================================
            BuildWallAlongZ("Muro_Oeste_BanoMujeres_x9.24", zones["08_Bano_Mujeres"], 9.24f, T_INT, Z_INT_NORTH, 7.71f, y0, y1, mBano, null);
            BuildWallAlongZ("Muro_Div_Banos_MH_x14.24", zones["08_Bano_Mujeres"], 14.24f, T_INT, Z_INT_NORTH, 6.42f, y0, y1, mBano, null);
            BuildWallAlongZ("Muro_Este_BanoHombres_x19.24", zones["09_Bano_Hombres"], 19.24f, T_INT, Z_INT_NORTH, 7.71f, y0, y1, mBano, null);

            BuildWallAlongX("Muro_Norte_AccesoBanos_z6.42", zones["10_Acceso_a_Banos"], 6.42f, T_INT, 12.24f, 16.24f, y0, y1, mCrema, null);
            BuildWallAlongX("Muro_Sur_BanoMujeres_z7.71", zones["08_Bano_Mujeres"], 7.71f, T_INT, 9.24f, 12.24f, y0, y1, mCrema, null);
            BuildWallAlongX("Muro_Sur_BanoHombres_z7.71", zones["09_Bano_Hombres"], 7.71f, T_INT, 16.24f, 19.24f, y0, y1, mCrema, null);

            BuildWallAlongZ("Muro_Nicho_BanoMujeres_x12.24", zones["10_Acceso_a_Banos"], 12.24f, T_INT, 6.42f, 7.71f, y0, y1, mCrema, new List<OpeningRange>
            {
                new OpeningRange(6.50f, 7.46f, y0, doorTop, "P13_Bano_Mujeres")
            });
            BuildWallAlongZ("Muro_Nicho_BanoHombres_x16.24", zones["10_Acceso_a_Banos"], 16.24f, T_INT, 6.42f, 7.71f, y0, y1, mCrema, new List<OpeningRange>
            {
                new OpeningRange(6.50f, 7.46f, y0, doorTop, "P14_Bano_Hombres")
            });

            // ==================================================================
            // C. BLOQUE NORTE ESTE (Sec 07: Cubículos C1-C3, Pasillo Este, Cubículos D1-D3)
            //    ACTUALIZACIÓN SEGÚN FOTO REAL DE CUBÍCULOS (media_1794307179904_77efc63d.jpg):
            //    - Todos los cubículos C1-C3 y D1-D3 llevan frentes con antepecho de madera abajo, ventana de cristal arriba y puerta de cristal a la derecha mirando de frente (construidos en Fase 3).
            //    - Divisiones internas en madera (mMadera) y muro sur de C3 (x = 19.24..22.86) y D3 (x = 24.86..X_INT_EAST) en lambrín de madera con tapajuntas.
            // ==================================================================
            BuildWallAlongX("Muro_Div_C1_C2_z2.57", zones["11_Cubiculo_Este_C1"], 2.57f, 0.10f, 19.24f, 22.86f, y0, y1, mMadera, null);
            BuildWallAlongX("Muro_Div_C2_C3_z5.14", zones["12_Cubiculo_Este_C2"], 5.14f, 0.10f, 19.24f, 22.86f, y0, y1, mMadera, null);

            BuildWallAlongX("Muro_Div_D1_D2_z2.57", zones["15_Cubiculo_Este_D1"], 2.57f, 0.10f, 24.86f, X_INT_EAST, y0, y1, mMadera, null);
            BuildWallAlongX("Muro_Div_D2_D3_z5.14", zones["16_Cubiculo_Este_D2"], 5.14f, 0.10f, 24.86f, X_INT_EAST, y0, y1, mMadera, null);

            // En z = 7.71 (muro sur de Cubículos C3 y D3 hacia Local Este):
            // C3 (x = 19.24..22.86) lleva muro de lambrín de madera con tapajuntas (dejando intacta la parte del baño x = 16.24..19.24 en crema y sin columna en x = 19.24);
            // D3 (x = 24.86..X_INT_EAST) lleva muro de madera; y el pasillo este (x = 22.86..24.86) lleva cancel con antepecho de madera + ventana y puerta de cristal (en Fase 3).
            BuildWallAlongX("Muro_Sur_Cubiculo_C3_z7.71", zones["13_Cubiculo_Este_C3"], 7.71f, 0.10f, 19.24f, 22.86f, y0, y1, mMadera, null);
            BuildWoodWallBattensAlongX("Tapajuntas_Madera_Sur_C3_z7.71", zones["13_Cubiculo_Este_C3"], 7.71f, 0.10f, 19.24f, 22.86f, y0, y1, mats["liston_madera"]);
            BuildWallAlongX("Muro_Sur_Cubiculo_D3_z7.71", zones["17_Cubiculo_Este_D3"], 7.71f, 0.10f, 24.86f, X_INT_EAST, y0, y1, mMadera, null);

            // ==================================================================
            // D. SEC 06 OESTE: ESPACIO OESTE (3.62 x 7.71) + ALCOBA OESTE (7.62 x 7.71)
            //    CORRECCIÓN VISUAL DEL USUARIO + PDF v4:
            //    - En x = 3.62 (z = 7.71..15.43) va el MURO DE MADERA (lambrín con textura de beta vertical y tapajuntas oscuros, como en la foto real) con puerta de madera P26 en z = 8.83..9.79 m.
            //    - En x = 11.24 (z = 7.71..15.43, pegada al Pasillo Central) va el CANCEL DE VIDRIO TEMPLADO TRANSPARENTE de piso a plafón con P15 de cristal en z = 8.83..9.79 m (construido en Fase 3).
            // ==================================================================
            BuildWallAlongZ("Muro_Madera_Oeste_x3.62", zones["18_Alcoba_Oeste"], 3.62f, 0.10f, 7.71f, 15.43f, y0, y1, mMadera, new List<OpeningRange>
            {
                new OpeningRange(8.83f, 9.79f, y0, doorTop, "P26_Muro_Madera_Oeste")
            });
            BuildWoodWallBattens("Tapajuntas_Madera_Oeste_x3.62", zones["18_Alcoba_Oeste"], 3.62f, 0.10f, 7.71f, 15.43f, 8.83f, 9.79f, y0, y1, mats["liston_madera"]);
            BuildWallAlongX("Muro_Sur_AlcobaOeste_z15.43", zones["18_Alcoba_Oeste"], 15.43f, T_INT, X_INT_WEST, 11.24f, y0, y1, mCrema, null);

            // ==================================================================
            // E. SEC 06 ESTE: LOCAL ESTE (7.62 x 7.71) + ESPACIO ESTE (3.62 x 7.71)
            //    CORRECCIÓN VISUAL DEL USUARIO + PDF v4:
            //    - En x = 24.86 (z = 7.71..15.43) va el MURO DE MADERA (lambrín con textura de beta vertical y tapajuntas oscuros) con puerta de madera P27* en z = 8.83..9.79 m.
            //    - En x = 17.24 (z = 7.71..15.43, pegada al Pasillo Central) va el CANCEL DE VIDRIO TEMPLADO TRANSPARENTE de piso a plafón con P16 de cristal en z = 8.83..9.79 m (construido en Fase 3).
            // ==================================================================
            BuildWallAlongZ("Muro_Madera_Este_x24.86", zones["21_Local_Este_Ajustado"], 24.86f, 0.10f, 7.71f, 15.43f, y0, y1, mMadera, new List<OpeningRange>
            {
                new OpeningRange(8.83f, 9.79f, y0, doorTop, "P27_Muro_Madera_Este")
            });
            BuildWoodWallBattens("Tapajuntas_Madera_Este_x24.86", zones["21_Local_Este_Ajustado"], 24.86f, 0.10f, 7.71f, 15.43f, 8.83f, 9.79f, y0, y1, mats["liston_madera"]);
            BuildWallAlongX("Muro_Sur_LocalEste_z15.43", zones["21_Local_Este_Ajustado"], 15.43f, T_INT, 17.24f, X_INT_EAST, y0, y1, mCrema, null);

            // ==================================================================
            // F. SEC 05: VESTÍBULO DE ENTRADA Y ESCALERA (z = 15.43..23.14 m)
            //    Paso 2: Puertas P24 y P25 dobles de 1.60 m en x = 20.16..21.76 y x = 24.96..26.56 (z = 23.14)
            // ==================================================================
            // Punto 3 (v4): P24 se mueve a x = 18.24..19.84 m (a 1.00 m de la esquina noroeste del Auditorio x = 17.24, alineada con el borde oeste del vestíbulo x = 18.24); P25 sigue en x = 24.96..26.56 m
            BuildWallAlongX("Muro_Div_Vestibulo_Auditorio_z23.14", zones["24_Auditorio"], 23.14f, T_INT, 17.24f, X_INT_EAST, y0, y1, mCrema, new List<OpeningRange>
            {
                new OpeningRange(18.24f, 19.84f, y0, Y_PISO_PB + 2.30f, "P24_Auditorio_Norte_A"),
                new OpeningRange(24.96f, 26.56f, y0, Y_PISO_PB + 2.30f, "P25_Auditorio_Norte_B")
            });

            BuildWallAlongX("Muro_Sur_Escalera_z23.14", zones["25_Aula_7"], 23.14f, T_INT, X_INT_WEST, 11.24f, y0, y1, mCrema, null);

            // ==================================================================
            // G. SEC 04 Y SEC 03: AUDITORIO (z = 23.14..38.57 m) Y AULAS 7 Y 6 (Oeste)
            //    Paso 2: P17 en z = 29.40..30.36 y P18 en z = 31.36..32.32 (a 0.30 m de la cara del pilar z = 30.86)
            // ==================================================================
            BuildWallAlongZ("Muro_Oeste_Auditorio_x17.24", zones["24_Auditorio"], 17.24f, T_INT, 23.14f, 38.57f, y0, y1, mCrema, null);
            BuildWallAlongX("Muro_Sur_Auditorio_z38.57", zones["24_Auditorio"], 38.57f, T_INT, 17.24f, X_INT_EAST, y0, y1, mCrema, null);

            BuildWallAlongZ("Muro_Este_Aulas7y6_x11.24", zones["25_Aula_7"], 11.24f, T_INT, 23.14f, 38.57f, y0, y1, mCrema, new List<OpeningRange>
            {
                new OpeningRange(29.40f, 30.36f, y0, doorTop, "P17_Aula_7"),
                new OpeningRange(31.36f, 32.32f, y0, doorTop, "P18_Aula_6")
            });
            BuildWallAlongX("Muro_Div_Aula7_Aula6_z30.86", zones["26_Aula_6"], 30.86f, T_INT, X_INT_WEST, 11.24f, y0, y1, mCrema, null);
            BuildWallAlongX("Muro_Sur_Aula6_z38.57", zones["26_Aula_6"], 38.57f, T_INT, X_INT_WEST, 11.24f, y0, y1, mCrema, null);

            // ==================================================================
            // H. SEC 02: AULA 5 (Oeste), PLAZA SUR CENTRAL Y AULA 1 (Este) (z = 38.57..46.29 m)
            // ==================================================================
            BuildWallAlongZ("Muro_Este_Aula5_x8.00", zones["27_Aula_5"], 8.00f, T_INT, 38.57f, 46.29f, y0, y1, mCrema, new List<OpeningRange>
            {
                new OpeningRange(38.88f, 39.84f, y0, doorTop, "P19_Aula_5")
            });
            BuildWallAlongZ("Muro_Oeste_Aula1_x20.48", zones["31_Aula_1"], 20.48f, T_INT, 38.57f, 46.29f, y0, y1, mCrema, new List<OpeningRange>
            {
                new OpeningRange(38.88f, 39.84f, y0, doorTop, "P23_Aula_1")
            });

            // ==================================================================
            // I. SEC 01 (Sur: z = 46.29..Z_INT_SOUTH m): AULAS 4, 3 Y 2
            // ==================================================================
            BuildWallAlongX("Muro_Norte_Aulas4_3_2_z46.29", zones["29_Aula_3"], 46.29f, T_INT, X_INT_WEST, X_INT_EAST, y0, y1, mCrema, new List<OpeningRange>
            {
                new OpeningRange(8.24f, 9.20f, y0, doorTop, "P20_Aula_4"),
                new OpeningRange(9.76f, 10.72f, y0, doorTop, "P21_Aula_3"),
                new OpeningRange(19.20f, 20.16f, y0, doorTop, "P22_Aula_2")
            });
            BuildWallAlongZ("Muro_Div_Aula4_Aula3_x9.49", zones["28_Aula_4"], 9.49f, T_INT, 46.29f, Z_INT_SOUTH, y0, y1, mCrema, null);
            BuildWallAlongZ("Muro_Div_Aula3_Aula2_x18.99", zones["30_Aula_2"], 18.99f, T_INT, 46.29f, Z_INT_SOUTH, y0, y1, mCrema, null);
        }

        // ----------------------------------------------------------------------
        // 3. FORRO INTERIOR DE MUROS EXTERIORES CON VANOS (SIN CORTINAS OSCURAS)
        // Regla 1 de alineación aprobada: todos los muros transversales caen exactamente
        // sobre pilares exteriores (z = 0.00, 7.71, 15.43, 23.14, 30.86, 38.57, 46.29, 54.00 m).
        // CORTINAS OSCURAS NECESARIAS: EXACTAMENTE 0 (Se eliminaron las 12 de la Fase 1).
        // ----------------------------------------------------------------------
        private static void BuildPBExteriorWallLiningsAndCurtains(Dictionary<string, Transform> zones, Dictionary<string, Material> mats)
        {
            Material mCrema = mats["muro_crema"];
            Material mBano = mats["muro_bano"];
            float y0 = Y_PISO_PB;
            float y1 = Y_PLAFON_PB;
            float liningThick = 0.04f;

            float stepZ = 54.0f / 7.0f; // 7.714286 m
            float halfClearW = (stepZ - 0.92f) * 0.5f; // ~3.397 m

            // 1. Fachada Este Interior (x_plan = 28.16 m):
            List<OpeningRange> eastOpenings = new List<OpeningRange>();
            for (int s = 0; s < 7; s++)
            {
                float zCenter = (s + 0.5f) * stepZ;
                float zA = zCenter - halfClearW;
                float zB = zCenter + halfClearW;
                if (s == 2)
                {
                    // Sección 05 de la Entrada Principal del exterior (z_plan = 15.43..23.14 m): vano de piso a dintel
                    eastOpenings.Add(new OpeningRange(zA + 0.18f, zB - 0.18f, y0, Y_WIN_PB_TOP, "Vano_Entrada_Principal_Este"));
                }
                else
                {
                    eastOpenings.Add(new OpeningRange(zA, zB, Y_WIN_PB_SILL, Y_WIN_PB_TOP, $"Vano_Ventana_Este_Sec{s + 1:02d}"));
                }
            }
            BuildWallAlongZ("ForroInterior_Fachada_Este_x28.10", zones["00_Losa_Entrepiso_y_Columnas"], 28.10f, liningThick, Z_INT_NORTH, Z_INT_SOUTH, y0, y1, mCrema, eastOpenings);

            // 2. Fachada Oeste Interior (x_plan = 0.38 m):
            List<OpeningRange> westOpenings = new List<OpeningRange>();
            for (int s = 0; s < 7; s++)
            {
                float zCenter = (s + 0.5f) * stepZ;
                float zA = zCenter - halfClearW;
                float zB = zCenter + halfClearW;
                westOpenings.Add(new OpeningRange(zA, zB, Y_WIN_PB_SILL, Y_WIN_PB_TOP, $"Vano_Ventana_Oeste_Sec{s + 1:02d}"));
            }
            BuildWallAlongZ("ForroInterior_Fachada_Oeste_x0.38", zones["00_Losa_Entrepiso_y_Columnas"], 0.38f, liningThick, Z_INT_NORTH, Z_INT_SOUTH, y0, y1, mCrema, westOpenings);

            // 3. Fachada Norte Interior (z_plan = 0.38 m, retraído para cero Z-fighting con muro exterior norte):
            BuildWallAlongX("ForroInterior_Fachada_Norte_Oeste", zones["00_Losa_Entrepiso_y_Columnas"], 0.38f, liningThick, X_INT_WEST, 9.24f, y0, y1, mCrema, null);
            BuildWallAlongX("ForroInterior_Fachada_Norte_Banos", zones["00_Losa_Entrepiso_y_Columnas"], 0.38f, liningThick, 9.24f, 19.24f, y0, y1, mBano, new List<OpeningRange>
            {
                new OpeningRange(13.34f, 15.14f, 2.42f, 3.18f, "Vano_Ventana_Norte_Banos_V7_V8")
            });
            BuildWallAlongX("ForroInterior_Fachada_Norte_Este", zones["00_Losa_Entrepiso_y_Columnas"], 0.38f, liningThick, 19.24f, X_INT_EAST, y0, y1, mCrema, null);

            // 4. Fachada Sur Interior (z_plan = 53.52 m, retraído para cero Z-fighting con muro exterior sur):
            BuildWallAlongX("ForroInterior_Fachada_Sur", zones["00_Losa_Entrepiso_y_Columnas"], 53.52f, liningThick, X_INT_WEST, X_INT_EAST, y0, y1, mCrema, new List<OpeningRange>
            {
                new OpeningRange(13.34f, 15.14f, 2.42f, 3.18f, "Vano_Ventana_Sur_Aula3_V9")
            });
        }

        // ----------------------------------------------------------------------
        // 4. LOSA DE ENTREPISO (Y = 3.20..3.60 m) CON EL VACÍO EXACTO DE LA ESCALERA EN SEC 05
        // ----------------------------------------------------------------------
        private static void BuildEntrepisoSlabWithStairVoid(Transform parent, Dictionary<string, Material> mats)
        {
            Material mLosa = mats["losa_concreto"];
            float y0 = Y_PLAFON_PB + 0.02f; // 3.22 m
            float y1 = Y_PISO_PA;           // 3.60 m

            // El vacío de la escalera en U ocupa exactamente x = 1.80..7.10 m, z = 17.09..21.49 m (4.40 m de ancho total, 2.20 m por tramo).
            // Las franjas norte (z = 15.43..17.09 m), sur (z = 21.49..23.14 m) y oeste (x = X_INT_WEST..1.80 m) quedan cerradas con losa de entrepiso para cero huecos abiertos:
            // Vacío de la escalera en U: abierto desde el muro norte z = 15.43 m hasta el muro sur z = 23.14 m,
            // y desde la fachada poniente X_INT_WEST hasta x = 7.10 m (doble altura completa como en las fotos reales del edificio)
            CreatePlanBox("Losa_Entrepiso_Tramo_Norte", parent, X_INT_WEST, X_INT_EAST, y0, y1, Z_INT_NORTH, 15.43f, mLosa, bevel: 0.008f, addCollider: true);
            CreatePlanBox("Losa_Entrepiso_Tramo_CentroEste_JuntoEscalera", parent, 7.10f, X_INT_EAST, y0, y1, 15.43f, 23.14f, mLosa, bevel: 0.008f, addCollider: true);
            CreatePlanBox("Losa_Entrepiso_Tramo_Sur", parent, X_INT_WEST, X_INT_EAST, y0, y1, 23.14f, Z_INT_SOUTH, mLosa, bevel: 0.008f, addCollider: true);
        }

        // ----------------------------------------------------------------------
        // 5. FASE 2: ESCALERA EN U, MURETE Y BARANDAL TUBULAR VERDE OSCURO (SEC 05: z = 17.09..21.49 m)
        // ----------------------------------------------------------------------
        private static void BuildPhase2StaircaseAndHandrail(Transform stairZone, Dictionary<string, Material> mats)
        {
            GameObject stairRoot = new GameObject("Escalera_En_U_Concreto_y_Barandal");
            stairRoot.transform.SetParent(stairZone, false);

            Material mTerrazo = mats["escalon_terrazo"];
            Material mNariz = mats["nariz_amarilla"];
            Material mCrema = mats["muro_crema"];
            Material mZoclo = mats["zoclo_gris"];
            Material mBarandal = mats["barandal_verde"];
            Material mLosa = mats["losa_concreto"];

            // PARTE 2 AJUSTES (versión 4) + CORRECCIÓN DE ANCHO Y HUECOS DE ESCALERA:
            // - Tramos movidos 0.60 m hacia el este: x = 4.40..7.10 m, pegados al límite este de la zona de la escalera (x = 7.10).
            // - Ajuste v6: descanso de 3.04 m (x = 1.36..4.40), con franja oeste de 1.00 m abierta.
            // - Ancho de cada tramo ajustado a 2.20 m (z = 17.09..19.29 m para el Tramo 1 Norte y z = 19.29..21.49 m para el Tramo 2 Sur, ancho total 4.40 m centrado en z = 19.29 m),
            //   eliminando el aspecto desproporcionadamente ancho y cerrando el hueco de entrepiso con barandal completo en PA (Norte, Oeste, Sur y Este sobre Tramo 1).
            // - Cierre bajo el tramo sur en x = 5.32 m, extendido hasta z = 21.49 m (altura libre >= 2.00 m al este).
            // Ajuste v6: descanso de 3.04 m y separación abierta de 1.00 m al forro oeste.
            float xLandingWest = 1.36f;
            float xLandingEast = 4.40f;
            float xFlightEast = 7.10f;
            float xHeadroom2m = 5.32f;

            // Medidas del plano (PDF v4): holgura de 1.45 m a los muros norte (z = 15.43) y sur (z = 23.14):
            // Cada tramo mide 2.20 m de ancho libre (ancho total de ambos tramos = 4.40 m centrado en z = 19.29 m):
            // - Tramo 1 Norte: z = 17.09..19.29 m (ancho 2.20 m, con holgura de 1.66 m al muro norte z = 15.43 m)
            // - Tramo 2 Sur:   z = 19.29..21.49 m (ancho 2.20 m, con holgura de 1.65 m al muro sur z = 23.14 m)
            // - Unión de ambos tramos pegados en el centro en z = 19.29 m
            float zFlight1North = 17.09f;
            float zFlight1South = 19.29f;
            float zWellNorth = 19.29f;
            float zWellSouth = 19.29f;
            float zFlight2North = 19.29f;
            float zFlight2South = 21.49f;

            float yLandingTop = 1.90f;
            float landingThick = 0.14f;

            // 1. Losa del descanso v6: Y = 1.76..1.90, x = 1.36..4.40, z = 17.09..21.49.
            //    El hueco entre el forro oeste y x = 1.36 permanece abierto por decisión del usuario.
            CreatePlanBox(
                "Descanso_Intermedio_Y1.90_x1.36_4.40",
                stairRoot.transform,
                xLandingWest, xLandingEast,
                yLandingTop - landingThick, yLandingTop,
                zFlight1North, zFlight2South,
                mTerrazo,
                bevel: 0.005f,
                addCollider: true
            );
            CreatePlanBox("Nariz_Descanso_Tramo1_Norte", stairRoot.transform, xLandingEast - 0.05f, xLandingEast, yLandingTop - 0.02f, yLandingTop + 0.002f, zFlight1North, zFlight1South, mNariz, 0.002f, false);
            CreatePlanBox("Nariz_Descanso_Tramo2_Sur", stairRoot.transform, xLandingEast - 0.05f, xLandingEast, yLandingTop - 0.02f, yLandingTop + 0.002f, zFlight2North, zFlight2South, mNariz, 0.002f, false);

            // 2. TRAMO 1 (Inferior, lado NORTE: z = 15.93..19.29 m, ancho 3.36 m)
            // Sube de Este (x = 7.10, Y = 0.20) hacia Oeste (x = 4.40, Y = 1.90) con 9 huellas de 0.30 m + llegada al descanso (10 contrahuellas de 0.17 m)
            float riserH = 0.17f;
            float treadD = 0.30f;
            for (int i = 0; i < 9; i++)
            {
                float stepXMax = xFlightEast - i * treadD;
                float stepXMin = stepXMax - treadD;
                float stepYTop = Y_PISO_PB + (i + 1) * riserH;
                float stepYBot = stepYTop - riserH - 0.12f;

                CreatePlanBox(
                    $"Tramo1_Norte_Escalon_{i + 1:02d}",
                    stairRoot.transform,
                    stepXMin, stepXMax,
                    Mathf.Max(Y_PISO_PB, stepYBot), stepYTop,
                    zFlight1North, zFlight1South,
                    mTerrazo,
                    bevel: 0.004f,
                    addCollider: true
                );

                CreatePlanBox(
                    $"Tramo1_Norte_Nariz_{i + 1:02d}",
                    stairRoot.transform,
                    stepXMax - 0.045f, stepXMax,
                    stepYTop - 0.025f, stepYTop + 0.002f,
                    zFlight1North + 0.02f, zFlight1South - 0.02f,
                    mNariz,
                    bevel: 0.002f,
                    addCollider: false
                );
            }

            BuildSlopedRampMesh(
                "Rampa_Estructural_Tramo1_Norte",
                stairRoot.transform,
                xLandingEast, yLandingTop - landingThick,
                xFlightEast, Y_PISO_PB,
                zFlight1North, zFlight1South,
                0.14f,
                mCrema
            );

            // 3. TRAMO 2 (Superior, lado SUR: z = 19.29..22.64 m, ancho 3.36 m)
            // Sube de Oeste (x = 4.40, Y = 1.90) hacia Este (x = 7.10, Y = 3.60) con 9 huellas de 0.30 m + llegada a PA en x = 7.10 m
            for (int i = 0; i < 9; i++)
            {
                float stepXMin = xLandingEast + i * treadD;
                float stepXMax = stepXMin + treadD;
                float stepYTop = yLandingTop + (i + 1) * riserH;
                float stepYBot = stepYTop - riserH - 0.12f;

                CreatePlanBox(
                    $"Tramo2_Sur_Escalon_{i + 11:02d}",
                    stairRoot.transform,
                    stepXMin, stepXMax,
                    stepYBot, stepYTop,
                    zFlight2North, zFlight2South,
                    mTerrazo,
                    bevel: 0.004f,
                    addCollider: true
                );

                CreatePlanBox(
                    $"Tramo2_Sur_Nariz_{i + 11:02d}",
                    stairRoot.transform,
                    stepXMin, stepXMin + 0.045f,
                    stepYTop - 0.025f, stepYTop + 0.002f,
                    zFlight2North + 0.02f, zFlight2South - 0.02f,
                    mNariz,
                    bevel: 0.002f,
                    addCollider: false
                );
            }

            // Nariz de desembarque en Planta Alta exactamente en el borde de la losa de entrepiso (x = 7.10 m, Y = 3.60 m)
            CreatePlanBox(
                "Nariz_Desembarque_PA_Escalon20_Sur_x7.10",
                stairRoot.transform,
                xFlightEast - 0.02f, xFlightEast + 0.04f,
                Y_PISO_PA - 0.025f, Y_PISO_PA + 0.002f,
                zFlight2North, zFlight2South,
                mNariz,
                bevel: 0.002f,
                addCollider: false
            );

            BuildSlopedRampMesh(
                "Rampa_Estructural_Tramo2_Sur",
                stairRoot.transform,
                xLandingEast, yLandingTop - landingThick,
                xFlightEast, Y_PISO_PA - landingThick,
                zFlight2North, zFlight2South,
                0.14f,
                mCrema
            );

            // 4. CIERRE EXACTO DEL BAJO-ESCALERA BAJO EL TRAMO SUR EN x = 5.32 m (hasta z = 22.64 m, altura libre >= 2.00 m al este):
            CreatePlanBox("Cierre_BajoEscalera_Tramo2Sur_x5.32_AlturaLibre2m", stairRoot.transform, xHeadroom2m - 0.12f, xHeadroom2m, Y_PISO_PB, 2.20f, zFlight2North, zFlight2South, mCrema, 0.005f, true);
            CreatePlanBox("Cierre_BajoEscalera_Tramo2Sur_Zoclo_x5.32", stairRoot.transform, xHeadroom2m - 0.13f, xHeadroom2m + 0.01f, Y_PISO_PB, Y_PISO_PB + 0.10f, zFlight2North, zFlight2South, mZoclo, 0.002f, false);

            CreatePlanBox("Cierre_BajoEscalera_Tramo2Sur_LatNorte", stairRoot.transform, xLandingEast, xHeadroom2m, Y_PISO_PB, 1.98f, zFlight2North, zFlight2North + 0.12f, mCrema, 0.004f, true);
            CreatePlanBox("Cierre_BajoEscalera_Tramo2Sur_LatSur", stairRoot.transform, xLandingEast, xHeadroom2m, Y_PISO_PB, 1.98f, zFlight2South - 0.12f, zFlight2South, mCrema, 0.004f, true);
            CreatePlanBox("Cierre_BajoEscalera_Tramo2Sur_Zoclo_LatNorte", stairRoot.transform, xLandingEast, xHeadroom2m, Y_PISO_PB, Y_PISO_PB + 0.10f, zFlight2North - 0.01f, zFlight2North + 0.13f, mZoclo, 0.002f, false);
            CreatePlanBox("Cierre_BajoEscalera_Tramo2Sur_Zoclo_LatSur", stairRoot.transform, xLandingEast, xHeadroom2m, Y_PISO_PB, Y_PISO_PB + 0.10f, zFlight2South - 0.13f, zFlight2South + 0.01f, mZoclo, 0.002f, false);

            CreatePlanBox("Cierre_BajoEscalera_DescansoNorte_x4.40", stairRoot.transform, xLandingEast - 0.12f, xLandingEast, Y_PISO_PB, yLandingTop - landingThick, zFlight1North, zFlight2North, mCrema, 0.005f, true);
            CreatePlanBox("Cierre_BajoEscalera_DescansoOeste_x1.36", stairRoot.transform, xLandingWest, xLandingWest + 0.12f, Y_PISO_PB, yLandingTop - landingThick, zFlight1North, zFlight2South, mCrema, 0.005f, true);
            CreatePlanBox("Cierre_BajoEscalera_Descanso_LatNorte_z17.09", stairRoot.transform, xLandingWest, xLandingEast, Y_PISO_PB, yLandingTop - landingThick, zFlight1North, zFlight1North + 0.12f, mCrema, 0.004f, true);
            CreatePlanBox("Cierre_BajoEscalera_Descanso_LatSur_z21.49", stairRoot.transform, xLandingWest, xLandingEast, Y_PISO_PB, yLandingTop - landingThick, zFlight2South - 0.12f, zFlight2South, mCrema, 0.004f, true);
            CreatePlanBox("Cierre_BajoEscalera_Zoclo_DescansoNorte", stairRoot.transform, xLandingEast - 0.13f, xLandingEast + 0.01f, Y_PISO_PB, Y_PISO_PB + 0.10f, zFlight1North, zFlight2North, mZoclo, 0.002f, false);
            CreatePlanBox("Cierre_BajoEscalera_Zoclo_DescansoOeste", stairRoot.transform, xLandingWest - 0.01f, xLandingWest + 0.13f, Y_PISO_PB, Y_PISO_PB + 0.10f, zFlight1North, zFlight2South, mZoclo, 0.002f, false);
            CreatePlanBox("Cierre_BajoEscalera_Zoclo_DescansoLatNorte", stairRoot.transform, xLandingWest, xLandingEast, Y_PISO_PB, Y_PISO_PB + 0.10f, zFlight1North - 0.01f, zFlight1North + 0.13f, mZoclo, 0.002f, false);
            CreatePlanBox("Cierre_BajoEscalera_Zoclo_DescansoLatSur", stairRoot.transform, xLandingWest, xLandingEast, Y_PISO_PB, Y_PISO_PB + 0.10f, zFlight2South - 0.13f, zFlight2South + 0.01f, mZoclo, 0.002f, false);

            // 5. PUNTO 2 (v4) + FOTO REAL DE LA ESCALERA (media_1794305309240_34100712.jpg):
            //    En lugar de barrotes verticales tipo reja, la escalera real tiene:
            //    - MURETE/GUALDERA SÓLIDO DE CONCRETO COLOR CREMA (0.78 m de altura sobre la nariz de los peldaños y sobre el piso del descanso),
            //    - PASAMANOS TUBULAR SENCILLO VERDE GRISÁCEO OSCURO (a 0.92 m de altura total) montado sobre soportes tubulares cortos inclinados/verticales cada ~1.20 m
            //      (dejando un espacio libre de solo 0.08 m <= 0.10 m entre el tope del murete sólido y el tubo del pasamanos, por lo que no hay ningún hueco > 0.10 m).
            float parapetH = 0.78f;
            float railTotalH = 0.92f;

            // Gualderas/muretes sólidos inclinados de los 2 tramos (0.78 m de alto + 0.30 m de faldón estructural = 1.08 m de espesor vertical):
            BuildSlopedRampMesh(
                "Murete_Gualdera_Tramo1_InteriorSur",
                stairRoot.transform,
                xLandingEast, yLandingTop + parapetH,
                xFlightEast, Y_PISO_PB + parapetH,
                zWellNorth - 0.06f, zWellNorth + 0.06f,
                parapetH + 0.30f,
                mCrema
            );
            BuildSlopedRampMesh(
                "Murete_Gualdera_Tramo1_ExteriorNorte",
                stairRoot.transform,
                xLandingEast, yLandingTop + parapetH,
                xFlightEast, Y_PISO_PB + parapetH,
                zFlight1North, zFlight1North + 0.10f,
                parapetH + 0.30f,
                mCrema
            );
            BuildSlopedRampMesh(
                "Murete_Gualdera_Tramo2_InteriorNorte",
                stairRoot.transform,
                xLandingEast, yLandingTop + parapetH,
                xFlightEast, Y_PISO_PA + parapetH,
                zWellSouth - 0.06f, zWellSouth + 0.06f,
                parapetH + 0.30f,
                mCrema
            );
            BuildSlopedRampMesh(
                "Murete_Gualdera_Tramo2_ExteriorSur",
                stairRoot.transform,
                xLandingEast, yLandingTop + parapetH,
                xFlightEast, Y_PISO_PA + parapetH,
                zFlight2South - 0.10f, zFlight2South,
                parapetH + 0.30f,
                mCrema
            );

            // Pasamanos tubulares sencillos sobre los muretes de los 2 tramos (con soportes tubulares cortos cada ~0.90 m, hueco libre 0.08 m <= 0.10 m):
            BuildTubularRailingFlight(
                "Barandal_Tramo1_Norte_Interior", stairRoot.transform,
                xFlightEast, Y_PISO_PB + 0.05f,
                xLandingEast, yLandingTop,
                zFlight1South - 0.04f, railTotalH, mBarandal
            );
            BuildTubularRailingFlight(
                "Barandal_Tramo1_Norte_Exterior", stairRoot.transform,
                xFlightEast, Y_PISO_PB + 0.05f,
                xLandingEast, yLandingTop,
                zFlight1North + 0.05f, railTotalH, mBarandal
            );
            BuildTubularRailingFlight(
                "Barandal_Tramo2_Sur_Interior", stairRoot.transform,
                xLandingEast, yLandingTop,
                xFlightEast, Y_PISO_PA,
                zFlight2North + 0.04f, railTotalH, mBarandal
            );
            BuildTubularRailingFlight(
                "Barandal_Tramo2_Sur_Exterior", stairRoot.transform,
                xLandingEast, yLandingTop,
                xFlightEast, Y_PISO_PA,
                zFlight2South - 0.05f, railTotalH, mBarandal
            );

            // Murete perimetral sólido de 0.78 m y pasamanos tubular continuo a 0.92 m en el descanso:
            // Según fotos reales (media_1794308686204_a081b32a.jpg y media_1794308691791_04de728b.jpg):
            // - Borde oeste del descanso en x = 1.36: murete y barandal separados del forro oeste.
            CreatePlanBox("Murete_Descanso_Oeste", stairRoot.transform, xLandingWest, xLandingWest + 0.10f, yLandingTop, yLandingTop + parapetH, zFlight1North, zFlight2South, mCrema, 0.004f, true);
            CreatePlanBox("Zoclo_Descanso_Oeste", stairRoot.transform, xLandingWest + 0.10f, xLandingWest + 0.115f, yLandingTop, yLandingTop + 0.08f, zFlight1North + 0.10f, zFlight2South - 0.10f, mZoclo, 0.001f, false);
            BuildTubularRailingHorizontalAlongZ("Barandal_Descanso_Oeste", stairRoot.transform, xLandingWest + 0.05f, zFlight1North + 0.05f, zFlight2South - 0.05f, yLandingTop, railTotalH, mBarandal);

            // - Lados norte y sur: pasamanos desde el nuevo borde oeste hasta las gualderas exteriores.
            CreatePlanBox("Murete_Descanso_Norte", stairRoot.transform, xLandingWest, xLandingEast, yLandingTop, yLandingTop + parapetH, zFlight1North, zFlight1North + 0.10f, mCrema, 0.004f, true);
            CreatePlanBox("Zoclo_Descanso_Norte", stairRoot.transform, xLandingWest + 0.10f, xLandingEast - 0.10f, yLandingTop, yLandingTop + 0.08f, zFlight1North + 0.10f, zFlight1North + 0.115f, mZoclo, 0.001f, false);
            BuildTubularRailingHorizontalAlongX("Barandal_Descanso_Norte", stairRoot.transform, xLandingWest + 0.05f, xLandingEast - 0.05f, zFlight1North + 0.05f, yLandingTop, railTotalH, mBarandal);

            CreatePlanBox("Murete_Descanso_Sur", stairRoot.transform, xLandingWest, xLandingEast, yLandingTop, yLandingTop + parapetH, zFlight2South - 0.10f, zFlight2South, mCrema, 0.004f, true);
            CreatePlanBox("Zoclo_Descanso_Sur", stairRoot.transform, xLandingWest + 0.10f, xLandingEast - 0.10f, yLandingTop, yLandingTop + 0.08f, zFlight2South - 0.115f, zFlight2South - 0.10f, mZoclo, 0.001f, false);
            BuildTubularRailingHorizontalAlongX("Barandal_Descanso_Sur", stairRoot.transform, xLandingWest + 0.05f, xLandingEast - 0.05f, zFlight2South - 0.05f, yLandingTop, railTotalH, mBarandal);

            // - Remate del murete central y curva en U del pasamanos tubular en el giro del descanso (x = 4.35..4.42, z = 19.23..19.35):
            CreatePlanBox("Murete_Central_Cabeza_Descanso", stairRoot.transform, xLandingEast - 0.06f, xLandingEast + 0.02f, yLandingTop, yLandingTop + parapetH, zWellNorth - 0.06f, zWellSouth + 0.06f, mCrema, 0.004f, true);
            CreatePlanBox("Union_Pasamanos_Central_Descanso", stairRoot.transform, xLandingEast - 0.05f, xLandingEast + 0.02f, yLandingTop + railTotalH - 0.05f, yLandingTop + railTotalH, zWellNorth - 0.06f, zWellSouth + 0.06f, mBarandal, 0.002f, false);

            // Murete sólido (0.80 m) y pasamanos tubular (0.95 m) en Planta Alta sobre el perímetro del vacío de la escalera:
            // 1. Lado Este sobre el Tramo 1 Norte (x = 7.10, z = 15.43..19.29, Y = 3.60..4.55 m):
            CreatePlanBox("Murete_VacioPA_Este_SobreTramo1Norte", stairRoot.transform, xFlightEast - 0.06f, xFlightEast + 0.06f, Y_PISO_PA, Y_PISO_PA + 0.80f, 15.43f, zWellSouth, mCrema, 0.004f, true);
            BuildTubularRailingHorizontalAlongZ("Barandal_VacioPA_Este_SobreTramo1Norte", stairRoot.transform, xFlightEast, 15.43f + 0.05f, zWellSouth, Y_PISO_PA, 0.95f, mBarandal);

            // Pequeno murete de cierre en Planta Alta al costado exterior del desembarque del Tramo 2 (x = 7.00..7.10, z = 21.49..23.14):
            CreatePlanBox("Murete_Desembarque_PA_LateralSur", stairRoot.transform, xFlightEast - 0.10f, xFlightEast + 0.06f, Y_PISO_PA, Y_PISO_PA + 0.80f, zFlight2South - 0.05f, 23.14f, mCrema, 0.004f, true);
            BuildTubularRailingHorizontalAlongZ("Barandal_Desembarque_PA_LateralSur", stairRoot.transform, xFlightEast, zFlight2South, 23.14f, Y_PISO_PA, 0.95f, mBarandal);

            // Proteger los bordes de losa que dan a la franja oeste abierta. No rellenar el vacío.
            Transform gapGuard = new GameObject("Proteccion_PA_HuecoOeste_1m_SIN_CERRAR").transform;
            gapGuard.SetParent(stairRoot.transform, false);
            foreach (float gapZ in new[] { 15.43f, 23.14f })
            {
                CreatePlanBox("Murete_BordePA_HuecoOeste_z" + gapZ, gapGuard,
                    0.42f, xLandingWest, Y_PISO_PA, Y_PISO_PA + parapetH,
                    gapZ - 0.05f, gapZ + 0.05f, mCrema, 0.004f, true);
                BuildTubularRailingHorizontalAlongX("Barandal_BordePA_HuecoOeste_z" + gapZ,
                    gapGuard, 0.45f, xLandingWest - 0.03f, gapZ, Y_PISO_PA, railTotalH, mBarandal);
            }
        }


        private static void BuildSlopedRampMesh(
            string name, Transform parent,
            float xWest, float yWest,
            float xEast, float yEast,
            float zMin, float zMax,
            float verticalThick,
            Material mat)
        {
            float uX0 = PlanZToUnityX(zMax);
            float uX1 = PlanZToUnityX(zMin);
            float uZWest = PlanXToUnityZ(xWest);
            float uZEast = PlanXToUnityZ(xEast);

            Vector3[] v = new Vector3[8]
            {
                new Vector3(uX0, yWest, uZWest),
                new Vector3(uX1, yWest, uZWest),
                new Vector3(uX1, yEast, uZEast),
                new Vector3(uX0, yEast, uZEast),

                new Vector3(uX0, yWest - verticalThick, uZWest),
                new Vector3(uX1, yWest - verticalThick, uZWest),
                new Vector3(uX1, yEast - verticalThick, uZEast),
                new Vector3(uX0, yEast - verticalThick, uZEast)
            };

            // Triángulos con doble cara para garantizar visibilidad independientemente del sentido de la normal
            int[] oneWayTris = new int[]
            {
                0, 1, 2, 0, 2, 3, // Superior
                4, 6, 5, 4, 7, 6, // Inferior
                0, 3, 7, 0, 7, 4, // Cara +X (zMin)
                1, 5, 6, 1, 6, 2, // Cara -X (zMax)
                0, 4, 5, 0, 5, 1, // Cara Oeste
                3, 2, 6, 3, 6, 7  // Cara Este
            };

            int numTris = oneWayTris.Length * 2;
            Vector3[] flatV = new Vector3[numTris];
            int[] flatT = new int[numTris];
            for (int i = 0; i < oneWayTris.Length; i += 3)
            {
                // Cara frontal
                flatV[i] = v[oneWayTris[i]];
                flatV[i + 1] = v[oneWayTris[i + 1]];
                flatV[i + 2] = v[oneWayTris[i + 2]];
                flatT[i] = i;
                flatT[i + 1] = i + 1;
                flatT[i + 2] = i + 2;

                // Cara trasera (inversa)
                int backIdx = oneWayTris.Length + i;
                flatV[backIdx] = v[oneWayTris[i + 2]];
                flatV[backIdx + 1] = v[oneWayTris[i + 1]];
                flatV[backIdx + 2] = v[oneWayTris[i]];
                flatT[backIdx] = backIdx;
                flatT[backIdx + 1] = backIdx + 1;
                flatT[backIdx + 2] = backIdx + 2;
            }

            Mesh m = new Mesh();
            m.name = "Mesh_" + name;
            m.vertices = flatV;
            m.triangles = flatT;
            m.RecalculateNormals();
            m.RecalculateBounds();

            string meshAssetPath = Path.Combine(MESH_DIR, m.name + ".asset").Replace("\\", "/");
            if (AssetDatabase.LoadAssetAtPath<Mesh>(meshAssetPath) != null)
                AssetDatabase.DeleteAsset(meshAssetPath);
            AssetDatabase.CreateAsset(m, meshAssetPath);

            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = m;
            MeshRenderer mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = ShadowCastingMode.On;
            mr.receiveShadows = true;
        }

        private static void BuildTubularRailingFlight(
            string name, Transform parent,
            float xStart, float yStart,
            float xEnd, float yEnd,
            float zPlan, float height, Material mat)
        {
            GameObject railGroup = new GameObject(name);
            railGroup.transform.SetParent(parent, false);

            // Soportes tubulares cortos sobre el murete crema (de y + 0.74 m a y + height = 0.92 m, espaciados cada ~0.90 m como en la foto real)
            float lenX = Mathf.Abs(xEnd - xStart);
            int numSupports = Mathf.Max(3, Mathf.CeilToInt(lenX / 0.90f) + 1);
            float halfW = 0.018f;
            for (int i = 0; i < numSupports; i++)
            {
                float t = i / (float)(numSupports - 1);
                float px = Mathf.Lerp(xStart, xEnd, t);
                float py = Mathf.Lerp(yStart, yEnd, t);
                CreatePlanBox(
                    $"{name}_SoporteTubular_{i + 1:02d}",
                    railGroup.transform,
                    px - halfW, px + halfW,
                    py + height - 0.18f, py + height - 0.03f,
                    zPlan - halfW, zPlan + halfW,
                    mat, 0.001f, false
                );
            }

            // Pasamanos tubular superior continuo (diámetro ~5 cm, hueco libre al murete = 0.08 m <= 0.10 m)
            // xWest siempre es el valor menor en X de plano (lado oeste = xLandingEast = 4.40), y xEast el mayor (lado este = xFlightEast = 7.10)
            float xW = Mathf.Min(xStart, xEnd);
            float xE = Mathf.Max(xStart, xEnd);
            float yW = (xStart < xEnd) ? yStart : yEnd;
            float yE = (xStart < xEnd) ? yEnd : yStart;

            BuildSlopedRampMesh(
                $"{name}_Pasamanos_Tubular",
                railGroup.transform,
                xW, yW + height,
                xE, yE + height,
                zPlan - 0.026f, zPlan + 0.026f,
                0.048f,
                mat
            );
        }

        private static void BuildTubularRailingHorizontalAlongZ(
            string name, Transform parent,
            float xPlan, float zMin, float zMax,
            float yBase, float height, Material mat)
        {
            GameObject railGroup = new GameObject(name);
            railGroup.transform.SetParent(parent, false);

            float len = Mathf.Abs(zMax - zMin);
            int numSupports = Mathf.Max(3, Mathf.CeilToInt(len / 1.10f) + 1);
            float halfW = 0.018f;
            for (int i = 0; i < numSupports; i++)
            {
                float t = i / (float)(numSupports - 1);
                float pz = Mathf.Lerp(zMin, zMax, t);
                CreatePlanBox(
                    $"{name}_SoporteTubular_{i + 1:02d}",
                    railGroup.transform,
                    xPlan - halfW, xPlan + halfW,
                    yBase + height - 0.18f, yBase + height - 0.03f,
                    pz - halfW, pz + halfW,
                    mat, 0.001f, false
                );
            }

            CreatePlanBox(
                $"{name}_Pasamanos_Tubular",
                railGroup.transform,
                xPlan - 0.026f, xPlan + 0.026f,
                yBase + height - 0.048f, yBase + height,
                zMin, zMax,
                mat, 0.002f, false
            );
        }

        private static void BuildTubularRailingHorizontalAlongX(
            string name, Transform parent,
            float xMin, float xMax, float zPlan,
            float yBase, float height, Material mat)
        {
            GameObject railGroup = new GameObject(name);
            railGroup.transform.SetParent(parent, false);

            float len = Mathf.Abs(xMax - xMin);
            int numSupports = Mathf.Max(2, Mathf.CeilToInt(len / 1.10f) + 1);
            float halfW = 0.018f;
            for (int i = 0; i < numSupports; i++)
            {
                float t = i / (float)(numSupports - 1);
                float px = Mathf.Lerp(xMin, xMax, t);
                CreatePlanBox(
                    $"{name}_SoporteTubular_{i + 1:02d}",
                    railGroup.transform,
                    px - halfW, px + halfW,
                    yBase + height - 0.18f, yBase + height - 0.03f,
                    zPlan - halfW, zPlan + halfW,
                    mat, 0.001f, false
                );
            }

            CreatePlanBox(
                $"{name}_Pasamanos_Tubular",
                railGroup.transform,
                xMin, xMax,
                yBase + height - 0.048f, yBase + height,
                zPlan - 0.026f, zPlan + 0.026f,
                mat, 0.002f, false
            );
        }

        private static void BuildWoodWallBattens(
            string baseName, Transform parent, float xAxis, float wallThick,
            float zStart, float zEnd, float doorZ0, float doorZ1,
            float yBottom, float yTop, Material battenMat)
        {
            GameObject grp = new GameObject(baseName);
            grp.transform.SetParent(parent, false);
            float bHalfX = wallThick * 0.5f + 0.005f;
            float bHalfZ = 0.015f; // 3 cm de ancho de tapajuntas vertical en tono nogal cálido

            // Tapajuntas verticales cada ~1.10 m como en la foto real del lambrín de madera, además de zoclo y cornisa de madera nogal
            float[] zPositions = new float[] { zStart + 0.02f, 8.81f, 9.81f, 10.93f, 12.05f, 13.17f, 14.29f, zEnd - 0.02f };
            for (int i = 0; i < zPositions.Length; i++)
            {
                float zp = zPositions[i];
                CreatePlanBox($"{baseName}_Liston_{i:02d}", grp.transform, xAxis - bHalfX, xAxis + bHalfX, yBottom, yTop, zp - bHalfZ, zp + bHalfZ, battenMat, 0.001f, false);
            }
            // Zoclo inferior y moldura superior en madera oscura
            CreatePlanBox($"{baseName}_Zoclo_Norte", grp.transform, xAxis - bHalfX, xAxis + bHalfX, yBottom, yBottom + 0.08f, zStart, doorZ0, battenMat, 0.001f, false);
            CreatePlanBox($"{baseName}_Zoclo_Sur", grp.transform, xAxis - bHalfX, xAxis + bHalfX, yBottom, yBottom + 0.08f, doorZ1, zEnd, battenMat, 0.001f, false);
            CreatePlanBox($"{baseName}_Moldura_Sup", grp.transform, xAxis - bHalfX, xAxis + bHalfX, yTop - 0.05f, yTop, zStart, zEnd, battenMat, 0.001f, false);
        }

        private static void BuildWoodWallBattensAlongX(
            string baseName, Transform parent, float zAxis, float wallThick,
            float xStart, float xEnd, float yBottom, float yTop, Material battenMat)
        {
            GameObject grp = new GameObject(baseName);
            grp.transform.SetParent(parent, false);
            float bHalfZ = wallThick * 0.5f + 0.005f;
            float bHalfX = 0.015f;

            int panels = Mathf.Max(2, Mathf.RoundToInt((xEnd - xStart) / 1.15f));
            float dx = (xEnd - xStart) / panels;
            for (int i = 0; i <= panels; i++)
            {
                float xp = xStart + i * dx;
                CreatePlanBox($"{baseName}_Liston_{i:02d}", grp.transform, xp - bHalfX, xp + bHalfX, yBottom, yTop, zAxis - bHalfZ, zAxis + bHalfZ, battenMat, 0.001f, false);
            }
            CreatePlanBox($"{baseName}_Zoclo", grp.transform, xStart, xEnd, yBottom, yBottom + 0.08f, zAxis - bHalfZ, zAxis + bHalfZ, battenMat, 0.001f, false);
            CreatePlanBox($"{baseName}_Moldura_Sup", grp.transform, xStart, xEnd, yTop - 0.05f, yTop, zAxis - bHalfZ, zAxis + bHalfZ, battenMat, 0.001f, false);
        }

        // ----------------------------------------------------------------------
        // 6. FASE 2: COLUMNAS ESTRUCTURALES DE 0.40 x 0.40 m EN SU POSICIÓN DEL PLANO
        //    (Con cubierta amarillo crema y zoclo cerámico gris en la base, como en las fotos)
        // ----------------------------------------------------------------------
        private static void BuildPhase2StructuralColumns(Transform parent, Dictionary<string, Material> mats)
        {
            GameObject colsRoot = new GameObject("Columnas_Estructurales_0.40x0.40_PB");
            colsRoot.transform.SetParent(parent, false);

            Material mCrema = mats["muro_crema"];
            Material mZoclo = mats["zoclo_gris"];

            // Lista exacta de columnas estructurales interiores de 0.40 x 0.40 m
            // adaptadas a la cuadricula modular aprobada por secciones:
            var columnCenters = new List<(float x, float z, string label)>
            {
                // Eje Oeste del Pasillo Central (x = 11.24 m)
                (11.24f, 7.71f,  "Col_PasilloOeste_z7.71_AlcobaNorte"),
                (11.24f, 15.43f, "Col_PasilloOeste_z15.43_EscaleraNorte"),
                (11.24f, 23.14f, "Col_PasilloOeste_z23.14_Aula7Norte"),
                (11.24f, 30.86f, "Col_PasilloOeste_z30.86_EntreP17yP18"),
                (11.24f, 38.57f, "Col_PasilloOeste_z38.57_Aula6Sur"),

                // Eje Este del Pasillo Central / Vestibulo / Auditorio (x = 17.24 m / 18.24 m)
                (17.24f, 7.71f,  "Col_PasilloEste_z7.71_LocalEsteNorte"),
                (17.24f, 15.43f, "Col_PasilloEste_z15.43_VestibuloNorte"),
                // (18.24f, 19.29f, "Col_Vestibulo_z19.29_EntradaCentro"), // Eliminada columna aislada en medio del vestíbulo/pasillo
                (17.24f, 23.14f, "Col_PasilloEste_z23.14_AuditorioNorte"),
                (17.24f, 30.86f, "Col_PasilloEste_z30.86_AuditorioCentro"),
                (17.24f, 38.57f, "Col_PasilloEste_z38.57_AuditorioSur"),

                // Ejes de la Plaza Sur (z = 38.57 m y z = 46.29 m)
                (8.00f,  38.57f, "Col_PlazaSur_Oeste_x8.00_z38.57"),
                (20.48f, 38.57f, "Col_PlazaSur_Este_x20.48_z38.57"),
                (8.00f,  46.29f, "Col_PlazaSur_Oeste_x8.00_z46.29"),
                (9.49f,  46.29f, "Col_PlazaSur_CentroOeste_x9.49_z46.29"),
                (18.99f, 46.29f, "Col_PlazaSur_CentroEste_x18.99_z46.29"),
                (20.48f, 46.29f, "Col_PlazaSur_Este_x20.48_z46.29"),

                // Columnas del Cubo de Escaleras y Acceso a Baños (se retiraron Col_BanoMujeres_Oeste_x9.24_z7.71 y Col_BanoHombres_Este_x19.24_z7.71 por solicitud del usuario)
                (7.10f,  15.43f, "Col_Escalera_Noreste_x7.10_z15.43"),
                (7.10f,  23.14f, "Col_Escalera_Sureste_x7.10_z23.14"),
                (12.24f, 7.71f,  "Col_AccesoBanos_Oeste_x12.24_z7.71"),
                (16.24f, 7.71f,  "Col_AccesoBanos_Este_x16.24_z7.71")
            };

                        float halfCol = 0.20f; // 0.40 x 0.40 m
            float halfZoclo = 0.206f; // Zoclo perimetral de 10 cm en la base de cada columna

            foreach (var c in columnCenters)
            {
                // Fuste de la columna (Y = 0.20..3.20 m) con cubierta amarillo crema
                CreatePlanBox(
                    c.label,
                    colsRoot.transform,
                    c.x - halfCol, c.x + halfCol,
                    Y_PISO_PB, Y_PLAFON_PB,
                    c.z - halfCol, c.z + halfCol,
                    mCrema,
                    bevel: 0.006f,
                    addCollider: true
                );

                // Zoclo cerámico gris en la base de la columna (Y = 0.20..0.30 m)
                CreatePlanBox(
                    $"{c.label}_Zoclo",
                    colsRoot.transform,
                    c.x - halfZoclo, c.x + halfZoclo,
                    Y_PISO_PB, Y_PISO_PB + 0.10f,
                    c.z - halfZoclo, c.z + halfZoclo,
                    mZoclo,
                    bevel: 0.003f,
                    addCollider: false
                );
            }
        }

        // ----------------------------------------------------------------------
        // CONSTRUCTORES GEOMÉTRICOS DE MUROS CON HUECOS Y CAJAS BISELADAS
        // ----------------------------------------------------------------------
        private static void BuildWallAlongZ(
            string baseName, Transform parent, float xAxis, float thickness,
            float zStart, float zEnd, float yBottom, float yTop,
            Material mat, List<OpeningRange> openings)
        {
            float x0 = xAxis - thickness * 0.5f;
            float x1 = xAxis + thickness * 0.5f;

            if (openings == null || openings.Count == 0)
            {
                CreatePlanBox(baseName, parent, x0, x1, yBottom, yTop, zStart, zEnd, mat, bevel: 0.006f, addCollider: true);
                return;
            }

            openings.Sort((a, b) => a.minCoord.CompareTo(b.minCoord));
            float curZ = zStart;
            int segIdx = 1;

            foreach (var op in openings)
            {
                float opZ0 = Mathf.Clamp(op.minCoord, zStart, zEnd);
                float opZ1 = Mathf.Clamp(op.maxCoord, zStart, zEnd);

                if (opZ0 > curZ + 0.01f)
                {
                    CreatePlanBox($"{baseName}_Tramo_{segIdx++:02d}", parent, x0, x1, yBottom, yTop, curZ, opZ0, mat, bevel: 0.006f, addCollider: true);
                }

                if (op.bottomY > yBottom + 0.01f)
                {
                    CreatePlanBox($"{baseName}_Antepecho_{op.label}", parent, x0, x1, yBottom, op.bottomY, opZ0, opZ1, mat, bevel: 0.005f, addCollider: true);
                }

                if (op.topY < yTop - 0.01f)
                {
                    CreatePlanBox($"{baseName}_Dintel_{op.label}", parent, x0, x1, op.topY, yTop, opZ0, opZ1, mat, bevel: 0.005f, addCollider: true);
                }

                curZ = opZ1;
            }

            if (zEnd > curZ + 0.01f)
            {
                CreatePlanBox($"{baseName}_Tramo_{segIdx:02d}", parent, x0, x1, yBottom, yTop, curZ, zEnd, mat, bevel: 0.006f, addCollider: true);
            }
        }

        private static void BuildWallAlongX(
            string baseName, Transform parent, float zAxis, float thickness,
            float xStart, float xEnd, float yBottom, float yTop,
            Material mat, List<OpeningRange> openings)
        {
            float z0 = zAxis - thickness * 0.5f;
            float z1 = zAxis + thickness * 0.5f;

            if (openings == null || openings.Count == 0)
            {
                CreatePlanBox(baseName, parent, xStart, xEnd, yBottom, yTop, z0, z1, mat, bevel: 0.006f, addCollider: true);
                return;
            }

            openings.Sort((a, b) => a.minCoord.CompareTo(b.minCoord));
            float curX = xStart;
            int segIdx = 1;

            foreach (var op in openings)
            {
                float opX0 = Mathf.Clamp(op.minCoord, xStart, xEnd);
                float opX1 = Mathf.Clamp(op.maxCoord, xStart, xEnd);

                if (opX0 > curX + 0.01f)
                {
                    CreatePlanBox($"{baseName}_Tramo_{segIdx++:02d}", parent, curX, opX0, yBottom, yTop, z0, z1, mat, bevel: 0.006f, addCollider: true);
                }

                if (op.bottomY > yBottom + 0.01f)
                {
                    CreatePlanBox($"{baseName}_Antepecho_{op.label}", parent, opX0, opX1, yBottom, op.bottomY, z0, z1, mat, bevel: 0.005f, addCollider: true);
                }

                if (op.topY < yTop - 0.01f)
                {
                    CreatePlanBox($"{baseName}_Dintel_{op.label}", parent, opX0, opX1, op.topY, yTop, z0, z1, mat, bevel: 0.005f, addCollider: true);
                }

                curX = opX1;
            }

            if (xEnd > curX + 0.01f)
            {
                CreatePlanBox($"{baseName}_Tramo_{segIdx:02d}", parent, curX, xEnd, yBottom, yTop, z0, z1, mat, bevel: 0.006f, addCollider: true);
            }
        }

        public static GameObject CreatePlanBox(
            string name, Transform parent,
            float xPlanMin, float xPlanMax,
            float yMin, float yMax,
            float zPlanMin, float zPlanMax,
            Material mat, float bevel = 0.005f, bool addCollider = true)
        {
            float uX0 = PlanZToUnityX(zPlanMax);
            float uX1 = PlanZToUnityX(zPlanMin);
            float uZ0 = PlanXToUnityZ(xPlanMax);
            float uZ1 = PlanXToUnityZ(xPlanMin);

            float sx = Mathf.Abs(uX1 - uX0);
            float sy = Mathf.Abs(yMax - yMin);
            float sz = Mathf.Abs(uZ1 - uZ0);

            Vector3 center = new Vector3(
                (uX0 + uX1) * 0.5f,
                (yMin + yMax) * 0.5f,
                (uZ0 + uZ1) * 0.5f
            );

            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.position = center;
            go.transform.localScale = new Vector3(sx, sy, sz);

            if (!addCollider)
            {
                var col = go.GetComponent<Collider>();
                if (col != null) UnityEngine.Object.DestroyImmediate(col);
            }

            var mr = go.GetComponent<MeshRenderer>();
            if (mr != null && mat != null)
            {
                mr.sharedMaterial = mat;
                mr.shadowCastingMode = ShadowCastingMode.On;
                mr.receiveShadows = true;

                // Escalar UVs en espacio de mundo (módulos de loseta de ~33x33 cm, plafón de 60x60 cm y lambrín de madera a escala real)
                // para que la textura de loseta cuadrada nunca se estire y tenga el mismo tamaño en pisos, descansos y peldaños de la escalera:
                string matName = mat.name;
                bool isFloorTile = matName.Contains("Piso_Loseta") || matName.Contains("Escalon_Terrazo");
                bool isCeilingGrid = matName.Contains("Plafon_Reticular");
                bool isWoodPanel = matName.Contains("Muro_Madera") || matName.Contains("Puerta_Madera");

                if (isFloorTile || isCeilingGrid || isWoodPanel)
                {
                    var mf = go.GetComponent<MeshFilter>();
                    if (mf != null && mf.sharedMesh != null)
                    {
                        Mesh mClone = UnityEngine.Object.Instantiate(mf.sharedMesh);
                        mClone.name = "MeshUV_" + name;
                        Vector3[] verts = mClone.vertices;
                        Vector3[] norms = mClone.normals;
                        Vector2[] uvs = new Vector2[verts.Length];

                        float scaleFactor = isFloorTile ? (1.0f / 0.66f) : (isCeilingGrid ? (1.0f / 1.20f) : (1.0f / 1.12f));
                        for (int i = 0; i < verts.Length; i++)
                        {
                            Vector3 wp = go.transform.TransformPoint(verts[i]);
                            Vector3 wn = norms[i];
                            if (Mathf.Abs(wn.y) > 0.5f)
                            {
                                // Cara horizontal (pisos, descansos, huellas de escalón, plafón)
                                uvs[i] = new Vector2(wp.x * scaleFactor, wp.z * scaleFactor);
                            }
                            else if (Mathf.Abs(wn.z) > 0.5f)
                            {
                                // Cara vertical perpendicular a Z de Unity (muros a lo largo de Z_plan, ej. muros de madera en x = 3.62 y 24.86)
                                // Para muros de madera mapeamos la altura completa de 3.00 m (Y = 0.20..3.20) de 0 a 1 para que NUNCA haya costura horizontal a mitad de la pared:
                                float vCoord = isWoodPanel ? ((wp.y - 0.20f) / 3.00f) : (wp.y * scaleFactor);
                                uvs[i] = new Vector2((wp.x - 11.53f) * scaleFactor, vCoord);
                            }
                            else
                            {
                                // Cara vertical perpendicular a X de Unity (contrahuellas de escalón o muros en X_plan)
                                float vCoord = isWoodPanel ? ((wp.y - 0.20f) / 3.00f) : (wp.y * scaleFactor);
                                uvs[i] = new Vector2((wp.z + 10.62f) * scaleFactor, vCoord);
                            }
                        }
                        mClone.uv = uvs;
                        mClone.RecalculateTangents();
                        mf.sharedMesh = mClone;
                    }
                }
            }

            GameObjectUtility.SetStaticEditorFlags(
                go,
                StaticEditorFlags.ContributeGI |
                StaticEditorFlags.OccluderStatic |
                StaticEditorFlags.BatchingStatic |
                StaticEditorFlags.ReflectionProbeStatic
            );

            return go;
        }

        // ----------------------------------------------------------------------
        // CAPTURAS DE VERIFICACIÓN DE LA FASE 2 + REGRESIÓN DEL EXTERIOR EN MODO NOCHE
        // ----------------------------------------------------------------------
        private static void RenderPhase2AndExteriorRegressionCaptures(GameObject pbRoot, Docencia2ModeSwitcher switcher, Dictionary<string, Material> mats)
        {
            // 1. REGRESIÓN DEL EXTERIOR: Renderizar las 3 cámaras de referencia en MODO NOCHE (1920x1080)
            //    sin ninguna luz temporal activa, exactamente con la iluminación nocturna aprobada.
            if (switcher != null)
            {
                switcher.ApplyMode(Docencia2VisualMode.Noche, true);
            }

            Camera[] allSceneCams = UnityEngine.Object.FindObjectsByType<Camera>();
            Camera cam1 = GameObject.Find("Camara_01_Frontal_Baja")?.GetComponent<Camera>();
            Camera cam2 = GameObject.Find("Camara_02_Aerea_Azotea")?.GetComponent<Camera>();
            Camera cam3 = GameObject.Find("Camara_03_Esquina_Contrafuerte")?.GetComponent<Camera>();

            System.Diagnostics.Stopwatch sw = System.Diagnostics.Stopwatch.StartNew();
            if (cam1 != null)
                RenderCamToPNG(cam1, allSceneCams, Path.Combine(RENDER_OUT_DIR, "Regresion_Noche_Cam_01_Frontal_Baja.png"), 1920, 1080);
            if (cam2 != null)
                RenderCamToPNG(cam2, allSceneCams, Path.Combine(RENDER_OUT_DIR, "Regresion_Noche_Cam_02_Aerea_Azotea.png"), 1920, 1080);
            if (cam3 != null)
                RenderCamToPNG(cam3, allSceneCams, Path.Combine(RENDER_OUT_DIR, "Regresion_Noche_Cam_03_Esquina_Contrafuerte.png"), 1920, 1080);
            sw.Stop();

            // Medir rendimiento y conteo de objetos en la escena
            GameObject[] allGOs = UnityEngine.Object.FindObjectsByType<GameObject>();
            Renderer[] allRenderers = UnityEngine.Object.FindObjectsByType<Renderer>();
            Renderer[] pbRenderers = pbRoot.GetComponentsInChildren<Renderer>(true);
            float avgMsPerCam = (float)sw.Elapsed.TotalMilliseconds / 6.0f; // cada llamada hace 2 renders para estabilizar RT
            float estFps = 1000.0f / Mathf.Max(1.0f, avgMsPerCam);
            Debug.Log($"[Docencia2 Rendimiento] Total GameObjects={allGOs.Length}, Total Renderers={allRenderers.Length}, PB_Interior Renderers={pbRenderers.Length}, Avg Render Time={avgMsPerCam:F2} ms (~{estFps:F1} FPS en 1920x1080)");

            // 2. CAPTURAS DE VERIFICACIÓN DE LA FASE 2 (con rig temporal de luces de trabajo interior que se destruye al terminar)
            GameObject tempRig = new GameObject("Temp_Rig_Verificacion_Fase2");

            float[] lightZs = new float[] { 6.0f, 15.0f, 23.3f, 33.0f, 44.0f, 50.0f };
            float[] lightXs = new float[] { 4.5f, 14.24f, 23.5f };
            foreach (float lz in lightZs)
            {
                foreach (float lx in lightXs)
                {
                    GameObject lgo = new GameObject($"WorkLight_{lx:F0}_{lz:F0}");
                    lgo.transform.SetParent(tempRig.transform, false);
                    lgo.transform.position = PlanToUnityPos(lx, 2.85f, lz);
                    Light pl = lgo.AddComponent<Light>();
                    pl.type = LightType.Point;
                    pl.range = 14.0f;
                    pl.intensity = 3.2f;
                    pl.color = new Color(1.0f, 0.96f, 0.88f);
                    pl.shadows = LightShadows.None;
                }
            }

            // Luz puntual adicional en la doble altura de la escalera (Y = 4.60 m) para iluminar el Tramo 2 y el descanso
            GameObject stairLightGo = new GameObject("WorkLight_Escalera_PA");
            stairLightGo.transform.SetParent(tempRig.transform, false);
            stairLightGo.transform.position = PlanToUnityPos(4.2f, 4.60f, 23.3f);
            Light stairLight = stairLightGo.AddComponent<Light>();
            stairLight.type = LightType.Point;
            stairLight.range = 14.0f;
            stairLight.intensity = 3.8f;
            stairLight.color = new Color(1.0f, 0.96f, 0.88f);
            stairLight.shadows = LightShadows.None;

            // Luz direccional cenital temporal para la vista superior en corte
            GameObject topSunGo = new GameObject("Temp_TopDown_Light");
            topSunGo.transform.SetParent(tempRig.transform, false);
            topSunGo.transform.rotation = Quaternion.Euler(75f, -35f, 0f);
            Light topSun = topSunGo.AddComponent<Light>();
            topSun.type = LightType.Directional;
            topSun.intensity = 1.6f;
            topSun.color = new Color(0.95f, 0.96f, 1.0f);
            topSun.shadows = LightShadows.Soft;
            topSun.enabled = false;

            // 2a. VISTA SUPERIOR ORTOGRÁFICA CON EL EXTERIOR SUPERPUESTO (Mostrando Escalera en U con flecha "SUBE" y Columnas 0.40x0.40 m)
            List<Renderer> hiddenForTopView = new List<Renderer>();
            foreach (Renderer r in UnityEngine.Object.FindObjectsByType<Renderer>())
            {
                if (!r.enabled) continue;
                string rn = r.gameObject.name;
                if (rn.Contains("Azotea") || rn.Contains("Voladizo") || rn.Contains("Pretil") || rn.Contains("Viga_Azotea") ||
                    rn.Contains("Goteron") || rn.Contains("Plafon_") || rn.Contains("Losa_Entrepiso"))
                {
                    r.enabled = false;
                    hiddenForTopView.Add(r);
                }
            }

            // Crear indicador visual en planta (Flecha + rótulo "SUBE") sobre el Tramo 1 Norte (z = 20.18..22.96 m) y giro en U hacia el Tramo 2 Sur (z = 23.69..26.47 m)
            GameObject subeArrowOverlay = CreateTopDownSubeArrowOverlay(tempRig.transform);

            topSun.enabled = true;
            GameObject topCamGo = new GameObject("Cam_Fase2_TopDown_Ortho");
            topCamGo.transform.SetParent(tempRig.transform, false);
            topCamGo.transform.position = new Vector3(0f, 45f, 0f);
            topCamGo.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            Camera topCam = topCamGo.AddComponent<Camera>();
            topCam.orthographic = true;
            topCam.orthographicSize = 18.5f;
            topCam.nearClipPlane = 1.0f;
            topCam.farClipPlane = 100.0f;
            topCam.clearFlags = CameraClearFlags.SolidColor;
            topCam.backgroundColor = new Color(0.07f, 0.09f, 0.12f, 1f);
            var topCamData = topCamGo.AddComponent<UniversalAdditionalCameraData>();
            topCamData.renderPostProcessing = true;

            RenderCamToPNG(topCam, allSceneCams, Path.Combine(RENDER_OUT_DIR, "Fase2_01_Vista_Superior_Con_Exterior.png"), 1920, 1080);

            if (subeArrowOverlay != null) UnityEngine.Object.DestroyImmediate(subeArrowOverlay);
            topSun.enabled = false;
            foreach (Renderer r in hiddenForTopView)
            {
                if (r != null) r.enabled = true;
            }

            // 2b. CÁMARA INTERIOR 2: Vista frontal de la Escalera en U desde el Vestíbulo/Pasillo (encuadre idéntico a WhatsApp Image 2026-10-08 at 11.01.26 PM.jpeg)
            //     Mirando al Oeste: a la DERECHA (Norte, z = 20.18..22.96) sube el Tramo 1 desde Y = 0.20 m; a la IZQUIERDA (Sur, z = 23.69..26.47) sube el Tramo 2 hacia PA (Y = 3.60 m)
            Camera camEscFrontal = CreateInteriorPerspectiveCam(
                "Cam_Fase2_Escalera_Frontal", tempRig.transform,
                PlanToUnityPos(13.20f, 1.65f, 19.29f),
                PlanToUnityPos(2.80f, 1.90f, 19.29f),
                64f
            );
            RenderCamToPNG(camEscFrontal, allSceneCams, Path.Combine(RENDER_OUT_DIR, "Fase2_02_Escalera_En_U_Vista_Frontal.png"), 1920, 1080);

            // 2c. CÁMARA INTERIOR 3: Vista desde el Descanso Intermedio (Y = 1.90 m, esquina Noroeste) mostrando el Tramo 1 Norte que sube desde PB y el Tramo 2 Sur que sube hacia PA
            Camera camEscDescanso = CreateInteriorPerspectiveCam(
                "Cam_Fase2_Escalera_Desde_Descanso", tempRig.transform,
                PlanToUnityPos(2.15f, 3.45f, 16.60f),
                PlanToUnityPos(6.80f, 2.10f, 21.00f),
                68f
            );
            RenderCamToPNG(camEscDescanso, allSceneCams, Path.Combine(RENDER_OUT_DIR, "Fase2_03_Escalera_Descanso_y_Barandal.png"), 1920, 1080);

            // 2d. CÁMARA INTERIOR 4: Verificación de altura libre (>= 2.00 m) bajo el Tramo 2 Sur y el cierre exacto del bajo-escalera en x_plan = 4.50 m
            Camera camEscAlturaLibre = CreateInteriorPerspectiveCam(
                "Cam_Fase2_Escalera_AlturaLibre", tempRig.transform,
                PlanToUnityPos(9.20f, 1.55f, 22.00f),
                PlanToUnityPos(3.50f, 1.85f, 20.00f),
                64f
            );
            RenderCamToPNG(camEscAlturaLibre, allSceneCams, Path.Combine(RENDER_OUT_DIR, "Fase2_04_Altura_Libre_Bajo_Tramo_Superior.png"), 1920, 1080);

            // 2e. CÁMARA INTERIOR 5: Columnas de 0.40 x 0.40 m en el Pasillo Central y columna entre P17 (Aula 7) y P18 (Aula 6) en z = 33.71 m
            Camera camColumnasPasillo = CreateInteriorPerspectiveCam(
                "Cam_Fase2_Columnas_Pasillo_Central", tempRig.transform,
                PlanToUnityPos(15.20f, 1.65f, 25.50f),
                PlanToUnityPos(11.24f, 1.55f, 35.50f),
                60f
            );
            RenderCamToPNG(camColumnasPasillo, allSceneCams, Path.Combine(RENDER_OUT_DIR, "Fase2_05_Columnas_Pasillo_Central_y_Aulas.png"), 1920, 1080);

            // 2f. CÁMARA INTERIOR 6: Vista superior oblicua desde PA (Y = 5.15 m) mostrando la llegada del Tramo 2 Sur a Y = 3.60 m y el murete/barandal sobre el Tramo 1 Norte
            Camera camEscDesdePA = CreateInteriorPerspectiveCam(
                "Cam_Fase2_Escalera_Llegada_PA", tempRig.transform,
                PlanToUnityPos(10.80f, 5.15f, 17.50f),
                PlanToUnityPos(3.20f, 2.20f, 20.00f),
                62f
            );
            RenderCamToPNG(camEscDesdePA, allSceneCams, Path.Combine(RENDER_OUT_DIR, "Fase2_06_Escalera_Llegada_PA_Y3.60.png"), 1920, 1080);

            // 2g. CÁMARAS ESPECÍFICAS BLOQUE A:
            // A2.1: Vestíbulo con plafón visible mirando hacia arriba/oeste
            Camera camPlafonVestibulo = CreateInteriorPerspectiveCam(
                "Cam_Fase2_Plafon_Vestibulo", tempRig.transform,
                PlanToUnityPos(26.00f, 1.50f, 19.29f),
                PlanToUnityPos(18.00f, 2.90f, 19.29f),
                68f
            );
            RenderCamToPNG(camPlafonVestibulo, allSceneCams, Path.Combine(RENDER_OUT_DIR, "BloqueA_Plafon_Vestibulo.png"), 1920, 1080);

            // A2.2: Pasillo Central mirando al sur con plafón visible
            Camera camPlafonPasillo = CreateInteriorPerspectiveCam(
                "Cam_Fase2_Plafon_PasilloCentral", tempRig.transform,
                PlanToUnityPos(14.24f, 1.45f, 11.57f),
                PlanToUnityPos(14.24f, 2.80f, 27.00f),
                68f
            );
            RenderCamToPNG(camPlafonPasillo, allSceneCams, Path.Combine(RENDER_OUT_DIR, "BloqueA_Plafon_PasilloCentral.png"), 1920, 1080);

            // C4. CAPTURAS DE VERIFICACIÓN FASE 3:
            // C4.1: Puerta de Aula con mirilla superior (P17 Aula 7 y P18 Aula 6 en pasillo)
            Camera camPuertaAula = CreateInteriorPerspectiveCam(
                "Cam_Fase3_Puerta_Aula", tempRig.transform,
                PlanToUnityPos(13.80f, 1.65f, 26.50f),
                PlanToUnityPos(11.24f, 1.60f, 29.38f),
                60f
            );
            RenderCamToPNG(camPuertaAula, allSceneCams, Path.Combine(RENDER_OUT_DIR, "Fase3_01_Puerta_Aula_Con_Mirilla.png"), 1920, 1080);

            // C4.2: Cancelería de pasillo de cubículos con franjas verdes
            Camera camCanceleria = CreateInteriorPerspectiveCam(
                "Cam_Fase3_Canceleria_Pasillo", tempRig.transform,
                PlanToUnityPos(4.62f, 1.60f, 1.00f),
                PlanToUnityPos(3.62f, 1.60f, 3.50f),
                65f
            );
            RenderCamToPNG(camCanceleria, allSceneCams, Path.Combine(RENDER_OUT_DIR, "Fase3_02_Canceleria_Pasillo_Cubiculos.png"), 1920, 1080);

            // C4.3: Ventana exterior con reja de protección vista desde dentro de un aula (Aula 7 hacia fachada Oeste)
            Camera camVentanaReja = CreateInteriorPerspectiveCam(
                "Cam_Fase3_Ventana_Con_Reja", tempRig.transform,
                PlanToUnityPos(4.50f, 1.65f, 27.00f),
                PlanToUnityPos(0.32f, 1.85f, 27.00f),
                65f
            );
            RenderCamToPNG(camVentanaReja, allSceneCams, Path.Combine(RENDER_OUT_DIR, "Fase3_03_Ventana_Con_Reja_PB.png"), 1920, 1080);

            // C5. CAPTURAS DE VERIFICACIÓN FASE 4 (ILUMINACIÓN Y ACABADOS PB):
            // C5.1: Vestíbulo Iluminado de Noche con lámparas tubulares y zoclos
            Camera camFase4Vestibulo = CreateInteriorPerspectiveCam(
                "Cam_Fase4_Vestibulo_Iluminado", tempRig.transform,
                PlanToUnityPos(26.00f, 1.55f, 19.29f),
                PlanToUnityPos(18.00f, 2.50f, 19.29f),
                72f
            );
            RenderCamToPNG(camFase4Vestibulo, allSceneCams, Path.Combine(RENDER_OUT_DIR, "Fase4_01_Vestibulo_Iluminacion_Noche.png"), 1920, 1080);

            // C5.2: Pasillo Central Iluminado con luminarias tubulares de plafón y zoclos
            Camera camFase4Pasillo = CreateInteriorPerspectiveCam(
                "Cam_Fase4_Pasillo_Iluminado", tempRig.transform,
                PlanToUnityPos(14.24f, 1.55f, 11.57f),
                PlanToUnityPos(14.24f, 2.45f, 32.00f),
                72f
            );
            RenderCamToPNG(camFase4Pasillo, allSceneCams, Path.Combine(RENDER_OUT_DIR, "Fase4_02_Pasillo_Central_Iluminado.png"), 1920, 1080);

            // C5.3: Aula 7 Iluminada mostrando cancelería, ventanas con rejas y luminarias
            Camera camFase4Aula = CreateInteriorPerspectiveCam(
                "Cam_Fase4_Aula_Iluminada", tempRig.transform,
                PlanToUnityPos(10.00f, 1.65f, 25.00f),
                PlanToUnityPos(4.00f, 1.65f, 28.00f),
                65f
            );
            RenderCamToPNG(camFase4Aula, allSceneCams, Path.Combine(RENDER_OUT_DIR, "Fase4_03_Aula_Iluminada_Zoclos.png"), 1920, 1080);

            // C5.4: Auditorio - Las 2 puertas dobles de cristal hacia el vestíbulo (encuadre idéntico a la foto de referencia)
            Camera camAuditorioPuertas = CreateInteriorPerspectiveCam(
                "Cam_Fase4_Auditorio_Puertas_Vestibulo", tempRig.transform,
                PlanToUnityPos(23.35f, 1.60f, 18.00f),
                PlanToUnityPos(23.35f, 1.50f, 23.14f),
                65f
            );
            RenderCamToPNG(camAuditorioPuertas, allSceneCams, Path.Combine(RENDER_OUT_DIR, "Fase4_04_Auditorio_Puertas_Dobles_Vestibulo.png"), 1920, 1080);

            // Eliminar el rig temporal y reactivar la cámara principal exterior
            UnityEngine.Object.DestroyImmediate(tempRig);
            foreach (Camera c in allSceneCams)
            {
                if (c != null) c.enabled = (c.gameObject.name == "Camara_01_Frontal_Baja");
            }
        }

        // ----------------------------------------------------------------------
        // CAPTURAS DE VERIFICACIÓN DE LA FASE 1 (VISTA SUPERIOR CORTADA + VISTAS INTERIORES)
        // ----------------------------------------------------------------------
        private static void RenderPhase1VerificationCaptures(GameObject pbRoot, Dictionary<string, Material> mats)
        {
            GameObject tempRig = new GameObject("Temp_Rig_Verificacion_Fase1");

            float[] lightZs = new float[] { 6.0f, 15.0f, 23.0f, 33.0f, 44.0f, 50.0f };
            float[] lightXs = new float[] { 5.0f, 14.24f, 23.5f };
            foreach (float lz in lightZs)
            {
                foreach (float lx in lightXs)
                {
                    GameObject lgo = new GameObject($"WorkLight_{lx:F0}_{lz:F0}");
                    lgo.transform.SetParent(tempRig.transform, false);
                    lgo.transform.position = PlanToUnityPos(lx, 2.85f, lz);
                    Light pl = lgo.AddComponent<Light>();
                    pl.type = LightType.Point;
                    pl.range = 14.0f;
                    pl.intensity = 3.2f;
                    pl.color = new Color(1.0f, 0.96f, 0.88f);
                    pl.shadows = LightShadows.None;
                }
            }

            GameObject topSunGo = new GameObject("Temp_TopDown_Light");
            topSunGo.transform.SetParent(tempRig.transform, false);
            topSunGo.transform.rotation = Quaternion.Euler(75f, -35f, 0f);
            Light topSun = topSunGo.AddComponent<Light>();
            topSun.type = LightType.Directional;
            topSun.intensity = 1.6f;
            topSun.color = new Color(0.95f, 0.96f, 1.0f);
            topSun.shadows = LightShadows.Soft;
            topSun.enabled = false;

            Camera[] allSceneCams = UnityEngine.Object.FindObjectsByType<Camera>();

            List<Renderer> hiddenForTopView = new List<Renderer>();
            foreach (Renderer r in UnityEngine.Object.FindObjectsByType<Renderer>())
            {
                if (!r.enabled) continue;
                string rn = r.gameObject.name;
                if (rn.Contains("Azotea") || rn.Contains("Voladizo") || rn.Contains("Pretil") || rn.Contains("Viga_Azotea") ||
                    rn.Contains("Goteron") || rn.Contains("Plafon_") || rn.Contains("Losa_Entrepiso"))
                {
                    r.enabled = false;
                    hiddenForTopView.Add(r);
                }
            }

            topSun.enabled = true;
            GameObject topCamGo = new GameObject("Cam_Fase1_TopDown_Ortho");
            topCamGo.transform.SetParent(tempRig.transform, false);
            topCamGo.transform.position = new Vector3(0f, 45f, 0f);
            topCamGo.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            Camera topCam = topCamGo.AddComponent<Camera>();
            topCam.orthographic = true;
            topCam.orthographicSize = 18.5f;
            topCam.nearClipPlane = 1.0f;
            topCam.farClipPlane = 100.0f;
            topCam.clearFlags = CameraClearFlags.SolidColor;
            topCam.backgroundColor = new Color(0.07f, 0.09f, 0.12f, 1f);
            var topCamData = topCamGo.AddComponent<UniversalAdditionalCameraData>();
            topCamData.renderPostProcessing = true;

            RenderCamToPNG(topCam, allSceneCams, Path.Combine(RENDER_OUT_DIR, "Fase1_01_Vista_Superior_Con_Exterior.png"), 1920, 1080);

            topSun.enabled = false;
            foreach (Renderer r in hiddenForTopView)
            {
                if (r != null) r.enabled = true;
            }

            Camera camVestibulo = CreateInteriorPerspectiveCam(
                "Cam_Fase1_Vestibulo", tempRig.transform,
                PlanToUnityPos(26.8f, 1.65f, 21.0f),
                PlanToUnityPos(5.0f, 1.55f, 23.0f),
                62f
            );
            RenderCamToPNG(camVestibulo, allSceneCams, Path.Combine(RENDER_OUT_DIR, "Fase1_02_Vestibulo_Entrada_PB.png"), 1920, 1080);

            Camera camPasilloSur = CreateInteriorPerspectiveCam(
                "Cam_Fase1_Pasillo_Central_Sur", tempRig.transform,
                PlanToUnityPos(14.24f, 1.65f, 18.5f),
                PlanToUnityPos(14.24f, 1.55f, 48.0f),
                58f
            );
            RenderCamToPNG(camPasilloSur, allSceneCams, Path.Combine(RENDER_OUT_DIR, "Fase1_03_Pasillo_Central_Mirando_Sur.png"), 1920, 1080);

            Camera camPasilloNorte = CreateInteriorPerspectiveCam(
                "Cam_Fase1_Pasillo_Central_Norte", tempRig.transform,
                PlanToUnityPos(14.24f, 1.65f, 26.0f),
                PlanToUnityPos(14.24f, 1.60f, 9.5f),
                58f
            );
            RenderCamToPNG(camPasilloNorte, allSceneCams, Path.Combine(RENDER_OUT_DIR, "Fase1_04_Pasillo_Central_Mirando_Norte_Banos.png"), 1920, 1080);

            Camera camPlazaSur = CreateInteriorPerspectiveCam(
                "Cam_Fase1_Plaza_Sur", tempRig.transform,
                PlanToUnityPos(14.24f, 1.65f, 38.5f),
                PlanToUnityPos(14.24f, 1.50f, 47.18f),
                68f
            );
            RenderCamToPNG(camPlazaSur, allSceneCams, Path.Combine(RENDER_OUT_DIR, "Fase1_05_Plaza_Sur_Aulas.png"), 1920, 1080);

            Camera camVacioEsc = CreateInteriorPerspectiveCam(
                "Cam_Fase1_Vacio_Escalera", tempRig.transform,
                PlanToUnityPos(13.5f, 5.20f, 23.3f),
                PlanToUnityPos(3.5f, 1.80f, 23.3f),
                60f
            );
            RenderCamToPNG(camVacioEsc, allSceneCams, Path.Combine(RENDER_OUT_DIR, "Fase1_06_Vacio_Escalera_Losa_Entrepiso.png"), 1920, 1080);

            UnityEngine.Object.DestroyImmediate(tempRig);
            foreach (Camera c in allSceneCams)
            {
                if (c != null) c.enabled = (c.gameObject.name == "Camara_01_Frontal_Baja");
            }
        }

        // Flecha temporal "SUBE" para la vista superior (solo para la captura; se destruye después)
        private static GameObject CreateTopDownSubeArrowOverlay(Transform parent)
        {
            GameObject root = new GameObject("Temp_Flecha_SUBE");
            root.transform.SetParent(parent, false);

            Shader unlit = Shader.Find("Universal Render Pipeline/Unlit");
            Material mArrow = new Material(unlit);
            mArrow.SetColor("_BaseColor", new Color(0.85f, 0.10f, 0.08f, 1f));

            // Ajustado a los tramos unidos en z = 19.29 m:
            // Tramo 1 Norte: z = 16.49 a 19.29 m (centro 17.89 m)
            // Tramo 2 Sur:   z = 19.29 a 22.09 m (centro 20.69 m)
            float y0 = 7.60f, y1 = 7.66f, w = 0.09f;
            float zT1 = 17.61f;   // eje del Tramo 1 Norte (z = 15.93..19.29)
            float zT2 = 20.965f;  // eje del Tramo 2 Sur (z = 19.29..22.64)
            float xStart = 7.00f, xTurn = 3.10f, xEnd = 6.80f;

            // Tramo 1: de Este (arranque PB) a Oeste
            CreatePlanBox("Flecha_T1", root.transform, xTurn, xStart, y0, y1, zT1 - w, zT1 + w, mArrow, 0f, false);
            // Giro sobre el descanso
            CreatePlanBox("Flecha_Giro", root.transform, xTurn - w, xTurn + w, y0, y1, zT1 - w, zT2 + w, mArrow, 0f, false);
            // Tramo 2: de Oeste a Este (llegada PA)
            CreatePlanBox("Flecha_T2", root.transform, xTurn, xEnd, y0, y1, zT2 - w, zT2 + w, mArrow, 0f, false);
            // Punta de flecha (escalonada) apuntando a +x_plan
            for (int i = 0; i < 6; i++)
            {
                float half = 0.42f - i * 0.07f;
                float xa = xEnd + i * 0.07f;
                CreatePlanBox($"Flecha_Punta_{i}", root.transform, xa, xa + 0.07f, y0, y1, zT2 - half, zT2 + half, mArrow, 0f, false);
            }
            // Círculo de arranque (cuadro) en el inicio del Tramo 1
            CreatePlanBox("Flecha_Arranque", root.transform, xStart - 0.12f, xStart + 0.12f, y0, y1, zT1 - 0.20f, zT1 + 0.20f, mArrow, 0f, false);

            // Rótulo "SUBE" sobre el Tramo 1
            GameObject txt = new GameObject("Rotulo_SUBE");
            txt.transform.SetParent(root.transform, false);
            txt.transform.position = PlanToUnityPos(5.20f, 7.70f, 18.20f);
            txt.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            TextMesh tm = txt.AddComponent<TextMesh>();
            tm.text = "SUBE";
            tm.fontSize = 64;
            tm.characterSize = 0.05f;
            tm.anchor = TextAnchor.MiddleCenter;
            tm.color = new Color(0.85f, 0.10f, 0.08f, 1f);
            Font f = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (f != null)
            {
                tm.font = f;
                txt.GetComponent<MeshRenderer>().sharedMaterial = f.material;
            }
            return root;
        }

        private static Camera CreateInteriorPerspectiveCam(string name, Transform parent, Vector3 pos, Vector3 target, float fov)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            go.transform.LookAt(target, Vector3.up);
            Camera cam = go.AddComponent<Camera>();
            cam.fieldOfView = fov;
            cam.nearClipPlane = 0.15f;
            cam.farClipPlane = 150.0f;
            cam.clearFlags = CameraClearFlags.Skybox;
            var camData = go.AddComponent<UniversalAdditionalCameraData>();
            camData.renderPostProcessing = true;
            camData.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            camData.antialiasingQuality = AntialiasingQuality.High;
            return cam;
        }

        private static void RenderCamToPNG(Camera targetCam, Camera[] otherCams, string outPath, int width, int height)
        {
            foreach (Camera c in otherCams)
            {
                if (c != null) c.enabled = false;
            }
            targetCam.enabled = true;

            RenderTexture rt = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
            rt.antiAliasing = 4;
            RenderTexture prevRT = RenderTexture.active;
            RenderTexture prevCamRT = targetCam.targetTexture;

            targetCam.targetTexture = rt;
            targetCam.Render();
            targetCam.Render();

            RenderTexture.active = rt;
            Texture2D tex = new Texture2D(width, height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            tex.Apply();

            targetCam.targetTexture = prevCamRT;
            RenderTexture.active = prevRT;
            targetCam.enabled = false;
            UnityEngine.Object.DestroyImmediate(rt);

            File.WriteAllBytes(outPath, tex.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(tex);
            Debug.Log("[Docencia2 Interior] Captura guardada en: " + outPath);
        }
    }
}
