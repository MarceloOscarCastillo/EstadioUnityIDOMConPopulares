using Unity.VisualScripting;
using UnityEngine;
using System.Collections.Generic;


public class SeatedStandGenerator : MonoBehaviour
{
    // Estructura simple para definir los cambios de alzada
    [System.Serializable]
    public struct RangoAlzada
    {
        public int filaInicio; // A partir de qué fila aplica
        public float multiplicador; // 1.0 = 40cm, 1.1 = 44cm, etc.
    }

    [Header("Prefabs")]
    public GameObject SeatersStandBlock;
    public GameObject Escalera;
    public GameObject AsientoPlatea;
    public GameObject PrefabPalcoVIP;

    [Header("Dimensiones")]
    public float anchoDeUnaPieza = 1.0f;
    public float largoMaximoTribuna = 118.0f;
    public float altoEscalonBase = 0.40f; // El valor base de 40cm
    public float profundidadEscalon = 0.8f;
    
    [Header("Lógica de Grada")]
    public int numFilas = 40;
    public int asientosEntrePasillos = 12;
    public float anchoPasilloEscalera = 2.0f;
    public bool invertir = true;

    [Header("Configuración de Alzada (Pasos)")]
    // Aquí definís los saltos: Ej: Fila 0 -> 1.0 | Fila 20 -> 1.1 | Fila 35 -> 1.2
    public RangoAlzada[] rangosDeAlzada;

    [Header("Configuración Palco de Lujo (VIP)")]
    public bool tienePalcoVIP = true;
    public float largoPalcoVIP = 104f;
    public int filasQueOcupaPalco = 6;
    public float profundidadTotalVIP = 9.10f;
    public Material BlackVIPMaterial;

    [Header("Seguridad (Vómitos)")]
    public int filaInicioBoca = 5;
    public int altoBoca = 4;
    public bool tieneSegundaFilaVomitos = false;
    public int filaInicioBoca2 = 25;

    [Header("Muro Superior")]
    public bool generarMuroSuperior = true;
    public float alturaMuroSuperior = 2.0f;

    [Header("Materiales")]
    public Material RedColour;
    public Material BlueColour;
    public Material GrisCemento;
    public Material Glass;
    public float anchoFranja = 20f;


    [Header("Recorte de Filas")]
    public bool usarRecorteFilas = false;
    public int filasEnExtremoIzquierdo = 48;
    public int filasEnExtremoDerecho = 48;
    public bool invertirRecorte = false;

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

    [Header("Túnel de Jugadores")]
    public bool generarTunel = false;
    public float anchoTunel = 6f;
    public float anchoZonaBancas = 11f;
    public int filasQuitadasTunel = 3;
    public int filasQuitadasBancas = 2;
    public float profundidadBancaSuplentes = 0.8f;
    public GameObject prefabBancaSuplentes;

    [Header("Escalera Vómito")]
    public bool generarEscaleraVomito = false;
    public int numEscalonesVomito = 4;
    public float altoEscalonVomito = 0.20f;
    public float profundidadEscalonVomito = 0.30f;
    public Material materialEscaleraVomito;
    public Material materialBarandilla;
    public bool generarBarandillaEscalera = false;


    private float zMuroFrontalCalculado = 0f;

    [Header("Nivel del Suelo")]
    public Transform ground0Level;

    [ContextMenu("Generar Platea")]


    void Start()
    {
        if (Application.isPlaying)
            GenerarSector();
    }

    public void GenerarSector()
    {
        Transform contenedorViejo = transform.Find("Contenedor_Platea_Recta");
        if (contenedorViejo != null) DestroyImmediate(contenedorViejo.gameObject);

        GameObject contenedor = new GameObject("Contenedor_Platea_Recta");
        contenedor.transform.SetParent(this.transform, false);
        contenedor.tag = "SectorEstadio";

        float multZ = invertir ? -1f : 1f;
        float margenLateralPalco = (largoMaximoTribuna - largoPalcoVIP) / 2f;

        float xActual = 0;
        int colAsiento = 0;

        while (xActual < largoMaximoTribuna)
        {
            bool esPasillo = (colAsiento >= asientosEntrePasillos);
            int filasEnEstaColumna = FilasEnX(xActual);

            float alturaAcumulada = 0f;

            for (int f = 0; f < filasEnEstaColumna; f++)
            {
                float factorEscalaY = ObtenerFactorParaFila(f);
                float altoRealFila = altoEscalonBase * factorEscalaY;

                bool dentroDeXPalco = (xActual >= margenLateralPalco && xActual < (largoMaximoTribuna - margenLateralPalco));
                bool dentroDeFilaPalco = (f < filasQueOcupaPalco);
                bool zonaOcupadaPorPalco = tienePalcoVIP && dentroDeXPalco && dentroDeFilaPalco;

                bool esFilaVomito = (f >= filaInicioBoca && f < filaInicioBoca + altoBoca) ||
                                   (tieneSegundaFilaVomitos && f >= filaInicioBoca2 && f < filaInicioBoca2 + altoBoca);

                bool esHuecoVomito = esPasillo && esFilaVomito;

                bool esZonaEliminada = false;

                bool esSinAsientos = false;

                // Logica de tunel y bancas
                if (generarTunel)
                {
                    float centroX = largoMaximoTribuna / 2f;
                    float inicioTunel = centroX - anchoTunel / 2f;
                    float finTunel = centroX + anchoTunel / 2f;
                    float inicioBancaIzq = inicioTunel - anchoZonaBancas;
                    float finBancaDer = finTunel + anchoZonaBancas;

                    bool enZonaTunel = xActual >= inicioTunel && xActual < finTunel;
                    bool enZonaBancaIzq = xActual >= inicioBancaIzq && xActual < inicioTunel;
                    bool enZonaBancaDer = xActual >= finTunel && xActual < finBancaDer;
                    
                    if (enZonaTunel && f < filasQuitadasTunel) esZonaEliminada = true;
                    if ((enZonaBancaIzq || enZonaBancaDer) && f < filasQuitadasBancas) esZonaEliminada = true;

                    esSinAsientos = false;
                    if (enZonaTunel && f == filasQuitadasTunel) esSinAsientos = true;
                    if ((enZonaBancaIzq || enZonaBancaDer) && f == filasQuitadasBancas) esSinAsientos = true;

                }

                if (!esZonaEliminada && !esHuecoVomito && !zonaOcupadaPorPalco)
                {                    
                    Vector3 posLocal;
                    if (esPasillo)
                        posLocal = new Vector3(
                            xActual,
                            alturaAcumulada,
                            f * profundidadEscalon * multZ
                        );
                    else
                        posLocal = new Vector3(
                            xActual,
                            alturaAcumulada + (altoEscalonBase * factorEscalaY / 2f),
                            f * profundidadEscalon * multZ
                        );

                    GameObject bloque = Instantiate(esPasillo ? Escalera : SeatersStandBlock, transform.TransformPoint(posLocal), transform.rotation, contenedor.transform);

                    if (esPasillo)
                        bloque.transform.localScale = new Vector3(1, factorEscalaY, 1); // igual que antes
                    else
                        bloque.transform.localScale = new Vector3(
                            anchoDeUnaPieza,
                            altoEscalonBase * factorEscalaY,
                            profundidadEscalon
                        );


                    if (invertir) bloque.transform.Rotate(0, 180, 0);
                                     
                    bloque.transform.SetParent(contenedor.transform);

                    bool esFilaSeguridad = (f >= filaInicioBoca - 1 && f <= filaInicioBoca + altoBoca) ||
                                           (tieneSegundaFilaVomitos && f >= filaInicioBoca2 - 1 && f <= filaInicioBoca2 + altoBoca);
                    bool esLadoVomito = (colAsiento == 0 || colAsiento == asientosEntrePasillos - 1);
                    bool esCaminable = esPasillo || (esFilaSeguridad && esLadoVomito);

                    Material matBloque = esCaminable ? GrisCemento : (Mathf.FloorToInt(xActual / anchoFranja) % 2 == 0 ? BlueColour : RedColour);
                    AplicarMaterialATodo(bloque, matBloque);
                    if (!esCaminable && !esSinAsientos) PonerAsientos(bloque.transform, matBloque);
                }

                if (esHuecoVomito && (f == filaInicioBoca || (tieneSegundaFilaVomitos && f == filaInicioBoca2)))
                {
                    GenerarMurosVomitoCompleto(xActual, f, multZ, contenedor.transform);

                    if (generarEscaleraVomito) 
                    {
                        GenerarEscaleraDescendenteVomito(xActual, f, multZ, contenedor.transform);
                    }                        
                }

                alturaAcumulada += altoRealFila;
            }

            xActual += esPasillo ? anchoPasilloEscalera : anchoDeUnaPieza;
            colAsiento = esPasillo ? 0 : colAsiento + 1;
        }

        float alturaAcumuladaFinal = 0f;
        for (int i = 0; i < numFilas; i++)
            alturaAcumuladaFinal += altoEscalonBase * ObtenerFactorParaFila(i);

        if (tienePalcoVIP) GenerarEstructuraVIP(multZ, margenLateralPalco, contenedor.transform);

        if (generarMuroSuperior) CrearMuroCierreSuperior(multZ, contenedor.transform, alturaAcumuladaFinal);

        if (generarMuroFrontal) GenerarMuroFrontal(multZ, contenedor.transform);

        if (generarMurosLateralesPlatea) GenerarMurosLateralesPlatea(multZ, contenedor.transform);

        if (generarTunel)
        {
            GenerarBancasSuplentes(multZ, contenedor.transform);
            GenerarEscalinataJugadores(multZ, contenedor.transform);
            GenerarTunelJugadores(multZ, contenedor.transform);
        }

        if (generarSoportes)
        {
            GenerarSoportes(multZ, contenedor.transform, anchoDeUnaPieza, largoMaximoTribuna);

            GenerarVigasTransversales(multZ, contenedor.transform, anchoDeUnaPieza, largoMaximoTribuna);
        }

        foreach (Transform hijo in contenedor.GetComponentsInChildren<Transform>())
        {
            if (hijo.gameObject != contenedor && Application.isPlaying)
                hijo.gameObject.isStatic = true;
        }

        if (Application.isPlaying)
            StaticBatchingUtility.Combine(contenedor);
    }

