using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class DetalleCapacidadController : MonoBehaviour
{
    [Header("Referencias UI")]
    public GameObject canvasDetalle;
    public TextMeshProUGUI textoDetalle;
    public Button buttonVolver;

    [Header("Referencias Sistema")]
    public ContadorDeCapacidad contador;
    public GameObject canvasAnterior;

    void Start()
    {
        canvasDetalle.SetActive(false);

        if (buttonVolver != null)
            buttonVolver.onClick.AddListener(Ocultar);
    }

    public void MostrarPantalla()
    {
        if (contador == null) return;

        contador.CalcularCapacidad();
        textoDetalle.text = contador.ArmarTextoDetalle();

        canvasDetalle.SetActive(true);
    }

    void Ocultar()
    {
        canvasDetalle.SetActive(false);
        if (canvasAnterior != null) canvasAnterior.SetActive(true);
    }
}
