using System.Collections.Generic;
using System.Linq;
using UnityEditor.ProBuilder;
using UnityEngine;
using UnityEngine.ProBuilder;
using static UnityEditor.Searcher.SearcherWindow.Alignment;

public class UpperCurveStandWithWalkpathScript : MonoBehaviour
{
    public enum TipoCelda { Bloque, Escalera, Vacio, BloqueLibre }

    struct Celda
    {
        public int columna;
        public int fila;
        public TipoCelda tipo;
        public float angStart;
        public float angEnd;
        public bool esPasillo;
    }

    [System.Serializable]
    public struct RangoAlzada
    {
        public int filaInicio;
        public float multiplicador;
    }

    [Header("Prefabs")]
    public GameObject BlockPlateaCurva;
    public GameObject AsientoPlatea;
    public GameObject Escalera;
    public GameObject PrefabParaavalanchas;

    [Header("Ajustes de la Tribuna")]
    public int cantidadPiezas = 90;
    public float anguloTotal = 90f;
    public float radioInferior = 10f;
    public float anchoEscalon = 0.80f;
    public float altoEscalon = 0.40f;

    [Header("Lógica de Recorte")]
    public bool invertirSentido = false;
    public int filasMaximas = 48;
    public int filasMinimas = 17;

    [Header("Filtros de Contenedores")]
    public string tagContenedores = "SectorEstadio";

    [Header("Materiales")]
    public Material Material;
    public Material GrisCemento;

    [Header("Es codo de platea")]
    public bool esCodoPlatea = true;

    [Header("Forma del Codo")]
    
    [Tooltip("Mezcla entre sigmoide (0) y lineal (1). Con 0 el recorte demora mas al principio y cae bruscamente al final. Con 1 el recorte es completamente lineal. Valores recomendados: 0.3-0.5")]
    public float mezclaLineal = 0.4f;

    [Tooltip("Exponente de la sigmoide. Mayor valor = mas demora al principio y caida mas brusca. Valores recomendados: 3-6")]
    public float exponente = 3f;

    [Tooltip("Si true usa forma personalizada (sigmoide) para el recorte de filas. Si false usa recorte circular puro.")]
    public bool usarFormaPersonalizada = true;



    [Header("Configuración de Alzada")]
    public RangoAlzada[] rangosDeAlzada;

    [Header("Vómitos")]
    public int vomitosPorLinea = 1;
    public bool tieneSegundaLinea = false;
    public float anchoPasilloVomito = 2.0f;
    public int altoBocaVomito = 4;
    public int filaInicioBocaVomito = 5;
    public int filaInicioBoca2 = 25;
    public float metrosLibresAlrededor = 0.5f;

    [Header("Muros de Vómitos")]
    public bool generarMurosVomitos = true;
    public float alturaMuroVomito = 1.5f;
    public Material MaterialMuroVomito;

    [Header("Paraavalanchas")]
    public bool generarParaavalanchas = true;
    public int filaInicioPara = 10;
    public int frecuenciaFilasPara = 10;
    public float distanciaEntrePares = 4.0f;
    public float anchoDelPar = 4f;


    [Header("Muros")]
    public bool generarMuroDelantero = true;
    public bool generarMurosLaterales = true;
    public bool generarMuroSuperior = true;
    public float alturaMuro = 1.0f;
    public float alturaMuroSuperior = 2.0f;
    public bool generarBaranda = true;
    public GameObject PrefabBaranda;
    public Material MaterialMuro;

    [Header("Soportes Estructurales")]
    public bool generarSoportes = false;
    public float separacionSoportes = 5f;
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


    [Header("Nivel del Suelo")]
    public Transform ground0Level;

    [HideInInspector]
    public float metrosLinealesCalculados = 0f;

    private float[] _longAcumInterior;
    private float _longitudTotalInterior;

    
    [ContextMenu("Generar Codo")]

    void Start()
    {
        if (Application.isPlaying)
            GenerarCodo();
    }

