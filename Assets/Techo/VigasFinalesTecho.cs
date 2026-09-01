using System;
using System.Collections.Generic;
using UnityEngine;

namespace Estadio.Techo
{
    public enum FormaVigaFinal
    {
        Recta,
        /// <summary>Cae rapido y se aplana, o al reves segun la orientacion. Da una linea
        /// tensa, de aire estructural.</summary>
        Hiperbolica,
        //// <summary>Ese perfil en S entre los dos extremos. El sesgo corre el punto de
        /// inflexion para que no quede simetrica.</summary>
        Sinusoidal
    }

    /// <summary>
    /// Las cuatro vigas que cierran las esquinas del techo.
    ///
    /// Cada una arranca en el ultimo anclaje de techo de su viga longitudinal —el punto mas
    /// alto y mas alejado— y baja hacia el centro del estadio hasta encontrar el muro
    /// superior de la cabecera, dando la idea de encastrar con el.
    ///
    /// A diferencia de los soportes de codo, que son estructura repetitiva, estas son piezas
    /// singulares y bien visibles: forman parte del frente del edificio. Por eso su forma es
    /// elegible y no necesariamente recta.
    /// </summary>
    [RequireComponent(typeof(ControladorTecho))]
    public sealed class VigasFinalesTecho : MonoBehaviour
    {
        [Header("Referencias")]
        [SerializeField] private Transform ground0Level;
        [SerializeField] private Material materialVigas;

        [Header("Forma de la diagonal")]
        [SerializeField] private FormaVigaFinal forma = FormaVigaFinal.Hiperbolica;
        [Tooltip("Hacia donde va la panza de la curva. Positivo la levanta, negativo la hunde.")]
        [SerializeField, Range(-1f, 1f)] private float orientacionCurvatura = -0.5f;
        [Tooltip("Solo para la sinusoidal: corre el punto de inflexion. 0.5 la deja simetrica.")]
        [SerializeField, Range(0.15f, 0.85f)] private float sesgoSinusoidal = 0.5f;
        [SerializeField, Range(8, 64)] private int segmentosDiagonal = 32;

        [Header("Secciones")]
        [SerializeField] private float anchoVigaDiagonal = 0.4f;
        [SerializeField] private float altoVigaDiagonal = 0.8f;
        [SerializeField] private float anchoVigaVertical = 0.3f;
        [SerializeField] private float altoVigaVertical = 0.3f;

        [Header("Verticales de apoyo")]
        [Tooltip("Posicion de cada vertical como fraccion del desarrollo horizontal de la " +
                 "viga, medido desde el extremo alto.")]
        [SerializeField, Range(0f, 1f)] private float posicionVerticalA = 0.35f;
        [SerializeField, Range(0f, 1f)] private float posicionVerticalB = 0.7f;

        [Header("Ajustes")]
        [Tooltip("Retiro del extremo inferior respecto del fin del codo, para que la viga no " +
                 "quede justo al borde.")]
        [SerializeField] private float retiroDelFinDelCodo = 1f;

        private ControladorTecho _controlador;
        private GameObject _raiz;

        public bool Generado => _raiz != null;
        public int VigasGeneradas { get; private set; }

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
            VigasGeneradas = 0;
        }

        public void Generar(Transform padre)
        {
            if (!Application.isPlaying)
            {
                Debug.LogWarning("[Techo] Las vigas finales solo se generan en modo juego.", this);
                return;
            }

            ControladorTecho c = Controlador;
            if (c == null || !c.GeometriaLista)
            {
                Debug.LogError("[Techo] La geometria del techo no esta lista.", this);
                return;
            }

            Descartar();

            _raiz = new GameObject("Vigas_Finales_Techo");
            _raiz.transform.SetParent(padre != null ? padre : transform, false);

            foreach (bool ladoPositivoX in new[] { false, true })
                foreach (bool ladoPositivoZ in new[] { false, true })
                    GenerarViga(c, ladoPositivoX, ladoPositivoZ);

            Debug.Log($"[Techo] {VigasGeneradas} vigas finales generadas.", this);
        }

        // ------------------------------------------------------------------
        //  Una viga
        // ------------------------------------------------------------------

