using System;
using System.Collections.Generic;
using UnityEngine;

namespace Estadio.Techo
{
    /// <summary>
    /// Un puente rigido del Diseno 2. A diferencia del puente de cables del Diseno 1, este
    /// tiene volumen: son dos celosias paralelas separadas unos metros y unidas por
    /// travesanos, con canto importante en el centro.
    ///
    /// Van por ENCIMA de la membrana, no por debajo, y son el elemento del que cuelga todo
    /// lo demas.
    /// </summary>
    [Serializable]
    public struct DefinicionPuenteRigido
    {
        public string id;
        [Tooltip("Cota Z del puente. Los exteriores coinciden con el extremo del techo; los " +
                 "interiores quedan unos metros mas adentro.")]
        public float z;
        [Tooltip("True si va tapado por un recubrimiento. Los exteriores del Diseno 2 no se " +
                 "ven: quedan detras de un cerramiento.")]
        public bool conRecubrimiento;
    }

    /// <summary>
    /// Genera los cuatro puentes rigidos del Diseno 2 y sus recubrimientos.
    ///
    /// Se apoyan en las dos vigas longitudinales, cruzando el estadio por lo ancho, y su
    /// cuerda superior corre a cota fija: son ellos los que fijan la cota del conjunto, al
    /// reves que en el Diseno 1 donde todo colgaba de los cables.
    /// </summary>
    [RequireComponent(typeof(ControladorTecho))]
    public sealed class PuentesRigidosTecho : MonoBehaviour
    {
        [Header("Ubicacion")]
        [Tooltip("Cuanto se retiran los puentes interiores respecto del borde del vano, " +
                 "hacia las cabeceras.")]
        [SerializeField] private float retiroPuenteInterior = 2.5f;
        [Tooltip("Cuanto sube la cuerda superior del puente respecto de la tela. Van por " +
                 "encima de la membrana, no por debajo.")]
        [SerializeField] private float alturaSobreLaTela = 1.5f;

        [Header("Seccion")]
        [Tooltip("Separacion entre las dos celosias paralelas que forman cada puente.")]
        [SerializeField] private float separacionEntreCaras = 3f;
        [Tooltip("Canto en el centro del vano. En los apoyos se reduce a cantoEnApoyos.")]
        [SerializeField] private float cantoMaximo = 6f;
        [SerializeField] private float cantoEnApoyos = 2f;
        [Tooltip("Panza de la cuerda inferior, relativa a la luz. Acompaña la de la membrana.")]
        [SerializeField] private float flechaRelativa = 0.03f;

        [Header("Celosia")]
        [SerializeField] private float diametroCordon = 0.30f;
        [SerializeField] private float diametroDiagonal = 0.18f;
        [SerializeField] private float diametroTravesano = 0.20f;
        [SerializeField, Range(6, 40)] private int paneles = 20;
        [SerializeField, Range(4, 10)] private int ladosTubo = 6;
        [SerializeField] private Material materialCelosia;

        [Header("Recubrimiento")]
        [Tooltip("Cerramiento que tapa los puentes exteriores. Si queda vacio no se genera.")]
        [SerializeField] private Material materialRecubrimiento;
        [SerializeField] private float holguraRecubrimiento = 0.4f;

        private ControladorTecho _controlador;
        private VigaLongitudinalTecho _viga;
        private GameObject _raiz;

        public bool Generado => _raiz != null;
        public int PuentesGenerados { get; private set; }

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
            PuentesGenerados = 0;
        }

        public void Generar(Transform padre)
        {
            if (!Application.isPlaying)
            {
                Debug.LogWarning("[Techo] Los puentes rigidos solo se generan en modo juego.", this);
                return;
            }

            ControladorTecho c = Controlador;
            if (c == null || !c.GeometriaLista)
            {
                Debug.LogError("[Techo] La geometria del techo no esta lista.", this);
                return;
            }

            if (_viga == null) _viga = GetComponent<VigaLongitudinalTecho>();
            if (_viga == null)
            {
                Debug.LogError("[Techo] Falta el componente VigaLongitudinalTecho.", this);
                return;
            }

            Descartar();

            _raiz = new GameObject("Puentes_Rigidos");
            _raiz.transform.SetParent(padre != null ? padre : transform, false);

            float semiVanoZ = c.Borde.Parametros.SemiVanoZ;
            float semiLargo = c.PerimetroTecho.SemiLargo;

            foreach (int signo in new[] { -1, 1 })
            {
                string sufijo = signo < 0 ? "Z-" : "Z+";

                GenerarPuente(c, signo * (semiVanoZ + retiroPuenteInterior),
                              $"puente_int_{sufijo}", false);

                GenerarPuente(c, signo * semiLargo, $"puente_ext_{sufijo}", true);
            }

            Debug.Log($"[Techo] {PuentesGenerados} puentes rigidos generados.", this);
        }

