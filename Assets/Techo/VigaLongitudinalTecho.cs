using System;
using System.Collections.Generic;
using UnityEngine;

namespace Estadio.Techo
{
    /// <summary>
    /// La viga longitudinal que ata todos los tensores de un lado del estadio.
    ///
    /// Es UNA sola pieza por lado, de punta a punta: pasa por las plateas, sigue por los
    /// codos y termina en la viga final. Antes la generaba cada tribuna por su cuenta, y eso
    /// tenia dos problemas: quiebres en las junturas entre sectores, y que los soportes de
    /// codo tenian que adivinar a que altura estaba en lugar de leerla.
    ///
    /// Ahora es el techo el que la construye, y expone su altura para que todo lo que se
    /// apoya en ella use la misma fuente. Es del techo, no de las tribunas: un estadio sin
    /// techo no la tendria.
    /// </summary>
    [RequireComponent(typeof(ControladorTecho))]
    public sealed class VigaLongitudinalTecho : MonoBehaviour
    {
        [Header("Referencias")]
        [SerializeField] private Material materialVigas;

        [Header("Seccion")]
        [SerializeField] private float ancho = 0.25f;
        [SerializeField] private float alto = 0.35f;

        [Header("Trazado")]
        [Tooltip("Cuanto por encima de la cabeza del tensor corre el eje de la viga. Con 0 " +
                 "el eje pasa por la punta; positivo la levanta.")]
        [SerializeField] private float elevacionSobreTensor = 0f;
        [SerializeField, Range(8, 128)] private int segmentosPorLado = 64;

        private ControladorTecho _controlador;
        private GameObject _raiz;

        private Vector3[] _ejeXNegativo;
        private Vector3[] _ejeXPositivo;

        public bool Generado => _raiz != null;
        public bool EjesListos => _ejeXNegativo != null && _ejeXPositivo != null;

        private ControladorTecho Controlador
        {
            get
            {
                if (_controlador == null) _controlador = GetComponent<ControladorTecho>();
                return _controlador;
            }
        }

        // ------------------------------------------------------------------
        //  Trazado: se calcula siempre, se dibuje o no
        // ------------------------------------------------------------------

        /// <summary>
        /// Recalcula los dos ejes. Es barato y no crea objetos, asi que puede correr en modo
        /// diseno: los soportes de codo y las vigas finales lo consultan para saber a que
        /// altura apoyar, sin depender de que las mallas esten generadas.
        /// </summary>
        public void CalcularEjes()
        {
            ControladorTecho c = Controlador;
            if (c == null || !c.GeometriaLista) { _ejeXNegativo = null; _ejeXPositivo = null; return; }

            _ejeXNegativo = CalcularEje(c, false);
            _ejeXPositivo = CalcularEje(c, true);
        }

        private Vector3[] CalcularEje(ControladorTecho c, bool ladoPositivo)
        {
            RectaViga recta = ladoPositivo
                ? c.PerimetroTecho.RectaXPositivo
                : c.PerimetroTecho.RectaXNegativo;

            float semiLargo = c.PerimetroTecho.SemiLargo;
            int n = Mathf.Max(8, segmentosPorLado);

            var eje = new Vector3[n + 1];
            for (int i = 0; i <= n; i++)
            {
                float z = Mathf.Lerp(-semiLargo, semiLargo, (float)i / n);
                eje[i] = new Vector3(recta.XenZ(z), AlturaEnZ(c, ladoPositivo, z), z);
            }

            return eje;
        }

        /// <summary>
        /// Altura de la viga a la cota Z: interpola entre las cabezas de tensor publicadas
        /// por la platea de ese lado. Mas alla del ultimo anclaje —la zona de codo— mantiene
        /// esa cota, sin bajar acompañando la grada.
        ///
        /// Es la unica fuente de verdad sobre la altura de la viga: los soportes de codo y
        /// las vigas finales la consultan en vez de recalcularla.
        /// </summary>
        public float AlturaEnZ(ControladorTecho c, bool ladoPositivo, float z)
        {
            IReadOnlyList<AnclajeTecho> anclajes = c.Registro.Anclajes;

            float zInferior = float.NegativeInfinity, yInferior = 0f;
            float zSuperior = float.PositiveInfinity, ySuperior = 0f;
            bool hayInferior = false, haySuperior = false;

            for (int i = 0; i < anclajes.Count; i++)
            {
                Vector3 p = anclajes[i].posicion;
                if ((p.x > 0f) != ladoPositivo) continue;

                if (p.z <= z && p.z > zInferior) { zInferior = p.z; yInferior = p.y; hayInferior = true; }
                if (p.z >= z && p.z < zSuperior) { zSuperior = p.z; ySuperior = p.y; haySuperior = true; }
            }

            float altura;
            if (hayInferior && haySuperior)
            {
                float span = zSuperior - zInferior;
                altura = span < 1e-4f
                    ? yInferior
                    : Mathf.Lerp(yInferior, ySuperior, (z - zInferior) / span);
            }
            else if (hayInferior) altura = yInferior;
            else if (haySuperior) altura = ySuperior;
            else altura = 0f;

            return altura + elevacionSobreTensor;
        }

