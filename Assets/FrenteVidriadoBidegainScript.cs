using UnityEngine;

public class FrenteVidriadoGenerator : MonoBehaviour
{
    [Header("Prefabs")]
    public GameObject prefabVidrioEntero;
    public GameObject prefabCuartoVidrio;

    [Header("Dimensiones de los prefabs")]
    [Tooltip("Ancho del vidrio entero, en metros")]
    public float anchoVidrioEntero = 2f;
    [Tooltip("Ancho del cuarto de vidrio, en metros")]
    public float anchoCuartoVidrio = 0.5f;
    [Tooltip("Alto de cada fila, en metros")]
    public float altoVidrio = 1.5f;

    [Header("Serie horizontal")]
    [Tooltip("Cantidad de vidrios enteros entre los dos cuartos")]
    public int vidriosEnterosPorFila = 10;

    [Header("Bloque inferior")]
    public int filasBloqueInferior = 4;

    [Header("Bloque superior")]
    public bool generarBloqueSuperior = true;
    public int filasBloqueSuperior = 3;
    [Tooltip("Separacion vertical vacia entre los dos bloques")]
    public float separacionEntreBloques = 2f;

    [Header("Orientacion")]
    [Tooltip("Los prefabs estan acostados: esta rotacion los para")]
    public Vector3 rotacionVidrio = new Vector3(90f, 0f, 0f);

    private const string NOMBRE_CONTENEDOR = "Contenedor_Frente_Vidriado";

    [ContextMenu("Generar Frente Vidriado")]
    public void GenerarFrenteVidriado()
    {
        LimpiarFrenteVidriado();

        if (prefabVidrioEntero == null || prefabCuartoVidrio == null) return;

        GameObject contenedor = new GameObject(NOMBRE_CONTENEDOR);
        contenedor.transform.SetParent(this.transform, false);
        contenedor.tag = "SectorEstadio";

        float y = 0f;

        for (int f = 0; f < filasBloqueInferior; f++)
        {
            GenerarFila(contenedor.transform, y, $"Inferior_{f + 1}");
            y += altoVidrio;
        }

        if (generarBloqueSuperior)
        {
            y += separacionEntreBloques;

            for (int f = 0; f < filasBloqueSuperior; f++)
            {
                GenerarFila(contenedor.transform, y, $"Superior_{f + 1}");
                y += altoVidrio;
            }
        }

        foreach (Transform hijo in contenedor.GetComponentsInChildren<Transform>())
        {
            if (hijo.gameObject != contenedor && Application.isPlaying)
                hijo.gameObject.isStatic = true;
        }

        if (Application.isPlaying)
            StaticBatchingUtility.Combine(contenedor);
    }

    void GenerarFila(Transform padre, float y, string sufijo)
    {
        float anchoTotal = anchoCuartoVidrio * 2f + anchoVidrioEntero * vidriosEnterosPorFila;
        float x = -anchoTotal / 2f;

        // Cuarto inicial
        CrearVidrio(padre, prefabCuartoVidrio,
            x + anchoCuartoVidrio / 2f, y, $"Cuarto_Ini_{sufijo}");
        x += anchoCuartoVidrio;

        // Vidrios enteros
        for (int i = 0; i < vidriosEnterosPorFila; i++)
        {
            CrearVidrio(padre, prefabVidrioEntero,
                x + anchoVidrioEntero / 2f, y, $"Vidrio_{i + 1}_{sufijo}");
            x += anchoVidrioEntero;
        }

        // Cuarto final
        CrearVidrio(padre, prefabCuartoVidrio,
            x + anchoCuartoVidrio / 2f, y, $"Cuarto_Fin_{sufijo}");
    }

    void CrearVidrio(Transform padre, GameObject prefab, float x, float y, string nombre)
    {
        GameObject vidrio = Instantiate(prefab, padre);
        vidrio.name = nombre;
        vidrio.transform.localPosition = new Vector3(x, y + altoVidrio / 2f, 0f);
        vidrio.transform.localRotation = Quaternion.Euler(rotacionVidrio);
    }

    [ContextMenu("Limpiar Frente Vidriado")]
    public void LimpiarFrenteVidriado()
    {
        Transform viejo = transform.Find(NOMBRE_CONTENEDOR);
        if (viejo == null) return;

        if (Application.isPlaying) Destroy(viejo.gameObject);
        else DestroyImmediate(viejo.gameObject);
    }
}
