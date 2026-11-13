using UnityEngine;

namespace Bushido
{
    // Guard, deflect, posture-break and hurt feedback.
    // One reusable "pulse" (ring + crossed brush strokes) is animated here; scatter goes through the shared
    // debris list (AddDebris) and never past its 48 cap. Only palette colours: art.Material caches per colour.
    public sealed partial class BushidoPrototype
    {
        const int GuardFxBlock = 0, GuardFxDeflect = 1, GuardFxBreak = 2;
        // Indexed by kind: block (small, dull) < deflect (gold, sharp) < posture break (big tear).
        static readonly float[] guardFxLife = { 0.18f, 0.30f, 0.50f }, guardFxRadius = { 0.5f, 1.05f, 2.7f },
            guardFxWidth = { 0.05f, 0.09f, 0.17f }, guardFxCross = { 0f, 1.5f, 2.6f };
        static readonly Vector3[] guardFxCircle = GuardFxLoop(0f), guardFxInked = GuardFxLoop(0.05f),
            guardFxTorn = GuardFxLoop(0.16f),
            guardFxBrush = { new Vector3(-0.5f, 0, 0), new Vector3(0, 0.05f, 0), new Vector3(0.5f, 0, 0) };
        readonly LineRenderer[] guardFxStrokes = new LineRenderer[4]; // ink A, ink B, gold A, white B
        Transform guardFxPulse;
        LineRenderer guardFxRing, guardFxStance;
        int guardFxKind;
        float guardFxAge = 99f, guardFxSeenHurt;

        static Vector3[] GuardFxLoop(float tear)
        {
            var points = new Vector3[24];
            for (int i = 0; i < points.Length; i++)
            {
                float angle = i * Mathf.PI * 2f / points.Length;
                points[i] = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0) * (1f + Mathf.Sin(i * 2.9f) * tear);
            }
            return points;
        }

        void GuardFxBuild()
        {
            guardFxAge = 99f;
            guardFxSeenHurt = 0;
            guardFxPulse = new GameObject("Pulso de guardia").transform;
            guardFxPulse.SetParent(world, false);
            guardFxRing = art.Line(guardFxPulse, guardFxCircle, PrototypeArt.Paper, 0.05f);
            guardFxRing.loop = true;
            // Brush stroke: pressed in near the start, lifted to a point.
            var brush = new AnimationCurve(new Keyframe(0, 0), new Keyframe(0.3f, 1), new Keyframe(1, 0));
            for (int i = 0; i < 4; i++)
            {
                bool top = i >= 2;
                var stroke = art.Line(guardFxPulse, guardFxBrush,
                    !top ? PrototypeArt.Ink : i == 2 ? PrototypeArt.Gold : Color.white, 0.1f);
                stroke.widthCurve = brush;
                stroke.transform.localPosition = new Vector3(0, 0, top ? -0.03f : 0f); // colour over its ink outline
                stroke.transform.localRotation = Quaternion.Euler(0, 0, i % 2 == 0 ? 0f : 72f);
                guardFxStrokes[i] = stroke;
            }
            guardFxPulse.gameObject.SetActive(false);
            guardFxStance = art.Line(world, guardFxInked, PrototypeArt.Paper, 0.035f);
            guardFxStance.loop = true;
            guardFxStance.enabled = false;
        }

        void GuardFxUpdate(float delta)
        {
            if (guardFxPulse == null) return;
            if (hurtFlash > guardFxSeenHurt) GuardFxHurtBurst();
            guardFxSeenHurt = hurtFlash;

            // Stance ring: gold and closing in during the deflect window, then a thin dull ring while holding.
            guardFxStance.enabled = guarding;
            if (guarding)
            {
                float height = view.orthographicSize * 0.68f; // fighter's on-screen height
                float window = Mathf.Clamp01(guardHeldTime / DeflectWindow);
                bool sharp = window < 1f;
                Transform ring = guardFxStance.transform;
                ring.position = fighter.position + new Vector3(0, height * 0.55f, -0.4f);
                ring.localScale = Vector3.one * height * Mathf.Lerp(0.74f, 0.46f, 1f - (1f - window) * (1f - window));
                ring.Rotate(0, 0, delta * 35f);
                guardFxStance.widthMultiplier = sharp ? 0.09f : 0.035f;
                guardFxStance.sharedMaterial = art.Material(sharp ? PrototypeArt.Gold
                    : stamina <= 25 ? PrototypeArt.Red : PrototypeArt.Paper);
            }

            if (!guardFxPulse.gameObject.activeSelf) return;
            guardFxAge += delta;
            if (guardFxAge >= guardFxLife[guardFxKind]) guardFxPulse.gameObject.SetActive(false);
            else GuardFxPose();
        }

