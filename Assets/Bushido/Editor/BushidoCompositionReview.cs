using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Bushido;

[InitializeOnLoad]
public static class BushidoCompositionReview
{
    const string Key="Bushido.CompositionReview";
    static int wait;
    static readonly BindingFlags Flags=BindingFlags.Instance|BindingFlags.NonPublic;
    static BushidoCompositionReview(){if(SessionState.GetBool(Key,false))EditorApplication.update+=Tick;}
    [MenuItem("Bushido/Revisión/Composición del ogro %#F8")]
    public static void ReviewCurrentScene()
    {
        SessionState.SetBool(Key,true);wait=0;Time.timeScale=1;
        EditorApplication.update-=Tick;EditorApplication.update+=Tick;
        if(!EditorApplication.isPlaying)EditorApplication.EnterPlaymode();
    }
    public static void Run()
    {
        SessionState.SetBool(Key,true);
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        new GameObject("Prueba de composición").AddComponent<BushidoPrototype>();
        EditorApplication.update-=Tick;EditorApplication.update+=Tick;
        EditorApplication.EnterPlaymode();
    }
    static void Set(object target,string field,object value)=>target.GetType().GetField(field,Flags).SetValue(target,value);
    static object Get(object target,string field)=>target.GetType().GetField(field,Flags).GetValue(target);
    static void Call(object target,string method,params object[] args)=>target.GetType().GetMethod(method,Flags).Invoke(target,args);
    static void Tick()
    {
        if(!EditorApplication.isPlaying||++wait<45)return;
        var game=UnityEngine.Object.FindFirstObjectByType<BushidoPrototype>();
        if(game==null||Get(game,"view")==null)return;
        EditorApplication.update-=Tick;
        try
        {
            Set(game,"mission",2);Call(game,"BeginBattle");Set(game,"paused",true);
            Set(game,"theaterState",2);Set(game,"theaterTime",.3f);Set(game,"paperOpening",1f);
            Call(game,"UpdatePaperTheater",0f);Call(game,"UpdateBossRitual",0f);
            string output=Path.GetFullPath(Path.Combine(Application.dataPath,"../../QualityReview"));Directory.CreateDirectory(output);
            Render(game,output,"encuentro",1600,900);
            Call(game,"OpenCounter");Call(game,"UpdateBossRitual",0f);
            Render(game,output,"sello",1600,900);
            var mask=(Transform)Get(game,"cursedMask");
            for(int i=0;i<3;i++)
            {
                Set(game,"bossSeals",i);
                var seal=(Transform)game.GetType().GetMethod("CreateSpiritTalisman",Flags).Invoke(game,null);
                seal.SetParent(mask,false);seal.localPosition=new Vector3((i-1)*.32f,-.22f,-.3f);seal.localScale=Vector3.one*.45f;
                Call(game,"AddSpiritChains",seal);
            }
            foreach(var chain in (System.Collections.IList)Get(game,"spiritChains"))chain.GetType().GetField("born").SetValue(chain,Time.time-.8f);
            Set(game,"bossSeals",3);Set(game,"landedBossSeals",3);Set(game,"bossFinalStage",1);Call(game,"UpdateFighter",0f);Call(game,"UpdateBossRitual",0f);
            Render(game,output,"sellado",1600,900);
            Debug.Log("COMPOSITION_REVIEW_OK "+output);
            SessionState.SetBool(Key,false);
            if(Application.isBatchMode)EditorApplication.Exit(0);
            else
            {
                Call(game,"BeginBattle");Set(game,"paused",false);Time.timeScale=0;
                Set(game,"theaterState",2);Set(game,"theaterTime",.3f);Set(game,"paperOpening",1f);
                Call(game,"UpdatePaperTheater",0f);Call(game,"OpenCounter");Call(game,"UpdateBossRitual",0f);
                ((Camera)Get(game,"view")).ResetAspect();Call(game,"PositionBackdrop");            }
        }
        catch(Exception e){Debug.LogException(e);SessionState.SetBool(Key,false);if(Application.isBatchMode)EditorApplication.Exit(1);}
    }
    static void Render(BushidoPrototype game,string folder,string name,int width,int height)
    {
        var camera=(Camera)Get(game,"view");
        var rt=new RenderTexture(width,height,24);rt.Create();camera.targetTexture=rt;camera.aspect=width/(float)height;
        Call(game,"PositionBackdrop");Call(game,"UpdateCursedMask",0f);
        camera.Render();RenderTexture.active=rt;
        var image=new Texture2D(width,height,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,width,height),0,0);image.Apply();
        File.WriteAllBytes(Path.Combine(folder,name+".png"),image.EncodeToPNG());
        camera.targetTexture=null;RenderTexture.active=null;rt.Release();UnityEngine.Object.Destroy(rt);UnityEngine.Object.Destroy(image);
    }
}



