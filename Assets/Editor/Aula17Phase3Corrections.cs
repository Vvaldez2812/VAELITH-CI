using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using Vaelith.Docencia2;
using Object=UnityEngine.Object;

public static class Aula17Phase3Corrections
{
    public static void FixTextDepth()
    {
        try
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new Exception("No modificar en Play");
            EditorSceneManager.OpenScene(Aula17Phase3.ScenePath);
            var aula=GameObject.Find("PA_Interior/Aula_17");var texts=aula.GetComponentsInChildren<TextMesh>(true);
            if(texts.Length!=5)throw new Exception("Se esperaban cuatro lineas de pizarron y WIFI");
            const string path="Assets/Models/Docencia2/Aula17_Fase3/A17_Texto_Profundidad.mat";
            var mat=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(!mat){mat=new Material(Shader.Find("VAELITH/Aula17/TextDepth"));mat.name="A17_Texto_Profundidad";AssetDatabase.CreateAsset(mat,path);}
            var lines=new List<string>();
            foreach(var t in texts){var r=t.GetComponent<Renderer>();lines.Add(Aula17Phase3.P(t.transform)+" | "+r.sharedMaterial.shader.name+" -> VAELITH/Aula17/TextDepth");r.sharedMaterial=mat;if(!t.GetComponent<Aula17TextDepthBinding>())t.gameObject.AddComponent<Aula17TextDepthBinding>();}
            AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(aula.scene);EditorSceneManager.SaveScene(aula.scene);
            File.WriteAllLines(Aula17Phase3.Out+"/correccion_texto_profundidad.txt",lines);
            Aula17Phase3.FinalCaptures();
        }catch(Exception e){Aula17Phase3.Fail(e);}
    }
    static Transform root;
    static Transform Find(string n)=>root.GetComponentsInChildren<Transform>(true).Single(t=>t.name==n);
    static void Set(Transform t,float x,float z,float y,float heading)
    {t.position=Aula17Phase3.W(x,y,z);t.rotation=Quaternion.LookRotation(Aula17Phase3.W(Mathf.Sin(heading*Mathf.Deg2Rad),0,-Mathf.Cos(heading*Mathf.Deg2Rad))-Aula17Phase3.W(0,0,0));}
    static Transform Clone(string source,string name,float x,float z,float y,float heading)
    {var original=Find(source);var t=Object.Instantiate(original.gameObject,original.parent).transform;t.name=name;Set(t,x,z,y,heading);return t;}
    static Dictionary<string,string> Snapshot()
    {
        var result=new Dictionary<string,string>();
        foreach(var g in root.gameObject.scene.GetRootGameObjects())foreach(var t in g.GetComponentsInChildren<Transform>(true))
        {if(t==root||t.IsChildOf(root))continue;foreach(var o in new Object[]{t.gameObject}.Concat(t.GetComponents<Component>().Cast<Object>()))if(o)result[GlobalObjectId.GetGlobalObjectIdSlow(o).ToString()]=EditorJsonUtility.ToJson(o);}
        return result;
    }
    public static void Run()
    {
        try
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new Exception("No modificar en Play");
            EditorSceneManager.OpenScene(Aula17Phase3.ScenePath);root=GameObject.Find("PA_Interior/Aula_17/Mobiliario_Equipo_Aula17_Fase2d_SUPUESTO").transform;
            if(root.GetComponentsInChildren<Transform>(true).Any(t=>t.name=="Torre_Norte_F01_1"))throw new Exception("Correcciones ya aplicadas");
            var before=Snapshot();
            Set(Find("Recipiente_Bolsa_Negra"),10.50f,25.00f,3.606f,270);
            Set(Find("Impresora_Multifuncion"),2.55f,23.70f,4.361f,180);
            Set(Find("Monitor_Norte"),1.97f,23.72f,4.361f,180);
            Set(Find("Teclado_Norte"),1.97f,23.94f,4.379f,180);
            Set(Find("Raton_1"),2.28f,24.0f,4.386f,0);
            Set(Find("Control_Remoto"),2.90f,23.97f,4.386f,15);
            for(int i=0;i<4;i++)Clone("Torre_Sur_1","Torre_Norte_F01_"+(i+1),3.05f+i*.24f,23.65f,4.361f,180);
            // Two additional monitor faces and a second tower on the last south table are visible in F11/F19.
            Set(Find("Torre_Sur_1"),3.20f,30.50f,4.361f,180);
            Set(Find("Monitor_Sur_1"),2.43f,30.22f,4.361f,-12);
            Clone("Monitor_Sur_1","Monitor_Sur_1_Adicional_F11",2.90f,30.28f,4.361f,15);
            Set(Find("Torre_Sur_2"),4.18f,30.50f,4.361f,0);
            Set(Find("Monitor_Sur_2"),4.65f,30.20f,4.361f,-8);
            var tilted=Clone("Monitor_Sur_1","Monitor_Sur_2_Adicional_F11",5.10f,30.43f,4.361f,14);tilted.Rotate(0,0,-6,Space.Self);
            Set(Find("Torre_Sur_4"),9.05f,30.45f,4.361f,0);
            Clone("Torre_Sur_1","Torre_Sur_4_Adicional_F11",8.40f,30.43f,4.361f,180);
            // Small visible bottle on the shelf, simplified, without brand or collision.
            var bottle=new GameObject("Botella_Estante_F15_SUPUESTO");bottle.layer=30;bottle.transform.SetParent(Find("Estante_Siete_Niveles").parent,false);Set(bottle.transform,10.88f,25.18f,5.12f,270);
            var mat=AssetDatabase.LoadAssetAtPath<Material>("Assets/Models/Docencia2/Aula17_Fase2d/A17_Compartido_Metal.mat");
            var blue=AssetDatabase.LoadAssetAtPath<Material>("Assets/Models/Docencia2/Aula17_Fase2d/A17_Compartido_Azul.mat");
            foreach(var part in new[]{("Cuerpo",.045f,.19f,0f,mat),("Cuello",.019f,.045f,.112f,mat),("Tapa",.022f,.018f,.143f,blue)})
            {var g=GameObject.CreatePrimitive(PrimitiveType.Cylinder);Object.DestroyImmediate(g.GetComponent<Collider>());g.name=part.Item1;g.layer=30;g.isStatic=true;g.transform.SetParent(bottle.transform,false);g.transform.localPosition=new Vector3(part.Item4,0,0);g.transform.localRotation=Quaternion.Euler(0,0,90);g.transform.localScale=new Vector3(part.Item2*2,part.Item3/2,part.Item2*2);var r=g.GetComponent<Renderer>();r.sharedMaterial=part.Item5;r.renderingLayerMask=49;r.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;}
            Physics.SyncTransforms();Geometry();
            var after=Snapshot();var changed=before.Keys.Where(k=>!after.ContainsKey(k)||before[k]!=after[k]).Concat(after.Keys.Except(before.Keys)).ToArray();
            File.WriteAllText(Aula17Phase3.Out+"/ambito_correcciones.txt",$"Registros protegidos {before.Count}; cambios externos {changed.Length}\n"+string.Join("\n",changed));if(changed.Length>0)throw new Exception("Cambios externos");
            EditorSceneManager.MarkSceneDirty(root.gameObject.scene);EditorSceneManager.SaveScene(root.gameObject.scene);
            File.WriteAllText(Aula17Phase3.Out+"/correcciones_completadas.txt","Bote desplazado a (10.50,25.00); 4 torres norte + 1 torre sur + 2 monitores sur + botella de estante. Impresora y perifericos reajustados. Mallas y materiales existentes compartidos. Posiciones SUPUESTO.\n"+DateTime.Now.ToString("O"));
            var inventory=new List<string>{"objeto|x_plano|z_plano|Y|certeza"};foreach(Transform group in root)foreach(Transform t in group){var p=t.position;inventory.Add(FormattableString.Invariant($"{t.name}|{(14.25f-p.z)*28.48f/28.50f:F3}|{(27-p.x)*53.92f/54:F3}|{p.y:F3}|SUPUESTO"));}File.WriteAllLines(Aula17Phase3.Out+"/inventario_final.csv",inventory);
            EditorApplication.Exit(0);
        }catch(Exception e){Aula17Phase3.Fail(e);}
    }
    static void Geometry()
    {
        var log=new List<string>();var cols=root.GetComponentsInChildren<Collider>();
        var protectedCols=Object.FindObjectsByType<Collider>(FindObjectsInactive.Exclude).Where(c=>!c.transform.IsChildOf(root)&&c.enabled&&!c.isTrigger&&!(c is CharacterController)).ToArray();
        var failures=new List<string>();
        foreach(var c in cols)foreach(var other in protectedCols)
            if(c.bounds.Intersects(other.bounds)&&Physics.ComputePenetration(c,c.transform.position,c.transform.rotation,other,other.transform.position,other.transform.rotation,out var dir,out float depth)&&depth>.008f)failures.Add(Aula17Phase3.P(c.transform)+" intersects "+Aula17Phase3.P(other.transform)+" depth="+depth);
        log.Add("Penetraciones >8mm contra colliders externos al mobiliario: "+failures.Count);log.AddRange(failures);
        foreach(var route in new[]{new[]{1.58f,10.10f,26.45f,27.65f},new[]{8.90f,10.10f,24.15f,30.0f},new[]{.38f,1.58f,25.30f,30.0f},new[]{9.78f,10.74f,23.215f,24.115f}})
        {
            var a=Aula17Phase3.W(route[0],3.64f,route[2]);var b=Aula17Phase3.W(route[1],5.24f,route[3]);var bounds=new Bounds((a+b)/2,new Vector3(Mathf.Abs(a.x-b.x),1.60f,Mathf.Abs(a.z-b.z)));
            var hits=cols.Concat(protectedCols).Where(c=>c.bounds.Intersects(bounds)).Select(c=>Aula17Phase3.P(c.transform)).ToArray();log.Add($"Paso plano ({route[0]},{route[2]})-({route[1]},{route[3]}): obstaculos {hits.Length}");log.AddRange(hits);failures.AddRange(hits);
        }
        var door=root.parent.GetComponentInChildren<Docencia2InteractiveDoor>();var dc=door.GetComponent<BoxCollider>();var saved=door.transform.localRotation;int count=0;
        for(int angle=0;angle<=90;angle+=5){door.transform.localRotation=door.closedRotation*Quaternion.Euler(0,-angle,0);Physics.SyncTransforms();foreach(var c in cols)if(Physics.ComputePenetration(dc,dc.transform.position,dc.transform.rotation,c,c.transform.position,c.transform.rotation,out var dir,out float depth)&&depth>.003f){failures.Add(c.name+" apertura "+angle);count++;}}
        door.transform.localRotation=saved;Physics.SyncTransforms();log.Add("Barrido de puerta 0-90 grados, 19 posiciones: colisiones mobiliario="+count);
        File.WriteAllLines(Aula17Phase3.Out+"/geometria_final.txt",log);if(failures.Count>0)throw new Exception("Geometria requiere revision: "+string.Join("\n",failures.Take(15)));
    }
}
