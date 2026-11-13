using UnityEngine;

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
        static readonly Color CampaignInk = new Color(0.15f, 0.10f, 0.075f);
        static readonly Color CampaignRed = new Color(0.48f, 0.16f, 0.105f);
        static readonly Color CampaignPaper = new Color(0.90f, 0.81f, 0.64f);
        Texture2D shojiPanel;
        Texture2D pausePaper;
        GUIStyle pauseHeading;
        GUIStyle pauseButtonLabel;
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

        bool Button(Rect rect, string label, bool enabled = true)
        {
            bool previous = GUI.enabled;
            GUI.enabled = enabled && shojiTime < 0f;
            bool clicked = GUI.Button(rect, label, buttonStyle);
            GUI.enabled = previous;
            return clicked && mode != Mode.Transition && mode != Mode.ScrollOpening && mode != Mode.ScrollClosing;
        }

        void Bar(Rect rect, float fraction, Color color)
        {
            Panel(rect, new Color(0.08f, 0.1f, 0.11f));
            Panel(new Rect(rect.x, rect.y, rect.width * Mathf.Clamp01(fraction), rect.height), color);
        }

        void DrawMapUI()
        {
            Panel(new Rect(0, 0, 1280, 76), CampaignInk);
            Panel(new Rect(0, 73, 1280, 3), PrototypeArt.Gold);
            bool onMap = mode == Mode.Map && !paused;
            if (CampaignButton(new Rect(30, 13, 122, 47), "Mapa", !showTechniques, onMap)) SelectTechniques(false);
            if (CampaignButton(new Rect(164, 13, 146, 47), "Técnicas", showTechniques, onMap)) SelectTechniques(true);
            GUI.Label(new Rect(339, 24, 390, 33), "Capítulo 01  /  El despertar", textStyle);
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

        void DrawTechniques()
        {
            Panel(new Rect(40, 110, 1200, 560), CampaignInk);
            Panel(new Rect(44, 114, 1192, 552), CampaignPaper);
            GUI.Label(new Rect(85, 140, 1100, 40), "Técnicas del viajero · Un símbolo, un movimiento", paperTextStyle);
            string[] names = { "Tiempo", "Abundancia", "Destierro" };
            string[] effects = { "Ralentiza las ranas durante 4 s", "Dos ranas extra por oleada durante 8 s", "Limpia las ranas y debilita la postura" };
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
            if (IsBoss && bossFinalStage == 2) return;
            if(RitualModelKatana && bossFinalStage==1)
            {
                if(Button(new Rect(1140,30,112,52),"Pausa",!paused))TogglePause();
                return;
            }
            DrawPaperHealth();
            if (IsBoss)
            {
                DrawBossSealIndicators();
                Bar(new Rect(420, 28, 420, 7), (float)bossHealth / bossMaxHealth, PrototypeArt.Red);
                Color postureInk = Color.Lerp(PrototypeArt.Gold, Color.white, postureFlash / 0.24f);
                Bar(new Rect(420, 41, 420, 4), vulnerable ? counterTime / 10f : 1f - (float)guard / guardLimit, postureInk);
            }
            else Bar(new Rect(420, 28, 420, 4), 1 - battleTime / 35, PrototypeArt.Jade);
            if (Button(new Rect(1140, 30, 112, 52), "Pausa", mode == Mode.Battle && !paused)) TogglePause();
            DrawStyleMeter();
            DrawDefense();
            if (jutsuTime > 0) Bar(new Rect(32, 144, 155, 5), jutsuTime / JutsuDuration, JutsuInk);
        }

        void DrawResultUI()
        {
            Panel(new Rect(0, 132, 1280, 500), new Color(0.025f, 0.045f, 0.05f, 0.9f));
            GUI.Label(new Rect(355, 160, 600, 46), won ? IsBoss ? "Jefe derrotado" : "Misión terminada" : "La batalla continúa", titleStyle);
            GUI.Label(new Rect(470, 210, 340, 100), won ? resultRank.ToString() : "Derrota", rankStyle);
            GUI.Label(new Rect(355, 326, 600, 52), "Cortes " + hits + "   ·   Fallos " + misses + "   ·   Daño " + wounds + "\nMejor combo " + bestCombo, textStyle);
            string message = !won ? "Puedes volver a intentarlo o elegir otra misión disponible."
                : unlockedNew ? "Nuevo destino desbloqueado. Elige tu próximo encuentro en el mapa."
                : IsBoss && mission == 4 ? "Has completado el camino del bambú. Tus encuentros siguen disponibles."
                : !IsBoss && resultRank < Rank.B && !progress.completed[mission] ? "Necesitas B o superior para desbloquear el siguiente punto."
                : "Tu mejor calificación queda guardada. Puedes volver a jugar desde el mapa.";
            GUI.Label(new Rect(355, 401, 600, 70), message, textStyle);
            if (Button(new Rect(355, 505, 265, 60), "Volver al mapa")) { selected = mission; ShowMap(); }
            if (Button(new Rect(650, 505, 265, 60), "Reintentar")) BeginBattle();
        }

        void DrawPauseUI()
        {
            Matrix4x4 previous = GUI.matrix;
            GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity,
                new Vector3(Screen.width / 1280f, Screen.height / 720f, 1));
            Panel(new Rect(0, 0, 1280, 720), new Color(0.025f, 0.035f, 0.03f, 0.76f));
            // Keep the card inside the phone's safe area, including camera cutouts.
            Rect safe = Screen.safeArea;
            float scale = Mathf.Min(safe.width / 1280f, safe.height / 720f);
            GUI.matrix = Matrix4x4.TRS(new Vector3(safe.x + (safe.width - 1280f * scale) * 0.5f,
                Screen.height - safe.yMax + (safe.height - 720f * scale) * 0.5f, 0),
                Quaternion.identity, new Vector3(scale, scale, 1));
            if (pauseHeading == null)
            {
                pauseHeading = new GUIStyle(titleLogoStyle) { fontSize = 42 };
                pauseHeading.normal.textColor = CampaignInk;
                pauseButtonLabel = new GUIStyle(titleButtonStyle) { fontSize = 22 };
            }
            if (pausePaper == null) pausePaper = Resources.Load<Texture2D>("Backgrounds/MapaTinta");
            Rect paper = new Rect(370, 146, 540, 428);
            Panel(new Rect(379, 158, 540, 428), new Color(0, 0, 0, 0.35f));
            Panel(paper, CampaignPaper);
            Color tint = GUI.color;
            if (pausePaper != null)
            {
                GUI.color = new Color(1, 1, 1, 0.18f);
                GUI.DrawTexture(paper, pausePaper, ScaleMode.ScaleAndCrop);
                GUI.color = tint;
            }
            // Uneven ink edges and restrained gold trim echo the campaign's paper cards.
            for (int i = 0; i < 27; i++)
            {
                float edge = 2f + Mathf.Sin(i * 2.3f) * 1.5f;
                Panel(new Rect(370 + i * 20, 146, 20, edge), CampaignInk);
                Panel(new Rect(370 + i * 20, 574 - edge, 20, edge), CampaignInk);
            }
            Panel(new Rect(386, 165, 2, 390), PrototypeArt.Gold);
            Panel(new Rect(892, 165, 2, 390), PrototypeArt.Gold);
            Panel(new Rect(624, 179, 32, 32), CampaignRed);
            Panel(new Rect(633, 187, 4, 16), CampaignPaper);
            Panel(new Rect(643, 187, 4, 16), CampaignPaper);
            GUI.Label(new Rect(410, 227, 460, 65), "Pausa", pauseHeading);
            DuelGuiLine(new Vector2(500, 310), new Vector2(780, 310), CampaignInk, 2);
            if (PausePaperButton(new Rect(430, 344, 420, 64), "Continuar", true)) TogglePause();
            if (PausePaperButton(new Rect(430, 430, 420, 64), "Volver al mapa", false)) ShowMap();
            GUI.matrix = previous;
        }

        bool PausePaperButton(Rect rect, string label, bool primary)
        {
            bool hover = rect.Contains(Event.current.mousePosition);
            Panel(new Rect(rect.x + 3, rect.y + 4, rect.width, rect.height), new Color(0, 0, 0, 0.18f));
            Color ink = primary ? CampaignRed : CampaignInk;
            if (hover) ink = Color.Lerp(ink, PrototypeArt.Gold, 0.18f);
            Panel(rect, ink);
            Panel(new Rect(rect.x + 12, rect.y + 7, rect.width - 24, 1), PrototypeArt.Gold);
            Panel(new Rect(rect.x + 12, rect.yMax - 8, rect.width - 24, 1), PrototypeArt.Gold);
            GUI.Label(rect, label, pauseButtonLabel);
            return GUI.Button(rect, GUIContent.none, GUIStyle.none);
        }
    }
}






