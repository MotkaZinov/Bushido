using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Puntaje + COMBO POR TIEMPO.
/// Cada tajo exitoso SUBE el multiplicador (más enemigos en el tajo = sube más)
/// y reinicia la ventana de tiempo. Si la ventana se acaba sin un nuevo tajo,
/// el multiplicador vuelve a x1.
/// </summary>
public class ScoreManager : MonoBehaviour
{
    public static ScoreManager Instance { get; private set; }

    [Serializable]
    public class ChainGain
    {
        [Tooltip("Mínimo de enemigos cortados de un solo tajo")]
        public int enemies;
        [Tooltip("Cuánto SUBE el multiplicador con ese tajo")]
        public float gain = 0.5f;

        public ChainGain() { }
        public ChainGain(int enemies, float gain)
        {
            this.enemies = enemies;
            this.gain = gain;
        }
    }

    [Header("UI")]
    public TMP_Text scoreText;
    [Tooltip("Muestra el multiplicador actual (x3.5)")]
    public TMP_Text multiplierText;
    [Tooltip("Barra que se vacía con el tiempo del combo (Pivot X = 0)")]
    public RectTransform comboBar;
    public string scoreFormat = "Puntos: {0}";
    public string multiplierFormat = "x{0:0.#}";

    [Header("Popups de puntos")]
    public ScorePopup popupPrefab;
    public Canvas canvas;
    public Camera cam;
    [Tooltip("Tamaño general del popup (1 = normal, 0.5 = la mitad)")]
    public float popupSize = 1f;
    [Tooltip("Alto de Canvas para el que se diseñó el prefab. Normalmente no se toca")]
    public float popupDesignHeight = 1080f;
    [Tooltip("Margen para que el popup no se salga de la pantalla (unidades de diseño)")]
    public float popupEdgeMargin = 150f;

    [Header("Combo por tiempo")]
    [Tooltip("Segundos para hacer otro tajo antes de perder el multiplicador")]
    public float comboWindow = 2.5f;
    [Tooltip("Tope del multiplicador. 0 = sin límite")]
    public float maxMultiplier = 0f;

    [Header("Cuánto sube el multiplicador según el tamaño del tajo")]
    [Tooltip("Se usa la fila de mayor 'enemies' que sea <= al tamaño del tajo")]
    public List<ChainGain> chainGains = new List<ChainGain>
    {
        new ChainGain(2, 0.5f),
        new ChainGain(3, 1f),
        new ChainGain(4, 1.5f),
        new ChainGain(5, 2f),
        new ChainGain(6, 3f),
    };
    [Tooltip("Después de la última fila: cada enemigo extra suma esto a la subida")]
    public float extraGainPerEnemy = 1f;
    [Tooltip("Hace que cada enemigo extra aporte cada vez más")]
    public float extraGainAcceleration = 0.5f;

    [Header("Colores según intensidad del combo")]
    public Color lowColor = Color.white;
    public Color midColor = new Color(1f, 0.85f, 0.2f);
    public Color highColor = new Color(1f, 0.25f, 0.35f);
    [Tooltip("Multiplicador en el que el color llega al máximo")]
    public float colorMaxMultiplier = 10f;

    public int Score { get; private set; }
    public float BestMultiplier { get; private set; } = 1f;
    public int BestChain { get; private set; }
    public float ComboMultiplier { get; private set; } = 1f;

    float comboExpireTime;
    float punch;
    float shownMultiplier = -1f;
    Vector3 multiplierBaseScale = Vector3.one;
    Vector3 barBaseScale = Vector3.one;
    Image barImage;

    void Awake()
    {
        Instance = this;
    }

    void Start()
    {
        if (cam == null) cam = Camera.main;
        if (multiplierText != null) multiplierBaseScale = multiplierText.rectTransform.localScale;
        if (comboBar != null)
        {
            barBaseScale = comboBar.localScale;
            barImage = comboBar.GetComponent<Image>();
        }

        RefreshScore();
        UpdateComboUI();
    }

    void Update()
    {
        // Se acabó la ventana de tiempo: el multiplicador se reinicia
        if (ComboMultiplier > 1f && Time.time >= comboExpireTime)
            ComboMultiplier = 1f;

        UpdateComboUI();
    }

    // ───────────── Lo llama BladeController al ejecutar un tajo ─────────────

