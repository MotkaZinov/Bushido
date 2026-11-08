using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Genera yokais fuera de la pantalla y los manda hacia otro borde.
/// Soporta enemigos especiales y el modo frenesí (muchos enemigos por un tiempo).
/// Asume que el juego ocurre en el plano Z = 0 y la cámara mira hacia +Z.
/// </summary>
public class EnemySpawner : MonoBehaviour
{
    [System.Serializable]
    public class SpecialSpawn
    {
        public Enemy prefab;
        [Tooltip("Probabilidad (0 a 1) de que cada aparición sea este enemigo")]
        [Range(0f, 1f)] public float chance = 0.08f;
    }

    [Header("Referencias")]
    public Enemy enemyPrefab;
    public Camera cam;

    [Header("Enemigos especiales")]
    public List<SpecialSpawn> specialEnemies = new List<SpecialSpawn>();
    [Tooltip("Si está apagado, no salen especiales mientras dura un frenesí")]
    public bool specialsDuringFrenzy = false;

    [Header("Aparición normal")]
    public Vector2 spawnInterval = new Vector2(0.6f, 1.6f); // min / max segundos
    public int maxAlive = 8;
    [Tooltip("Qué tan afuera de la pantalla aparecen (0.1 = 10% del tamaño)")]
    public float offscreenMargin = 0.1f;

    [Header("Movimiento")]
    public Vector2 speedRange = new Vector2(2f, 4f);         // unidades por segundo
    public Vector2 amplitudeRange = new Vector2(0.5f, 1.5f); // qué tanto se mueve de lado
    public Vector2 frequencyRange = new Vector2(1.5f, 3.5f); // qué tan rápido oscila

    [Header("Modo frenesí")]
    public int frenzyMaxAlive = 50;
    public Vector2 frenzySpawnInterval = new Vector2(0.05f, 0.2f);
    [Tooltip("Multiplica la velocidad durante el frenesí. 1 = igual, menor = más lentos")]
    public float frenzySpeedMultiplier = 0.6f;

    public bool InFrenzy => Time.time < frenzyEndTime;
    public float FrenzyTimeLeft => Mathf.Max(0f, frenzyEndTime - Time.time);

    readonly Plane gamePlane = new Plane(Vector3.back, Vector3.zero);
    float nextSpawnTime;
    float frenzyEndTime;

    // Valores actuales (los normales vuelven solos cuando termina el frenesí)
    int CurrentMaxAlive => InFrenzy ? frenzyMaxAlive : maxAlive;
    Vector2 CurrentInterval => InFrenzy ? frenzySpawnInterval : spawnInterval;
    float CurrentSpeedMultiplier => InFrenzy ? frenzySpeedMultiplier : 1f;

    // Lados: 0 = izquierda, 1 = derecha, 2 = arriba, 3 = abajo
    void OnEnable()  { Enemy.Killed += OnEnemyKilled; }
    void OnDisable() { Enemy.Killed -= OnEnemyKilled; }

    void Start()
    {
        if (cam == null) cam = Camera.main;
        ScheduleNext();
    }

    void Update()
    {
        if (Time.time < nextSpawnTime) return;
        ScheduleNext();

        if (Enemy.Alive >= CurrentMaxAlive) return;
        Spawn();
    }

    // ───────────── Frenesí ─────────────

    void OnEnemyKilled(Enemy e)
    {
        if (e.triggersFrenzy) StartFrenzy(e.frenzyDuration);
    }

    public void StartFrenzy(float duration)
    {
        if (GameAudio.Instance != null) GameAudio.Instance.OnFrenzyStart();

        // Si ya había un frenesí, se renueva el tiempo (no se suma)
        frenzyEndTime = Mathf.Max(frenzyEndTime, Time.time + duration);

        // Que empiecen a salir de inmediato, sin esperar el intervalo anterior
        float soon = Time.time + Random.Range(frenzySpawnInterval.x, frenzySpawnInterval.y);
        nextSpawnTime = Mathf.Min(nextSpawnTime, soon);
    }

    // ───────────── Aparición ─────────────

    void ScheduleNext()
    {
        Vector2 interval = CurrentInterval;
        nextSpawnTime = Time.time + Random.Range(interval.x, interval.y);
    }

    Enemy PickPrefab()
    {
        if (!InFrenzy || specialsDuringFrenzy)
        {
            foreach (SpecialSpawn s in specialEnemies)
            {
                if (s.prefab != null && Random.value < s.chance)
                    return s.prefab;
            }
        }
        return enemyPrefab;
    }

    void Spawn()
    {
        int startSide = Random.Range(0, 4);
        int endSide = PickEndSide(startSide);

        Vector3 start = ViewportToPlane(EdgePoint(startSide));
        Vector3 end = ViewportToPlane(EdgePoint(endSide));

        Enemy e = Instantiate(PickPrefab(), start, Quaternion.identity);
        e.Init(
            start, end,
            Random.Range(speedRange.x, speedRange.y) * CurrentSpeedMultiplier,
            Random.Range(amplitudeRange.x, amplitudeRange.y),
            Random.Range(frequencyRange.x, frequencyRange.y)
        );
    }

    // 70% cruza al lado opuesto, 30% sale por un lado adyacente (diagonal)
    int PickEndSide(int startSide)
    {
        if (Random.value < 0.7f)
        {
            return startSide switch { 0 => 1, 1 => 0, 2 => 3, _ => 2 };
        }
        return startSide < 2 ? Random.Range(2, 4) : Random.Range(0, 2);
    }

    // Punto en coordenadas de viewport (0..1) sobre un borde, un poco fuera de pantalla
    Vector2 EdgePoint(int side)
    {
        float along = Random.Range(0.15f, 0.85f);
        float m = offscreenMargin;

        return side switch
        {
            0 => new Vector2(-m, along),
            1 => new Vector2(1f + m, along),
            2 => new Vector2(along, 1f + m),
            _ => new Vector2(along, -m),
        };
    }

    Vector3 ViewportToPlane(Vector2 viewport)
    {
        Ray ray = cam.ViewportPointToRay(new Vector3(viewport.x, viewport.y, 0f));
        gamePlane.Raycast(ray, out float distance);
        return ray.GetPoint(distance);
    }
}