using UnityEngine;
using UnityEditor;
using System.Reflection;
using System.IO;
using Bushido;

[InitializeOnLoad]
public static class BushidoDuelPreview
{
    const string Key="Bushido.DuelPreview";
    static int ticks;
    static readonly BindingFlags Flags=BindingFlags.Instance|BindingFlags.NonPublic;
    static BushidoDuelPreview(){if(SessionState.GetBool(Key,false))EditorApplication.update+=Tick;}
    [MenuItem("Bushido/Revisión/Ver nuevo duelo %#F10")]
    static void Run(){SessionState.SetBool(Key,true);ticks=0;EditorApplication.update-=Tick;EditorApplication.update+=Tick;if(!EditorApplication.isPlaying)EditorApplication.EnterPlaymode();}
    static void Call(object o,string name,params object[] args)=>o.GetType().GetMethod(name,Flags).Invoke(o,args);
    static void Set(object o,string name,object v)=>o.GetType().GetField(name,Flags).SetValue(o,v);
    static void Tick()
    {
        if(!EditorApplication.isPlaying||++ticks<45)return;
        var game=Object.FindAnyObjectByType<BushidoPrototype>();if(game==null)return;
        if(ticks==45)
        {
            Time.timeScale=0;Set(game,"mission",2);Call(game,"BeginBattle");Set(game,"paused",false);
            Call(game,"BeginTheaterAttack");Call(game,"UpdatePaperTheater",.5f);Call(game,"UpdateBossRitual",0f);
        }
        if(ticks==65)Capture("duel-warning");
        if(ticks==85){Call(game,"UpdatePaperTheater",.8f);Call(game,"UpdatePaperTheater",.15f);Call(game,"UpdateBossRitual",0f);}
        if(ticks==105){Capture("duel-symbol");SessionState.SetBool(Key,false);EditorApplication.update-=Tick;}
    }
    static void Capture(string name)
    {
        string folder=Path.GetFullPath(Path.Combine(Application.dataPath,"../../QualityReview"));Directory.CreateDirectory(folder);
        ScreenCapture.CaptureScreenshot(Path.Combine(folder,name+".png"));
    }
}
