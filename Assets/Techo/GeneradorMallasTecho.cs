using System;
using System.Collections.Generic;
using UnityEngine;

namespace Estadio.Techo
{
    /// <summary>
    /// Convierte la geometria del techo en objetos de escena. Por ahora los cuatro elementos
    /// del borde del vano: barre el prefab modular a lo largo de cada eje.
    ///
    /// Solo corre en modo juego. Asi los objetos generados no pueden quedar serializados en
    /// el archivo de escena.
    /// </summary>
    public sealed class GeneradorMallasTecho : MonoBehaviour
    {
        [Header("Prefabs")]
        [Tooltip("Modulo repetible de la estructura tubular. Eje del barrido en X, " +
                 "arrancando en x=0 y terminando en x=longitudModuloTubular.")]
        [SerializeField] private GameObject prefabTubular;
        [SerializeField] private float longitudModuloTubular = 2.0f;

        [Tooltip("Modulo del puente de cabecera. Solo se usa si el puente se genera con " +
                 "prefab; si esta vacio se genera procedural con cables.")]
        [SerializeField] private GameObject prefabPuente;
        [SerializeField] private float longitudModuloPuente = 2.0f;

        [Header("Puente de cables (Diseno 1)")]
        [Tooltip("En el Diseno 1 el puente son dos cables paralelos separados el canto del " +
                 "elemento, unidos por pendolas verticales. No lleva prefab: la forma sale " +
                 "de la panza, que a su vez depende de la tension de la membrana.")]
        [SerializeField] private bool puenteConCables = true;
        [SerializeField] private float diametroCablePuente = 0.12f;
        [SerializeField] private int pendolasPorPuente = 10;
        [SerializeField] private float diametroPendola = 0.08f;
        [SerializeField, Range(4, 12)] private int ladosCilindro = 6;
        [SerializeField] private Material materialCables;

        [Header("Esquinas del vano")]
        [Tooltip("Giro a partir del cual se corta el barrido y se deja el hueco de la " +
                 "esquina. Con el vano casi rectangular el giro se concentra en pocos metros.")]
        [SerializeField] private float anguloMaximoEntreModulos = 20f;

        [Header("Cables")]
        [SerializeField] private bool generarCables = true;
        [SerializeField] private float diametroCableTransversal = 0.06f;
        [SerializeField] private float diametroCableLongitudinal = 0.05f;
        [Tooltip("Segmentos por tramo al muestrear cada cable. Mas segmentos, mas suave la " +
                 "panza; los longitudinales van casi rectos y necesitan menos.")]
        [SerializeField, Range(2, 24)] private int segmentosPorTramoCable = 8;

        [Header("Membrana")]
        [SerializeField] private bool generarMembrana = true;
        [Tooltip("Material del pano. Conviene un shader Lit en modo Transparent con Render " +
                 "Face en Both: asi se ve de los dos lados sin duplicar geometria.")]
        [SerializeField] private Material materialMembrana;
        [Tooltip("Material del faldon, mas transparente que el del pano.")]
        [SerializeField] private Material materialFaldon;

        [Header("Salida")]
        [SerializeField] private Transform origenTecho;
        [Tooltip("El batching estatico no se aplica a la membrana: al combinarla con el resto " +
                 "se pierde el orden de dibujo que necesitan los materiales transparentes.")]
        [SerializeField] private bool combinarEstatico = true;

        [Tooltip("Cuanto baja el tubular respecto del eje del borde, en fracciones de su canto. " +
         "Ajustar hasta que los cables pasen por sus dos tubos superiores.")]
        [SerializeField, Range(0f, 2f)] private float bajadaTubularEnCantos = 1f;

        [Tooltip("Corrimiento extra hacia afuera del vano, por encima de medio ancho de seccion.")]
        [SerializeField] private float corrimientoTubularExtra = 0f;


        [Header("Viguetas del pano")]
        [Tooltip("Perfiles que corren del borde exterior al vano, por debajo de la tela. Los cables " +
         "las sostienen y la membrana apoya sobre ellas.")]
        [SerializeField] private bool generarViguetas = true;
        [SerializeField] private float separacionViguetas = 4f;
        [SerializeField] private float altoVigueta = 0.20f;
        [SerializeField] private float grosorVigueta = 0.08f;
        [SerializeField] private Material materialViguetas;

        [Header("Cables del faldon")]
        [Tooltip("Cables horizontales que recorren el faldon dando la vuelta al estadio, separados " +
                 "en altura. Donde el faldon es minimo entra uno solo; donde cae veinte metros, diez.")]
        [SerializeField] private bool generarCablesFaldon = true;
        [SerializeField] private float separacionCablesFaldon = 2f;
        [SerializeField] private float diametroCableFaldon = 0.05f;


        [Header("Costuras del pano")]
        [SerializeField] private bool generarCosturasPano = true;
        [Tooltip("Lado de los cuadros que forman las costuras.")]
        [SerializeField] private float separacionCosturasPano = 4f;
        [SerializeField] private float anchoCostura = 0.05f;
        [SerializeField] private float espesorCostura = 0.02f;
        [SerializeField] private Material materialCosturas;

