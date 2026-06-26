using UnityEngine;

public class CampoDeJuego : MonoBehaviour
{
    [Header("Referencias")]
    public Transform futbolField;
    public GameObject prefabCartel;
    public float largoFilaNorteSur = 76f;  // largo real de las filas detras de los arcos
    public float largoFilaEsteOeste = 114f; // largo real de las filas laterales

    [Header("Configuración de Carteles")]
    public float distanciaAlBordeZ = 12f;
    public float distanciaAlBordeX = 3f;
    public float separacionEntreCarteles = 0.1f;
    public float largoCartel = 2.0f;
    public float alturaCartel = 1f;

    [Header("Bordes Activos")]
    public bool cartelesBordeNorte = true;
    public bool cartelesBordeSur = true;
    public bool cartelesBordeEste = false;
    public bool cartelesBordeOeste = false;

    private const string NOMBRE_CONTENEDOR = "Contenedor_Carteles";

    [ContextMenu("Generar Carteles")]
    public void GenerarCarteles()
    {
        if (futbolField == null || prefabCartel == null) return;

        Transform contenedorViejo = transform.Find(NOMBRE_CONTENEDOR);
        if (contenedorViejo != null) DestroyImmediate(contenedorViejo.gameObject);

        GameObject contenedor = new GameObject(NOMBRE_CONTENEDOR);
        contenedor.transform.SetParent(this.transform);

        Vector3 dirX = futbolField.right;
        Vector3 dirZ = futbolField.forward;

        Renderer r = futbolField.GetComponent<Renderer>();
        Vector3 centro = r.bounds.center;
        float largoX = r.bounds.size.x;

        float largoZ = r.bounds.size.z;

        
        float yBase = r.bounds.min.y + alturaCartel /2f;

        UnityEngine.Debug.Log($"bounds.center={r.bounds.center}, bounds.size={r.bounds.size}, bounds.min={r.bounds.min}, bounds.max={r.bounds.max}");
        UnityEngine.Debug.Log($"futbolField.position={futbolField.position}");

        // Centros de cada borde
        Vector3 centroNorte = centro + dirZ * (largoZ / 2f-distanciaAlBordeZ);
          
        Vector3 centroSur = centro - dirZ * (largoZ / 2f - distanciaAlBordeZ);
        Vector3 centroEste = centro + dirX * (largoX / 2f - distanciaAlBordeX);
        Vector3 centroOeste = centro - dirX * (largoX / 2f - distanciaAlBordeX);

        centroNorte.y = yBase;
        centroSur.y = yBase;
        centroEste.y = yBase;
        centroOeste.y = yBase;

        if (cartelesBordeNorte)
            GenerarFilaCarteles(contenedor, centroNorte, dirX, largoFilaNorteSur,
                Quaternion.LookRotation(-dirZ, Vector3.up));

        if (cartelesBordeSur)
            GenerarFilaCarteles(contenedor, centroSur, dirX, largoFilaNorteSur,
                Quaternion.LookRotation(dirZ, Vector3.up));

        if (cartelesBordeEste)
            GenerarFilaCarteles(contenedor, centroEste, dirZ, largoFilaEsteOeste,
                Quaternion.LookRotation(-dirX, Vector3.up));

        if (cartelesBordeOeste)
            GenerarFilaCarteles(contenedor, centroOeste, dirZ, largoFilaEsteOeste,
                Quaternion.LookRotation(dirX, Vector3.up));
    }

    void GenerarFilaCarteles(GameObject contenedor, Vector3 centroBorde, Vector3 dirFila, float largo, Quaternion rotacion)
    {
        float espacioTotal = largoCartel + separacionEntreCarteles;
        int cantidad = Mathf.FloorToInt(largo / espacioTotal);
        float offset = (largo - cantidad * espacioTotal + separacionEntreCarteles) / 2f;

        for (int i = 0; i < cantidad; i++)
        {
            float t = -largo / 2f + offset + i * espacioTotal + largoCartel / 2f;
            Vector3 posicion = centroBorde + dirFila * t;

            Instantiate(prefabCartel, posicion, rotacion, contenedor.transform);
        }
    }

    [ContextMenu("Limpiar Carteles")]
    public void LimpiarCarteles()
    {
        Transform contenedor = transform.Find(NOMBRE_CONTENEDOR);
        if (contenedor != null) DestroyImmediate(contenedor.gameObject);
    }
}