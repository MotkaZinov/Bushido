using UnityEngine;
namespace Bushido
{
    public sealed partial class BushidoPrototype
    {
        enum CompanionMood { Calm, Blink, Focus, Happy, Excited, Parry, Hurt, Disappointed, Seal, Danger, Draw, Victory }
        float companionReactionTime, companionReactionAge;
        int companionPreviousCombo, companionPreviousRank, companionPreviousSeals, companionPreviousLives;
        bool companionWasGuardFlash;
        CompanionMood companionMood;
        Texture2D companionSpriteSheet;
        Texture2D companionLifeSheet;
        static readonly int[] HealthyLifeFrames = { 0, 1, 2, 1, 0, 0, 3, 0 };
        static readonly int[] AlertLifeFrames = { 0, 1, 0, 2, 0, 1, 3, 0 };
        static readonly int[] CriticalLifeFrames = { 0, 1, 1, 2, 2, 0, 3, 0 };
        void BeginCompanionFace()
        {
            companionSpriteSheet = Resources.Load<Texture2D>("UI/SamuraiReactionsMasked");
            companionLifeSheet = Resources.Load<Texture2D>("UI/SamuraiLifeStages");
            foreach(var renderer in fighter.GetComponentsInChildren<Renderer>()) renderer.enabled=false;
            companionPreviousCombo=0; companionPreviousRank=0; companionPreviousSeals=0;
            companionPreviousLives=lives; companionWasGuardFlash=false;
            companionReactionTime=companionReactionAge=0; companionMood=CompanionMood.Calm;
        }
        int CompanionPriority(CompanionMood mood) => mood==CompanionMood.Hurt?5:
            mood==CompanionMood.Victory || mood==CompanionMood.Draw?4:
            mood==CompanionMood.Parry || mood==CompanionMood.Seal?3:
            mood==CompanionMood.Excited || mood==CompanionMood.Disappointed?2:1;
        void ReactCompanion(CompanionMood mood, float duration)
        {
            if(companionReactionTime>.25f && CompanionPriority(mood)<CompanionPriority(companionMood))return;
            companionMood=mood;companionReactionTime=duration;companionReactionAge=0;
        }
        void UpdateCompanionFace(float delta)
        {
            companionReactionTime=Mathf.Max(0,companionReactionTime-delta);
            companionReactionAge+=delta;
            if(lives<companionPreviousLives)ReactCompanion(CompanionMood.Hurt,.95f);
            else if(guardFlash>0 && !companionWasGuardFlash)ReactCompanion(CompanionMood.Parry,.9f);
            else if(landedBossSeals>companionPreviousSeals)ReactCompanion(CompanionMood.Seal,1.2f);
            else if(StyleLevel>companionPreviousRank)ReactCompanion(CompanionMood.Excited,1.25f);
            else if(companionPreviousCombo>0 && combo==0)ReactCompanion(CompanionMood.Disappointed,1.1f);
            else if(combo>companionPreviousCombo)
                ReactCompanion(strokeHits>=2 || combo%5==0?CompanionMood.Happy:CompanionMood.Focus,.55f);
            companionPreviousLives=lives;companionWasGuardFlash=guardFlash>0;
            companionPreviousSeals=landedBossSeals;companionPreviousRank=StyleLevel;companionPreviousCombo=combo;
            if(bossFinalStage==1 && companionMood!=CompanionMood.Draw)ReactCompanion(CompanionMood.Draw,1);
            if(bossFinalStage==2 && companionMood!=CompanionMood.Victory)ReactCompanion(CompanionMood.Victory,1.5f);
            if(companionReactionTime<=0 && bossFinalStage==0)
                companionMood=lives==1?CompanionMood.Danger:StyleLevel>=4?CompanionMood.Happy:CompanionMood.Calm;
        }
        void DrawIllustratedCompanion()
        {
            if(companionSpriteSheet==null || (mode!=Mode.Battle && mode!=Mode.Transition) || (bossFinalStage==2 && !bossInkBurst))return;
            int frame=(int)companionMood;
            bool lifeIdle = companionReactionTime <= 0 && bossFinalStage == 0 && companionLifeSheet != null;
            Texture2D sheet = companionSpriteSheet;
            if (lifeIdle)
            {
                int lifeRow = lives >= 3 ? 0 : lives == 2 ? 1 : 2;
                float rate = lifeRow == 2 ? 2.8f : lifeRow == 1 ? 4f : 3.4f;
                int tick = Mathf.FloorToInt(battleTime * rate) % 8;
                int pose = lifeRow == 0 ? HealthyLifeFrames[tick] : lifeRow == 1 ? AlertLifeFrames[tick] : CriticalLifeFrames[tick];
                frame = lifeRow * 4 + pose;
                sheet = companionLifeSheet;
            }
            if(!lifeIdle && companionReactionTime<=0 && Mathf.Repeat(battleTime+1.3f,3.7f)<.13f && companionMood==CompanionMood.Calm)frame=1;
            float age=companionReactionAge;
            float settle=Mathf.Sin(age*19)*Mathf.Exp(-age*5);
            float hop=companionMood==CompanionMood.Excited?Mathf.Abs(Mathf.Sin(age*9))*12*Mathf.Min(1,companionReactionTime*2):
                companionMood==CompanionMood.Happy?Mathf.Sin(age*7)*4:Mathf.Sin(battleTime*2.2f)*2;
            float recoil=companionMood==CompanionMood.Hurt?Mathf.Sin(Mathf.Clamp01(age/.7f)*Mathf.PI)*12:0;
            if (lifeIdle)
                hop = Mathf.Sin(battleTime * (lives == 1 ? 3.5f : 2.2f)) * (lives == 1 ? 3.5f : 1.8f);
            Vector2 pivot=new Vector2(1120,620);
            Matrix4x4 matrix=GUI.matrix;
            float tilt=companionMood==CompanionMood.Hurt?-recoil:companionMood==CompanionMood.Excited?settle*10:
                companionMood==CompanionMood.Disappointed?5:Mathf.Sin(battleTime*2)*1.5f;
            GUIUtility.RotateAroundPivot(tilt,pivot);
            Vector2 stretch=new Vector2(1-settle*.035f,1+settle*.055f);
            GUIUtility.ScaleAroundPivot(stretch,pivot);
            Rect cell=new Rect((frame%4)*.25f,(2-frame/4)/3f,.25f,1/3f);
            GUI.DrawTextureWithTexCoords(new Rect(1000+recoil,394-hop,240,240),sheet,cell,true);
            GUI.matrix=matrix;
        }
    }
}
