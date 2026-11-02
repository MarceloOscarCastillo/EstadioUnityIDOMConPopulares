using System.Collections.Generic;
using UnityEngine;

public class GeneradorTorresTechoBidegain : MonoBehaviour
{
    [Header("Posiciones de las torres")]
    [Tooltip("Distancia en metros a lo largo del frente, medida desde el origen del controller")]
    public List<float> posicionesTorres = new List<float>();

    [Header("Bases de hormigon")]
    public bool generarBases = true;
    public float diametroBase = 1.2f;
    public float alturaBase = 0.5f;
    public Material materialBase;

    [Header("Columnas")]
    public float separacionColumnas = 2.6f;

    [Header("Reticulado")]
    public GameObject prefabSeccionReticulado;
    public float alturaSeccion = 4f;
    public int cantidadSecciones = 6;
    public Vector3 rotacionSeccion = Vector3.zero;
    public Vector3 rotacionCodo = Vector3.zero;

    [Header("Codo de transicion")]
    public GameObject prefabCodo;
    public float alturaCodo = 4f;

    [Header("Viga horizontal")]
    public bool generarViga = true;
    public float largoViga = 38f;
    public float cantoVigaEnTorre = 3f;
    public float cantoVigaEnPunta = 2f;
    [Tooltip("Cuanto sobresale hacia afuera del estadio")]
    public float voladizoExterior = 10f;
    public float diametroLargueroViga = 0.3f;
    public Material materialViga;

    [Header("Reticulado de la viga")]
    public bool generarReticuladoViga = true;
    public int cantidadVesViga = 6;
    public float diametroDiagonalViga = 0.15f;
    [Tooltip("Travesaño horizontal en el vertice de cada V, como en las columnas")]
    public bool generarTravesanosViga = true;

    [Tooltip("Corrige si el pivot del prefab no esta en su base")]
    public float offsetYSeccion = 0f;
    public float offsetYCodo = 0f;
    public float offsetYViga = 0f;

    [Tooltip("Corrige si el pivot del prefab no esta centrado en planta")]
    public Vector2 offsetXZSeccion = Vector2.zero;
    public Vector2 offsetXZCodo = Vector2.zero;

    private const string NOMBRE_CONTENEDOR = "Contenedor_Torres";

    public float AlturaInferiorVigas =>
    alturaBase + cantidadSecciones * alturaSeccion + alturaCodo + offsetYViga;


    [ContextMenu("Generar Torres")]
    public void GenerarTorres()
    {
        LimpiarTorres();

        GameObject contenedor = new GameObject(NOMBRE_CONTENEDOR);
        contenedor.transform.SetParent(this.transform, false);
        contenedor.tag = "SectorEstadio";

        foreach (float x in posicionesTorres)
            GenerarUnaTorre(x, contenedor.transform);

        foreach (Transform hijo in contenedor.GetComponentsInChildren<Transform>())
        {
            if (hijo.gameObject != contenedor && Application.isPlaying)
                hijo.gameObject.isStatic = true;
        }

        if (Application.isPlaying)
            StaticBatchingUtility.Combine(contenedor);

        TechoBidegain techo = GetComponentInChildren<TechoBidegain>();
        if (techo != null) techo.GenerarTecho();
    }