        [Header("Costuras del faldon")]
        [SerializeField] private bool generarCosturasFaldon = true;
        [Tooltip("Separacion de las costuras verticales del faldon.")]
        [SerializeField] private float separacionCosturasFaldon = 2f;


        [Header("Tubulares del Diseno 2")]
        [Tooltip("Material propio de las tubulares cuando corren por encima de la tela. Si queda " +
                 "vacio se usa el mismo prefab sin cambiarle el material.")]
        [SerializeField] private Material materialTubularDiseno2;
        [Tooltip("Cuanto suben las tubulares respecto del eje del borde en el Diseno 2. Ahi van por " +
                 "ENCIMA de la tela, al reves que en el Diseno 1.")]
        [SerializeField] private float subidaTubularDiseno2 = 1.5f;
        [Tooltip("Cuanto se retiran las tubulares hacia las cabeceras respecto del borde del vano.")]
        [SerializeField] private float retiroTubularDiseno2 = 2.5f;



        private ControladorTecho _controlador;

        private ControladorTecho Controlador
        {
            get
            {
                if (_controlador == null) _controlador = GetComponent<ControladorTecho>();
                return _controlador;
            }
        }

        private GameObject _raiz;

        public bool Generado => _raiz != null;

        public int ModulosInstanciados { get; private set; }

        public int EsquinasSalteadas { get; private set; }

       
        public void Descartar()
        {
            if (_raiz == null) return;

            if (Application.isPlaying) Destroy(_raiz);
            else DestroyImmediate(_raiz);

            _raiz = null;
            ModulosInstanciados = 0;
            EsquinasSalteadas = 0;
        }

        public void MostrarUOcultar(bool visible)
        {
            if (_raiz != null) _raiz.SetActive(visible);
        }

        public void Generar(MarcoRigidoTecho marco, MembranaTecho membrana,
                            TendidoCables tendido)
        {
            if (!Application.isPlaying)
            {
                Debug.LogWarning("[Techo] El generador de mallas solo corre en modo juego.", this);
                return;
            }



            if (marco == null) throw new ArgumentNullException(nameof(marco));
            if (prefabTubular == null)
            {
                Debug.LogError("[Techo] Falta asignar el prefab tubular.", this);
                return;
            }

            Descartar();

            _raiz = new GameObject("Techo_Generado");
            _raiz.transform.SetParent(origenTecho != null ? origenTecho : transform, false);

            foreach (ElementoBordeConstruido elemento in marco.Elementos)
                BarrerElemento(elemento);

            if (combinarEstatico)
            {
                foreach (Transform hijo in _raiz.GetComponentsInChildren<Transform>())
                    if (hijo.gameObject != _raiz) hijo.gameObject.isStatic = true;

                StaticBatchingUtility.Combine(_raiz);
            }

            // La membrana va despues del batching y fuera de el: es transparente, y Unity
            // ordena el dibujo por objeto. Combinarla con la estructura opaca mezclaria las
            // dos colas de render.
            if (generarMembrana && membrana != null && membrana.Construida)
                GenerarMembrana(membrana);

            if (membrana != null && membrana.Construida)
            {
                if (generarViguetas) GenerarViguetas(membrana);
                if (generarCablesFaldon) GenerarCablesFaldon(membrana);
            }

            bool esDiseno1 = Controlador != null && Controlador.Diseno == DisenoTecho.Diseno1Membrana;

            if (generarCables && esDiseno1 && tendido != null && tendido.Construido)
                GenerarCables(tendido);

            //if (generarCables && tendido != null && tendido.Construido)
            //    GenerarCables(tendido);

            if (generarCosturasPano) GenerarCosturasPano(membrana);
            if (generarCosturasFaldon) GenerarCosturasFaldon(membrana);
           
        }
        
