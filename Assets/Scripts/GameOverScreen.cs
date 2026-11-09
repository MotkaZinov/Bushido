using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Va en la escena GameOver (en un objeto vacío 'GameOverController').
/// Muestra el puntaje y el mejor combo, y conecta los botones Reiniciar y Salir.
/// </summary>
public class GameOverScreen : MonoBehaviour
{
    [Header("Textos (TextMeshPro)")]
    public TMP_Text scoreText;
    public TMP_Text comboText;
    public string scoreFormat = "{0}";
    public string comboFormat = "Combo máx: x{0:0.#}";

    [Header("Botones")]
    public Button restartButton;
    public Button quitButton;

    [Header("Escenas (deben estar en Build Profiles > Scene List)")]
    public string gameplayScene = "Gameplay";
    [Tooltip("Apagado: 'Salir' cierra el juego. Encendido: 'Salir' vuelve al menú principal")]
    public bool quitGoesToMainMenu = false;
    public string mainMenuScene = "MainMenu";

    void Start()
    {
        Time.timeScale = 1f;

        if (scoreText != null) scoreText.text = string.Format(scoreFormat, GameResults.Score);
        if (comboText != null) comboText.text = string.Format(comboFormat, GameResults.BestMultiplier);

        if (restartButton != null) restartButton.onClick.AddListener(Restart);
        if (quitButton != null) quitButton.onClick.AddListener(Quit);
    }

    public void Restart()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(gameplayScene); // al recargar la escena todo vuelve a empezar de cero
    }

    public void Quit()
    {
        if (quitGoesToMainMenu)
        {
            SceneManager.LoadScene(mainMenuScene);
            return;
        }

#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false; // en el editor: detiene el Play
#else
        Application.Quit();
#endif
    }
}