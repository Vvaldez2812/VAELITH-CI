using System;using System.IO;using System.Linq;using System.Reflection;using System.Collections.Generic;using UnityEngine;using UnityEditor;using UnityEditor.SceneManagement;using UnityEngine.Rendering;using Vaelith.Docencia2;using Object=UnityEngine.Object;
public static class FaseAVanos {
 static string O=>FaseARevision.Output;static Vector3 W(float x,float y,float z)=>Docencia2InteriorBuilder.PlanToUnityPos(x,y,z);
 static float PX(Vector3 p)=>(14.25f-p.z)/(28.5f/28.48f);static float PZ(Vector3 p)=>(27-p.x)/(54f/53.92f);
 static Material M(string n)=>AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Docencia2/"+n+".mat");
 static void Filter(MeshRenderer r,Func<Vector3,bool> inside){
  var mf=r.GetComponent<MeshFilter>();if(!mf||!mf.sharedMesh)return;var original=mf.sharedMesh;var vertices=original.vertices;var lists=new List<int>[original.subMeshCount];int removed=0,kept=0;
  for(int sm=0;sm<lists.Length;sm++){var tri=original.GetTriangles(sm);lists[sm]=new List<int>();for(int i=0;i<tri.Length;i+=3){if(inside(r.transform.TransformPoint(vertices[tri[i]]))&&inside(r.transform.TransformPoint(vertices[tri[i+1]]))&&inside(r.transform.TransformPoint(vertices[tri[i+2]]))){removed+=3;continue;}lists[sm].AddRange(new[]{tri[i],tri[i+1],tri[i+2]});kept+=3;}}
  if(removed==0)return;if(kept==0){Object.DestroyImmediate(r.gameObject);return;}
  var mesh=Object.Instantiate(original);mesh.name=original.name+"_VanosA";for(int sm=0;sm<lists.Length;sm++)mesh.SetTriangles(lists[sm],sm);mesh.RecalculateBounds();string path="Assets/Models/Docencia2/"+mesh.name+".asset";
  var existing=AssetDatabase.LoadAssetAtPath<Mesh>(path);if(existing){EditorUtility.CopySerialized(mesh,existing);Object.DestroyImmediate(mesh);mesh=existing;}else AssetDatabase.CreateAsset(mesh,path);mf.sharedMesh=mesh;
 }
 static bool Cubicle(Vector3 p){float x=PX(p),z=PZ(p);return p.y>=.19f&&p.y<=3.21f&&z>=.33f&&z<=7.74f&&new[]{3.62f,5.62f,22.86f,24.86f}.Any(a=>Mathf.Abs(x-a)<.065f);}
 static void Finish(Transform t,uint mask){foreach(var g in t.GetComponentsInChildren<Transform>(true))g.gameObject.layer=8;foreach(var r in t.GetComponentsInChildren<Renderer>(true)){r.renderingLayerMask=mask;r.shadowCastingMode=ShadowCastingMode.Off;}}
 static void Combine(Transform t,string name,string skip=null)=>typeof(Docencia2InteriorBuilder).GetMethod("CombineStaticChildrenByMaterial",BindingFlags.NonPublic|BindingFlags.Static).Invoke(null,new object[]{t,name,false,skip});
 public static void Apply(){
  var pb=GameObject.Find("PB_Interior").transform;var cancels=pb.Find("03_Fase3_Carpinteria_y_Vidrios/Canceleria_Interior_Vidrio_Verde");uint mask=cancels.GetComponentsInChildren<Renderer>()[0].renderingLayerMask;
  foreach(var t in cancels.Cast<Transform>().Where(t=>t.name.StartsWith("Cancel_Cubiculo_")).ToArray())Object.DestroyImmediate(t.gameObject);
  foreach(var r in cancels.GetComponentsInChildren<MeshRenderer>(true).Where(r=>r.name.StartsWith("Comb_Canceleria_PB")).ToArray())Filter(r,Cubicle);
  foreach(var c in cancels.GetComponentsInChildren<Collider>(true).Where(c=>Cubicle(c.bounds.center)).ToArray())Object.DestroyImmediate(c.gameObject);
  var tmp=new GameObject("TEMP_FaseA_Vanos");
  try{
   typeof(Docencia2Phase3Builder).GetMethod("BuildInteriorCancels",BindingFlags.NonPublic|BindingFlags.Static).Invoke(null,new object[]{tmp.transform,M("Mat_Int_Puerta_Madera_Barniz"),M("Mat_Int_Vidrio_Interior_Claro"),M("Mat_Int_Franja_Verde_Institucional"),M("Mat_Int_Linea_Guinda_Institucional"),M("Mat_Int_Marco_Madera_Oscura"),M("Mat_Int_Marco_Puerta_Bronce"),M("Mat_Int_Chapa_Acero_Inox")});
   foreach(var t in tmp.transform.Cast<Transform>().Where(t=>t.name.StartsWith("Cancel_Cubiculo_")).ToArray()){
    t.SetParent(cancels,true);foreach(var pivot in t.GetComponentsInChildren<Transform>().Where(v=>v.name=="Pivote_Bisagra").ToArray())Combine(pivot,"FaseA_Hoja_"+pivot.parent.name);
    Combine(t,"FaseA_"+t.name,"Pivote_");Finish(t,mask);
   }
  }finally{Object.DestroyImmediate(tmp);}
  var doors=pb.Find("03_Fase3_Carpinteria_y_Vidrios/Puertas_P1_P27");
  bool InDoor(Vector3 p){float x=PX(p),z=PZ(p);return (Mathf.Abs(x-8)<.12f||Mathf.Abs(x-20.48f)<.12f)&&z>=38.58f&&z<=39.65f&&p.y>=.19f&&p.y<=2.37f;}
  foreach(var r in doors.GetComponentsInChildren<MeshRenderer>(true).Where(r=>r.name.StartsWith("Comb_Marcos_Puertas")).ToArray())Filter(r,InDoor);
  foreach(var c in doors.GetComponentsInChildren<Collider>(true).Where(c=>c.transform.parent==doors&&InDoor(c.bounds.center)).ToArray())Object.DestroyImmediate(c.gameObject);
  foreach(var t in doors.Cast<Transform>().Where(t=>t.name=="P19_Aula_5"||t.name=="P23_Aula_1").ToArray())Object.DestroyImmediate(t.gameObject);
  tmp=new GameObject("TEMP_FaseA_P19_P23");
  try{
   typeof(Docencia2Phase3Builder).GetMethod("BuildAllDoors",BindingFlags.NonPublic|BindingFlags.Static).Invoke(null,new object[]{tmp.transform,M("Mat_Int_Marco_Madera_Oscura"),M("Mat_Int_Puerta_Madera_Barniz"),M("Mat_Int_Chapa_Acero_Inox"),M("Mat_Int_Vidrio_Interior_Claro")});
   foreach(var t in tmp.transform.Cast<Transform>().Where(t=>t.name=="P19_Aula_5"||t.name=="P23_Aula_1").ToArray()){t.SetParent(doors,true);Combine(t.Find("Pivote_Bisagra"),"FaseA_Hoja_"+t.name);Combine(t,"FaseA_Marco_"+t.name,"Pivote_");Finish(t,mask);}
  }finally{Object.DestroyImmediate(tmp);}
  foreach(var zoneName in new[]{"27_Aula_5","31_Aula_1"})foreach(var t in pb.Find(zoneName).Cast<Transform>().Where(t=>t.name.StartsWith(zoneName.StartsWith("27")?"Muro_Este_Aula5_x8.00":"Muro_Oeste_Aula1_x20.48"))){float a=t.name.Contains("Tramo_12")?38.57f:t.name.Contains("Dintel")?38.88f:39.84f,b=t.name.Contains("Tramo_12")?38.88f:t.name.Contains("Dintel")?39.84f:46.29f;var pos=t.position;pos.x=W(0,0,(a+b)/2).x;t.position=pos;var scale=t.localScale;scale.x=(b-a)*(54/53.92f);t.localScale=scale;}
  // Refit the baseboards on both faces of these two walls; preserve every other baseboard.
  var zoclos=pb.GetComponentsInChildren<MeshRenderer>(true).Where(r=>r.name.StartsWith("Comb_Zoclos_PB")).ToArray();
  foreach(var r in zoclos)Filter(r,p=>p.y>=.19f&&p.y<=.32f&&PZ(p)>=38.56f&&PZ(p)<=46.30f&&(Mathf.Abs(PX(p)-8)<.14f||Mathf.Abs(PX(p)-20.48f)<.14f));
  var baseboards=new GameObject("Zoclos_Vanos_P19_P23_FaseA").transform;baseboards.SetParent(pb.Find("04_Fase4_Acabados_e_Iluminacion"),false);
  foreach(float x in new[]{8f,20.48f})foreach(float side in new[]{-.087f,.087f})foreach(var span in new[]{new Vector2(38.57f,38.88f),new Vector2(39.84f,46.29f)})Docencia2InteriorBuilder.CreatePlanBox("Zoclo_Reajustado",baseboards,x+side-.012f,x+side+.012f,.2f,.3f,span.x,span.y,M("Interior/Mat_Int_Zoclo_CeramicoGris_PB"),0,false);
  Finish(baseboards,mask);Combine(baseboards,"FaseA_Zoclos_P19_P23");Finish(baseboards,mask);
  typeof(FaseAResto).GetMethod("Doors",BindingFlags.NonPublic|BindingFlags.Static).Invoke(null,null);FaseARefinar.Apply();
 }
 public static void Run(){Directory.CreateDirectory(O);if(File.Exists(O+"/vanos_ok.txt"))throw new Exception("Vanos ya aplicados");var scene=EditorSceneManager.OpenScene("Assets/Scenes/Exterior_Docencia2.unity");EditorSceneManager.SaveScene(scene,O+"/Antes_Vanos14.unity",true);
  FaseARevision.Shot("Cubiculos14_Antes",4.62f,1.8f,6.8f,3.62f,1.5f,3.2f);FaseARevision.Shot("P19_Antes",10,1.8f,40.5f,7.8f,1.3f,39.2f);
  Apply();EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();
  FaseARevision.Shot("Cubiculos14_Despues",4.62f,1.8f,6.8f,3.62f,1.5f,3.2f);FaseARevision.Shot("P19_Despues",10,1.8f,40.5f,7.8f,1.3f,39.2f);
  File.WriteAllText(O+"/vanos_ok.txt","12 vanos PB desplazados8cm hacia su panel fijo; P19/P23 z38.88–39.84. Ancho vano0.96 conservado, hojas sin estrechar, columnas intactas. Autorizado por usuario14nov2026.");
 }
}