        private void BarrerElemento(ElementoBordeConstruido elemento)
        {
            bool esDiseno1 = Controlador != null && Controlador.Diseno == DisenoTecho.Diseno1Membrana;

            Vector3[] eje = elemento.eje;
            if (eje == null || eje.Length < 2) return;

            bool esPuente = elemento.tipo == TipoElementoBorde.PuenteCabecera;

            var contenedor = new GameObject($"Borde_{elemento.id}");
            contenedor.transform.SetParent(_raiz.transform, false);

            // El puente del Diseno 1 no es un reticulado: son dos cables paralelos con
            // pendolas. Su forma sale de la panza del eje, que depende de la tension de la
            // membrana, asi que no puede venir de un prefab rigido.
            //if (esPuente && puenteConCables)
            //{
            //    GenerarPuenteDeCables(elemento, contenedor.transform);
            //    return;
            //}

            //var ejeDesplazado = new Vector3[eje.Length];


            //Vector3 centroVano = Vector3.zero;

            //for (int i = 0; i < eje.Length; i++)
            //{
            //    Vector3 p = eje[i];

            //    Vector2 haciaAfuera = new Vector2(p.x - centroVano.x, p.z - centroVano.z);
            //    haciaAfuera = haciaAfuera.sqrMagnitude > 1e-6f ? haciaAfuera.normalized : Vector2.right;

            //    float corrimiento = elemento.ancho * 0.5f + corrimientoTubularExtra;

            //    ejeDesplazado[i] = new Vector3(
            //        p.x + haciaAfuera.x * corrimiento,
            //        p.y - elemento.canto * bajadaTubularEnCantos,
            //        p.z + haciaAfuera.y * corrimiento);
            //}

            //eje = ejeDesplazado;

            if (esPuente)
            {
                // En el Diseno 2 los puentes son rigidos y con volumen: los genera su propio
                // componente. Aca solo se resuelve el puente de cables del Diseno 1.
                if (!esDiseno1) return;

                if (puenteConCables)
                {
                    GenerarPuenteDeCables(elemento, contenedor.transform);
                    return;
                }
            }

            var ejeDesplazado = new Vector3[eje.Length];
            Vector3 centroVano = Vector3.zero;

            for (int i = 0; i < eje.Length; i++)
            {
                Vector3 p = eje[i];

                Vector2 haciaAfuera = new Vector2(p.x - centroVano.x, p.z - centroVano.z);
                haciaAfuera = haciaAfuera.sqrMagnitude > 1e-6f ? haciaAfuera.normalized : Vector2.right;

                // Diseno 1: la tubular cuelga de los cables y queda bajo la tela. Diseno 2: va por
                // encima y algo mas retirada hacia las cabeceras.
                float corrimiento = esDiseno1
                    ? elemento.ancho * 0.5f + corrimientoTubularExtra
                    : elemento.ancho * 0.5f + retiroTubularDiseno2;

                float desplazamientoY = esDiseno1
                    ? -elemento.canto * bajadaTubularEnCantos
                    : +subidaTubularDiseno2;

                ejeDesplazado[i] = new Vector3(
                    p.x + haciaAfuera.x * corrimiento,
                    p.y + desplazamientoY,
                    p.z + haciaAfuera.y * corrimiento);
            }

            eje = ejeDesplazado;

            GameObject prefab = esPuente && prefabPuente != null ? prefabPuente : prefabTubular;
            float longitudModulo = esPuente && prefabPuente != null
                ? longitudModuloPuente
                : longitudModuloTubular;

            // Se acumulan segmentos consecutivos mientras la direccion se mantenga estable.
            var tramo = new List<Vector3> { eje[0] };
            Vector3 direccionAnterior = (eje[1] - eje[0]).normalized;

            for (int i = 1; i < eje.Length; i++)
            {
                Vector3 direccion = (eje[i] - eje[i - 1]).normalized;

                if (Vector3.Angle(direccion, direccionAnterior) > anguloMaximoEntreModulos)
                {
                    BarrerTramoRecto(tramo, contenedor.transform, prefab, longitudModulo);
                    EsquinasSalteadas++;
                    tramo.Clear();
                }

                tramo.Add(eje[i]);
                direccionAnterior = direccion;
            }

            BarrerTramoRecto(tramo, contenedor.transform, prefab, longitudModulo);
        }
        
        //private void BarrerTramoRecto(List<Vector3> puntos, Transform padre,
        //                              GameObject prefab, float longitudModulo)
        //{
        //    if (puntos.Count < 2) return;

        //    float longitud = 0f;
        //    for (int i = 1; i < puntos.Count; i++)
        //        longitud += Vector3.Distance(puntos[i - 1], puntos[i]);

        //    if (longitud < longitudModulo * 0.5f) return;

        //    int cantidad = Mathf.Max(1, Mathf.RoundToInt(longitud / longitudModulo));
        //    float escalaX = (longitud / cantidad) / longitudModulo;

        //    for (int i = 0; i < cantidad; i++)
        //    {
        //        Vector3 inicio = PuntoEnPolilinea(puntos, longitud, (float)i / cantidad);
        //        Vector3 fin = PuntoEnPolilinea(puntos, longitud, (float)(i + 1) / cantidad);

        //        Vector3 direccion = fin - inicio;
        //        if (direccion.sqrMagnitude < 1e-6f) continue;

        //        GameObject modulo = Instantiate(prefab, padre);
        //        modulo.transform.localPosition = inicio;

        //        // LookRotation fija tambien el giro alrededor del eje: FromToRotation deja el
        //        // roll indeterminado y los modulos no empalman.
        //        Vector3 dir = direccion.normalized;
        //        modulo.transform.localRotation = Quaternion.LookRotation(dir, Vector3.up)
        //                                       * Quaternion.Euler(0f, -90f, 0f);

        //        modulo.transform.localScale = new Vector3(escalaX, 1f, 1f);

        //        ModulosInstanciados++;
        //    }
        //}


