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
    /// Superficie horizontal a cota fija. El Diseno 2 no tiene cables de los que derivar
    /// el borde, asi que hasta modelar su parrilla reticulada se usa esto.
    /// </summary>
    public sealed class SuperficiePlana : ISuperficieCables
    {
        private readonly float _altura;
        public SuperficiePlana(float altura) { _altura = altura; }
        public bool TryAltura(float x, float z, out float altura) { altura = _altura; return true; }
    }

    /// <summary>
    /// Dueno de la geometria del techo. La calcula una sola vez y la expone; el visor de
    /// Gizmos y el generador de mallas la consumen sin recalcularla, que es lo que evita
    /// que lo que se ve en escena y lo que se ve en juego diverjan.
    ///
    /// Tambien maneja el ciclo del boton "Ver con techo": la geometria se calcula siempre
    /// —cuesta milisegundos y hace que los errores de configuracion aparezcan al cargar—
    /// pero las mallas solo se generan cuando el usuario las pide.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ControladorTecho : MonoBehaviour
    {
        [Header("Origen")]
        [Tooltip("Define el centro del campo y la orientacion del estadio. Toda la " +
                 "geometria del techo vive en este sistema de coordenadas.")]
        [SerializeField] private Transform origenTecho;
        [SerializeField] private EstadioConfigurator configurador;

        [Header("Perimetro del estadio")]
        [Tooltip("Z es el eje LARGO del campo (de arco a arco); X es el ANCHO.")]
        [SerializeField] private float semiejeX = 91f;
        [SerializeField] private float semiejeZ = 90f;
        [SerializeField] private float exponenteCodos = 5f;

        [Header("Anclajes sinteticos (mientras no publiquen las tribunas)")]
        [SerializeField] private bool usarAnclajesSinteticos = false;
        [SerializeField] private float separacionVigas = 7.5f;
        [SerializeField] private float alturaCoronamientoLateral = 44f;
        [SerializeField] private float alturaCoronamientoCabecera = 30f;

        [Header("Diseno")]
        [SerializeField] private DisenoTecho diseno = DisenoTecho.Diseno1Membrana;

        [Header("Parametros")]
        [SerializeField] private ParametrosBordeInterior parametrosBorde = ParametrosBordeInterior.PorDefecto;
        [SerializeField] private ParametrosTendido parametrosTendido = ParametrosTendido.PorDefecto;
        [SerializeField] private ParametrosMembrana parametrosMembrana = ParametrosMembrana.PorDefecto;

        [Header("Generacion por etapas")]
        [Tooltip("Cede un frame entre etapas para que no se note el tiron. Ademas queda " +
                 "bien: el techo se arma por capas delante del usuario.")]
        [SerializeField] private bool generarPorEtapas = true;

        private PerimetroSuperelipse _perimetro;
        private RegistroAnclajesTecho _registro;
        private BordeInteriorTecho _borde;
        private MarcoRigidoTecho _marco;
        private TendidoCables _tendido;
        private MembranaTecho _membrana;

        private GeneradorMallasTecho _generador;

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

        public PerimetroSuperelipse Perimetro => _perimetro;
        public RegistroAnclajesTecho Registro => _registro;
        public BordeInteriorTecho Borde => _borde;
        public MarcoRigidoTecho Marco => _marco;
        public TendidoCables Tendido => _tendido;
        public MembranaTecho Membrana => _membrana;

        /// <summary>Transformacion de coordenadas locales del techo al mundo.</summary>
        public Matrix4x4 MatrizEstadio => origenTecho != null
            ? origenTecho.localToWorldMatrix
            : Matrix4x4.identity;

        public bool TechoVisible => _generador != null && _generador.Generado;

        /// <summary>Se dispara cuando el techo aparece o desaparece. El modo de
        /// visibilidad necesita enterarse: un espectador ve cosas muy distintas con y
        /// sin techo encima.</summary>
        public event Action<bool> TechoCambio;

        // ------------------------------------------------------------------
        //  Construccion de la geometria
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
                _perimetro = new PerimetroSuperelipse(semiejeX, semiejeZ, exponenteCodos);
                _registro = ObtenerRegistro();
                if (!_registro.IndiceValido) _registro.Indexar(_perimetro);

                _borde = new BordeInteriorTecho(parametrosBorde);

                if (diseno == DisenoTecho.Diseno1Membrana)
                {
                    // Los transversales definen la superficie; el borde lee su altura de
                    // ellos; recien despues se parten los cables y se tienden los
                    // longitudinales.
                    _tendido = new TendidoCables(parametrosTendido);
                    _tendido.ConstruirTransversales(_perimetro, _registro);

                    _borde.Construir(_tendido);

                    _marco = new MarcoRigidoTecho(DescriptorMarco.Diseno1(_borde));
                    _marco.Construir(_perimetro, _registro, _borde);

                    _tendido.Completar(_perimetro, _registro, _borde, _marco);

                    _membrana = new MembranaTecho(parametrosMembrana);
                    _membrana.Construir(_perimetro, _registro, _borde, _tendido);
                }
                else
                {
                    _tendido = null;
                    _membrana = null;

                    _borde.Construir(new SuperficiePlana(_registro.AlturaMaxima));

                    _marco = new MarcoRigidoTecho(DescriptorMarco.Diseno2(_borde, _perimetro));
                    _marco.Construir(_perimetro, _registro, _borde);
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
        /// Coronamiento inventado: alto en los laterales, bajo en las cabeceras. Sirve para
        /// calibrar antes de conectar los generadores de tribuna, pero es simetrico por
        /// construccion y por lo tanto no muestra el desnivel real del estadio.
        /// </summary>
        private void PublicarAnclajesSinteticos(RegistroAnclajesTecho registro)
        {
            Vector2[] puntos = _perimetro.MuestrearPorSeparacion(separacionVigas, out _);

            for (int i = 0; i < puntos.Length; i++)
            {
                float t = _perimetro.TDePunto(puntos[i]);
                float mezcla = Mathf.Abs(Mathf.Cos(t));   // 0 en cabecera, 1 en lateral
                mezcla = mezcla * mezcla * (3f - 2f * mezcla);

                float altura = Mathf.Lerp(alturaCoronamientoCabecera, alturaCoronamientoLateral, mezcla);

                Vector2 normal = _perimetro.NormalExterior(t);
                Vector3 ejeViga = new Vector3(normal.x, 1.6f, normal.y).normalized;

                registro.Publicar(new Vector3(puntos[i].x, altura, puntos[i].y),
                                  ejeViga, "sintetico", i);
            }
        }

        // ------------------------------------------------------------------
        //  Boton "Ver con techo"
        // ------------------------------------------------------------------

        [ContextMenu("Alternar")]
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
                TechoCambio?.Invoke(true);
            }
        }

        [ContextMenu("Ocultar Techo")]
        public void Ocultar()
        {
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

            Debug.Log(_perimetro.Diagnostico(), this);
            Debug.Log(_registro.Diagnostico(), this);
            Debug.Log(_borde.Diagnostico(), this);
            Debug.Log(_marco.Diagnostico(), this);
            if (_tendido != null) Debug.Log(_tendido.Diagnostico(), this);
            if (_membrana != null) Debug.Log(_membrana.Diagnostico(), this);

            var mensajes = new List<string>();

            _registro.Validar(ParametrosValidacionAnclajes.PorDefecto, mensajes);
            _borde.Validar(_perimetro, _registro, mensajes);
            _marco.Validar(ParametrosValidacionMarco.PorDefecto, _borde, mensajes);
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
