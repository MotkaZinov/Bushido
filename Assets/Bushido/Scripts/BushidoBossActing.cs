using UnityEngine;
namespace Bushido
{
    public sealed partial class BushidoPrototype
    {
        enum BossReaction { None, Deflected, Seal, Guard }
        BossReaction bossReaction;
        float bossReactionTime;
        void ReactBoss(BossReaction reaction)
        {
            bossReaction=reaction;bossReactionTime=0;
            // Half the deflects get a complaint, so it stays a reaction and not a loop.
            if(reaction==BossReaction.Deflected && IsBoss && Random.value<.5f)BossSay(VoiceUmbrella,BossPick(UmbrellaHurt),1.8f);
        }
        void UpdateBossActing(float delta)
        {
            if (opponent != null && !bossInkBurst) UpdateUmbrella(delta);
        }

        Vector3 BossSealTarget()
        {
            if (opponent == null) return CounterCenter;
            return opponent.TransformPoint(new Vector3((bossSeals - 2) * .35f, 1.3f, -.25f));
        }
        void DrawParryPrompt()
        {
            DrawDuelHUD();
        }
    }
}
