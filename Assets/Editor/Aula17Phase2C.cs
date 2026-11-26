using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Vaelith.Docencia2;
using Object = UnityEngine.Object;

public static class Aula17Phase2C
{
    const string ScenePath = "Assets/Scenes/Exterior_Docencia2.unity", Out = "Aula17_Fase2c";
    const string FixedName = "Detalles_Fijos_Aula17_Fase2c_SUPUESTO", MovingName = "Detalles_Moviles_Aula17_Fase2c_SUPUESTO";
    static Transform aula, hinge, fixedRoot, movingRoot;
    static Docencia2InteractiveDoor door;
    static Aula17DecorativeCloser closer;
    static bool correctingSwing;
    static Vector3 W(float x,float y,float z) => Docencia2InteriorBuilder.PlanToUnityPos(x,y,z);
    static string PathOf(Transform t) => t.parent ? PathOf(t.parent)+"/"+t.name : t.name;
    static Material Mat(string p) => AssetDatabase.LoadAssetAtPath<Material>(p);
    static Transform Group(string n,Transform p) { var g=new GameObject(n);g.layer=30;g.transform.SetParent(p,false);return g.transform; }
    static bool New(Transform t) => (fixedRoot && (t==fixedRoot||t.IsChildOf(fixedRoot))) || (movingRoot && (t==movingRoot||t.IsChildOf(movingRoot)));
    static Dictionary<string,string> Snapshot()
    {
        var result=new Dictionary<string,string>();
        foreach(var g in aula.gameObject.scene.GetRootGameObjects()) foreach(var t in g.GetComponentsInChildren<Transform>(true))
        {
            if(New(t))continue;
            foreach(var obj in new Object[]{t.gameObject}.Concat(t.GetComponents<Component>().Cast<Object>()))
            {
                if(!obj)continue;
                if(correctingSwing && (obj==door || obj==hinge.GetComponent<BoxCollider>() || obj==hinge.Find("Hoja") || obj==hinge.Find("Manija") || obj==hinge.Find("Hoja").GetComponent<Docencia2PADoor>()))continue;
                // Only child links may change on these two existing transforms; all their TRS are asserted separately.
                if(obj==aula||obj==hinge)continue;
                result[GlobalObjectId.GetGlobalObjectIdSlow(obj).ToString()]=EditorJsonUtility.ToJson(obj);
            }
        }
        return result;
    }
    static void Check(Dictionary<string,string> before)
    {
        var now=Snapshot();var changed=before.Keys.Where(k=>!now.ContainsKey(k)||now[k]!=before[k]).Concat(now.Keys.Except(before.Keys)).ToArray();
        File.WriteAllText(Out+"/auditoria_ambito.txt",$"Objetos/componentes protegidos: {before.Count}; cambios fuera de detalles nuevos: {changed.Length}\n"+string.Join("\n",changed));
        if(changed.Length>0)throw new Exception("Cambios fuera de ambito: no guardar");
    }
    static GameObject Box(string n,Transform parent,float x0,float x1,float y0,float y1,float z0,float z1,Material material,bool isStatic=false)
    {
        if(!material)throw new Exception("Material ausente: "+n);
        var g=GameObject.CreatePrimitive(PrimitiveType.Cube);g.name=n;Object.DestroyImmediate(g.GetComponent<Collider>());
        var a=W(x0,y0,z0);var b=W(x1,y1,z1);g.transform.position=(a+b)*.5f;g.transform.localScale=new Vector3(Mathf.Abs(b.x-a.x),y1-y0,Mathf.Abs(b.z-a.z));g.transform.SetParent(parent,true);
        g.layer=30;g.isStatic=isStatic;var r=g.GetComponent<Renderer>();r.sharedMaterial=material;r.shadowCastingMode=ShadowCastingMode.Off;r.renderingLayerMask=17;return g;
    }
    static void OpenScene()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode)throw new Exception("Construccion requiere fuera de Play");
        Directory.CreateDirectory(Out);EditorSceneManager.OpenScene(ScenePath);
        aula=GameObject.Find("PA_Interior/Aula_17")?.transform;if(!aula)throw new Exception("Aula17 ausente");
        var doors=aula.GetComponentsInChildren<Docencia2InteractiveDoor>(true);if(doors.Length!=1)throw new Exception("Puerta no univoca");
        door=doors[0];hinge=door.transform;
        fixedRoot=aula.Find(FixedName);movingRoot=hinge.Find(MovingName);
    }
    static void VerifyHinge()
    {
        var leaf=hinge.Find("Hoja");var pa=leaf.GetComponent<Docencia2PADoor>();
        if(!pa||pa.hinge!=hinge||(!leaf.GetComponent<Collider>()&&!hinge.GetComponent<Collider>()))throw new Exception("Bisagra o collider no valido");
        var saved=EditorJsonUtility.ToJson(door);var rotation=hinge.localRotation;var pos=hinge.localPosition;var scale=hinge.localScale;
        var closed=door.closedRotation;var log=new List<string>{"# Verificación de bisagra previa al cierrapuertas","","Sin Play; simulación geométrica reversible con el controlador existente.","","- Ruta: `"+PathOf(hinge)+"`.","- Componentes: "+string.Join(", ",hinge.GetComponents<Component>().Select(c=>c.GetType().Name))+".","- Hoja: "+string.Join(", ",leaf.GetComponents<Component>().Select(c=>c.GetType().Name))+".","- Material aprobado: `"+AssetDatabase.GetAssetPath(leaf.GetComponent<Renderer>().sharedMaterial)+"`.","- Pivote Unity: "+hinge.position.ToString("F4")+".","- Ángulo configurado: "+door.inwardAngle+" grados.","- Dimensiones de hoja cerrada (escala local): "+leaf.localScale.ToString("F4")+".","- F14 parece tener la bisagra opuesta; se conserva la aprobada al oeste. Adaptación del herraje: SUPUESTO."};
        try
        {
            door.SetOpen(false);var start=leaf.GetComponent<Renderer>().bounds.center;door.SetOpen(true);
            var end=leaf.GetComponent<Renderer>().bounds.center;
            float angle=Quaternion.Angle(closed,hinge.localRotation);
            if(Mathf.Abs(angle-90)>.05f||end.x>=start.x)throw new Exception("No se confirmó giro 90 hacia interior sur");
            log.Add("- Prueba: giro 90° hacia sur/interior confirmado; centro cerrado "+start.ToString("F4")+", abierto "+end.ToString("F4")+".");
            log.Add("- Posición y escala del pivote constantes. No se cambió lado, hoja, marco, material ni script.");
        }
        finally{EditorJsonUtility.FromJsonOverwrite(saved,door);hinge.localRotation=rotation;hinge.localPosition=pos;hinge.localScale=scale;Physics.SyncTransforms();}
        File.WriteAllLines(Out+"/Verificacion_Bisagra.md",log);
    }
    static void InspectWindows()
    {
        var log=new List<string>{"# Ventanas oeste — inspección sin modificación","","Se conservan los marcos, vidrios y geometría aprobados. Sin nuevas cortinas ni rejas.","Las mallas de fachada pueden estar combinadas: el número de renderers no equivale a paneles.","","| Objeto | Centro Unity | Tamaño bounds | Materiales |","|---|---|---|---|"};
        foreach(var g in aula.gameObject.scene.GetRootGameObjects()) foreach(var r in g.GetComponentsInChildren<MeshRenderer>(true))
        {
            var b=r.bounds;var c=b.center;float x=(14.25f-c.z)*28.48f/28.50f,z=(27-c.x)*53.92f/54;
            if(c.y>5.3f&&c.y<7.0f && x<.5f&&x>-.6f && z>22.5f&&z<31.5f)
                log.Add("| `"+PathOf(r.transform)+"` | "+c.ToString("F3")+" | "+b.size.ToString("F3")+" | "+string.Join(", ",r.sharedMaterials.Where(m=>m).Select(m=>m.name))+" |");
        }
        File.WriteAllLines(Out+"/Ventanas_Conservadas.md",log);
    }
    static void Build()
    {
        if(fixedRoot||movingRoot)throw new Exception("2c ya existe; no duplicar");
        var paper=Mat("Assets/Materials/Docencia2/Fase5/F5_Papel.mat");
        var metal=Mat("Assets/Models/Docencia2/Aula17_Fase2b/A17_Marco_Metalico_Claro.mat");
        var dark=Mat("Assets/Materials/Docencia2/Fase5/F5_Negro.mat");
        fixedRoot=Group(FixedName,aula);movingRoot=Group(MovingName,hinge);
        Box("Papel_Superior_F14_Sin_Texto",movingRoot,10.08f,10.37f,4.91f,5.15f,23.159f,23.161f,paper);
        Box("Papel_Inferior_F14_Sin_Texto",movingRoot,10.23f,10.37f,4.64f,4.93f,23.1615f,23.1635f,paper);
        Box("Cuerpo_Cierrapuertas_SUPUESTO",movingRoot,9.94f,10.16f,5.59f,5.645f,23.16f,23.225f,metal);
        Box("Eje_Superior",movingRoot,10.035f,10.055f,5.64f,5.775f,23.23f,23.25f,dark);
        // Small offset bracket connects the vertical spindle to the body, clear of the original lintel.
        Box("Soporte_Eje",movingRoot,10.02f,10.07f,5.625f,5.645f,23.21f,23.25f,metal);
        var mp=Group("Pivote_Movil",movingRoot);mp.position=W(10.045f,5.775f,23.24f);
        Box("Placa_Anclaje_Muro",fixedRoot,9.64f,9.74f,5.745f,5.805f,23.215f,23.23f,metal,true);
        var fp=Group("Pivote_Fijo",fixedRoot);fp.position=W(9.69f,5.775f,23.24f);
        var a=Box("Brazo_1",fixedRoot,0,.014f,0,.012f,0,.24f,dark);
        var b=Box("Brazo_2",fixedRoot,0,.014f,0,.012f,0,.24f,dark);
        var e=Box("Articulacion",fixedRoot,0,.025f,0,.018f,0,.025f,metal);
        closer=fixedRoot.gameObject.AddComponent<Aula17DecorativeCloser>();closer.movingPin=mp;closer.fixedPin=fp;closer.armA=a.transform;closer.armB=b.transform;closer.elbow=e.transform;closer.RefreshVisual();
    }
    static void VerifyLinkage()
    {
        var saved=EditorJsonUtility.ToJson(door);var rotation=hinge.localRotation;
        var lines=new List<string>{"angulo,distancia_anclajes_m,longitud_brazo1_m,longitud_brazo2_m"};
        try
        {
            for(int angle=0;angle<=90;angle+=5)
            {
                hinge.localRotation=door.closedRotation*Quaternion.Euler(0,angle*Mathf.Sign(door.inwardAngle),0);closer.RefreshVisual();
                float d=Vector3.Distance(closer.movingPin.position,closer.fixedPin.position);
                if(d>closer.armLength*2+.001f)throw new Exception("Cierrapuertas fuera de alcance");
                if(Mathf.Abs(closer.armA.localScale.z-.24f)>.001f||Mathf.Abs(closer.armB.localScale.z-.24f)>.001f)throw new Exception("Longitud de brazo inconsistente");
                lines.Add(FormattableString.Invariant($"{angle},{d:F4},{closer.armA.localScale.z:F4},{closer.armB.localScale.z:F4}"));
            }
        }
        finally{EditorJsonUtility.FromJsonOverwrite(saved,door);hinge.localRotation=rotation;closer.RefreshVisual();Physics.SyncTransforms();}
        File.WriteAllLines(Out+"/barrido_cierrapuertas.csv",lines);
    }
    public static void Run()
    {
        try
        {
            OpenScene();var before=Snapshot();var aulaTRS=(aula.localPosition,aula.localRotation,aula.localScale);var hingeTRS=(hinge.localPosition,hinge.localRotation,hinge.localScale);
            VerifyHinge();InspectWindows();Build();VerifyLinkage();Check(before);
            if(aulaTRS!=(aula.localPosition,aula.localRotation,aula.localScale)||hingeTRS!=(hinge.localPosition,hinge.localRotation,hinge.localScale))throw new Exception("TRS protegido alterado");
            AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(aula.gameObject.scene);EditorSceneManager.SaveScene(aula.gameObject.scene);
            Capture();Check(before);
            int objects=fixedRoot.GetComponentsInChildren<Transform>(true).Length+movingRoot.GetComponentsInChildren<Transform>(true).Length;
            int renderers=fixedRoot.GetComponentsInChildren<Renderer>(true).Length+movingRoot.GetComponentsInChildren<Renderer>(true).Length;
            File.WriteAllText(Out+"/completado.txt",$"Fase 2c guardada fuera de Play. {objects} GameObjects nuevos (incluye grupos), {renderers} renderers. Sin colliders nuevos. Sin cortinas ni rejas. Detención antes de 2d.\n"+DateTime.Now.ToString("O"));
            EditorApplication.Exit(0);
        }
        catch(Exception e){Directory.CreateDirectory(Out);File.WriteAllText(Out+"/error.txt",e.ToString());Debug.LogException(e);EditorApplication.Exit(1);}
    }
    public static void CaptureSaved()
    {
        try{OpenScene();closer=fixedRoot.GetComponent<Aula17DecorativeCloser>();var before=Snapshot();Capture();Check(before);EditorApplication.Exit(0);}catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}
    }
    // Explicit user correction: hinge on the right viewed from inside, still opens inward.
    public static void CorrectRightInward()
    {
        try
        {
            OpenScene();correctingSwing=true;
            if(door.isOpen||Quaternion.Angle(hinge.localRotation,door.closedRotation)>.01f)throw new Exception("Se requiere puerta cerrada para corregir sin perder su estado");
            if(door.inwardAngle<0)throw new Exception("Ya tiene giro derecho; no repetir");
            closer=fixedRoot.GetComponent<Aula17DecorativeCloser>();var before=Snapshot();
            var leaf=hinge.Find("Hoja");var handle=hinge.Find("Manija");var collider=hinge.GetComponent<BoxCollider>();var pa=leaf.GetComponent<Docencia2PADoor>();
            var leafBounds=leaf.GetComponent<Renderer>().bounds;var material=leaf.GetComponent<Renderer>().sharedMaterial;var mesh=leaf.GetComponent<MeshFilter>().sharedMesh;
            var colliderWorld=hinge.TransformPoint(collider.center);var oldHinge=hinge.position;
            var positions=hinge.Cast<Transform>().ToDictionary(t=>t,t=>t.position);
            float middleZ=leafBounds.center.z;
            Vector3 Mirror(Vector3 p)=>new Vector3(p.x,p.y,2*middleZ-p.z);
            // Axis on the room-facing edge of the leaf, so its thickness clears the unchanged jamb.
            hinge.position=Mirror(oldHinge)+Vector3.left*.022f;
            foreach(var kv in positions)kv.Key.position=kv.Value;
            collider.center=hinge.InverseTransformPoint(colliderWorld);
            handle.position=Mirror(handle.position);
            // Keep papers in their photographed arrangement; mirror only hardware.
            foreach(var t in movingRoot.Cast<Transform>())if(!t.name.StartsWith("Papel_"))t.position=Mirror(t.position);
            foreach(var t in fixedRoot.Cast<Transform>())t.position=Mirror(t.position);
            door.inwardAngle=-90;pa.inwardAngle=-90;closer.bendSign=-1;closer.RefreshVisual();
            if(leaf.GetComponent<Renderer>().bounds!=leafBounds || leaf.GetComponent<Renderer>().sharedMaterial!=material || leaf.GetComponent<MeshFilter>().sharedMesh!=mesh)throw new Exception("Hoja cerrada/material/malla alterados");
            VerifyLinkage();
            var saved=EditorJsonUtility.ToJson(door);var rotation=hinge.localRotation;var collisions=new HashSet<string>();
            try
            {
                door.SetOpen(true);var opened=leaf.GetComponent<Renderer>().bounds.center;
                if(opened.x>=leafBounds.center.x||Mathf.Abs(opened.z-hinge.position.z)>.03f)throw new Exception("Giro no interior o borde incorrecto");
                // Sample the existing door collider against solid architectural colliders of this room.
                var obstacles=aula.GetComponentsInChildren<Collider>(true).Where(c=>c!=collider&&!c.transform.IsChildOf(hinge)&&c.enabled&&!c.isTrigger).ToArray();
                for(int angle=0;angle<=90;angle+=5)
                {
                    hinge.localRotation=door.closedRotation*Quaternion.Euler(0,-angle,0);closer.RefreshVisual();Physics.SyncTransforms();
                    foreach(var other in obstacles)if(Physics.ComputePenetration(collider,collider.transform.position,collider.transform.rotation,other,other.transform.position,other.transform.rotation,out var dir,out float depth)&&depth>.005f)collisions.Add(other.name+" @ "+angle+" profundidad "+depth.ToString("F4"));
                }
                if(collisions.Count>0)throw new Exception("Colision puerta/muro: "+string.Join("; ",collisions));
                File.WriteAllText(Out+"/Correccion_Giro_Derecha.md","# Corrección de giro — 23 de noviembre de 2026\n\nConfirmación del usuario: abre hacia dentro del aula, desde el otro lado.\n\n- Bisagra trasladada al borde este/derecho visto desde dentro.\n- Giro configurado en ambos componentes: -90°, hacia sur/interior.\n- Manija trasladada al borde izquierdo. Cierrapuertas reflejado y articulación adaptada.\n- Hoja cerrada: misma posición, tamaño, malla y material. Papeles conservados en la misma disposición.\n- Pivote anterior Unity: "+oldHinge.ToString("F4")+"; nuevo: "+hinge.position.ToString("F4")+".\n- Centro abierto: "+opened.ToString("F4")+".\n- Barrido 0–90 cada 5°: cero penetraciones mayores a 5 mm con colliders arquitectónicos locales.\n- Prueba geométrica fuera de Play; prueba de personaje y FPS sigue pendiente de Fase 3.\n");
            }
            finally{EditorJsonUtility.FromJsonOverwrite(saved,door);hinge.localRotation=rotation;closer.RefreshVisual();Physics.SyncTransforms();}
            Check(before);EditorSceneManager.MarkSceneDirty(aula.gameObject.scene);EditorSceneManager.SaveScene(aula.gameObject.scene);Capture();Check(before);
            File.WriteAllText(Out+"/correccion_giro_completada.txt","Bisagra derecha, apertura interior, escena guardada cerrada. "+DateTime.Now.ToString("O"));EditorApplication.Exit(0);
        }
        catch(Exception e){Directory.CreateDirectory(Out);File.WriteAllText(Out+"/error_giro.txt",e.ToString());Debug.LogException(e);EditorApplication.Exit(1);}
    }
    static void Capture()
    {
        var tmp=new GameObject("TEMP_A17_2c_Camara");var c=tmp.AddComponent<Camera>();c.enabled=false;c.nearClipPlane=.025f;c.farClipPlane=60;c.clearFlags=CameraClearFlags.SolidColor;c.backgroundColor=Color.gray;var data=tmp.AddComponent<UniversalAdditionalCameraData>();data.renderPostProcessing=false;data.renderShadows=false;
        var temps=new List<GameObject>{tmp};var oldMode=RenderSettings.ambientMode;var oldAmbient=RenderSettings.ambientLight;var oldSky=RenderSettings.ambientSkyColor;var oldEquator=RenderSettings.ambientEquatorColor;var oldGround=RenderSettings.ambientGroundColor;bool oldFog=RenderSettings.fog;var oldSun=RenderSettings.sun;
        var kg=new GameObject("TEMP_A17_2c_Luz");temps.Add(kg);var key=kg.AddComponent<Light>();key.type=LightType.Directional;key.intensity=.85f;key.shadows=LightShadows.None;
        RenderSettings.sun=key;RenderSettings.fog=false;RenderSettings.ambientMode=AmbientMode.Flat;RenderSettings.ambientLight=new Color(.3f,.3f,.3f);
        var rt=new RenderTexture(1600,1100,24);rt.antiAliasing=2;var prev=RenderTexture.active;c.targetTexture=rt;
        var saved=EditorJsonUtility.ToJson(door);var rot=hinge.localRotation;
        try
        {
            void Shot(string name,Vector3 from,Vector3 to,float fov=65)
            {
                c.fieldOfView=fov;c.transform.position=from;c.transform.LookAt(to);key.transform.rotation=c.transform.rotation;
                var tex=new Texture2D(1600,1100,TextureFormat.RGB24,false);
                for(int i=0;i<4;i++){c.Render();RenderTexture.active=rt;tex.ReadPixels(new Rect(0,0,1600,1100),0,0);tex.Apply();}
                File.WriteAllBytes(Out+"/"+name+".png",tex.EncodeToPNG());Object.DestroyImmediate(tex);
            }
            door.SetOpen(false);closer.RefreshVisual();
            Shot("01_Puerta_Cerrada",W(9.95f,5.15f,25.5f),W(10.05f,4.85f,23.15f),62);
            Shot("02_Cierrapuertas_Detalle",W(door.inwardAngle<0?10.9f:9.35f,6.13f,24.15f),W(door.inwardAngle<0?10.49f:10.03f,5.73f,23.21f),50);
            Shot("03_Ventanas_Sin_Cortinas_Ni_Rejas",W(5.1f,5.70f,27),W(.25f,6.03f,27),85);
            door.SetOpen(true);closer.RefreshVisual();
            Shot("04_Puerta_Abierta_90",W(8.8f,5.25f,25.6f),W(10.1f,4.9f,23.55f),65);
            Shot("05_Cierrapuertas_Abierto",W(door.inwardAngle<0?10.95f:9.25f,6.2f,24.6f),W(door.inwardAngle<0?10.67f:9.85f,5.73f,23.5f),55);
        }
        finally
        {
            EditorJsonUtility.FromJsonOverwrite(saved,door);hinge.localRotation=rot;closer.RefreshVisual();Physics.SyncTransforms();
            RenderSettings.sun=oldSun;RenderSettings.fog=oldFog;RenderSettings.ambientLight=oldAmbient;RenderSettings.ambientSkyColor=oldSky;RenderSettings.ambientEquatorColor=oldEquator;RenderSettings.ambientGroundColor=oldGround;RenderSettings.ambientMode=oldMode;
            c.targetTexture=null;RenderTexture.active=prev;rt.Release();Object.DestroyImmediate(rt);foreach(var g in temps)Object.DestroyImmediate(g);
        }
    }
}
