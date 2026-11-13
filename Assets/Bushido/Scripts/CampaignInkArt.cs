using UnityEngine;

namespace Bushido
{
    // Cached UI masks. White RGB preserves tinting; pigment lives in alpha.
    static class CampaignInkArt
    {
        static float Grain(int x, int y, int seed)
        {
            unchecked
            {
                uint h = (uint)(x * 374761393 + y * 668265263 + seed * 1274126177);
                h = (h ^ (h >> 13)) * 1274126177;
                return (h & 65535) / 65535f;
            }
        }

        static Texture2D Finish(string name, int width, int height, Color32[] pixels)
        {
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
            { name = name, wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            return texture;
        }

        public static Texture2D Seal(bool shogi)
        {
            const int size = 192;
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float px = (x + .5f) / size * 2 - 1, py = (y + .5f) / size * 2 - 1;
                float angle = Mathf.Atan2(py, px);
                float distance;
                if (shogi)
                {
                    // Five-sided travelling piece with a pointed crown.
                    float halfWidth = Mathf.Lerp(.72f, .56f, (py + .86f) / 1.4f);
                    distance = Mathf.Min(halfWidth - Mathf.Abs(px), Mathf.Min(py + .86f, .90f - py - Mathf.Abs(px) * .63f));
                }
                else distance = .89f + .012f * Mathf.Sin(angle * 13) + .009f * Mathf.Sin(angle * 29) - Mathf.Sqrt(px * px + py * py);
                float alpha = Mathf.Clamp01(distance * 70 + (Grain(x, y, 3) - .5f) * .65f);
                // Subtle paper fibres, with a solid enough silhouette for small screens.
                alpha *= .94f + Grain(x, y / 2, 8) * .06f;
                pixels[y * size + x] = new Color32(255, 255, 255, (byte)(alpha * 255));
            }
            return Finish(shogi ? "Ficha de papel recortado" : "Sello de tinta", size, size, pixels);
        }

        public static Texture2D Road(int from, out Rect bounds)
        {
            Vector2 a = CampaignBoardLayout.Positions[from], b = CampaignBoardLayout.Positions[from + 1];
            bounds = Rect.MinMaxRect(Mathf.Min(a.x, b.x) - 15, Mathf.Min(a.y, b.y) - 45,
                Mathf.Max(a.x, b.x) + 15, Mathf.Max(a.y, b.y) + 45);
            const float resolution = 2;
            int width = Mathf.CeilToInt(bounds.width * resolution), height = Mathf.CeilToInt(bounds.height * resolution);
            var pixels = new Color32[width * height];
            int samples = Mathf.CeilToInt(Vector2.Distance(a, b) * 2);
            for (int sample = 0; sample <= samples; sample++)
            {
                float t = sample / (float)samples;
                Vector2 point = (CampaignBoardLayout.Point(from, from + 1, t) - bounds.position) * resolution;
                float pressure = 2.4f + 1.2f * Mathf.Sin(t * Mathf.PI) + .5f * Mathf.Sin(t * 18 + from);
                float radius = pressure * resolution;
                int extent = Mathf.CeilToInt(radius + 2);
                for (int y = Mathf.Max(0, (int)point.y - extent); y <= Mathf.Min(height - 1, (int)point.y + extent); y++)
                for (int x = Mathf.Max(0, (int)point.x - extent); x <= Mathf.Min(width - 1, (int)point.x + extent); x++)
                {
                    float distance = Vector2.Distance(new Vector2(x + .5f, y + .5f), point);
                    float noise = Grain(x / 2, y / 2, from + 17);
                    float alpha = Mathf.Clamp01(radius - distance + (noise - .5f) * 2.8f);
                    alpha *= .82f + .18f * noise;
                    // Tiny unpainted pores and dry fibres, not extra line objects.
                    if (Grain(x / 3, y, 33) > .975f) alpha *= .22f;
                    int index = (height - 1 - y) * width + x;
                    byte opacity = (byte)(alpha * 255);
                    if (opacity > pixels[index].a) pixels[index] = new Color32(255, 255, 255, opacity);
                }
            }
            return Finish("Camino a pincel " + from, width, height, pixels);
        }
    }
}