        /// <summary>
        /// La cantidad de modulos se calcula por tramo y la escala en X se ajusta levemente para
        /// que cierre exacto: es mucho menos visible que dejar un resto sin cubrir.
        /// </summary>
        private void BarrerTramoRecto(List<Vector3> puntos, Transform padre,
                                      GameObject prefab, float longitudModulo)
        {
            if (puntos.Count < 2) return;

            float longitud = 0f;
            for (int i = 1; i < puntos.Count; i++)
                longitud += Vector3.Distance(puntos[i - 1], puntos[i]);

            if (longitud < longitudModulo * 0.5f) return;

            int cantidad = Mathf.Max(1, Mathf.RoundToInt(longitud / longitudModulo));
            float escalaX = (longitud / cantidad) / longitudModulo;

            // En el Diseno 2 las tubulares corren por encima de la tela y llevan material propio.
            bool esDiseno1 = Controlador == null || Controlador.Diseno == DisenoTecho.Diseno1Membrana;
            bool cambiarMaterial = !esDiseno1 && materialTubularDiseno2 != null;

            for (int i = 0; i < cantidad; i++)
            {
                Vector3 inicio = PuntoEnPolilinea(puntos, longitud, (float)i / cantidad);
                Vector3 fin = PuntoEnPolilinea(puntos, longitud, (float)(i + 1) / cantidad);

                Vector3 direccion = fin - inicio;
                if (direccion.sqrMagnitude < 1e-6f) continue;

                GameObject modulo = Instantiate(prefab, padre);
                modulo.transform.localPosition = inicio;

                // LookRotation fija tambien el giro alrededor del eje: FromToRotation deja el roll
                // indeterminado y los modulos no empalman.
                Vector3 dir = direccion.normalized;
                modulo.transform.localRotation = Quaternion.LookRotation(dir, Vector3.up)
                                               * Quaternion.Euler(0f, -90f, 0f);

                modulo.transform.localScale = new Vector3(escalaX, 1f, 1f);

                if (cambiarMaterial)
                    foreach (Renderer r in modulo.GetComponentsInChildren<Renderer>())
                        r.sharedMaterial = materialTubularDiseno2;

                ModulosInstanciados++;
            }
        }


        // ------------------------------------------------------------------
        //  Puente de cables
        // ------------------------------------------------------------------

        /// <summary>
        /// Dos cables paralelos separados el canto del elemento, unidos por pendolas
        /// verticales. El superior corre por el eje —a la altura de los cables que sostienen
        /// la membrana— y el inferior a la cota del cordon inferior de las tubulares.
        /// </summary>
        //private void GenerarPuenteDeCables(ElementoBordeConstruido elemento, Transform padre)
        //{
        //    Vector3[] superior = elemento.eje;

        //    var inferior = new Vector3[superior.Length];
        //    for (int i = 0; i < superior.Length; i++)
        //        inferior[i] = superior[i] - Vector3.up * elemento.canto;

        //    CrearTuboPorPolilinea(superior, diametroCablePuente, $"{elemento.id}_cable_sup", padre);
        //    CrearTuboPorPolilinea(inferior, diametroCablePuente, $"{elemento.id}_cable_inf", padre);

        //    int pendolas = Mathf.Max(2, pendolasPorPuente);
        //    for (int p = 0; p <= pendolas; p++)
        //    {
        //        float u = (float)p / pendolas;
        //        int i = Mathf.Clamp(Mathf.RoundToInt(u * (superior.Length - 1)), 0, superior.Length - 1);

        //        CrearTuboPorPolilinea(new[] { superior[i], inferior[i] },
        //                              diametroPendola, $"{elemento.id}_pendola_{p}", padre);
        //    }

        //    ModulosInstanciados += 2 + pendolas + 1;
        //}

        private void GenerarPuenteDeCables(ElementoBordeConstruido elemento, Transform padre)
        {
            Vector3[] superior = elemento.eje;

            var inferior = new Vector3[superior.Length];
            for (int i = 0; i < superior.Length; i++)
                inferior[i] = superior[i] - Vector3.up * elemento.canto;

            CrearTuboPorPolilinea(superior, diametroCablePuente, $"{elemento.id}_cable_sup", padre);
            CrearTuboPorPolilinea(inferior, diametroCablePuente, $"{elemento.id}_cable_inf", padre);

            // Por longitud y no por indice: el eje viene muestreado por parametro, que con el vano
            // casi rectangular concentra la mitad de las muestras en las esquinas.
            float total = 0f;
            var acumulado = new float[superior.Length];
            for (int i = 1; i < superior.Length; i++)
            {
                total += Vector3.Distance(superior[i - 1], superior[i]);
                acumulado[i] = total;
            }

            int pendolas = Mathf.Max(2, pendolasPorPuente);
            for (int p = 0; p <= pendolas; p++)
            {
                float objetivo = (float)p / pendolas * total;

                // Interpolando y no eligiendo el indice mas cercano: el eje viene con las muestras
                // muy desparejas, y quedarse con el indice amontona las pendolas donde hay muchas
                // muestras juntas.
                Vector3 arriba = superior[superior.Length - 1];

                for (int k = 1; k < superior.Length; k++)
                {
                    if (acumulado[k] < objetivo) continue;

                    float tramo = acumulado[k] - acumulado[k - 1];
                    float f = tramo > 1e-4f ? (objetivo - acumulado[k - 1]) / tramo : 0f;
                    arriba = Vector3.Lerp(superior[k - 1], superior[k], f);
                    break;
                }

                Vector3 abajo = arriba - Vector3.up * elemento.canto;

                CrearTuboPorPolilinea(new[] { arriba, abajo },
                                      diametroPendola, $"{elemento.id}_pendola_{p}", padre);                
            }
            
            ModulosInstanciados += 2 + pendolas + 1;
        }

