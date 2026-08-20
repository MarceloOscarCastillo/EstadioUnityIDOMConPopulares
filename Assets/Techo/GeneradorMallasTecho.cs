using System;
using System.Collections.Generic;
using UnityEngine;

namespace Estadio.Techo
{
    /// <summary>
    /// Convierte la geometria del techo en objetos de escena. Por ahora solo las
    /// estructuras longitudinales: barre el prefab modular a lo largo de cada eje.
    ///
    /// Como todo lo demas del techo, no crea nada en modo editor: solo genera cuando se
    /// lo pide el controlador del boton, en modo juego. Asi no puede quedar serializado
    /// en la escena.
    /// </summary>
    public sealed class GeneradorMallasTecho : MonoBehaviour
    {
        [Header("Prefabs")]
        [Tooltip("Modulo repetible de la estructura tubular. Eje del barrido en X, " +
                 "arrancando en x=0 y terminando en x=longitudModuloTubular.")]
        [SerializeField] private GameObject prefabTubular;
        [SerializeField] private float longitudModuloTubular = 2.0f;

        [Header("Esquinas del vano")]
        [Tooltip("Angulo de giro a partir del cual se corta el barrido y se deja el hueco " +
                 "de la esquina sin cubrir. Con el vano casi rectangular, el giro se " +
                 "concentra en pocos metros.")]
        [SerializeField] private float anguloMaximoEntreModulos = 12f;

        [Header("Salida")]
        [SerializeField] private Transform origenTecho;
        [SerializeField] private bool combinarEstatico = true;

        private GameObject _raiz;

        public bool Generado => _raiz != null;
        public int ModulosInstanciados { get; private set; }
        public int EsquinasSalteadas { get; private set; }

        // ------------------------------------------------------------------
        //  Ciclo de vida
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

        /// <summary>
        /// Genera las estructuras longitudinales del marco. Se llama solo en modo juego:
        /// si se llamara en modo editor, los objetos quedarian serializados en la escena.
        /// </summary>
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

            foreach (LongitudinalConstruido longitudinal in marco.Longitudinales)
                BarrerTubular(longitudinal);

            if (combinarEstatico)
            {
                foreach (Transform hijo in _raiz.GetComponentsInChildren<Transform>())
                    if (hijo.gameObject != _raiz) hijo.gameObject.isStatic = true;

                StaticBatchingUtility.Combine(_raiz);
            }

            Debug.Log($"[Techo] {ModulosInstanciados} modulos tubulares instanciados, " +
                      $"{EsquinasSalteadas} tramos de esquina salteados.", this);
        }

        // ------------------------------------------------------------------
        //  Barrido
        // ------------------------------------------------------------------

        /// <summary>
        /// Recorre la polilinea del eje instanciando modulos. La cantidad se calcula por
        /// tramo y la escala en X se ajusta levemente para que cierre exacto: es mucho
        /// menos visible que dejar un resto sin cubrir al final.
        ///
        /// Donde el giro entre segmentos consecutivos supera el umbral —las esquinas del
        /// vano— se corta el barrido y se deja el hueco. Una esquina no es un modulo mas
        /// de la serie sino un nudo estructural, y merece su propia pieza.
        /// </summary>
        private void BarrerTubular(LongitudinalConstruido longitudinal)
        {
            Vector3[] eje = longitudinal.eje;
            if (eje == null || eje.Length < 2) return;

            var contenedor = new GameObject($"Tubular_{longitudinal.id}");
            contenedor.transform.SetParent(_raiz.transform, false);

            // Se acumulan segmentos consecutivos mientras la direccion se mantenga estable.
            var tramo = new List<Vector3> { eje[0] };
            Vector3 direccionAnterior = (eje[1] - eje[0]).normalized;

            for (int i = 1; i < eje.Length; i++)
            {
                Vector3 direccion = (eje[i] - eje[i - 1]).normalized;

                if (Vector3.Angle(direccion, direccionAnterior) > anguloMaximoEntreModulos)
                {
                    BarrerTramoRecto(tramo, contenedor.transform);
                    EsquinasSalteadas++;
                    tramo.Clear();
                }

                tramo.Add(eje[i]);
                direccionAnterior = direccion;
            }

            BarrerTramoRecto(tramo, contenedor.transform);
        }

        private void BarrerTramoRecto(List<Vector3> puntos, Transform padre)
        {
            if (puntos.Count < 2) return;

            float longitud = 0f;
            for (int i = 1; i < puntos.Count; i++)
                longitud += Vector3.Distance(puntos[i - 1], puntos[i]);

            if (longitud < longitudModuloTubular * 0.5f) return;

            int cantidad = Mathf.Max(1, Mathf.RoundToInt(longitud / longitudModuloTubular));
            float longitudReal = longitud / cantidad;
            float escalaX = longitudReal / longitudModuloTubular;

            for (int i = 0; i < cantidad; i++)
            {
                Vector3 inicio = PuntoEnPolilinea(puntos, longitud, (float)i / cantidad);
                Vector3 fin = PuntoEnPolilinea(puntos, longitud, (float)(i + 1) / cantidad);

                Vector3 direccion = fin - inicio;
                if (direccion.sqrMagnitude < 1e-6f) continue;

                GameObject modulo = Instantiate(prefabTubular, padre);
                modulo.transform.localPosition = inicio;

                // El eje X local del modulo sigue la direccion del tramo.
                modulo.transform.localRotation = Quaternion.FromToRotation(Vector3.right, direccion.normalized);
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
