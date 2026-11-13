using UnityEngine;

namespace Bushido
{
    // Presentation only: nothing here changes seal rules, durations or damage.
    public sealed partial class BushidoPrototype
    {
        const float JutsuFxSealIn = 0.45f, JutsuFxBurstTime = 0.45f;
        const int JutsuFxRingPoints = 33;
        static readonly AnimationCurve JutsuFxBrush = new AnimationCurve(
            new Keyframe(0, 0.5f), new Keyframe(0.14f, 1), new Keyframe(0.8f, 0.85f), new Keyframe(1, 0.3f));
        Transform jutsuFxSealArt, jutsuFxRing;
        LineRenderer[] jutsuFxSealInk;
        LineRenderer jutsuFxRingLine, jutsuFxGlyph;
        float jutsuFxSealAge, jutsuFxRingAge;
        int jutsuFxKind;

        // Gold stroke over a wider ink stroke, so the seal reads on any backdrop.
        // The art sits on a child: the traced path (jutsuSeal) never changes size.
        void JutsuFxBuildSeal()
        {
            jutsuFxSealAge = 0;
            jutsuFxSealArt = new GameObject("Trazo del sello").transform;
            jutsuFxSealArt.SetParent(jutsuSeal, false);
            jutsuFxSealInk = new LineRenderer[sealStrokes.Length];
            for (int i = 0; i < sealStrokes.Length; i++)
            {
                jutsuFxSealInk[i] = art.Line(jutsuFxSealArt, sealStrokes[i], PrototypeArt.Ink, 0.17f);
                jutsuFxSealInk[i].transform.localPosition = Vector3.forward * 0.02f;
                sealLegs[i] = art.Line(jutsuFxSealArt, sealStrokes[i], i == 0 ? PrototypeArt.Gold : PrototypeArt.Ink, 0.085f);
                sealLegs[i].widthCurve = jutsuFxSealInk[i].widthCurve = JutsuFxBrush;
            }
            JutsuFxUpdateSeal(0);
        }

        void JutsuFxUpdateSeal(float delta)
        {
            bool painting = jutsuFxSealAge < JutsuFxSealIn;
            jutsuFxSealAge += delta;
            float appear = Mathf.Clamp01(jutsuFxSealAge / JutsuFxSealIn);
            float leave = Mathf.Clamp01(sealTime / 0.3f);
            float settle = 1 - Mathf.Pow(1 - appear, 3);
            jutsuFxSealArt.localScale = Vector3.one * (Mathf.Lerp(1.25f, 1f, settle) * Mathf.Lerp(0.6f, 1f, leave));
            // The last three seconds flicker so a fading seal is never a surprise.
            float warn = sealTime < 3f ? 0.65f + 0.35f * Mathf.Sin(sealTime * 14f) : 1f;
            float breathe = 1 + 0.12f * Mathf.Sin(jutsuFxSealAge * 4f);
            for (int i = 0; i < sealLegs.Length; i++)
            {
                if (painting)
                {
                    JutsuFxReveal(sealLegs[i], sealStrokes[i], settle);
                    JutsuFxReveal(jutsuFxSealInk[i], sealStrokes[i], settle);
                }
                sealLegs[i].widthMultiplier = 0.085f * leave * warn * (completedSealStrokes[i] ? 1 : breathe);
                jutsuFxSealInk[i].widthMultiplier = 0.17f * leave;
            }
        }

        // Draws the first `t` of the path, like a brush being pulled along it.
        static void JutsuFxReveal(LineRenderer line, Vector3[] path, float t)
        {
            float span = Mathf.Clamp01(t) * (path.Length - 1);
            int whole = Mathf.Min((int)span, path.Length - 2);
            line.positionCount = whole + 2;
            for (int i = 0; i <= whole; i++) line.SetPosition(i, path[i]);
            line.SetPosition(whole + 1, Vector3.Lerp(path[whole], path[whole + 1], span - whole));
        }

