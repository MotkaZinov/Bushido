using UnityEngine;

namespace Bushido
{
    // Poses driven by the joints exported in Samurai.fbx.
    sealed class SamuraiRig
    {
        [System.Serializable]
        sealed class Motion
        {
            public float duration;
            public Quaternion[] poses;

            public Quaternion Sample(float phase, int joint, int jointCount)
            {
                float frame = Mathf.Clamp01(phase) * (poses.Length / jointCount - 1);
                int first = Mathf.FloorToInt(frame);
                int second = Mathf.CeilToInt(frame);
                return Quaternion.Slerp(poses[first * jointCount + joint], poses[second * jointCount + joint], frame - first);
            }
        }

        [System.Serializable]
        sealed class SwordTrial
        {
            public Motion sword, block;
        }

        sealed class Joint
        {
            public readonly Transform bone;
            readonly Quaternion rest;

            public Joint(Transform bone)
            {
                this.bone = bone;
                rest = bone.localRotation;
            }

            public void Reset() => bone.localRotation = rest;

            public void Aim(Vector3 current, Vector3 target)
            {
                bone.rotation = Quaternion.FromToRotation(current, target) * bone.rotation;
            }
        }

        readonly Transform root;
        readonly Joint chest, swordArm, swordElbow, swordHand, freeArm, freeElbow, freeWrist;
        readonly Transform freeHand, blade;
        readonly Joint head, leftThigh, rightThigh, leftKnee, rightKnee;
        readonly Vector3 tipLocal, guardLocal;
        readonly Transform[] eyes = new Transform[2];
        readonly Vector3[] openEyes = new Vector3[2];
        public Vector3 BladeTip => blade.TransformPoint(tipLocal);
        public Vector3 BladeGuard => blade.TransformPoint(guardLocal);
        public Vector3 ChestPoint => chest.bone.position;
        readonly Vector3 bladeAxis;
        readonly Joint[] trialJoints;
        readonly Quaternion[] trialRest, proceduralPose;
        readonly SwordTrial trial;
        public float SwordDuration => trial?.sword.duration ?? 0.28f;

        public static SamuraiRig Bind(Transform root)
        {
            var bones = root.GetComponentsInChildren<Transform>();
            Transform Find(string name) => System.Array.Find(bones, bone => bone.name == name);
            Transform chest = Find("Columna3");
            Transform arm = Find("Brazo1.L"), elbow = Find("Brazo2.L"), hand = Find("Brazo3.L");
            Transform free = Find("Brazo1.R"), freeElbow = Find("Brazo2.R"), freeHand = Find("Brazo3.R");
            Transform blade = System.Array.Find(bones, bone => bone.name.StartsWith("Katana", System.StringComparison.Ordinal));
            if (chest == null || arm == null || elbow == null || hand == null || free == null || freeElbow == null || freeHand == null || blade == null)
                return null;
            return new SamuraiRig(root, chest, arm, elbow, hand, free, freeElbow, freeHand, blade);
        }

        SamuraiRig(Transform root, Transform chest, Transform arm, Transform elbow, Transform hand,
            Transform free, Transform freeElbow, Transform freeHand, Transform blade)
        {
            this.root = root;
            this.chest = new Joint(chest);
            swordArm = new Joint(arm);
            swordElbow = new Joint(elbow);
            swordHand = new Joint(hand);
            freeArm = new Joint(free);
            this.freeElbow = new Joint(freeElbow);
            this.freeHand = freeHand;
            freeWrist = new Joint(freeHand);
            this.blade = blade;
            var filter = blade.GetComponent<MeshFilter>();
            Bounds bounds = filter != null ? filter.sharedMesh.bounds : new Bounds(Vector3.forward, Vector3.forward);
            Vector3 size = bounds.size;
            int axis = size.x > size.y && size.x > size.z ? 0 : size.y > size.z ? 1 : 2;
            bladeAxis = Vector3.zero;
            bladeAxis[axis] = bounds.center[axis] < blade.InverseTransformPoint(hand.position)[axis] ? -1 : 1;
            tipLocal = bounds.center;
            tipLocal[axis] += bladeAxis[axis] * bounds.extents[axis];
            guardLocal = bounds.center;
            guardLocal[axis] -= bladeAxis[axis] * bounds.extents[axis] * .5f; // the guard sits about a quarter of the way up the katana
            var bones = root.GetComponentsInChildren<Transform>();
            Joint Limb(string name) {
                var bone = System.Array.Find(bones, candidate => candidate.name == name && candidate.GetComponent<Renderer>() == null);
                return bone == null ? null : new Joint(bone);
            }
            head = Limb("Cabeza");
            for (int i = 0; i < eyes.Length; i++)
            {
                string eyeName = i == 0 ? "Ojo.L" : "Ojo.R";
                eyes[i] = System.Array.Find(bones, part => part.name == eyeName);
                if (eyes[i] != null) openEyes[i] = eyes[i].localScale;
            }
            leftThigh = Limb("Pierna1.L"); rightThigh = Limb("Pierna1.R");
            leftKnee = Limb("Pierna2.L"); rightKnee = Limb("Pierna2.R");
            trialJoints = new[] { this.chest, swordArm, swordElbow, swordHand, freeArm, this.freeElbow, freeWrist };
            trialRest = new Quaternion[trialJoints.Length];
            proceduralPose = new Quaternion[trialJoints.Length];
            for (int i = 0; i < trialJoints.Length; i++)
                trialRest[i] = Quaternion.Inverse(root.rotation) * trialJoints[i].bone.rotation;
            var data = Resources.Load<TextAsset>("ThirdParty/Quaternius/SwordTrial");
            if (data != null) trial = JsonUtility.FromJson<SwordTrial>(data.text);
        }

