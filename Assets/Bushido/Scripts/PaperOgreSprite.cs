using UnityEngine;

namespace Bushido
{
    // Two 4x4 atlases, one quad. No per-frame Sprite or material allocations.
    public sealed class PaperOgreSprite : MonoBehaviour
    {
        Texture2D combat, binding;
        Material ink;
        int lastFrame = -1;
        bool lastBinding;

        public void Initialize(Material material, Texture2D combatAtlas, Texture2D bindingAtlas)
        {
            ink = material; combat = combatAtlas; binding = bindingAtlas;
            Show(false, 0);
        }

        public void Show(bool bound, int frame)
        {
            if (ink == null) return;
            bound &= binding != null;
            frame = Mathf.Clamp(frame, 0, 15);
            if (lastFrame == frame && lastBinding == bound) return;
            lastFrame = frame; lastBinding = bound;
            Texture2D atlas = bound ? binding : combat;
            ink.SetTexture("_BaseMap", atlas);
            // Half-texel inset prevents neighboring cells from bleeding at the edges.
            Vector2 inset = new Vector2(.5f / atlas.width, .5f / atlas.height);
            ink.SetTextureScale("_BaseMap", new Vector2(.25f, .25f) - inset * 2);
            ink.SetTextureOffset("_BaseMap", new Vector2((frame % 4) * .25f, (3 - frame / 4) * .25f) + inset);
        }
    }
}
