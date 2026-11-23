using UnityEngine;

namespace Bushido
{
    public sealed partial class BushidoPrototype
    {
        PaperOgreSprite paperOgre;
        float ogreSealHeight = 1.6f;
        static readonly int[] OgreBreathing = { 0, 1, 2, 1, 0, 3 };

#if UNITY_EDITOR
        [ContextMenu("Pruebas/Ogro de papel (en Play Mode)")]
        void StartPaperOgrePreview()
        {
            if(!Application.isPlaying){Debug.Log("Activa Play Mode antes de iniciar la prueba del ogro.");return;}
            mission=2;
            BeginBattle();
        }
#endif

        Transform CreatePaperOgre(Vector3 position, float height)
        {
            var combat = Resources.Load<Texture2D>("Bosses/PaperOgre/Combat");
            var binding = Resources.Load<Texture2D>("Bosses/PaperOgre/Binding");
            if (combat == null || binding == null) return null;
            var root = new GameObject("Ogro de tinta / animación dibujada").transform;
            root.SetParent(world, false); root.position = position;
            var contact=art.Shape(root,"Sombra de contacto recortada",PrimitiveType.Sphere,
                new Vector3(0,-height*.075f,.055f),new Vector3(height*.52f,height*.045f,.01f),new Color(.64f,.53f,.31f));
            float canvas = height / .8f;
            var quad = art.Shape(root, "Ilustración animada", PrimitiveType.Quad,
                Vector3.up * (canvas * .4f), new Vector3(canvas, canvas, 1), Color.white);
            var material = art.PaperWeaponMaterial(combat);
            material.SetFloat("_PaperAlphaCutoff", .2f);
            quad.GetComponent<Renderer>().sharedMaterial = material;
            var animation = root.gameObject.AddComponent<PaperOgreSprite>();
            animation.Initialize(material, combat, binding);
            return root;
        }

        void UpdatePaperOgre(float delta)
        {
            bossReactionTime += delta;
            float delay = DuelActive ? DuelStrikeDelay : SecondPhase ? .6f : .85f;
            bool bound = false;
            int frame;
            float recoil = 0;
            if (bossFinalStage == 2)
            {
                bound = true;
                frame = 12 + Mathf.Min(3, Mathf.FloorToInt(bossFinalTime / .21f));
            }
            else if (bossReaction == BossReaction.Seal)
            {
                bound = true;
                frame = (Mathf.Clamp(landedBossSeals, 1, 3) - 1) * 4 + Mathf.Min(3, Mathf.FloorToInt(bossReactionTime / .21f));
                recoil = Mathf.Sin(Mathf.Clamp01(bossReactionTime / .85f) * Mathf.PI) * .12f;
                if (bossReactionTime >= .85f) bossReaction = BossReaction.None;
            }
            else if (bossFinalStage == 1)
            {
                bound = landedBossSeals >= 3;
                frame = 10;
            }
            else if (DuelActive && (duelPhase==DuelPhase.Exhausted || duelPhase==DuelPhase.Seal || duelPhase==DuelPhase.RecoveryRound)) frame=10;
            else if (bossReaction == BossReaction.Deflected)
            {
                frame = bossReactionTime < .065f ? 7 : 8 + Mathf.Min(2, Mathf.FloorToInt((bossReactionTime-.065f) / .12f));
                recoil = Mathf.Sin(Mathf.Clamp01(bossReactionTime / .65f) * Mathf.PI) * .3f;
                if (bossReactionTime >= .65f) bossReaction = BossReaction.None;
            }
            else if (bossReaction == BossReaction.Guard)
            {
                frame = 12 + Mathf.Min(3, Mathf.FloorToInt(bossReactionTime / .095f));
                if (bossReactionTime >= .38f) bossReaction = BossReaction.None;
            }
            else if (vulnerable) frame = 10;
            else if (DuelActive && duelPhase==DuelPhase.Charge) frame=duelClock<.65f?4:5;
            else if (theaterState == 2 && !theaterStrike)
            {
                // Anticipation holds until the same timing window used by the parry logic.
                float remaining = delay - theaterTime;
                frame = remaining <= DeflectWindow ? 6 : theaterTime < (delay - DeflectWindow) * .55f ? 4 : 5;
            }
            else if (theaterState == 2 && theaterStrike && theaterTime < delay + .35f) frame = 7;
            else if (landedBossSeals > 0)
            {
                bound = true;
                frame = (Mathf.Clamp(landedBossSeals, 1, 3) - 1) * 4 + 2 + ((int)(Time.time * 4) % 2);
            }
            else frame = OgreBreathing[(int)(Time.time * 7) % OgreBreathing.Length];

            paperOgre.Show(bound, frame);
            var drawing=opponent.Find("Ilustración animada");
            if(drawing!=null)
            {
                var scale=drawing.localScale;
                scale.x=Mathf.Abs(scale.x)*(DuelActive && duelBeat%2==1 && !vulnerable?-1:1);
                drawing.localScale=scale;
            }
            bool attacking=DuelActive && duelPhase==DuelPhase.Telegraph;
            float leaning=attacking?Mathf.Clamp01((duelClock-(delay-.18f))/.18f):0;
            float side=duelBeat%2==0?1:-1;
            ogreSealHeight = bound && frame >= 8 ? (frame >= 12 ? .7f : 1.05f) : 1.6f;
            bool dazed=vulnerable && bossReaction==BossReaction.None;
            float sway=dazed?Mathf.Sin(Time.time*1.6f):0;
            opponent.rotation = Quaternion.Euler(0,0,sway*1.2f+leaning*side*(duelBeat==3?7:4));
            opponent.position = new Vector3(-recoil * .5f+sway*.025f+leaning*side*.16f, 1.0f - recoil * .2f+(dazed?Mathf.Sin(Time.time*2.2f)*.012f:0), 3 - paperOpening * 2);
            for(int i=0;i<spiritChainTargets.Count;i+=2)
            {
                var seal=spiritChainTargets[i];
                if(seal!=null && seal.parent==opponent)
                    seal.localPosition=new Vector3((i/2-1)*.35f,ogreSealHeight,-.25f);
            }
        }

        Vector3 BossSealTarget()
        {
            if(cursedMask!=null)return CursedSealTarget();
            if(opponent==null)return CounterCenter;
            float height=paperOgre!=null?ogreSealHeight:1.3f;
            return opponent.position+new Vector3((bossSeals-2)*.35f,height,-.25f);
        }
    }
}





