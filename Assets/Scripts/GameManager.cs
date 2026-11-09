using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Estado de la partida. Va en la escena Gameplay (un objeto vacío 'GameManager').
/// Un enemigo peligroso (Enemy con 'causesGameOver') llama a GameOver():
/// se congela todo, suena un golpe, tiembla la cámara y, tras 'delayBeforeScene' segundos,
/// se carga la escena de Game Over. El puntaje final viaja en GameResults.
/// </summary>
public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }
    public static bool IsGameOver { get; private set; }

    [Header("Escenas (deben estar en Build Profiles > Scene List)")]
    public string gameOverScene = "GameOver";

    [Header("Game Over")]
    [Tooltip("Segundos reales que se queda todo congelado antes de cambiar de escena")]
    public float delayBeforeScene = 1.2f;
    [Tooltip("Fuerza de la sacudida de cámara al morir (0 a 1)")]
    [Range(0f, 1f)] public float shakeTrauma = 1f;

    bool triggered;

    void Awake()
    {
        Instance = this;
        IsGameOver = false;
        Time.timeScale = 1f; // por si venimos de una escena que dejó el tiempo detenido
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    public void GameOver()
    {
        if (triggered) return;
        triggered = true;
        IsGameOver = true;

        // Guardamos los resultados para mostrarlos en la pantalla de Game Over
        ScoreManager score = ScoreManager.Instance;
        GameResults.Score = score != null ? score.Score : 0;
        GameResults.BestMultiplier = score != null ? score.BestMultiplier : 1f;
        GameResults.BestChain = score != null ? score.BestChain : 0;

        if (GameAudio.Instance != null) GameAudio.Instance.OnGameOver();

        CameraRig rig = FindFirstObjectByType<CameraRig>();
        if (rig != null) rig.AddTrauma(shakeTrauma);

        Time.timeScale = 0f; // todo se pausa (la sacudida y el sonido siguen)
        StartCoroutine(LoadGameOverScene());
    }

    IEnumerator LoadGameOverScene()
    {
        yield return new WaitForSecondsRealtime(delayBeforeScene);

        Time.timeScale = 1f; // importante: el timeScale NO se reinicia solo al cambiar de escena

        if (!Application.CanStreamedLevelBeLoaded(gameOverScene))
        {
            Debug.LogError($"GameManager: la escena '{gameOverScene}' no está en Build Profiles > Scene List " +
                           "(o el nombre no coincide). Se reinicia el nivel para no quedarse trabado.");
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
            yield break;
        }

        SceneManager.LoadScene(gameOverScene);
    }
}

/// <summary>Datos de la última partida, para la pantalla de Game Over.</summary>
public static class GameResults
{
    public static int Score;
    public static float BestMultiplier = 1f;
    public static int BestChain;
}