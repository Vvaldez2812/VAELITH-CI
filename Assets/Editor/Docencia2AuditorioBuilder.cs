using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEditor;
using UnityEditor.SceneManagement;
using System.IO;
using System.Linq;
using System.Collections.Generic;
namespace Vaelith.Docencia2 {
 public static partial class Docencia2Phase5Builder {
  public static void BuildAuditorio(){
   if(EditorApplication.isPlayingOrWillChangePlaymode)throw new System.Exception("Exit Play first");
   var pb=GameObject.Find("PB_Interior").transform;var old=pb.Find("06_Auditorio_PB");if(old)Object.DestroyImmediate(old.gameObject);
   var root=Zone(pb,"06_Auditorio_PB");mats=new Dictionary<string,Material>();prototypes=new Dictionary<string,GameObject>();
   foreach(var p in Directory.GetFiles(MatDir,"*.mat")){var m=AssetDatabase.LoadAssetAtPath<Material>(p);mats[m.name.Substring(3)]=m;}
   MaterialFor("Aud_Madera",new Color(.52f,.31f,.12f),.25f,0);MaterialFor("Aud_Tapiz",new Color(.035f,.038f,.033f),.12f,0);
   MaterialFor("Aud_Hoja",new Color(.08f,.26f,.085f),.15f,0);MaterialFor("Aud_Maceta",new Color(.06f,.23f,.14f),.25f,0);
   var architecture=pb.Find("24_Auditorio");var originalFloor=architecture.GetComponentsInChildren<Renderer>(true).First(r=>r.name.StartsWith("Piso_"));var tile=originalFloor.sharedMaterial;
   var wall=architecture.GetComponentsInChildren<Renderer>(true).First(r=>r.name.StartsWith("Muro_Sur")).sharedMaterial;
   // Only interior footprint below the auditorium; original shared meshes are retained as backups.
   AuditorioFloorCut.Apply();
   foreach(Transform t in architecture)if(t.name.StartsWith("Piso_Auditorio"))t.gameObject.SetActive(false);
   void Floor(string n,float x0,float x1,float z0,float z1,float top,Material mat,float depth=.12f){var g=Docencia2InteriorBuilder.CreatePlanBox(n,root,x0,x1,top-depth,top,z0,z1,mat,0,true);g.GetComponent<Renderer>().renderingLayerMask=130u;}
   Floor("Acceso_Alto_SUPUESTO",17.32f,28.04f,23.22f,25.20f,.20f,tile);
   for(int r=0;r<8;r++)Floor("Grada_"+(r+1)+"_SUPUESTO",17.32f,28.04f,25.2f+r*1.15f,25.2f+(r+1)*1.15f,.08f-r*.12f,tile);
   Floor("Paso_Frente_Escenario_SUPUESTO",17.32f,28.04f,34.4f,35.8f,-.76f,tile);
   Floor("Escenario_Madera_SUPUESTO",17.32f,28.04f,35.8f,38.49f,-.04f,mats["Aud_Madera"],.72f);
   // Close the lowered interior sides without changing the existing upper walls.
   foreach(float x in new[]{17.35f,28.01f})Docencia2InteriorBuilder.CreatePlanBox("Forro_Bajo_Gradas_SUPUESTO",root,x-.03f,x+.03f,-.9f,.205f,25.19f,38.50f,wall,0,true);
   Docencia2InteriorBuilder.CreatePlanBox("Forro_Bajo_Escenario_SUPUESTO",root,17.32f,28.04f,-.9f,.205f,38.46f,38.50f,wall,0,true);
   foreach(float x in new[]{18.15f,27.2f})for(int i=0;i<3;i++)Floor("Escalon_Acceso_Escenario_"+(i+1)+"_SUPUESTO",x-.55f,x+.55f,34.9f+i*.30f,35.2f+i*.30f,-.58f+i*.18f,mats["Aud_Madera"],.18f*(i+1));
   try{
    Prototype("AudButaca",p=>{
     Box(p,"Asiento_Base","Aud_Madera",0,.43f,0,.52f,.055f,.45f);Box(p,"Cojin","Aud_Tapiz",0,.48f,0,.48f,.06f,.42f);
     Box(p,"Respaldo_Base","Aud_Madera",0,.76f,-.21f,.52f,.48f,.055f);Box(p,"Respaldo","Aud_Tapiz",0,.76f,-.175f,.48f,.43f,.035f);
     foreach(float x in new[]{-.28f,.28f}){Box(p,"Anclaje","Metal",x,.035f,0,.10f,.07f,.36f);Box(p,"Pie","Metal",x,.30f,0,.04f,.55f,.04f);Box(p,"Brazo","Negro",x,.64f,0,.055f,.055f,.43f);}
     Box(p,"Paleta_Madera","Aud_Madera",.20f,.72f,.25f,.27f,.025f,.35f);
    });
    Prototype("AudMesaLarga",p=>AudTable(p,1.8f));Prototype("AudMesaCorta",p=>AudTable(p,.9f));Prototype("AudSilla",p=>VisitorChair(p,0,0,"Negro"));
    Prototype("AudBocina",p=>{Box(p,"Gabinete","Negro",0,.39f,0,.45f,.78f,.34f);foreach(float y in new[]{.23f,.57f})Part(p,"Rejilla","Aud_Tapiz",new Vector3(0,y,-.18f),new Vector3(.32f,.29f,.025f),PrimitiveType.Sphere);});
    Prototype("AudPanelMovil",p=>{
     Box(p,"Panel_Blanco","Papel",0,1.30f,0,1.10f,1.35f,.035f);
     foreach(float x in new[]{-.57f,.57f}){Box(p,"Marco","Metal",x,1.30f,0,.035f,1.42f,.05f);Box(p,"Pata","Metal",x,.42f,0,.04f,.84f,.04f);Box(p,"Base_Ruedas","Metal",x,.08f,0,.05f,.04f,.65f);foreach(float z in new[]{-.28f,.28f})Part(p,"Rueda","Negro",new Vector3(x,.045f,z),new Vector3(.07f,.08f,.04f),PrimitiveType.Sphere);}
     foreach(float y in new[]{.61f,1.99f})Box(p,"Marco","Metal",0,y,0,1.17f,.035f,.05f);
    });
    Prototype("AudPlanta",p=>{
     Cylinder(p,"Maceta","Aud_Maceta",0,.27f,0,.48f,.54f);Cylinder(p,"Tallo","Aud_Hoja",0,.95f,0,.025f,1.25f);
     for(int i=0;i<24;i++){float a=i*2.4f;float radius=.18f+(i%3)*.04f;var leaf=Part(p,"Hoja","Aud_Hoja",new Vector3(Mathf.Cos(a)*radius,.65f+i*.045f,Mathf.Sin(a)*radius),new Vector3(.20f,.025f,.40f),PrimitiveType.Sphere);leaf.transform.localRotation=Quaternion.Euler(i%2==0?22:-22,i*137,0);}
    });
    Prototype("AudMicrofono",p=>{Cylinder(p,"Mango","Negro",0,.78f,0,.035f,.16f);Part(p,"Capsula","Metal",new Vector3(0,.89f,0),new Vector3(.06f,.07f,.06f),PrimitiveType.Sphere);});
    var seats=Zone(root,"Butacas_Centrales_80_SUPUESTO");
    GameObject Put(string proto,Transform parent,string name,float x,float z,float floor,float rot=0){var g=Place(proto,parent,name,x,z,rot);g.transform.position+=Vector3.up*(floor-.20f);return g;}
    for(int r=0;r<8;r++)for(int col=0;col<10;col++)Put("AudButaca",seats,"Butaca_F"+(r+1)+"_C"+(col+1)+"_SUPUESTO",22.68f+(col-4.5f)*.70f,25.80f+r*1.15f,.08f-r*.12f);
    var stage=Zone(root,"Equipo_Escenario");
    Put("AudMesaLarga",stage,"Mesa_Larga_SUPUESTO",21.2f,37f,-.04f);Put("AudMesaCorta",stage,"Mesa_Corta_SUPUESTO",25.8f,37f,-.04f);
    Put("AudSilla",stage,"Silla_1_SUPUESTO",20.3f,37.8f,-.04f,180);Put("AudSilla",stage,"Silla_2_SUPUESTO",22f,37.8f,-.04f,180);
    Put("AudBocina",stage,"Bocina_SUPUESTO",24.6f,38f,-.04f);Put("AudPanelMovil",stage,"Pizarron_Movil_SUPUESTO",26.1f,38.1f,-.04f);
    Put("AudPlanta",stage,"Planta_Maceta_SUPUESTO",27.4f,37.6f,-.04f);Put("AudMicrofono",stage,"Microfono_Sobre_Mesa_SUPUESTO",21.2f,37f,-.04f);
    var night=Zone(root,"Iluminacion_NOCHE_SUPUESTO");var tracker=root.gameObject.AddComponent<Docencia2InteriorModeTracker>();tracker.nightGroup=night.gameObject;tracker.switcher=Object.FindAnyObjectByType<Docencia2ModeSwitcher>();
    foreach(float x in new[]{19.5f,22.68f,25.86f})foreach(float z in new[]{26f,29.5f,33f,36.5f}){
     Docencia2InteriorBuilder.CreatePlanBox("Marco_Luminaria_Rectangular_SUPUESTO",root,x-.62f,x+.62f,3.14f,3.175f,z-.17f,z+.17f,mats["Metal"],0,false);
     Docencia2InteriorBuilder.CreatePlanBox("Difusor_Apagado",root,x-.58f,x+.58f,3.13f,3.14f,z-.13f,z+.13f,mats["Beige"],0,false);
     Docencia2InteriorBuilder.CreatePlanBox("LED_Noche",night,x-.58f,x+.58f,3.125f,3.13f,z-.13f,z+.13f,mats["LED_Noche"],0,false);
     var go=new GameObject("Luz_Auditorio_Noche");go.transform.SetParent(night,false);go.transform.position=Docencia2InteriorBuilder.PlanToUnityPos(x,3.02f,z);go.transform.rotation=Quaternion.Euler(90,0,0);
     var l=go.AddComponent<Light>();l.type=LightType.Spot;l.spotAngle=140;l.innerSpotAngle=105;l.range=6;l.intensity=2.3f;l.shadows=LightShadows.None;l.cullingMask=1<<8;go.AddComponent<UniversalAdditionalLightData>().renderingLayers=128;
    }
    foreach(var t in root.GetComponentsInChildren<Transform>(true))t.gameObject.layer=8;
    foreach(var r in root.GetComponentsInChildren<Renderer>(true)){r.renderingLayerMask=130;r.shadowCastingMode=ShadowCastingMode.Off;}
    foreach(var r in architecture.GetComponentsInChildren<Renderer>(true))r.renderingLayerMask|=128;
    var previousLight=Object.FindObjectsByType<Light>(FindObjectsInactive.Include).FirstOrDefault(l=>l.name=="LuzInt_Noche_12_Auditorio");if(previousLight)previousLight.enabled=false;
   }finally{foreach(var p in prototypes.Values)if(p)Object.DestroyImmediate(p);}
   AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
  }
  static void AudTable(Transform p,float w){Box(p,"Tablero","Aud_Madera",0,.72f,0,w,.04f,.55f);foreach(float x in new[]{-w/2+.06f,w/2-.06f})foreach(float z in new[]{-.22f,.22f})Box(p,"Pata","Metal",x,.35f,z,.025f,.70f,.025f);}
 }
}