        void Bend(Joint joint, float angle)
        {
            if (joint == null) return;
            joint.Reset();
            joint.bone.rotation = Quaternion.AngleAxis(angle, root.right) * joint.bone.rotation;
        }

        public void PoseExpression(float nod, float gaze, float lean, float knees, float step, float lift, float eyelid)
        {
            Bend(leftThigh, -knees * .5f - step);
            Bend(rightThigh, -knees * .5f + step * .45f);
            Bend(leftKnee, knees + step * .65f);
            Bend(rightKnee, knees);
            chest.bone.rotation = Quaternion.AngleAxis(lean, root.right) * chest.bone.rotation;
            Bend(head, nod);
            if (head != null) head.bone.rotation = Quaternion.AngleAxis(gaze, root.up) * head.bone.rotation;
            freeArm.bone.rotation = Quaternion.AngleAxis(-lift, root.right) * freeArm.bone.rotation;
            for (int i = 0; i < eyes.Length; i++)
                if (eyes[i] != null)
                    eyes[i].localScale = Vector3.Scale(openEyes[i], new Vector3(1, Mathf.Lerp(.55f, 1f, eyelid), 1)); // closed-line eyes: expressions only thin them
        }

        void PointJoint(Joint joint, Transform child, Vector3 direction)
        {
            joint.Aim(child.position - joint.bone.position, root.TransformDirection(direction));
        }

        public void Pose(Vector2 gesture, float swing, float flourish, float salute, float celebration, float guard, float recoil, float breath = 0f)
        {
            chest.Reset();
            swordArm.Reset();
            swordElbow.Reset();
            swordHand.Reset();
            freeArm.Reset();
            freeElbow.Reset();
            freeWrist.Reset();

            float twist = 10f * swing * gesture.x + 20f * flourish - 12f * guard;
            chest.bone.rotation = Quaternion.AngleAxis(twist, root.up) * chest.bone.rotation;
            chest.bone.rotation = Quaternion.AngleAxis(12f * salute - 10f * recoil - 14f * celebration, root.right) * chest.bone.rotation;

            Vector3 swordDirection = new Vector3(0.33f, 0.9f, 0.22f);
            Vector3 armDirection = new Vector3(0.42f, -0.93f, 0.19f);
            Vector3 forearmDirection = new Vector3(0.13f, 0.24f, 0.94f);
            armDirection = Vector3.Lerp(armDirection, new Vector3(gesture.x * 0.85f, gesture.y * 0.64f - 0.12f, 0.82f), swing);
            forearmDirection = Vector3.Lerp(forearmDirection, new Vector3(gesture.x, gesture.y * 0.85f, 0.52f), swing);
            swordDirection = Vector3.Lerp(swordDirection, new Vector3(gesture.x, gesture.y, 0.28f), swing);
            armDirection = Vector3.Lerp(armDirection, new Vector3(0.45f, 0.75f, 0.62f), celebration);
            forearmDirection = Vector3.Lerp(forearmDirection, new Vector3(-0.4f, 0.9f, 0.29f), celebration);
            swordDirection = Vector3.Lerp(swordDirection, new Vector3(-0.26f, 1f, 0.14f), celebration);
            armDirection = Vector3.Lerp(armDirection, new Vector3(0.12f, -0.2f, 0.97f), guard);
            forearmDirection = Vector3.Lerp(forearmDirection, new Vector3(-0.68f, 0.71f, 0.2f), guard);
            swordDirection = Vector3.Lerp(swordDirection, new Vector3(0.92f, 0.29f, 0.24f), guard);
            // Ready stance breathes: the tip sways on each breath, fading out as soon as he cuts or guards.
            float rest = (1f - swing) * (1f - guard) * (1f - celebration);
            swordDirection = Quaternion.AngleAxis(breath * 7f * rest, Vector3.forward) * swordDirection;
            armDirection += new Vector3(0, breath * .05f * rest, 0);
            PointJoint(swordArm, swordElbow.bone, armDirection);
            PointJoint(swordElbow, swordHand.bone, forearmDirection);
            swordHand.Aim(blade.TransformDirection(bladeAxis), root.TransformDirection(swordDirection));

            float brace = Mathf.Max(guard, salute * 0.7f);
            Vector3 freeUpper = Vector3.Lerp(new Vector3(-0.35f, -0.93f, 0.15f), new Vector3(-0.16f, -0.28f, 0.95f), brace);
            Vector3 freeLower = Vector3.Lerp(new Vector3(-0.1f, -0.62f, 0.71f), new Vector3(0.68f, 0.67f, 0.26f), brace);
            PointJoint(freeArm, freeElbow.bone, Vector3.Lerp(freeUpper, new Vector3(-0.88f, 0.27f, 0.42f), celebration));
            PointJoint(freeElbow, freeHand, Vector3.Lerp(freeLower, new Vector3(0.12f, 0.96f, 0.26f), celebration));
        }