    public void GenerarCodo()
    {
        if (BlockPlateaCurva == null) return;

        Transform contenedorViejo = transform.Find("Contenedor_Codo_Superior");
        if (contenedorViejo != null)
            DestroyImmediate(contenedorViejo.gameObject);

        GameObject contenedor = new GameObject("Contenedor_Codo_Superior");
        contenedor.transform.SetParent(this.transform, false);
        contenedor.tag = "SectorEstadio";

        int pasos = 900;

        // Calcular tabla de longitudes de la fila mas larga
        float[] longAcumFilaLarga = new float[pasos + 1];

        // Calcular tabla de longitudes del arco interior (fila 0)

        _longAcumInterior = new float[pasos + 1];
        _longAcumInterior[0] = 0f;
        for (int g = 1; g <= pasos; g++)
        {
            float aA = (g - 1) * (anguloTotal / pasos);
            float aB = g * (anguloTotal / pasos);
            Vector3 pA = transform.TransformPoint(CalcularPunto(aA, radioInferior, 0));
            Vector3 pB = transform.TransformPoint(CalcularPunto(aB, radioInferior, 0));
            _longAcumInterior[g] = _longAcumInterior[g - 1] + Vector3.Distance(pA, pB);
        }
        _longitudTotalInterior = _longAcumInterior[pasos];


        longAcumFilaLarga[0] = 0f;


        int filaLarga = filasMaximas - 1;

        for (int g = 1; g <= pasos; g++)
        {
            float aA = (g - 1) * (anguloTotal / pasos);
            float aB = g * (anguloTotal / pasos);
            Vector3 pA = transform.TransformPoint(CalcularPunto(aA, radioInferior + filaLarga * anchoEscalon, filaLarga));
            Vector3 pB = transform.TransformPoint(CalcularPunto(aB, radioInferior + filaLarga * anchoEscalon, filaLarga));
            longAcumFilaLarga[g] = longAcumFilaLarga[g - 1] + Vector3.Distance(pA, pB);
        }

        // Calcular angulos de vomitos
        List<float> angulosVomitos = new List<float>();
        for (int v = 1; v <= vomitosPorLinea; v++)
        {
            float distObjetivo = longAcumFilaLarga[pasos] * v / (vomitosPorLinea + 1);
            float paso = BuscarAngulo(longAcumFilaLarga, distObjetivo, pasos);
            angulosVomitos.Add(paso * (anguloTotal / pasos));
        }

        // Construir mapa de celdas
        List<Celda> mapa = ConstruirMapa(angulosVomitos);

        // Instanciar hormigon segun mapa
        foreach (Celda celda in mapa)
        {
            float radioInterno = radioInferior + (celda.fila * anchoEscalon);
            float radioExterno = radioInterno + anchoEscalon;

            if (celda.tipo == TipoCelda.Vacio) continue;

            if (celda.tipo == TipoCelda.Escalera)
            {
                Vector3 pA = transform.TransformPoint(CalcularPunto(celda.angStart, radioInterno, celda.fila));
                Vector3 pB = transform.TransformPoint(CalcularPunto(celda.angEnd, radioInterno, celda.fila));
                Vector3 pC = transform.TransformPoint(CalcularPunto(celda.angStart, radioExterno, celda.fila));
                Vector3 pD = transform.TransformPoint(CalcularPunto(celda.angEnd, radioExterno, celda.fila));

                Vector3 posicion = (pA + pB + pC + pD) / 4f;
                Vector3 dirHaciaAfuera = (((pC + pD) / 2f) - ((pA + pB) / 2f)).normalized;

                GameObject escaleraObj = Instantiate(Escalera, contenedor.transform);
                escaleraObj.transform.position = posicion;
                escaleraObj.transform.rotation = Quaternion.LookRotation(dirHaciaAfuera, Vector3.up);
                escaleraObj.transform.localScale = Vector3.one;
                AplicarMaterialATodo(escaleraObj, GrisCemento);
            }
            else
            {
                Vector3 w1 = transform.TransformPoint(CalcularPunto(celda.angStart, radioInterno, celda.fila));
                Vector3 w2 = transform.TransformPoint(CalcularPunto(celda.angEnd, radioInterno, celda.fila));
                Vector3 w3 = transform.TransformPoint(CalcularPunto(celda.angStart, radioExterno, celda.fila));
                Vector3 w4 = transform.TransformPoint(CalcularPunto(celda.angEnd, radioExterno, celda.fila));

                GameObject bloqueObj = Instantiate(BlockPlateaCurva, contenedor.transform);
                ConfigurarEscalon(bloqueObj, w1, w2, w3, w4);
                Material matAUsar = celda.tipo == TipoCelda.BloqueLibre ? GrisCemento : Material;
                AplicarMaterialATodo(bloqueObj, matAUsar);
            }
        }

        // Loop de asientos y paraavalanchas
        for (int f = 0; f < filasMaximas; f++)
        {

            float[] longitudesAcumuladas = new float[pasos + 1];

            longitudesAcumuladas[0] = 0f;
            int pasoMaximoFila = 0;
            int pasoInicioFila = 0;

            for (int g = 1; g <= pasos; g++)
            {
                float anguloActual = g * (anguloTotal / pasos);

                if (FilasEnAngulo(anguloActual) > f)
                {
                    pasoInicioFila = g - 1;
                    break;
                }
            }

            for (int g = pasoInicioFila + 1; g <= pasos; g++)
            {
                float anguloActual = g * (anguloTotal / pasos);
                float anguloAnterior = (g - 1) * (anguloTotal / pasos);

                if (FilasEnAngulo(anguloActual) <= f)
                {
                    pasoMaximoFila = g - 1;
                    break;
                }
                pasoMaximoFila = g;

                Vector3 puntoAnterior = transform.TransformPoint(CalcularPunto(anguloAnterior, radioInferior + f * anchoEscalon, f));
                Vector3 puntoActual = transform.TransformPoint(CalcularPunto(anguloActual, radioInferior + f * anchoEscalon, f));
                longitudesAcumuladas[g] = longitudesAcumuladas[g - 1] + Vector3.Distance(puntoAnterior, puntoActual);
            }



            if (pasoMaximoFila == 0) continue;

            float longitudTotal = longitudesAcumuladas[pasoMaximoFila];

            if (longitudTotal < 0.5f) continue;

            float radioCentro = radioInferior + f * anchoEscalon + anchoEscalon * 0.3f;
            float altoEscalonFila = altoEscalon * ObtenerFactorParaFila(f);

            // Paraavalanchas
            bool correspondeParaEnEstaFila = !esCodoPlatea && generarParaavalanchas && PrefabParaavalanchas != null &&
                                            f >= filaInicioPara &&
                                            (f - filaInicioPara) % frecuenciaFilasPara == 0;

            if (correspondeParaEnEstaFila)
            {
                int nivelZigZag = (f - filaInicioPara) / frecuenciaFilasPara;
                float desplazamientoZigZag = (nivelZigZag % 2 == 0) ? 0 : distanciaEntrePares;

                float distanciaActual = 0f;

                while (distanciaActual < longitudTotal)
                {
                    float restoX = (distanciaActual - desplazamientoZigZag) % (distanciaEntrePares * 2);
                    if (restoX < 0) restoX += distanciaEntrePares * 2;

                    if (restoX < 0.1f || restoX > (distanciaEntrePares * 2) - 0.1f)
                    {
                        float[] offsetsPar = { -anchoDelPar / 2f, anchoDelPar / 2f };
                        foreach (float off in offsetsPar)
                        {
                            float distPara = distanciaActual + off;
                            if (distPara < 0 || distPara > longitudTotal) continue;

                            float pasoPara = BuscarAngulo(longitudesAcumuladas, distPara, pasoMaximoFila);
                            float anguloPara = pasoPara * (anguloTotal / pasos);

                            Vector3 posPara = transform.TransformPoint(CalcularPunto(anguloPara, radioCentro, f));
                            posPara.y += altoEscalonFila;

                            // Orientacion perpendicular al arco (misma que el escalon)
                            Vector3 pInt = transform.TransformPoint(CalcularPunto(anguloPara, radioInferior, f));
                            Vector3 pExt = transform.TransformPoint(CalcularPunto(anguloPara, radioInferior + anchoEscalon * filasMaximas, f));
                            Vector3 dirRadial = (pExt - pInt).normalized;

                            // Buscar celda para el color
                            Celda? celdaPara = null;
                            foreach (Celda c in mapa)
                            {
                                if (c.fila == f && anguloPara >= c.angStart && anguloPara <= c.angEnd)
                                {
                                    celdaPara = c;
                                    break;
                                }
                            }
                            Material matPara = (celdaPara != null && celdaPara.Value.tipo == TipoCelda.BloqueLibre)
                                ? GrisCemento : Material;

                            GameObject para = Instantiate(PrefabParaavalanchas, contenedor.transform);
                            para.transform.position = posPara;
                            para.transform.rotation = Quaternion.LookRotation(dirRadial, Vector3.up);
                            AplicarMaterialATodo(para, matPara);
                        }
                    }
                    distanciaActual += anchoEscalon;
                }
            }

            // Asientos
            if (esCodoPlatea)
            {
                int cuantosAsientos = Mathf.FloorToInt(longitudTotal / 0.5f);

                for (int a = 0; a < cuantosAsientos; a++)
                {
                    float distanciaObjetivo = (a + 0.5f) * 0.5f;
                    float paso = BuscarAngulo(longitudesAcumuladas, distanciaObjetivo, pasoMaximoFila);
                    float angulo = paso * (anguloTotal / pasos);

                    Celda? celdaEncontrada = null;
                    foreach (Celda c in mapa)
                    {
                        if (c.fila == f && angulo >= c.angStart && angulo <= c.angEnd)
                        {
                            celdaEncontrada = c;
                            break;
                        }
                    }

                    if (celdaEncontrada == null || celdaEncontrada.Value.tipo != TipoCelda.Bloque)
                        continue;

                    float deltaA = 0.5f;
                    float anguloA = Mathf.Max(0, angulo - deltaA);
                    float anguloB = Mathf.Min(anguloTotal, angulo + deltaA);

                    Vector3 puntoA = transform.TransformPoint(CalcularPunto(anguloA, radioCentro, f));
                    Vector3 puntoB = transform.TransformPoint(CalcularPunto(anguloB, radioCentro, f));
                    Vector3 posicion = transform.TransformPoint(CalcularPunto(angulo, radioCentro, f));
                    Vector3 tangente = (puntoB - puntoA).normalized;

                    UbicarAsiento(posicion, tangente, contenedor);
                }
            }
        }

        if (generarMuroDelantero) GenerarMuroDelantero(contenedor, pasos);
        if (generarMurosLaterales) GenerarMurosLaterales(contenedor);

        if (generarMuroSuperior) GenerarMuroSuperior(contenedor, mapa);

        if (generarMurosVomitos) GenerarMurosVomitos(contenedor, angulosVomitos);

        if (generarSoportes)
        {
            GenerarSoportesCodo(contenedor);

            GenerarVigasTransversalesCodo(contenedor);
        }

        foreach (Transform hijo in contenedor.GetComponentsInChildren<Transform>())
        {
            if (hijo.gameObject != contenedor && Application.isPlaying)
                hijo.gameObject.isStatic = true;
        }

        if (Application.isPlaying)
            StaticBatchingUtility.Combine(contenedor);


        CalcularMetrosLinealesInternos();
    }

    Vector3 CalcularPunto(float angulo, float radio, int fila)
    {
        float rad = angulo * Mathf.Deg2Rad;
        float x = radio * Mathf.Cos(rad);
        float z = radio * Mathf.Sin(rad);
        float y = CalcularAlturaAcumulada(fila);
        return new Vector3(x, y, z);
    }


    float CalcularAlturaAcumulada(int fila)
    {
        float altura = 0f;
        for (int i = 0; i < fila; i++)
            altura += altoEscalon * ObtenerFactorParaFila(i);
        return altura;
    }

    float ObtenerFactorParaFila(int fila)
    {
        float factor = 1.0f;
        if (rangosDeAlzada == null || rangosDeAlzada.Length == 0) return factor;
        foreach (var rango in rangosDeAlzada)
        {
            if (fila >= rango.filaInicio) factor = rango.multiplicador;
        }
        return factor;
    }

    List<Celda> ConstruirMapa(List<float> angulosVomitos)
    {
        List<Celda> mapa = new List<Celda>();
        float anguloPorPieza = anguloTotal / cantidadPiezas;

        for (int p = 0; p < cantidadPiezas; p++)
        {
            float angStart = p * anguloPorPieza;
            float angEnd = (p + 1) * anguloPorPieza;
            float anguloMedio = (angStart + angEnd) / 2f;
            int filasEnEstaPieza = FilasEnAngulo(anguloMedio);

            for (int f = 0; f < filasEnEstaPieza; f++)
            {
                TipoCelda tipo = DeterminarTipo(anguloMedio, f, angulosVomitos);
                bool esPasillo = (tipo == TipoCelda.Escalera || tipo == TipoCelda.Vacio);

                mapa.Add(new Celda
                {
                    columna = p,
                    fila = f,
                    tipo = tipo,
                    angStart = angStart,
                    angEnd = angEnd,
                    esPasillo = esPasillo
                });
            }
        }

        return mapa;
    }

