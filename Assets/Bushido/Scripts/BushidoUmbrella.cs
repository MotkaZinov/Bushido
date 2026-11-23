using UnityEngine;

namespace Bushido
{
    public sealed partial class BushidoPrototype
    {
        static readonly string[,] EncounterDialogue = {
            { "", "", "", "" },
            { "", "", "", "" },
            { "", "", "", "" },
            { "Farol blanco", "Oye, tú. Si ves a mi hermano, no le hagas caso: siempre me apaga cuando hablo.", "Farol rojo", "¡Mentira! Se apaga solito. Acércate y te lo explico a lengüetazos." },
            { "Sombrilla", "Me olvidaron junto al pozo hace cien años. Desde entonces no me cierro por nadie.", "Sombrilla", "¿Vienes a cerrarme tú? Primero tendrás que pillarme." }
        };

        static readonly string[] UmbrellaSealLines = {
            "¡Eh! Eso era mi varilla favorita.",
            "Me tiembla el pie... Antes bailaba así en las bodas.",
            "Vale, vale. Ya paró de llover."
        };

        // Barrido, caída y vuelo quebrado. The cue and the actor share the same score.
        static readonly int[,] UmbrellaSteps = { {4, 0, 4, 5}, {1, 5, 2, 4}, {4, 2, 5, 3} };

        Transform posedUmbrella, footJoint, eyeJoint;
        Quaternion footRest;
        Vector3 eyeRest;
        int eyeLidAxis;
        Transform[] tongueJoints = new Transform[0];
        Quaternion[] tongueRest = new Quaternion[0];
        readonly Transform[] canopyPanels = new Transform[2];
        readonly Quaternion[] canopyRest = new Quaternion[2];

        void BindUmbrellaParts()
        {
            posedUmbrella = opponent;
            footJoint = eyeJoint = null;
            tongueJoints = new Transform[0];
            System.Array.Clear(canopyPanels, 0, canopyPanels.Length);
            foreach (var part in opponent.GetComponentsInChildren<Transform>(true))
            {
                int panel = part.name == "Cono" ? 0 : part.name == "Cono.001" ? 1 : -1;
                if (panel < 0) continue;
                canopyPanels[panel] = part;
                canopyRest[panel] = part.localRotation;
            }
            // Cube is the weighted leg mesh. Pick the lowest joint on its own branch
            // so bending the foot cannot rotate the canopy inherited from the rig.
            foreach (var skin in opponent.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (skin.name != "Cube" && (skin.sharedMesh == null || skin.sharedMesh.name != "Cube")) continue;
                foreach (var joint in skin.bones)
                {
                    if (joint == null || (canopyPanels[0] != null && canopyPanels[0].IsChildOf(joint))) continue;
                    if (footJoint == null || joint.position.y < footJoint.position.y) footJoint = joint;
                }
            }
            if (footJoint != null) footRest = footJoint.localRotation;
            // "Cubo" is the tongue on a long bone chain; "Esfera.001" is the eye on a bone of its own.
            foreach (var skin in opponent.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (skin.name == "Cubo") tongueJoints = System.Array.FindAll(skin.bones, bone => bone != null);
                else if (skin.name == "Esfera.001" && skin.bones.Length > 0) eyeJoint = skin.bones[0];
            }
            tongueRest = System.Array.ConvertAll(tongueJoints, bone => bone.localRotation);
            if (eyeJoint == null) return;
            eyeRest = eyeJoint.localScale;
            // Squash the eye along whichever local axis points up on screen.
            Vector3 up = opponent.up;
            float x = Mathf.Abs(Vector3.Dot(eyeJoint.right, up)), y = Mathf.Abs(Vector3.Dot(eyeJoint.up, up)), z = Mathf.Abs(Vector3.Dot(eyeJoint.forward, up));
            eyeLidAxis = x > y && x > z ? 0 : y > z ? 1 : 2;
        }

        // Kasa-obake life: the eye blinks (and pinches during a wind-up), the tongue whips like a ribbon.
        void PoseUmbrellaFace(float wind, float hop)
        {
            if (eyeJoint != null)
            {
                float cycle = Mathf.Repeat(battleTime + .4f, 2.9f);
                float blink = cycle < .14f ? Mathf.Abs(cycle - .07f) / .07f : 1f;
                float lid = Mathf.Min(blink, 1f - wind * .55f);
                if (bossReaction == BossReaction.Deflected) lid = Mathf.Min(lid, .2f);
                if (bossFinalStage > 0) lid = Mathf.Min(lid, .35f);
                Vector3 scale = eyeRest;
                scale[eyeLidAxis] *= Mathf.Lerp(.08f, 1f, lid);
                eyeJoint.localScale = scale;
            }
            if (tongueJoints.Length == 0) return;
            // A wave travels down the chain; it lashes harder while winding up and flops on landing.
            float amplitude = 3.2f + wind * 6f + (1f - hop) * 2.5f;
            if (bossFinalStage > 0) amplitude = 1.2f;
            Vector3 axis = opponent.forward;
            for (int i = 0; i < tongueJoints.Length; i++)
            {
                var bone = tongueJoints[i];
                float along = i / (float)tongueJoints.Length;
                float angle = Mathf.Sin(battleTime * 7.5f - along * 9f) * amplitude * (.3f + along);
                bone.localRotation = Quaternion.AngleAxis(angle, bone.parent.InverseTransformDirection(axis)) * tongueRest[i];
            }
        }

