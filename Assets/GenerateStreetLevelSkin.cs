using UnityEngine;

public class PielEstadioNivelCalle : MonoBehaviour
{
    [Header("Referencias")]
    public GameObject frontBuilding;
    public GameObject prefabPiel; // prefab completo con cuadros + plane con material

    [Header("Dimensiones")]
    public float yInicio = 6.5f;
    public float offsetY = -3f;
    public float offsetZ = 0f;
    public float offsetFrente = 8f;

    private const string NOMBRE_CONTENEDOR = "Contenedor_Piel";

    [ContextMenu("Generar Piel")]
    public void GenerarPiel()
    {
        if (frontBuilding == null || prefabPiel == null) return;

        Transform contenedorViejo = transform.Find(NOMBRE_CONTENEDOR);
        if (contenedorViejo != null) DestroyImmediate(contenedorViejo.gameObject);

        GameObject contenedor = new GameObject(NOMBRE_CONTENEDOR);
        contenedor.transform.SetParent(this.transform);

        Renderer r = frontBuilding.GetComponent<Renderer>();
        float largoFrente = r.bounds.size.z;

        Vector3 dirX = frontBuilding.transform.right;
        Vector3 caraFrontal = r.bounds.center + frontBuilding.transform.forward * (r.bounds.size.x / 2f)
                            - frontBuilding.transform.forward * offsetFrente;

        Vector3 origen = caraFrontal - dirX * (largoFrente / 2f);
        origen.y = frontBuilding.transform.position.y + yInicio + offsetY;

        // El prefab mide 8m, instanciar a lo largo del frente
        float anchoPrefab = 8f;
        int cantidadModulos = Mathf.FloorToInt(largoFrente / anchoPrefab);
        float offsetInicio = (largoFrente - cantidadModulos * anchoPrefab) / 2f;

        for (int i = 0; i < cantidadModulos; i++)
        {
            float xBase = offsetInicio + i * anchoPrefab + anchoPrefab / 2f;

            Vector3 posicion = origen + dirX * xBase + Vector3.forward * offsetZ;

            Instantiate(prefabPiel, posicion, frontBuilding.transform.rotation, contenedor.transform);
        }
    }

    [ContextMenu("Limpiar Piel")]
    public void LimpiarPiel()
    {
        Transform contenedor = transform.Find(NOMBRE_CONTENEDOR);
        if (contenedor != null) DestroyImmediate(contenedor.gameObject);
    }
}