    TipoCelda DeterminarTipo(float anguloMedio, int fila, List<float> angulosVomitos)
    {
        float radioMedio = radioInferior + (filasMaximas / 2f) * anchoEscalon;
        float circunferenciaMedia = 2f * Mathf.PI * radioMedio;
        float gradosPasillo = (anchoPasilloVomito / circunferenciaMedia) * 360f;
        float gradosLibres = (metrosLibresAlrededor / circunferenciaMedia) * 360f;

        bool esFilaVomito1 = (fila >= filaInicioBocaVomito && fila < filaInicioBocaVomito + altoBocaVomito);
        bool esFilaVomito2 = tieneSegundaLinea && (fila >= filaInicioBoca2 && fila < filaInicioBoca2 + altoBocaVomito);
        bool esFilaVomito = esFilaVomito1 || esFilaVomito2;

        foreach (float anguloVomito in angulosVomitos)
        {
            if (Mathf.Abs(anguloMedio - anguloVomito) <= gradosPasillo)
            {
                if (esFilaVomito) return TipoCelda.Vacio;
                if (!esCodoPlatea) return TipoCelda.Bloque;
                return TipoCelda.Escalera;
            }

            if (esFilaVomito && Mathf.Abs(anguloMedio - anguloVomito) <= gradosPasillo + gradosLibres)
                return esCodoPlatea ? TipoCelda.BloqueLibre : TipoCelda.Bloque;
        }

        return TipoCelda.Bloque;
    }
    
    int FilasEnAngulo(float angulo)
    {
        float t = angulo / anguloTotal;
        if (invertirSentido) t = 1f - t;

        float tCurvado;
        if (usarFormaPersonalizada)
        {
            float tSigmoide = Mathf.Pow(t, exponente) /
                              (Mathf.Pow(t, exponente) + Mathf.Pow(1f - t, exponente));
            tCurvado = Mathf.Lerp(tSigmoide, t, mezclaLineal);
        }
        else
        {
            tCurvado = t; // circular puro
        }

        return Mathf.RoundToInt(Mathf.Lerp(filasMaximas, filasMinimas, tCurvado));
    }


    void UbicarAsiento(Vector3 posicion, Vector3 tangente, GameObject contenedor)
    {
        GameObject asiento = Instantiate(AsientoPlatea);
        asiento.transform.localScale = Vector3.one;
        asiento.transform.position = posicion + (Vector3.up * 0.6f);
        asiento.transform.rotation = Quaternion.LookRotation(tangente, Vector3.up);
        asiento.transform.Rotate(0, 180, 0);
        asiento.transform.SetParent(contenedor.transform, true);
        AplicarMaterialATodo(asiento, Material);
    }

    float BuscarAngulo(float[] longitudesAcumuladas, float distanciaObjetivo, int anguloMaximo)
    {
        for (int g = 1; g <= anguloMaximo; g++)
        {
            if (longitudesAcumuladas[g] >= distanciaObjetivo)
            {
                float t = (distanciaObjetivo - longitudesAcumuladas[g - 1]) /
                          (longitudesAcumuladas[g] - longitudesAcumuladas[g - 1]);
                return Mathf.Lerp(g - 1, g, t);
            }
        }
        return anguloMaximo;
    }

    void ConfigurarEscalon(GameObject pieza, Vector3 w1, Vector3 w2, Vector3 w3, Vector3 w4)
    {
        ProBuilderMesh mesh = pieza.GetComponentInChildren<ProBuilderMesh>();
        if (mesh == null) return;

        pieza.transform.position = Vector3.zero;
        pieza.transform.rotation = Quaternion.identity;

        Vector3[] vertices = mesh.positions.ToArray();
        for (int i = 0; i < vertices.Length; i++)
        {
            float xOrig = vertices[i].x;
            float zOrig = vertices[i].z;
            bool esArriba = vertices[i].y > 0.1f;

            Vector3 destino;
            if (xOrig <= 0.5f && zOrig <= 0.5f) destino = w1;
            else if (xOrig > 0.5f && zOrig <= 0.5f) destino = w2;
            else if (xOrig <= 0.5f && zOrig > 0.5f) destino = w3;
            else destino = w4;

            if (esArriba) destino.y += altoEscalon * ObtenerFactorParaFila(0);

            vertices[i] = pieza.transform.InverseTransformPoint(destino);
        }
        mesh.positions = vertices;
        mesh.ToMesh();
        mesh.Refresh();

        foreach (var face in mesh.faces)
            face.Reverse();

        mesh.ToMesh();
        mesh.Refresh();
    }

    


    void AplicarMaterialATodo(GameObject obj, Material mat)
    {
        foreach (var r in obj.GetComponentsInChildren<MeshRenderer>())
            r.sharedMaterial = mat;
    }