    int FilasEnX(float xActual)
    {
        if (!usarRecorteFilas) return numFilas;
        float t = xActual / largoMaximoTribuna;
        if (invertirRecorte) t = 1f - t;
        return Mathf.RoundToInt(Mathf.Lerp(filasEnExtremoIzquierdo, filasEnExtremoDerecho, t));
    }

    // Método para buscar qué multiplicador aplica a cada fila
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
    
    void GenerarMurosVomitoCompleto(float xInicioPasillo, int filaInicio, float mZ, Transform padre)
    {
        float compensacionPivote = (anchoDeUnaPieza / 2f);
        float xAjustado = xInicioPasillo - compensacionPivote;
        float[] posicionesX = { xAjustado, xAjustado + anchoPasilloEscalera };

        float yInicio = CalcularAlturaAcumuladaPlatea(filaInicio);
        float zInicio = filaInicio * profundidadEscalon * mZ;
        float yFin = CalcularAlturaAcumuladaPlatea(filaInicio + altoBoca);
        float zFin = (filaInicio + altoBoca) * profundidadEscalon * mZ;

        foreach (float x in posicionesX)
        {
            GameObject muroGO = new GameObject("Muro_Lateral_Vomito");
            muroGO.transform.SetParent(padre);
            muroGO.transform.localPosition = Vector3.zero;
            muroGO.transform.localRotation = Quaternion.identity;
            muroGO.AddComponent<MeshFilter>().mesh = CrearMeshMuro(x, yInicio, zInicio, yFin, zFin);
            muroGO.AddComponent<MeshRenderer>().sharedMaterial = BlueColour;
        }

        GameObject dintel = GameObject.CreatePrimitive(PrimitiveType.Cube);
        dintel.name = "Muro_Superior_Vomito";
        dintel.transform.SetParent(padre);
        dintel.transform.localScale = new Vector3(anchoPasilloEscalera, 1.0f, 0.1f);
        dintel.transform.localPosition = new Vector3(xAjustado + (anchoPasilloEscalera / 2f), yFin + 0.5f, zFin);
        dintel.transform.localRotation = Quaternion.identity;
        dintel.GetComponent<Renderer>().sharedMaterial = BlueColour;
        DestroyImmediate(dintel.GetComponent<BoxCollider>());
    }

    Mesh CrearMeshMuro(float x, float yI, float zI, float yF, float zF)
    {
        Mesh mesh = new Mesh();
        float g = 0.05f; float hM = 1.0f;
        Vector3[] v = new Vector3[8];
        v[0] = new Vector3(x - g, yI, zI); v[1] = new Vector3(x - g, yI + hM, zI);
        v[2] = new Vector3(x - g, yF, zF); v[3] = new Vector3(x - g, yF + hM, zF);
        v[4] = new Vector3(x + g, yI, zI); v[5] = new Vector3(x + g, yI + hM, zI);
        v[6] = new Vector3(x + g, yF, zF); v[7] = new Vector3(x + g, yF + hM, zF);
        mesh.vertices = v;
        mesh.triangles = new int[] { 0, 1, 2, 1, 3, 2, 4, 6, 5, 5, 6, 7, 0, 4, 1, 4, 5, 1, 2, 3, 6, 3, 7, 6, 1, 5, 3, 5, 7, 3, 0, 2, 4, 2, 6, 4 };
        mesh.RecalculateNormals();
        return mesh;
    }