    public void RegisterChain(int baseScore, int chainSize, Vector3 worldCenter)
    {
        // Si el combo anterior ya había expirado, empezamos desde x1
        if (Time.time >= comboExpireTime) ComboMultiplier = 1f;

        // El tajo sube el multiplicador y reinicia la ventana
        ComboMultiplier += GetGain(chainSize);
        if (maxMultiplier > 0f) ComboMultiplier = Mathf.Min(ComboMultiplier, maxMultiplier);
        comboExpireTime = Time.time + comboWindow;
        punch = 0.6f;

        BestMultiplier = Mathf.Max(BestMultiplier, ComboMultiplier);
        BestChain = Mathf.Max(BestChain, chainSize);

        // Los puntos de este tajo ya usan el multiplicador actualizado
        int total = Mathf.RoundToInt(baseScore * ComboMultiplier);
        Score += total;
        RefreshScore();

        SpawnPopup(total, chainSize, worldCenter);
    }

    float GetGain(int chainSize)
    {
        ChainGain best = null;
        int highestRow = int.MinValue;

        foreach (ChainGain g in chainGains)
        {
            if (g.enemies > highestRow) highestRow = g.enemies;
            if (g.enemies <= chainSize && (best == null || g.enemies > best.enemies))
                best = g;
        }

        if (best == null) return 0f;

        float gain = best.gain;

        // Si el tajo pasa de la última fila, la subida sigue creciendo
        if (best.enemies == highestRow)
        {
            int k = chainSize - best.enemies;
            gain += extraGainPerEnemy * k + extraGainAcceleration * k * (k - 1) / 2f;
        }

        return gain;
    }

    // ───────────── Popup que cae del cielo ─────────────

    void SpawnPopup(int total, int chainSize, Vector3 worldCenter)
    {
        if (popupPrefab == null || canvas == null) return;
        if (cam == null) cam = Camera.main;

        RectTransform canvasRect = (RectTransform)canvas.transform;
        Camera uiCam = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;

        Vector2 screen = cam.WorldToScreenPoint(worldCenter);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screen, uiCam, out Vector2 local);

        // Que no se salga de la pantalla
        float uiScale = canvasRect.rect.height / Mathf.Max(1f, popupDesignHeight);
        Vector2 half = canvasRect.rect.size * 0.5f - Vector2.one * (popupEdgeMargin * uiScale);
        local.x = Mathf.Clamp(local.x, -half.x, half.x);
        local.y = Mathf.Clamp(local.y, -half.y, half.y);

        float t01 = Mathf.InverseLerp(1f, colorMaxMultiplier, ComboMultiplier);

        ScorePopup popup = Instantiate(popupPrefab, canvas.transform);
        popup.transform.SetAsLastSibling();

        string text = $"<size=55%>{chainSize} YOKAIS  x{ComboMultiplier:0.#}</size>\n+{total}";
        popup.Play(text, local, TierColor(ComboMultiplier), Mathf.Lerp(1f, 1.8f, t01) * popupSize, uiScale);
    }

    // ───────────── UI ─────────────

    void UpdateComboUI()
    {
        bool active = ComboMultiplier > 1f;
        Color c = TierColor(ComboMultiplier);

        if (multiplierText != null)
        {
            // Solo reescribimos el texto cuando cambia (evita basura)
            if (!Mathf.Approximately(shownMultiplier, ComboMultiplier))
            {
                shownMultiplier = ComboMultiplier;
                multiplierText.text = active ? string.Format(multiplierFormat, ComboMultiplier) : "";
            }

            multiplierText.color = c;
            punch = Mathf.Lerp(punch, 0f, 12f * Time.unscaledDeltaTime);
            multiplierText.rectTransform.localScale = multiplierBaseScale * (1f + punch);
        }

        if (comboBar != null)
        {
            float left = (active && comboWindow > 0f)
                ? Mathf.Clamp01((comboExpireTime - Time.time) / comboWindow)
                : 0f;

            Vector3 s = barBaseScale;
            s.x = barBaseScale.x * left;
            comboBar.localScale = s;

            if (barImage != null) barImage.color = c;
        }
    }

    Color TierColor(float multiplier)
    {
        float t = Mathf.InverseLerp(1f, colorMaxMultiplier, multiplier);
        return t < 0.5f
            ? Color.Lerp(lowColor, midColor, t * 2f)
            : Color.Lerp(midColor, highColor, (t - 0.5f) * 2f);
    }

    void RefreshScore()
    {
        if (scoreText != null)
            scoreText.text = string.Format(scoreFormat, Score);
    }
}