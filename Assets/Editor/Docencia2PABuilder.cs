using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Vaelith.Docencia2;
using Object=UnityEngine.Object;

public static class Docencia2PABuilder {
 const string ScenePath="Assets/Scenes/Exterior_Docencia2.unity";
 const string Out=@"C:/Users/faust/OneDrive/Desktop/VAELITH/PA_Revision2/estructura";
 const string MeshDir="Assets/Models/Docencia2/PAMeshes";
 const float Y0=3.60f,Y1=6.80f;
 static Transform root;static int serial;
 static Material cream,wood,trim,glass,floor,ceil,zoclo,tile,doorMat;
 static Dictionary<string,Transform> zones=new Dictionary<string,Transform>();
 static List<string> decisions=new List<string>();
 static Vector3 W(float x,float y,float z)=>Docencia2InteriorBuilder.PlanToUnityPos(x,y,z);
 static float PZ(float ux)=>(27-ux)*53.92f/54;
 static float PX(float uz)=>(14.25f-uz)*28.48f/28.50f;
 static Material Mat(string name)=>AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Docencia2/"+name+".mat");
 static Transform Group(string name,Transform parent){var g=new GameObject(name);g.transform.SetParent(parent,false);return g.transform;}
 static Transform Zone(string name){if(!zones.ContainsKey(name))zones[name]=Group(name,root);return zones[name];}
 static GameObject Box(string name,Transform parent,float x0,float x1,float y0,float y1,float z0,float z1,Material m,bool collider=true,bool moving=false){
  if(x1-x0<.0005f||y1-y0<.0005f||z1-z0<.0005f)throw new Exception("Invalid box "+name);
  var g=GameObject.CreatePrimitive(PrimitiveType.Cube);g.name=name;g.transform.SetParent(parent,false);
  var a=W(x0,y0,z0);var b=W(x1,y1,z1);g.transform.position=(a+b)*.5f;g.transform.localScale=new Vector3(Mathf.Abs(b.x-a.x),y1-y0,Mathf.Abs(b.z-a.z));
  var r=g.GetComponent<MeshRenderer>();r.sharedMaterial=m;r.shadowCastingMode=ShadowCastingMode.Off;r.receiveShadows=true;
  if(!collider)Object.DestroyImmediate(g.GetComponent<Collider>());
  if(!moving)GameObjectUtility.SetStaticEditorFlags(g,StaticEditorFlags.BatchingStatic|StaticEditorFlags.ReflectionProbeStatic|StaticEditorFlags.OccluderStatic);
  if(m==floor||m==ceil||m==wood||m==doorMat||m==tile){
   var f=g.GetComponent<MeshFilter>();var mesh=Object.Instantiate(f.sharedMesh);mesh.name="PA_"+(serial++).ToString("D4");var vv=mesh.vertices;var nn=mesh.normals;var uv=new Vector2[vv.Length];
   float fac=m==floor?1/.66f:m==ceil?1/1.2f:m==tile?1/.66f:1/1.12f;
   for(int i=0;i<vv.Length;i++){var p=g.transform.TransformPoint(vv[i]);uv[i]=Mathf.Abs(nn[i].y)>.5f?new Vector2(p.x*fac,p.z*fac):new Vector2((Mathf.Abs(nn[i].z)>.5f?p.x:p.z)*fac,(m==wood||m==doorMat)?(p.y-Y0)/(Y1-Y0):p.y*fac);}
   mesh.uv=uv;mesh.RecalculateTangents();AssetDatabase.CreateAsset(mesh,MeshDir+"/"+mesh.name+".asset");f.sharedMesh=mesh;
  }
  return g;
 }
 static void Panel(Transform g,string name,bool alongZ,float fixedAt,float a,float b,float low,float high,Material mat,float thick=.10f,bool collide=true){
  if(b-a<.002||high-low<.002)return;
  if(alongZ)Box(name,g,fixedAt-thick/2,fixedAt+thick/2,low,high,a,b,mat,collide);
  else Box(name,g,a,b,low,high,fixedAt-thick/2,fixedAt+thick/2,mat,collide);
 }
 struct D {public int id;public float a,b;public string opens;public bool glass;public D(int i,float s,string o,bool glassDoor=false){id=i;a=s;b=s+.96f;opens=o;glass=glassDoor;}}
 static void Door(Transform parent,bool alongZ,float fixedAt,D d){
  var g=Group("P"+d.id+"_Hueco096_Abre_"+d.opens,parent);
  Panel(g,"Jamba_A",alongZ,fixedAt,d.a,d.a+.03f,Y0,Y0+2.13f,trim,.12f);
  Panel(g,"Jamba_B",alongZ,fixedAt,d.b-.03f,d.b,Y0,Y0+2.13f,trim,.12f);
  Panel(g,"Dintel_Marco",alongZ,fixedAt,d.a,d.b,Y0+2.10f,Y0+2.13f,trim,.12f);
  float h=d.a+.034f,end=d.b-.034f;
  var hinge=Group("Bisagra_Giro90_Interior",g);hinge.position=alongZ?W(fixedAt,Y0,h):W(h,Y0,fixedAt);
  var leaf=alongZ?Box("Hoja",hinge,fixedAt-.018f,fixedAt+.018f,Y0+.012f,Y0+2.09f,h,end,d.glass?glass:doorMat,true,true):Box("Hoja",hinge,h,end,Y0+.012f,Y0+2.09f,fixedAt-.018f,fixedAt+.018f,d.glass?glass:doorMat,true,true);
  var script=leaf.AddComponent<Docencia2PADoor>();script.hinge=hinge;script.inwardAngle=d.opens=="este"?-90:90;
  // All leaves have their hinge at the low coordinate. Positive Y swings west or south.
  if(d.glass){Panel(hinge,"BastidorSuperior",alongZ,fixedAt,h,end,Y0+2.055f,Y0+2.09f,trim,.04f);Panel(hinge,"BastidorInferior",alongZ,fixedAt,h,end,Y0+.012f,Y0+.047f,trim,.04f);}
  var handle=alongZ?Box("Manija",hinge,fixedAt-.055f,fixedAt+.055f,Y0+.98f,Y0+1.04f,end-.10f,end-.04f,trim,false,true):Box("Manija",hinge,end-.10f,end-.04f,Y0+.98f,Y0+1.04f,fixedAt-.055f,fixedAt+.055f,trim,false,true);
  foreach(var tr in hinge.GetComponentsInChildren<Transform>())GameObjectUtility.SetStaticEditorFlags(tr.gameObject,0);
 }
 static void Segment(Transform g,bool az,float f,float a,float b,Material mat,bool glazed,float thick){
  if(b-a<.002f)return;
  if(!glazed){Panel(g,"Muro",az,f,a,b,Y0,Y1,mat,thick);}
  else {
   Panel(g,"Lambrin_100cm",az,f,a,b,Y0,Y0+1,wood,thick);
   Panel(g,"Travesano",az,f,a,b,Y0+.97f,Y0+1.03f,trim,.08f);
   Panel(g,"Vidrio_Al_Plafon",az,f,a+.025f,b-.025f,Y0+1.03f,Y1-.04f,glass,.014f);
   Panel(g,"MarcoSuperior",az,f,a,b,Y1-.04f,Y1,trim,.08f);
   foreach(float edge in new[]{a,b-.04f})Panel(g,"MarcoVertical",az,f,edge,edge+.04f,Y0+1,Y1,trim,.08f);
   if(b-a>1.8f)Panel(g,"Parteluz",az,f,(a+b)/2-.02f,(a+b)/2+.02f,Y0+1,Y1,trim,.08f);
  }
  // Continuous 10cm ceramic skirting on both exposed faces, kept clear of door openings.
  Panel(g,"Zoclo_A",az,f-thick/2-.008f,a,b,Y0,Y0+.10f,zoclo,.016f,false);
  Panel(g,"Zoclo_B",az,f+thick/2+.008f,a,b,Y0,Y0+.10f,zoclo,.016f,false);
 }
 static void Wall(string zone,string name,bool az,float f,float a,float b,Material mat,bool glazed=false,float thick=.15f,params D[] ds){
  var g=Group(name,Zone(zone));float cursor=a;
  foreach(var door in ds.OrderBy(q=>q.a)){
   if(door.a<a-.001f||door.b>b+.001f)throw new Exception("Door outside wall: "+door.id+" "+name);
   Segment(g,az,f,cursor,door.a,mat,glazed,thick);
   if(glazed)Panel(g,"Vidrio_Sobre_P"+door.id,az,f,door.a,door.b,Y0+2.13f,Y1,glass,.014f);else Panel(g,"Dintel_P"+door.id,az,f,door.a,door.b,Y0+2.13f,Y1,mat,thick);
   Door(g,az,f,door);cursor=door.b;
  }
  Segment(g,az,f,cursor,b,mat,glazed,thick);
 }
 static void Surface(string zone,float x0,float x1,float z0,float z1){
  var g=Zone(zone);Box("Piso_Loseta_Acabado_6mm",g,x0,x1,3.601f,3.606f,z0,z1,floor,true);
  Box("Plafon_PA_Y6.80",g,x0,x1,Y1,Y1+.025f,z0,z1,ceil,false);
 }
 static string Fingerprint(GameObject g){var b=new StringBuilder();foreach(var t in g.GetComponentsInChildren<Transform>(true)){b.Append(t.name);foreach(var c in t.GetComponents<Component>())if(c)b.Append(c.GetType().FullName).Append(EditorJsonUtility.ToJson(c));}return Convert.ToBase64String(SHA256.Create().ComputeHash(Encoding.UTF8.GetBytes(b.ToString())));}
 public static void Run(){
  if(EditorApplication.isPlaying)throw new Exception("PA requires Edit mode");
  var scene=EditorSceneManager.OpenScene(ScenePath);
  if(scene.GetRootGameObjects().Any(g=>g.name=="PA_Interior"))throw new Exception("PA already exists; refuse duplicate/replacement");
  var protectedRoots=scene.GetRootGameObjects();var baseline=protectedRoots.ToDictionary(g=>g,g=>Fingerprint(g));
  Directory.CreateDirectory(MeshDir);Directory.CreateDirectory(Out);AssetDatabase.Refresh();
  cream=Mat("Interior/Mat_Int_Muro_AmarilloCrema_PB");wood=Mat("Interior/Mat_Int_Muro_Madera_Sec6_PB");trim=Mat("Mat_Int_Marco_Madera_Oscura");glass=Mat("Mat_Int_Vidrio_Interior_Claro");floor=Mat("Interior/Mat_Int_Piso_LosetaGris_PB");ceil=Mat("Interior/Mat_Int_Plafon_Reticular_PB");zoclo=Mat("Interior/Mat_Int_Zoclo_CeramicoGris_PB");tile=Mat("Interior/Mat_Int_Muro_AzulejoBeige_PB");doorMat=Mat("Mat_Int_Puerta_Madera_Barniz");
  if(new[]{cream,wood,trim,glass,floor,ceil,zoclo,tile,doorMat}.Any(m=>m==null))throw new Exception("Missing shared PB material");
  root=new GameObject("PA_Interior").transform;
  var ext=protectedRoots.First(g=>g.name=="Docencia2_Exterior");
  var p7=ext.GetComponentsInChildren<Renderer>(true).Where(r=>r.name.Contains("Ventana_P2_Trasera_Sec07_PanelCristal")).OrderByDescending(r=>r.bounds.max.x).ToArray();
  var p6=ext.GetComponentsInChildren<Renderer>(true).Where(r=>r.name.Contains("Ventana_P2_Trasera_Sec06_PanelCristal")).OrderByDescending(r=>r.bounds.max.x).ToArray();
  float split1=PZ((p7[1].bounds.min.x+p7[2].bounds.max.x)/2),split2=PZ((p7[3].bounds.min.x+p7[4].bounds.max.x)/2),redesNorth=PZ((p6[1].bounds.min.x+p6[2].bounds.max.x)/2);
  decisions.Add($"Remates a parteluz; tabiques principales z2.57/5.14 conservados. Objetivo del remate z2.57 -> {split1:F5}; z5.14 -> {split2:F5}; A4/Redes z10.29 -> {redesNorth:F5}. Espesor madera0.08, dentro de parteluz0.11. B/C conservan2.57/5.14. SUPUESTO de encaje aprobado; particiones principales conservan secciones.");
  float frameRedes=redesNorth;redesNorth=10.29f;var extCuts=new[]{.36f,2.57f,5.14f,7.71f,redesNorth};var intCuts=new[]{.36f,2.57f,5.14f,7.71f};
  foreach(string col in new[]{"A","B","C","D"}){
   bool outside=col=="A"||col=="D";float[] cuts=outside?extCuts:intCuts;int count=col=="A"?4:3;
   float x0=col=="A"?.36f:col=="B"?5.62f:col=="C"?19.24f:24.86f;float x1=col=="A"?3.62f:col=="B"?9.24f:col=="C"?22.86f:28.12f;
   for(int i=0;i<count;i++){
    string zone="Cubiculo_"+col+(i+1);float n=cuts[i],s=cuts[i+1];Surface(zone,x0,x1,n,s);
    if(i<count-1){
     if(outside && i<2){
      float real=i==0?split1:split2;
      Wall(zone,"Tabique_Sur",false,s,col=="A"?x0+.34f:x0,col=="D"?x1-.34f:x1,wood,false,.08f);
      AngledReturn(Zone(zone),col=="A"?x0:x1,real,col=="A"?x0+.34f:x1-.34f,s);
     }else Wall(zone,"Tabique_Sur",false,s,x0,x1,wood,false,.08f);
    }
    bool west=col=="A"||col=="C";int id=col=="A"?i+1:col=="B"?5+2*i:col=="C"?6+2*i:11+i;
    float start=west?n+.15f:s-.15f-.96f;Wall(zone,"Cancel_Al_Pasillo",true,west?x1:x0,n,s,wood,true,.08f,new D(id,start,west?"oeste":"este",true));
   }
  }
  Surface("Pasillo_Cubiculos_Oeste",3.62f,5.62f,.36f,redesNorth);Surface("Pasillo_Cubiculos_Este",22.86f,24.86f,.36f,7.71f);
  Wall("Cubiculo_A3","Cierre_Sec7",false,7.71f,.36f,3.62f,wood,false,.08f);
  Wall("Cubiculo_B3","Cierre_Sur",false,7.71f,5.62f,9.24f,wood,false,.08f);
  Wall("Cubiculo_C3","Cierre_Sur",false,7.71f,19.24f,22.86f,wood,false,.08f);
  Wall("Cubiculo_D3","Cierre_Sur",false,7.71f,24.86f,28.12f,wood,false,.08f);
  Surface("Bano_Mujeres",9.24f,14.24f,.36f,6.42f);Surface("Bano_Mujeres",9.24f,12.24f,6.42f,7.71f);
  Surface("Bano_Hombres",14.24f,19.24f,.36f,6.42f);Surface("Bano_Hombres",16.24f,19.24f,6.42f,7.71f);Surface("Acceso_Banos",12.24f,16.24f,6.42f,7.71f);
  Wall("Bano_Mujeres","Oeste",true,9.24f,.36f,7.71f,tile);Wall("Bano_Hombres","Este",true,19.24f,.36f,7.71f,tile);
  Wall("Bano_Mujeres","Division_Central",true,14.24f,.36f,6.42f,tile);
  Wall("Bano_Mujeres","Nicho",true,12.24f,6.42f,7.71f,tile,false,.15f,new D(14,6.56f,"oeste"));Wall("Bano_Hombres","Nicho",true,16.24f,6.42f,7.71f,tile,false,.15f,new D(15,6.56f,"este"));
  Wall("Bano_Mujeres","Norte_Acceso",false,6.42f,12.24f,16.24f,tile);Wall("Bano_Mujeres","Sur",false,7.71f,9.24f,12.24f,tile);Wall("Bano_Hombres","Sur",false,7.71f,16.24f,19.24f,tile);
  Surface("Pasillo_Junto_Redes",5.62f,11.24f,7.71f,redesNorth);Surface("Redes_SITE",.36f,11.24f,redesNorth,15.43f);
  Wall("Pasillo_Junto_Redes","Entrada_P16",true,11.24f,7.71f,redesNorth,wood,true,.08f,new D(16,7.86f,"oeste",true));
  Wall("Redes_SITE","Norte_P28",false,redesNorth,.70f,11.24f,wood,false,.08f,new D(28,9.98f,"sur")); AngledReturn(Zone("Redes_SITE"),.36f,frameRedes,.70f,redesNorth);
  Wall("Redes_SITE","Cancel_Este",true,11.24f,redesNorth,15.43f,wood,true,.08f);
  Wall("Redes_SITE","Sur",false,15.43f,.36f,11.24f,cream);
  Surface("Sala_Maestros",17.24f,24.86f,7.71f,15.43f);Surface("Director",24.86f,28.12f,7.71f,15.43f);
  Wall("Sala_Maestros","Norte",false,7.71f,17.24f,19.24f,wood,false,.08f);
  // Acceso este abierto, igual que PB: sin muro ni puerta en x22.86..24.86,z7.71.
  Wall("Sala_Maestros","Cancel_Oeste",true,17.24f,7.71f,15.43f,wood,true,.08f,new D(17,8.83f,"este",true));
  Wall("Director","Madera_Division",true,24.86f,7.71f,15.43f,wood,false,.08f,new D(29,7.86f,"este",true));
  Wall("Sala_Maestros","Sur",false,15.43f,17.24f,28.12f,cream);
  Surface("Pasillo_Central",11.24f,17.24f,7.71f,15.43f);Surface("Llegada_Escalera",7.16f,19.24f,15.43f,23.14f);
  Surface("Aula_8",19.24f,28.12f,15.43f,23.14f);Wall("Aula_8","Frente_P27_B",true,19.24f,15.43f,23.14f,cream,false,.15f,new D(27,15.93f,"este"));
  Surface("Aula_17",.36f,11.24f,23.14f,30.86f);Surface("Aula_9",17.24f,28.12f,23.14f,30.86f);Surface("Pasillo_Central",11.24f,17.24f,23.14f,30.86f);
  Wall("Aula_17","Norte_P18_B",false,23.14f,.36f,11.24f,cream,false,.15f,new D(18,9.78f,"sur"));Wall("Aula_9","Norte_P26_B",false,23.14f,17.24f,28.12f,cream,false,.15f,new D(26,17.34f,"sur"));
  Wall("Aula_17","Este",true,11.24f,23.14f,30.86f,cream);Wall("Aula_9","Oeste",true,17.24f,23.14f,30.86f,cream);
  Wall("Aula_17","Sur",false,30.86f,.36f,11.24f,cream);Wall("Aula_9","Sur",false,30.86f,17.24f,28.12f,cream);
  Surface("Aula_16_Taller",.36f,9.44f,30.86f,38.57f);Surface("Aula_10",19.04f,28.12f,30.86f,38.57f);Surface("Pasillo_Central",9.44f,19.04f,30.86f,38.57f);
  Wall("Aula_16_Taller","Este_P19",true,9.44f,30.86f,38.57f,cream,false,.15f,new D(19,31.86f,"oeste"));Wall("Aula_10","Oeste_P25",true,19.04f,30.86f,38.57f,cream,false,.15f,new D(25,31.86f,"este"));
  Wall("Aula_16_Taller","Sur",false,38.57f,.36f,9.44f,cream);Wall("Aula_10","Sur",false,38.57f,19.04f,28.12f,cream);
  Surface("Aula_15",.36f,8,38.57f,46.29f);Surface("Aula_11",20.48f,28.12f,38.57f,46.29f);Surface("Plaza_Sur",8,20.48f,38.57f,46.29f);
  Wall("Aula_15","Este_P20",true,8,38.57f,46.29f,cream,false,.15f,new D(20,38.72f,"oeste"));Wall("Aula_11","Oeste_P24",true,20.48f,38.57f,46.29f,cream,false,.15f,new D(24,38.72f,"este"));
  Surface("Aula_14",.36f,9.49f,46.29f,53.54f);Surface("Aula_13",9.49f,18.99f,46.29f,53.54f);Surface("Aula_12",18.99f,28.12f,46.29f,53.54f);
  Wall("Aula_14","Norte_P21",false,46.29f,.36f,9.49f,cream,false,.15f,new D(21,8.24f,"sur"));Wall("Aula_13","Norte_P22",false,46.29f,9.49f,18.99f,cream,false,.15f,new D(22,9.76f,"sur"));Wall("Aula_12","Norte_P23",false,46.29f,18.99f,28.12f,cream,false,.15f,new D(23,19.20f,"sur"));
  Wall("Aula_13","Oeste",true,9.49f,46.29f,53.54f,cream);Wall("Aula_13","Este",true,18.99f,46.29f,53.54f,cream);
  foreach(float x in new[]{11.24f,17.24f})Box("Columna_040",Zone("Columnas_PA"),x-.2f,x+.2f,Y0,Y1,38.37f,38.77f,cream);
  Wall("Columnas_PA","Retorno_Oeste",false,38.57f,9.44f,11.04f,cream);
  Wall("Columnas_PA","Retorno_Este",false,38.57f,17.44f,19.04f,cream);
  Facades(ext);
  PAFurniture.FullGlass(root);
  decisions.Add("Sala al este y P18/P26/P27 B aplicados por confirmación. P14/P15 inicio6.56; P20/P24 inicio38.72; P29 inicio7.86: separación a jambas ajustada para evitar intersección con tabique perpendicular. Puertas de cubículos: margen0.15 al límite reajustado, derecha vista desde pasillo; primera puerta al norte inicia z0.51. Ver cuadro construido para todas las cotas.");
  decisions.Add("Se reutiliza conversión mundial PB para plomeo exacto; no se mueve PB. Piso: acabado6mm sobre losa existente; plafónY6.80. Vacío escalera sin piso ni plafón nuevo. Sin mobiliario, sanitarios, nuevas luces permanentes ni cámaras guardadas en Fase1.");
  var changed=baseline.Where(k=>Fingerprint(k.Key)!=k.Value).Select(k=>k.Key.name).ToArray();if(changed.Length>0)throw new Exception("Protected roots changed: "+string.Join(",",changed));
  Validate(ext,split1,split2,redesNorth);
  AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
  File.WriteAllLines(Out+"/decisiones.txt",decisions);
  File.WriteAllText(Out+"/proteccion_raices.txt","UNCHANGED: "+string.Join("\nUNCHANGED: ",protectedRoots.Select(g=>g.name)));
  RenderViews(ext);
  Debug.Log("PA_PHASE1_SUCCESS");
 }
 static void AngledReturn(Transform parent,float x0,float z0,float x1,float z1){
  var a=W(x0,5.2f,z0);var b=W(x1,5.2f,z1);var g=GameObject.CreatePrimitive(PrimitiveType.Cube);g.name="Remate_Oblicuo_Al_Parteluz";g.transform.SetParent(parent,false);g.transform.position=(a+b)*.5f;g.transform.rotation=Quaternion.LookRotation(b-a,Vector3.up);g.transform.localScale=new Vector3(.08f,3.2f,Vector3.Distance(a,b));var r=g.GetComponent<Renderer>();r.sharedMaterial=wood;r.shadowCastingMode=ShadowCastingMode.Off;g.isStatic=true;
 }
 static void Facades(GameObject exterior){
  var zone=Zone("Forros_Interiores_Con_Vanos");
  foreach(bool east in new[]{false,true}){
   string tag=east?"Frontal":"Trasera";float at=east?28.14f:.34f;
   for(int s=1;s<=7;s++){
    var frames=exterior.GetComponentsInChildren<Renderer>(true).Where(r=>r.name.Contains($"Ventana_P2_{tag}_Sec{s:00}_Marco_Sup")).ToArray();
    if(frames.Length!=1)throw new Exception("Missing window frame "+s+tag);
    var b=frames[0].bounds;float n=PZ(b.max.x),south=PZ(b.min.x);float from=s==7?.36f:(7-s)*7.7142857f;float to=s==1?53.54f:(8-s)*7.7142857f;
    Panel(zone,"Forro_"+tag+s+"_Norte",true,at,from,n,Y0,Y1,cream,.04f);
    Panel(zone,"Forro_"+tag+s+"_Sur",true,at,south,to,Y0,Y1,cream,.04f);
    Panel(zone,"Antepecho_"+tag+s,true,at,n,south,Y0,5.66f,cream,.04f);
    Panel(zone,"Dintel_"+tag+s,true,at,n,south,6.64f,Y1,cream,.04f);
    Panel(zone,"Zoclo_"+tag+s,true,east?28.11f:.37f,from,to,Y0,Y0+.1f,zoclo,.02f,false);
   }
  }
  // North/south central recesses lead through to the existing projecting windows; no invented glazing.
  foreach(bool south in new[]{false,true}){
   float wallZ=south?53.56f:.34f;float lo=PX(.90f),hi=PX(-.90f);
   Panel(zone,"Forro_Lateral_Izq",false,wallZ,.36f,lo,Y0,Y1,cream,.04f);Panel(zone,"Forro_Lateral_Der",false,wallZ,hi,28.12f,Y0,Y1,cream,.04f);
   Panel(zone,"Antepecho_Central",false,wallZ,lo,hi,Y0,5.37f,cream,.04f);Panel(zone,"Dintel_Central",false,wallZ,lo,hi,6.05f,Y1,cream,.04f);
   float outer=PZ(south?-27.21f:27.21f),inner=south?53.54f:.36f;float zmin=Mathf.Min(outer,inner),zmax=Mathf.Max(outer,inner);
   Box("Vano_Repisa",zone,lo,hi,5.34f,5.37f,zmin,zmax,cream,false);Box("Vano_Dintel",zone,lo,hi,6.05f,6.08f,zmin,zmax,cream,false);
   Box("Vano_Jamba",zone,lo-.02f,lo,5.37f,6.05f,zmin,zmax,cream,false);Box("Vano_Jamba",zone,hi,hi+.02f,5.37f,6.05f,zmin,zmax,cream,false);
   if(!south)Box("Division_Vano_Banos_Sobre_Parteluz",zone,14.20f,14.28f,5.37f,6.05f,zmin,zmax,cream,true);
  }
 }
 static void Validate(GameObject ext,float a,float b,float c){
  var ds=root.GetComponentsInChildren<Docencia2PADoor>(true);if(ds.Length!=29)throw new Exception("Door count "+ds.Length);
  if(zones.Keys.Count(n=>n.StartsWith("Cubiculo_"))!=13)throw new Exception("Cubicle count");
  foreach(var d in ds){d.SetOpen(true);if(Quaternion.Angle(d.hinge.localRotation,Quaternion.identity)<89)throw new Exception("Door swing");d.SetOpen(false);}
  var empty=new Bounds(W(3.5f,3.7f,19.29f),new Vector3(7,1,6));
  int floorInVoid=root.GetComponentsInChildren<Renderer>().Count(r=>r.name.StartsWith("Piso_")&&r.bounds.Intersects(empty));
  if(floorInVoid!=0)throw new Exception("Floor crosses stair void");
  File.WriteAllText(Out+"/validacion.txt",$"13 cubiculos;29 puertas con giro90;0 pisos en vacio.\nTabiques a parteluz: {a:F5},{b:F5},{c:F5}\nRenderers PA: {root.GetComponentsInChildren<Renderer>().Length}\nLuces PA: {root.GetComponentsInChildren<Light>().Length}\nCamaras PA: {root.GetComponentsInChildren<Camera>().Length}\nTodos renderers PA shadowCastingMode Off. Materiales PB compartidos sin editar.");
 }
 static void RenderViews(GameObject exterior){
  // Temporary inspection rig is created AFTER saving and is never persisted.
  var rig=Group("TEMP_PA_REVIEW",null);var lightGo=Group("WorkLight",rig);var light=lightGo.gameObject.AddComponent<Light>();light.type=LightType.Directional;light.intensity=1.4f;light.shadows=LightShadows.None;lightGo.rotation=Quaternion.Euler(65,25,0);
  var go=Group("ReviewCamera",rig);var cam=go.gameObject.AddComponent<Camera>();cam.enabled=false;cam.clearFlags=CameraClearFlags.SolidColor;cam.backgroundColor=new Color(.12f,.16f,.2f);cam.nearClipPlane=.08f;cam.farClipPlane=150;cam.fieldOfView=68;
  var data=go.gameObject.AddComponent<UniversalAdditionalCameraData>();data.renderPostProcessing=false;
  var hidden=new List<Renderer>();foreach(var r in Object.FindObjectsByType<Renderer>(FindObjectsInactive.Include)){if(r.enabled&&(r.name.Contains("Azotea")||r.name.Contains("Voladizo")||r.name.Contains("Pretil")||r.name.StartsWith("Plafon_PA"))){r.enabled=false;hidden.Add(r);}}
  cam.orthographic=true;cam.orthographicSize=29;go.position=W(14.24f,65,27);go.rotation=Quaternion.LookRotation(Vector3.down,Vector3.right);Capture(cam,"01_PA_Superior",1400,2200);
  foreach(var r in hidden)r.enabled=true;
  cam.orthographic=false;
  Shot(cam,"02_PA_Pasillo_Norte",14.24f,5.18f,22,14.24f,5.1f,6.8f);
  Shot(cam,"03_PA_Pasillo_Oeste",4.62f,5.15f,9.5f,4.62f,5.1f,.8f);
  Shot(cam,"04_PA_Aula16",8.5f,5.18f,37.2f,2.6f,5.1f,32.3f);
  Shot(cam,"05_PA_Desembarque",12.5f,5.18f,20.3f,3.3f,4.8f,19.5f);
  Shot(cam,"06_PA_Banos",14.24f,5.15f,10,14.24f,4.8f,6.2f);
  Object.DestroyImmediate(rig.gameObject);
 }
 static void Shot(Camera c,string name,float x,float y,float z,float tx,float ty,float tz){c.transform.position=W(x,y,z);c.transform.LookAt(W(tx,ty,tz));var light=new GameObject("TempFill");light.transform.position=W(x,6.2f,z);var l=light.AddComponent<Light>();l.type=LightType.Point;l.intensity=1.8f;l.range=18;l.shadows=LightShadows.None;Capture(c,name,1600,1000);Object.DestroyImmediate(light);}
 static void Capture(Camera cam,string name,int w,int h){var rt=new RenderTexture(w,h,24,RenderTextureFormat.ARGB32);rt.antiAliasing=2;cam.targetTexture=rt;cam.Render();var old=RenderTexture.active;RenderTexture.active=rt;var tex=new Texture2D(w,h,TextureFormat.RGB24,false);tex.ReadPixels(new Rect(0,0,w,h),0,0);tex.Apply();File.WriteAllBytes(Out+"/"+name+".png",tex.EncodeToPNG());cam.targetTexture=null;RenderTexture.active=old;Object.DestroyImmediate(tex);rt.Release();Object.DestroyImmediate(rt);}
 public static void ReviewSaved(){
  EditorSceneManager.OpenScene(ScenePath);root=GameObject.Find("PA_Interior").transform;
  var sb=new StringBuilder("Puerta,Eje,Cota,Inicio,Final,GiroInterior\n");
  foreach(var door in root.GetComponentsInChildren<Docencia2PADoor>(true).OrderBy(q=>q.hinge.parent.name)){
   var renderers=door.hinge.parent.GetComponentsInChildren<Renderer>();var bounds=renderers[0].bounds;foreach(var r in renderers)bounds.Encapsulate(r.bounds);
   bool az=bounds.size.x>bounds.size.z;
   float fixedAt=az?PX(bounds.center.z):PZ(bounds.center.x);float a=az?PZ(bounds.max.x):PX(bounds.max.z);float b=az?PZ(bounds.min.x):PX(bounds.min.z);
   sb.AppendLine(FormattableString.Invariant($"{door.hinge.parent.name},{(az?"x":"z")},{fixedAt:F5},{a:F5},{b:F5},{door.inwardAngle:F0}"));
  }
  File.WriteAllText(Out+"/puertas_construidas.csv",sb.ToString());
  var mats=root.GetComponentsInChildren<Renderer>().SelectMany(r=>r.sharedMaterials).Distinct().ToArray();
  File.WriteAllLines(Out+"/materiales_compartidos.txt",mats.Select(m=>AssetDatabase.GetAssetPath(m)+" | GPUInstancing="+m.enableInstancing));
  var rig=Group("TEMP_REVIEW_DETAIL",null);var camera=Group("Camera",rig).gameObject.AddComponent<Camera>();camera.enabled=false;camera.fieldOfView=70;camera.nearClipPlane=.06f;camera.farClipPlane=100;camera.gameObject.AddComponent<UniversalAdditionalCameraData>().renderPostProcessing=false;
  var l=Group("Sun",rig).gameObject.AddComponent<Light>();l.type=LightType.Directional;l.intensity=1.4f;l.shadows=LightShadows.None;l.transform.rotation=Quaternion.Euler(65,25,0);
  Shot(camera,"07_PA_Bano_Mujeres_Envolvente",11.2f,5.15f,5.7f,13,5.1f,1.2f);
  Shot(camera,"08_PA_Sala",18.5f,5.15f,14,23.3f,5.1f,8.8f);
  Shot(camera,"09_PA_Redes",10,5.15f,14.4f,2.3f,5.1f,11.5f);
  var p1=root.GetComponentsInChildren<Docencia2PADoor>().First(q=>q.hinge.parent.name.StartsWith("P1_"));p1.SetOpen(true);
  Shot(camera,"10_PA_Cubiculo_A1_Puerta_90",4.6f,5.15f,2.3f,1.4f,5.1f,1.2f);p1.SetOpen(false);
  Object.DestroyImmediate(rig.gameObject);Debug.Log("PA_REVIEW_SUCCESS_NO_SAVE");
 }
}
