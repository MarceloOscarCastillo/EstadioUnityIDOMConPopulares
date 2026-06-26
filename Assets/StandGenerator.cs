using System.Collections.Generic;
using UnityEngine;

public class StandGenerator : MonoBehaviour
{
    public GameObject prefabEscalon;
    

    [Header("Dimensiones del Escalón")]
    public float altoEscalon = 0.20f;
    public float profundidadEscalon = 0.40f;
    public float anchoDeUnaPieza = 1.0f;

    [Header("Configuración de la Tribuna")]
    public int numFilas = 10;
    public int piezasPorFila = 20;

    [Header("Orientación")]
    public bool inverter = false;

    [Header("Configuración de Bocas de Salida")]
    public int piezasEntreBocas = 20;
    public int anchoBoca = 3;
    public int filaInicioBoca = 5;
    public int altoBoca = 4;

    [Header("Muro Superior (Cabeceras)")]
    public bool generarMuroSuperior = false;
    public float alturaMuroSuperior = 2.0f;

    [Header("Muro Frontal (Cabeceras)")]
    public bool generarMuroFrontal = false;
    public float alturaMuroFrontal = 1.0f;

    [Header("Muros Laterales (Al vacío)")]
    public bool generarMurosLaterales = false;
    public int filaInicioMuroLateral = 35;
    public float alturaMuroSobreEscalon = 1.0f;

    [Header("Estética de San Lorenzo")]
    public Material RedColour;
    public Material BlueColour;
    public float anchoFranja = 20f;

    [Header("Filtros de Contenedores")]
    public string tagContenedores = "SectorEstadio";

    [Header("Configuración Paraavalanchas")]
    public GameObject prefabParaavalanchas;
    public bool generarParaavalanchas = true;
    public int filaInicioPara = 10;
    public int frecuenciaFilasPara = 10;
    public float distanciaEntrePares = 4.0f;
    public float anchoDelPar = 4f;

    [Header("Alambrado")]
    public bool generarAlambrado = false;
    public GameObject prefabAlambrado;
    public float anchoSeccionAlambrado = 2.0f;
    public float grosorMuroAlambrado = 0.1f; // ancho del muro en Z

    [System.Serializable]
    public struct RangoAlzada
    {
        public int filaInicio;
        public float multiplicador;
    }

    [Header("Configuración de Alzada")]
    public RangoAlzada[] rangosDeAlzada;

    [Header("Soportes Estructurales")]
    public bool generarSoportes = false;
    public float separacionSoportes = 5f;
    public float alturaTramoVertical = 2f;
    public Material MaterialVigas;

    [Header("Viga Vertical Interior")]
    public int filaVigaVerticalInterior = 5;
    public float alturaVigaVerticalInterior = 2f;

    [Header("Viga Vertical Exterior")]
    public int filaVigaVerticalExterior = 20;
    public float alturaVigaVerticalExterior = 4f;

    [Header("Viga Diagonal")]
    public int filaArranqueDiagonal = 3;
    public float mordidaViga = 0.05f;

    [Header("Viga Horizontal")]
    public bool tieneVigaHorizontal = true;

    [Header("Secciones Transversales")]
    public float anchoViga = 0.2f;
    public float altoViga = 0.2f;
    public float anchoVigaDiagonal = 0.2f;
    public float altoVigaDiagonal = 0.3f;

    [Header("Soporte de Techo")]
    public bool vigaDiagonalTerminaConSoportesDeTecho = false;
    public float metrajeExtraDiagonal = 1.0f;
    public float alturasSoporteTecho = 0.5f;
    public float grosorSoporteTecho = 0.2f;
    public float profundidadSoporteTecho = 1.0f;

    [Header("Vigas Transversales")]
    public bool tieneVigasTransversales = true;
    public float alturaUnionVigasInteriores = 1.5f;
    public float alturaUnionVigasExteriores = 3.5f;
    public float anchoVigaTransversal = 0.2f;
    public float altoVigaTransversal = 0.2f;

    [Header("Escalera Vómito")]
    public bool generarEscaleraVomito = false;
    public int numEscalonesVomito = 4;
    public float altoEscalonVomito = 0.20f;
    public float profundidadEscalonVomito = 0.30f;
    public bool generarBarandillaEscalera = false;
    public Material materialBarandilla;
    public Material materialEscaleraVomito;


    [Header("Nivel del Suelo")]
    public Transform ground0Level;

    void Start()
    {
        if (Application.isPlaying)
            GenerarSector();
    }

