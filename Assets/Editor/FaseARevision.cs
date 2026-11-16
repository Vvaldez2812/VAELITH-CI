using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.Rendering;
using Vaelith.Docencia2;
using Object=UnityEngine.Object;

// Deliberate, scoped corrections; never called automatically on import.
public static class FaseARevision {
 public static string Output => @"C:/Users/faust/OneDrive/Desktop/VAELITH/FaseA_Correcciones/"+(Application.dataPath.Contains("Checkpoint_Fase5")?"Prueba":"Aplicado");
 static Vector3 W(float x,float y,float z)=>Docencia2InteriorBuilder.PlanToUnityPos(x,y,z);
 static float Z(Vector3 p)=>(27-p.x)/(54f/53.92f);
 static string PathOf(Transform t)=>t.parent?PathOf(t.parent)+"/"+t.name:t.name;
 static Material Clear=>AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Docencia2/Mat_FaseA_Vidrio_Cancel_Transparente.mat");
 static Material Metal=>AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Docencia2/Mat_Int_Marco_Madera_Oscura.mat");
 public static void Shot(string name,float x,float y,float z,float tx,float ty,float tz){
  Directory.CreateDirectory(Output);var go=new GameObject("TEMP_FaseA_Render");var c=go.AddComponent<Camera>();c.enabled=false;c.fieldOfView=68;c.nearClipPlane=.06f;c.farClipPlane=150;c.transform.position=W(x,y,z);c.transform.LookAt(W(tx,ty,tz));
  var rt=new RenderTexture(1500,950,24);var prev=RenderTexture.active;var tex=new Texture2D(1500,950,TextureFormat.RGB24,false);
  try{c.targetTexture=rt;c.Render();RenderTexture.active=rt;tex.ReadPixels(new Rect(0,0,1500,950),0,0);tex.Apply();File.WriteAllBytes(Output+"/"+name+".png",tex.EncodeToPNG());}
  finally{c.targetTexture=null;RenderTexture.active=prev;rt.Release();Object.DestroyImmediate(rt);Object.DestroyImmediate(tex);Object.DestroyImmediate(go);}
 }
 public static void CorrectP16(){
  var g=GameObject.Find("PA_Interior/Pasillo_Junto_Redes/Entrada_P16").transform;
  var door=g.Find("P16_Hueco096_Abre_oeste");var top=g.Find("Vidrio_Sobre_P16");
  float oldA=Z(top.position)-.48f, oldB=oldA+.96f;
  const float newA=7.86f,newB=8.82f; // 15 cm remnant beside north wall: SUPUESTO.
  foreach(var t in g.Cast<Transform>().ToArray()){
   if(t==door||t==top)continue;
   if(t.name=="Lambrin_100cm"||t.name.StartsWith("Zoclo_")){Object.DestroyImmediate(t.gameObject);continue;}
   float z=Z(t.position);bool north=z<oldA;float a=north?7.71f:newB,b=north?newA:10.29f;
   var p=t.position;var s=t.localScale;
   if(t.name=="MarcoVertical"){
    bool atStart=north?z<(7.71f+oldA)/2:z<(oldB+10.29f)/2;
    p.x=W(0,0,atStart?a+.02f:b-.02f).x;p.y=5.20f;s.y=3.20f;
   }else{p.x=W(0,0,(a+b)/2).x;s.x=(b-a)*(54f/53.92f);
    if(t.name=="Vidrio_Al_Plafon"){s.x-=.05f;p.y=5.20f;s.y=3.12f;}
    if(t.name=="Travesano"||t.name=="MarcoInferior_Claro"){p.y=3.62f;s.y=.04f;t.name="MarcoInferior_Claro";}
   }
   t.position=p;t.localScale=s;
  }
  var delta=W(0,0,newA)-W(0,0,oldA);door.position+=delta;top.position+=delta;
  foreach(var r in g.GetComponentsInChildren<Renderer>()){
   if(r.sharedMaterial.name.Contains("Vidrio"))r.sharedMaterial=Clear;
   else if(r.sharedMaterial.name.Contains("Marco")||r.sharedMaterial.name=="F5_CromoBano")r.sharedMaterial=Metal;
  }
  var d=g.GetComponentInChildren<Docencia2PADoor>();var initial=d.hinge.localRotation;
  d.SetOpen(true);Physics.SyncTransforms();Shot("P16_Banos_Abierta",14,5.2f,11,10.7f,5.1f,8.5f);d.SetOpen(false);d.hinge.localRotation=initial;
  File.WriteAllText(Output+"/P16.txt","P16 PA: hueco z7.86–8.82, extremo NORTE hacia baños. Remate de 0.15 m SUPUESTO. Cancel completo de cristal; travesaño superior conservado.\n");
 }
 public static void Run(){
  if(EditorApplication.isPlayingOrWillChangePlaymode)throw new Exception("Construccion exige Edit mode");
  Directory.CreateDirectory(Output);var scene=EditorSceneManager.OpenScene("Assets/Scenes/Exterior_Docencia2.unity");
  EditorSceneManager.SaveScene(scene,Output+"/Antes_Revision_"+DateTime.UtcNow.ToString("yyyyMMdd_HHmmss")+".unity",true);
  Shot("P16_Antes_Revision",14,5.2f,11,10.7f,5.1f,8.5f);
  CorrectP16();
  Shot("P16_Banos_Corregida",14,5.2f,11,10.7f,5.1f,8.5f);
  EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();
  File.WriteAllText(Output+"/revision_ok.txt",DateTime.UtcNow.ToString("O"));
 }
}
