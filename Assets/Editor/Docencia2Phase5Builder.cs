using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEditor;
using UnityEditor.SceneManagement;

namespace Vaelith.Docencia2
{
    // Approved Step 1. Dimensions/placements are assumptions, never a site survey.
    // Only owns PB_Interior/05_Fase5_Mobiliario_PB and Assets/*/Docencia2/Fase5.
    public static partial class Docencia2Phase5Builder
    {
        const string ScenePath = "Assets/Scenes/Exterior_Docencia2.unity";
        const string MeshDir = "Assets/Models/Docencia2/Fase5";
        const string MatDir = "Assets/Materials/Docencia2/Fase5";
        const float SX=28.50f/28.48f, SZ=54f/53.92f;
        static Dictionary<string, Material> mats;
        static Dictionary<string, GameObject> prototypes;

        public static void Build()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Salir de Play antes de construir.");
            if(EditorSceneManager.GetActiveScene().path != ScenePath) throw new InvalidOperationException("Abrir la escena maestra antes de construir.");
            var pb=GameObject.Find("PB_Interior");
            if(!pb) throw new InvalidOperationException("Falta PB_Interior.");
            Directory.CreateDirectory(MeshDir); Directory.CreateDirectory(MatDir); AssetDatabase.Refresh();
            var previous=pb.transform.Find("05_Fase5_Mobiliario_PB");
            if(previous) UnityEngine.Object.DestroyImmediate(previous.gameObject);
            var root=new GameObject("05_Fase5_Mobiliario_PB").transform;root.SetParent(pb.transform,false);
            mats=new Dictionary<string,Material>();prototypes=new Dictionary<string,GameObject>();
            Material M(string n,Color c,float smooth=.3f,float metal=0){return MaterialFor(n,c,smooth,metal);}
            M("Madera",new Color(.37f,.20f,.095f),.35f);M("Crema",new Color(.76f,.72f,.57f));
            M("Negro",new Color(.025f,.03f,.033f),.25f);M("Pantalla",new Color(.018f,.035f,.045f),.55f);
            M("VerdeAzulado",new Color(.025f,.26f,.25f),.42f);M("Azul",new Color(.04f,.10f,.36f));
            M("Metal",new Color(.25f,.28f,.29f),.58f,.75f);M("Papel",new Color(.92f,.92f,.87f),.1f);
            M("Amarillo",new Color(.94f,.62f,.025f),.4f);M("Rojo",new Color(.55f,.035f,.025f),.4f);
            M("CarteleraAzul",new Color(.025f,.10f,.40f));M("Beige",new Color(.53f,.50f,.41f));
            M("VerdeSenal",new Color(.025f,.36f,.15f));
            var led=M("LED_Noche",new Color(.93f,.95f,1f));
            led.globalIlluminationFlags=MaterialGlobalIlluminationFlags.BakedEmissive;
            led.SetColor("_EmissionColor",new Color(3.255f,3.325f,3.5f,1f));led.EnableKeyword("_EMISSION");EditorUtility.SetDirty(led);
            try
            {
                for(int variant=0;variant<3;variant++){
                    int v=variant;Prototype("Puesto"+v,p=>Workstation(p,v));
                    Prototype("PuestoFrontal120_"+v,p=>Workstation(p,v,.75f));
                    Prototype("PuestoFrontal140_"+v,p=>Workstation(p,v,.875f));
                }
                Prototype("Repisa",Shelf);Prototype("Pupitre",StudentDesk);Prototype("Pizarron",Whiteboard);
                Prototype("Profesor",TeacherDesk);Prototype("Banca4",Bench);Prototype("Bote",Bin);
                Prototype("PanelLED",p=>{Box(p,"Marco","Negro",0,2.985f,0,.60f,.025f,.60f);Box(p,"Difusor_Apagado","Beige",0,2.968f,0,.52f,.012f,.52f);});
                Prototype("PanelLED_Encendido",p=>Box(p,"Difusor_Noche","LED_Noche",0,2.958f,0,.52f,.005f,.52f));
                Prototype("MaquinaAmarilla",p=>Vending(p,"Amarillo"));Prototype("MaquinaRoja",p=>Vending(p,"Rojo"));
                Prototype("Cartelera",Noticeboard);Prototype("Extintor",Extinguisher);
                for(int group=0;group<4;group++) for(int row=0;row<3;row++)
                {
                    string id="ABCD"[group].ToString()+(row+1);
                    var zone=Zone(root,"Cubiculo_"+id);
                    float x=new[]{1.55f,7.69f,20.79f,26.93f}[group];
                    float mid=new[]{1.465f,3.855f,6.425f}[row];
                    bool mirrored=group==1||group==3;
                    bool frontal=(group+row)%2==0;int density=(group+row*2)%3;
                    float rotation=frontal?(mirrored?-90:90):(mirrored?180:0);
                    float deskZ=frontal?new[]{.36f,2.57f,5.14f}[row]+.05f+(row==0?.60f:.70f):mid;
                    string proto=frontal?(row==0?"PuestoFrontal120_":"PuestoFrontal140_")+density:"Puesto"+density;
                    Place(proto,zone,id+(frontal?"_Frente_Puerta":"_Lateral")+"_Densidad"+density+"_SUPUESTO",x,deskZ,rotation);
                    if((group+row)%3!=0){
                        float south=new[]{2.57f,5.14f,7.71f}[row];
                        float shelfZ=frontal?new[]{.36f,2.57f,5.14f}[row]+.20f:south-.20f;
                        Place("Repisa",zone,id+"_Repisa_SUPUESTO",x,shelfZ,frontal?0:180);
                    }
                }
                Classrooms(root);
                SupplementalPB(root);
                var vest=Zone(root,"Vestibulo");
                Place("MaquinaAmarilla",vest,"Expendedora_Amarilla_SUPUESTO",27.40f,16.40f);
                Place("MaquinaRoja",vest,"Expendedora_Roja_SUPUESTO",26.25f,16.40f);
                Place("Cartelera",vest,"Cartelera_Azul_SUPUESTO",22.40f,23.04f,180);
                Place("Banca4",vest,"Banca_4_Plazas_SUPUESTO",19.80f,15.80f);
                Place("Extintor",vest,"Extintor_Ubicacion_SUPUESTO",17.45f,15.55f);
                var corridor=Zone(root,"Pasillo_Central");
                // One conservative four-seat module per visible row; no invented hidden capacity.
                foreach(float z in new[]{26.5f,35f})
                {
                    Place("Banca4",corridor,"Banca_Oeste_4_Plazas_SUPUESTO_z"+z,11.65f,z,-90);
                    Place("Banca4",corridor,"Banca_Este_4_Plazas_SUPUESTO_z"+z,16.83f,z,90);
                }
                Place("Bote",corridor,"Bote_Beige_SUPUESTO",11.68f,23.95f);
                Sign(corridor,"Placa_Aula7_SUPUESTO","AULA 7",11.35f,29.88f,2.23f,.25f,.08f,-90);
                Sign(corridor,"Placa_Aula6_SUPUESTO","AULA 6",11.35f,31.84f,2.23f,.25f,.08f,-90);
                var plaza=Zone(root,"Plaza_Sur");
                for(int j=-1;j<=1;j++)Place("Banca4",plaza,"Banca_Este_4_Plazas_SUPUESTO_"+(j+2),20.08f,42.45f+j*1.90f,90);
                Place("Banca4",plaza,"Banca_Oeste_4_Plazas_SUPUESTO",8.40f,42.40f,-90);
                Place("Banca4",plaza,"Banca_Sur_4_Plazas_SUPUESTO",14.10f,45.93f,180);
                // Shifted 0.30 m south of the proposed center to clear the P19 jamb/swing approach.
                Place("Bote",plaza,"Bote_Beige_SUPUESTO_z40.00",8.50f,40f);
                Sign(plaza,"Senal_Evacuacion_Pictograma_SUPUESTO","<",8.10f,40f,1.55f,.30f,.15f,-90,true);
                foreach(var t in root.GetComponentsInChildren<Transform>(true))t.gameObject.layer=8;
            }
            finally {foreach(var p in prototypes.Values)if(p)UnityEngine.Object.DestroyImmediate(p);}
            // Generation templates must be destroyed before serializing the scene.
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());AssetDatabase.SaveAssets();
        }
        static Transform Zone(Transform root,string n){var g=new GameObject(n);g.transform.SetParent(root,false);return g.transform;}
        static Material MaterialFor(string n,Color col,float smooth,float metallic)
        {
            string path=MatDir+"/F5_"+n+".mat";var m=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(!m){m=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(m,path);}
            m.SetColor("_BaseColor",col);m.SetFloat("_Smoothness",smooth);m.SetFloat("_Metallic",metallic);
            m.enableInstancing=true;EditorUtility.SetDirty(m);mats[n]=m;return m;
        }
        static GameObject Part(Transform p,string n,string mat,Vector3 pos,Vector3 scale,PrimitiveType shape=PrimitiveType.Cube)
        {
            var go=GameObject.CreatePrimitive(shape);go.name=n;go.transform.SetParent(p,false);
            go.transform.localPosition=new Vector3(-pos.z*SZ,pos.y,-pos.x*SX);
            go.transform.localScale=new Vector3(scale.z*SZ,scale.y*(shape==PrimitiveType.Cylinder?.5f:1),scale.x*SX);
            UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());go.GetComponent<Renderer>().sharedMaterial=mats[mat];return go;
        }
        static void Box(Transform p,string n,string m,float x,float y,float z,float dx,float dy,float dz){Part(p,n,m,new Vector3(x,y,z),new Vector3(dx,dy,dz));}
        static void Cylinder(Transform p,string n,string m,float x,float y,float z,float diameter,float h){Part(p,n,m,new Vector3(x,y,z),new Vector3(diameter,h,diameter),PrimitiveType.Cylinder);}
        static void Workstation(Transform p,int density,float widthFactor=1)
        {
            Box(p,"Cubierta","Madera",0,.73f,0,1.60f,.04f,.65f);
            foreach(float x in new[]{-.57f,.57f})
            {
                Box(p,"Pedestal","Crema",x,.36f,0,.38f,.70f,.55f);
                for(int j=0;j<3;j++){Box(p,"Frente_Cajon","Crema",x,.25f+j*.16f,-.285f,.35f,.15f,.018f);Box(p,"Tirador","Metal",x,.28f+j*.16f,-.305f,.13f,.012f,.018f);}
            }
            Box(p,"Faldon","Crema",0,.43f,-.255f,.78f,.48f,.025f);
            Cylinder(p,"Soporte_Brazo_Doble","Negro",0,.91f,.07f,.045f,.32f);
            Box(p,"Brazo_Doble","Negro",0,1.05f,.08f,.95f,.035f,.035f);
            foreach(float x in new[]{-.30f,.30f}){
                Box(p,"Monitor_Negro","Negro",x,1.13f,.08f,.55f,.33f,.045f);
                Box(p,"Pantalla_Apagada","Pantalla",x,1.13f,.106f,.51f,.29f,.008f);
            }
            Box(p,"Teclado","Negro",.05f,.762f,.24f,.40f,.018f,.13f);
            Box(p,"Raton","Negro",.32f,.765f,.23f,.055f,.025f,.085f);
            for(int j=0;j<new[]{1,4,7}[density];j++)Box(p,"Papeles","Papel",-.55f,.759f+j*.007f,-.01f,.30f,.005f,.22f);
            for(int j=0;j<density+1;j++){
                Box(p,"Organizador_Bandeja","Negro",.55f,.79f+j*.075f,-.04f,.30f,.012f,.22f);
                Box(p,"Papeles_Bandeja","Papel",.55f,.801f+j*.075f,-.04f,.27f,.008f,.19f);
            }
            foreach(float x in new[]{.405f,.695f})Box(p,"Organizador_Lateral","Negro",x,.79f+density*.0375f,-.04f,.01f,.07f+density*.075f,.22f);
            // Compact frontal desktops; chair sizes stay unchanged. Leaves access around the side of the desk.
            foreach(Transform t in p){var pos=t.localPosition;pos.z*=widthFactor;t.localPosition=pos;var scale=t.localScale;scale.z*=widthFactor;t.localScale=scale;}
            OfficeChair(p,0,.65f);VisitorChair(p,0,-.65f);
        }
        static void Shelf(Transform p)
        {
            Box(p,"Repisa_Madera","Madera",0,1.85f,0,.80f,.035f,.22f);
            foreach(float x in new[]{-.29f,.29f})Box(p,"Escuadra","Metal",x,1.77f,-.07f,.025f,.15f,.025f);
            for(int i=0;i<5;i++)Box(p,"Carpeta","Crema",-.25f+i*.075f,1.99f,0,.055f,.24f,.17f);
        }
        static void StudentDesk(Transform p)
        {
            Box(p,"Asiento","VerdeAzulado",0,.44f,.14f,.43f,.055f,.42f);
            Box(p,"Respaldo","VerdeAzulado",0,.71f,.385f,.43f,.34f,.045f);
            Box(p,"Paleta_Clara","Crema",0,.75f,-.25f,.70f,.035f,.43f);
            Box(p,"Extension_Paleta","Crema",.26f,.75f,.02f,.18f,.035f,.24f);
            foreach(float x in new[]{-.24f,.24f})foreach(float z in new[]{-.32f,.35f})Box(p,"Pata","Negro",x,.22f,z,.025f,.44f,.025f);
            Box(p,"Soporte_Paleta","Negro",.26f,.58f,-.12f,.025f,.33f,.025f);
            foreach(float x in new[]{-.18f,-.09f,0,.09f,.18f})Box(p,"Parrilla","Negro",x,.16f,.08f,.012f,.012f,.42f);
            foreach(float z in new[]{-.12f,.28f})Box(p,"Marco_Parrilla","Negro",0,.16f,z,.42f,.018f,.018f);
        }
        static void Whiteboard(Transform p)
        {
            Box(p,"Pizarron_Blanco","Papel",0,1.60f,0,2.90f,1.25f,.035f);
            foreach(float x in new[]{-1.47f,1.47f})Box(p,"Marco","Metal",x,1.60f,.01f,.035f,1.30f,.04f);
            foreach(float y in new[]{.96f,2.24f})Box(p,"Marco","Metal",0,y,.01f,2.97f,.035f,.04f);
            Box(p,"Bandeja","Metal",0,.95f,.07f,2.90f,.025f,.12f);
        }
        static void TeacherDesk(Transform p)
        {
            Box(p,"Cubierta","Madera",0,.73f,0,1.20f,.04f,.60f);
            Box(p,"Faldon","Crema",0,.43f,.22f,1.12f,.50f,.025f);
            foreach(float x in new[]{-.55f,.55f})foreach(float z in new[]{-.25f,.25f})Box(p,"Pata","Metal",x,.36f,z,.035f,.72f,.035f);
            VisitorChair(p,0,-.62f,"Negro");
        }
        static void Classrooms(Transform root)
        {
            // x0,x1,z0,z1, entry wall: -1 east, +1 west, 0 north. All placements SUPUESTO.
            float[][] rooms={new[]{20.48f,28.12f,38.57f,46.29f,1f},new[]{18.99f,28.12f,46.29f,53.54f,0f},new[]{9.49f,18.99f,46.29f,53.54f,0f},new[]{.36f,9.49f,46.29f,53.54f,0f},new[]{.36f,8f,38.57f,46.29f,-1f},new[]{.36f,11.24f,30.86f,38.57f,-1f},new[]{.36f,11.24f,23.14f,30.86f,-1f}};
            for(int i=0;i<rooms.Length;i++){
                var a=rooms[i];var zone=Zone(root,"Aula_"+(i+1));int side=(int)a[4];
                float width=(side==0?a[1]-a[0]:a[3]-a[2])-.18f, depth=(side==0?a[3]-a[2]:a[1]-a[0])-.18f;
                float rot=side==0?0:-side*90;
                Vector2 Map(float u,float v){return side==0?new Vector2((a[0]+a[1])/2+u,a[2]+.09f+v):side<0?new Vector2(a[1]-.09f-v,(a[2]+a[3])/2+u):new Vector2(a[0]+.09f+v,(a[2]+a[3])/2-u);}
                void Put(string proto,string name,float u,float v){var q=Map(u,v);Place(proto,zone,name+"_SUPUESTO",q.x,q.y,rot);}
                Put("Pizarron","Pizarron_Pared_Acceso",0,.10f);
                // Teacher station at the end opposite the access door; keep central route clear.
                float teacherSide=(i==3||i==6)?-1:1;
                if(i==0)teacherSide=-1;
                Put("Profesor","Escritorio_Profesor",teacherSide*(width/2-.90f),1.0f);
                int halfCols=width>8.6f?4:3;
                float pitch=.80f; // Contiguous rows, access retained along both sides.
                int rows=Mathf.FloorToInt((depth-1.42f-2.15f)/1.20f)+1;
                for(int r=0;r<rows;r++)for(int c=0;c<halfCols*2;c++){
                    float u=(c-(halfCols*2-1)/2f)*pitch;
                    Put("Pupitre","Pupitre_F"+(r+1)+"_C"+(c+1),u,2.15f+r*1.20f);
                }
                ClassroomCurtains(zone,i);
                var night=Zone(zone,"Iluminacion_NOCHE_SUPUESTO");
                var tracker=zone.gameObject.AddComponent<Docencia2InteriorModeTracker>();
                tracker.nightGroup=night.gameObject;tracker.switcher=UnityEngine.Object.FindAnyObjectByType<Docencia2ModeSwitcher>();
                // Stay inside the 8-bit rendering-layer buffer. Aulas 1 and 7 are farther apart than lamp range.
                uint lightLayer=(uint)(1<<(2+i%6));
                // A separate rendering layer prevents the new lamps from lighting exterior or neighboring furniture.
                foreach(var r in zone.GetComponentsInChildren<Renderer>())r.renderingLayerMask=2u|lightLayer;
                var architecture=root.parent.Find((31-i).ToString("00")+"_Aula_"+(i+1));
                if(architecture)foreach(var r in architecture.GetComponentsInChildren<Renderer>())r.renderingLayerMask|=lightLayer;
                for(int k=0;k<2;k++){
                    var q=Map(0,depth*(k==0?.32f:.72f));var lamp=new GameObject("Luz_Aula_"+(i+1)+"_"+(k+1));lamp.transform.SetParent(night,false);
                    lamp.transform.position=Docencia2InteriorBuilder.PlanToUnityPos(q.x,2.95f,q.y);
                    var light=lamp.AddComponent<Light>();light.type=LightType.Point;light.range=5.2f;light.intensity=3.0f;light.color=new Color(.97f,.98f,1f);light.shadows=LightShadows.None;light.cullingMask=1<<8;
                    lamp.AddComponent<UniversalAdditionalLightData>().renderingLayers=lightLayer;
                }
                // Aulas 6/7 already have four ceiling fixtures each; add fixtures only where absent.
                if(i<5)foreach(float u in new[]{-width*.24f,width*.24f})foreach(float v in new[]{depth*.32f,depth*.72f}){
                    var q=Map(u,v);Place("PanelLED",zone,"Panel_Plafon_SUPUESTO",q.x,q.y);
                    Place("PanelLED_Encendido",night,"Difusor_Noche_SUPUESTO",q.x,q.y);
                }
            }
        }
        static void OfficeChair(Transform p,float x,float z)
        {
            Cylinder(p,"Silla_Negra_Eje","Metal",x,.25f,z,.065f,.36f);
            for(int i=0;i<5;i++){
                float a=i*Mathf.PI*2/5;float ax=Mathf.Cos(a),az=Mathf.Sin(a);
                var arm=Part(p,"Base_Estrella","Negro",new Vector3(x+ax*.13f,.075f,z+az*.13f),new Vector3(.28f,.035f,.04f));arm.transform.localRotation=Quaternion.Euler(0,-i*72,0);
                Part(p,"Rueda","Negro",new Vector3(x+ax*.26f,.045f,z+az*.26f),new Vector3(.06f,.06f,.06f),PrimitiveType.Sphere);
            }
            Box(p,"Asiento_Negro","Negro",x,.45f,z,.50f,.08f,.49f);
            Box(p,"Respaldo_Negro","Negro",x,.76f,z+.225f,.48f,.48f,.065f);
            foreach(float side in new[]{-.28f,.28f}){
                Box(p,"Apoyabrazos","Negro",x+side,.64f,z,.05f,.045f,.34f);
                Box(p,"Soporte_Brazo","Metal",x+side,.55f,z+.1f,.025f,.20f,.025f);
            }
        }
        static void VisitorChair(Transform p,float x,float z,string seatMaterial="Azul")
        {
            Box(p,"Asiento_Visita",seatMaterial,x,.44f,z,.45f,.06f,.44f);
            Box(p,"Respaldo_Visita",seatMaterial,x,.68f,z-.225f,.45f,.40f,.045f);
            foreach(float a in new[]{-.20f,.20f})foreach(float b in new[]{-.20f,.20f})Box(p,"Pata_Silla","Negro",x+a,.22f,z+b,.025f,.44f,.025f);
        }
        static void Bench(Transform p)
        {
            Box(p,"Travesano","Negro",0,.30f,0,1.86f,.07f,.07f);
            foreach(float x in new[]{-.70f,.70f}){
                Box(p,"Soporte","Negro",x,.16f,0,.045f,.30f,.05f);Box(p,"Pie","Negro",x,.025f,0,.07f,.05f,.52f);
            }
            for(int i=0;i<4;i++){
                float x=(i-1.5f)*.48f;Box(p,"Asiento_VerdeAzulado","VerdeAzulado",x,.43f,.02f,.44f,.055f,.43f);
                Box(p,"Respaldo_Sup","VerdeAzulado",x,.78f,-.19f,.40f,.07f,.055f);
                foreach(float side in new[]{-.16f,.16f})Box(p,"Respaldo_Lateral","VerdeAzulado",x+side,.63f,-.19f,.09f,.28f,.055f);
            }
        }
        static void Bin(Transform p)
        {
            Box(p,"Cuerpo","Beige",0,.43f,0,.50f,.82f,.50f);Box(p,"Tapa","Beige",0,.91f,0,.52f,.18f,.52f);
            Box(p,"Boca_Oscura","Negro",0,.89f,.267f,.36f,.12f,.012f);
        }
        static void Vending(Transform p,string color)
        {
            Box(p,"Gabinete",color,0,.97f,0,.95f,1.86f,.80f);Box(p,"Panel_Exhibidor","Negro",-.08f,1.16f,.408f,.65f,1.25f,.018f);
            for(int row=0;row<4;row++)for(int col=0;col<3;col++)Box(p,"Producto_Esquematico",(row+col)%2==0?"Amarillo":"Rojo",-.29f+col*.21f,.73f+row*.28f,.426f,.12f,.16f,.02f);
            Box(p,"Panel_Mando","Negro",.36f,1.14f,.416f,.14f,.48f,.025f);
            for(int i=0;i<4;i++)Box(p,"Boton","Metal",.36f,1.25f-i*.075f,.434f,.05f,.035f,.012f);
            Box(p,"Entrega","Negro",0,.28f,.414f,.62f,.18f,.02f);
            foreach(float x in new[]{-.36f,.36f})Box(p,"Pie_Maquina","Negro",x,.035f,0,.08f,.07f,.60f);
        }
        static void Noticeboard(Transform p)
        {
            Box(p,"Fondo_Azul","CarteleraAzul",0,1.55f,0,2f,1f,.05f);
            foreach(float x in new[]{-.99f,.99f})Box(p,"Marco_Madera","Madera",x,1.55f,.025f,.04f,1.04f,.06f);
            foreach(float y in new[]{1.05f,2.05f})Box(p,"Marco_Madera","Madera",0,y,.025f,2f,.04f,.06f);
            for(int j=0;j<2;j++)for(int i=0;i<5;i++)Box(p,"Aviso_Sin_Texto_Inventado","Papel",-.75f+i*.37f,1.31f+j*.46f,.035f,.24f,.34f,.005f);
        }
        static void Extinguisher(Transform p)
        {
            Cylinder(p,"Cuerpo_Rojo","Rojo",0,.95f,0,.16f,.42f);Cylinder(p,"Cuello","Metal",0,1.18f,0,.045f,.06f);
            Box(p,"Manija","Negro",0,1.23f,0,.17f,.025f,.055f);Box(p,"Etiqueta","Papel",0,.98f,.082f,.085f,.17f,.004f);
            Box(p,"Manguera","Negro",.095f,1.02f,0,.025f,.34f,.025f);
        }
        static void Prototype(string name,Action<Transform> build)
        {
            var p=new GameObject("__F5_"+name);build(p.transform);
            var renderers=p.GetComponentsInChildren<MeshRenderer>();
            foreach(var mat in renderers.Select(r=>r.sharedMaterial).Distinct()){
                var combines=renderers.Where(r=>r.sharedMaterial==mat).Select(r=>new CombineInstance{mesh=r.GetComponent<MeshFilter>().sharedMesh,transform=r.transform.localToWorldMatrix}).ToArray();
                var mesh=new Mesh{name="F5_"+name+"_"+mat.name};mesh.CombineMeshes(combines,true,true);
                string path=MeshDir+"/"+mesh.name+".asset";var old=AssetDatabase.LoadAssetAtPath<Mesh>(path);
                if(old){EditorUtility.CopySerialized(mesh,old);UnityEngine.Object.DestroyImmediate(mesh);mesh=old;}else AssetDatabase.CreateAsset(mesh,path);
                var go=new GameObject(mat.name);go.transform.SetParent(p.transform,false);go.AddComponent<MeshFilter>().sharedMesh=mesh;
                var mr=go.AddComponent<MeshRenderer>();mr.sharedMaterial=mat;mr.shadowCastingMode=ShadowCastingMode.Off;mr.receiveShadows=true;mr.renderingLayerMask=2u;
                GameObjectUtility.SetStaticEditorFlags(go,StaticEditorFlags.ContributeGI|StaticEditorFlags.OccludeeStatic|StaticEditorFlags.ReflectionProbeStatic);
            }
            foreach(var r in renderers)UnityEngine.Object.DestroyImmediate(r.gameObject);
            p.SetActive(false);prototypes[name]=p;
        }
        static GameObject Place(string prototype,Transform zone,string name,float x,float z,float rotation=0)
        {
            var go=UnityEngine.Object.Instantiate(prototypes[prototype],zone);go.name=name;
            go.transform.position=Docencia2InteriorBuilder.PlanToUnityPos(x,.20f,z);go.transform.rotation=Quaternion.Euler(0,rotation,0);go.SetActive(true);
            // Simple physical hulls; decorative drawers/arms/paper never add collider cost.
            if(prototype=="PuestoLocal"){
                Hull(go.transform,0,.38f,0,1.8f,.75f,.80f);Hull(go.transform,0,.52f,.80f,.62f,1.04f,.62f);
            } else if(prototype.StartsWith("Puesto")){
                float w=prototype.StartsWith("PuestoFrontal120")?1.2f:prototype.StartsWith("PuestoFrontal140")?1.4f:1.6f;
                Hull(go.transform,0,.38f,0,w,.75f,.65f);Hull(go.transform,0,.52f,.65f,.62f,1.04f,.62f);Hull(go.transform,0,.44f,-.65f,.48f,.86f,.52f);
            } else if(prototype=="CabinaBano"){
                Hull(go.transform,-.65f,1.15f,0,.035f,1.8f,1.70f);Hull(go.transform,.65f,1.15f,0,.035f,1.8f,1.70f);
                Hull(go.transform,0,1.15f,.85f,1.30f,1.8f,.035f);
            } else if(prototype!="Cartelera"&&prototype!="Extintor"&&!prototype.StartsWith("PanelLED")&&!prototype.StartsWith("Espejo")&&!prototype.StartsWith("Dispensador")){
                var rr=go.GetComponentsInChildren<Renderer>();Bounds b=rr[0].bounds;foreach(var r in rr.Skip(1))b.Encapsulate(r.bounds);
                var c=go.AddComponent<BoxCollider>();c.center=go.transform.InverseTransformPoint(b.center);Vector3 size=b.size;if(Mathf.Abs(rotation)%180>45)size=new Vector3(size.z,size.y,size.x);c.size=size;
            }
            return go;
        }
        static void Hull(Transform parent,float x,float y,float z,float dx,float dy,float dz){var g=new GameObject("Colision_Simple");g.transform.SetParent(parent,false);g.transform.localPosition=new Vector3(-z*SZ,y,-x*SX);g.AddComponent<BoxCollider>().size=new Vector3(dz*SZ,dy,dx*SX);}
        static void Sign(Transform zone,string name,string text,float x,float z,float y,float w,float h,float rotation,bool green=false)
        {
            var p=new GameObject(name).transform;p.SetParent(zone,false);p.position=Docencia2InteriorBuilder.PlanToUnityPos(x,.20f,z);p.rotation=Quaternion.Euler(0,rotation,0);
            Box(p,"Placa",green?"VerdeSenal":"Papel",0,y,0,w,h,.02f);
            var label=new GameObject("Texto");label.transform.SetParent(p,false);label.transform.localPosition=new Vector3(-.013f,y,0);label.transform.localRotation=Quaternion.Euler(0,-90,0);
            var tm=label.AddComponent<TextMesh>();tm.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if(tm.font)label.GetComponent<MeshRenderer>().sharedMaterial=tm.font.material;
            tm.text=text;tm.fontSize=64;tm.characterSize=h*.70f/64f*10;tm.anchor=TextAnchor.MiddleCenter;tm.alignment=TextAlignment.Center;tm.color=green?Color.white:Color.black;
            foreach(var r in p.GetComponentsInChildren<Renderer>()){r.shadowCastingMode=ShadowCastingMode.Off;GameObjectUtility.SetStaticEditorFlags(r.gameObject,StaticEditorFlags.OccludeeStatic|StaticEditorFlags.ReflectionProbeStatic);}
        }
    }
}