        void GuardFxPose()
        {
            int kind = guardFxKind;
            float t = Mathf.Clamp01(guardFxAge / guardFxLife[kind]);
            float ease = 1f - (1f - t) * (1f - t) * (1f - t);
            guardFxRing.transform.localScale = Vector3.one * guardFxRadius[kind] * (0.4f + 0.6f * ease);
            guardFxRing.widthMultiplier = guardFxWidth[kind] * (1f - t);
            // The cross is the impact frame: full on contact, gone before the ring settles.
            float cross = Mathf.Clamp01(t / 0.45f), thin = (1f - cross) * (1f - cross);
            for (int i = 0; i < 4; i++)
            {
                LineRenderer stroke = guardFxStrokes[i];
                stroke.enabled = guardFxCross[kind] > 0 && cross < 1f;
                stroke.transform.localScale = Vector3.one * guardFxCross[kind] * (i % 2 == 0 ? 1f : 0.6f) * (0.55f + 0.45f * ease);
                stroke.widthMultiplier = guardFxCross[kind] * 0.11f * thin * (i < 2 ? 1.8f : 1f);
            }
        }

        void GuardFxPulseAt(Vector3 contact, int kind, Vector3 direction)
        {
            guardFxKind = kind;
            guardFxAge = 0;
            guardFxPulse.gameObject.SetActive(true);
            guardFxPulse.position = contact;
            guardFxPulse.rotation = Quaternion.Euler(0, 0, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg);
            guardFxRing.SetPositions(kind == GuardFxBreak ? guardFxTorn : guardFxCircle);
            guardFxRing.sharedMaterial = art.Material(kind == GuardFxDeflect ? PrototypeArt.Gold : PrototypeArt.Paper);
            GuardFxPose();
        }

        // Flat paper shreds fanned around `direction`; spread is the half-angle in radians.
        void GuardFxShards(Vector3 origin, Vector3 direction, int count, float spread, float speed, float size, Color main, Color accent)
        {
            count = Mathf.Min(count, 48 - cutEffects.Count);
            float heading = Mathf.Atan2(direction.y, direction.x);
            for (int i = 0; i < count; i++)
            {
                float angle = heading + Mathf.Sin(i * 2.4f) * spread;
                Vector3 fly = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0);
                float length = size * (0.6f + (i % 4) * 0.25f);
                var shard = art.Shape(world, "Jirón de papel", PrimitiveType.Cube, origin + fly * 0.2f,
                    new Vector3(length * 0.45f, length, 0.02f), i % 3 == 0 ? accent : main);
                shard.transform.rotation = Quaternion.Euler(0, 0, angle * Mathf.Rad2Deg - 90f + (i % 2 == 0 ? 22f : -22f));
                AddDebris(shard.transform, fly * speed * (0.6f + (i % 5) * 0.2f) + Vector3.up * 1.5f,
                    new Vector3(0, 0, (i % 2 == 0 ? 240f : -240f) + i * 30f), 0.34f + (i % 3) * 0.06f);
            }
        }

        void GuardFxBreakBurst()
        {
            if (guardFxPulse == null) return;
            Vector3 center = CounterCenter;
            center.z = -2.3f;
            GuardFxPulseAt(center, GuardFxBreak, new Vector3(0.94f, 0.34f, 0));
            GuardFxShards(center, Vector3.up, 14, Mathf.PI, 5.5f, 0.42f, PrototypeArt.Paper, PrototypeArt.Gold);
            impactTime = Mathf.Max(impactTime, 0.09f);
            shakeTime = Mathf.Max(shakeTime, 0.26f);
        }

        void GuardFxHurtBurst()
        {
            float height = view.orthographicSize * 0.68f;
            Vector3 chest = fighter.position + new Vector3(0, height * 0.6f, -0.6f);
            int count = Mathf.Min(8, 48 - cutEffects.Count);
            for (int i = 0; i < count; i++)
            {
                // Fan up and towards the stage; gravity in the debris update turns it into dripping ink.
                float angle = Mathf.PI * (0.3f + 0.8f * i / 7f);
                Vector3 fly = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0);
                float size = 0.16f + (i % 3) * 0.09f;
                var blot = art.Shape(world, "Mancha de tinta roja", PrimitiveType.Sphere, chest + fly * 0.25f,
                    new Vector3(size, size * 1.3f, 0.02f), i % 4 == 3 ? PrototypeArt.Ink : PrototypeArt.Red);
                blot.transform.rotation = Quaternion.Euler(0, 0, angle * Mathf.Rad2Deg - 90f);
                AddDebris(blot.transform, fly * (2.2f + (i % 3) * 1.1f), Vector3.zero, 0.42f + (i % 2) * 0.12f);
            }
            shakeTime = Mathf.Max(shakeTime, 0.28f);
        }
    }
}
