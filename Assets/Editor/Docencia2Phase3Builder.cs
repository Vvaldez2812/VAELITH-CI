// Assets/Editor/Docencia2Phase3Builder.cs
// Implementación modular y procedural de la FASE 3 (actualizada con las correcciones de la Parte 2 + Sentido de Apertura de Puertas):
// C1. Puertas P1-P26 + P27* en muro de madera este (marco de madera 5 cm, hoja 0.86 x 2.10 m en vano de 0.96 m, mirilla superior de cristal en aulas, chapa metálica).
//     - Todas las puertas interiores (P1..P23, P26, P27*) tienen un nodo pivote ("Pivote_Bisagra") en su borde de bisagra y pueden rotar 90° HACIA DENTRO del espacio al que dan acceso.
//     - P17 (z = 29.40..30.36 m) y P18 (z = 31.36..32.32 m) a 0.30 m de la cara del pilar z = 30.86 m.
//     - Eliminada P_Pasillo_Este_Local (el pasillo de cubículos este queda abierto en z = 7.71 m).
//     - Añadidas P26 (muro madera oeste x = 3.62 m, z = 8.83..9.79 m) y P27* (muro madera este x = 24.86 m, z = 8.83..9.79 m).
// C2. Cancelería interior de madera hasta 0.90 m + vidrio fijo hasta plafón (Y_CEILING = 3.20 m) con doble franja verde institucional.
// C3. Ventanas exteriores vistas desde dentro: 6 paneles por aula, rejas de protección de herrería en PB, retraídas al interior del forro (x = 0.42 m Oeste y x = 28.06 m Este).
//     - Portal de entrada interior: conserva las jambas de 1.60 m (z = 18.49..20.09) hasta recibir el OK del usuario, pero cada hoja cuenta con su nodo "Pivote_Vaiven" para giro ±90° y travesaño + barra de empuje + doble franja verde con línea guinda debajo.

