using UnityEngine;
using TMPro;

public class FilaTablaRecaudaciones : MonoBehaviour
{
    public TextMeshProUGUI textNombre;
    public TextMeshProUGUI textCapacidadTotal;
    public TextMeshProUGUI textPopulares;
    public TextMeshProUGUI textPlateas;
    public TextMeshProUGUI textPalcos;
    public TextMeshProUGUI textRecaudacion;

    public void Configurar(string nombre, int capacidadTotal, int populares,
        int plateas, int palcos, float recaudacion)
    {
        textNombre.text = nombre;
        textCapacidadTotal.text = capacidadTotal.ToString("N0");
        textPopulares.text = populares.ToString("N0");
        textPlateas.text = plateas.ToString("N0");
        textPalcos.text = palcos.ToString("N0");
        textRecaudacion.text = recaudacion.ToString("C0"); // formato moneda
    }
}