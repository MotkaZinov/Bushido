using System.Collections.Generic;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Navaja del jugador (mouse O dedo). Al pasar sobre yokais los va MARCANDO y los une con una línea.
/// El corte se ejecuta al soltar o cuando pasan 'chainTimeout' segundos desde el primer marcado.
/// Hacen falta al menos 'minChain' enemigos. Si un marcado sale de la pantalla, la cadena se rompe.
///
/// DETECCIÓN: se lanzan rayos desde la cámara por cada punto del trazo y se comparan con la
/// esfera de cada yokai. Así se corta exactamente lo que se VE, también en las esquinas
/// (la perspectiva ensancha las esferas cerca de las orillas).
/// </summary>
[RequireComponent(typeof(TrailRenderer))]
public class BladeController : MonoBehaviour
{
    [Header("Referencias")]
    public Camera cam;

    [Header("Corte")]
    [Tooltip("Radio de la navaja en % del alto de la pantalla. Se siente igual en cualquier pantalla")]
    public float bladeRadiusPercent = 2.5f;
    [Tooltip("Mínimo de enemigos marcados para que el corte funcione")]
    public int minChain = 2;
    [Tooltip("Segundos desde el PRIMER enemigo marcado hasta que se ejecuta el corte")]
    public float chainTimeout = 1f;

    [Header("Aspecto (relativo al tamaño de la pantalla)")]
    [Tooltip("Grosor de la estela, en % del alto de la pantalla")]
    public float trailWidthPercent = 1.6f;
    [Tooltip("Grosor de la línea de unión, en % del alto de la pantalla")]
    public float chainLineWidthPercent = 1f;
    public Color chainLineColor = new Color(1f, 0.85f, 0.2f);
    [Tooltip("Cuánto se acercan estela y línea a la cámara para dibujarse SIEMPRE por encima de los yokais")]
    public float drawTowardCamera = 3f;

    readonly List<Enemy> chain = new List<Enemy>(); // en orden de toque
    readonly Plane gamePlane = new Plane(Vector3.back, Vector3.zero);

    TrailRenderer trail;
    LineRenderer chainLine;
    bool slicing;
    Vector2 lastScreen;
    float chainStartTime;
    float lastTrailWidth, lastLineWidth;

    void Awake()
    {
        SetupTrail();
        SetupChainLine();
    }

    void Start()
    {
        if (cam == null) cam = Camera.main;
    }

    void Update()
    {
        if (cam == null || GameManager.IsGameOver) return;

        if (PressedThisFrame())
            StartSlice();
        else if (IsHeld() && slicing)
            ContinueSlice();
        else if (slicing)
            EndSlice();

        UpdateChain();
        UpdateWidths();
        UpdateChainLine();
    }

    // ───────────── Estados del corte ─────────────

    void StartSlice()
    {
        Vector2 screen = GetPointerPosition();

        slicing = true;
        lastScreen = screen;

        if (TryGetWorldPoint(screen, out Vector3 p))
            transform.position = TowardCamera(p);

        trail.Clear();
        trail.emitting = true;

        DetectHits(screen, screen); // también corta si empiezas justo encima de un yokai
    }

    void ContinueSlice()
    {
        Vector2 screen = GetPointerPosition();

        if (TryGetWorldPoint(screen, out Vector3 p))
            transform.position = TowardCamera(p); // lo que se VE va por delante de los yokais

        DetectHits(lastScreen, screen);
        ReportSpeed(lastScreen, screen);
        lastScreen = screen;
    }

    void EndSlice()
    {
        slicing = false;
        trail.emitting = false;

        // Al soltar el dedo/click se ejecuta el corte
        if (chain.Count > 0) ResolveChain();
    }

    // ───────────── Detección por rayos ─────────────

    void DetectHits(Vector2 from, Vector2 to)
    {
        // Se muestrea el trazo cada medio radio para no dejar huecos aunque muevas rápido
        float radiusPx = Screen.height * bladeRadiusPercent / 100f;
        float step = Mathf.Max(3f, radiusPx * 0.5f);
        int steps = Mathf.Clamp(Mathf.CeilToInt(Vector2.Distance(from, to) / step), 1, 96);

        for (int s = 0; s <= steps; s++)
        {
            Vector2 point = Vector2.Lerp(from, to, s / (float)steps);
            TestRay(cam.ScreenPointToRay(point));
        }
    }

