using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using System.Collections.Generic;
using System.Linq;
using System.IO;
namespace Vaelith.Docencia2 {
 public static partial class Docencia2Phase5Builder {
  static Vector3 BathV(Vector3 v)=>new Vector3(-v.z*SZ,v.y,-v.x*SX);
  static void BathMesh(Transform p,string name,string material,List<Vector3> v,List<int> t){var m=new Mesh{name=name};m.SetVertices(v);m.SetTriangles(t,0);m.RecalculateNormals();m.RecalculateBounds();var g=new GameObject(name);g.transform.SetParent(p,false);g.AddComponent<MeshFilter>().sharedMesh=m;g.AddComponent<MeshRenderer>().sharedMaterial=mats[material];}
  static void BasinProfile(Transform p,string name,float x,float z,float rx,float rz,float[] radii,float[] heights){
   const int n=48;var v=new List<Vector3>();var t=new List<int>();for(int r=0;r<radii.Length;r++)for(int i=0;i<n;i++){float a=i*2*Mathf.PI/n;v.Add(BathV(new Vector3(x+rx*radii[r]*Mathf.Cos(a),heights[r],z+rz*radii[r]*Mathf.Sin(a))));}
   for(int r=0;r<radii.Length-1;r++)for(int i=0;i<n;i++){int a=r*n+i,b=r*n+(i+1)%n,c=a+n,d=b+n;t.AddRange(new[]{a,b,c,b,d,c});}BathMesh(p,name,"Porcelana",v,t);
  }
  static void Tube(Transform p,string name,Vector3[] path,float radius,string material){
   const int n=10;var v=new List<Vector3>();var t=new List<int>();for(int j=0;j<path.Length;j++){Vector3 tangent=(path[Mathf.Min(j+1,path.Length-1)]-path[Mathf.Max(0,j-1)]).normalized;var u=Vector3.Cross(tangent,Mathf.Abs(tangent.y)>.9f?Vector3.right:Vector3.up).normalized;var w=Vector3.Cross(tangent,u);for(int i=0;i<n;i++){float a=i*Mathf.PI*2/n;v.Add(BathV(path[j]+radius*(Mathf.Cos(a)*u+Mathf.Sin(a)*w)));}}
   for(int j=0;j<path.Length-1;j++)for(int i=0;i<n;i++){int a=j*n+i,b=j*n+(i+1)%n,c=a+n,d=b+n;t.AddRange(new[]{a,c,b,b,c,d});}BathMesh(p,name,material,v,t);
  }
  static void CounterAroundBasin(Transform p,float x){
   const int n=48;var v=new List<Vector3>();var t=new List<int>();
   for(int ring=0;ring<2;ring++)for(int i=0;i<n;i++){float a=i*2*Mathf.PI/n,c=Mathf.Cos(a),s=Mathf.Sin(a);float dist=Mathf.Min(.475f/Mathf.Max(Mathf.Abs(c),.00001f),.325f/Mathf.Max(Mathf.Abs(s),.00001f));v.Add(BathV(ring==0?new Vector3(x+c*dist,.85f,s*dist):new Vector3(x+c*.255f,.85f,.03f+s*.185f)));}
   for(int i=0;i<n;i++){int a=i,b=(i+1)%n,c=a+n,d=b+n;foreach(var tri in new[]{new[]{a,b,c},new[]{b,d,c}}){if(Vector3.Cross(v[tri[1]]-v[tri[0]],v[tri[2]]-v[tri[0]]).y<0)System.Array.Reverse(tri);t.AddRange(tri);}}
   BathMesh(p,"Cubierta_Con_Hueco","AzulejoBeige",v,t);
  }
  static void DetailedToilet(Transform p){
   Part(p,"Base_Redondeada","Porcelana",new Vector3(0,.035f,0),new Vector3(.30f,.07f,.40f),PrimitiveType.Sphere);
   Part(p,"Pedestal_Curvo","Porcelana",new Vector3(0,.14f,-.03f),new Vector3(.24f,.24f,.31f),PrimitiveType.Sphere);
   BasinProfile(p,"Taza_Concava",0,.08f,.22f,.29f,new[]{.35f,.78f,1f,.98f,.83f,.50f,.24f},new[]{.18f,.28f,.39f,.425f,.425f,.32f,.29f});
   OvalRim(p,"Asiento",0,.455f,.08f,.24f,.31f,.17f,.23f,.035f,"Porcelana");
   Part(p,"Agua_Interior","AguaBano",new Vector3(0,.292f,.08f),new Vector3(.10f,.008f,.14f),PrimitiveType.Cylinder);
   Box(p,"Deposito","Porcelana",0,.62f,-.255f,.40f,.36f,.18f);Part(p,"Tapa_Tanque_Redondeada","Porcelana",new Vector3(0,.815f,-.255f),new Vector3(.44f,.045f,.22f),PrimitiveType.Sphere);
   Part(p,"Tapa_Asiento_Levantada","Porcelana",new Vector3(0,.68f,-.14f),new Vector3(.43f,.45f,.04f),PrimitiveType.Sphere);
   Cylinder(p,"Boton_Cromado","CromoBano",0,.84f,-.255f,.055f,.012f);
   foreach(float x in new[]{-.09f,.09f})Box(p,"Bisagra_Asiento","CromoBano",x,.455f,-.18f,.035f,.025f,.06f);
  }
  static void DetailedVanity(Transform p,int count){
   float width=count*.95f;Box(p,"Frente_Azulejo","AzulejoBeige",0,.65f,.30f,width,.40f,.05f);Box(p,"Fondo_Encimera","AzulejoBeige",0,.81f,-.30f,width,.08f,.05f);Box(p,"Copete","AzulejoBeige",0,.90f,-.315f,width,.10f,.025f);
   foreach(float x in new[]{-width/2+.06f,width/2-.06f})Box(p,"Apoyo_Azulejo","AzulejoBeige",x,.39f,0,.12f,.78f,.62f);
   for(int i=0;i<count;i++){
    float x=(i-(count-1)/2f)*.95f;CounterAroundBasin(p,x);
    BasinProfile(p,"Lavabo_Concavo",x,.03f,.28f,.21f,new[]{1f,1.03f,1f,.87f,.77f,.36f,.08f},new[]{.835f,.865f,.885f,.88f,.80f,.735f,.73f});
    Cylinder(p,"Desague_Cromado","CromoBano",x,.734f,.03f,.035f,.008f);
    Tube(p,"Grifo_Curvo",new[]{new Vector3(x,.85f,-.24f),new Vector3(x,.94f,-.24f),new Vector3(x,.975f,-.22f),new Vector3(x,.98f,-.18f),new Vector3(x,.96f,-.10f)},.018f,"CromoBano");
    Part(p,"Base_Grifo","CromoBano",new Vector3(x,.858f,-.24f),new Vector3(.12f,.02f,.06f),PrimitiveType.Sphere);Box(p,"Palanca","CromoBano",x,.985f,-.255f,.026f,.012f,.09f);
    Tube(p,"Sifon_Curvo",new[]{new Vector3(x,.73f,.03f),new Vector3(x,.52f,.03f),new Vector3(x,.46f,0),new Vector3(x,.46f,-.08f),new Vector3(x,.52f,-.12f),new Vector3(x,.55f,-.30f)},.022f,"Porcelana");
   }
   for(float x=-width/2+.30f;x<width/2;x+=.30f){Box(p,"Junta_Frontal","Junta",x,.65f,.327f,.003f,.40f,.002f);}
   for(int seam=1;seam<count;seam++)Box(p,"Junta_Cubierta","Junta",-width/2+seam*.95f,.852f,0,.003f,.002f,.65f);
   Box(p,"Junta_Horizontal","Junta",0,.60f,.327f,width,.003f,.002f);
  }
  static void StallDetail(Transform p){
   foreach(var r in p.GetComponentsInChildren<Renderer>())if(r.sharedMaterial==mats["Metal"])r.sharedMaterial=mats["Crema"];
   var old=p.Find("Tirador");if(old)Object.DestroyImmediate(old.gameObject);
   Tube(p,"Tirador_Curvo",new[]{new Vector3(.29f,1.02f,.88f),new Vector3(.29f,1.035f,.94f),new Vector3(.29f,1.20f,.94f),new Vector3(.29f,1.215f,.88f)},.012f,"CromoBano");
   foreach(float y in new[]{.43f,1.10f,1.78f})Cylinder(p,"Bisagra","CromoBano",-.40f,y,.88f,.024f,.08f);
   Part(p,"Cierre","CromoBano",new Vector3(.30f,1.31f,.88f),new Vector3(.055f,.055f,.025f),PrimitiveType.Sphere);
   foreach(float x in new[]{-.65f,.65f})foreach(float z in new[]{-.80f,.82f})Box(p,"Pata_Acero","CromoBano",x,.15f,z,.045f,.25f,.045f);
  }
  static void AddBathFill(Transform zone,bool male){
   var night=zone.Find("Iluminacion_NOCHE_SUPUESTO");if(!night)return;var found=night.Find("Rebote_Cabinas_SUPUESTO");var g=found?found.gameObject:new GameObject("Rebote_Cabinas_SUPUESTO");g.transform.SetParent(night,false);g.transform.position=Docencia2InteriorBuilder.PlanToUnityPos(male?15.2f:13.2f,2.6f,male?1.25f:2.6f);g.layer=8;var l=g.GetComponent<Light>();if(!l)l=g.AddComponent<Light>();l.type=LightType.Point;l.intensity=1.25f;l.range=3.2f;l.shadows=LightShadows.None;l.cullingMask=1<<8;l.color=new Color(.98f,.98f,1f);var data=g.GetComponent<UnityEngine.Rendering.Universal.UniversalAdditionalLightData>();if(!data)data=g.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalLightData>();data.renderingLayers=male?8u:4u;
  }
  public static void RefineBathrooms(){
   if(EditorApplication.isPlayingOrWillChangePlaymode){Debug.LogWarning("[Docencia2] Sal de Play Mode primero para ejecutar 'Refinar banos existentes'.");return;}mats=new Dictionary<string,Material>();prototypes=new Dictionary<string,GameObject>();foreach(var path in Directory.GetFiles(MatDir,"*.mat")){var m=AssetDatabase.LoadAssetAtPath<Material>(path);mats[m.name.Substring(3)]=m;}
   MaterialFor("CromoBano",new Color(.72f,.75f,.77f),.75f,.8f);MaterialFor("AguaBano",new Color(.22f,.34f,.34f),.7f,.1f);
   try{Prototype("Inodoro",Toilet);Prototype("CabinaBano",Stall);Prototype("Lavabos3",p=>Vanity(p,3));Prototype("Lavabos2",p=>Vanity(p,2));
    foreach(string zoneName in new[]{"Bano_Mujeres","Bano_Hombres"}){var zone=GameObject.Find("PB_Interior/05_Fase5_Mobiliario_PB/"+zoneName).transform;
     AddBathFill(zone,zoneName=="Bano_Hombres");
     foreach(var old in zone.Cast<Transform>().ToArray()){string key=old.name.StartsWith("Inodoro_")?"Inodoro":old.name.StartsWith("Cabina_")?"CabinaBano":old.name.StartsWith("Lavabos_")?(zoneName=="Bano_Hombres"?"Lavabos2":"Lavabos3"):null;if(key==null)continue;
      var g=Object.Instantiate(prototypes[key],zone);g.name=old.name;g.transform.SetPositionAndRotation(old.position,old.rotation);g.transform.localScale=old.localScale;g.SetActive(true);
      foreach(var col in old.GetComponentsInChildren<BoxCollider>()){var obj=g;if(col.transform!=old){obj=new GameObject("Colision_Simple");obj.transform.SetParent(g.transform,false);obj.transform.localPosition=old.InverseTransformPoint(col.transform.position);obj.transform.localRotation=Quaternion.Inverse(old.rotation)*col.transform.rotation;obj.transform.localScale=col.transform.localScale;}var c=obj.AddComponent<BoxCollider>();c.center=col.center;c.size=col.size;if(key=="Inodoro"){c.center+=Vector3.up*.03f;c.size+=Vector3.up*.06f;}}
      foreach(var t in g.GetComponentsInChildren<Transform>(true))t.gameObject.layer=8;foreach(var r in g.GetComponentsInChildren<Renderer>()){r.renderingLayerMask=zoneName=="Bano_Hombres"?10u:6u;r.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;}
      Object.DestroyImmediate(old.gameObject);
     }
    }
   }finally{foreach(var p in prototypes.Values)if(p)Object.DestroyImmediate(p);}
   AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
  }
 }
}
