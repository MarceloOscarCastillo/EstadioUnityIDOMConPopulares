using UnityEngine;

namespace Estadio.Techo
{
    /// <summary>
    /// Proyección de puntos arbitrarios sobre el perímetro. La inversión de la
    /// parametrización es cerrada: si (x,z) está sobre la curva, elevando las
    /// coordenadas normalizadas a n/2 se recuperan exactamente cos(t) y sin(t).
    ///
    ///   x/a = signo(cos t) * |cos t|^(2/n)   =>   (x/a)^(n/2) = cos t
    ///
    /// Para puntos cercanos a la curva (que es el caso de las cabezas de viga)
    /// el resultado es una proyección de tipo radial, más que suficiente.
    /// </summary>
    public static class PerimetroExtensiones
    {
        public static float TDePunto(this IPerimetroEstadio perimetro, Vector2 puntoXZ)
        {
            float mitadN = perimetro.Exponente * 0.5f;
            float cosT = PotenciaConSigno(puntoXZ.x / perimetro.SemiejeX, mitadN);
            float senT = PotenciaConSigno(puntoXZ.y / perimetro.SemiejeZ, mitadN);

            if (Mathf.Abs(cosT) < 1e-12f && Mathf.Abs(senT) < 1e-12f) return 0f;

            float t = Mathf.Atan2(senT, cosT);
            return t < 0f ? t + 2f * Mathf.PI : t;
        }

        public static float SDePunto(this IPerimetroEstadio perimetro, Vector2 puntoXZ)
        {
            return perimetro.LongitudDeT(perimetro.TDePunto(puntoXZ));
        }

        /// <summary>
        /// Proyecta un punto sobre la curva y devuelve el desvío en metros, que sirve
        /// para detectar anclajes mal ubicados (una tribuna generada con otro perímetro,
        /// o posiciones ya arrasadas por el Static Batching).
        /// </summary>
        public static void Proyectar(this IPerimetroEstadio perimetro, Vector2 puntoXZ,
                                     out float t, out float s, out Vector2 proyectado, out float desvio)
        {
            t = perimetro.TDePunto(puntoXZ);
            s = perimetro.LongitudDeT(t);
            proyectado = perimetro.Punto(t);
            desvio = Vector2.Distance(puntoXZ, proyectado);
        }

        public static Vector2 AXZ(this Vector3 v) => new Vector2(v.x, v.z);

        public static Vector3 AMundo(this Vector2 xz, float altura) => new Vector3(xz.x, altura, xz.y);

        private static float PotenciaConSigno(float valor, float exponente)
        {
            float magnitud = Mathf.Pow(Mathf.Abs(valor), exponente);
            return valor < 0f ? -magnitud : magnitud;
        }
    }
}
