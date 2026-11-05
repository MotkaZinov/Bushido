using UnityEngine;
using UnityEngine.SceneManagement;
 
public class MenuInicial : MonoBehaviour
{
 
    void Awake()
    {
    }
 
    public void Jugar()
    {
        Debug.Log("Jugar");
    }

    public void Opciones()
    {
        Debug.Log("Opciones");
    }

    public void Salir()
    {
        Debug.Log("Salir");
        Application.Quit();
    }
}