    void CrearMuroCierreSuperior(float mZ, Transform padre, float yFinal)
    {
        if (!usarRecorteFilas)
        {
            float anchoTotal = largoMaximoTribuna;
            Vector3 posLocal = new Vector3(anchoTotal / 2f, yFinal, (numFilas - 1) * profundidadEscalon * mZ);
            GameObject muro = GameObject.CreatePrimitive(PrimitiveType.Cube);
            muro.name = "Muro_Cierre_Superior";
            muro.transform.SetParent(padre);
            muro.transform.localScale = new Vector3(anchoTotal, alturaMuroSuperior, 0.2f);
            muro.transform.localPosition = posLocal + new Vector3(0, alturaMuroSuperior / 2f, 0);
            muro.transform.localRotation = Quaternion.identity;
            muro.GetComponent<Renderer>().sharedMaterial = BlueColour;
            DestroyImmediate(muro.GetComponent<BoxCollider>());
            return;
        }

        // Calcular los dos extremos del muro
        int filasIzq = FilasEnX(0);
        int filasDir = FilasEnX(largoMaximoTribuna);

        float yIzq = 0f;
        for (int i = 0; i < filasIzq; i++)
            yIzq += altoEscalonBase * ObtenerFactorParaFila(i);
        float zIzq = filasIzq * profundidadEscalon * mZ; ;

        float yDer = 0f;
        for (int i = 0; i < filasDir; i++)
            yDer += altoEscalonBase * ObtenerFactorParaFila(i);
        float zDer = filasDir * profundidadEscalon * mZ;

        float grosor = 0.2f;
        float g = grosor / 2f;

        GameObject muroGO = new GameObject("Muro_Cierre_Superior");
        muroGO.transform.SetParent(padre);
        muroGO.transform.localPosition = Vector3.zero;
        muroGO.transform.localRotation = Quaternion.identity;

        Mesh mesh = new Mesh();
        Vector3[] v = new Vector3[8];
        v[0] = new Vector3(0, yIzq, zIzq - g);
        v[1] = new Vector3(0, yIzq + alturaMuroSuperior, zIzq - g);
        v[2] = new Vector3(largoMaximoTribuna, yDer, zDer - g);
        v[3] = new Vector3(largoMaximoTribuna, yDer + alturaMuroSuperior, zDer - g);
        v[4] = new Vector3(0, yIzq, zIzq + g);
        v[5] = new Vector3(0, yIzq + alturaMuroSuperior, zIzq + g);
        v[6] = new Vector3(largoMaximoTribuna, yDer, zDer + g);
        v[7] = new Vector3(largoMaximoTribuna, yDer + alturaMuroSuperior, zDer + g);

        mesh.vertices = v;
        mesh.triangles = new int[] {
        0, 1, 2, 1, 3, 2,  // cara frontal
        4, 6, 5, 5, 6, 7,  // cara trasera
        0, 4, 1, 4, 5, 1,  // lado izquierdo
        2, 3, 6, 3, 7, 6,  // lado derecho
        1, 5, 3, 5, 7, 3,  // techo
        0, 2, 4, 2, 6, 4   // piso
    };
        mesh.RecalculateNormals();

        muroGO.AddComponent<MeshFilter>().mesh = mesh;
        muroGO.AddComponent<MeshRenderer>().sharedMaterial = BlueColour;
    }
    
    void PonerAsientos(Transform padre, Material mat)
    {
        if (AsientoPlatea == null) return;
        for (int i = 0; i < 2; i++)
        {
            GameObject asiento = Instantiate(AsientoPlatea);
            asiento.transform.localScale = Vector3.one;
            
            Vector3 posLocal = new Vector3((i == 0) ? -0.25f : 0.25f, 0.815f, -0.25f);

            asiento.transform.position = padre.TransformPoint(posLocal);
            asiento.transform.rotation = padre.rotation * Quaternion.Euler(0, 90f, 0f);

            asiento.transform.SetParent(padre, true);
            AplicarMaterialATodo(asiento, mat);
        }
    }



    void AplicarMaterialATodo(GameObject obj, Material mat)
    {
        foreach (var r in obj.GetComponentsInChildren<MeshRenderer>()) r.sharedMaterial = mat;
    }

    float CalcularAlturaAcumuladaPlatea(int fila)
    {
        float altura = 0f;
        for (int i = 0; i < fila; i++)
            altura += altoEscalonBase * ObtenerFactorParaFila(i);
        return altura;
    }

    [Header("Muros Platea")]
    public bool generarMuroFrontal = true;
    public bool generarMurosLateralesPlatea = true;
    public float alturaMuroPlatea = 1.0f;
    public float profundidadPisoFrontal = 0.5f;
    public Material MaterialMuroPlatea;

    void GenerarMuroFrontal(float mZ, Transform padre)
    {
        if (!generarMuroFrontal) return;

        float margenLateralPalco = (largoMaximoTribuna - largoPalcoVIP) / 2f;

        float ySegmentoBajo = 0f;
        float ySegmentoAlto = 0f;
        for (int i = 0; i < filasQueOcupaPalco; i++)
            ySegmentoAlto += altoEscalonBase * ObtenerFactorParaFila(i);


        if (!tienePalcoVIP)
        {
            if (!generarTunel)
            {
                CrearSegmentoMuroFrontal(padre, 0, largoMaximoTribuna, ySegmentoBajo, 0f, mZ);
            }
            else
            {
                float centroX = largoMaximoTribuna / 2f;
                float inicioTunel = centroX - anchoTunel / 2f;
                float finTunel = centroX + anchoTunel / 2f;
                float inicioBancaIzq = inicioTunel - anchoZonaBancas;
                float finBancaDer = finTunel + anchoZonaBancas;

                float yBancas = CalcularAlturaAcumuladaPlatea(filasQuitadasBancas);
                float yTunel = CalcularAlturaAcumuladaPlatea(filasQuitadasTunel);

                float zBancas = (filasQuitadasBancas * profundidadEscalon - profundidadEscalon / 2f) * mZ;
                float zTunel = (filasQuitadasTunel * profundidadEscalon - profundidadEscalon / 2f) * mZ;

                CrearSegmentoMuroFrontal(padre, 0, inicioBancaIzq, ySegmentoBajo, 0f, mZ);
                CrearSegmentoMuroFrontal(padre, inicioBancaIzq, inicioTunel, yBancas, zBancas, mZ);
                CrearSegmentoMuroFrontal(padre, inicioTunel, finTunel, yTunel, zTunel, mZ);
                CrearSegmentoMuroFrontal(padre, finTunel, finBancaDer, yBancas, zBancas, mZ);
                CrearSegmentoMuroFrontal(padre, finBancaDer, largoMaximoTribuna, ySegmentoBajo, 0f, mZ);

                // Transicion normal -> bancas izquierda
                CrearTransicionMuroFrontal(padre, inicioBancaIzq, ySegmentoBajo, 0f, yBancas, zBancas, mZ);
                // Transicion bancas -> tunel izquierda
                CrearTransicionMuroFrontal(padre, inicioTunel, yBancas, zBancas, yTunel, zTunel, mZ);
                // Transicion tunel -> bancas derecha
                CrearTransicionMuroFrontal(padre, finTunel, yTunel, zTunel, yBancas, zBancas, mZ);
                // Transicion bancas -> normal derecha
                CrearTransicionMuroFrontal(padre, finBancaDer, yBancas, zBancas, ySegmentoBajo, 0f, mZ);

            }
        }


        else
        {
            // Segmento izquierdo (fila 0)
            CrearSegmentoMuroFrontal(padre, 0, margenLateralPalco, ySegmentoBajo, 0f, mZ);

            // Triangulo de transicion izquierdo
            CrearTrianguloTransicion(padre, margenLateralPalco, ySegmentoBajo, ySegmentoAlto, 0f, mZ, true);

            // Segmento central (filasQueOcupaPalco)
            float zEscalonAlto = filasQueOcupaPalco * profundidadEscalon * mZ;
            CrearSegmentoMuroFrontal(padre, margenLateralPalco, largoMaximoTribuna - margenLateralPalco, ySegmentoAlto, zEscalonAlto, mZ);

            // Triangulo de transicion derecho
            CrearTrianguloTransicion(padre, largoMaximoTribuna - margenLateralPalco, ySegmentoBajo, ySegmentoAlto, 0f, mZ, false);

            // Segmento derecho (fila 0)
            CrearSegmentoMuroFrontal(padre, largoMaximoTribuna - margenLateralPalco, largoMaximoTribuna, ySegmentoBajo, 0f, mZ);

            // Pared triangular izquierda
            CrearParedTriangularPalco(padre, margenLateralPalco, ySegmentoAlto, mZ, true);
            // Pared triangular derecha
            CrearParedTriangularPalco(padre, largoMaximoTribuna - margenLateralPalco, ySegmentoAlto, mZ, false);
        }
    }