    void GenerarMuroDelantero(GameObject contenedor, int pasos)
    {
        float grosor = 0.2f;
        int segmentos = cantidadPiezas;
        float anguloPorSegmento = anguloTotal / segmentos;

        Vector3[] sInner = new Vector3[segmentos + 1];
        Vector3[] sOuter = new Vector3[segmentos + 1];
        Vector3[] sDirRadial = new Vector3[segmentos + 1];

        for (int s = 0; s <= segmentos; s++)
        {
            float angulo = s * anguloPorSegmento;
            float rad = angulo * Mathf.Deg2Rad;
            Vector3 dirRadial = new Vector3(Mathf.Cos(rad), 0f, Mathf.Sin(rad));

            Vector3 pOuter = new Vector3(radioInferior * Mathf.Cos(rad), 0f, radioInferior * Mathf.Sin(rad));
            Vector3 pInner = new Vector3((radioInferior + grosor) * Mathf.Cos(rad), 0f, (radioInferior + grosor) * Mathf.Sin(rad));

            sOuter[s] = contenedor.transform.InverseTransformPoint(transform.TransformPoint(pOuter));
            sInner[s] = contenedor.transform.InverseTransformPoint(transform.TransformPoint(pInner));
            sDirRadial[s] = dirRadial;
        }

        List<Vector3> vertices = new List<Vector3>();
        List<Vector3> normales = new List<Vector3>();
        List<int> triangulos = new List<int>();

        for (int s = 0; s < segmentos; s++)
        {
            Vector3 i0 = sInner[s], i1 = sInner[s + 1];
            Vector3 o0 = sOuter[s], o1 = sOuter[s + 1];
            Vector3 nLi = sDirRadial[s], nRi = sDirRadial[s + 1];  // interior apunta hacia adentro
            Vector3 nLo = -sDirRadial[s], nRo = -sDirRadial[s + 1]; // exterior apunta hacia afuera
            float h = alturaMuro;
            int base_i;

            // --- Cara interior ---
            base_i = vertices.Count;
            vertices.AddRange(new[] { i0, i0 + Vector3.up * h, i1, i1 + Vector3.up * h });
            normales.AddRange(new[] { nLi, nLi, nRi, nRi });
            triangulos.AddRange(new[] {
            base_i, base_i + 2, base_i + 1,
            base_i + 1, base_i + 2, base_i + 3
        });

            // --- Cara exterior ---
            base_i = vertices.Count;
            vertices.AddRange(new[] { o0, o0 + Vector3.up * h, o1, o1 + Vector3.up * h });
            normales.AddRange(new[] { nLo, nLo, nRo, nRo });
            triangulos.AddRange(new[] {
            base_i, base_i + 1, base_i + 2,
            base_i + 1, base_i + 3, base_i + 2
        });

            // --- Techo ---
            base_i = vertices.Count;
            vertices.AddRange(new[] { i0 + Vector3.up * h, o0 + Vector3.up * h, i1 + Vector3.up * h, o1 + Vector3.up * h });
            normales.AddRange(new[] { Vector3.up, Vector3.up, Vector3.up, Vector3.up });
            triangulos.AddRange(new[] {
            base_i, base_i + 2, base_i + 1,
            base_i + 1, base_i + 2, base_i + 3
        });

            // --- Piso ---
            base_i = vertices.Count;
            vertices.AddRange(new[] { i0, o0, i1, o1 });
            normales.AddRange(new[] { Vector3.down, Vector3.down, Vector3.down, Vector3.down });
            triangulos.AddRange(new[] {
            base_i, base_i + 1, base_i + 2,
            base_i + 1, base_i + 3, base_i + 2
        });
        }

        // --- Tapa lateral inicial (angulo = 0) ---
        {
            float rad = 0f;
            Vector3 normal = new Vector3(Mathf.Sin(rad), 0f, -Mathf.Cos(rad));
            Vector3 i0 = sInner[0], o0 = sOuter[0];
            int base_i = vertices.Count;
            vertices.AddRange(new[] { i0, o0, i0 + Vector3.up * alturaMuro, o0 + Vector3.up * alturaMuro });
            normales.AddRange(new[] { normal, normal, normal, normal });
            triangulos.AddRange(new[] {
            base_i, base_i + 1, base_i + 2,
            base_i + 1, base_i + 3, base_i + 2
        });
        }

        // --- Tapa lateral final (angulo = anguloTotal) ---
        {
            float rad = anguloTotal * Mathf.Deg2Rad;
            Vector3 normal = new Vector3(-Mathf.Sin(rad), 0f, Mathf.Cos(rad));
            Vector3 i1 = sInner[segmentos], o1 = sOuter[segmentos];
            int base_i = vertices.Count;
            vertices.AddRange(new[] { i1, o1, i1 + Vector3.up * alturaMuro, o1 + Vector3.up * alturaMuro });
            normales.AddRange(new[] { normal, normal, normal, normal });
            triangulos.AddRange(new[] {
            base_i, base_i + 2, base_i + 1,
            base_i + 1, base_i + 2, base_i + 3
        });
        }

        Mesh mesh = new Mesh();
        mesh.vertices = vertices.ToArray();
        mesh.normals = normales.ToArray();
        mesh.triangles = triangulos.ToArray();

        GameObject muroGO = new GameObject("Muro_Delantero");
        muroGO.transform.SetParent(contenedor.transform);
        muroGO.transform.localPosition = Vector3.zero;
        muroGO.transform.localRotation = Quaternion.identity;
        muroGO.AddComponent<MeshFilter>().mesh = mesh;
        muroGO.AddComponent<MeshRenderer>().sharedMaterial = MaterialMuro;

        // Baranda (sin cambios)
        if (generarBaranda && PrefabBaranda != null)
        {
            int pasosMesh = 900;
            float[] longAcum = new float[pasosMesh + 1];
            longAcum[0] = 0f;
            for (int g = 1; g <= pasosMesh; g++)
            {
                float aA = (g - 1) * (anguloTotal / pasosMesh);
                float aB = g * (anguloTotal / pasosMesh);
                Vector3 pA = transform.TransformPoint(CalcularPunto(aA, radioInferior, 0));
                Vector3 pB = transform.TransformPoint(CalcularPunto(aB, radioInferior, 0));
                longAcum[g] = longAcum[g - 1] + Vector3.Distance(pA, pB);
            }
            float longitudTotal = longAcum[pasosMesh];
            float espaciadoBaranda = 1.0f;
            float distancia = 0f;
            while (distancia < longitudTotal)
            {
                float paso = BuscarAngulo(longAcum, distancia, pasosMesh);
                float angulo = paso * (anguloTotal / pasosMesh);
                Vector3 pos = transform.TransformPoint(CalcularPunto(angulo, radioInferior, 0));
                pos += Vector3.up * alturaMuro;
                float delta = 0.5f;
                float anguloA = Mathf.Max(0, angulo - delta);
                float anguloB = Mathf.Min(anguloTotal, angulo + delta);
                Vector3 pA = transform.TransformPoint(CalcularPunto(anguloA, radioInferior, 0));
                Vector3 pB = transform.TransformPoint(CalcularPunto(anguloB, radioInferior, 0));
                Vector3 tangente = (pB - pA).normalized;
                GameObject baranda = Instantiate(PrefabBaranda, contenedor.transform);
                baranda.transform.position = pos;
                baranda.transform.rotation = Quaternion.LookRotation(tangente, Vector3.up);
                distancia += espaciadoBaranda;
            }
        }
    }

    void GenerarMurosLaterales(GameObject contenedor)
    {
        float grosor = 0.2f;
        float[] angulos = { 0f, anguloTotal };
        string[] nombres = { "Muro_Lateral_Inicio", "Muro_Lateral_Fin" };

        for (int lado = 0; lado < 2; lado++)
        {
            float angulo = angulos[lado];
            float rad = angulo * Mathf.Deg2Rad;
            int filasEnEsteAngulo = FilasEnAngulo(angulo);

            // Normal: tangente al arco en este angulo
            // Inicio apunta "hacia afuera" del arco, Fin en sentido contrario
            Vector3 normal = lado == 0
                ? new Vector3(Mathf.Sin(rad), 0f, -Mathf.Cos(rad))
                : new Vector3(-Mathf.Sin(rad), 0f, Mathf.Cos(rad));

            // Direccion de grosor: hacia afuera del estadio (tangente)
            Vector3 desplazamiento = normal * grosor;

            List<Vector3> vertices = new List<Vector3>();
            List<Vector3> normales = new List<Vector3>();
            List<int> triangulos = new List<int>();

            // Precalcular puntos del perfil (cara frontal e interior)
            Vector3[] sFront = new Vector3[filasEnEsteAngulo + 1];
            Vector3[] sBack = new Vector3[filasEnEsteAngulo + 1];

            for (int f = 0; f <= filasEnEsteAngulo; f++)
            {
                float radio = radioInferior + f * anchoEscalon;
                Vector3 pBase = transform.TransformPoint(CalcularPunto(angulo, radio, f));
                Vector3 local = contenedor.transform.InverseTransformPoint(pBase);
                sFront[f] = local;
                sBack[f] = contenedor.transform.InverseTransformPoint(pBase + desplazamiento);
            }

            for (int f = 0; f < filasEnEsteAngulo; f++)
            {
                Vector3 f0 = sFront[f], f1 = sFront[f + 1];
                Vector3 b0 = sBack[f], b1 = sBack[f + 1];
                float h = alturaMuro;
                int base_i;

                // --- Cara frontal (normal hacia afuera) ---
                base_i = vertices.Count;
                vertices.AddRange(new[] { f0, f0 + Vector3.up * h, f1, f1 + Vector3.up * h });
                normales.AddRange(new[] { normal, normal, normal, normal });
                triangulos.AddRange(new[] {
                base_i, base_i + 1, base_i + 2,
                base_i + 1, base_i + 3, base_i + 2
            });

                // --- Cara trasera (normal invertida) ---
                Vector3 normalInv = -normal;
                base_i = vertices.Count;
                vertices.AddRange(new[] { b0, b0 + Vector3.up * h, b1, b1 + Vector3.up * h });
                normales.AddRange(new[] { normalInv, normalInv, normalInv, normalInv });
                triangulos.AddRange(new[] {
                base_i, base_i + 2, base_i + 1,
                base_i + 1, base_i + 2, base_i + 3
            });

                // --- Techo ---
                base_i = vertices.Count;
                vertices.AddRange(new[] { f0 + Vector3.up * h, b0 + Vector3.up * h, f1 + Vector3.up * h, b1 + Vector3.up * h });
                normales.AddRange(new[] { Vector3.up, Vector3.up, Vector3.up, Vector3.up });
                triangulos.AddRange(new[] {
                base_i, base_i + 1, base_i + 2,
                base_i + 1, base_i + 3, base_i + 2
            });

                // --- Piso ---
                base_i = vertices.Count;
                vertices.AddRange(new[] { f0, b0, f1, b1 });
                normales.AddRange(new[] { Vector3.down, Vector3.down, Vector3.down, Vector3.down });
                triangulos.AddRange(new[] {
                base_i, base_i + 2, base_i + 1,
                base_i + 1, base_i + 2, base_i + 3
            });
            }

            // --- Tapa inferior (radio interior, borde del arco) ---
            {
                Vector3 dirRadial = new Vector3(Mathf.Cos(rad), 0f, Mathf.Sin(rad));
                Vector3 f0 = sFront[0], b0 = sBack[0];
                int base_i = vertices.Count;
                vertices.AddRange(new[] { f0, b0, f0 + Vector3.up * alturaMuro, b0 + Vector3.up * alturaMuro });
                normales.AddRange(new[] { -dirRadial, -dirRadial, -dirRadial, -dirRadial });
                triangulos.AddRange(new[] {
                base_i, base_i + 2, base_i + 1,
                base_i + 1, base_i + 2, base_i + 3
            });
            }

            // --- Tapa superior (radio exterior, borde exterior del arco) ---
            {
                Vector3 dirRadial = new Vector3(Mathf.Cos(rad), 0f, Mathf.Sin(rad));
                Vector3 fN = sFront[filasEnEsteAngulo], bN = sBack[filasEnEsteAngulo];
                int base_i = vertices.Count;
                vertices.AddRange(new[] { fN, bN, fN + Vector3.up * alturaMuro, bN + Vector3.up * alturaMuro });
                normales.AddRange(new[] { dirRadial, dirRadial, dirRadial, dirRadial });
                triangulos.AddRange(new[] {
                base_i, base_i + 1, base_i + 2,
                base_i + 1, base_i + 3, base_i + 2
            });
            }

            Mesh mesh = new Mesh();
            mesh.vertices = vertices.ToArray();
            mesh.normals = normales.ToArray();
            mesh.triangles = triangulos.ToArray();

            GameObject muroGO = new GameObject(nombres[lado]);
            muroGO.transform.SetParent(contenedor.transform);
            muroGO.transform.localPosition = Vector3.zero;
            muroGO.transform.localRotation = Quaternion.identity;
            muroGO.AddComponent<MeshFilter>().mesh = mesh;
            muroGO.AddComponent<MeshRenderer>().sharedMaterial = MaterialMuro;
        }
    }