        /// <summary>
        /// Barre una seccion circular a lo largo de una polilinea. Se genera procedural en
        /// vez de instanciar prefabs porque la directriz cambia con la tension de la membrana.
        /// </summary>
        private void CrearTuboPorPolilinea(Vector3[] eje, float diametro, string nombre, Transform padre)
        {
            if (eje == null || eje.Length < 2) return;

            int lados = Mathf.Max(4, ladosCilindro);
            float radio = diametro * 0.5f;

            var vertices = new List<Vector3>(eje.Length * lados);
            var triangulos = new List<int>(eje.Length * lados * 6);

            for (int i = 0; i < eje.Length; i++)
            {
                Vector3 tangente = i == 0
                    ? (eje[1] - eje[0]).normalized
                    : (eje[i] - eje[i - 1]).normalized;

                Vector3 normal = Vector3.Cross(tangente, Vector3.up);
                if (normal.sqrMagnitude < 1e-6f) normal = Vector3.right;
                normal.Normalize();

                Vector3 binormal = Vector3.Cross(tangente, normal).normalized;

                for (int l = 0; l < lados; l++)
                {
                    float ang = 2f * Mathf.PI * l / lados;
                    vertices.Add(eje[i] + (normal * Mathf.Cos(ang) + binormal * Mathf.Sin(ang)) * radio);
                }
            }

            for (int i = 0; i < eje.Length - 1; i++)
            {
                int a = i * lados;
                int b = (i + 1) * lados;

                for (int l = 0; l < lados; l++)
                {
                    int l1 = l;
                    int l2 = (l + 1) % lados;
                    triangulos.AddRange(new[] { a + l1, a + l2, b + l1, a + l2, b + l2, b + l1 });
                }
            }

            var mesh = new Mesh();
            mesh.vertices = vertices.ToArray();
            mesh.triangles = triangulos.ToArray();
            mesh.RecalculateNormals();

            var go = new GameObject(nombre);
            go.transform.SetParent(padre, false);
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;
            go.AddComponent<MeshFilter>().mesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = materialCables;
        }

        // ------------------------------------------------------------------
        //  Cables
        // ------------------------------------------------------------------

        /// <summary>
        /// Barre un tubo a lo largo de cada cable. La panza ya esta resuelta en la geometria:
        /// Muestrear devuelve la polilinea con la parabola de cada tramo aplicada.
        /// </summary>
        private void GenerarCables(TendidoCables tendido)
        {
            var contenedor = new GameObject("Cables");
            contenedor.transform.SetParent(_raiz.transform, false);

            var transversales = new GameObject("Transversales");
            transversales.transform.SetParent(contenedor.transform, false);

            foreach (Cable cable in tendido.Transversales)
                CrearTuboPorPolilinea(cable.Muestrear(segmentosPorTramoCable),
                                      diametroCableTransversal,
                                      $"Transversal_{cable.indice}", transversales.transform);

            var longitudinales = new GameObject("Longitudinales");
            longitudinales.transform.SetParent(contenedor.transform, false);

            foreach (Cable cable in tendido.Longitudinales)
                CrearTuboPorPolilinea(cable.Muestrear(segmentosPorTramoCable),
                                      diametroCableLongitudinal,
                                      $"Longitudinal_{cable.indice}", longitudinales.transform);

            ModulosInstanciados += tendido.Transversales.Count + tendido.Longitudinales.Count;
        }

        // ------------------------------------------------------------------
        //  Membrana
        // ------------------------------------------------------------------

        /// <summary>
        /// Triangula las dos rejillas que calcula MembranaTecho. No hay geometria que
        /// inventar: los vertices y las UV ya estan resueltos, y aca solo se arman las caras.
        /// </summary>
        private void GenerarMembrana(MembranaTecho membrana)
        {
            var contenedor = new GameObject("Membrana");
            contenedor.transform.SetParent(_raiz.transform, false);

            CrearMallaDeRejilla(membrana.RejillaPano, materialMembrana, "Pano",
                                contenedor.transform);

            CrearMallaDeRejilla(membrana.RejillaFaldon, materialFaldon, "Faldon",
                                contenedor.transform);
        }

