using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace Estadio.Techo
{
    /// <summary>
    /// Rejilla de una superficie lista para triangular. Fila 0 es el borde interior; la
    /// ultima fila, el borde inferior del faldon.
    /// </summary>
    public struct RejillaSuperficie
    {
        public Vector3[] vertices;
        public Vector2[] uv;
        public int filas;
        public int columnas;

        public int Indice(int fila, int columna) => fila * columnas + (columna % columnas);
        public Vector3 Vertice(int fila, int columna) => vertices[Indice(fila, columna)];
    }

    [Serializable]
    public struct ParametrosMembrana
    {
        [Header("Resolucion")]
        public int divisionesPerimetrales;   // columnas alrededor del anillo
        public int anillosPano;              // filas entre el borde interior y el perimetro
        public int anillosFaldon;            // filas de la banda vertical

        [Header("Forma")]
        [Tooltip("Caida de la tela entre cables adyacentes. Da el borde festoneado tipico " +
                 "de las membranas tensadas.")]
        public float festonRelativo;

        [Header("Faldon")]
        [Tooltip("Cuanto baja la tela por debajo del muro superior de la grada. Un solape " +
                 "asegura el cierre en vez de dejar una junta al ras.")]
        public float solapeFaldon;
        [Tooltip("Caida minima del faldon donde la tribuna casi llega al techo.")]
        public float caidaMinimaFaldon;

        [Header("Emparejamiento del pano")]
        [Tooltip("Cuartos de vuelta que se desplaza el borde interior respecto del perimetro " +
         "del techo. Ajustar hasta que los lados largos del vano queden emparejados con " +
         "las vigas longitudinales.")]
        [Range(0, 3)] public int desplazamientoArcoBorde;



        public static ParametrosMembrana PorDefecto => new ParametrosMembrana
        {
            divisionesPerimetrales = 192,
            anillosPano = 12,
            anillosFaldon = 2,
            festonRelativo = 0.022f,
            solapeFaldon = 1.5f,
            caidaMinimaFaldon = 0.5f
        };
    }

    /// <summary>
    /// La membrana es UNA sola superficie continua. Sube desde el borde del vano, cruza por
    /// encima de los anclajes —el anclaje es un punto por donde pasa, no el final— y baja
    /// verticalmente hasta superar el muro superior de la grada.
    ///
    /// Por eso el faldon no es un elemento aparte que cuelga del borde: es la continuacion
    /// de la misma tela. Y por eso existe en todo el perimetro: en los laterales la tribuna
    /// casi llega al techo y la caida es minima; en las cabeceras la tribuna esta mucho mas
    /// abajo y la caida es maxima. La panza del faldon no se modela, sale de esa resta.
    ///
    /// La altura de la tela sale de la familia transversal de cables. Los longitudinales no
    /// intervienen: viven sobre esta superficie, no la definen.
    /// </summary>
    public sealed class MembranaTecho
    {
        private ParametrosMembrana _parametros;

        private IPerimetroEstadio _perimetroEstadio;
        private PerimetroTecho _perimetroTecho;
        private RegistroCoronamientos _coronamientos;
        private BordeInteriorTecho _borde;

        private Cable[] _transversalesPorZ;

        private RejillaSuperficie _rejillaPano;
        private RejillaSuperficie _rejillaFaldon;

        private int _versionMembrana;
        private int _versionTendidoUsada = -1;
        private bool _construida;

        public ParametrosMembrana Parametros => _parametros;
        public int VersionMembrana => _versionMembrana;
        public bool Construida => _construida;

        public RejillaSuperficie RejillaPano { get { AsegurarConstruida(); return _rejillaPano; } }
        public RejillaSuperficie RejillaFaldon { get { AsegurarConstruida(); return _rejillaFaldon; } }

        public float SuperficieAproximada { get; private set; }
        public float CaidaFaldonMaxima { get; private set; }
        public float CaidaFaldonMinima { get; private set; }

        public MembranaTecho(ParametrosMembrana parametros)
        {
            Configurar(parametros);
        }

        public void Configurar(ParametrosMembrana parametros)
        {
            _parametros = parametros;
            _parametros.divisionesPerimetrales = Mathf.Max(16, parametros.divisionesPerimetrales);
            _parametros.anillosPano = Mathf.Max(2, parametros.anillosPano);
            _parametros.anillosFaldon = Mathf.Max(1, parametros.anillosFaldon);
            _construida = false;
            _versionMembrana++;
        }

        public bool NecesitaConstruir(TendidoCables tendido)
        {
            return !_construida || tendido.VersionTendido != _versionTendidoUsada;
        }

        // ------------------------------------------------------------------
        //  Construccion
        // ------------------------------------------------------------------

        public void Construir(IPerimetroEstadio perimetroEstadio, PerimetroTecho perimetroTecho,
                              RegistroCoronamientos coronamientos, BordeInteriorTecho borde,
                              TendidoCables tendido)
        {
            _perimetroEstadio = perimetroEstadio ?? throw new ArgumentNullException(nameof(perimetroEstadio));
            _perimetroTecho = perimetroTecho ?? throw new ArgumentNullException(nameof(perimetroTecho));
            _coronamientos = coronamientos ?? throw new ArgumentNullException(nameof(coronamientos));
            _borde = borde ?? throw new ArgumentNullException(nameof(borde));
            if (tendido == null) throw new ArgumentNullException(nameof(tendido));

            _transversalesPorZ = new List<Cable>(tendido.Transversales).ToArray();
            Array.Sort(_transversalesPorZ, (a, b) => a.coordenada.CompareTo(b.coordenada));

            ConstruirPano();
            ConstruirFaldon();

            _versionTendidoUsada = tendido.VersionTendido;
            _construida = true;
            _versionMembrana++;
        }

        /// <summary>
        /// Pano principal: del borde del vano al perimetro del techo. Como el perimetro del
        /// techo es un rectangulo —dos vigas rectas y dos cables de cierre— y el borde del
        /// vano tambien, el mapeo entre los dos es directo: misma fraccion de recorrido.
        /// </summary>
        private void ConstruirPano()
        {
            int columnas = _parametros.divisionesPerimetrales;
            int filas = _parametros.anillosPano + 1;
                
            var rejilla = new RejillaSuperficie
            {
                filas = filas,
                columnas = columnas,
                vertices = new Vector3[filas * columnas],
                uv = new Vector2[filas * columnas]
            };
                
            float area = 0f;

            for (int c = 0; c < columnas; c++)
            {
                float sigma = (float)c / columnas;
               
                Vector3 interior = PuntoBordePorCuartos(sigma);

                Vector3 exterior = PuntoPerimetroTecho(sigma);

                for (int f = 0; f < filas; f++)
                {
                    float w = (float)f / (filas - 1);

                    float x = Mathf.Lerp(interior.x, exterior.x, w);
                    float z = Mathf.Lerp(interior.z, exterior.z, w);

                    float y;
                    if (!TryAlturaTela(x, z, out y))
                        y = Mathf.Lerp(interior.y, exterior.y, w);

                    // Los bordes mandan: la tela esta fijada ahi.
                    if (f == 0) y = interior.y;
                    else if (f == filas - 1) y = exterior.y;

                    int i = rejilla.Indice(f, c);
                    rejilla.vertices[i] = new Vector3(x, y, z);
                    rejilla.uv[i] = new Vector2(sigma, w);
                }
            }

            for (int c = 0; c < columnas; c++)
                for (int f = 0; f < filas - 1; f++)
                    area += AreaCelda(rejilla, f, c);

            _rejillaPano = rejilla;
            SuperficieAproximada = area;
        }

        /// <summary>
        /// Faldon: la misma tela, ya pasado el perimetro del techo, bajando en vertical hasta
        /// superar el muro superior de la grada. La caida es la resta entre por donde pasa la
        /// tela y ese muro, mas el solape: minima en los laterales, maxima en el medio de las
        /// cabeceras. No hay que darle forma.
        /// </summary>
        private void ConstruirFaldon()
        {
            int columnas = _parametros.divisionesPerimetrales;
            int filas = _parametros.anillosFaldon + 1;

            var rejilla = new RejillaSuperficie
            {
                filas = filas,
                columnas = columnas,
                vertices = new Vector3[filas * columnas],
                uv = new Vector2[filas * columnas]
            };

            CaidaFaldonMaxima = 0f;
            CaidaFaldonMinima = float.PositiveInfinity;

            for (int c = 0; c < columnas; c++)
            {
                float sigma = (float)c / columnas;

                Vector3 arriba = PuntoPerimetroTecho(sigma);
                float muro = _coronamientos.AlturaBajoPunto(new Vector2(arriba.x, arriba.z));

                float caida = Mathf.Max(_parametros.caidaMinimaFaldon,
                                        arriba.y - muro + _parametros.solapeFaldon);

                CaidaFaldonMaxima = Mathf.Max(CaidaFaldonMaxima, caida);
                CaidaFaldonMinima = Mathf.Min(CaidaFaldonMinima, caida);

                for (int f = 0; f < filas; f++)
                {
                    float w = (float)f / (filas - 1);

                    int i = rejilla.Indice(f, c);
                    rejilla.vertices[i] = new Vector3(arriba.x, arriba.y - caida * w, arriba.z);
                    rejilla.uv[i] = new Vector2(sigma, w * caida);
                }
            }

            if (CaidaFaldonMinima > CaidaFaldonMaxima) CaidaFaldonMinima = 0f;

            _rejillaFaldon = rejilla;
        }

        // ------------------------------------------------------------------
        //  Geometria
        // ------------------------------------------------------------------

        /// <summary>
        /// Punto del perimetro del techo a la fraccion sigma del recorrido. Es el rectangulo
        /// que forman las dos vigas longitudinales y los dos cables de cierre: la tela pasa
        /// por encima de ahi y despues baja.
        /// </summary>
        private Vector3 PuntoPerimetroTecho(float sigma)
        {
            float semiLargo = _perimetroTecho.SemiLargo;

            // El recorrido se reparte en cuatro tramos, uno por lado, con la misma fraccion
            // que el borde interior para que el pano no se retuerza.
            float t = Mathf.Repeat(sigma, 1f) * 4f;
            int lado = Mathf.Min(3, Mathf.FloorToInt(t));
            float u = t - lado;

            switch (lado)
            {
                case 0: return PuntoEnViga(true, Mathf.Lerp(semiLargo, -semiLargo, u));
                case 1: return PuntoEnCierre(false, Mathf.Lerp(1f, 0f, u));
                case 2: return PuntoEnViga(false, Mathf.Lerp(-semiLargo, semiLargo, u));
                default: return PuntoEnCierre(true, Mathf.Lerp(0f, 1f, u));
            }
        }

        /// <summary>Punto sobre una viga longitudinal a la cota Z dada, tomando la altura de
        /// la tela ahi.</summary>
        private Vector3 PuntoEnViga(bool ladoPositivo, float z)
        {
            RectaViga recta = ladoPositivo
                ? _perimetroTecho.RectaXPositivo
                : _perimetroTecho.RectaXNegativo;

            float x = recta.XenZ(z);
            if (!TryAlturaTela(x, z, out float y)) y = 0f;
            return new Vector3(x, y, z);
        }

        /// <summary>Punto sobre un cable de cierre, a la fraccion u de su recorrido en X.</summary>
        private Vector3 PuntoEnCierre(bool ladoPositivo, float u)
        {
            float z = ladoPositivo ? _perimetroTecho.ZCierrePositivo : _perimetroTecho.ZCierreNegativo;

            float xNeg = _perimetroTecho.RectaXNegativo.XenZ(z);
            float xPos = _perimetroTecho.RectaXPositivo.XenZ(z);
            float x = Mathf.Lerp(xNeg, xPos, Mathf.Clamp01(u));

            if (!TryAlturaTela(x, z, out float y)) y = 0f;
            return new Vector3(x, y, z);
        }

        /// <summary>
        /// Altura de la tela en (x, z): se interpola entre los dos cables transversales que
        /// flanquean el punto y se resta el feston que hace la tela entre ambos.
        /// </summary>
        public bool TryAlturaTela(float x, float z, out float altura)
        {
            altura = 0f;
            if (_transversalesPorZ == null || _transversalesPorZ.Length < 2) return false;

            int siguiente = -1;
            for (int i = 0; i < _transversalesPorZ.Length; i++)
                if (_transversalesPorZ[i].coordenada >= z) { siguiente = i; break; }

            if (siguiente < 0) siguiente = _transversalesPorZ.Length - 1;
            if (siguiente == 0) siguiente = 1;
            int anterior = siguiente - 1;

            if (!_transversalesPorZ[anterior].TryPuntoEnEje(x, out Vector3 pa)) return false;
            if (!_transversalesPorZ[siguiente].TryPuntoEnEje(x, out Vector3 pb)) return false;

            float separacion = _transversalesPorZ[siguiente].coordenada
                             - _transversalesPorZ[anterior].coordenada;

            if (separacion < 1e-4f) { altura = pa.y; return true; }

            float u = Mathf.Clamp01((z - _transversalesPorZ[anterior].coordenada) / separacion);
            altura = Mathf.Lerp(pa.y, pb.y, u)
                   - 4f * _parametros.festonRelativo * separacion * u * (1f - u);

            return true;
        }

        private static float AreaCelda(RejillaSuperficie rejilla, int fila, int columna)
        {
            Vector3 a = rejilla.Vertice(fila, columna);
            Vector3 b = rejilla.Vertice(fila, columna + 1);
            Vector3 c = rejilla.Vertice(fila + 1, columna + 1);
            Vector3 d = rejilla.Vertice(fila + 1, columna);

            return 0.5f * (Vector3.Cross(b - a, d - a).magnitude + Vector3.Cross(b - c, d - c).magnitude);
        }

        private void AsegurarConstruida()
        {
            if (!_construida)
                throw new InvalidOperationException("La membrana no esta construida.");
        }

        // ------------------------------------------------------------------
        //  Validacion y diagnostico
        // ------------------------------------------------------------------

        public bool Validar(List<string> mensajes)
        {
            if (mensajes == null) throw new ArgumentNullException(nameof(mensajes));

            if (!_construida)
            {
                mensajes.Add("ERROR: la membrana no esta construida.");
                return false;
            }

            bool valido = true;
            int porDebajo = 0;
            float peor = 0f;

            // Ningun punto del pano puede quedar por debajo del muro que tiene abajo: seria
            // tela atravesando la ultima fila de la grada.
            for (int c = 0; c < _rejillaPano.columnas; c += 4)
            {
                for (int f = 1; f < _rejillaPano.filas - 1; f++)
                {
                    Vector3 p = _rejillaPano.Vertice(f, c);
                    float muro = _coronamientos.AlturaBajoPunto(new Vector2(p.x, p.z));

                    if (p.y >= muro) continue;

                    porDebajo++;
                    peor = Mathf.Max(peor, muro - p.y);
                }
            }

            if (porDebajo > 0)
            {
                mensajes.Add($"ERROR: {porDebajo} puntos del pano quedan por debajo del muro de la " +
                             $"grada (peor caso {peor:F2} m). Reducir festonRelativo o aumentar la " +
                             "tension de los cables transversales.");
                valido = false;
            }

            return valido;
        }

        public string Diagnostico()
        {
            var sb = new StringBuilder();
            sb.AppendLine($"Membrana (version {_versionMembrana}, construida: {_construida})");

            if (!_construida) return sb.ToString();

            sb.AppendLine($"Pano: {_rejillaPano.filas} anillos x {_rejillaPano.columnas} divisiones " +
                          $"({_rejillaPano.vertices.Length} vertices)");
            sb.AppendLine($"Superficie del pano: {SuperficieAproximada:F0} m2");
            sb.AppendLine($"Faldon: caida de {CaidaFaldonMinima:F2} a {CaidaFaldonMaxima:F2} m " +
                          $"(solape {_parametros.solapeFaldon:F2} m)");

            sb.AppendLine("Emparejamiento del pano (columna: interior -> exterior):");
            int paso = Mathf.Max(1, _rejillaPano.columnas / 16);
            for (int c = 0; c < _rejillaPano.columnas; c += paso)
            {
                Vector3 interior = _rejillaPano.Vertice(0, c);
                Vector3 exterior = _rejillaPano.Vertice(_rejillaPano.filas - 1, c);
                sb.AppendLine($"  col {c,4}: ({interior.x,7:F1}, {interior.z,7:F1}) -> " +
                              $"({exterior.x,7:F1}, {exterior.z,7:F1})");
            }


            return sb.ToString();
        }

        //private Vector3 PuntoBordePorCuartos(float sigma)
        //{
        //    // Se invierte el recorrido completo y se desplaza la fase, en vez de invertir dentro
        //    // de cada arco: hacerlo por arco rompe el empalme entre uno y el siguiente, y produce
        //    // un salto al lado opuesto del vano en cada cambio de arco.
        //    float sigmaBorde = Mathf.Repeat(_parametros.desplazamientoArcoBorde * 0.25f - sigma, 1f);

        //    float t = sigmaBorde * 4f;
        //    int arco = Mathf.Min(3, Mathf.FloorToInt(t));
        //    float u = Mathf.Repeat(t, 1f);

        //    float tInicio = (0.25f + 0.5f * arco) * Mathf.PI;
        //    return _borde.PuntoEnT(tInicio + u * 0.5f * Mathf.PI);
        //}

        private Vector3 PuntoBordePorCuartos(float sigma)
        {
            float sigmaBorde = Mathf.Repeat(_parametros.desplazamientoArcoBorde * 0.25f - sigma, 1f);

            float t = sigmaBorde * 4f;
            int arco = Mathf.Min(3, Mathf.FloorToInt(t));
            float u = Mathf.Repeat(t, 1f);

            // Reparto por longitud de arco y no por parametro: con exponente 16 el borde recorre
            // casi todo su largo en la parte recta y gira la esquina en un tramo minimo. Repartir
            // por parametro concentra columnas en las esquinas y las abre en abanico.
            Vector3[] muestras = _borde.MuestrearArco(arco, 64);

            float total = 0f;
            var acumulado = new float[muestras.Length];
            for (int i = 1; i < muestras.Length; i++)
            {
                total += Vector2.Distance(new Vector2(muestras[i - 1].x, muestras[i - 1].z),
                                          new Vector2(muestras[i].x, muestras[i].z));
                acumulado[i] = total;
            }

            float objetivo = u * total;
            for (int i = 1; i < muestras.Length; i++)
            {
                if (acumulado[i] < objetivo) continue;
                float f = (objetivo - acumulado[i - 1]) / Mathf.Max(1e-4f, acumulado[i] - acumulado[i - 1]);
                return Vector3.Lerp(muestras[i - 1], muestras[i], f);
            }

            return muestras[muestras.Length - 1];
        }

    }
}
