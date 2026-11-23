using System.Collections.Generic;
using UnityEngine;

namespace Bushido
{
    // Cortes de kenjutsu del samurái y la estela de energía de su katana.
    // Cada tajo sigue las fases de un corte real:
    //   furikaburi (alzar)  ->  kiri (cortar, la parte más rápida)  ->  zanshin (sostener, atento)
    //   ->  chiburi (sacudir la hoja hacia abajo)  ->  vuelta a la guardia.
    // El arco depende de la dirección del gesto del jugador:
    //   hacia abajo en diagonal = kesa-giri, hacia arriba = kiri-age, lateral = yoko-giri, vertical = kiri-oroshi.
    public sealed partial class BushidoPrototype
    {
        const float SamuraiCutDuration = .56f;
        float samuraiCutCrouch, samuraiCutLean;
        const float CutRaise = .16f, CutStrike = .34f, CutHold = .6f, CutFlick = .76f;
        // Rest guard: the sword arm's resting direction in fighter space (see SamuraiRig.Pose), as a screen angle.
        const float GuardAngle = 70f;
        const float ChiburiAngle = -120f;

        struct SamuraiCutPose { public Vector2 blade; public float weight, lunge, crouch, lean, cutting; }

        static float EaseOut(float t) => 1f - (1f - t) * (1f - t);
        static float EaseIn(float t) => t * t;

        // Fighter-space pose of one cut at a given phase (0..1) for a swipe direction.
        static SamuraiCutPose SamuraiCut(float phase, Vector2 direction)
        {
            float end = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            bool rising = direction.y > .35f;
            // The blade travels 150°: over the head for falling and level cuts, under the hips for rising ones.
            float over = end + 150f, under = end - 150f;
            bool overHigher = Mathf.Sin((end + 75f) * Mathf.Deg2Rad) > Mathf.Sin((end - 75f) * Mathf.Deg2Rad);
            float start = overHigher != rising ? over : under;
            var pose = new SamuraiCutPose();
            float angle;
            if (phase < CutRaise)
            {
                float t = EaseOut(phase / CutRaise);
                angle = Mathf.LerpAngle(GuardAngle, start, t);
                pose.lean = -6f * t; pose.crouch = 4f * t;
            }
            else if (phase < CutStrike)
            {
                // Accelerate into the cut so the fastest part lands in its second half.
                float t = Mathf.InverseLerp(CutRaise, CutStrike, phase);
                angle = Mathf.Lerp(start, end, EaseIn(t) * .55f + Mathf.SmoothStep(0, 1, t) * .45f);
                pose.lean = Mathf.Lerp(-6f, 10f, t); pose.crouch = Mathf.Lerp(4f, 18f, t); pose.lunge = t;
                pose.cutting = 1f;
            }
            else if (phase < CutHold)
            {
                // Zanshin: the blade stays out past the target, settling with a small overshoot.
                float t = Mathf.InverseLerp(CutStrike, CutHold, phase);
                angle = end + Mathf.Sin(t * Mathf.PI) * (end - start) * .04f;
                pose.lean = 10f; pose.crouch = 18f - 4f * t; pose.lunge = 1f;
                pose.cutting = 1f - t;
            }
            else if (phase < CutFlick)
            {
                // Chiburi: a sharp downward flick to the side.
                float t = EaseOut(Mathf.InverseLerp(CutHold, CutFlick, phase));
                angle = Mathf.LerpAngle(end, ChiburiAngle, t);
                pose.lean = 10f - 6f * t; pose.crouch = 14f - 4f * t; pose.lunge = 1f - .4f * t;
            }
            else
            {
                float t = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(CutFlick, 1f, phase));
                angle = Mathf.LerpAngle(ChiburiAngle, GuardAngle, t);
                pose.lean = 4f * (1 - t); pose.crouch = 10f * (1 - t); pose.lunge = .6f * (1 - t);
            }
            pose.blade = new Vector2(Mathf.Cos(angle * Mathf.Deg2Rad), Mathf.Sin(angle * Mathf.Deg2Rad));
            // Blend in from and back out to the procedural guard at the ends of the move.
            pose.weight = Mathf.Min(Mathf.Clamp01(phase / .05f), Mathf.Clamp01((1f - phase) / .12f));
            return pose;
        }

        // A multi-cut reads as two crossing cuts: the second one comes back the other way.
        SamuraiCutPose CurrentSamuraiCut(float phase)
        {
            if (fighterMove != FighterMove.MultiCut) return SamuraiCut(phase, fighterDirection);
            bool second = phase >= .5f;
            Vector2 direction = second ? new Vector2(-fighterDirection.x, -fighterDirection.y * .6f + .4f).normalized : fighterDirection;
            return SamuraiCut(Mathf.Repeat(phase * 2f, 1f), direction);
        }

