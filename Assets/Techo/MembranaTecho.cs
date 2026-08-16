using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace Estadio.Techo
{
    /// <summary>
    /// Rejilla de una superficie lista para triangular. Fila 0 es el borde interior
    /// (o el borde superior, en el faldon); la ultima fila es el borde exterior
    /// (o el inferior). Las columnas recorren el perimetro y cierran sobre si mismas.
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
        public int anillos;                  // filas entre borde interior y perimetro

        [Header("Forma")]
        public float festonRelativo;         // caida de la tela entre cables adyacentes

        [Header("Borde exterior")]
        [Tooltip("0 = el borde sigue exactamente el coronamiento de cada tribuna (sin faldon). " +
                 "1 = el borde corre liso a cota fija y el faldon cierra toda la diferencia.")]
        public float suavizadoBordeExterior;
        public float alturaBordeLiso;        // cota del borde cuando suavizado = 1

        [Header("Faldon")]
        public bool generarFaldon;
        public float alturaMinimaFaldon;     // por debajo de esto no se genera banda

        public static ParametrosMembrana PorDefecto => new ParametrosMembrana
        {
            divisionesPerimetrales = 192,
            anillos = 12,
            festonRelativo = 0.022f,
            suavizadoBordeExterior = 0.65f,
            alturaBordeLiso = 34.0f,
            generarFaldon = true,
            alturaMinimaFaldon = 0.5f
        };
    }

    /// <summary>
    /// La superficie de membrana y su faldon vertical.
    ///
    /// La altura sale de la familia transversal de cables: para un punto (x,z) se buscan
    /// los dos cables transversales que lo flanquean, se evalua cada uno a esa cota Z y se
    /// interpola, restando el feston que hace la tela entre ambos. Los cables longitudinales
    /// no intervienen: viven sobre esta superficie, no la definen.
    ///
    /// El faldon es la resta entre el borde exterior de la membrana y el coronamiento de la
    /// tribuna, exactamente como lo dedujimos: maximo en el centro de las cabeceras, nulo
    /// donde la tribuna llega a la cota del borde.
    /// </summary>
    public sealed class MembranaTecho
    {
        private ParametrosMembrana _parametros;

        private IPerimetroEstadio _perimetro;
        private RegistroAnclajesTecho _registro;
        private BordeInteriorTecho _borde;

        private Cable[] _transversalesPorZ;   // ordenados por coordenada creciente

        private RejillaSuperficie _rejillaMembrana;
        private RejillaSuperficie _rejillaFaldon;
        private bool _hayFaldon;

        private int _versionMembrana;
        private int _versionTendidoUsada = -1;
        private bool _construida;

        public ParametrosMembrana Parametros => _parametros;
        public int VersionMembrana => _versionMembrana;
        public bool Construida => _construida;
        public bool HayFaldon => _hayFaldon;

        public RejillaSuperficie RejillaMembrana { get { AsegurarConstruida(); return _rejillaMembrana; } }
        public RejillaSuperficie RejillaFaldon { get { AsegurarConstruida(); return _rejillaFaldon; } }

        public float AlturaFaldonMaxima { get; private set; }
        public float SuperficieAproximada { get; private set; }

        public MembranaTecho(ParametrosMembrana parametros)
        {
            Configurar(parametros);
        }

        public void Configurar(ParametrosMembrana parametros)
        {
            _parametros = parametros;
            _parametros.divisionesPerimetrales = Mathf.Max(16, parametros.divisionesPerimetrales);
            _parametros.anillos = Mathf.Max(2, parametros.anillos);
            _parametros.suavizadoBordeExterior = Mathf.Clamp01(parametros.suavizadoBordeExterior);
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

        public void Construir(IPerimetroEstadio perimetro, RegistroAnclajesTecho registro,
                              BordeInteriorTecho borde, TendidoCables tendido)
        {
            _perimetro = perimetro ?? throw new ArgumentNullException(nameof(perimetro));
            _registro = registro ?? throw new ArgumentNullException(nameof(registro));
            _borde = borde ?? throw new ArgumentNullException(nameof(borde));
            if (tendido == null) throw new ArgumentNullException(nameof(tendido));

            _transversalesPorZ = new List<Cable>(tendido.Transversales).ToArray();
            Array.Sort(_transversalesPorZ, (a, b) => a.coordenada.CompareTo(b.coordenada));

            ConstruirRejillaMembrana();
            ConstruirRejillaFaldon();

            _versionTendidoUsada = tendido.VersionTendido;
            _construida = true;
            _versionMembrana++;
        }

        private void ConstruirRejillaMembrana()
        {
            int columnas = _parametros.divisionesPerimetrales;
            int filas = _parametros.anillos + 1;

            var rejilla = new RejillaSuperficie
            {
                filas = filas,
                columnas = columnas,
                vertices = new Vector3[filas * columnas],
                uv = new Vector2[filas * columnas]
            };

            float longitudBorde = _borde.LongitudTotal;
            float longitudPerimetro = _perimetro.LongitudTotal;
            float area = 0f;

            for (int c = 0; c < columnas; c++)
            {
                float sigma = (float)c / columnas;

                Vector3 interior = _borde.PuntoEnS(sigma * longitudBorde);
                Vector3 exterior = PuntoBordeExterior(sigma * longitudPerimetro);

                for (int f = 0; f < filas; f++)
                {
                    float w = (float)f / (filas - 1);

                    // Recorrido radial en planta, del borde del vano hacia el perimetro.
                    float x = Mathf.Lerp(interior.x, exterior.x, w);
                    float z = Mathf.Lerp(interior.z, exterior.z, w);

                    float y;
                    if (!TryAlturaSuperficie(x, z, out y))
                        y = Mathf.Lerp(interior.y, exterior.y, w);

                    // Los bordes mandan: la membrana esta fijada ahi.
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

            _rejillaMembrana = rejilla;
            SuperficieAproximada = area;
        }

        private static float AreaCelda(RejillaSuperficie rejilla, int fila, int columna)
        {
            Vector3 a = rejilla.Vertice(fila, columna);
            Vector3 b = rejilla.Vertice(fila, columna + 1);
            Vector3 c = rejilla.Vertice(fila + 1, columna + 1);
            Vector3 d = rejilla.Vertice(fila + 1, columna);

            return 0.5f * (Vector3.Cross(b - a, d - a).magnitude + Vector3.Cross(b - c, d - c).magnitude);
        }

        /// <summary>
        /// Banda vertical que cuelga del borde exterior hasta el coronamiento de la tribuna.
        /// Su altura es la resta entre las dos curvas: maxima donde la cabecera es mas baja,
        /// nula donde la tribuna alcanza el borde. No hay que disenarla.
        /// </summary>
        private void ConstruirRejillaFaldon()
        {
            _hayFaldon = false;
            AlturaFaldonMaxima = 0f;

            if (!_parametros.generarFaldon)
            {
                _rejillaFaldon = default;
                return;
            }

            int columnas = _parametros.divisionesPerimetrales;
            float longitudPerimetro = _perimetro.LongitudTotal;

            var rejilla = new RejillaSuperficie
            {
                filas = 2,
                columnas = columnas,
                vertices = new Vector3[2 * columnas],
                uv = new Vector2[2 * columnas]
            };

            for (int c = 0; c < columnas; c++)
            {
                float sigma = (float)c / columnas;
                float s = sigma * longitudPerimetro;

                Vector3 arriba = PuntoBordeExterior(s);
                float coronamiento = _registro.AlturaCoronamiento(s);
                float caida = Mathf.Max(0f, arriba.y - coronamiento);

                if (caida > AlturaFaldonMaxima) AlturaFaldonMaxima = caida;
                if (caida >= _parametros.alturaMinimaFaldon) _hayFaldon = true;

                Vector3 abajo = new Vector3(arriba.x, arriba.y - caida, arriba.z);

                rejilla.vertices[rejilla.Indice(0, c)] = arriba;
                rejilla.vertices[rejilla.Indice(1, c)] = abajo;
                rejilla.uv[rejilla.Indice(0, c)] = new Vector2(sigma, 0f);
                rejilla.uv[rejilla.Indice(1, c)] = new Vector2(sigma, caida);
            }

            _rejillaFaldon = rejilla;
        }

        // ------------------------------------------------------------------
        //  Evaluacion de la superficie
        // ------------------------------------------------------------------

        /// <summary>
        /// Altura de la membrana en (x, z), interpolando entre los dos cables transversales
        /// que flanquean el punto y restando el feston de la tela entre ambos.
        /// </summary>
        public bool TryAlturaSuperficie(float x, float z, out float altura)
        {
            altura = 0f;
            if (_transversalesPorZ == null || _transversalesPorZ.Length < 2) return false;

            // Los cables transversales estan a z constante y varian en X: se busca el par
            // que flanquea el punto en Z y se evalua cada uno a la cota X pedida.
            int siguiente = -1;
            for (int i = 0; i < _transversalesPorZ.Length; i++)
            {
                if (_transversalesPorZ[i].coordenada >= z) { siguiente = i; break; }
            }

            if (siguiente <= 0) return false;
            int anterior = siguiente - 1;

            Cable cableAnterior = _transversalesPorZ[anterior];
            Cable cableSiguiente = _transversalesPorZ[siguiente];

            if (!cableAnterior.TryPuntoEnEje(x, out Vector3 puntoAnterior)) return false;
            if (!cableSiguiente.TryPuntoEnEje(x, out Vector3 puntoSiguiente)) return false;

            float separacion = cableSiguiente.coordenada - cableAnterior.coordenada;
            if (separacion < 1e-4f) { altura = puntoAnterior.y; return true; }

            float u = Mathf.Clamp01((z - cableAnterior.coordenada) / separacion);
            altura = Mathf.Lerp(puntoAnterior.y, puntoSiguiente.y, u)
                   - 4f * _parametros.festonRelativo * separacion * u * (1f - u);

            return true;
        }

        /// <summary>
        /// Borde exterior de la membrana. Entre seguir el coronamiento de cada tribuna
        /// (suavizado = 0, sin faldon) y correr liso a cota fija (suavizado = 1, faldon
        /// maximo). Es el parametro que decide cuanto faldon hay.
        /// </summary>
        public Vector3 PuntoBordeExterior(float s)
        {
            float t = _perimetro.TDeLongitud(s);
            Vector2 xz = _perimetro.Punto(t);

            float coronamiento = _registro.AlturaCoronamiento(s);
            float altura = Mathf.Lerp(coronamiento, _parametros.alturaBordeLiso,
                                      _parametros.suavizadoBordeExterior);

            return new Vector3(xz.x, Mathf.Max(altura, coronamiento), xz.y);
        }

        private void AsegurarConstruida()
        {
            if (!_construida)
                throw new InvalidOperationException(
                    "La membrana no esta construida. Llamar a Construir(...).");
        }

        // ------------------------------------------------------------------
        //  Validacion y diagnostico
        // ------------------------------------------------------------------

        public bool Validar(List<string> mensajes, float holguraMinimaSobreCoronamiento = 0f)
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

            // Ningun punto de la membrana puede quedar por debajo del coronamiento de la
            // tribuna que tiene abajo: seria tela atravesando la ultima fila.
            for (int c = 0; c < _rejillaMembrana.columnas; c += 4)
            {
                for (int f = 1; f < _rejillaMembrana.filas - 1; f++)
                {
                    Vector3 p = _rejillaMembrana.Vertice(f, c);
                    float s = _perimetro.SDePunto(new Vector2(p.x, p.z));
                    float coronamiento = _registro.AlturaCoronamiento(s);
                    float holgura = p.y - coronamiento;

                    if (holgura >= holguraMinimaSobreCoronamiento) continue;

                    porDebajo++;
                    peor = Mathf.Max(peor, -holgura);
                }
            }

            if (porDebajo > 0)
            {
                mensajes.Add($"ERROR: {porDebajo} puntos de la membrana quedan por debajo del " +
                             $"coronamiento (peor caso {peor:F2} m). Reducir festonRelativo o la " +
                             "panza de los cables transversales.");
                valido = false;
            }

            if (_parametros.generarFaldon && !_hayFaldon)
            {
                mensajes.Add("AVISO: el faldon quedo vacio. Con suavizadoBordeExterior en " +
                             $"{_parametros.suavizadoBordeExterior:F2} el borde de la membrana " +
                             "sigue al coronamiento y no queda hueco que cerrar.");
            }

            return valido;
        }

        public string Diagnostico()
        {
            var sb = new StringBuilder();
            sb.AppendLine($"Membrana (version {_versionMembrana}, construida: {_construida})");

            if (!_construida) return sb.ToString();

            sb.AppendLine($"Rejilla: {_rejillaMembrana.filas} anillos x " +
                          $"{_rejillaMembrana.columnas} divisiones " +
                          $"({_rejillaMembrana.vertices.Length} vertices)");
            sb.AppendLine($"Superficie aproximada: {SuperficieAproximada:F0} m2");
            sb.AppendLine($"Faldon: {(_hayFaldon ? "si" : "no")}, altura maxima " +
                          $"{AlturaFaldonMaxima:F2} m");
            sb.AppendLine($"Suavizado del borde exterior: {_parametros.suavizadoBordeExterior:F2} " +
                          $"(cota lisa {_parametros.alturaBordeLiso:F1} m)");

            return sb.ToString();
        }
    }
}
