using UnityEngine;
using TMPro;
using System.Collections.Generic;
using UnityEngine.UI;

public class RecaudacionesController : MonoBehaviour
{
    [Header("Referencias UI")]
    public GameObject canvasRecaudaciones;
    public GameObject canvasMenu;
    public TMP_InputField inputPrecioPopular;
    public TMP_InputField inputPrecioPlatea;
    public TMP_InputField inputPrecioPalco;
    public Transform contenedorTabla; // donde se instancian las filas
    public GameObject prefabFilaTabla; // prefab de una fila

    [Header("Referencias Sistema")]
    public EstadioConfigurator configurator;

    private float precioPopular = 0f;
    private float precioPlatea = 0f;
    private float precioPalco = 0f;

    public Button buttonVolverMenu;

    void Start()
    {
        inputPrecioPopular.onValueChanged.AddListener(OnPrecioChanged);
        inputPrecioPlatea.onValueChanged.AddListener(OnPrecioChanged);
        inputPrecioPalco.onValueChanged.AddListener(OnPrecioChanged);
        buttonVolverMenu.onClick.AddListener(VolverAlMenu);
    }

    void OnPrecioChanged(string valor)
    {
        float.TryParse(inputPrecioPopular.text, out precioPopular);
        float.TryParse(inputPrecioPlatea.text, out precioPlatea);
        float.TryParse(inputPrecioPalco.text, out precioPalco);
        ActualizarTabla();
    }

    public void MostrarPantalla()
    {
        Debug.Log("MostrarPantalla llamado");
        canvasRecaudaciones.SetActive(true);
        canvasMenu.SetActive(false);
        ActualizarTabla();
    }

    public void VolverAlMenu()
    {
        canvasRecaudaciones.SetActive(false);
        canvasMenu.SetActive(true);
    }

    void ActualizarTabla()
    {
        Debug.Log($"ActualizarTabla llamado, variantes consultadas: {configurator.variantesConsultadas.Count}");

        // Limpiar filas existentes pero NO la primera (encabezado)
        for (int i = contenedorTabla.childCount - 1; i >= 1; i--)
            Destroy(contenedorTabla.GetChild(i).gameObject);

        // Generar una fila por variante consultada
        foreach (EstadioConfigurator.DatosVariante datos in configurator.variantesConsultadas)
        {
            Debug.Log($"Generando fila para: {datos.nombre}");

            float recaudacion = (datos.capacidadPopulares * precioPopular) +
                                (datos.capacidadPlateas * precioPlatea) +
                                (datos.capacidadPalcos * precioPalco);

            GameObject fila = Instantiate(prefabFilaTabla, contenedorTabla);
            FilaTablaRecaudaciones filaScript = fila.GetComponent<FilaTablaRecaudaciones>();
            if (filaScript != null)
                filaScript.Configurar(NombresVariantes.ObtenerNombre((EstadioConfigurator.TipoConfiguracion)System.Enum.Parse(typeof(EstadioConfigurator.TipoConfiguracion), datos.nombre)), datos.capacidadTotal,
                    datos.capacidadPopulares, datos.capacidadPlateas,
                    datos.capacidadPalcos, recaudacion);
        }
    }
}