    void GenerarUnaTorre(float xTorre, Transform padre)
    {
        GameObject torre = new GameObject($"Torre_{xTorre:F0}");
        torre.transform.SetParent(padre, false);
        torre.transform.localPosition = new Vector3(xTorre, 0f, 0f);

        float s = separacionColumnas / 2f;

        // Solo las bases de hormigon, una bajo cada pata del prefab
        if (generarBases)
        {
            float[] offsetsX = { -s, s, -s, s };
            float[] offsetsZ = { -s, -s, s, s };

            for (int i = 0; i < 4; i++)
            {
                Vector3 pie = new Vector3(offsetsX[i], 0f, offsetsZ[i]);
                CrearCilindro(torre.transform, pie + Vector3.up * (alturaBase / 2f),
                    diametroBase, alturaBase, materialBase, "Base_Hormigon");
            }
        }

        // Secciones de reticulado apiladas sobre las bases
        for (int i = 0; i < cantidadSecciones; i++)
        {
            GameObject seccion = Instantiate(prefabSeccionReticulado, torre.transform);
            seccion.name = $"Seccion_{i + 1}";
            
            seccion.transform.localPosition = new Vector3(
    offsetXZSeccion.x,
    alturaBase + i * alturaSeccion + offsetYSeccion,
    offsetXZSeccion.y);

            seccion.transform.localRotation = Quaternion.Euler(rotacionSeccion);
        }

        float yCodo = alturaBase + cantidadSecciones * alturaSeccion;

        if (prefabCodo != null)
        {
            GameObject codo = Instantiate(prefabCodo, torre.transform);
            codo.name = "Codo";
            
            codo.transform.localPosition = new Vector3(
    offsetXZCodo.x,
    yCodo + offsetYCodo,
    offsetXZCodo.y);

            codo.transform.localRotation = Quaternion.Euler(rotacionCodo);
        }

        if (generarViga)
            GenerarVigaHorizontal(torre.transform, yCodo + alturaCodo + offsetYViga);
    }

    void GenerarVigaHorizontal(Transform padre, float yBase)
    {
        float s = separacionColumnas / 2f;

        float zInicio = -voladizoExterior;
        float zFin = largoViga - voladizoExterior;

        // Largueros en los dos lados
        foreach (float xLado in new float[] { -s, s })
        {
            Vector3 aInf = new Vector3(xLado, yBase, zInicio);
            Vector3 bInf = new Vector3(xLado, yBase, zFin);
            CrearCanio(padre, aInf, bInf, diametroLargueroViga, materialViga, "Larguero_Inferior");

            Vector3 aSup = new Vector3(xLado, yBase + cantoVigaEnTorre, zInicio);
            Vector3 bSup = new Vector3(xLado, yBase + cantoVigaEnPunta, zFin);
            CrearCanio(padre, aSup, bSup, diametroLargueroViga, materialViga, "Larguero_Superior");
        }

        if (!generarReticuladoViga || cantidadVesViga <= 0) return;

        float pasoV = (zFin - zInicio) / cantidadVesViga;

        foreach (float xLado in new float[] { -s, s })
        {
            for (int i = 0; i < cantidadVesViga; i++)
            {
                float zA = zInicio + i * pasoV;
                float zB = zA + pasoV / 2f;
                float zC = zA + pasoV;

                // La V apunta hacia abajo: baja del larguero superior al inferior y sube
                Vector3 pA = new Vector3(xLado, AlturaSuperiorViga(yBase, zA, zInicio, zFin), zA);
                Vector3 pB = new Vector3(xLado, yBase, zB);
                Vector3 pC = new Vector3(xLado, AlturaSuperiorViga(yBase, zC, zInicio, zFin), zC);

                CrearCanio(padre, pA, pB, diametroDiagonalViga, materialViga, "Diagonal_Viga");
                CrearCanio(padre, pB, pC, diametroDiagonalViga, materialViga, "Diagonal_Viga");
            }
        }

        // V en la cara inferior y en la superior, cruzando de un lado al otro
        for (int i = 0; i < cantidadVesViga; i++)
        {
            float zA = zInicio + i * pasoV;
            float zB = zA + pasoV / 2f;
            float zC = zA + pasoV;

            // Cara inferior: la V va de un larguero al otro en horizontal
            Vector3 iA = new Vector3(-s, yBase, zA);
            Vector3 iB = new Vector3(s, yBase, zB);
            Vector3 iC = new Vector3(-s, yBase, zC);

            CrearCanio(padre, iA, iB, diametroDiagonalViga, materialViga, "Diagonal_Viga_Inf");
            CrearCanio(padre, iB, iC, diametroDiagonalViga, materialViga, "Diagonal_Viga_Inf");

            // Cara superior
            float ySupA = AlturaSuperiorViga(yBase, zA, zInicio, zFin);
            float ySupB = AlturaSuperiorViga(yBase, zB, zInicio, zFin);
            float ySupC = AlturaSuperiorViga(yBase, zC, zInicio, zFin);

            Vector3 sA = new Vector3(-s, ySupA, zA);
            Vector3 sB = new Vector3(s, ySupB, zB);
            Vector3 sC = new Vector3(-s, ySupC, zC);

            CrearCanio(padre, sA, sB, diametroDiagonalViga, materialViga, "Diagonal_Viga_Sup");
            CrearCanio(padre, sB, sC, diametroDiagonalViga, materialViga, "Diagonal_Viga_Sup");
        }


        // Travesaños en el vertice de cada V, cruzando de un lado al otro
        if (generarTravesanosViga)
        {
            for (int i = 0; i < cantidadVesViga; i++)
            {
                float zV = zInicio + i * pasoV + pasoV / 2f;

                Vector3 pIzq = new Vector3(-s, yBase, zV);
                Vector3 pDer = new Vector3(s, yBase, zV);

                CrearCanio(padre, pIzq, pDer, diametroDiagonalViga, materialViga, "Travesano_Viga");
            }
        }

        // Marco de cierre en el extremo de la viga
        {
            float ySup = AlturaSuperiorViga(yBase, zFin, zInicio, zFin);

            Vector3 infIzq = new Vector3(-s, yBase, zFin);
            Vector3 infDer = new Vector3(s, yBase, zFin);
            Vector3 supIzq = new Vector3(-s, ySup, zFin);
            Vector3 supDer = new Vector3(s, ySup, zFin);

            CrearCanio(padre, infIzq, infDer, diametroDiagonalViga, materialViga, "Cierre_Inferior");
            CrearCanio(padre, supIzq, supDer, diametroDiagonalViga, materialViga, "Cierre_Superior");
            CrearCanio(padre, infIzq, supIzq, diametroDiagonalViga, materialViga, "Cierre_Lateral");
            CrearCanio(padre, infDer, supDer, diametroDiagonalViga, materialViga, "Cierre_Lateral");
        }

    }

