using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Estadio.Techo
{
    public enum DisenoTecho
    {
        Diseno1Membrana,
        Diseno2Reticulado
    }

    /// <summary>
    /// Dueno de la geometria del techo. La calcula una sola vez y la expone; el visor de
    /// Gizmos y el generador de mallas la consumen sin recalcularla.
    ///
    /// La cadena:
    ///   perimetro del estadio (superelipse)  -> coronamiento, y de ahi el faldon
    ///   perimetro del techo (dos rectas)     -> ajustado a los anclajes publicados
    ///   transversales                        -> definen la superficie
    ///   borde interior                       -> lee su altura de los transversales
    ///   marco                                -> los cuatro lados del vano sobre el borde
    ///   completar tendido                    -> parte transversales, tiende longitudinales
    ///   membrana                             -> panos y faldon
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ControladorTecho : MonoBehaviour
    {
        [Header("Origen")]
        [Tooltip("Define el centro del campo y la orientacion del estadio. Toda la " +
                 "geometria del techo vive en este sistema de coordenadas.")]
        [SerializeField] private Transform origenTecho;
        [SerializeField] private EstadioConfigurator configurador;

        [Header("Perimetro del estadio (para el coronamiento y el faldon)")]
        [Tooltip("Z es el eje LARGO del campo; X es el ANCHO.")]
        [SerializeField] private float semiejeX = 91f;
        [SerializeField] private float semiejeZ = 90f;
        [SerializeField] private float exponenteCodos = 5f;

        [Header("Anclajes sinteticos (mientras no publiquen las tribunas)")]
        [SerializeField] private bool usarAnclajesSinteticos = false;
        [SerializeField] private float separacionVigas = 5f;
        [SerializeField] private float alturaCoronamientoLateral = 44f;
        [SerializeField] private float alturaCoronamientoCabecera = 30f;

        [Header("Diseno")]
        [SerializeField] private DisenoTecho diseno = DisenoTecho.Diseno1Membrana;

        [Header("Parametros")]
        [SerializeField] private ParametrosPerimetroTecho parametrosPerimetroTecho = ParametrosPerimetroTecho.PorDefecto;
        [SerializeField] private ParametrosBordeInterior parametrosBorde = ParametrosBordeInterior.PorDefecto;
        [SerializeField] private ParametrosTendido parametrosTendido = ParametrosTendido.PorDefecto;
        [SerializeField] private ParametrosMembrana parametrosMembrana = ParametrosMembrana.PorDefecto;

        [Header("Secciones del marco")]
        [SerializeField] private float cantoTubular = 1.8f;
        [SerializeField] private float anchoTubular = 1.2f;
        [SerializeField] private float cantoPuente = 1.8f;
        [SerializeField] private float anchoPuente = 1.2f;

        [Header("Generacion por etapas")]
        [SerializeField] private bool generarPorEtapas = true;

        private PerimetroSuperelipse _perimetroEstadio;
        private PerimetroTecho _perimetroTecho;
        private RegistroAnclajesTecho _registro;
        private RegistroCoronamientos _coronamientos;
        private BordeInteriorTecho _borde;
        private MarcoRigidoTecho _marco;
        private TendidoCables _tendido;
        private MembranaTecho _membrana;

        private GeneradorMallasTecho _generador;
        private SoportesTechoCodo _soportesCodo;
        private VigasFinalesTecho _vigasFinales;

        private bool _geometriaLista;
        private string _ultimoError;
        private int _versionGeometria;

        // ------------------------------------------------------------------
        //  Acceso a la geometria
        // ------------------------------------------------------------------

        public bool GeometriaLista => _geometriaLista;
        public string UltimoError => _ultimoError;
        public int VersionGeometria => _versionGeometria;
        public DisenoTecho Diseno => diseno;

        public PerimetroSuperelipse PerimetroEstadio => _perimetroEstadio;
        public PerimetroTecho PerimetroTecho => _perimetroTecho;
        public RegistroAnclajesTecho Registro => _registro;
        public RegistroCoronamientos Coronamientos => _coronamientos;
        public BordeInteriorTecho Borde => _borde;
        public MarcoRigidoTecho Marco => _marco;
        public TendidoCables Tendido => _tendido;
        public MembranaTecho Membrana => _membrana;

        public Matrix4x4 MatrizEstadio => origenTecho != null
            ? origenTecho.localToWorldMatrix
            : Matrix4x4.identity;

        public bool TechoVisible => _generador != null && _generador.Generado;

        /// <summary>Se dispara cuando el techo aparece o desaparece. El modo de visibilidad
        /// necesita enterarse: un espectador ve cosas muy distintas con y sin techo.</summary>
        public event Action<bool> TechoCambio;

        // ------------------------------------------------------------------
        //  Construccion
        // ------------------------------------------------------------------

        private void OnEnable() => ConstruirGeometria();
        private void OnValidate() => ConstruirGeometria();

        [ContextMenu("Reconstruir geometria")]
        public void ConstruirGeometria()
        {
            _geometriaLista = false;
            _ultimoError = null;

            try
            {
                _perimetroEstadio = new PerimetroSuperelipse(semiejeX, semiejeZ, exponenteCodos);

                _registro = ObtenerRegistro();
                if (!_registro.IndiceValido) _registro.Indexar(_perimetroEstadio);

                _coronamientos = ObtenerCoronamientos();
                if (!_coronamientos.IndiceValido) _coronamientos.Indexar(_perimetroEstadio);

                _perimetroTecho = new PerimetroTecho(parametrosPerimetroTecho);
                _perimetroTecho.Construir(_registro);

                _borde = new BordeInteriorTecho(parametrosBorde);

                if (diseno == DisenoTecho.Diseno1Membrana)
                {
                    _tendido = new TendidoCables(parametrosTendido);
                    _tendido.ConstruirTransversales(_perimetroTecho, _registro);

                    _borde.Construir(_tendido);

                    _marco = new MarcoRigidoTecho(
                        DescriptorMarco.Diseno1(cantoTubular, anchoTubular, cantoPuente, anchoPuente));
                    _marco.Construir(_borde);

                    _tendido.Completar(_perimetroTecho, _borde);

                    _membrana = new MembranaTecho(parametrosMembrana);
                    _membrana.Construir(_perimetroEstadio, _perimetroTecho, _coronamientos,
                                        _borde, _tendido);
                }
                else
                {
                    // El Diseno 2 tiene parrilla reticulada, no cables. Hasta modelarla, el
                    // borde se apoya en una superficie plana provisoria.
                    _tendido = null;
                    _membrana = null;

                    _borde.Construir(new SuperficiePlana(_registro.AlturaMaxima));

                    _marco = new MarcoRigidoTecho(
                        DescriptorMarco.Diseno1(cantoTubular, anchoTubular, cantoPuente, anchoPuente));
                    _marco.Construir(_borde);
                }

                _geometriaLista = true;
                _versionGeometria++;
            }
            catch (Exception e)
            {
                _ultimoError = e.Message;
                Debug.LogError($"[Techo] {e}", this);
            }
        }

        private RegistroAnclajesTecho ObtenerRegistro()
        {
            if (!usarAnclajesSinteticos && configurador != null && configurador.RegistroTecho != null)
            {
                RegistroAnclajesTecho real = configurador.RegistroTecho;

                if (real.CantidadPublicados == 0)
                    throw new InvalidOperationException(
                        "El registro del configurador esta vacio. Aplicar una variante desde " +
                        "el EstadioConfigurator antes de reconstruir el techo.");

                return real;
            }

            var sintetico = new RegistroAnclajesTecho();
            PublicarAnclajesSinteticos(sintetico);
            return sintetico;
        }

        /// <summary>
        /// Coronamientos: el borde superior de todas las gradas, que es hasta donde baja el
        /// faldon. Lo publican todos los sectores, sostengan o no el techo.
        /// </summary>
        private RegistroCoronamientos ObtenerCoronamientos()
        {
            if (!usarAnclajesSinteticos && configurador != null &&
                configurador.RegistroCoronamientos != null &&
                configurador.RegistroCoronamientos.CantidadPublicados > 0)
            {
                return configurador.RegistroCoronamientos;
            }

            var sintetico = new RegistroCoronamientos();

            const int muestras = 160;
            float longitud = _perimetroEstadio.LongitudTotal;

            for (int i = 0; i < muestras; i++)
            {
                float s = longitud * i / muestras;
                float t = _perimetroEstadio.TDeLongitud(s);
                Vector2 xz = _perimetroEstadio.Punto(t);

                float mezcla = Mathf.Abs(Mathf.Cos(t));
                mezcla = mezcla * mezcla * (3f - 2f * mezcla);
                float altura = Mathf.Lerp(alturaCoronamientoCabecera, alturaCoronamientoLateral, mezcla);

                sintetico.Publicar(new Vector3(xz.x, altura - 2f, xz.y), "sintetico");
            }

            return sintetico;
        }

        /// <summary>
        /// Anclajes sinteticos: dos rectas paralelas a los lados, con una altura que cae hacia
        /// las cabeceras. Sirve para probar sin tribunas, pero es simetrico por construccion.
        /// </summary>
        private void PublicarAnclajesSinteticos(RegistroAnclajesTecho registro)
        {
            float semiLargo = parametrosPerimetroTecho.semiLargoTecho * 0.85f;
            int cantidad = Mathf.Max(4, Mathf.RoundToInt(2f * semiLargo / separacionVigas));

            int indice = 0;
            foreach (int signo in new[] { -1, 1 })
            {
                for (int i = 0; i <= cantidad; i++)
                {
                    float z = Mathf.Lerp(-semiLargo, semiLargo, (float)i / cantidad);

                    float mezcla = 1f - Mathf.Abs(z) / semiLargo;
                    mezcla = mezcla * mezcla * (3f - 2f * mezcla);
                    float altura = Mathf.Lerp(alturaCoronamientoCabecera, alturaCoronamientoLateral, mezcla);

                    registro.Publicar(new Vector3(signo * semiejeX, altura, z),
                                      Vector3.up, signo < 0 ? "sintetico_X-" : "sintetico_X+", indice++);
                }
            }
        }

        // ------------------------------------------------------------------
        //  Boton "Ver con techo"
        // ------------------------------------------------------------------

        [ContextMenu("Alternar techo")]
        public void Alternar()
        {
            if (TechoVisible) Ocultar();
            else Mostrar();
        }

        [ContextMenu("Mostrar techo")]
        public void Mostrar()
        {
            if (!Application.isPlaying)
            {
                Debug.LogWarning("[Techo] Las mallas solo se generan en modo juego.", this);
                return;
            }

            if (!_geometriaLista) ConstruirGeometria();
            if (!_geometriaLista)
            {
                Debug.LogError($"[Techo] No se puede generar: {_ultimoError}", this);
                return;
            }

            if (_generador == null) _generador = GetComponent<GeneradorMallasTecho>();
            if (_generador == null)
            {
                Debug.LogError("[Techo] Falta el componente GeneradorMallasTecho.", this);
                return;
            }

            if (generarPorEtapas) StartCoroutine(GenerarPorEtapas());
            else
            {
                _generador.Generar(_marco);
                GenerarSoportesCodo();
                TechoCambio?.Invoke(true);
            }
        }

        /// <summary>
        /// Los soportes de codo son del techo, no del estadio: sostienen la viga longitudinal
        /// donde ya no hay platea abajo. Por eso aparecen y desaparecen con el.
        /// </summary>
        private void GenerarSoportesCodo()
        {
            if (_soportesCodo == null) _soportesCodo = GetComponent<SoportesTechoCodo>();
            if (_soportesCodo == null) return;

            _soportesCodo.Generar(origenTecho, configurador);

            if (_vigasFinales == null) _vigasFinales = GetComponent<VigasFinalesTecho>();
            _vigasFinales?.Generar(origenTecho);
        }

        [ContextMenu("Ocultar techo")]
        public void Ocultar()
        {
            if (_soportesCodo == null) _soportesCodo = GetComponent<SoportesTechoCodo>();
            _soportesCodo?.Descartar();

            if (_vigasFinales == null) _vigasFinales = GetComponent<VigasFinalesTecho>();
            _vigasFinales?.Descartar();

            if (_generador == null) _generador = GetComponent<GeneradorMallasTecho>();
            if (_generador == null) return;

            _generador.Descartar();
            TechoCambio?.Invoke(false);
        }

        private IEnumerator GenerarPorEtapas()
        {
            yield return null;
            _generador.Generar(_marco);

            yield return null;
            GenerarSoportesCodo();

            yield return null;
            TechoCambio?.Invoke(true);
        }

        // ------------------------------------------------------------------
        //  Diagnostico
        // ------------------------------------------------------------------

        [ContextMenu("Imprimir diagnostico")]
        public void ImprimirDiagnostico()
        {
            if (!_geometriaLista) ConstruirGeometria();
            if (!_geometriaLista)
            {
                Debug.LogError($"[Techo] No se pudo construir: {_ultimoError}", this);
                return;
            }

            Debug.Log(_perimetroEstadio.Diagnostico(), this);
            Debug.Log(_registro.Diagnostico(), this);
            Debug.Log(_coronamientos.Diagnostico(), this);
            Debug.Log(_perimetroTecho.Diagnostico(), this);
            Debug.Log(_borde.Diagnostico(), this);
            Debug.Log(_marco.Diagnostico(), this);
            if (_tendido != null) Debug.Log(_tendido.Diagnostico(), this);
            if (_membrana != null) Debug.Log(_membrana.Diagnostico(), this);

            var mensajes = new List<string>();

            _registro.Validar(ParametrosValidacionAnclajes.PorDefecto, mensajes);
            _coronamientos.Validar(mensajes);
            _perimetroTecho.Validar(mensajes);
            _borde.Validar(_perimetroEstadio, _registro, mensajes);
            _marco.Validar(ParametrosValidacionMarco.PorDefecto, mensajes);
            _tendido?.Validar(mensajes);
            _membrana?.Validar(mensajes);

            if (mensajes.Count == 0)
            {
                Debug.Log("[Techo] Sin observaciones.", this);
                return;
            }

            foreach (string mensaje in mensajes)
            {
                if (mensaje.StartsWith("ERROR")) Debug.LogError($"[Techo] {mensaje}", this);
                else Debug.LogWarning($"[Techo] {mensaje}", this);
            }
        }
    }
}
