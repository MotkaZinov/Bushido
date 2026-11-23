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
            Bar(BarFuse, new Rect(x, y, width, 11), fraction, pigment);
            if (fraction <= 0) return;
            Vector2 tip = new Vector2(x + width * fraction, y + 5.5f);
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

        // ---------------- Brush-and-paper HUD ----------------
        struct HudBar { public bool seen; public float last, ghostHi, ghostT, pulse; }
        readonly HudBar[] hudBars = new HudBar[9];
        const int BarBoss = 0, BarPosture = 1, BarTime = 2, BarStamina = 3, BarJutsu = 4, BarAccuracy = 5, BarFuse = 6;
        GUIStyle hudCap, hudNum, hudBanner, hudKey;
        int hudHits = -1, hudMisses = -1;
        float hudHitPop, hudMissPop, hudSplat, hudClock, ensoLast, ensoPulse, ensoFull;
        string noticeSeen = "";
        float noticeStart, noticeLast;
        bool noticeStamp;
        Vector2 noticeSize;

        static Color Ink(float alpha) => new Color(PrototypeArt.Ink.r, PrototypeArt.Ink.g, PrototypeArt.Ink.b, alpha);

        // Uniform scale inside the safe area, pinned to a corner (ax, ay in 0..1) so HUD groups keep their shape on any aspect.
        void Anchor(float ax, float ay)
        {
            Rect safe = Screen.safeArea;
            float s = Mathf.Min(safe.width / 1280f, safe.height / 720f);
            GUI.matrix = Matrix4x4.TRS(new Vector3(safe.x + (safe.width - 1280f * s) * ax,
                Screen.height - safe.yMax + (safe.height - 720f * s) * ay, 0), Quaternion.identity, new Vector3(s, s, 1));
        }

        void PrepareHudStyles()
        {
            hudCap = new GUIStyle(titleLogoStyle) { fontSize = 13, wordWrap = false, alignment = TextAnchor.MiddleLeft };
            hudCap.normal.textColor = PrototypeArt.Ink;
            hudNum = new GUIStyle(hudCap) { fontSize = 30 };
            hudBanner = new GUIStyle(hudCap) { fontSize = 26, alignment = TextAnchor.MiddleCenter };
            hudBanner.normal.textColor = PrototypeArt.Paper;
            hudKey = new GUIStyle(hudCap) { fontSize = 15, alignment = TextAnchor.MiddleCenter };
        }

        void ResetHud() { System.Array.Clear(hudBars, 0, hudBars.Length); hudHits = hudMisses = -1; hudHitPop = hudMissPop = hudSplat = 0; ensoLast = 0; }

        // One brush stroke from a to b. Only the part u0..u1 is shown; the rest of the stroke stays implied.
        void Stroke(Vector2 a, Vector2 b, float thick, Color color, bool dry = false, float u0 = 0f, float u1 = 1f)
        {
            if (u1 <= u0) return;
            Matrix4x4 m = GUI.matrix; Color c = GUI.color;
            float length = Vector2.Distance(a, b);
            GUI.matrix = m * Matrix4x4.TRS(a, Quaternion.Euler(0, 0, Mathf.Atan2(b.y - a.y, b.x - a.x) * Mathf.Rad2Deg), Vector3.one);
            GUI.color = c * color;
            GUI.DrawTextureWithTexCoords(new Rect(length * u0, -thick * .5f, length * (u1 - u0), thick), PrototypeArt.Brush(dry), new Rect(u0, 0, u1 - u0, 1));
            GUI.matrix = m; GUI.color = c;
        }

        // GUIUtility.*AroundPivot reads its pivot in screen space and ignores GUI.matrix, so anchored groups would drift; this works in local space.
        void Pivot(Vector2 p, float degrees, float scale = 1f) =>
            GUI.matrix *= Matrix4x4.TRS(p, Quaternion.Euler(0, 0, degrees), new Vector3(scale, scale, 1)) * Matrix4x4.TRS(-p, Quaternion.identity, Vector3.one);

        void Blot(Vector2 center, Vector2 size, Color color, float rotation = 0f, Texture2D texture = null)
        {
            Matrix4x4 m = GUI.matrix; Color c = GUI.color;
            Pivot(center, rotation);
            GUI.color = c * color;
            GUI.DrawTexture(new Rect(center.x - size.x * .5f, center.y - size.y * .5f, size.x, size.y), texture != null ? texture : PrototypeArt.InkBlob);
            GUI.matrix = m; GUI.color = c;
        }

        void Grain(Rect r, float alpha)
        {
            Color c = GUI.color;
            GUI.color = new Color(1, 1, 1, c.a * alpha);
            GUI.DrawTextureWithTexCoords(r, PrototypeArt.PaperGrain, new Rect(0, 0, r.width / 128f, r.height / 128f));
            GUI.color = c;
        }

        // Four hand-drawn lines that overshoot at the corners, like a box drawn in one brush each.
        void BrushBorder(Rect r, float thick, Color color)
        {
            Stroke(new Vector2(r.x - 3, r.y), new Vector2(r.xMax + 3, r.y), thick, color);
            Stroke(new Vector2(r.xMax, r.y - 3), new Vector2(r.xMax, r.yMax + 3), thick, color, true);
            Stroke(new Vector2(r.xMax + 3, r.yMax), new Vector2(r.x - 3, r.yMax), thick, color);
            Stroke(new Vector2(r.x, r.yMax + 3), new Vector2(r.x, r.y - 3), thick, color, true);
        }

        void PaperCard(Rect r)
        {
            Panel(new Rect(r.x + 5, r.y + 6, r.width, r.height), Ink(.25f)); // guide: displaced paper shadow, ink at 25%
            Panel(r, PrototypeArt.Paper);
            Grain(r, .55f);
            BrushBorder(r, 5f, PrototypeArt.Ink);
        }

        // Brush-stroke bar: wet ink blot at the tip pulses when the value moves; what was lost lingers lighter for 0.4 s.
        void Bar(int slot, Rect rect, float fraction, Color color)
        {
            fraction = Mathf.Clamp01(fraction);
            ref HudBar s = ref hudBars[slot];
            if (!s.seen) { s.seen = true; s.last = fraction; }
            if (Event.current.type == EventType.Repaint)
            {
                float dt = Mathf.Min(Time.unscaledDeltaTime, .05f);
                if (s.last - fraction > .004f) { s.ghostHi = s.ghostT > 0 ? Mathf.Max(s.ghostHi, s.last) : s.last; s.ghostT = .4f; }
                if (Mathf.Abs(fraction - s.last) > .004f) s.pulse = .35f;
                s.last = fraction; s.ghostT -= dt; s.pulse -= dt;
            }
            Vector2 a = new Vector2(rect.x, rect.center.y), b = new Vector2(rect.xMax, rect.center.y);
            float thick = rect.height * 1.35f; // the texture's ragged edges eat into its height
            Stroke(a, b, thick, Ink(.32f), true);
            if (s.ghostT > 0 && s.ghostHi > fraction)
                Stroke(a, b, thick, new Color(color.r, color.g, color.b, .45f * s.ghostT / .4f), false, fraction, s.ghostHi);
            Stroke(a, b, thick, color, false, 0f, fraction);
            if (fraction > .02f)
                Blot(new Vector2(rect.x + rect.width * fraction - 1, a.y), Vector2.one * (rect.height * 1.05f + Mathf.Max(0, s.pulse) * 16f),
                    Color.Lerp(color, PrototypeArt.Ink, .35f), fraction * 400f);
        }

        void Caption(Rect r, string text, Color color, TextAnchor anchor = TextAnchor.MiddleLeft)
        {
            hudCap.alignment = anchor; hudCap.normal.textColor = color;
            GUI.Label(r, text, hudCap);
        }

        void DrawFrogMark(Vector2 c, Color ink)
        {
            Blot(c + new Vector2(0, 3), new Vector2(23, 15), ink);
            for (int side = -1; side <= 1; side += 2)
            {
                Blot(c + new Vector2(side * 7, -4), Vector2.one * 9, ink);
                Blot(c + new Vector2(side * 7, -4), Vector2.one * 3, PrototypeArt.Paper);
            }
        }

        void DrawCount(Rect r, int value, float pop, Color color)
        {
            Matrix4x4 m = GUI.matrix;
            Pivot(new Vector2(r.x + 14, r.center.y), 0, 1f + .25f * Mathf.Clamp01(pop / .2f)); // pops 1.25 -> 1 in 0.2 s
            hudNum.normal.textColor = color;
            GUI.Label(r, value.ToString(), hudNum);
            GUI.matrix = m;
        }

        void HangingTag(Rect tag)
        {
            Vector2 apex = new Vector2(640, -30);
            Pivot(apex, Mathf.Sin(Time.unscaledTime * 1.3f) * .6f); // the tag sways on its cord
            Stroke(apex, new Vector2(tag.x + 46, tag.y + 12), 4, PrototypeArt.Ink);
            Stroke(apex, new Vector2(tag.xMax - 46, tag.y + 12), 4, PrototypeArt.Ink);
            PaperCard(tag);
            Blot(new Vector2(tag.x + 46, tag.y + 12), Vector2.one * 11, PrototypeArt.Ink);
            Blot(new Vector2(tag.xMax - 46, tag.y + 12), Vector2.one * 11, PrototypeArt.Ink);
        }

        // Hanging tag, top centre: time, accuracy against the rank-B notch, and the two counters.
        void DrawSenderoTag()
        {
            int total = hits + misses;
            float accuracy = total > 0 ? (float)hits / total : 1f;
            bool below = total > 0 && accuracy < CampaignProgress.RankBAccuracy;
            if (Event.current.type == EventType.Repaint)
            {
                float dt = Mathf.Min(Time.unscaledDeltaTime, .05f);
                if (hits < hudHits || misses < hudMisses) hudHits = hudMisses = -1; // a new battle
                if (hudHits >= 0 && hits != hudHits) hudHitPop = .2f;
                if (hudMisses >= 0 && misses != hudMisses) { hudMissPop = .2f; if (misses > hudMisses) hudSplat = .6f; }
                hudHits = hits; hudMisses = misses;
                hudHitPop -= dt; hudMissPop -= dt; hudSplat -= dt;
            }
            Rect tag = new Rect(430, 10, 420, 124);
            Matrix4x4 m = GUI.matrix;
            HangingTag(tag);
            float barX = tag.x + 108, barW = tag.width - 108 - 22;
            Caption(new Rect(tag.x + 20, tag.y + 12, 84, 22), "Tiempo", PrototypeArt.Ink);
            Bar(BarTime, new Rect(barX, tag.y + 16, barW, 13), 1 - battleTime / 35f, PrototypeArt.Ink);
            if (mission == 2)
            {
                Caption(new Rect(tag.x + 20, tag.y + 34, 84, 22), "Meta", PrototypeArt.Ink);
                Bar(BarAccuracy, new Rect(barX, tag.y + 38, barW, 13), Mathf.Clamp01(arcade.Points / (float)FestivalScore.Target), PrototypeArt.Gold);
            }
            else
            {
                Caption(new Rect(tag.x + 20, tag.y + 34, 84, 22), "Puntería", PrototypeArt.Ink);
                Bar(BarAccuracy, new Rect(barX, tag.y + 38, barW, 13), accuracy, below ? PrototypeArt.Red : PrototypeArt.Gold);
                float notch = barX + barW * CampaignProgress.RankBAccuracy;
                Stroke(new Vector2(notch, tag.y + 33), new Vector2(notch, tag.y + 56), 5, PrototypeArt.Ink, true);
                Caption(new Rect(notch - 10, tag.y + 54, 20, 13), "B", PrototypeArt.Ink, TextAnchor.MiddleCenter);
            }
            Caption(new Rect(tag.x + 16, tag.y + 96, 195, 24), mission == 2 ?
                $"Puntos  {arcade.Points} / {FestivalScore.Target}" : $"Puntos  {arcade.Points}", PrototypeArt.Ink);
            Caption(new Rect(tag.x + 210, tag.y + 96, 195, 24), arcade.RushLeft > 0 ?
                $"Frenesí ×2   {Mathf.CeilToInt(arcade.RushLeft)} s" : $"Frenesí   {arcade.Petals} / 3", PrototypeArt.Red);
            Caption(new Rect(tag.x + 50, tag.y + 69, 100, 22), "Purificadas", PrototypeArt.Ink);
            DrawCount(new Rect(tag.x + 152, tag.y + 64, 60, 32), hits, hudHitPop, PrototypeArt.Ink);
            Caption(new Rect(tag.x + 232, tag.y + 69, 100, 22), "Escapadas", PrototypeArt.Red);
            if (hudSplat > 0) Blot(new Vector2(tag.x + 358, tag.y + 80), Vector2.one * 74, new Color(PrototypeArt.Red.r, PrototypeArt.Red.g, PrototypeArt.Red.b, hudSplat / .6f * .7f), 20, PrototypeArt.InkSplat);
            DrawCount(new Rect(tag.x + 334, tag.y + 64, 60, 32), misses, hudMissPop, PrototypeArt.Red);
            GUI.matrix = m;
        }

        void DrawBossTag()
        {
            Rect tag = new Rect(430, 10, 420, 74);
            Matrix4x4 m = GUI.matrix;
            HangingTag(tag);
            float barX = tag.x + 108, barW = tag.width - 108 - 22;
            Caption(new Rect(tag.x + 20, tag.y + 12, 84, 22), "Jefe", PrototypeArt.Red);
            Bar(BarBoss, new Rect(barX, tag.y + 16, barW, 13), (float)bossHealth / bossMaxHealth, PrototypeArt.Red);
            Caption(new Rect(tag.x + 20, tag.y + 38, 84, 22), vulnerable ? "Contra" : "Postura", PrototypeArt.Ink);
            Bar(BarPosture, new Rect(barX, tag.y + 42, barW, 13), vulnerable ? counterTime / 10f : 1f - (float)guard / guardLimit,
                Color.Lerp(PrototypeArt.Gold, PrototypeArt.Ink, postureFlash / 0.24f));
            GUI.matrix = m;
        }

        // Ensō: ring of brush segments, clockwise from the lower left, thick where the brush lands and thin where it lifts.
        void DrawEnsoArc(Vector2 c, float radius, float sweep, float thick, Color color, bool dry)
        {
            const float Total = 300f, Start = 225f;
            Texture2D tex = PrototypeArt.Brush(dry);
            Matrix4x4 m = GUI.matrix; Color gc = GUI.color;
            int count = Mathf.CeilToInt(sweep / 5f);
            GUI.color = gc * color;
            for (int i = 0; i < count; i++)
            {
                float deg = Start + Mathf.Min(sweep, (i + .5f) * 5f), f = (deg - Start) / Total;
                float rad = deg * Mathf.Deg2Rad, h = thick * (1.25f - .7f * f) * (1f + .08f * Mathf.Sin(i * 2.7f));
                Vector2 p = c + new Vector2(Mathf.Sin(rad), -Mathf.Cos(rad)) * radius;
                GUI.matrix = m * Matrix4x4.TRS(p, Quaternion.Euler(0, 0, deg), Vector3.one);
                float u = .2f + (i * 37 % 10) / 10f * .5f, len = radius * .087f * 1.7f;
                GUI.DrawTextureWithTexCoords(new Rect(-len * .5f, -h * .5f, len, h), tex, new Rect(u, 0, .25f, 1));
            }
            GUI.matrix = m; GUI.color = gc;
        }

        // Judgment ensō (bottom left) doubles as the tap target; stamina is a brush bar beside it.
        void DrawJudgmentMeter()
        {
            if (mode != Mode.Battle) return;
            bool full = judgmentCharge >= 1f;
            if (Event.current.type == EventType.Repaint)
            {
                float dt = Mathf.Min(Time.unscaledDeltaTime, .05f);
                if (judgmentCharge > ensoLast + .001f) ensoPulse = .3f;
                ensoLast = judgmentCharge; ensoPulse -= dt;
                ensoFull = Mathf.MoveTowards(ensoFull, full ? 1f : 0f, dt * 4f);
            }
            const float K = 1.2f; // the signature technique gets a ~130 px disc and a ~105 px ring
            Vector2 c = new Vector2(84, 636);
            float time = Time.unscaledTime;
            Matrix4x4 m = GUI.matrix;
            Pivot(c, 0, K * (full ? 1f + .04f * Mathf.Sin(time * 5f) : 1f) * (1f + Mathf.Max(0, ensoPulse) / .3f * .09f));
            Blot(c + new Vector2(4, 5), Vector2.one * 118, Ink(.3f));
            Blot(c, Vector2.one * 118, PrototypeArt.Ink, 30); // inked rim: the paper disc must read against any backdrop
            Blot(c, Vector2.one * 111, PrototypeArt.Paper, 30);
            Blot(c, Vector2.one * 111, new Color(PrototypeArt.Ink.r, PrototypeArt.Ink.g, PrototypeArt.Ink.b, ensoFull), 30);
            Grain(new Rect(c.x - 44, c.y - 44, 88, 88), .5f * (1f - ensoFull));
            Color ring = Color.Lerp(PrototypeArt.Ink, JutsuInk, ensoFull);
            DrawEnsoArc(c, 38, 300f, 10, Ink(.28f * (1f - ensoFull) + .2f * ensoFull), true);
            float sweep = 300f * Mathf.Clamp01(judgmentCharge);
            DrawEnsoArc(c, 38, sweep, 11, ring, false);
            if (sweep > 8f && !full)
            {
                float rad = (225f + sweep) * Mathf.Deg2Rad;
                Blot(c + new Vector2(Mathf.Sin(rad), -Mathf.Cos(rad)) * 38f, Vector2.one * (13f + Mathf.Max(0, ensoPulse) * 24f), PrototypeArt.Gold, sweep * 3f);
            }
            hudKey.normal.textColor = Color.Lerp(Ink(.85f), PrototypeArt.Paper, ensoFull);
            GUI.Label(new Rect(c.x - 40, c.y - 10, 80, 20), "JUICIO", hudKey); // the J key still fires it; the label only names the move
            GUI.matrix = m;
            if (full)
                for (int i = 0; i < 4; i++)
                {
                    float phase = Mathf.Repeat(time * .7f + i * .25f, 1f);
                    Blot(c + new Vector2((i - 1.5f) * 15f + Mathf.Sin(time * 3f + i) * 5f, -48f * K - phase * 46f),
                        Vector2.one * (15f * (1f - phase * .5f)), new Color(JutsuInk.r, JutsuInk.g, JutsuInk.b, Mathf.Sin(phase * Mathf.PI) * .8f));
                }
            Caption(new Rect(168, 628, 130, 20), "Aguante", PrototypeArt.Ink);
            Bar(BarStamina, new Rect(168, 652, 130, 13), stamina / MaxStamina, guarding ? PrototypeArt.Gold : stamina <= 25 ? PrototypeArt.Red : PrototypeArt.Jade);
            if (full && !paused && GUI.Button(new Rect(c.x - 67, c.y - 67, 134, 134), GUIContent.none, GUIStyle.none)) judgmentTap = true;
        }

        // Battle notices (hints and popups) as a brush banner: hints slide in, short all-caps popups stamp down.
        void DrawNotice()
        {
            if (noticeTime <= 0 || string.IsNullOrEmpty(notice)) return;
            if (notice != noticeSeen || noticeTime > noticeLast + .05f)
            {
                noticeSeen = notice; noticeStart = Time.unscaledTime;
                noticeStamp = notice.Length <= 18 && notice == notice.ToUpperInvariant();
                hudBanner.fontSize = noticeStamp ? 22 : 20;
                noticeSize = hudBanner.CalcSize(new GUIContent(notice));
            }
            noticeLast = noticeTime;
            float age = Time.unscaledTime - noticeStart, fade = Mathf.Clamp01(noticeTime / .3f);
            // Short stamps live in the empty left column, hints in a slim banner above the launch marks: the centre stays clear.
            float w = noticeSize.x + (noticeStamp ? 66 : 80), h = noticeStamp ? 46 : 40;
            Vector2 c = noticeStamp ? new Vector2(195, 245) : new Vector2(640, 586);
            Anchor(noticeStamp ? 0f : .5f, noticeStamp ? 0f : 1f);
            Matrix4x4 m = GUI.matrix; Color gc = GUI.color;
            GUI.color = new Color(1, 1, 1, fade);
            if (noticeStamp)
            {
                float k = Mathf.Clamp01(age / .14f);
                Pivot(c, -4f, Mathf.Lerp(1.8f, 1f, k * k));
                if (age > .14f) Blot(c, new Vector2(w * 1.15f, h * 2.4f), Ink(Mathf.Clamp01(1f - (age - .14f) / .25f) * .5f), 0, PrototypeArt.InkSplat);
            }
            else c.x += -Mathf.Pow(1f - Mathf.Clamp01(age / .25f), 3f) * (w + 400f);
            Stroke(c + new Vector2(-w * .5f, 3), c + new Vector2(w * .5f, 3), h, Ink(.28f), true);
            Stroke(c + new Vector2(-w * .5f, 0), c + new Vector2(w * .5f, 0), h, PrototypeArt.Ink);
            hudBanner.fontSize = noticeStamp ? 22 : 20;
            GUI.Label(new Rect(c.x - w * .5f, c.y - h * .5f, w, h), notice, hudBanner);
            GUI.matrix = m; GUI.color = gc;
        }
    }
}