    float AlturaSuperiorViga(float yBase, float z, float zInicio, float zFin)
    {
        float t = Mathf.InverseLerp(zInicio, zFin, z);
        return yBase + Mathf.Lerp(cantoVigaEnTorre, cantoVigaEnPunta, t);
    }



    void CrearCilindro(Transform padre, Vector3 posLocal, float diametro, float altura,
        Material mat, string nombre)
    {
        GameObject c = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        c.name = nombre;
        c.transform.SetParent(padre, false);
        c.transform.localPosition = posLocal;
        c.transform.localScale = new Vector3(diametro, altura / 2f, diametro);

        QuitarCollider(c);
        if (mat != null) c.GetComponent<Renderer>().sharedMaterial = mat;
    }

    void CrearCanio(Transform padre, Vector3 a, Vector3 b, float diametro,
        Material mat, string nombre)
    {
        GameObject c = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        c.name = nombre;
        c.transform.SetParent(padre, false);
        c.transform.localPosition = (a + b) / 2f;
        c.transform.localRotation = Quaternion.FromToRotation(Vector3.up, (b - a).normalized);
        c.transform.localScale = new Vector3(diametro, Vector3.Distance(a, b) / 2f, diametro);

        QuitarCollider(c);
        if (mat != null) c.GetComponent<Renderer>().sharedMaterial = mat;
    }

    void QuitarCollider(GameObject go)
    {
        Collider col = go.GetComponent<Collider>();
        if (col == null) return;
        if (Application.isPlaying) Destroy(col);
        else DestroyImmediate(col);
    }

    [ContextMenu("Limpiar Torres")]
    public void LimpiarTorres()
    {
        Transform viejo = transform.Find(NOMBRE_CONTENEDOR);
        if (viejo == null) return;

        if (Application.isPlaying) Destroy(viejo.gameObject);
        else DestroyImmediate(viejo.gameObject);
    }
}
