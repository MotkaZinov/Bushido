using UnityEngine;
namespace Bushido
{
    public sealed partial class BushidoPrototype
    {
        Texture2D healthGourd;
        GUIStyle inkRankStyle;
        GUIStyle inkCaptionStyle;
        Texture2D rankSlash;

        void DrawPaperHealth()
        {
            if (healthGourd == null) healthGourd = Resources.Load<Texture2D>("UI/HealthGourd");
            Color previous = GUI.color;
            for (int i = 0; i < 3; i++)
            {
                bool full = i < lives;
                float pulse = full && hurtFlash > 0 ? Mathf.Sin(hurtFlash * 60) * 3 : 0;
                Rect icon = new Rect(25 + i * 58, 18 + pulse, 48, 68);
                GUI.color = full ? Color.white : new Color(0.30f, 0.27f, 0.22f, 0.3f);
                if (healthGourd != null) GUI.DrawTexture(icon, healthGourd, ScaleMode.ScaleToFit);
            }
            GUI.color = previous;
        }

        void DrawInkArc(Vector2 center, float radius, float fraction, Color ink, float width)
        {
            Matrix4x4 saved = GUI.matrix;
            int segments = Mathf.CeilToInt(Mathf.Clamp01(fraction) * 64);
            for (int i = 0; i < segments; i++)
            {
                GUI.matrix = saved;
                GUIUtility.RotateAroundPivot(-95 + i * 5.625f, center);
                float irregular = Mathf.Sin(i * 2.7f) * 1.1f;
                Panel(new Rect(center.x + radius + irregular, center.y, width, radius * 0.104f), ink);
            }
            GUI.matrix = saved;
        }

        int lastHudRank = -1;
        float rankBeganAt;
        bool hudRankPromoted;

        void DrawPaperCompanion()
        {
            int level = StyleLevel;
            if (lastHudRank != level || rankBeganAt > battleTime)
            {
                hudRankPromoted = level > lastHudRank && lastHudRank >= 0;
                lastHudRank = level;
                rankBeganAt = battleTime;
            }
            float age = Mathf.Max(0, battleTime - rankBeganAt);
            float burst = hudRankPromoted ? Mathf.Clamp01(1 - age / 0.65f) : 0;
            float bounce = Mathf.Sin(age * 24) * Mathf.Exp(-age * 7) * 0.22f;
            if (inkRankStyle == null)
            {
                inkRankStyle = new GUIStyle(rankStyle) { font = titleLogoStyle.font, fontSize = 108, fontStyle = FontStyle.BoldAndItalic, alignment = TextAnchor.MiddleCenter, wordWrap = false };
                inkCaptionStyle = new GUIStyle(smallStyle) { alignment = TextAnchor.MiddleCenter, fontSize = 14 };
            }
            inkRankStyle.fontSize = level >= 5 ? 76 : 108;
            Color accent = Color.Lerp(styleColors[level], PrototypeArt.Gold, 0.15f);
            // Every rank effect is clipped to this local HUD rectangle.
            GUI.BeginGroup(new Rect(990, 125, 270, 230));
            Vector2 center = new Vector2(130, 125);
            Matrix4x4 saved = GUI.matrix;
            Color savedColor = GUI.color;
            DrawRankInkWaves(center + Vector2.up * -44f, level, accent);
            GUI.matrix = saved;
            float breathing = Mathf.Sin(battleTime * (2 + level * 0.35f)) * (0.008f + level * 0.0015f);
            GUIUtility.ScaleAroundPivot(Vector2.one * (1 + bounce + comboPulse * (0.09f + level * 0.009f) + breathing), center);
            GUIUtility.RotateAroundPivot(-9 + Mathf.Sin(age * 21) * burst * 8 + Mathf.Sin(battleTime * 3) * 0.7f, center);
            Matrix4x4 rankMatrix = GUI.matrix;
            DrawRankWind(center, level, accent, burst);
            DrawRankSlash(center, level, accent, burst);
            if (burst > 0)
            {
                for (int i = 0; i < 8; i++)
                {
                    GUI.matrix = rankMatrix;
                    GUIUtility.RotateAroundPivot(i * 45 + age * 14, center);
                    Panel(new Rect(center.x + 56 + (1 - burst) * 9, center.y, 6 + burst * 8, 2), new Color(accent.r, accent.g, accent.b, burst * 0.8f));
                }
            }
            GUI.matrix = rankMatrix;
            Rect letter = new Rect(center.x - 123, center.y - 59.5f, 246, 135);
            if (burst > 0)
            {
                inkRankStyle.normal.textColor = new Color(accent.r, accent.g, accent.b, burst * 0.3f);
                Rect echo = letter; echo.x -= (1 - burst) * 32; echo.y += (1 - burst) * 9;
                GUI.Label(echo, styleLetters[level], inkRankStyle);
            }
            inkRankStyle.normal.textColor = PrototypeArt.Ink;
            Rect shadow = letter; shadow.position += new Vector2(4, 5);
            GUI.Label(shadow, styleLetters[level], inkRankStyle);
            inkRankStyle.normal.textColor = Color.Lerp(accent, Color.white, burst * 0.7f);
            GUI.Label(letter, styleLetters[level], inkRankStyle);
            GUI.matrix = saved;
            GUI.color = savedColor;
            inkCaptionStyle.normal.textColor = PrototypeArt.Paper;
            GUI.Label(new Rect(10, 194, 240, 25), styleNames[level], inkCaptionStyle);
            GUI.EndGroup();
            DrawComboFuse(accent);
            DrawIllustratedCompanion();
            Bar(new Rect(1060, 662, 120, 4), stamina / MaxStamina, guarding ? PrototypeArt.Gold : PrototypeArt.Jade);
        }

