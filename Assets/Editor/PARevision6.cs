using System;using System.IO;using System.Linq;using System.Reflection;using UnityEngine;using UnityEditor;using UnityEditor.SceneManagement;using UnityEngine.Rendering;using Vaelith.Docencia2;using Object=UnityEngine.Object;
public static class PARevision6 {
const string O=@"C:/Users/faust/OneDrive/Desktop/VAELITH/PA_Revision6";
static Transform zone;static float floor;static int layer;static uint mask;
static Vector3 W(float x,float y,float z)=>Docencia2InteriorBuilder.PlanToUnityPos(x,y,z);
static Material M(string n)=>AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Docencia2/Fase5/F5_"+n+".mat");
static Transform G(string name,Transform p){var g=new GameObject(name);g.transform.SetParent(p,false);return g.transform;}
static GameObject B(Transform p,string name,string mat,float x,float y,float z,float dx,float dy,float dz){var g=GameObject.CreatePrimitive(PrimitiveType.Cube);g.name=name;g.transform.SetParent(p,false);g.transform.position=W(x,floor+y,z);g.transform.localScale=new Vector3(dz*54/53.92f,dy,dx*28.5f/28.48f);g.layer=layer;g.isStatic=true;var r=g.GetComponent<Renderer>();r.sharedMaterial=M(mat);r.shadowCastingMode=ShadowCastingMode.Off;r.renderingLayerMask=mask;Object.DestroyImmediate(g.GetComponent<Collider>());return g;}
static void Wall(Transform p,bool alongX,float constant,float begin,float end,float height){float mid=(begin+end)/2;
B(p,"Azulejo_Base","AzulejoBeige",alongX?mid:constant,height/2,alongX?constant:mid,alongX?end-begin:.014f,height,alongX?.014f:end-begin);
// Thin joints, continuous and slightly inset toward the room rather than coplanar meshes.
for(float u=begin+.30f;u<end;u+=.30f)B(p,"Junta_Vertical","Beige",alongX?u:constant,height/2,alongX?constant:u,alongX?.0045f:.030f,height,alongX?.030f:.0045f);
for(float y=.30f;y<height;y+=.30f)B(p,"Junta_Horizontal","Beige",alongX?mid:constant,y,alongX?constant:mid,alongX?end-begin:.030f,.0045f,alongX?.030f:end-begin);
}
static void Bath(Transform bath,bool male,bool upper){zone=bath;layer=upper?30:8;mask=upper?17u:(male?10u:6u);var vanity=bath.Cast<Transform>().First(t=>t.name.StartsWith("Lavabos_"));floor=vanity.position.y;float west=male?14.24f:9.24f,east=west+5;
foreach(string n in new[]{"Acabado_Azulejo_SUPUESTO","Detalles_Bano_Revision6_SUPUESTO"}){var t=bath.Find(n);if(t)Object.DestroyImmediate(t.gameObject);}
var details=G("Detalles_Bano_Revision6_SUPUESTO",bath);
Wall(details,false,west+.10f,.49f,6.31f,2.65f);Wall(details,false,east-.10f,.49f,6.31f,2.65f);Wall(details,true,.47f,west+.11f,east-.11f,2.20f);
// Floor drain seen in the women's washbasin photo; position estimated.
var drain=B(details,"Coladera_SUPUESTO","CromoBano",west+2.1f,.008f,4.30f,.13f,.008f,.13f);
for(int i=0;i<5;i++)B(details,"Ranura_Coladera","Negro",west+2.05f+i*.025f,.013f,4.30f,.008f,.002f,.10f);
if(male){foreach(var old in bath.Cast<Transform>().Where(t=>t.name.StartsWith("Espejo_")).ToArray())Object.DestroyImmediate(old.gameObject);
var source=GameObject.Find("PB_Interior/05_Fase5_Mobiliario_PB/Bano_Mujeres/Espejo_Largo_SUPUESTO").transform;
var mirror=Object.Instantiate(source.gameObject,bath);mirror.name="Espejo_Corrido_Usuario";mirror.transform.position=W(east-.13f,floor,4.30f);mirror.transform.rotation=Quaternion.Euler(0,90,0);mirror.transform.localScale=new Vector3(1,1,1.95f/2.9f);
foreach(var t in mirror.GetComponentsInChildren<Transform>(true))t.gameObject.layer=layer;foreach(var r in mirror.GetComponentsInChildren<Renderer>(true)){r.renderingLayerMask=mask;r.shadowCastingMode=ShadowCastingMode.Off;}}
foreach(var mir in bath.GetComponentsInChildren<Docencia2Mirror>(true)){mir.surfaces=bath.GetComponentsInChildren<Renderer>(true).Where(r=>r.sharedMaterial&&r.sharedMaterial.name=="F5_Espejo").ToArray();mir.normal=male?Vector3.forward:Vector3.back;mir.viewingBounds=new Bounds(W(west+2.5f,floor+1.5f,3.5f),new Vector3(6.9f,3.3f,4.95f));}
// Photographed soap and paper accessories; retain original fixture alignment.
foreach(var soap in bath.Cast<Transform>().Where(t=>t.name.StartsWith("Jabon_")))soap.position=new Vector3(soap.position.x,floor,soap.position.z);
float wallX=male?east-.125f:west+.125f;
B(details,"Aviso_Papel_Sin_Texto_Inventado","Papel",wallX,1.06f,5.17f,.008f,.28f,.20f);
if(male){var ret=B(details,"Retorno_Azulejo_Lavabos_SUPUESTO","AzulejoBeige",east-.80f,1.10f,3.13f,1.40f,2.20f,.10f);ret.AddComponent<BoxCollider>();
for(float x=east-1.50f;x<east-.1f;x+=.30f)B(details,"Junta_Retorno","Beige",x,1.10f,3.186f,.0045f,2.2f,.003f);
for(float y=.3f;y<2.2f;y+=.3f)B(details,"Junta_Retorno","Beige",east-.80f,y,3.186f,1.4f,.0045f,.003f);
var paper=bath.Find("Papel_SUPUESTO");if(paper){paper.position=W(east-.72f,floor,3.23f);paper.rotation=Quaternion.identity;}
}
File.AppendAllText(O+"/verificacion.txt",(upper?"PA":"PB")+" "+bath.name+": "+bath.Cast<Transform>().Count(t=>t.name.StartsWith("Cabina_"))+" cabinas contra muro central, "+(male?2:3)+" lavabos. Espejos ajustados al piso Y="+floor+".\n");
}
public static void Run(){var scene=EditorSceneManager.OpenScene("Assets/Scenes/Exterior_Docencia2.unity");File.WriteAllText(O+"/verificacion.txt","");var pa=GameObject.Find("PA_Interior").transform;foreach(string n in new[]{"Proteccion_Nuevo_Borde_Oeste","Pasamanos_Nuevo_Borde_Oeste"}){var t=pa.Find("Escalera_Encuentro_Desplazado_1m/"+n);if(t)Object.DestroyImmediate(t.gameObject);}
foreach(bool upper in new[]{false,true})foreach(bool male in new[]{false,true}){string n=male?"Bano_Hombres":"Bano_Mujeres";var b=upper?pa.Find(n+"/Mobiliario_PA_SUPUESTO"):GameObject.Find("PB_Interior/05_Fase5_Mobiliario_PB/"+n).transform;Bath(b,male,upper);}
typeof(PARevision3).GetMethod("Audit",BindingFlags.NonPublic|BindingFlags.Static).Invoke(null,new object[]{pa});EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
var go=new GameObject("TEMP_PA6_CAMERA");var c=go.AddComponent<Camera>();c.enabled=false;c.nearClipPlane=.06f;c.farClipPlane=100;c.fieldOfView=70;var shot=typeof(PARevision3).GetMethod("Shot",BindingFlags.NonPublic|BindingFlags.Static);
void Snap(string n,float x,float y,float z,float tx,float ty,float tz){shot.Invoke(null,new object[]{c,"PA6_"+n,x,y,z,tx,ty,tz});File.Copy(@"C:/Users/faust/OneDrive/Desktop/VAELITH/PA_Revision3/PA6_"+n+".png",O+"/"+n+".png",true);}
foreach(bool upper in new[]{false,true}){float fy=upper?3.606f:.20f;string pre=upper?"PA":"PB";Snap(pre+"_Mujeres",12.2f,fy+1.6f,5.8f,9.6f,fy+1.2f,3.3f);Snap(pre+"_Hombres",16.8f,fy+1.6f,5.8f,18.9f,fy+1.2f,3.5f);Snap(pre+"_Cabinas",10.5f,fy+1.6f,5.8f,13.4f,fy+1.1f,2.5f);}
Snap("Escalera_Sin_Murete",1.0f,5.7f,23,1,3.4f,16);Object.DestroyImmediate(go);File.WriteAllText(O+"/success.txt","PA6_OK");}
}
