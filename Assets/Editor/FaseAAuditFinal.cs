using System;using System.IO;using System.Linq;using System.Collections.Generic;using UnityEngine;using UnityEditor;using UnityEditor.SceneManagement;using Vaelith.Docencia2;using Object=UnityEngine.Object;
public static class FaseAAuditFinal {
 public static void ApplyAndAudit(){FaseAVanos.Run();Run();}
 public static void Evidence(){Run();
  var doors=Object.FindObjectsByType<Docencia2InteractiveDoor>().Where(d=>d.doubleSwing).ToArray();foreach(var d in doors)d.SetOpen(true);
  var center=Docencia2InteriorBuilder.PlanToUnityPos(25.56f,1.25f,18.2167f);var half=new Vector3(.60f*54/53.92f,.98f,2.56f*28.5f/28.48f);
  var hits=Physics.OverlapBox(center,half,Quaternion.identity,~0,QueryTriggerInteraction.Ignore);File.WriteAllLines(FaseARevision.Output+"/paso_entrada.txt",new[]{"Prisma de paso 1.20m ancho x 5.12m largo x1.96m alto: x23.00..28.12, z17.6167..18.8167, y0.27..2.23. Entrada abierta hacia interior. Collider hits="+hits.Length}.Concat(hits.Select(c=>P(c.transform))));
  FaseARevision.Shot("Entrada_Abierta_Final",33,1.8f,19.29f,26,1.5f,19.29f);foreach(var d in doors)d.SetOpen(false);
  foreach(var d in Object.FindObjectsByType<Docencia2InteractiveDoor>().Where(d=>d.name.StartsWith("Bisagra_Cabina")))d.SetOpen(true);
  FaseARevision.Shot("PB_Cabinas_Abiertas",10.6f,1.85f,6.1f,13.9f,1.35f,3.45f);FaseARevision.Shot("PA_Cabinas_Abiertas",10.6f,5.256f,6.1f,13.9f,4.756f,3.45f);
  FaseARevision.Shot("Candidatos_Forros_Norte",14.24f,5.7f,3,14.24f,5.7f,.36f);FaseARevision.Shot("Candidatos_Forros_Sur",14.24f,5.7f,51,14.24f,5.7f,53.54f);FaseARevision.Shot("Candidatos_Zoclos_A3",1.8f,4.3f,6.1f,1.8f,3.8f,7.71f);
  FaseARevision.Shot("Candidatos_Barandal",3.6f,5.7f,17,2.36f,2.6f,19.29f);
  FaseARegression.Run();
 }
 static string P(Transform t)=>t.parent?P(t.parent)+"/"+t.name:t.name;
 static float X(Vector3 p)=>(14.25f-p.z)/(28.5f/28.48f);static float Z(Vector3 p)=>(27-p.x)/(54f/53.92f);
 static Bounds B(Transform t){var rr=t.GetComponentsInChildren<Renderer>();var b=rr[0].bounds;foreach(var r in rr)b.Encapsulate(r.bounds);return b;}
 public static void Run(){EditorSceneManager.OpenScene("Assets/Scenes/Exterior_Docencia2.unity");var rows=new List<string>();var all=UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Transform>(true)).ToArray();
 foreach(var t in all){
  if(t.name.StartsWith("TEMP_")||t.name.IndexOf("Plantilla",StringComparison.OrdinalIgnoreCase)>=0||t.name.IndexOf("Prototype",StringComparison.OrdinalIgnoreCase)>=0)rows.Add("AUXILIAR? "+P(t));
  if(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject)>0)rows.Add("SCRIPT_FALTANTE "+P(t));
  var r=t.GetComponent<Renderer>();if(r){if(r.sharedMaterials.Any(m=>!m))rows.Add("MATERIAL_FALTANTE "+P(t));if((t.IsChildOf(GameObject.Find("PB_Interior").transform)||t.IsChildOf(GameObject.Find("PA_Interior").transform))&&(r.bounds.min.x< -27.2f||r.bounds.max.x>27.2f||r.bounds.min.z< -14.4f||r.bounds.max.z>14.4f))rows.Add("FUERA_LIMITE? "+P(t)+" "+r.bounds);}
  if((t.name.StartsWith("Bote")||t.name.StartsWith("Expendedora"))&&t.GetComponentsInChildren<Renderer>().Length>0){var b=B(t);rows.Add("PROP "+P(t)+" plan(x,z)="+X(t.position)+","+Z(t.position)+" bounds="+b);}
  if(t.name.Contains("Puerta")&&P(t).Contains("Bano")&&!t.GetComponentInParent<Docencia2InteractiveDoor>()&&!t.GetComponentInChildren<Docencia2InteractiveDoor>(true))rows.Add("PUERTA_BANO_SIN_INTERACCION? "+P(t)+" pos="+t.position+" scale="+t.localScale);
 }
 // Exact renderer duplicates only: candidates, not proof that either is unwanted.
 var rrall=all.Select(t=>t.GetComponent<MeshRenderer>()).Where(r=>r&&r.gameObject.activeInHierarchy&&r.GetComponent<MeshFilter>()&&r.GetComponent<MeshFilter>().sharedMesh).ToArray();
 foreach(var group in rrall.GroupBy(r=>r.GetComponent<MeshFilter>().sharedMesh.GetEntityId().ToString()+"|"+r.transform.localToWorldMatrix.ToString("F5")+"|"+string.Join(",",r.sharedMaterials.Select(m=>m?m.GetEntityId().ToString():"null"))).Where(g=>g.Count()>1))rows.Add("DUPLICADO_EXACTO? "+string.Join(" | ",group.Select(r=>P(r.transform))));
 File.WriteAllLines(FaseARevision.Output+"/auditoria_final.txt",rows);File.WriteAllText(FaseARevision.Output+"/auditoria_totales.txt","Transforms="+all.Length+"; renderers activos="+rrall.Length+"; hallazgos para revision="+rows.Count(s=>!s.StartsWith("PROP "))+"; props inventariados="+rows.Count(s=>s.StartsWith("PROP ")));
 }
}