    [ContextMenu("Generar Sector")]
    public void GenerarSector()
    {
        
        // 1. LIMPIEZA USANDO EL CONTENEDOR
        Transform contenedorViejo = transform.Find("Contenedor_Cabecera");
        if (contenedorViejo != null)
        {
            DestroyImmediate(contenedorViejo.gameObject);
        }

        // 2. CREACIÓN DEL NUEVO CONTENEDOR
        GameObject contenedorGO = new GameObject("Contenedor_Cabecera");
        contenedorGO.transform.SetParent(this.transform, false);
        contenedorGO.tag = tagContenedores;
        Transform contenedor = contenedorGO.transform;

        float multiplicadorZ = inverter ? -1f : 1f;

        // 3. GENERACIÓN DE ESCALONES
        for (int f = 0; f < numFilas; f++)
        {
            bool esEspacioDeBocaVertical = (f >= filaInicioBoca && f < filaInicioBoca + altoBoca);

            bool correspondeParaEnEstaFila = generarParaavalanchas &&
                                            f >= filaInicioPara &&
                                            (f - filaInicioPara) % frecuenciaFilasPara == 0;

            // Calculamos el desplazamiento zig-zag para esta fila
            int nivelZigZag = (f - filaInicioPara) / frecuenciaFilasPara;
            float desplazamientoZigZag = (nivelZigZag % 2 == 0) ? 0 : distanciaEntrePares;

            float alturaAcumulada = 0f;
            for (int i = 0; i < f; i++)
                alturaAcumulada += altoEscalon * ObtenerFactorParaFila(i);


            for (int e = 0; e < piezasPorFila; e++)
            {
                float posicionXMetros = e * anchoDeUnaPieza;
                bool esFranjaAzul = (Mathf.FloorToInt(posicionXMetros / anchoFranja) % 2 == 0);
                bool esEspacioDeBocaHorizontal = (e % piezasEntreBocas) < anchoBoca;

                if (esEspacioDeBocaHorizontal && esEspacioDeBocaVertical) continue;

                Vector3 posicionLocal = new Vector3(
                    e * anchoDeUnaPieza,
                    alturaAcumulada,
                    f * profundidadEscalon * multiplicadorZ
                );

                Vector3 posicionFinal = transform.TransformPoint(posicionLocal);
                Quaternion rotacionPieza = transform.rotation;
                if (inverter) rotacionPieza *= Quaternion.Euler(0, 180, 0);

               
                // Instanciamos directamente dentro del contenedor
                GameObject pieza = Instantiate(prefabEscalon, posicionFinal, rotacionPieza, contenedor);
                pieza.name = "Escalon_Cabecera"; // Nombre clave para el Calculador de Aforo

                MeshRenderer rendererHijo = pieza.GetComponentInChildren<MeshRenderer>();
                if (rendererHijo != null)
                {
                    rendererHijo.material = esFranjaAzul ? BlueColour : RedColour;
                }

                if (correspondeParaEnEstaFila)
                {
                    
                    float restoX = (posicionXMetros - desplazamientoZigZag) % (distanciaEntrePares * 2);

                    if (restoX < 0) restoX += distanciaEntrePares * 2;


                    int ultimaFilaPara = filaInicioPara + ((numFilas - filaInicioPara) / frecuenciaFilasPara) * frecuenciaFilasPara;

                    //if (f == ultimaFilaPara)
                    //{
                    //    Debug.Log($"e={e}, posX={posicionXMetros}, restoX={restoX}, distancia*2={distanciaEntrePares * 2}");
                    //}

                    if (restoX < 0.1f || restoX > (distanciaEntrePares * 2) - 0.1f)
                    {
                        float anchoTotal = (piezasPorFila - 1) * anchoDeUnaPieza;

                        //Colocamos el PAR(Izquierdo y Derecho)
                        float[] offsetsPar = { -anchoDelPar / 2f, anchoDelPar / 2f };

                        foreach (float off in offsetsPar)
                        {
                            float xFinal = e * anchoDeUnaPieza + off;
                            if (xFinal < 0 || xFinal > anchoTotal) continue;

                            Vector3 posParaLocal = new Vector3(
                        e * anchoDeUnaPieza + off,
                        alturaAcumulada + (altoEscalon * ObtenerFactorParaFila(f)),          // altura del escalón en espacio local
                        f * profundidadEscalon * multiplicadorZ  // misma Z que el escalón
            );                            
                            GameObject para = Instantiate(prefabParaavalanchas, transform.TransformPoint(posParaLocal), rotacionPieza, contenedor);
                            para.name = "Paraavalanchas_Popular";

                            // Color individual basado en la X del paraavalanchas
                            bool paraEsAzul = (Mathf.FloorToInt((posicionXMetros + off) / anchoFranja) % 2 == 0);
                            MeshRenderer[] mrsPara = para.GetComponentsInChildren<MeshRenderer>();
                            foreach (MeshRenderer mr in mrsPara)
                                mr.material = paraEsAzul ? BlueColour : RedColour;
                        }
                    }
                }
            }
        }

        // 4. GENERACIÓN DE ESTRUCTURAS AUXILIARES (Pasando el contenedor)
        GenerarEstructurasVomitos(multiplicadorZ, contenedor);

        if (generarMuroSuperior)
        {
            CrearMuroCierreSuperior(multiplicadorZ, contenedor);

            if (generarMurosLaterales)
            {
                GenerarMurosLateralesAlVacio(multiplicadorZ, contenedor);
            }
        }

        if (generarAlambrado) GenerarAlambrado(multiplicadorZ, contenedor);

        if (!generarAlambrado) CrearMuroCierreFrontal(multiplicadorZ, contenedor);

        if (generarSoportes) 
        {
            GenerarSoportes(multiplicadorZ, contenedor);
            //cambio para prueba
            GenerarVigasTransversales(multiplicadorZ, contenedor);
        }

        foreach (Transform hijo in contenedorGO.GetComponentsInChildren<Transform>())
        {
            if (hijo.gameObject != contenedorGO && Application.isPlaying)
                hijo.gameObject.isStatic = true;
        }

        if (Application.isPlaying)
            StaticBatchingUtility.Combine(contenedorGO);

    }

    
    void GenerarEstructurasVomitos(float mZ, Transform padre)
    {
        float grosorMuroVomito = 0.15f;
        for (int e = 0; e < piezasPorFila; e++)
        {
            if (e % piezasEntreBocas == 0)
            {
                
                float yFin = CalcularAlturaAcumuladaCabecera(filaInicioBoca + altoBoca);

                float zFin = (filaInicioBoca + altoBoca) * profundidadEscalon * mZ;

                Vector3 posTraseroLocal = new Vector3((e + (anchoBoca / 2f) - 0.5f) * anchoDeUnaPieza, yFin, zFin);

                // Usamos el helper pasando el padre
                CrearBloqueMuro(transform.TransformPoint(posTraseroLocal),
                               new Vector3(anchoBoca * anchoDeUnaPieza, 1.0f, 0.1f),
                               padre, BlueColour);

                float offsetPieza = (anchoDeUnaPieza / 2f);
                float[] columnasX = { (e * anchoDeUnaPieza) - offsetPieza, ((e + anchoBoca - 1) * anchoDeUnaPieza) + offsetPieza };

                foreach (float x in columnasX)
                {
                    GameObject muroVomitoGO = new GameObject("Muro_Lateral_Vomito");
                    muroVomitoGO.transform.SetParent(padre); // ASIGNAR AL CONTENEDOR
                    muroVomitoGO.transform.localPosition = Vector3.zero;
                    muroVomitoGO.transform.localRotation = Quaternion.identity;

                    MeshFilter mf = muroVomitoGO.AddComponent<MeshFilter>();
                    MeshRenderer mr = muroVomitoGO.AddComponent<MeshRenderer>();
                    mr.sharedMaterial = BlueColour;

                    // ... (Lógica de mesh igual, pero el objeto ya está en el contenedor)
                    Mesh mesh = GenerarMeshMuroVomito(x, filaInicioBoca, altoBoca, mZ, grosorMuroVomito);
                    mf.mesh = mesh;
                    muroVomitoGO.AddComponent<MeshCollider>();
                }

                if (generarEscaleraVomito)
                {
                    float xCentroVomito = (e + anchoBoca / 2f - 0.5f) * anchoDeUnaPieza;
                    GenerarEscaleraDescendenteVomito(xCentroVomito, anchoBoca * anchoDeUnaPieza, filaInicioBoca, mZ, padre);
                }
            }
        }
    }

