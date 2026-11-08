using TMPro;
using UnityEngine;

/// <summary>
/// Texto de puntos que CAE DEL CIELO, rebota al aterrizar con un golpe de escala,
/// se queda un momento y se desvanece subiendo.
/// Va en un prefab con un TextMeshPro (UI) dentro del Canvas.
/// Todas las distancias están pensadas para un Canvas de 1080 de alto y se
/// escalan solas según el Canvas real (ScoreManager le pasa 'uiScale').
/// </summary>
[RequireComponent(typeof(TMP_Text))]
public class ScorePopup : MonoBehaviour
{
    [Header("Caída")]
    [Tooltip("Desde qué altura empieza a caer (en unidades de un Canvas de 1080 de alto)")]
    public float dropHeight = 700f;
    public float fallTime = 0.3f;

    [Header("Impacto")]
    [Tooltip("Qué tanto crece de golpe al aterrizar")]
    public float punchScale = 1.5f;
    public float bounceHeight = 70f;
    public float impactTime = 0.45f;

    [Header("Permanencia y salida")]
    public float holdTime = 0.5f;
    public float fadeTime = 0.4f;
    public float exitRise = 140f;

    [Header("Inclinación aleatoria")]
    public float maxTilt = 8f;

    TMP_Text label;
    RectTransform rt;
    Vector2 target;
    Color color = Color.white;
    float size = 1f;   // tamaño por intensidad del combo
    float ui = 1f;     // escala según el Canvas
    float tilt;
    float t;

    void Awake()
    {
        label = GetComponent<TMP_Text>();
        rt = (RectTransform)transform;
    }

    public void Play(string text, Vector2 targetPos, Color col, float sizeMultiplier, float uiScale)
    {
        label.text = text;
        label.raycastTarget = false;

        target = targetPos;
        color = col;
        size = sizeMultiplier;
        ui = uiScale;
        tilt = Random.Range(-maxTilt, maxTilt);

        // Centro/centro para que anchoredPosition sea relativo al centro del Canvas
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);

        t = 0f;
        rt.anchoredPosition = target + Vector2.up * (dropHeight * ui);
        label.color = new Color(col.r, col.g, col.b, 0f);
    }

    void Update()
    {
        t += Time.unscaledDeltaTime; // sigue animando aunque haya hit-stop

        float fallEnd = fallTime;
        float impactEnd = fallEnd + impactTime;
        float holdEnd = impactEnd + holdTime;
        float end = holdEnd + fadeTime;

        float baseScale = size * ui;
        Vector2 pos;
        float scale;
        float alpha;

        if (t < fallEnd)
        {
            // 1) CAÍDA: acelera como si cayera del cielo
            float k = t / fallEnd;
            float ease = k * k;
            pos = Vector2.LerpUnclamped(target + Vector2.up * (dropHeight * ui), target, ease);
            scale = baseScale * Mathf.Lerp(0.6f, 1f, ease);
            alpha = Mathf.Clamp01(k * 4f);
        }
        else if (t < impactEnd)
        {
            // 2) IMPACTO: golpe de escala + rebotes que se van apagando
            float k = (t - fallEnd) / impactTime;
            float bounce = Mathf.Abs(Mathf.Sin(k * Mathf.PI * 2f)) * (1f - k) * bounceHeight * ui;
            float punch = 1f + (punchScale - 1f) * Mathf.Cos(k * Mathf.PI * 3f) * Mathf.Exp(-4f * k);
            pos = target + Vector2.up * bounce;
            scale = baseScale * punch;
            alpha = 1f;
        }
        else if (t < holdEnd)
        {
            // 3) PERMANENCIA: flota un poquito
            pos = target + Vector2.up * (Mathf.Sin((t - impactEnd) * 4f) * 4f * ui);
            scale = baseScale;
            alpha = 1f;
        }
        else if (t < end)
        {
            // 4) SALIDA: sube y se desvanece
            float k = (t - holdEnd) / fadeTime;
            pos = target + Vector2.up * (exitRise * ui * k * k);
            scale = baseScale * (1f + 0.15f * k);
            alpha = 1f - k;
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        rt.anchoredPosition = pos;
        rt.localScale = Vector3.one * scale;
        rt.localRotation = Quaternion.Euler(0f, 0f, tilt);

        Color c = color;
        c.a = alpha;
        label.color = c;
    }
}