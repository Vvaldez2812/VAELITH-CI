using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Vaelith.Docencia2;

namespace Vaelith.Trailer.Editor
{
    /// <summary>
    /// Utilidad temporal de solo lectura para auditar coordenadas exactas en Trailer_Docencia2.unity
    /// (sin [MenuItem], ejecutada únicamente por -executeMethod).
    /// </summary>
    public static class TrailerSceneInspector
    {
        public static void InspectPBAndDoors()
        {
            var scene = EditorSceneManager.OpenScene("Assets/Scenes/Trailer_Docencia2.unity", OpenSceneMode.Single);
            var sb = new StringBuilder();
            sb.AppendLine("=== TRAILER SCENE AUDIT (Trailer_Docencia2.unity) ===");

            var switcher = UnityEngine.Object.FindAnyObjectByType<Docencia2ModeSwitcher>();
            sb.AppendLine($"ModeSwitcher: {(switcher != null ? switcher.modoActual.ToString() : "NULL")}");

            var darkness = UnityEngine.Object.FindAnyObjectByType<Docencia2TerrorDarknessController>();
            if (darkness != null)
            {
                sb.AppendLine($"TerrorDarkness: brightness={darkness.brightnessLevel}, emergencyLights={darkness.emergencyAmberLights.Count}, dyingFluorescent={(darkness.dyingFluorescentLight != null ? darkness.dyingFluorescentLight.transform.position.ToString("F2") : "NULL")}");
                for (int i = 0; i < darkness.emergencyAmberLights.Count; i++)
                {
                    var l = darkness.emergencyAmberLights[i];
                    if (l != null)
                        sb.AppendLine($"  Emergency[{i}]: {l.name} world={l.transform.position:F2} range={l.range} int={l.intensity}");
                }
            }

            var player = UnityEngine.Object.FindAnyObjectByType<Docencia2FirstPersonPlayer>();
            if (player != null)
            {
                sb.AppendLine($"Player: pos={player.transform.position:F2} rot={player.transform.eulerAngles:F1} standH={player.standingHeight} camY={player.standingCameraY}");
                if (player.playerCamera != null)
                    sb.AppendLine($"  PlayerCam: name={player.playerCamera.name} localPos={player.playerCamera.transform.localPosition:F3} fov={player.playerCamera.fieldOfView} near={player.playerCamera.nearClipPlane} mask={player.playerCamera.cullingMask}");
                if (player.phoneHandRoot != null)
                    sb.AppendLine($"  PhoneHandRoot: parent={player.phoneHandRoot.parent?.name} localPos={player.phoneHandRoot.localPosition:F3} localRot={player.phoneHandRoot.localEulerAngles:F1}");
                if (player.rigDriver != null)
                    sb.AppendLine($"  RigDriver: mount={(player.rigDriver.phoneVisualMount != null ? player.rigDriver.phoneVisualMount.position.ToString("F3") : "NULL")} flashAnchor={(player.rigDriver.phoneFlashAnchor != null ? player.rigDriver.phoneFlashAnchor.position.ToString("F3") : "NULL")}");
            }

            var testigo = GameObject.Find("PB_Interior/LaTestigo") ?? GameObject.Find("LaTestigo");
            if (testigo != null)
            {
                sb.AppendLine($"LaTestigo: world={testigo.transform.position:F3} rot={testigo.transform.eulerAngles:F1} forward={testigo.transform.forward:F2}");
            }

            sb.AppendLine("--- MIRRORS (PB y < 3.4 m) ---");
            foreach (var m in UnityEngine.Object.FindObjectsByType<Docencia2Mirror>(FindObjectsInactive.Include))
            {
                if (m.viewingBounds.center.y < 3.4f)
                {
                    var b = (m.surfaces != null && m.surfaces.Length > 0 && m.surfaces[0] != null) ? m.surfaces[0].bounds : new Bounds();
                    sb.AppendLine($"Mirror: {GetPath(m.transform)} | normal={m.normal} | boundsCenter={m.viewingBounds.center:F3} ext={m.viewingBounds.extents:F3} | surfCenter={b.center:F3} surfSize={b.size:F3}");
                }
            }

            sb.AppendLine("--- VANITIES & SINKS IN PB BANO MUJERES ---");
            var banoM = GameObject.Find("PB_Interior/05_Fase5_Mobiliario_PB/Bano_Mujeres");
            if (banoM != null)
            {
                foreach (var r in banoM.GetComponentsInChildren<Renderer>(true))
                {
                    if (r.name.IndexOf("Lavabo", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        r.name.IndexOf("Espejo", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        r.name.IndexOf("Frente", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        sb.AppendLine($"  {r.name}: boundsCenter={r.bounds.center:F3} size={r.bounds.size:F3}");
                    }
                }
            }

            sb.AppendLine("--- INTERACTIVE DOORS (PB y < 3.0 m) ---");
            foreach (var d in UnityEngine.Object.FindObjectsByType<Docencia2InteractiveDoor>(FindObjectsInactive.Include))
            {
                if (d.transform.position.y < 3.0f)
                {
                    var p = Docencia2FirstPersonPlayer.UnityToPlanXZ(d.transform.position);
                    sb.AppendLine($"Door: {GetPath(d.transform)} | world={d.transform.position:F2} | plan=({p.x:F2}, {p.y:F2}) | inward={d.inwardAngle} | isOpen={d.isOpen} | locked={d.isLocked}");
                }
            }

            sb.AppendLine("--- STAIRCASE & LANDING (PB) ---");
            var stairRoot = GameObject.Find("PB_Interior/22_Cubo_Escaleras_PB");
            if (stairRoot != null)
            {
                Bounds total = new Bounds();
                bool init = false;
                foreach (var r in stairRoot.GetComponentsInChildren<Renderer>(true))
                {
                    if (r.name.IndexOf("Descanso", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        r.name.IndexOf("Tramo", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        r.name.IndexOf("Escal", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        sb.AppendLine($"  StairPart: {r.name} center={r.bounds.center:F2} min={r.bounds.min:F2} max={r.bounds.max:F2}");
                    }
                    if (!init) { total = r.bounds; init = true; } else total.Encapsulate(r.bounds);
                }
                sb.AppendLine($"  StairRootBounds: center={total.center:F2} size={total.size:F2}");
            }

            sb.AppendLine("--- AUDITORIO / AULA 5 / AULA 3 ---");
            foreach (string zName in new[] { "PB_Interior/06_Auditorio_PB", "PB_Interior/27_Aula_5", "PB_Interior/29_Aula_3", "PB_Interior/25_Aula_7" })
            {
                var z = GameObject.Find(zName);
                if (z == null) continue;
                Bounds zb = new Bounds(z.transform.position, Vector3.zero);
                bool init = false;
                foreach (var r in z.GetComponentsInChildren<Renderer>(true))
                {
                    if (!init) { zb = r.bounds; init = true; } else zb.Encapsulate(r.bounds);
                }
                var planC = Docencia2FirstPersonPlayer.UnityToPlanXZ(zb.center);
                sb.AppendLine($"  Zone {zName}: center={zb.center:F2} size={zb.size:F2} planCenter=({planC.x:F2},{planC.y:F2})");
            }

            string outFile = Path.GetFullPath("backup_trailer/Trailer_Scene_Audit.txt");
            File.WriteAllText(outFile, sb.ToString(), Encoding.UTF8);
            Debug.Log("[TrailerSceneInspector] Auditoria guardada en: " + outFile);
        }

        private static string GetPath(Transform t)
        {
            string s = t.name;
            while (t.parent != null)
            {
                t = t.parent;
                s = t.name + "/" + s;
            }
            return s;
        }
    }
}
