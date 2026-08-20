using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace Estadio.Techo
{
    /// <summary>
    /// Superficie definida por la familia transversal de cables. El borde interior la
    /// consulta para saber a que altura queda: no la elige, la lee.
    /// </summary>
    public interface ISuperficieCables
    {
        bool TryAltura(float x, float z, out float altura);
    }

    [Serializable]
    public struct ParametrosBordeInterior
    {
        [Header("Campo de juego")]
        public float semiLongitudCampo;   // 52.5 para un campo de 105 m
        public float semiAnchoCampo;      // 34.0 para 68 m

        [Header("Vano libre")]
        public float margenVanoX;
        public float margenVanoZ;
        [Tooltip("Redondeo de las esquinas del vano. 2 = elipse, 16 = rectangulo de " +
                 "esquinas apenas matadas.")]
        public float exponenteVano;

        [Header("Muestreo")]
        public int muestrasAltura;

        // Z es el eje LARGO del campo, X el ANCHO.
        public float SemiVanoX => semiAnchoCampo + margenVanoX;
        public float SemiVanoZ => semiLongitudCampo + margenVanoZ;

        public static ParametrosBordeInterior PorDefecto => new ParametrosBordeInterior
        {
            semiLongitudCampo = 52.5f,
            semiAnchoCampo = 34.0f,
            margenVanoX = 6.0f,
            margenVanoZ = 5.0f,
            exponenteVano = 16f,
            muestrasAltura = 240
        };
    }

    /// <summary>
    /// Borde interior del techo: la curva cerrada que rodea el vano sobre el campo.
    ///
    /// Su altura NO es un parametro. Las estructuras tubulares del borde cuelgan de los
    /// cables transversales, asi que su cota es simplemente la del cable en el punto
    /// donde lo cruza. Si una platea es mas alta que la otra, la cuerda del cable ya
    /// viene inclinada y el borde hereda esa inclinacion sin que nadie se lo diga.
    ///
    /// En planta sigue siendo una superelipse propia, con su exponente: el vano es mas
    /// rectangular que el perimetro exterior.
    /// </summary>
    public sealed class BordeInteriorTecho
    {
        private ParametrosBordeInterior _parametros;
        private PerimetroSuperelipse _planta;

        private static readonly float[] TEsquinas =
        {
            0.25f * Mathf.PI, 0.75f * Mathf.PI, 1.25f * Mathf.PI, 1.75f * Mathf.PI
        };

        private float[] _alturas;      // muestreo uniforme en longitud de arco
        private float _pasoMuestra;
        private readonly Vector3[] _esquinas = new Vector3[4];

        private int _versionBorde;
        private bool _construido;

        public ParametrosBordeInterior Parametros => _parametros;
        public int VersionBorde => _versionBorde;
        public bool Construido => _construido;

        public IPerimetroEstadio Planta { get { AsegurarConstruido(); return _planta; } }
        public float LongitudTotal { get { AsegurarConstruido(); return _planta.LongitudTotal; } }
        public IReadOnlyList<Vector3> Esquinas { get { AsegurarConstruido(); return _esquinas; } }

        public float AlturaMaxima { get; private set; }
        public float AlturaMinima { get; private set; }
        public int MuestrasSinCable { get; private set; }

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
            _parametros.muestrasAltura = Mathf.Max(32, parametros.muestrasAltura);
            _construido = false;
            _versionBorde++;
        }

        // ------------------------------------------------------------------
        //  Construccion
        // ------------------------------------------------------------------

        /// <summary>
        /// Muestrea la superficie de cables a lo largo de la curva del vano. Los tramos
        /// donde ningun cable llega —si los hubiera— se rellenan interpolando entre los
        /// vecinos validos, de forma ciclica.
        /// </summary>
        public void Construir(ISuperficieCables superficie)
        {
            if (superficie == null) throw new ArgumentNullException(nameof(superficie));

            _planta = new PerimetroSuperelipse(_parametros.SemiVanoX,
                                               _parametros.SemiVanoZ,
                                               _parametros.exponenteVano);

            int n = _parametros.muestrasAltura;
            _pasoMuestra = _planta.LongitudTotal / n;
            _alturas = new float[n];
            var valida = new bool[n];

            MuestrasSinCable = 0;

            for (int i = 0; i < n; i++)
            {
                Vector2 xz = _planta.PuntoPorLongitud(i * _pasoMuestra);
                if (superficie.TryAltura(xz.x, xz.y, out float y))
                {
                    _alturas[i] = y;
                    valida[i] = true;
                }
                else
                {
                    MuestrasSinCable++;
                }
            }

            RellenarHuecos(valida);

            AlturaMaxima = float.NegativeInfinity;
            AlturaMinima = float.PositiveInfinity;
            for (int i = 0; i < n; i++)
            {
                AlturaMaxima = Mathf.Max(AlturaMaxima, _alturas[i]);
                AlturaMinima = Mathf.Min(AlturaMinima, _alturas[i]);
            }

            for (int i = 0; i < 4; i++)
            {
                Vector2 xz = _planta.Punto(TEsquinas[i]);
                // Lectura directa de la tabla: AlturaEnT exige el borde ya construido y
                // todavia estamos dentro de Construir.
                float y = AlturaMuestreada(_planta.LongitudDeT(TEsquinas[i]));
                _esquinas[i] = new Vector3(xz.x, y, xz.y);
            }

            _construido = true;
            _versionBorde++;
        }

        private void RellenarHuecos(bool[] valida)
        {
            int n = _alturas.Length;

            int primeraValida = -1;
            for (int i = 0; i < n; i++) if (valida[i]) { primeraValida = i; break; }

            if (primeraValida < 0)
                throw new InvalidOperationException(
                    "Ningun punto del borde interior encontro cable encima. Revisar que la " +
                    "familia transversal se haya construido y que el vano quede dentro de su alcance.");

            for (int k = 0; k < n; k++)
            {
                int i = (primeraValida + k) % n;
                if (valida[i]) continue;

                // Vecinos validos hacia atras y hacia adelante, ciclicos.
                int atras = i, pasosAtras = 0;
                do { atras = (atras - 1 + n) % n; pasosAtras++; } while (!valida[atras] && pasosAtras < n);

                int adelante = i, pasosAdelante = 0;
                do { adelante = (adelante + 1) % n; pasosAdelante++; } while (!valida[adelante] && pasosAdelante < n);

                float f = (float)pasosAtras / (pasosAtras + pasosAdelante);
                _alturas[i] = Mathf.Lerp(_alturas[atras], _alturas[adelante], f);
                valida[i] = true;
            }
        }

        private void AsegurarConstruido()
        {
            if (!_construido)
                throw new InvalidOperationException(
                    "El borde interior no esta construido. Llamar a Construir(superficie) " +
                    "despues de tender la familia transversal de cables.");
        }

        // ------------------------------------------------------------------
        //  Consultas
        // ------------------------------------------------------------------

        public float AlturaEnS(float s)
        {
            AsegurarConstruido();
            return AlturaMuestreada(s);
        }

        /// <summary>Interpolacion en la tabla de alturas, sin validar estado. Se usa
        /// tambien desde Construir, cuando el borde todavia no esta marcado como listo.</summary>
        private float AlturaMuestreada(float s)
        {
            int n = _alturas.Length;
            float longitud = _planta.LongitudTotal;
            s = Mathf.Repeat(s, longitud);

            float indice = s / _pasoMuestra;
            int i0 = Mathf.FloorToInt(indice) % n;
            int i1 = (i0 + 1) % n;
            float f = indice - Mathf.Floor(indice);

            return Mathf.Lerp(_alturas[i0], _alturas[i1], f);
        }

        public float AlturaEnT(float t)
        {
            AsegurarConstruido();
            return AlturaEnS(_planta.LongitudDeT(t));
        }

        public Vector3 PuntoEnS(float s)
        {
            AsegurarConstruido();
            Vector2 xz = _planta.PuntoPorLongitud(s);
            return new Vector3(xz.x, AlturaEnS(s), xz.y);
        }

        public Vector3 PuntoEnT(float t)
        {
            AsegurarConstruido();
            Vector2 xz = _planta.Punto(t);
            return new Vector3(xz.x, AlturaEnT(t), xz.y);
        }

        private Vector3 PuntoDesdeXZ(Vector2 xz)
        {
            return new Vector3(xz.x, AlturaEnS(_planta.SDePunto(xz)), xz.y);
        }

        /// <summary>Cruce del borde con un cable transversal en z0. Son los dos puntos
        /// donde el cable toma la estructura tubular del vano.</summary>
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

        /// <summary>Muestreo del arco entre dos esquinas consecutivas. arco 1 y 3 son los
        /// lados largos (sobre las plateas), 0 y 2 las cabeceras.</summary>
        public Vector3[] MuestrearArco(int arco, int segmentos)
        {
            AsegurarConstruido();
            arco = Mathf.Clamp(arco, 0, 3);
            segmentos = Mathf.Max(2, segmentos);

            float longitud = _planta.LongitudTotal;
            float sInicio = _planta.LongitudDeT(TEsquinas[arco]);
            float sFin = _planta.LongitudDeT(TEsquinas[(arco + 1) % 4]);
            if (sFin <= sInicio) sFin += longitud;

            var puntos = new Vector3[segmentos + 1];
            for (int i = 0; i <= segmentos; i++)
                puntos[i] = PuntoEnS(Mathf.Lerp(sInicio, sFin, (float)i / segmentos));

            return puntos;
        }

        public float LongitudArco(int arco)
        {
            AsegurarConstruido();
            arco = Mathf.Clamp(arco, 0, 3);

            float longitud = _planta.LongitudTotal;
            float sInicio = _planta.LongitudDeT(TEsquinas[arco]);
            float sFin = _planta.LongitudDeT(TEsquinas[(arco + 1) % 4]);
            if (sFin <= sInicio) sFin += longitud;

            return sFin - sInicio;
        }

        // ------------------------------------------------------------------
        //  Validacion y diagnostico
        // ------------------------------------------------------------------

        public bool Validar(IPerimetroEstadio perimetroExterior, RegistroAnclajesTecho registro,
                            List<string> mensajes, float holguraMinimaSobreCampo = 20f)
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
                valido = false;
            }

            if (MuestrasSinCable > 0)
            {
                float fraccion = (float)MuestrasSinCable / _alturas.Length;
                mensajes.Add($"AVISO: el {fraccion * 100f:F0}% del borde no encontro cable encima " +
                             "y se relleno interpolando. Suele ser la zona de los codos.");
            }

            if (AlturaMinima < holguraMinimaSobreCampo)
            {
                mensajes.Add($"ERROR: el borde interior baja hasta {AlturaMinima:F1} m " +
                             $"(minimo {holguraMinimaSobreCampo:F1} m). Aumentar la tension de los " +
                             "cables transversales para reducir la panza.");
                valido = false;
            }

            return valido;
        }

        public string Diagnostico()
        {
            var sb = new StringBuilder();
            sb.AppendLine($"Borde interior (version {_versionBorde}, construido: {_construido})");

            if (!_construido) return sb.ToString();

            sb.AppendLine($"Vano: {2f * _parametros.SemiVanoX:F1} x {2f * _parametros.SemiVanoZ:F1} m, " +
                          $"exponente {_parametros.exponenteVano:F1}");
            sb.AppendLine($"Perimetro del borde: {LongitudTotal:F1} m");
            sb.AppendLine($"Altura derivada de los cables: min {AlturaMinima:F2} m, " +
                          $"max {AlturaMaxima:F2} m, desnivel {AlturaMaxima - AlturaMinima:F2} m");
            sb.AppendLine($"Esquinas: " +
                          $"{_esquinas[0].y:F1} | {_esquinas[1].y:F1} | " +
                          $"{_esquinas[2].y:F1} | {_esquinas[3].y:F1} m");
            sb.AppendLine($"Lados largos (plateas): {LongitudArco(1):F1} m y {LongitudArco(3):F1} m");
            sb.AppendLine($"Cabeceras: {LongitudArco(0):F1} m y {LongitudArco(2):F1} m");

            if (MuestrasSinCable > 0)
                sb.AppendLine($"Muestras sin cable encima: {MuestrasSinCable} de {_alturas.Length}");

            return sb.ToString();
        }
    }
}
