using System;
using System.Collections.Generic;
using UnityEngine;

namespace Estadio.Techo
{
    /// <summary>
    /// Genera los soportes de techo en la zona de los codos: donde la viga longitudinal
    /// sigue de largo pero ya no hay platea abajo que la sostenga.
    ///
    /// No son estructura de estadio. Un estadio sin techo no los tendria: no sostienen
    /// ninguna grada, solo la viga longitudinal y los cables que ya nacen de ella. Por eso
    /// vive del lado del techo y corre al final de la cadena.
    ///
    /// Son parientes de los soportes rectos y continuan SU geometria: cada lado lee las
    /// distancias y la pendiente de la platea de ese lado, que pueden diferir entre los dos
    /// —distinta cantidad de filas, distinta profundidad de escalon, o directamente una
    /// popular en lugar de una platea—. La diferencia es que la diagonal se corta apoyandose
    /// en la vertical mas cercana al campo, para no chocar contra el codo.
    /// </summary>
    [RequireComponent(typeof(ControladorTecho))]
    public sealed class SoportesTechoCodo : MonoBehaviour
    {
        [Header("Referencias")]
        [SerializeField] private Transform ground0Level;
        [SerializeField] private Material materialVigas;

        [Header("Secciones")]
        [SerializeField] private float anchoViga = 0.2f;
        [SerializeField] private float altoViga = 0.2f;
        [SerializeField] private float anchoVigaDiagonal = 0.2f;
        [SerializeField] private float altoVigaDiagonal = 0.3f;

        [Header("Tensor")]
        [SerializeField] private bool generarTensor = true;
        [SerializeField] private float alturaTensor = 2f;
        [SerializeField] private float grosorTensor = 0.2f;
        [SerializeField] private float profundidadTensor = 1f;

        [Header("Ajustes")]
        [Tooltip("Cuanto mas alla del ultimo anclaje publicado empiezan a generarse. Evita " +
                 "que el primero se solape con el ultimo soporte recto.")]
        [SerializeField] private float margenAlUltimoAnclaje = 1f;

        [Tooltip("Corrimiento adicional de las dos verticales hacia el campo. Sirve para " +
                 "afinar el empalme si la linea no coincide exacto con la platea vecina.")]
        [SerializeField] private float corrimientoVerticales = 0f;

        [Tooltip("Cuanto por debajo del muro del codo arranca la diagonal, para que quede " +
                 "sosteniendolo desde abajo y no atravesandolo.")]
        [SerializeField] private float holguraBajoCoronamiento = 1f;

        [Tooltip("Deja libre el ultimo lugar de cada codo para la viga final, que ocupa ese " +
                 "mismo punto. Desmarcar si no se generan vigas finales.")]
        [SerializeField] private bool reservarExtremoParaVigaFinal = true;

        private ControladorTecho _controlador;
        private VigaLongitudinalTecho _viga;
        private GameObject _raiz;

        public bool Generado => _raiz != null;
        public int SoportesGenerados { get; private set; }

        private ControladorTecho Controlador
        {
            get
            {
                if (_controlador == null) _controlador = GetComponent<ControladorTecho>();
                return _controlador;
            }
        }

        // ------------------------------------------------------------------

        public void Descartar()
        {
            if (_raiz == null) return;

            if (Application.isPlaying) Destroy(_raiz);
            else DestroyImmediate(_raiz);

            _raiz = null;
            SoportesGenerados = 0;
        }

        public void Generar(Transform padre, EstadioConfigurator configurador)
        {
            if (!Application.isPlaying)
            {
                Debug.LogWarning("[Techo] Los soportes de codo solo se generan en modo juego.", this);
                return;
            }

            ControladorTecho c = Controlador;
            if (c == null || !c.GeometriaLista)
            {
                Debug.LogError("[Techo] La geometria del techo no esta lista.", this);
                return;
            }

            if (configurador == null)
            {
                Debug.LogError("[Techo] Sin configurador no se puede leer la geometria de los " +
                               "soportes de las plateas.", this);
                return;
            }

            Descartar();

            _raiz = new GameObject("Soportes_Techo_Codo");
            _raiz.transform.SetParent(padre != null ? padre : transform, false);

            GenerarLado(c, true, configurador.GeometriaSoporteXPositivo);
            GenerarLado(c, false, configurador.GeometriaSoporteXNegativo);

            Debug.Log($"[Techo] {SoportesGenerados} soportes de codo generados.", this);
        }

