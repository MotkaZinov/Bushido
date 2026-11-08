using UnityEngine;

/// <summary>
/// "Jugo" del juego: hit-stop (congelar un instante al cortar) + screen shake.
/// Ambos escalan con el número de yokais del tajo.
/// </summary>
public class GameFeel : MonoBehaviour
{
    public static GameFeel Instance { get; private set; }

    [Header("Hit-stop")]
    public bool hitStopEnabled = true;
    [Tooltip("Segundos de congelamiento con un tajo de 2 yokais")]
    public float hitStopBase = 0.04f;
    [Tooltip("Segundos extra por cada yokai adicional")]
    public float hitStopPerEnemy = 0.015f;
    public float hitStopMax = 0.12f;
    [Tooltip("0 = congelado total. Un valor bajo (0.05) se siente como cámara súper lenta")]
    [Range(0f, 0.5f)] public float hitStopTimeScale = 0f;

    [Header("Screen shake")]
    public bool shakeEnabled = true;
    public CameraRig cameraRig;
    [Tooltip("Trauma (0 a 1) con un tajo de 2 yokais")]
    public float shakeBase = 0.25f;
    [Tooltip("Trauma extra por cada yokai adicional")]
    public float shakePerEnemy = 0.08f;

    float hitStopEnd;
    bool frozen;
    float savedTimeScale = 1f;

    void Awake()
    {
        Instance = this;
    }

    void Start()
    {
        if (cameraRig == null) cameraRig = FindFirstObjectByType<CameraRig>();
    }

    /// <summary>Lo llama BladeController al ejecutar un tajo.</summary>
    public void OnSlash(int chainSize, float multiplier)
    {
        int extra = Mathf.Max(0, chainSize - 2);

        if (hitStopEnabled)
            HitStop(Mathf.Min(hitStopMax, hitStopBase + hitStopPerEnemy * extra));

        if (shakeEnabled && cameraRig != null)
            cameraRig.AddTrauma(shakeBase + shakePerEnemy * extra);
    }

    public void HitStop(float duration)
    {
        if (duration <= 0f) return;

        if (!frozen)
        {
            savedTimeScale = Time.timeScale; // para devolverlo como estaba (por si hay pausa)
            frozen = true;
            hitStopEnd = 0f;
        }

        Time.timeScale = hitStopTimeScale;
        hitStopEnd = Mathf.Max(hitStopEnd, Time.unscaledTime + duration);
    }

    void Update()
    {
        // Update sigue corriendo con timeScale = 0, y usamos tiempo sin escala
        if (frozen && Time.unscaledTime >= hitStopEnd)
        {
            Time.timeScale = savedTimeScale;
            frozen = false;
        }
    }

    void OnDisable()
    {
        if (frozen)
        {
            Time.timeScale = savedTimeScale;
            frozen = false;
        }
    }
}