        /// <summary>
        /// Arma una malla a partir de una rejilla de filas x columnas. Las columnas cierran
        /// sobre si mismas —el anillo da la vuelta completa—, asi que la ultima se une con la
        /// primera sin costura.
        /// </summary>
        private static void CrearMallaDeRejilla(RejillaSuperficie rejilla, Material material,
                                                string nombre, Transform padre)
        {
            if (rejilla.vertices == null || rejilla.vertices.Length == 0) return;
            if (rejilla.filas < 2 || rejilla.columnas < 3) return;

            int filas = rejilla.filas;
            int columnas = rejilla.columnas;

            var triangulos = new List<int>(filas * columnas * 6);

            for (int c = 0; c < columnas; c++)
            {
                int cSiguiente = (c + 1) % columnas;

                for (int f = 0; f < filas - 1; f++)
                {
                    int a = rejilla.Indice(f, c);
                    int b = rejilla.Indice(f, cSiguiente);
                    int d = rejilla.Indice(f + 1, c);
                    int e = rejilla.Indice(f + 1, cSiguiente);

                    triangulos.Add(a); triangulos.Add(b); triangulos.Add(d);
                    triangulos.Add(b); triangulos.Add(e); triangulos.Add(d);
                }
            }

            var mesh = new Mesh();

            // 2496 vertices entran de sobra en 16 bits, pero con resoluciones altas del pano
            // se pasa el limite y la malla sale vacia sin avisar.
            if (rejilla.vertices.Length > 65000)
                mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;

            mesh.vertices = rejilla.vertices;
            mesh.uv = rejilla.uv;
            mesh.triangles = triangulos.ToArray();
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            var go = new GameObject(nombre);
            go.transform.SetParent(padre, false);
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;
            go.AddComponent<MeshFilter>().mesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = material;
        }

        private static Vector3 PuntoEnPolilinea(List<Vector3> puntos, float longitudTotal, float u)
        {
            float objetivo = Mathf.Clamp01(u) * longitudTotal;
            float acumulado = 0f;

            for (int i = 1; i < puntos.Count; i++)
            {
                float tramo = Vector3.Distance(puntos[i - 1], puntos[i]);
                if (acumulado + tramo >= objetivo || i == puntos.Count - 1)
                {
                    float t = tramo > 1e-6f ? (objetivo - acumulado) / tramo : 0f;
                    return Vector3.Lerp(puntos[i - 1], puntos[i], Mathf.Clamp01(t));
                }
                acumulado += tramo;
            }

            return puntos[puntos.Count - 1];
        }

       
        /// <summary>
        /// Perfiles que corren por debajo de la tela y por encima de los cables. Siguen la
        /// orientacion de los cables de su zona: transversales donde hay anclajes —plateas y
        /// codos— y longitudinales en las cabeceras, donde los cables corren a lo largo.
        ///
        /// No se toman de la rejilla del pano: ese recorrido es radial y en las esquinas queda
        /// oblicuo. Se generan sobre la superficie de la tela a su propia separacion.
        /// </summary>
        private void GenerarViguetas(MembranaTecho membrana)
        {
            ControladorTecho c = Controlador;
            if (c == null || !c.GeometriaLista) return;

            PerimetroTecho perimetro = c.PerimetroTecho;
            BordeInteriorTecho borde = c.Borde;


            var contenedor = new GameObject("Viguetas");
            contenedor.transform.SetParent(_raiz.transform, false);

            float semiLargo = perimetro.SemiLargo;
            float semiVanoX = borde.Parametros.SemiVanoX;
            float semiVanoZ = borde.Parametros.SemiVanoZ;

            // --- Transversales: a Z constante, de una viga longitudinal a la otra ---
            int pasosZ = Mathf.FloorToInt(semiLargo / separacionViguetas);

            for (int k = -pasosZ; k <= pasosZ; k++)
            {
                float z = k * separacionViguetas;

                bool sobrePlatea = !perimetro.EsZonaCodo(true, z) || !perimetro.EsZonaCodo(false, z);
                if (!sobrePlatea) continue;

                //perimetro.ExtremosTransversal(z, out Vector2 xzNeg, out Vector2 xzPos);
                //CrearViguetaSobreTela(membrana, xzNeg.x, xzPos.x, z, true, contenedor.transform);

                perimetro.ExtremosTransversal(z, out Vector2 xzNeg, out Vector2 xzPos);

                // La vigueta no cruza el vano: solo los cables lo hacen. Se parte en dos tramos, uno por
                // cada lado del borde del vano.
                if (borde.IntersectarZ(z, out Vector3 bordeXNeg, out Vector3 bordeXPos))
                {
                    CrearViguetaSobreTela(membrana, xzNeg.x, bordeXNeg.x, z, true, contenedor.transform);
                    CrearViguetaSobreTela(membrana, bordeXPos.x, xzPos.x, z, true, contenedor.transform);
                }
                else
                {
                    CrearViguetaSobreTela(membrana, xzNeg.x, xzPos.x, z, true, contenedor.transform);
                }



            }

            // --- Longitudinales: a X constante, en las cabeceras ---
            int pasosX = Mathf.FloorToInt(semiVanoX * 2f / separacionViguetas);

            for (int k = -pasosX; k <= pasosX; k++)
            {
                float x = k * separacionViguetas;

                foreach (int signo in new[] { -1, 1 })
                {
                    float zBorde = signo * semiVanoZ;
                    float zCierre = signo * semiLargo;

                    // Solo donde no hay anclajes: en las plateas ya pusimos las transversales.
                    if (!perimetro.EsZonaCodo(x > 0f, zCierre)) continue;

                    CrearViguetaSobreTela(membrana, zBorde, zCierre, x, false, contenedor.transform);
                }
            }
        }