    // Helper para no repetir código de vértices
    Mesh GenerarMeshMuroVomito(float x, int fInicio, int aBoca, float mZ, float grosor)
    {
        Mesh mesh = new Mesh();
        //float yI = fInicio * altoEscalon;
        float zI = fInicio * profundidadEscalon * mZ;

        float yI = CalcularAlturaAcumuladaCabecera(fInicio);
        float yF = CalcularAlturaAcumuladaCabecera(fInicio + aBoca);

        //float yF = (fInicio + aBoca) * altoEscalon;
        float zF = (fInicio + aBoca) * profundidadEscalon * mZ;
        float g = grosor / 2f;
        float hMuro = 1.0f;

        Vector3[] v = new Vector3[8];
        v[0] = new Vector3(x - g, yI, zI); v[1] = new Vector3(x - g, yI + hMuro, zI);
        v[2] = new Vector3(x - g, yF, zF); v[3] = new Vector3(x - g, yF + hMuro, zF);
        v[4] = new Vector3(x + g, yI, zI); v[5] = new Vector3(x + g, yI + hMuro, zI);
        v[6] = new Vector3(x + g, yF, zF); v[7] = new Vector3(x + g, yF + hMuro, zF);

        mesh.vertices = v;
        mesh.triangles = new int[] { 0, 1, 2, 1, 3, 2, 4, 6, 5, 5, 6, 7, 0, 4, 1, 4, 5, 1, 2, 3, 6, 3, 7, 6, 1, 5, 3, 5, 7, 3, 0, 2, 4, 2, 6, 4 };
        mesh.RecalculateNormals();
        return mesh;
    }

