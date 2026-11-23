using UnityEngine;

namespace Bushido
{
    // Marioneta de la chancla en vuelo. El modelo trae huesos para la suela, la tira, los brazos, las piernas y el ojo.
    // Se posan cada fotograma y la pose se hornea en las mallas visibles, así el corte (FrogSlicer) parte la pose actual.
    //   Subiendo: brazos arriba, piernas juntas.  Cima: pánico, brazos en molinillo y patadas de bicicleta.
    //   Cayendo: brazos abiertos, piernas pedaleando despacio.  Siempre: la suela culebrea, la tira ondea y el ojo parpadea.
    public sealed class SandalPuppet : MonoBehaviour
    {
        SkinnedMeshRenderer[] skins;
        MeshFilter[] pieces;
        Transform[] sole, strap, arms, legs;
        Quaternion[] soleRest, strapRest, armRest, legRest;
        Transform eye;
        Vector3 eyeRest;
        int eyeAxis = 1;
        float seed;

        public void Bind(SkinnedMeshRenderer[] skins, MeshFilter[] pieces)
        {
            this.skins = skins; this.pieces = pieces;
            seed = Random.value * 10f;
            foreach (var skin in skins)
            {
                // Bone names repeat across the model's sub-skeletons, so each part is read from its own skin.
                switch (skin.name)
                {
                    case "Cubo": sole = skin.bones; break;
                    case "Cubo.001": strap = skin.bones; break;
                    case "Cilindro.001": arms = Bases(skin.bones); break;
                    case "Cilindro.002": legs = Bases(skin.bones); break;
                    case "Esfera.001": if (skin.bones.Length > 0) eye = skin.bones[0]; break;
                }
            }
            soleRest = Rest(sole); strapRest = Rest(strap); armRest = Rest(arms); legRest = Rest(legs);
            if (eye != null)
            {
                eyeRest = eye.localScale;
                Vector3 up = transform.up;
                float x = Mathf.Abs(Vector3.Dot(eye.right, up)), y = Mathf.Abs(Vector3.Dot(eye.up, up)), z = Mathf.Abs(Vector3.Dot(eye.forward, up));
                eyeAxis = x > y && x > z ? 0 : y > z ? 1 : 2;
            }
        }

        // Limb roots: the left and right base bones of a limb skin, as [L, R].
        static Transform[] Bases(Transform[] bones)
        {
            var roots = System.Array.FindAll(bones, b => b != null && (b.name == "Hueso.L" || b.name == "Hueso.R"));
            System.Array.Sort(roots, (a, b) => string.CompareOrdinal(a.name, b.name));
            return roots.Length == 2 ? roots : new Transform[0];
        }

        static Quaternion[] Rest(Transform[] bones) =>
            bones == null ? new Quaternion[0] : System.Array.ConvertAll(bones, b => b != null ? b.localRotation : Quaternion.identity);

        static void Turn(Transform bone, Quaternion rest, float degrees, Vector3 worldAxis)
        {
            if (bone == null) return;
            bone.localRotation = Quaternion.AngleAxis(degrees, bone.parent.InverseTransformDirection(worldAxis)) * rest;
        }

        // age: seconds in the air; rise: vertical speed (positive going up); frantic: 0..1 extra energy.
        // Every limb turns about the camera axis, so the motion reads like a paper puppet on the screen plane.
        public void Animate(float age, float rise, float frantic, Vector3 screenAxis)
        {
            if (skins == null) return;
            float t = age + seed;
            float climb = Mathf.Clamp01(rise / 6f), fall = Mathf.Clamp01(-rise / 6f);
            float panic = Mathf.Clamp01(1f - Mathf.Abs(rise) / 3.5f) + frantic * .6f;
            if (sole != null)
                for (int i = 0; i < sole.Length; i++)
                    Turn(sole[i], soleRest[i], Mathf.Sin(t * (6f + panic * 6f) - i * .9f) * (5f + panic * 6f), screenAxis);
            if (strap != null)
                for (int i = 0; i < strap.Length; i++)
                    Turn(strap[i], strapRest[i], Mathf.Sin(t * 9f - i * .5f) * (2f + panic * 3f) * (i / (float)strap.Length), screenAxis);
            if (arms != null && arms.Length == 2)
            {
                float raise = climb * 55f + fall * 30f;
                float windmill = Mathf.Sin(t * 16f) * 45f * panic;
                float flap = Mathf.Sin(t * 7f) * 12f * (1f - panic);
                Turn(arms[0], armRest[0], raise + windmill + flap, screenAxis);
                Turn(arms[1], armRest[1], -(raise - windmill + flap), screenAxis);
            }
            if (legs != null && legs.Length == 2)
            {
                float speed = 5f + panic * 13f;
                float swing = (8f + panic * 26f + fall * 12f) * (1f - climb * .7f);
                float kick = Mathf.Sin(t * speed) * swing;
                Turn(legs[0], legRest[0], kick, screenAxis);
                Turn(legs[1], legRest[1], kick, screenAxis);
            }
            if (eye != null)
            {
                float cycle = Mathf.Repeat(t, 2.4f);
                float blink = cycle < .12f ? Mathf.Abs(cycle - .06f) / .06f : 1f;
                Vector3 scale = eyeRest * (1f + panic * .18f);
                scale[eyeAxis] = eyeRest[eyeAxis] * (1f + panic * .25f) * Mathf.Max(.1f, blink);
                eye.localScale = scale;
            }
            for (int i = 0; i < skins.Length; i++)
            {
                if (pieces[i] == null) continue;
                // Measured: once CreateFrog has scaled the model, an unscaled bake comes out multiplied by the
                // model's scale (2.55 at rest becomes 0.011 at scale 0.0043). The piece undoes that factor.
                skins[i].BakeMesh(pieces[i].sharedMesh, false);
                pieces[i].sharedMesh.RecalculateBounds();
                float model = transform.lossyScale.x;
                pieces[i].transform.localScale = Vector3.one / Mathf.Max(model, 1e-6f);
            }
        }
    }
}
