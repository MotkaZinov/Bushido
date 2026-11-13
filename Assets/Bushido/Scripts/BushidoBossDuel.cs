using UnityEngine;
using System.Collections.Generic;

namespace Bushido
{
    public sealed partial class BushidoPrototype
    {
        enum DuelPhase { None, Charge, Telegraph, Recovery, Exhausted, Seal, RecoveryRound }
        DuelPhase duelPhase;
        float duelClock, duelPreparedAt;
        bool duelPrepared, duelPerfect;
        int duelGestureBeat = -1;
        string duelFeedback;
        float duelFeedbackTime;
        AudioClip duelAlert;
        readonly List<Vector3> duelDrawing = new List<Vector3>();
        bool DuelActive => IsBoss && bossFinalStage == 0 && duelPhase != DuelPhase.None;
        float DuelStrikeDelay => DuelSymbol == 3 ? 1.65f : DuelSymbol == 2 ? 1.35f : 1.15f;
        int DuelSymbol => bossSeals == 0 ? duelBeat % 2 : bossSeals == 1 ? (duelBeat % 2 == 0 ? 0 : 2) : (duelBeat == 3 ? 3 : duelBeat == 1 ? 2 : 0);

        Vector3[] DuelPattern()
        {
            switch (DuelSymbol)
            {
                case 1: return new[]{new Vector3(-.5f,.5f),new Vector3(.5f,-.5f)};
                case 2: return new[]{new Vector3(-.5f,.5f),new Vector3(0,-.5f),new Vector3(.5f,.5f)};
                case 3: return new[]{new Vector3(-.5f,.5f),new Vector3(.5f,.5f),new Vector3(-.5f,-.5f),new Vector3(.5f,-.5f)};
                default:return new[]{new Vector3(-.5f,-.5f),new Vector3(.5f,.5f)};
            }
        }
        void ResetDuel()
        {
            gestureEpoch++;duelContactPending=false;
            duelPhase=DuelPhase.None;duelClock=0;duelBeat=duelBlocks=0;
            duelPrepared=false;duelDrawing.Clear();duelGestureBeat=-1;duelFeedbackTime=0;
        }
        void StartDuelRound()
        {
            gestureEpoch++;duelContactPending=false;
            duelPhase=DuelPhase.Charge;duelClock=0;duelBeat=duelBlocks=0;
            duelPrepared=false;duelGestureBeat=-1;theaterStrike=false;
            bossReaction=BossReaction.None;duelFeedbackTime=0;
            if(duelAlert==null)
            {
                const int rate=22050;var samples=new float[rate/3];
                for(int i=0;i<samples.Length;i++)
                {float t=i/(float)rate;samples[i]=(Mathf.Sin(2*Mathf.PI*260*t)+.4f*Mathf.Sin(2*Mathf.PI*780*t))*Mathf.Exp(-t*13)*Mathf.Min(1,t*150)*.3f;}
                duelAlert=AudioClip.Create("Aviso de ráfaga",samples.Length,1,rate,false);duelAlert.SetData(samples,0);
            }
            SetupCutAudio();cutAudio.PlayOneShot(duelAlert,.75f);
        }
        void StartDuelBeat()
        {
            gestureEpoch++;duelContactPending=false;
            duelPhase=DuelPhase.Telegraph;duelClock=0;duelPrepared=false;duelPerfect=false;
            duelGestureBeat=-1;theaterStrike=false;theaterTime=0;bossReaction=BossReaction.None;
            duelDrawing.Clear();validStroke=false;
        }
        void UpdateBossDuel(float delta)
        {
            if(!DuelActive)return;
            duelClock+=delta;duelFeedbackTime=Mathf.Max(0,duelFeedbackTime-delta);
            theaterState=2;theaterTime=duelClock;
            if(duelPhase==DuelPhase.Charge && duelClock>=1.2f)StartDuelBeat();
            else if(duelPhase==DuelPhase.Telegraph && duelClock>=DuelStrikeDelay)
            {
                duelContactPending=true;
            }
            else if(duelPhase==DuelPhase.Recovery && duelClock>=(duelBeat==3?.65f:.38f))
            {
                if(duelBeat<3){duelBeat++;StartDuelBeat();}
                else {duelPhase=DuelPhase.Exhausted;duelClock=0;bossReaction=BossReaction.None;}
            }
            else if(duelPhase==DuelPhase.Exhausted && duelClock>=.65f)
            {
                if(duelBlocks==4)
                {
                    duelOpeningSeal=true;OpenCounter();duelOpeningSeal=false;
                    counterTime=6;duelPhase=DuelPhase.Seal;duelClock=0;
                }
                else {duelPhase=DuelPhase.RecoveryRound;duelClock=0;}
            }
            else if(duelPhase==DuelPhase.Seal && !vulnerable && flyingSeal==null && bossReaction!=BossReaction.Seal)
            {duelPhase=DuelPhase.RecoveryRound;duelClock=0;}
            else if(duelPhase==DuelPhase.RecoveryRound && duelClock>=1.2f)StartDuelRound();
        }
        bool duelContactPending;
        void ResolveDuelContact()
        {
            if(paused || mode!=Mode.Battle || !DuelActive || !duelContactPending || duelPhase!=DuelPhase.Telegraph)return;
            duelContactPending=false;
                theaterStrike=true;duelPhase=DuelPhase.Recovery;duelClock=0;
                if(duelPrepared)
                {
                    duelBlocks++;var direction=DuelPattern();
                    PlayKatanaParry(direction[direction.Length-1]-direction[0]);
                    FighterCut(direction[direction.Length-1]-direction[0]);
                    PlayGuardSound(duelPerfect);GuardBurst(CounterCenter,duelPerfect);
                    ReactBoss(BossReaction.Deflected);
                    impactTime=duelBeat==3?.08f:duelPerfect?.065f:.045f;
                    shakeTime=duelBeat==3?.16f:.09f;combo++;comboTime=3;
                    duelFeedback=duelPerfect?"DESVÍO PERFECTO":"BLOQUEO";
                    if(duelBeat==3)PlayPostureSound(true);
                }
                else {duelFeedback="GOLPE RECIBIDO";Hurt();}
                duelFeedbackTime=.65f;
                if(mode!=Mode.Battle){duelPhase=DuelPhase.None;return;}
        }
        // Shape recognition preserves aspect ratio: straight slashes are valid inputs.
        Vector2[] NormalizeDuel(IList<Vector3> path)
        {
            if(path.Count<2)return null;float length=0;
            Vector2 min=path[0],max=path[0];
            for(int i=1;i<path.Count;i++){length+=Vector2.Distance(path[i-1],path[i]);min=Vector2.Min(min,path[i]);max=Vector2.Max(max,path[i]);}
            float size=Mathf.Max(max.x-min.x,max.y-min.y);if(size<.2f||length<size*.85f)return null;
            var points=new Vector2[32];int seg=1;float traveled=0;
            for(int i=0;i<32;i++)
            {
                float d=length*i/31;
                while(seg<path.Count-1 && traveled+Vector2.Distance(path[seg-1],path[seg])<d){traveled+=Vector2.Distance(path[seg-1],path[seg]);seg++;}
                float span=Vector2.Distance(path[seg-1],path[seg]);
                points[i]=(Vector2.Lerp(path[seg-1],path[seg],Mathf.Clamp01((d-traveled)/Mathf.Max(.00001f,span)))-(min+max)*.5f)/size;
            }
            return points;
        }
        bool CompleteDuelGesture(List<Vector3> path)
        {
            if(duelPhase!=DuelPhase.Telegraph||duelPrepared||duelGestureBeat!=duelBeat || strokeGestureEpoch!=gestureEpoch)return false;
            var input=NormalizeDuel(path);var target=NormalizeDuel(DuelPattern());if(input==null)return false;
            if (GestureShapeError(input, target) > .22f)
            {duelFeedback="OTRA VEZ";duelFeedbackTime=.35f;return false;}
            duelPrepared=true;duelPreparedAt=duelClock;duelPerfect=DuelStrikeDelay-duelClock<=.18f && duelClock<=DuelStrikeDelay+.05f;
            duelFeedback="EN GUARDIA";duelFeedbackTime=.4f;guardPose=.3f;
            return true;
        }