    void CrearSegmentoMuroFrontal(Transform padre, float xInicio, float xFin, float yBase, float zEscalon, float mZ)
    {
        float grosor = 0.1f;
        float g = grosor / 2f;
        float signo = (mZ >= 0) ? -1f : 1f;
        float zPiso = zEscalon + profundidadPisoFrontal * signo;
        float zMuro = zPiso + grosor * signo;

        Vector3 posMuroMundo = transform.TransformPoint(new Vector3(0, 0, zMuro));
        Vector3 posMuroLocal = padre.InverseTransformPoint(posMuroMundo);
        zMuroFrontalCalculado = posMuroLocal.z;

        // Piso
        GameObject pisoGO = new GameObject("Piso_Frontal");
        pisoGO.transform.SetParent(padre);
        pisoGO.transform.localPosition = Vector3.zero;
        pisoGO.transform.localRotation = Quaternion.identity;

        Mesh meshPiso = new Mesh();
        Vector3[] vPiso = new Vector3[4];

        vPiso[0] = new Vector3(xInicio, yBase, zEscalon);
        vPiso[1] = new Vector3(xFin, yBase, zEscalon);
        vPiso[2] = new Vector3(xInicio, yBase, zPiso);
        vPiso[3] = new Vector3(xFin, yBase, zPiso);

        meshPiso.vertices = vPiso;

        meshPiso.triangles = new int[] { 0, 2, 1, 1, 2, 3 };
        meshPiso.RecalculateNormals();
        pisoGO.AddComponent<MeshFilter>().mesh = meshPiso;
        pisoGO.AddComponent<MeshRenderer>().sharedMaterial = MaterialMuroPlatea;

        // Muro
        GameObject muroGO = new GameObject("Muro_Frontal");
        muroGO.transform.SetParent(padre);
        muroGO.transform.localPosition = Vector3.zero;
        muroGO.transform.localRotation = Quaternion.identity;

        Mesh meshMuro = new Mesh();
        Vector3[] v = new Vector3[8];
        v[0] = new Vector3(xInicio, yBase, zMuro - g);
        v[1] = new Vector3(xInicio, yBase + alturaMuroPlatea, zMuro - g);
        v[2] = new Vector3(xFin, yBase, zMuro - g);
        v[3] = new Vector3(xFin, yBase + alturaMuroPlatea, zMuro - g);
        v[4] = new Vector3(xInicio, yBase, zMuro + g);
        v[5] = new Vector3(xInicio, yBase + alturaMuroPlatea, zMuro + g);
        v[6] = new Vector3(xFin, yBase, zMuro + g);
        v[7] = new Vector3(xFin, yBase + alturaMuroPlatea, zMuro + g);

        meshMuro.vertices = v;
        meshMuro.triangles = new int[] {
        0, 1, 2, 1, 3, 2,
        4, 6, 5, 5, 6, 7,
        0, 4, 1, 4, 5, 1,
        2, 3, 6, 3, 7, 6,
        1, 5, 3, 5, 7, 3,
        0, 2, 4, 2, 6, 4
    };
        meshMuro.RecalculateNormals();
        muroGO.AddComponent<MeshFilter>().mesh = meshMuro;
        muroGO.AddComponent<MeshRenderer>().sharedMaterial = MaterialMuroPlatea;
    }
   
    void CrearTrianguloTransicion(Transform padre, float x, float yBajo, float yAlto, float zBajo, float mZ, bool esIzquierdo)
    {
            float zAlto = filasQueOcupaPalco * profundidadEscalon * mZ;
            float signo = (mZ >= 0) ? -1f : 1f;
            float zPisoBajo = zBajo + profundidadPisoFrontal * signo;
            float zPisoAlto = zAlto + profundidadPisoFrontal * signo;
            float grosor = 0.1f;
            float g = grosor / 2f;

            GameObject triGO = new GameObject("Triangulo_Transicion");
            triGO.transform.SetParent(padre);
            triGO.transform.localPosition = Vector3.zero;
            triGO.transform.localRotation = Quaternion.identity;

            Mesh mesh = new Mesh();

            // 6 vertices: 3 del triangulo en cara frontal, 3 en cara trasera
            Vector3[] v = new Vector3[6];
            v[0] = new Vector3(x - g, yBajo, zPisoBajo);           // abajo frente
            v[1] = new Vector3(x - g, yAlto, zPisoAlto);           // arriba frente
            v[2] = new Vector3(x - g, yBajo + alturaMuroPlatea, zPisoBajo); // arriba del muro frente
            v[3] = new Vector3(x + g, yBajo, zPisoBajo);           // abajo atras
            v[4] = new Vector3(x + g, yAlto, zPisoAlto);           // arriba atras
            v[5] = new Vector3(x + g, yBajo + alturaMuroPlatea, zPisoBajo); // arriba del muro atras

            mesh.vertices = v;

            if (esIzquierdo)
                mesh.triangles = new int[] {
            0, 1, 2,       // cara frontal
            3, 5, 4,       // cara trasera
            0, 3, 1, 3, 4, 1,  // lado inferior
            1, 4, 2, 4, 5, 2,  // lado superior
            0, 2, 3, 2, 5, 3   // lado vertical
        };
            else
                mesh.triangles = new int[] {
            0, 2, 1,       // cara frontal invertida
            3, 4, 5,       // cara trasera invertida
            0, 1, 3, 3, 1, 4,  // lado inferior
            1, 2, 4, 4, 2, 5,  // lado superior
            0, 3, 2, 2, 3, 5
             };

        mesh.RecalculateNormals();
        triGO.AddComponent<MeshFilter>().mesh = mesh;
        triGO.AddComponent<MeshRenderer>().sharedMaterial = MaterialMuroPlatea;
    }

    void CrearParedTriangularPalco(Transform padre, float x, float yAlto, float mZ, bool esIzquierdo)
    {
        float zBorde = -profundidadEscalon / 2f * mZ; // borde frontal primer escalon
        float zBordeAlto = (filasQueOcupaPalco * profundidadEscalon - profundidadEscalon / 2f) * mZ;
        float grosor = 0.1f;
        float g = grosor / 2f;

        GameObject paredGO = new GameObject("Pared_Triangular_Palco");
        paredGO.transform.SetParent(padre);
        paredGO.transform.localPosition = Vector3.zero;
        paredGO.transform.localRotation = Quaternion.identity;

        Mesh mesh = new Mesh();

        Vector3[] v = new Vector3[6];
        // Cara frontal (x - g)
        v[0] = new Vector3(x - g, 0f, zBorde);      // esquina inferior frontal
        v[1] = new Vector3(x - g, 0f, zBordeAlto);  // esquina inferior trasera
        v[2] = new Vector3(x - g, yAlto, zBordeAlto); // esquina superior
                                                      // Cara trasera (x + g)
        v[3] = new Vector3(x + g, 0f, zBorde);
        v[4] = new Vector3(x + g, 0f, zBordeAlto);
        v[5] = new Vector3(x + g, yAlto, zBordeAlto);

        mesh.vertices = v;

        if (esIzquierdo)
            mesh.triangles = new int[] {
            0, 2, 1,       // cara frontal
            3, 4, 5,       // cara trasera
            0, 1, 3, 3, 1, 4,  // base inferior
            1, 2, 4, 4, 2, 5,  // cara inclinada
            0, 3, 2, 3, 5, 2   // cara vertical
        };
        else
            mesh.triangles = new int[] {
            0, 1, 2,       // cara frontal
            3, 5, 4,       // cara trasera
            0, 3, 1, 3, 4, 1,  // base inferior
            1, 4, 2, 4, 5, 2,  // cara inclinada
            0, 2, 3, 2, 5, 3   // cara vertical
        };

        mesh.RecalculateNormals();
        paredGO.AddComponent<MeshFilter>().mesh = mesh;
        paredGO.AddComponent<MeshRenderer>().sharedMaterial = MaterialMuroPlatea;
    }