using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Vaelith.Docencia2
{
    public static class Docencia2Phase3Builder
    {
        public const float DOOR_WIDTH = 0.96f;
        public const float DOOR_HEIGHT = 2.10f;
        public const float FRAME_THICK = 0.05f;   // 5 cm
        public const float FRAME_DEPTH = 0.16f;   // cubre el muro de 15 cm
        public const float LEAF_THICK = 0.04f;    // 4 cm de hoja de madera

        public const float Y_FLOOR = 0.20f;
        public const float Y_CEILING = 3.20f;

        public static void BuildPhase3(GameObject pbRoot, Dictionary<string, Transform> zones, Dictionary<string, Material> mats)
        {
            Transform p3Root = pbRoot.transform.Find("03_Fase3_Carpinteria_y_Vidrios");
            if (p3Root != null)
            {
                Object.DestroyImmediate(p3Root.gameObject);
            }
            GameObject goRoot = new GameObject("03_Fase3_Carpinteria_y_Vidrios");
            goRoot.transform.SetParent(pbRoot.transform, false);
            p3Root = goRoot.transform;

            Transform doorsGroup = new GameObject("Puertas_P1_P27").transform;
            doorsGroup.SetParent(p3Root, false);

            Transform cancelsGroup = new GameObject("Canceleria_Interior_Vidrio_Verde").transform;
            cancelsGroup.SetParent(p3Root, false);

            Transform windowsGroup = new GameObject("Ventanas_Exteriores_y_Rejas_PB").transform;
            windowsGroup.SetParent(p3Root, false);

            // 1. Obtener o crear materiales específicos de Fase 3
            Material matWoodDoor = GetOrCreateMaterial("Mat_Int_Puerta_Madera_Barniz.mat", new Color(0.95f, 0.72f, 0.45f, 1f), 0.30f, 0.0f);
            Texture2D texWood = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Textures/Docencia2_Madera_Lambrin_Sec6.png");
            Texture2D texWoodNorm = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Textures/Docencia2_Madera_Lambrin_Sec6_Normal.png");
            if (texWood != null)
            {
                matWoodDoor.SetTexture("_BaseMap", texWood);
                matWoodDoor.SetTextureScale("_BaseMap", new Vector2(1f, 1f));
                if (texWoodNorm != null)
                {
                    matWoodDoor.SetTexture("_BumpMap", texWoodNorm);
                    matWoodDoor.SetFloat("_BumpScale", 0.42f);
                    matWoodDoor.EnableKeyword("_NORMALMAP");
                }
                EditorUtility.SetDirty(matWoodDoor);
            }
            Material matFrame = GetOrCreateMaterial("Mat_Int_Marco_Madera_Oscura.mat", new Color(0.38f, 0.27f, 0.18f, 1f), 0.26f, 0.0f);
            Material matHandle = GetOrCreateMaterial("Mat_Int_Chapa_Acero_Inox.mat", new Color(0.85f, 0.85f, 0.86f, 1f), 0.85f, 0.95f);
            Material matGlassClear = GetOrCreateMaterial("Mat_Int_Vidrio_Interior_Claro.mat", new Color(0.90f, 0.94f, 0.96f, 0.25f), 0.90f, 0.0f, true);
            Material matGreenStripe = GetOrCreateMaterial("Mat_Int_Franja_Verde_Institucional.mat", new Color(0.06f, 0.32f, 0.18f, 1f), 0.50f, 0.0f);
            Material matGuindaStripe = GetOrCreateMaterial("Mat_Int_Linea_Guinda_Institucional.mat", new Color(0.42f, 0.07f, 0.14f, 1f), 0.50f, 0.0f);
            Material matBronzeFrame = GetOrCreateMaterial("Mat_Int_Marco_Puerta_Bronce.mat", new Color(0.32f, 0.23f, 0.14f, 1f), 0.55f, 0.45f);
            Material matReja = GetOrCreateMaterial("Mat_Int_Herreria_Reja_Proteccion_PB.mat", new Color(0.18f, 0.19f, 0.20f, 1f), 0.30f, 0.80f);
            Material matWinFrame = GetOrCreateMaterial("Mat_Int_Marco_Ventana_Claro.mat", new Color(0.82f, 0.83f, 0.82f, 1f), 0.30f, 0.05f);

            // 2. Construir C1: Puertas de madera P1-P14, P17-P23 y P26/P27* (en los muros de madera x = 3.62 y x = 24.86)
            BuildAllDoors(doorsGroup, matFrame, matWoodDoor, matHandle, matGlassClear);

            // 3. Construir C2: Cancelería interior con zócalo y franjas verdes hasta Y = 3.20 m + Canceles de vidrio templado de piso a plafón en Sec 06 pegados al Pasillo Central (x = 11.24 con P15 y x = 17.24 con P16)
            BuildInteriorCancels(cancelsGroup, matWoodDoor, matGlassClear, matGreenStripe, matGuindaStripe, matFrame, matBronzeFrame, matHandle);

            // 4. Construir C3: Ventanas interiores con 6 paneles, rejas en PB y portal de entrada
            BuildPBWindowsAndGrilles(windowsGroup, matWinFrame, matBronzeFrame, matGlassClear, matGreenStripe, matGuindaStripe, matReja);
        }

        public static Material GetOrCreateMaterial(string name, Color color, float smoothness, float metallic, bool transparent = false)
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
                m.SetFloat("_Surface", 1);
                m.SetFloat("_Blend", 0);
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

            m.enableInstancing = true;
            EditorUtility.SetDirty(m);
            return m;
        }

        // -----------------------------------------------------------------------------------------
        // C1. PUERTAS CON PIVOTE DE BISAGRA (GIRO 90° HACIA DENTRO DEL ESPACIO, HOJA PEGADA AL MURO)
        // -----------------------------------------------------------------------------------------
        private struct DoorDef
        {
            public string name;
            public float x, z;
            public bool isAlongX;        // True: muro corre en X (ej. z = 46.29); False: muro corre en Z (ej. x = 11.24)
            public float zMin, zMax;     // Rango en Z si !isAlongX
            public float xMin, xMax;     // Rango en X si isAlongX
            public bool hasVisionGlass;  // Mirilla superior de aula
            public bool hingeAtMinCoord; // True: bisagra en zMin (o xMin); False: bisagra en zMax (o xMax)
            public int inwardSign;       // -1: abre hacia -x_plan (o -z_plan); +1: abre hacia +x_plan (o +z_plan)
            public float initialOpenDeg; // 0f = cerrada (con pivote listo para abrir 90° hacia dentro)
        }

        private static void BuildAllDoors(Transform parent, Material mFrame, Material mLeaf, Material mHandle, Material mGlass)
        {
            // Todas las puertas abren HACIA DENTRO del espacio al que dan acceso y su bisagra se coloca del lado del muro/esquina más cercano para que al girar 90° la hoja quede pegada al muro.
            // Nota: Los cubículos A1-A3, B1-B3, C1-C3 y D1-D3 llevan frentes de cancel con antepecho de madera abajo, ventana de cristal arriba y puerta de cristal a la derecha mirando de frente (construidos en BuildInteriorCancels).
            var doors = new List<DoorDef>
            {
                // Baños (nicho x = 12.24 abre hacia Baño Mujeres x < 12.24 -> inwardSign = -1; x = 16.24 abre hacia Baño Hombres x > 16.24 -> inwardSign = +1; bisagra en zMax = 7.46 junto al muro sur z = 7.71)
                new DoorDef { name = "P13_Bano_Mujeres", x = 12.24f, isAlongX = false, zMin = 6.50f, zMax = 7.46f, hasVisionGlass = false, hingeAtMinCoord = false, inwardSign = -1, initialOpenDeg = 0f },
                new DoorDef { name = "P14_Bano_Hombres", x = 16.24f, isAlongX = false, zMin = 6.50f, zMax = 7.46f, hasVisionGlass = false, hingeAtMinCoord = false, inwardSign = +1 },

                // Sec 06: Puertas P26 (muro de madera oeste x = 3.62, abre hacia dentro del Espacio Oeste x < 3.62 -> inwardSign = -1)
                // y P27* (muro de madera este x = 24.86, abre hacia dentro del Espacio Este x > 24.86 -> inwardSign = +1),
                // ambas en la parte norte z = 8.83..9.79 m, alineadas con las puertas de cristal P15 (x = 11.24) y P16 (x = 17.24):
                new DoorDef { name = "P26_Muro_Madera_Oeste", x = 3.62f, isAlongX = false, zMin = 8.83f, zMax = 9.79f, hasVisionGlass = false, hingeAtMinCoord = true, inwardSign = -1 },
                new DoorDef { name = "P27_Supuesto_Muro_Madera_Este", x = 24.86f, isAlongX = false, zMin = 8.83f, zMax = 9.79f, hasVisionGlass = false, hingeAtMinCoord = true, inwardSign = +1 },

                // Aulas 7 y 6 (muro x = 11.24, abren hacia dentro de las aulas x < 11.24 -> inwardSign = -1):
                // - P17 (z = 29.40..30.36): bisagra en zMax = 30.36 (a 0.30 m del pilar y a 0.425 m del tabique z = 30.86), de modo que al abrir 90° la hoja queda pegada al tabique divisorio z = 30.86 sin chocar con él (fotos 11.01.27 PM (2) y (3)).
                // - P18 (z = 31.36..32.32): bisagra en zMin = 31.36 (a 0.30 m del pilar y a 0.425 m del tabique z = 30.86), de modo que al abrir 90° la hoja queda pegada al tabique divisorio z = 30.86 (en la foto 11.01.27 PM (3) la manija se ve a la izquierda = sur zMax y la bisagra a la derecha = norte zMin).
                new DoorDef { name = "P17_Aula_7", x = 11.24f, isAlongX = false, zMin = 29.40f, zMax = 30.36f, hasVisionGlass = true, hingeAtMinCoord = false, inwardSign = -1 },
                new DoorDef { name = "P18_Aula_6", x = 11.24f, isAlongX = false, zMin = 31.36f, zMax = 32.32f, hasVisionGlass = true, hingeAtMinCoord = true,  inwardSign = -1 },

                // Aulas 5 y 1 (muros x = 8.00 y x = 20.48, z = 38.63..39.59):
                // Ambas están a solo 0.06 m del muro transversal z = 38.57 (38.63 - 38.57 = 0.06 m, y la cara interior del muro z = 38.57 está en z = 38.645 m).
                // Bisagra en zMin = 38.63 para que al abrir 90° hacia dentro del aula la hoja quede pegada al muro norte del aula (z = 38.57).
                new DoorDef { name = "P19_Aula_5", x = 8.00f,  isAlongX = false, zMin = 38.88f, zMax = 39.84f, hasVisionGlass = true, hingeAtMinCoord = true, inwardSign = -1 },
                new DoorDef { name = "P23_Aula_1", x = 20.48f, isAlongX = false, zMin = 38.88f, zMax = 39.84f, hasVisionGlass = true, hingeAtMinCoord = true, inwardSign = +1 },

                // Aulas Sur 4, 3, 2 (muro z = 46.29, abren hacia dentro z > 46.29 -> inwardSign = +1):
                // - P20 (Aula 4, x = 8.24..9.20): tabique divisorio Aula 4/3 en x = 9.49. Bisagra en xMax = 9.20 para pegar al tabique x = 9.49.
                // - P21 (Aula 3, x = 9.76..10.72): tabique divisorio Aula 4/3 en x = 9.49. Bisagra en xMin = 9.76 para pegar al tabique x = 9.49.
                // - P22 (Aula 2, x = 19.20..20.16): tabique divisorio Aula 3/2 en x = 18.99. Bisagra en xMin = 19.20 para pegar al tabique x = 18.99.
                new DoorDef { name = "P20_Aula_4", z = 46.29f, isAlongX = true, xMin = 8.24f,  xMax = 9.20f,  hasVisionGlass = true, hingeAtMinCoord = false, inwardSign = +1 },
                new DoorDef { name = "P21_Aula_3", z = 46.29f, isAlongX = true, xMin = 9.76f,  xMax = 10.72f, hasVisionGlass = true, hingeAtMinCoord = true,  inwardSign = +1 },
                new DoorDef { name = "P22_Aula_2", z = 46.29f, isAlongX = true, xMin = 19.20f, xMax = 20.16f, hasVisionGlass = true, hingeAtMinCoord = true,  inwardSign = +1 }
            };

            foreach (var d in doors)
            {
                GameObject dGo = new GameObject(d.name);
                dGo.transform.SetParent(parent, false);

                float yBot = Y_FLOOR;
                float yTop = Y_FLOOR + DOOR_HEIGHT;

                if (d.isAlongX)
                {
                    float x0 = d.xMin;
                    float x1 = d.xMax;
                    float z = d.z;

                    // Marcos fijos (no se combinan con la hoja móvil)
                    Docencia2InteriorBuilder.CreatePlanBox("Marco_Izq", dGo.transform, x0, x0 + FRAME_THICK, yBot, yTop + FRAME_THICK, z - FRAME_DEPTH * 0.5f, z + FRAME_DEPTH * 0.5f, mFrame, 0.002f, false);
                    Docencia2InteriorBuilder.CreatePlanBox("Marco_Der", dGo.transform, x1 - FRAME_THICK, x1, yBot, yTop + FRAME_THICK, z - FRAME_DEPTH * 0.5f, z + FRAME_DEPTH * 0.5f, mFrame, 0.002f, false);
                    Docencia2InteriorBuilder.CreatePlanBox("Marco_Dintel", dGo.transform, x0, x1, yTop, yTop + FRAME_THICK, z - FRAME_DEPTH * 0.5f, z + FRAME_DEPTH * 0.5f, mFrame, 0.002f, false);

                    float leafX0 = x0 + FRAME_THICK;
                    float leafX1 = x1 - FRAME_THICK;
                    float hingeX = d.hingeAtMinCoord ? leafX0 : leafX1;

                    // Pivote de bisagra para giro de 90° hacia dentro
                    GameObject pivotGo = new GameObject("Pivote_Bisagra");
                    pivotGo.transform.SetParent(dGo.transform, false);
                    pivotGo.transform.position = Docencia2InteriorBuilder.PlanToUnityPos(hingeX, yBot, z);

                    if (d.hasVisionGlass)
                    {
                        float midX = (leafX0 + leafX1) * 0.5f;
                        float gX0 = midX - 0.10f;
                        float gX1 = midX + 0.10f;
                        float gY0 = Y_FLOOR + 1.30f;
                        float gY1 = Y_FLOOR + 1.90f;

                        Docencia2InteriorBuilder.CreatePlanBox("Hoja_Madera_Inf", pivotGo.transform, leafX0, leafX1, yBot, gY0, z - LEAF_THICK * 0.5f, z + LEAF_THICK * 0.5f, mLeaf, 0.002f, true);
                        Docencia2InteriorBuilder.CreatePlanBox("Hoja_Madera_Sup", pivotGo.transform, leafX0, leafX1, gY1, yTop, z - LEAF_THICK * 0.5f, z + LEAF_THICK * 0.5f, mLeaf, 0.002f, false);
                        Docencia2InteriorBuilder.CreatePlanBox("Hoja_Madera_LatIzq", pivotGo.transform, leafX0, gX0, gY0, gY1, z - LEAF_THICK * 0.5f, z + LEAF_THICK * 0.5f, mLeaf, 0.002f, false);
                        Docencia2InteriorBuilder.CreatePlanBox("Hoja_Madera_LatDer", pivotGo.transform, gX1, leafX1, gY0, gY1, z - LEAF_THICK * 0.5f, z + LEAF_THICK * 0.5f, mLeaf, 0.002f, false);
                        Docencia2InteriorBuilder.CreatePlanBox("Mirilla_Vidrio", pivotGo.transform, gX0, gX1, gY0, gY1, z - 0.006f, z + 0.006f, mGlass, 0f, false);
                    }
                    else
                    {
                        Docencia2InteriorBuilder.CreatePlanBox("Hoja_Madera_Solida", pivotGo.transform, leafX0, leafX1, yBot, yTop, z - LEAF_THICK * 0.5f, z + LEAF_THICK * 0.5f, mLeaf, 0.002f, true);
                    }

                    // Manija en el lado opuesto a la bisagra
                    float handleX = d.hingeAtMinCoord ? (leafX1 - 0.08f) : (leafX0 + 0.08f);
                    Docencia2InteriorBuilder.CreatePlanBox("Manija_Frente", pivotGo.transform, handleX - 0.03f, handleX + 0.03f, Y_FLOOR + 1.00f, Y_FLOOR + 1.10f, z + LEAF_THICK * 0.5f, z + LEAF_THICK * 0.5f + 0.05f, mHandle, 0.002f, false);
                    Docencia2InteriorBuilder.CreatePlanBox("Manija_Atras", pivotGo.transform, handleX - 0.03f, handleX + 0.03f, Y_FLOOR + 1.00f, Y_FLOOR + 1.10f, z - LEAF_THICK * 0.5f - 0.05f, z - LEAF_THICK * 0.5f, mHandle, 0.002f, false);

                    if (Mathf.Abs(d.initialOpenDeg) > 0.01f)
                    {
                        float sign = (d.hingeAtMinCoord ? 1f : -1f) * d.inwardSign;
                        pivotGo.transform.localRotation = Quaternion.Euler(0f, sign * d.initialOpenDeg, 0f);
                    }
                }
                else
                {
                    float z0 = d.zMin;
                    float z1 = d.zMax;
                    float x = d.x;

                    Docencia2InteriorBuilder.CreatePlanBox("Marco_Izq", dGo.transform, x - FRAME_DEPTH * 0.5f, x + FRAME_DEPTH * 0.5f, yBot, yTop + FRAME_THICK, z0, z0 + FRAME_THICK, mFrame, 0.002f, false);
                    Docencia2InteriorBuilder.CreatePlanBox("Marco_Der", dGo.transform, x - FRAME_DEPTH * 0.5f, x + FRAME_DEPTH * 0.5f, yBot, yTop + FRAME_THICK, z1 - FRAME_THICK, z1, mFrame, 0.002f, false);
                    Docencia2InteriorBuilder.CreatePlanBox("Marco_Dintel", dGo.transform, x - FRAME_DEPTH * 0.5f, x + FRAME_DEPTH * 0.5f, yTop, yTop + FRAME_THICK, z0, z1, mFrame, 0.002f, false);

                    float leafZ0 = z0 + FRAME_THICK;
                    float leafZ1 = z1 - FRAME_THICK;
                    float hingeZ = d.hingeAtMinCoord ? leafZ0 : leafZ1;

                    GameObject pivotGo = new GameObject("Pivote_Bisagra");
                    pivotGo.transform.SetParent(dGo.transform, false);
                    pivotGo.transform.position = Docencia2InteriorBuilder.PlanToUnityPos(x, yBot, hingeZ);

                    if (d.hasVisionGlass)
                    {
                        float midZ = (leafZ0 + leafZ1) * 0.5f;
                        float gZ0 = midZ - 0.10f;
                        float gZ1 = midZ + 0.10f;
                        float gY0 = Y_FLOOR + 1.30f;
                        float gY1 = Y_FLOOR + 1.90f;

                        Docencia2InteriorBuilder.CreatePlanBox("Hoja_Madera_Inf", pivotGo.transform, x - LEAF_THICK * 0.5f, x + LEAF_THICK * 0.5f, yBot, gY0, leafZ0, leafZ1, mLeaf, 0.002f, true);
                        Docencia2InteriorBuilder.CreatePlanBox("Hoja_Madera_Sup", pivotGo.transform, x - LEAF_THICK * 0.5f, x + LEAF_THICK * 0.5f, gY1, yTop, leafZ0, leafZ1, mLeaf, 0.002f, false);
                        Docencia2InteriorBuilder.CreatePlanBox("Hoja_Madera_LatIzq", pivotGo.transform, x - LEAF_THICK * 0.5f, x + LEAF_THICK * 0.5f, gY0, gY1, leafZ0, gZ0, mLeaf, 0.002f, false);
                        Docencia2InteriorBuilder.CreatePlanBox("Hoja_Madera_LatDer", pivotGo.transform, x - LEAF_THICK * 0.5f, x + LEAF_THICK * 0.5f, gY0, gY1, gZ1, leafZ1, mLeaf, 0.002f, false);
                        Docencia2InteriorBuilder.CreatePlanBox("Mirilla_Vidrio", pivotGo.transform, x - 0.006f, x + 0.006f, gY0, gY1, gZ0, gZ1, mGlass, 0f, false);
                    }
                    else
                    {
                        Docencia2InteriorBuilder.CreatePlanBox("Hoja_Madera_Solida", pivotGo.transform, x - LEAF_THICK * 0.5f, x + LEAF_THICK * 0.5f, yBot, yTop, leafZ0, leafZ1, mLeaf, 0.002f, true);
                    }

                    // Manija en el lado opuesto a la bisagra
                    float handleZ = d.hingeAtMinCoord ? (leafZ1 - 0.08f) : (leafZ0 + 0.08f);
                    Docencia2InteriorBuilder.CreatePlanBox("Manija_Frente", pivotGo.transform, x + LEAF_THICK * 0.5f, x + LEAF_THICK * 0.5f + 0.05f, Y_FLOOR + 1.00f, Y_FLOOR + 1.10f, handleZ - 0.03f, handleZ + 0.03f, mHandle, 0.002f, false);
                    Docencia2InteriorBuilder.CreatePlanBox("Manija_Atras", pivotGo.transform, x - LEAF_THICK * 0.5f - 0.05f, x - LEAF_THICK * 0.5f, Y_FLOOR + 1.00f, Y_FLOOR + 1.10f, handleZ - 0.03f, handleZ + 0.03f, mHandle, 0.002f, false);

                    if (Mathf.Abs(d.initialOpenDeg) > 0.01f)
                    {
                        float sign = (d.hingeAtMinCoord ? -1f : 1f) * d.inwardSign;
                        pivotGo.transform.localRotation = Quaternion.Euler(0f, sign * d.initialOpenDeg, 0f);
                    }
                }
            }
        }

        // -----------------------------------------------------------------------------------------
        // C2. CANCELERÍA INTERIOR DE MADERA Y VIDRIO FIJO + CANCELES DE VIDRIO TEMPLADO SEC 06 PEGADOS AL PASILLO CENTRAL (HASTA PLAFÓN Y = 3.20 m)
        // -----------------------------------------------------------------------------------------
        private static void BuildInteriorCancels(Transform parent, Material mWood, Material mGlass, Material mGreen, Material mGuinda, Material mFrame, Material mBronzeFrame, Material mHandle)
        {
            float yBot = Y_FLOOR;
            float yWoodTop = Y_FLOOR + 1.00f;       // Antepecho de madera a 1.00 m sobre el piso (Y = 1.20 m) como en la foto real
            float yDoorTop = Y_FLOOR + DOOR_HEIGHT; // 2.30 m
            float yCeil = Y_CEILING;                // 3.20 m
            float fHalf = 0.025f;
            float fThick = 0.038f;

            // Helper para construir un frente de cubículo a lo largo de Z (ej. x = 3.62, 5.62, 22.86, 24.86):
            // Tiene parte de ventana con antepecho de madera abajo + cristal arriba, y puerta de cristal a la DERECHA mirándolo de frente:
            // - Si el cubículo está al OESTE del pasillo (inwardSign == -1, miramos hacia -X / Oeste): la derecha del observador es el NORTE (zMin), por lo que la puerta va en z0..z0+0.96 y la ventana con madera abajo va en z0+0.96..z1.
            // - Si el cubículo está al ESTE del pasillo (inwardSign == +1, miramos hacia +X / Este): la derecha del observador es el SUR (zMax), por lo que la ventana con madera abajo va en z0..z1-0.96 y la puerta va en z1-0.96..z1.
            void BuildCubicleFrontAlongZ(string cubicleName, string doorName, float x, float z0, float z1, bool doorAtMinZ, int inwardSign)
            {
                GameObject cGo = new GameObject(cubicleName);
                cGo.transform.SetParent(parent, false);

                float dZ0 = doorAtMinZ ? z0 + .08f : (z1 - DOOR_WIDTH - .08f);
                float dZ1 = dZ0 + DOOR_WIDTH;
                float wZ0 = doorAtMinZ ? dZ1 : z0;
                float wZ1 = doorAtMinZ ? z1 : dZ0;
                // Fase A: 8 cm approved reveal protects the 90-degree swing from the transverse partition.
                float rem0 = doorAtMinZ ? z0 : dZ1, rem1 = doorAtMinZ ? dZ0 : z1;
                Docencia2InteriorBuilder.CreatePlanBox("Antepecho_Remate_Lateral", cGo.transform, x-.042f,x+.042f,yBot,yWoodTop,rem0,rem1,mWood,.001f,true);
                Docencia2InteriorBuilder.CreatePlanBox("Vidrio_Remate_Lateral", cGo.transform, x-.005f,x+.005f,yWoodTop+.02f,yDoorTop-.02f,rem0,rem1,mGlass,0,false);

                // 1. Tramo de ventana con antepecho de madera abajo (Y = 0.20..1.20 m) y cristal fijo arriba (Y = 1.20..3.20 m)
                Docencia2InteriorBuilder.CreatePlanBox("Antepecho_Madera", cGo.transform, x - 0.042f, x + 0.042f, yBot, yWoodTop, wZ0, wZ1, mWood, 0.002f, true);
                Docencia2InteriorBuilder.CreatePlanBox("Zoclo_Madera_Inf", cGo.transform, x - 0.046f, x + 0.046f, yBot, yBot + 0.07f, wZ0, wZ1, mFrame, 0.001f, false);
                Docencia2InteriorBuilder.CreatePlanBox("Riel_Medio_Antepecho", cGo.transform, x - fHalf, x + fHalf, yWoodTop - 0.02f, yWoodTop + 0.02f, wZ0, wZ1, mBronzeFrame, 0.001f, false);
                Docencia2InteriorBuilder.CreatePlanBox("Vidrio_Ventana_Cubiculo", cGo.transform, x - 0.005f, x + 0.005f, yWoodTop + 0.02f, yDoorTop - 0.02f, wZ0 + fThick * 0.5f, wZ1 - fThick * 0.5f, mGlass, 0f, true);

                // Riel superior a ras de plafón, travesaño de dintel corrido y cristal fijo superior (imposta) a todo lo ancho del cubículo
                Docencia2InteriorBuilder.CreatePlanBox("Riel_Sup_Plafon", cGo.transform, x - fHalf, x + fHalf, yCeil - 0.04f, yCeil, z0, z1, mBronzeFrame, 0.001f, false);
                Docencia2InteriorBuilder.CreatePlanBox("Travesano_Dintel", cGo.transform, x - fHalf, x + fHalf, yDoorTop - 0.02f, yDoorTop + 0.02f, z0, z1, mBronzeFrame, 0.001f, false);
                Docencia2InteriorBuilder.CreatePlanBox("Vidrio_Imposta_Sup", cGo.transform, x - 0.005f, x + 0.005f, yDoorTop + 0.02f, yCeil - 0.04f, z0 + fThick * 0.5f, z1 - fThick * 0.5f, mGlass, 0f, false);

                // Montantes verticales en bronce (extremos z0, z1, jamba entre ventana y puerta, y montante intermedio de ventana)
                float midWZ = (wZ0 + wZ1) * 0.5f;
                float[] posts = new float[] { z0 + fThick * 0.5f, dZ0, dZ1, midWZ, z1 - fThick * 0.5f };
                for (int i = 0; i < posts.Length; i++)
                {
                    float zp = posts[i];
                    float yPostBot = (Mathf.Abs(zp - midWZ) < 0.02f) ? yWoodTop : yBot;
                    Docencia2InteriorBuilder.CreatePlanBox($"Montante_{i:02d}", cGo.transform, x - fHalf, x + fHalf, yPostBot, yCeil, zp - fThick * 0.5f, zp + fThick * 0.5f, mBronzeFrame, 0.001f, false);
                }

                // 2. Puerta de cristal completa con marco bronce (en el extremo derecho mirando de frente) y Pivote_Bisagra que abre 90° hacia dentro del cubículo
                GameObject dGo = new GameObject(doorName);
                dGo.transform.SetParent(cGo.transform, false);

                float leafZ0 = dZ0 + fThick * 0.5f;
                float leafZ1 = dZ1 - fThick * 0.5f;
                float hingeZ = doorAtMinZ ? leafZ0 : leafZ1; // Bisagra pegada al muro divisorio del cubículo

                GameObject pivotGo = new GameObject("Pivote_Bisagra");
                pivotGo.transform.SetParent(dGo.transform, false);
                pivotGo.transform.position = Docencia2InteriorBuilder.PlanToUnityPos(x, yBot, hingeZ);

                float dFrameW = 0.040f;
                float dHalfX = 0.022f;
                Docencia2InteriorBuilder.CreatePlanBox("Perfil_Hoja_Norte", pivotGo.transform, x - dHalfX, x + dHalfX, yBot + 0.005f, yDoorTop - 0.01f, leafZ0, leafZ0 + dFrameW, mBronzeFrame, 0.001f, false);
                Docencia2InteriorBuilder.CreatePlanBox("Perfil_Hoja_Sur",   pivotGo.transform, x - dHalfX, x + dHalfX, yBot + 0.005f, yDoorTop - 0.01f, leafZ1 - dFrameW, leafZ1, mBronzeFrame, 0.001f, false);
                Docencia2InteriorBuilder.CreatePlanBox("Perfil_Hoja_Inf",   pivotGo.transform, x - dHalfX, x + dHalfX, yBot + 0.005f, yBot + 0.065f, leafZ0 + dFrameW, leafZ1 - dFrameW, mBronzeFrame, 0.001f, false);
                Docencia2InteriorBuilder.CreatePlanBox("Perfil_Hoja_Sup",   pivotGo.transform, x - dHalfX, x + dHalfX, yDoorTop - 0.055f, yDoorTop - 0.01f, leafZ0 + dFrameW, leafZ1 - dFrameW, mBronzeFrame, 0.001f, false);
                Docencia2InteriorBuilder.CreatePlanBox("Cristal_Hoja",      pivotGo.transform, x - 0.005f, x + 0.005f, yBot + 0.065f, yDoorTop - 0.055f, leafZ0 + dFrameW, leafZ1 - dFrameW, mGlass, 0f, true);

                float handleZ = doorAtMinZ ? (leafZ1 - 0.07f) : (leafZ0 + 0.07f);
                Docencia2InteriorBuilder.CreatePlanBox("Manija_Ext", pivotGo.transform, x + dHalfX, x + dHalfX + 0.025f, Y_FLOOR + 0.95f, Y_FLOOR + 1.12f, handleZ - 0.015f, handleZ + 0.015f, mHandle, 0.001f, false);
                Docencia2InteriorBuilder.CreatePlanBox("Manija_Int", pivotGo.transform, x - dHalfX - 0.025f, x - dHalfX, Y_FLOOR + 0.95f, Y_FLOOR + 1.12f, handleZ - 0.015f, handleZ + 0.015f, mHandle, 0.001f, false);
            }

            // Helper histórico (reemplazado por cancelería abierta de sección 6)
#pragma warning disable CS8321
            void BuildCubicleCorridorEntryAlongX(string cancelName, string doorName, float z, float x0, float x1)
#pragma warning restore CS8321
            {
                GameObject cGo = new GameObject(cancelName);
                cGo.transform.SetParent(parent, false);

                float dX0 = x1 - DOOR_WIDTH; // Puerta a la derecha mirando hacia el norte (+X)
                float dX1 = x1;
                float wX0 = x0;              // Ventana con antepecho de madera a la izquierda
                float wX1 = dX0;

                Docencia2InteriorBuilder.CreatePlanBox("Antepecho_Madera_Izq", cGo.transform, wX0, wX1, yBot, yWoodTop, z - 0.042f, z + 0.042f, mWood, 0.002f, true);
                Docencia2InteriorBuilder.CreatePlanBox("Zoclo_Madera_Inf",     cGo.transform, wX0, wX1, yBot, yBot + 0.07f, z - 0.046f, z + 0.046f, mFrame, 0.001f, false);
                Docencia2InteriorBuilder.CreatePlanBox("Riel_Medio_Antepecho", cGo.transform, wX0, wX1, yWoodTop - 0.02f, yWoodTop + 0.02f, z - fHalf, z + fHalf, mBronzeFrame, 0.001f, false);
                Docencia2InteriorBuilder.CreatePlanBox("Vidrio_Ventana_Izq",   cGo.transform, wX0 + fThick * 0.5f, wX1 - fThick * 0.5f, yWoodTop + 0.02f, yDoorTop - 0.02f, z - 0.005f, z + 0.005f, mGlass, 0f, true);

                Docencia2InteriorBuilder.CreatePlanBox("Riel_Sup_Plafon",  cGo.transform, x0, x1, yCeil - 0.04f, yCeil, z - fHalf, z + fHalf, mBronzeFrame, 0.001f, false);
                Docencia2InteriorBuilder.CreatePlanBox("Travesano_Dintel", cGo.transform, x0, x1, yDoorTop - 0.02f, yDoorTop + 0.02f, z - fHalf, z + fHalf, mBronzeFrame, 0.001f, false);
                Docencia2InteriorBuilder.CreatePlanBox("Vidrio_Imposta_Sup", cGo.transform, x0 + fThick * 0.5f, x1 - fThick * 0.5f, yDoorTop + 0.02f, yCeil - 0.04f, z - 0.005f, z + 0.005f, mGlass, 0f, false);

                float[] postsX = new float[] { x0 + fThick * 0.5f, dX0, x1 - fThick * 0.5f };
                for (int i = 0; i < postsX.Length; i++)
                {
                    float xp = postsX[i];
                    Docencia2InteriorBuilder.CreatePlanBox($"Montante_{i:02d}", cGo.transform, xp - fThick * 0.5f, xp + fThick * 0.5f, yBot, yCeil, z - fHalf, z + fHalf, mBronzeFrame, 0.001f, false);
                }

                GameObject dGo = new GameObject(doorName);
                dGo.transform.SetParent(cGo.transform, false);

                float leafX0 = dX0 + fThick * 0.5f;
                float leafX1 = dX1 - fThick * 0.5f;
                float hingeX = leafX1; // Bisagra a la derecha (x1), manija a la izquierda junto al montante central (como en la foto real)

                GameObject pivotGo = new GameObject("Pivote_Bisagra");
                pivotGo.transform.SetParent(dGo.transform, false);
                pivotGo.transform.position = Docencia2InteriorBuilder.PlanToUnityPos(hingeX, yBot, z);

                float dFrameW = 0.040f;
                float dHalfZ = 0.022f;
                Docencia2InteriorBuilder.CreatePlanBox("Perfil_Hoja_Izq", pivotGo.transform, leafX0, leafX0 + dFrameW, yBot + 0.005f, yDoorTop - 0.01f, z - dHalfZ, z + dHalfZ, mBronzeFrame, 0.001f, false);
                Docencia2InteriorBuilder.CreatePlanBox("Perfil_Hoja_Der", pivotGo.transform, leafX1 - dFrameW, leafX1, yBot + 0.005f, yDoorTop - 0.01f, z - dHalfZ, z + dHalfZ, mBronzeFrame, 0.001f, false);
                Docencia2InteriorBuilder.CreatePlanBox("Perfil_Hoja_Inf", pivotGo.transform, leafX0 + dFrameW, leafX1 - dFrameW, yBot + 0.005f, yBot + 0.065f, z - dHalfZ, z + dHalfZ, mBronzeFrame, 0.001f, false);
                Docencia2InteriorBuilder.CreatePlanBox("Perfil_Hoja_Sup", pivotGo.transform, leafX0 + dFrameW, leafX1 - dFrameW, yDoorTop - 0.055f, yDoorTop - 0.01f, z - dHalfZ, z + dHalfZ, mBronzeFrame, 0.001f, false);
                Docencia2InteriorBuilder.CreatePlanBox("Cristal_Hoja",    pivotGo.transform, leafX0 + dFrameW, leafX1 - dFrameW, yBot + 0.065f, yDoorTop - 0.055f, z - 0.005f, z + 0.005f, mGlass, 0f, true);

                float handleX = leafX0 + 0.07f;
                Docencia2InteriorBuilder.CreatePlanBox("Manija_Sur",   pivotGo.transform, handleX - 0.015f, handleX + 0.015f, Y_FLOOR + 0.95f, Y_FLOOR + 1.12f, z + dHalfZ, z + dHalfZ + 0.025f, mHandle, 0.001f, false);
                Docencia2InteriorBuilder.CreatePlanBox("Manija_Norte", pivotGo.transform, handleX - 0.015f, handleX + 0.015f, Y_FLOOR + 0.95f, Y_FLOOR + 1.12f, z - dHalfZ - 0.025f, z - dHalfZ, mHandle, 0.001f, false);
            }

            // 1. Frentes de los 12 Cubículos de Sec 07 (A1-A3, B1-B3, C1-C3, D1-D3) con antepecho de madera abajo, ventana de cristal arriba y puerta de cristal a la derecha mirándolos de frente:
            // - Cubículos A1-A3 (x = 3.62, mirándolos desde el pasillo hacia el Oeste -X, la derecha es el Norte = zMin -> doorAtMinZ: true):
            BuildCubicleFrontAlongZ("Cancel_Cubiculo_A1", "P01_Cubiculo_A1_Cristal", 3.62f, 0.36f, 2.57f, doorAtMinZ: true, inwardSign: -1);
            BuildCubicleFrontAlongZ("Cancel_Cubiculo_A2", "P02_Cubiculo_A2_Cristal", 3.62f, 2.57f, 5.14f, doorAtMinZ: true, inwardSign: -1);
            BuildCubicleFrontAlongZ("Cancel_Cubiculo_A3", "P03_Cubiculo_A3_Cristal", 3.62f, 5.14f, 7.71f, doorAtMinZ: true, inwardSign: -1);

            // - Cubículos B1-B3 (x = 5.62, mirándolos desde el pasillo hacia el Este +X, la derecha es el Sur = zMax -> doorAtMinZ: false):
            BuildCubicleFrontAlongZ("Cancel_Cubiculo_B1", "P04_Cubiculo_B1_Cristal", 5.62f, 0.36f, 2.57f, doorAtMinZ: false, inwardSign: +1);
            BuildCubicleFrontAlongZ("Cancel_Cubiculo_B2", "P06_Cubiculo_B2_Cristal", 5.62f, 2.57f, 5.14f, doorAtMinZ: false, inwardSign: +1);
            BuildCubicleFrontAlongZ("Cancel_Cubiculo_B3", "P08_Cubiculo_B3_Cristal", 5.62f, 5.14f, 7.71f, doorAtMinZ: false, inwardSign: +1);

            // - Cubículos C1-C3 (x = 22.86, mirándolos desde el pasillo hacia el Oeste -X, la derecha es el Norte = zMin -> doorAtMinZ: true):
            BuildCubicleFrontAlongZ("Cancel_Cubiculo_C1", "P05_Cubiculo_C1_Cristal", 22.86f, 0.36f, 2.57f, doorAtMinZ: true, inwardSign: -1);
            BuildCubicleFrontAlongZ("Cancel_Cubiculo_C2", "P07_Cubiculo_C2_Cristal", 22.86f, 2.57f, 5.14f, doorAtMinZ: true, inwardSign: -1);
            BuildCubicleFrontAlongZ("Cancel_Cubiculo_C3", "P09_Cubiculo_C3_Cristal", 22.86f, 5.14f, 7.71f, doorAtMinZ: true, inwardSign: -1);

            // - Cubículos D1-D3 (x = 24.86, mirándolos desde el pasillo hacia el Este +X, la derecha es el Sur = zMax -> doorAtMinZ: false):
            BuildCubicleFrontAlongZ("Cancel_Cubiculo_D1", "P10_Cubiculo_D1_Cristal", 24.86f, 0.36f, 2.57f, doorAtMinZ: false, inwardSign: +1);
            BuildCubicleFrontAlongZ("Cancel_Cubiculo_D2", "P11_Cubiculo_D2_Cristal", 24.86f, 2.57f, 5.14f, doorAtMinZ: false, inwardSign: +1);
            BuildCubicleFrontAlongZ("Cancel_Cubiculo_D3", "P12_Cubiculo_D3_Cristal", 24.86f, 5.14f, 7.71f, doorAtMinZ: false, inwardSign: +1);

            // 2. Canceles de entrada a los pasillos de cubículos Oeste (x = 3.62..5.62) y Este (x = 22.86..24.86) en z = 7.71 m (como en la foto real media_1794307179904_77efc63d.jpg):
            // Corrección del usuario: en la entrada de los 2 pasillos de cubículos (z = 7.71) NO va puerta; los pasillos quedan abiertos.
            // BuildCubicleCorridorEntryAlongX("Cancel_Entrada_PasilloCubiculos_Oeste_z7.71", "P_Entrada_PasilloCub_Oeste_Cristal", 7.71f, 3.62f, 5.62f);
            // BuildCubicleCorridorEntryAlongX("Cancel_Entrada_PasilloCubiculos_Este_z7.71",  "P_Entrada_PasilloCub_Este_Cristal",  7.71f, 22.86f, 24.86f);

            // CORRECCIÓN VISUAL DEL USUARIO + FOTO REAL (media_1794305308128_5d386d19.png):
            // Los canceles de vidrio transparente templado de piso a plafón (Y = 0.20..3.20 m) son las paredes PEGADAS AL PASILLO CENTRAL:
            // - x = 11.24 m (Alcoba Oeste | Pasillo Central, entre las columnas z = 7.71 y z = 15.43 -> z = 7.91..15.23 m) con puerta de cristal P15 en z = 8.83..9.79 m (abre hacia Alcoba Oeste, inwardSign = -1).
            // - x = 17.24 m (Local Este | Pasillo Central, entre las columnas z = 7.71 y z = 15.43 -> z = 7.91..15.23 m) con puerta de cristal P16 en z = 8.83..9.79 m (abre hacia Local Este, inwardSign = +1).
            // En la foto real se observa: marco bronce de piso a plafón, franja verde con línea guinda debajo, y travesaño horizontal a media altura.
            void BuildFullGlassCancelSec6(string cancelName, string doorName, float x, float z0, float z1, float dZ0, float dZ1, int inwardSign)
            {
                GameObject cGo = new GameObject(cancelName);
                cGo.transform.SetParent(parent, false);

                float yBot = Y_FLOOR;
                float yDoorTop = Y_FLOOR + DOOR_HEIGHT; // 2.30 m
                float yCeil = Y_CEILING;                // 3.20 m
                float fHalfX = 0.024f;                  // marco bronce de 4.8 cm
                float fThickZ = 0.038f;

                // Riel inferior delgado (zoclo de aluminio bronce de 5 cm en los tramos fijos, interrumpido en la puerta)
                Docencia2InteriorBuilder.CreatePlanBox("Riel_Inf_Norte", cGo.transform, x - fHalfX, x + fHalfX, yBot, yBot + 0.05f, z0, dZ0, mBronzeFrame, 0.001f, false);
                Docencia2InteriorBuilder.CreatePlanBox("Riel_Inf_Sur",   cGo.transform, x - fHalfX, x + fHalfX, yBot, yBot + 0.05f, dZ1, z1, mBronzeFrame, 0.001f, false);

                // Riel superior a ras de plafón (Y = 3.16..3.20 m), travesaño de dintel corrido (Y = 2.27..2.31 m) y travesaño medio en tramos fijos (Y = 1.00..1.03 m)
                Docencia2InteriorBuilder.CreatePlanBox("Riel_Sup_Plafon", cGo.transform, x - fHalfX, x + fHalfX, yCeil - 0.04f, yCeil, z0, z1, mBronzeFrame, 0.001f, false);
                Docencia2InteriorBuilder.CreatePlanBox("Travesano_Dintel", cGo.transform, x - fHalfX, x + fHalfX, yDoorTop - 0.02f, yDoorTop + 0.02f, z0, z1, mBronzeFrame, 0.001f, false);
                Docencia2InteriorBuilder.CreatePlanBox("Travesano_Medio_Norte", cGo.transform, x - fHalfX, x + fHalfX, yBot + 0.98f, yBot + 1.015f, z0, dZ0, mBronzeFrame, 0.001f, false);
                Docencia2InteriorBuilder.CreatePlanBox("Travesano_Medio_Sur",   cGo.transform, x - fHalfX, x + fHalfX, yBot + 0.98f, yBot + 1.015f, dZ1, z1, mBronzeFrame, 0.001f, false);

                // Cristales fijos de piso a plafón (norte de la puerta, sur de la puerta, y montante superior sobre la puerta)
                Docencia2InteriorBuilder.CreatePlanBox("Vidrio_Fijo_Norte", cGo.transform, x - 0.005f, x + 0.005f, yBot + 0.05f, yDoorTop - 0.02f, z0 + fThickZ, dZ0 - fThickZ, mGlass, 0f, true);
                Docencia2InteriorBuilder.CreatePlanBox("Vidrio_Fijo_Sur",   cGo.transform, x - 0.005f, x + 0.005f, yBot + 0.05f, yDoorTop - 0.02f, dZ1 + fThickZ, z1 - fThickZ, mGlass, 0f, true);
                Docencia2InteriorBuilder.CreatePlanBox("Vidrio_Fijo_Montante_Sup", cGo.transform, x - 0.005f, x + 0.005f, yDoorTop + 0.02f, yCeil - 0.04f, z0 + fThickZ, z1 - fThickZ, mGlass, 0f, false);

                // Franja verde institucional + línea guinda delgada debajo (idéntica a la foto real media_1794305308128_5d386d19.png)
                Docencia2InteriorBuilder.CreatePlanBox("Franja_Verde_Norte",  cGo.transform, x - 0.008f, x + 0.008f, Y_FLOOR + 0.72f, Y_FLOOR + 0.81f, z0, dZ0, mGreen, 0f, false);
                Docencia2InteriorBuilder.CreatePlanBox("Linea_Guinda_Norte",  cGo.transform, x - 0.008f, x + 0.008f, Y_FLOOR + 0.66f, Y_FLOOR + 0.69f, z0, dZ0, mGuinda, 0f, false);
                Docencia2InteriorBuilder.CreatePlanBox("Franja_Verde_Sur",    cGo.transform, x - 0.008f, x + 0.008f, Y_FLOOR + 0.72f, Y_FLOOR + 0.81f, dZ1, z1, mGreen, 0f, false);
                Docencia2InteriorBuilder.CreatePlanBox("Linea_Guinda_Sur",    cGo.transform, x - 0.008f, x + 0.008f, Y_FLOOR + 0.66f, Y_FLOOR + 0.69f, dZ1, z1, mGuinda, 0f, false);

                // Montantes verticales en bronce: extremo norte junto a columna (z0), jambas de la puerta (dZ0 y dZ1), y modulación en el tramo sur (dZ1..z1)
                float[] postZs = new float[]
                {
                    z0 + fThickZ * 0.5f,
                    dZ0,
                    dZ1,
                    dZ1 + 1.08f,
                    dZ1 + 2.16f,
                    dZ1 + 3.24f,
                    dZ1 + 4.32f,
                    z1 - fThickZ * 0.5f
                };
                for (int i = 0; i < postZs.Length; i++)
                {
                    float zp = postZs[i];
                    Docencia2InteriorBuilder.CreatePlanBox($"Montante_Delgado_{i:02d}", cGo.transform, x - fHalfX, x + fHalfX, yBot, yCeil, zp - fThickZ * 0.5f, zp + fThickZ * 0.5f, mBronzeFrame, 0.001f, false);
                }

                // Puerta de cristal templado transparente (P15 o P16) con marco perimetral bronce delgado, travesaño medio, franja verde + guinda y Pivote_Bisagra que abre 90° hacia dentro
                GameObject dGo = new GameObject(doorName);
                dGo.transform.SetParent(cGo.transform, false);

                float leafZ0 = dZ0 + fThickZ * 0.5f;
                float leafZ1 = dZ1 - fThickZ * 0.5f;
                float hingeZ = leafZ0; // bisagra en z = 8.83 m para que al abrir 90° hacia dentro quede paralela al muro norte z = 7.71 m

                GameObject pivotGo = new GameObject("Pivote_Bisagra");
                pivotGo.transform.SetParent(dGo.transform, false);
                pivotGo.transform.position = Docencia2InteriorBuilder.PlanToUnityPos(x, yBot, hingeZ);

                float dFrameW = 0.036f;
                float dHalfX = 0.020f;
                // Perfiles bronce de la hoja de cristal + travesaño a la altura de la manija (visible en la derecha de la foto real)
                Docencia2InteriorBuilder.CreatePlanBox("Perfil_Hoja_Norte", pivotGo.transform, x - dHalfX, x + dHalfX, yBot + 0.005f, yDoorTop - 0.01f, leafZ0, leafZ0 + dFrameW, mBronzeFrame, 0.001f, false);
                Docencia2InteriorBuilder.CreatePlanBox("Perfil_Hoja_Sur",   pivotGo.transform, x - dHalfX, x + dHalfX, yBot + 0.005f, yDoorTop - 0.01f, leafZ1 - dFrameW, leafZ1, mBronzeFrame, 0.001f, false);
                Docencia2InteriorBuilder.CreatePlanBox("Perfil_Hoja_Inf",   pivotGo.transform, x - dHalfX, x + dHalfX, yBot + 0.005f, yBot + 0.055f, leafZ0 + dFrameW, leafZ1 - dFrameW, mBronzeFrame, 0.001f, false);
                Docencia2InteriorBuilder.CreatePlanBox("Perfil_Hoja_Sup",   pivotGo.transform, x - dHalfX, x + dHalfX, yDoorTop - 0.05f, yDoorTop - 0.01f, leafZ0 + dFrameW, leafZ1 - dFrameW, mBronzeFrame, 0.001f, false);
                Docencia2InteriorBuilder.CreatePlanBox("Travesano_Hoja_Medio", pivotGo.transform, x - dHalfX, x + dHalfX, yBot + 0.98f, yBot + 1.015f, leafZ0 + dFrameW, leafZ1 - dFrameW, mBronzeFrame, 0.001f, false);

                // Cristal templado transparente completo de la hoja + franja verde + línea guinda debajo (como en la foto real)
                Docencia2InteriorBuilder.CreatePlanBox("Cristal_Templado_Hoja", pivotGo.transform, x - 0.005f, x + 0.005f, yBot + 0.055f, yDoorTop - 0.05f, leafZ0 + dFrameW, leafZ1 - dFrameW, mGlass, 0f, true);
                Docencia2InteriorBuilder.CreatePlanBox("Franja_Verde_Hoja",     pivotGo.transform, x - 0.008f, x + 0.008f, Y_FLOOR + 0.72f, Y_FLOOR + 0.81f, leafZ0 + dFrameW, leafZ1 - dFrameW, mGreen, 0f, false);
                Docencia2InteriorBuilder.CreatePlanBox("Linea_Guinda_Hoja",     pivotGo.transform, x - 0.008f, x + 0.008f, Y_FLOOR + 0.66f, Y_FLOOR + 0.69f, leafZ0 + dFrameW, leafZ1 - dFrameW, mGuinda, 0f, false);

                // Herraje delgado en ambos lados
                float handleZ = leafZ1 - 0.09f;
                Docencia2InteriorBuilder.CreatePlanBox("Herraje_Jaladera_Ext", pivotGo.transform, x + dHalfX, x + dHalfX + 0.025f, Y_FLOOR + 0.90f, Y_FLOOR + 1.20f, handleZ - 0.012f, handleZ + 0.012f, mHandle, 0.001f, false);
                Docencia2InteriorBuilder.CreatePlanBox("Herraje_Jaladera_Int", pivotGo.transform, x - dHalfX - 0.025f, x - dHalfX, Y_FLOOR + 0.90f, Y_FLOOR + 1.20f, handleZ - 0.012f, handleZ + 0.012f, mHandle, 0.001f, false);
            }

            // Entre las columnas de 0.40x0.40 m en z = 7.71 (cara sur = 7.91) y z = 15.43 (cara norte = 15.23):
            BuildFullGlassCancelSec6("Cancel_Vidrio_Sec06_PasilloOeste_x11.24", "P15_Alcoba_Oeste_CancelVidrio", 11.24f, 7.91f, 15.23f, 8.83f, 9.79f, inwardSign: -1);
            BuildFullGlassCancelSec6("Cancel_Vidrio_Sec06_PasilloEste_x17.24",  "P16_Local_Este_CancelVidrio",  17.24f, 7.91f, 15.23f, 8.83f, 9.79f, inwardSign: +1);
        }

        // -----------------------------------------------------------------------------------------
        // C3. VENTANAS EXTERIORES VISTAS DESDE DENTRO Y PORTAL DE ENTRADA PRINCIPAL
        // -----------------------------------------------------------------------------------------
        private static void BuildPBWindowsAndGrilles(Transform parent, Material mWinFrame, Material mBronzeFrame, Material mGlass, Material mGreen, Material mGuinda, Material mReja)
        {
            float stepZ = 54.0f / 7.0f;
            float halfClearW = (stepZ - 0.92f) * 0.5f;

            float ySill = Docencia2InteriorBuilder.Y_WIN_PB_SILL;
            float yTop = Docencia2InteriorBuilder.Y_WIN_PB_TOP;

            for (int s = 0; s < 7; s++)
            {
                float zCenter = (s + 0.5f) * stepZ;
                float z0 = zCenter - halfClearW;
                float z1 = zCenter + halfClearW;

                if (s != 2)
                {
                    BuildSectionWindowAndGrille(parent, $"Ventana_Este_Sec{s + 1:02d}", 28.06f, z0, z1, ySill, yTop, mWinFrame, mGlass, mReja, isEast: true);
                }
                else
                {
                    // Punto 4 (v4): Entrada principal aprobada con 2 puertas dobles lado a lado (4 hojas de 0.80 m = 3.20 m en total)
                    // en z = 17.69..20.89 m, centradas en z = 19.29 m dentro del vano de vidrio de 6.44 m (z = 16.07..22.51 m, 1.62 m de panel fijo a cada lado),
                    // con pivotes verticales de vaivén (±90°) y reemplazando la puerta doble anterior de 1.60 m del exterior.
                    BuildEntranceGlazedPortal(parent, "Portal_Entrada_Principal_Sec05", 28.18f, 16.07f, 22.51f, Y_FLOOR, yTop, mBronzeFrame, mGlass, mGreen, mGuinda, useTwoDoubleDoors320m: true);
                    ReplaceExteriorSingleDoubleDoorWithTwoDoubleDoors();
                }

                BuildSectionWindowAndGrille(parent, $"Ventana_Oeste_Sec{s + 1:02d}", 0.42f, z0, z1, ySill, yTop, mWinFrame, mGlass, mReja, isEast: false);
            }
        }

        private static void BuildSectionWindowAndGrille(Transform parent, string name, float x, float z0, float z1, float y0, float y1, Material mFrame, Material mGlass, Material mReja, bool isEast)
        {
            GameObject wGo = new GameObject(name);
            wGo.transform.SetParent(parent, false);

            float totalWidth = z1 - z0;
            int panels = 6;
            float panelW = totalWidth / panels;

            float fThick = 0.04f;
            Docencia2InteriorBuilder.CreatePlanBox("Marco_Inf", wGo.transform, x - 0.042f, x + 0.042f, y0, y0 + fThick, z0, z1, mFrame, 0.002f, false);
            Docencia2InteriorBuilder.CreatePlanBox("Marco_Sup", wGo.transform, x - 0.042f, x + 0.042f, y1 - fThick, y1, z0, z1, mFrame, 0.002f, false);
            Docencia2InteriorBuilder.CreatePlanBox("Marco_Izq", wGo.transform, x - 0.042f, x + 0.042f, y0, y1, z0, z0 + fThick, mFrame, 0.002f, false);
            Docencia2InteriorBuilder.CreatePlanBox("Marco_Der", wGo.transform, x - 0.042f, x + 0.042f, y0, y1, z1 - fThick, z1, mFrame, 0.002f, false);

            float yMid = y0 + (y1 - y0) * 0.65f;
            Docencia2InteriorBuilder.CreatePlanBox("Travesano_Medio", wGo.transform, x - 0.042f, x + 0.042f, yMid - 0.02f, yMid + 0.02f, z0, z1, mFrame, 0.002f, false);

            for (int i = 0; i < panels; i++)
            {
                float pzA = z0 + i * panelW;
                float pzB = pzA + panelW;

                if (i > 0)
                {
                    Docencia2InteriorBuilder.CreatePlanBox($"Montante_{i}", wGo.transform, x - 0.042f, x + 0.042f, y0, y1, pzA - 0.02f, pzA + 0.02f, mFrame, 0.002f, false);
                }

                Docencia2InteriorBuilder.CreatePlanBox($"Vidrio_Inf_{i}", wGo.transform, x - 0.004f, x + 0.004f, y0 + fThick, yMid - 0.02f, pzA + 0.02f, pzB - 0.02f, mGlass, 0f, false);
                Docencia2InteriorBuilder.CreatePlanBox($"Vidrio_Sup_{i}", wGo.transform, x - 0.004f, x + 0.004f, yMid + 0.02f, y1 - fThick, pzA + 0.02f, pzB - 0.02f, mGlass, 0f, false);
            }

            // Rejillas de herrería (Reja_Varilla_* y Reja_Solera_*) retiradas por solicitud del usuario
            // para dejar las ventanas limpias sin barrotes interiores.
        }

        // Construye el portal de entrada en bronce, con doble franja verde + línea guinda debajo y hojas de vaivén (±90°) con pivote vertical
        public static GameObject BuildEntranceGlazedPortal(
            Transform parent, string name, float x, float z0, float z1, float y0, float y1,
            Material mBronzeFrame, Material mGlass, Material mGreen, Material mGuinda,
            bool useTwoDoubleDoors320m, float leaf3OpenAngleDeg = 0f, bool sixEqualModules = true)
        {
            GameObject eGo = new GameObject(name);
            eGo.transform.SetParent(parent, false);

            // Marco exterior del portal (tono bronce)
            Docencia2InteriorBuilder.CreatePlanBox("Marco_Portal_Sup", eGo.transform, x - 0.045f, x + 0.045f, y1 - 0.06f, y1, z0, z1, mBronzeFrame, 0.002f, false);
            Docencia2InteriorBuilder.CreatePlanBox("Marco_Portal_Izq", eGo.transform, x - 0.045f, x + 0.045f, y0, y1, z0, z0 + 0.06f, mBronzeFrame, 0.002f, false);
            Docencia2InteriorBuilder.CreatePlanBox("Marco_Portal_Der", eGo.transform, x - 0.045f, x + 0.045f, y0, y1, z1 - 0.06f, z1, mBronzeFrame, 0.002f, false);

            // Dintel corrido sobre las puertas a Y = 2.45 m (y0 + 2.25 m)
            float yDoor = y0 + 2.25f;
            Docencia2InteriorBuilder.CreatePlanBox("Dintel_Portal", eGo.transform, x - 0.04f, x + 0.04f, yDoor - 0.045f, yDoor, z0, z1, mBronzeFrame, 0.002f, false);

            // Alturas de la doble franja verde corrida + línea guinda delgada debajo (según fotos 20261009_185344..185352)
            float yStripeGreenSup0 = y0 + 0.68f, yStripeGreenSup1 = y0 + 0.75f;
            float yStripeGreenInf0 = y0 + 0.56f, yStripeGreenInf1 = y0 + 0.63f;
            float yStripeGuinda0   = y0 + 0.50f, yStripeGuinda1   = y0 + 0.53f;

            // Helper para crear una hoja de vaivén de 0.80 m con pivote vertical (gira ±90° hacia adentro y hacia afuera)
            void BuildVaivenLeaf(string leafName, float zLeafMin, float zLeafMax, bool pivotAtMinZ, float openDeg)
            {
                float pivotZ = pivotAtMinZ ? (zLeafMin + 0.025f) : (zLeafMax - 0.025f);
                GameObject piv = new GameObject("Pivote_Vaiven_" + leafName);
                piv.transform.SetParent(eGo.transform, false);
                piv.transform.position = Docencia2InteriorBuilder.PlanToUnityPos(x, y0, pivotZ);

                float fW = 0.045f;
                // Rieles verticales y zócalo/cabezal en bronce
                Docencia2InteriorBuilder.CreatePlanBox("Perfil_Izq", piv.transform, x - 0.042f, x + 0.042f, y0, yDoor - 0.045f, zLeafMin, zLeafMin + fW, mBronzeFrame, 0.001f, false);
                Docencia2InteriorBuilder.CreatePlanBox("Perfil_Der", piv.transform, x - 0.042f, x + 0.042f, y0, yDoor - 0.045f, zLeafMax - fW, zLeafMax, mBronzeFrame, 0.001f, false);
                Docencia2InteriorBuilder.CreatePlanBox("Zocalo_Inf", piv.transform, x - 0.042f, x + 0.042f, y0, y0 + 0.10f, zLeafMin, zLeafMax, mBronzeFrame, 0.001f, false);
                Docencia2InteriorBuilder.CreatePlanBox("Cabezal_Sup", piv.transform, x - 0.042f, x + 0.042f, yDoor - 0.11f, yDoor - 0.045f, zLeafMin, zLeafMax, mBronzeFrame, 0.001f, false);

                // Travesaño horizontal a la altura de la manija y barra de empuje (ambos lados)
                Docencia2InteriorBuilder.CreatePlanBox("Travesano_Manija", piv.transform, x - 0.042f, x + 0.042f, y0 + 0.96f, y0 + 1.01f, zLeafMin, zLeafMax, mBronzeFrame, 0.001f, false);
                Docencia2InteriorBuilder.CreatePlanBox("Barra_Empuje_Int", piv.transform, x - 0.045f, x - 0.025f, y0 + 0.97f, y0 + 1.00f, zLeafMin + 0.04f, zLeafMax - 0.04f, mBronzeFrame, 0.001f, false);
                Docencia2InteriorBuilder.CreatePlanBox("Barra_Empuje_Ext", piv.transform, x + 0.025f, x + 0.045f, y0 + 0.97f, y0 + 1.00f, zLeafMin + 0.04f, zLeafMax - 0.04f, mBronzeFrame, 0.001f, false);

                // Cristal claro completo
                Docencia2InteriorBuilder.CreatePlanBox("Vidrio_Claro_Hoja", piv.transform, x - 0.005f, x + 0.005f, y0 + 0.10f, yDoor - 0.11f, zLeafMin + fW, zLeafMax - fW, mGlass, 0f, true);

                // Doble franja verde corrida + línea guinda delgada debajo en la hoja
                Docencia2InteriorBuilder.CreatePlanBox("Franja_Verde_Sup", piv.transform, x - 0.036f, x + 0.036f, yStripeGreenSup0, yStripeGreenSup1, zLeafMin + fW, zLeafMax - fW, mGreen, 0f, false);
                Docencia2InteriorBuilder.CreatePlanBox("Franja_Verde_Inf", piv.transform, x - 0.036f, x + 0.036f, yStripeGreenInf0, yStripeGreenInf1, zLeafMin + fW, zLeafMax - fW, mGreen, 0f, false);
                Docencia2InteriorBuilder.CreatePlanBox("Linea_Guinda_Inf", piv.transform, x - 0.036f, x + 0.036f, yStripeGuinda0, yStripeGuinda1, zLeafMin + fW, zLeafMax - fW, mGuinda, 0f, false);

                if (Mathf.Abs(openDeg) > 0.01f)
                {
                    piv.transform.localRotation = Quaternion.Euler(0f, openDeg, 0f);
                }
            }

            // Helper para panel fijo lateral con travesaño, vidrio, doble franja verde + línea guinda e imposta superior
            void BuildFixedSidePanel(string sideName, float zSideMin, float zSideMax)
            {
                if (zSideMax <= zSideMin + 0.08f) return;
                Docencia2InteriorBuilder.CreatePlanBox($"Zocalo_Fijo_{sideName}", eGo.transform, x - 0.042f, x + 0.042f, y0, y0 + 0.10f, zSideMin, zSideMax, mBronzeFrame, 0.001f, false);
                Docencia2InteriorBuilder.CreatePlanBox($"Travesano_Fijo_{sideName}", eGo.transform, x - 0.042f, x + 0.042f, y0 + 0.96f, y0 + 1.01f, zSideMin, zSideMax, mBronzeFrame, 0.001f, false);
                Docencia2InteriorBuilder.CreatePlanBox($"Vidrio_Fijo_{sideName}", eGo.transform, x - 0.005f, x + 0.005f, y0 + 0.10f, yDoor - 0.045f, zSideMin, zSideMax, mGlass, 0f, true);
                Docencia2InteriorBuilder.CreatePlanBox($"Franja_Verde_Sup_Fijo_{sideName}", eGo.transform, x - 0.036f, x + 0.036f, yStripeGreenSup0, yStripeGreenSup1, zSideMin, zSideMax, mGreen, 0f, false);
                Docencia2InteriorBuilder.CreatePlanBox($"Franja_Verde_Inf_Fijo_{sideName}", eGo.transform, x - 0.036f, x + 0.036f, yStripeGreenInf0, yStripeGreenInf1, zSideMin, zSideMax, mGreen, 0f, false);
                Docencia2InteriorBuilder.CreatePlanBox($"Linea_Guinda_Fijo_{sideName}", eGo.transform, x - 0.036f, x + 0.036f, yStripeGuinda0, yStripeGuinda1, zSideMin, zSideMax, mGuinda, 0f, false);
            }

            if (!useTwoDoubleDoors320m)
            {
                // MODO ACTUAL (Paso 1d): 1 puerta doble de 1.60 m centrada en z = 19.29 (z = 18.49..20.09)
                float zDoorNorth = 18.49f;
                float zDoorMid   = 19.29f;
                float zDoorSouth = 20.09f;

                Docencia2InteriorBuilder.CreatePlanBox("Jamba_PuertaDoble_Norte_z18.49", eGo.transform, x - 0.045f, x + 0.045f, y0, y1, zDoorNorth - 0.03f, zDoorNorth + 0.03f, mBronzeFrame, 0.002f, true);
                Docencia2InteriorBuilder.CreatePlanBox("Jamba_PuertaDoble_Sur_z20.09", eGo.transform, x - 0.045f, x + 0.045f, y0, y1, zDoorSouth - 0.03f, zDoorSouth + 0.03f, mBronzeFrame, 0.002f, true);

                BuildFixedSidePanel("Norte", z0 + 0.06f, zDoorNorth - 0.03f);
                BuildFixedSidePanel("Sur",   zDoorSouth + 0.03f, z1 - 0.06f);

                BuildVaivenLeaf("HojaNorte", zDoorNorth + 0.03f, zDoorMid - 0.005f, pivotAtMinZ: true,  openDeg: 0f);
                BuildVaivenLeaf("HojaSur",   zDoorMid + 0.005f,  zDoorSouth - 0.03f, pivotAtMinZ: false, openDeg: 0f);
            }
            else
            {
                // MODO PROPUESTO SEGÚN FOTO REAL: 2 puertas dobles una al lado de la otra (4 hojas de 0.80 m = 3.20 m en total, z = 17.69..20.89)
                float module = (z1-z0)/6f;
                float zD1North = sixEqualModules ? z0+module : 17.69f;
                float zD1Mid   = sixEqualModules ? z0+module*2 : 18.49f;
                float zCenter  = sixEqualModules ? z0+module*3 : 19.29f; // Poste central entre las 2 puertas dobles
                float zD2Mid   = sixEqualModules ? z0+module*4 : 20.09f;
                float zD2South = sixEqualModules ? z0+module*5 : 20.89f;

                // 3 montantes verticales principales de piso a techo: z = 17.69 (jamba norte), z = 19.29 (poste central entre las 2 puertas dobles) y z = 20.89 (jamba sur)
                Docencia2InteriorBuilder.CreatePlanBox("Jamba_2PuertasDobles_Norte_z17.69", eGo.transform, x - 0.045f, x + 0.045f, y0, y1, zD1North - 0.03f, zD1North + 0.03f, mBronzeFrame, 0.002f, true);
                Docencia2InteriorBuilder.CreatePlanBox("Poste_Central_EntrePuertasDobles_z19.29", eGo.transform, x - 0.045f, x + 0.045f, y0, y1, zCenter - 0.03f, zCenter + 0.03f, mBronzeFrame, 0.002f, true);
                Docencia2InteriorBuilder.CreatePlanBox("Jamba_2PuertasDobles_Sur_z20.89", eGo.transform, x - 0.045f, x + 0.045f, y0, y1, zD2South - 0.03f, zD2South + 0.03f, mBronzeFrame, 0.002f, true);

                // Montantes en la imposta superior sobre los encuentros de hojas (z = 18.49 y z = 20.09)
                Docencia2InteriorBuilder.CreatePlanBox("Montante_Imposta_z18.49", eGo.transform, x - 0.035f, x + 0.035f, yDoor, y1 - 0.06f, zD1Mid - 0.02f, zD1Mid + 0.02f, mBronzeFrame, 0.001f, false);
                Docencia2InteriorBuilder.CreatePlanBox("Montante_Imposta_z20.09", eGo.transform, x - 0.035f, x + 0.035f, yDoor, y1 - 0.06f, zD2Mid - 0.02f, zD2Mid + 0.02f, mBronzeFrame, 0.001f, false);

                // Paneles fijos a los lados (z0..17.69 al norte y 20.89..z1 al sur)
                BuildFixedSidePanel("Norte", z0 + 0.06f, zD1North - 0.03f);
                BuildFixedSidePanel("Sur",   zD2South + 0.03f, z1 - 0.06f);

                // Puerta Doble 1 (Sur/Izquierda mirando desde afuera: z = 19.29..20.89, hojas 1 y 2)
                // Puerta Doble 2 (Norte/Derecha mirando desde afuera: z = 17.69..19.29, hojas 3 y 4)
                BuildVaivenLeaf("PuertaDobleNorte_Hoja1_z17.69_18.49", zD1North + 0.03f, zD1Mid - 0.005f, pivotAtMinZ: true,  openDeg: 0f);
                BuildVaivenLeaf("PuertaDobleNorte_Hoja2_z18.49_19.29", zD1Mid + 0.005f,  zCenter - 0.03f, pivotAtMinZ: false, openDeg: leaf3OpenAngleDeg);
                BuildVaivenLeaf("PuertaDobleSur_Hoja3_z19.29_20.09",   zCenter + 0.03f,  zD2Mid - 0.005f, pivotAtMinZ: true,  openDeg: 0f);
                BuildVaivenLeaf("PuertaDobleSur_Hoja4_z20.09_20.89",   zD2Mid + 0.005f,  zD2South - 0.03f, pivotAtMinZ: false, openDeg: 0f);
            }

            // Cristal fijo superior (imposta acristalada a todo lo ancho del vano)
            Docencia2InteriorBuilder.CreatePlanBox("Vidrio_Imposta_Corrida", eGo.transform, x - 0.005f, x + 0.005f, yDoor, y1 - 0.06f, z0 + 0.06f, z1 - 0.06f, mGlass, 0f, false);

            return eGo;
        }

        // Oculta únicamente las mallas de la antigua puerta doble de 1.60 m en Docencia2_Exterior (Sec 05),
        // dejando 100% intactos el muro/panel verde superior de Planta Alta (PA), la ventana P2 superior, la faja de entrepiso, plataforma, escalones, pilares y torres.
        public static void ReplaceExteriorSingleDoubleDoorWithTwoDoubleDoors()
        {
            GameObject extRoot = GameObject.Find("Docencia2_Exterior");
            if (extRoot == null) return;
            foreach (Renderer r in extRoot.GetComponentsInChildren<Renderer>(true))
            {
                // Primero restaurar cualquier renderer que haya sido ocultado por error antes
                r.enabled = true;

                string n = r.gameObject.name;
                // NUNCA ocultar elementos de Planta Alta (P2, PA, PanelVerde, Muro, Columna, Pilar, Losa, Viga, Faja, Pretil)
                // ni elementos cuyo borde superior supere Y = 3.55 m (techo de PB)
                if (r.bounds.max.y > 3.55f) continue;
                if (n.Contains("P2") || n.Contains("PA") || n.Contains("PanelVerde") || n.Contains("Muro") ||
                    n.Contains("Columna") || n.Contains("Pilar") || n.Contains("Zoclo") || n.Contains("Losa") ||
                    n.Contains("Viga") || n.Contains("Faja") || n.Contains("Pretil") || n.Contains("Escal") ||
                    n.Contains("Plataforma") || n.Contains("Lampara") || n.Contains("Luminaria"))
                {
                    continue;
                }

                // Ocultar únicamente los elementos de la antigua puerta doble de 1.60 m y su cancel de PB en Sec 05 (x_unity entre 4.0 y 11.4, z_unity < -13.5, max.y <= 3.55 m)
                bool isInEntranceBayPB = (r.bounds.center.z < -13.5f && r.bounds.center.x > 4.0f && r.bounds.center.x < 11.4f && r.bounds.max.y <= 3.55f && r.bounds.min.y >= 0.05f);
                if (n.Contains("Entrada") || isInEntranceBayPB)
                {
                    r.enabled = false;
                }
            }
        }
    }
}
