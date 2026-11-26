using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Vaelith.Docencia2.Editor
{
    /// <summary>
    /// Punto de entrada único en el menú VAELITH para preparar toda la escena completa,
    /// restaurar arquitectura faltante si se borró, aplicar correcciones e integrar a La Testigo.
    /// </summary>
    public static class VaelithUnifiedMenu
    {
        private const string ScenePath = "Assets/Scenes/Exterior_Docencia2.unity";
        private const string BackupCompleteScenePath = "Assets/Scenes/Exterior_Docencia2_backup.unity";

        [MenuItem("VAELITH/Preparar Todo (Escena Completa + Personajes)", false, 1)]
        public static void PrepararTodo()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogWarning("[VAELITH] Sal de Play Mode primero para ejecutar 'Preparar Todo'.");
                if (!Application.isBatchMode)
                {
                    EditorUtility.DisplayDialog(
                        "VAELITH",
                        "Sal de Play Mode primero y vuelve a pulsar VAELITH -> Preparar Todo.",
                        "OK");
                }
                return;
            }

            if (!File.Exists(ScenePath))
            {
                throw new FileNotFoundException($"No se encontro la escena principal en {ScenePath}");
            }

            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            // Si algún constructor anterior borró Planta Alta, Auditorio o Jugador, restaurar la escena base completa.
            if (!IsSceneComplete(scene))
            {
                scene = RestoreCompleteBaseScene();
            }

            // 1. Aplicar correcciones e iluminación / colliders / puertas / jugador / perfil TERROR de forma idempotente.
            VaelithAuditFixer.RunAllAuditFixesBatch();

            // 2. Reemplazar mesabancos escolares Tripo en Planta Baja y Planta Alta.
            global::MesabancoIntegrator.IntegrateAll();

            // 3. Configurar materiales URP, Avatar Humanoid, Animación, Prefab y colocar a La Testigo en PB_Interior.
            global::LaTestigoIntegrator.IntegrateAll();

            // 4. Integrar Protagonista rigged (1.60 m, Layer 28 espejo, Celular_Pro en mano, animaciones e IK y vista 1ª/3ª con tecla V).
            global::ProtagonistaIntegrator.IntegrateAll();

            // 5. Verificación final y guardado limpio en modo TERROR listo para Play Mode.
            scene = SceneManager.GetActiveScene();
            bool hasLaTestigo = FindByHierarchyPath(scene, "PB_Interior/LaTestigo") != null
                || FindByHierarchyPath(scene, "LaTestigo") != null;
            bool hasProtagonista = FindByHierarchyPath(scene, "Jugador_PrimeraPersona_Docencia2/Protagonista") != null;
            if (!IsSceneComplete(scene) || !hasLaTestigo || !hasProtagonista)
            {
                throw new Exception("La preparacion termino pero falta algun objeto principal en Exterior_Docencia2.unity.");
            }

            var switcher = UnityEngine.Object.FindAnyObjectByType<Docencia2ModeSwitcher>();
            if (switcher != null && switcher.modoActual != Docencia2VisualMode.Terror)
            {
                switcher.ApplyMode(Docencia2VisualMode.Terror, false);
            }

            EditorSceneManager.MarkSceneDirty(scene);

            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

            const string summary =
                "Escena completa preparada (Exterior, PB/PA, Auditorio, Mesabancos Tripo, Protagonista 1.60 m con Celular_Pro y espejo [V: 1ª/3ª], La Testigo 2.30 m y Modo TERROR). Ya puedes darle Play.";
            Debug.Log("[VAELITH] " + summary);

            if (!Application.isBatchMode)
            {
                EditorUtility.DisplayDialog("VAELITH - Todo Listo", summary, "OK");
            }
        }

        private static bool IsSceneComplete(Scene scene)
        {
            if (!scene.IsValid()) return false;
            return FindByHierarchyPath(scene, "Docencia2_Exterior") != null
                && FindByHierarchyPath(scene, "PB_Interior") != null
                && FindByHierarchyPath(scene, "PA_Interior") != null
                && FindByHierarchyPath(scene, "PB_Interior/06_Auditorio_PB") != null
                && FindByHierarchyPath(scene, "Jugador_PrimeraPersona_Docencia2") != null
                && FindByHierarchyPath(scene, "Controlador_Modo_Noche_Terror") != null;
        }

        private static Transform FindByHierarchyPath(Scene scene, string hierarchyPath)
        {
            if (!scene.IsValid() || string.IsNullOrEmpty(hierarchyPath)) return null;
            string[] parts = hierarchyPath.Split('/');
            Transform current = null;
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (root.name == parts[0])
                {
                    current = root.transform;
                    break;
                }
            }
            for (int i = 1; i < parts.Length && current != null; i++)
            {
                current = current.Find(parts[i]);
            }
            return current;
        }

        private static Scene RestoreCompleteBaseScene()
        {
            if (!File.Exists(BackupCompleteScenePath))
            {
                throw new FileNotFoundException(
                    $"Falta geometria de escena completa (PA_Interior / Auditorio) y no existe respaldo en {BackupCompleteScenePath}.");
            }

            string raw = File.ReadAllText(BackupCompleteScenePath);
            if (!raw.Contains("PA_Interior") || !raw.Contains("PB_Interior"))
            {
                throw new InvalidOperationException(
                    $"El respaldo {BackupCompleteScenePath} tampoco contiene PA_Interior y PB_Interior completos.");
            }

            Debug.Log("[VAELITH] Restaurando escena completa (Exterior + PB + PA + Auditorio + Jugador) desde Exterior_Docencia2_backup.unity...");
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            File.Copy(BackupCompleteScenePath, ScenePath, overwrite: true);
            AssetDatabase.ImportAsset(ScenePath, ImportAssetOptions.ForceSynchronousImport);
            return EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        }
    }
}
