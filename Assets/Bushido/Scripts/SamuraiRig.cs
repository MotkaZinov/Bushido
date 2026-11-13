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
            bladeAxis[axis] = bounds.center[axis] < 0 ? -1 : 1;
            trialJoints = new[] { this.chest, swordArm, swordElbow, swordHand, freeArm, this.freeElbow, freeWrist };
            trialRest = new Quaternion[trialJoints.Length];
            proceduralPose = new Quaternion[trialJoints.Length];
            for (int i = 0; i < trialJoints.Length; i++)
                trialRest[i] = Quaternion.Inverse(root.rotation) * trialJoints[i].bone.rotation;
            var data = Resources.Load<TextAsset>("ThirdParty/Quaternius/SwordTrial");
            if (data != null) trial = JsonUtility.FromJson<SwordTrial>(data.text);
        }

        void PointJoint(Joint joint, Transform child, Vector3 direction)
        {
            joint.Aim(child.position - joint.bone.position, root.TransformDirection(direction));
        }

        public void Pose(Vector2 gesture, float swing, float flourish, float salute, float celebration, float guard, float recoil)
        {
            chest.Reset();
            swordArm.Reset();
            swordElbow.Reset();
            swordHand.Reset();
            freeArm.Reset();
            freeElbow.Reset();
            freeWrist.Reset();

            float twist = 24f * swing * gesture.x + 20f * flourish - 12f * guard;
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
            PointJoint(swordArm, swordElbow.bone, armDirection);
            PointJoint(swordElbow, swordHand.bone, forearmDirection);
            swordHand.Aim(blade.TransformDirection(bladeAxis), root.TransformDirection(swordDirection));

            float brace = Mathf.Max(guard, salute * 0.7f);
            Vector3 freeUpper = Vector3.Lerp(new Vector3(-0.35f, -0.93f, 0.15f), new Vector3(-0.16f, -0.28f, 0.95f), brace);
            Vector3 freeLower = Vector3.Lerp(new Vector3(-0.1f, -0.62f, 0.71f), new Vector3(0.68f, 0.67f, 0.26f), brace);
            PointJoint(freeArm, freeElbow.bone, Vector3.Lerp(freeUpper, new Vector3(-0.88f, 0.27f, 0.42f), celebration));
            PointJoint(freeElbow, freeHand, Vector3.Lerp(freeLower, new Vector3(0.12f, 0.96f, 0.26f), celebration));
        }

        public void PlaySwordTrial(bool cutting, float phase, float guard, float recoil)
        {
            if (trial == null) return;
            Motion motion = guard > 0f ? trial.block : cutting ? trial.sword : null;
            if (motion == null) return;
            float time = guard > 0f ? Mathf.Lerp(0f, 0.52f + recoil * 0.1f, guard) : phase;
            float blend = guard > 0f ? guard : Mathf.Min(Mathf.Clamp01(phase * 10f), Mathf.Clamp01((1f - phase) * 7f));
            for (int i = 0; i < trialJoints.Length; i++) proceduralPose[i] = trialJoints[i].bone.rotation;
            for (int i = 0; i < trialJoints.Length; i++)
            {
                Quaternion target = root.rotation * motion.Sample(time, i, trialJoints.Length) * trialRest[i];
                trialJoints[i].bone.rotation = Quaternion.Slerp(proceduralPose[i], target, blend);
            }
        }
    }
}
