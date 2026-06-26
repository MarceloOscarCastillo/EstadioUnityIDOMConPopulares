using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class UIEstadioController : MonoBehaviour
{
    [Header("Referencias UI")]
    public GameObject panelCarga;
    public TextMeshProUGUI textGenerando;

    public GameObject panelStats;
    public TextMeshProUGUI textCapacidad;
    public TextMeshProUGUI textPopulares;
    public TextMeshProUGUI textPlateas;
    public TextMeshProUGUI textPalcos;
    public Button buttonHome;

    [Header("Referencias de Sistema")]
    public EstadioConfigurator configurator;
    public ContadorDeCapacidad contador;

    [Header("Referencias Menu")]
    public GameObject canvasMenu;

    void Start()
    {
        // Estado inicial: mostrando carga
        panelCarga.SetActive(false);
        panelStats.SetActive(false);

        buttonHome.onClick.AddListener(OnHomeClick);
    }

    public void MostrarCarga()
    {
        panelCarga.SetActive(true);
        panelStats.SetActive(false);
    }

    public void MostrarStats()
    {
        panelCarga.SetActive(false);
        panelStats.SetActive(true);

        ActualizarTextos();
    }

    public void ActualizarTextos()
    {
        textCapacidad.text = $"Capacidad: {contador.capacidadTotal:N0}";
        textPopulares.text = $"Populares: {contador.capacidadPopulares:N0}";
        textPlateas.text = $"Plateas: {contador.capacidadPlateas:N0}";
        textPalcos.text = $"Palcos: {contador.capacidadPalcos:N0}";
    }

    void OnHomeClick()
    {
        panelStats.SetActive(false);
        if (canvasMenu != null)
            canvasMenu.SetActive(true);
        else
        {
            // Fallback: buscar por nombre
            GameObject menu = GameObject.Find("Canvas_Menu");
            if (menu != null) menu.SetActive(true);
        }
    }
}