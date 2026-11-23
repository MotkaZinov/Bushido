using UnityEngine;
namespace Bushido
{
    public sealed partial class BushidoPrototype
    {
        enum CompanionMood { Calm, Focus, Happy, Excited, Parry, Hurt, Disappointed, Seal, Danger, Draw, Victory }
        float companionReactionTime, companionReactionAge;
        int companionPreviousCombo, companionPreviousRank, companionPreviousSeals, companionPreviousLives;
        bool companionWasGuardFlash;
        CompanionMood companionMood;
        LineRenderer companionTrail;
        Transform belt;
        Quaternion beltRest;
        float clothAngle, clothVelocity, previousTurn;
        readonly Vector3[] edgeSamples = new Vector3[7];
        int edgeCount;
        float reactionBlend, companionClock;
        void BeginCompanionFace()
        {
            companionTrail = art.Line(world, edgeSamples, PrototypeArt.Gold, .055f);
            companionTrail.useWorldSpace = true;
            companionTrail.widthCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
            companionTrail.enabled = false;
            edgeCount = 0;
            companionClock = reactionBlend = 0f;
            clothAngle = clothVelocity = previousTurn = 0f;
            belt = System.Array.Find(fighter.GetComponentsInChildren<Transform>(), part => part.name == "Cinturon");
            if (belt != null) beltRest = belt.localRotation;
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
        void PoseCompanion(float delta, float swing)
        {
            if (fighterRig == null) return;
            companionClock += delta;
            float energy = Mathf.Clamp01(companionReactionTime * 5f);
            if (bossFinalStage != 0 || mode == Mode.Result || companionMood == CompanionMood.Happy) energy = 1;
            reactionBlend = Mathf.MoveTowards(reactionBlend, energy, delta * 9f);
            float beat = Mathf.Sin(companionClock * (lives == 1 ? 4.6f : 2.4f));
            float fatigue = (3 - Mathf.Clamp(lives, 0, 3)) / 3f;
            float nod = 2f * beat + 11f * fatigue;
            float gaze = 9f, lean = 3f * fatigue, knees = 7f + fatigue * 12f;
            float leftStep = swing * 16f, freeHand = 0, eyes = 1f;
            // Breathing and a slow shift of weight between the feet, so the guard never looks frozen.
            float breath = Mathf.Sin(companionClock * 2.1f);
            knees += 3f * (1f + breath);
            lean += 1.6f * breath;
            nod += 1.2f * breath;
            leftStep += Mathf.Sin(companionClock * .55f) * 6f;
            knees += samuraiCutCrouch; lean += samuraiCutLean;
            float response = reactionBlend;
            float bounce = Mathf.Sin(companionReactionAge * 10f);
            switch (companionMood)
            {
                case CompanionMood.Focus: nod += 7 * response; eyes -= .36f * response; break;
                case CompanionMood.Happy: nod -= (7 + bounce * 5) * response; freeHand = 18 * response; break;
                case CompanionMood.Excited: knees += (1 + bounce) * 12 * response; freeHand = 42 * response; break;
                case CompanionMood.Parry: lean -= 12 * response; leftStep += 18 * response; eyes -= .42f * response; break;
                case CompanionMood.Hurt: nod -= 13 * response; gaze -= 19 * response; knees += 24 * response; eyes -= .85f * response; break;
                case CompanionMood.Disappointed: nod += 21 * response; lean += 10 * response; break;
                case CompanionMood.Seal: freeHand = 58 * response; gaze += 14 * response; break;
                case CompanionMood.Danger: knees += 6 + beat * 4; gaze += beat * 6; eyes = .74f; break;
                case CompanionMood.Draw: nod += 11; leftStep += 22; freeHand = 28; lean += 9 * drawPullProgress; break;
                case CompanionMood.Victory: nod -= 9; freeHand = 54; knees -= 4; break;
            }
            float blinkPhase = Mathf.Repeat(companionClock + .9f, 3.65f);
            float blink = Mathf.Clamp01(Mathf.Abs(blinkPhase - .095f) / .095f);
            fighterRig.PoseExpression(nod, gaze, lean, knees, leftStep, freeHand, eyes * blink);
            if (belt != null && delta > 0f)
            {
                float turn = Mathf.DeltaAngle(168f, fighter.eulerAngles.y);
                float drag = Mathf.Clamp(-Mathf.DeltaAngle(previousTurn, turn) / delta * .03f, -11f, 11f);
                previousTurn = turn;
                clothAngle = Mathf.SmoothDamp(clothAngle, drag, ref clothVelocity, .16f, Mathf.Infinity, delta);
                belt.localRotation = Quaternion.AngleAxis(clothAngle, belt.parent.InverseTransformDirection(fighter.up)) * beltRest;
            }
            float grip = Mathf.Max(guardPose, fighterMove == FighterMove.Cut ? swing : 0f);
            fighterRig.SupportHilt(grip * (1f - Mathf.Clamp01(freeHand / 40f)));
            bool tracing = mode == Mode.Battle && bossFinalStage == 0 && judgmentRoot == null
                && fighterSwing > 0 && (fighterMove == FighterMove.Cut || fighterMove == FighterMove.MultiCut);
            if (!tracing)
            {
                edgeCount = 0;
                companionTrail.enabled = false;
                return;
            }
            Vector3 tip = fighterRig.BladeTip;
            if (edgeCount > 0 && (tip - edgeSamples[edgeCount - 1]).sqrMagnitude < .0009f) return;
            if (edgeCount == edgeSamples.Length)
            {
                System.Array.Copy(edgeSamples, 1, edgeSamples, 0, edgeCount - 1);
                edgeCount--;
            }
            edgeSamples[edgeCount++] = tip;
            companionTrail.positionCount = edgeCount;
            for (int i = 0; i < edgeCount; i++) companionTrail.SetPosition(i, edgeSamples[i]);
            companionTrail.enabled = edgeCount > 1;
        }
    }
}