        // Ring burst + paper talismans; the ring then settles into the timer of the active jutsu.
        void JutsuFxActivate(Vector3 center)
        {
            impactTime = Mathf.Max(impactTime, 0.07f);
            Color tint = sealVariant == 1 ? PrototypeArt.Gold : JutsuInk;
            center.z = -2f;
            if (jutsuFxRing != null) Destroy(jutsuFxRing.gameObject);
            jutsuFxRing = new GameObject("Onda del jutsu").transform;
            jutsuFxRing.SetParent(world, false);
            jutsuFxRing.position = center;
            var circle = new Vector3[JutsuFxRingPoints];
            for (int i = 0; i < circle.Length; i++)
            {
                // Starts at the top and runs clockwise, so the timer empties like a clock.
                float angle = Mathf.PI * 0.5f - i * Mathf.PI * 2f / (circle.Length - 1);
                circle[i] = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0);
            }
            jutsuFxRingLine = art.Line(jutsuFxRing, circle, tint, 0.2f);
            jutsuFxGlyph = art.Line(jutsuFxRing, sealStrokes[0], tint, 0);
            jutsuFxGlyph.transform.localScale = Vector3.one * 0.45f;
            jutsuFxKind = sealVariant;
            jutsuFxRingAge = 0;
            JutsuFxShreds(center, 8, PrototypeArt.Paper, tint, 3.2f, 0.95f);
            JutsuFxUpdateRing(0);
        }

        void JutsuFxUpdateRing(float delta)
        {
            if (jutsuFxRing == null) return;
            jutsuFxRingAge += delta;
            float burst = Mathf.Clamp01(jutsuFxRingAge / JutsuFxBurstTime);
            if (burst < 1)
            {
                jutsuFxRingLine.transform.localScale = Vector3.one * Mathf.Lerp(0.5f, 3.2f, 1 - Mathf.Pow(1 - burst, 3));
                jutsuFxRingLine.widthMultiplier = 0.2f * (1 - burst);
                return;
            }
            float left = jutsuFxKind == 0 ? jutsuTime / JutsuDuration : jutsuFxKind == 1 ? abundanceTime / 8f : 0;
            if (left <= 0)
            {
                // Running out: the timer drops as a few paper shreds.
                if (jutsuFxKind < 2) JutsuFxShreds(jutsuFxRing.position, 4, PrototypeArt.Paper, PrototypeArt.Ink, 1.2f, 0.6f);
                Destroy(jutsuFxRing.gameObject);
                jutsuFxRing = null;
                return;
            }
            float settle = Mathf.Clamp01((jutsuFxRingAge - JutsuFxBurstTime) / 0.25f);
            float pulse = Mathf.Sin(jutsuFxRingAge * (left < 0.25f ? 14f : 3f));
            jutsuFxRingLine.transform.localScale = Vector3.one * 0.62f;
            jutsuFxRingLine.positionCount = 2 + Mathf.RoundToInt((JutsuFxRingPoints - 2) * left);
            jutsuFxRingLine.widthMultiplier = settle * (0.045f + 0.012f * pulse);
            jutsuFxGlyph.widthMultiplier = settle * 0.04f;
        }

        // Flat paper shreds on the shared debris list (respects its cap of 48).
        void JutsuFxShreds(Vector3 origin, int count, Color first, Color second, float speed, float lifetime)
        {
            count = Mathf.Min(count, 48 - cutEffects.Count);
            for (int i = 0; i < count; i++)
            {
                float angle = (i + Random.value * 0.6f) * Mathf.PI * 2f / count;
                Vector3 direction = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0);
                var shred = art.Shape(world, "Jirón de papel", PrimitiveType.Cube, origin + direction * 0.2f,
                    new Vector3(0.13f + (i % 3) * 0.04f, 0.3f, 0.015f), i % 2 == 0 ? first : second);
                shred.transform.rotation = Quaternion.Euler(0, 0, Random.Range(0f, 360f));
                AddDebris(shred.transform, direction * speed * Random.Range(0.6f, 1f) + Vector3.up * speed * 0.5f,
                    new Vector3(220, 0, i % 2 == 0 ? 160 : -160), lifetime);
            }
        }
    }
}