        /// <summary>
        /// Recorre las posiciones que reparte el perimetro del techo y genera un soporte en
        /// cada una que caiga fuera del tramo con anclajes publicados. La separacion y la
        /// linea salen de la misma recta que los soportes rectos.
        /// </summary>
        private void GenerarLado(ControladorTecho c, bool ladoPositivo, GeometriaSoporte geometria)
        {
            if (!geometria.EsValida)
            {
                Debug.LogWarning($"[Techo] El lado {(ladoPositivo ? "x+" : "x-")} no publico la " +
                                 "geometria de su soporte. Revisar generarSoportes en esa platea.", this);
                return;
            }

            Vector2[] posiciones = c.PerimetroTecho.RepartirAnclajes(ladoPositivo, out _);
            RectaViga recta = ladoPositivo
                ? c.PerimetroTecho.RectaXPositivo
                : c.PerimetroTecho.RectaXNegativo;

            float yPiso = NivelPiso(c);
            float semiLargo = c.PerimetroTecho.SemiLargo;

            // Direccion en planta hacia el campo. Es hacia donde se separan las verticales.
            Vector2 haciaElCampo = new Vector2(ladoPositivo ? -1f : 1f, 0f);
            Vector2 tangente = recta.Direccion;

            foreach (Vector2 posicion in posiciones)
            {
                if (!c.PerimetroTecho.EsZonaCodo(ladoPositivo, posicion.y, margenAlUltimoAnclaje))
                    continue;

                // La altura se consulta por posicion: la viga no es horizontal, sigue las
                // cabezas de tensor de la platea de ese lado.
                float alturaViga = AlturaViga(c, ladoPositivo, posicion.y);

                // El extremo del techo lo ocupa la viga final. Ahi solo va el tensor: el
                // cable de cierre nace de el.
                if (reservarExtremoParaVigaFinal &&
                    Mathf.Abs(Mathf.Abs(posicion.y) - semiLargo) < 0.5f)
                {
                    if (generarTensor)
                    {
                        var soloCabeza = new Vector3(posicion.x, alturaViga, posicion.y);
                        CrearCaja(soloCabeza - Vector3.up * alturaTensor, soloCabeza,
                                  grosorTensor, profundidadTensor, tangente, "Tensor");
                    }
                    continue;
                }

                GenerarSoporte(c, posicion, haciaElCampo, tangente, geometria,
                               alturaViga, yPiso);
                SoportesGenerados++;
            }
        }

        private void GenerarSoporte(ControladorTecho c, Vector2 posicion, Vector2 haciaElCampo,
                                    Vector2 tangente, GeometriaSoporte geometria,
                                    float alturaViga, float yPiso)
        {
            float dExterior = geometria.distanciaVerticalExterior + corrimientoVerticales;
            float dInterior = geometria.distanciaVerticalInterior + corrimientoVerticales;

            Vector2 xzExterior = posicion + haciaElCampo * dExterior;
            Vector2 xzInterior = posicion + haciaElCampo * dInterior;

            // La cabeza esta en la viga, y el tensor es lo ultimo: la diagonal muere donde
            // arranca el tensor, no en la viga. Asi los soportes de codo quedan igual que los
            // de la platea, donde la viga se apoya sobre la arista del tensor.
            var cabeza = new Vector3(posicion.x, alturaViga, posicion.y);
            var baseTensor = new Vector3(posicion.x, alturaViga - alturaTensor, posicion.y);

            //CrearCaja(new Vector3(xzExterior.x, yPiso, xzExterior.y),
            //          new Vector3(xzExterior.x, baseTensor.y, xzExterior.y),
            //          anchoViga, altoViga, tangente, "Vertical_Exterior");

            //// El arranque de la diagonal sale del coronamiento real del codo en este punto,
            //// no de la pendiente de la platea: el codo baja mucho mas rapido por el recorte
            //// de filas, y heredar esa pendiente dejaba la diagonal sobre los escalones.
            //float yCoronamiento = c.Coronamientos.AlturaBajoPunto(xzInterior);
            //float yArranque = Mathf.Max(yCoronamiento - holguraBajoCoronamiento, yPiso + 0.5f);

            //CrearCaja(new Vector3(xzInterior.x, yPiso, xzInterior.y),
            //          new Vector3(xzInterior.x, yArranque, xzInterior.y),
            //          anchoViga, altoViga, tangente, "Vertical_Interior");

            //CrearCaja(new Vector3(xzInterior.x, yArranque, xzInterior.y), baseTensor,
            //          anchoVigaDiagonal, altoVigaDiagonal, tangente, "Diagonal");

            float yCoronamiento = c.Coronamientos.AlturaBajoPunto(xzInterior);
            float yArranque = Mathf.Max(yCoronamiento - holguraBajoCoronamiento, yPiso + 0.5f);

            var arranque = new Vector3(xzInterior.x, yArranque, xzInterior.y);

            // La vertical exterior termina en el punto de la diagonal que tiene encima: si llegara
            // mas arriba la atravesaria.
            float uExterior = Mathf.InverseLerp(dInterior, 0f, dExterior);
            float yTopeExterior = Mathf.Lerp(yArranque, baseTensor.y, uExterior);

            CrearCaja(new Vector3(xzExterior.x, yPiso, xzExterior.y),
                      new Vector3(xzExterior.x, yTopeExterior, xzExterior.y),
                      anchoViga, altoViga, tangente, "Vertical_Exterior");

            CrearCaja(new Vector3(xzInterior.x, yPiso, xzInterior.y), arranque,
                      anchoViga, altoViga, tangente, "Vertical_Interior");

            CrearCaja(arranque, baseTensor, anchoVigaDiagonal, altoVigaDiagonal, tangente, "Diagonal");


            if (generarTensor)
                CrearCaja(baseTensor, cabeza, grosorTensor, profundidadTensor, tangente, "Tensor");
        }

