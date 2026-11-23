using UnityEngine;
using UnityEngine.InputSystem;

namespace Bushido
{
    public sealed partial class BushidoPrototype
    {
        GUIStyle titleStyle;
        GUIStyle textStyle;
        GUIStyle smallStyle;
        GUIStyle buttonStyle;
        GUIStyle rankStyle;
        GUIStyle paperTextStyle;
        GUIStyle titleLogoStyle;
        GUIStyle titleButtonStyle;
        GUIStyle campaignButtonStyle;
        Texture2D techniqueMedallion;
        Texture2D techniqueEffectIcons;
        static readonly Color CampaignInk = PrototypeArt.InkSepia;
        static readonly Color CampaignRed = PrototypeArt.Red;
        static readonly Color CampaignPaper = PrototypeArt.Paper;
        Texture2D shojiPanel;
        Texture2D pausePaper;
        GUIStyle pauseHeading;
        GUIStyle tagLabel, inkTitleStyle, stampStyle, resultMessageStyle;
        float shojiTime = -1f;
        const float ShojiSlideDuration = 0.43f;
        const float ShojiRevealTime = 0.55f;
        bool showTechniques;

        void PrepareStyles()
        {
            if (titleStyle != null) return;
            titleStyle = new GUIStyle(GUI.skin.label) { fontSize = 30, fontStyle = FontStyle.Bold, wordWrap = true };
            textStyle = new GUIStyle(GUI.skin.label) { fontSize = 19, wordWrap = true };
            smallStyle = new GUIStyle(GUI.skin.label) { fontSize = 15, wordWrap = true };
            buttonStyle = new GUIStyle(GUI.skin.button) { fontSize = 18, wordWrap = true, padding = new RectOffset(10, 10, 8, 8) };
            rankStyle = new GUIStyle(titleStyle) { fontSize = 66, alignment = TextAnchor.MiddleCenter };
            titleStyle.normal.textColor = textStyle.normal.textColor = smallStyle.normal.textColor = rankStyle.normal.textColor = PrototypeArt.Paper;
            paperTextStyle = new GUIStyle(textStyle) { fontStyle = FontStyle.Bold };
            paperTextStyle.normal.textColor = PrototypeArt.Ink;
            Font cinzel = Resources.Load<Font>("ThirdParty/Cinzel/CinzelDecorative-Bold");
            titleLogoStyle = new GUIStyle(titleStyle)
            {
                font = cinzel != null ? cinzel : titleStyle.font,
                fontStyle = cinzel != null
                    ? FontStyle.Normal : FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                fontSize = 88,
                wordWrap = false
            };
            titleButtonStyle = new GUIStyle(titleLogoStyle) { fontSize = 22 };
            titleButtonStyle.normal.textColor = new Color(1f, 0.89f, 0.68f);
            titleButtonStyle.hover.textColor = titleButtonStyle.active.textColor = Color.white;
            campaignButtonStyle = new GUIStyle(titleButtonStyle) { fontSize = 18, wordWrap = true };
            PrepareHudStyles();
            tagLabel = new GUIStyle(titleButtonStyle) { fontSize = 18 };
            inkTitleStyle = new GUIStyle(titleLogoStyle) { fontSize = 34 };
            inkTitleStyle.normal.textColor = PrototypeArt.Ink;
            stampStyle = new GUIStyle(titleLogoStyle) { fontSize = 92 };
            resultMessageStyle = new GUIStyle(titleLogoStyle) { fontSize = 18, wordWrap = true, alignment = TextAnchor.UpperCenter };
            resultMessageStyle.normal.textColor = PrototypeArt.Ink;
        }

        // Launch cues stay at the lower edge so they cannot be mistaken for targets.
        void DrawWaveWarnings()
        {
            if (mode != Mode.Battle || (IsBoss && theaterState != 0)) return;
            foreach (var frog in pendingFrogs)
                if (frog.delay <= WaveLeadTime)
                    DrawLaunchMark(frog.x, frog.drift, frog.delay / WaveLeadTime, false);
            if (pendingBombDelay >= 0f && pendingBombDelay <= WaveLeadTime)
                DrawLaunchMark(pendingBombX, 0f, pendingBombDelay / WaveLeadTime, true);
        }