        // ------------------------------------------------------------------
        //  Un puente
        // ------------------------------------------------------------------

        private void GenerarPuente(ControladorTecho c, float z, string id, bool conRecubrimiento)
        {
            float xNeg = c.PerimetroTecho.RectaXNegativo.XenZ(z);
            float xPos = c.PerimetroTecho.RectaXPositivo.XenZ(z);

            float yNeg = _viga.AlturaEnZ(c, false, z) + alturaSobreLaTela;
            float yPos = _viga.AlturaEnZ(c, true, z) + alturaSobreLaTela;

            float luz = Mathf.Abs(xPos - xNeg);
            if (luz < 5f) return;

            var contenedor = new GameObject(id);
            contenedor.transform.SetParent(_raiz.transform, false);

            // Las dos caras, separadas en Z. Cada una es una celosia completa.
            float mitad = separacionEntreCaras * 0.5f;

            Vector3[] superiorA = MuestrearCuerdaSuperior(xNeg, xPos, yNeg, yPos, z - mitad);
            Vector3[] inferiorA = MuestrearCuerdaInferior(superiorA, luz);

            Vector3[] superiorB = MuestrearCuerdaSuperior(xNeg, xPos, yNeg, yPos, z + mitad);
            Vector3[] inferiorB = MuestrearCuerdaInferior(superiorB, luz);

            CrearCelosia(superiorA, inferiorA, $"{id}_caraA", contenedor.transform);
            CrearCelosia(superiorB, inferiorB, $"{id}_caraB", contenedor.transform);

            // Travesanos: unen las dos caras y son lo que le da volumen al conjunto.
            for (int i = 0; i < superiorA.Length; i++)
            {
                CrearTubo(new[] { superiorA[i], superiorB[i] }, diametroTravesano,
                          $"{id}_trav_sup_{i}", contenedor.transform, materialCelosia);

                if (i % 2 == 0)
                    CrearTubo(new[] { inferiorA[i], inferiorB[i] }, diametroTravesano,
                              $"{id}_trav_inf_{i}", contenedor.transform, materialCelosia);
            }

            if (conRecubrimiento && materialRecubrimiento != null)
                CrearRecubrimiento(superiorA, inferiorA, superiorB, inferiorB,
                                   id, contenedor.transform);

            PuentesGenerados++;
        }

        /// <summary>Cuerda superior: recta a cota fija, interpolando entre los dos apoyos.</summary>
        private Vector3[] MuestrearCuerdaSuperior(float xNeg, float xPos, float yNeg, float yPos, float z)
        {
            var puntos = new Vector3[paneles + 1];

            for (int i = 0; i <= paneles; i++)
            {
                float u = (float)i / paneles;
                puntos[i] = new Vector3(Mathf.Lerp(xNeg, xPos, u), Mathf.Lerp(yNeg, yPos, u), z);
            }

            return puntos;
        }

        /// <summary>
        /// Cuerda inferior: cuelga con panza y con canto variable. El canto sigue el diagrama
        /// de momentos —maximo al centro, minimo en los apoyos— y la panza acompaña la de la
        /// membrana, que corre paralela.
        /// </summary>
        private Vector3[] MuestrearCuerdaInferior(Vector3[] superior, float luz)
        {
            var puntos = new Vector3[superior.Length];
            float flecha = flechaRelativa * luz;

            for (int i = 0; i < superior.Length; i++)
            {
                float u = (float)i / (superior.Length - 1);

                float canto = cantoEnApoyos + (cantoMaximo - cantoEnApoyos) * 4f * u * (1f - u);
                float panza = 4f * flecha * u * (1f - u);

                puntos[i] = superior[i] - Vector3.up * (canto + panza);
            }

            return puntos;
        }

