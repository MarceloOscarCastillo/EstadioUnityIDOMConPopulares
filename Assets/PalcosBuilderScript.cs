using UnityEngine;

public class PalcosBuilderScript : MonoBehaviour
{
    [Header("Configuración del Palco")]
    public GameObject Palco; // Tu cubo grande
    public int palcosPorFila = 16;
    public int cantidadFilas = 2;

    [Header("Ajustes de Espaciado")]
    public Vector3 margenEntrePalcos = Vector3.zero; // Por si querés dejar un huequito

    [Header("Filtros de Contenedores")]
    public string tagContenedores = "SectorEstadio";

    void Start()
    {
        //GenerarPalcos();
    }

    [ContextMenu("Generar Palcos")]
    public void GenerarPalcos()
    {
        if (Palco == null) return;

        // LIMPIEZA: Buscar si ya existe un "Palcos" y borrarlo
        Transform anterior = this.transform.Find("Palcos");
        if (anterior != null)
        {
            if (Application.isPlaying) Destroy(anterior.gameObject);
            else DestroyImmediate(anterior.gameObject);
        }

        // 1. Obtenemos el tamaño del palco automáticamente
        // Usamos el Renderer para saber cuánto mide el cubo
        MeshRenderer renderer = Palco.GetComponentInChildren<MeshRenderer>();
        Vector3 tamanoPalco = renderer != null ? renderer.bounds.size : Vector3.one;

        // 2. Creamos un contenedor para no ensuciar la Hierarchy
        GameObject contenedor = new GameObject("Palcos");
        contenedor.transform.SetParent(this.transform);
        contenedor.transform.localPosition = Vector3.zero;
        contenedor.transform.localScale = Vector3.one;
        contenedor.tag = "SectorEstadio";

        for (int fila = 0; fila < cantidadFilas; fila++)
        {
            for (int i = 0; i < palcosPorFila; i++)
            {
                // 3. Calculamos la posición
                // X: se desplaza según el ancho del palco
                // Y: se desplaza según la altura del palco (fila 0 abajo, fila 1 arriba)
                float posX = i * (tamanoPalco.x + margenEntrePalcos.x);
                float posY = fila * (tamanoPalco.y + margenEntrePalcos.y);
                float posZ = 0; // Se mantienen alineados en Z

                Vector3 posicionLocal = new Vector3(posX, posY, posZ);

                // 4. Instanciamos
                GameObject nuevoPalco = Instantiate(Palco, contenedor.transform);

                // Usamos TransformPoint para que respete la posición y rotación del Controller
                nuevoPalco.transform.position = transform.TransformPoint(posicionLocal);
                nuevoPalco.transform.rotation = transform.rotation;
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
}