        /// <summary>
        /// Altura de la viga longitudinal a esa cota Z. No se recalcula aca: la provee
        /// VigaLongitudinalTecho, que es la unica fuente de verdad. Antes cada componente la
        /// deducia por su cuenta y tenian que coincidir por casualidad.
        /// </summary>
        private float AlturaViga(ControladorTecho c, bool ladoPositivo, float z)
        {
            if (_viga == null) _viga = GetComponent<VigaLongitudinalTecho>();

            if (_viga == null)
            {
                Debug.LogError("[Techo] Falta el componente VigaLongitudinalTecho.", this);
                return 0f;
            }

            return _viga.AlturaEnZ(c, ladoPositivo, z);
        }

        private float NivelPiso(ControladorTecho c)
        {
            if (ground0Level == null) return 0f;
            return c.MatrizEstadio.inverse.MultiplyPoint3x4(ground0Level.position).y;
        }

        // ------------------------------------------------------------------
        //  Geometria
        // ------------------------------------------------------------------

        /// <summary>
        /// Caja recta entre dos puntos. La orientacion de la seccion se fija con la tangente
        /// de la viga longitudinal y no con un Cross contra Vector3.up: para una caja vertical
        /// ese producto es indeterminado y la seccion sale rotada al azar.
        /// </summary>
        private void CrearCaja(Vector3 desde, Vector3 hasta, float ancho, float alto,
                               Vector2 tangente, string nombre)
        {
            Vector3 eje = hasta - desde;
            if (eje.sqrMagnitude < 1e-6f) return;

            Vector3 direccion = eje.normalized;
            var tangente3 = new Vector3(tangente.x, 0f, tangente.y).normalized;

            Vector3 lateral = Vector3.Cross(direccion, tangente3);
            if (lateral.sqrMagnitude < 1e-6f) lateral = Vector3.Cross(direccion, Vector3.up);
            if (lateral.sqrMagnitude < 1e-6f) lateral = Vector3.right;
            lateral = lateral.normalized * (alto * 0.5f);

            Vector3 normal = tangente3 * (ancho * 0.5f);

            var v = new Vector3[8];
            v[0] = desde - lateral - normal;
            v[1] = desde - lateral + normal;
            v[2] = desde + lateral + normal;
            v[3] = desde + lateral - normal;
            v[4] = hasta - lateral - normal;
            v[5] = hasta - lateral + normal;
            v[6] = hasta + lateral + normal;
            v[7] = hasta + lateral - normal;

            var mesh = new Mesh
            {
                vertices = v,
                triangles = new[]
                {
                    0,1,2, 0,2,3,
                    4,6,5, 4,7,6,
                    0,4,5, 0,5,1,
                    3,2,6, 3,6,7,
                    1,5,6, 1,6,2,
                    0,3,7, 0,7,4
                }
            };
            mesh.RecalculateNormals();

            var go = new GameObject(nombre);
            go.transform.SetParent(_raiz.transform, false);
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;
            go.AddComponent<MeshFilter>().mesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = materialVigas;
        }
    }
}