    void TestRay(Ray ray)
    {
        List<Enemy> enemies = Enemy.Active;

        for (int i = 0; i < enemies.Count; i++)
        {
            Enemy e = enemies[i];
            if (e == null || e.IsMarked) continue;

            Vector3 toEnemy = e.Center - ray.origin;
            float along = Vector3.Dot(toEnemy, ray.direction);
            if (along <= 0f) continue; // detrás de la cámara

            float depth = Mathf.Max(0.1f, Vector3.Dot(toEnemy, cam.transform.forward));
            float reach = e.Radius + BladeRadiusWorld(depth);

            Vector3 offset = toEnemy - ray.direction * along; // distancia del rayo al centro
            if (offset.sqrMagnitude <= reach * reach)
                MarkEnemy(e);
        }
    }

    // El radio de la navaja convertido a unidades de mundo a esa profundidad
    float BladeRadiusWorld(float depth)
    {
        float visibleHeight = cam.orthographic
            ? cam.orthographicSize * 2f
            : 2f * depth * Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad);

        return visibleHeight * bladeRadiusPercent / 100f;
    }

    void MarkEnemy(Enemy e)
    {
        if (GameManager.IsGameOver) return;

        // Cubo de la muerte: si se pierde al tocarlo, se acabó
        if (e.causesGameOver && e.gameOverOnTouch)
        {
            TriggerGameOver();
            return;
        }

        if (chain.Count == 0) chainStartTime = Time.time; // arranca el reloj

        e.Mark();
        chain.Add(e);

        if (GameAudio.Instance != null) GameAudio.Instance.OnMark(chain.Count - 1);
    }

    // ───────────── Cadena ─────────────

    void UpdateChain()
    {
        if (chain.Count == 0) return;

        // ¿Alguno de los marcados desapareció (salió de pantalla)? → se rompe todo
        foreach (Enemy e in chain)
        {
            if (e == null)
            {
                BreakChain();
                return;
            }
        }

        // ¿Se acabó el tiempo? → ejecutar corte
        if (Time.time - chainStartTime >= chainTimeout)
            ResolveChain();
    }

    void ResolveChain()
    {
        if (chain.Count < minChain)
        {
            BreakChain(); // con un solo enemigo no pasa nada
            return;
        }

        // Si alguno desapareció justo en este frame, se rompe la cadena
        foreach (Enemy e in chain)
        {
            if (e == null)
            {
                BreakChain();
                return;
            }
        }

        // Un enemigo peligroso dentro del tajo: Game Over en vez de puntos
        foreach (Enemy e in chain)
        {
            if (e.causesGameOver)
            {
                TriggerGameOver();
                return;
            }
        }

        int count = chain.Count;
        int baseScore = 0;
        Vector3 center = Vector3.zero;

        foreach (Enemy e in chain)
        {
            baseScore += e.points;
            center += e.transform.position;
        }
        center /= count; // el popup aparece en el centro del tajo

        foreach (Enemy e in chain) e.Die();
        chain.Clear();

        ScoreManager score = ScoreManager.Instance;
        if (score != null) score.RegisterChain(baseScore, count, center);

        // Hit-stop + screen shake + sonido
        float mult = score != null ? score.ComboMultiplier : 1f;
        if (GameFeel.Instance != null) GameFeel.Instance.OnSlash(count, mult);
        if (GameAudio.Instance != null) GameAudio.Instance.OnSlash(count, mult);
    }

    // Enemigo peligroso cortado: fin de la partida
    void TriggerGameOver()
    {
        chain.Clear();
        chainLine.positionCount = 0;
        slicing = false;
        trail.emitting = false;

        if (GameManager.Instance != null)
            GameManager.Instance.GameOver();
        else
            Debug.LogWarning("BladeController: cortaste un enemigo peligroso pero no hay un GameManager en la escena.");
    }

    void BreakChain()
    {
        bool hadMarks = chain.Count > 0;

        foreach (Enemy e in chain)
            if (e != null) e.Unmark();

        chain.Clear();

        if (hadMarks && GameAudio.Instance != null) GameAudio.Instance.OnChainBroken();
    }

    // ───────────── Línea de unión ─────────────

    void UpdateChainLine()
    {
        if (chain.Count < 2)
        {
            chainLine.positionCount = 0;
            return;
        }

        chainLine.positionCount = chain.Count;
        for (int i = 0; i < chain.Count; i++)
        {
            if (chain[i] == null) { chainLine.positionCount = 0; return; }
            chainLine.SetPosition(i, TowardCamera(chain[i].transform.position));
        }
    }

    // Grosor en % del alto de pantalla: se ve igual en cualquier cámara o dispositivo
    void UpdateWidths()
    {
        float dist = Mathf.Max(0.1f, Mathf.Abs(gamePlane.GetDistanceToPoint(cam.transform.position)) - drawTowardCamera);
        float visibleHeight = cam.orthographic
            ? cam.orthographicSize * 2f
            : 2f * dist * Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad);

        float trailW = visibleHeight * trailWidthPercent / 100f;
        float lineW = visibleHeight * chainLineWidthPercent / 100f;

        if (Mathf.Abs(trailW - lastTrailWidth) > 0.0001f)
        {
            lastTrailWidth = trailW;
            trail.widthMultiplier = trailW;
        }
        if (Mathf.Abs(lineW - lastLineWidth) > 0.0001f)
        {
            lastLineWidth = lineW;
            chainLine.widthMultiplier = lineW;
        }
    }

    // Le avisa al audio qué tan rápido va el dedo (en alturas de pantalla por segundo)
    void ReportSpeed(Vector2 from, Vector2 to)
    {
        if (GameAudio.Instance == null) return;

        float dt = Time.unscaledDeltaTime;
        if (dt <= 0f) return;

        GameAudio.Instance.OnBladeMoved(Vector2.Distance(from, to) / dt / Mathf.Max(1f, Screen.height));
    }

    // Acerca un punto a la cámara SIN moverlo en pantalla (sigue el rayo de la cámara).
    // Así la línea toca el centro de cada bolita aunque esté al frente.
    Vector3 TowardCamera(Vector3 p)
    {
        if (cam.orthographic)
            return p - cam.transform.forward * drawTowardCamera;

        Vector3 camPos = cam.transform.position;
        Vector3 toPoint = p - camPos;
        float dist = toPoint.magnitude;
        if (dist < 0.001f) return p;

        float newDist = Mathf.Max(0.1f, dist - drawTowardCamera);
        return camPos + toPoint * (newDist / dist);
    }

    // ───────────── Setup ─────────────

    void SetupTrail()
    {
        trail = GetComponent<TrailRenderer>();
        trail.emitting = false;

        if (trail.sharedMaterial == null || trail.sharedMaterial.name.StartsWith("Default"))
            trail.material = new Material(Shader.Find("Sprites/Default"));

        trail.time = 0.15f;
        trail.widthCurve = AnimationCurve.Linear(0f, 1f, 1f, 0f); // gruesa → fina
        trail.numCapVertices = 3;
        trail.startColor = Color.white;
        trail.endColor = new Color(1f, 1f, 1f, 0f);
    }

    void SetupChainLine()
    {
        GameObject go = new GameObject("ChainLine");
        go.transform.SetParent(transform, false);

        chainLine = go.AddComponent<LineRenderer>();
        chainLine.useWorldSpace = true;
        chainLine.material = new Material(Shader.Find("Sprites/Default"));
        chainLine.startColor = chainLineColor;
        chainLine.endColor = chainLineColor;
        chainLine.numCornerVertices = 4;
        chainLine.numCapVertices = 4;
        chainLine.positionCount = 0;
    }

    // ───────────── Input: mouse, táctil y lápiz con el mismo código ─────────────

    // Solo para dibujar la estela (el punto sobre el plano de juego)
    bool TryGetWorldPoint(Vector2 screen, out Vector3 point)
    {
        Ray ray = cam.ScreenPointToRay(screen);
        if (gamePlane.Raycast(ray, out float distance))
        {
            point = ray.GetPoint(distance);
            return true;
        }
        point = default;
        return false;
    }

    // Pointer.current = el último dispositivo usado (mouse, pantalla táctil o lápiz).
    // En pantalla táctil sigue el dedo principal.
    static Vector2 GetPointerPosition()
    {
#if ENABLE_INPUT_SYSTEM
        Pointer p = Pointer.current;
        return p != null ? p.position.ReadValue() : Vector2.zero;
#else
        return Input.mousePosition; // el sistema viejo también simula el mouse con el dedo
#endif
    }

    static bool PressedThisFrame()
    {
#if ENABLE_INPUT_SYSTEM
        Pointer p = Pointer.current;
        return p != null && p.press.wasPressedThisFrame;
#else
        return Input.GetMouseButtonDown(0);
#endif
    }

    static bool IsHeld()
    {
#if ENABLE_INPUT_SYSTEM
        Pointer p = Pointer.current;
        return p != null && p.press.isPressed;
#else
        return Input.GetMouseButton(0);
#endif
    }
}