        /// <summary>
        /// Una vigueta entre dos coordenadas, siguiendo la superficie de la tela. Se ubica
        /// altoVigueta por debajo de ella: el orden vertical es tela, vigueta, cable.
        /// </summary>
        private void CrearViguetaSobreTela(MembranaTecho membrana, float desde, float hasta,
                                           float fija, bool transversal, Transform padre)
        {
            const int segmentos = 24;
            var eje = new List<Vector3>(segmentos + 1);

            for (int i = 0; i <= segmentos; i++)
            {
                float t = Mathf.Lerp(desde, hasta, (float)i / segmentos);

                float x = transversal ? t : fija;
                float z = transversal ? fija : t;

                if (!membrana.TryAlturaTela(x, z, out float y)) continue;

                eje.Add(new Vector3(x, y - altoVigueta * 0.5f, z));
            }

            if (eje.Count < 2) return;

            CrearPerfilPorPolilinea(eje.ToArray(), grosorVigueta, altoVigueta,
                                    $"Vigueta_{(transversal ? "T" : "L")}_{fija:F0}",
                                    padre, materialViguetas);

            ModulosInstanciados++;
        }


        /// <summary>
        /// Cables horizontales del faldon. La rejilla tiene solo dos filas, asi que cada cable se
        /// arma interpolando entre ellas. Como la caida varia mucho a lo largo del perimetro, cada
        /// cable se corta donde el faldon deja de llegar a esa profundidad.
        /// </summary>
        private void GenerarCablesFaldon(MembranaTecho membrana)
        {
            RejillaSuperficie rejilla = membrana.RejillaFaldon;
            if (rejilla.vertices == null || rejilla.columnas < 3) return;

            var contenedor = new GameObject("Cables_Faldon");
            contenedor.transform.SetParent(_raiz.transform, false);

            int filas = rejilla.filas;
            int niveles = Mathf.Max(1, Mathf.CeilToInt(membrana.CaidaFaldonMaxima / separacionCablesFaldon));

            for (int n = 1; n <= niveles; n++)
            {
                float profundidad = n * separacionCablesFaldon;
                var tramo = new List<Vector3>();

                for (int c = 0; c <= rejilla.columnas; c++)
                {
                    Vector3 arriba = rejilla.Vertice(0, c);
                    Vector3 abajo = rejilla.Vertice(filas - 1, c);

                    if (arriba.y - abajo.y >= profundidad)
                    {
                        tramo.Add(new Vector3(arriba.x, arriba.y - profundidad, arriba.z));
                        continue;
                    }

                    if (tramo.Count >= 2)
                    {
                        CrearTuboPorPolilinea(tramo.ToArray(), diametroCableFaldon,
                                              $"CableFaldon_{n}_{c}", contenedor.transform);
                        ModulosInstanciados++;
                    }
                    tramo.Clear();
                }

                if (tramo.Count >= 2)
                {
                    CrearTuboPorPolilinea(tramo.ToArray(), diametroCableFaldon,
                                          $"CableFaldon_{n}_fin", contenedor.transform);
                    ModulosInstanciados++;
                }
            }
        }

        /// <summary>Barre una seccion rectangular a lo largo de una polilinea.</summary>
        private static void CrearPerfilPorPolilinea(Vector3[] eje, float grosor, float alto,
                                                    string nombre, Transform padre, Material material)
        {
            if (eje == null || eje.Length < 2) return;

            var vertices = new List<Vector3>(eje.Length * 4);
            var triangulos = new List<int>(eje.Length * 24);

            float g = grosor * 0.5f;
            float h = alto * 0.5f;

            for (int i = 0; i < eje.Length; i++)
            {
                Vector3 direccion = i == 0
                    ? (eje[1] - eje[0]).normalized
                    : (eje[i] - eje[i - 1]).normalized;

                Vector3 lateral = Vector3.Cross(direccion, Vector3.up);
                if (lateral.sqrMagnitude < 1e-6f) lateral = Vector3.right;
                lateral = lateral.normalized * g;

                Vector3 vertical = Vector3.up * h;

                vertices.Add(eje[i] - lateral - vertical);
                vertices.Add(eje[i] - lateral + vertical);
                vertices.Add(eje[i] + lateral + vertical);
                vertices.Add(eje[i] + lateral - vertical);
            }

            for (int i = 0; i < eje.Length - 1; i++)
            {
                int a = i * 4;
                int b = (i + 1) * 4;

                for (int k = 0; k < 4; k++)
                {
                    int k1 = k;
                    int k2 = (k + 1) % 4;
                    triangulos.AddRange(new[] { a + k1, a + k2, b + k1, a + k2, b + k2, b + k1 });
                }
            }

            var mesh = new Mesh();
            mesh.vertices = vertices.ToArray();
            mesh.triangles = triangulos.ToArray();
            mesh.RecalculateNormals();

            var go = new GameObject(nombre);
            go.transform.SetParent(padre, false);
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;
            go.AddComponent<MeshFilter>().mesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = material;
        }

