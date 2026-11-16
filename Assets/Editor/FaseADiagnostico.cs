using System;using System.IO;using System.Linq;using System.Collections.Generic;using UnityEngine;using UnityEditor;using UnityEditor.SceneManagement;using Vaelith.Docencia2;using Object=UnityEngine.Object;
public static class FaseADiagnostico {
 public static void Run(){
  EditorSceneManager.OpenScene("Assets/Scenes/Exterior_Docencia2.unity");Directory.CreateDirectory(FaseARevision.Output);
  var pb=GameObject.Find("PB_Interior");var pa=GameObject.Find("PA_Interior");var lines=new List<string>();
  foreach(var root in new[]{pb,pa})foreach(var l in root.GetComponentsInChildren<Light>(true))if(l.name.Contains("Escal")||l.name.Contains("esemb"))lines.Add(l.name+" pos="+l.transform.position+" active="+l.gameObject.activeInHierarchy+" int="+l.intensity);
  var mode=Object.FindAnyObjectByType<Docencia2ModeSwitcher>();var orig=mode.modoActual;mode.ApplyMode(Docencia2VisualMode.Noche,true);
  void Shot(string s)=>FaseARevision.Shot(s,3.4f,5.6f,20.8f,6.4f,3.1f,20.6f);
  Shot("Destello_Base");
  foreach(var l in pb.GetComponentsInChildren<Light>(true).Where(l=>l.name.Contains("Escalera")&&l.gameObject.activeInHierarchy)){
   bool enabled=l.enabled;l.enabled=false;Shot("Destello_Sin_"+l.name);l.enabled=enabled;
   var pos=l.transform.position;l.transform.position=Docencia2InteriorBuilder.PlanToUnityPos(4.3f,5.8f,19.29f);Shot("Destello_Alto_"+l.name);l.transform.position=pos;
  }
  var roots=UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects();
  foreach(var root in roots)foreach(var t in root.GetComponentsInChildren<Transform>(true)){
   var rr=t.GetComponent<Renderer>();if(!rr)continue;
   var b=rr.bounds;
   if(root==pb||root==pa){if(b.min.x < -27.2f||b.max.x>27.2f||b.min.z < -14.4f||b.max.z>14.4f)lines.Add("FUERA? "+PathOf(t)+" "+b);}
   if(t.name.StartsWith("TEMP_")||t.name.Contains("Prototype")||t.name.Contains("Plantilla"))lines.Add("AUXILIAR? "+PathOf(t));
   if(rr.sharedMaterials.Any(m=>!m))lines.Add("MATERIAL_NULL "+PathOf(t));
  }
  File.WriteAllLines(FaseARevision.Output+"/diagnostico.txt",lines);mode.ApplyMode(orig,true);
 }
 static string PathOf(Transform t)=>t.parent?PathOf(t.parent)+"/"+t.name:t.name;
}