        // Two-link IK follows the prop after the sampled animation, without stretching the arm.
        public void SupportHilt(float weight)
        {
            if (weight <= 0f) return;
            Transform shoulder = freeArm.bone, elbow = freeElbow.bone;
            float upper = Vector3.Distance(shoulder.position, elbow.position);
            float lower = Vector3.Distance(elbow.position, freeHand.position);
            if (Mathf.Min(upper, lower) < .001f) return;
            Vector3 target = swordHand.bone.position - blade.TransformDirection(bladeAxis) * (lower * .38f);
            Vector3 reach = target - shoulder.position;
            if (reach.sqrMagnitude < .00001f) return;
            Vector3 axis = reach.normalized;
            float distance = Mathf.Clamp(reach.magnitude, Mathf.Abs(upper - lower) + .0001f, upper + lower - .0001f);
            float along = (upper * upper - lower * lower + distance * distance) / (2f * distance);
            Vector3 bend = Vector3.ProjectOnPlane(elbow.position - shoulder.position, axis);
            if (bend.sqrMagnitude < .00001f) bend = Vector3.ProjectOnPlane(-root.up, axis);
            Vector3 aim = axis * along + bend.normalized * Mathf.Sqrt(Mathf.Max(0, upper * upper - along * along));
            Quaternion correction = Quaternion.FromToRotation(elbow.position - shoulder.position, aim);
            shoulder.rotation = Quaternion.Slerp(Quaternion.identity, correction, weight) * shoulder.rotation;
            correction = Quaternion.FromToRotation(freeHand.position - elbow.position,
                shoulder.position + axis * distance - elbow.position);
            elbow.rotation = Quaternion.Slerp(Quaternion.identity, correction, weight) * elbow.rotation;
        }

        public void PlaySwordTrial(bool cutting, float phase, float guard, float recoil)
        {
            if (trial == null) return;
            Motion motion = guard > 0f ? trial.block : cutting ? trial.sword : null;
            if (motion == null) return;
            float time = guard > 0f ? Mathf.Lerp(0f, 0.52f + recoil * 0.1f, guard) : phase;
            float blend = guard > 0f ? guard : Mathf.Min(Mathf.Clamp01(phase * 10f), Mathf.Clamp01((1f - phase) * 7f));
            if (cutting && guard <= 0f) blend *= .58f; // Retain the player's swipe direction under the sampled swing.
            for (int i = 0; i < trialJoints.Length; i++) proceduralPose[i] = trialJoints[i].bone.rotation;
            // The sampled cut twists the chest away from the camera on this rig: keep the chest procedural while cutting.
            for (int i = cutting && guard <= 0f ? 1 : 0; i < trialJoints.Length; i++)
            {
                Quaternion target = root.rotation * motion.Sample(time, i, trialJoints.Length) * trialRest[i];
                trialJoints[i].bone.rotation = Quaternion.Slerp(proceduralPose[i], target, blend);
            }
        }
    }
}