        Texture2D rankStain;
        Texture2D rankStainGhost;

        Texture2D MakeInkStain(float seed)
        {
            const int size = 192;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            texture.name = "Mancha de tinta " + seed;
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.filterMode = FilterMode.Bilinear;
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float px = (x + 0.5f) / size * 2 - 1;
                    float py = (y + 0.5f) / size * 2 - 1;
                    float angle = Mathf.Atan2(py, px);
                    float radius = Mathf.Sqrt(px * px + py * py);
                    float lobes = Mathf.Sin(angle * 7 + seed) * 0.045f + Mathf.Sin(angle * 13 - seed * 2) * 0.025f;
                    float noise = Mathf.PerlinNoise(x * 0.15f + seed, y * 0.15f) * 0.045f;
                    float alpha = Mathf.Clamp01((0.82f + lobes + noise - radius) * 50);
                    float fiber = Mathf.PerlinNoise(x * 0.6f, y * 0.35f + seed);
                    pixels[y * size + x] = new Color32(8, 12, 9, (byte)(alpha * (0.94f + fiber * 0.06f) * 255));
                }
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            titleAssets.Add(texture);
            return texture;
        }

        void DrawRankInkWaves(Vector2 center, int level, Color accent)
        {
            if (rankStain == null) rankStain = MakeInkStain(3.1f);
            if (rankStainGhost == null) rankStainGhost = MakeInkStain(8.7f);
            Matrix4x4 saved = GUI.matrix;
            Color previous = GUI.color;
            float energy = level / 6f;
            float breath = Mathf.Sin(battleTime * (0.7f + energy)) * (1 + energy * 2);
            // A settled impression remains under the changing wet ink silhouette.
            GUI.color = new Color(1, 1, 1, 0.24f);
            GUI.DrawTexture(new Rect(center.x - 125, center.y - 110, 250, 220), rankStainGhost);
            GUI.color = Color.white;
            GUIUtility.RotateAroundPivot(Mathf.Sin(battleTime * 0.5f) * (1 + energy * 2), center);
            GUI.DrawTexture(new Rect(center.x - 122 - breath, center.y - 106, 244 + breath * 2, 212), rankStain);
            GUI.color = new Color(1, 1, 1, 0.5f + comboPulse * 0.3f);
            GUIUtility.RotateAroundPivot(-3 + Mathf.Sin(battleTime * 0.8f) * 3, center);
            GUI.DrawTexture(new Rect(center.x - 119, center.y - 104 - breath, 238, 208 + breath * 2), rankStainGhost);
            GUI.matrix = saved;
            GUI.color = previous;
        }
        void DrawRankSlash(Vector2 center, int level, Color accent, float burst)
        {
            if (rankSlash == null) rankSlash = Resources.Load<Texture2D>("UI/RankFX/Slash");
            if (rankSlash == null) return;
            Matrix4x4 saved = GUI.matrix;
            Color previous = GUI.color;
            float energy = level / 6f;
            int arcs = level >= 3 ? 1 : 0;
            for (int i = 0; i < arcs; i++)
            {
                GUI.matrix = saved;
                GUIUtility.RotateAroundPivot(battleTime * (18 + energy * 35) + i * 133 + comboPulse * 38, center);
                float size = 132 + energy * 18 + burst * 12;
                GUI.color = new Color(accent.r, accent.g, accent.b, 0.24f + energy * 0.24f + comboPulse * 0.25f);
                GUI.DrawTexture(new Rect(center.x - size / 2, center.y - size / 2, size, size), rankSlash);
                if (comboPulse > 0.1f)
                {
                    GUI.color = new Color(1, 0.96f, 0.82f, comboPulse * 0.5f);
                    GUI.DrawTexture(new Rect(center.x - size * 0.46f, center.y - size * 0.46f, size * 0.92f, size * 0.92f), rankSlash);
                }
            }
            GUI.matrix = saved;
            GUI.color = previous;
        }

