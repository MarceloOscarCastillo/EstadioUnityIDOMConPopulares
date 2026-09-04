using System;
using System.Collections.Generic;
using UnityEngine;

namespace Estadio.Techo
{
    /// <summary>
    /// Convierte la geometria del techo en objetos de escena. Por ahora los cuatro elementos
    /// del borde del vano: barre el prefab modular a lo largo de cada eje.
    ///
    /// Solo corre en modo juego. Asi los objetos generados no pueden quedar serializados en
    /// el archivo de escena.
    /// </summary>
    public sealed class GeneradorMallasTecho : MonoBehaviour
    {
        [Header("Prefabs")]
        [Tooltip("Modulo repetible de la estructura tubular. Eje del barrido en X, " +
                 "arrancando en x=0 y terminando en x=longitudModuloTubular.")]
        [SerializeField] private GameObject prefabTubular;
        [SerializeField] private float longitudModuloTubular = 2.0f;

        [Tooltip("Modulo del puente de cabecera. Solo se usa si el puente se genera con " +
                 "prefab; si esta vacio se genera procedural con cables.")]
        [SerializeField] private GameObject prefabPuente;
        [SerializeField] private float longitudModuloPuente = 2.0f;

        [Header("Puente de cables (Diseno 1)")]
        [Tooltip("En el Diseno 1 el puente son dos cables paralelos separados el canto del " +
                 "elemento, unidos por pendolas verticales. No lleva prefab: la forma sale " +
                 "de la panza, que a su vez depende de la tension de la membrana.")]
        [SerializeField] private bool puenteConCables = true;
        [SerializeField] private float diametroCablePuente = 0.12f;
        [SerializeField] private int pendolasPorPuente = 10;
        [SerializeField] private float diametroPendola = 0.08f;
        [SerializeField, Range(4, 12)] private int ladosCilindro = 6;
        [SerializeField] private Material materialCables;

        [Header("Esquinas del vano")]
        [Tooltip("Giro a partir del cual se corta el barrido y se deja el hueco de la " +
                 "esquina. Con el vano casi rectangular el giro se concentra en pocos metros.")]
        [SerializeField] private float anguloMaximoEntreModulos = 20f;

        [Header("Membrana")]
        [SerializeField] private bool generarMembrana = true;
        [Tooltip("Material del pano. Conviene un shader Lit en modo Transparent con Render " +
                 "Face en Both: asi se ve de los dos lados sin duplicar geometria.")]
        [SerializeField] private Material materialMembrana;
        [Tooltip("Material del faldon, mas transparente que el del pano.")]
        [SerializeField] private Material materialFaldon;

        [Header("Salida")]
        [SerializeField] private Transform origenTecho;
        [Tooltip("El batching estatico no se aplica a la membrana: al combinarla con el resto " +
                 "se pierde el orden de dibujo que necesitan los materiales transparentes.")]
        [SerializeField] private bool combinarEstatico = true;

        private GameObject _raiz;

        public bool Generado => _raiz != null;
        public int ModulosInstanciados { get; private set; }
        public int EsquinasSalteadas { get; private set; }

        // ------------------------------------------------------------------

        public void Descartar()
        {
            if (_raiz == null) return;

            if (Application.isPlaying) Destroy(_raiz);
            else DestroyImmediate(_raiz);

            _raiz = null;
            ModulosInstanciados = 0;
            EsquinasSalteadas = 0;
        }

        public void MostrarUOcultar(bool visible)
        {
            if (_raiz != null) _raiz.SetActive(visible);
        }

        public void Generar(MarcoRigidoTecho marco, MembranaTecho membrana)
        {
            if (!Application.isPlaying)
            {
                Debug.LogWarning("[Techo] El generador de mallas solo corre en modo juego.", this);
                return;
            }

            if (marco == null) throw new ArgumentNullException(nameof(marco));
            if (prefabTubular == null)
            {
                Debug.LogError("[Techo] Falta asignar el prefab tubular.", this);
                return;
            }

            Descartar();

            _raiz = new GameObject("Techo_Generado");
            _raiz.transform.SetParent(origenTecho != null ? origenTecho : transform, false);

            foreach (ElementoBordeConstruido elemento in marco.Elementos)
                BarrerElemento(elemento);

            if (combinarEstatico)
            {
                foreach (Transform hijo in _raiz.GetComponentsInChildren<Transform>())
                    if (hijo.gameObject != _raiz) hijo.gameObject.isStatic = true;

                StaticBatchingUtility.Combine(_raiz);
            }

            // La membrana va despues del batching y fuera de el: es transparente, y Unity
            // ordena el dibujo por objeto. Combinarla con la estructura opaca mezclaria las
            // dos colas de render.
            if (generarMembrana && membrana != null && membrana.Construida)
                GenerarMembrana(membrana);

            Debug.Log($"[Techo] {ModulosInstanciados} modulos instanciados, " +
                      $"{EsquinasSalteadas} tramos de esquina salteados.", this);
        }

