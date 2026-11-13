using UnityEngine;
using UnityEditor;
using System.Reflection;
using System.IO;
using Bushido;
[InitializeOnLoad]
public static class BushidoMapPreview
{
    const string Key="Bushido.MapPreview";
    static int ticks;
    static readonly BindingFlags Flags=BindingFlags.Instance|BindingFlags.NonPublic;
    static BushidoMapPreview(){if(SessionState.GetBool(Key,false))EditorApplication.update+=Tick;}
    [MenuItem("Bushido/Revisión/Ver mapa de tinta %#F7")]
    static void Run()
    {
        SessionState.SetBool(Key,true);ticks=0;
        EditorApplication.update-=Tick;EditorApplication.update+=Tick;
        if(!EditorApplication.isPlaying)EditorApplication.EnterPlaymode();
    }
    static void Tick()
    {
        if(!EditorApplication.isPlaying || EditorApplication.isPaused || ++ticks<50)return;
        var game=Object.FindAnyObjectByType<BushidoPrototype>();if(game==null)return;
        if(ticks==50)
        {
            Time.timeScale=1;
            game.GetType().GetMethod("ShowMap",Flags).Invoke(game,new object[]{false});
            game.GetType().GetField("shojiTime",Flags).SetValue(game,-1f);
        }
        if(ticks<80)return;
        string[] icons={"bamboo","oni","mountains","samurai-helmet","japanese-bridge"};
        foreach(string icon in icons)if(Resources.Load<Texture2D>("MapInk/"+icon)==null)throw new System.Exception("Falta icono "+icon);
        string folder=Path.GetFullPath(Path.Combine(Application.dataPath,"../../QualityReview"));
        Directory.CreateDirectory(folder);
        ScreenCapture.CaptureScreenshot(Path.Combine(folder,"map-ink-icons.png"));
        Debug.Log("BUSHIDO_MAP_INK_OK: 5 iconos cargados; mapa abierto sin modificar el guardado.");
        SessionState.SetBool(Key,false);EditorApplication.update-=Tick;
    }
}
