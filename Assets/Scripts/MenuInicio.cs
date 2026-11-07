using UnityEngine;
using UnityEngine.SceneManagement;
 
public class MenuInicial : MonoBehaviour
{
    [SerializeField] private string Gameplay = "Gameplay";
    [SerializeField] private string MainMenu = "MainMenu";
    public GameObject PanelMenu;

    void Awake()
    {
    }
 
    public void Jugar()
    {
        Debug.Log("Jugar");
        SceneManager.LoadScene(Gameplay);
    }

    public void Opciones()
    {
        Debug.Log("Opciones");
        PanelMenu.SetActive(true);
    }

    public void Salir()
    {
        Debug.Log("Salir");
        Application.Quit();
    }

    public void SalirMenu()
    {
        PanelMenu.SetActive(false);
    }

    public void Pausa()
    {
        PanelMenu.SetActive(true);
    }

    public void Volver()
    {
        SceneManager.LoadScene(MainMenu);
    }
}