    void GenerarMurosLateralesPlatea(float mZ, Transform padre)
    {
        if (!generarMurosLateralesPlatea) return;

        float margenLateralPalco = (largoMaximoTribuna - largoPalcoVIP) / 2f;

        // Muros laterales extremos
        CrearMuroLateralPlatea(padre, 0f, mZ, false);
        CrearMuroLateralPlatea(padre, largoMaximoTribuna, mZ, true);

        // Muros laterales internos (solo si hay palcos VIP)
        if (tienePalcoVIP)
        {
            CrearMuroLateralInternoPlatea(padre, margenLateralPalco, mZ, true);
            CrearMuroLateralInternoPlatea(padre, largoMaximoTribuna - margenLateralPalco, mZ, false);
        }
    }

    void CrearMuroLateralPlatea(Transform padre, float x, float mZ, bool esIzquierdo)
    {
        float grosor = 0.1f;
        float g = grosor / 2f;

        // Calcular altura acumulada total
        float yFinal = 0f;
        for (int i = 0; i < numFilas; i++)
            yFinal += altoEscalonBase * ObtenerFactorParaFila(i);

        float zInicio = -profundidadEscalon / 2f * mZ; // borde frontal primer escalon
        float zFin = (numFilas - 1) * profundidadEscalon * mZ + profundidadEscalon / 2f * mZ;

        // Pendiente del ultimo tramo
        int filaUltimoRango = 0;
        if (rangosDeAlzada != null && rangosDeAlzada.Length > 0)
            filaUltimoRango = rangosDeAlzada[rangosDeAlzada.Length - 1].filaInicio;

        float yUltimoRangoInicio = CalcularAlturaAcumuladaPlatea(filaUltimoRango);
        float zUltimoRangoInicio = (filaUltimoRango * profundidadEscalon - profundidadEscalon / 2f) * mZ;
        float longitudZ = Mathf.Abs(zFin - zInicio);
        float pendiente = (yFinal - yUltimoRangoInicio) / Mathf.Abs(zFin - zUltimoRangoInicio);
        float yFinalCorregida = 0f + longitudZ * pendiente;

        GameObject muroGO = new GameObject("Muro_Lateral_Extremo");
        muroGO.transform.SetParent(padre);
        muroGO.transform.localPosition = Vector3.zero;
        muroGO.transform.localRotation = Quaternion.identity;

        Mesh mesh = new Mesh();
        Vector3[] v = new Vector3[8];
        v[0] = new Vector3(x - g, 0f, zInicio);
        v[1] = new Vector3(x - g, alturaMuroPlatea, zInicio);
        v[2] = new Vector3(x - g, yFinalCorregida, zFin);
        v[3] = new Vector3(x - g, yFinalCorregida + alturaMuroPlatea, zFin);
        v[4] = new Vector3(x + g, 0f, zInicio);
        v[5] = new Vector3(x + g, alturaMuroPlatea, zInicio);
        v[6] = new Vector3(x + g, yFinalCorregida, zFin);
        v[7] = new Vector3(x + g, yFinalCorregida + alturaMuroPlatea, zFin);

        mesh.vertices = v;

        if (esIzquierdo)
            mesh.triangles = new int[] {
            0, 1, 2, 1, 3, 2,
            4, 6, 5, 5, 6, 7,
            0, 4, 1, 4, 5, 1,
            2, 3, 6, 3, 7, 6,
            1, 5, 3, 5, 7, 3,
            0, 2, 4, 2, 6, 4
        };
        else
            mesh.triangles = new int[] {
            0, 2, 1, 1, 2, 3,
            4, 5, 6, 5, 7, 6,
            0, 1, 4, 4, 1, 5,
            2, 6, 3, 3, 6, 7,
            1, 3, 5, 5, 3, 7,
            0, 4, 2, 2, 4, 6
        };

        mesh.RecalculateNormals();
        muroGO.AddComponent<MeshFilter>().mesh = mesh;
        muroGO.AddComponent<MeshRenderer>().sharedMaterial = MaterialMuroPlatea;
    }

    void CrearMuroLateralInternoPlatea(Transform padre, float x, float mZ, bool esIzquierdo)
    {
        float grosor = 0.1f;
        float g = grosor / 2f;

        // Solo cubre las filas cortadas por los palcos
        float yFinal = CalcularAlturaAcumuladaPlatea(filasQueOcupaPalco);
        float zInicio = -profundidadEscalon / 2f * mZ;
        float zFin = (filasQueOcupaPalco * profundidadEscalon - profundidadEscalon / 2f) * mZ;

        GameObject muroGO = new GameObject("Muro_Lateral_Interno");
        muroGO.transform.SetParent(padre);
        muroGO.transform.localPosition = Vector3.zero;
        muroGO.transform.localRotation = Quaternion.identity;

        Mesh mesh = new Mesh();
        Vector3[] v = new Vector3[8];
        v[0] = new Vector3(x - g, 0f, zInicio);
        v[1] = new Vector3(x - g, alturaMuroPlatea, zInicio);
        v[2] = new Vector3(x - g, yFinal, zFin);
        v[3] = new Vector3(x - g, yFinal + alturaMuroPlatea, zFin);
        v[4] = new Vector3(x + g, 0f, zInicio);
        v[5] = new Vector3(x + g, alturaMuroPlatea, zInicio);
        v[6] = new Vector3(x + g, yFinal, zFin);
        v[7] = new Vector3(x + g, yFinal + alturaMuroPlatea, zFin);

        mesh.vertices = v;

        if (esIzquierdo)
            mesh.triangles = new int[] {
            0, 1, 2, 1, 3, 2,
            4, 6, 5, 5, 6, 7,
            0, 4, 1, 4, 5, 1,
            2, 3, 6, 3, 7, 6,
            1, 5, 3, 5, 7, 3,
            0, 2, 4, 2, 6, 4
        };
        else
            mesh.triangles = new int[] {
            0, 2, 1, 1, 2, 3,
            4, 5, 6, 5, 7, 6,
            0, 1, 4, 4, 1, 5,
            2, 6, 3, 3, 6, 7,
            1, 3, 5, 5, 3, 7,
            0, 4, 2, 2, 4, 6
        };

        mesh.RecalculateNormals();
        muroGO.AddComponent<MeshFilter>().mesh = mesh;
        muroGO.AddComponent<MeshRenderer>().sharedMaterial = MaterialMuroPlatea;
    }