        /// <summary>Altura de la viga a la cota Z del lado indicado, usando el controlador
        /// propio. Es lo que consultan los otros componentes.</summary>
        public float AlturaEnZ(bool ladoPositivo, float z)
        {
            ControladorTecho c = Controlador;
            return c == null || !c.GeometriaLista ? 0f : AlturaEnZ(c, ladoPositivo, z);
        }

        public Vector3[] Eje(bool ladoPositivo)
        {
            if (!EjesListos) CalcularEjes();
            return ladoPositivo ? _ejeXPositivo : _ejeXNegativo;
        }

        // ------------------------------------------------------------------
        //  Mallas
        // ------------------------------------------------------------------

        public void Descartar()
        {
            if (_raiz == null) return;

            if (Application.isPlaying) Destroy(_raiz);
            else DestroyImmediate(_raiz);

            _raiz = null;
        }

        public void Generar(Transform padre)
        {
            if (!Application.isPlaying)
            {
                Debug.LogWarning("[Techo] La viga longitudinal solo se genera en modo juego.", this);
                return;
            }

            ControladorTecho c = Controlador;
            if (c == null || !c.GeometriaLista)
            {
                Debug.LogError("[Techo] La geometria del techo no esta lista.", this);
                return;
            }

            CalcularEjes();
            Descartar();

            _raiz = new GameObject("Viga_Longitudinal_Techo");
            _raiz.transform.SetParent(padre != null ? padre : transform, false);

            CrearViga(_ejeXNegativo, "Viga_X-");
            CrearViga(_ejeXPositivo, "Viga_X+");

            Debug.Log($"[Techo] Viga longitudinal generada: 2 lados de " +
                      $"{_ejeXNegativo.Length} tramos.", this);
        }

        private void CrearViga(Vector3[] eje, string nombre)
        {
            if (eje == null || eje.Length < 2) return;

            var vertices = new List<Vector3>(eje.Length * 4);
            var triangulos = new List<int>(eje.Length * 24);

            float g = ancho * 0.5f;
            float h = alto * 0.5f;

            for (int i = 0; i < eje.Length; i++)
            {
                Vector3 direccion = i == 0
                    ? (eje[1] - eje[0]).normalized
                    : (eje[i] - eje[i - 1]).normalized;

                Vector3 lateral = Vector3.Cross(direccion, Vector3.up);
                if (lateral.sqrMagnitude < 1e-6f) lateral = Vector3.right;
                lateral = lateral.normalized * g;

                Vector3 vertical = Vector3.up * h;

                vertices.Add(eje[i] - lateral - vertical);
                vertices.Add(eje[i] - lateral + vertical);
                vertices.Add(eje[i] + lateral + vertical);
                vertices.Add(eje[i] + lateral - vertical);
            }

            for (int i = 0; i < eje.Length - 1; i++)
            {
                int a = i * 4;
                int b = (i + 1) * 4;

                for (int c = 0; c < 4; c++)
                {
                    int c1 = c;
                    int c2 = (c + 1) % 4;
                    triangulos.AddRange(new[] { a + c1, a + c2, b + c1, a + c2, b + c2, b + c1 });
                }
            }

            triangulos.AddRange(new[] { 0, 2, 1, 0, 3, 2 });
            int u = (eje.Length - 1) * 4;
            triangulos.AddRange(new[] { u, u + 1, u + 2, u, u + 2, u + 3 });

            var mesh = new Mesh();
            mesh.vertices = vertices.ToArray();
            mesh.triangles = triangulos.ToArray();
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
