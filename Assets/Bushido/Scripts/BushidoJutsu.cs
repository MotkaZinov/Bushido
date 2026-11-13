using UnityEngine;
namespace Bushido
{
    public sealed partial class BushidoPrototype
    {
        static readonly Color JutsuInk = new Color(0.42f, 0.8f, 0.6f); // luminous jade, in palette
        const float JutsuDuration = 4f;
        Transform jutsuSeal;
        float sealTime, jutsuTime;
        int sealVariant, sealsShown, activeSealStroke = -1, sealCheckpoint;
        Vector3[][] sealStrokes;
        LineRenderer[] sealLegs;
        bool[] completedSealStrokes;
        bool sealGestureClaimed;
        const float SealTraceTolerance = .46f;
        static Vector3[] K(params float[] points)
        {
            var result = new Vector3[points.Length / 2];
            for (int i = 0; i < result.Length; i++) result[i] = new Vector3(points[i * 2], points[i * 2 + 1]);
            return result;
        }
        static Vector3[][] KanjiPaths(int variant) => new[] { variant == 0
            ? K(-.7f,.7f,.7f,.7f,-.7f,-.7f,.7f,-.7f)
            : variant == 1 ? K(-.8f,.7f,-.4f,-.7f,0,.25f,.4f,-.7f,.8f,.7f)
            : K(.65f,.55f,.2f,.8f,-.45f,.65f,-.75f,.1f,-.55f,-.55f,.1f,-.75f,.65f,-.4f,.65f,0,.1f,0) };
        float abundanceTime;
        void ResetJutsu()
        {
            jutsuSeal = null; jutsuFxRing = null; sealTime = jutsuTime = abundanceTime = 0; sealsShown = 0;
            activeSealStroke = -1; sealGestureClaimed = false;
        }
        void ShowJutsuSeal()
        {
            ClearJutsuSeal();
            sealVariant = sealsShown++ % 3;
            sealStrokes = KanjiPaths(sealVariant);
            completedSealStrokes = new bool[sealStrokes.Length];
            sealLegs = new LineRenderer[sealStrokes.Length];
            jutsuSeal = new GameObject(sealVariant == 0 ? "Z - Tiempo" : sealVariant == 1 ? "W - Abundancia" : "G - Destierro").transform;
            jutsuSeal.SetParent(world, false);
            jutsuSeal.position = view.ViewportToWorldPoint(new Vector3(0.18f, 0.57f, 15));
            jutsuSeal.localScale = Vector3.one * 1.3f;
            sealTime = 18;
            JutsuFxBuildSeal();
            ResetSealTrace();
        }
        void UpdateJutsu(float delta)
        {
            abundanceTime = Mathf.Max(0, abundanceTime - delta);
            jutsuTime = Mathf.Max(0, jutsuTime - delta);
            JutsuFxUpdateRing(delta);
            if (jutsuSeal == null) return;
            sealTime -= delta;
            if (sealTime <= 0) ClearJutsuSeal();
            else JutsuFxUpdateSeal(delta);
        }
        void ClearJutsuSeal()
        {
            if (jutsuSeal != null) Destroy(jutsuSeal.gameObject);
            jutsuSeal = null; activeSealStroke = -1; sealGestureClaimed = false;
        }
        void ResetSealTrace()
        {
            activeSealStroke = -1; sealCheckpoint = 0; sealGestureClaimed = false;
        }
        bool TraceJutsu(Vector3 from, Vector3 to)
        {
            if (jutsuSeal == null) return false;
            if (activeSealStroke < 0 && !sealGestureClaimed)
            {
                for (int i = 0; i < sealStrokes.Length; i++)
                {
                    if (completedSealStrokes[i]) continue;
                    if (SegmentDistance(jutsuSeal.TransformPoint(sealStrokes[i][sealStrokes[i].Length - 1]), from, to) <= SealTraceTolerance)
                        System.Array.Reverse(sealStrokes[i]);
                    if (SegmentDistance(jutsuSeal.TransformPoint(sealStrokes[i][0]), from, to) > SealTraceTolerance) continue;
                    activeSealStroke = i; sealCheckpoint = 1; sealGestureClaimed = true;
                    break;
                }
            }
            if (activeSealStroke < 0) return sealGestureClaimed;
            Vector3[] path = sealStrokes[activeSealStroke];
            // A fast swipe can cross several checkpoints between input frames.
            // Require them in order along the segment, including reversed strokes.
            float previousFraction = -1f;
            Vector3 segment = to - from;
            float lengthSquared = segment.sqrMagnitude;
            while (sealCheckpoint < path.Length)
            {
                Vector3 point = jutsuSeal.TransformPoint(path[sealCheckpoint]);
                float fraction = Mathf.Clamp01(Vector3.Dot(point - from, segment) / Mathf.Max(lengthSquared, .00001f));
                if (fraction + .05f < previousFraction || SegmentDistance(point, from, to) > SealTraceTolerance) break;
                previousFraction = fraction;
                sealCheckpoint++;
            }
            if (sealCheckpoint < path.Length) return true;
            completedSealStrokes[activeSealStroke] = true;
            sealLegs[activeSealStroke].sharedMaterial = art.Material(JutsuInk);
            activeSealStroke = -1;
            for (int i = 0; i < completedSealStrokes.Length; i++)
                if (!completedSealStrokes[i]) { sealLegs[i].sharedMaterial = art.Material(PrototypeArt.Gold); return true; }
            Vector3 center = jutsuSeal.position;
            ClearJutsuSeal();
            if (sealVariant == 0) jutsuTime = JutsuDuration;
            else if (sealVariant == 1) abundanceTime = 8f;
            else
            {
                for (int i = projectiles.Count - 1; i >= 0; i--)
                {
                    if (projectiles[i].kind != ProjectileKind.Frog) continue;
                    RewardCut(projectiles[i].root.position); RemoveProjectile(i);
                }
                if (IsBoss && !vulnerable) { DamagePosture(3); if (guard >= guardLimit) OpenCounter(); }
            }
            waveTimer = Mathf.Min(waveTimer, 0.35f);
            JutsuFxActivate(center); PlayGuardSound(true);
            return true;
        }
    }
}



