using UnityEngine;

/// <summary>
/// Va en la Main Camera.
/// 1) ENCUADRE AUTOMÁTICO: ajusta la distancia de la cámara para que SIEMPRE se vea el área
///    de juego indicada, en cualquier pantalla (celular vertical, horizontal, tablet, PC).
///    El spawner usa los bordes de la cámara, así que la zona de aparición se adapta sola.
/// 2) SCREEN SHAKE: sacudida tipo "trauma" (Perlin) que se apaga sola.
/// La cámara debe mirar hacia +Z (rotación 0,0,0) y el plano de juego estar en Z = 0.
/// </summary>
[RequireComponent(typeof(Camera))]
public class CameraRig : MonoBehaviour
{
    [Header("Encuadre automático (unidades de mundo que SIEMPRE deben verse)")]
    public bool autoFit = true;
    [Tooltip("Pantalla horizontal: (ancho, alto). 30.7 x 17.3 = lo que ya veías con la cámara en Z -15")]
    public Vector2 landscapeArea = new Vector2(30.7f, 17.3f);
    [Tooltip("Pantalla vertical: (ancho, alto)")]
    public Vector2 portraitArea = new Vector2(12f, 21f);

    [Header("Móvil")]
    [Tooltip("En celular Unity usa 30 FPS por defecto. 0 = no tocar")]
    public int targetFrameRate = 60;
    public bool keepScreenAwake = true;

    [Header("Screen shake")]
    [Tooltip("Desplazamiento máximo, en % del alto visible de la pantalla")]
    public float shakeOffsetPercent = 2f;
    [Tooltip("Giro máximo en grados")]
    public float shakeMaxRoll = 1.5f;
    [Tooltip("Qué tan rápido se apaga (trauma por segundo)")]
    public float shakeDecay = 1.8f;
    public float shakeFrequency = 25f;

    Camera cam;
    Vector3 basePosition;
    Quaternion baseRotation;
    float trauma;
    int lastWidth, lastHeight;

    void Awake()
    {
        cam = GetComponent<Camera>();
        basePosition = transform.position;
        baseRotation = transform.rotation;

        if (targetFrameRate > 0) Application.targetFrameRate = targetFrameRate;
        if (keepScreenAwake) Screen.sleepTimeout = SleepTimeout.NeverSleep;

        if (autoFit) ApplyFit();
        transform.position = basePosition;
    }

    /// <summary>Suma trauma (0 a 1). Lo llama GameFeel.</summary>
    public void AddTrauma(float amount)
    {
        trauma = Mathf.Clamp01(trauma + amount);
    }

    void LateUpdate()
    {
        if (autoFit && (Screen.width != lastWidth || Screen.height != lastHeight))
            ApplyFit();

        Vector3 offset = Vector3.zero;
        float roll = 0f;

        if (trauma > 0f)
        {
            // Unscaled: la sacudida sigue durante el hit-stop
            trauma = Mathf.Max(0f, trauma - shakeDecay * Time.unscaledDeltaTime);

            float shake = trauma * trauma;
            float t = Time.unscaledTime * shakeFrequency;
            float amp = VisibleHeight() * shakeOffsetPercent / 100f * shake;

            offset = new Vector3(Noise(0f, t), Noise(31.7f, t), 0f) * amp;
            roll = Noise(63.1f, t) * shakeMaxRoll * shake;
        }

        transform.position = basePosition + offset;
        transform.rotation = baseRotation * Quaternion.Euler(0f, 0f, roll);
    }

    void ApplyFit()
    {
        lastWidth = Screen.width;
        lastHeight = Screen.height;
        if (lastHeight <= 0) return;

        float aspect = (float)lastWidth / lastHeight;
        Vector2 area = aspect >= 1f ? landscapeArea : portraitArea;

        if (cam.orthographic)
        {
            cam.orthographicSize = Mathf.Max(area.y * 0.5f, area.x * 0.5f / aspect);
        }
        else
        {
            float tanHalf = Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad);
            float distForHeight = (area.y * 0.5f) / tanHalf;
            float distForWidth = (area.x * 0.5f) / (tanHalf * aspect);
            basePosition.z = -Mathf.Max(distForHeight, distForWidth);
        }
    }

    float VisibleHeight()
    {
        if (cam.orthographic) return cam.orthographicSize * 2f;
        return 2f * Mathf.Abs(basePosition.z) * Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad);
    }

    static float Noise(float seed, float t)
    {
        return (Mathf.PerlinNoise(seed, t) - 0.5f) * 2f;
    }
}