    void CrearMuroCierreSuperior(float mZ, Transform padre)
    {
        float anchoTotalMetros = piezasPorFila * anchoDeUnaPieza;
        Vector3 posLocal = new Vector3(
            (anchoTotalMetros / 2f) - (anchoDeUnaPieza / 2f),
            CalcularAlturaAcumuladaCabecera(numFilas),
            (numFilas - 1) * profundidadEscalon * mZ
        );

        CrearBloqueMuro(transform.TransformPoint(posLocal),
                       new Vector3(anchoTotalMetros, alturaMuroSuperior, 0.2f),
                       padre, BlueColour);
    }

    void CrearMuroCierreFrontal(float mZ, Transform padre)
    {
        float anchoTotalMetros = piezasPorFila * anchoDeUnaPieza;

        float yBase = 0f; // altura del primer escalon

        float zMuroFrontal = (-profundidadEscalon / 2f - grosorMuroAlambrado) * mZ;

        Vector3 posLocal = new Vector3(
            (anchoTotalMetros / 2f) - (anchoDeUnaPieza / 2f),
            yBase,
            zMuroFrontal
        );

        CrearBloqueMuro(transform.TransformPoint(posLocal),
                       new Vector3(anchoTotalMetros, alturaMuroFrontal, 0.2f),
                       padre, BlueColour);
    }

    void CrearBloqueMuro(Vector3 posMundo, Vector3 escala, Transform padre, Material mat)
    {
        GameObject muro = GameObject.CreatePrimitive(PrimitiveType.Cube);
        muro.transform.SetParent(padre); // ASIGNAR AL CONTENEDOR
        muro.transform.position = posMundo + (transform.up * (escala.y / 2f));
        muro.transform.localScale = escala;
        muro.transform.rotation = transform.rotation;

        MeshRenderer mr = muro.GetComponent<MeshRenderer>();
        if (mr != null) mr.sharedMaterial = mat;
        if (muro.GetComponent<BoxCollider>()) DestroyImmediate(muro.GetComponent<BoxCollider>());
    }

    float ObtenerFactorParaFila(int fila)
    {
        float factor = 1.0f; // Default 40cm
        if (rangosDeAlzada == null || rangosDeAlzada.Length == 0) return factor;

        // Buscamos el rango más alto que sea menor o igual a la fila actual
        foreach (var rango in rangosDeAlzada)
        {
            if (fila >= rango.filaInicio) factor = rango.multiplicador;
        }
        return factor;
    }

    float CalcularAlturaAcumuladaCabecera(int fila)
    {
        float altura = 0f;
        for (int i = 0; i < fila; i++)
            altura += altoEscalon * ObtenerFactorParaFila(i);
        return altura;
    }

    void GenerarAlambrado(float mZ, Transform padre)
    {
        if (!generarAlambrado || prefabAlambrado == null) return;

        float yBase = 0f; // altura del primer escalon
        
        float zAlambrado = (-profundidadEscalon / 2f - grosorMuroAlambrado) * mZ;

        float xActual = anchoSeccionAlambrado / 2f;

        float anchoTotal = piezasPorFila * anchoDeUnaPieza;

        while (xActual < anchoTotal - anchoSeccionAlambrado / 2f)
        {
            Vector3 posLocal = new Vector3(xActual, yBase, zAlambrado);
            Vector3 posMundo = transform.TransformPoint(posLocal);
            Quaternion rotacion = transform.rotation;
            if (inverter) rotacion *= Quaternion.Euler(0, 180, 0);

            GameObject seccion = Instantiate(prefabAlambrado, posMundo, rotacion, padre);
            xActual += anchoSeccionAlambrado;
        }
    }

    
    void GenerarSoportes(float mZ, Transform padre)
    {
        if (!generarSoportes || MaterialVigas == null) return;

        // Zonas prohibidas: vomitos
        List<(float xInicio, float xFin)> zonasProhibidas = new List<(float, float)>();

        float xActualVomito = 0;
        int eVomito = 0;
        while (xActualVomito < piezasPorFila * anchoDeUnaPieza)
        {
            bool esVomito = (eVomito % piezasEntreBocas) < anchoBoca;
            if (esVomito)
            {
                float xInicioVomito = xActualVomito - anchoDeUnaPieza / 2f;
                float xFinVomito = xInicioVomito + anchoBoca * anchoDeUnaPieza;
                zonasProhibidas.Add((xInicioVomito, xFinVomito));
            }
            xActualVomito += anchoDeUnaPieza;
            eVomito++;
        }

        float anchoTotal = piezasPorFila * anchoDeUnaPieza;
        float xActual = separacionSoportes / 2f;
        float xAnterior = -1f;

        while (xActual < anchoTotal)
        {
            float xAjustada = AjustarXSoporte(xActual, zonasProhibidas);

            if (Mathf.Abs(xAjustada - xAnterior) > 0.01f)
            {
                GenerarSoporte(xAjustada, mZ, padre);
                xAnterior = xAjustada;
            }
            xActual += separacionSoportes;
        }
    }

