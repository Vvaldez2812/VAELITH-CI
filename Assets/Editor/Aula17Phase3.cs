using System;
using System.IO;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEditor;
using UnityEditor.SceneManagement;
using Vaelith.Docencia2;
using Object=UnityEngine.Object;

public static class Aula17Phase3
{
    static bool thenExterior;
    public static void FinalCaptures(){thenExterior=true;Photos();}
    public const string ScenePath="Assets/Scenes/Exterior_Docencia2.unity", Out="Aula17_Fase3";
    public static Vector3 W(float x,float y,float z)=>Docencia2InteriorBuilder.PlanToUnityPos(x,y,z);
    public static string P(Transform t)=>t.parent?P(t.parent)+"/"+t.name:t.name;
    static readonly float[][] Views={
        new[]{.65f,5.2f,30.15f,5.4f,5.2f,26.8f,94f},new[]{.65f,5.2f,30.1f,5.5f,5.0f,26.7f,94f},
        new[]{.52f,5.2f,29.7f,3.8f,5.35f,24.3f,92f},new[]{1.4f,5.2f,30.35f,7.5f,5f,27f,94f},
        new[]{.65f,5.2f,30.15f,5.4f,5.2f,26.8f,94f},new[]{10.8f,5.2f,23.4f,3.5f,4.9f,27f,90f},
        new[]{11.05f,5.2f,24.4f,5.5f,5.1f,29.6f,94f},new[]{2.5f,5.2f,27.0f,10.7f,5.1f,26.9f,90f},
        new[]{9.7f,5.25f,24.3f,10.9f,5.15f,28f,88f},new[]{11.05f,5.2f,24.15f,5.3f,5.15f,29.5f,94f},
        new[]{5.3f,5.15f,23.8f,5.8f,5.4f,30.6f,92f},new[]{9.5f,5.15f,26.2f,1f,5.15f,27.6f,92f},
        new[]{3f,5.2f,25.6f,1.8f,5.0f,23.85f,95f},new[]{8.5f,5.15f,25.9f,10.3f,5.0f,24.6f,96f},
        new[]{8.6f,5.3f,25.2f,10.9f,5.15f,25.2f,86f},new[]{6.05f,5.05f,23.9f,6.05f,6.0f,28f,94f},
        new[]{10.8f,5.1f,23.4f,2.4f,4.7f,25.6f,90f},new[]{8.9f,5.2f,24.3f,4.7f,5.2f,29.1f,94f},
        new[]{7.6f,4.95f,27.1f,7.3f,4.9f,30.3f,62f},new[]{10.0f,5.15f,24.6f,5.7f,5f,23.8f,94f}
    };
    public static void Mode(Docencia2VisualMode mode)
    {
        Object.FindAnyObjectByType<Docencia2ModeSwitcher>().ApplyMode(mode,false);
        foreach(var t in Object.FindObjectsByType<Docencia2InteriorModeTracker>(FindObjectsInactive.Include))if(t.enabled)t.SendMessage("Update");
    }
    public static void Shot(Camera c,string path,int width,int height)
    {
        var rt=new RenderTexture(width,height,24,RenderTextureFormat.ARGBHalf);var prev=RenderTexture.active;var old=c.targetTexture;c.targetTexture=rt;
        var tex=new Texture2D(width,height,TextureFormat.RGB24,false);
        try{for(int i=0;i<5;i++){c.Render();RenderTexture.active=rt;tex.ReadPixels(new Rect(0,0,width,height),0,0);tex.Apply();}File.WriteAllBytes(path,tex.EncodeToPNG());}
        finally{c.targetTexture=old;RenderTexture.active=prev;Object.DestroyImmediate(tex);rt.Release();Object.DestroyImmediate(rt);}
    }
    public static void Photos()
    {
        try
        {
            Directory.CreateDirectory(Out+"/renders");EditorSceneManager.OpenScene(ScenePath);Mode(Docencia2VisualMode.Noche);
            var go=new GameObject("TEMP_A17_F3_Fotos");var c=go.AddComponent<Camera>();c.enabled=false;c.nearClipPlane=.035f;c.farClipPlane=100;c.allowHDR=true;c.clearFlags=CameraClearFlags.SolidColor;
            var data=go.AddComponent<UniversalAdditionalCameraData>();data.renderPostProcessing=true;
            var rows=new List<string>{"foto|x_plano|Y|z_plano|target_x|target_Y|target_z|fov_vertical|certeza"};
            for(int i=0;i<Views.Length;i++)
            {
                var a=Views[i];go.transform.position=W(a[0],a[1],a[2]);go.transform.LookAt(W(a[3],a[4],a[5]));c.fieldOfView=a[6];if(i==8)go.transform.Rotate(0,0,-9,Space.Self);
                Shot(c,Out+"/renders/F"+(i+1).ToString("D2")+".png",768,1024);
                rows.Add("F"+(i+1).ToString("D2")+"|"+string.Join("|",a.Select(v=>v.ToString("F3",System.Globalization.CultureInfo.InvariantCulture)))+"|SUPUESTO");
            }
            File.WriteAllLines(Out+"/camaras_fotos_SUPUESTO.csv",rows);Object.DestroyImmediate(go);
            EditorSceneManager.OpenScene(ScenePath);File.WriteAllText(Out+"/fotos_completado.txt","20 renders portrait 768x1024; camaras eliminadas; escena reabierta sin guardar ajustes temporales.");if(thenExterior)Exterior();else EditorApplication.Exit(0);
        }catch(Exception e){Fail(e);}
    }
    public static void Exterior()
    {
        const string temp="Assets/Scenes/TEMP_Aula17_Fase3_Baseline.unity";
        try
        {
            Directory.CreateDirectory(Out+"/exterior");File.Copy("backup_aula17/Exterior_Docencia2.unity",temp,true);AssetDatabase.ImportAsset(temp);
            foreach(var entry in new[]{(temp,"Antes_Aula17"),(ScenePath,"Actual")})
            {
                EditorSceneManager.OpenScene(entry.Item1);
                foreach(var mode in new[]{Docencia2VisualMode.Noche,Docencia2VisualMode.Terror})
                {
                    Mode(mode);var cams=GameObject.Find("Camaras_Referencia_Docencia2").GetComponentsInChildren<Camera>(true);
                    if(cams.Length!=3)throw new Exception("Se esperaban tres camaras exteriores");
                    foreach(var c in cams)Shot(c,Out+"/exterior/"+entry.Item2+"_"+mode+"_"+c.name+".png",1280,720);
                }
            }
            EditorSceneManager.OpenScene(ScenePath);AssetDatabase.DeleteAsset(temp);File.WriteAllText(Out+"/exterior_completado.txt","Tres camaras existentes, dos modos, antes de Aula17 contra estado actual; sin guardar cambios.");EditorApplication.Exit(0);
        }catch(Exception e){if(File.Exists(temp))AssetDatabase.DeleteAsset(temp);Fail(e);}
    }
    public static void Play()
    {Directory.CreateDirectory(Out);EditorSceneManager.OpenScene(ScenePath);SessionState.SetBool("AULA17_FASE3_PLAY",true);EditorApplication.isPlaying=true;}
    public static void Fail(Exception e){Directory.CreateDirectory(Out);File.WriteAllText(Out+"/error.txt",e.ToString());Debug.LogException(e);EditorApplication.Exit(1);}
}

