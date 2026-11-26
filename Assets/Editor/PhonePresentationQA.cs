using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using Vaelith.Docencia2;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using System.Reflection;
using Object=UnityEngine.Object;

public static class PhonePresentationQA
{
    public const string Out="Revision_Celular_Linterna";
    public static void Run(){Directory.CreateDirectory(Out);EditorSceneManager.OpenScene(Aula17Phase3.ScenePath);SessionState.SetBool("PHONE_QA",true);EditorApplication.isPlaying=true;}
    public static void Install(){EditorSceneManager.OpenScene(Aula17Phase3.ScenePath);var p=Object.FindAnyObjectByType<Docencia2FirstPersonPlayer>();if(!p)throw new Exception("No player");if(!p.GetComponent<Docencia2PhonePresentation>())p.gameObject.AddComponent<Docencia2PhonePresentation>();EditorSceneManager.MarkSceneDirty(p.gameObject.scene);EditorSceneManager.SaveScene(p.gameObject.scene);EditorApplication.Exit(0);}
    public static void SaveAndTest(){EditorSceneManager.OpenScene(Aula17Phase3.ScenePath);var p=Object.FindAnyObjectByType<Docencia2FirstPersonPlayer>();if(!p)throw new Exception("No player");if(!p.GetComponent<Docencia2PhonePresentation>())p.gameObject.AddComponent<Docencia2PhonePresentation>();EditorSceneManager.MarkSceneDirty(p.gameObject.scene);EditorSceneManager.SaveScene(p.gameObject.scene);SessionState.SetBool("PHONE_SAVED_QA",true);Run();}
}
[InitializeOnLoad] public static class PhoneQABoot
{
    static PhoneQABoot(){EditorApplication.update+=Tick;}
    static void Tick(){if(SessionState.GetBool("PHONE_QA",false)&&EditorApplication.isPlaying&&!EditorApplication.isCompiling){SessionState.SetBool("PHONE_QA",false);new GameObject("TEMP_PhoneQA").AddComponent<PhoneQARuntime>();}}
}
public class PhoneQARuntime:MonoBehaviour
{
    List<string> report=new List<string>();
    void Log(string t,string s,LogType ty){if(ty==LogType.Error||ty==LogType.Exception)report.Add(ty+":"+t+s);}
    IEnumerator Start()
    {
        Application.logMessageReceived+=Log;Application.runInBackground=true;
        yield return null;yield return null;
        var keyboard=Keyboard.current??InputSystem.AddDevice<Keyboard>();
        var p=Object.FindAnyObjectByType<Docencia2FirstPersonPlayer>();var c=p.playerCamera;
        p.enableBatteryDrain=false;
        var rt=new RenderTexture(1280,720,24);c.targetTexture=rt;
        for(int version=SessionState.GetBool("PHONE_SAVED_QA",false)?1:0;version<2;version++)
        {
            var label=version==0?"Antes":SessionState.GetBool("PHONE_SAVED_QA",false)?"Final":"Despues";
            if(version==1&&!p.GetComponent<Docencia2PhonePresentation>())p.gameObject.AddComponent<Docencia2PhonePresentation>();
            foreach(var mode in new[]{Docencia2VisualMode.Noche,Docencia2VisualMode.Terror})
            {
                Object.FindAnyObjectByType<Docencia2ModeSwitcher>().ApplyMode(mode,false);
                p.TeleportToPlan(9.5f,3.606f,27.05f,0);
                p.transform.rotation=Quaternion.LookRotation(Aula17Phase3.W(11.1f,0,27.05f)-Aula17Phase3.W(9.5f,0,27.05f));
                p.SetFlashlightState(true,false);
                for(int i=0;i<45;i++)yield return null;
                Aula17Phase3.Shot(c,PhonePresentationQA.Out+"/"+label+"_"+mode+".png",1280,720);
                report.Add(label+mode+" camera="+c.transform.position+" mount="+p.rigDriver.phoneVisualMount.position+" light="+p.phoneSpotLight.intensity);
                p.TeleportToPlan(12.5f,3.606f,27f,90f);
                for(int i=0;i<35;i++)yield return null;
                Aula17Phase3.Shot(c,PhonePresentationQA.Out+"/"+label+"_Pasillo_"+mode+".png",1280,720);
            }
            if(version==1)
            {
                Object.FindAnyObjectByType<Docencia2ModeSwitcher>().ApplyMode(Docencia2VisualMode.Noche,false);
                var pres=p.GetComponent<Docencia2PhonePresentation>();
                foreach(float pitch in new[]{-45f,45f})
                {
                    typeof(Docencia2FirstPersonPlayer).GetField("_pitch",BindingFlags.NonPublic|BindingFlags.Instance).SetValue(p,pitch);
                    for(int i=0;i<25;i++)yield return null;
                    Aula17Phase3.Shot(c,PhonePresentationQA.Out+"/Mirada_"+pitch+".png",1280,720);
                }
                typeof(Docencia2FirstPersonPlayer).GetField("_pitch",BindingFlags.NonPublic|BindingFlags.Instance).SetValue(p,0f);
                var move=typeof(Docencia2FirstPersonPlayer).GetMethod("UpdateMovement",BindingFlags.NonPublic|BindingFlags.Instance);
                for(int i=0;i<40;i++){InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.LeftCtrl));InputSystem.Update();move.Invoke(p,new object[]{keyboard,1f/60f});yield return null;}
                report.Add("Crouching="+p.IsCrouchingState);
                Aula17Phase3.Shot(c,PhonePresentationQA.Out+"/Agachado.png",1280,720);
                InputSystem.QueueStateEvent(keyboard,new KeyboardState());
                p.isThirdPersonView=true;
                for(int i=0;i<50;i++)yield return null;
                Aula17Phase3.Shot(c,PhonePresentationQA.Out+"/TerceraPersona.png",1280,720);
                p.isThirdPersonView=false;
                for(int i=0;i<50;i++)yield return null;
                var start=p.transform.position;
                for(int i=0;i<20;i++){InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.W));InputSystem.Update();move.Invoke(p,new object[]{keyboard,1f/60f});yield return null;}
                InputSystem.QueueStateEvent(keyboard,new KeyboardState());
                report.Add("Movement distance="+Vector3.Distance(start,p.transform.position));
                Object.FindAnyObjectByType<Docencia2ModeSwitcher>().ApplyMode(Docencia2VisualMode.Terror,false);
                p.SetFlashlightState(false,false);for(int i=0;i<20;i++)yield return null;
                Aula17Phase3.Shot(c,PhonePresentationQA.Out+"/Despues_Apagada.png",1280,720);
                report.Add("Off enabled="+p.phoneSpotLight.enabled+" intensity="+p.phoneSpotLight.intensity);
                foreach(var r in p.rigDriver.phoneVisualMount.GetComponentsInChildren<Renderer>())report.Add(r.name+" bounds="+r.bounds+" local="+r.transform.localPosition);
                report.Add("maxGripError="+p.GetComponent<Docencia2PhonePresentation>().maxGripError);
            }
        }
        File.WriteAllLines(PhonePresentationQA.Out+"/qa.txt",report);c.targetTexture=null;rt.Release();Destroy(rt);EditorApplication.Exit(0);
    }
}
