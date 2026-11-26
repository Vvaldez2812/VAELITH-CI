using System;
using System.Collections.Generic;
using UnityEngine;

namespace Vaelith.Trailer
{
    public enum TrailerShotCategory
    {
        ArchitecturalExterior,
        ArchitecturalInterior,
        FirstPersonJeid,
        ThirdPersonJeid,
        BlackTitleCard
    }

    public enum TrailerFlashlightMode
    {
        Off,
        On,
        OnAtSecond08,
        FlickerDip,
        FlickerAndDie
    }

    [Serializable]
    public class TrailerShotDefinition
    {
        public string id;
        public string trackTitle;
        public double startTime;
        public double duration;
        public TrailerShotCategory category;

        [Header("Cámara y Lente")]
        public Vector3 camPosStart;
        public bool useCamMid = false;
        public Vector3 camPosMid;
        public Vector3 camPosEnd;
        public Vector3 lookTargetStart;
        public bool useLookMid = false;
        public Vector3 lookTargetMid;
        public Vector3 lookTargetEnd;
        public float fovStart = 48f;
        public float fovEnd = 48f;
        public float nearClip = 0.08f;
        public float farClip = 350f;
        public float dutchAngle = 0f;
        public float handheldPosAmp = 0f;
        public float handheldRotAmp = 0f;

        [Header("Post-Proceso y Enfoque")]
        public bool useDof = false;
        public float dofFocusDistance = 4.0f;
        public float dofAperture = 4.0f;
        public bool isIndoorFog = false;
        public float customIndoorFogDensity = 0.022f;
        public float customPostExposureOffset = 0f;

        [Header("Jeid (Protagonista 1.599 m)")]
        public bool showJeid = false;
        public bool isThirdPerson = false;
        public bool includeLayer28 = false;
        public float jeidWalkSpeed = 0f;
        public Vector3 jeidPosStart;
        public Vector3 jeidPosEnd;
        public float jeidYawStart = 0f;
        public float jeidYawEnd = 0f;
        public TrailerFlashlightMode flashlightMode = TrailerFlashlightMode.Off;
        public bool useClosePhoneFraming = false;

        [Header("La Testigo (2.302 m · Ruta A1)")]
        public bool showTestigo = false;
        public Vector3 testigoPosPrimary;
        public Vector3 testigoPosAfterBlackout;
        public float testigoYaw = 90f;
        public float testigoAppearWindowStart = -1f;
        public float testigoAppearWindowEnd = -1f;
        public bool testigoHasBlackoutStep = false;
        public float blackoutStart = -1f;
        public float blackoutEnd = -1f;

        [Header("Entorno e Iluminación Local (solo interior)")]
        public bool openMainPortal = false;
        public bool openBanoMujeresDoor = false;
        public bool openAula3Door = false;
        public int interiorAccentId = 0; // 0 = ninguno (SIEMPRE 0 en exterior)
    }

    public static class TrailerShotCatalog
    {
        // Formula canónica exacta de Docencia2FirstPersonPlayer.PlanToUnityPos
        public static Vector3 Plan(float xPlan, float yHeight, float zPlan)
        {
            float ux = 27.00f - zPlan * (54.00f / 53.92f);
            float uz = 14.25f - xPlan * (28.50f / 28.48f);
            return new Vector3(ux, yHeight, uz);
        }