    void GenerarMuroSuperior(GameObject contenedor, List<Celda> mapa)
    {
        float grosor = 0.2f;
        float anguloPorPieza = anguloTotal / cantidadPiezas;

        List<Vector3> vertices = new List<Vector3>();
        List<Vector3> normales = new List<Vector3>();
        List<int> triangulos = new List<int>();

        // Precalcular puntos por seccion
        // pInner/pOuter en local del contenedor, dirRadial en world


        // Unity no permite structs locales en C# < 7.3, asi que usamos arrays paralelos
        Vector3[] sInner = new Vector3[cantidadPiezas + 1];
        Vector3[] sOuter = new Vector3[cantidadPiezas + 1];
        Vector3[] sDirRadial = new Vector3[cantidadPiezas + 1];

        for (int p = 0; p <= cantidadPiezas; p++)
        {
            float angulo = p * anguloPorPieza;
            float filasFloat = FilasEnAnguloFloat(angulo);
            int filaBase = (int)filasFloat;
            float fraccion = filasFloat % 1f;

            float yBase = CalcularAlturaAcumulada(filaBase)
                        + fraccion * altoEscalon * ObtenerFactorParaFila(filaBase);

            float radio = radioInferior + filasFloat * anchoEscalon + anchoEscalon;

            float rad = angulo * Mathf.Deg2Rad;
            Vector3 dirRadial = new Vector3(Mathf.Cos(rad), 0f, Mathf.Sin(rad));

            Vector3 pInner = new Vector3(radio * Mathf.Cos(rad), yBase, radio * Mathf.Sin(rad));
            Vector3 pOuter = new Vector3((radio + grosor) * Mathf.Cos(rad), yBase, (radio + grosor) * Mathf.Sin(rad));

            // Pasar a local del contenedor
            sInner[p] = contenedor.transform.InverseTransformPoint(transform.TransformPoint(pInner));
            sOuter[p] = contenedor.transform.InverseTransformPoint(transform.TransformPoint(pOuter));
            sDirRadial[p] = dirRadial;
        }

        // Construir caras entre secciones contiguas
        for (int p = 0; p < cantidadPiezas; p++)
        {
            Vector3 i0 = sInner[p], i1 = sInner[p + 1];
            Vector3 o0 = sOuter[p], o1 = sOuter[p + 1];
            Vector3 nL = -sDirRadial[p], nR = -sDirRadial[p + 1]; // normales interior
            Vector3 nLo = sDirRadial[p], nRo = sDirRadial[p + 1]; // normales exterior
            float h = alturaMuroSuperior;

            int base_i = vertices.Count;

            // --- Cara interior (normal hacia adentro del arco) ---
            vertices.AddRange(new[] {
            i0,              i0 + Vector3.up * h,
            i1,              i1 + Vector3.up * h
        });
            normales.AddRange(new[] { nL, nL, nR, nR });
            triangulos.AddRange(new[] {
            base_i,     base_i + 1, base_i + 2,
            base_i + 1, base_i + 3, base_i + 2
        });

            base_i = vertices.Count;

            // --- Cara exterior (normal hacia afuera) ---
            vertices.AddRange(new[] {
            o0,              o0 + Vector3.up * h,
            o1,              o1 + Vector3.up * h
        });
            normales.AddRange(new[] { nLo, nLo, nRo, nRo });
            triangulos.AddRange(new[] {
            base_i,     base_i + 2, base_i + 1,
            base_i + 1, base_i + 2, base_i + 3
        });

            base_i = vertices.Count;

            // --- Techo (normal hacia arriba) ---
            vertices.AddRange(new[] {
            i0 + Vector3.up * h, o0 + Vector3.up * h,
            i1 + Vector3.up * h, o1 + Vector3.up * h
        });
            normales.AddRange(new[] { Vector3.up, Vector3.up, Vector3.up, Vector3.up });
            triangulos.AddRange(new[] {
            base_i,     base_i + 1, base_i + 2,
            base_i + 1, base_i + 3, base_i + 2
        });

            base_i = vertices.Count;

            // --- Piso (normal hacia abajo) ---
            vertices.AddRange(new[] { i0, o0, i1, o1 });
            normales.AddRange(new[] { Vector3.down, Vector3.down, Vector3.down, Vector3.down });
            triangulos.AddRange(new[] {
            base_i,     base_i + 2, base_i + 1,
            base_i + 1, base_i + 2, base_i + 3
        });
        }

        // --- Tapa lateral inicial (angulo = 0) ---
        {
            float rad = 0f;
            Vector3 normal = new Vector3(Mathf.Sin(rad), 0f, -Mathf.Cos(rad)); // tangente invertida
            Vector3 i0 = sInner[0], o0 = sOuter[0];
            int base_i = vertices.Count;
            vertices.AddRange(new[] {
            i0,                    o0,
            i0 + Vector3.up * alturaMuro, o0 + Vector3.up * alturaMuroSuperior
        });
            normales.AddRange(new[] { normal, normal, normal, normal });
            triangulos.AddRange(new[] {
            base_i,     base_i + 2, base_i + 1,
            base_i + 1, base_i + 2, base_i + 3
        });
        }

        // --- Tapa lateral final (angulo = anguloTotal) ---
        {
            float rad = anguloTotal * Mathf.Deg2Rad;
            Vector3 normal = new Vector3(-Mathf.Sin(rad), 0f, Mathf.Cos(rad)); // tangente
            Vector3 i1 = sInner[cantidadPiezas], o1 = sOuter[cantidadPiezas];
            int base_i = vertices.Count;
            vertices.AddRange(new[] {
            i1,                    o1,
            i1 + Vector3.up * alturaMuroSuperior, o1 + Vector3.up * alturaMuroSuperior
        });
            normales.AddRange(new[] { normal, normal, normal, normal });
            triangulos.AddRange(new[] {
            base_i,     base_i + 1, base_i + 2,
            base_i + 1, base_i + 3, base_i + 2
        });
        }

        Mesh mesh = new Mesh();
        mesh.vertices = vertices.ToArray();
        mesh.normals = normales.ToArray();
        mesh.triangles = triangulos.ToArray();

        GameObject muroGO = new GameObject("Muro_Superior");
        muroGO.transform.SetParent(contenedor.transform);
        muroGO.transform.localPosition = Vector3.zero;
        muroGO.transform.localRotation = Quaternion.identity;
        muroGO.AddComponent<MeshFilter>().mesh = mesh;
        muroGO.AddComponent<MeshRenderer>().sharedMaterial = MaterialMuro;
    }


    void GenerarMurosVomitos(GameObject contenedor, List<float> angulosVomitos)
    {
        if (!generarMurosVomitos) return;

        foreach (float anguloVomito in angulosVomitos)
        {
            float radioMedio = radioInferior + (filasMaximas / 2f) * anchoEscalon;
            float circunferenciaMedia = 2f * Mathf.PI * radioMedio;
            float gradosPasillo = (anchoPasilloVomito / circunferenciaMedia) * 360f;

            float anguloIzq = anguloVomito - gradosPasillo;
            float anguloDer = anguloVomito + gradosPasillo;

            // Para cada linea de vomitos
            GenerarMurosVomitoEnAngulos(contenedor, anguloIzq, anguloDer,
                filaInicioBocaVomito, altoBocaVomito);

            if (tieneSegundaLinea)
            {
                // Verificar que la fila existe en ese angulo
                if (FilasEnAngulo(anguloVomito) > filaInicioBoca2)
                    GenerarMurosVomitoEnAngulos(contenedor, anguloIzq, anguloDer,
                        filaInicioBoca2, altoBocaVomito);
            }
        }
    }

