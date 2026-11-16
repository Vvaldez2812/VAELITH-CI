using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEditor;

namespace Vaelith.Docencia2
{
    // User explicitly authorized Local, Alcoba and bathrooms after the earlier empty-space restriction.
    public static partial class Docencia2Phase5Builder
    {
        static void SupplementalPB(Transform root)
        {
            MaterialFor("Porcelana",new Color(.91f,.92f,.90f),.65f,0);
            MaterialFor("AzulejoBeige",new Color(.65f,.60f,.47f),.24f,0);
            MaterialFor("Junta",new Color(.32f,.30f,.25f),.15f,0);
            var mirrorMat=MaterialFor("Espejo",Color.white,1,1);
            mirrorMat.shader=Shader.Find("VAELITH/InteriorMirror");
            Prototype("PuestoLocal",LocalDesk);
            Prototype("MesaOval",p=>{
                Part(p,"Cubierta_Oval","Madera",new Vector3(0,.73f,0),new Vector3(3f,.045f,1.40f),PrimitiveType.Cylinder);
                foreach(float x in new[]{-.85f,.85f}){Box(p,"Base","Negro",x,.36f,0,.12f,.70f,.12f);Box(p,"Pie","Negro",x,.035f,0,.65f,.07f,.70f);}
            });
            Prototype("SillaReunion",p=>VisitorChair(p,0,0,"Negro"));
            MaterialFor("CromoBano",new Color(.72f,.75f,.77f),.75f,.8f);MaterialFor("AguaBano",new Color(.22f,.34f,.34f),.7f,.1f);
            Prototype("Inodoro",Toilet);Prototype("CabinaBano",Stall);
            Prototype("Lavabos3",p=>Vanity(p,3));Prototype("Lavabos2",p=>Vanity(p,2));
            Prototype("EspejoLargo",p=>Mirror(p,2.9f));Prototype("EspejoIndividual",p=>Mirror(p,.85f));
            Prototype("DispensadorJabon",p=>{Box(p,"Dispensador","Negro",0,1.10f,0,.16f,.22f,.09f);Box(p,"Pulsador","Beige",0,1.01f,.055f,.10f,.05f,.035f);});
            Prototype("DispensadorPapel",p=>{Box(p,"Dispensador","Negro",0,1.40f,0,.31f,.43f,.18f);Box(p,"Papel","Papel",0,1.27f,.10f,.23f,.17f,.015f);});
            Prototype("PapeleraBano",p=>{Box(p,"Bote_Negro","Negro",0,.18f,0,.27f,.36f,.27f);Box(p,"Abertura","Junta",0,.365f,0,.21f,.008f,.21f);});
            Prototype("Lavadero",p=>{Box(p,"Pedestal_Azulejo","AzulejoBeige",0,.32f,0,.62f,.64f,.58f);OvalRim(p,"Pileta",0,.65f,0,.27f,.24f,.22f,.19f,.08f,"Porcelana");Box(p,"Fondo","Porcelana",0,.58f,0,.43f,.025f,.36f);Box(p,"Grifo","Metal",0,1.02f,-.24f,.035f,.045f,.14f);});
            var local=Zone(root,"Local_Este");
            Place("PuestoLocal",local,"Estacion_PC_1_SUPUESTO",20.5f,10.8f,-90);
            Place("PuestoLocal",local,"Estacion_PC_2_SUPUESTO",20.5f,13.3f,-90);
            var alcoba=Zone(root,"Alcoba_Oeste");Place("MesaOval",alcoba,"Mesa_Oval_3x1_4_SUPUESTO",7.43f,12f);
            for(int i=0;i<3;i++){
                Place("SillaReunion",alcoba,"Silla_Norte_"+(i+1)+"_SUPUESTO",6.53f+i*.90f,10.85f);
                Place("SillaReunion",alcoba,"Silla_Sur_"+(i+1)+"_SUPUESTO",6.53f+i*.90f,13.15f,180);
            }
            Place("SillaReunion",alcoba,"Silla_Oeste_SUPUESTO",5.48f,12f,-90);
            Place("SillaReunion",alcoba,"Silla_Este_SUPUESTO",9.38f,12f,90);
            Bathroom(root,false);Bathroom(root,true);
        }
        static void LocalDesk(Transform p)
        {
            Box(p,"Cubierta","Madera",0,.73f,0,1.8f,.04f,.80f);Box(p,"Faldon","Crema",0,.40f,-.25f,1.70f,.53f,.025f);
            foreach(float x in new[]{-.83f,.83f})foreach(float z in new[]{-.33f,.33f})Box(p,"Pata","Metal",x,.36f,z,.035f,.72f,.035f);
            Box(p,"Monitor","Negro",0,1.06f,-.04f,.54f,.33f,.05f);Box(p,"Pantalla","Pantalla",0,1.06f,-.009f,.50f,.29f,.008f);
            Box(p,"Pie_Monitor","Negro",0,.83f,-.04f,.045f,.16f,.04f);Box(p,"Base","Negro",0,.76f,-.04f,.23f,.025f,.15f);
            Box(p,"Teclado","Negro",0,.76f,.20f,.40f,.018f,.13f);Box(p,"Raton","Negro",.29f,.765f,.20f,.055f,.025f,.085f);
            Box(p,"Torre_PC","Negro",.48f,.24f,0,.19f,.46f,.40f);OfficeChair(p,0,.80f);
        }
        static void OvalRim(Transform p,string name,float x,float y,float z,float rx,float rz,float ix,float iz,float height,string mat)
        {
            const int n=32;var verts=new Vector3[n*4];var tris=new int[n*4*6];int t=0;
            for(int ring=0;ring<4;ring++)for(int j=0;j<n;j++){
                float a=j*Mathf.PI*2/n;bool outer=ring==0||ring==3;float px=x+Mathf.Cos(a)*(outer?rx:ix),pz=z+Mathf.Sin(a)*(outer?rz:iz);
                verts[ring*n+j]=new Vector3(-pz*SZ,y-(ring>=2?height:0),-px*SX);
            }
            for(int ring=0;ring<4;ring++)for(int j=0;j<n;j++){
                int a=ring*n+j,b=ring*n+(j+1)%n,c=((ring+1)%4)*n+j,d=((ring+1)%4)*n+(j+1)%n;
                tris[t++]=a;tris[t++]=b;tris[t++]=c;tris[t++]=b;tris[t++]=d;tris[t++]=c;
            }
            var mesh=new Mesh{name=name};mesh.vertices=verts;mesh.triangles=tris;mesh.RecalculateNormals();mesh.RecalculateBounds();
            var go=new GameObject(name);go.transform.SetParent(p,false);go.AddComponent<MeshFilter>().sharedMesh=mesh;go.AddComponent<MeshRenderer>().sharedMaterial=mats[mat];
        }
        static void Toilet(Transform p)
        {
            DetailedToilet(p);
        }
        static void Stall(Transform p)
        {
            foreach(float x in new[]{-.65f,.65f}){
                Box(p,"Mampara_Crema","Crema",x,1.15f,0,.035f,1.80f,1.70f);
                foreach(float z in new[]{-.80f,.82f})Box(p,"Poste","Metal",x,1.15f,z,.035f,2.30f,.035f);
                Box(p,"Travesano","Metal",x,2.28f,0,.035f,.035f,1.70f);
            }
            foreach(float x in new[]{-.525f,.525f})Box(p,"Frente_Fijo","Crema",x,1.15f,.85f,.25f,1.80f,.035f);
            Box(p,"Puerta_Cerrada_0_80","Crema",0,1.15f,.85f,.79f,1.79f,.035f);
            Box(p,"Dintel","Metal",0,2.28f,.85f,1.30f,.035f,.035f);
            StallDetail(p);
        }
        static void Vanity(Transform p,int count)
        {
            DetailedVanity(p,count);
        }
        static void Mirror(Transform p,float width)
        {
            Box(p,"Espejo_Aproximado","Espejo",0,1.62f,0,width,.76f,.02f);
            foreach(float x in new[]{-width/2,width/2})Box(p,"Marco",width<1?"Negro":"Madera",x,1.62f,.01f,.035f,.80f,.035f);
            foreach(float y in new[]{1.22f,2.02f})Box(p,"Marco",width<1?"Negro":"Madera",0,y,.01f,width+.035f,.035f,.035f);
        }
        static void Bathroom(Transform root,bool male)
        {
            float west=male?14.24f:9.24f,east=west+5;
            var zone=Zone(root,male?"Bano_Hombres":"Bano_Mujeres");
            // Three visible stall fronts in the women's reference. Only one clearly visible men's stall is represented.
            int count=male?1:3;
            for(int i=0;i<count;i++){
                float z=1.25f+i*1.35f;
                Place("CabinaBano",zone,"Cabina_"+(i+1)+"_SUPUESTO",east-.945f,z,90);
                Place("Inodoro",zone,"Inodoro_"+(i+1)+"_SUPUESTO",east-.59f,z,90);
            }
            Place(male?"Lavabos2":"Lavabos3",zone,"Lavabos_"+(male?2:3)+"_SUPUESTO",west+.46f,4.30f,-90);
            // User requested a single continuous mirror in all four bathrooms.
            var continuousMirror=Place("EspejoLargo",zone,"Espejo_Largo_SUPUESTO",west+.13f,4.30f,-90);
            if(male)continuousMirror.transform.localScale=new Vector3(1,1,1.95f/2.9f);
            Place("DispensadorJabon",zone,"Jabon_SUPUESTO",west+.15f,male?4.30f:4.0f,-90);
            Place("DispensadorPapel",zone,"Papel_SUPUESTO",west+.18f,2.55f,-90);
            Place("PapeleraBano",zone,"Bote_Negro_SUPUESTO",east-.35f,5.60f);
            if(male)Place("DispensadorPapel",zone,"Papel_Lateral_SUPUESTO",east-.18f,4.80f,90);
            else Place("Lavadero",zone,"Lavadero_SUPUESTO",west+.60f,.80f);
            // Mirror male fixtures around the bathroom center: cabins against shared x=14.24 wall.
            if(male)foreach(Transform fixture in zone){
                var pos=fixture.position;pos.z=2*(14.25f-(west+2.5f)*SX)-pos.z;fixture.position=pos;
                fixture.rotation=Quaternion.Euler(0,-fixture.eulerAngles.y,0);
            }
            var mirrors=new System.Collections.Generic.List<Renderer>();
            foreach(var renderer in zone.GetComponentsInChildren<Renderer>())if(renderer.sharedMaterial==mats["Espejo"])mirrors.Add(renderer);
            var reflection=zone.gameObject.AddComponent<Docencia2Mirror>();reflection.surfaces=mirrors.ToArray();
            reflection.normal=male?Vector3.forward:Vector3.back;
            reflection.viewingBounds=new Bounds(Docencia2InteriorBuilder.PlanToUnityPos(west+2.5f,1.6f,3.8f),new Vector3(9f,5f,5.7f));
            // Thin finish panels and grout; the existing wall/slab/door geometry is preserved.
            Prototype("AzulejoBano"+(male?"H":"M"),p=>{
                void Panel(float x,float z,float w,float rot){var g=new GameObject("Acabado_Azulejo").transform;g.SetParent(p,false);g.localPosition=new Vector3(-z*SZ,0,-x*SX);g.localRotation=Quaternion.Euler(0,rot,0);
                    Box(g,"Base","AzulejoBeige",0,1.10f,0,w,2.20f,.012f);
                    for(float u=-w/2+.30f;u<w/2;u+=.30f)Box(g,"Junta_Vertical","Junta",u,1.10f,.011f,.005f,2.20f,.003f);
                    for(float y=.30f;y<2.20f;y+=.30f)Box(g,"Junta_Horizontal","Junta",0,y,.011f,w,.005f,.003f);
                }
                Panel(west+2.5f,.45f,4.80f,0);Panel(west+.09f,3.40f,5.85f,-90);Panel(east-.09f,3.40f,5.85f,90);
            });
            // The prototype uses absolute plan coordinates, so anchor at plan origin.
            var finish=Place("AzulejoBano"+(male?"H":"M"),zone,"Acabado_Azulejo_SUPUESTO",0,0);
            Object.DestroyImmediate(finish.GetComponent<BoxCollider>());
            uint mask=male?8u:4u;foreach(var r in zone.GetComponentsInChildren<Renderer>())r.renderingLayerMask=2u|mask;
            var architecture=root.parent.Find(male?"09_Bano_Hombres":"08_Bano_Mujeres");
            if(architecture)foreach(var r in architecture.GetComponentsInChildren<Renderer>())r.renderingLayerMask|=mask;
            var night=Zone(zone,"Iluminacion_NOCHE_SUPUESTO");var tracker=zone.gameObject.AddComponent<Docencia2InteriorModeTracker>();tracker.nightGroup=night.gameObject;tracker.switcher=Object.FindAnyObjectByType<Docencia2ModeSwitcher>();
            foreach(float z in new[]{2.0f,4.8f}){
                Place("PanelLED",zone,"Panel_Bano_SUPUESTO",west+2.20f,z);Place("PanelLED_Encendido",night,"LED_Noche_SUPUESTO",west+2.20f,z);
                var go=new GameObject("Luz_Bano_Noche");go.transform.SetParent(night,false);go.transform.position=Docencia2InteriorBuilder.PlanToUnityPos(west+2.20f,2.95f,z);
                var l=go.AddComponent<Light>();l.type=LightType.Point;l.intensity=2.5f;l.range=4.0f;l.color=new Color(.97f,.98f,1f);l.shadows=LightShadows.None;l.cullingMask=1<<8;go.AddComponent<UniversalAdditionalLightData>().renderingLayers=mask;
            }
            AddBathFill(zone,male);
        }
    }
}