        // ---- Katana trail: the same energy ribbon as the judgment cut, born between the guard and the tip. ----
        struct SamuraiTrailSample { public Vector2 guard, tip; public float time; }
        readonly List<SamuraiTrailSample> samuraiTrail = new List<SamuraiTrailSample>();
        readonly List<Vector3> samuraiTrailVertices = new List<Vector3>();
        readonly List<Vector2> samuraiTrailUv = new List<Vector2>(), samuraiTrailPlane = new List<Vector2>();
        readonly List<int> samuraiTrailTriangles = new List<int>();
        Transform samuraiTrailRoot;
        Mesh samuraiTrailMesh;
        Material samuraiTrailMaterial;
        float samuraiTrailClock;
        const float SamuraiTrailLife = .16f;

        void UpdateSamuraiTrail(float delta, float cutting)
        {
            if (fighterRig == null || world == null) return;
            if (samuraiTrailRoot == null)
            {
                if (samuraiTrailMesh == null)
                {
                    samuraiTrailMesh = new Mesh { name = "Estela de la katana" };
                    samuraiTrailMesh.MarkDynamic();
                    samuraiTrailMaterial = new Material(Resources.Load<Shader>("BushidoVfxEnergy")) { renderQueue = 3600 };
                    samuraiTrailMaterial.SetFloat("_Reveal", 2);
                    samuraiTrailMaterial.SetFloat("_Sharp", 1.3f);
                    samuraiTrailMaterial.SetFloat("_Streak", .7f);
                    samuraiTrailMaterial.SetFloat("_Radial", 1); // fades with the age stored in uv.x
                    samuraiTrailMaterial.SetFloat("_White", 1);
                    samuraiTrailMaterial.SetColor("_Tint", SpiritLight(.5f, 1.6f)); // same ink-and-spirit stroke as the judgment cut
                }
                samuraiTrailRoot = new GameObject("Estela de la katana").transform;
                samuraiTrailRoot.SetParent(world, false);
                samuraiTrailRoot.gameObject.AddComponent<MeshFilter>().sharedMesh = samuraiTrailMesh;
                var renderer = samuraiTrailRoot.gameObject.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = samuraiTrailMaterial;
                renderer.localBounds = new Bounds(Vector3.zero, Vector3.one * 200); // the shader places vertices from UVs
                samuraiTrail.Clear();
            }
            // A plane just in front of the fighter, facing the camera; the shader draws in its XY.
            samuraiTrailRoot.SetPositionAndRotation(view.transform.position + view.transform.forward * (Vector3.Distance(view.transform.position, fighter.position) - 1.5f), view.transform.rotation);
            samuraiTrailClock += delta;
            if (cutting > .05f)
                samuraiTrail.Add(new SamuraiTrailSample
                {
                    // From mid-blade to the tip: the stroke reads as a crescent, not a filled fan.
                    guard = samuraiTrailRoot.InverseTransformPoint(Vector3.Lerp(fighterRig.BladeGuard, fighterRig.BladeTip, .45f)),
                    tip = samuraiTrailRoot.InverseTransformPoint(fighterRig.BladeTip),
                    time = samuraiTrailClock
                });
            samuraiTrail.RemoveAll(sample => samuraiTrailClock - sample.time > SamuraiTrailLife);
            samuraiTrailVertices.Clear(); samuraiTrailUv.Clear(); samuraiTrailPlane.Clear(); samuraiTrailTriangles.Clear();
            for (int i = 0; i < samuraiTrail.Count; i++)
            {
                var sample = samuraiTrail[i];
                float age = (samuraiTrailClock - sample.time) / SamuraiTrailLife;
                samuraiTrailVertices.Add(sample.guard); samuraiTrailVertices.Add(sample.tip);
                samuraiTrailPlane.Add(sample.guard); samuraiTrailPlane.Add(sample.tip);
                samuraiTrailUv.Add(new Vector2(age, 0)); samuraiTrailUv.Add(new Vector2(age, 1));
                if (i == 0) continue;
                int a = i * 2 - 2;
                samuraiTrailTriangles.Add(a); samuraiTrailTriangles.Add(a + 1); samuraiTrailTriangles.Add(a + 2);
                samuraiTrailTriangles.Add(a + 1); samuraiTrailTriangles.Add(a + 3); samuraiTrailTriangles.Add(a + 2);
            }
            samuraiTrailMesh.Clear();
            samuraiTrailMesh.SetVertices(samuraiTrailVertices); samuraiTrailMesh.SetUVs(0, samuraiTrailUv); samuraiTrailMesh.SetUVs(1, samuraiTrailPlane);
            samuraiTrailMesh.SetTriangles(samuraiTrailTriangles, 0);
        }

        void DisposeSamuraiTrail()
        {
            if (samuraiTrailMesh != null) Destroy(samuraiTrailMesh);
            if (samuraiTrailMaterial != null) Destroy(samuraiTrailMaterial);
        }
    }
}
