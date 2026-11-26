using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Vaelith.Tests.PlayMode
{
    public class VaelithReleasePlayModeTests
    {
        private IEnumerator EnsureMainSceneLoaded()
        {
            AsyncOperation op = SceneManager.LoadSceneAsync("Exterior_Docencia2", LoadSceneMode.Single);
            Assert.IsNotNull(op, "No se pudo iniciar la carga de Exterior_Docencia2");
            while (!op.isDone)
                yield return null;
            yield return null;
            yield return null;
        }

        [UnityTest]
        public IEnumerator PlayerSpawnsSafely_WithValidCharacterControllerCameraAndFloatingPhone()
        {
            yield return EnsureMainSceneLoaded();

            Type playerType = Type.GetType("Vaelith.Docencia2.Docencia2FirstPersonPlayer, Assembly-CSharp");
            Assert.IsNotNull(playerType);

            var player = UnityEngine.Object.FindAnyObjectByType(playerType) as Component;
            Assert.IsNotNull(player, "El jugador no aparece al iniciar Exterior_Docencia2");

            var cc = player.GetComponent<CharacterController>();
            Assert.IsNotNull(cc);
            Assert.IsTrue(cc.enabled, "CharacterController debe iniciar activo");

            var camField = playerType.GetField("playerCamera");
            var cam = camField?.GetValue(player) as Camera;
            Assert.IsNotNull(cam, "La camara en primera persona debe estar asignada");
            Assert.IsTrue(cam.enabled);

            yield return new WaitForSeconds(0.35f);
            Assert.Greater(player.transform.position.y, -1.0f, "El jugador cayo por debajo del nivel del suelo");

            // Verificar aislamiento de cuerpo en Layer 28 en primera persona (celular flotante sin mano)
            var body = GameObject.Find("Jugador_PrimeraPersona_Docencia2/Protagonista/Protagonista_Cuerpo");
            if (body != null)
            {
                Assert.AreEqual(28, body.layer, "En 1a persona el cuerpo debe estar en Layer 28 para no invadir el celular flotante");
            }
        }

        [UnityTest]
        public IEnumerator ModeSwitcher_AlternatesNocheAndTerror_AndRecoversInteriorTrackers()
        {
            yield return EnsureMainSceneLoaded();

            Type switcherType = Type.GetType("Vaelith.Docencia2.Docencia2ModeSwitcher, Assembly-CSharp");
            Type darknessType = Type.GetType("Vaelith.Docencia2.Docencia2TerrorDarknessController, Assembly-CSharp");
            Type enumType = Type.GetType("Vaelith.Docencia2.Docencia2VisualMode, Assembly-CSharp");
            Assert.IsNotNull(switcherType);

            var switcher = UnityEngine.Object.FindAnyObjectByType(switcherType) as Component;
            var darkness = UnityEngine.Object.FindAnyObjectByType(darknessType) as Component;
            Assert.IsNotNull(switcher);

            MethodInfo applyMethod = switcherType.GetMethod("ApplyMode");
            MethodInfo syncMethod = darknessType?.GetMethod("SyncState");

            // 1. Modo NOCHE: grupo NOCHE de PA activo
            applyMethod.Invoke(switcher, new[] { Enum.ToObject(enumType, 0), false });
            syncMethod?.Invoke(darkness, null);
            yield return null;
            var paNoche = GameObject.Find("PA_Interior/Iluminacion_PA_Mismo_Criterio_PB/NOCHE");
            Assert.IsNotNull(paNoche);
            Assert.IsTrue(paNoche.activeSelf, "Las luminarias NOCHE de PA deben encenderse en modo NOCHE");

            // 2. Modo TERROR: oscuridad interior activa
            applyMethod.Invoke(switcher, new[] { Enum.ToObject(enumType, 1), false });
            syncMethod?.Invoke(darkness, null);
            yield return null;

            // 3. Regreso a modo NOCHE: los trackers interiores deben reactivar las luminarias NOCHE
            applyMethod.Invoke(switcher, new[] { Enum.ToObject(enumType, 0), false });
            syncMethod?.Invoke(darkness, null);
            yield return null;
            yield return null;
            Assert.IsTrue(paNoche.activeSelf, "Al regresar de TERROR a NOCHE, Docencia2InteriorModeTracker debe reactivar el grupo NOCHE");
        }

        [UnityTest]
        public IEnumerator GlazedCancelDoors_PBAndPA_OpenAndCloseSmoothly()
        {
            yield return EnsureMainSceneLoaded();

            Type doorType = Type.GetType("Vaelith.Docencia2.Docencia2InteractiveDoor, Assembly-CSharp");
            FieldInfo isOpenField = doorType.GetField("isOpen");
            MethodInfo setOpenAnim = doorType.GetMethod("SetOpenAnimated");

            string[] cancelDoorPaths =
            {
                "PB_Interior/03_Fase3_Carpinteria_y_Vidrios/Canceleria_Interior_Vidrio_Verde/Cancel_Vidrio_Sec06_PasilloOeste_x11.24/P15_Alcoba_Oeste_CancelVidrio/Pivote_Bisagra",
                "PA_Interior/Pasillo_Junto_Redes/Entrada_P16/P16_Hueco096_Abre_oeste/Bisagra_Giro90_Interior"
            };

            foreach (string path in cancelDoorPaths)
            {
                var doorGo = GameObject.Find(path);
                Assert.IsNotNull(doorGo, $"No se encontro la puerta de cancel en {path}");
                var door = doorGo.GetComponent(doorType);
                Assert.IsNotNull(door);

                Quaternion closedRot = doorGo.transform.localRotation;
                setOpenAnim.Invoke(door, new object[] { true, false });
                yield return new WaitForSeconds(0.50f);

                Assert.IsTrue((bool)isOpenField.GetValue(door), $"La puerta {path} debio quedar marcada como abierta");
                Assert.Greater(Quaternion.Angle(doorGo.transform.localRotation, closedRot), 60f,
                    $"La hoja de {path} debio rotar al abrirse");

                setOpenAnim.Invoke(door, new object[] { false, false });
                yield return new WaitForSeconds(0.50f);
                Assert.IsFalse((bool)isOpenField.GetValue(door), $"La puerta {path} debio volver a cerrarse");
            }
        }

        [UnityTest]
        public IEnumerator DirectorDoorP29_RemainsLockedAgainstInteraction()
        {
            yield return EnsureMainSceneLoaded();

            var p29Go = GameObject.Find("PA_Interior/Director/Madera_Division/P29_Hueco096_Abre_este/Bisagra_Giro90_Interior");
            Assert.IsNotNull(p29Go, "No se encontro la puerta P29 del Cubiculo del Director");

            Type doorType = Type.GetType("Vaelith.Docencia2.Docencia2InteractiveDoor, Assembly-CSharp");
            var door = p29Go.GetComponent(doorType);
            Assert.IsNotNull(door);

            FieldInfo isLockedField = doorType.GetField("isLocked");
            FieldInfo isOpenField = doorType.GetField("isOpen");
            MethodInfo toggleMethod = doorType.GetMethod("Toggle");
            MethodInfo setOpenAnim = doorType.GetMethod("SetOpenAnimated");

            Assert.IsTrue((bool)isLockedField.GetValue(door), "P29 del Director debe estar bloqueada permanentemente");

            toggleMethod.Invoke(door, new object[] { Vector3.zero });
            setOpenAnim.Invoke(door, new object[] { true, false });
            yield return new WaitForSeconds(0.25f);

            Assert.IsFalse((bool)isOpenField.GetValue(door), "P29 bloqueada nunca debe abrirse");
        }

        [UnityTest]
        public IEnumerator TerrorMode_PreservesExteriorLighting_AndPreservesNocheShadows()
        {
            yield return EnsureMainSceneLoaded();

            Type playerType = Type.GetType("Vaelith.Docencia2.Docencia2FirstPersonPlayer, Assembly-CSharp");
            Type switcherType = Type.GetType("Vaelith.Docencia2.Docencia2ModeSwitcher, Assembly-CSharp");
            Type darknessType = Type.GetType("Vaelith.Docencia2.Docencia2TerrorDarknessController, Assembly-CSharp");
            Type enumType = Type.GetType("Vaelith.Docencia2.Docencia2VisualMode, Assembly-CSharp");

            var player = UnityEngine.Object.FindAnyObjectByType(playerType) as Component;
            var switcher = UnityEngine.Object.FindAnyObjectByType(switcherType) as Component;
            var darkness = UnityEngine.Object.FindAnyObjectByType(darknessType) as Component;
            Assert.IsNotNull(player);
            Assert.IsNotNull(switcher);
            Assert.IsNotNull(darkness);

            MethodInfo applyMethod = switcherType.GetMethod("ApplyMode");
            MethodInfo teleportMethod = playerType.GetMethod("TeleportToPlan");
            MethodInfo isOutdoorMethod = darknessType.GetMethod("IsViewerOutdoor");

            applyMethod.Invoke(switcher, new[] { Enum.ToObject(enumType, 0), false });
            yield return null;
            var nocheMoon = GameObject.Find("Iluminacion_Cinematografica_Docencia2/Luz_Luna_Principal_7500K")?.GetComponent<Light>();
            Assert.IsNotNull(nocheMoon);
            Assert.AreNotEqual(LightShadows.None, nocheMoon.shadows, "Luz_Luna_Principal_7500K debe conservar sombras en Play NOCHE");

            applyMethod.Invoke(switcher, new[] { Enum.ToObject(enumType, 1), false });
            teleportMethod.Invoke(player, new object[] { 24.5f, 0.20f, 19.29f, -90f });
            yield return null;
            Assert.IsFalse((bool)isOutdoorMethod.Invoke(darkness, null));
            Assert.Less(RenderSettings.ambientSkyColor.b, 0.04f, "El interior TERROR debe estar oscuro");

            teleportMethod.Invoke(player, new object[] { 33.0f, 0.20f, 19.29f, -90f });
            yield return null;
            Assert.IsTrue((bool)isOutdoorMethod.Invoke(darkness, null));
            Assert.Greater(RenderSettings.ambientSkyColor.b, 0.06f, "El exterior TERROR no debe aplastarse a negro");
        }
    }
}