        /// <summary>Una cara: los dos cordones mas las diagonales que los triangulan.</summary>
        private void CrearCelosia(Vector3[] superior, Vector3[] inferior, string nombre, Transform padre)
        {
            CrearTubo(superior, diametroCordon, $"{nombre}_cordon_sup", padre, materialCelosia);
            CrearTubo(inferior, diametroCordon, $"{nombre}_cordon_inf", padre, materialCelosia);

            for (int i = 0; i < superior.Length - 1; i++)
            {
                // Montante y diagonal alternada: da el zigzag tipico del reticulado.
                CrearTubo(new[] { superior[i], inferior[i] }, diametroDiagonal,
                          $"{nombre}_montante_{i}", padre, materialCelosia);

                Vector3 desde = i % 2 == 0 ? inferior[i] : superior[i];
                Vector3 hasta = i % 2 == 0 ? superior[i + 1] : inferior[i + 1];

                CrearTubo(new[] { desde, hasta }, diametroDiagonal,
                          $"{nombre}_diag_{i}", padre, materialCelosia);
            }

            int ultimo = superior.Length - 1;
            CrearTubo(new[] { superior[ultimo], inferior[ultimo] }, diametroDiagonal,
                      $"{nombre}_montante_fin", padre, materialCelosia);
        }

        /// <summary>
        /// Cerramiento que envuelve el puente. Los exteriores del Diseno 2 no muestran su
        /// celosia: quedan detras de un recubrimiento que sigue su contorno.
        /// </summary>
        private void CrearRecubrimiento(Vector3[] supA, Vector3[] infA,
                                        Vector3[] supB, Vector3[] infB,
                                        string id, Transform padre)
        {
            int n = supA.Length;
            var vertices = new List<Vector3>(n * 4);
            var triangulos = new List<int>(n * 24);

            float h = holguraRecubrimiento;

            for (int i = 0; i < n; i++)
            {
                Vector3 haciaB = (supB[i] - supA[i]).normalized * h;

                vertices.Add(supA[i] - haciaB + Vector3.up * h);
                vertices.Add(supB[i] + haciaB + Vector3.up * h);
                vertices.Add(infB[i] + haciaB - Vector3.up * h);
                vertices.Add(infA[i] - haciaB - Vector3.up * h);
            }

            for (int i = 0; i < n - 1; i++)
            {
                int a = i * 4;
                int b = (i + 1) * 4;

                for (int k = 0; k < 4; k++)
                {
                    int k1 = k;
                    int k2 = (k + 1) % 4;
                    triangulos.AddRange(new[] { a + k1, a + k2, b + k1, a + k2, b + k2, b + k1 });
                }
            }

            triangulos.AddRange(new[] { 0, 2, 1, 0, 3, 2 });
            int u = (n - 1) * 4;
            triangulos.AddRange(new[] { u, u + 1, u + 2, u, u + 2, u + 3 });

            var mesh = new Mesh();
            mesh.vertices = vertices.ToArray();
            mesh.triangles = triangulos.ToArray();
            mesh.RecalculateNormals();

            var go = new GameObject($"{id}_recubrimiento");
            go.transform.SetParent(padre, false);
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;
            go.AddComponent<MeshFilter>().mesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = materialRecubrimiento;
        }

        // ------------------------------------------------------------------

        private void CrearTubo(Vector3[] eje, float diametro, string nombre,
                               Transform padre, Material material)
        {
            if (eje == null || eje.Length < 2) return;
            if (Vector3.Distance(eje[0], eje[eje.Length - 1]) < 0.05f) return;

            int lados = Mathf.Max(4, ladosTubo);
            float radio = diametro * 0.5f;

            var vertices = new List<Vector3>(eje.Length * lados);
            var triangulos = new List<int>(eje.Length * lados * 6);

            for (int i = 0; i < eje.Length; i++)
            {
                Vector3 tangente = i == 0
                    ? (eje[1] - eje[0]).normalized
                    : (eje[i] - eje[i - 1]).normalized;

                Vector3 normal = Vector3.Cross(tangente, Vector3.up);
                if (normal.sqrMagnitude < 1e-6f) normal = Vector3.right;
                normal.Normalize();

                Vector3 binormal = Vector3.Cross(tangente, normal).normalized;

                for (int l = 0; l < lados; l++)
                {
                    float ang = 2f * Mathf.PI * l / lados;
                    vertices.Add(eje[i] + (normal * Mathf.Cos(ang) + binormal * Mathf.Sin(ang)) * radio);
                }
            }

            for (int i = 0; i < eje.Length - 1; i++)
            {
                int a = i * lados;
                int b = (i + 1) * lados;

                for (int l = 0; l < lados; l++)
                {
                    int l1 = l;
                    int l2 = (l + 1) % lados;
                    triangulos.AddRange(new[] { a + l1, a + l2, b + l1, a + l2, b + l2, b + l1 });
                }
            }

            var mesh = new Mesh();
            mesh.vertices = vertices.ToArray();
            mesh.triangles = triangulos.ToArray();
            mesh.RecalculateNormals();

            var go = new GameObject(nombre);
            go.transform.SetParent(padre, false);
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;
            go.AddComponent<MeshFilter>().mesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = material;
        }
    }
}
