using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Bushido
{
    // All motion is sampled from combat time. No autonomous Update, global shader
    // state, particle GameObjects, or allocations on the per-frame path.
    public sealed class KatanaSpiritVfx : MonoBehaviour
    {
        const int SmokeCount = 40, SparkCount = 40, FogCount = 36, RibbonPoints = 64;
        sealed class Billboards
        {
            public Mesh mesh;
            public MeshRenderer renderer;
            public Material material;
            public Vector3[] vertices;
            public Vector2[] uv;
            public Color[] colors;
        }
        Camera sceneCamera;
        UniversalAdditionalCameraData cameraData;
        bool previousPost, previousHdr, ownsCamera;
        Volume volume;
        VolumeProfile profile;
        Billboards smoke, sparks, fallingFog;
        Transform halo;
        Material haloMaterial, ribbonMaterial;
        readonly LineRenderer[] ribbons = new LineRenderer[3];
        public float SmokeStrength { get; private set; }
        public Vector3 EmissionOrigin { get; private set; }
        public float SampleTime { get; private set; }
        public bool PresentationVisible { get; private set; }

        static Material MakeMaterial(string resource)
        {
            var shader = Resources.Load<Shader>(resource);
            if (shader == null) throw new System.InvalidOperationException("Falta shader: " + resource);
            return new Material(shader) { name = resource + " / remate" };
        }

        public void Initialize(Camera camera)
        {
            sceneCamera = camera;
            cameraData = camera.GetUniversalAdditionalCameraData();
            smoke = CreateBillboards("Vapor espiritual", SmokeCount, false);
            sparks = CreateBillboards("Ascuas del espíritu", SparkCount, true);
            fallingFog = CreateBillboards("Niebla que desborda la funda", FogCount, false);
            fallingFog.material.renderQueue = 3010;
            halo = GameObject.CreatePrimitive(PrimitiveType.Quad).transform;
            halo.name = "Resplandor de la abertura";
            halo.SetParent(transform, false);
            Destroy(halo.GetComponent<Collider>());
            haloMaterial = MakeMaterial("BushidoKatanaAura");
            halo.GetComponent<Renderer>().sharedMaterial = haloMaterial;
            ribbonMaterial = MakeMaterial("BushidoKatanaFilament");
            for (int i = 0; i < ribbons.Length; i++)
            {
                var line = new GameObject("Espiral de energía " + i).AddComponent<LineRenderer>();
                line.transform.SetParent(transform, false);
                line.useWorldSpace = true;
                line.positionCount = RibbonPoints;
                line.numCornerVertices = 4;
                line.numCapVertices = 4;
                line.sharedMaterial = ribbonMaterial;
                line.shadowCastingMode = ShadowCastingMode.Off;
                line.receiveShadows = false;
                line.widthCurve = new AnimationCurve(new Keyframe(0, 0), new Keyframe(.3f, 1), new Keyframe(.7f, .6f), new Keyframe(1, 0));
                ribbons[i] = line;
            }
            profile = ScriptableObject.CreateInstance<VolumeProfile>();
            profile.name = "Atmósfera temporal del desenfunde";
            var bloom = profile.Add<Bloom>(true);
            bloom.threshold.Override(1.05f); bloom.intensity.Override(.7f);
            bloom.scatter.Override(.65f); bloom.clamp.Override(6);
            bloom.downscale.Override(BloomDownscaleMode.Quarter); bloom.maxIterations.Override(4);
            var vignette = profile.Add<Vignette>(true);
            vignette.color.Override(new Color(.025f, .055f, .09f));
            vignette.center.Override(new Vector2(.53f, .44f));
            vignette.intensity.Override(.24f); vignette.smoothness.Override(.7f);
            var grade = profile.Add<ColorAdjustments>(true);
            grade.postExposure.Override(-.18f); grade.contrast.Override(7); grade.saturation.Override(-6);
            volume = gameObject.AddComponent<Volume>();
            volume.isGlobal = true; volume.priority = 30; volume.sharedProfile = profile; volume.weight = 0;
            HidePresentation();
        }

        Billboards CreateBillboards(string name, int count, bool embers)
        {
            var child = new GameObject(name); child.transform.SetParent(transform, false);
            var b = new Billboards
            {
                mesh = new Mesh { name = name },
                material = MakeMaterial("BushidoKatanaVapor"),
                vertices = new Vector3[count * 4],
                uv = new Vector2[count * 4],
                colors = new Color[count * 4],
                renderer = child.AddComponent<MeshRenderer>()
            };
            var triangles = new int[count * 6];
            for (int i = 0; i < count; i++)
            {
                int v = i * 4, t = i * 6;
                b.uv[v] = Vector2.zero; b.uv[v + 1] = Vector2.right; b.uv[v + 2] = Vector2.one; b.uv[v + 3] = Vector2.up;
                triangles[t] = v; triangles[t + 1] = v + 2; triangles[t + 2] = v + 1;
                triangles[t + 3] = v; triangles[t + 4] = v + 3; triangles[t + 5] = v + 2;
            }
            b.mesh.MarkDynamic(); b.mesh.vertices = b.vertices; b.mesh.uv = b.uv;
            b.mesh.colors = b.colors; b.mesh.triangles = triangles;
            child.AddComponent<MeshFilter>().sharedMesh = b.mesh;
            b.material.SetFloat("_Ember", embers ? 1 : 0);
            b.material.renderQueue = embers ? 3012 : 3008;
            b.renderer.sharedMaterial = b.material;
            b.renderer.shadowCastingMode = ShadowCastingMode.Off; b.renderer.receiveShadows = false;
            return b;
        }

        static void Quad(Billboards b, int i, Vector3 center, Vector3 right, Vector3 up, Color color)
        {
            int v = i * 4;
            b.vertices[v] = center - right - up; b.vertices[v + 1] = center + right - up;
            b.vertices[v + 2] = center + right + up; b.vertices[v + 3] = center - right + up;
            b.colors[v] = b.colors[v + 1] = b.colors[v + 2] = b.colors[v + 3] = color;
        }
        static void Upload(Billboards b, float age)
        {
            b.mesh.vertices = b.vertices; b.mesh.colors = b.colors; b.mesh.RecalculateBounds();
            b.material.SetFloat("_Age", age);
        }

        public void Sample(float age, Vector3 mouth, Vector3 guard, Vector3 axis, float scale, float opening, float charge)
        {
            if (!ownsCamera)
            {
                previousPost = cameraData.renderPostProcessing; previousHdr = sceneCamera.allowHDR;
                cameraData.renderPostProcessing = true; sceneCamera.allowHDR = true; ownsCamera = true;
            }
            SampleTime = age; EmissionOrigin = mouth; PresentationVisible = true;
            float enter = Mathf.SmoothStep(0, 1, age / .85f);
            float pulse = Mathf.Exp(-Mathf.Pow((age - 1.25f) / .13f, 2));
            // The saya contains the spirit: only the exposed blade releases power.
            float power = Mathf.Pow(Mathf.Clamp01(opening / .72f), .7f);
            SmokeStrength = Mathf.SmoothStep(0, 1, (age - 1.15f) / .6f) * power;
            volume.weight = enter * Mathf.Lerp(.04f, 1, power);
            Vector3 right = sceneCamera.transform.right, up = sceneCamera.transform.up, forward = sceneCamera.transform.forward;
            Vector3 side = Vector3.Cross(axis, forward).normalized;
            halo.gameObject.SetActive(true);
            halo.position = mouth - forward * .26f; halo.rotation = sceneCamera.transform.rotation;
            halo.localScale = Vector3.one * scale * (.4f + power * .7f + pulse * power * .2f);
            haloMaterial.SetFloat("_Age", age);
            haloMaterial.SetFloat("_Power", enter * power * (1.3f + pulse * .25f));

            smoke.renderer.enabled = SmokeStrength > 0;
            for (int i = 0; i < SmokeCount; i++)
            {
                float born = age - 1.15f - i * .067f;
                float life = 2.2f;
                float u = born < 0 ? 0 : Mathf.Repeat(born / life, 1);
                float seed = i * 2.39996f;
                float radius = Mathf.Lerp(.055f, .23f, u) * Mathf.Lerp(.6f, 1.12f, power);
                float drift = Mathf.Sin(seed + u * 4.8f) * (.025f + .12f * u) + Mathf.Sin(age * .9f + u * 5) * u * .09f;
                Vector3 p = mouth + scale * Mathf.Lerp(.6f, 1, power) * (right * drift + up * (u * .88f) + axis * (u * .055f)) - forward * (.12f + i * .001f);
                float angle = seed + u * .8f;
                Vector3 r = (right * Mathf.Cos(angle) + up * Mathf.Sin(angle)) * radius * scale;
                Vector3 v = (-right * Mathf.Sin(angle) + up * Mathf.Cos(angle)) * radius * scale * 1.35f;
                float fade = Mathf.SmoothStep(0, 1, u / .12f) * (1 - Mathf.SmoothStep(.45f, 1, u));
                Color color = Color.Lerp(new Color(.09f, .39f, .64f), new Color(.23f, .36f, .56f), u);
                color.a = born < 0 ? 0 : fade * SmokeStrength * (1.25f + charge * .15f);
                Quad(smoke, i, p, r, v, color);
            }
            Upload(smoke, age);

            // Cold mist spills from inside the opening toward the viewer's feet.
            // Its lifetime and screen-edge fade dissolve it before it leaves the view.
            fallingFog.renderer.enabled = SmokeStrength > 0;
            float mouthDepth = sceneCamera.WorldToViewportPoint(mouth).z;
            Vector3 bottom = sceneCamera.ViewportToWorldPoint(new Vector3(.5f, -.12f, mouthDepth));
            float fallDistance = Mathf.Max(scale * .3f, Vector3.Dot(mouth - bottom, up));
            for (int i = 0; i < FogCount; i++)
            {
                float born = age - 1.15f - i * .047f;
                float u = born < 0 ? 0 : Mathf.Repeat(born / 2.8f, 1);
                float seed = i * 2.39996f;
                float drift = Mathf.Sin(seed + u * 4) * u * .2f + Mathf.Sin(age * .7f + seed) * u * .055f;
                Vector3 p = mouth - up * (fallDistance * Mathf.Pow(u, 1.08f)) + right * (drift * scale) - forward * (.16f + u * .65f);
                float radius = scale * Mathf.Lerp(.045f, .32f, u);
                float fade = Mathf.SmoothStep(0, 1, u / .08f) * (1 - Mathf.SmoothStep(0, 1, (u - .65f) / .35f));
                fade *= Mathf.SmoothStep(0, 1, (sceneCamera.WorldToViewportPoint(p).y + .08f) / .2f);
                Color color = Color.Lerp(new Color(.15f, .46f, .61f), new Color(.36f, .65f, .73f), u);
                color.a = born < 0 ? 0 : fade * SmokeStrength * (.6f + charge * .15f);
                Quad(fallingFog, i, p, right * radius, up * radius * 1.4f, color);
            }
            Upload(fallingFog, age);

            sparks.renderer.enabled = SmokeStrength > 0;
            for (int i = 0; i < SparkCount; i++)
            {
                float born = age - 1.2f - i * .09f;
                float u = born < 0 ? 0 : Mathf.Repeat(born / 2.15f, 1);
                float seed = i * 2.39996f;
                Vector3 p = mouth + scale * (side * Mathf.Sin(seed + u * 5) * (.03f + u * .28f) + up * (u * .65f) + axis * u * .15f) - forward * .29f;
                float size = scale * (i % 4 == 0 ? .010f : .005f) * (1 + charge * .3f);
                Color color = i % 5 == 0 ? new Color(1.9f, 1.05f, .32f) : new Color(.3f, 1.4f, 2.6f);
                color.a = born < 0 ? 0 : Mathf.Sin(u * Mathf.PI) * SmokeStrength * (.35f + .4f * Mathf.Pow(.5f + .5f * Mathf.Sin(age * 6 + seed), 2));
                Quad(sparks, i, p, right * size, up * size * 1.5f, color);
            }
            Upload(sparks, age);
            for (int i = 0; i < ribbons.Length; i++)
            {
                var line = ribbons[i]; line.enabled = SmokeStrength > 0;
                line.widthMultiplier = scale * (.019f + charge * .012f) * SmokeStrength;
                for (int k = 0; k < RibbonPoints; k++)
                {
                    float u = k / (float)(RibbonPoints - 1);
                    float angle = u * Mathf.PI * 2.6f - age * 1.8f + i * Mathf.PI;
                    float radius = (.075f + .025f * Mathf.Sin(u * Mathf.PI)) * scale;
                    Vector3 p = Vector3.Lerp(mouth, guard + axis * .08f * scale, u);
                    p += side * Mathf.Cos(angle) * radius + up * Mathf.Sin(angle) * radius * .28f;
                    p -= forward * (.21f + Mathf.Sin(angle) * .06f);
                    line.SetPosition(k, p);
                }
            }
            ribbonMaterial.SetFloat("_Age", age);
            ribbonMaterial.SetFloat("_Power", SmokeStrength * (1.05f + charge * .85f));
        }

        public void HidePresentation()
        {
            PresentationVisible = false; SmokeStrength = 0;
            if (smoke != null) smoke.renderer.enabled = false;
            if (fallingFog != null) fallingFog.renderer.enabled = false;
            if (sparks != null) sparks.renderer.enabled = false;
            if (halo != null) halo.gameObject.SetActive(false);
            foreach (var line in ribbons) if (line != null) line.enabled = false;
            if (volume != null) volume.weight = 0;
            RestoreCamera();
        }

        // A brief burst travels with the existing ink cut, then disappears. The
        // white-paper finisher remains readable; no long full-screen flash.
        public void SampleRelease(float age, Vector3 center, Vector3 direction, float scale)
        {
            HidePresentation();
            if (age < 0 || age > .48f) return;
            sparks.renderer.enabled = true;
            Vector3 right = sceneCamera.transform.right, up = sceneCamera.transform.up;
            Vector3 side = Vector3.Cross(direction, sceneCamera.transform.forward).normalized;
            float fade = 1 - Mathf.SmoothStep(.08f, .48f, age);
            for (int i = 0; i < SparkCount; i++)
            {
                float spread = (i / (float)(SparkCount - 1) - .5f) * 2;
                Vector3 p = center + direction * spread * scale * (.35f + age * 2.8f) + side * Mathf.Sin(i * 2.4f) * age * scale * .6f;
                float size = scale * (.004f + (i % 3) * .002f);
                Color color = i % 5 == 0 ? new Color(1, .61f, .18f, fade) : new Color(.08f, .47f, .85f, fade);
                Quad(sparks, i, p, right * size, up * size * 2, color);
            }
            Upload(sparks, age);
        }

        void RestoreCamera()
        {
            if (!ownsCamera) return;
            if (cameraData != null) cameraData.renderPostProcessing = previousPost;
            if (sceneCamera != null) sceneCamera.allowHDR = previousHdr;
            ownsCamera = false;
        }
        void OnDisable() { RestoreCamera(); }
        void OnDestroy()
        {
            RestoreCamera();
            if (smoke != null) { Destroy(smoke.mesh); Destroy(smoke.material); }
            if (fallingFog != null) { Destroy(fallingFog.mesh); Destroy(fallingFog.material); }
            if (sparks != null) { Destroy(sparks.mesh); Destroy(sparks.material); }
            if (haloMaterial != null) Destroy(haloMaterial);
            if (ribbonMaterial != null) Destroy(ribbonMaterial);
            if (profile != null) { foreach (var effect in profile.components) Destroy(effect); Destroy(profile); }
        }
    }
}
