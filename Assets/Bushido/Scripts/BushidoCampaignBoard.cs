using UnityEngine;
namespace Bushido
{
    static class CampaignBoardLayout
    {
        public static readonly Vector2[] Positions = {
            new Vector2(155,470),new Vector2(355,320),new Vector2(585,405),
            new Vector2(775,245),new Vector2(1010,205)
        };
        public static readonly string[] Names={"El viajero","Bosque de bambú","Aldea del ogro","Paso de montaña","Santuario del oni"};
        public static Vector2 Point(int from,int to,float t) => Vector2.Lerp(Positions[from],Positions[to],t)+Vector2.up*Mathf.Sin(t*Mathf.PI)*26f;
    }
    public sealed partial class BushidoPrototype
    {
        Texture2D boardPaper, boardSeal, boardPawn;
        readonly Texture2D[] boardIcons = new Texture2D[5];
        readonly Texture2D[] boardRoads = new Texture2D[4];
        readonly Rect[] boardRoadBounds = new Rect[4];
        Texture2D samuraiMapIcon;
        GUIStyle boardCaption,boardHeading,boardStatus,boardCredit,boardRank;
        static readonly string[] BoardIconNames={"japanese-bridge","bamboo","oni","mountains","samurai-helmet"};

        void PrepareBoardArt()
        {
            if(boardCaption==null || boardStatus==null || boardCredit==null || boardHeading==null || boardRank==null)
            {
                boardCaption=new GUIStyle(paperTextStyle){alignment=TextAnchor.MiddleCenter,fontSize=18};
                boardStatus=new GUIStyle(boardCaption){fontSize=13,fontStyle=FontStyle.Normal};
                boardHeading=new GUIStyle(campaignButtonStyle){alignment=TextAnchor.MiddleLeft,fontSize=26};
                boardHeading.normal.textColor=CampaignInk;
                boardCredit=new GUIStyle(boardStatus){fontSize=10,alignment=TextAnchor.MiddleRight};
                boardRank=new GUIStyle(boardCaption){fontSize=13,wordWrap=false};
                boardRank.normal.textColor=CampaignPaper;
            }
            if(boardPaper==null)boardPaper=Resources.Load<Texture2D>("Backgrounds/MapaTinta");
            if(samuraiMapIcon==null)samuraiMapIcon=Resources.Load<Texture2D>("MapInk/samurai-helmet");
            if(boardSeal==null){boardSeal=CampaignInkArt.Seal(false);titleAssets.Add(boardSeal);}
            if(boardPawn==null){boardPawn=CampaignInkArt.Seal(true);titleAssets.Add(boardPawn);}
            for(int i=0;i<5;i++)if(boardIcons[i]==null)boardIcons[i]=Resources.Load<Texture2D>("MapInk/"+BoardIconNames[i]);
            for(int i=0;i<4;i++)if(boardRoads[i]==null)
            {
                boardRoads[i]=CampaignInkArt.Road(i,out boardRoadBounds[i]);titleAssets.Add(boardRoads[i]);
            }
        }
        void BoardTexture(Texture2D texture,Rect rect,Color ink)
        {
            if(texture==null)return;
            Color saved=GUI.color;GUI.color=saved*ink;GUI.DrawTexture(rect,texture);GUI.color=saved;
        }
        void DrawMapSeal(Vector2 p,int node)
        {
            bool available=progress.IsUnlocked(node), completed=progress.completed[node];
            bool chosen=selected==node;
            Color pigment=available?CampaignInk:new Color(.46f,.42f,.35f,.62f);
            BoardTexture(boardSeal,new Rect(p.x-55,p.y-49,116,116),new Color(.16f,.10f,.06f,.14f));
            BoardTexture(boardSeal,new Rect(p.x-58,p.y-58,116,116),chosen?CampaignRed:pigment);
            BoardTexture(boardSeal,new Rect(p.x-53,p.y-53,106,106),available?new Color(.96f,.88f,.72f):new Color(.82f,.77f,.65f));
            BoardTexture(boardIcons[node],new Rect(p.x-36,p.y-36,72,72),chosen?CampaignRed:pigment);
            if(completed)
            {
                BoardTexture(boardSeal,new Rect(p.x+27,p.y+24,32,32),CampaignRed);
                Rank rank=(Rank)progress.bestRanks[node];
                GUI.Label(new Rect(p.x+28,p.y+25,30,30),rank==Rank.Perfect?"S+":rank.ToString(),boardRank);
            }
            if(!available)
            {
                // Closed binding tab, paired with the text below for readability.
                Panel(new Rect(p.x+29,p.y+20,20,23),CampaignInk);
                Panel(new Rect(p.x+34,p.y+16,10,5),CampaignInk);
                Panel(new Rect(p.x+38,p.y+27,2,7),CampaignPaper);
            }
        }
        void DrawMapPawn(Vector2 position)
        {
            // Fixed paper cutout silhouette; only its position moves along the route.
            Vector2 p=position+new Vector2(-84,-8);
            float hop=mode==Mode.Travel?Mathf.Sin(travelTime*Mathf.PI)*8:0;
            BoardTexture(boardPawn,new Rect(p.x-28,p.y-29,62,72),new Color(.12f,.08f,.04f,.22f));
            p.y-=hop;
            BoardTexture(boardPawn,new Rect(p.x-31,p.y-39,62,72),CampaignInk);
            BoardTexture(boardPawn,new Rect(p.x-27,p.y-35,54,64),new Color(.89f,.70f,.40f));
            BoardTexture(samuraiMapIcon,new Rect(p.x-20,p.y-22,40,40),CampaignInk);
            Panel(new Rect(p.x-13,p.y+18,26,2),CampaignRed);
        }
        void DrawCampaignBoard(bool interactive)
        {
            PrepareBoardArt();
            Panel(new Rect(0,76,1280,644),CampaignPaper);
            BoardTexture(boardPaper,new Rect(0,76,1280,644),new Color(1,1,1,.22f));
            Panel(new Rect(28,95,1224,2),new Color(.3f,.2f,.1f,.25f));
            GUI.Label(new Rect(60,116,520,48),"El camino del exorcista",boardHeading);
            for(int n=0;n<4;n++)
            {
                Color ink=progress.IsUnlocked(n+1)?new Color(.19f,.15f,.12f,.95f):new Color(.36f,.31f,.24f,.38f);
                BoardTexture(boardRoads[n],boardRoadBounds[n],ink);
            }
            for(int i=0;i<5;i++)
            {
                Vector2 p=CampaignBoardLayout.Positions[i];
                bool available=progress.IsUnlocked(i);
                DrawMapSeal(p,i);
                float labelY=p.y+(i==1 || i==3 ? -100 : 58);
                GUI.Label(new Rect(p.x-105,labelY,210,28),CampaignBoardLayout.Names[i],boardCaption);
                string status=!available?"Por descubrir":progress.completed[i]?"Exorcizado":i==0?"Inicio del viaje":"Disponible";
                GUI.Label(new Rect(p.x-100,labelY+27,200,24),status,boardStatus);
                if(interactive && i>0 && available && GUI.Button(new Rect(p.x-52,p.y-52,104,104),GUIContent.none,GUIStyle.none))selected=i;
            }
            Vector2 token=CampaignBoardLayout.Positions[routeNode];
            if(mode==Mode.Travel)token=CampaignBoardLayout.Point(routeNode,nextRouteNode,Mathf.SmoothStep(0,1,travelTime));
            DrawMapPawn(token);
            Panel(new Rect(60,606,1160,1),new Color(.3f,.2f,.1f,.3f));
            GUI.Label(new Rect(65,625,710,36),CampaignBoardLayout.Names[selected],boardHeading);
            GUI.Label(new Rect(65,665,740,28),CampaignProgress.IsBoss(selected)?"Encuentro yōkai · Rompe su maldición":"Sendero · Afina tu espada antes del encuentro",boardStatus);
            if(CampaignButton(new Rect(930,632,280,55),progress.completed[selected]?"Volver al encuentro":"Seguir el camino",true,interactive && progress.IsUnlocked(selected) && selected>0))StartTravel();
            GUI.Label(new Rect(790,696,420,18),"Iconos: Delapouite y Lorc · game-icons.net · CC BY 3.0",boardCredit);
        }
    }
}
