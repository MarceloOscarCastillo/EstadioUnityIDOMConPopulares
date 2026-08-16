using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace Estadio.Techo
{
    [Serializable]
    public struct ParametrosBordeInterior
    {
        [Header("Campo de juego")]
        public float semiLongitudCampo;   // 52.5 para un campo de 105 m
        public float semiAnchoCampo;      // 34.0 para 68 m

        [Header("Vano libre")]
        public float margenVanoX;
        public float margenVanoZ;
        public float exponenteVano;       // redondeo de las esquinas del vano; 2 = elipse

        [Header("Cotas")]
        public float alturaEsquinas;      // cota de los cuatro puntos altos
        public float flechaRelativaLadoLargo;   // panza sobre las plateas laterales
        public float flechaRelativaLadoCorto;   // panza sobre las cabeceras

        // Convencion de ejes: Z es el eje LARGO del campo (de arco a arco),
        // X es el ANCHO (de platea lateral a platea lateral).
        public float SemiVanoX => semiAnchoCampo + margenVanoX;
        public float SemiVanoZ => semiLongitudCampo + margenVanoZ;

        public static ParametrosBordeInterior PorDefecto => new ParametrosBordeInterior
        {
            semiLongitudCampo = 52.5f,
            semiAnchoCampo = 34.0f,
            margenVanoX = 6.0f,
            margenVanoZ = 5.0f,
            exponenteVano = 2.6f,
            alturaEsquinas = 44.0f,
            flechaRelativaLadoLargo = 0.045f,
            flechaRelativaLadoCorto = 0.035f
        };
    }

    /// <summary>
    /// Borde interior del techo: la curva cerrada que rodea el vano libre sobre el campo.
    ///
    /// Es la pieza que comparten los dos proyectos. Los dos tienen el mismo gesto —sube en
    /// las cuatro esquinas, baja en el medio de cada lado— y cambian solo en como lo
    /// materializan: el Diseno 1 con una viga tubular colgada de los cables, el Diseno 2
    /// con el cordon superior de una celosia rigida.
    ///
    /// En planta es otra superelipse, con su propio exponente: el vano tiene esquinas
    /// redondeadas, mas suaves que las del perimetro exterior.
    ///
    /// En altura son cuatro tramos que cuelgan entre las cuatro esquinas, con la misma
    /// parabola que usamos para los cables y para la panza de los puentes.
    /// </summary>
    public sealed class BordeInteriorTecho
    {
        private ParametrosBordeInterior _parametros;
        private PerimetroSuperelipse _planta;

        // Esquinas en el parametro trigonometrico: puntos altos de la curva.
        private static readonly float[] TEsquinas =
        {
            0.25f * Mathf.PI,
            0.75f * Mathf.PI,
            1.25f * Mathf.PI,
            1.75f * Mathf.PI
        };

        private readonly float[] _sEsquinas = new float[4];
        private readonly float[] _luzArco = new float[4];
        private readonly float[] _flechaArco = new float[4];
        private readonly Vector3[] _esquinas = new Vector3[4];

        private int _versionBorde;
        private bool _construido;

        public ParametrosBordeInterior Parametros => _parametros;
        public int VersionBorde => _versionBorde;
        public bool Construido => _construido;

        /// <summary>Proyeccion en planta del borde. Expone las mismas intersecciones
        /// cerradas que el perimetro exterior.</summary>
        public IPerimetroEstadio Planta { get { AsegurarConstruido(); return _planta; } }

        public float LongitudTotal { get { AsegurarConstruido(); return _planta.LongitudTotal; } }

        /// <summary>Las cuatro esquinas altas, en orden antihorario.</summary>
        public IReadOnlyList<Vector3> Esquinas { get { AsegurarConstruido(); return _esquinas; } }

        public float AlturaMaxima => _parametros.alturaEsquinas;

        public float AlturaMinima
        {
            get
            {
                AsegurarConstruido();
                float minima = _parametros.alturaEsquinas;
                for (int i = 0; i < 4; i++)
                    minima = Mathf.Min(minima, _parametros.alturaEsquinas - _flechaArco[i]);
                return minima;
            }
        }

        public BordeInteriorTecho(ParametrosBordeInterior parametros)
        {
            Configurar(parametros);
        }

        public void Configurar(ParametrosBordeInterior parametros)
        {
            if (parametros.exponenteVano < PerimetroSuperelipse.ExponenteMinimo)
                throw new ArgumentOutOfRangeException(nameof(parametros),
                    $"exponenteVano debe ser >= {PerimetroSuperelipse.ExponenteMinimo}.");

            _parametros = parametros;
            _construido = false;
            _versionBorde++;
        }

        // ------------------------------------------------------------------
        //  Construccion
        // ------------------------------------------------------------------

        public void Construir()
        {
            _planta = new PerimetroSuperelipse(_parametros.SemiVanoX,
                                               _parametros.SemiVanoZ,
                                               _parametros.exponenteVano);

            float longitud = _planta.LongitudTotal;

            for (int i = 0; i < 4; i++)
                _sEsquinas[i] = _planta.LongitudDeT(TEsquinas[i]);

            for (int i = 0; i < 4; i++)
            {
                _luzArco[i] = i < 3
                    ? _sEsquinas[i + 1] - _sEsquinas[i]
                    : longitud - _sEsquinas[3] + _sEsquinas[0];

                // Con Z como eje largo, los arcos 1 y 3 contienen el medio de los lados
                // largos (sobre las plateas laterales, en x = +-a); los arcos 0 y 2, el
                // medio de las cabeceras (en z = +-b).
                float coeficiente = (i % 2 == 1)
                    ? _parametros.flechaRelativaLadoLargo
                    : _parametros.flechaRelativaLadoCorto;

                _flechaArco[i] = coeficiente * _luzArco[i];
            }

            for (int i = 0; i < 4; i++)
            {
                Vector2 xz = _planta.Punto(TEsquinas[i]);
                _esquinas[i] = new Vector3(xz.x, _parametros.alturaEsquinas, xz.y);
            }

            _construido = true;
            _versionBorde++;
        }

        private void AsegurarConstruido()
        {
            if (!_construido)
                throw new InvalidOperationException(
                    "El borde interior no esta construido. Llamar a Construir().");
        }

        // ------------------------------------------------------------------
        //  Altura
        // ------------------------------------------------------------------

        /// <summary>
        /// Altura del borde a la longitud de arco s. Cada uno de los cuatro tramos cuelga
        /// entre dos esquinas con la parabola de siempre; los lados largos cuelgan mas
        /// porque su luz es mayor, sin que haya que decirselo.
        /// </summary>
        public float AlturaEnS(float s)
        {
            AsegurarConstruido();

            float longitud = _planta.LongitudTotal;
            s = Mathf.Repeat(s, longitud);

            int arco = LocalizarArco(s, longitud, out float u);
            float flecha = _flechaArco[arco];

            return _parametros.alturaEsquinas - 4f * flecha * u * (1f - u);
        }

        public float AlturaEnT(float t)
        {
            AsegurarConstruido();
            return AlturaEnS(_planta.LongitudDeT(t));
        }

        private int LocalizarArco(float s, float longitud, out float u)
        {
            // El arco 3 envuelve el origen del parametro.
            if (s < _sEsquinas[0] || s >= _sEsquinas[3])
            {
                float recorrido = s >= _sEsquinas[3]
                    ? s - _sEsquinas[3]
                    : longitud - _sEsquinas[3] + s;
                u = Mathf.Clamp01(recorrido / _luzArco[3]);
                return 3;
            }

            for (int i = 0; i < 3; i++)
            {
                if (s < _sEsquinas[i + 1])
                {
                    u = Mathf.Clamp01((s - _sEsquinas[i]) / _luzArco[i]);
                    return i;
                }
            }

            u = 0f;
            return 0;
        }

        // ------------------------------------------------------------------
        //  Puntos y cruces
        // ------------------------------------------------------------------

        public Vector3 PuntoEnT(float t)
        {
            AsegurarConstruido();
            Vector2 xz = _planta.Punto(t);
            return new Vector3(xz.x, AlturaEnT(t), xz.y);
        }

        public Vector3 PuntoEnS(float s)
        {
            AsegurarConstruido();
            Vector2 xz = _planta.PuntoPorLongitud(s);
            return new Vector3(xz.x, AlturaEnS(s), xz.y);
        }

        /// <summary>
        /// Cruce del borde con un cable transversal en x0. Estos son los apoyos
        /// intermedios de la familia transversal: el punto donde el cable toma la viga
        /// borde (Diseno 1) o la celosia perimetral (Diseno 2).
        /// </summary>
        public bool IntersectarX(float x0, out Vector3 puntoZNegativo, out Vector3 puntoZPositivo)
        {
            AsegurarConstruido();
            puntoZNegativo = default;
            puntoZPositivo = default;

            if (!_planta.IntersectarX(x0, out float zPositivo, out float zNegativo)) return false;

            puntoZPositivo = PuntoDesdeXZ(new Vector2(x0, zPositivo));
            puntoZNegativo = PuntoDesdeXZ(new Vector2(x0, zNegativo));
            return true;
        }

        public bool IntersectarZ(float z0, out Vector3 puntoXNegativo, out Vector3 puntoXPositivo)
        {
            AsegurarConstruido();
            puntoXNegativo = default;
            puntoXPositivo = default;

            if (!_planta.IntersectarZ(z0, out float xPositivo, out float xNegativo)) return false;

            puntoXPositivo = PuntoDesdeXZ(new Vector2(xPositivo, z0));
            puntoXNegativo = PuntoDesdeXZ(new Vector2(xNegativo, z0));
            return true;
        }

        private Vector3 PuntoDesdeXZ(Vector2 xz)
        {
            return new Vector3(xz.x, AlturaEnS(_planta.SDePunto(xz)), xz.y);
        }

        /// <summary>
        /// Muestreo equiespaciado en metros del borde completo. La cantidad se ajusta al
        /// multiplo de 4 mas cercano para que caigan puntos exactos sobre los cuatro ejes.
        /// </summary>
        public Vector3[] MuestrearPorSeparacion(float separacionObjetivo, out float separacionReal)
        {
            AsegurarConstruido();

            Vector2[] planta = _planta.MuestrearPorSeparacion(separacionObjetivo, out separacionReal);
            var puntos = new Vector3[planta.Length];

            for (int i = 0; i < planta.Length; i++)
                puntos[i] = new Vector3(planta[i].x, AlturaEnS(i * separacionReal), planta[i].y);

            return puntos;
        }

        // ------------------------------------------------------------------
        //  Validacion
        // ------------------------------------------------------------------

        /// <summary>
        /// El borde no puede bajar por debajo del coronamiento de las tribunas: si lo hace,
        /// la viga borde o la celosia atravesarian la ultima fila de la platea.
        /// </summary>
        public bool Validar(IPerimetroEstadio perimetroExterior, RegistroAnclajesTecho registro,
                            List<string> mensajes, float holguraMinima = 3f)
        {
            if (mensajes == null) throw new ArgumentNullException(nameof(mensajes));

            if (!_construido)
            {
                mensajes.Add("ERROR: el borde interior no esta construido.");
                return false;
            }

            bool valido = true;

            if (_parametros.SemiVanoX >= perimetroExterior.SemiejeX ||
                _parametros.SemiVanoZ >= perimetroExterior.SemiejeZ)
            {
                mensajes.Add("ERROR: el vano excede las dimensiones del estadio.");
                return false;
            }

            // El punto mas bajo del borde esta en el medio de cada lado. Se compara contra
            // el coronamiento en la misma direccion radial.
            const int muestras = 4;
            float[] tMedios = { 0f, 0.5f * Mathf.PI, Mathf.PI, 1.5f * Mathf.PI };
            string[] nombres = { "lateral X+", "cabecera Z+", "lateral X-", "cabecera Z-" };

            for (int i = 0; i < muestras; i++)
            {
                Vector3 puntoBorde = PuntoEnT(tMedios[i]);
                Vector2 direccion = new Vector2(puntoBorde.x, puntoBorde.z);
                if (direccion.sqrMagnitude < 1e-6f) continue;

                float sExterior = perimetroExterior.SDePunto(direccion);
                float coronamiento = registro.AlturaCoronamiento(sExterior);
                float holgura = puntoBorde.y - coronamiento;

                if (holgura < 0f)
                {
                    mensajes.Add($"ERROR: en el medio de la {nombres[i]} el borde interior queda " +
                                 $"{-holgura:F1} m por debajo del coronamiento de la tribuna. " +
                                 "Subir alturaEsquinas o reducir la flecha.");
                    valido = false;
                }
                else if (holgura < holguraMinima)
                {
                    mensajes.Add($"AVISO: solo {holgura:F1} m entre el borde interior y el " +
                                 $"coronamiento en la {nombres[i]}.");
                }
            }

            return valido;
        }

        public string Diagnostico()
        {
            var sb = new StringBuilder();
            sb.AppendLine($"Borde interior (version {_versionBorde}, construido: {_construido})");

            if (!_construido) return sb.ToString();

            sb.AppendLine($"Vano: {2f * _parametros.SemiVanoX:F1} x {2f * _parametros.SemiVanoZ:F1} m, " +
                          $"exponente {_parametros.exponenteVano:F2}");
            sb.AppendLine($"Perimetro del borde: {LongitudTotal:F1} m");
            sb.AppendLine($"Cota de esquinas: {_parametros.alturaEsquinas:F2} m");
            sb.AppendLine($"Lados largos (plateas): luz {_luzArco[1]:F1} m, panza {_flechaArco[1]:F2} m " +
                          $"-> cota minima {_parametros.alturaEsquinas - _flechaArco[1]:F2} m");
            sb.AppendLine($"Cabeceras:              luz {_luzArco[0]:F1} m, panza {_flechaArco[0]:F2} m " +
                          $"-> cota minima {_parametros.alturaEsquinas - _flechaArco[0]:F2} m");
            sb.AppendLine($"Desnivel total del borde: {AlturaMaxima - AlturaMinima:F2} m");

            return sb.ToString();
        }
    }
}