    float AjustarXSoporte(float x, List<(float xInicio, float xFin)> zonasProhibidas)
    {
        foreach (var zona in zonasProhibidas)
        {
            if (x > zona.xInicio && x < zona.xFin)
            {
                float distIzq = x - zona.xInicio;
                float distDer = zona.xFin - x;
                return distIzq < distDer ? zona.xInicio : zona.xFin;
            }
        }
        return x;
    }

    void GenerarSoporte(float x, float mZ, Transform padre)
    {
        float zInterior = filaVigaVerticalInterior * profundidadEscalon * mZ;
        float zExterior = filaVigaVerticalExterior * profundidadEscalon * mZ;
        float zArranque = filaArranqueDiagonal * profundidadEscalon * mZ;
        float zFinal = (numFilas - 1) * profundidadEscalon * mZ + profundidadEscalon / 2f * mZ;

        float yArranque = CalcularAlturaAcumuladaCabecera(filaArranqueDiagonal);
        float yFinDiagonal = CalcularAlturaAcumuladaCabecera(numFilas - 1);

        float yBase = ground0Level != null ?
            padre.InverseTransformPoint(ground0Level.position).y : 0f;

        float alturaInterior = alturaVigaVerticalInterior - yBase;
        float alturaExterior = alturaVigaVerticalExterior - yBase;

        CrearVigaVertical(padre, x, yBase, zInterior, alturaInterior, anchoViga, altoViga);
        CrearVigaVertical(padre, x, yBase, zExterior, alturaExterior, anchoViga, altoViga);

        if (tieneVigaHorizontal)
            CrearVigaHorizontalZ(padre, x, alturaVigaVerticalInterior, zInterior, zExterior, anchoViga, altoViga);

        CrearVigaDiagonal(padre, x, zArranque, yArranque, zFinal, yFinDiagonal, mZ);
    }


    void CrearVigaVertical(Transform padre, float x, float yBase, float z, float altura, float ancho, float alto)
    {
        GameObject viga = new GameObject("Viga_Vertical");
        viga.transform.SetParent(padre);
        viga.transform.localPosition = Vector3.zero;
        viga.transform.localRotation = Quaternion.identity;

        float g = ancho / 2f;
        float h = alto / 2f;

        Vector3[] v = new Vector3[8];
        v[0] = new Vector3(x - g, yBase, z - h);
        v[1] = new Vector3(x - g, yBase + altura, z - h);
        v[2] = new Vector3(x - g, yBase, z + h);
        v[3] = new Vector3(x - g, yBase + altura, z + h);
        v[4] = new Vector3(x + g, yBase, z - h);
        v[5] = new Vector3(x + g, yBase + altura, z - h);
        v[6] = new Vector3(x + g, yBase, z + h);
        v[7] = new Vector3(x + g, yBase + altura, z + h);

        Mesh mesh = new Mesh();
        mesh.vertices = v;
        mesh.triangles = new int[] {
        0, 1, 2, 1, 3, 2,
        4, 6, 5, 5, 6, 7,
        0, 4, 1, 4, 5, 1,
        2, 3, 6, 3, 7, 6,
        1, 5, 3, 5, 7, 3,
        0, 2, 4, 2, 6, 4
    };
        mesh.RecalculateNormals();

        viga.AddComponent<MeshFilter>().mesh = mesh;
        viga.AddComponent<MeshRenderer>().sharedMaterial = MaterialVigas;
    }

    void CrearVigaHorizontalZ(Transform padre, float x, float y, float zInicio, float zFin, float ancho, float alto)
    {
        GameObject viga = new GameObject("Viga_Horizontal");
        viga.transform.SetParent(padre);
        viga.transform.localPosition = Vector3.zero;
        viga.transform.localRotation = Quaternion.identity;

        float g = ancho / 2f;
        float h = alto / 2f;

        Vector3[] v = new Vector3[8];
        v[0] = new Vector3(x - g, y - h, zInicio);
        v[1] = new Vector3(x - g, y + h, zInicio);
        v[2] = new Vector3(x - g, y - h, zFin);
        v[3] = new Vector3(x - g, y + h, zFin);
        v[4] = new Vector3(x + g, y - h, zInicio);
        v[5] = new Vector3(x + g, y + h, zInicio);
        v[6] = new Vector3(x + g, y - h, zFin);
        v[7] = new Vector3(x + g, y + h, zFin);

        Mesh mesh = new Mesh();
        mesh.vertices = v;
        mesh.triangles = new int[] {
        0, 1, 2, 1, 3, 2,
        4, 6, 5, 5, 6, 7,
        0, 4, 1, 4, 5, 1,
        2, 3, 6, 3, 7, 6,
        1, 5, 3, 5, 7, 3,
        0, 2, 4, 2, 6, 4
    };
        mesh.RecalculateNormals();

        viga.AddComponent<MeshFilter>().mesh = mesh;
        viga.AddComponent<MeshRenderer>().sharedMaterial = MaterialVigas;
    }

