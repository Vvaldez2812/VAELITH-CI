using UnityEngine;
namespace Vaelith.Docencia2
{
    public static partial class Docencia2Phase5Builder
    {
        static void ClassroomCurtains(Transform zone,int room)
        {
            if(!mats.ContainsKey("TelaCortina"))MaterialFor("TelaCortina",new Color(.055f,.060f,.052f),.05f,0);
            bool small=room==2;
            string proto=small?"CortinaAlta":"CortinaAula";
            if(!prototypes.ContainsKey(proto))Prototype(proto,p=>{
                float w=small?1.90f:6.60f,top=small?2.96f:2.76f,bottom=small?2.20f:1.40f;
                const int n=96;var v=new Vector3[(n+1)*2];var uv=new Vector2[v.Length];var tri=new int[n*12];
                for(int i=0;i<=n;i++){
                    float x=-w/2+w*i/n,fold=.045f*Mathf.Sin(i*Mathf.PI*.5f);
                    float hem=bottom+.025f*Mathf.Cos(i*Mathf.PI/24);
                    v[i*2]=new Vector3(-fold,hem,-x*SX);v[i*2+1]=new Vector3(-fold,top,-x*SX);
                    uv[i*2]=new Vector2(i/(float)n,0);uv[i*2+1]=new Vector2(i/(float)n,1);
                    if(i==n)continue;int a=i*2,b=a+1,c=a+2,d=a+3,t=i*12;
                    int[] faces={a,b,c,b,d,c,c,b,a,c,d,b};for(int j=0;j<12;j++)tri[t+j]=faces[j];
                }
                // Separate front/back vertices so opposite triangle winding cannot cancel normals.
                int offset=v.Length;var twoSides=new Vector3[offset*2];var twoUV=new Vector2[offset*2];
                System.Array.Copy(v,0,twoSides,0,offset);System.Array.Copy(v,0,twoSides,offset,offset);
                System.Array.Copy(uv,0,twoUV,0,offset);System.Array.Copy(uv,0,twoUV,offset,offset);
                for(int t=0;t<tri.Length;t+=12)for(int j=6;j<12;j++)tri[t+j]+=offset;
                var mesh=new Mesh{name="Tela_Plises"};mesh.vertices=twoSides;mesh.uv=twoUV;mesh.triangles=tri;mesh.RecalculateNormals();mesh.RecalculateBounds();
                var g=new GameObject("Cortina_Oscura_Plises");g.transform.SetParent(p,false);g.AddComponent<MeshFilter>().sharedMesh=mesh;g.AddComponent<MeshRenderer>().sharedMaterial=mats["TelaCortina"];
                Box(p,"Barra_Cortina","Negro",0,top+.025f,0,w+.02f,.025f,.025f);
            });
            GameObject item;
            if(small)item=Place(proto,zone,"Cortina_Ventana_Sur_SUPUESTO",14.24f,53.32f,180);
            else{
                int section=room<=1||room==3? (room==0?5:6):room==4?5:room==5?4:3;
                bool east=room<=1;
                item=Place(proto,zone,"Cortina_Ventana_"+(east?"Este":"Oeste")+"_SUPUESTO",east?27.85f:.63f,(section+.5f)*(54f/7),east?90:-90);
            }
            Object.DestroyImmediate(item.GetComponent<BoxCollider>());
        }
    }
}
