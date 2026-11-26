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
            Assert.IsTrue(File.Exists("Assets/Scripts/Docencia2ReleaseTelemetry.cs"), "Falta Docencia2ReleaseTelemetry.cs");

            string manifest = File.ReadAllText("Packages/manifest.json");
            Assert.IsTrue(manifest.Contains("\"com.unity.inputsystem\""), "Falta com.unity.inputsystem en manifest.json");
            Assert.IsTrue(manifest.Contains("\"com.unity.render-pipelines.universal\""), "Falta URP en manifest.json");
            Assert.IsTrue(manifest.Contains("\"com.unity.test-framework\""), "Falta Test Framework en manifest.json");

            Assert.IsNotNull(Type.GetType("BuildCI, Assembly-CSharp-Editor"), "No se resolvio BuildCI en Assembly-CSharp-Editor");
            Assert.IsNotNull(Type.GetType("Vaelith.Docencia2.Docencia2FirstPersonPlayer, Assembly-CSharp"), "No se resolvio Docencia2FirstPersonPlayer");
            Assert.IsNotNull(Type.GetType("Vaelith.Docencia2.Docencia2ModeSwitcher, Assembly-CSharp"), "No se resolvio Docencia2ModeSwitcher");
            Assert.IsNotNull(Type.GetType("Vaelith.Docencia2.Docencia2InteractiveDoor, Assembly-CSharp"), "No se resolvio Docencia2InteractiveDoor");
            Assert.IsNotNull(Type.GetType("Vaelith.Docencia2.Docencia2TerrorDarknessController, Assembly-CSharp"), "No se resolvio Docencia2TerrorDarknessController");
            Assert.IsNotNull(Type.GetType("Vaelith.Docencia2.Docencia2PhonePresentation, Assembly-CSharp"), "No se resolvio Docencia2PhonePresentation");
            Assert.IsNotNull(Type.GetType("Vaelith.Docencia2.Docencia2ReleaseTelemetry, Assembly-CSharp"), "No se resolvio Docencia2ReleaseTelemetry");
        }

        [Test]
        public void ExteriorDocencia2Scene_HasValidPlayerDoorsAndModeSwitcher()
        {
            var scene = EditorSceneManager.OpenScene(MainScenePath, OpenSceneMode.Single);
            Assert.IsTrue(scene.IsValid(), "La escena Exterior_Docencia2 no se pudo abrir en EditMode");

            Type playerType = Type.GetType("Vaelith.Docencia2.Docencia2FirstPersonPlayer, Assembly-CSharp");
            Type switcherType = Type.GetType("Vaelith.Docencia2.Docencia2ModeSwitcher, Assembly-CSharp");
            Type doorType = Type.GetType("Vaelith.Docencia2.Docencia2InteractiveDoor, Assembly-CSharp");
            Type paDoorType = Type.GetType("Vaelith.Docencia2.Docencia2PADoor, Assembly-CSharp");
            Type darknessType = Type.GetType("Vaelith.Docencia2.Docencia2TerrorDarknessController, Assembly-CSharp");

            var player = UnityEngine.Object.FindAnyObjectByType(playerType) as Component;
            Assert.IsNotNull(player, "No se encontro Docencia2FirstPersonPlayer en Exterior_Docencia2");

            var cc = player.GetComponent<CharacterController>();
            Assert.IsNotNull(cc, "El jugador debe tener CharacterController");
            Assert.AreEqual(1.60f, cc.height, 0.05f, "La altura del personaje principal debe ser ~1.60 m");
            Assert.AreEqual(0.30f, cc.radius, 0.05f, "El radio del CharacterController debe ser ~0.30 m");

            var spotField = playerType.GetField("phoneSpotLight");
            var spot = spotField?.GetValue(player) as Light;
            Assert.IsNotNull(spot, "El jugador debe tener asignado phoneSpotLight");
            Assert.AreEqual(LightType.Spot, spot.type);
            Assert.AreEqual(LightShadows.Soft, spot.shadows, "La linterna del celular debe usar sombras suaves");
            Assert.GreaterOrEqual(spot.range, 6.0f);

            var switcher = UnityEngine.Object.FindAnyObjectByType(switcherType);
            Assert.IsNotNull(switcher, "Falta Docencia2ModeSwitcher en la escena");

            var darkness = UnityEngine.Object.FindAnyObjectByType(darknessType);
            Assert.IsNotNull(darkness, "Falta Docencia2TerrorDarknessController en la escena");

            var doors = UnityEngine.Object.FindObjectsByType(doorType, FindObjectsInactive.Include);
            Assert.GreaterOrEqual(doors.Length, 65, "Se esperaban al menos 65 puertas interactivas configuradas en Docencia 2");

            var paDoors = UnityEngine.Object.FindObjectsByType(paDoorType, FindObjectsInactive.Include);
            Assert.AreEqual(29, paDoors.Length, "Planta Alta debe conservar exactamente sus 29 puertas registradas");
        }

        [Test]
        public void CharactersAndFloatingPhoneRig_AreConfiguredCorrectly()
        {
            EditorSceneManager.OpenScene(MainScenePath, OpenSceneMode.Single);

            Assert.IsNotNull(GameObject.Find("Jugador_PrimeraPersona_Docencia2/Protagonista"),
                "Falta el rig 3D de la protagonista Jeid Verdugo");
            Assert.IsNotNull(GameObject.Find("PB_Interior/LaTestigo"),
                "Falta la entidad 3D LaTestigo en Planta Baja");

            Type phonePresType = Type.GetType("Vaelith.Docencia2.Docencia2PhonePresentation, Assembly-CSharp");
            var phonePres = UnityEngine.Object.FindAnyObjectByType(phonePresType) as Component;
            Assert.IsNotNull(phonePres, "Falta Docencia2PhonePresentation en el jugador");

            var floatingField = phonePresType.GetField("floatingWithoutHand");
            Assert.IsNotNull(floatingField);
            Assert.IsTrue((bool)floatingField.GetValue(phonePres), "El celular en 1a persona debe estar configurado en modo flotante sin mano");
        }

        [Test]
        public void ArchitecturalZones_StairwellCancelsAndLuminairesExist()
        {
            EditorSceneManager.OpenScene(MainScenePath, OpenSceneMode.Single);

            Assert.IsNotNull(GameObject.Find("Docencia2_Exterior"), "Falta Docencia2_Exterior");
            Assert.IsNotNull(GameObject.Find("PB_Interior"), "Falta PB_Interior");
            Assert.IsNotNull(GameObject.Find("PA_Interior"), "Falta PA_Interior");
            Assert.IsNotNull(GameObject.Find("PA_Interior/Escalera_Sec05_VentanaYPlafon_Real"), "Falta ventana trasera 6x2 y plafon de escalera Sec05");
            Assert.IsNotNull(GameObject.Find("PA_Interior/Decoracion_Pasillo_Sec06_PA_Fotos"), "Falta decoracion de pasillo Sec06 en PA");
            Assert.IsNotNull(GameObject.Find("PA_Interior/Pasillo_Central/Plafon_CirculacionPA_Zona2_SUPUESTO"), "Falta plafon modular de Circulacion PA Zona 2");
            Assert.IsNotNull(GameObject.Find("PA_Interior/Aula_16_Taller/Plafon_Aula16_Zona1_SUPUESTO"), "Falta plafon modular de Aula 16 Zona 1");
        }
    }
}
