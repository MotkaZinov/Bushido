using System;
using System.IO;
using System.Reflection;
using Bushido;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

[InitializeOnLoad]
public static class BushidoKatanaReview
{
    const string Key="Bushido.KatanaReview";
    static int ticks,stage;
    static readonly BindingFlags Flags=BindingFlags.Instance|BindingFlags.NonPublic;
    static readonly System.Text.StringBuilder report=new System.Text.StringBuilder();
    static string Folder=>Path.GetFullPath(Path.Combine(Application.dataPath,"../../QualityReview"));
    static BushidoKatanaReview(){if(SessionState.GetBool(Key,false))EditorApplication.update+=Tick;}
    static object Get(object o,string n)=>o.GetType().GetField(n,Flags).GetValue(o);
    static void Set(object o,string n,object v)=>o.GetType().GetField(n,Flags).SetValue(o,v);
    static object Call(object o,string n,params object[] args)=>o.GetType().GetMethod(n,Flags).Invoke(o,args);
    static void Check(bool condition,string message){if(!condition)throw new Exception(message);report.AppendLine("PASS: "+message);}
    [MenuItem("Bushido/Revisión/Ver katana 3D y humo %#F6")]
    public static void Run()
    {
        SessionState.SetBool(Key,true);ticks=stage=0;report.Clear();
        EditorApplication.update-=Tick;EditorApplication.update+=Tick;
        if(!EditorApplication.isPlaying)EditorApplication.EnterPlaymode();
    }
    static void Capture(string name)=>ScreenCapture.CaptureScreenshot(Path.Combine(Folder,name+".png"));
    static void Tick()
    {
        if(!EditorApplication.isPlaying || ++ticks<60 || ticks%15!=0)return;
        var game=UnityEngine.Object.FindAnyObjectByType<BushidoPrototype>();if(game==null)return;
        try
        {
            if(stage==0)
            {
                Time.timeScale=0;Set(game,"mission",2);Call(game,"BeginBattle");Set(game,"paused",false);
                Set(game,"theaterState",2);Set(game,"paperOpening",1f);Call(game,"UpdatePaperTheater",0f);
                Call(game,"PlayKatanaParry",Vector3.up);Call(game,"UpdateKatanaProps",.08f);
                Check(((Transform)Get(game,"counterKatana")).gameObject.activeInHierarchy,"La katana es visible durante el bloqueo del ogro");
                Capture("ogro-katana-bloqueo");
            }
            if(stage==1)
            {
                Call(game,"UpdateKatanaProps",1f);
            }
            if(stage==2)
            {
                var oldSword=(Transform)Get(game,"counterKatana");
                var oldSaya=(Transform)Get(game,"drawSaya");
                Set(game,"bossFinalStage",1);Call(game,"UpdateBossRitual",2.2f);
                var camera=(Camera)Get(game,"view");
                var ogreSword=(Transform)Get(game,"counterKatana");
                var ogreSaya=(Transform)Get(game,"drawSaya");
                Check(!oldSword.gameObject.activeSelf && !oldSaya.gameObject.activeSelf,"La katana y funda de papel se ocultan en el remate del ogro");
                Check(ogreSword.GetComponent<MeshFilter>().sharedMesh.vertexCount>1000 && ogreSaya.GetComponent<MeshFilter>().sharedMesh.triangles.Length>1000,"El remate del ogro utiliza la katana 3D original y su funda");
                Check(ogreSword.gameObject.activeInHierarchy && ogreSaya.gameObject.activeInHierarchy,"Katana 3D y funda activas en el remate del ogro");
                Vector2 ogreGrip=camera.WorldToScreenPoint((Vector3)Get(game,"drawHiltPosition"));
                Call(game,"BeginDrawGrip",ogreGrip);
                Check((bool)Get(game,"drawGripHeld"),"El mango del remate del ogro responde al arrastre");
                Check(((KatanaSpiritVfx)Get(game,"katanaSpirit")).SmokeStrength>0,"El remate del ogro incluye vapor y energía espiritual");
                Set(game,"theaterState",2);Set(game,"paperOpening",1f);Call(game,"UpdatePaperTheater",0f);
                for(int i=0;i<3;i++)Call(game,"AddSpiritChains",(Transform)Get(game,"opponent"));
                foreach(var chain in (System.Collections.IList)Get(game,"spiritChains"))chain.GetType().GetField("born").SetValue(chain,Time.time-.8f);
                Set(game,"landedBossSeals",3);Call(game,"UpdateBossRitual",0f);
                var chains=(System.Collections.IList)Get(game,"spiritChains");
                var layers=(LineRenderer[])chains[0].GetType().GetField("smoke").GetValue(chains[0]);
                Check(layers.Length==3 && layers[0].widthMultiplier>=.6f,"Las cadenas emiten tres capas de humo más ancho");
                Capture("ogro-katana-cadenas");
            }
            if(stage==3)
            {
                Set(game,"mission",4);Call(game,"BeginBattle");Set(game,"paused",false);
                Set(game,"theaterState",2);Set(game,"paperOpening",1f);Call(game,"UpdatePaperTheater",0f);
                Set(game,"landedBossSeals",3);Set(game,"bossFinalStage",1);Set(game,"wasPressed",false);
                Call(game,"UpdateBossRitual",.4f);
                var sword=(Transform)Get(game,"counterKatana");var saya=(Transform)Get(game,"drawSaya");
                Check(sword.GetComponent<MeshFilter>().sharedMesh.vertexCount>1000,"Se utiliza la katana 3D original");
                Check(saya.GetComponent<MeshFilter>().sharedMesh.triangles.Length>1000,"La funda tiene geometría independiente");
                Check((float)Get(game,"drawPullProgress")==0,"La katana entra completamente enfundada");
                Check(((KatanaSpiritVfx)Get(game,"katanaSpirit")).SmokeStrength==0,"El humo espera al desenfunde");
                Capture("katana-entrada");
            }
            if(stage==4){Call(game,"UpdateBossRitual",.6f);Capture("katana-enfundada");}
            if(stage==5)
            {
                for(int i=0;i<132;i++)Call(game,"UpdateBossRitual",1f/60);
                float pull=(float)Get(game,"drawPullProgress");
                Check(pull>.1f && pull<.25f,"Desenfunde automático parcial");
                var mist=(KatanaSpiritVfx)Get(game,"katanaSpirit");
                Check(mist.PresentationVisible && mist.SmokeStrength>0,"Humo espiritual activo después de abrir la funda");
                var saya=(Transform)Get(game,"drawSaya");
                Check(Vector3.Distance(mist.EmissionOrigin,saya.TransformPoint(Vector3.up*.43f))<.3f,"El humo nace en la boca de la funda");
                var view=(Camera)Get(game,"view");Vector2 grip=view.WorldToScreenPoint((Vector3)Get(game,"drawHiltPosition"));
                Vector3 viewport=view.WorldToViewportPoint((Vector3)Get(game,"drawHiltPosition"));
                Check(viewport.x>.53f && viewport.x<.60f && viewport.y>.34f && viewport.y<.42f,"El mango conserva la posición vertical marcada por el usuario");
                Vector3 bladeDirection=-saya.up;
                Check(bladeDirection.y<-.9f && bladeDirection.x>0 && bladeDirection.x<.3f,"La funda queda abajo y ligeramente a la derecha");
                Check(mist.SmokeStrength>.2f && mist.SmokeStrength<.5f,"Los efectos iniciales son visibles y mantienen energía contenida");
                Check(((KatanaSpiritVfx)Get(game,"katanaSpirit")).PresentationVisible,"Aura localizada activa durante el desenfunde");
                Call(game,"BeginDrawGrip",grip);Check((bool)Get(game,"drawGripHeld"),"El mango visible responde al gesto");
                float sampled=mist.SampleTime;Vector3 origin=mist.EmissionOrigin;
                Call(game,"UpdateBossRitual",0f);
                Check(mist.SampleTime==sampled && mist.EmissionOrigin==origin,"Los efectos permanecen inmóviles sin avance de tiempo");
                Check(view.GetUniversalAdditionalCameraData().renderPostProcessing,"Postprocesado activo durante la presentación");
                var fog=mist.transform.Find("Niebla que desborda la funda");
                Check(fog.GetComponent<Renderer>().enabled && fog.GetComponent<MeshFilter>().sharedMesh.bounds.min.y<mist.EmissionOrigin.y-.3f,"La niebla sale de la funda y cae hacia la parte inferior de pantalla");
                Capture("katana-humo");
            }
            if(stage==6)
            {
                var mist=(KatanaSpiritVfx)Get(game,"katanaSpirit");float contained=mist.SmokeStrength;
                Set(game,"drawPullProgress",.6f);Set(game,"drawPullTarget",.6f);Set(game,"wasPressed",true);
                Call(game,"UpdateBossRitual",.6f);
                Check(mist.SmokeStrength>contained*2,"El poder crece conforme se extrae la hoja");
                var fogMesh=mist.transform.Find("Niebla que desborda la funda").GetComponent<MeshFilter>().sharedMesh;
                Check(fogMesh.colors[0].a<fogMesh.colors[48].a,"La niebla pierde opacidad al acercarse al borde inferior");
                Capture("katana-poder");
            }
            if(stage==7)
            {
                var view=(Camera)Get(game,"view");Vector2 grip=view.WorldToScreenPoint((Vector3)Get(game,"drawHiltPosition"));
                Call(game,"BeginDrawGrip",grip);
                Set(game,"strokeStart",grip);
                var saya=(Transform)Get(game,"drawSaya");
                Vector2 axis=((Vector2)view.WorldToScreenPoint(saya.position+saya.up)-(Vector2)view.WorldToScreenPoint(saya.position)).normalized;
                Vector2 end=grip+axis*Screen.height*.18f;
                Vector3 p=view.ScreenToWorldPoint(new Vector3(end.x,end.y,6));
                Call(game,"BossGesture",p,p);Check((int)Get(game,"bossFinalStage")==2,"El gesto continúa al corte final");
                Call(game,"UpdateKatanaProps",.01f);
                Check(!((KatanaSpiritVfx)Get(game,"katanaSpirit")).PresentationVisible,"El aura se retira al ejecutar el corte");
                Check(!((KatanaSpiritVfx)Get(game,"katanaSpirit")).transform.Find("Niebla que desborda la funda").GetComponent<Renderer>().enabled,"La niebla se retira al ejecutar el corte");
                Check(!((Transform)Get(game,"drawSaya")).gameObject.activeSelf && ((KatanaSpiritVfx)Get(game,"katanaSpirit")).SmokeStrength==0,"Funda y humo se retiran durante el remate");
                Call(game,"PlayKatanaCounter",Vector3.up);
                Check(((Material)Get(game,"katanaBladeMaterial")).GetFloat("_BladeBottom")< -1,"La hoja completa queda visible al cortar");
                Check(!view.GetUniversalAdditionalCameraData().renderPostProcessing,"La cámara recupera su estado al terminar la presentación");
                Finish(true);return;
            }
            stage++;
        }
        catch(Exception e){report.AppendLine("FAIL: "+e);Debug.LogException(e);Finish(false);}
    }
    static void Finish(bool success)
    {
        File.WriteAllText(Path.Combine(Folder,"katana-validation.txt"),report.ToString());
        SessionState.SetBool(Key,false);EditorApplication.update-=Tick;Time.timeScale=1;
        if(Application.isBatchMode)EditorApplication.Exit(success?0:1);
        else EditorApplication.ExitPlaymode();
    }
    public static void Inspect()
    {
        var report = new System.Text.StringBuilder();
        foreach (string name in new[] { "katana", "katana no case" })
        {
            var asset = Resources.Load<GameObject>("Models/Weapons/katana/" + name);
            report.AppendLine(name);
            var instance = Object.Instantiate(asset);
            foreach (var filter in instance.GetComponentsInChildren<MeshFilter>())
            {
                var mesh = filter.sharedMesh;
                report.AppendLine(filter.name + " position=" + filter.transform.position.ToString("F4") + " rotation=" + filter.transform.eulerAngles + " scale=" + filter.transform.lossyScale);
                report.AppendLine("bounds=" + filter.GetComponent<Renderer>().bounds.ToString("F4") + " local=" + mesh.bounds.ToString("F4") + " submeshes=" + mesh.subMeshCount);
            }
            Object.DestroyImmediate(instance);
        }
        File.WriteAllText(Path.Combine(Application.dataPath, "../../QualityReview/katana-model.txt"), report.ToString());
    }
}
