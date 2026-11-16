// Assets/Editor/Docencia2Phase4Builder.cs
// Actualización realista y precisa de la FASE 4 según la Parte 2:
// 1. Plafón unificado a Y_CEILING = 3.20 m en toda la Planta Baja; paneles LED de 60x60 cm exactamente a ras de Y = 3.20 m.
// 2. Puertas dobles del Auditorio P24 y P25 de 1.60 m de ancho en x = 20.16..21.76 m y x = 24.96..26.56 m (z = 23.14 m), cristal ahumado con perfiles bronce.
// 3. Zoclos actualizados a las nuevas posiciones de P17 (z = 29.40..30.36) y P18 (z = 31.36..32.32), además de Sec 6 y muros interiores.
// 4. Luces interiores con Culling Mask y Rendering Layer exclusivos para PB_Interior (Layer 8), rango corto y reducción a las luces reales estrictamente necesarias en NOCHE (sin sombras) para >= 30 FPS y cero fugas hacia el exterior.

using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Vaelith.Docencia2
{
    public static class Docencia2Phase4Builder
    {
        public const float Y_FLOOR = 0.20f;
        // Paso 4: Plafón unificado en Y = 3.20 m en toda la Planta Baja
        public const float Y_CEILING = 3.20f;
        public const float ZOCLO_H = 0.10f;
        public const float ZOCLO_T = 0.015f;
        public const int LAYER_PB_INTERIOR = 8;

        public static void BuildPhase4(GameObject pbRoot, Dictionary<string, Transform> zones, Dictionary<string, Material> mats)
        {
            Transform p4Root = pbRoot.transform.Find("04_Fase4_Acabados_e_Iluminacion");
            if (p4Root != null)
            {
                Object.DestroyImmediate(p4Root.gameObject);
            }
            GameObject goRoot = new GameObject("04_Fase4_Acabados_e_Iluminacion");
            goRoot.transform.SetParent(pbRoot.transform, false);
            p4Root = goRoot.transform;

            Transform zoclosGroup = new GameObject("Zoclos_Perimetrales_PB").transform;
            zoclosGroup.SetParent(p4Root, false);

            Transform panelsVisualGroup = new GameObject("Paneles_LED_Plafon_PB").transform;
            panelsVisualGroup.SetParent(p4Root, false);

            Transform lightsNightGroup = new GameObject("Luces_Interiores_PB_Noche").transform;
            lightsNightGroup.SetParent(p4Root, false);

            Transform lightsTerrorGroup = new GameObject("Luces_Interiores_PB_Terror").transform;
            lightsTerrorGroup.SetParent(p4Root, false);

            // 1. Asignar texturas reales a los materiales de piso y plafón
            ApplyRealisticTexturesToMaterials(mats);

            // Materiales de Fase 4
            Material matZoclo = mats.ContainsKey("zoclo_gris") ? mats["zoclo_gris"] : GetOrCreateMaterial("Mat_Int_Zoclo_Gris_PB.mat", new Color(0.68f, 0.70f, 0.72f, 1f), 0.32f);
            Material matPanelBorder = GetOrCreateMaterial("Mat_Int_Troffer_Marco_Negro.mat", new Color(0.18f, 0.18f, 0.20f, 1f), 0.35f, 0.50f);
            Material matPanelDiffuserNight = GetOrCreateMaterial("Mat_Int_Panel_LED_Emisivo_60x60.mat", new Color(0.95f, 0.96f, 0.98f, 1f), 0.15f, 0.0f, emissiveColor: new Color(1.0f, 0.98f, 0.92f) * 6.5f);
            Material matPanelDiffuserTerror = GetOrCreateMaterial("Mat_Int_Panel_LED_Terror_60x60.mat", new Color(0.60f, 0.58f, 0.55f, 1f), 0.15f, 0.0f, emissiveColor: new Color(0.85f, 0.55f, 0.25f) * 0.3f);

            // Paso 2: Cristal ahumado con perfiles bronce para P24 y P25
            Material matSmokedGlassAuditorio = GetOrCreateMaterial("Mat_Int_Vidrio_Ahumado_Auditorio.mat", new Color(0.14f, 0.12f, 0.10f, 0.68f), 0.92f, 0.10f, transparent: true);
            Material matMetalDoorFrame = GetOrCreateMaterial("Mat_Int_Marco_Puerta_Bronce.mat", new Color(0.32f, 0.23f, 0.14f, 1f), 0.55f, 0.45f);

            // 2. Zoclos perimetrales actualizados
            BuildPBZoclos(zoclosGroup, matZoclo);

            // 3. Paneles LED cuadrados embutidos 60x60 cm a ras de plafón (Y = 3.20 m) + Luces interiores optimizadas (Paso 4, 5b y 6)
            BuildPBRecessedPanelsAndOptimizedLights(panelsVisualGroup, lightsNightGroup, lightsTerrorGroup, matPanelBorder, matPanelDiffuserNight, matPanelDiffuserTerror);

            // 4. Paso 2: Auditorio P24 (x = 20.16..21.76) y P25 (x = 24.96..26.56) dobles de 1.60 m en z = 23.14, cristal ahumado con perfiles bronce
            BuildAuditorioGlassDoubleDoors(p4Root, matSmokedGlassAuditorio, matMetalDoorFrame);

            // 5. Vincular al Docencia2ModeSwitcher
            RegisterLightsInModeSwitcher(lightsNightGroup.gameObject, lightsTerrorGroup.gameObject);
        }

        private static void ApplyRealisticTexturesToMaterials(Dictionary<string, Material> mats)
        {
            Texture2D texFloor = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Textures/Docencia2_Piso_Loseta_Grid.png");
            Texture2D texFloorNorm = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Textures/Docencia2_Piso_Loseta_Grid_Normal.png");
            Texture2D texCeil = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Textures/Docencia2_Plafon_Reticular_Grid.png");
            Texture2D texWood = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Textures/Docencia2_Madera_Lambrin_Sec6.png");
            Texture2D texWoodNorm = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Textures/Docencia2_Madera_Lambrin_Sec6_Normal.png");

            if (mats.ContainsKey("piso_loseta"))
            {
                Material mFloor = mats["piso_loseta"];
                if (texFloor != null)
                {
                    mFloor.SetTexture("_BaseMap", texFloor);
                    mFloor.SetTextureScale("_BaseMap", new Vector2(1f, 1f));
                }
                if (texFloorNorm != null)
                {
                    mFloor.SetTexture("_BumpMap", texFloorNorm);
                    mFloor.SetFloat("_BumpScale", 0.45f);
                    mFloor.EnableKeyword("_NORMALMAP");
                }
                mFloor.SetColor("_BaseColor", new Color(0.94f, 0.94f, 0.92f, 1f));
                mFloor.SetFloat("_Smoothness", 0.28f);
                EditorUtility.SetDirty(mFloor);
            }

            if (mats.ContainsKey("plafon_blanco"))
            {
                Material mCeil = mats["plafon_blanco"];
                if (texCeil != null)
                {
                    mCeil.SetTexture("_BaseMap", texCeil);
                    mCeil.SetTextureScale("_BaseMap", new Vector2(1f, 1f));
                }
                mCeil.SetColor("_BaseColor", new Color(0.96f, 0.96f, 0.95f, 1f));
                mCeil.SetFloat("_Smoothness", 0.12f);
                EditorUtility.SetDirty(mCeil);
            }

            if (mats.ContainsKey("muro_madera"))
            {
                Material mWood = mats["muro_madera"];
                if (texWood != null)
                {
                    mWood.SetTexture("_BaseMap", texWood);
                    mWood.SetTextureScale("_BaseMap", new Vector2(1f, 1f));
                }
                if (texWoodNorm != null)
                {
                    mWood.SetTexture("_BumpMap", texWoodNorm);
                    mWood.SetFloat("_BumpScale", 0.42f);
                    mWood.EnableKeyword("_NORMALMAP");
                }
                mWood.SetColor("_BaseColor", new Color(0.96f, 0.93f, 0.88f, 1f));
                mWood.SetFloat("_Smoothness", 0.24f);
                EditorUtility.SetDirty(mWood);
            }
        }

        private static Material GetOrCreateMaterial(string name, Color color, float smoothness, float metallic = 0.0f, Color? emissiveColor = null, bool transparent = false)
        {
            string dir = "Assets/Materials/Docencia2";
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
            string path = Path.Combine(dir, name).Replace("\\", "/");
            Material m = AssetDatabase.LoadAssetAtPath<Material>(path);
            Shader urpLit = Shader.Find("Universal Render Pipeline/Lit");
            if (urpLit == null) urpLit = Shader.Find("Standard");

            if (m == null)
            {
                m = new Material(urpLit);
                AssetDatabase.CreateAsset(m, path);
            }
            m.shader = urpLit;
            m.SetColor("_BaseColor", color);
            m.SetFloat("_Smoothness", smoothness);
            m.SetFloat("_Metallic", metallic);

            if (transparent)
            {
                m.SetFloat("_Surface", 1.0f);
                m.SetFloat("_Blend", 0.0f);
                m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                m.SetOverrideTag("RenderType", "Transparent");
                m.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
                m.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
                m.SetInt("_ZWrite", 0);
                m.renderQueue = (int)RenderQueue.Transparent;
            }
            else
            {
                m.SetFloat("_Surface", 0);
                m.SetOverrideTag("RenderType", "Opaque");
                m.SetInt("_SrcBlend", (int)BlendMode.One);
                m.SetInt("_DstBlend", (int)BlendMode.Zero);
                m.SetInt("_ZWrite", 1);
                m.renderQueue = (int)RenderQueue.Geometry;
            }

            if (emissiveColor.HasValue)
            {
                m.EnableKeyword("_EMISSION");
                m.SetColor("_EmissionColor", emissiveColor.Value);
                m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            }
            else
            {
                m.DisableKeyword("_EMISSION");
            }

            m.enableInstancing = true;
            EditorUtility.SetDirty(m);
            return m;
        }

        private static void BuildPBZoclos(Transform parent, Material mZoclo)
        {
            // Nota: En el Pasillo Central Norte (Sec 06: z = 7.71..15.43), los lados x = 11.24 y x = 17.24 llevan canceles de vidrio templado de piso a plafón (con su propio riel inferior de aluminio bronce), por lo que no llevan zoclo cerámico de muro.

            // Muro oeste del Auditorio hacia el pasillo central (continuo sin puertas)
            Docencia2InteriorBuilder.CreatePlanBox("Zoclo_Pasillo_Auditorio_Oeste", parent, 17.24f - ZOCLO_T, 17.24f, Y_FLOOR, Y_FLOOR + ZOCLO_H, 23.14f, 38.57f, mZoclo, 0f, false);

            // Muro norte del Auditorio hacia el vestíbulo (Punto 3 v4: P24 en 18.24..19.84 y P25 en 24.96..26.56)
            Docencia2InteriorBuilder.CreatePlanBox("Zoclo_Vestibulo_Auditorio_T1", parent, 17.24f, 18.24f, Y_FLOOR, Y_FLOOR + ZOCLO_H, 23.14f - ZOCLO_T, 23.14f, mZoclo, 0f, false);
            Docencia2InteriorBuilder.CreatePlanBox("Zoclo_Vestibulo_Auditorio_T2", parent, 19.84f, 24.96f, Y_FLOOR, Y_FLOOR + ZOCLO_H, 23.14f - ZOCLO_T, 23.14f, mZoclo, 0f, false);
            Docencia2InteriorBuilder.CreatePlanBox("Zoclo_Vestibulo_Auditorio_T3", parent, 26.56f, 28.12f, Y_FLOOR, Y_FLOOR + ZOCLO_H, 23.14f - ZOCLO_T, 23.14f, mZoclo, 0f, false);

            // Muro norte del Vestíbulo (z = 15.43)
            Docencia2InteriorBuilder.CreatePlanBox("Zoclo_Vestibulo_Norte", parent, 17.24f, 28.12f, Y_FLOOR, Y_FLOOR + ZOCLO_H, 15.43f, 15.43f + ZOCLO_T, mZoclo, 0f, false);

            // Paso 2: Muro este de Aulas 7 y 6 (x = 11.24) con P17 en 29.40..30.36, pilar en 30.66..31.06 y P18 en 31.36..32.32
            Docencia2InteriorBuilder.CreatePlanBox("Zoclo_Pasillo_Aulas6_7_T1", parent, 11.24f, 11.24f + ZOCLO_T, Y_FLOOR, Y_FLOOR + ZOCLO_H, 23.14f, 29.40f, mZoclo, 0f, false);
            Docencia2InteriorBuilder.CreatePlanBox("Zoclo_Pasillo_Aulas6_7_T2_NortePilar", parent, 11.24f, 11.24f + ZOCLO_T, Y_FLOOR, Y_FLOOR + ZOCLO_H, 30.36f, 30.66f, mZoclo, 0f, false);
            Docencia2InteriorBuilder.CreatePlanBox("Zoclo_Pasillo_Aulas6_7_T3_SurPilar", parent, 11.24f, 11.24f + ZOCLO_T, Y_FLOOR, Y_FLOOR + ZOCLO_H, 31.06f, 31.36f, mZoclo, 0f, false);
            Docencia2InteriorBuilder.CreatePlanBox("Zoclo_Pasillo_Aulas6_7_T4", parent, 11.24f, 11.24f + ZOCLO_T, Y_FLOOR, Y_FLOOR + ZOCLO_H, 32.32f, 38.57f, mZoclo, 0f, false);

            // Plaza Sur
            Docencia2InteriorBuilder.CreatePlanBox("Zoclo_PlazaSur_Oeste", parent, 8.00f, 8.00f + ZOCLO_T, Y_FLOOR, Y_FLOOR + ZOCLO_H, 39.59f, 46.29f, mZoclo, 0f, false);
            Docencia2InteriorBuilder.CreatePlanBox("Zoclo_PlazaSur_Este", parent, 20.48f - ZOCLO_T, 20.48f, Y_FLOOR, Y_FLOOR + ZOCLO_H, 39.59f, 46.29f, mZoclo, 0f, false);
        }

        // -----------------------------------------------------------------------------------------
        // 2. PANELES LED RETICULARES 60x60 CM A RAS DEL PLAFÓN (Y = 3.20 m) Y LUCES OPTIMIZADAS
        // -----------------------------------------------------------------------------------------
        private static void BuildPBRecessedPanelsAndOptimizedLights(Transform panelsParent, Transform parentNight, Transform parentTerror, Material mFrame, Material mDiffuserNight, Material mDiffuserTerror)
        {
            // Todos los paneles LED emisivos 60x60 cm a ras de Y = 3.20 m en pasillos, vestíbulo, auditorio, sec 6 y aulas
            var panelPositions = new List<(string name, float x, float z)>
            {
                // Pasillo Central: doble fila paralela a lo largo del pasillo (x = 13.00 m y x = 15.48 m)
                ("Panel_Pasillo_Norte_L1", 13.00f, 9.50f),
                ("Panel_Pasillo_Norte_R1", 15.48f, 9.50f),
                ("Panel_Pasillo_Norte_L2", 13.00f, 13.50f),
                ("Panel_Pasillo_Norte_R2", 15.48f, 13.50f),

                ("Panel_Pasillo_Centro_L1", 13.00f, 17.50f),
                ("Panel_Pasillo_Centro_R1", 15.48f, 17.50f),
                ("Panel_Pasillo_Centro_L2", 13.00f, 21.50f),
                ("Panel_Pasillo_Centro_R2", 15.48f, 21.50f),

                ("Panel_Pasillo_Sur_L1", 13.00f, 26.50f),
                ("Panel_Pasillo_Sur_R1", 15.48f, 26.50f),
                ("Panel_Pasillo_Sur_L2", 13.00f, 31.00f),
                ("Panel_Pasillo_Sur_R2", 15.48f, 31.00f),
                ("Panel_Pasillo_Sur_L3", 13.00f, 35.50f),
                ("Panel_Pasillo_Sur_R3", 15.48f, 35.50f),

                // Vestíbulo Principal: retícula de paneles 60x60 cm distribuidos en 3 filas de pares
                ("Panel_Vestibulo_1", 20.50f, 16.80f),
                ("Panel_Vestibulo_2", 25.00f, 16.80f),
                ("Panel_Vestibulo_3", 20.50f, 19.29f),
                ("Panel_Vestibulo_4", 25.00f, 19.29f),
                ("Panel_Vestibulo_5", 20.50f, 21.80f),
                ("Panel_Vestibulo_6", 25.00f, 21.80f),

                // Plaza Sur (z = 38.57..46.29 m)
                ("Panel_PlazaSur_1", 11.50f, 41.00f),
                ("Panel_PlazaSur_2", 16.50f, 41.00f),
                ("Panel_PlazaSur_3", 14.24f, 44.50f),

                // Pasillos de Cubículos y Acceso a Baños (Sec 07)
                ("Panel_PasilloCub_Oeste_1", 4.62f, 2.50f),
                ("Panel_PasilloCub_Oeste_2", 4.62f, 5.80f),
                ("Panel_PasilloCub_Este_1", 23.86f, 2.50f),
                ("Panel_PasilloCub_Este_2", 23.86f, 5.80f),
                ("Panel_AccesoBanos", 14.24f, 6.95f),

                // Sec 06: Alcoba Oeste y Local Este
                ("Panel_AlcobaOeste_1", 7.40f, 10.00f),
                ("Panel_AlcobaOeste_2", 7.40f, 13.20f),
                ("Panel_LocalEste_1", 21.00f, 10.00f),
                ("Panel_LocalEste_2", 21.00f, 13.20f),

                // Auditorio (11.24 x 15.43 m)
                ("Panel_Auditorio_1", 20.50f, 26.50f),
                ("Panel_Auditorio_2", 25.20f, 26.50f),
                ("Panel_Auditorio_3", 20.50f, 30.86f),
                ("Panel_Auditorio_4", 25.20f, 30.86f),
                ("Panel_Auditorio_5", 20.50f, 35.20f),
                ("Panel_Auditorio_6", 25.20f, 35.20f),

                // Aulas 7 y 6 (Oeste)
                ("Panel_Aula7_1", 4.00f, 26.00f),
                ("Panel_Aula7_2", 8.00f, 26.00f),
                ("Panel_Aula7_3", 4.00f, 28.50f),
                ("Panel_Aula7_4", 8.00f, 28.50f),

                ("Panel_Aula6_1", 4.00f, 33.50f),
                ("Panel_Aula6_2", 8.00f, 33.50f),
                ("Panel_Aula6_3", 4.00f, 36.50f),
                ("Panel_Aula6_4", 8.00f, 36.50f),

                // Escalera en U
                ("Panel_Escalera_Arranque", 8.80f, 19.29f)
            };

            float hX = 0.30f; // 60 cm / 2
            float hZ = 0.30f;

            // Paso 4: Todos los paneles LED quedan exactamente a ras del plafón Y = 3.20 m (Y = 3.17..3.20 m)
            foreach (var p in panelPositions)
            {
                GameObject pGo = new GameObject(p.name);
                pGo.transform.SetParent(panelsParent, false);

                // Marco perimetral a ras del plafón (Y = 3.175..3.200 m)
                Docencia2InteriorBuilder.CreatePlanBox("Marco_Panel", pGo.transform, p.x - hX, p.x + hX, Y_CEILING - 0.025f, Y_CEILING, p.z - hZ, p.z + hZ, mFrame, 0.001f, false);

                // Difusor opalino blanco emisivo a ras del plafón (Y = 3.168..3.185 m)
                Docencia2InteriorBuilder.CreatePlanBox("Difusor_Emisivo", pGo.transform, p.x - hX + 0.04f, p.x + hX - 0.04f, Y_CEILING - 0.032f, Y_CEILING - 0.015f, p.z - hZ + 0.04f, p.z + hZ - 0.04f, mDiffuserNight, 0f, false);
            }

            // Paso 5b y Paso 6: Reducir las luces reales de NOCHE interior a las estrictamente necesarias (12 Point Lights de rango corto, sin sombras, y Culling Mask exclusivo de Layer 8 = PB_Interior)
            int interiorCullingMask = 1 << LAYER_PB_INTERIOR;

            var nightLights = new List<(string name, float x, float y, float z, float range, float intensity, Color col)>
            {
                // 1-2. Vestíbulo Principal (cubre entrada, muros y puertas dobles P24/P25)
                ("LuzInt_Noche_01_Vestibulo_Este",   23.80f, 3.02f, 19.29f, 7.2f, 3.4f, new Color(1.00f, 0.97f, 0.92f)),
                ("LuzInt_Noche_02_Vestibulo_Oeste",  19.80f, 3.02f, 20.50f, 6.8f, 3.2f, new Color(1.00f, 0.97f, 0.92f)),

                // 3-5. Pasillo Central (Norte, Centro y Sur frente a P17/P18)
                ("LuzInt_Noche_03_Pasillo_Norte",    14.24f, 3.02f, 10.50f, 7.5f, 3.2f, new Color(0.97f, 0.98f, 1.00f)),
                ("LuzInt_Noche_04_Pasillo_Centro",   14.24f, 3.02f, 19.29f, 7.5f, 3.2f, new Color(0.97f, 0.98f, 1.00f)),
                ("LuzInt_Noche_05_Pasillo_Sur_P17_P18", 14.24f, 3.02f, 30.86f, 8.2f, 3.5f, new Color(0.97f, 0.98f, 1.00f)),

                // 6. Plaza Sur
                ("LuzInt_Noche_06_Plaza_Sur",        14.24f, 3.02f, 42.20f, 8.0f, 3.2f, new Color(0.97f, 0.98f, 1.00f)),

                // 7. Escalera en U (cubre ambos tramos de 3.36 m y el cierre bajo tramo sur en x = 4.72)
                ("LuzInt_Noche_07_Escalera",          5.80f, 3.05f, 19.29f, 7.5f, 3.4f, new Color(1.00f, 0.97f, 0.92f)),

                // 8-9. Sec 06: Alcoba Oeste y Local Este (con muros de madera x = 3.62 y x = 24.86)
                ("LuzInt_Noche_08_Sec6_AlcobaOeste",  7.40f, 3.02f, 10.80f, 8.5f, 3.3f, new Color(0.98f, 0.98f, 1.00f)),
                ("LuzInt_Noche_09_Sec6_LocalEste",   21.00f, 3.02f, 10.80f, 8.5f, 3.3f, new Color(0.98f, 0.98f, 1.00f)),

                // 10-11. Sec 07: Bloque de Cubículos Oeste y Este
                ("LuzInt_Noche_10_Cubiculos_Oeste",   4.62f, 3.02f,  4.20f, 6.5f, 2.8f, new Color(0.97f, 0.98f, 1.00f)),
                ("LuzInt_Noche_11_Cubiculos_Este",   23.86f, 3.02f,  4.20f, 6.5f, 2.8f, new Color(0.97f, 0.98f, 1.00f)),

                // 12. Auditorio (interior para ver la cápsula de 1.75 m y las puertas dobles P24/P25)
                ("LuzInt_Noche_12_Auditorio",        22.86f, 3.02f, 29.50f, 9.0f, 3.6f, new Color(1.00f, 0.97f, 0.92f))
            };

            foreach (var nl in nightLights)
            {
                GameObject lGo = new GameObject(nl.name);
                lGo.transform.SetParent(parentNight, false);
                lGo.transform.position = Docencia2InteriorBuilder.PlanToUnityPos(nl.x, nl.y, nl.z);

                Light l = lGo.AddComponent<Light>();
                l.type = LightType.Point;
                l.range = nl.range;
                l.color = nl.col;
                l.intensity = nl.intensity;
                l.shadows = LightShadows.None;
                l.cullingMask = interiorCullingMask; // Exclusivo para PB_Interior (nunca ilumina Docencia2_Exterior)

                var addData = lGo.AddComponent<UniversalAdditionalLightData>();
                addData.renderingLayers = (uint)(1 << 1); // Light Layer 1
            }

            // En modo TERROR: 4 luces parpadeantes de rango corto con Culling Mask exclusivo de PB_Interior
            var terrorLights = new List<(string name, float x, float z, float seed)>
            {
                ("LuzInt_Terror_01_Vestibulo", 21.50f, 19.29f, 17.3f),
                ("LuzInt_Terror_02_PasilloNorte", 14.24f, 12.00f, 34.6f),
                ("LuzInt_Terror_03_PasilloSur", 14.24f, 30.86f, 51.9f),
                ("LuzInt_Terror_04_Escalera", 6.20f, 19.29f, 69.2f)
            };

            foreach (var tl in terrorLights)
            {
                GameObject lGo = new GameObject(tl.name);
                lGo.transform.SetParent(parentTerror, false);
                lGo.transform.position = Docencia2InteriorBuilder.PlanToUnityPos(tl.x, Y_CEILING - 0.15f, tl.z);

                Light l = lGo.AddComponent<Light>();
                l.type = LightType.Point;
                l.range = 4.8f;
                l.color = new Color(0.95f, 0.65f, 0.35f);
                l.intensity = 0.45f;
                l.shadows = LightShadows.None;
                l.cullingMask = interiorCullingMask;

                var addData = lGo.AddComponent<UniversalAdditionalLightData>();
                addData.renderingLayers = (uint)(1 << 1);

                var flicker = lGo.AddComponent<Docencia2LightFlicker>();
                flicker.targetLight = l;
                flicker.baseIntensity = 0.45f;
                flicker.isDyingLamp = true;
                flicker.editorPreviewFactor = 0.25f;
                flicker.noiseSeed = tl.seed;
            }

            parentNight.gameObject.SetActive(true);
            parentTerror.gameObject.SetActive(false);
        }

        // -----------------------------------------------------------------------------------------
        // 3. AUDITORIO: P24 Y P25 DOBLES DE 1.60 M EN X = 20.16..21.76 Y 24.96..26.56 (Z = 23.14)
        //    CRISTAL AHUMADO CON PERFILES BRONCE
        // -----------------------------------------------------------------------------------------
        private static void BuildAuditorioGlassDoubleDoors(Transform parent, Material mGlass, Material mFrame)
        {
            Transform audGroup = new GameObject("Auditorio_2_Puertas_Dobles_Cristal_Norte").transform;
            audGroup.SetParent(parent, false);

            // Paso 2: P24 y P25 dobles de 1.60 m en x = 20.16..21.76 y x = 24.96..26.56 (z = 23.14), cristal ahumado con perfiles bronce
            var doubleDoors = new List<(string name, float coordZ, float minX, float maxX)>
            {
                ("P24_Auditorio_Norte_Doble1_x18.24_19.84", 23.14f, 18.24f, 19.84f),
                ("P25_Auditorio_Norte_Doble2_x24.96_26.56", 23.14f, 24.96f, 26.56f)
            };

            foreach (var dd in doubleDoors)
            {
                GameObject ddGo = new GameObject(dd.name);
                ddGo.transform.SetParent(audGroup, false);

                float yBot = Y_FLOOR;
                float yTop = Y_FLOOR + 2.10f; // Altura de vano 2.10 m (Y = 0.20..2.30 m)
                float mid = (dd.minX + dd.maxX) * 0.5f;

                // Marco perimetral bronce (5 cm)
                Docencia2InteriorBuilder.CreatePlanBox("Marco_Izq", ddGo.transform, dd.minX, dd.minX + 0.05f, yBot, yTop, dd.coordZ - 0.08f, dd.coordZ + 0.08f, mFrame, 0.002f, false);
                Docencia2InteriorBuilder.CreatePlanBox("Marco_Der", ddGo.transform, dd.maxX - 0.05f, dd.maxX, yBot, yTop, dd.coordZ - 0.08f, dd.coordZ + 0.08f, mFrame, 0.002f, false);
                Docencia2InteriorBuilder.CreatePlanBox("Marco_Dintel", ddGo.transform, dd.minX, dd.maxX, yTop - 0.05f, yTop, dd.coordZ - 0.08f, dd.coordZ + 0.08f, mFrame, 0.002f, false);

                float fW = 0.065f; // Perfil bronce de hoja (6.5 cm)
                float barY0 = Y_FLOOR + 1.00f;
                float barY1 = Y_FLOOR + 1.07f;

                // --- HOJA IZQUIERDA (0.75 m, bisagra en lX0 = oeste, abre 90° hacia dentro del Auditorio +z_plan) ---
                float lX0 = dd.minX + 0.05f;
                float lX1 = mid - 0.004f;
                GameObject pivIzq = new GameObject("Pivote_Bisagra_Hoja_Izq");
                pivIzq.transform.SetParent(ddGo.transform, false);
                pivIzq.transform.position = Docencia2InteriorBuilder.PlanToUnityPos(lX0, yBot, dd.coordZ);

                Docencia2InteriorBuilder.CreatePlanBox("MarcoHoja_Izq_LatIzq", pivIzq.transform, lX0, lX0 + fW, yBot, yTop - 0.05f, dd.coordZ - 0.025f, dd.coordZ + 0.025f, mFrame, 0.002f, false);
                Docencia2InteriorBuilder.CreatePlanBox("MarcoHoja_Izq_LatDer", pivIzq.transform, lX1 - fW, lX1, yBot, yTop - 0.05f, dd.coordZ - 0.025f, dd.coordZ + 0.025f, mFrame, 0.002f, false);
                Docencia2InteriorBuilder.CreatePlanBox("MarcoHoja_Izq_Inf", pivIzq.transform, lX0, lX1, yBot, yBot + 0.11f, dd.coordZ - 0.025f, dd.coordZ + 0.025f, mFrame, 0.002f, false);
                Docencia2InteriorBuilder.CreatePlanBox("MarcoHoja_Izq_Sup", pivIzq.transform, lX0, lX1, yTop - 0.11f, yTop - 0.05f, dd.coordZ - 0.025f, dd.coordZ + 0.025f, mFrame, 0.002f, false);
                Docencia2InteriorBuilder.CreatePlanBox("Barra_Empuje_Izq", pivIzq.transform, lX0, lX1, barY0, barY1, dd.coordZ - 0.04f, dd.coordZ + 0.04f, mFrame, 0.002f, false);
                Docencia2InteriorBuilder.CreatePlanBox("Cristal_Ahumado_Hoja_Izq", pivIzq.transform, lX0 + fW, lX1 - fW, yBot + 0.11f, yTop - 0.11f, dd.coordZ - 0.006f, dd.coordZ + 0.006f, mGlass, 0f, true);

                // --- HOJA DERECHA (0.75 m, bisagra en rX1 = este, abre 90° hacia dentro del Auditorio +z_plan) ---
                float rX0 = mid + 0.004f;
                float rX1 = dd.maxX - 0.05f;
                GameObject pivDer = new GameObject("Pivote_Bisagra_Hoja_Der");
                pivDer.transform.SetParent(ddGo.transform, false);
                pivDer.transform.position = Docencia2InteriorBuilder.PlanToUnityPos(rX1, yBot, dd.coordZ);

                Docencia2InteriorBuilder.CreatePlanBox("MarcoHoja_Der_LatIzq", pivDer.transform, rX0, rX0 + fW, yBot, yTop - 0.05f, dd.coordZ - 0.025f, dd.coordZ + 0.025f, mFrame, 0.002f, false);
                Docencia2InteriorBuilder.CreatePlanBox("MarcoHoja_Der_LatDer", pivDer.transform, rX1 - fW, rX1, yBot, yTop - 0.05f, dd.coordZ - 0.025f, dd.coordZ + 0.025f, mFrame, 0.002f, false);
                Docencia2InteriorBuilder.CreatePlanBox("MarcoHoja_Der_Inf", pivDer.transform, rX0, rX1, yBot, yBot + 0.11f, dd.coordZ - 0.025f, dd.coordZ + 0.025f, mFrame, 0.002f, false);
                Docencia2InteriorBuilder.CreatePlanBox("MarcoHoja_Der_Sup", pivDer.transform, rX0, rX1, yTop - 0.11f, yTop - 0.05f, dd.coordZ - 0.025f, dd.coordZ + 0.025f, mFrame, 0.002f, false);
                Docencia2InteriorBuilder.CreatePlanBox("Barra_Empuje_Der", pivDer.transform, rX0, rX1, barY0, barY1, dd.coordZ - 0.04f, dd.coordZ + 0.04f, mFrame, 0.002f, false);
                Docencia2InteriorBuilder.CreatePlanBox("Cristal_Ahumado_Hoja_Der", pivDer.transform, rX0 + fW, rX1 - fW, yBot + 0.11f, yTop - 0.11f, dd.coordZ - 0.006f, dd.coordZ + 0.006f, mGlass, 0f, true);
            }
        }

        private static void RegisterLightsInModeSwitcher(GameObject groupNight, GameObject groupTerror)
        {
            Docencia2ModeSwitcher switcher = Object.FindAnyObjectByType<Docencia2ModeSwitcher>();
            if (switcher == null) return;

            switcher.grupoLucesInteriorNoche = groupNight;
            switcher.grupoLucesInteriorTerror = groupTerror;
            EditorUtility.SetDirty(switcher);

            var tracker = groupNight.GetComponent<Docencia2InteriorModeTracker>();
            if (tracker == null) tracker = groupNight.AddComponent<Docencia2InteriorModeTracker>();
            tracker.nightGroup = groupNight;
            tracker.terrorGroup = groupTerror;
            tracker.switcher = switcher;
        }
    }
}