        // ------------------------------------------------------------------
        //  Barrido
        // ------------------------------------------------------------------

        private void BarrerElemento(ElementoBordeConstruido elemento)
        {
            Vector3[] eje = elemento.eje;
            if (eje == null || eje.Length < 2) return;

            bool esPuente = elemento.tipo == TipoElementoBorde.PuenteCabecera;

            var contenedor = new GameObject($"Borde_{elemento.id}");
            contenedor.transform.SetParent(_raiz.transform, false);

            // El puente del Diseno 1 no es un reticulado: son dos cables paralelos con
            // pendolas. Su forma sale de la panza del eje, que depende de la tension de la
            // membrana, asi que no puede venir de un prefab rigido.
            if (esPuente && puenteConCables)
            {
                GenerarPuenteDeCables(elemento, contenedor.transform);
                return;
            }

            GameObject prefab = esPuente && prefabPuente != null ? prefabPuente : prefabTubular;
            float longitudModulo = esPuente && prefabPuente != null
                ? longitudModuloPuente
                : longitudModuloTubular;

            // Se acumulan segmentos consecutivos mientras la direccion se mantenga estable.
            var tramo = new List<Vector3> { eje[0] };
            Vector3 direccionAnterior = (eje[1] - eje[0]).normalized;

            for (int i = 1; i < eje.Length; i++)
            {
                Vector3 direccion = (eje[i] - eje[i - 1]).normalized;

                if (Vector3.Angle(direccion, direccionAnterior) > anguloMaximoEntreModulos)
                {
                    BarrerTramoRecto(tramo, contenedor.transform, prefab, longitudModulo);
                    EsquinasSalteadas++;
                    tramo.Clear();
                }

                tramo.Add(eje[i]);
                direccionAnterior = direccion;
            }

            BarrerTramoRecto(tramo, contenedor.transform, prefab, longitudModulo);
        }

        /// <summary>
        /// La cantidad de modulos se calcula por tramo y la escala en X se ajusta levemente
        /// para que cierre exacto: es mucho menos visible que dejar un resto sin cubrir.
        /// </summary>
        private void BarrerTramoRecto(List<Vector3> puntos, Transform padre,
                                      GameObject prefab, float longitudModulo)
        {
            if (puntos.Count < 2) return;

            float longitud = 0f;
            for (int i = 1; i < puntos.Count; i++)
                longitud += Vector3.Distance(puntos[i - 1], puntos[i]);

            if (longitud < longitudModulo * 0.5f) return;

            int cantidad = Mathf.Max(1, Mathf.RoundToInt(longitud / longitudModulo));
            float escalaX = (longitud / cantidad) / longitudModulo;

            for (int i = 0; i < cantidad; i++)
            {
                Vector3 inicio = PuntoEnPolilinea(puntos, longitud, (float)i / cantidad);
                Vector3 fin = PuntoEnPolilinea(puntos, longitud, (float)(i + 1) / cantidad);

                Vector3 direccion = fin - inicio;
                if (direccion.sqrMagnitude < 1e-6f) continue;

                GameObject modulo = Instantiate(prefab, padre);
                modulo.transform.localPosition = inicio;

                // LookRotation fija tambien el giro alrededor del eje: FromToRotation deja el
                // roll indeterminado y los modulos no empalman.
                Vector3 dir = direccion.normalized;
                modulo.transform.localRotation = Quaternion.LookRotation(dir, Vector3.up)
                                               * Quaternion.Euler(0f, -90f, 0f);

                modulo.transform.localScale = new Vector3(escalaX, 1f, 1f);

                ModulosInstanciados++;
            }
        }

        // ------------------------------------------------------------------
        //  Puente de cables
        // ------------------------------------------------------------------