        void DrawRankWind(Vector2 center, int level, Color accent, float promotion)
        {
            Matrix4x4 saved = GUI.matrix;
            float energy = level / 6f;
            // Curved wisps stay within the rank area; their speed rises with style.
            int wisps = 2 + level / 3;
            for (int i = 0; i < wisps; i++)
            {
                float phase = Mathf.Repeat(battleTime * (0.16f + energy * 0.32f) + i * 0.381f, 1);
                float fade = Mathf.Sin(phase * Mathf.PI);
                float angle = -24 + Mathf.Sin(i * 2.4f) * 30;
                GUI.matrix = saved;
                GUIUtility.RotateAroundPivot(angle, center);
                float startX = center.x - 65 + phase * 70;
                float baseY = center.y + (i % 2 == 0 ? -1 : 1) * (30 + i * 3);
                Color wind = Color.Lerp(PrototypeArt.Paper, accent, energy * 0.7f);
                wind.a = fade * (0.14f + energy * 0.25f + comboPulse * 0.12f);
                for (int piece = 0; piece < 6; piece++)
                {
                    float bend = Mathf.Sin(piece * 0.65f + phase * 3) * (3 + energy * 4);
                    Panel(new Rect(startX + piece * 8, baseY + bend, 9, 1 + energy), wind);
                }
            }
            // Each cut sends a brief slash across the edges of the rank lettering.
            if (comboPulse > 0 && combo > 0)
            {
                float progress = 1 - comboPulse;
                int cuts = 1 + level / 2;
                for (int i = 0; i < cuts; i++)
                {
                    GUI.matrix = saved;
                    GUIUtility.RotateAroundPivot(-28 + i * 22 + (combo % 2) * 12, center);
                    float y = center.y - 32 + i * 20;
                    float x = center.x - 65 + progress * 35;
                    Color trail = accent;
                    trail.a = comboPulse * (0.4f + energy * 0.35f);
                    Panel(new Rect(x - 15, y + 2, 75 + energy * 30, 3), new Color(trail.r, trail.g, trail.b, trail.a * 0.3f));
                    Panel(new Rect(x, y, 60 + energy * 28, 1.5f + promotion * 2), trail);
                    Panel(new Rect(x + 53 + energy * 28, y - 1, 7, 3), new Color(1, 0.95f, 0.78f, comboPulse * 0.8f));
                }
            }
            GUI.matrix = saved;
        }

        void DrawComboFuse(Color accent)
        {
            const float x = 1028, y = 358, width = 184;
            float fraction = Mathf.Clamp01(comboTime / 4.5f);
            float warning = fraction > 0 && fraction < 0.3f ? 1 - fraction / 0.3f : 0;
            float beat = 0.5f + 0.5f * Mathf.Sin(battleTime * (12 + warning * 14));
            Color pigment = Color.Lerp(accent, PrototypeArt.Red, warning * (0.45f + beat * 0.4f));
            // Uneven brush silhouette and visible spent paper make remaining time readable.
            for (int i = 0; i < 32; i++)
            {
                float offset = Mathf.Sin(i * 2.3f) * 1.4f;
                float height = 7 + Mathf.Sin(i * 1.7f) * 2;
                Rect segment = new Rect(x + i * width / 32, y + offset, width / 32 + 0.5f, height);
                Panel(new Rect(segment.x - 1, segment.y - 2, segment.width + 2, segment.height + 4), new Color(0.16f, 0.18f, 0.13f, 0.7f));
                Panel(segment, new Color(0.77f, 0.70f, 0.53f, 0.8f));
                float filled = Mathf.Clamp01(fraction * 32 - i);
                if (filled > 0)
                    Panel(new Rect(segment.x, segment.y, segment.width * filled, segment.height), pigment);
            }
            if (fraction <= 0) return;
            Vector2 tip = new Vector2(x + width * fraction, y + 4);
            float glow = 3 + comboPulse * 5 + warning * beat * 2;
            Matrix4x4 saved = GUI.matrix;
            GUIUtility.RotateAroundPivot(45, tip);
            Panel(new Rect(tip.x - glow, tip.y - glow, glow * 2, glow * 2), new Color(pigment.r, pigment.g, pigment.b, 0.25f));
            Panel(new Rect(tip.x - 2, tip.y - 2, 4, 4), PrototypeArt.Paper);
            GUI.matrix = saved;
            for (int i = 0; i < 4; i++)
            {
                float phase = Mathf.Repeat(battleTime * (0.8f + warning) + i * 0.25f, 1);
                float drift = Mathf.Sin(i * 3.7f) * 9;
                float size = 1 + (1 - phase) * (1 + comboPulse * 2);
                Panel(new Rect(tip.x + drift * phase, tip.y - 5 - phase * (12 + comboPulse * 13), size, size),
                    new Color(pigment.r, pigment.g, pigment.b, (1 - phase) * 0.8f));
            }
        }
    }
}