    void CrearVigaDiagonal(Transform padre, float x, float zInicio, float yInicio, float zFin, float yFin, float mZ)
    {
        GameObject viga = new GameObject("Viga_Diagonal");
        viga.transform.SetParent(padre);
        viga.transform.localPosition = Vector3.zero;
        viga.transform.localRotation = Quaternion.identity;

        float g = anchoVigaDiagonal / 2f;
        float h = altoVigaDiagonal / 2f;

        // Direccion de la diagonal
        Vector3 dir = new Vector3(0, yFin - yInicio, zFin - zInicio).normalized;
        Vector3 perp = Vector3.Cross(dir, Vector3.right).normalized * h;

        // Desplazamiento Y para que la viga quede debajo de los escalones
        float desplazamientoY = -(altoVigaDiagonal - mordidaViga);

        Vector3 pInicio = new Vector3(x, yInicio + desplazamientoY, zInicio);
        Vector3 pFin = new Vector3(x, yFin + desplazamientoY, zFin);

        // Extremo inicio (diagonal)
        Vector3 i0 = new Vector3(x - g, pInicio.y - perp.y, pInicio.z - perp.z);
        Vector3 i1 = new Vector3(x - g, pInicio.y + perp.y, pInicio.z + perp.z);
        Vector3 i4 = new Vector3(x + g, pInicio.y - perp.y, pInicio.z - perp.z);
        Vector3 i5 = new Vector3(x + g, pInicio.y + perp.y, pInicio.z + perp.z);

        // Extremo final con corte VERTICAL (perpendicular al suelo)
        float yFinSuperior = pFin.y + h;
        float yFinInferior = pFin.y - h;
        Vector3 f2 = new Vector3(x - g, yFinInferior, zFin);
        Vector3 f3 = new Vector3(x - g, yFinSuperior, zFin);
        Vector3 f6 = new Vector3(x + g, yFinInferior, zFin);
        Vector3 f7 = new Vector3(x + g, yFinSuperior, zFin);

        Vector3[] v = new Vector3[] { i0, i1, f2, f3, i4, i5, f6, f7 };

        Mesh mesh = new Mesh();
        mesh.vertices = v;
        mesh.triangles = new int[] {
        0, 1, 2, 1, 3, 2,
        4, 6, 5, 5, 6, 7,
        0, 4, 1, 4, 5, 1,
        2, 3, 6, 3, 7, 6,
        1, 5, 3, 5, 7, 3,
        0, 2, 4, 2, 6, 4
    };
        mesh.RecalculateNormals();

        viga.AddComponent<MeshFilter>().mesh = mesh;
        viga.AddComponent<MeshRenderer>().sharedMaterial = MaterialVigas;
    }

    void GenerarVigasTransversales(float mZ, Transform padre)
    {
        if (!tieneVigasTransversales) return;

        float anchoTotal = piezasPorFila * anchoDeUnaPieza;
        float xInicio = separacionSoportes / 2f;
        float xFin = xInicio;

        while (xFin + separacionSoportes < anchoTotal)
            xFin += separacionSoportes;

        float zInterior = filaVigaVerticalInterior * profundidadEscalon * mZ;
        float zExterior = filaVigaVerticalExterior * profundidadEscalon * mZ;

        // Viga transversal interior
        CrearVigaTransversal(padre, xInicio, xFin, alturaUnionVigasInteriores, zInterior);
        // Viga transversal exterior
        CrearVigaTransversal(padre, xInicio, xFin, alturaUnionVigasExteriores, zExterior);
    }

    void CrearVigaTransversal(Transform padre, float xInicio, float xFin, float y, float z)
    {
        GameObject viga = new GameObject("Viga_Transversal");
        viga.transform.SetParent(padre);
        viga.transform.localPosition = Vector3.zero;
        viga.transform.localRotation = Quaternion.identity;

        float g = anchoVigaTransversal / 2f;
        float h = altoVigaTransversal / 2f;

        Vector3[] v = new Vector3[8];
        v[0] = new Vector3(xInicio, y - h, z - g);
        v[1] = new Vector3(xInicio, y + h, z - g);
        v[2] = new Vector3(xInicio, y - h, z + g);
        v[3] = new Vector3(xInicio, y + h, z + g);
        v[4] = new Vector3(xFin, y - h, z - g);
        v[5] = new Vector3(xFin, y + h, z - g);
        v[6] = new Vector3(xFin, y - h, z + g);
        v[7] = new Vector3(xFin, y + h, z + g);

        Mesh mesh = new Mesh();
        mesh.vertices = v;
        mesh.triangles = new int[] {
        0, 1, 2, 1, 3, 2,
        4, 6, 5, 5, 6, 7,
        0, 4, 1, 4, 5, 1,
        2, 3, 6, 3, 7, 6,
        1, 5, 3, 5, 7, 3,
        0, 2, 4, 2, 6, 4
    };
        mesh.RecalculateNormals();

        viga.AddComponent<MeshFilter>().mesh = mesh;
        viga.AddComponent<MeshRenderer>().sharedMaterial = MaterialVigas;
    }


