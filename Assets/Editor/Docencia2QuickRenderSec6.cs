using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Vaelith.Docencia2
{
    public static class Docencia2QuickRenderSec6
    {
        public static void Run()
        {
            try
            {
                string outDir = "Renders_Verificacion_Part2";
                if (!Directory.Exists(outDir)) Directory.CreateDirectory(outDir);

                Docencia2InteriorBuilder.BuildPhase2PlantaBaja(false);

                var scene = EditorSceneManager.GetActiveScene();
                var switcher = UnityEngine.Object.FindAnyObjectByType<Docencia2ModeSwitcher>();
                if (switcher != null) switcher.ApplyMode(Docencia2VisualMode.Noche, true);

                Camera[] allSceneCams = UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsInactive.Include);
                foreach (Camera c in allSceneCams)
                {
                    if (c != null) c.cullingMask |= (1 << 0) | (1 << Docencia2Phase4Builder.LAYER_PB_INTERIOR);
                }

                GameObject tempRig = new GameObject("Temp_QuickRender_Rig");

                // 1. Vista del Local Este mirando hacia el Norte (z = 7.71):
                //    Muestra la pared del baño intacta (crema sin columna en x = 19.24), el muro sur de C3 en madera (x = 19.24..22.86),
                //    y el cancel de entrada al pasillo de cubículos este (x = 22.86..24.86) con antepecho de madera + ventana a la izquierda y puerta de cristal a la derecha (como en media_1794307179904_77efc63d.jpg)
                Camera camSec6Este = MakeCam("Cam_Sec6_Este", tempRig.transform,
                    Docencia2InteriorBuilder.PlanToUnityPos(18.60f, 1.75f, 12.80f),
                    Docencia2InteriorBuilder.PlanToUnityPos(22.20f, 1.50f, 7.71f), 74f);
                RenderCam(camSec6Este, allSceneCams, Path.Combine(outDir, "Part2_06b_Sec6_Cancel_Vidrio_Este_P27.png"));

                // 2. Vista de la Alcoba Oeste mirando hacia el Norte (z = 7.71):
                //    Muestra la pared del baño intacta (crema sin columna en x = 9.24), el muro sur de B3 en madera (x = 5.62..9.24),
                //    y el cancel de entrada al pasillo de cubículos oeste (x = 3.62..5.62) con antepecho de madera + ventana a la izquierda y puerta de cristal a la derecha
                Camera camSec6Oeste = MakeCam("Cam_Sec6_Oeste", tempRig.transform,
                    Docencia2InteriorBuilder.PlanToUnityPos(9.80f, 1.75f, 12.80f),
                    Docencia2InteriorBuilder.PlanToUnityPos(6.20f, 1.50f, 7.71f), 74f);
                RenderCam(camSec6Oeste, allSceneCams, Path.Combine(outDir, "Part2_06_Sec6_Cancel_Vidrio_Oeste_P26.png"));

                // 3. Vista dentro del Pasillo de Cubículos Este (x = 22.86..24.86):
                //    Muestra los cubículos C1-C3 y D1-D3 con su antepecho de madera abajo, ventana de cristal arriba y puerta de cristal en el extremo derecho mirando de frente
                Camera camPasilloCubEste = MakeCam("Cam_Pasillo_Cub_Este", tempRig.transform,
                    Docencia2InteriorBuilder.PlanToUnityPos(23.86f, 1.72f, 6.80f),
                    Docencia2InteriorBuilder.PlanToUnityPos(23.86f, 1.45f, 2.20f), 76f);
                RenderCam(camPasilloCubEste, allSceneCams, Path.Combine(outDir, "Part2_05_Bloque_Cubiculos_Pasillo_Este_Abierto.png"));

                // 4. Vista dentro del Pasillo de Cubículos Oeste (x = 3.62..5.62):
                Camera camPasilloCubOeste = MakeCam("Cam_Pasillo_Cub_Oeste", tempRig.transform,
                    Docencia2InteriorBuilder.PlanToUnityPos(4.62f, 1.72f, 6.80f),
                    Docencia2InteriorBuilder.PlanToUnityPos(4.62f, 1.45f, 2.20f), 76f);
                RenderCam(camPasilloCubOeste, allSceneCams, Path.Combine(outDir, "Fix5_Pasillo_Cubiculos_Oeste_Madera_y_Puertas_Cristal.png"));

                // 5. Vista aérea / perspectiva de la escalera desde Planta Alta hacia abajo (encuadre idéntico a media_1794308691791_04de728b.jpg):
                Camera camEscaleraHueco = MakeCam("Cam_Escalera_Hueco_Libre", tempRig.transform,
                    Docencia2InteriorBuilder.PlanToUnityPos(8.60f, 5.15f, 19.29f),
                    Docencia2InteriorBuilder.PlanToUnityPos(3.20f, 2.10f, 19.29f), 65f);
                RenderCam(camEscaleraHueco, allSceneCams, Path.Combine(outDir, "Fix2_Escalera_Menos_Ancha_2.20m_Vista_Superior.png"));

                // 5b. Vista oblicua desde el descanso hacia los muretes laterales y frontal (encuadre idéntico a media_1794356811548_15d3b2f0.jpg):
                Camera camDescansoErrorCheck = MakeCam("Cam_Descanso_Error_Check", tempRig.transform,
                    Docencia2InteriorBuilder.PlanToUnityPos(2.20f, 3.80f, 16.20f),
                    Docencia2InteriorBuilder.PlanToUnityPos(2.80f, 1.80f, 20.50f), 70f);
                RenderCam(camDescansoErrorCheck, allSceneCams, Path.Combine(outDir, "Fix2_Escalera_Descanso_Muretes_Laterales.png"));

                // 6. Vista frontal de la escalera desde el vestíbulo mirando al oeste (encuadre idéntico a WhatsApp Image 2026-10-08 at 11.01.26 PM.jpeg):
                Camera camEscFrontal = MakeCam("Cam_Escalera_Frontal_PB", tempRig.transform,
                    Docencia2InteriorBuilder.PlanToUnityPos(12.50f, 1.65f, 19.29f),
                    Docencia2InteriorBuilder.PlanToUnityPos(4.40f, 1.80f, 19.29f), 64f);
                RenderCam(camEscFrontal, allSceneCams, Path.Combine(outDir, "Fase2_02_Escalera_En_U_Vista_Frontal.png"));

                UnityEngine.Object.DestroyImmediate(tempRig);

                foreach (Camera c in allSceneCams)
                {
                    if (c != null) c.enabled = (c.gameObject.name == "Camara_01_Frontal_Baja");
                }

                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                AssetDatabase.SaveAssets();
                EditorApplication.Exit(0);
            }
            catch (Exception ex)
            {
                Debug.LogError("[QuickRenderSec6] Error: " + ex);
                EditorApplication.Exit(1);
            }
        }

        private static Camera MakeCam(string name, Transform parent, Vector3 pos, Vector3 lookAt, float fov)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            go.transform.LookAt(lookAt, Vector3.up);
            Camera cam = go.AddComponent<Camera>();
            cam.fieldOfView = fov;
            cam.nearClipPlane = 0.15f;
            cam.farClipPlane = 150.0f;
            cam.clearFlags = CameraClearFlags.Skybox;
            cam.cullingMask = (1 << 0) | (1 << Docencia2Phase4Builder.LAYER_PB_INTERIOR);
            var camData = go.AddComponent<UniversalAdditionalCameraData>();
            camData.renderPostProcessing = true;
            camData.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            camData.antialiasingQuality = AntialiasingQuality.High;
            return cam;
        }

        private static void RenderCam(Camera targetCam, Camera[] allCams, string outPath)
        {
            foreach (Camera c in allCams)
            {
                if (c != null) c.enabled = false;
            }
            targetCam.enabled = true;

            RenderTexture rt = new RenderTexture(1920, 1080, 24, RenderTextureFormat.ARGB32);
            rt.antiAliasing = 4;
            targetCam.targetTexture = rt;
            targetCam.Render();

            RenderTexture.active = rt;
            Texture2D tex = new Texture2D(1920, 1080, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, 1920, 1080), 0, 0);
            tex.Apply();

            targetCam.targetTexture = null;
            RenderTexture.active = null;
            UnityEngine.Object.DestroyImmediate(rt);

            File.WriteAllBytes(outPath, tex.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(tex);
            targetCam.enabled = false;
        }
    }
}