        public static List<TrailerShotDefinition> BuildCanon20Shots()
        {
            var list = new List<TrailerShotDefinition>(20);
            double t = 0.0;

            void Add(TrailerShotDefinition s)
            {
                s.startTime = t;
                t += s.duration;
                list.Add(s);
            }

            // T01 (0:00–0:05): Mano derecha con Celular_Pro en Aula 7 oscura (altura ojos ~1.50 m -> Y=1.68 sobre piso 0.20)
            Add(new TrailerShotDefinition
            {
                id = "T01",
                trackTitle = "T01_Gancho_Celular_0317",
                duration = 5.0,
                category = TrailerShotCategory.FirstPersonJeid,
                camPosStart = Plan(7.20f, 1.68f, 27.20f),
                camPosEnd = Plan(7.20f, 1.68f, 27.20f),
                lookTargetStart = Plan(8.10f, 1.48f, 26.55f),
                lookTargetEnd = Plan(8.45f, 1.54f, 26.85f),
                fovStart = 44f,
                fovEnd = 48f,
                nearClip = 0.04f,
                useDof = true,
                dofFocusDistance = 0.34f,
                dofAperture = 2.8f,
                isIndoorFog = true,
                customIndoorFogDensity = 0.020f,
                handheldPosAmp = 0.004f,
                handheldRotAmp = 0.35f,
                showJeid = true,
                isThirdPerson = false,
                includeLayer28 = false,
                jeidPosStart = Plan(7.20f, 0.20f, 27.20f),
                jeidPosEnd = Plan(7.20f, 0.20f, 27.20f),
                jeidYawStart = 128f,
                jeidYawEnd = 112f,
                flashlightMode = TrailerFlashlightMode.Off,
                useClosePhoneFraming = true
            });

            // T02 (0:05–0:10): Vestíbulo PB — Jeid enciende linterna (F) hacia puertas dobles cerradas
            Add(new TrailerShotDefinition
            {
                id = "T02",
                trackTitle = "T02_Gancho_Vestibulo_Linterna",
                duration = 5.0,
                category = TrailerShotCategory.FirstPersonJeid,
                camPosStart = Plan(23.80f, 1.68f, 19.30f),
                camPosEnd = Plan(25.10f, 1.68f, 19.30f),
                lookTargetStart = Plan(27.80f, 1.58f, 19.70f),
                lookTargetEnd = Plan(28.18f, 1.62f, 19.30f),
                fovStart = 58f,
                fovEnd = 54f,
                nearClip = 0.04f,
                useDof = false,
                isIndoorFog = true,
                customIndoorFogDensity = 0.026f,
                handheldPosAmp = 0.004f,
                handheldRotAmp = 0.30f,
                showJeid = true,
                isThirdPerson = false,
                includeLayer28 = false,
                jeidPosStart = Plan(23.80f, 0.20f, 19.30f),
                jeidPosEnd = Plan(25.10f, 0.20f, 19.30f),
                jeidYawStart = 172f,
                jeidYawEnd = 180f,
                flashlightMode = TrailerFlashlightMode.OnAtSecond08,
                openMainPortal = false
            });

            // T03 (0:10–0:16): Exterior aéreo nocturno en Modo Terror (Unity + Flow)
            Add(new TrailerShotDefinition
            {
                id = "T03",
                trackTitle = "T03_Exterior_Aereo_Docencia2",
                duration = 6.0,
                category = TrailerShotCategory.ArchitecturalExterior,
                camPosStart = new Vector3(30.0f, 14.0f, -30.0f),
                camPosEnd = new Vector3(22.0f, 9.5f, -24.0f),
                lookTargetStart = new Vector3(4.0f, 3.0f, -9.0f),
                lookTargetEnd = new Vector3(6.5f, 2.6f, -12.5f),
                fovStart = 38f,
                fovEnd = 38f,
                nearClip = 0.20f,
                isIndoorFog = false,
                showJeid = false,
                interiorAccentId = 0
            });

            // T04 (0:16–0:23): Exterior fachada Este en Modo Terror, travelling lateral bajo (Unity + Flow)
            Add(new TrailerShotDefinition
            {
                id = "T04",
                trackTitle = "T04_Exterior_Fachada_Este",
                duration = 7.0,
                category = TrailerShotCategory.ArchitecturalExterior,
                camPosStart = new Vector3(37.5f, 2.05f, -33.5f),
                camPosEnd = new Vector3(21.5f, 1.95f, -29.0f),
                lookTargetStart = new Vector3(4.5f, 3.6f, -6.5f),
                lookTargetEnd = new Vector3(6.5f, 3.3f, -11.5f),
                fovStart = 40f,
                fovEnd = 40f,
                nearClip = 0.15f,
                isIndoorFog = false,
                showJeid = false,
                interiorAccentId = 0
            });

            // T05 (0:23–0:29): Exterior acercamiento lento al portal doble de cristal (Unity + Flow)
            Add(new TrailerShotDefinition
            {
                id = "T05",
                trackTitle = "T05_Exterior_Portal_Entrada",
                duration = 6.0,
                category = TrailerShotCategory.ArchitecturalExterior,
                camPosStart = new Vector3(14.0f, 1.85f, -26.5f),
                camPosEnd = new Vector3(8.5f, 1.75f, -19.2f),
                lookTargetStart = new Vector3(7.68f, 2.15f, -13.95f),
                lookTargetEnd = new Vector3(7.68f, 2.05f, -13.95f),
                fovStart = 42f,
                fovEnd = 40f,
                nearClip = 0.12f,
                isIndoorFog = false,
                showJeid = false,
                openMainPortal = false,
                interiorAccentId = 0
            });

            // T06 (0:29–0:35): Respeta regla de la demo — Jeid 3ª persona YA ATRAPADA ADENTRO en Vestíbulo PB frente al portal cerrado mirando la noche afuera, y da media vuelta hacia el pasillo (Layer 28 ACTIVA, altura >= cuerpo, sin mirar debajo del cabello)
            Add(new TrailerShotDefinition
            {
                id = "T06",
                trackTitle = "T06_Vestibulo_Atrapada_3aPersona",
                duration = 6.0,
                category = TrailerShotCategory.ThirdPersonJeid,
                camPosStart = Plan(23.20f, 1.66f, 17.65f),
                camPosEnd = Plan(21.40f, 1.66f, 18.10f),
                lookTargetStart = Plan(27.00f, 1.24f, 19.25f),
                lookTargetEnd = Plan(24.30f, 1.24f, 19.30f),
                fovStart = 50f,
                fovEnd = 52f,
                nearClip = 0.08f,
                isIndoorFog = true,
                customIndoorFogDensity = 0.020f,
                showJeid = true,
                isThirdPerson = true,
                includeLayer28 = true,
                jeidWalkSpeed = 0.54f,
                jeidPosStart = Plan(27.05f, 0.20f, 19.30f),
                jeidPosEnd = Plan(24.00f, 0.20f, 19.30f),
                jeidYawStart = 176f,
                jeidYawEnd = 4f,
                flashlightMode = TrailerFlashlightMode.On,
                openMainPortal = false,
                interiorAccentId = 0
            });

            // T07 (0:35–0:41): Interior Vestíbulo PB — 1ª persona avanzando con linterna (Layer 28 OCULTA)
            Add(new TrailerShotDefinition
            {
                id = "T07",
                trackTitle = "T07_Interior_Vestibulo_1aPersona",
                duration = 6.0,
                category = TrailerShotCategory.FirstPersonJeid,
                camPosStart = Plan(25.20f, 1.68f, 19.30f),
                camPosEnd = Plan(19.60f, 1.68f, 19.30f),
                lookTargetStart = Plan(18.00f, 1.56f, 19.85f),
                lookTargetEnd = Plan(13.50f, 1.55f, 18.45f),
                fovStart = 60f,
                fovEnd = 60f,
                nearClip = 0.04f,
                isIndoorFog = true,
                customIndoorFogDensity = 0.025f,
                handheldPosAmp = 0.010f,
                handheldRotAmp = 0.45f,
                showJeid = true,
                isThirdPerson = false,
                includeLayer28 = false,
                jeidWalkSpeed = 0.48f,
                jeidPosStart = Plan(25.20f, 0.20f, 19.30f),
                jeidPosEnd = Plan(19.60f, 0.20f, 19.30f),
                jeidYawStart = 354f,
                jeidYawEnd = 8f,
                flashlightMode = TrailerFlashlightMode.On,
                openMainPortal = true
            });

            // T08 (0:41–0:47): Interior Pasillo Central PB arquitectónico (Unity + Flow, sin personajes)
            Add(new TrailerShotDefinition
            {
                id = "T08",
                trackTitle = "T08_Interior_Pasillo_Central_PB",
                duration = 6.0,
                category = TrailerShotCategory.ArchitecturalInterior,
                camPosStart = Plan(14.24f, 1.68f, 14.50f),
                camPosEnd = Plan(14.24f, 1.68f, 19.80f),
                lookTargetStart = Plan(14.24f, 1.48f, 38.00f),
                lookTargetEnd = Plan(14.24f, 1.48f, 38.00f),
                fovStart = 56f,
                fovEnd = 56f,
                nearClip = 0.08f,
                isIndoorFog = true,
                customIndoorFogDensity = 0.022f,
                showJeid = false,
                interiorAccentId = 1
            });

            // T09 (0:47–0:53): Entrada 1ª persona al Baño de Mujeres PB pasando por el centro exacto de P13_Bano_Mujeres
            Add(new TrailerShotDefinition
            {
                id = "T09",
                trackTitle = "T09_Interior_Bano_Mujeres_Entrada",
                duration = 6.0,
                category = TrailerShotCategory.FirstPersonJeid,
                camPosStart = new Vector3(20.01f, 1.68f, 0.55f),
                useCamMid = true,
                camPosMid = new Vector3(20.01f, 1.68f, 3.05f),
                camPosEnd = new Vector3(21.75f, 1.68f, 2.35f),
                lookTargetStart = new Vector3(20.01f, 1.56f, 3.10f),
                useLookMid = true,
                lookTargetMid = new Vector3(21.20f, 1.52f, 2.80f),
                lookTargetEnd = new Vector3(21.79f, 1.68f, 0.13f),
                fovStart = 60f,
                fovEnd = 58f,
                nearClip = 0.04f,
                isIndoorFog = true,
                customIndoorFogDensity = 0.020f,
                handheldPosAmp = 0.008f,
                handheldRotAmp = 0.35f,
                showJeid = true,
                isThirdPerson = false,
                includeLayer28 = false,
                jeidWalkSpeed = 0.45f,
                jeidPosStart = new Vector3(20.01f, 0.20f, 0.55f),
                jeidPosEnd = new Vector3(21.75f, 0.20f, 2.35f),
                jeidYawStart = 0f,
                jeidYawEnd = 175f,
                flashlightMode = TrailerFlashlightMode.On,
                openBanoMujeresDoor = true,
                interiorAccentId = 2
            });

            // T10 (0:53–0:59): Baño de Mujeres PB — Espejo real con Jeid + aparición 0.4s de La Testigo detrás + giro vacío (Ruta A1)
            Add(new TrailerShotDefinition
            {
                id = "T10",
                trackTitle = "T10_Interior_Bano_Espejo_Testigo",
                duration = 6.0,
                category = TrailerShotCategory.FirstPersonJeid,
                camPosStart = new Vector3(21.79f, 1.68f, 1.32f),
                camPosEnd = new Vector3(21.79f, 1.68f, 1.36f),
                lookTargetStart = new Vector3(21.79f, 1.66f, 0.13f),
                lookTargetEnd = new Vector3(21.65f, 1.62f, 3.60f),
                fovStart = 56f,
                fovEnd = 58f,
                nearClip = 0.04f,
                isIndoorFog = true,
                customIndoorFogDensity = 0.018f,
                handheldPosAmp = 0.004f,
                handheldRotAmp = 0.30f,
                showJeid = true,
                isThirdPerson = false,
                includeLayer28 = false,
                jeidWalkSpeed = 0f,
                jeidPosStart = new Vector3(21.79f, 0.20f, 1.32f),
                jeidPosEnd = new Vector3(21.79f, 0.20f, 1.36f),
                jeidYawStart = 180f,
                jeidYawEnd = 355f,
                flashlightMode = TrailerFlashlightMode.FlickerDip,
                showTestigo = true,
                testigoPosPrimary = new Vector3(22.15f, 0.20f, 3.15f),
                testigoYaw = 180f,
                testigoAppearWindowStart = 2.20f,
                testigoAppearWindowEnd = 3.05f,
                blackoutStart = 2.15f,
                blackoutEnd = 2.35f,
                openBanoMujeresDoor = true,
                interiorAccentId = 2
            });

            // T11 (0:59–1:05): Pasillo PB — Jeid 3ª persona caminando con celular (Layer 28 ACTIVA, altura >= ojos, sin mirar bajo el cabello)
            Add(new TrailerShotDefinition
            {
                id = "T11",
                trackTitle = "T11_Interior_Jeid_3aPersona_Pasillo",
                duration = 6.0,
                category = TrailerShotCategory.ThirdPersonJeid,
                camPosStart = new Vector3(19.65f, 1.74f, -0.95f),
                camPosEnd = new Vector3(14.55f, 1.72f, -0.82f),
                lookTargetStart = new Vector3(16.60f, 1.28f, 0.00f),
                lookTargetEnd = new Vector3(11.30f, 1.28f, 0.00f),
                fovStart = 50f,
                fovEnd = 50f,
                nearClip = 0.08f,
                isIndoorFog = true,
                customIndoorFogDensity = 0.022f,
                showJeid = true,
                isThirdPerson = true,
                includeLayer28 = true,
                jeidWalkSpeed = 0.52f,
                jeidPosStart = new Vector3(17.50f, 0.20f, 0.00f),
                jeidPosEnd = new Vector3(12.20f, 0.20f, 0.00f),
                jeidYawStart = 270f,
                jeidYawEnd = 270f,
                flashlightMode = TrailerFlashlightMode.On,
                openBanoMujeresDoor = true,
                interiorAccentId = 1
            });

            // T12 (1:05–1:11): Escalera en U PB — SOLO Tramo 1 hasta descanso intermedio Y=1.90 m (NUNCA PA) (Unity + Flow)
            Add(new TrailerShotDefinition
            {
                id = "T12",
                trackTitle = "T12_Escalera_PB_Hasta_Descanso",
                duration = 6.0,
                category = TrailerShotCategory.ArchitecturalInterior,
                camPosStart = new Vector3(8.78f, 1.48f, 4.95f),
                camPosEnd = new Vector3(8.78f, 2.38f, 7.65f),
                lookTargetStart = new Vector3(7.90f, 1.98f, 10.65f),
                lookTargetEnd = new Vector3(7.70f, 2.08f, 10.95f),
                fovStart = 54f,
                fovEnd = 54f,
                nearClip = 0.08f,
                isIndoorFog = true,
                customIndoorFogDensity = 0.022f,
                showJeid = false,
                interiorAccentId = 3
            });

            // T13 (1:11–1:17): Aula 3 PB — pupitres originales F5_Pupitre y pizarrón en penumbra (Unity + Flow)
            Add(new TrailerShotDefinition
            {
                id = "T13",
                trackTitle = "T13_Aula3_PB_Pupitres",
                duration = 6.0,
                category = TrailerShotCategory.ArchitecturalInterior,
                camPosStart = Plan(10.60f, 1.55f, 47.20f),
                camPosEnd = Plan(13.60f, 1.55f, 47.60f),
                lookTargetStart = Plan(17.40f, 0.90f, 52.40f),
                lookTargetEnd = Plan(17.80f, 0.90f, 52.80f),
                fovStart = 56f,
                fovEnd = 56f,
                nearClip = 0.08f,
                isIndoorFog = true,
                customIndoorFogDensity = 0.020f,
                showJeid = false,
                interiorAccentId = 4
            });

            // T14 (1:17–1:23): Auditorio PB — 80 butacas vacías hacia escenario (Unity + Flow)
            Add(new TrailerShotDefinition
            {
                id = "T14",
                trackTitle = "T14_Auditorio_PB_Butacas",
                duration = 6.0,
                category = TrailerShotCategory.ArchitecturalInterior,
                camPosStart = Plan(18.40f, 2.20f, 25.40f),
                camPosEnd = Plan(20.20f, 1.72f, 28.60f),
                lookTargetStart = Plan(24.60f, 0.45f, 36.80f),
                lookTargetEnd = Plan(24.80f, 0.55f, 36.80f),
                fovStart = 60f,
                fovEnd = 58f,
                nearClip = 0.08f,
                isIndoorFog = true,
                customIndoorFogDensity = 0.018f,
                showJeid = false,
                interiorAccentId = 5
            });

            // T15 (1:23–1:29): Plaza Sur PB — bancas y fallo progresivo de luces (Unity + Flow)
            Add(new TrailerShotDefinition
            {
                id = "T15",
                trackTitle = "T15_PlazaSur_PB_FalloLuces",
                duration = 6.0,
                category = TrailerShotCategory.ArchitecturalInterior,
                camPosStart = Plan(14.24f, 1.55f, 35.60f),
                camPosEnd = Plan(14.24f, 1.55f, 40.60f),
                lookTargetStart = Plan(14.24f, 1.35f, 46.20f),
                lookTargetEnd = Plan(14.24f, 1.35f, 46.50f),
                fovStart = 60f,
                fovEnd = 60f,
                nearClip = 0.08f,
                isIndoorFog = true,
                customIndoorFogDensity = 0.022f,
                showJeid = false,
                interiorAccentId = 6
            });

            // T16 (1:29–1:35): Pasillo Central PB — silueta lejana inmóvil de La Testigo (Ruta A1, 2.30 m)
            Add(new TrailerShotDefinition
            {
                id = "T16",
                trackTitle = "T16_Pasillo_Silueta_LaTestigo_Lejos",
                duration = 6.0,
                category = TrailerShotCategory.ArchitecturalInterior,
                camPosStart = Plan(14.24f, 1.65f, 15.00f),
                camPosEnd = Plan(14.24f, 1.65f, 16.40f),
                lookTargetStart = Plan(14.24f, 1.45f, 26.80f),
                lookTargetEnd = Plan(14.24f, 1.45f, 26.80f),
                fovStart = 52f,
                fovEnd = 50f,
                nearClip = 0.08f,
                isIndoorFog = true,
                customIndoorFogDensity = 0.024f,
                showJeid = false,
                showTestigo = true,
                testigoPosPrimary = Plan(14.24f, 0.20f, 26.80f),
                testigoYaw = 90f,
                interiorAccentId = 7
            });

            // T17 (1:35–1:40): 1ª persona con linterna + apagón 0.35s: La Testigo avanza 3 m sin caminar (Ruta A1)
            Add(new TrailerShotDefinition
            {
                id = "T17",
                trackTitle = "T17_Climax_Apagon_Avance_3m",
                duration = 5.0,
                category = TrailerShotCategory.FirstPersonJeid,
                camPosStart = Plan(14.24f, 1.68f, 17.80f),
                camPosEnd = Plan(14.24f, 1.68f, 18.80f),
                lookTargetStart = Plan(14.24f, 1.55f, 25.60f),
                lookTargetEnd = Plan(14.24f, 1.62f, 22.60f),
                fovStart = 54f,
                fovEnd = 50f,
                nearClip = 0.04f,
                isIndoorFog = true,
                customIndoorFogDensity = 0.024f,
                handheldPosAmp = 0.008f,
                handheldRotAmp = 0.40f,
                showJeid = true,
                isThirdPerson = false,
                includeLayer28 = false,
                jeidPosStart = Plan(14.24f, 0.20f, 17.80f),
                jeidPosEnd = Plan(14.24f, 0.20f, 18.80f),
                jeidYawStart = 270f,
                jeidYawEnd = 270f,
                flashlightMode = TrailerFlashlightMode.FlickerDip,
                showTestigo = true,
                testigoHasBlackoutStep = true,
                testigoPosPrimary = Plan(14.24f, 0.20f, 25.60f),
                testigoPosAfterBlackout = Plan(14.24f, 0.20f, 22.60f),
                testigoYaw = 90f,
                blackoutStart = 2.10f,
                blackoutEnd = 2.45f,
                interiorAccentId = 7
            });

            // T18 (1:40–1:45): Contrapicado 1ª persona a 3.0 m de La Testigo (altura ojos 1.48 -> mirando hacia 2.15 m de La Testigo 2.30 m)
            Add(new TrailerShotDefinition
            {
                id = "T18",
                trackTitle = "T18_Climax_FrenteAFrente_3m",
                duration = 5.0,
                category = TrailerShotCategory.FirstPersonJeid,
                camPosStart = Plan(14.45f, 1.68f, 19.60f),
                camPosEnd = Plan(14.40f, 1.68f, 20.10f),
                lookTargetStart = Plan(14.24f, 1.55f, 22.60f),
                lookTargetEnd = Plan(14.24f, 2.08f, 22.60f),
                fovStart = 46f,
                fovEnd = 44f,
                nearClip = 0.04f,
                useDof = true,
                dofFocusDistance = 2.70f,
                dofAperture = 3.2f,
                isIndoorFog = true,
                customIndoorFogDensity = 0.022f,
                handheldPosAmp = 0.005f,
                handheldRotAmp = 0.30f,
                showJeid = true,
                isThirdPerson = false,
                includeLayer28 = false,
                jeidPosStart = Plan(14.45f, 0.20f, 19.60f),
                jeidPosEnd = Plan(14.40f, 0.20f, 20.10f),
                jeidYawStart = 266f,
                jeidYawEnd = 267f,
                flashlightMode = TrailerFlashlightMode.On,
                showTestigo = true,
                testigoPosPrimary = Plan(14.24f, 0.20f, 22.60f),
                testigoYaw = 90f,
                interiorAccentId = 7
            });

            // T19 (1:45–1:50): Primer plano La Testigo + fallo y apagado total de linterna a negro
            Add(new TrailerShotDefinition
            {
                id = "T19",
                trackTitle = "T19_Climax_LinternaMuere_Negro",
                duration = 5.0,
                category = TrailerShotCategory.FirstPersonJeid,
                camPosStart = Plan(14.42f, 1.70f, 20.55f),
                camPosEnd = Plan(14.36f, 1.72f, 20.95f),
                lookTargetStart = Plan(14.24f, 2.08f, 22.60f),
                lookTargetEnd = Plan(14.24f, 2.15f, 22.60f),
                fovStart = 42f,
                fovEnd = 38f,
                nearClip = 0.04f,
                useDof = true,
                dofFocusDistance = 1.85f,
                dofAperture = 2.8f,
                isIndoorFog = true,
                customIndoorFogDensity = 0.022f,
                handheldPosAmp = 0.006f,
                handheldRotAmp = 0.45f,
                showJeid = true,
                isThirdPerson = false,
                includeLayer28 = false,
                jeidPosStart = Plan(14.42f, 0.20f, 20.55f),
                jeidPosEnd = Plan(14.36f, 0.20f, 20.95f),
                jeidYawStart = 268f,
                jeidYawEnd = 268f,
                flashlightMode = TrailerFlashlightMode.FlickerAndDie,
                showTestigo = true,
                testigoPosPrimary = Plan(14.24f, 0.20f, 22.60f),
                testigoYaw = 90f,
                interiorAccentId = 7
            });

            // T20 (1:50–2:00): Tarjeta limpia VAELITH / After You Wake / PRÓXIMAMENTE sobre negro puro
            Add(new TrailerShotDefinition
            {
                id = "T20",
                trackTitle = "T20_Titulo_VAELITH",
                duration = 10.0,
                category = TrailerShotCategory.BlackTitleCard,
                camPosStart = new Vector3(0f, -60f, 0f),
                camPosEnd = new Vector3(0f, -60f, 0f),
                lookTargetStart = new Vector3(0f, -60f, 10f),
                lookTargetEnd = new Vector3(0f, -60f, 10f),
                fovStart = 45f,
                fovEnd = 45f,
                nearClip = 0.1f,
                farClip = 25f,
                isIndoorFog = false,
                showJeid = false,
                interiorAccentId = 0
            });

            return list;
        }
    }
}