[InitializeOnLoad]
public static class Aula17Phase3Boot
{
    static Aula17Phase3Boot(){EditorApplication.update+=Tick;}
    static void Tick(){if(SessionState.GetBool("AULA17_FASE3_PLAY",false)&&EditorApplication.isPlaying&&!EditorApplication.isCompiling){SessionState.SetBool("AULA17_FASE3_PLAY",false);new GameObject("TEMP_A17_Fase3_Play").AddComponent<Aula17Phase3Runtime>();}}
}

public class Aula17Phase3Runtime:MonoBehaviour
{
    readonly List<string> messages=new List<string>();
    void Log(string text,string stack,LogType type){if(type==LogType.Exception||type==LogType.Error||text.ToLower().Contains("shadow")&&text.ToLower().Contains("atlas"))messages.Add(type+": "+text);}
    IEnumerator Start()
    {
        Application.logMessageReceived+=Log;Application.runInBackground=true;QualitySettings.vSyncCount=0;Application.targetFrameRate=-1;
        yield return null;yield return null;
        var player=Docencia2FirstPersonPlayer.Instance??Object.FindAnyObjectByType<Docencia2FirstPersonPlayer>();
        if(!player){Aula17Phase3.Fail(new Exception("Jugador real ausente"));yield break;}
        var aula=GameObject.Find("PA_Interior/Aula_17").transform;var door=aula.GetComponentInChildren<Docencia2InteractiveDoor>();var cc=player.GetComponent<CharacterController>();
        var cam=player.playerCamera;var rt=new RenderTexture(1280,720,24,RenderTextureFormat.ARGBHalf);cam.targetTexture=rt;
        player.enableBatteryDrain=false;player.showDebugHudAndShortcuts=true;
        var report=new List<string>{"Jugador real: "+Aula17Phase3.P(player.transform),$"Height={cc.height}; radius={cc.radius}; step={cc.stepOffset}"};
        var fps=new List<string>{"modo|vista|resolucion|muestras|fps_promedio|fps_p95|ms_max|luces_aula_activas|linterna"};
        // Real CharacterController movement, with the player's own movement input disabled only while the route is driven.
        player.TeleportToPlan(10.20f,3.606f,22.55f,0);player.enabled=false;door.SetOpenAnimated(true);
        yield return new WaitForSeconds(.6f);report.Add("Puerta abierta: "+door.isOpen+"; angulo respecto cierre="+Quaternion.Angle(door.closedRotation,door.transform.localRotation));
        var paths=new[]{new[]{10.20f,24.55f},new[]{9.50f,24.55f},new[]{9.50f,27.05f},new[]{1.05f,27.05f},new[]{1.05f,29.70f},new[]{1.05f,27.05f},new[]{9.50f,27.05f},new[]{9.50f,29.7f},new[]{9.50f,24.55f},new[]{10.20f,24.55f},new[]{10.20f,22.55f}};
        for(int j=0;j<paths.Length;j++)
        {
            var p=paths[j];var target=Aula17Phase3.W(p[0],3.626f,p[1]);float elapsed=0;
            while(elapsed<7f)
            {
                var delta=target-player.transform.position;delta.y=0;if(delta.magnitude<.045f)break;
                cc.Move(Vector3.ClampMagnitude(delta,2.5f*Time.deltaTime)+Vector3.down*2f*Time.deltaTime);elapsed+=Time.deltaTime;yield return null;
            }
            var remainder=target-player.transform.position;remainder.y=0;report.Add($"Ruta {j+1}: destino ({p[0]},{p[1]}), error={remainder.magnitude:F3}m, Y={player.transform.position.y:F3}, {(remainder.magnitude<.08f&&Mathf.Abs(player.transform.position.y-3.636f)<.1f?"PASS":"BLOQUEADO_O_ESCALON")}");
        }
        door.SetOpenAnimated(false);yield return new WaitForSeconds(.6f);report.Add("Puerta cerrada: "+!door.isOpen+"; error angular="+Quaternion.Angle(door.closedRotation,door.transform.localRotation));player.enabled=true;
        var spots=new[]{new[]{1.0f,29.5f,7.0f,25.8f},new[]{9.5f,24.7f,3.0f,27.4f},new[]{5.5f,27.05f,5.5f,24.0f}};
        foreach(var mode in new[]{Docencia2VisualMode.Noche,Docencia2VisualMode.Terror})
        {
            Aula17Phase3.Mode(mode);player.SetFlashlightState(mode==Docencia2VisualMode.Terror,false);
            for(int view=0;view<spots.Length;view++)
            {
                var a=spots[view];player.TeleportToPlan(a[0],3.606f,a[1],0);var forward=Aula17Phase3.W(a[2],0,a[3])-Aula17Phase3.W(a[0],0,a[1]);player.transform.rotation=Quaternion.LookRotation(forward);cam.transform.localRotation=Quaternion.identity;
                for(int w=0;w<40;w++)yield return null;
                var samples=new List<double>();double last=Time.realtimeSinceStartupAsDouble;
                double start=last;for(int i=0;i<5000&&(i<300||last-start<8.0);i++){yield return null;double now=Time.realtimeSinceStartupAsDouble;samples.Add(now-last);last=now;}
                samples.Sort();double average=samples.Count/samples.Sum(),p95=1.0/samples[(int)(samples.Count*.95)];
                int active=aula.GetComponentsInChildren<Light>(true).Concat(GameObject.Find("PA_Interior/Iluminacion_PA_Mismo_Criterio_PB").GetComponentsInChildren<Light>(true).Where(l=>l.name=="Luz_PA_Aula_17_35"||l.name=="Luz_PA_Aula_17_36")).Count(l=>l.enabled&&l.gameObject.activeInHierarchy);
                fps.Add(FormattableString.Invariant($"{mode}|Vista{view+1}|1280x720|{samples.Count}|{average:F2}|{p95:F2}|{samples.Last()*1000:F2}|{active}|{player.flashlightOn}"));
                Aula17Phase3.Shot(cam,Aula17Phase3.Out+"/Play_"+mode+"_"+(view+1)+".png",1280,720);
            }
        }
        player.TeleportToPlan(9.50f,3.606f,27.05f,0);player.transform.rotation=Quaternion.LookRotation(Aula17Phase3.W(11.1f,0,27.05f)-Aula17Phase3.W(9.5f,0,27.05f));cam.transform.localRotation=Quaternion.identity;
        foreach(bool on in new[]{false,true})
        {player.SetFlashlightState(on,false);for(int i=0;i<40;i++)yield return null;Aula17Phase3.Shot(cam,Aula17Phase3.Out+"/Linterna_Pared_"+(on?"ON":"OFF")+".png",1280,720);}
        foreach(var l in player.GetComponentsInChildren<Light>(true))report.Add($"Luz jugador {l.name}: enabled={l.enabled}; active={l.gameObject.activeInHierarchy}; intensidad={l.intensity}; rango={l.range}; mascara={l.cullingMask}; rendering={l.renderingLayerMask}");
        report.Add("Dispositivo="+SystemInfo.graphicsDeviceName+"; CPU="+SystemInfo.processorType+"; memoria="+SystemInfo.systemMemorySize+"MB; Unity="+Application.unityVersion);
        File.WriteAllLines(Aula17Phase3.Out+"/recorrido_play.txt",report);File.WriteAllLines(Aula17Phase3.Out+"/fps_play.csv",fps);File.WriteAllLines(Aula17Phase3.Out+"/consola_play.txt",messages);
        cam.targetTexture=null;rt.Release();Object.Destroy(rt);Application.logMessageReceived-=Log;File.WriteAllText(Aula17Phase3.Out+"/play_completado.txt","Prueba en Play mode finalizada. Sin guardar cambios de runtime.");EditorApplication.Exit(0);
    }
}
