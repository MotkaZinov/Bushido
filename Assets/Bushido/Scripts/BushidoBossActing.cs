using UnityEngine;
using System.Collections.Generic;
namespace Bushido
{
    public sealed partial class BushidoPrototype
    {
        enum BossReaction { None, Deflected, Seal, Guard }
        BossReaction bossReaction;
        float bossReactionTime;
        readonly List<Transform> bossPoseBones=new List<Transform>();
        readonly List<Quaternion> bossRestRotations=new List<Quaternion>();
        Transform posedBoss, bossPaperWeapon;
        void ReactBoss(BossReaction reaction)
        {
            bossReaction=reaction;bossReactionTime=0;
        }
        void UpdateBossActing(float delta)
        {
            if(opponent==null || bossInkBurst)return;
            if(posedBoss!=opponent)
            {
                posedBoss=opponent;bossPoseBones.Clear();bossRestRotations.Clear();
                paperOgre=opponent.GetComponent<PaperOgreSprite>();
                var unique=new HashSet<Transform>();
                foreach(var skin in opponent.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                    foreach(var bone in skin.bones)
                        if(bone!=null && unique.Add(bone)){bossPoseBones.Add(bone);bossRestRotations.Add(bone.localRotation);}
                bossReaction=BossReaction.None;bossReactionTime=0;
                if(bossPaperWeapon!=null)Destroy(bossPaperWeapon.gameObject);
                if(paperSwordMesh!=null && paperOgre==null)
                {
                    var weapon=art.Shape(world,"Arma ilustrada del jefe",PrimitiveType.Quad,Vector3.zero,Vector3.one,Color.white);
                    weapon.GetComponent<MeshFilter>().sharedMesh=Instantiate(paperSwordMesh);
                    weapon.AddComponent<SliceMeshOwner>();
                    weapon.GetComponent<Renderer>().sharedMaterial=art.PaperWeaponMaterial(Resources.Load<Texture2D>("Weapons/PaperKatana"));
                    bossPaperWeapon=weapon.transform;
                }
            }
            if(paperOgre!=null){UpdatePaperOgre(delta);return;}
            bossReactionTime+=delta;
            float delay=DuelActive?DuelStrikeDelay:SecondPhase?.6f:.85f;
            float wind=theaterState==2&&!theaterStrike?Mathf.Clamp01(theaterTime/(delay-DeflectWindow)):0;
            float strike=theaterState==2&&!vulnerable?Mathf.Clamp01((theaterTime-(delay-DeflectWindow))/DeflectWindow):0;
            float recover=theaterStrike&&!vulnerable?1-Mathf.Clamp01((theaterTime-delay)/.48f):1;
            float swing=Mathf.SmoothStep(0,1,strike)*recover;
            Vector3 offset=new Vector3(-.22f*wind+.65f*swing,.09f*wind-.17f*swing,.32f*wind-.85f*swing);
            Vector3 angles=new Vector3(-7*wind+16*swing,0-18*wind+32*swing,7*wind-13*swing);
            float arm=wind*55-swing*105, chest=wind*12-swing*24;
            if(bossReaction!=BossReaction.None)
            {
                float duration=bossReaction==BossReaction.Seal?.85f:bossReaction==BossReaction.Deflected?.65f:.38f;
                float t=Mathf.Clamp01(bossReactionTime/duration);
                float kick=Mathf.Sin(Mathf.Clamp01(t/.2f)*Mathf.PI*.5f)*(1-Mathf.SmoothStep(.2f,1,t));
                if(bossReaction==BossReaction.Deflected){offset=new Vector3(-.48f,.08f,.5f)*kick;angles=new Vector3(-19*kick,0+23*kick,16*kick);arm=-55*kick;chest=-20*kick;}
                else if(bossReaction==BossReaction.Seal){offset=new Vector3(Mathf.Sin(t*27)*.045f,-.28f*kick,.28f*kick);angles=new Vector3(18*kick,0,Mathf.Sin(t*22)*4*kick);arm=24*kick;chest=25*kick;}
                else {offset=new Vector3(.1f,0,.12f)*kick;angles=new Vector3(0,0-12*kick,-7*kick);arm=65*kick;chest=-10*kick;}
                if(t>=1)bossReaction=BossReaction.None;
            }
            if(DuelActive && bossReaction==BossReaction.None && (duelPhase==DuelPhase.Exhausted || duelPhase==DuelPhase.Seal))
            {
                offset=new Vector3(0,-.16f,0);angles=new Vector3(14,0,Mathf.Sin(duelClock*1.6f)*1.2f);arm=18;chest=16;
            }
            if(bossFinalStage==1 && bossReaction==BossReaction.None){offset=new Vector3(0,-.2f,0);angles=new Vector3(12,0,Mathf.Sin(Time.time*9)*1.5f);arm=18;chest=16;}
            if(bossFinalStage==2)
            {
                float t=Mathf.Clamp01((bossFinalTime-.12f)/.65f);
                offset=new Vector3(.32f*t,-.55f*t,.15f*t);angles=new Vector3(-12*t,0+14*t,-24*t);arm=-45*t;chest=-18*t;
            }
            opponent.position=new Vector3(0,1.0f,3-paperOpening*2)+offset;
            opponent.rotation=Quaternion.Euler(angles);
            if(bossPaperWeapon!=null)
            {
                bossPaperWeapon.gameObject.SetActive(bossFinalStage==0 && opponent.gameObject.activeInHierarchy && (theaterState==2 || bossReaction==BossReaction.Guard));
                float guardKick=bossReaction==BossReaction.Guard?Mathf.Sin(Mathf.Clamp01(bossReactionTime/.38f)*Mathf.PI):0;
                bossPaperWeapon.position=opponent.position+new Vector3(.5f-.9f*swing,1.75f+.2f*wind,-1.2f);
                bossPaperWeapon.rotation=Quaternion.Euler(0,0,-35+wind*110-swing*155+guardKick*60);
                bossPaperWeapon.localScale=Vector3.one*.85f;
            }
            for(int i=0;i<bossPoseBones.Count;i++)
            {
                var bone=bossPoseBones[i];string n=bone.name.ToLowerInvariant();float bend=0;
                if(n.Contains("upperarm")||n.Contains("upper_arm"))bend=arm;
                else if(n.Contains("spine")||n.Contains("chest"))bend=chest*.4f;
                else if(n.Contains("head"))bend=-chest*.3f;
                bone.localRotation=bossRestRotations[i]*Quaternion.Euler(bend,0,0);
            }
        }
        void DrawParryPrompt()
        {
            DrawDuelHUD();
        }
    }
}
