using System;using System.IO;using System.Linq;using System.Collections.Generic;using UnityEngine;using UnityEditor;using UnityEditor.SceneManagement;using Vaelith.Docencia2;using Object=UnityEngine.Object;
public static class FaseADoorStudy {
 public static void Run(){EditorSceneManager.OpenScene("Assets/Scenes/Exterior_Docencia2.unity");var rows=new List<string>();
 foreach(var d in Object.FindObjectsByType<Docencia2InteractiveDoor>(FindObjectsInactive.Include).Where(d=>d.transform.parent.name.StartsWith("P23_Hueco")||d.transform.parent.name.StartsWith("P3_Hueco")||d.transform.parent.name.StartsWith("P17_Aula"))){var c=d.GetComponent<BoxCollider>();rows.Add(d.transform.parent.name+" hinge="+d.transform.position+" center="+c.center+" size="+c.size+" angle="+d.inwardAngle);
 for(int i=0;i<=9;i++){d.transform.localRotation=d.closedRotation*Quaternion.Euler(0,d.inwardAngle*i/9f,0);Physics.SyncTransforms();foreach(var other in Physics.OverlapBox(d.transform.TransformPoint(c.center),Vector3.Scale(c.size*.5f,d.transform.lossyScale),d.transform.rotation,~0,QueryTriggerInteraction.Ignore))if(other!=c&&!other.transform.IsChildOf(d.transform.parent)&&Physics.ComputePenetration(c,c.transform.position,c.transform.rotation,other,other.transform.position,other.transform.rotation,out var dir,out float depth)&&depth>.005f)rows.Add("angle="+i*10+" other="+other.name+" center="+other.bounds.center+" size="+other.bounds.size+" depth="+depth+" direction="+dir);}
 d.SetOpen(false);}
 File.WriteAllLines(FaseARevision.Output+"/estudio_bisagras.txt",rows);
 }
}
