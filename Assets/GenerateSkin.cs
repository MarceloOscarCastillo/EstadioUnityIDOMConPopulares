using UnityEngine;

public class PielEstadio : MonoBehaviour
{
    [Header("Filtros de Contenedores")]
    public string tagContenedores = "SectorEstadio";

    [Header("Prefab")]
    public GameObject prefabPiel;

    [Header("Dimensiones")]
    public float yInicio = 6.5f;
    public float offsetY = -3f;
    public float offsetZ = 0f;
    public float largoFrente = 180f;
    public float anchoPrefab = 8f;

    private const string NOMBRE_CONTENEDOR = "Contenedor_Piel";

    [ContextMenu("Generar Piel")]
    public void GenerarPiel()
    {
        if (prefabPiel == null) return;

        Transform contenedorViejo = transform.Find(NOMBRE_CONTENEDOR);
        if (contenedorViejo != null) DestroyImmediate(contenedorViejo.gameObject);

        GameObject contenedor = new GameObject(NOMBRE_CONTENEDOR);
        contenedor.transform.SetParent(this.transform);
        contenedor.tag = tagContenedores;

        // Usar el transform del controller como referencia
        Vector3 dirX = transform.right;
        Vector3 origen = transform.position - dirX * (largoFrente / 2f);
        origen.y = transform.position.y + yInicio + offsetY;

        int cantidadModulos = Mathf.FloorToInt(largoFrente / anchoPrefab);
        float offsetInicio = (largoFrente - cantidadModulos * anchoPrefab) / 2f;

        for (int i = 0; i < cantidadModulos; i++)
        {
            float xBase = offsetInicio + i * anchoPrefab + anchoPrefab / 2f;
            Vector3 posicion = origen + dirX * xBase + transform.forward * offsetZ;

            Instantiate(prefabPiel, posicion, transform.rotation, contenedor.transform);
        }
    }

    [ContextMenu("Limpiar Piel")]
    public void LimpiarPiel()
    {
        Transform contenedor = transform.Find(NOMBRE_CONTENEDOR);
        if (contenedor != null) DestroyImmediate(contenedor.gameObject);
    }
}