        /// <summary>
        /// Dos cables paralelos separados el canto del elemento, unidos por pendolas
        /// verticales. El superior corre por el eje —a la altura de los cables que sostienen
        /// la membrana— y el inferior a la cota del cordon inferior de las tubulares.
        /// </summary>
        private void GenerarPuenteDeCables(ElementoBordeConstruido elemento, Transform padre)
        {
            Vector3[] superior = elemento.eje;

            var inferior = new Vector3[superior.Length];
            for (int i = 0; i < superior.Length; i++)
                inferior[i] = superior[i] - Vector3.up * elemento.canto;

            CrearTuboPorPolilinea(superior, diametroCablePuente, $"{elemento.id}_cable_sup", padre);
            CrearTuboPorPolilinea(inferior, diametroCablePuente, $"{elemento.id}_cable_inf", padre);

            int pendolas = Mathf.Max(2, pendolasPorPuente);
            for (int p = 0; p <= pendolas; p++)
            {
                float u = (float)p / pendolas;
                int i = Mathf.Clamp(Mathf.RoundToInt(u * (superior.Length - 1)), 0, superior.Length - 1);

                CrearTuboPorPolilinea(new[] { superior[i], inferior[i] },
                                      diametroPendola, $"{elemento.id}_pendola_{p}", padre);
            }

            ModulosInstanciados += 2 + pendolas + 1;
        }

        /// <summary>
        /// Barre una seccion circular a lo largo de una polilinea. Se genera procedural en
        /// vez de instanciar prefabs porque la directriz cambia con la tension de la membrana.
        /// </summary>
        private void CrearTuboPorPolilinea(Vector3[] eje, float diametro, string nombre, Transform padre)
        {
            if (eje == null || eje.Length < 2) return;

            int lados = Mathf.Max(4, ladosCilindro);
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
            go.AddComponent<MeshRenderer>().sharedMaterial = materialCables;
        }

        // ------------------------------------------------------------------
        //  Membrana
        // ------------------------------------------------------------------

        /// <summary>
        /// Triangula las dos rejillas que calcula MembranaTecho. No hay geometria que
        /// inventar: los vertices y las UV ya estan resueltos, y aca solo se arman las caras.
        /// </summary>
        private void GenerarMembrana(MembranaTecho membrana)
        {
            var contenedor = new GameObject("Membrana");
            contenedor.transform.SetParent(_raiz.transform, false);

            CrearMallaDeRejilla(membrana.RejillaPano, materialMembrana, "Pano",
                                contenedor.transform);

            CrearMallaDeRejilla(membrana.RejillaFaldon, materialFaldon, "Faldon",
                                contenedor.transform);
        }

        /// <summary>
        /// Arma una malla a partir de una rejilla de filas x columnas. Las columnas cierran
        /// sobre si mismas —el anillo da la vuelta completa—, asi que la ultima se une con la
        /// primera sin costura.
        /// </summary>
        private static void CrearMallaDeRejilla(RejillaSuperficie rejilla, Material material,
                                                string nombre, Transform padre)
        {
            if (rejilla.vertices == null || rejilla.vertices.Length == 0) return;
            if (rejilla.filas < 2 || rejilla.columnas < 3) return;

            int filas = rejilla.filas;
            int columnas = rejilla.columnas;

            var triangulos = new List<int>(filas * columnas * 6);

            for (int c = 0; c < columnas; c++)
            {
                int cSiguiente = (c + 1) % columnas;

                for (int f = 0; f < filas - 1; f++)
                {
                    int a = rejilla.Indice(f, c);
                    int b = rejilla.Indice(f, cSiguiente);
                    int d = rejilla.Indice(f + 1, c);
                    int e = rejilla.Indice(f + 1, cSiguiente);

                    triangulos.Add(a); triangulos.Add(b); triangulos.Add(d);
                    triangulos.Add(b); triangulos.Add(e); triangulos.Add(d);
                }
            }

            var mesh = new Mesh();

            // 2496 vertices entran de sobra en 16 bits, pero con resoluciones altas del pano
            // se pasa el limite y la malla sale vacia sin avisar.
            if (rejilla.vertices.Length > 65000)
                mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;

            mesh.vertices = rejilla.vertices;
            mesh.uv = rejilla.uv;
            mesh.triangles = triangulos.ToArray();
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            var go = new GameObject(nombre);
            go.transform.SetParent(padre, false);
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;
            go.AddComponent<MeshFilter>().mesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = material;
        }

        private static Vector3 PuntoEnPolilinea(List<Vector3> puntos, float longitudTotal, float u)
        {
            float objetivo = Mathf.Clamp01(u) * longitudTotal;
            float acumulado = 0f;

            for (int i = 1; i < puntos.Count; i++)
            {
                float tramo = Vector3.Distance(puntos[i - 1], puntos[i]);
                if (acumulado + tramo >= objetivo || i == puntos.Count - 1)
                {
                    float t = tramo > 1e-6f ? (objetivo - acumulado) / tramo : 0f;
                    return Vector3.Lerp(puntos[i - 1], puntos[i], Mathf.Clamp01(t));
                }
                acumulado += tramo;
            }

            return puntos[puntos.Count - 1];
        }
    }
}
