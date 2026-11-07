using UnityEngine;

/// <summary>
/// Yokai: viaja de un punto a otro con movimiento oscilante (como fantasma).
/// Se corta solo mediante BladeController (cadena de cortes).
/// </summary>
public class Enemy : MonoBehaviour
{
    public static int Alive { get; private set; }

    /// <summary>Se dispara cuando un enemigo muere por un corte.</summary>
    public static event System.Action<Enemy> Killed;

    [Header("Puntuación")]
    [Min(0)] public int points = 100;

    [Header("Efecto especial: Frenesí")]
    [Tooltip("Si está activo, al matar a este enemigo empieza el modo frenesí")]
    public bool triggersFrenzy = false;
    [Tooltip("Segundos que dura el frenesí")]
    [Min(0f)] public float frenzyDuration = 8f;

    [Header("Colores")]
    public Color normalColor = new Color(0.6f, 0.9f, 1f);
    public Color markedColor = new Color(1f, 0.3f, 0.3f);

    public bool IsMarked { get; private set; }

    Renderer rend;
    Vector3 startPos, endPos, perpendicular;
    float travelTime, elapsed;
    float amplitude, frequency, phase;
    bool initialized;

    void Awake()
    {
        rend = GetComponent<Renderer>();
        SetColor(normalColor);
    }

    void OnEnable()  { Alive++; }
    void OnDestroy() { Alive--; }

    /// <summary>Lo llama el spawner justo después de instanciar.</summary>
    public void Init(Vector3 start, Vector3 end, float speed, float amp, float freq)
    {
        startPos = start;
        endPos = end;
        amplitude = amp;
        frequency = freq;
        phase = Random.Range(0f, Mathf.PI * 2f);

        Vector3 dir = (end - start).normalized;
        perpendicular = Vector3.Cross(dir, Vector3.forward).normalized;

        travelTime = Vector3.Distance(start, end) / Mathf.Max(speed, 0.01f);
        elapsed = 0f;
        initialized = true;
        transform.position = start;
    }

    void Update()
    {
        if (!initialized) return;

        elapsed += Time.deltaTime;
        float t = elapsed / travelTime;

        if (t >= 1f)
        {
            // Salió de la pantalla. Si estaba marcado, BladeController
            // detecta que desapareció y rompe la cadena.
            Destroy(gameObject);
            return;
        }

        Vector3 pos = Vector3.Lerp(startPos, endPos, t);

        float wave = Mathf.Sin(elapsed * frequency + phase) - Mathf.Sin(phase);
        pos += perpendicular * (wave * amplitude);

        transform.position = pos;
    }

    public void Mark()
    {
        IsMarked = true;
        SetColor(markedColor);
    }

    public void Unmark()
    {
        IsMarked = false;
        SetColor(normalColor);
    }

    public void Die()
    {
        Killed?.Invoke(this); // el spawner escucha esto para iniciar el frenesí
        // Aquí luego puedes meter partículas, sonido, etc.
        Destroy(gameObject);
    }

    void SetColor(Color c)
    {
        if (rend != null) rend.material.color = c;
    }
}