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

public static class Aula17LightsPhase2E
{
    const string ScenePath="Assets/Scenes/Exterior_Docencia2.unity", Out="Aula17_Fase2e", Dir="Assets/Models/Docencia2/Aula17_Fase2e", RootName="Luces_Aula17_Fase2e_SUPUESTO";
    const uint Mask=32;
    static Transform aula, root, oldRoot, night, terror;
    static Docencia2ModeSwitcher switcher;
    static Light[] reused;
    static GameObject[] obsolete;
    static HashSet<Object> allowed=new HashSet<Object>();
    static string P(Transform t)=>t.parent?P(t.parent)+"/"+t.name:t.name;
    static Vector3 W(float x,float y,float z)=>Docencia2InteriorBuilder.PlanToUnityPos(x,y,z);
    static IEnumerable<Transform> All()=>aula.gameObject.scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Transform>(true));
    static Transform Group(string name,Transform parent){var g=new GameObject(name);g.layer=30;g.transform.SetParent(parent,false);return g.transform;}
    static void Init()
    {
        Directory.CreateDirectory(Out);
        if(EditorApplication.isPlayingOrWillChangePlaymode)throw new Exception("Construir fuera de Play mode.");
        EditorSceneManager.OpenScene(ScenePath);
        aula=GameObject.Find("PA_Interior/Aula_17")?.transform;if(!aula)throw new Exception("Aula ausente");
        oldRoot=GameObject.Find("PA_Interior/Iluminacion_PA_Mismo_Criterio_PB").transform;
        root=aula.Find(RootName);
        switcher=Object.FindAnyObjectByType<Docencia2ModeSwitcher>();if(!switcher)throw new Exception("Selector ausente");
        reused=oldRoot.GetComponentsInChildren<Light>(true).Where(l=>l.name=="Luz_PA_Aula_17_35"||l.name=="Luz_PA_Aula_17_36").OrderBy(l=>l.name).ToArray();
        obsolete=oldRoot.GetComponentsInChildren<Transform>(true).Where(t=>t.name=="Marco_Aula_17"||t.name=="Difusor_Aula_17"||t.name=="Difusor_Terror_Aula_17").Select(t=>t.gameObject).ToArray();
        if(reused.Length!=2)throw new Exception("Se requieren las dos luces originales");
    }
    public static void Inspect()
    {
        try
        {
            Init();var lines=new List<string>{"# Inventario previo 2e — Aula 17","","Escena: "+ScenePath,"Modo guardado: "+switcher.modoActual,"","| Objeto | Componentes | Posición Unity | Luz |","|---|---|---|---|"};
            foreach(var g in obsolete.Concat(reused.Select(l=>l.gameObject)))
            {var l=g.GetComponent<Light>();lines.Add($"| {P(g.transform)} | {string.Join(", ",g.GetComponents<Component>().Select(c=>c.GetType().Name))} | {g.transform.position.ToString("F4")} | {(l?$"{l.type}; intensidad {l.intensity}; alcance {l.range}; sombras {l.shadows}; capa {l.renderingLayerMask}":"—")} |");}
            var rp=QualitySettings.renderPipeline as UniversalRenderPipelineAsset;
            lines.Add("\nURP: "+(rp?AssetDatabase.GetAssetPath(rp):"no detectado")+"; rendering layers: "+(rp&&rp.useRenderingLayers));
            var outside=All().Where(t=>!t.IsChildOf(aula)&&t!=aula).SelectMany(t=>t.GetComponents<Renderer>()).Where(r=>(r.renderingLayerMask&Mask)!=0).ToArray();
            lines.Add("Renderers externos que usan bit 32: "+outside.Length);
            var centers=aula.Find("Plafon_Aula17_Fase2a_SUPUESTO/Troffers_10_encendidos_1_tenue").GetComponentsInChildren<Renderer>(true).Where(r=>r.name=="Cuerpo").Select(r=>new Vector3(r.bounds.center.x,6.60f,r.bounds.center.z)).ToArray();
            foreach(var r in outside)lines.Add(P(r.transform)+"; distancia minima a troffers="+centers.Min(p=>Vector3.Distance(p,r.bounds.ClosestPoint(p))).ToString("F3"));
            File.WriteAllLines(Out+(root?"/Comprobacion_Alcances.md":"/Inventario_Antes.md"),lines);
            if(outside.Any(r=>centers.Any(p=>Vector3.Distance(p,r.bounds.ClosestPoint(p))<4.71f)))throw new Exception("Receptor ajeno dentro del alcance");
            if(!rp||!rp.useRenderingLayers)throw new Exception("Rendering layers no habilitadas");
            EditorApplication.Exit(0);
        }catch(Exception e){Fail(e);}
    }
    static void Permit(GameObject g){allowed.Add(g);foreach(var c in g.GetComponents<Component>())allowed.Add(c);}
    static Dictionary<string,string> Snapshot()
    {
        var d=new Dictionary<string,string>();
        foreach(var t in All())
        {
            if(root&&(t==root||t.IsChildOf(root)))continue;
            foreach(var o in new Object[]{t.gameObject}.Concat(t.GetComponents<Component>().Cast<Object>()))
            {if(!o||allowed.Contains(o))continue;d[GlobalObjectId.GetGlobalObjectIdSlow(o).ToString()]=EditorJsonUtility.ToJson(o);}
        }return d;
    }
    static void Check(Dictionary<string,string> before)
    {var now=Snapshot();var diff=before.Keys.Union(now.Keys).Where(k=>!before.ContainsKey(k)||!now.ContainsKey(k)||before[k]!=now[k]).ToArray();File.WriteAllText(Out+"/auditoria_ambito.txt",$"Registros protegidos: {before.Count}; diferencias: {diff.Length}\n"+string.Join("\n",diff));if(diff.Length>0)throw new Exception("Cambios no autorizados fuera de 2e");}
    static void SetupLight(Light l, Vector3 pos, bool low, bool horror, int index)
    {
        pos.y=6.32f;l.transform.position=pos;l.transform.rotation=Quaternion.LookRotation(Vector3.down,Vector3.forward);l.type=LightType.Point;l.range=4.70f;
        l.color=horror?new Color(.65f,.78f,.80f):new Color(1f,.94f,.83f);
        l.intensity=horror?(low?.05f:(index==1||index==5||index==8?1.1f:.25f)):(low?.25f:2.2f);
        l.shadows=LightShadows.None;l.bounceIntensity=0;l.lightmapBakeType=LightmapBakeType.Realtime;l.cullingMask=1<<30;l.renderMode=LightRenderMode.ForcePixel;l.enabled=true;
        var d=l.GetComponent<UniversalAdditionalLightData>();if(!d)d=l.gameObject.AddComponent<UniversalAdditionalLightData>();d.renderingLayers=Mask;d.customShadowLayers=false;
    }
    static Material TerrorMaterial(Material source,bool low,bool strong)
    {
        string name=low?"A17_Difusor_Terror_Tenue":strong?"A17_Difusor_Terror_Debil":"A17_Difusor_Terror_Residual";
        string path=Dir+"/"+name+".mat";var m=AssetDatabase.LoadAssetAtPath<Material>(path);if(m)return m;
        m=new Material(source){name=name,enableInstancing=true};m.SetColor("_EmissionColor",new Color(.50f,.65f,.68f)*(low?.015f:strong?.35f:.035f));m.EnableKeyword("_EMISSION");m.globalIlluminationFlags=MaterialGlobalIlluminationFlags.None;AssetDatabase.CreateAsset(m,path);return m;
    }
    public static void Run()
    {
        try
        {
            Init();if(root)throw new Exception("2e ya existe; no duplicar");if(obsolete.Length!=6)throw new Exception("Inventario antiguo distinto del revisado");
            var rp=QualitySettings.renderPipeline as UniversalRenderPipelineAsset;if(!rp||!rp.useRenderingLayers)throw new Exception("Se requieren rendering layers existentes");
            allowed.Clear();allowed.Add(aula);
            foreach(var g in obsolete){Permit(g);allowed.Add(g.transform.parent);}
            foreach(var l in reused)Permit(l.gameObject);
            var holders=aula.Find("Plafon_Aula17_Fase2a_SUPUESTO/Troffers_10_encendidos_1_tenue").Cast<Transform>().OrderBy(t=>t.name).ToArray();
            if(holders.Length!=11)throw new Exception("Troffers deben ser once");
            foreach(var h in holders){allowed.Add(h);Permit(h.Find("Difusor").gameObject);}
            // Only receiver bits change on existing Aula17 renderers; materials and geometry are preserved.
            foreach(var r in aula.GetComponentsInChildren<Renderer>(true))allowed.Add(r);
            var before=Snapshot();
            Directory.CreateDirectory(Dir);AssetDatabase.Refresh();root=Group(RootName,aula);night=Group("NOCHE",root);terror=Group("TERROR",root);
            foreach(var g in obsolete)Object.DestroyImmediate(g);
            var rows=new List<string>{"troffer|modo|luz|x_unity|y_unity|z_unity|intensidad|alcance|angulo|rendering_layer|sombras|certeza"};
            for(int i=0;i<holders.Length;i++)
            {
                var h=holders[i];bool low=h.name.Contains("Tenue");var diff=h.Find("Difusor");var pos=diff.GetComponent<Renderer>().bounds.center;pos.y=6.32f;
                var hn=Group(h.name,night);var ht=Group(h.name,terror);
                diff.SetParent(hn,true);diff.name="Difusor_NOCHE_"+h.name;
                var td=Object.Instantiate(diff.gameObject,ht);td.name="Difusor_TERROR_"+h.name;td.transform.SetPositionAndRotation(diff.position,diff.rotation);td.transform.localScale=diff.lossyScale;
                td.GetComponent<Renderer>().sharedMaterial=TerrorMaterial(diff.GetComponent<Renderer>().sharedMaterial,low,i==1||i==5||i==8);
                foreach(bool horror in new[]{false,true})
                {
                    Light l;
                    if(!horror&&i<2)l=reused[i];
                    else{var g=Group("Luz_"+(horror?"TERROR_":"NOCHE_")+h.name,horror?ht:hn);l=g.gameObject.AddComponent<Light>();}
                    SetupLight(l,pos,low,horror,i);rows.Add(FormattableString.Invariant($"{h.name}|{(horror?"TERROR":"NOCHE")}|{P(l.transform)}|{pos.x:F4}|{pos.y:F4}|{pos.z:F4}|{l.intensity:F3}|{l.range:F2}|{l.spotAngle:F0}|{Mask}|None|SUPUESTO"));
                }
            }
            foreach(var r in aula.GetComponentsInChildren<Renderer>(true))r.renderingLayerMask|=Mask;
            var tracker=root.gameObject.AddComponent<Docencia2InteriorModeTracker>();tracker.nightGroup=night.gameObject;tracker.terrorGroup=terror.gameObject;tracker.switcher=switcher;
            Sync();Check(before);Audit();AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(aula.gameObject.scene);EditorSceneManager.SaveScene(aula.gameObject.scene);
            File.WriteAllLines(Out+"/Luces_Colocadas_SUPUESTO.csv",rows);
            Capture();
            Complete();EditorApplication.Exit(0);
        }catch(Exception e){Fail(e);}
    }
    static void Sync(){foreach(var tracker in Object.FindObjectsByType<Docencia2InteriorModeTracker>(FindObjectsInactive.Include))tracker.SendMessage("Update",SendMessageOptions.RequireReceiver);}
    static void Audit()
    {
        var lines=new List<string>();var outside=All().Where(t=>t!=aula&&!t.IsChildOf(aula)).SelectMany(t=>t.GetComponents<Renderer>()).Where(r=>(r.renderingLayerMask&Mask)!=0).ToArray();
        var lights=root.GetComponentsInChildren<Light>(true).Concat(reused).ToArray();
        int reachable=outside.Count(r=>lights.Any(l=>Vector3.Distance(l.transform.position,r.bounds.ClosestPoint(l.transform.position))<=l.range+.01f));
        if(reachable!=0||lights.Length!=22||lights.Any(l=>l.renderingLayerMask!=Mask||l.shadows!=LightShadows.None))throw new Exception("Aislamiento/cantidad/sombras incorrectos");
        lines.Add("Renderers ajenos con bit32: "+outside.Length+"; dentro del alcance de alguna luz Aula17: "+reachable);
        lines.Add("Luces de 2e: "+lights.Length+"; mapas de sombras adicionales solicitados: 0");
        lines.Add("Rendering layers habilitadas en URP activo. Culling capa30. Bit32 compartido con receptores lejanos de PB: se verifica separacion por alcance.");
        lines.Add("Nuevos objetos grupo2e: "+root.GetComponentsInChildren<Transform>(true).Length+" (incluye 11 difusores reubicados de2a)");
        lines.Add("Renderers en grupo2e: "+root.GetComponentsInChildren<Renderer>(true).Length);
        lines.Add("Control anterior, tecla T y materiales originales preservados. Se agrega instancia del tracker existente para grupos locales.");
        File.WriteAllLines(Out+"/auditoria_luces.txt",lines);
    }
    static void Capture()
    {
        var originalMode=switcher.modoActual;var temp=new GameObject("TEMP_A17_2e_Camara");var c=temp.AddComponent<Camera>();c.enabled=false;c.nearClipPlane=.04f;c.farClipPlane=100;c.clearFlags=CameraClearFlags.SolidColor;c.allowHDR=true;
        var data=temp.AddComponent<UniversalAdditionalCameraData>();data.renderPostProcessing=true;data.renderShadows=true;
        var rt=new RenderTexture(1600,1100,24,RenderTextureFormat.ARGBHalf);rt.antiAliasing=1;c.targetTexture=rt;var previous=RenderTexture.active;var log=new List<string>();
        try
        {
            foreach(var mode in new[]{Docencia2VisualMode.Noche,Docencia2VisualMode.Terror})
            {
                if(switcher.modoActual!=mode)switcher.ToggleMode();Sync();
                var active=root.GetComponentsInChildren<Light>(true).Concat(reused).Count(l=>l.enabled&&l.gameObject.activeInHierarchy);log.Add(mode+": luces activas "+active+"; tecla configurada "+switcher.toggleKey);
                if(active!=11)throw new Exception("Modo no sincronizado");
                c.backgroundColor=mode==Docencia2VisualMode.Terror?switcher.terrorFogColor:switcher.nightFogColor;
                void Shot(string name,Vector3 from,Vector3 to,float fov)
                {c.fieldOfView=fov;c.transform.position=from;c.transform.LookAt(to);var tex=new Texture2D(1600,1100,TextureFormat.RGB24,false);for(int i=0;i<6;i++){c.Render();RenderTexture.active=rt;tex.ReadPixels(new Rect(0,0,1600,1100),0,0);tex.Apply();}File.WriteAllBytes(Out+"/"+mode+"_"+name+".png",tex.EncodeToPNG());Object.DestroyImmediate(tex);}
                Shot("01_General",W(.95f,5.32f,29.6f),W(7.5f,4.9f,25.7f),82);
                Shot("02_Inversa",W(10f,5.3f,24.4f),W(3.5f,4.9f,27.2f),82);
                Shot("03_Plafon",W(5.5f,4.9f,28.8f),W(5.5f,6.8f,26.0f),85);
            }
            File.WriteAllLines(Out+"/verificacion_modos.txt",log);
        }
        finally{switcher.ApplyMode(originalMode,false);Sync();c.targetTexture=null;RenderTexture.active=previous;rt.Release();Object.DestroyImmediate(rt);Object.DestroyImmediate(temp);}
    }
    public static void CaptureSaved(){try{Init();if(!root)throw new Exception("2e ausente");Audit();Capture();EditorApplication.Exit(0);}catch(Exception e){Fail(e);}}
    public static void Revise()
    {
        try
        {
            Init();if(!root)throw new Exception("2e ausente");
            allowed.Clear();foreach(var l in reused)Permit(l.gameObject);foreach(var r in aula.GetComponentsInChildren<Renderer>(true))allowed.Add(r);
            var before=Snapshot();
            foreach(var r in aula.GetComponentsInChildren<Renderer>(true))r.renderingLayerMask=(r.renderingLayerMask&~256u)|Mask;
            night=root.Find("NOCHE");terror=root.Find("TERROR");
            var rows=new List<string>{"modo|luz|posicion_unity|tipo|intensidad|alcance|mascara|supuesto"};
            foreach(var mode in new[]{night,terror})
            {
                var holders=mode.Cast<Transform>().OrderBy(t=>t.name).ToArray();
                for(int i=0;i<holders.Length;i++)
                {
                    var l=mode==night&&i<2?reused[i]:holders[i].GetComponentInChildren<Light>(true);
                    SetupLight(l,l.transform.position,holders[i].name.Contains("Tenue"),mode==terror,i);
                    rows.Add($"{mode.name}|{P(l.transform)}|{l.transform.position.ToString("F4")}|{l.type}|{l.intensity}|{l.range}|{Mask}|SUPUESTO");
                }
            }
            Audit();Check(before);File.WriteAllLines(Out+"/Luces_Colocadas_SUPUESTO.csv",rows);EditorSceneManager.MarkSceneDirty(aula.gameObject.scene);EditorSceneManager.SaveScene(aula.gameObject.scene);Capture();Complete();EditorApplication.Exit(0);
        }catch(Exception e){Fail(e);}
    }
    static void Complete(){File.WriteAllText(Out+"/completado.txt","Fase2e guardada fuera de Play; 6 piezas antiguas retiradas; 2 luces originales conservadas; 11 luces por modo (10 normales +1 tenue en NOCHE). Point, Y6.32, rango4.70, rendering layer32, sin sombras adicionales. Capturas sin luces auxiliares.\n"+DateTime.Now.ToString("O"));}
    static void Fail(Exception e){Directory.CreateDirectory(Out);File.WriteAllText(Out+"/error.txt",e.ToString());Debug.LogException(e);EditorApplication.Exit(1);}
}
