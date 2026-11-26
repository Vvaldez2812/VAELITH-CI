// Fase 2a exclusivamente. Plano aproximado SUPUESTO autorizado el 22-11-2026.
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Vaelith.Docencia2;
using Object = UnityEngine.Object;

public static class Aula17CeilingPhase2A
{
    const string ScenePath = "Assets/Scenes/Exterior_Docencia2.unity";
    const string Dir = "Assets/Models/Docencia2/Aula17_Fase2a";
    const string Output = "Aula17_Fase2a";
    const string GroupName = "Plafon_Aula17_Fase2a_SUPUESTO";
    const float X0=.36f, X1=11.24f, Z0=23.14f, Z1=30.86f, Y=6.80f;
    // Center on clear interior faces: x .36..11.165, z 23.215..30.785.
    // Visible edge cuts .3025 / .3025 and .485 / .485, all >= .20 m.
    static float BX(int i)=>i==0?X0:i==19?X1:.6625f+(i-1)*.6f;
    static float BZ(int i)=>i==0?Z0:i==13?Z1:23.70f+(i-1)*.6f;
    static float CX(int c)=>(BX(c)+BX(c+1))*.5f;
    static float CZ(int r)=>(BZ(r)+BZ(r+1))*.5f;
    static Transform aula, root, panels, bars, lamps, details, stains;
    static Material ceiling, dark, white, lit, dim, stainMat, stainSoft, fracture;
    static Mesh cube;
    static List<string> inventory;
    static Vector3 W(float x,float y,float z) => Docencia2InteriorBuilder.PlanToUnityPos(x,y,z);
    static Transform Group(string name, Transform parent) { var t=new GameObject(name).transform; t.SetParent(parent,false); return t; }
    static Material Load(string path) => AssetDatabase.LoadAssetAtPath<Material>(path);
    static string Key(Object o) => GlobalObjectId.GetGlobalObjectIdSlow(o).ToString();
    static Dictionary<string,string> Snapshot()
    {
        var result=new Dictionary<string,string>();
        foreach(var go in aula.gameObject.scene.GetRootGameObjects())
        foreach(var t in go.GetComponentsInChildren<Transform>(true))
        {
            if(t==aula || t.IsChildOf(aula)) continue;
            result[Key(t.gameObject)]=EditorJsonUtility.ToJson(t.gameObject);
            foreach(var c in t.GetComponents<Component>()) if(c) result[Key(c)]=EditorJsonUtility.ToJson(c);
        }
        // Preserve every existing object within Aula17 except its ceiling and the aula child list.
        foreach(var t in aula.GetComponentsInChildren<Transform>(true))
        {
            if(t==aula || t.name=="Plafon_PA_Y6.80" || t.name==GroupName || (root && t.IsChildOf(root))) continue;
            result[Key(t.gameObject)]=EditorJsonUtility.ToJson(t.gameObject);
            foreach(var c in t.GetComponents<Component>()) if(c) result[Key(c)]=EditorJsonUtility.ToJson(c);
        }
        return result;
    }
    static void Check(Dictionary<string,string> expected)
    {
        var after=Snapshot();
        var changed=expected.Keys.Where(k=>!after.ContainsKey(k)||after[k]!=expected[k]).ToArray();
        var extra=after.Keys.Except(expected.Keys).ToArray();
        File.WriteAllText(Output+"/auditoria_ambito.txt",$"Objetos/componentes protegidos: {expected.Count}\nAlterados: {changed.Length}\nAdiciones fuera del grupo: {extra.Length}\n"+string.Join("\n",changed.Concat(extra)));
        if(changed.Length+extra.Length>0) throw new Exception("Se detectaron cambios fuera del plafon autorizado; no se guarda.");
    }
    static Material Own(string name,Material source,Color color,Color? emission=null)
    {
        string path=Dir+"/"+name+".mat";
        var m=Load(path);
        if(!m) {m=new Material(source);AssetDatabase.CreateAsset(m,path);}
        m.name=name; m.SetColor("_BaseColor",color);m.SetColor("_Color",color);
        m.SetTexture("_BaseMap",null);m.SetTexture("_MainTex",null);
        m.SetFloat("_Smoothness",.1f);m.enableInstancing=true;
        m.globalIlluminationFlags=emission.HasValue ? MaterialGlobalIlluminationFlags.RealtimeEmissive : MaterialGlobalIlluminationFlags.None;
        if(emission.HasValue) {m.EnableKeyword("_EMISSION");m.SetColor("_EmissionColor",emission.Value);}
        else {m.DisableKeyword("_EMISSION");m.SetColor("_EmissionColor",Color.black);}
        EditorUtility.SetDirty(m); return m;
    }
    static GameObject Box(string name,Transform parent,float x0,float x1,float y0,float y1,float z0,float z1,Material material,bool panel=false)
    {
        var g=GameObject.CreatePrimitive(PrimitiveType.Cube);g.name=name;g.transform.SetParent(parent,false);
        Object.DestroyImmediate(g.GetComponent<Collider>());
        Vector3 a=W(x0,y0,z0),b=W(x1,y1,z1);
        g.transform.position=(a+b)*.5f;g.transform.localScale=new Vector3(Mathf.Abs(a.x-b.x),y1-y0,Mathf.Abs(a.z-b.z));
        if(panel) g.GetComponent<MeshFilter>().sharedMesh=cube;
        var r=g.GetComponent<MeshRenderer>();r.sharedMaterial=material;r.shadowCastingMode=ShadowCastingMode.Off;r.receiveShadows=true;r.renderingLayerMask=17;
        return g;
    }
    static void Record(string id,int c,int r,string desc)
    {
        inventory.Add(FormattableString.Invariant($"{id},{c+1},{r+1},{CX(c):F4},{CZ(r):F4},SUPUESTO,{desc}"));
    }
    static Mesh Persist(Mesh mesh,string name)
    {
        string path=Dir+"/"+name+".asset";var old=AssetDatabase.LoadAssetAtPath<Mesh>(path);
        mesh.name=name;
        if(old) {EditorUtility.CopySerialized(mesh,old);Object.DestroyImmediate(mesh);EditorUtility.SetDirty(old);return old;}
        AssetDatabase.CreateAsset(mesh,path);return mesh;
    }
    static Material StainMaterial(string name,float alpha)
    {
        var m=Own(name,ceiling,new Color(.42f,.35f,.22f,alpha));
        m.SetFloat("_Surface",1);m.SetFloat("_Blend",0);m.SetFloat("_SrcBlend",5);m.SetFloat("_DstBlend",10);m.SetFloat("_ZWrite",0);
        m.SetFloat("_SrcBlendAlpha",1);m.SetFloat("_DstBlendAlpha",10);m.SetOverrideTag("RenderType","Transparent");
        m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");m.renderQueue=3000;EditorUtility.SetDirty(m);return m;
    }
    static GameObject Surface(string name,Transform parent,Vector3[] vertices,int[] triangles,Material material)
    {
        if(name.Contains("Aureola") || vertices.All(v=>Mathf.Abs(v.y-vertices[0].y)<.00001f))
        {
            var unique=new HashSet<string>();var faces=new List<int>();
            for(int i=0;i<triangles.Length;i+=3)
            {
                int a=triangles[i],b=triangles[i+1],c=triangles[i+2];
                string key=string.Join("_",new[]{a,b,c}.OrderBy(v=>v));if(!unique.Add(key))continue;
                if(Vector3.Cross(vertices[b]-vertices[a],vertices[c]-vertices[a]).y>0) {int swap=b;b=c;c=swap;}
                faces.AddRange(new[]{a,b,c});
            }
            triangles=faces.ToArray();
        }
        var mesh=new Mesh();mesh.vertices=vertices;mesh.triangles=triangles;
        mesh.uv=Enumerable.Repeat(new Vector2(.125f,.125f),vertices.Length).ToArray();mesh.RecalculateNormals();mesh.RecalculateBounds();
        var g=new GameObject(name,typeof(MeshFilter),typeof(MeshRenderer));g.transform.SetParent(parent,false);
        g.GetComponent<MeshFilter>().sharedMesh=Persist(mesh,name);
        var r=g.GetComponent<MeshRenderer>();r.sharedMaterial=material;r.shadowCastingMode=ShadowCastingMode.Off;r.renderingLayerMask=17;
        return g;
    }
    static void Ring(string name,float x,float z,float rx,float rz,float phase,float inner=.78f,float outer=1f,int layer=0)
    {
        const int n=48;var vv=new List<Vector3>();var tt=new List<int>();
        for(int i=0;i<n;i++)
        {
            float a=i*Mathf.PI*2/n;float f=1+.05f*Mathf.Sin(5*a+phase)+.025f*Mathf.Cos(9*a);
            foreach(float radius in new[]{inner,outer})
            {
                var pos=W(x+rx*Mathf.Cos(a)*f*radius,Y-.003f,z+rz*Mathf.Sin(a)*f*radius);
                // Follow the small tilt of D3 instead of intersecting its lower surface.
                if(name.StartsWith("M1b_Aureola"))pos.y+=.001f-Mathf.Tan(1.3f*Mathf.Deg2Rad)*(pos.z-W(CX(17),0,CZ(2)).z);
                vv.Add(pos);
            }
        }
        for(int i=0;i<n;i++) {int a=i*2,b=((i+1)%n)*2;tt.AddRange(new[]{a,b,a+1,a+1,b,b+1,a+1,b,a,b+1,b,a+1});}
        Surface(name,stains,vv.ToArray(),tt.ToArray(),layer==0?stainMat:stainSoft);
        if(layer==0)
        {
            Ring(name+"_Centro",x,z,rx,rz,phase,.001f,.78f,1);
            Ring(name+"_Borde_Difuso",x,z,rx,rz,phase,1f,1.10f,1);
        }
    }
    static void Cable(string name,Vector3[] points,float radius,Material material)
    {
        var vv=new List<Vector3>();var tt=new List<int>(); const int n=8;
        for(int j=0;j<points.Length;j++)
        {
            Vector3 direction=(points[Mathf.Min(j+1,points.Length-1)]-points[Mathf.Max(0,j-1)]).normalized;
            Vector3 a=Vector3.Cross(direction,Vector3.forward).normalized;if(a.sqrMagnitude<.1f)a=Vector3.right;
            Vector3 b=Vector3.Cross(direction,a).normalized;
            for(int i=0;i<n;i++) vv.Add(points[j]+radius*(Mathf.Cos(i*Mathf.PI*2/n)*a+Mathf.Sin(i*Mathf.PI*2/n)*b));
        }
        for(int j=0;j<points.Length-1;j++)for(int i=0;i<n;i++) {int a=j*n+i,b=j*n+(i+1)%n;tt.AddRange(new[]{a,b,a+n,b,b+n,a+n});}
        Surface(name,details,vv.ToArray(),tt.ToArray(),material);
    }
    public static void Run()
    {
        try
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new Exception("Debe estar fuera de Play.");
            Directory.CreateDirectory(Output);Directory.CreateDirectory(Dir);AssetDatabase.Refresh();
            var scene=EditorSceneManager.OpenScene(ScenePath,OpenSceneMode.Single);
            aula=GameObject.Find("PA_Interior/Aula_17")?.transform;
            if(!aula)throw new Exception("Aula_17 no existe. No se construye estructura.");
            root=aula.Find(GroupName);
            var before=Snapshot();
            var original=aula.Find("Plafon_PA_Y6.80");
            if(!original && !root)throw new Exception("No se encontro plafon esperado.");
            if(original) Capture("Antes",false);
            if(original)Object.DestroyImmediate(original.gameObject);
            if(root)Object.DestroyImmediate(root.gameObject);
            root=Group(GroupName,aula);panels=Group("Paneles_60x60",root);bars=Group("Reticula",root);lamps=Group("Troffers_10_encendidos_1_tenue",root);details=Group("Detalles_D1_D2_D3",root);stains=Group("Manchas_M1_M2",root);
            ceiling=Load("Assets/Materials/Docencia2/Interior/Mat_Int_Plafon_Reticular_PB.mat");
            dark=Load("Assets/Materials/Docencia2/Mat_Int_Troffer_Marco_Negro.mat");
            if(!ceiling||!dark)throw new Exception("Material aprobado no encontrado.");
            white=Own("A17_Herrajes_Claros",ceiling,new Color(.79f,.8f,.78f));
            lit=Own("A17_Difusor_Encendido",ceiling,new Color(.88f,.88f,.8f),new Color(2.2f,2.05f,1.65f));
            dim=Own("A17_Difusor_Tenue",ceiling,new Color(.68f,.66f,.57f),new Color(.18f,.16f,.11f));
            stainMat=StainMaterial("A17_Mancha_Superficial",.19f);
            stainSoft=StainMaterial("A17_Mancha_Borde_Suave",.065f);
            fracture=Own("A17_Receso_Rotura",ceiling,new Color(.14f,.13f,.12f));
            var temp=GameObject.CreatePrimitive(PrimitiveType.Cube);var mesh=Object.Instantiate(temp.GetComponent<MeshFilter>().sharedMesh);Object.DestroyImmediate(temp);
            // Sample only the light interior of one existing texture tile; never change its material or texture.
            mesh.uv=mesh.uv.Select(uv=>new Vector2(.025f+uv.x*.2f,.025f+uv.y*.2f)).ToArray();cube=Persist(mesh,"Panel_UV_Material_Aprobado");
            inventory=new List<string>{"id,columna,fila,x_plano,z_plano,certeza,detalle"};
            var lights=new HashSet<Vector2Int>{new Vector2Int(2,1),new Vector2Int(7,1),new Vector2Int(12,1),new Vector2Int(16,1),new Vector2Int(2,6),new Vector2Int(10,6),new Vector2Int(16,6),new Vector2Int(2,11),new Vector2Int(7,11),new Vector2Int(16,11)};
            int count=0;
            for(int c=0;c<19;c++)for(int r=0;r<13;r++)
            {
                float x=BX(c),z=BZ(r),xx=BX(c+1),zz=BZ(r+1);
                bool on=lights.Contains(new Vector2Int(c,r)), low=c==9&&r==8;
                if(on||low)
                {
                    string id=low?"L11_Tenue":"L"+(++count).ToString("D2")+"_Encendido";
                    var holder=Group(id,lamps);
                    Box("Cuerpo",holder,x+.008f,xx-.008f,Y,Y+.025f,z+.008f,zz-.008f,dark);
                    Box("Difusor",holder,x+.022f,xx-.022f,Y-.002f,Y+.006f,z+.022f,zz-.022f,on?lit:dim);
                    Record(id,c,r,on?"Emision visible; luz funcional pendiente 2e":"Difusor tenue; luz funcional pendiente 2e");
                }
                else
                {
                    var p=Box($"Panel_C{c+1:00}_F{r+1:00}",panels,x+.005f,xx-.005f,Y,Y+.025f,z+.005f,zz-.005f,ceiling,true);
                    if(c==17&&r==2) {p.name+="_D3_Desencajado";p.transform.position+=Vector3.up*.001f;p.transform.Rotate(Vector3.right,1.3f,Space.World);Record("D3",c,r,"Desencajado leve 1.3 grados; respaldo opaco");}
                }
            }
            Box("Respaldo_Continuo_Sin_Huecos_Completos",panels,X0,X1,Y+.027f,Y+.037f,Z0,Z1,ceiling,true);
            for(int c=0;c<=19;c++) {float x=BX(c);Box("Perfil_X_"+c,bars,Mathf.Max(X0,x-.009f),Mathf.Min(X1,x+.009f),Y-.004f,Y+.009f,Z0,Z1,dark);}
            for(int r=0;r<=13;r++) {float z=BZ(r);Box("Perfil_Z_"+r,bars,X0,X1,Y-.004f,Y+.009f,Mathf.Max(Z0,z-.009f),Mathf.Min(Z1,z+.009f),dark);}
            // D1 is a local chipped surface with opaque backing, not a complete missing tile.
            float px=BX(10),pz=BZ(6);
            Surface("D1_Rotura_Local",details,new[]{W(px+.02f,Y-.003f,pz-.12f),W(px+.12f,Y-.003f,pz-.10f),W(px+.10f,Y-.003f,pz-.02f),W(px+.04f,Y-.003f,pz-.01f),W(px+.01f,Y-.003f,pz-.08f)},new[]{0,1,2,0,2,3,0,3,4},fracture);
            var chip=Box("D1_Borde_Fracturado",details,px+.01f,px+.075f,Y-.012f,Y+.007f,pz-.10f,pz-.07f,ceiling,true);chip.transform.Rotate(0,0,9);
            Box("Soporte_Proyector_Reserva_2d",details,px-.025f,px+.025f,Y-.39f,Y+.009f,pz-.025f,pz+.025f,white);
            Box("Placa_Soporte",details,px-.065f,px+.065f,Y-.012f,Y-.003f,pz-.05f,pz+.05f,white);
            Cable("D2_Cable_Colgante_Sin_Colision",new[]{W(6.96f,Y,27.34f),W(6.94f,Y-.07f,27.35f),W(6.98f,Y-.14f,27.37f),W(6.975f,Y-.20f,27.39f)},.005f,dark);
            var device=GameObject.CreatePrimitive(PrimitiveType.Cylinder);device.name="Dispositivo_Circular_Solo_Forma";device.transform.SetParent(details,false);device.transform.position=W(6.96f,Y-.015f,28.04f);device.transform.localScale=new Vector3(.18f,.014f,.18f);Object.DestroyImmediate(device.GetComponent<Collider>());device.GetComponent<Renderer>().sharedMaterial=white;device.GetComponent<Renderer>().shadowCastingMode=ShadowCastingMode.Off;
            Ring("M1a_Aureola",CX(16),CZ(2),.22f,.19f,.2f);Ring("M1b_Aureola",CX(17),CZ(2),.20f,.16f,1.2f);
            Ring("M2a_Aureola",CX(12),CZ(7),.19f,.14f,2f);Ring("M2b_Aureola",CX(11),CZ(7),.14f,.10f,.5f);
            inventory.AddRange(new[]{"D1,,,6.0625,26.70,SUPUESTO,Rotura junto a soporte; sin hueco completo","D2,,,6.96,27.34,SUPUESTO,Cable negro 20 cm sin colision","M1,,,9.9625-10.5625,24.60,SUPUESTO,Dos aureolas en zona este","M2,,,6.9625-7.5625,27.60,SUPUESTO,Dos aureolas centrales","Circular,,,6.96,28.04,SUPUESTO,Disco 18 cm solo geometria"});
            File.WriteAllLines(Output+"/plano_elementos_SUPUESTO.csv",inventory);
            foreach(var t in root.GetComponentsInChildren<Transform>(true))t.gameObject.layer=aula.gameObject.layer;
            foreach(var r in root.GetComponentsInChildren<Renderer>(true))r.renderingLayerMask=17;
            OptimizeCeiling();
            AssetDatabase.SaveAssets();
            Check(before);
            if(root.GetComponentsInChildren<Light>().Length!=0||root.GetComponentsInChildren<Collider>().Length!=0)throw new Exception("No se permiten luces funcionales ni colliders en fase2a.");
            Capture("Despues",true);
            Check(before);
            EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
            File.WriteAllText(Output+"/completado.txt",$"Fase 2a reticula RE-CENTRADA guardada fuera de Play. {DateTime.Now:O}\n236 paneles y 34 perfiles combinados; troffers: {lamps.childCount}.\nY=6.80; paso .60; primer cruce interior .6625,23.70; cortes visibles O/E=.3025 N/S=.485; SUPUESTO.\nSin rejillas, minisplit, luces funcionales o colision. Capturas con luz temporal de inspeccion eliminada antes de guardar.\n");
            Debug.Log("AULA17_FASE2A_OK");EditorApplication.Exit(0);
        }
        catch(Exception e){Debug.LogException(e);Directory.CreateDirectory(Output);File.WriteAllText(Output+"/error.txt",e.ToString());EditorApplication.Exit(1);}
    }
    static void CombineGroup(Transform target,string name,Func<MeshFilter,bool> filter)
    {
        var sources=target.GetComponentsInChildren<MeshFilter>().Where(filter).ToArray();
        if(sources.Length<2)return;
        var m=sources[0].GetComponent<Renderer>().sharedMaterial;
        if(sources.Any(s=>s.GetComponent<Renderer>().sharedMaterial!=m))throw new Exception("Combinacion con materiales diferentes");
        var mesh=new Mesh();mesh.indexFormat=IndexFormat.UInt32;
        mesh.CombineMeshes(sources.Select(s=>new CombineInstance{mesh=s.sharedMesh,transform=target.worldToLocalMatrix*s.transform.localToWorldMatrix}).ToArray(),true,true,false);
        var go=new GameObject(name,typeof(MeshFilter),typeof(MeshRenderer));go.transform.SetParent(target,false);go.layer=aula.gameObject.layer;go.isStatic=true;
        go.GetComponent<MeshFilter>().sharedMesh=Persist(mesh,name);
        var r=go.GetComponent<MeshRenderer>();r.sharedMaterial=m;r.shadowCastingMode=ShadowCastingMode.Off;r.renderingLayerMask=17;
        foreach(var s in sources)Object.DestroyImmediate(s.gameObject);
    }
    static void OptimizeCeiling()
    {
        int before=root.GetComponentsInChildren<Renderer>().Length;
        foreach(var t in panels.GetComponentsInChildren<Transform>(true))t.gameObject.isStatic=true;
        foreach(var t in bars.GetComponentsInChildren<Transform>(true))t.gameObject.isStatic=true;
        CombineGroup(panels,"Paneles_Estaticos_Combinados",s=>!s.name.Contains("D3")&&!s.name.Contains("Respaldo"));
        CombineGroup(bars,"Perfiles_Estaticos_Combinados",s=>true);
        int after=root.GetComponentsInChildren<Renderer>().Length;
        File.WriteAllText(Output+"/rendimiento_geometria.txt",$"Plafon: renderers antes {before}, despues {after}. Objetos nuevos de plafon incluido grupo: {root.GetComponentsInChildren<Transform>(true).Length}. Paneles/perfiles Static. Materiales compartidos, instancing existente preservado. FPS pendiente de Fase 3.\n");
    }
    public static void CaptureSaved()
    {
        try
        {
            EditorSceneManager.OpenScene(ScenePath,OpenSceneMode.Single);
            aula=GameObject.Find("PA_Interior/Aula_17").transform;root=aula.Find(GroupName);
            var expected=Snapshot();Capture("Despues",true);Check(expected);
            File.WriteAllText(Output+"/capturas_escena_reabierta.txt","Capturas regeneradas desde la escena guardada, sin reconstruir ni guardar cambios.\n");
            EditorApplication.Exit(0);
        }
        catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}
    }
    public static void FinalizePhase()
    {
        try
        {
            var scene=EditorSceneManager.OpenScene(ScenePath,OpenSceneMode.Single);
            aula=GameObject.Find("PA_Interior/Aula_17").transform;root=aula.Find(GroupName);
            var expected=Snapshot();
            foreach(var t in root.GetComponentsInChildren<Transform>(true))t.gameObject.layer=aula.gameObject.layer;
            foreach(var r in root.GetComponentsInChildren<Renderer>(true))r.renderingLayerMask=17;
            Check(expected);EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
            Capture("Despues",true);Check(expected);
            File.WriteAllText(Output+"/capturas_escena_reabierta.txt","Escena reabierta y capas de objetos nuevos igualadas a Aula17: "+aula.gameObject.layer+". RenderingLayerMask=17. Capturas finales sin reconstruir mallas; temporales eliminados.\n");
            EditorApplication.Exit(0);
        }
        catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}
    }
    static void Capture(string prefix,bool all)
    {
        // Inspection light only for geometry readability, never saved and never claimed as final 2e lighting.
        var temp=new GameObject("TEMP_A17_Inspeccion");var cam=temp.AddComponent<Camera>();cam.enabled=false;cam.clearFlags=CameraClearFlags.SolidColor;cam.backgroundColor=new Color(.18f,.19f,.20f);cam.nearClipPlane=.03f;cam.farClipPlane=60;cam.fieldOfView=85;
        var data=temp.AddComponent<UniversalAdditionalCameraData>();data.renderPostProcessing=false;data.renderShadows=false;
        var lampGo=new GameObject("TEMP_A17_Luz_Inspeccion");var light=lampGo.AddComponent<Light>();light.type=LightType.Directional;light.intensity=1.1f;light.shadows=LightShadows.None;light.transform.rotation=Quaternion.LookRotation(Vector3.up);
        var rt=new RenderTexture(1600,1100,24,RenderTextureFormat.ARGB32);rt.antiAliasing=2;var previous=RenderTexture.active;
        try
        {
            cam.targetTexture=rt;
            void Shot(string name,Vector3 p,Vector3 target,bool ortho=false)
            {
                cam.orthographic=ortho;cam.orthographicSize=4.3f;cam.transform.position=p;
                if(ortho)cam.transform.rotation=Quaternion.LookRotation(Vector3.up,Vector3.right);else cam.transform.LookAt(target);
                var tex=new Texture2D(rt.width,rt.height,TextureFormat.RGB24,false);
                // Warm up new shader variants and force completion before retaining a camera image.
                for(int warm=0;warm<4;warm++){cam.Render();RenderTexture.active=rt;tex.ReadPixels(new Rect(0,0,rt.width,rt.height),0,0);tex.Apply();}
                File.WriteAllBytes(Output+"/"+prefix+"_"+name+".png",tex.EncodeToPNG());Object.DestroyImmediate(tex);
            }
            Shot("01_Vista_F01_APROX",W(1.0f,5.05f,30.1f),W(6.4f,6.25f,26));
            Shot("02_Vista_Inversa",W(10.45f,5.1f,23.9f),W(4.9f,6.25f,27.8f));
            if(all) {Shot("03_Plano_Desde_Abajo",W(5.8f,5.4f,27),W(5.8f,6.8f,27),true);cam.fieldOfView=58;Shot("04_Detalles_D1_D2_M2",W(5.1f,5.8f,28.4f),W(6.8f,6.7f,27.2f));Shot("05_Manchas_M1_D3",W(9.3f,5.7f,26.2f),W(10.5f,6.8f,24.7f));}
        }
        finally {cam.targetTexture=null;RenderTexture.active=previous;rt.Release();Object.DestroyImmediate(rt);Object.DestroyImmediate(temp);Object.DestroyImmediate(lampGo);}
    }
}
