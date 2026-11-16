using System;using System.IO;using System.Linq;using System.Collections;using System.Collections.Generic;using System.Diagnostics;using UnityEngine;using UnityEditor;using UnityEditor.SceneManagement;using UnityEngine.Rendering.Universal;using Vaelith.Docencia2;using Object=UnityEngine.Object;
[InitializeOnLoad] public static class FaseAPlayBoot {
 static FaseAPlayBoot(){EditorApplication.update+=Tick;}
 public static void Run(){EditorSceneManager.OpenScene("Assets/Scenes/Exterior_Docencia2.unity");SessionState.SetBool("FA_PLAY",true);EditorApplication.isPlaying=true;}
 static void Tick(){if(SessionState.GetBool("FA_PLAY",false)&&EditorApplication.isPlaying&&!EditorApplication.isCompiling){SessionState.SetBool("FA_PLAY",false);new GameObject("TEMP_FaseA_PlayAudit").AddComponent<FaseAPlayAudit>();}}
}
public class FaseAPlayAudit:MonoBehaviour {
 string O=>FaseARevision.Output;HashSet<string> warnings=new HashSet<string>();
 static string P(Transform t)=>t.parent?P(t.parent)+"/"+t.name:t.name;
 void Log(string condition,string stack,LogType type){if(condition.ToLowerInvariant().Contains("shadow")||type==LogType.Error||type==LogType.Exception)warnings.Add(condition);}
 IEnumerator Start(){
  Directory.CreateDirectory(O);Application.logMessageReceived+=Log;Application.runInBackground=true;QualitySettings.vSyncCount=0;Application.targetFrameRate=-1;
  yield return null;yield return null;
  var doors=Object.FindObjectsByType<Docencia2InteractiveDoor>(FindObjectsInactive.Include);var report=new List<string>{"puerta,giro90,vaiven,interferencias_mayores_2cm"};
  foreach(var d in doors){var errors=new HashSet<string>();var c=d.GetComponent<BoxCollider>();var closed=d.closedRotation;bool angleOK=true;
   foreach(bool rev in d.doubleSwing?new[]{false,true}:new[]{false}){
    for(int step=0;step<=9;step++){d.transform.localRotation=closed*Quaternion.Euler(0,d.inwardAngle*(rev?-1:1)*step/9f,0);Physics.SyncTransforms();
     var hits=Physics.OverlapBox(d.transform.TransformPoint(c.center),Vector3.Scale(c.size*.5f,d.transform.lossyScale),d.transform.rotation,~0,QueryTriggerInteraction.Ignore);
     foreach(var other in hits){if(other==c||other.transform.IsChildOf(d.transform.parent)||!other.enabled)continue;
      if(Physics.ComputePenetration(c,c.transform.position,c.transform.rotation,other,other.transform.position,other.transform.rotation,out _,out float distance)&&distance>.02f)errors.Add(P(other.transform));}
    }
    d.SetOpen(true,rev);angleOK&=Mathf.Abs(Quaternion.Angle(closed,d.transform.localRotation)-90)<.01f;
   }
   d.SetOpen(false);report.Add('"'+P(d.transform)+"\","+angleOK+","+d.doubleSwing+",\""+string.Join(" | ",errors)+"\"");
  }
  File.WriteAllLines(O+"/puertas_play.csv",report);
  foreach(var c in Object.FindObjectsByType<Camera>())c.enabled=false;
  var camera=gameObject.AddComponent<Camera>();camera.enabled=false;camera.nearClipPlane=.08f;camera.farClipPlane=180;camera.fieldOfView=68;gameObject.AddComponent<UniversalAdditionalCameraData>().renderPostProcessing=true;
  var rt=new RenderTexture(1920,1080,24);camera.targetTexture=rt;var mode=Object.FindAnyObjectByType<Docencia2ModeSwitcher>();
  var rows=new List<string>{"modo,vista,resolucion,fps_promedio,fps_p95_lento,luces_activas,luces_sombra"};
  foreach(bool terror in new[]{false,true}){
   mode.ApplyMode(terror?Docencia2VisualMode.Terror:Docencia2VisualMode.Noche,false);yield return null;yield return null;
   foreach(int view in new[]{0,1,2,3,4,5}){
    string viewName;
    if(view<3){var refs=GameObject.Find("Camaras_Referencia_Docencia2").GetComponentsInChildren<Camera>(true).OrderBy(c=>c.name).ToArray();var reference=refs[view];camera.transform.SetPositionAndRotation(reference.transform.position,reference.transform.rotation);camera.fieldOfView=reference.fieldOfView;viewName=reference.name;}
    else{Vector3 pos=view==3?new Vector3(14,5.2f,11):view==4?new Vector3(3.4f,5.6f,20.8f):new Vector3(20,5.2f,22);Vector3 target=view==3?new Vector3(10.7f,5.1f,8.5f):view==4?new Vector3(6.4f,3.1f,20.6f):new Vector3(24,4.8f,16);camera.transform.position=Docencia2InteriorBuilder.PlanToUnityPos(pos.x,pos.y,pos.z);camera.transform.LookAt(Docencia2InteriorBuilder.PlanToUnityPos(target.x,target.y,target.z));camera.fieldOfView=68;viewName=view==3?"P16":view==4?"Escalera":"Aula8";}
    for(int i=0;i<20;i++){camera.Render();yield return null;}
    var samples=new List<double>();long last=Stopwatch.GetTimestamp();for(int i=0;i<90;i++){camera.Render();yield return null;long now=Stopwatch.GetTimestamp();samples.Add((now-last)/(double)Stopwatch.Frequency);last=now;}
    var lights=Object.FindObjectsByType<Light>().Where(l=>l.enabled&&l.gameObject.activeInHierarchy).ToArray();samples.Sort();rows.Add(FormattableString.Invariant($"{(terror?"TERROR":"NOCHE")},{viewName},1920x1080,{90/samples.Sum():F1},{1/samples[85]:F1},{lights.Length},{lights.Count(l=>l.shadows!=LightShadows.None)}"));
    var prev=RenderTexture.active;RenderTexture.active=rt;var texture=new Texture2D(1920,1080,TextureFormat.RGB24,false);texture.ReadPixels(new Rect(0,0,1920,1080),0,0);texture.Apply();File.WriteAllBytes(O+"/Play_"+(terror?"TERROR":"NOCHE")+"_"+viewName+".png",texture.EncodeToPNG());RenderTexture.active=prev;Destroy(texture);
    File.WriteAllLines(O+"/fps_play.csv",rows);
   }
  }
  File.WriteAllLines(O+"/advertencias_play.txt",warnings.Count==0?new[]{"No se capturaron errores ni advertencias de sombras durante esta prueba."}:warnings.ToArray());File.WriteAllText(O+"/play_ok.txt","PLAY_COMPLETO; medicion de intervalos por frame en Play con render manual 1920x1080; no es una captura del panel Stats.");EditorApplication.Exit(0);
 }
}