    void GenerarMurosLateralesAlVacio(float mZ, Transform padre)
    {
        float offsetLateralX = (anchoDeUnaPieza / 2f);
        float[] posicionesX = { 0 - offsetLateralX, (piezasPorFila - 1) * anchoDeUnaPieza + offsetLateralX };
        float grosorMuro = 0.2f;

        float yA = CalcularAlturaAcumuladaCabecera(filaInicioMuroLateral);
        float zA = filaInicioMuroLateral * profundidadEscalon * mZ;
        float yB = CalcularAlturaAcumuladaCabecera(numFilas);
        float zB = numFilas * profundidadEscalon * mZ;

        int filaUltimoRango = 0;
        if (rangosDeAlzada != null && rangosDeAlzada.Length > 0)
            filaUltimoRango = rangosDeAlzada[rangosDeAlzada.Length - 1].filaInicio;

        float yUltimoRangoInicio = CalcularAlturaAcumuladaCabecera(filaUltimoRango);
        float zUltimoRangoInicio = filaUltimoRango * profundidadEscalon * mZ;
        float yUltimoRangoFin = CalcularAlturaAcumuladaCabecera(numFilas);
        float zUltimoRangoFin = numFilas * profundidadEscalon * mZ;

        float longitudZ = Mathf.Abs(zB - zA);
        float pendiente = (yUltimoRangoFin - yUltimoRangoInicio) / Mathf.Abs(zUltimoRangoFin - zUltimoRangoInicio);
        float yBCorregida = yA + longitudZ * pendiente;

        float hA = alturaMuroSobreEscalon;
        float hB = alturaMuroSobreEscalon;

        foreach (float x in posicionesX)
        {
            float g = grosorMuro / 2f;

            // Los 4 puntos base del perfil (sin grosor)
            Vector3 p00 = new Vector3(0, yA, zA); // base inicio
            Vector3 p01 = new Vector3(0, yA + hA, zA); // top inicio
            Vector3 p10 = new Vector3(0, yBCorregida, zB); // base fin
            Vector3 p11 = new Vector3(0, yBCorregida + hB, zB); // top fin

            // Offset lateral para cada cara
            Vector3 offL = new Vector3(x - g, 0, 0);
            Vector3 offR = new Vector3(x + g, 0, 0);

            List<Vector3> vertices = new List<Vector3>();
            List<Vector3> normales = new List<Vector3>();
            List<int> triangulos = new List<int>();
            int base_i;

            // --- Cara izquierda (normal = left) ---
            base_i = vertices.Count;
            vertices.AddRange(new[] { p00 + offL, p01 + offL, p10 + offL, p11 + offL });
            normales.AddRange(new[] { Vector3.left, Vector3.left, Vector3.left, Vector3.left });
            triangulos.AddRange(new[] {
            base_i, base_i+2, base_i+1,
            base_i+1, base_i+2, base_i+3
        });

            // --- Cara derecha (normal = right) ---
            base_i = vertices.Count;
            vertices.AddRange(new[] { p00 + offR, p01 + offR, p10 + offR, p11 + offR });
            normales.AddRange(new[] { Vector3.right, Vector3.right, Vector3.right, Vector3.right });
            triangulos.AddRange(new[] {
            base_i, base_i+1, base_i+2,
            base_i+1, base_i+3, base_i+2
        });

            // --- Cara frontal (inicio, normal apunta hacia los espectadores) ---
            Vector3 nFront = new Vector3(0, 0, -mZ);
            base_i = vertices.Count;
            vertices.AddRange(new[] { p00 + offL, p01 + offL, p00 + offR, p01 + offR });
            normales.AddRange(new[] { nFront, nFront, nFront, nFront });
            triangulos.AddRange(new[] {
            base_i, base_i+1, base_i+2,
            base_i+1, base_i+3, base_i+2
        });

            // --- Cara trasera (fin, normal apunta hacia afuera) ---
            Vector3 nBack = new Vector3(0, 0, mZ);
            base_i = vertices.Count;
            vertices.AddRange(new[] { p10 + offL, p11 + offL, p10 + offR, p11 + offR });
            normales.AddRange(new[] { nBack, nBack, nBack, nBack });
            triangulos.AddRange(new[] {
            base_i, base_i+2, base_i+1,
            base_i+1, base_i+2, base_i+3
        });

            // --- Techo (normal = up) ---
            base_i = vertices.Count;
            vertices.AddRange(new[] { p01 + offL, p11 + offL, p01 + offR, p11 + offR });
            normales.AddRange(new[] { Vector3.up, Vector3.up, Vector3.up, Vector3.up });
            triangulos.AddRange(new[] {
            base_i, base_i+1, base_i+2,
            base_i+1, base_i+3, base_i+2
        });

            // --- Piso (normal = down) ---
            base_i = vertices.Count;
            vertices.AddRange(new[] { p00 + offL, p10 + offL, p00 + offR, p10 + offR });
            normales.AddRange(new[] { Vector3.down, Vector3.down, Vector3.down, Vector3.down });
            triangulos.AddRange(new[] {
            base_i, base_i+2, base_i+1,
            base_i+1, base_i+2, base_i+3
        });

            Mesh mesh = new Mesh();
            mesh.vertices = vertices.ToArray();
            mesh.normals = normales.ToArray();
            mesh.triangles = triangulos.ToArray();

            GameObject muroGO = new GameObject("Muro_Lateral_Solido");
            muroGO.transform.SetParent(padre);
            muroGO.transform.localPosition = Vector3.zero;
            muroGO.transform.localRotation = Quaternion.identity;
            MeshFilter mf = muroGO.AddComponent<MeshFilter>();
            MeshRenderer mr = muroGO.AddComponent<MeshRenderer>();
            mr.sharedMaterial = BlueColour;
            mf.mesh = mesh;
        }
    }

