using UnityEngine;
using UnityEngine.Rendering;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;
namespace Vaelith.Docencia2 {
 public static class AuditorioFloorCut {
  struct V { public Vector3 p,n; public Vector4 t; public Vector2 uv,uv2; public Color c;
   public static V Mix(V a,V b,float f){return new V{p=Vector3.Lerp(a.p,b.p,f),n=Vector3.Lerp(a.n,b.n,f),t=Vector4.Lerp(a.t,b.t,f),uv=Vector2.Lerp(a.uv,b.uv,f),uv2=Vector2.Lerp(a.uv2,b.uv2,f),c=Color.Lerp(a.c,b.c,f)};}
  }
  public static void Apply(){
   var names=new[]{"Terreno_Tierra_y_Roca","Losa_Piso_Interior","Sombra_Contacto_Perimetral_Edificio","Mancha_Tierra_Oscura_Base_32d"};
   foreach(var f in Object.FindObjectsByType<MeshFilter>(FindObjectsInactive.Include).Where(f=>names.Contains(f.name))){
    if(f.sharedMesh.name.StartsWith("AudCut_"))continue;
    var src=f.sharedMesh;var ps=src.vertices;var ns=src.normals;var ts=src.tangents;var uv=src.uv;var uv2=src.uv2;var cs=src.colors;
    V Get(int i){return new V{p=ps[i],n=ns.Length==ps.Length?ns[i]:Vector3.up,t=ts.Length==ps.Length?ts[i]:new Vector4(1,0,0,1),uv=uv.Length==ps.Length?uv[i]:Vector2.zero,uv2=uv2.Length==ps.Length?uv2[i]:Vector2.zero,c=cs.Length==ps.Length?cs[i]:Color.white};}
    // Four halfplanes delimit ONLY the interior excavation, in plan coordinates.
    float D(V v,int edge){var w=f.transform.TransformPoint(v.p);float x=(14.25f-w.z)*28.48f/28.5f,z=(27-w.x)*53.92f/54;return edge==0?x-17.32f:edge==1?28.04f-x:edge==2?z-25.20f:38.49f-z;}
    List<V> Clip(List<V> poly,int edge,bool inside){var o=new List<V>();if(poly.Count==0)return o;var a=poly[poly.Count-1];float da=D(a,edge)*(inside?1:-1);foreach(var b in poly){float db=D(b,edge)*(inside?1:-1);if((da>=0)!=(db>=0))o.Add(V.Mix(a,b,da/(da-db)));if(db>=0)o.Add(b);a=b;da=db;}return o;}
    var vs=new List<V>();var subs=new List<int[]>();
    for(int s=0;s<src.subMeshCount;s++){var ids=new List<int>();var triangles=src.GetTriangles(s);
     void Emit(List<V> p){for(int j=1;j<p.Count-1;j++){ids.Add(vs.Count);vs.Add(p[0]);ids.Add(vs.Count);vs.Add(p[j]);ids.Add(vs.Count);vs.Add(p[j+1]);}}
     for(int i=0;i<triangles.Length;i+=3){var poly=new List<V>{Get(triangles[i]),Get(triangles[i+1]),Get(triangles[i+2])};
      bool outside=false;for(int e=0;e<4;e++)if(poly.All(v=>D(v,e)<0)){outside=true;break;}
      if(outside){Emit(poly);continue;}for(int e=0;e<4&&poly.Count>0;e++){Emit(Clip(poly,e,false));poly=Clip(poly,e,true);}
     }subs.Add(ids.ToArray());
    }
    var mesh=new Mesh{name="AudCut_"+f.name,indexFormat=IndexFormat.UInt32};mesh.SetVertices(vs.Select(v=>v.p).ToList());mesh.SetNormals(vs.Select(v=>v.n).ToList());mesh.SetTangents(vs.Select(v=>v.t).ToList());mesh.SetUVs(0,vs.Select(v=>v.uv).ToList());if(uv2.Length>0)mesh.SetUVs(1,vs.Select(v=>v.uv2).ToList());if(cs.Length>0)mesh.SetColors(vs.Select(v=>v.c).ToList());mesh.subMeshCount=subs.Count;for(int s=0;s<subs.Count;s++)mesh.SetTriangles(subs[s],s);mesh.RecalculateBounds();
    string path="Assets/Models/Docencia2/Fase5/"+mesh.name+".asset";var existing=AssetDatabase.LoadAssetAtPath<Mesh>(path);if(existing){EditorUtility.CopySerialized(mesh,existing);Object.DestroyImmediate(mesh);mesh=existing;}else AssetDatabase.CreateAsset(mesh,path);f.sharedMesh=mesh;
   }
  }
 }
}
