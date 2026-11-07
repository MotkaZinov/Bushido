using System.Collections.Generic;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Navaja del jugador. Al pasar sobre yokais los va MARCANDO y los une con una línea.
/// El corte se ejecuta al soltar el click o cuando pasa 'chainTimeout' segundos
/// desde el primer enemigo marcado. Hacen falta al menos 'minChain' enemigos.
/// Si un enemigo marcado sale de la pantalla, la cadena se rompe.
/// </summary>
[RequireComponent(typeof(TrailRenderer))]
public class BladeController : MonoBehaviour
{
    [Header("Referencias")]
    public Camera cam;

    [Header("Corte")]
    public float bladeRadius = 0.3f;
    [Tooltip("Mínimo de enemigos marcados para que el corte funcione")]
    public int minChain = 2;
    [Tooltip("Segundos desde el PRIMER enemigo marcado hasta que se ejecuta el corte")]
    public float chainTimeout = 1f;

    [Header("Línea de unión")]
    public Color chainLineColor = new Color(1f, 0.85f, 0.2f);
    public float chainLineWidth = 0.12f;
    [Tooltip("Negativo = más cerca de la cámara, para que se vea sobre las bolitas")]
    public float chainLineZOffset = -0.6f;

    readonly List<Enemy> chain = new List<Enemy>(); // en orden de toque
    readonly Collider[] hitBuffer = new Collider[32];
    readonly Plane gamePlane = new Plane(Vector3.back, Vector3.zero);

    TrailRenderer trail;
    LineRenderer chainLine;
    bool slicing;
    Vector3 lastPos;
    float chainStartTime;

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
        if (PressedThisFrame())
            StartSlice();
        else if (IsHeld() && slicing)
            ContinueSlice();
        else if (slicing)
            EndSlice();

        UpdateChain();
        UpdateChainLine();
    }

    // ───────────── Estados del corte ─────────────

    void StartSlice()
    {
        if (!TryGetWorldPoint(out Vector3 p)) return;

        slicing = true;
        transform.position = p;
        lastPos = p;
        trail.Clear();
        trail.emitting = true;
    }

    void ContinueSlice()
    {
        if (!TryGetWorldPoint(out Vector3 p)) return;

        transform.position = p;
        DetectHits(lastPos, p);
        lastPos = p;
    }

    void EndSlice()
    {
        slicing = false;
        trail.emitting = false;

        // Al soltar el click se ejecuta el corte
        if (chain.Count > 0) ResolveChain();
    }

    // ───────────── Detección y cadena ─────────────

    void DetectHits(Vector3 from, Vector3 to)
    {
        int count = Physics.OverlapCapsuleNonAlloc(from, to, bladeRadius, hitBuffer);

        for (int i = 0; i < count; i++)
        {
            Enemy e = hitBuffer[i].GetComponent<Enemy>();
            if (e == null || e.IsMarked) continue;

            if (chain.Count == 0) chainStartTime = Time.time; // arranca el reloj
            e.Mark();
            chain.Add(e);
        }
    }

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

        int count = chain.Count;
        int baseScore = 0;
        foreach (Enemy e in chain) baseScore += e.points;

        ScoreManager score = ScoreManager.Instance;
        float multiplier = score != null ? score.GetMultiplier(count) : 1f;

        foreach (Enemy e in chain) e.Die();
        chain.Clear();

        if (score != null) score.AddChain(baseScore, count, multiplier);
    }

    void BreakChain()
    {
        foreach (Enemy e in chain)
            if (e != null) e.Unmark();

        chain.Clear();
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

            Vector3 p = chain[i].transform.position;
            p.z += chainLineZOffset;
            chainLine.SetPosition(i, p);
        }
    }

    // ───────────── Setup ─────────────

    void SetupTrail()
    {
        trail = GetComponent<TrailRenderer>();
        trail.emitting = false;

        if (trail.sharedMaterial == null || trail.sharedMaterial.name.StartsWith("Default"))
            trail.material = new Material(Shader.Find("Sprites/Default"));

        trail.time = 0.15f;
        trail.startWidth = 0.25f;
        trail.endWidth = 0f;
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
        chainLine.widthMultiplier = chainLineWidth;
        chainLine.numCornerVertices = 4;
        chainLine.numCapVertices = 4;
        chainLine.positionCount = 0;
    }

    // ───────────── Utilidades de input ─────────────

    bool TryGetWorldPoint(out Vector3 point)
    {
        Ray ray = cam.ScreenPointToRay(GetMousePosition());
        if (gamePlane.Raycast(ray, out float distance))
        {
            point = ray.GetPoint(distance);
            return true;
        }
        point = default;
        return false;
    }

    static Vector2 GetMousePosition()
    {
#if ENABLE_INPUT_SYSTEM
        return Mouse.current != null ? Mouse.current.position.ReadValue() : Vector2.zero;
#else
        return Input.mousePosition;
#endif
    }

    static bool PressedThisFrame()
    {
#if ENABLE_INPUT_SYSTEM
        return Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame;
#else
        return Input.GetMouseButtonDown(0);
#endif
    }

    static bool IsHeld()
    {
#if ENABLE_INPUT_SYSTEM
        return Mouse.current != null && Mouse.current.leftButton.isPressed;
#else
        return Input.GetMouseButton(0);
#endif
    }
}