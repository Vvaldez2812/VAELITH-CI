using System;using System.IO;using System.Linq;using System.Reflection;using System.Collections.Generic;using UnityEngine;using UnityEditor;using UnityEditor.SceneManagement;using UnityEngine.Rendering;using Vaelith.Docencia2;using Object=UnityEngine.Object;
public static class FaseAResto {
 static string O=>FaseARevision.Output;
 static Vector3 W(float x,float y,float z)=>Docencia2InteriorBuilder.PlanToUnityPos(x,y,z);
 static Material M(string name)=>AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Docencia2/"+name+".mat");
 static string P(Transform t)=>t.parent?P(t.parent)+"/"+t.name:t.name;
 static List<string> log=new List<string>();
 static Bounds BoundsOf(Transform t){var rr=t.GetComponentsInChildren<Renderer>(true);if(rr.Length==0)throw new Exception("No renderer "+P(t));var b=rr[0].bounds;foreach(var r in rr)b.Encapsulate(r.bounds);return b;}
 static void Glass(){
  var clear=M("Mat_FaseA_Vidrio_Cancel_Transparente");var frame=M("Mat_Int_Marco_Ventana_Claro");
  var pa=GameObject.Find("PA_Interior/Sala_Maestros/Cancel_Oeste").transform;
  foreach(var r in pa.GetComponentsInChildren<Renderer>(true)){
   if(r.sharedMaterial.name.Contains("Vidrio"))r.sharedMaterial=clear;
   else if(r.sharedMaterial.name.Contains("Marco"))r.sharedMaterial=frame;
  }
  foreach(var r in GameObject.Find("PA_Interior/Redes_SITE/Cancel_Este").GetComponentsInChildren<Renderer>(true))if(r.sharedMaterial.name.Contains("Vidrio"))r.sharedMaterial=clear;
  var pb=GameObject.Find("PB_Interior/03_Fase3_Carpinteria_y_Vidrios/Canceleria_Interior_Vidrio_Verde").transform;
  // Fixed glass/frame batches also contain the cubicles: isolate only the Sec06 faces.
  foreach(var r in pb.GetComponentsInChildren<MeshRenderer>(true)){
   if(P(r.transform).Contains("Cancel_Vidrio_Sec06")){
    if(r.sharedMaterial.name.Contains("Vidrio"))r.sharedMaterial=clear;
    else if(r.sharedMaterial.name.Contains("Marco"))r.sharedMaterial=frame;
    continue;
   }
   if(!r.name.StartsWith("Comb_Canceleria_PB")||r.GetComponent<MeshFilter>().sharedMesh.name.EndsWith("_FaseA"))continue;
   bool glass=r.sharedMaterial.name.Contains("Vidrio"),metal=r.sharedMaterial.name.Contains("Marco_Puerta_Bronce");if(!glass&&!metal)continue;
   var mf=r.GetComponent<MeshFilter>();var source=mf.sharedMesh;var vv=source.vertices;var tri=source.triangles;var keep=new List<int>();var selected=new List<int>();
   bool In(Vector3 v){v=r.transform.TransformPoint(v);return v.x>=W(0,0,15.24f).x&&v.x<=W(0,0,7.90f).x&&Mathf.Abs(Mathf.Abs(v.z)-W(11.24f,0,0).z)<.09f&&v.y>=.19f&&v.y<=3.21f;}
   for(int i=0;i<tri.Length;i+=3){var list=In(vv[tri[i]])&&In(vv[tri[i+1]])&&In(vv[tri[i+2]])?selected:keep;list.Add(tri[i]);list.Add(tri[i+1]);list.Add(tri[i+2]);}
   if(selected.Count==0)continue;
   var mesh=Object.Instantiate(source);mesh.name=source.name+"_FaseA";mesh.subMeshCount=2;mesh.SetTriangles(keep,0);mesh.SetTriangles(selected,1);mesh.RecalculateBounds();
   string path="Assets/Models/Docencia2/"+mesh.name+".asset";var existing=AssetDatabase.LoadAssetAtPath<Mesh>(path);if(existing){EditorUtility.CopySerialized(mesh,existing);Object.DestroyImmediate(mesh);mesh=existing;}else AssetDatabase.CreateAsset(mesh,path);
   mf.sharedMesh=mesh;r.sharedMaterials=new[]{r.sharedMaterial,glass?clear:frame};log.Add("Cancel PB: "+selected.Count/3+" triangulos aislados para "+(glass?"vidrio claro":"marco claro")+"; cubiculos conservados.");
  }
 }
 static void Entrance(){
  var old=GameObject.Find("PB_Interior/03_Fase3_Carpinteria_y_Vidrios/Ventanas_Exteriores_y_Rejas_PB/Portal_Entrada_Principal_Sec05");
  var parent=old.transform.parent;var layer=old.GetComponentsInChildren<Renderer>()[0].renderingLayerMask;
  Object.DestroyImmediate(old);
  var g=Docencia2Phase3Builder.BuildEntranceGlazedPortal(parent,"Portal_Entrada_Principal_Sec05",28.18f,16.07f,22.51f,.20f,Docencia2InteriorBuilder.Y_WIN_PB_TOP,M("Mat_Int_Marco_Puerta_Bronce"),M("Mat_Int_Vidrio_Interior_Claro"),M("Mat_Int_Franja_Verde_Institucional"),M("Mat_Int_Linea_Guinda_Institucional"),true,0,true);
  foreach(var t in g.GetComponentsInChildren<Transform>()){t.gameObject.layer=8;t.gameObject.isStatic=!P(t).Contains("Pivote_Vaiven");}
  foreach(var r in g.GetComponentsInChildren<Renderer>()){r.renderingLayerMask=layer;r.shadowCastingMode=ShadowCastingMode.Off;}
  int i=1;foreach(var hinge in g.transform.Cast<Transform>().Where(t=>t.name.StartsWith("Pivote_Vaiven")))hinge.name="Pivote_Vaiven_Modulo_"+(i++);
  log.Add("Entrada: seis modulos nominales de 1.0733 m. Fijo+doble+doble+fijo; 4 hojas. Vano existente conservado.");
  var vest=GameObject.Find("PB_Interior/05_Fase5_Mobiliario_PB/Vestibulo").transform;
  foreach(var t in vest.Cast<Transform>().Where(t=>t.name.StartsWith("Expendedora_"))){bool yellow=t.name.Contains("Amarilla");var before=t.position;t.position=W(yellow?26.35f:25.20f,.2f,16.0f);log.Add(t.name+" "+before+" -> "+t.position+". Posicion SUPUESTO, al norte del barrido.");}
 }
 static void Minisplits(){
  var removed=new List<string>();foreach(string rootName in new[]{"PB_Interior","PA_Interior"})foreach(var t in GameObject.Find(rootName).GetComponentsInChildren<Transform>(true).Where(t=>t.name.IndexOf("minisplit",StringComparison.OrdinalIgnoreCase)>=0).ToArray())if(t){removed.Add(P(t));Object.DestroyImmediate(t.gameObject);}
  File.WriteAllLines(O+"/minisplits_retirados.txt",removed);log.Add("Minisplit/rejillas retirados: "+removed.Count+" objetos. Generacion PARevision4 y ajuste PARevision5 retirados del codigo.");
 }
 static void Classrooms(){
  var rows=new List<string>{"planta,aula,pupitres,distancia_centros_columnas_antes_m,despues_m,distancia_centros_filas_antes_m,despues_m,nota"};
  foreach(var g in GameObject.Find("PB_Interior/05_Fase5_Mobiliario_PB").transform.Cast<Transform>().Where(t=>t.name.StartsWith("Aula_"))){
   var ds=g.Cast<Transform>().Where(t=>t.name.StartsWith("Pupitre_")).ToArray();if(ds.Length==0)continue;
   var inv=Quaternion.Inverse(ds[0].rotation);var rot=ds[0].rotation;var pos=ds.Select(t=>inv*t.position).ToArray();float right=pos.Min(p=>p.z),front=pos.Max(p=>p.x);
   for(int i=0;i<ds.Length;i++){var p=pos[i];p.z=right+(p.z-right)*.75f/.80f;p.x=front+(p.x-front)*1.10f/1.20f;ds[i].position=rot*p;}
   rows.Add("PB,"+g.name+","+ds.Length+",0.80,0.75,1.20,1.10,Extremo derecho y primera fila conservados");
  }
  float[,] rooms={{8,19.24f,28.12f,15.43f,23.14f},{9,17.24f,28.12f,23.14f,30.86f},{10,19.04f,28.12f,30.86f,38.57f},{11,20.48f,28.12f,38.57f,46.29f},{12,18.99f,28.12f,46.29f,53.54f},{13,9.49f,18.99f,46.29f,53.54f},{14,.36f,9.49f,46.29f,53.54f},{15,.36f,8,38.57f,46.29f}};
  for(int n=0;n<8;n++){int id=(int)rooms[n,0];float x0=rooms[n,1],x1=rooms[n,2],z0=rooms[n,3],z1=rooms[n,4];var g=GameObject.Find("PA_Interior/Aula_"+id+"/Mobiliario_PA_SUPUESTO").transform;var ds=g.Cast<Transform>().Where(t=>t.name.StartsWith("Pupitre_")).ToArray();bool east=id==14;int cols=(east?z1-z0:x1-x0)>8.6f?8:6;
   for(int i=0;i<ds.Length;i++){int row=i/cols,col=i%cols;float x=east?x1-2.15f-row*1.10f:x1-.43f-col*.75f,z=east?z1-.43f-col*.75f:z0+2.15f+row*1.10f;ds[i].position=W(x,3.606f,z);ds[i].rotation=Quaternion.Euler(0,east?90:0,0);}
   var board=g.Find("Pizarron_Pared_Acceso_SUPUESTO");board.position=W(east?x1-.15f:(x0+x1)/2,3.606f,east?(z0+z1)/2:z0+.15f);board.rotation=Quaternion.Euler(0,east?90:0,0);
   var teacher=g.Find("Escritorio_Profesor_SUPUESTO");teacher.position=W(east?x1-1.05f:x1-1.05f,3.606f,east?z0+1.3f:z0+1.05f);teacher.rotation=board.rotation;
   rows.Add("PA,Aula_"+id+","+ds.Length+",0.80,0.75,1.20,1.10,"+(east?"Pizarron muro compartido con Aula13":"Direccion Aula8")+"; medidas SUPUESTO");
  }
  File.WriteAllLines(O+"/pupitres.csv",rows);log.Add("Compactacion de aulas PB1-7 y PA8-15; PA16/17 sin modificar mobiliario.");
 }
 static void Doors(){
  foreach(var rootName in new[]{"PB_Interior","PA_Interior"}){
   var root=GameObject.Find(rootName);
   foreach(var h in root.GetComponentsInChildren<Transform>(true).Where(t=>t.name.StartsWith("Pivote_")||t.name=="Bisagra_Giro90_Interior").ToArray()){
    var rr=h.GetComponentsInChildren<Renderer>();if(rr.Length==0)continue;var legacy=h.GetComponentInChildren<Docencia2PADoor>();var name=P(h);var b=BoundsOf(h);var v=b.center-h.position;v.y=0;bool vaiven=h.name.StartsWith("Pivote_Vaiven");Vector3 desired;
    if(legacy)desired=Quaternion.AngleAxis(legacy.inwardAngle,Vector3.up)*v;
    else if(name.Contains("Auditorio")||name.Contains("P20_Aula")||name.Contains("P21_Aula")||name.Contains("P22_Aula"))desired=Vector3.left;
    else if(vaiven)desired=Vector3.forward;
    else {bool west=name.Contains("Mujeres")||name.Contains("P26_")||name.Contains("P17_Aula")||name.Contains("P18_Aula")||name.Contains("P19_Aula")||name.Contains("Cubiculo_A")||name.Contains("Cubiculo_C")||name.Contains("Alcoba");desired=west?Vector3.forward:Vector3.back;}
    var door=h.GetComponent<Docencia2InteractiveDoor>()??h.gameObject.AddComponent<Docencia2InteractiveDoor>();door.closedRotation=h.localRotation;door.inwardAngle=Vector3.Dot(Quaternion.AngleAxis(90,Vector3.up)*v,desired)>0?90:-90;door.doubleSwing=vaiven;
    var local=new Bounds(h.InverseTransformPoint(b.center),Vector3.zero);for(int a=0;a<8;a++)local.Encapsulate(h.InverseTransformPoint(new Vector3((a&1)==0?b.min.x:b.max.x,(a&2)==0?b.min.y:b.max.y,(a&4)==0?b.min.z:b.max.z)));
    foreach(var c in h.GetComponentsInChildren<Collider>().ToArray())Object.DestroyImmediate(c);
    var box=h.gameObject.AddComponent<BoxCollider>();box.center=local.center;box.size=local.size;
    foreach(var t in h.GetComponentsInChildren<Transform>())GameObjectUtility.SetStaticEditorFlags(t.gameObject,0);
   }
  }
  var pb=GameObject.Find("PB_Interior");if(!pb.GetComponent<Docencia2DoorInteractor>())pb.AddComponent<Docencia2DoorInteractor>();
  log.Add("Interaccion E/clic con nuevo Input System. Bisagras a 90 grados; entrada ±90. Sin crear personaje ni cambiar camaras.");
 }
 static void Bath(){
  foreach(bool upper in new[]{false,true}){
   var b=GameObject.Find(upper?"PA_Interior/Bano_Hombres/Mobiliario_PA_SUPUESTO":"PB_Interior/05_Fase5_Mobiliario_PB/Bano_Hombres").transform;
   var bin=b.Cast<Transform>().FirstOrDefault(t=>t.name.Contains("Bote"));if(bin){bin.position=W(18.75f,upper?3.606f:.2f,.95f);log.Add((upper?"PA":"PB")+" Bote hombres a esquina fondo, pared opuesta a lavabos. Cota SUPUESTO.");}
   var mirrors=b.Cast<Transform>().Where(t=>t.name.StartsWith("Espejo")).ToArray();log.Add((upper?"PA":"PB")+" espejos en hombres="+mirrors.Length+"; mingitorios="+b.Cast<Transform>().Count(t=>t.name.StartsWith("Mingitorio_")));
  }
 }
 static void Captures(string suffix){
  FaseARevision.Shot("P16_"+suffix,14,5.2f,11,10.7f,5.1f,8.5f);
  FaseARevision.Shot("Sala_"+suffix,14,5.2f,12,18,5.0f,10);
  FaseARevision.Shot("PB_Canceles_"+suffix,14.24f,1.85f,15,14.24f,1.6f,8);
  FaseARevision.Shot("Entrada_"+suffix,33,1.8f,19.29f,26,1.5f,19.29f);
  FaseARevision.Shot("Expendedoras_"+suffix,23,1.8f,20,26,1.2f,16);
  FaseARevision.Shot("Aula8_"+suffix,20,5.2f,22,24,4.8f,16);
  FaseARevision.Shot("Escalera_"+suffix,3.4f,5.6f,20.8f,6.4f,3.1f,20.6f);
  FaseARevision.Shot("Pasillo_Cubiculos_"+suffix,4.62f,5.15f,7.2f,4.62f,5.0f,.6f);
  FaseARevision.Shot("Bote_BanoH_"+suffix,16.6f,5.2f,3.0f,18.75f,4.0f,.95f);
  foreach(bool upper in new[]{false,true})FaseARevision.Shot((upper?"PA":"PB")+"_BanoH_"+suffix,18.15f,(upper?3.606f:.2f)+1.65f,6.1f,14.6f,(upper?3.606f:.2f)+1.15f,3.45f);
 }
 public static void Run(){
  if(EditorApplication.isPlayingOrWillChangePlaymode)throw new Exception("Edit mode requerido");Directory.CreateDirectory(O);var s=EditorSceneManager.OpenScene("Assets/Scenes/Exterior_Docencia2.unity");
  if(File.Exists(O+"/resto_ok.txt"))throw new Exception("Ya aplicado: no repetir compactacion");
  EditorSceneManager.SaveScene(s,O+"/Antes_Resto.unity",true);log.Clear();Captures("Antes");
  FaseARevision.CorrectP16();Glass();Entrance();Minisplits();Classrooms();Bath();Doors();FaseARefinar.Apply();
  EditorSceneManager.MarkSceneDirty(s);EditorSceneManager.SaveScene(s);AssetDatabase.SaveAssets();Captures("Despues");File.WriteAllLines(O+"/cambios.txt",log);File.WriteAllText(O+"/resto_ok.txt",DateTime.UtcNow.ToString("O"));
 }
}
