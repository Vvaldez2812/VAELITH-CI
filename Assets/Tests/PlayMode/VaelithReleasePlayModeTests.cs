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
        public IEnumerator PlayerSpawnsSafely_WithValidCharacterControllerAndCamera()
        {
            yield return EnsureMainSceneLoaded();

            Type playerType = Type.GetType("Vaelith.Docencia2.Docencia2FirstPersonPlayer, Assembly-CSharp");
            Assert.IsNotNull(playerType);

            var player = UnityEngine.Object.FindFirstObjectByType(playerType) as Component;
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
        }

        [UnityTest]
        public IEnumerator ModeSwitcher_AlternatesNocheAndTerrorWithoutErrors()
        {
            yield return EnsureMainSceneLoaded();

            Type switcherType = Type.GetType("Vaelith.Docencia2.Docencia2ModeSwitcher, Assembly-CSharp");
            Type enumType = Type.GetType("Vaelith.Docencia2.Docencia2VisualMode, Assembly-CSharp");
            Assert.IsNotNull(switcherType);
            Assert.IsNotNull(enumType);

            var switcher = UnityEngine.Object.FindFirstObjectByType(switcherType);
            Assert.IsNotNull(switcher);

            FieldInfo modeField = switcherType.GetField("modoActual");
            MethodInfo applyMethod = switcherType.GetMethod("ApplyMode");
            Assert.IsNotNull(modeField);
            Assert.IsNotNull(applyMethod);

            object nocheVal = Enum.ToObject(enumType, 0);
            object terrorVal = Enum.ToObject(enumType, 1);

            applyMethod.Invoke(switcher, new[] { terrorVal, false });
            yield return null;
            Assert.AreEqual(terrorVal, modeField.GetValue(switcher));

            applyMethod.Invoke(switcher, new[] { nocheVal, false });
            yield return null;
            Assert.AreEqual(nocheVal, modeField.GetValue(switcher));

            applyMethod.Invoke(switcher, new[] { terrorVal, false });
            yield return null;
        }

        [UnityTest]
        public IEnumerator Flashlight_TogglesCleanly_AndRemainsOnlySoftShadowSpot()
        {
            yield return EnsureMainSceneLoaded();

            Type playerType = Type.GetType("Vaelith.Docencia2.Docencia2FirstPersonPlayer, Assembly-CSharp");
            var player = UnityEngine.Object.FindFirstObjectByType(playerType);
            Assert.IsNotNull(player);

            MethodInfo setFlashlight = playerType.GetMethod("SetFlashlightState");
            FieldInfo spotField = playerType.GetField("phoneSpotLight");
            Assert.IsNotNull(setFlashlight);
            Assert.IsNotNull(spotField);

            var spot = spotField.GetValue(player) as Light;
            Assert.IsNotNull(spot);

            setFlashlight.Invoke(player, new object[] { true, false });
            yield return null;
            Assert.IsTrue(spot.enabled, "La linterna debe encenderse");
            Assert.AreEqual(LightShadows.Soft, spot.shadows);

            setFlashlight.Invoke(player, new object[] { false, false });
            yield return null;
            Assert.IsFalse(spot.enabled, "La linterna debe apagarse");
        }

        [UnityTest]
        public IEnumerator InteractiveDoor_OpensAndClosesAnimated()
        {
            yield return EnsureMainSceneLoaded();

            Type doorType = Type.GetType("Vaelith.Docencia2.Docencia2InteractiveDoor, Assembly-CSharp");
            var door = UnityEngine.Object.FindFirstObjectByType(doorType) as Component;
            Assert.IsNotNull(door, "No se encontro ninguna Docencia2InteractiveDoor");

            FieldInfo isOpenField = doorType.GetField("isOpen");
            FieldInfo closedRotField = doorType.GetField("closedRotation");
            MethodInfo setOpenAnim = doorType.GetMethod("SetOpenAnimated");

            bool initialOpen = (bool)isOpenField.GetValue(door);
            if (initialOpen)
            {
                setOpenAnim.Invoke(door, new object[] { false, false });
                yield return new WaitForSeconds(0.55f);
            }

            Quaternion closedRot = (Quaternion)closedRotField.GetValue(door);
            setOpenAnim.Invoke(door, new object[] { true, false });
            yield return new WaitForSeconds(0.55f);

            Assert.IsTrue((bool)isOpenField.GetValue(door), "La puerta debio quedar marcada como abierta");
            Assert.Greater(Quaternion.Angle(door.transform.localRotation, closedRot), 15f,
                "La hoja de la puerta debio rotar al abrirse");

            setOpenAnim.Invoke(door, new object[] { false, false });
            yield return new WaitForSeconds(0.55f);
            Assert.IsFalse((bool)isOpenField.GetValue(door), "La puerta debio volver a cerrarse");
        }
    }
}
