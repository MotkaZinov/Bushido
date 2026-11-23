using UnityEngine;

namespace Bushido
{
    // The duel's light: one ink-dark veil just in front of the backdrop and behind everything that plays, with ragged watercolour
    // pools of warm light around the lit sources (BushidoDusk.shader, up to 8). Gameplay objects are never under it, so they keep their contrast.
    public sealed partial class BushidoPrototype
    {
        Transform duskVeil;
        Material duskMat;
        readonly Vector4[] duskLights = new Vector4[8];     // x,y viewport; z radius in screen heights; w strength
        float blackoutAmt;                                  // 0..1, eased: the pools shrink to embers while the blackout lasts
        static readonly int LightsId = Shader.PropertyToID("_Lights"), DarkId = Shader.PropertyToID("_Dark"), WarmthId = Shader.PropertyToID("_Warmth"), AspectId = Shader.PropertyToID("_Aspect");

        void BeginDusk()
        {
            var quad = art.Shape(world, "Crepúsculo de tinta", PrimitiveType.Quad, Vector3.zero, Vector3.one, Color.white);
            duskMat = art.DuskMaterial(); quad.GetComponent<Renderer>().sharedMaterial = duskMat; duskVeil = quad.transform; blackoutAmt = 0f;
        }

        void Pool(int slot, Vector3 at, float radius)
        {
            Vector3 v = view.WorldToViewportPoint(at);
            duskLights[slot] = new Vector4(v.x, v.y, radius, radius > 0.001f ? 1f : 0f);
        }

        void ApplyDusk()
        {
            float target = LanternDusk * (twinPurified[0] ? 0.5f : 1f) * (twinPurified[1] ? 0.5f : 1f);
            if (twinPurified[0] && twinPurified[1]) target = 0f;
            duskNow = Mathf.MoveTowards(duskNow, target, Time.unscaledDeltaTime * 0.8f);
            blackoutAmt = Mathf.MoveTowards(blackoutAmt, duskBlackout, Time.unscaledDeltaTime * LanternBlackoutFade);
            if (duskVeil == null) return;
            float dark = Mathf.Max(duskNow * LanternDarkBase, blackoutAmt * LanternDarkBlackout);
            duskVeil.gameObject.SetActive(dark > 0.005f);
            if (dark <= 0.005f) return;
            float height = view.orthographicSize * 2f * 1.04f;   // a little oversize: the camera shakes
            duskVeil.SetPositionAndRotation(view.transform.position + view.transform.forward * 39.5f, view.transform.rotation);
            duskVeil.localScale = new Vector3(height * view.aspect, height, 1f);
            for (int i = 0; i < 2; i++)
                Pool(i, twins[i].Body, twins[i].holder.gameObject.activeSelf ? Mathf.Lerp(LanternPoolTwin * twins[i].Glow, LanternEmber, blackoutAmt) : 0f);
            for (int k = 0; k < 4; k++)
                Pool(2 + k, villageLanterns[k].position, lanternLit[k] > 0.01f ? Mathf.Lerp(LanternPoolSmall * lanternLit[k], LanternEmber * 0.7f, blackoutAmt) : 0f);
            Pool(6, relayFlame != null ? relayFlame.position : Vector3.zero, relayFlame != null ? LanternPoolFlame : 0f);
            duskLights[7] = Vector4.zero;
            duskMat.SetVectorArray(LightsId, duskLights);
            duskMat.SetFloat(DarkId, dark); duskMat.SetFloat(WarmthId, LanternWarmth * Mathf.Min(1f, dark / 0.1f)); duskMat.SetFloat(AspectId, view.aspect);
        }
    }
}
