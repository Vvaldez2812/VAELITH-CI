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

public static class Aula17FurniturePhase2D
{
    const string ScenePath="Assets/Scenes/Exterior_Docencia2.unity", Out="Aula17_Fase2d", Dir="Assets/Models/Docencia2/Aula17_Fase2d", Name="Mobiliario_Equipo_Aula17_Fase2d_SUPUESTO";
    const float Floor=3.606f;
    static Transform aula,root,templates;
    static Dictionary<string,Material> mats=new Dictionary<string,Material>();
    static Dictionary<string,Transform> groups=new Dictionary<string,Transform>();
    static List<string> inventory=new List<string>();
    static Vector3 W(float x,float y,float z)=>Docencia2InteriorBuilder.PlanToUnityPos(x,y,z);
    static string PathOf(Transform t)=>t.parent?PathOf(t.parent)+"/"+t.name:t.name;
    static Transform Group(string n,Transform p){var g=new GameObject(n);g.layer=30;g.transform.SetParent(p,false);return g.transform;}
    static Transform TypeGroup(string n){if(!groups.ContainsKey(n))groups[n]=Group(n,root);return groups[n];}
    static Material M(string n)
    {
        if(mats.ContainsKey(n))return mats[n];
        string source=n=="MetalClaro"?"Assets/Models/Docencia2/Aula17_Fase2b/A17_Marco_Metalico_Claro.mat":"Assets/Materials/Docencia2/Fase5/F5_"+n+".mat";
        var original=AssetDatabase.LoadAssetAtPath<Material>(source);if(!original)throw new Exception("Material ausente "+source);
        string path=Dir+"/A17_Compartido_"+n+".mat";var material=AssetDatabase.LoadAssetAtPath<Material>(path);
        if(!material){material=new Material(original);material.name="A17_Compartido_"+n;material.enableInstancing=true;AssetDatabase.CreateAsset(material,path);}
        mats[n]=material;return material;
    }
    static void Style(GameObject g,string mat){g.layer=30;g.isStatic=true;var r=g.GetComponent<Renderer>();r.sharedMaterial=M(mat);r.renderingLayerMask=17;r.shadowCastingMode=ShadowCastingMode.Off;}
    static GameObject Box(Transform p,string n,Vector3 pos,Vector3 size,string mat)
    {var g=GameObject.CreatePrimitive(PrimitiveType.Cube);g.name=n;Object.DestroyImmediate(g.GetComponent<Collider>());g.transform.SetParent(p,false);g.transform.localPosition=pos;g.transform.localScale=size;Style(g,mat);return g;}
    static GameObject Cylinder(Transform p,string n,Vector3 pos,float radius,float height,string mat)
    {var g=GameObject.CreatePrimitive(PrimitiveType.Cylinder);g.name=n;Object.DestroyImmediate(g.GetComponent<Collider>());g.transform.SetParent(p,false);g.transform.localPosition=pos;g.transform.localScale=new Vector3(radius*2,height*.5f,radius*2);Style(g,mat);return g;}
    static void Segment(Transform p,string n,Vector3 a,Vector3 b,float width,string mat)
    {var g=Box(p,n,(a+b)*.5f,new Vector3(width,width,(b-a).magnitude),mat);g.transform.localRotation=Quaternion.LookRotation(b-a);}
    static void ColliderBox(Transform p,Vector3 center,Vector3 size){var c=p.gameObject.AddComponent<BoxCollider>();c.center=center;c.size=size;}
    static void Rounded(Transform p,string n,Vector3 center,float width,float height,float depth,float radius,string mat,Quaternion rotation)
    {
        var vv=new List<Vector3>();var tt=new List<int>();var ring=new List<Vector2>();
        for(int corner=0;corner<4;corner++)for(int s=0;s<=4;s++)
        {float a=(corner*90+s*22.5f)*Mathf.Deg2Rad;float cx=corner==0||corner==3?width/2-radius:-width/2+radius;float cy=corner<2?height/2-radius:-height/2+radius;ring.Add(new Vector2(cx+Mathf.Cos(a)*radius,cy+Mathf.Sin(a)*radius));}
        int count=ring.Count;
        for(int face=0;face<2;face++){int b=vv.Count;vv.Add(new Vector3(0,0,(face==0?1:-1)*depth/2));foreach(var q in ring)vv.Add(new Vector3(q.x,q.y,(face==0?1:-1)*depth/2));for(int i=0;i<count;i++){int j=(i+1)%count;tt.AddRange(face==0?new[]{b,b+i+1,b+j+1}:new[]{b,b+j+1,b+i+1});}}
        for(int i=0;i<count;i++){int j=(i+1)%count,b=vv.Count;vv.AddRange(new[]{new Vector3(ring[i].x,ring[i].y,-depth/2),new Vector3(ring[j].x,ring[j].y,-depth/2),new Vector3(ring[j].x,ring[j].y,depth/2),new Vector3(ring[i].x,ring[i].y,depth/2)});tt.AddRange(new[]{b,b+1,b+2,b,b+2,b+3});}
        var mesh=new Mesh();mesh.SetVertices(vv);mesh.SetTriangles(tt,0);mesh.RecalculateNormals();mesh.RecalculateBounds();
        var g=new GameObject(n,typeof(MeshFilter),typeof(MeshRenderer));g.transform.SetParent(p,false);g.transform.localPosition=center;g.transform.localRotation=rotation;g.GetComponent<MeshFilter>().sharedMesh=mesh;Style(g,mat);
    }
    static void Combine(Transform p,string key)
    {
        foreach(var group in p.GetComponentsInChildren<MeshFilter>().GroupBy(f=>f.GetComponent<Renderer>().sharedMaterial).ToArray())
        {
            var source=group.ToArray();var mesh=new Mesh();mesh.indexFormat=IndexFormat.UInt32;mesh.CombineMeshes(source.Select(s=>new CombineInstance{mesh=s.sharedMesh,transform=p.worldToLocalMatrix*s.transform.localToWorldMatrix}).ToArray());mesh.name=key+"_"+group.Key.name;
            string path=Dir+"/"+mesh.name+".asset";var old=AssetDatabase.LoadAssetAtPath<Mesh>(path);if(old){EditorUtility.CopySerialized(mesh,old);Object.DestroyImmediate(mesh);mesh=old;}else AssetDatabase.CreateAsset(mesh,path);
            var g=new GameObject(group.Key.name,typeof(MeshFilter),typeof(MeshRenderer));g.transform.SetParent(p,false);g.GetComponent<MeshFilter>().sharedMesh=mesh;g.layer=30;g.isStatic=true;var r=g.GetComponent<MeshRenderer>();r.sharedMaterial=group.Key;r.shadowCastingMode=ShadowCastingMode.Off;r.renderingLayerMask=17;
            foreach(var f in source)Object.DestroyImmediate(f.gameObject);
        }
    }
    static Transform Place(Transform template,string type,string id,float x,float z,float heading,string photos,float y=Floor)
    {
        var p=Object.Instantiate(template.gameObject,TypeGroup(type)).transform;p.name=id;p.position=W(x,y,z);p.rotation=Quaternion.LookRotation(W(Mathf.Sin(heading*Mathf.Deg2Rad),0,-Mathf.Cos(heading*Mathf.Deg2Rad))-W(0,0,0));
        inventory.Add(FormattableString.Invariant($"{id}|{type}|{photos}|{x:F3}|{z:F3}|{y:F3}|{heading:F0}|SUPUESTO"));return p;
    }
    static Transform Table(string key,float length,float depth,bool white=false)
    {
        var t=Group(key,templates);Box(t,"Tablero",new Vector3(0,.737f,0),new Vector3(length,.036f,depth),white?"Papel":"Madera");Box(t,"Faldon",new Vector3(0,.65f,0),new Vector3(length-.12f,.10f,.035f),"Negro");
        foreach(float x in new[]{-length/2+.16f,length/2-.16f}){Box(t,"Columna",new Vector3(x,.37f,0),new Vector3(.075f,.70f,.15f),"Negro");Rounded(t,"Pie_T",new Vector3(x,.035f,0),.09f,depth+.02f,.045f,.04f,"Negro",Quaternion.Euler(90,0,0));ColliderBox(t,new Vector3(x,.365f,0),new Vector3(.09f,.73f,depth+.02f));}
        ColliderBox(t,new Vector3(0,.737f,0),new Vector3(length,.036f,depth));Combine(t,key);return t;
    }
    static Transform OfficeChair(string key,bool back=true,bool tilted=false)
    {
        var t=Group(key,templates);Cylinder(t,"Piston",new Vector3(0,.25f,0),.027f,.33f,"Metal");
        for(int i=0;i<5;i++){float a=i*Mathf.PI*2/5;var end=new Vector3(Mathf.Sin(a)*.245f,.08f,Mathf.Cos(a)*.245f);Segment(t,"Radio",new Vector3(0,.13f,0),end,.035f,"Negro");var wheel=Cylinder(t,"Rueda",end+Vector3.down*.035f,.035f,.038f,"Negro");wheel.transform.localRotation=Quaternion.Euler(0,i*72,90);}
        Rounded(t,"Asiento",new Vector3(0,.465f,0),.46f,.44f,.075f,.065f,back?"Azul":"Negro",Quaternion.Euler(90,0,0));
        if(back)
        {
            Segment(t,"Soporte_Respaldo",new Vector3(0,.40f,-.18f),new Vector3(0,.70f,-.22f),.035f,"Negro");
            var rot=Quaternion.Euler(tilted?-30:-8,0,0);Rounded(t,"Carcasa",new Vector3(0,.735f,-.235f),.435f,.39f,.05f,.07f,"Negro",rot);Rounded(t,"Tapiz",new Vector3(0,.735f,-.205f),.42f,.375f,.045f,.067f,"Azul",rot);
            foreach(float side in new[]{-1f,1f}){Segment(t,"Apoyo_Brazo",new Vector3(side*.20f,.42f,.01f),new Vector3(side*.25f,.62f,.01f),.025f,"Negro");Rounded(t,"Reposabrazos",new Vector3(side*.25f,.64f,.015f),.065f,.26f,.035f,.03f,"Negro",Quaternion.Euler(90,0,0));}
        }
        var col=t.gameObject.AddComponent<CapsuleCollider>();col.center=new Vector3(0,back?.47f:.26f,0);col.radius=back?.29f:.23f;col.height=back?.94f:.52f;Combine(t,key);return t;
    }
    static Transform SchoolChair()
    {
        var t=Group("Silla_Escolar",templates);Rounded(t,"Asiento",new Vector3(0,.445f,0),.43f,.42f,.026f,.025f,"Madera",Quaternion.Euler(90,0,0));Rounded(t,"Respaldo",new Vector3(0,.73f,-.20f),.43f,.25f,.026f,.018f,"Madera",Quaternion.identity);
        foreach(float x in new[]{-.20f,.20f})foreach(float z in new[]{-.18f,.18f})Segment(t,"Pata",new Vector3(x,0,z),new Vector3(x,.45f,z),.021f,"Negro");
        foreach(float x in new[]{-.20f,.20f}){Segment(t,"Respaldo_Montante",new Vector3(x,.42f,-.20f),new Vector3(x,.85f,-.20f),.021f,"Negro");Segment(t,"Parrilla_Lateral",new Vector3(x,.20f,-.18f),new Vector3(x,.20f,.18f),.015f,"Negro");}
        for(int i=0;i<4;i++)Segment(t,"Parrilla",new Vector3(-.2f,.20f,-.15f+i*.1f),new Vector3(.2f,.20f,-.15f+i*.1f),.012f,"Negro");ColliderBox(t,new Vector3(0,.43f,0),new Vector3(.45f,.86f,.46f));Combine(t,"Silla_Escolar");return t;
    }
    static Transform Screen(string key,float width,float height,bool stand)
    {
        var t=Group(key,templates);float y=stand?.29f:0;
        Rounded(t,"Carcasa",new Vector3(0,y,0),width,height,.055f,.018f,"Negro",Quaternion.identity);
        Box(t,"Pantalla_Apagada",new Vector3(0,y,.030f),new Vector3(width-.035f,height-.035f,.005f),"Pantalla");
        if(stand){Box(t,"Peana",new Vector3(0,.012f,0),new Vector3(.23f,.025f,.16f),"Negro");Box(t,"Pie",new Vector3(0,.09f,-.005f),new Vector3(.04f,.16f,.035f),"Negro");}
        Combine(t,key);return t;
    }
    static Transform Tower()
    {
        var t=Group("Torre",templates);Box(t,"Caja",new Vector3(0,.20f,0),new Vector3(.19f,.40f,.38f),"Negro");Box(t,"Frontal",new Vector3(0,.20f,.194f),new Vector3(.17f,.38f,.012f),"Metal");
        for(int i=0;i<3;i++)Box(t,"Bahia",new Vector3(0,.32f-i*.035f,.201f),new Vector3(.14f,.023f,.005f),"Negro");Cylinder(t,"Boton",new Vector3(0,.19f,.205f),.008f,.006f,"MetalClaro").transform.localRotation=Quaternion.Euler(90,0,0);
        for(int i=0;i<7;i++)Box(t,"Ventilacion",new Vector3(0,.06f+i*.012f,.201f),new Vector3(.13f,.003f,.004f),"Negro");Combine(t,"Torre");return t;
    }
    static Transform Printer()
    {
        var t=Group("Impresora",templates);Box(t,"Cuerpo",new Vector3(0,.19f,0),new Vector3(.50f,.38f,.47f),"Papel");Box(t,"BaseOscura",new Vector3(0,.035f,0),new Vector3(.48f,.07f,.46f),"Negro");Box(t,"Scanner",new Vector3(0,.405f,-.04f),new Vector3(.49f,.055f,.40f),"Negro");Box(t,"Bandeja",new Vector3(0,.455f,-.05f),new Vector3(.38f,.018f,.26f),"Negro");Box(t,"Salida",new Vector3(0,.30f,.238f),new Vector3(.39f,.07f,.009f),"Negro");Box(t,"Cajon",new Vector3(0,.12f,.241f),new Vector3(.45f,.14f,.008f),"Crema");Combine(t,"Impresora");return t;
    }
    static Transform Cabinet()
    {
        var t=Group("Archivero",templates);Box(t,"Cuerpo",new Vector3(0,.64f,0),new Vector3(.49f,1.28f,.60f),"Beige");for(int i=0;i<4;i++){float y=.17f+i*.31f;Box(t,"Cajon",new Vector3(0,y,.306f),new Vector3(.45f,.28f,.015f),"Beige");Box(t,"Tirador",new Vector3(0,y+.09f,.329f),new Vector3(.20f,.025f,.035f),"Metal");Box(t,"PortaEtiqueta",new Vector3(-.145f,y+.03f,.319f),new Vector3(.075f,.035f,.007f),"MetalClaro");}ColliderBox(t,new Vector3(0,.64f,0),new Vector3(.49f,1.28f,.63f));Combine(t,"Archivero");return t;
    }
    static Transform Shelf()
    {
        var t=Group("Estante",templates);foreach(float x in new[]{-.475f,.475f})foreach(float z in new[]{-.205f,.205f})Box(t,"Montante",new Vector3(x,1.075f,z),new Vector3(.035f,2.15f,.035f),"MetalClaro");for(int i=0;i<7;i++){float y=.06f+i*.345f;Box(t,"Repisa",new Vector3(0,y,0),new Vector3(.97f,.025f,.44f),"MetalClaro");Box(t,"Borde",new Vector3(0,y-.015f,.213f),new Vector3(.97f,.04f,.014f),"MetalClaro");}ColliderBox(t,new Vector3(0,1.075f,0),new Vector3(1,2.15f,.45f));Combine(t,"Estante");return t;
    }
    static void Cable(Transform p,string id,Vector3[] points,string mat="Negro",float diameter=.008f)
    {for(int i=0;i<points.Length-1;i++)Segment(p,id+"_"+i,points[i],points[i+1],diameter,mat);}
    static Transform NetworkRack()
    {
        var t=Group("Rack_Red",templates);Box(t,"Fondo",new Vector3(0,0,-.18f),new Vector3(.55f,.68f,.035f),"Negro");
        foreach(float x in new[]{-.265f,.265f})Box(t,"Lateral",new Vector3(x,0,0),new Vector3(.025f,.68f,.40f),"Negro");foreach(float y in new[]{-.33f,.33f})Box(t,"Tapa",new Vector3(0,y,0),new Vector3(.55f,.025f,.40f),"Negro");
        Box(t,"Switch",new Vector3(0,.23f,.06f),new Vector3(.46f,.06f,.22f),"Metal");for(int i=0;i<12;i++)Box(t,"Puerto",new Vector3(-.20f+i*.036f,.23f,.177f),new Vector3(.021f,.017f,.008f),"Negro");
        for(int i=0;i<6;i++)Cable(t,"Cable_Azul",new[]{new Vector3(-.18f+i*.045f,.21f,.18f),new Vector3(-.19f+i*.035f,-.11f,.14f),new Vector3(-.08f+i*.035f,-.25f,.08f),new Vector3(.16f,-.21f,.02f)},"Azul",.007f);
        foreach(float x in new[]{-.245f,.245f})Box(t,"Marco_Frontal",new Vector3(x,0,.205f),new Vector3(.024f,.65f,.02f),"Negro");foreach(float y in new[]{-.31f,.31f})Box(t,"Marco_Frontal",new Vector3(0,y,.205f),new Vector3(.50f,.024f,.02f),"Negro");Combine(t,"Rack_Red");return t;
    }
    static Transform Carton()
    {
        var t=Group("Caja_Carton",templates);Box(t,"Caja",Vector3.zero,Vector3.one,"Beige");Box(t,"Cinta",new Vector3(0,.501f,0),new Vector3(.13f,.003f,1),"Madera");Box(t,"Etiqueta_Sin_Texto",new Vector3(.22f,0,.502f),new Vector3(.26f,.23f,.003f),"Papel");Combine(t,"Caja_Carton");return t;
    }
    static void Build()
    {
        templates=Group("TEMP_Plantillas",root);inventory.Add("id|tipo|foto|x_plano|z_plano|Y|orientacion_grados_desde_norte|certeza");
        var table=Table("Mesa_Central",1.55f,.65f);var ret=Table("Mesa_Retorno",1.20f,.65f);var pcTable=Table("Mesa_PC",1.35f,.65f);var northWhite=Table("Mesa_Norte_Clara",1.25f,.65f,true);
        for(int row=0;row<2;row++)for(int j=0;j<4;j++)Place(table,"Mesas_Centrales",$"Mesa_Central_{row+1}_{j+1}",3.075f+j*1.55f,row==0?25.45f:28.05f,0,"F01 F08 F12");
        Place(ret,"Mesas_Retorno","Retorno_Oeste_1",1.95f,25.85f,90,"F01 F03");Place(ret,"Mesas_Retorno","Retorno_Oeste_2",1.95f,28.25f,90,"F01 F12");
        for(int i=0;i<4;i++)Place(pcTable,"Mesas_Sur","Mesa_Ordenadores_Sur_"+(i+1),2.7f+i*2,30.35f,180,"F11 F19");
        Place(pcTable,"Mesas_Norte","Mesa_Norte_Madera",2.35f,23.70f,0,"F13 F20");Place(northWhite,"Mesas_Norte","Mesa_Norte_Clara",3.65f,23.70f,0,"F13 F20");
        var chair=OfficeChair("Silla_Oficina_Azul");var tilt=OfficeChair("Silla_Respaldo_Inclinado",true,true);var backless=OfficeChair("Silla_Negra_Sin_Respaldo",false);int number=0;
        for(int i=0;i<8;i++)Place(chair,"Sillas_Giratorias","Silla_Azul_"+(++number).ToString("D2"),2.6875f+i*.775f,26.15f,i%3==0?-8:i%3==1?7:0,"F01 F12");
        for(int i=0;i<6;i++)Place(i==4?tilt:chair,"Sillas_Giratorias","Silla_Azul_"+(++number).ToString("D2"),2.8f+i,28.78f,i%2==0?10:-12,"F12 F18 F19");
        foreach(float x in new[]{2.7f,6.7f,8.5f})Place(chair,"Sillas_Giratorias","Silla_Azul_"+(++number).ToString("D2"),x,29.65f,180,"F11 F19");
        Place(chair,"Sillas_Giratorias","Silla_Azul_"+(++number).ToString("D2"),2.35f,24.40f,0,"F13");
        Place(backless,"Silla_Sin_Respaldo","Asiento_Negro_Sin_Respaldo_Visible",8.55f,25.85f,10,"F10 F17");
        var school=SchoolChair();Place(school,"Sillas_Escolares","Escolar_Apilada_Inferior",9.13f,23.65f,180,"F14 F17");Place(school,"Sillas_Escolares","Escolar_Apilada_Superior",9.13f,23.65f,180,"F14 F17",Floor+.13f);
        Place(school,"Sillas_Escolares","Escolar_Este",10.82f,27.5f,270,"F09");Place(school,"Sillas_Escolares","Escolar_Sur",5.35f,30.20f,0,"F16");
        var tv=Screen("TV_130cm",1.30f,.76f,false);var tn=Place(tv,"Televisores","TV_Norte",4.75f,23.30f,180,"F01 F20",5.86f);tn.Rotate(8,0,0,Space.Self);var ts=Place(tv,"Televisores","TV_Sur",5.40f,30.67f,0,"F11 F16",5.88f);ts.Rotate(8,0,0,Space.Self);
        var monitor=Screen("Monitor_48cm",.48f,.31f,true);var tower=Tower();
        Place(monitor,"Monitores","Monitor_Norte",2.20f,23.67f,180,"F13",Floor+.755f);
        for(int i=0;i<4;i++){var m=Place(monitor,"Monitores","Monitor_Sur_"+(i+1),2.48f+i*2,30.29f,(i%2==0?-12:12),"F11 F19",Floor+.755f);if(i==1)m.Rotate(0,0,-6,Space.Self);Place(tower,"Torres_PC","Torre_Sur_"+(i+1),2.98f+i*2,30.39f,i%2==0?180:0,"F11 F19",Floor+.755f);}
        Place(tower,"Torres_PC","Torre_Bajo_Mesa_Norte",3.95f,23.70f,180,"F17",Floor);
        Place(Printer(),"Impresora","Impresora_Multifuncion",3.62f,23.70f,180,"F13",Floor+.755f);
        Place(Cabinet(),"Archivero","Archivero_Cuatro_Cajones",.80f,24.79f,90,"F13");
        Place(NetworkRack(),"Rack_Red","Rack_Mural_Cables_Azules",.60f,23.98f,90,"F13 F17",4.91f);
        Place(Shelf(),"Estante","Estante_Siete_Niveles",10.90f,25.20f,270,"F07 F15");
        var carton=Carton();
        var cartons=new[]{new Vector4(10.88f,24.46f,Floor+.25f,.50f),new Vector4(10.88f,24.46f,Floor+.61f,.22f),new Vector4(10.88f,25.22f,Floor+.82f,.17f),new Vector4(10.86f,25.92f,Floor+.16f,.32f),new Vector4(10.86f,25.92f,Floor+.46f,.28f),new Vector4(.80f,24.79f,Floor+1.44f,.32f)};
        for(int i=0;i<cartons.Length;i++){var q=cartons[i];var p=Place(carton,"Cajas_Carton","Caja_"+(i+1),q.x,q.y,i==5?90:270,i==5?"F13":"F15",q.z);p.localScale=new Vector3(i==2?.70f:.38f,q.w,i==2?.30f:.38f);}
        Accessories();ProjectorScreen();ProjectorCage();StorageProps();
        Object.DestroyImmediate(templates.gameObject);templates=null;
    }
    static Transform Custom(string type,string id,float x,float y,float z,float heading,string photos)
    {var tmp=Group(id,templates);var p=Place(tmp,type,id,x,z,heading,photos,y);Object.DestroyImmediate(tmp.gameObject);return p;}
    static void Accessories()
    {
        var p=Custom("Perifericos","Teclado_Norte",2.30f,Floor+.773f,23.99f,180,"F13");Box(p,"Base",Vector3.zero,new Vector3(.43f,.022f,.15f),"Negro");for(int r=0;r<4;r++)for(int c=0;c<13;c++)Box(p,"Tecla",new Vector3(-.185f+c*.03f,.014f,-.053f+r*.032f),new Vector3(.023f,.007f,.025f),"Metal");Combine(p,"Teclado");
        for(int i=0;i<2;i++){p=Custom("Perifericos","Raton_"+(i+1),i==0?2.67f:7.95f,Floor+.78f,i==0?23.98f:25.47f,0,i==0?"F13":"F08");Rounded(p,"Cuerpo",Vector3.zero,.065f,.11f,.035f,.03f,"Negro",Quaternion.Euler(90,0,0));Cable(p,"Cable",new[]{new Vector3(0,0,.05f),new Vector3(.1f,-.015f,.19f),new Vector3(.24f,-.015f,.12f),new Vector3(.15f,-.015f,-.04f),new Vector3(-.08f,-.015f,.10f)},"Negro",.004f);Combine(p,"Raton_"+i);}
        p=Custom("Perifericos","Control_Remoto",3.08f,Floor+.78f,23.97f,15,"F13");Rounded(p,"Cuerpo",Vector3.zero,.047f,.16f,.021f,.018f,"Papel",Quaternion.Euler(90,0,0));for(int i=0;i<5;i++)Box(p,"Boton",new Vector3(0,.014f,-.05f+i*.023f),new Vector3(.02f,.005f,.01f),i==0?"Rojo":"Metal");Combine(p,"Remoto");
        var cables=Custom("Cables_Decorativos","Dos_Recorridos_Suelo",0,0,0,0,"F07 F18");cables.position=Vector3.zero;cables.rotation=Quaternion.identity;
        var paths=new[]{new[]{W(7.8f,3.615f,25.4f),W(8.2f,3.615f,25.9f),W(8.0f,3.615f,26.25f),W(7.65f,3.615f,26.17f),W(7.7f,3.615f,25.92f),W(8.1f,3.615f,25.98f)},new[]{W(6.7f,4.4f,30.6f),W(6.7f,3.62f,30.55f),W(6.2f,3.615f,30.05f),W(6.45f,3.615f,29.98f),W(7.1f,3.615f,30.40f)}};
        for(int i=0;i<paths.Length;i++)Cable(cables,"Cable_Suelo_"+i,paths[i]);Combine(cables,"Cables_Suelo");
    }
    static void ProjectorScreen()
    {
        var p=Custom("Pantalla_Proyeccion","Pantalla_Desplegada",7.0f,5.625f,23.34f,180,"F01 F20");
        Box(p,"Tela_Blanca",Vector3.zero,new Vector3(2.0f,1.95f,.009f),"Papel");foreach(float x in new[]{-1.01f,1.01f})Box(p,"Borde_Negro",new Vector3(x,0,0),new Vector3(.022f,1.97f,.012f),"Negro");
        Box(p,"Carcasa_Enrollador",new Vector3(0,1.015f,0),new Vector3(2.13f,.085f,.085f),"Papel");Box(p,"Barra_Inferior",new Vector3(0,-.99f,0),new Vector3(2.06f,.025f,.025f),"Negro");Cable(p,"Cordel",new[]{new Vector3(0,-1,0),new Vector3(0,-1.28f,.01f)},"Beige",.004f);Combine(p,"Pantalla_Proyeccion");
    }
    static void ProjectorCage()
    {
        var p=Custom("Proyector","Proyector_Jaula",6.0625f,6.055f,26.70f,0,"F11 F16");
        Box(p,"Base_Jaula",new Vector3(0,.015f,0),new Vector3(.62f,.03f,.47f),"MetalClaro");
        foreach(float x in new[]{-.30f,.30f})foreach(float z in new[]{-.225f,.225f})Box(p,"Montante",new Vector3(x,.18f,z),new Vector3(.018f,.36f,.018f),"MetalClaro");
        foreach(float z in new[]{-.225f,.225f}){Box(p,"Travesano",new Vector3(0,.35f,z),new Vector3(.62f,.018f,.018f),"MetalClaro");foreach(float x in new[]{-.15f,0,.15f})Box(p,"Barrote",new Vector3(x,.18f,z),new Vector3(.012f,.32f,.012f),"MetalClaro");}
        foreach(float x in new[]{-.30f,.30f})Box(p,"Travesano_Lateral",new Vector3(x,.35f,0),new Vector3(.018f,.018f,.47f),"MetalClaro");
        Box(p,"Proyector",new Vector3(0,.11f,0),new Vector3(.40f,.15f,.29f),"Papel");var lens=Cylinder(p,"Lente",new Vector3(-.11f,.12f,.158f),.043f,.03f,"Negro");lens.transform.localRotation=Quaternion.Euler(90,0,0);
        Cylinder(p,"Optica",new Vector3(-.11f,.12f,.176f),.033f,.007f,"Pantalla").transform.localRotation=Quaternion.Euler(90,0,0);
        for(int i=0;i<8;i++)Box(p,"Rejilla_Equipo",new Vector3(.045f+i*.014f,.12f,.149f),new Vector3(.005f,.065f,.005f),"Negro");
        Cable(p,"Cable_Equipo",new[]{new Vector3(.10f,.15f,-.15f),new Vector3(.12f,.30f,-.19f),new Vector3(0,.35f,0)},"Negro",.007f);Combine(p,"Proyector_Jaula");
    }
    static void StorageProps()
    {
        var p=Custom("Almacenamiento_Accesorios","Caja_Plastica_Superior",10.90f,Floor+2.19f,25.20f,270,"F15");Box(p,"Fondo",Vector3.zero,new Vector3(.47f,.02f,.30f),"Negro");foreach(float z in new[]{-.15f,.15f}){for(int i=0;i<8;i++)Box(p,"Barra",new Vector3(-.23f+i*.066f,.10f,z),new Vector3(.009f,.20f,.009f),"Metal");Box(p,"Borde",new Vector3(0,.20f,z),new Vector3(.48f,.012f,.012f),"Metal");}foreach(float x in new[]{-.235f,.235f})Box(p,"BordeLateral",new Vector3(x,.20f,0),new Vector3(.012f,.012f,.31f),"Metal");for(int i=0;i<3;i++)Box(p,"Contenido_Parcial",new Vector3(-.12f+i*.12f,.055f,0),new Vector3(.08f,.09f,.16f),"VerdeAzulado");Combine(p,"Caja_Plastica");
        p=Custom("Almacenamiento_Accesorios","Barras_Perfiles_SUPUESTO",10.90f,Floor,25.20f,270,"F07 F15");for(int i=0;i<4;i++)Box(p,"Perfil",new Vector3(-.10f,1.13f+i*.017f,-.11f+i*.045f),new Vector3(1.05f,.018f,.018f),"MetalClaro");Combine(p,"Perfiles_Estante");
        p=Custom("Almacenamiento_Accesorios","Piezas_Apoyadas",10.98f,Floor,24.46f,270,"F14 F15");for(int i=0;i<2;i++)Segment(p,"Barra",new Vector3(.05f+i*.04f,0,-.10f),new Vector3(.28f+i*.04f,1.50f,-.13f),.025f,i==0?"Papel":"Madera");Combine(p,"Piezas_Apoyadas");
        p=Custom("Bote_Basura","Recipiente_Bolsa_Negra",10.38f,Floor,24.52f,270,"F14 F15");Box(p,"Recipiente",new Vector3(0,.17f,0),new Vector3(.29f,.34f,.28f),"Negro");Rounded(p,"Borde_Bolsa",new Vector3(0,.35f,0),.31f,.30f,.025f,.035f,"Negro",Quaternion.Euler(90,0,0));Box(p,"Papel_Visible",new Vector3(.03f,.36f,0),new Vector3(.13f,.035f,.15f),"Papel");ColliderBox(p,new Vector3(0,.18f,0),new Vector3(.31f,.36f,.30f));Combine(p,"Bote_Bolsa");
    }
    static Dictionary<string,string> Snapshot()
    {
        var d=new Dictionary<string,string>();foreach(var g in aula.gameObject.scene.GetRootGameObjects())foreach(var t in g.GetComponentsInChildren<Transform>(true))
        {if(t==aula||(root&&(t==root||t.IsChildOf(root))))continue;foreach(var o in new Object[]{t.gameObject}.Concat(t.GetComponents<Component>().Cast<Object>()))if(o)d[GlobalObjectId.GetGlobalObjectIdSlow(o).ToString()]=EditorJsonUtility.ToJson(o);}return d;
    }
    static void Check(Dictionary<string,string> before)
    {var now=Snapshot();var diff=before.Keys.Where(k=>!now.ContainsKey(k)||now[k]!=before[k]).Concat(now.Keys.Except(before.Keys)).ToArray();File.WriteAllText(Out+"/auditoria_ambito.txt",$"Registros protegidos: {before.Count}; cambios externos: {diff.Length}\n"+string.Join("\n",diff));if(diff.Length>0)throw new Exception("Cambios fuera del mobiliario");}
    static void AuditGeometry()
    {
        var log=new List<string>();var cols=root.GetComponentsInChildren<Collider>();
        void Corridor(string name,float x0,float x1,float z0,float z1)
        {
            var a=W(x0,3.61f,z0);var b=W(x1,5.22f,z1);var path=new Bounds((a+b)*.5f,new Vector3(Mathf.Abs(a.x-b.x),1.61f,Mathf.Abs(a.z-b.z)));
            var hit=cols.Where(c=>{var cb=c.bounds;return cb.max.x>path.min.x+.002f&&cb.min.x<path.max.x-.002f&&cb.max.z>path.min.z+.002f&&cb.min.z<path.max.z-.002f&&cb.max.y>path.min.y+.002f&&cb.min.y<path.max.y-.002f;}).Select(c=>PathOf(c.transform)).ToArray();
            log.Add(name+": "+(x1-x0).ToString("F2")+" x "+(z1-z0).ToString("F2")+" m; obstaculos nuevos="+hit.Length);log.AddRange(hit);if(hit.Length>0)throw new Exception("Pasillo bloqueado "+name+": "+string.Join(";",hit));
        }
        Corridor("Pasillo central",1.58f,10.10f,26.45f,27.65f);Corridor("Pasillo este",8.90f,10.10f,24.15f,30.00f);Corridor("Paso oeste",.38f,1.58f,25.30f,30.0f);Corridor("Frente puerta",9.78f,10.74f,23.215f,24.115f);
        var door=aula.GetComponentInChildren<Docencia2InteractiveDoor>();var saved=EditorJsonUtility.ToJson(door);var rotation=door.transform.localRotation;var collider=door.GetComponent<BoxCollider>();var collisions=new List<string>();
        try{for(int a=0;a<=90;a+=5){door.transform.localRotation=door.closedRotation*Quaternion.Euler(0,-a,0);Physics.SyncTransforms();foreach(var c in cols)if(Physics.ComputePenetration(collider,collider.transform.position,collider.transform.rotation,c,c.transform.position,c.transform.rotation,out var direction,out float depth)&&depth>.003f)collisions.Add(c.name+" @ "+a);}}
        finally{EditorJsonUtility.FromJsonOverwrite(saved,door);door.transform.localRotation=rotation;Physics.SyncTransforms();}
        log.Add("Barrido puerta contra mobiliario nuevo: "+collisions.Count);log.AddRange(collisions);if(collisions.Count>0)throw new Exception("Mobiliario invade apertura");
        var outside=new List<string>();foreach(var r in root.GetComponentsInChildren<Renderer>()){var b=r.bounds;float xMin=(14.25f-b.max.z)*28.48f/28.5f,xMax=(14.25f-b.min.z)*28.48f/28.5f,zMin=(27-b.max.x)*53.92f/54,zMax=(27-b.min.x)*53.92f/54;if(xMin<.35f||xMax>11.167f||zMin<23.215f||zMax>30.787f||b.min.y<3.60f||b.max.y>6.8f)outside.Add(PathOf(r.transform));}
        log.Add("Renderers fuera del volumen del aula: "+outside.Count);log.AddRange(outside);File.WriteAllLines(Out+"/auditoria_geometria.txt",log);if(outside.Count>0)throw new Exception("Objetos fuera del aula: "+string.Join(";",outside));
    }
    public static void Run()
    {
        try
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new Exception("Fuera de Play requerido");Directory.CreateDirectory(Dir);Directory.CreateDirectory(Out);AssetDatabase.Refresh();EditorSceneManager.OpenScene(ScenePath);
            aula=GameObject.Find("PA_Interior/Aula_17")?.transform;if(!aula)throw new Exception("Aula17 no existe");root=aula.Find(Name);if(root)throw new Exception("Mobiliario ya existe; no duplicar");var before=Snapshot();root=Group(Name,aula);
            Build();Physics.SyncTransforms();AuditGeometry();Check(before);AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(aula.gameObject.scene);EditorSceneManager.SaveScene(aula.gameObject.scene);
            File.WriteAllLines(Out+"/Inventario_Colocado_SUPUESTO.csv",inventory);Capture();Check(before);
            File.WriteAllText(Out+"/completado.txt",$"2d guardada. Objetos {root.GetComponentsInChildren<Transform>(true).Length}; renderers {root.GetComponentsInChildren<Renderer>(true).Length}; colliders {root.GetComponentsInChildren<Collider>(true).Length}. FPS pendiente Fase3.\n"+DateTime.Now.ToString("O"));EditorApplication.Exit(0);
        }
        catch(Exception e){Directory.CreateDirectory(Out);File.WriteAllText(Out+"/error.txt",e.ToString());Debug.LogException(e);EditorApplication.Exit(1);}
    }
    public static void CaptureSaved()
    {try{Directory.CreateDirectory(Out);EditorSceneManager.OpenScene(ScenePath);aula=GameObject.Find("PA_Interior/Aula_17").transform;root=aula.Find(Name);var before=Snapshot();Capture();Check(before);EditorApplication.Exit(0);}catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}}
    static void Capture()
    {
        var tmp=new GameObject("TEMP_A17_2d_Camara");var c=tmp.AddComponent<Camera>();c.enabled=false;c.nearClipPlane=.035f;c.farClipPlane=60;c.clearFlags=CameraClearFlags.SolidColor;c.backgroundColor=Color.gray;var data=tmp.AddComponent<UniversalAdditionalCameraData>();data.renderPostProcessing=false;data.renderShadows=false;
        var temps=new List<GameObject>{tmp};var oldMode=RenderSettings.ambientMode;var oldAmbient=RenderSettings.ambientLight;var oldSky=RenderSettings.ambientSkyColor;var oldEquator=RenderSettings.ambientEquatorColor;var oldGround=RenderSettings.ambientGroundColor;bool oldFog=RenderSettings.fog;var oldSun=RenderSettings.sun;
        var kg=new GameObject("TEMP_A17_2d_Luz");temps.Add(kg);var key=kg.AddComponent<Light>();key.type=LightType.Directional;key.intensity=.85f;key.shadows=LightShadows.None;
        RenderSettings.sun=key;RenderSettings.fog=false;RenderSettings.ambientMode=AmbientMode.Flat;RenderSettings.ambientLight=new Color(.3f,.3f,.3f);
        var rt=new RenderTexture(1600,1100,24);rt.antiAliasing=2;var prev=RenderTexture.active;c.targetTexture=rt;
        try
        {
            void Shot(string name,Vector3 from,Vector3 to,float fov=75)
            {c.fieldOfView=fov;c.transform.position=from;c.transform.LookAt(to);key.transform.rotation=c.transform.rotation;var tex=new Texture2D(1600,1100,TextureFormat.RGB24,false);for(int i=0;i<4;i++){c.Render();RenderTexture.active=rt;tex.ReadPixels(new Rect(0,0,1600,1100),0,0);tex.Apply();}File.WriteAllBytes(Out+"/"+name+".png",tex.EncodeToPNG());Object.DestroyImmediate(tex);}
            Shot("01_General_F01_APROX",W(.95f,5.32f,29.6f),W(7.5f,4.9f,25.7f),82);
            Shot("02_Inversa_F12_APROX",W(10.0f,5.30f,24.4f),W(3.5f,4.90f,27.2f),82);
            Shot("03_Pantalla_Norte_Equipos",W(7.9f,5.24f,25.0f),W(4.1f,5.0f,23.45f),80);
            Shot("04_Estante_F15_APROX",W(8.7f,5.1f,25.3f),W(10.9f,4.75f,25.2f),78);
            Shot("05_Proyector",W(5.1f,5.55f,28.0f),W(6.0625f,6.25f,26.70f),60);
            Shot("06_Sillas_Apiladas_Puerta",W(8.0f,5.0f,25.3f),W(9.7f,4.5f,23.7f),72);
            Shot("07_Equipos_Sur",W(7.8f,5.0f,27.0f),W(5.4f,4.65f,30.5f),82);
        }
        finally{RenderSettings.sun=oldSun;RenderSettings.fog=oldFog;RenderSettings.ambientLight=oldAmbient;RenderSettings.ambientSkyColor=oldSky;RenderSettings.ambientEquatorColor=oldEquator;RenderSettings.ambientGroundColor=oldGround;RenderSettings.ambientMode=oldMode;c.targetTexture=null;RenderTexture.active=prev;rt.Release();Object.DestroyImmediate(rt);foreach(var g in temps)Object.DestroyImmediate(g);}
    }
}
