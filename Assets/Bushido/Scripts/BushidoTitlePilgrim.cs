using System.Collections.Generic;
using UnityEngine;

namespace Bushido
{
    public sealed partial class BushidoPrototype
    {
        const int RobeWidth = 18, RobeLength = 16;
        Transform pilgrim;
        Mesh pilgrimRobe;
        Vector3[] robeVertices;
        float pilgrimStart;
        Transform leftThigh, rightThigh, leftKnee, rightKnee;
        Quaternion leftThighRest, rightThighRest, leftKneeRest, rightKneeRest;

        void BuildTitlePilgrim()
        {
            pilgrim = new GameObject("Samurái del sendero").transform;
            pilgrim.SetParent(world, false);
            CreateSamuraiModel(pilgrim, 1.78f);
            SamuraiRig.Bind(pilgrim)?.Pose(Vector2.zero, 0, 0, 0, 0, 0, 0);
            Transform[] joints = pilgrim.GetComponentsInChildren<Transform>();
            leftThigh = System.Array.Find(joints, joint => joint.name == "Pierna1.L");
            rightThigh = System.Array.Find(joints, joint => joint.name == "Pierna1.R");
            leftKnee = System.Array.Find(joints, joint => joint.name == "Pierna2.L");
            rightKnee = System.Array.Find(joints, joint => joint.name == "Pierna2.R");
            if (leftThigh != null) leftThighRest = leftThigh.localRotation;
            if (rightThigh != null) rightThighRest = rightThigh.localRotation;
            if (leftKnee != null) leftKneeRest = leftKnee.localRotation;
            if (rightKnee != null) rightKneeRest = rightKnee.localRotation;

            robeVertices = new Vector3[(RobeWidth + 1) * (RobeLength + 1)];
            var uv = new Vector2[robeVertices.Length];
            var triangles = new List<int>();
            for (int row = 0; row <= RobeLength; row++)
            for (int col = 0; col <= RobeWidth; col++)
            {
                int index = row * (RobeWidth + 1) + col;
                uv[index] = new Vector2(col / (float)RobeWidth, row / (float)RobeLength);
                if (row == 0 || col == 0) continue;
                int above = index - RobeWidth - 1;
                if (row == RobeLength && (col == 3 || col == 14)) continue;
                triangles.Add(above - 1); triangles.Add(index); triangles.Add(index - 1);
                triangles.Add(above - 1); triangles.Add(above); triangles.Add(index);
            }

            pilgrimRobe = new Mesh { name = "Túnica deshilachada" };
            pilgrimRobe.MarkDynamic();
            pilgrimRobe.vertices = robeVertices;
            pilgrimRobe.uv = uv;
            pilgrimRobe.SetTriangles(triangles, 0);
            titleAssets.Add(pilgrimRobe);
            var cloth = new Material(art.Material(new Color(0.31f, 0.24f, 0.17f)));
            cloth.SetFloat("_ClothWear", 1f);
            titleAssets.Add(cloth);
            TitleMesh(pilgrim, "Túnica larga al viento", pilgrimRobe, cloth, Vector3.zero, Vector3.one);
            pilgrimStart = Time.unscaledTime;
        }

        void UpdateTitlePilgrim(float time, float gust)
        {
            if (pilgrim == null) return;
            float elapsed = time - pilgrimStart;
            const float walkTime = 10.5f;
            float progress = Mathf.Clamp01(elapsed / walkTime);
            float walking = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.87f, 1f, progress));
            float phase = elapsed * 5.4f;
            float stride = Mathf.Sin(phase) * walking;
            float z = Mathf.Lerp(2.0f, 7.65f, progress);
            float ground = Mathf.Lerp(StairHeightAt(z - 0.24f), StairHeightAt(z + 0.24f), 0.5f);
            pilgrim.localPosition = new Vector3(-0.18f + stride * 0.07f, ground + 0.035f + Mathf.Abs(stride) * 0.042f, z);
            pilgrim.localRotation = Quaternion.Euler(0f, 3.2f * stride, -stride * 1.7f);
            PosePilgrimLeg(leftThigh, leftThighRest, stride * 15f);
            PosePilgrimLeg(rightThigh, rightThighRest, -stride * 15f);
            PosePilgrimLeg(leftKnee, leftKneeRest, Mathf.Max(0f, -stride) * 17f);
            PosePilgrimLeg(rightKnee, rightKneeRest, Mathf.Max(0f, stride) * 17f);

            for (int row = 0; row <= RobeLength; row++)
            {
                float length = row / (float)RobeLength;
                float freedom = length * length;
                for (int col = 0; col <= RobeWidth; col++)
                {
                    float side = col / (float)RobeWidth * 2f - 1f;
                    float edge = Mathf.Abs(side);
                    float hem = Mathf.PerlinNoise(col * 0.91f, 13.2f) * 0.13f;
                    float width = 0.29f + length * 0.21f;
                    float ripple = Mathf.Sin(time * 3.2f - length * 4.4f + side * 6.5f);
                    float x = side * width + freedom * (0.19f * gust + 0.07f * ripple + 0.03f * stride);
                    float y = 1.43f - length * 1.22f - edge * 0.06f + freedom * (hem + 0.05f * Mathf.Abs(ripple));
                    float back = -0.27f - length * 0.13f + edge * edge * 0.29f
                        - freedom * (0.17f * (0.5f + 0.5f * gust) + 0.09f * ripple);
                    back += 0.023f * Mathf.Sin(side * 20f + length * 4f) * (0.35f + length);
                    robeVertices[row * (RobeWidth + 1) + col] = new Vector3(x, y, back);
                }
            }
            pilgrimRobe.vertices = robeVertices;
            pilgrimRobe.RecalculateNormals();
            pilgrimRobe.RecalculateBounds();
        }

        void PosePilgrimLeg(Transform joint, Quaternion rest, float angle)
        {
            if (joint == null) return;
            joint.localRotation = rest;
            joint.rotation = Quaternion.AngleAxis(angle, pilgrim.right) * joint.rotation;
        }
    }
}
