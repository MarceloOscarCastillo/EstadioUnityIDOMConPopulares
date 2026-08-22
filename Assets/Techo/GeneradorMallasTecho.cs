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

        [Tooltip("Modulo del puente de cabecera. Si queda vacio se usa el mismo que las " +
                 "tubulares.")]
        [SerializeField] private GameObject prefabPuente;
        [SerializeField] private float longitudModuloPuente = 2.0f;

        [Header("Esquinas del vano")]
        [Tooltip("Giro a partir del cual se corta el barrido y se deja el hueco de la " +
                 "esquina. Con el vano casi rectangular el giro se concentra en pocos metros.")]
        [SerializeField] private float anguloMaximoEntreModulos = 20f;

        [Header("Salida")]
        [SerializeField] private Transform origenTecho;
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

        public void Generar(MarcoRigidoTecho marco)
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
            GameObject prefab = esPuente && prefabPuente != null ? prefabPuente : prefabTubular;
            float longitudModulo = esPuente && prefabPuente != null
                ? longitudModuloPuente
                : longitudModuloTubular;

            var contenedor = new GameObject($"Borde_{elemento.id}");
            contenedor.transform.SetParent(_raiz.transform, false);

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