        private void GenerarViga(ControladorTecho c, bool ladoPositivoX, bool ladoPositivoZ)
        {
            RectaViga recta = ladoPositivoX
                ? c.PerimetroTecho.RectaXPositivo
                : c.PerimetroTecho.RectaXNegativo;

            float zExtremo = ladoPositivoZ
                ? c.PerimetroTecho.ZCierrePositivo
                : c.PerimetroTecho.ZCierreNegativo;

            // Extremo alto: el ultimo anclaje de techo de esta viga longitudinal.

            var superior = new Vector3(recta.XenZ(zExtremo),
                           AlturaVigaLongitudinal(c, ladoPositivoX, ladoPositivoZ),
                           zExtremo);

            // Extremo bajo en X: el fin del codo. Se toma del perimetro del estadio en esa
            // cota Z, que es donde la grada deja de existir.
            if (!c.PerimetroEstadio.IntersectarZ(zExtremo, out float xPositivo, out float xNegativo))
            {
                Debug.LogWarning($"[Techo] La viga final en z={zExtremo:F1} no encuentra el fin " +
                                 "del codo: esa cota cae fuera del perimetro del estadio.", this);
                return;
            }

            float xFinCodo = ladoPositivoX ? xPositivo : xNegativo;
            float xInferior = xFinCodo - Mathf.Sign(xFinCodo) * retiroDelFinDelCodo;

            // Extremo bajo en Y: el muro superior de lo que haya debajo. El registro de
            // coronamientos ya se quedo con el mas alto donde se solapan dos sectores, asi
            // que si hay dos bandejas devuelve la de arriba.
            float yInferior = c.Coronamientos.AlturaBajoPunto(new Vector2(xInferior, zExtremo));

            if (yInferior >= superior.y)
            {
                Debug.LogWarning($"[Techo] La viga final en z={zExtremo:F1} tendria que subir: el " +
                                 $"muro de la cabecera ({yInferior:F1} m) esta por encima del " +
                                 $"anclaje de techo ({superior.y:F1} m). No se genera.", this);
                return;
            }

            var inferior = new Vector3(xInferior, yInferior, zExtremo);

            float desarrollo = Mathf.Abs(inferior.x - superior.x);
            if (desarrollo < 2f)
            {
                Debug.LogWarning($"[Techo] La viga final en z={zExtremo:F1} tiene solo " +
                                 $"{desarrollo:F1} m de desarrollo horizontal: quedaria casi " +
                                 "vertical. Revisar el largo del techo o el fin del codo.", this);
            }

            Vector3[] eje = MuestrearDiagonal(superior, inferior);

            var contenedor = new GameObject(
                $"VigaFinal_{(ladoPositivoX ? "X+" : "X-")}_{(ladoPositivoZ ? "Z+" : "Z-")}");
            contenedor.transform.SetParent(_raiz.transform, false);

            for (int i = 1; i < eje.Length; i++)
                CrearCaja(eje[i - 1], eje[i], anchoVigaDiagonal, altoVigaDiagonal,
                          contenedor.transform, $"Tramo_{i}");

            float yPiso = NivelPiso(c);
            CrearVerticalEn(eje, posicionVerticalA, yPiso, contenedor.transform, "Vertical_A");
            CrearVerticalEn(eje, posicionVerticalB, yPiso, contenedor.transform, "Vertical_B");

            VigasGeneradas++;
        }

        /// <summary>
        /// Muestrea la diagonal segun la forma elegida. La interpolacion en X siempre es
        /// lineal: lo que cambia es como desciende en Y.
        /// </summary>
        private Vector3[] MuestrearDiagonal(Vector3 superior, Vector3 inferior)
        {
            int n = Mathf.Max(2, segmentosDiagonal);
            var puntos = new Vector3[n + 1];

            for (int i = 0; i <= n; i++)
            {
                float u = (float)i / n;
                float v = PerfilVertical(u);

                puntos[i] = new Vector3(
                    Mathf.Lerp(superior.x, inferior.x, u),
                    Mathf.Lerp(superior.y, inferior.y, v),
                    Mathf.Lerp(superior.z, inferior.z, u));
            }

            return puntos;
        }

        /// <summary>
        /// Fraccion de descenso en funcion de la fraccion de avance horizontal. Devuelve 0 en
        /// el extremo alto y 1 en el bajo; lo que cambia entre formas es el camino.
        /// </summary>
        private float PerfilVertical(float u)
        {
            switch (forma)
            {
                case FormaVigaFinal.Hiperbolica:
                {
                    // Con orientacion negativa cae rapido y se aplana; con positiva, al reves.
                    float k = 1f + Mathf.Abs(orientacionCurvatura) * 6f;
                    float t = orientacionCurvatura < 0f
                        ? 1f - Mathf.Pow(1f - u, k)
                        : Mathf.Pow(u, k);
                    return t;
                }

                case FormaVigaFinal.Sinusoidal:
                {
                    // S entre los dos extremos. El sesgo corre el punto de inflexion, asi que
                    // no queda simetrica.
                    float s = Mathf.Clamp(sesgoSinusoidal, 0.05f, 0.95f);
                    float uSesgado = u < s
                        ? 0.5f * (u / s)
                        : 0.5f + 0.5f * ((u - s) / (1f - s));

                    float base_ = 0.5f - 0.5f * Mathf.Cos(uSesgado * Mathf.PI);
                    return Mathf.Lerp(u, base_, Mathf.Abs(orientacionCurvatura));
                }

                default:
                    return u;
            }
        }

        private void CrearVerticalEn(Vector3[] eje, float fraccion, float yPiso,
                                     Transform padre, string nombre)
        {
            fraccion = Mathf.Clamp01(fraccion);
            int i = Mathf.Clamp(Mathf.RoundToInt(fraccion * (eje.Length - 1)), 0, eje.Length - 1);

            Vector3 arriba = eje[i];
            var abajo = new Vector3(arriba.x, yPiso, arriba.z);

            if (arriba.y - yPiso < 0.5f) return;

            CrearCaja(abajo, arriba, anchoVigaVertical, altoVigaVertical, padre, nombre);
        }

        // ------------------------------------------------------------------

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

        /// <summary>Caja recta entre dos puntos, con la seccion orientada segun el eje Z del
        /// techo: estas vigas viven en un plano transversal.</summary>
        private void CrearCaja(Vector3 desde, Vector3 hasta, float ancho, float alto,
                               Transform padre, string nombre)
        {
            Vector3 eje = hasta - desde;
            if (eje.sqrMagnitude < 1e-6f) return;

            Vector3 direccion = eje.normalized;

            Vector3 normal = Vector3.forward * (ancho * 0.5f);
            Vector3 lateral = Vector3.Cross(direccion, Vector3.forward);
            if (lateral.sqrMagnitude < 1e-6f) lateral = Vector3.up;
            lateral = lateral.normalized * (alto * 0.5f);

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
            go.transform.SetParent(padre, false);
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;
            go.AddComponent<MeshFilter>().mesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = materialVigas;
        }
    }
}
