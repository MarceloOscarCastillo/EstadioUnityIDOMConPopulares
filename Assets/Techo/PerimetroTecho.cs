using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace Estadio.Techo
{
    /// <summary>
    /// Una de las dos vigas longitudinales del techo, en planta. Es una recta ajustada a
    /// los anclajes que publica su platea: si esa platea tiene recorte de filas, su borde
    /// exterior se aleja del campo y la recta queda inclinada. Las dos rectas del estadio
    /// no tienen por que ser paralelas entre si.
    /// </summary>
    public struct RectaViga
    {
        /// <summary>x = pendiente * z + ordenada</summary>
        public float pendiente;
        public float ordenada;

        public int cantidadAnclajes;
        public float desvioMaximo;      // distancia del anclaje mas alejado a la recta
        public float zPrimerAnclaje;
        public float zUltimoAnclaje;

        public float XenZ(float z) => pendiente * z + ordenada;

        public Vector2 PuntoEnZ(float z) => new Vector2(XenZ(z), z);

        /// <summary>Direccion unitaria de la recta en planta, hacia +Z.</summary>
        public Vector2 Direccion
        {
            get
            {
                var d = new Vector2(pendiente, 1f);
                return d.normalized;
            }
        }

        /// <summary>Normal en planta apuntando hacia afuera del estadio.</summary>
        public Vector2 NormalExterior(float signoLado)
        {
            Vector2 d = Direccion;
            var n = new Vector2(d.y, -d.x);
            return n * Mathf.Sign(signoLado * n.x);
        }
    }

    [Serializable]
    public struct ParametrosPerimetroTecho
    {
        [Header("Extension del techo")]
        [Tooltip("Semi-largo del techo medido desde el centro del campo. Ronda la mitad " +
                 "de la longitud del estadio; se ajusta para controlar cuanto sobresale " +
                 "la membrana en las esquinas.")]
        public float semiLargoTecho;

        [Header("Reparto de anclajes")]
        [Tooltip("Separacion entre tensores a lo largo de cada viga longitudinal. Debe " +
                 "coincidir con la separacion de soportes de las tribunas.")]
        public float separacionAnclajes;

        public static ParametrosPerimetroTecho PorDefecto => new ParametrosPerimetroTecho
        {
            semiLargoTecho = 90f,
            separacionAnclajes = 5f
        };
    }

    /// <summary>
    /// El perimetro del techo no es una curva cerrada: son las dos vigas longitudinales
    /// —una por platea lateral, rectas de punta a punta del estadio— cerradas en cada
    /// extremo por un cable transversal.
    ///
    /// Las rectas no se configuran: se ajustan por minimos cuadrados a los anclajes que
    /// publicaron las plateas. Si una platea tiene mas filas que la otra, su recta queda
    /// mas lejos del campo, y la asimetria del estadio aparece sola.
    ///
    /// La superelipse sigue existiendo, pero describe el perimetro del ESTADIO —de donde
    /// sale el coronamiento y por lo tanto el faldon—, no el del techo.
    /// </summary>
    public sealed class PerimetroTecho
    {
        private ParametrosPerimetroTecho _parametros;

        private RectaViga _rectaXNegativo;
        private RectaViga _rectaXPositivo;

        private int _versionPerimetro;
        private int _versionRegistroUsada = -1;
        private bool _construido;

        public ParametrosPerimetroTecho Parametros => _parametros;
        public int VersionPerimetro => _versionPerimetro;
        public bool Construido => _construido;

        public RectaViga RectaXNegativo { get { AsegurarConstruido(); return _rectaXNegativo; } }
        public RectaViga RectaXPositivo { get { AsegurarConstruido(); return _rectaXPositivo; } }

        public float SemiLargo => _parametros.semiLargoTecho;

        public PerimetroTecho(ParametrosPerimetroTecho parametros)
        {
            Configurar(parametros);
        }

        public void Configurar(ParametrosPerimetroTecho parametros)
        {
            if (parametros.semiLargoTecho <= 0f)
                throw new ArgumentOutOfRangeException(nameof(parametros), "semiLargoTecho debe ser positivo.");
            if (parametros.separacionAnclajes <= 0f)
                throw new ArgumentOutOfRangeException(nameof(parametros), "separacionAnclajes debe ser positiva.");

            _parametros = parametros;
            _construido = false;
            _versionPerimetro++;
        }

        public bool NecesitaConstruir(RegistroAnclajesTecho registro)
        {
            return !_construido || registro.VersionRegistro != _versionRegistroUsada;
        }

        // ------------------------------------------------------------------
        //  Construccion
        // ------------------------------------------------------------------

        /// <summary>
        /// Ajusta una recta a los anclajes de cada lado. Se separan por el signo de X:
        /// cada platea publica los suyos y no hay ambiguedad posible, porque estan a mas
        /// de cien metros unos de otros.
        /// </summary>
        public void Construir(RegistroAnclajesTecho registro)
        {
            if (registro == null) throw new ArgumentNullException(nameof(registro));

            var ladoNegativo = new List<Vector2>();
            var ladoPositivo = new List<Vector2>();

            IReadOnlyList<AnclajeTecho> anclajes = registro.Anclajes;
            for (int i = 0; i < anclajes.Count; i++)
            {
                Vector3 p = anclajes[i].posicion;
                if (p.x < 0f) ladoNegativo.Add(new Vector2(p.x, p.z));
                else ladoPositivo.Add(new Vector2(p.x, p.z));
            }

            _rectaXNegativo = AjustarRecta(ladoNegativo, "x negativo");
            _rectaXPositivo = AjustarRecta(ladoPositivo, "x positivo");

            _versionRegistroUsada = registro.VersionRegistro;
            _construido = true;
            _versionPerimetro++;
        }

        /// <summary>
        /// Minimos cuadrados de x en funcion de z. Se ajusta x = m*z + b y no al reves
        /// porque las vigas corren a lo largo de Z: si la recta fuera casi paralela al eje
        /// X el ajuste divergeria, pero eso no puede pasar en un estadio.
        /// </summary>
        private static RectaViga AjustarRecta(List<Vector2> puntos, string nombre)
        {
            if (puntos.Count < 2)
                throw new InvalidOperationException(
                    $"El lado {nombre} tiene {puntos.Count} anclajes: hacen falta al menos 2 " +
                    "para ajustar la viga longitudinal. Revisar que esa platea publique.");

            float sumaZ = 0f, sumaX = 0f, sumaZZ = 0f, sumaZX = 0f;
            float zMin = float.MaxValue, zMax = float.MinValue;

            foreach (Vector2 p in puntos)
            {
                float x = p.x, z = p.y;
                sumaZ += z; sumaX += x; sumaZZ += z * z; sumaZX += z * x;
                zMin = Mathf.Min(zMin, z); zMax = Mathf.Max(zMax, z);
            }

            int n = puntos.Count;
            float denominador = n * sumaZZ - sumaZ * sumaZ;

            float pendiente = Mathf.Abs(denominador) > 1e-6f
                ? (n * sumaZX - sumaZ * sumaX) / denominador
                : 0f;

            float ordenada = (sumaX - pendiente * sumaZ) / n;

            float desvioMaximo = 0f;
            foreach (Vector2 p in puntos)
                desvioMaximo = Mathf.Max(desvioMaximo, Mathf.Abs(p.x - (pendiente * p.y + ordenada)));

            return new RectaViga
            {
                pendiente = pendiente,
                ordenada = ordenada,
                cantidadAnclajes = n,
                desvioMaximo = desvioMaximo,
                zPrimerAnclaje = zMin,
                zUltimoAnclaje = zMax
            };
        }

        private void AsegurarConstruido()
        {
            if (!_construido)
                throw new InvalidOperationException(
                    "El perimetro del techo no esta construido. Llamar a Construir(registro).");
        }

        // ------------------------------------------------------------------
        //  Consultas
        // ------------------------------------------------------------------

        /// <summary>
        /// Los dos extremos de un cable transversal a la cota Z dada: donde corta cada una
        /// de las dos vigas. Sin busqueda ni tolerancia: es evaluar dos rectas.
        /// </summary>
        public void ExtremosTransversal(float z, out Vector2 xNegativo, out Vector2 xPositivo)
        {
            AsegurarConstruido();
            xNegativo = _rectaXNegativo.PuntoEnZ(z);
            xPositivo = _rectaXPositivo.PuntoEnZ(z);
        }

        public float AnchoEnZ(float z)
        {
            AsegurarConstruido();
            return _rectaXPositivo.XenZ(z) - _rectaXNegativo.XenZ(z);
        }

        /// <summary>Cotas Z de los dos cables que cierran el techo en las cabeceras.</summary>
        public float ZCierreNegativo => -_parametros.semiLargoTecho;
        public float ZCierrePositivo => +_parametros.semiLargoTecho;

        /// <summary>
        /// Reparte anclajes a paso constante sobre una viga, en toda su extension. Los que
        /// caen mas alla de donde llegan los anclajes publicados son los del codo: continuan
        /// la misma recta y a la misma altura, sin bajar con la grada.
        /// </summary>
        public Vector2[] RepartirAnclajes(bool ladoPositivo, out float separacionReal)
        {
            AsegurarConstruido();

            RectaViga recta = ladoPositivo ? _rectaXPositivo : _rectaXNegativo;

            float largo = 2f * _parametros.semiLargoTecho;
            Vector2 direccion = recta.Direccion;
            float largoSobreRecta = largo / Mathf.Abs(direccion.y);

            int cantidad = Mathf.Max(2, Mathf.RoundToInt(largoSobreRecta / _parametros.separacionAnclajes));
            separacionReal = largoSobreRecta / cantidad;

            var puntos = new Vector2[cantidad + 1];
            for (int i = 0; i <= cantidad; i++)
            {
                float z = Mathf.Lerp(-_parametros.semiLargoTecho, _parametros.semiLargoTecho,
                                     (float)i / cantidad);
                puntos[i] = recta.PuntoEnZ(z);
            }

            return puntos;
        }

        /// <summary>
        /// True si la cota Z queda mas alla de donde llegan los anclajes publicados de esa
        /// viga: es la zona del codo, donde la estructura de techo no se apoya en la grada.
        /// </summary>
        public bool EsZonaCodo(bool ladoPositivo, float z, float margen = 0f)
        {
            AsegurarConstruido();
            RectaViga recta = ladoPositivo ? _rectaXPositivo : _rectaXNegativo;
            return z < recta.zPrimerAnclaje - margen || z > recta.zUltimoAnclaje + margen;
        }

        // ------------------------------------------------------------------
        //  Validacion y diagnostico
        // ------------------------------------------------------------------

        public bool Validar(List<string> mensajes, float desvioMaximoTolerado = 1.5f)
        {
            if (mensajes == null) throw new ArgumentNullException(nameof(mensajes));

            if (!_construido)
            {
                mensajes.Add("ERROR: el perimetro del techo no esta construido.");
                return false;
            }

            bool valido = true;

            valido &= ValidarRecta(_rectaXNegativo, "x negativo", desvioMaximoTolerado, mensajes);
            valido &= ValidarRecta(_rectaXPositivo, "x positivo", desvioMaximoTolerado, mensajes);

            float alcance = Mathf.Max(
                Mathf.Max(Mathf.Abs(_rectaXNegativo.zPrimerAnclaje), Mathf.Abs(_rectaXNegativo.zUltimoAnclaje)),
                Mathf.Max(Mathf.Abs(_rectaXPositivo.zPrimerAnclaje), Mathf.Abs(_rectaXPositivo.zUltimoAnclaje)));

            if (_parametros.semiLargoTecho < alcance)
            {
                mensajes.Add($"ERROR: semiLargoTecho ({_parametros.semiLargoTecho:F1} m) es menor que " +
                             $"la extension de los anclajes publicados ({alcance:F1} m). El techo " +
                             "quedaria mas corto que las plateas.");
                valido = false;
            }

            return valido;
        }

        private static bool ValidarRecta(RectaViga recta, string nombre, float tolerancia,
                                         List<string> mensajes)
        {
            if (recta.desvioMaximo > tolerancia)
            {
                mensajes.Add($"AVISO: los anclajes del lado {nombre} se apartan hasta " +
                             $"{recta.desvioMaximo:F2} m de su recta. Si la platea tiene recorte de " +
                             "filas, un desvio chico es esperable; si es grande, revisar que solo " +
                             "esa platea publique de ese lado.");
                return true;
            }
            return true;
        }

        public string Diagnostico()
        {
            var sb = new StringBuilder();
            sb.AppendLine($"Perimetro del techo (version {_versionPerimetro}, construido: {_construido})");

            if (!_construido) return sb.ToString();

            DescribirRecta(sb, _rectaXNegativo, "Viga x negativo");
            DescribirRecta(sb, _rectaXPositivo, "Viga x positivo");

            sb.AppendLine($"Semi-largo del techo: {_parametros.semiLargoTecho:F1} m " +
                          $"(cierres en z = {ZCierreNegativo:F1} y {ZCierrePositivo:F1})");
            sb.AppendLine($"Ancho del techo: {AnchoEnZ(0f):F1} m en el centro, " +
                          $"{AnchoEnZ(ZCierrePositivo):F1} m en el cierre positivo, " +
                          $"{AnchoEnZ(ZCierreNegativo):F1} m en el negativo");

            return sb.ToString();
        }

        private static void DescribirRecta(StringBuilder sb, RectaViga recta, string titulo)
        {
            float inclinacion = Mathf.Atan(recta.pendiente) * Mathf.Rad2Deg;

            sb.AppendLine($"{titulo}: x = {recta.pendiente:F4} * z + {recta.ordenada:F2}  " +
                          $"({inclinacion:F2} grados)");
            sb.AppendLine($"  {recta.cantidadAnclajes} anclajes entre z = {recta.zPrimerAnclaje:F1} " +
                          $"y {recta.zUltimoAnclaje:F1} | desvio maximo {recta.desvioMaximo:F2} m");
        }
    }
}