    void GenerarEstructuraVIP(float multZ, float margenLateralPalco, Transform padre)
    {
        float anchoPalcoUnitario = 4.0f;
        int cantidadPalcos = Mathf.FloorToInt(largoPalcoVIP / anchoPalcoUnitario);

        float zInicioBajoPlatea = (profundidadEscalon * filasQueOcupaPalco) - (profundidadEscalon / 2f);
        float zCentro = (zInicioBajoPlatea + 9.1f / 2f+0.5f) * multZ;
        float yCentro = 3.6f / 2f + 0.5f;

        for (int i = 0; i < cantidadPalcos; i++)
        {
            float posX = margenLateralPalco + (i * anchoPalcoUnitario) + (anchoPalcoUnitario / 2f);

            GameObject palco = Instantiate(PrefabPalcoVIP, padre);
            palco.transform.localPosition = new Vector3(posX, yCentro, zCentro);
           
            palco.transform.localRotation = invertir ? Quaternion.Euler(0, 0, 0) : Quaternion.Euler(0, 180, 0);
        }
    }

    void GenerarSoporte(float x, float mZ, Transform padre)
    {
        float zInterior = filaVigaVerticalInterior * profundidadEscalon * mZ;
        float zExterior = filaVigaVerticalExterior * profundidadEscalon * mZ;
        float zArranque = filaArranqueDiagonal * profundidadEscalon * mZ;
        float zFinal = (numFilas - 1) * profundidadEscalon * mZ + profundidadEscalon / 2f * mZ;

        float yArranque = CalcularAlturaAcumuladaCabecera(filaArranqueDiagonal);
        float yFinDiagonal = CalcularAlturaAcumuladaCabecera(numFilas - 1);

        // Calcular yBase desde ground0Level
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


    void GenerarSoportes(float mZ, Transform padre, float anchoDeUnaPieza, float largoMaximoTribuna)
    {
        if (!generarSoportes || MaterialVigas == null) return;

        // Construir lista de zonas prohibidas
        List<(float xInicio, float xFin)> zonasProhibidas = new List<(float, float)>();

        // Zona tunel
        if (generarTunel)
        {
            float centroX = largoMaximoTribuna / 2f;
            zonasProhibidas.Add((centroX - anchoTunel / 2f, centroX + anchoTunel / 2f));
        }

        // Zonas vomitos
        float xActualVomito = 0;
        int colAsientoVomito = 0;
        while (xActualVomito < largoMaximoTribuna)
        {
            bool esPasillo = (colAsientoVomito >= asientosEntrePasillos);
            if (esPasillo)
                zonasProhibidas.Add((xActualVomito, xActualVomito + anchoPasilloEscalera));

            xActualVomito += esPasillo ? anchoPasilloEscalera : anchoDeUnaPieza;
            colAsientoVomito = esPasillo ? 0 : colAsientoVomito + 1;
        }

        float anchoTotal = largoMaximoTribuna;

        float xActual = separacionSoportes / 2f;

        float xAnterior = -1f;

        while (xActual < anchoTotal)
        {
            float xAjustada = AjustarXSoporte(xActual, zonasProhibidas);

            // Solo generar si no es la misma posicion que el soporte anterior
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

    void GenerarVigasTransversales(float mZ, Transform padre, float anchoDeUnaPieza, float largoMaximoTribuna)
    {
        
        if (!tieneVigasTransversales) return;

        float anchoTotal = (largoMaximoTribuna/anchoDeUnaPieza) * anchoDeUnaPieza;
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

    float CalcularAlturaAcumuladaCabecera(int fila)
    {
        float altura = 0f;
        for (int i = 0; i < fila; i++)
            altura += altoEscalonBase * ObtenerFactorParaFila(i);
        return altura;
    }

    void GenerarBancasSuplentes(float mZ, Transform padre)
    {
        if (!generarTunel || prefabBancaSuplentes == null) return;

        float centroX = (largoMaximoTribuna / 2f)+1.8f;
        float inicioTunel = centroX - anchoTunel / 2f;
        float finTunel = centroX + anchoTunel / 2f;
        float inicioBancaIzq = inicioTunel - anchoZonaBancas;
        float finBancaDer = finTunel + anchoZonaBancas;

        float yBanca = CalcularAlturaAcumuladaPlatea(filasQuitadasBancas) + altoEscalonBase * (filasQuitadasBancas + 1);
        
        //float zBanca = 5.17f;

        MeshFilter mf = null;
        foreach (MeshFilter m in prefabBancaSuplentes.GetComponentsInChildren<MeshFilter>())
        {
            if (m.gameObject.name == "MuroFrontal")
            {
                mf = m;
                break;
            }
        }
        float largoHorizontalBanca = mf != null ? mf.sharedMesh.bounds.size.x : 11.1f;

        float zBanca = (largoHorizontalBanca / 2f - profundidadEscalon / 2f) * mZ;


        float xCentroIzq = (inicioBancaIzq + inicioTunel) / 2f;
        GameObject bancaIzq = Instantiate(prefabBancaSuplentes, padre);
        bancaIzq.transform.localPosition = new Vector3(xCentroIzq, yBanca, zBanca);
        bancaIzq.transform.localRotation = Quaternion.Euler(0, 180, 0);

        float xCentroDer = (finTunel + finBancaDer) / 2f;
        GameObject bancaDer = Instantiate(prefabBancaSuplentes, padre);
        bancaDer.transform.localPosition = new Vector3(xCentroDer, yBanca, zBanca);
        bancaDer.transform.localRotation = Quaternion.Euler(0, 180, 0);
    }

    void GenerarEscalinataJugadores(float mZ, Transform padre)
    {
        if (!generarTunel) return;

        int numEscalones = 8;
        float profEscalon = 0.30f;
        float altoEscalon = 0.20f;
        float anchoEscalinata = anchoTunel;
        float centroX = largoMaximoTribuna / 2f;


        //float zInicio = 1.89001012f;

        float yInicio = -(numEscalones * altoEscalon - altoEscalon / 2f);
        float zInicio = filasQuitadasTunel * profundidadEscalon - profundidadEscalon / 2f;

        for (int e = 0; e < numEscalones; e++)
        {
            float yEscalon = yInicio + e * altoEscalon;
            float zEscalon = zInicio + (e * profEscalon + profEscalon / 2f) * (invertir ? 1f : -1f);

            GameObject escalonGO = GameObject.CreatePrimitive(PrimitiveType.Cube);
            escalonGO.name = $"Escalon_Tunel_{e}";
            escalonGO.transform.SetParent(padre);
            escalonGO.transform.localPosition = new Vector3(centroX, yEscalon + altoEscalon / 2f, zEscalon);
            escalonGO.transform.localScale = new Vector3(anchoEscalinata, altoEscalon, profEscalon);
            escalonGO.transform.localRotation = Quaternion.identity;

            if (GrisCemento != null)
                escalonGO.GetComponent<Renderer>().sharedMaterial = GrisCemento;

            DestroyImmediate(escalonGO.GetComponent<BoxCollider>());
        }
    }

    void CrearTransicionMuroFrontal(Transform padre, float x, float yInicio, float zInicio, float yFin, float zFin, float mZ)
    {
        float grosor = 0.1f;
        float g = grosor / 2f;
        float signo = (mZ >= 0) ? -1f : 1f;

        float zPisoInicio = zInicio + profundidadPisoFrontal * signo;
        float zMuroInicio = zPisoInicio + grosor * signo;

        float zPisoFin = zFin + profundidadPisoFrontal * signo;
        float zMuroFin = zPisoFin + grosor * signo;

        // Piso inclinado
        GameObject pisoGO = new GameObject("Piso_Transicion");
        pisoGO.transform.SetParent(padre);
        pisoGO.transform.localPosition = Vector3.zero;
        pisoGO.transform.localRotation = Quaternion.identity;

        Mesh meshPiso = new Mesh();
        Vector3[] vPiso = new Vector3[4];
        vPiso[0] = new Vector3(x - g, yInicio, zInicio);
        vPiso[1] = new Vector3(x + g, yInicio, zInicio);
        vPiso[2] = new Vector3(x - g, yFin, zFin);
        vPiso[3] = new Vector3(x + g, yFin, zFin);
        meshPiso.vertices = vPiso;
        meshPiso.triangles = new int[] { 0, 2, 1, 1, 2, 3 };
        meshPiso.RecalculateNormals();
        pisoGO.AddComponent<MeshFilter>().mesh = meshPiso;
        pisoGO.AddComponent<MeshRenderer>().sharedMaterial = MaterialMuroPlatea;

        // Muro inclinado
        GameObject muroGO = new GameObject("Muro_Transicion");
        muroGO.transform.SetParent(padre);
        muroGO.transform.localPosition = Vector3.zero;
        muroGO.transform.localRotation = Quaternion.identity;

        Mesh meshMuro = new Mesh();
        Vector3[] v = new Vector3[8];
        v[0] = new Vector3(x - g, yInicio, zMuroInicio - g);
        v[1] = new Vector3(x - g, yInicio + alturaMuroPlatea, zMuroInicio - g);
        v[2] = new Vector3(x - g, yFin, zMuroFin - g);
        v[3] = new Vector3(x - g, yFin + alturaMuroPlatea, zMuroFin - g);
        v[4] = new Vector3(x + g, yInicio, zMuroInicio + g);
        v[5] = new Vector3(x + g, yInicio + alturaMuroPlatea, zMuroInicio + g);
        v[6] = new Vector3(x + g, yFin, zMuroFin + g);
        v[7] = new Vector3(x + g, yFin + alturaMuroPlatea, zMuroFin + g);

        meshMuro.vertices = v;
        meshMuro.triangles = new int[] {
        0, 1, 2, 1, 3, 2,
        4, 6, 5, 5, 6, 7,
        0, 4, 1, 4, 5, 1,
        2, 3, 6, 3, 7, 6,
        1, 5, 3, 5, 7, 3,
        0, 2, 4, 2, 6, 4
    };
        meshMuro.RecalculateNormals();
        muroGO.AddComponent<MeshFilter>().mesh = meshMuro;
        muroGO.AddComponent<MeshRenderer>().sharedMaterial = MaterialMuroPlatea;
    }

    void GenerarTunelJugadores(float mZ, Transform padre)
    {
        if (!generarTunel) return;

        int numEscalones = 8;
        float profEscalon = 0.30f;
        float altoEscalon = 0.20f;
        float largoPiso = 20f;
        float alturaMaxParedes = 3.5f;
        float alturaMaxTecho = 7f;
        float centroX = largoMaximoTribuna / 2f;
        float xIzq = centroX - anchoTunel / 2f;
        float xDer = centroX + anchoTunel / 2f;
        float grosor = 0.1f;

        // Z y Y del inicio de la escalinata (escalon 0, el mas bajo)
        float zInicioEscalinata = filasQuitadasTunel * profundidadEscalon - profundidadEscalon / 2f;
        float yInicioEscalinata = -(numEscalones * altoEscalon - altoEscalon / 2f);

        // Z del final de la escalinata = inicio de la platea del tunel
        float zFinEscalinata = zInicioEscalinata + numEscalones * profEscalon;
        //float zFinPiso = zFinEscalinata + largoPiso;

        float zFinPiso = zInicioEscalinata + largoPiso;


        // --- PISO ---
        GameObject pisoGO = new GameObject("Piso_Tunel");
        pisoGO.transform.SetParent(padre);
        pisoGO.transform.localPosition = Vector3.zero;
        pisoGO.transform.localRotation = Quaternion.identity;

        Mesh meshPiso = new Mesh();
        Vector3[] vPiso = new Vector3[4];

        vPiso[0] = new Vector3(xIzq, yInicioEscalinata, zInicioEscalinata);
        vPiso[1] = new Vector3(xDer, yInicioEscalinata, zInicioEscalinata);
        vPiso[2] = new Vector3(xIzq, yInicioEscalinata, zFinPiso);
        vPiso[3] = new Vector3(xDer, yInicioEscalinata, zFinPiso);

        meshPiso.vertices = vPiso;
        meshPiso.triangles = new int[] { 0, 2, 1, 1, 2, 3 };
        meshPiso.RecalculateNormals();
        pisoGO.AddComponent<MeshFilter>().mesh = meshPiso;
        pisoGO.AddComponent<MeshRenderer>().sharedMaterial = BlueColour;

        // --- PAREDES LATERALES ---
        float[] posicionesX = { xIzq, xDer };
        string[] nombres = { "Pared_Lateral_Izq", "Pared_Lateral_Der" };

        for (int lado = 0; lado < 2; lado++)
        {
            float x = posicionesX[lado];
            List<Vector3> verts = new List<Vector3>();
            List<int> tris = new List<int>();

            // TRAMO 1: Triangulo bajo la escalinata
            // La altura maxima es desde yInicioEscalinata hasta Y=0
            float alturaTriangulo = Mathf.Abs(yInicioEscalinata); // = 1.50

            for (int e = 0; e < numEscalones; e++)
            {
                float yBaseEscalon = yInicioEscalinata + e * altoEscalon;
                float zEscalon = zInicioEscalinata - e * profEscalon;
                //float zSiguiente = zEscalon - profEscalon;
                float zSiguiente = zEscalon + profEscalon;

                // Cada escalon genera un segmento rectangular
                int idx = verts.Count;
                verts.Add(new Vector3(x, yBaseEscalon, zEscalon));
                verts.Add(new Vector3(x, yBaseEscalon + alturaTriangulo, zEscalon));
                verts.Add(new Vector3(x, yBaseEscalon, zSiguiente));
                verts.Add(new Vector3(x, yBaseEscalon + alturaTriangulo, zSiguiente));

                if (lado == 0)
                {
                    tris.AddRange(new int[] { idx, idx + 1, idx + 2, idx + 1, idx + 3, idx + 2 });
                }
                else
                {
                    tris.AddRange(new int[] { idx, idx + 2, idx + 1, idx + 1, idx + 2, idx + 3 });
                }
            }

            // TRAMO 2: Bajo la platea siguiendo pendiente hasta 3.5m, luego horizontal

            float zActual = zInicioEscalinata;
            float yTecho = 0f; // altura de la cara inferior del escalon de platea
            int filaActual = filasQuitadasTunel;

            while (zActual < zFinPiso)
            {
                float zSiguiente = Mathf.Min(zActual + profundidadEscalon, zFinPiso);
                float yTechoSiguiente = Mathf.Min(yTecho + altoEscalonBase * ObtenerFactorParaFila(filaActual), alturaMaxParedes);

                int idx = verts.Count;
                verts.Add(new Vector3(x, yInicioEscalinata, zActual));        // base
                verts.Add(new Vector3(x, yTecho, zActual));                    // techo
                verts.Add(new Vector3(x, yInicioEscalinata, zSiguiente));     // base
                verts.Add(new Vector3(x, yTechoSiguiente, zSiguiente));       // techo

                if (lado == 0)
                    tris.AddRange(new int[] { idx, idx + 1, idx + 2, idx + 1, idx + 3, idx + 2 });
                else
                    tris.AddRange(new int[] { idx, idx + 2, idx + 1, idx + 1, idx + 2, idx + 3 });

                zActual = zSiguiente;
                yTecho = yTechoSiguiente;
                filaActual++;
            }


            Mesh mesh = new Mesh();
            mesh.vertices = verts.ToArray();
            mesh.triangles = tris.ToArray();
            mesh.RecalculateNormals();

            GameObject paredGO = new GameObject(nombres[lado]);
            paredGO.transform.SetParent(padre);
            paredGO.transform.localPosition = Vector3.zero;
            paredGO.transform.localRotation = Quaternion.identity;
            paredGO.AddComponent<MeshFilter>().mesh = mesh;
            paredGO.AddComponent<MeshRenderer>().sharedMaterial = BlueColour;
        }

        // --- TECHO ---
        List<Vector3> vertsTecho = new List<Vector3>();
        List<int> trisTecho = new List<int>();

        float zTechoActual = zInicioEscalinata;
        float yTechoActual = 0f;
        int filaTecho = filasQuitadasTunel;
        bool techoComenzado = false;
        float zInicioTecho = zInicioEscalinata;

        // Primero encontrar donde la altura llega a 3 metros
        while (zTechoActual < zFinPiso && yTechoActual < alturaMaxTecho)
        {
            float yTechoSiguiente = yTechoActual + altoEscalonBase * ObtenerFactorParaFila(filaTecho);
            float zTechoSiguiente = Mathf.Min(zTechoActual + profundidadEscalon, zFinPiso);

            if (!techoComenzado && yTechoActual >= 3f)
            {
                techoComenzado = true;
                zInicioTecho = zTechoActual;
            }

            if (techoComenzado)
            {
                if (yTechoSiguiente > alturaMaxTecho) yTechoSiguiente = alturaMaxTecho;

                int idx = vertsTecho.Count;
                vertsTecho.Add(new Vector3(xIzq, yTechoActual, zTechoActual));
                vertsTecho.Add(new Vector3(xDer, yTechoActual, zTechoActual));
                vertsTecho.Add(new Vector3(xIzq, yTechoSiguiente, zTechoSiguiente));
                vertsTecho.Add(new Vector3(xDer, yTechoSiguiente, zTechoSiguiente));

                trisTecho.AddRange(new int[] { idx, idx + 2, idx + 1, idx + 1, idx + 2, idx + 3 });
            }

            zTechoActual = zTechoSiguiente;
            yTechoActual = yTechoSiguiente;
            filaTecho++;
        }

        Mesh meshTecho = new Mesh();
        meshTecho.vertices = vertsTecho.ToArray();
        meshTecho.triangles = trisTecho.ToArray();
        meshTecho.RecalculateNormals();

        GameObject techoGO = new GameObject("Techo_Tunel");
        techoGO.transform.SetParent(padre);
        techoGO.transform.localPosition = Vector3.zero;
        techoGO.transform.localRotation = Quaternion.identity;
        techoGO.AddComponent<MeshFilter>().mesh = meshTecho;
        techoGO.AddComponent<MeshRenderer>().sharedMaterial = BlueColour;
    }

    void GenerarEscaleraDescendenteVomito(float xPasillo, int filaInicio, float mZ, Transform padre)
    {
        float yBase = CalcularAlturaAcumuladaPlatea(filaInicio);
        float zBase = filaInicio * profundidadEscalon * mZ;
        float anchoPasillo = anchoPasilloEscalera;
        float xCentro = xPasillo + anchoPasillo / 2f;

        float anchoReal = 1f;
        Renderer r = Escalera.GetComponentInChildren<Renderer>();
        if (r != null) anchoReal = r.bounds.size.x;

        float factorY = altoEscalonVomito / altoEscalonBase;

        List<GameObject> escalonesGenerados = new List<GameObject>();

        Debug.Log($"anchoPasillo={anchoPasillo}, anchoReal={anchoReal}, resultado={anchoPasillo / anchoReal}");

        for (int e = 0; e < numEscalonesVomito; e++)
        {
            float yEscalon = yBase - (e + 1) * altoEscalonVomito;
            float zEscalon = zBase + (e * profundidadEscalonVomito + profundidadEscalonVomito / 2f) * mZ;

            GameObject escalonGO = Instantiate(Escalera, padre);

            escalonGO.transform.position = transform.TransformPoint(
                new Vector3(xCentro, yEscalon + altoEscalonVomito / 2f, zEscalon));

            escalonGO.transform.rotation = transform.rotation * Quaternion.Euler(0, 180, 0);

            escalonGO.transform.localScale = new Vector3(anchoPasillo/2, factorY, profundidadEscalonVomito / profundidadEscalon);

            if (materialEscaleraVomito != null) 
            {
                AplicarMaterialATodo(escalonGO, materialEscaleraVomito);
            }
                
            else if (GrisCemento != null) 
            {
                AplicarMaterialATodo(escalonGO, GrisCemento);
            }
                

            escalonesGenerados.Add(escalonGO);
        }

        if (generarEscaleraVomito && materialBarandilla != null)
            GenerarBarandillaEscalera(xPasillo, filaInicio, mZ, padre);
    }

    void GenerarBarandillaEscalera(float xPasillo, int filaInicio, float mZ, Transform padre)
    {
        float diametro = 0.05f;
        float alturaBarandilla = 1.0f;
        float anchoPasillo = anchoPasilloEscalera / 2f; // igual que en la escala de los escalones

        Renderer rEscalera = Escalera.GetComponentInChildren<Renderer>();
        float mitadPrefab = rEscalera != null ? rEscalera.bounds.size.x / 2f : 0.5f;

        float xIzq = xPasillo - mitadPrefab;

        float xDer = xPasillo + mitadPrefab + anchoPasillo;
        
        float yBase = CalcularAlturaAcumuladaPlatea(filaInicio);

        float zBase = filaInicio * profundidadEscalon * mZ;

        float[] posicionesX = { xIzq + diametro, xDer - diametro };

        foreach (float x in posicionesX)
        {
            // Top del primer escalon
            Vector3 pInicio = transform.TransformPoint(new Vector3(x,
                yBase - altoEscalonVomito + alturaBarandilla,
                zBase + profundidadEscalonVomito / 2f * mZ));

            // Top del ultimo escalon
            Vector3 pFin = transform.TransformPoint(new Vector3(x,
                yBase - numEscalonesVomito * altoEscalonVomito + alturaBarandilla,
                zBase + (numEscalonesVomito * profundidadEscalonVomito - profundidadEscalonVomito / 2f) * mZ));

            // Caño principal inclinado
            CrearCañoEntreDosPuntos(pInicio, pFin, diametro, padre);

            // Soporte vertical primer escalon
            Vector3 baseInicio = transform.TransformPoint(new Vector3(x,
                yBase - altoEscalonVomito,
                zBase + profundidadEscalonVomito / 2f * mZ));
            CrearCañoEntreDosPuntos(baseInicio, pInicio, diametro, padre);

            // Soporte vertical ultimo escalon
            Vector3 baseFin = transform.TransformPoint(new Vector3(x,
                yBase - numEscalonesVomito * altoEscalonVomito,
                zBase + (numEscalonesVomito * profundidadEscalonVomito - profundidadEscalonVomito / 2f) * mZ));
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