        private void GenerarCosturasPano(MembranaTecho membrana)
        {
            ControladorTecho c = Controlador;
            if (c == null || !c.GeometriaLista) return;

            PerimetroTecho perimetro = c.PerimetroTecho;
            BordeInteriorTecho borde = c.Borde;

            var contenedor = new GameObject("Costuras_Pano");
            contenedor.transform.SetParent(_raiz.transform, false);

            float semiLargo = perimetro.SemiLargo;
            float semiVanoX = borde.Parametros.SemiVanoX;
            float semiVanoZ = borde.Parametros.SemiVanoZ;

            // --- A Z constante: sobre plateas y codos, cortadas por el vano ---
            int pasosZ = Mathf.FloorToInt(semiLargo / separacionCosturasPano);

            for (int k = -pasosZ; k <= pasosZ; k++)
            {
                float z = k * separacionCosturasPano;

                perimetro.ExtremosTransversal(z, out Vector2 xzNeg, out Vector2 xzPos);

                if (borde.IntersectarZ(z, out Vector3 bNeg, out Vector3 bPos))
                {
                    CrearCosturaSobreTela(membrana, xzNeg.x, bNeg.x, z, true, contenedor.transform);
                    CrearCosturaSobreTela(membrana, bPos.x, xzPos.x, z, true, contenedor.transform);
                }
                else
                {
                    CrearCosturaSobreTela(membrana, xzNeg.x, xzPos.x, z, true, contenedor.transform);
                }
            }

            // --- A X constante: cruzan las anteriores para cerrar los cuadros ---
            float xLimite = Mathf.Max(Mathf.Abs(perimetro.RectaXNegativo.XenZ(0f)),
                                      Mathf.Abs(perimetro.RectaXPositivo.XenZ(0f)));
            int pasosX = Mathf.FloorToInt(xLimite / separacionCosturasPano);

            for (int k = -pasosX; k <= pasosX; k++)
            {
                float x = k * separacionCosturasPano;

                foreach (int signo in new[] { -1, 1 })
                {
                    float zInterior = Mathf.Abs(x) < semiVanoX ? signo * semiVanoZ : 0f;
                    float zExterior = signo * semiLargo;

                    CrearCosturaSobreTela(membrana, zInterior, zExterior, x, false, contenedor.transform);
                }
            }
        }

        private void CrearCosturaSobreTela(MembranaTecho membrana, float desde, float hasta,
                                           float fija, bool transversal, Transform padre)
        {
            const int segmentos = 32;
            var eje = new List<Vector3>(segmentos + 1);

            for (int i = 0; i <= segmentos; i++)
            {
                float t = Mathf.Lerp(desde, hasta, (float)i / segmentos);

                float x = transversal ? t : fija;
                float z = transversal ? fija : t;

                if (!membrana.TryAlturaTela(x, z, out float y)) continue;

                eje.Add(new Vector3(x, y + espesorCostura * 0.5f, z));
            }

            if (eje.Count < 2) return;

            CrearPerfilPorPolilinea(eje.ToArray(), anchoCostura, espesorCostura,
                                    $"Costura_{(transversal ? "T" : "L")}_{fija:F0}",
                                    padre, materialCosturas);

            ModulosInstanciados++;
        }



        /// <summary>Costuras verticales del faldon, de su borde superior al inferior.</summary>
        private void GenerarCosturasFaldon(MembranaTecho membrana)
        {
            RejillaSuperficie rejilla = membrana.RejillaFaldon;
            if (rejilla.vertices == null || rejilla.columnas < 3) return;

            var contenedor = new GameObject("Costuras_Faldon");
            contenedor.transform.SetParent(_raiz.transform, false);

            float perimetro = 0f;
            for (int col = 0; col < rejilla.columnas; col++)
                perimetro += Vector3.Distance(rejilla.Vertice(0, col), rejilla.Vertice(0, col + 1));

            float separacionColumna = perimetro / rejilla.columnas;
            int paso = Mathf.Max(1, Mathf.RoundToInt(separacionCosturasFaldon / Mathf.Max(0.01f, separacionColumna)));

            for (int col = 0; col < rejilla.columnas; col += paso)
            {
                var eje = new List<Vector3>(rejilla.filas);
                for (int f = 0; f < rejilla.filas; f++)
                    eje.Add(rejilla.Vertice(f, col));

                if (Vector3.Distance(eje[0], eje[eje.Count - 1]) < 0.3f) continue;

                CrearPerfilPorPolilinea(eje.ToArray(), anchoCostura, espesorCostura,
                                        $"CosturaFaldon_{col}", contenedor.transform, materialCosturas);
                ModulosInstanciados++;
            }
        }
    }
}