        // Bounded alignment tolerates uneven corners and small wrist rotations.
        // Keep stroke order and orientation: a random scribble must not match.
        float GestureShapeError(Vector2[] input, Vector2[] target)
        {
            int count = input.Length;
            if (count != target.Length) return float.PositiveInfinity;
            float best = float.PositiveInfinity;
            int window = Mathf.Max(3, count / 5);
            var previous = new float[count + 1];
            var current = new float[count + 1];
            for (int direction = 0; direction < 2; direction++)
            for (int degrees = -15; degrees <= 15; degrees += 5)
            {
                float angle = degrees * Mathf.Deg2Rad;
                float cosine = Mathf.Cos(angle), sine = Mathf.Sin(angle);
                for (int j = 0; j <= count; j++) previous[j] = float.PositiveInfinity;
                previous[0] = 0;
                for (int i = 1; i <= count; i++)
                {
                    for (int j = 0; j <= count; j++) current[j] = float.PositiveInfinity;
                    Vector2 point = input[direction == 0 ? i - 1 : count - i];
                    point = new Vector2(point.x * cosine - point.y * sine,
                        point.x * sine + point.y * cosine);
                    for (int j = Mathf.Max(1, i - window); j <= Mathf.Min(count, i + window); j++)
                    {
                        float cost = Vector2.Distance(point, target[j - 1]);
                        // Penalize stretching so extra loops cannot disappear in alignment.
                        current[j] = cost + Mathf.Min(previous[j - 1],
                            Mathf.Min(previous[j] + .025f, current[j - 1] + .025f));
                    }
                    var swap = previous; previous = current; current = swap;
                }
                best = Mathf.Min(best, previous[count] / count);
            }
            return best;
        }
        void DuelGuiLine(Vector2 a,Vector2 b,Color color,float width)
        {
            var matrix=GUI.matrix;
            GUI.matrix=matrix*Matrix4x4.TRS(new Vector3(a.x,a.y,0),Quaternion.Euler(0,0,Mathf.Atan2(b.y-a.y,b.x-a.x)*Mathf.Rad2Deg),Vector3.one);
            Panel(new Rect(0,-width*.5f,Vector2.Distance(a,b),width),color);GUI.matrix=matrix;
        }
        void DrawDuelHUD()
        {
            if(!DuelActive||paused)return;
            if(duelPhase==DuelPhase.Charge && duelClock>=.1f && duelClock<1.05f)
            {
                float alpha=Mathf.Min(Mathf.Clamp01((duelClock-.1f)/.14f),Mathf.Clamp01((1.05f-duelClock)/.2f));
                Vector3 p=view.WorldToViewportPoint(CounterCenter+Vector3.up*2f);
                Vector2 origin=new Vector2(p.x*1280,(1-p.y)*720);
                float size=72*(1+.2f*(1-Mathf.Clamp01((duelClock-.1f)/.14f)));
                var strokes=new[]{new[]{new Vector2(-.5f,-.28f),new Vector2(-.04f,-.28f)},new[]{new Vector2(-.27f,-.28f),new Vector2(-.27f,.34f)},new[]{new Vector2(-.53f,.43f),new Vector2(.0f,.23f)},new[]{new Vector2(.22f,-.54f),new Vector2(.02f,-.05f)},new[]{new Vector2(.13f,-.25f),new Vector2(.53f,-.25f)},new[]{new Vector2(.43f,-.23f),new Vector2(.31f,.13f),new Vector2(.04f,.48f)},new[]{new Vector2(.12f,-.10f),new Vector2(.29f,.23f),new Vector2(.56f,.48f)}};
                foreach(var stroke in strokes)for(int i=1;i<stroke.Length;i++)
                {
                    DuelGuiLine(origin+stroke[i-1]*size,origin+stroke[i]*size,new Color(.96f,.88f,.69f,alpha),10);
                    DuelGuiLine(origin+stroke[i-1]*size,origin+stroke[i]*size,new Color(.72f,.075f,.045f,alpha),6);
                }
            }
            for(int i=0;i<4;i++)
            {
                var r=new Rect(585+i*29,654,17,17);Panel(r,i<duelBlocks?new Color(.38f,.61f,.45f):new Color(.22f,.25f,.2f,.7f));
                if(i==duelBeat)Panel(new Rect(r.x,675,17,2),PrototypeArt.Gold);
            }
            if(duelPhase==DuelPhase.Telegraph)
            {
                Vector2 center=new Vector2(duelBeat%2==0?365:855,365);
                var box=new Rect(center.x-70,center.y-87,140,174);
                Panel(new Rect(box.x+5,box.y+6,box.width,box.height),new Color(.08f,.1f,.07f,.3f));Panel(box,PrototypeArt.Red);
                Panel(new Rect(box.x+5,box.y+5,130,164),PrototypeArt.Paper);
                var pattern=DuelPattern();for(int i=1;i<pattern.Length;i++)DuelGuiLine(center+new Vector2(pattern[i-1].x,-pattern[i-1].y)*82,center+new Vector2(pattern[i].x,-pattern[i].y)*82,duelPrepared?new Color(.23f,.45f,.33f):PrototypeArt.Ink,6);
                var start=center+new Vector2(pattern[0].x,-pattern[0].y)*82;Panel(new Rect(start.x-5,start.y-5,10,10),PrototypeArt.Gold);
                float remaining=1-Mathf.Clamp01(duelClock/DuelStrikeDelay);
                for(int i=0;i<40*remaining;i++)
                {
                    float a=(-90+i*9)*Mathf.Deg2Rad,b=(-90+(i+1)*9)*Mathf.Deg2Rad;
                    DuelGuiLine(center+new Vector2(Mathf.Cos(a),Mathf.Sin(a))*112,center+new Vector2(Mathf.Cos(b),Mathf.Sin(b))*112,duelPrepared?new Color(.38f,.61f,.45f):PrototypeArt.Gold,4);
                }
                DuelGuiLine(center+Vector2.down*103,center+Vector2.down*122,PrototypeArt.Red,4);
            }
        }
    }
}