        void PoseUmbrellaParts(float wind, float cross, float reach, float side, float land)
        {
            if (posedUmbrella != opponent) BindUmbrellaParts();
            float step = Mathf.Sin(battleTime * 5f);
            float kick = DuelSymbol == 5 ? 46f : 29f;
            // The ankle folds on each landing and springs back for the next hop.
            float ankle = step * 6f + land * 24f + side * reach * Mathf.Lerp(-kick, kick, cross);
            float flutter = Mathf.Sin(battleTime * 2.7f - .8f) * 5f;
            float canopyTurn = flutter + side * reach * Mathf.Lerp(18, -25, cross);
            if (DuelSymbol == 2 || DuelSymbol == 3) canopyTurn += side * 360f * Mathf.SmoothStep(0, 1, wind);
            float canopyTilt = -step * 3f + wind * 12f;
            if (bossFinalStage > 0)
            {
                ankle = -28f;
                canopyTurn = flutter * .3f;
                canopyTilt = bossFinalStage == 2 ? Mathf.Lerp(8, 42, Mathf.Clamp01(bossFinalTime / .65f)) : 8f;
            }
            if (footJoint != null)
                footJoint.localRotation = Quaternion.AngleAxis(ankle, footJoint.parent.InverseTransformDirection(opponent.right)) * footRest;
            for (int i = 0; i < canopyPanels.Length; i++)
            {
                var panel = canopyPanels[i];
                if (panel == null) continue;
                panel.localRotation = Quaternion.AngleAxis(canopyTurn, panel.parent.InverseTransformDirection(opponent.up))
                    * Quaternion.AngleAxis(canopyTilt, panel.parent.InverseTransformDirection(opponent.forward)) * canopyRest[i];
            }
        }

        void UpdateUmbrella(float delta)
        {
            bossReactionTime += delta;
            float wind = duelPhase == DuelPhase.Telegraph ? Mathf.Clamp01(duelClock / DuelStrikeDelay) : 0f;
            bool recovery = duelPhase == DuelPhase.Recovery;
            float release = recovery ? Mathf.Clamp01(duelClock / .38f) : 0f;
            float cross = recovery ? Mathf.SmoothStep(0, 1, Mathf.Min(1, duelClock / .12f)) : 0f;
            float reach = recovery ? (1f - Mathf.SmoothStep(0, 1, release)) : Mathf.SmoothStep(0, 1, wind);
            float side = duelBeat % 2 == 0 ? 1 : -1;
            // Kasa-obake hop: up on the single leg, a beat in the air, squash on landing.
            float hopPhase = Mathf.Repeat(battleTime * 1.6f, 1f);
            float air = Mathf.Sin(Mathf.Clamp01(hopPhase / .62f) * Mathf.PI);
            float land = hopPhase > .62f ? Mathf.Sin(Mathf.InverseLerp(.62f, 1f, hopPhase) * Mathf.PI) : 0f;
            float calm = 1f - Mathf.Max(reach, wind) * .7f;
            float bob = air * .42f * calm;
            Vector3 offset = Vector3.zero;
            float roll = 0, spread = 1;
            switch (DuelSymbol)
            {
                case 4: // Pull sideways, then sweep across the stage.
                    offset.x = side * reach * Mathf.Lerp(-1.1f, 1.8f, cross);
                    roll = side * reach * Mathf.Lerp(36, -48, cross);
                    break;
                case 5: // Gather the canopy overhead and fall tip first.
                    offset.y = reach * Mathf.Lerp(1.25f, -.65f, cross);
                    spread = 1f - reach * .38f;
                    break;
                default: // A diagonal bank becomes a turning leap on the compound gestures.
                    offset = new Vector3(side * reach * Mathf.Lerp(-.7f, 1, cross), reach * .55f, 0);
                    roll = side * reach * Mathf.Lerp(-26, 44, cross);
                    break;
            }
            if (bossReaction == BossReaction.Deflected || bossReaction == BossReaction.Seal)
            {
                float duration = bossReaction == BossReaction.Seal ? .85f : .38f;
                float t = Mathf.Clamp01(bossReactionTime / duration);
                roll += Mathf.Sin(t * Mathf.PI * 3) * (1 - t) * 23;
                if (t >= 1f) bossReaction = BossReaction.None;
            }
            if (bossFinalStage > 0)
            {
                offset = Vector3.zero;
                spread = bossFinalStage == 1 ? .82f : Mathf.Lerp(.82f, .2f, Mathf.Clamp01(bossFinalTime / .8f));
                roll = bossFinalStage == 1 ? -8f : -8f - 65f * Mathf.Clamp01(bossFinalTime / .8f);
            }
            float squash = bossFinalStage > 0 ? 0f : land * .16f * calm - air * .05f * calm - wind * .1f;
            roll += Mathf.Sin(battleTime * 1.6f * Mathf.PI * 2f) * 4f * calm * (bossFinalStage > 0 ? 0 : 1);
            opponent.position = new Vector3(0, 2.3f + bob, 1f) + offset;
            opponent.rotation = Quaternion.Euler(0, 0, roll);
            opponent.localScale = new Vector3(spread * (1 + squash), 1 - squash, spread * (1 + squash));
            PoseUmbrellaParts(wind, cross, reach, side, land * calm);
            PoseUmbrellaFace(wind, air);
        }
    }
}