        void DrawLaunchMark(float x, float drift, float remaining, bool danger)
        {
            Vector3 screen = view.WorldToViewportPoint(new Vector3(x, -1.1f, 0f));
            Vector2 start = new Vector2(screen.x * 1280f, 652f);
            float progress = 1f - Mathf.Clamp01(remaining);
            Color ink = danger ? PrototypeArt.Red : PrototypeArt.Ink;
            ink.a = 0.35f + progress * 0.65f;
            Vector2 tip = start + new Vector2(drift * 18f, -20f - progress * 18f);
            DuelGuiLine(start, tip, ink, 3f + progress * 2f);
            if (danger)
            {
                DuelGuiLine(tip + new Vector2(-9, -9), tip + new Vector2(9, 9), ink, 4f);
                DuelGuiLine(tip + new Vector2(9, -9), tip + new Vector2(-9, 9), ink, 4f);
            }
            else
            {
                DuelGuiLine(tip + new Vector2(-6, 8), tip, ink, 2f);
                DuelGuiLine(tip, tip + new Vector2(6, 8), ink, 2f);
            }
        }

        void OnGUI()
        {
            if (view == null || progress == null) return;
            PrepareStyles();
            Matrix4x4 previousMatrix = GUI.matrix;
            GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(Screen.width / 1280f, Screen.height / 720f, 1));
            bool movingScroll = mode == Mode.ScrollOpening || mode == Mode.ScrollClosing;
            if (mode == Mode.Title)
            {
                float scale = Mathf.Min(Screen.width / 1280f, Screen.height / 720f);
                Vector3 offset = new Vector3((Screen.width - 1280f * scale) * 0.5f, (Screen.height - 720f * scale) * 0.5f, 0);
                GUI.matrix = Matrix4x4.TRS(offset, Quaternion.identity, new Vector3(scale, scale, 1));
                DrawTitleScreen();
            }
            else if (mode == Mode.Map || mode == Mode.Travel || mode == Mode.FaceOff || movingScroll)
            {
                Panel(new Rect(0, 0, 1280, 720), CampaignPaper);
                Rect safe = Screen.safeArea;
                float scale = Mathf.Min(safe.width / 1280f, safe.height / 720f);
                Vector3 offset = new Vector3(safe.x + (safe.width - 1280f * scale) * .5f,
                    Screen.height - safe.yMax + (safe.height - 720f * scale) * .5f, 0);
                GUI.matrix = Matrix4x4.TRS(offset, Quaternion.identity, new Vector3(scale, scale, 1));
                DrawMapUI();
            }
            else { DrawBattleUI(); DrawWaveWarnings(); }
            if (mode != Mode.Result) resultSince = -1f;
            if (mode == Mode.Result) DrawResultUI();
            if (paused) DrawPauseUI();
            if (movingScroll)
            {
                GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(Screen.width / 1280f, Screen.height / 720f, 1));
                DrawScrollTransition();
            }
            DrawBossRitualOverlay();
            if (shojiTime >= 0f)
            {
                GUI.matrix = Matrix4x4.identity;
                DrawShojiTransition();
            }
            GUI.matrix = previousMatrix;
        }

        void DrawTitleScreen()
        {
            Color previous = GUI.color;
            GUI.color = new Color(0.08f, 0.047f, 0.03f);
            GUI.Label(new Rect(1, 483, 1280, 115), "Bushido", titleLogoStyle);
            GUI.color = previous;
            GUI.Label(new Rect(0, 480, 1280, 115), "Bushido", titleLogoStyle);

            var play = new Rect(520, 607, 240, 55);
            bool hovered = play.Contains(Event.current.mousePosition);
            Color border = hovered ? new Color(0.98f, 0.78f, 0.43f) : new Color(0.73f, 0.53f, 0.27f);
            Panel(new Rect(play.x + 1, play.y + 4, play.width, play.height), new Color(0.07f, 0.04f, 0.025f, 0.85f));
            Panel(play, border);
            Panel(new Rect(play.x + 1, play.y + 1, play.width - 2, play.height - 2),
                hovered ? new Color(0.34f, 0.105f, 0.067f) : new Color(0.19f, 0.065f, 0.043f));
            Panel(new Rect(play.x + 7, play.y + 6, play.width - 14, 1), new Color(border.r, border.g, border.b, 0.35f));
            Panel(new Rect(play.x + 7, play.yMax - 7, play.width - 14, 1), new Color(border.r, border.g, border.b, 0.35f));
            if (GUI.Button(play, "Jugar", titleButtonStyle) && shojiTime < 0f)
            {
                shojiPanel = Resources.Load<Texture2D>("Backgrounds/ShojiPanel");
                shojiTime = 0f;
            }
        }

        bool UpdateShojiTransition()
        {
            if (shojiTime < 0f) return false;
            float previous = shojiTime;
            shojiTime += Mathf.Min(Time.unscaledDeltaTime, 0.05f);
            if (previous < ShojiSlideDuration && shojiTime >= ShojiSlideDuration) ShowMap(false);
            if (shojiTime >= ShojiRevealTime + ShojiSlideDuration) shojiTime = -1f;
            return true;
        }

        void DrawShojiTransition()
        {
            if (Event.current.type != EventType.Repaint) return;
            float motion = shojiTime <= ShojiSlideDuration
                ? shojiTime / ShojiSlideDuration : 1f - (shojiTime - ShojiRevealTime) / ShojiSlideDuration;
            float slide = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(motion));
            float width = Mathf.Ceil(Screen.width * 0.5f) + 1f;
            float offset = width * (1f - slide);
            DrawShojiPanel(new Rect(-offset, 0f, width, Screen.height), false);
            DrawShojiPanel(new Rect(Screen.width - width + offset, 0f, width, Screen.height), true);
        }

        void DrawShojiPanel(Rect panel, bool mirrored)
        {
            if (shojiPanel != null)
            {
                GUI.DrawTextureWithTexCoords(panel, shojiPanel, mirrored ? new Rect(1, 0, -1, 1) : new Rect(0, 0, 1, 1), false);
                return;
            }
            Color wood = new Color(0.24f, 0.14f, 0.085f);
            Panel(panel, new Color(0.87f, 0.77f, 0.58f));
            float rail = Mathf.Max(2f, panel.height * 0.009f);
            for (int col = 0; col <= 4; col++)
                Panel(new Rect(Mathf.Lerp(panel.x, panel.xMax - rail, col / 4f), panel.y, rail, panel.height), wood);
            for (int row = 0; row <= 8; row++)
                Panel(new Rect(panel.x, Mathf.Lerp(panel.y, panel.yMax - rail, row / 8f), panel.width, rail), wood);
        }

        void Panel(Rect rect, Color color)
        {
            Color previous = GUI.color;
            GUI.color = previous * color;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = previous;
        }

        // Every menu button is a paper tag: Paper with ink border, or vermilion with Paper text for the primary action.
        bool Button(Rect rect, string label, bool enabled = true, bool primary = false)
        {
            bool previous = GUI.enabled;
            GUI.enabled = enabled && shojiTime < 0f;
            bool hover = GUI.enabled && rect.Contains(Event.current.mousePosition);
            bool down = hover && Mouse.current != null && Mouse.current.leftButton.isPressed;
            DrawPaperTag(rect, label, primary, hover, down);
            bool clicked = GUI.Button(rect, GUIContent.none, GUIStyle.none);
            GUI.enabled = previous;
            return clicked && mode != Mode.Transition && mode != Mode.ScrollOpening && mode != Mode.ScrollClosing;
        }

        void DrawPaperTag(Rect rect, string label, bool primary, bool hover, bool down)
        {
            Color gc = GUI.color;
            if (!GUI.enabled) GUI.color = new Color(1, 1, 1, gc.a * .5f);
            Rect face = down ? new Rect(rect.x + 2, rect.y + 2, rect.width, rect.height) : rect; // pressed: sinks 2 px onto its shadow
            Panel(new Rect(rect.x + 4, rect.y + 5, rect.width, rect.height), Ink(down ? .15f : .28f));
            Panel(face, primary ? PrototypeArt.Red : PrototypeArt.Paper);
            Grain(face, .5f);
            if (hover || down) Panel(face, Ink(down ? .3f : .15f)); // ink wash fills the tag
            BrushBorder(face, 4f, PrototypeArt.Ink);
            Blot(new Vector2(face.x + 18, face.center.y), Vector2.one * 10, primary ? PrototypeArt.Ink : Ink(.8f)); // the tag's tie hole
            tagLabel.normal.textColor = primary ? PrototypeArt.Paper : PrototypeArt.Ink;
            GUI.Label(new Rect(face.x + 28, face.y, face.width - 36, face.height), label, tagLabel);
            GUI.color = gc;
        }

        // Ink-wash dim: flat wash plus darker bands towards the edges.
        void InkWash(float alpha)
        {
            Panel(new Rect(0, 0, 1280, 720), Ink(.4f * alpha));
            for (int i = 1; i <= 6; i++)
            {
                float band = i * 26f; Color edge = Ink(.07f * alpha);
                Panel(new Rect(0, 0, 1280, band), edge); Panel(new Rect(0, 720 - band, 1280, band), edge);
                Panel(new Rect(0, band, band, 720 - band * 2), edge); Panel(new Rect(1280 - band, band, band, 720 - band * 2), edge);
            }
        }

        void DrawMapUI()
        {
            Panel(new Rect(0, 0, 1280, 76), CampaignInk);
            Panel(new Rect(0, 73, 1280, 3), PrototypeArt.Gold);
            bool onMap = mode == Mode.Map && !paused;
            if (CampaignButton(new Rect(30, 13, 122, 47), "Mapa", !showTechniques, onMap)) SelectTechniques(false);
            if (CampaignButton(new Rect(164, 13, 146, 47), "Técnicas", showTechniques, onMap)) SelectTechniques(true);
            GUI.Label(new Rect(339, 24, 390, 33), "Capítulo 1  ·  Temporada de lluvias", textStyle);
            if (showTechniques)
            {
                DrawTechniques();
                return;
            }
            DrawCampaignBoard(onMap && shojiTime < 0f);
            if ((mode == Mode.Travel || mode == Mode.FaceOff) && CampaignButton(new Rect(1090, 13, 152, 47), "Pausa", false, !paused)) TogglePause();
            if (noticeTime > 0) GUI.Label(new Rect(35, 148, 1100, 40), notice, paperTextStyle);
            if (!string.IsNullOrEmpty(saveWarning)) GUI.Label(new Rect(35, 112, 1200, 48), saveWarning, paperTextStyle);
        }

        bool CampaignButton(Rect rect, string label, bool accent, bool enabled)
        {
            bool previous = GUI.enabled;
            GUI.enabled = enabled && shojiTime < 0f;
            bool hover = GUI.enabled && rect.Contains(Event.current.mousePosition);
            Panel(new Rect(rect.x + 2, rect.y + 3, rect.width, rect.height), new Color(0.10f, 0.06f, 0.04f, 0.45f));
            Panel(rect, accent || hover ? PrototypeArt.Gold : new Color(0.52f, 0.41f, 0.27f));
            Panel(new Rect(rect.x + 2, rect.y + 2, rect.width - 4, rect.height - 4),
                accent ? CampaignRed : hover ? new Color(0.32f, 0.21f, 0.14f) : CampaignInk);
            bool clicked = GUI.Button(rect, label, campaignButtonStyle);
            GUI.enabled = previous;
            return clicked;
        }

        void SelectTechniques(bool visible)
        {
            showTechniques = visible;
        }

        GUIStyle techniqueCredit;
        void DrawTechniques()
        {
            Panel(new Rect(40, 110, 1200, 560), CampaignInk);
            Panel(new Rect(44, 114, 1192, 552), CampaignPaper);
            GUI.Label(new Rect(85, 140, 1100, 40), "Técnicas del viajero · Un símbolo, un movimiento", paperTextStyle);
            // CC BY 3.0 asks for visible credit; it lives here since the map no longer shows it.
            if (techniqueCredit == null)
            {
                techniqueCredit = new GUIStyle(smallStyle) { fontSize = 13, alignment = TextAnchor.MiddleRight };
                techniqueCredit.normal.textColor = new Color(PrototypeArt.Ink.r, PrototypeArt.Ink.g, PrototypeArt.Ink.b, .7f);
            }
            GUI.Label(new Rect(85, 636, 1110, 22), "Iconos del mapa: Delapouite y Lorc · game-icons.net · CC BY 3.0", techniqueCredit);
            string[] names = { "Tiempo", "Abundancia", "Destierro" };
            string[] effects = { "Ralentiza las chanclas durante 4 s", "Dos chanclas extra por oleada durante 8 s", "Limpia las chanclas y debilita la postura" };
            for (int i = 0; i < 3; i++)
            {
                float y = 245 + i * 155;
                // Circular ink medallion built from horizontal chords.
                for (int row = -48; row <= 48; row++)
                {
                    float half = Mathf.Sqrt(48 * 48 - row * row);
                    Panel(new Rect(150-half, y+row, half*2, 1.1f), CampaignInk);
                }
                DrawTechniqueGesture(new Vector2(150, y), 37, PrototypeArt.Gold, i);
                DrawTechniqueGesture(new Vector2(305, y), 40, CampaignRed, i);
                GUI.Label(new Rect(410, y-35, 730, 35), names[i], paperTextStyle);
                GUI.Label(new Rect(410, y+5, 730, 55), effects[i], paperTextStyle);
                if (i < 2) Panel(new Rect(85, y+70, 1100, 1), new Color(.3f,.2f,.1f,.25f));
            }
        }
        void DrawTechniqueGesture(Vector2 center, float scale, Color ink, int variant = 0)
        {
            foreach (Vector3[] path in KanjiPaths(variant))
            for (int i = 0; i < path.Length; i++)
            {
                Vector2 point = center + new Vector2(path[i].x, -path[i].y) * scale;
                if (i > 0)
                {
                    Vector2 start = center + new Vector2(path[i - 1].x, -path[i - 1].y) * scale;
                    Vector2 direction = point - start;
                    Matrix4x4 matrix = GUI.matrix;
                    GUI.matrix = matrix * Matrix4x4.TRS(start,
                        Quaternion.Euler(0f, 0f, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg), Vector3.one);
                    Panel(new Rect(0, -2, direction.magnitude, 4), ink);
                    GUI.matrix = matrix;
                }
                Panel(new Rect(point.x - 4, point.y - 4, 8, 8), ink);
            }
        }

        void DrawTechniqueEffect(Vector2 center, bool clock, string glyph, string legend)
        {
            if (techniqueMedallion == null)
            {
                techniqueMedallion = CampaignInkArt.Seal(false);
                titleAssets.Add(techniqueMedallion);
            }
            if (techniqueEffectIcons == null)
            {
                const int size = 48;
                techniqueEffectIcons = new Texture2D(size * 2, size, TextureFormat.RGBA32, false);
                techniqueEffectIcons.wrapMode = TextureWrapMode.Clamp;
                var pixels = new Color32[size * size * 2];
                for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    Vector2 p = (new Vector2(x, y) + Vector2.one * 0.5f) / (size * 0.5f) - Vector2.one;
                    bool clockInk = Mathf.Abs(p.magnitude - 0.72f) < 0.09f
                        || (Mathf.Abs(p.x) < 0.07f && p.y >= 0f && p.y < 0.45f)
                        || (Mathf.Abs(p.y) < 0.07f && p.x >= 0f && p.x < 0.36f);
                    Vector2 eye = new Vector2(Mathf.Abs(p.x) - 0.38f, p.y - 0.37f);
                    bool frogInk = (new Vector2(p.x, (p.y + 0.10f) * 1.45f).sqrMagnitude < 0.69f * 0.69f
                        || eye.sqrMagnitude < 0.23f * 0.23f) && eye.sqrMagnitude > 0.082f * 0.082f;
                    int index = y * size * 2 + x;
                    pixels[index] = clockInk ? PrototypeArt.Gold : Color.clear;
                    pixels[index + size] = frogInk ? PrototypeArt.Gold : Color.clear;
                }
                techniqueEffectIcons.SetPixels32(pixels);
                techniqueEffectIcons.Apply(false, true);
                titleAssets.Add(techniqueEffectIcons);
            }
            GUI.DrawTexture(new Rect(center.x - 50, center.y - 50, 100, 100), techniqueMedallion);
            GUI.DrawTextureWithTexCoords(new Rect(center.x - 24, center.y - 34, 48, 48), techniqueEffectIcons,
                new Rect(clock ? 0f : 0.5f, 0f, 0.5f, 1f));
            GUI.Label(new Rect(center.x - 15, center.y + 10, 30, 30), glyph, campaignButtonStyle);
            GUI.Label(new Rect(194, center.y - 15, 435, 48), legend, paperTextStyle);
        }

        void DrawBattleUI()
        {
            if (bossFinalStage == 2) return;
            Matrix4x4 stretched = GUI.matrix;
            if (battleTime < hudClock - .5f) ResetHud(); // BeginBattle rewinds battleTime
            hudClock = battleTime;
            Anchor(1, 0);
            if (RitualModelKatana && bossFinalStage == 1)
            {
                if (Button(new Rect(1140, 30, 112, 52), "Pausa", !paused)) TogglePause();
                GUI.matrix = stretched;
                return;
            }
            if (Button(new Rect(1140, 30, 112, 52), "Pausa", mode == Mode.Battle && !paused)) TogglePause();
            Anchor(0, 0);
            DrawPaperHealth();
            if (jutsuTime > 0)
            {
                PaperCard(new Rect(22, 114, 176, 54));
                Caption(new Rect(34, 118, 100, 20), "Técnica", PrototypeArt.Ink);
                Bar(BarJutsu, new Rect(36, 142, 148, 12), jutsuTime / JutsuDuration, Color.Lerp(JutsuInk, PrototypeArt.Ink, .3f));
            }
            Anchor(.5f, 0);
            if (mode == Mode.Result) { } // the scroll takes the stage
            else if (IsBoss) { DrawBossSealIndicators(); DrawBossTag(); }
            else if (LanternDuel) DrawLanternTag();
            else DrawSenderoTag();
            Anchor(1, .5f);
            DrawStyleMeter();
            GUI.matrix = stretched;
            DrawDefense();
            DrawBossVoice();
            Anchor(0, 1);
            DrawJudgmentMeter();
            DrawNotice();
            GUI.matrix = stretched;
        }

        float resultSince = -1f;

        // Hanging scroll: unrolls, then stats count up, the rank is stamped, the message and buttons follow.
        void DrawResultUI()
        {
            if (resultSince < 0f) resultSince = Time.unscaledTime;
            float t = Time.unscaledTime - resultSince;
            if (Event.current.type == EventType.MouseDown && t < 2f) { resultSince -= 3f; t += 3f; } // a click skips the show
            InkWash(Mathf.Clamp01(t / .3f));
            float land = t - 1.45f, shake = land > 0f && land < .2f ? Mathf.Sin(land * 120f) * 4f * (1f - land / .2f) : 0f;
            Anchor(.5f, .5f);
            GUI.matrix *= Matrix4x4.Translate(new Vector3(shake, shake * .5f, 0));
            const float px = 380, pw = 520, top = 70, full = 560;
            float open = 1f - Mathf.Pow(1f - Mathf.Clamp01(t / .35f), 3f), bottom = top + full * open;
            Color gc = GUI.color;
            Panel(new Rect(px + 8, top + 8, pw, bottom - top), Ink(.25f));
            for (float y = top; y < bottom; y += 4f) // deckled sides: each 4 px slice is inset by noise
                Panel(new Rect(px + Mathf.PerlinNoise(y * .09f, 3.1f) * 9f, y, pw - Mathf.PerlinNoise(y * .09f, 3.1f) * 9f - Mathf.PerlinNoise(y * .09f, 7.7f) * 9f, Mathf.Min(4f, bottom - y)), PrototypeArt.Paper);
            Grain(new Rect(px + 9, top, pw - 18, bottom - top), .6f);
            Stroke(new Vector2(px + 60, top), new Vector2(640, -40), 4, PrototypeArt.Ink);
            Stroke(new Vector2(px + pw - 60, top), new Vector2(640, -40), 4, PrototypeArt.Ink);
            foreach (float rod in new[] { top, bottom })
            {
                Stroke(new Vector2(px - 28, rod), new Vector2(px + pw + 28, rod), 20, PrototypeArt.Ink);
                Blot(new Vector2(px - 28, rod), Vector2.one * 26, PrototypeArt.Ink);
                Blot(new Vector2(px + pw + 28, rod), Vector2.one * 26, PrototypeArt.Ink);
            }
            GUI.color = new Color(1, 1, 1, Mathf.Clamp01((t - .3f) / .2f));
            GUI.Label(new Rect(px, top + 28, pw, 56), won ? IsBoss ? "Jefe derrotado" : "Misión terminada" : "La batalla continúa", inkTitleStyle);
            Stroke(new Vector2(px + 70, top + 92), new Vector2(px + pw - 70, top + 94), 9, PrototypeArt.Ink, false, 0f, Mathf.Clamp01((t - .4f) / .3f));
            string[] names = { "Aciertos", "Fallos", "Heridas", "Mejor combo" };
            int[] values = { hits, misses, wounds, bestCombo };
            for (int i = 0; i < 4; i++)
            {
                float start = .55f + i * .22f, k = Mathf.Clamp01((t - start) / .3f), y = top + 120 + i * 46;
                GUI.color = new Color(1, 1, 1, Mathf.Clamp01((t - start) / .15f));
                hudCap.alignment = TextAnchor.MiddleLeft; hudCap.fontSize = 18; hudCap.normal.textColor = PrototypeArt.Ink;
                GUI.Label(new Rect(px + 50, y, 170, 36), names[i], hudCap);
                hudCap.fontSize = 13;
                hudNum.alignment = TextAnchor.MiddleRight; hudNum.normal.textColor = PrototypeArt.Ink;
                GUI.Label(new Rect(px + 220, y - 3, 70, 40), Mathf.RoundToInt(values[i] * (1f - Mathf.Pow(1f - k, 3f))).ToString(), hudNum);
                hudNum.alignment = TextAnchor.MiddleLeft;
                Stroke(new Vector2(px + 50, y + 38), new Vector2(px + 290, y + 38), 4, Ink(.5f), true, 0f, k);
            }
            GUI.color = gc;
            if (land > 0f)
            {
                float k = Mathf.Clamp01(land / .16f), a = Mathf.Clamp01(land / .06f);
                Vector2 c = new Vector2(px + pw - 130, top + 210);
                if (land > .16f) Blot(c, Vector2.one * 250 * (.85f + .15f * Mathf.Clamp01((land - .16f) / .12f)), new Color(PrototypeArt.Red.r, PrototypeArt.Red.g, PrototypeArt.Red.b, .6f), 15, PrototypeArt.InkSplat);
                Matrix4x4 m = GUI.matrix;
                Pivot(c, -6f, Mathf.Lerp(1.6f, 1f, k * k)); // the stamp falls onto the paper
                GUI.color = new Color(PrototypeArt.Red.r, PrototypeArt.Red.g, PrototypeArt.Red.b, a);
                GUI.DrawTexture(new Rect(c.x - 70, c.y - 70, 140, 140), PrototypeArt.Hanko);
                string rank = !won ? "DERROTA" : resultRank == Rank.Perfect ? "PERFECTO" : resultRank.ToString();
                stampStyle.fontSize = rank.Length > 2 ? 25 : 104;
                GUI.color = new Color(1, 1, 1, a); // the hanko tint above would also dye the lettering
                stampStyle.normal.textColor = PrototypeArt.Paper;
                GUI.Label(new Rect(c.x - 70, c.y - 70, 140, 140), rank, stampStyle);
                GUI.matrix = m; GUI.color = gc;
            }
            if (t > 1.85f)
            {
                string message = !won ? "Puedes volver a intentarlo o elegir otra misión disponible."
                    : unlockedNew ? "Nuevo destino desbloqueado. Elige tu próximo encuentro en el mapa."
                    : IsBoss && mission == 4 ? "Sombrilla — ¿Oyes eso? Por fin llueve fuera de mí.\nSamurái — Ven. Hay sitio bajo el alero."
                    : !IsBoss && !LanternDuel && resultRank < Rank.B && !progress.completed[mission] ? "Necesitas rango B para seguir el camino"
                    : "Tu mejor calificación queda guardada. Puedes volver a jugar desde el mapa.";
                GUI.color = new Color(1, 1, 1, Mathf.Clamp01((t - 1.85f) / .25f));
                if (IsTrail)
                {
                    if (!won && mission == 2 && lives > 0) message = $"Meta del festival: {FestivalScore.Target} puntos.";
                    message = $"Puntos {arcade.Points}   ·   Récord {progress.bestScores[mission]}\n" + message;
                }
                GUI.Label(new Rect(px + 44, top + 340, pw - 88, 80), message, resultMessageStyle);
                GUI.color = gc;
            }
            if (t > 2.1f)
            {
                float slide = (1f - Mathf.Clamp01((t - 2.1f) / .2f)) * 14f;
                GUI.color = new Color(1, 1, 1, Mathf.Clamp01((t - 2.1f) / .2f));
                bool mapFirst = won && (IsBoss || LanternDuel || resultRank >= Rank.B); // below rank B the useful next step is to retry
                // Behaviour unchanged: Finish() cannot be edited, so these two lines are the old button actions verbatim.
                if (Button(new Rect(px + 24, top + 440 + slide, 226, 58), "Volver al mapa", true, mapFirst)) { selected = mission; mode = Mode.Transition; StartCoroutine(RevealMap()); }
                if (Button(new Rect(px + pw - 250, top + 440 + slide, 226, 58), "Reintentar", true, !mapFirst)) BeginBattle();
                GUI.color = gc;
            }
        }

        void DrawPauseUI()
        {
            Matrix4x4 previous = GUI.matrix;
            GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity,
                new Vector3(Screen.width / 1280f, Screen.height / 720f, 1));
            InkWash(1f);
            // Keep the card inside the phone's safe area, including camera cutouts.
            Anchor(.5f, .5f);
            if (pauseHeading == null)
            {
                pauseHeading = new GUIStyle(titleLogoStyle) { fontSize = 42 };
                pauseHeading.normal.textColor = PrototypeArt.Ink;
            }
            Rect paper = new Rect(370, 146, 540, 428);
            PaperCard(paper);
            Panel(new Rect(624, 172, 32, 32), PrototypeArt.Red);
            Panel(new Rect(633, 180, 4, 16), PrototypeArt.Paper);
            Panel(new Rect(643, 180, 4, 16), PrototypeArt.Paper);
            GUI.Label(new Rect(410, 222, 460, 65), "Pausa", pauseHeading);
            Stroke(new Vector2(480, 306), new Vector2(800, 308), 8, PrototypeArt.Ink);
            if (Button(new Rect(430, 344, 420, 64), "Continuar", true, true)) TogglePause();
            if (Button(new Rect(430, 430, 420, 64), "Volver al mapa")) ShowMap();
            GUI.matrix = previous;
        }
    }
}