    void GenerarMurosVomitoEnAngulos(GameObject contenedor, float anguloIzq, float anguloDer,
        int filaInicio, int altoVomito)
    {
        // Muros laterales izquierdo y derecho
        float[] angulos = { anguloIzq, anguloDer };
        string[] nombres = { "Muro_Vomito_Izq", "Muro_Vomito_Der" };

        for (int lado = 0; lado < 2; lado++)
        {
            float angulo = angulos[lado];
            List<Vector3> vertices = new List<Vector3>();
            List<int> triangulos = new List<int>();

            for (int f = filaInicio; f <= filaInicio + altoVomito; f++)
            {
                float radio = radioInferior + f * anchoEscalon;
                Vector3 pBase = transform.TransformPoint(CalcularPunto(angulo, radio, f));
                float altoEscalonEnEstaFila = altoEscalon * ObtenerFactorParaFila(f);
                Vector3 pBaseCorregida = pBase + Vector3.up * altoEscalonEnEstaFila;
                Vector3 pTop = pBase + Vector3.up * alturaMuroVomito;
                vertices.Add(pBase);
                vertices.Add(pTop);
            }

            for (int f = 0; f < altoVomito; f++)
            {
                int i = f * 2;
                if (lado == 0) // izquierdo
                {
                    triangulos.Add(i); triangulos.Add(i + 1); triangulos.Add(i + 2);
                    triangulos.Add(i + 1); triangulos.Add(i + 3); triangulos.Add(i + 2);
                }
                else // derecho, cara invertida
                {
                    triangulos.Add(i + 2); triangulos.Add(i + 1); triangulos.Add(i);
                    triangulos.Add(i + 2); triangulos.Add(i + 3); triangulos.Add(i + 1);
                }
            }

            Mesh mesh = new Mesh();
            mesh.vertices = vertices.ToArray();
            mesh.triangles = triangulos.ToArray();
            mesh.RecalculateNormals();

            GameObject muroGO = new GameObject(nombres[lado]);
            muroGO.transform.SetParent(contenedor.transform);
            muroGO.AddComponent<MeshFilter>().mesh = mesh;
            muroGO.AddComponent<MeshRenderer>().sharedMaterial =
                MaterialMuroVomito != null ? MaterialMuroVomito : MaterialMuro;
        }

        // Muro superior (curvo, sobre la boca del vomito)
        int segmentos = 10; // suficiente para el ancho del vomito

        float anguloSuperiorFila = filaInicio + altoVomito;

        float radioSuperior = radioInferior + (filaInicio + altoVomito) * anchoEscalon;

        List<Vector3> verticesSup = new List<Vector3>();
        List<int> triangulosSup = new List<int>();

        for (int s = 0; s <= segmentos; s++)
        {
            float t = (float)s / segmentos;
            float angulo = Mathf.Lerp(anguloIzq, anguloDer, t);
            int filaTop = filaInicio + altoVomito;
            Vector3 pBase = transform.TransformPoint(CalcularPunto(angulo, radioSuperior, filaTop));
            float altoEscalonTop = altoEscalon * ObtenerFactorParaFila(filaTop);
            Vector3 pBaseCorregida = pBase + Vector3.up * altoEscalonTop;
            Vector3 pTop = pBaseCorregida + Vector3.up * alturaMuroVomito;
            verticesSup.Add(pBaseCorregida);
            verticesSup.Add(pTop);
        }


        for (int s = 0; s < segmentos; s++)
        {
            int i = s * 2;
            triangulosSup.Add(i); triangulosSup.Add(i + 1); triangulosSup.Add(i + 2);
            triangulosSup.Add(i + 1); triangulosSup.Add(i + 3); triangulosSup.Add(i + 2);
        }

        Mesh meshSup = new Mesh();
        meshSup.vertices = verticesSup.ToArray();
        meshSup.triangles = triangulosSup.ToArray();
        meshSup.RecalculateNormals();

        GameObject muroSupGO = new GameObject("Muro_Vomito_Superior");
        muroSupGO.transform.SetParent(contenedor.transform);
        muroSupGO.AddComponent<MeshFilter>().mesh = meshSup;
        muroSupGO.AddComponent<MeshRenderer>().sharedMaterial =
            MaterialMuroVomito != null ? MaterialMuroVomito : MaterialMuro;

        if (generarEscaleraVomito)
            GenerarEscaleraDescendenteVomitoCodo(anguloIzq, anguloDer, filaInicio, contenedor);

    }

    public void CalcularMetrosLinealesInternos()
    {
        float metros = 0f;
        int pasos = 900;

        for (int f = 0; f < filasMaximas; f++)
        {
            int pasoInicioFila = 0;
            for (int g = 1; g <= pasos; g++)
            {
                float anguloActual = g * (anguloTotal / pasos);
                if (FilasEnAngulo(anguloActual) > f)
                {
                    pasoInicioFila = g - 1;
                    break;
                }
            }

            float[] longitudesAcumuladas = new float[pasos + 1];
            longitudesAcumuladas[0] = 0f;
            int pasoMaximoFila = pasoInicioFila;

            for (int g = pasoInicioFila + 1; g <= pasos; g++)
            {
                float anguloActual = g * (anguloTotal / pasos);
                float anguloAnterior = (g - 1) * (anguloTotal / pasos);

                if (FilasEnAngulo(anguloActual) <= f)
                {
                    pasoMaximoFila = g - 1;
                    break;
                }
                pasoMaximoFila = g;

                Vector3 puntoAnterior = transform.TransformPoint(CalcularPunto(anguloAnterior, radioInferior + f * anchoEscalon, f));
                Vector3 puntoActual = transform.TransformPoint(CalcularPunto(anguloActual, radioInferior + f * anchoEscalon, f));
                longitudesAcumuladas[g] = longitudesAcumuladas[g - 1] + Vector3.Distance(puntoAnterior, puntoActual);
            }

            if (pasoMaximoFila <= pasoInicioFila) continue;
            float longitudTotal = longitudesAcumuladas[pasoMaximoFila];
            if (longitudTotal < 0.5f) continue;

            metros += longitudTotal;
        }

        metrosLinealesCalculados = metros;
    }

    float FilasEnAnguloFloat(float angulo)
    {
        float t = angulo / anguloTotal;
        if (invertirSentido) t = 1f - t;

        float tCurvado;
        if (usarFormaPersonalizada)
        {
            float tSigmoide = Mathf.Pow(t, exponente) /
                              (Mathf.Pow(t, exponente) + Mathf.Pow(1f - t, exponente));
            tCurvado = Mathf.Lerp(tSigmoide, t, mezclaLineal);
        }
        else
        {
            tCurvado = t;
        }

        return Mathf.Lerp(filasMaximas, filasMinimas, tCurvado);
    }

    void GenerarSoportesCodo(GameObject contenedor)
    {
        if (!generarSoportes || MaterialVigas == null) return;

        int pasos = 900;

        // Calcular tabla de longitudes del arco interior (fila 0)
        float[] longAcum = new float[pasos + 1];
        longAcum[0] = 0f;
        for (int g = 1; g <= pasos; g++)
        {
            float aA = (g - 1) * (anguloTotal / pasos);
            float aB = g * (anguloTotal / pasos);
            Vector3 pA = transform.TransformPoint(CalcularPunto(aA, radioInferior, 0));
            Vector3 pB = transform.TransformPoint(CalcularPunto(aB, radioInferior, 0));
            longAcum[g] = longAcum[g - 1] + Vector3.Distance(pA, pB);
        }

        float longitudTotal = longAcum[pasos];

        // Distribuir soportes a lo largo del arco interior
        float distanciaActual = separacionSoportes / 2f;

        while (distanciaActual < longitudTotal)
        {
            float paso = BuscarAngulo(longAcum, distanciaActual, pasos);
            float angulo = paso * (anguloTotal / pasos);

            GenerarSoporteCodo(angulo, contenedor);

            distanciaActual += separacionSoportes;
        }
    }

