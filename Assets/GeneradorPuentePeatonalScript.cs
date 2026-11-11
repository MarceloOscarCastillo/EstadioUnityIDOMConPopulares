using UnityEngine;

public class PuentePeatonalGenerator : MonoBehaviour
{
    [Header("Losa")]
    public float largoLosa = 30f;
    public float anchoLosa = 3f;
    public float espesorLosa = 0.25f;
    [Tooltip("Altura de la cara superior de la losa")]
    public float alturaLosa = 6f;
    public Material materialLosa;

    [Header("Barandas")]
    public bool generarBarandas = true;
    public float alturaBaranda = 1.1f;
    public float espesorBaranda = 0.2f;
    public Material materialBaranda;

    [Header("Vigas longitudinales")]
    public bool generarVigasLongitudinales = true;
    public float anchoVigaLong = 0.3f;
    public float altoVigaLong = 0.5f;
    public Material materialVigas;

    [Header("Vigas transversales y columnas")]
    public bool generarColumnas = true;
    [Tooltip("Separacion entre pares de columnas, en metros")]
    public float separacionColumnas = 5f;
    public float anchoVigaTransv = 0.3f;
    public float altoVigaTransv = 0.4f;
    public float ladoColumna = 0.4f;
    [Tooltip("Hasta donde bajan las columnas")]
    public float alturaBaseColumnas = 0f;
    public Material materialColumnas;


    [Header("Vano libre para acceso vehicular")]
    public bool hayVanoLibre = false;
    [Tooltip("Distancia desde el inicio del puente donde empieza el vano, en metros")]
    public float inicioVano = 10f;
    public float anchoVano = 8f;
    [Tooltip("Si es true, tampoco se genera la viga transversal en el vano")]
    public bool omitirTransversalEnVano = false;



    private const string NOMBRE_CONTENEDOR = "Contenedor_Puente";

    [ContextMenu("Generar Puente")]
    public void GenerarPuente()
    {
        LimpiarPuente();

        GameObject contenedor = new GameObject(NOMBRE_CONTENEDOR);
        contenedor.transform.SetParent(this.transform, false);
        contenedor.tag = "SectorEstadio";

        GenerarLosa(contenedor.transform);
        if (generarBarandas) GenerarBarandas(contenedor.transform);
        if (generarVigasLongitudinales) GenerarVigasLong(contenedor.transform);
        if (generarColumnas) GenerarColumnasYTransversales(contenedor.transform);

        foreach (Transform hijo in contenedor.GetComponentsInChildren<Transform>())
        {
            if (hijo.gameObject != contenedor && Application.isPlaying)
                hijo.gameObject.isStatic = true;
        }

        if (Application.isPlaying)
            StaticBatchingUtility.Combine(contenedor);
    }

    void GenerarLosa(Transform padre)
    {
        CrearCubo(padre, "Losa",
            new Vector3(0f, alturaLosa - espesorLosa / 2f, 0f),
            new Vector3(largoLosa, espesorLosa, anchoLosa),
            materialLosa);
    }

    void GenerarBarandas(Transform padre)
    {
        float z = anchoLosa / 2f - espesorBaranda / 2f;
        float y = alturaLosa + alturaBaranda / 2f;

        foreach (float lado in new float[] { -z, z })
        {
            CrearCubo(padre, "Baranda",
                new Vector3(0f, y, lado),
                new Vector3(largoLosa, alturaBaranda, espesorBaranda),
                materialBaranda != null ? materialBaranda : materialLosa);
        }
    }

    void GenerarVigasLong(Transform padre)
    {
        float z = anchoLosa / 2f - anchoVigaLong / 2f;
        float y = alturaLosa - espesorLosa - altoVigaLong / 2f;

        foreach (float lado in new float[] { -z, z })
        {
            CrearCubo(padre, "Viga_Longitudinal",
                new Vector3(0f, y, lado),
                new Vector3(largoLosa, altoVigaLong, anchoVigaLong),
                materialVigas);
        }
    }

    void GenerarColumnasYTransversales(Transform padre)
    {
        if (separacionColumnas <= 0f) return;

        int cantidad = Mathf.Max(2, Mathf.RoundToInt(largoLosa / separacionColumnas) + 1);
        float paso = largoLosa / (cantidad - 1);
        float xIni = -largoLosa / 2f;

        float yTransv = alturaLosa - espesorLosa - altoVigaLong - altoVigaTransv / 2f;
        float yTopeColumna = yTransv - altoVigaTransv / 2f;
        float alturaColumna = yTopeColumna - alturaBaseColumnas;

        float zColumna = anchoLosa / 2f - ladoColumna / 2f;

        for (int i = 0; i < cantidad; i++)
        {
            float x = xIni + i * paso;

            float distanciaDesdeInicio = x - xIni;

            bool enVano = hayVanoLibre
                       && distanciaDesdeInicio > inicioVano
                       && distanciaDesdeInicio < inicioVano + anchoVano;

            if (!enVano || !omitirTransversalEnVano)
            {
                CrearCubo(padre, "Viga_Transversal",
                    new Vector3(x, yTransv, 0f),
                    new Vector3(anchoVigaTransv, altoVigaTransv, anchoLosa),
                    materialVigas);
            }

            if (enVano) continue;
          
            // Par de columnas
            if (alturaColumna <= 0f) continue;

            foreach (float lado in new float[] { -zColumna, zColumna })
            {
                CrearCubo(padre, "Columna",
                    new Vector3(x, alturaBaseColumnas + alturaColumna / 2f, lado),
                    new Vector3(ladoColumna, alturaColumna, ladoColumna),
                    materialColumnas != null ? materialColumnas : materialVigas);
            }
        }
    }

    void CrearCubo(Transform padre, string nombre, Vector3 pos, Vector3 escala, Material mat)
    {
        GameObject c = GameObject.CreatePrimitive(PrimitiveType.Cube);
        c.name = nombre;
        c.transform.SetParent(padre, false);
        c.transform.localPosition = pos;
        c.transform.localScale = escala;

        Collider col = c.GetComponent<Collider>();
        if (col != null)
        {
            if (Application.isPlaying) Destroy(col);
            else DestroyImmediate(col);
        }

        if (mat != null) c.GetComponent<Renderer>().sharedMaterial = mat;
    }

    [ContextMenu("Limpiar Puente")]
    public void LimpiarPuente()
    {
        Transform viejo = transform.Find(NOMBRE_CONTENEDOR);
        if (viejo == null) return;

        if (Application.isPlaying) Destroy(viejo.gameObject);
        else DestroyImmediate(viejo.gameObject);
    }
}
