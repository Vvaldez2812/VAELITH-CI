using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Vaelith.Tests.EditMode
{
    public class VaelithReleaseEditModeTests
    {
        private const string MainScenePath = "Assets/Scenes/Exterior_Docencia2.unity";

        [Test]
        public void MainSceneExists_AndIsConfiguredInBuildSettings()
        {
            Assert.IsTrue(File.Exists(MainScenePath), $"No existe la escena principal en {MainScenePath}");

            var enabledScenes = EditorBuildSettings.scenes
                .Where(s => s != null && s.enabled)
                .Select(s => s.path)
                .ToArray();

            Assert.Contains(MainScenePath, enabledScenes,
                "Exterior_Docencia2.unity debe estar habilitada en EditorBuildSettings.scenes");
        }

        [Test]
        public void RequiredPackages_AndReleaseScriptsExist()
        {
            Assert.IsTrue(File.Exists("Packages/manifest.json"), "Falta Packages/manifest.json");
            Assert.IsTrue(File.Exists("Packages/packages-lock.json"), "Falta Packages/packages-lock.json");
            Assert.IsTrue(File.Exists("Assets/Editor/BuildCI.cs"), "Falta Assets/Editor/BuildCI.cs");

            string manifest = File.ReadAllText("Packages/manifest.json");
            Assert.IsTrue(manifest.Contains("\"com.unity.inputsystem\""), "Falta com.unity.inputsystem en manifest.json");
            Assert.IsTrue(manifest.Contains("\"com.unity.render-pipelines.universal\""), "Falta URP en manifest.json");
            Assert.IsTrue(manifest.Contains("\"com.unity.test-framework\""), "Falta Test Framework en manifest.json");

            Assert.IsNotNull(Type.GetType("BuildCI, Assembly-CSharp-Editor"), "No se resolvio BuildCI en Assembly-CSharp-Editor");
            Assert.IsNotNull(Type.GetType("Vaelith.Docencia2.Docencia2FirstPersonPlayer, Assembly-CSharp"), "No se resolvio Docencia2FirstPersonPlayer");
            Assert.IsNotNull(Type.GetType("Vaelith.Docencia2.Docencia2ModeSwitcher, Assembly-CSharp"), "No se resolvio Docencia2ModeSwitcher");
            Assert.IsNotNull(Type.GetType("Vaelith.Docencia2.Docencia2InteractiveDoor, Assembly-CSharp"), "No se resolvio Docencia2InteractiveDoor");
            Assert.IsNotNull(Type.GetType("Vaelith.Docencia2.Docencia2TerrorDarknessController, Assembly-CSharp"), "No se resolvio Docencia2TerrorDarknessController");
        }

        [Test]
        public void ExteriorDocencia2Scene_HasValidPlayerDoorsAndModeSwitcher()
        {
            var scene = EditorSceneManager.OpenScene(MainScenePath, OpenSceneMode.Single);
            Assert.IsTrue(scene.IsValid(), "La escena Exterior_Docencia2 no se pudo abrir en EditMode");

            Type playerType = Type.GetType("Vaelith.Docencia2.Docencia2FirstPersonPlayer, Assembly-CSharp");
            Type switcherType = Type.GetType("Vaelith.Docencia2.Docencia2ModeSwitcher, Assembly-CSharp");
            Type doorType = Type.GetType("Vaelith.Docencia2.Docencia2InteractiveDoor, Assembly-CSharp");
            Type darknessType = Type.GetType("Vaelith.Docencia2.Docencia2TerrorDarknessController, Assembly-CSharp");

            var player = UnityEngine.Object.FindFirstObjectByType(playerType) as Component;
            Assert.IsNotNull(player, "No se encontro Docencia2FirstPersonPlayer en Exterior_Docencia2");

            var cc = player.GetComponent<CharacterController>();
            Assert.IsNotNull(cc, "El jugador debe tener CharacterController");
            Assert.AreEqual(1.75f, cc.height, 0.05f, "La altura del jugador debe ser ~1.75 m");
            Assert.AreEqual(0.30f, cc.radius, 0.05f, "El radio del CharacterController debe ser ~0.30 m");

            var spotField = playerType.GetField("phoneSpotLight");
            var spot = spotField?.GetValue(player) as Light;
            Assert.IsNotNull(spot, "El jugador debe tener asignado phoneSpotLight");
            Assert.AreEqual(LightType.Spot, spot.type);
            Assert.AreEqual(LightShadows.Soft, spot.shadows, "La linterna del celular debe usar sombras suaves");
            Assert.GreaterOrEqual(spot.range, 6.0f);

            var switcher = UnityEngine.Object.FindFirstObjectByType(switcherType);
            Assert.IsNotNull(switcher, "Falta Docencia2ModeSwitcher en la escena");

            var darkness = UnityEngine.Object.FindFirstObjectByType(darknessType);
            Assert.IsNotNull(darkness, "Falta Docencia2TerrorDarknessController en la escena");

            var doors = UnityEngine.Object.FindObjectsByType(doorType, FindObjectsSortMode.None);
            Assert.GreaterOrEqual(doors.Length, 10, "Se esperaban al menos 10 puertas interactivas configuradas en Docencia 2");
        }
    }
}