    void GenerarSoporteCodo(float angulo, GameObject contenedor)
    {
        int filasEnEsteAngulo = FilasEnAngulo(angulo);

        int filaExtUsada = Mathf.Min(filaVigaVerticalExterior, filasEnEsteAngulo - 1);

        int filaIntUsada = Mathf.Min(filaVigaVerticalInterior, filasEnEsteAngulo - 1);

        int filaArranqueUsada = Mathf.Min(filaArranqueDiagonal, filasEnEsteAngulo - 1);

        float radioInterior = radioInferior + filaIntUsada * anchoEscalon;
        float radioExterior = radioInferior + filaExtUsada * anchoEscalon;
        float radioArranque = radioInferior + filaArranqueUsada * anchoEscalon;
        float radioFinal = radioInferior + (filasEnEsteAngulo - 1) * anchoEscalon;

        Vector3 pInterior = transform.TransformPoint(CalcularPunto(angulo, radioInterior, filaIntUsada));
        Vector3 pExterior = transform.TransformPoint(CalcularPunto(angulo, radioExterior, filaExtUsada));
        Vector3 pArranque = transform.TransformPoint(CalcularPunto(angulo, radioArranque, filaArranqueUsada));
        Vector3 pFinal = transform.TransformPoint(CalcularPunto(angulo, radioFinal, filasEnEsteAngulo - 1));


        // Direccion radial (perpendicular al arco, en el plano XZ)
        float rad = angulo * Mathf.Deg2Rad;
        Vector3 dirRadial = new Vector3(Mathf.Cos(rad), 0, Mathf.Sin(rad)).normalized;
        Vector3 dirTangente = new Vector3(-Mathf.Sin(rad), 0, Mathf.Cos(rad)).normalized;


        // Viga vertical interior
        CrearVigaVerticalCodo(contenedor, pInterior, dirRadial, dirTangente, anchoViga, altoViga);

        // Viga vertical exterior
        CrearVigaVerticalCodo(contenedor, pExterior, dirRadial, dirTangente, anchoViga, altoViga);

        // Viga horizontal opcional
        if (tieneVigaHorizontal)
        {
            float yGround = contenedor.transform.InverseTransformPoint(ground0Level.position).y;

            Vector3 pInteriorViga = pInterior;

            pInteriorViga.y = pInterior.y;

            Vector3 pExteriorViga = pExterior;

            pExteriorViga.y = pInterior.y; ;

            CrearVigaHorizontalCodo(contenedor, pInteriorViga, pExteriorViga, dirRadial, dirTangente, anchoViga, altoViga);
        }

        // Viga diagonal

        Vector3 pArranqueSuelo = new Vector3(pArranque.x, CalcularAlturaAcumulada(filaArranqueDiagonal), pArranque.z);

        Vector3 pFinalSuelo = new Vector3(pFinal.x, CalcularAlturaAcumulada(filasMaximas - 1), pFinal.z);

        CrearVigaDiagonalCodo(contenedor, pArranque, pFinal, dirRadial, dirTangente);
    }

