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


        [Tooltip("Deja libre el ultimo lugar de cada codo para la viga final, que ocupa ese mismo " +
         "punto. Desmarcar si no se generan vigas finales.")]
        [SerializeField] private bool reservarExtremoParaVigaFinal = true;

        private ControladorTecho _controlador;
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

            //float alturaViga = AlturaVigaLongitudinal(c, ladoPositivo);
            float yPiso = NivelPiso(c);

            // Direccion en planta hacia el campo. Es hacia donde se separan las verticales.
            Vector2 haciaElCampo = new Vector2(ladoPositivo ? -1f : 1f, 0f);
            Vector2 tangente = recta.Direccion;

            float semiLargo = c.PerimetroTecho.SemiLargo;

            foreach (Vector2 posicion in posiciones)
            {
                float alturaViga = AlturaVigaLongitudinal(c, ladoPositivo, posicion.y > 0f);

                if (!c.PerimetroTecho.EsZonaCodo(ladoPositivo, posicion.y, margenAlUltimoAnclaje))
                    continue;

                // El extremo del techo lo ocupa la viga final: si tambien pusieramos un soporte de
                // codo ahi, quedarian superpuestos en el mismo punto.

                if (reservarExtremoParaVigaFinal &&
    Mathf.Abs(Mathf.Abs(posicion.y) - semiLargo) < 0.5f)
                {
                    // La viga final ocupa este punto, pero el tensor sigue haciendo falta: de el nace
                    // el cable de cierre.
                    if (generarTensor)
                    {
                        var cabeza = new Vector3(posicion.x, alturaViga, posicion.y);
                        CrearCaja(cabeza, cabeza + Vector3.up * alturaTensor,
                                  grosorTensor, profundidadTensor, tangente, "Tensor");
                    }
                    continue;
                }

                GenerarSoporte(posicion, haciaElCampo, tangente, geometria, alturaViga, yPiso);
                SoportesGenerados++;
            }

        }

        private void GenerarSoporte(Vector2 posicion, Vector2 haciaElCampo, Vector2 tangente,
                                    GeometriaSoporte geometria, float alturaViga, float yPiso)
        {
            // La cabeza del tensor esta sobre la linea de la viga longitudinal; las verticales
            // hacia el campo, a las distancias que publico la platea de este lado.
            float dExterior = geometria.distanciaVerticalExterior + corrimientoVerticales;
            float dInterior = geometria.distanciaVerticalInterior + corrimientoVerticales;

            Vector2 xzExterior = posicion + haciaElCampo * dExterior;
            Vector2 xzInterior = posicion + haciaElCampo * dInterior;

            Vector3 cabeza = new Vector3(posicion.x, alturaViga, posicion.y);

            CrearCaja(new Vector3(xzExterior.x, yPiso, xzExterior.y),
                      new Vector3(xzExterior.x, alturaViga, xzExterior.y),
                      anchoViga, altoViga, tangente, "Vertical_Exterior");

            // La diagonal continua la pendiente de la platea vecina, asi que su altura de
            // arranque no se elige: sale de restar el desnivel que produce esa pendiente a lo
            // largo del tramo que la separa de la cabeza. Se corta en la vertical interior
            // para no chocar contra el codo.
            float avance = Mathf.Abs(dInterior);
            float yArranque = alturaViga - geometria.pendienteDiagonal * avance;

            CrearCaja(new Vector3(xzInterior.x, yPiso, xzInterior.y),
                      new Vector3(xzInterior.x, Mathf.Max(yArranque, yPiso + 0.5f), xzInterior.y),
                      anchoViga, altoViga, tangente, "Vertical_Interior");

            CrearCaja(new Vector3(xzInterior.x, yArranque, xzInterior.y), cabeza,
                      anchoVigaDiagonal, altoVigaDiagonal, tangente, "Diagonal");

            if (generarTensor)
            {
                CrearCaja(cabeza, cabeza + Vector3.up * alturaTensor,
                          grosorTensor, profundidadTensor, tangente, "Tensor");
            }
        }

        // ------------------------------------------------------------------
        //  Datos derivados
        // ------------------------------------------------------------------

        /// <summary>
        /// Altura de la viga longitudinal en la zona del codo: la del ultimo anclaje
        /// publicado de ese lado. No baja acompañando la grada del codo.
        /// </summary>
        //private static float AlturaVigaLongitudinal(ControladorTecho c, bool ladoPositivo)
        //{
        //    IReadOnlyList<AnclajeTecho> anclajes = c.Registro.Anclajes;

        //    float mejorZ = float.NegativeInfinity;
        //    float altura = 0f;

        //    for (int i = 0; i < anclajes.Count; i++)
        //    {
        //        Vector3 p = anclajes[i].posicion;
        //        if ((p.x > 0f) != ladoPositivo) continue;

        //        float distancia = Mathf.Abs(p.z);
        //        if (distancia <= mejorZ) continue;

        //        mejorZ = distancia;
        //        altura = p.y;
        //    }

        //    return altura;
        //}


        private static float AlturaVigaLongitudinal(ControladorTecho c, bool ladoPositivoX,
                                            bool ladoPositivoZ)
        {
            IReadOnlyList<AnclajeTecho> anclajes = c.Registro.Anclajes;

            float mejorZ = float.NegativeInfinity;
            float altura = 0f;

            for (int i = 0; i < anclajes.Count; i++)
            {
                Vector3 p = anclajes[i].posicion;
                if ((p.x > 0f) != ladoPositivoX) continue;
                if ((p.z > 0f) != ladoPositivoZ) continue;

                // El anclaje mas alejado del centro de ESTE extremo: con rampa, los dos extremos
                // de una misma platea estan a alturas distintas.
                float distancia = Mathf.Abs(p.z);
                if (distancia <= mejorZ) continue;

                mejorZ = distancia;
                altura = p.y;
            }

            return altura;
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
