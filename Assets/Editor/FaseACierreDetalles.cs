using System;using System.IO;using System.Linq;using System.Reflection;using System.Collections.Generic;using UnityEngine;using UnityEditor;using UnityEditor.SceneManagement;using UnityEngine.Rendering;using Vaelith.Docencia2;using Object=UnityEngine.Object;
public static class FaseACierreDetalles {
 static string O=>FaseARevision.Output;static Vector3 W(float x,float y,float z)=>Docencia2InteriorBuilder.PlanToUnityPos(x,y,z);
 static Mesh Save(Mesh m,string n){m.name=n;string p="Assets/Models/Docencia2/"+n+".asset";var old=AssetDatabase.LoadAssetAtPath<Mesh>(p);if(old){EditorUtility.CopySerialized(m,old);Object.DestroyImmediate(m);return old;}AssetDatabase.CreateAsset(m,p);return m;}
 static void Stall(Transform stall,string id){
  if(stall.parent.Find("Puerta_Cabina_"+id))return;
  var holder=new GameObject("Puerta_Cabina_"+id).transform;holder.SetParent(stall.parent,false);
  var h=new GameObject("Bisagra_Cabina_90").transform;h.SetParent(holder,false);h.position=stall.TransformPoint(new Vector3(-.85f*54/53.92f,0,-.395f*28.5f/28.48f));h.rotation=stall.rotation;
  bool Inside(Vector3 v){v=stall.InverseTransformPoint(v);return v.x<-.79f&&v.x> -1.02f&&v.y>.245f&&v.y<2.05f&&Mathf.Abs(v.z)<.398f;}
  int triangles=0;
  foreach(var r in stall.GetComponentsInChildren<MeshRenderer>().ToArray()){
   var mf=r.GetComponent<MeshFilter>();if(!mf||!mf.sharedMesh)continue;var src=mf.sharedMesh;var vs=src.vertices;var keep=new List<int>[src.subMeshCount];var take=new List<int>[src.subMeshCount];int count=0;
   for(int sm=0;sm<src.subMeshCount;sm++){keep[sm]=new List<int>();take[sm]=new List<int>();var ts=src.GetTriangles(sm);for(int i=0;i<ts.Length;i+=3){bool yes=Inside(r.transform.TransformPoint(vs[ts[i]]))&&Inside(r.transform.TransformPoint(vs[ts[i+1]]))&&Inside(r.transform.TransformPoint(vs[ts[i+2]]));var list=yes?take[sm]:keep[sm];list.AddRange(new[]{ts[i],ts[i+1],ts[i+2]});if(yes)count++;}}
   if(count==0)continue;triangles+=count;
   var fixedMesh=Object.Instantiate(src);for(int sm=0;sm<src.subMeshCount;sm++)fixedMesh.SetTriangles(keep[sm],sm);fixedMesh.RecalculateBounds();mf.sharedMesh=Save(fixedMesh,"FaseA_Cabina_Fija_"+id+"_"+r.name);
   var leaf=Object.Instantiate(src);leaf.vertices=vs.Select(v=>h.InverseTransformPoint(r.transform.TransformPoint(v))).ToArray();leaf.normals=src.normals.Select(v=>h.InverseTransformDirection(r.transform.TransformDirection(v))).ToArray();for(int sm=0;sm<src.subMeshCount;sm++)leaf.SetTriangles(take[sm],sm);leaf.RecalculateBounds();
   var obj=new GameObject("Hoja_"+r.name);obj.transform.SetParent(h,false);obj.layer=stall.gameObject.layer;obj.AddComponent<MeshFilter>().sharedMesh=Save(leaf,"FaseA_Cabina_Hoja_"+id+"_"+r.name);var nr=obj.AddComponent<MeshRenderer>();nr.sharedMaterials=r.sharedMaterials;nr.renderingLayerMask=r.renderingLayerMask;nr.shadowCastingMode=ShadowCastingMode.Off;
  }
  if(triangles<12)throw new Exception("No se aislo la puerta de "+id+"; triangulos="+triangles);
  // Replace only the frontal full-width hull by the two fixed jamb hulls.
  foreach(var c in stall.GetComponentsInChildren<BoxCollider>().Where(c=>Mathf.Abs(stall.InverseTransformPoint(c.bounds.center).x+.85f*54/53.92f)<.06f&&c.size.z>.7f).ToArray())Object.DestroyImmediate(c.gameObject);
  foreach(float z in new[]{-.525f,.525f}){var go=new GameObject("Colision_Frente_Fijo");go.transform.SetParent(stall,false);go.transform.localPosition=new Vector3(-.85f*54/53.92f,1.15f,z*28.5f/28.48f);var c=go.AddComponent<BoxCollider>();c.size=new Vector3(.035f*54/53.92f,1.80f,.25f*28.5f/28.48f);}
  float width=.79f*28.5f/28.48f*stall.lossyScale.z;var box=h.gameObject.AddComponent<BoxCollider>();box.center=new Vector3(0,1.15f,width/2);box.size=new Vector3(.035f,1.79f,width);h.gameObject.layer=stall.gameObject.layer;
  var door=h.gameObject.AddComponent<Docencia2InteractiveDoor>();door.closedRotation=h.localRotation;door.inwardAngle=90;
 }
 static void Shots(string suffix){foreach(bool up in new[]{false,true})foreach(bool male in new[]{false,true}){float fy=up?3.606f:.2f;FaseARevision.Shot((up?"PA":"PB")+"_Bano"+(male?"H":"M")+"_"+suffix,male?18.15f:10.6f,fy+1.65f,6.1f,male?14.6f:13.9f,fy+1.15f,3.45f);}FaseARevision.Shot("P19_Acceso_"+suffix,10,1.8f,40.5f,7.8f,1.3f,39.2f);}
 public static void Run(){
  if(EditorApplication.isPlayingOrWillChangePlaymode)throw new Exception("Construccion requiere Edit mode");if(File.Exists(O+"/detalles_ok.txt"))throw new Exception("Detalles ya aplicados");
  var scene=EditorSceneManager.OpenScene("Assets/Scenes/Exterior_Docencia2.unity");EditorSceneManager.SaveScene(scene,O+"/Antes_Detalles.unity",true);Shots("Antes_Detalles");Directory.CreateDirectory("C:/Users/faust/OneDrive/Desktop/VAELITH/PA_Revision8");
  foreach(bool up in new[]{false,true})foreach(bool male in new[]{false,true}){
   var b=GameObject.Find(up?"PA_Interior/Bano_"+(male?"Hombres":"Mujeres")+"/Mobiliario_PA_SUPUESTO":"PB_Interior/05_Fase5_Mobiliario_PB/Bano_"+(male?"Hombres":"Mujeres")).transform;
   typeof(PARevision8).GetMethod("Bath",BindingFlags.NonPublic|BindingFlags.Static).Invoke(null,new object[]{b,up,male});int i=0;
   foreach(var stall in b.Cast<Transform>().Where(t=>t.name.StartsWith("Cabina_")).ToArray())Stall(stall,(up?"PA":"PB")+"_"+(male?"H":"M")+"_"+(++i));
  }
  foreach(string path in new[]{"PB_Interior/05_Fase5_Mobiliario_PB/Plaza_Sur","PA_Interior/Plaza_Sur/Mobiliario_PA_SUPUESTO"})foreach(var bin in GameObject.Find(path).transform.Cast<Transform>().Where(t=>t.name.StartsWith("Bote_Beige")))bin.position=W(8.5f,path.StartsWith("PA")?3.606f:.2f,41.10f);
  EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();Shots("Final");
  File.WriteAllText(O+"/detalles_ok.txt","8 puertas de cabinas aisladas de sus mallas, bisagra y collider, apertura 90 interior. Lavabos hacia entrada; mampara entre 2 mingitorios; azulejo hasta plafon conservando materiales. Botes PB/PA plaza sur z41.10 fuera de acceso P19. Dimensiones y posiciones de ajuste SUPUESTO.");FaseAAuditFinal.Run();
 }
}