    void CrearVigaVerticalCodo(GameObject contenedor, Vector3 posBase, Vector3 dirRadial, Vector3 dirTangente, float ancho, float alto)
    {
        GameObject viga = new GameObject("Viga_Vertical_Codo");
        viga.transform.SetParent(contenedor.transform);
        viga.transform.localPosition = Vector3.zero;
        viga.transform.localRotation = Quaternion.identity;

        float g = ancho / 2f;
        float h = alto / 2f;

        // Altura desde el suelo hasta la base de la viga diagonal
        float yGround = contenedor.transform.InverseTransformPoint(ground0Level.position).y;
        float yTop = contenedor.transform.InverseTransformPoint(posBase).y;

        // Posicion en coordenadas locales del contenedor
        Vector3 posBaseLocal = contenedor.transform.InverseTransformPoint(posBase);
        posBaseLocal.y = yGround; // la base arranca en el suelo

        Vector3 dirRadialLocal = contenedor.transform.InverseTransformDirection(dirRadial);
        Vector3 dirTangenteLocal = contenedor.transform.InverseTransformDirection(dirTangente);

        Vector3 base1 = posBaseLocal + dirTangenteLocal * g - dirRadialLocal * h;
        Vector3 base2 = posBaseLocal - dirTangenteLocal * g - dirRadialLocal * h;
        Vector3 base3 = posBaseLocal + dirTangenteLocal * g + dirRadialLocal * h;
        Vector3 base4 = posBaseLocal - dirTangenteLocal * g + dirRadialLocal * h;

        Vector3 top1 = base1 + Vector3.up * (yTop - yGround);
        Vector3 top2 = base2 + Vector3.up * (yTop - yGround);
        Vector3 top3 = base3 + Vector3.up * (yTop - yGround);
        Vector3 top4 = base4 + Vector3.up * (yTop - yGround);

        Vector3[] v = new Vector3[8];
        v[0] = base1; v[1] = top1;
        v[2] = base2; v[3] = top2;
        v[4] = base3; v[5] = top3;
        v[6] = base4; v[7] = top4;

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

    void CrearVigaHorizontalCodo(GameObject contenedor, Vector3 pInterior, Vector3 pExterior, Vector3 dirRadial, Vector3 dirTangente, float ancho, float alto)
    {
        GameObject viga = new GameObject("Viga_Horizontal_Codo");
        viga.transform.SetParent(contenedor.transform);
        viga.transform.localPosition = Vector3.zero;
        viga.transform.localRotation = Quaternion.identity;

        float g = ancho / 2f;
        float h = alto / 2f;

        Vector3[] v = new Vector3[8];
        v[0] = contenedor.transform.InverseTransformPoint(pInterior + dirTangente * g - Vector3.up * h);
        v[1] = contenedor.transform.InverseTransformPoint(pInterior + dirTangente * g + Vector3.up * h);
        v[2] = contenedor.transform.InverseTransformPoint(pExterior + dirTangente * g - Vector3.up * h);
        v[3] = contenedor.transform.InverseTransformPoint(pExterior + dirTangente * g + Vector3.up * h);
        v[4] = contenedor.transform.InverseTransformPoint(pInterior - dirTangente * g - Vector3.up * h);
        v[5] = contenedor.transform.InverseTransformPoint(pInterior - dirTangente * g + Vector3.up * h);
        v[6] = contenedor.transform.InverseTransformPoint(pExterior - dirTangente * g - Vector3.up * h);
        v[7] = contenedor.transform.InverseTransformPoint(pExterior - dirTangente * g + Vector3.up * h);

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

    void CrearVigaDiagonalCodo(GameObject contenedor, Vector3 pInicio, Vector3 pFin, Vector3 dirRadial, Vector3 dirTangente)
    {
        GameObject viga = new GameObject("Viga_Diagonal_Codo");
        viga.transform.SetParent(contenedor.transform);
        viga.transform.localPosition = Vector3.zero;
        viga.transform.localRotation = Quaternion.identity;

        float g = anchoVigaDiagonal / 2f;
        float h = altoVigaDiagonal / 2f;

        Vector3 dir = (pFin - pInicio).normalized;
        Vector3 perp = Vector3.Cross(dir, dirTangente).normalized * h;

        float desplazamientoY = -(altoVigaDiagonal - mordidaViga);
        Vector3 pInicioAdj = pInicio + Vector3.up * desplazamientoY;
        Vector3 pFinAdj = pFin + Vector3.up * desplazamientoY;

        // Extremo inicio diagonal
        Vector3 i0 = pInicioAdj - dirTangente * g - perp;
        Vector3 i1 = pInicioAdj - dirTangente * g + perp;
        Vector3 i4 = pInicioAdj + dirTangente * g - perp;
        Vector3 i5 = pInicioAdj + dirTangente * g + perp;

        // Extremo final con corte vertical
        Vector3 f2 = new Vector3(pFinAdj.x, pFinAdj.y - h, pFinAdj.z) - dirTangente * g;
        Vector3 f3 = new Vector3(pFinAdj.x, pFinAdj.y + h, pFinAdj.z) - dirTangente * g;
        Vector3 f6 = new Vector3(pFinAdj.x, pFinAdj.y - h, pFinAdj.z) + dirTangente * g;
        Vector3 f7 = new Vector3(pFinAdj.x, pFinAdj.y + h, pFinAdj.z) + dirTangente * g;

        Vector3[] v = new Vector3[] {
        contenedor.transform.InverseTransformPoint(i0),
        contenedor.transform.InverseTransformPoint(i1),
        contenedor.transform.InverseTransformPoint(f2),
        contenedor.transform.InverseTransformPoint(f3),
        contenedor.transform.InverseTransformPoint(i4),
        contenedor.transform.InverseTransformPoint(i5),
        contenedor.transform.InverseTransformPoint(f6),
        contenedor.transform.InverseTransformPoint(f7)
    };

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

    void GenerarVigasTransversalesCodo(GameObject contenedor)
    {
        if (!tieneVigasTransversales) return;

        int pasos = 900;

        // Calcular tabla de longitudes del arco interior
        float[] longAcum = new float[pasos + 1];
        longAcum[0] = 0f;
        for (int g = 1; g <= pasos; g++)
        {
            float aA = (g - 1) * (anguloTotal / pasos);
            float aB = g * (anguloTotal / pasos);
            Vector3 pA = transform.TransformPoint(CalcularPunto(aA, radioInferior, 0));
            Vector3 pB = transform.TransformPoint(CalcularPunto(aB, radioInferior, 0));
            longAcum[g] = longAcum[g - 1] + Vector3.Distance(pA, pB);
        }

        float longitudTotal = longAcum[pasos];

        // Lista de angulos de soportes
        List<float> angulosSoportes = new List<float>();
        float distanciaActual = separacionSoportes / 2f;
        while (distanciaActual < longitudTotal)
        {
            float paso = BuscarAngulo(longAcum, distanciaActual, pasos);
            angulosSoportes.Add(paso * (anguloTotal / pasos));
            distanciaActual += separacionSoportes;
        }

        // Generar vigas transversales entre soportes consecutivos
        for (int i = 0; i < angulosSoportes.Count - 1; i++)
        {
            float anguloA = angulosSoportes[i];
            float anguloB = angulosSoportes[i + 1];

            int filasA = FilasEnAngulo(anguloA);
            int filasB = FilasEnAngulo(anguloB);

            int filaIntA = Mathf.Min(filaVigaVerticalInterior, filasA - 1);
            int filaIntB = Mathf.Min(filaVigaVerticalInterior, filasB - 1);
            int filaExtA = Mathf.Min(filaVigaVerticalExterior, filasA - 1);
            int filaExtB = Mathf.Min(filaVigaVerticalExterior, filasB - 1);

            float radioIntA = radioInferior + filaIntA * anchoEscalon;
            float radioIntB = radioInferior + filaIntB * anchoEscalon;
            float radioExtA = radioInferior + filaExtA * anchoEscalon;
            float radioExtB = radioInferior + filaExtB * anchoEscalon;

            Vector3 pIntA = transform.TransformPoint(CalcularPunto(anguloA, radioIntA, filaIntA));
            Vector3 pIntB = transform.TransformPoint(CalcularPunto(anguloB, radioIntB, filaIntB));
            Vector3 pExtA = transform.TransformPoint(CalcularPunto(anguloA, radioExtA, filaExtA));
            Vector3 pExtB = transform.TransformPoint(CalcularPunto(anguloB, radioExtB, filaExtB));

            // Ajustar Y al nivel de alturaUnionVigasInteriores desde ground0Level
            float yGround = ground0Level.position.y;
            pIntA.y = yGround + alturaUnionVigasInteriores;
            pIntB.y = yGround + alturaUnionVigasInteriores;
            pExtA.y = yGround + alturaUnionVigasExteriores;
            pExtB.y = yGround + alturaUnionVigasExteriores;

            // Viga transversal interior
            CrearSegmentoTransversalCodo(contenedor, pIntA, pIntB, anchoVigaTransversal, altoVigaTransversal, "Viga_Transversal_Int_Codo");
            // Viga transversal exterior
            CrearSegmentoTransversalCodo(contenedor, pExtA, pExtB, anchoVigaTransversal, altoVigaTransversal, "Viga_Transversal_Ext_Codo");
        }
    }

    void CrearSegmentoTransversalCodo(GameObject contenedor, Vector3 pA, Vector3 pB, float ancho, float alto, string nombre)
    {
        GameObject viga = new GameObject(nombre);
        viga.transform.SetParent(contenedor.transform);
        viga.transform.localPosition = Vector3.zero;
        viga.transform.localRotation = Quaternion.identity;

        // Direccion del segmento
        Vector3 dir = (pB - pA).normalized;
        Vector3 perp = Vector3.Cross(dir, Vector3.up).normalized * ancho / 2f;

        float h = alto / 2f;

        Vector3[] v = new Vector3[8];
        v[0] = contenedor.transform.InverseTransformPoint(pA + perp - Vector3.up * h);
        v[1] = contenedor.transform.InverseTransformPoint(pA + perp + Vector3.up * h);
        v[2] = contenedor.transform.InverseTransformPoint(pB + perp - Vector3.up * h);
        v[3] = contenedor.transform.InverseTransformPoint(pB + perp + Vector3.up * h);
        v[4] = contenedor.transform.InverseTransformPoint(pA - perp - Vector3.up * h);
        v[5] = contenedor.transform.InverseTransformPoint(pA - perp + Vector3.up * h);
        v[6] = contenedor.transform.InverseTransformPoint(pB - perp - Vector3.up * h);
        v[7] = contenedor.transform.InverseTransformPoint(pB - perp + Vector3.up * h);

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

    void GenerarEscaleraDescendenteVomitoCodo(float anguloIzq, float anguloDer, int filaInicio, GameObject contenedor)
    {
        float anguloMedio = (anguloIzq + anguloDer) / 2f;
        float rad = anguloMedio * Mathf.Deg2Rad;

        Vector3 dirRadial = new Vector3(Mathf.Cos(rad), 0, Mathf.Sin(rad)).normalized;
        Vector3 dirTangente = new Vector3(-Mathf.Sin(rad), 0, Mathf.Cos(rad)).normalized;

        float yBase = CalcularAlturaAcumulada(filaInicio);

        // La escalera arranca en el radio interior, al angulo del vomito
        Vector3 posBase = transform.TransformPoint(CalcularPunto(anguloMedio, radioInferior, filaInicio));

        // Direccion de descenso: tangente al arco (misma que el pasillo)
        // Hacia afuera del estadio = dirTangente o -dirTangente segun orientacion
        Vector3 dirDescenso = dirTangente;

        for (int e = 0; e < numEscalonesVomito; e++)
        {
            float yEscalon = yBase - (e + 1) * altoEscalonVomito;
            float distDescenso = (e * profundidadEscalonVomito + profundidadEscalonVomito / 2f);

            Vector3 posEscalon = posBase + dirDescenso * distDescenso;
            posEscalon.y = yEscalon + altoEscalonVomito / 2f;

            GameObject escalonGO = Instantiate(Escalera, contenedor.transform);
            escalonGO.transform.position = posEscalon;
            escalonGO.transform.rotation = Quaternion.LookRotation(dirDescenso, Vector3.up);
            escalonGO.transform.localScale = new Vector3(anchoPasilloVomito / 2f,
                altoEscalonVomito / altoEscalon,
                profundidadEscalonVomito / anchoEscalon);

            if (GrisCemento != null)
                AplicarMaterialATodo(escalonGO, GrisCemento);
        }

        if (generarBarandillaEscalera)
            GenerarBarandillaVomitoCodo(posBase, dirDescenso, dirRadial, filaInicio, contenedor);
    }

    void GenerarBarandillaVomitoCodo(Vector3 posBase, Vector3 dirRadial, Vector3 dirTangente, int filaInicio, GameObject contenedor)
    {
        float diametro = 0.05f;
        float alturaBarandilla = 1.0f;
        float mitadAncho = anchoPasilloVomito / 4f;

        float yBase = CalcularAlturaAcumulada(filaInicio);

        Vector3[] posicionesLado = {
        posBase - dirTangente * mitadAncho,
        posBase + dirTangente * mitadAncho
    };

        foreach (Vector3 posLado in posicionesLado)
        {
            Vector3 pInicio = posLado + dirRadial * (profundidadEscalonVomito / 2f);
            pInicio.y = yBase - altoEscalonVomito + alturaBarandilla;

            Vector3 pFin = posLado + dirRadial * (numEscalonesVomito * profundidadEscalonVomito - profundidadEscalonVomito / 2f);
            pFin.y = yBase - numEscalonesVomito * altoEscalonVomito + alturaBarandilla;

            CrearCañoEntreDosPuntos(pInicio, pFin, diametro, contenedor.transform);

            Vector3 baseInicio = pInicio;
            baseInicio.y -= alturaBarandilla;
            CrearCañoEntreDosPuntos(baseInicio, pInicio, diametro, contenedor.transform);

            Vector3 baseFin = pFin;
            baseFin.y -= alturaBarandilla;
            CrearCañoEntreDosPuntos(baseFin, pFin, diametro, contenedor.transform);
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