    void GenerarEscaleraDescendenteVomito(float xCentro, float anchoVomito, int filaInicio, float mZ, Transform padre)
    {
        float yBase = CalcularAlturaAcumuladaCabecera(filaInicio);
        float zBase = filaInicio * profundidadEscalon * mZ;

        for (int e = 0; e < numEscalonesVomito; e++)
        {
            float yEscalon = yBase - (e + 1) * altoEscalonVomito;
            float zEscalon = zBase + (e * profundidadEscalonVomito + profundidadEscalonVomito / 2f) * mZ;

            GameObject escalonGO = GameObject.CreatePrimitive(PrimitiveType.Cube);
            escalonGO.name = $"Escalon_Vomito_{e}";
            escalonGO.transform.SetParent(padre);
            escalonGO.transform.position = transform.TransformPoint(
                new Vector3(xCentro, yEscalon + altoEscalonVomito / 2f, zEscalon));
            escalonGO.transform.rotation = transform.rotation;
            escalonGO.transform.localScale = new Vector3(anchoVomito, altoEscalonVomito, profundidadEscalonVomito);

            if (materialEscaleraVomito != null)
                escalonGO.GetComponent<Renderer>().sharedMaterial = materialEscaleraVomito;
            else if (BlueColour != null)
                escalonGO.GetComponent<Renderer>().sharedMaterial = BlueColour;

            DestroyImmediate(escalonGO.GetComponent<BoxCollider>());
        }

        if (generarBarandillaEscalera)
            GenerarBarandillaVomito(xCentro, anchoVomito, filaInicio, mZ, padre);
    }

    void GenerarBarandillaVomito(float xCentro, float anchoVomito, int filaInicio, float mZ, Transform padre)
    {
        float diametro = 0.05f;
        float alturaBarandilla = 1.0f;
        float yBase = CalcularAlturaAcumuladaCabecera(filaInicio);
        float zBase = filaInicio * profundidadEscalon * mZ;

        float xIzq = xCentro - anchoVomito / 2f + diametro;
        float xDer = xCentro + anchoVomito / 2f - diametro;

        float[] posicionesX = { xIzq, xDer };

        foreach (float x in posicionesX)
        {
            Vector3 pInicio = transform.TransformPoint(new Vector3(x,
                yBase - altoEscalonVomito + alturaBarandilla,
                zBase + profundidadEscalonVomito / 2f * mZ));

            Vector3 pFin = transform.TransformPoint(new Vector3(x,
                yBase - numEscalonesVomito * altoEscalonVomito + alturaBarandilla,
                zBase + (numEscalonesVomito * profundidadEscalonVomito - profundidadEscalonVomito / 2f) * mZ));

            CrearCañoEntreDosPuntos(pInicio, pFin, diametro, padre);

            Vector3 baseInicio = pInicio; baseInicio.y -= alturaBarandilla;
            CrearCañoEntreDosPuntos(baseInicio, pInicio, diametro, padre);

            Vector3 baseFin = pFin; baseFin.y -= alturaBarandilla;
            CrearCañoEntreDosPuntos(baseFin, pFin, diametro, padre);
        }
    }

    void CrearCañoEntreDosPuntos(Vector3 pA, Vector3 pB, float diametro, Transform padre)
    {
        GameObject caño = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        caño.name = "Caño_Barandilla";
        caño.transform.SetParent(padre);
        DestroyImmediate(caño.GetComponent<CapsuleCollider>());

        Vector3 centro = (pA + pB) / 2f;
        float largo = Vector3.Distance(pA, pB);
        Vector3 direccion = (pB - pA).normalized;

        caño.transform.position = centro;
        caño.transform.up = direccion;
        caño.transform.localScale = new Vector3(diametro, largo / 2f, diametro);

        if (materialBarandilla != null)
            caño.GetComponent<Renderer>().sharedMaterial = materialBarandilla;
    }

}
