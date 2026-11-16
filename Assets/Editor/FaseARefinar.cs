using System;using System.IO;using System.Linq;using UnityEngine;using UnityEditor;using UnityEditor.SceneManagement;using Vaelith.Docencia2;using Object=UnityEngine.Object;
public static class FaseARefinar {
 static Vector3 W(float x,float y,float z)=>Docencia2InteriorBuilder.PlanToUnityPos(x,y,z);
 public static void Apply(){
  // Collider encloses the leaf, not the protruding handle. Bottom clearance avoids floor tile contact.
  foreach(var d in Object.FindObjectsByType<Docencia2InteractiveDoor>(FindObjectsInactive.Include)){
   d.SetOpen(false);var box=d.GetComponent<BoxCollider>();var size=box.size;var center=box.center;
   if(size.x<size.z)size.x=Mathf.Min(size.x,.04f);else size.z=Mathf.Min(size.z,.04f);
   float desiredBottom=d.transform.position.y<1?.23f:3.63f;float bottom=d.transform.TransformPoint(center-size*.5f).y;float trim=Mathf.Max(0,desiredBottom-bottom);
   center.y+=trim/2;size.y-=trim;box.center=center;box.size=size;
  }
  GameObject.Find("PA_Interior/Aula_15/Mobiliario_PA_SUPUESTO/Escritorio_Profesor_SUPUESTO").transform.position=W(1.5f,3.606f,39.62f);
  GameObject.Find("PA_Interior/Aula_14/Mobiliario_PA_SUPUESTO/Escritorio_Profesor_SUPUESTO").transform.position=W(8.44f,3.606f,52.24f);
  var pb=GameObject.Find("PB_Interior");
  foreach(var l in pb.GetComponentsInChildren<Light>(true).Where(l=>l.name=="LuzInt_Noche_07_Escalera"||l.name=="LuzInt_Terror_04_Escalera")){
   l.transform.position=W(4.2f,4.45f,19.29f);var flicker=l.GetComponent<Docencia2LightFlicker>();
   if(flicker){flicker.baseIntensity=4.2f;flicker.ApplyFactor(flicker.editorPreviewFactor);}
  }
 }
 public static void Run(){var s=EditorSceneManager.OpenScene("Assets/Scenes/Exterior_Docencia2.unity");
  FaseARevision.Shot("Destello_Antes_Refinar",3.4f,5.6f,20.8f,6.4f,3.1f,20.6f);Apply();
  EditorSceneManager.MarkSceneDirty(s);EditorSceneManager.SaveScene(s);FaseARevision.Shot("Destello_Despues_Refinar",3.4f,5.6f,20.8f,6.4f,3.1f,20.6f);
  File.WriteAllText(FaseARevision.Output+"/refinado_ok.txt","Colliders ajustados a hojas (sin volumen de manija) y 3 cm de despeje al piso. Escritorios docentes A14/A15 fuera del barrido. Luz escalera reubicada en aire libre a Y4.45; intensidad base terror4.2. Coordenada propuesta SUPUESTO.");
 }
}
