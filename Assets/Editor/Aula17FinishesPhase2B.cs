// Acabados locales del Aula17. No construye puertas, ventanas, mobiliario ni luces funcionales.
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
using Object=UnityEngine.Object;

public static class Aula17FinishesPhase2B
{
    const string ScenePath="Assets/Scenes/Exterior_Docencia2.unity", Dir="Assets/Models/Docencia2/Aula17_Fase2b", Out="Aula17_Fase2b", Name="Acabados_Aula17_Fase2b_SUPUESTO";
    static Transform aula,root;static Material paint,board,metal,dark,zoclo,wear;static List<string> rows;
    static Vector3 W(float x,float y,float z)=>Docencia2InteriorBuilder.PlanToUnityPos(x,y,z);
    static string PathOf(Transform t)=>t.parent?PathOf(t.parent)+"/"+t.name:t.name;
    static Material Load(string p)=>AssetDatabase.LoadAssetAtPath<Material>(p);
    static Transform Group(string n){var t=new GameObject(n).transform;t.SetParent(root,false);t.gameObject.layer=30;return t;}
    static Dictionary<string,string> Snapshot()
    {
        var d=new Dictionary<string,string>();
        foreach(var g in aula.gameObject.scene.GetRootGameObjects())foreach(var t in g.GetComponentsInChildren<Transform>(true))
        {
            if(t==aula || (root&&(t==root||t.IsChildOf(root))))continue;
            foreach(var o in new Object[]{t.gameObject}.Concat(t.GetComponents<Component>().Cast<Object>()))if(o)d[GlobalObjectId.GetGlobalObjectIdSlow(o).ToString()]=EditorJsonUtility.ToJson(o);
        }
        return d;
    }
    static void Check(Dictionary<string,string> before)
    {
        var now=Snapshot();var changed=before.Keys.Where(k=>!now.ContainsKey(k)||now[k]!=before[k]).Concat(now.Keys.Except(before.Keys)).ToArray();
        File.WriteAllText(Out+"/auditoria_ambito_2b.txt",$"Componentes/objetos protegidos: {before.Count}. Cambios fuera de Acabados_Aula17: {changed.Length}\n"+string.Join("\n",changed));
        if(changed.Length!=0)throw new Exception("Cambio de ambito; no guardar.");
    }
    static Material Local(string n,Material source,Color color,float smooth=.1f)
    {
        var p=Dir+"/"+n+".mat";var m=Load(p);if(!m){m=new Material(source);AssetDatabase.CreateAsset(m,p);}
        m.name=n;m.SetColor("_BaseColor",color);m.SetColor("_Color",color);m.SetFloat("_Smoothness",smooth);m.enableInstancing=true;EditorUtility.SetDirty(m);return m;
    }
    static void Style(GameObject g,Material m)
    {
        g.layer=30;g.isStatic=true;var r=g.GetComponent<Renderer>();r.sharedMaterial=m;r.shadowCastingMode=ShadowCastingMode.Off;r.renderingLayerMask=17;
    }
    static GameObject Box(string n,Transform p,float x0,float x1,float y0,float y1,float z0,float z1,Material m)
    {
        var g=GameObject.CreatePrimitive(PrimitiveType.Cube);g.name=n;g.transform.SetParent(p,false);Object.DestroyImmediate(g.GetComponent<Collider>());
        var a=W(x0,y0,z0);var b=W(x1,y1,z1);g.transform.position=(a+b)*.5f;g.transform.localScale=new Vector3(Mathf.Abs(b.x-a.x),y1-y0,Mathf.Abs(b.z-a.z));Style(g,m);return g;
    }
    static Mesh Persist(Mesh mesh,string n)
    {
        string p=Dir+"/"+n+".asset";var old=AssetDatabase.LoadAssetAtPath<Mesh>(p);mesh.name=n;
        if(old){EditorUtility.CopySerialized(mesh,old);Object.DestroyImmediate(mesh);EditorUtility.SetDirty(old);return old;}AssetDatabase.CreateAsset(mesh,p);return mesh;
    }
    static GameObject MeshObject(string n,Transform p,List<Vector3> vv,List<int> tt,Material m)
    {
        var mesh=new Mesh();mesh.vertices=vv.ToArray();mesh.triangles=tt.ToArray();mesh.RecalculateNormals();mesh.RecalculateBounds();
        var g=new GameObject(n,typeof(MeshFilter),typeof(MeshRenderer));g.transform.SetParent(p,false);g.GetComponent<MeshFilter>().sharedMesh=Persist(mesh,n);Style(g,m);return g;
    }
    static void Face(string n,Transform p,Vector3[] v,Vector3 normal,Material m)
    {
        var tri=new List<int>();for(int i=1;i<v.Length-1;i++){if(Vector3.Dot(Vector3.Cross(v[i]-v[0],v[i+1]-v[0]),normal)>0)tri.AddRange(new[]{0,i,i+1});else tri.AddRange(new[]{0,i+1,i});}MeshObject(n,p,v.ToList(),tri,m);
    }
    static void Text(string n,Transform p,string text,float x,float y,float z,float h,Color color)
    {
        var g=new GameObject(n);g.transform.SetParent(p,false);g.transform.position=W(x,y,z);g.transform.rotation=Quaternion.Euler(0,90,0);g.layer=30;
        var t=g.AddComponent<TextMesh>();t.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");t.font.RequestCharactersInTexture(text,64,FontStyle.Normal);
        t.text=text;t.fontSize=64;t.characterSize=h*.70f/64f*10;t.anchor=TextAnchor.MiddleCenter;t.alignment=TextAlignment.Center;t.color=color;
        var r=g.GetComponent<Renderer>();r.sharedMaterial=t.font.material;r.shadowCastingMode=ShadowCastingMode.Off;r.renderingLayerMask=17;
    }
    static void QR(Transform parent)
    {
        const int n=21;var v=new List<Vector3>();var tri=new List<int>();
        bool Finder(int r,int c)=>r==0||r==6||c==0||c==6||(r>=2&&r<=4&&c>=2&&c<=4);
        for(int r=0;r<n;r++)for(int c=0;c<n;c++)
        {
            bool on=r<7&&c<7?Finder(r,c):r<7&&c>=14?Finder(r,c-14):r>=14&&c<7?Finder(r-14,c):((r*17+c*23+r*c*7)%11)<5;
            if(!on)continue;
            float x=9.255f+c*.0074f,y=5.105f+(20-r)*.0074f;int b=v.Count;
            v.AddRange(new[]{W(x,y,23.227f),W(x+.0074f,y,23.227f),W(x+.0074f,y+.0074f,23.227f),W(x,y+.0074f,23.227f)});
            // Face toward the interior, world -X.
            tri.AddRange(new[]{b,b+2,b+1,b,b+3,b+2});
        }
        MeshObject("QR_Generico_No_Codifica_Datos",parent,v,tri,dark);
    }
    static void InspectOldLights()
    {
        var group=GameObject.Find("PA_Interior/Iluminacion_PA_Mismo_Criterio_PB").transform;
        var list=new List<string>{"# Luminarias antiguas del Aula 17 — inventario previo para 2e","","Solo lectura: no se han retirado, desplazado ni modificado.","","| Objeto | Activo propio / jerarquía | Componentes | Posición Unity |","|---|---|---|---|"};
        foreach(var t in group.GetComponentsInChildren<Transform>(true).Where(t=>t.name=="Marco_Aula_17"||t.name=="Difusor_Aula_17"||t.name=="Difusor_Terror_Aula_17"||t.name.StartsWith("Luz_PA_Aula_17_")))
        {
            var comp=string.Join(", ",t.GetComponents<Component>().Where(c=>c).Select(c=>c.GetType().Name));
            list.Add($"| `{PathOf(t)}` | {t.gameObject.activeSelf} / {t.gameObject.activeInHierarchy} | {comp} | {t.position.ToString("F4")} |");
        }
        list.Add("\nControl conservado: `"+PathOf(group)+"`: "+string.Join(", ",group.GetComponents<Component>().Select(c=>c.GetType().Name))+". Su script sigue vinculado al selector NOCHE/TERROR existente.\n\nEn 2e está autorizada únicamente la corrección de los elementos del Aula 17 identificados arriba; las otras aulas y el script de control quedan protegidos.");
        File.WriteAllLines(Out+"/Inventario_Luminarias_Antiguas_Para_2e.md",list);
        var foreign=new List<string>{"# Objetos previos de otros grupos dentro del volumen del aula","","Lectura de bounds. Se conservan sin modificaciones; no autoriza cambios fuera de Aula17.",""};
        foreach(var g in aula.gameObject.scene.GetRootGameObjects())foreach(var r in g.GetComponentsInChildren<Renderer>(true))
        {
            if(r.transform.IsChildOf(aula))continue;var b=r.bounds;var c=b.center;float x=(14.25f-c.z)*28.48f/28.50f,z=(27-c.x)*53.92f/54f;
            if(x>.4f&&x<11.16f&&z>23.22f&&z<30.78f&&c.y>3.65f&&c.y<6.6f)foreign.Add($"- `{PathOf(r.transform)}` — centro Unity {c}; tamaño {b.size}.");
        }
        File.WriteAllLines(Out+"/Objetos_Previos_Fuera_Del_Grupo.md",foreign);
    }
    public static void Run()
    {
        try
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new Exception("Fuera de Play obligatorio.");
            Directory.CreateDirectory(Dir);Directory.CreateDirectory(Out);AssetDatabase.Refresh();
            var scene=EditorSceneManager.OpenScene(ScenePath,OpenSceneMode.Single);aula=GameObject.Find("PA_Interior/Aula_17")?.transform;if(!aula)throw new Exception("No existe Aula17");
            root=aula.Find(Name);var before=Snapshot();InspectOldLights();if(root)Object.DestroyImmediate(root.gameObject);
            root=new GameObject(Name).transform;root.SetParent(aula,false);root.gameObject.layer=30;
            var wall=Load("Assets/Materials/Docencia2/Interior/Mat_Int_Muro_AmarilloCrema_PB.mat");
            paint=Local("A17_Parche_Pintura_GrisCrema_Mate",wall,new Color(214/255f,213/255f,201/255f));
            board=Load("Assets/Materials/Docencia2/Fase5/F5_Papel.mat");dark=Load("Assets/Materials/Docencia2/Fase5/F5_Negro.mat");
            metal=Local("A17_Marco_Metalico_Claro",Load("Assets/Materials/Docencia2/Fase5/F5_Metal.mat"),new Color(.70f,.72f,.74f),.42f);
            zoclo=Load("Assets/Materials/Docencia2/Interior/Mat_Int_Zoclo_CeramicoGris_PB.mat");wear=Load("Assets/Models/Docencia2/Aula17_Fase2a/A17_Mancha_Borde_Suave.mat");
            if(!board||!dark||!zoclo||!wear)throw new Exception("Material de acabados ausente");
            rows=new List<string>{"elemento|foto|ubicacion_plano|medidas|certeza"};
            var patches=Group("Pintura_Local");
            Face("Parche_Este_Pintura_Sin_Marco_F09",patches,new[]{W(11.157f,4.48f,24.7f),W(11.157f,5.98f,24.7f),W(11.157f,5.98f,26.1f),W(11.157f,5.96f,26.12f),W(11.157f,5.97f,29.24f),W(11.157f,5.94f,29.3f),W(11.157f,4.49f,29.3f),W(11.157f,4.5f,27.8f),W(11.157f,4.48f,27.78f)},Vector3.forward,paint);
            Face("Huella_Pintura_Norte_F13",patches,new[]{W(1.6f,4.48f,23.223f),W(3.2f,4.48f,23.223f),W(3.2f,6.70f,23.223f),W(1.6f,6.70f,23.223f)},Vector3.left,paint);
            rows.Add("Parche este|F09 F15|x11.157; z24.70-29.30; Y4.48-5.98|4.60x1.50 m|SUPUESTO; tono presentado previamente");
            rows.Add("Huella norte|F13|x1.60-3.20; z23.223; Y4.48-6.70|1.60x2.22 m|SUPUESTO; pintura plana");
            var baseboard=Group("Zoclos");
            Box("Zoclo_Oeste_Complemento",baseboard,.36f,.377f,3.606f,3.70f,23.215f,30.785f,zoclo);
            rows.Add("Piso y zoclos existentes|F03 F09 F20|Se conservan|Zoclo oeste añadido 9.4 cm alto|Piso y N/E/S originales intactos");
            var b=Group("Pizarron_Parte_Visible_F20");
            Box("Superficie_Visible_No_Completar_Oculto",b,7.85f,9.05f,4.48f,6.02f,23.225f,23.245f,board);
            Box("Marco_Superior",b,7.85f,9.065f,6.005f,6.035f,23.225f,23.26f,metal);
            Box("Marco_Inferior",b,7.85f,9.065f,4.465f,4.495f,23.225f,23.26f,metal);
            Box("Marco_Derecho",b,9.035f,9.065f,4.465f,6.035f,23.225f,23.26f,metal);
            var texts=new[]{"A - 0 pt","B - 1 pt","C - 2 pts","D - 3 pts"};
            for(int i=0;i<texts.Length;i++)Text("Texto_Visible_"+(i+1),b,texts[i],8.48f,5.68f-i*.28f,23.265f,.19f,new Color(.20f,.27f,.43f));
            rows.Add("Pizarron visible|F20|x7.85-9.05; z23.225; Y4.48-6.02|1.20x1.54 m visibles|SUPUESTO; pantalla y parte oculta pendientes 2d");
            var signs=Group("Aviso_WIFI_y_Apagador_F14_F20");
            Box("Papel_WIFI_Decorativo",signs,9.22f,9.45f,5.06f,5.36f,23.223f,23.225f,board);Text("WIFI",signs,"WIFI",9.335f,5.315f,23.228f,.065f,Color.black);QR(signs);
            Box("Placa_Apagador",signs,9.48f,9.56f,4.68f,4.80f,23.223f,23.230f,board);Box("Tecla_Apagador",signs,9.50f,9.54f,4.71f,4.775f,23.230f,23.238f,board);
            rows.Add("Aviso WIFI y apagador|F14 F20|x9.22-9.56; z23.23|Papel23x30cm; placa8x12cm|SUPUESTO; patrón decorativo sin datos");
            var channels=Group("Canaletas_y_Contactos_F11_F15_F19");
            foreach(float x in new[]{3.0f,5.8f,9.6f})Box("Canaleta_Sur_"+x,channels,x-.014f,x+.014f,4.35f,6.78f,30.76f,30.78f,board);
            Box("Canaleta_Horizontal_Sur_Visible",channels,2.0f,9.9f,4.325f,4.35f,30.76f,30.78f,board);
            Box("Placa_Contacto_Sur",channels,2.95f,3.05f,4.30f,4.40f,30.747f,30.76f,board);
            for(int i=0;i<2;i++)Box("Contacto_Sur_Ranura_"+i,channels,2.977f+i*.03f,2.983f+i*.03f,4.335f,4.365f,30.744f,30.747f,dark);
            Box("Placa_Contacto_Este",channels,11.137f,11.157f,4.00f,4.10f,24.75f,24.86f,board);
            for(int i=0;i<2;i++)Box("Contacto_Este_Ranura_"+i,channels,11.134f,11.137f,4.035f,4.065f,24.783f+i*.03f,24.789f+i*.03f,dark);
            rows.Add("Canaletas y 2 contactos|F11 F15 F19|Sur y junto a estante este|Canaleta2.8cm; placas10cm|SUPUESTO; sin cables ocultos ni minisplit");
            var scuffs=Group("Desgaste_Leve_F03_F15_F18");
            Face("Roce_Este_1",scuffs,new[]{W(11.154f,4.62f,24.3f),W(11.154f,4.60f,24.38f),W(11.154f,4.605f,24.40f),W(11.154f,4.628f,24.31f)},Vector3.forward,wear);
            for(int i=0;i<3;i++)Face("Roce_Piso_"+i,scuffs,new[]{W(2.8f+i*.09f,3.6085f,28.8f),W(2.94f+i*.09f,3.6085f,28.94f),W(2.95f+i*.09f,3.6085f,28.94f)},Vector3.up,wear);
            rows.Add("Roces leves|F03 F15 F18|Zona oeste del piso y esquina este|Tres trazos pequeños y un roce mural|SUPUESTO; no altera texturas aprobadas");
            CombineQRAndSmallParts();
            AssetDatabase.SaveAssets();Check(before);EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
            File.WriteAllLines(Out+"/Inventario_2b_SUPUESTO.txt",rows);
            WriteCounts();Capture();Check(before);
            File.WriteAllText(Out+"/completado_2b.txt","2b guardada fuera de Play. Sin cambios a estructura, puerta, ventanas, plafón o luces antiguas. Detención antes de 2c. "+DateTime.Now.ToString("O"));
            EditorApplication.Exit(0);
        }
        catch(Exception e){Debug.LogException(e);Directory.CreateDirectory(Out);File.WriteAllText(Out+"/error.txt",e.ToString());EditorApplication.Exit(1);}
    }
    static void CombineQRAndSmallParts()
    {
        // Preserve identifiable groups; combine only the numerous small contact/channel meshes by material.
        var parent=root.Find("Canaletas_y_Contactos_F11_F15_F19");
        foreach(var group in parent.GetComponentsInChildren<MeshFilter>().GroupBy(f=>f.GetComponent<Renderer>().sharedMaterial).ToArray())
        {
            var source=group.ToArray();var mesh=new Mesh();mesh.CombineMeshes(source.Select(s=>new CombineInstance{mesh=s.sharedMesh,transform=s.transform.localToWorldMatrix}).ToArray());
            var g=new GameObject("Combinado_"+group.Key.name,typeof(MeshFilter),typeof(MeshRenderer));g.transform.SetParent(parent,false);g.GetComponent<MeshFilter>().sharedMesh=Persist(mesh,g.name);Style(g,group.Key);foreach(var f in source)Object.DestroyImmediate(f.gameObject);
        }
    }
    static void WriteCounts()
    {
        var ceiling=aula.Find("Plafon_Aula17_Fase2a_SUPUESTO");int co=ceiling.GetComponentsInChildren<Transform>(true).Length,cr=ceiling.GetComponentsInChildren<Renderer>(true).Length;
        int fo=root.GetComponentsInChildren<Transform>(true).Length,fr=root.GetComponentsInChildren<Renderer>(true).Length;
        File.WriteAllText(Out+"/Conteo_Objetos_y_Renderers.txt",$"Plafón: {co} GameObjects (incluye grupos), {cr} renderers.\nAcabados2b: {fo} GameObjects (incluye grupos), {fr} renderers.\nTotal objetos nuevos actualmente persistentes 2a+2b: {co+fo}; renderers {cr+fr}. El plafón antiguo se retiró, por lo que el incremento neto frente a Fase1 es {co+fo-1} objetos.\nFPS: PENDIENTE Fase3, no inferido de renderizadores ni de capturas.\n");
    }
    public static void CaptureSaved()
    {
        try{EditorSceneManager.OpenScene(ScenePath);aula=GameObject.Find("PA_Interior/Aula_17").transform;root=aula.Find(Name);var before=Snapshot();InspectOldLights();Capture();Check(before);EditorApplication.Exit(0);}catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}
    }
    static void Capture()
    {
        var tmp=new GameObject("TEMP_A17_2b_Captura");var c=tmp.AddComponent<Camera>();c.enabled=false;c.nearClipPlane=.04f;c.farClipPlane=60;c.fieldOfView=75;c.clearFlags=CameraClearFlags.SolidColor;c.backgroundColor=Color.gray;var data=tmp.AddComponent<UniversalAdditionalCameraData>();data.renderPostProcessing=false;data.renderShadows=false;
        var temps=new List<GameObject>{tmp};
        var oldMode=RenderSettings.ambientMode;var oldAmbient=RenderSettings.ambientLight;var oldSky=RenderSettings.ambientSkyColor;var oldEquator=RenderSettings.ambientEquatorColor;var oldGround=RenderSettings.ambientGroundColor;bool oldFog=RenderSettings.fog;var oldSun=RenderSettings.sun;
        var keyGo=new GameObject("TEMP_A17_2b_Luz_Neutra");var key=keyGo.AddComponent<Light>();key.type=LightType.Directional;key.intensity=.85f;key.shadows=LightShadows.None;temps.Add(keyGo);
        RenderSettings.sun=key;RenderSettings.fog=false;RenderSettings.ambientMode=AmbientMode.Flat;RenderSettings.ambientLight=new Color(.3f,.3f,.3f);
        foreach(var pos in new[]{W(4,5.3f,25),W(8.3f,5.3f,27.8f)}){var g=new GameObject("TEMP_A17_2b_Luz");var l=g.AddComponent<Light>();l.type=LightType.Point;l.intensity=2.2f;l.range=15;l.shadows=LightShadows.None;g.transform.position=pos;temps.Add(g);}
        var rt=new RenderTexture(1600,1100,24);rt.antiAliasing=2;var prev=RenderTexture.active;c.targetTexture=rt;
        try
        {
            void Shot(string name,Vector3 from,Vector3 to,float fov=75){c.orthographic=false;c.fieldOfView=fov;c.transform.position=from;c.transform.LookAt(to);key.transform.rotation=c.transform.rotation;var tex=new Texture2D(1600,1100,TextureFormat.RGB24,false);for(int i=0;i<4;i++){c.Render();RenderTexture.active=rt;tex.ReadPixels(new Rect(0,0,1600,1100),0,0);tex.Apply();}File.WriteAllBytes(Out+"/"+name+".png",tex.EncodeToPNG());Object.DestroyImmediate(tex);}
            Shot("01_Acabados_Vista_General",W(1.1f,5.2f,30.1f),W(7.0f,5.15f,24.2f));
            Shot("02_Pared_Este_Parche_Pintura",W(6.7f,5.25f,27.0f),W(11.16f,5.22f,27.0f));
            Shot("03_Pizarron_WIFI_Apagador",W(8.65f,5.22f,26.1f),W(8.65f,5.20f,23.23f),58);
            Shot("04_Sur_Canaletas_Piso",W(7.2f,5.25f,25.3f),W(5.8f,4.95f,30.7f));
            Shot("05_Antiguas_Reservadas_2e",W(5.8f,5.65f,27),W(5.8f,6.79f,27),100);
        }
        finally{RenderSettings.sun=oldSun;RenderSettings.fog=oldFog;RenderSettings.ambientLight=oldAmbient;RenderSettings.ambientSkyColor=oldSky;RenderSettings.ambientEquatorColor=oldEquator;RenderSettings.ambientGroundColor=oldGround;RenderSettings.ambientMode=oldMode;c.targetTexture=null;RenderTexture.active=prev;rt.Release();Object.DestroyImmediate(rt);foreach(var g in temps)Object.DestroyImmediate(g);}
    }
}
