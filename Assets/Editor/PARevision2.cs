using System;using System.IO;using System.Linq;using System.Text;using System.Collections.Generic;using System.Security.Cryptography;using UnityEngine;using UnityEditor;using UnityEditor.SceneManagement;using UnityEngine.Rendering;using UnityEngine.Rendering.Universal;using Vaelith.Docencia2;using Object=UnityEngine.Object;
public static class PARevision2 {
 const string Out=@"C:/Users/faust/OneDrive/Desktop/VAELITH/PA_Revision2";const string MD="Assets/Models/Docencia2/PARevision2Meshes";const uint Mask=16u;const int Layer=30;
 static int meshIndex;static Transform pa;static Vector3 W(float x,float y,float z)=>Docencia2InteriorBuilder.PlanToUnityPos(x,y,z);
 static string Snapshot(GameObject go,HashSet<Component> except){var b=new StringBuilder();foreach(var t in go.GetComponentsInChildren<Transform>(true))foreach(var c in t.GetComponents<Component>())if(c&&!except.Contains(c))b.Append(EditorJsonUtility.ToJson(c));return Convert.ToBase64String(SHA256.Create().ComputeHash(Encoding.UTF8.GetBytes(b.ToString())));}
 static Material M(string n)=>AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Docencia2/"+n+".mat");
 static Transform G(string n,Transform parent){var g=new GameObject(n);g.transform.SetParent(parent,false);return g.transform;}
 static GameObject B(string n,Transform p,float x0,float x1,float y0,float y1,float z0,float z1,Material m,bool col=false){var g=Docencia2InteriorBuilder.CreatePlanBox(n,p,x0,x1,y0,y1,z0,z1,m,0,col);var mf=g.GetComponent<MeshFilter>();if(mf.sharedMesh&&!AssetDatabase.Contains(mf.sharedMesh)&&mf.sharedMesh.name.StartsWith("MeshUV")){AssetDatabase.CreateAsset(mf.sharedMesh,MD+"/Mesh_"+(meshIndex++)+".asset");}g.GetComponent<Renderer>().shadowCastingMode=ShadowCastingMode.Off;return g;}
 static void ResizeX(Transform t,float x0,float x1){var p=t.position;p.z=(W(x0,0,0).z+W(x1,0,0).z)/2;t.position=p;var s=t.localScale;s.z=Mathf.Abs(W(x0,0,0).z-W(x1,0,0).z);t.localScale=s;}
 public static void Run(){
  var scene=EditorSceneManager.OpenScene("Assets/Scenes/Exterior_Docencia2.unity");if(EditorApplication.isPlaying)throw new Exception("Edit mode required");
  var existingStair=GameObject.Find("PB_Interior/22_Cubo_Escaleras_PB/Escalera_En_U_Concreto_y_Barandal");
  if(!existingStair||existingStair.transform.localPosition.sqrMagnitude>.0001f)throw new Exception("Revision already applied or stair missing; no changes made");
  var oldPA=GameObject.Find("PA_Interior");Object.DestroyImmediate(oldPA);EditorSceneManager.SaveScene(scene);
  Docencia2PABuilder.Run();scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();pa=GameObject.Find("PA_Interior").transform;
  var pb=GameObject.Find("PB_Interior");var stair=pb.transform.Find("22_Cubo_Escaleras_PB/Escalera_En_U_Concreto_y_Barandal");var slab=pb.transform.Find("00_Losa_Entrepiso_y_Columnas/Losa_Entrepiso_Tramo_CentroEste_JuntoEscalera");
  if(!stair||!slab)throw new Exception("Missing stair or slab");if(stair.localPosition.sqrMagnitude>.0001)throw new Exception("Stair already displaced");
  var exceptions=new HashSet<Component>{stair,slab};var roots=scene.GetRootGameObjects().Where(g=>g!=pa.gameObject).ToDictionary(g=>g,g=>Snapshot(g,exceptions));
  File.WriteAllLines(Out+"/allowed_pb_transform_ids.txt",exceptions.Select(c=>GlobalObjectId.GetGlobalObjectIdSlow(c).targetObjectId.ToString()));
  Directory.CreateDirectory(MD);AssetDatabase.Refresh();
  stair.position+=W(1,0,0)-W(0,0,0);ResizeX(slab,8.10f,28.12f);
  var connections=G("Escalera_Encuentro_Desplazado_1m",pa);
  B("Losa_Franja_Oeste_Tras_Mover_Hueco",connections,.36f,1.36f,3.22f,3.60f,15.43f,23.14f,M("Interior/Mat_Int_Losa_Entrepiso_Concreto"),true);
  B("Piso_Franja_Oeste",connections,.36f,1.36f,3.601f,3.606f,15.43f,23.14f,M("Interior/Mat_Int_Piso_LosetaGris_PB"),true);
  B("Plafon_Franja_Oeste_PA",connections,.36f,1.36f,6.80f,6.825f,15.43f,23.14f,M("Interior/Mat_Int_Plafon_Reticular_PB"));
  B("Proteccion_Nuevo_Borde_Oeste",connections,1.30f,1.36f,3.60f,4.40f,15.43f,23.14f,M("Interior/Mat_Int_Muro_AmarilloCrema_PB"),true);
  B("Pasamanos_Nuevo_Borde_Oeste",connections,1.29f,1.37f,4.51f,4.55f,15.43f,23.14f,M("Interior/Mat_Int_Barandal_Tubular_VerdeOscuro"));
  foreach(var t in pa.Find("Llegada_Escalera").Cast<Transform>().Where(t=>t.name.StartsWith("Piso_")||t.name.StartsWith("Plafon_"))){ResizeX(t,8.16f,19.24f);var mf=t.GetComponent<MeshFilter>();if(mf&&AssetDatabase.Contains(mf.sharedMesh)){var vv=mf.sharedMesh.vertices;var nn=mf.sharedMesh.normals;var uv=mf.sharedMesh.uv;float fac=t.name.StartsWith("Piso")?1/.66f:1/1.2f;for(int i=0;i<vv.Length;i++){var w=t.TransformPoint(vv[i]);if(Mathf.Abs(nn[i].y)>.5)uv[i]=new Vector2(w.x*fac,w.z*fac);}mf.sharedMesh.uv=uv;EditorUtility.SetDirty(mf.sharedMesh);}}
  AddLighting();
  PARevision5.Apply(pa);
  if(roots.Any(k=>Snapshot(k.Key,exceptions)!=k.Value))throw new Exception("Unexpected protected object change");
  AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
  File.WriteAllText(Out+"/validacion.txt","Escalera y descansos desplazados +1m eje x plano. Losa este recortada a x8.10; nueva franja oeste x0.36-1.36 y proteccion. Hueco trasladado a x1.36-8.10 sin cerrar hueco de1m del conjunto. PA llegada arranca x8.16. P18 intacta x9.78-10.74,z23.14 junto al desembarque.\nColumnas unidas por retornos; acceso cubiculos este abierto como PB. Cubiculos principales y banos alineados a PB; remates oblicuos cortos de0.34m al parteluz para no cortar cristales.\nSolo dos Transform originalesPB cambiados: escalera y losa este. Materiales/camaras/luces/exterior/PB restante intactos.\n");
  CaptureModes();Debug.Log("PA_REVISION2_SUCCESS");
 }
 static void AddLighting(){
  foreach(var t in pa.GetComponentsInChildren<Transform>(true))t.gameObject.layer=Layer;
  foreach(var r in pa.GetComponentsInChildren<Renderer>(true)){r.renderingLayerMask=1u|Mask;r.shadowCastingMode=ShadowCastingMode.Off;}
  var group=G("Iluminacion_PA_Mismo_Criterio_PB",pa);var fixtures=G("Marcos_Troffer_60x60",group);var night=G("NOCHE",group);var terror=G("TERROR",group);
  var tracker=group.gameObject.AddComponent<Docencia2InteriorModeTracker>();tracker.switcher=Object.FindAnyObjectByType<Docencia2ModeSwitcher>();tracker.nightGroup=night.gameObject;tracker.terrorGroup=terror.gameObject;
  Material frame=M("Mat_Int_Troffer_Marco_Negro"),diffN=M("Mat_Int_Panel_LED_Emisivo_60x60"),diffT=M("Mat_Int_Panel_LED_Terror_60x60");
  var samples=new List<(string,Vector3,float)>();
  foreach(var zone in pa.Cast<Transform>().ToArray()){
   if(zone==group||zone.name=="Escalera_Encuentro_Desplazado_1m")continue;
   var floors=zone.GetComponentsInChildren<Renderer>().Where(r=>r.name.StartsWith("Piso_")).ToArray();
   foreach(var floor in floors){var b=floor.bounds;
    bool corridor=zone.name.Contains("Pasillo")||zone.name.Contains("Plaza")||zone.name.Contains("Llegada");
    int num=zone.name.Contains("Cubiculo")?1:corridor?Mathf.Max(1,Mathf.CeilToInt(b.size.x/5f)):b.size.x*b.size.z>60?2:1;
    for(int i=0;i<num;i++){var pos=b.center;pos.x=Mathf.Lerp(b.min.x,b.max.x,(i+.5f)/num);pos.y=6.55f;samples.Add((zone.name,pos,zone.name.Contains("Cubiculo")?4.2f:6.8f));
     var panelPos=pos;panelPos.y=6.79f;panelPos.x=Mathf.Floor(panelPos.x/.6f)*.6f+.3f;panelPos.z=Mathf.Floor(panelPos.z/.6f)*.6f+.3f;
     Panel("Marco_"+zone.name,fixtures,panelPos,new Vector3(.6f,.025f,.6f),frame);
     Panel("Difusor_"+zone.name,night,panelPos+Vector3.down*.018f,new Vector3(.55f,.012f,.55f),diffN);
     Panel("Difusor_Terror_"+zone.name,terror,panelPos+Vector3.down*.018f,new Vector3(.55f,.012f,.55f),diffT);
    }
   }
  }
  int idx=0,nt=0;foreach(var entry in samples){idx++;CreateLight(night,"Luz_PA_"+entry.Item1+"_"+idx,entry.Item2,entry.Item3,entry.Item1.Contains("Cubiculo")?2.8f:3.2f,new Color(.97f,.98f,1));
   if((entry.Item1.Contains("Pasillo")||entry.Item1.Contains("Llegada")||entry.Item1.Contains("Plaza"))&&idx%2==0){var l=CreateLight(terror,"Luz_PA_Terror_"+idx,entry.Item2,4.8f,.45f,new Color(.95f,.65f,.35f));var flick=l.gameObject.AddComponent<Docencia2LightFlicker>();flick.targetLight=l;flick.baseIntensity=.45f;flick.isDyingLamp=true;flick.editorPreviewFactor=.25f;flick.noiseSeed=idx*17.3f;nt++;}
  }
  foreach(var t in group.GetComponentsInChildren<Transform>(true))t.gameObject.layer=Layer;
  night.gameObject.SetActive(true);terror.gameObject.SetActive(false);
  File.WriteAllText(Out+"/luces.txt",$"PA: {samples.Count} luces NOCHE, {nt} luces TERROR; todas sin sombras. Layer30 y renderingLayer16, exclusivaPA. Materiales emisivos/troffer dePB compartidos, sin editar. Control T: trackerPA sigue al controlador existente sin modificarlo. Posiciones SUPUESTO segun superficies, pendientes de contrastar cantidades exactas de luminarias por foto.\n");
 }
 static void Panel(string n,Transform p,Vector3 pos,Vector3 scale,Material mat){var g=GameObject.CreatePrimitive(PrimitiveType.Cube);g.name=n;g.transform.SetParent(p,false);g.transform.position=pos;g.transform.localScale=scale;Object.DestroyImmediate(g.GetComponent<Collider>());var r=g.GetComponent<Renderer>();r.sharedMaterial=mat;r.renderingLayerMask=Mask;r.shadowCastingMode=ShadowCastingMode.Off;g.isStatic=true;}
 static Light CreateLight(Transform p,string n,Vector3 pos,float range,float intensity,Color c){var g=G(n,p);g.position=pos;var l=g.gameObject.AddComponent<Light>();l.type=LightType.Point;l.range=range;l.intensity=intensity;l.color=c;l.shadows=LightShadows.None;l.cullingMask=1<<Layer;g.gameObject.AddComponent<UniversalAdditionalLightData>().renderingLayers=Mask;return l;}
 static void CaptureModes(){
  var mode=Object.FindAnyObjectByType<Docencia2ModeSwitcher>();var group=pa.Find("Iluminacion_PA_Mismo_Criterio_PB");
  var cameraGO=new GameObject("TEMP_CAM_PA_REAL_LIGHTS");var c=cameraGO.AddComponent<Camera>();c.enabled=false;c.nearClipPlane=.08f;c.farClipPlane=120;c.fieldOfView=68;c.clearFlags=CameraClearFlags.Skybox;cameraGO.AddComponent<UniversalAdditionalCameraData>().renderPostProcessing=true;
  foreach(bool terror in new[]{false,true}){
   mode.ApplyMode(terror?Docencia2VisualMode.Terror:Docencia2VisualMode.Noche,true);group.Find("NOCHE").gameObject.SetActive(!terror);group.Find("TERROR").gameObject.SetActive(terror);
   string suffix=terror?"TERROR":"NOCHE";
   Shot(c,"Pasillo_Norte_"+suffix,14.24f,5.18f,22,14.24f,5.1f,6.8f);
   Shot(c,"Sala_Acceso_Cubiculos_"+suffix,20.2f,5.18f,13.8f,23.86f,5.1f,5.5f);
   Shot(c,"Columnas_"+suffix,14.24f,5.18f,43,14.24f,5.1f,36.5f);
  }
  mode.ApplyMode(Docencia2VisualMode.Noche,true);group.Find("NOCHE").gameObject.SetActive(true);group.Find("TERROR").gameObject.SetActive(false);
  Shot(c,"Escalera_Desplazada_PA",13.4f,5.18f,20.4f,4.3f,4.8f,19.5f);
  Shot(c,"P18_Junto_Desembarque",10.3f,5.18f,19.4f,9.5f,4.8f,23.14f);
  var hidden=new List<Renderer>();foreach(var r in Object.FindObjectsByType<Renderer>()){if(r.enabled&&(r.name.Contains("Azotea")||r.name.Contains("Voladizo")||r.name.Contains("Pretil")||r.name.StartsWith("Plafon_")||r.name.StartsWith("Difusor_")||r.name.StartsWith("Marco_"))){r.enabled=false;hidden.Add(r);}}
  c.orthographic=true;c.orthographicSize=7;c.transform.position=W(5.5f,45,19.29f);c.transform.rotation=Quaternion.LookRotation(Vector3.down,Vector3.right);Capture(c,"Escalera_Superior",1400,1400);
  foreach(var r in hidden)r.enabled=true;Object.DestroyImmediate(cameraGO);
 }
 static void Shot(Camera c,string n,float x,float y,float z,float tx,float ty,float tz){c.transform.position=W(x,y,z);c.transform.LookAt(W(tx,ty,tz));Capture(c,n,1600,1000);}
 static void Capture(Camera c,string n,int w,int h){var rt=new RenderTexture(w,h,24);c.targetTexture=rt;c.Render();var old=RenderTexture.active;RenderTexture.active=rt;var tex=new Texture2D(w,h,TextureFormat.RGB24,false);tex.ReadPixels(new Rect(0,0,w,h),0,0);tex.Apply();File.WriteAllBytes(Out+"/"+n+".png",tex.EncodeToPNG());c.targetTexture=null;RenderTexture.active=old;Object.DestroyImmediate(tex);rt.Release();Object.DestroyImmediate(rt);}
}
