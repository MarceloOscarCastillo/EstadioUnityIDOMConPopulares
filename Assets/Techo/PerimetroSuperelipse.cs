using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace Estadio.Techo
{
    /// <summary>
    /// Perímetro cerrado del estadio en planta (XZ). Todas las magnitudes de longitud
    /// están en metros. El recorrido es antihorario visto desde +Y, arrancando en (+X, 0).
    /// </summary>
    public interface IPerimetroEstadio
    {
        float SemiejeX { get; }
        float SemiejeZ { get; }
        float Exponente { get; }

        /// <summary>Se incrementa ante cualquier cambio de geometría. Los consumidores
        /// comparan contra el último valor que procesaron para saber si deben reconstruir.</summary>
        int VersionGeometria { get; }

        float LongitudTotal { get; }

        Vector2 Punto(float t);
        Vector2 Tangente(float t);
        Vector2 NormalExterior(float t);

        float LongitudDeT(float t);
        float TDeLongitud(float s);
        Vector2 PuntoPorLongitud(float s);

        /// <summary>Puntos de salida de un cable paralelo al eje Z ubicado en x0.
        /// Devuelve false si x0 cae fuera del estadio.</summary>
        bool IntersectarX(float x0, out float zPositivo, out float zNegativo);

        /// <summary>Puntos de salida de un cable paralelo al eje X ubicado en z0.</summary>
        bool IntersectarZ(float z0, out float xPositivo, out float xNegativo);

        /// <summary>Ángulo en grados entre un cable paralelo a Z y la tangente del
        /// perímetro en su punto de salida. 90 = incidencia perpendicular (ideal),
        /// 0 = rasante (inservible). Usar para descartar cables en los codos.</summary>
        float AnguloIncidenciaX(float x0);

        float AnguloIncidenciaZ(float z0);

        /// <summary>Muestreo equiespaciado en metros a lo largo del perímetro.
        /// La cantidad real se ajusta al múltiplo de 4 más cercano para que los puntos
        /// caigan exactamente sobre los ejes y el conjunto sea simétrico respecto de
        /// ambos, condición necesaria para el emparejamiento de tensores.</summary>
        Vector2[] MuestrearPorSeparacion(float separacionObjetivo, out float separacionReal);
    }

    /// <summary>
    /// Superelipse (curva de Lamé): |x/a|^n + |z/b|^n = 1
    ///   n = 2  -> elipse
    ///   n = 4  -> "cuadrado redondeado" de codos marcados
    ///   n -> oo -> rectángulo
    ///
    /// La parametrización trigonométrica se vuelve patológica cerca de los ejes cuando
    /// n > 2 (la velocidad |P'(t)| diverge, aunque la curva es suave y la longitud finita).
    /// Por eso la relación t <-> longitud de arco se resuelve con una tabla construida por
    /// subdivisión adaptativa por cuerda, que concentra muestras exactamente donde hacen falta.
    /// </summary>
    public sealed class PerimetroSuperelipse : IPerimetroEstadio
    {
        public const float ExponenteMinimo = 2f;

        private const float EpsilonEje = 1e-6f;
        private const int ProfundidadMaxima = 32;
        private const int PuntosMaximosTabla = 20000;

        private float _semiejeX;
        private float _semiejeZ;
        private float _exponente;
        private float _m;                    // 2 / n, exponente de la forma trigonométrica
        private float _toleranciaCuerda;

        private int _versionGeometria;

        // Tabla del PRIMER CUADRANTE únicamente, t en [0, PI/2].
        // Los otros tres se resuelven por simetría, lo que garantiza que el emparejamiento
        // norte/sur y este/oeste sea exacto y no dependa de errores de muestreo.
        private List<float> _tablaT;
        private List<float> _tablaS;
        private float _longitudCuadrante;
        private bool _tablaValida;

        public PerimetroSuperelipse(float semiejeX, float semiejeZ, float exponente,
                                    float toleranciaCuerdaMetros = 0.05f)
        {
            _toleranciaCuerda = Mathf.Max(1e-3f, toleranciaCuerdaMetros);
            Configurar(semiejeX, semiejeZ, exponente);
        }

        public float SemiejeX => _semiejeX;
        public float SemiejeZ => _semiejeZ;
        public float Exponente => _exponente;
        public int VersionGeometria => _versionGeometria;

        public float LongitudTotal
        {
            get { AsegurarTabla(); return 4f * _longitudCuadrante; }
        }

        public static bool EsExponenteValido(float exponente)
        {
            return exponente >= ExponenteMinimo && !float.IsNaN(exponente) && !float.IsInfinity(exponente);
        }

        /// <summary>
        /// Cambia la geometría. Seguro de llamar en runtime; si nada cambió, no invalida nada.
        /// </summary>
        public void Configurar(float semiejeX, float semiejeZ, float exponente)
        {
            if (semiejeX <= 0f || semiejeZ <= 0f)
                throw new ArgumentOutOfRangeException(nameof(semiejeX),
                    $"Los semiejes deben ser positivos (recibido {semiejeX} x {semiejeZ}).");

            if (!EsExponenteValido(exponente))
                throw new ArgumentOutOfRangeException(nameof(exponente),
                    $"El exponente debe ser >= {ExponenteMinimo} (recibido {exponente}). " +
                    "Por debajo de 2 la superelipse se vuelve cóncava: la intersección deja de " +
                    "tener dos soluciones por eje, se rompe el emparejamiento de tensores y la " +
                    "geometría del techo pierde sentido físico.");

            bool sinCambios = Mathf.Approximately(semiejeX, _semiejeX)
                           && Mathf.Approximately(semiejeZ, _semiejeZ)
                           && Mathf.Approximately(exponente, _exponente);
            if (sinCambios && _tablaValida) return;

            _semiejeX = semiejeX;
            _semiejeZ = semiejeZ;
            _exponente = exponente;
            _m = 2f / exponente;

            _tablaValida = false;
            _versionGeometria++;
        }

        // ------------------------------------------------------------------
        //  Geometría puntual
        // ------------------------------------------------------------------

        public Vector2 Punto(float t)
        {
            float c = Mathf.Cos(t);
            float s = Mathf.Sin(t);
            return new Vector2(_semiejeX * PotenciaConSigno(c, _m),
                               _semiejeZ * PotenciaConSigno(s, _m));
        }

        public Vector2 Tangente(float t)
        {
            float c = Mathf.Cos(t);
            float s = Mathf.Sin(t);
            float ac = Mathf.Abs(c);
            float as_ = Mathf.Abs(s);

            // Sobre los ejes la derivada diverge para n > 2, pero la tangente normalizada
            // tiene límite bien definido. Se devuelve directamente.
            if (as_ < EpsilonEje) return new Vector2(0f, Mathf.Sign(c));
            if (ac < EpsilonEje) return new Vector2(-Mathf.Sign(s), 0f);

            float dx = -_semiejeX * _m * Mathf.Pow(ac, _m - 1f) * s;
            float dz = _semiejeZ * _m * Mathf.Pow(as_, _m - 1f) * c;
            return new Vector2(dx, dz).normalized;
        }

        public Vector2 NormalExterior(float t)
        {
            Vector2 tg = Tangente(t);
            return new Vector2(tg.y, -tg.x);   // recorrido antihorario -> esto apunta hacia afuera
        }

        public Vector2 PuntoPorLongitud(float s) => Punto(TDeLongitud(s));

        // ------------------------------------------------------------------
        //  Intersecciones: forma cerrada, sin búsqueda de raíces
        // ------------------------------------------------------------------

        public bool IntersectarX(float x0, out float zPositivo, out float zNegativo)
        {
            zPositivo = 0f;
            zNegativo = 0f;
            float u = Mathf.Abs(x0) / _semiejeX;
            if (u >= 1f) return false;

            float z = _semiejeZ * Mathf.Pow(1f - Mathf.Pow(u, _exponente), 1f / _exponente);
            zPositivo = z;
            zNegativo = -z;
            return true;
        }

        public bool IntersectarZ(float z0, out float xPositivo, out float xNegativo)
        {
            xPositivo = 0f;
            xNegativo = 0f;
            float u = Mathf.Abs(z0) / _semiejeZ;
            if (u >= 1f) return false;

            float x = _semiejeX * Mathf.Pow(1f - Mathf.Pow(u, _exponente), 1f / _exponente);
            xPositivo = x;
            xNegativo = -x;
            return true;
        }

        public float AnguloIncidenciaX(float x0)
        {
            if (!IntersectarX(x0, out float z, out _)) return 0f;
            return AnguloContra(DireccionTangenteEnPunto(x0, z), Vector2.up);
        }

        public float AnguloIncidenciaZ(float z0)
        {
            if (!IntersectarZ(z0, out float x, out _)) return 0f;
            return AnguloContra(DireccionTangenteEnPunto(x, z0), Vector2.right);
        }

        /// <summary>
        /// Tangente por derivación implícita, en unidades normalizadas para no perder
        /// precisión: dz/dx = -(b/a) * (|x|/a)^(n-1) / (|z|/b)^(n-1).
        /// Se devuelve sin normalizar y sin dividir, para tolerar z = 0 (tangente vertical).
        /// </summary>
        private Vector2 DireccionTangenteEnPunto(float x, float z)
        {
            float X = Mathf.Abs(x) / _semiejeX;
            float Z = Mathf.Abs(z) / _semiejeZ;
            float k = _exponente - 1f;
            return new Vector2(_semiejeX * Mathf.Pow(Z, k),
                              -_semiejeZ * Mathf.Pow(X, k));
        }

        private static float AnguloContra(Vector2 direccion, Vector2 referencia)
        {
            if (direccion.sqrMagnitude < 1e-20f) return 0f;
            float a = Vector2.Angle(direccion, referencia);
            return Mathf.Min(a, 180f - a);
        }

        // ------------------------------------------------------------------
        //  Longitud de arco
        // ------------------------------------------------------------------

        public float LongitudDeT(float t)
        {
            AsegurarTabla();
            float cuarto = Mathf.PI * 0.5f;
            float tn = Mathf.Repeat(t, 2f * Mathf.PI);
            int q = Mathf.Min(3, (int)(tn / cuarto));
            float tq = tn - q * cuarto;
            float Lq = _longitudCuadrante;

            switch (q)
            {
                case 0: return LongitudEnCuadrante(tq);
                case 1: return 2f * Lq - LongitudEnCuadrante(cuarto - tq);
                case 2: return 2f * Lq + LongitudEnCuadrante(tq);
                default: return 4f * Lq - LongitudEnCuadrante(cuarto - tq);
            }
        }

        public float TDeLongitud(float s)
        {
            AsegurarTabla();
            float Lq = _longitudCuadrante;
            float sn = Mathf.Repeat(s, 4f * Lq);
            int q = Mathf.Min(3, (int)(sn / Lq));
            float sq = sn - q * Lq;

            switch (q)
            {
                case 0: return TEnCuadrante(sq);
                case 1: return Mathf.PI - TEnCuadrante(Lq - sq);
                case 2: return Mathf.PI + TEnCuadrante(sq);
                default: return 2f * Mathf.PI - TEnCuadrante(Lq - sq);
            }
        }

        public Vector2[] MuestrearPorSeparacion(float separacionObjetivo, out float separacionReal)
        {
            if (separacionObjetivo <= 0f)
                throw new ArgumentOutOfRangeException(nameof(separacionObjetivo));

            AsegurarTabla();
            float L = LongitudTotal;

            // Múltiplo de 4: garantiza puntos exactos sobre los cuatro ejes y simetría
            // perfecta del conjunto, que es lo que hace que el par de cada tensor sea
            // simplemente su índice espejado.
            int grupos = Mathf.Max(1, Mathf.RoundToInt(L / (4f * separacionObjetivo)));
            int cantidad = grupos * 4;

            separacionReal = L / cantidad;

            var puntos = new Vector2[cantidad];
            for (int i = 0; i < cantidad; i++)
                puntos[i] = PuntoPorLongitud(i * separacionReal);

            return puntos;
        }

        // ------------------------------------------------------------------
        //  Construcción de la tabla (primer cuadrante)
        // ------------------------------------------------------------------

        private void AsegurarTabla()
        {
            if (_tablaValida) return;
            ConstruirTabla();
            _tablaValida = true;
        }

        private void ConstruirTabla()
        {
            float cuarto = Mathf.PI * 0.5f;

            _tablaT = new List<float>(2048) { 0f };
            Subdividir(0f, cuarto, Punto(0f), Punto(cuarto), 0);
            _tablaT.Add(cuarto);

            _tablaS = new List<float>(_tablaT.Count) { 0f };
            Vector2 anterior = Punto(_tablaT[0]);
            float acumulado = 0f;

            for (int i = 1; i < _tablaT.Count; i++)
            {
                Vector2 actual = Punto(_tablaT[i]);
                acumulado += Vector2.Distance(anterior, actual);
                _tablaS.Add(acumulado);
                anterior = actual;
            }

            _longitudCuadrante = acumulado;
        }

        private void Subdividir(float t0, float t1, Vector2 p0, Vector2 p1, int profundidad)
        {
            if (profundidad >= ProfundidadMaxima) return;
            if (_tablaT.Count >= PuntosMaximosTabla) return;
            if (Vector2.Distance(p0, p1) <= _toleranciaCuerda) return;

            float tm = 0.5f * (t0 + t1);
            Vector2 pm = Punto(tm);

            Subdividir(t0, tm, p0, pm, profundidad + 1);
            _tablaT.Add(tm);
            Subdividir(tm, t1, pm, p1, profundidad + 1);
        }

        private float LongitudEnCuadrante(float tq)
        {
            tq = Mathf.Clamp(tq, 0f, Mathf.PI * 0.5f);

            int lo = 0;
            int hi = _tablaT.Count - 1;
            while (hi - lo > 1)
            {
                int mid = (lo + hi) >> 1;
                if (_tablaT[mid] <= tq) lo = mid; else hi = mid;
            }

            float span = _tablaT[hi] - _tablaT[lo];
            float f = span > 1e-9f ? (tq - _tablaT[lo]) / span : 0f;
            return Mathf.Lerp(_tablaS[lo], _tablaS[hi], f);
        }

        private float TEnCuadrante(float sq)
        {
            sq = Mathf.Clamp(sq, 0f, _longitudCuadrante);

            int lo = 0;
            int hi = _tablaS.Count - 1;
            while (hi - lo > 1)
            {
                int mid = (lo + hi) >> 1;
                if (_tablaS[mid] <= sq) lo = mid; else hi = mid;
            }

            float span = _tablaS[hi] - _tablaS[lo];
            float f = span > 1e-9f ? (sq - _tablaS[lo]) / span : 0f;
            return Mathf.Lerp(_tablaT[lo], _tablaT[hi], f);
        }

        private static float PotenciaConSigno(float valor, float exponente)
        {
            float magnitud = Mathf.Pow(Mathf.Abs(valor), exponente);
            return valor < 0f ? -magnitud : magnitud;
        }

        // ------------------------------------------------------------------
        //  Diagnóstico
        // ------------------------------------------------------------------

        public string Diagnostico()
        {
            AsegurarTabla();
            var sb = new StringBuilder();
            sb.AppendLine($"Superelipse a={_semiejeX:F2} b={_semiejeZ:F2} n={_exponente:F2} (version {_versionGeometria})");
            sb.AppendLine($"Longitud total: {LongitudTotal:F2} m | tabla: {_tablaT.Count} puntos por cuadrante");
            sb.AppendLine($"Tolerancia de cuerda: {_toleranciaCuerda:F3} m");

            // Fracción del semieje X inutilizable por incidencia rasante a 30 grados.
            float limite = 0f;
            for (int i = 100; i >= 0; i--)
            {
                float x = _semiejeX * (i / 100f) * 0.999f;
                if (AnguloIncidenciaX(x) >= 30f) { limite = x; break; }
            }
            float descartado = 1f - limite / _semiejeX;
            sb.AppendLine($"Zona rasante (<30 grados) en cabeceras: ultimo {descartado * 100f:F1}% del semieje X");
            return sb.ToString();
        }
    }
}
