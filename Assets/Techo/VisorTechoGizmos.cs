using System;
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
    /// Herramienta de calibracion. Construye toda la geometria del techo y la dibuja con
    /// Gizmos en la vista de escena.
    ///
    /// No instancia nada: los Gizmos son lineas de editor, no GameObjects, asi que esto
    /// NO puede engordar el archivo de escena ni quedar serializado. Se puede usar en modo
    /// editor sin riesgo.
    ///
    /// Si todavia no conectaste la publicacion de anclajes desde tus generadores de tribuna,
    /// dejalo en anclajes sinteticos: fabrica un coronamiento plausible (laterales altas,
    /// cabeceras bajas) para que puedas calibrar el techo desde hoy.
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public sealed class VisorTechoGizmos : MonoBehaviour
    {
        [Header("Ubicacion del estadio en la escena")]
        [Tooltip("Centro del campo de juego en coordenadas de mundo.")]
        [SerializeField] private Vector3 centroEstadio = new Vector3(136.2f, 58.1f, -505.8f);
        [Tooltip("Rotacion del estadio alrededor de Y, en grados.")]
        [SerializeField] private float rotacionYGrados = 170.42f;

        [Header("Perimetro del estadio")]
        [Tooltip("Z es el eje LARGO del campo (de arco a arco); X es el ANCHO.")]
        [SerializeField] private float semiejeX = 100f;
        [SerializeField] private float semiejeZ = 130f;
        [SerializeField] private float exponenteCodos = 4f;

        [Header("Anclajes sinteticos (mientras no publiquen las tribunas)")]
        [SerializeField] private bool usarAnclajesSinteticos = true;
        [SerializeField] private float separacionVigas = 7.5f;
        [SerializeField] private float alturaCoronamientoLateral = 38f;
        [SerializeField] private float alturaCoronamientoCabecera = 28f;

        [Header("Diseno")]
        [SerializeField] private DisenoTecho diseno = DisenoTecho.Diseno1Membrana;

        [Header("Parametros")]
        [SerializeField] private ParametrosBordeInterior parametrosBorde = ParametrosBordeInterior.PorDefecto;
        [SerializeField] private ParametrosTendido parametrosTendido = ParametrosTendido.PorDefecto;
        [SerializeField] private ParametrosMembrana parametrosMembrana = ParametrosMembrana.PorDefecto;

        [Header("Capas a dibujar")]
        [SerializeField] private bool dibujarPerimetro = true;
        [SerializeField] private bool dibujarAnclajes = true;
        [SerializeField] private bool dibujarBordeInterior = true;
        [SerializeField] private bool dibujarPuentes = true;
        [SerializeField] private bool dibujarLongitudinales = true;
        [SerializeField] private bool dibujarCablesTransversales = true;
        [SerializeField] private bool dibujarCablesLongitudinales = false;
        [SerializeField] private bool dibujarMembrana = true;
        [SerializeField] private bool dibujarFaldon = true;

        [Header("Resolucion del dibujo")]
        [SerializeField, Range(1, 8)] private int pasoDibujoMembrana = 4;
        [SerializeField, Range(1, 6)] private int pasoDibujoCables = 1;

        private PerimetroSuperelipse _perimetro;
        private RegistroAnclajesTecho _registro;
        private BordeInteriorTecho _borde;
        private MarcoRigidoTecho _marco;
        private TendidoCables _tendido;
        private MembranaTecho _membrana;

        private bool _valido;
        private string _ultimoError;

        private void OnEnable() => Reconstruir();
        private void OnValidate() => Reconstruir();

        // ------------------------------------------------------------------
        //  Construccion
        // ------------------------------------------------------------------

        [ContextMenu("Reconstruir")]
        public void Reconstruir()
        {
            _valido = false;
            _ultimoError = null;

            try
            {
                _perimetro = new PerimetroSuperelipse(semiejeX, semiejeZ, exponenteCodos);

                _registro = new RegistroAnclajesTecho();
                if (usarAnclajesSinteticos) PublicarAnclajesSinteticos();
                _registro.Indexar(_perimetro);

                _borde = new BordeInteriorTecho(parametrosBorde);
                _borde.Construir();

                DescriptorMarco descriptor = diseno == DisenoTecho.Diseno1Membrana
                    ? DescriptorMarco.Diseno1(_borde)
                    : DescriptorMarco.Diseno2(_borde, _perimetro);

                _marco = new MarcoRigidoTecho(descriptor);
                _marco.Construir(_perimetro, _registro, _borde);

                if (diseno == DisenoTecho.Diseno1Membrana)
                {
                    _tendido = new TendidoCables(parametrosTendido);
                    _tendido.Construir(_perimetro, _registro, _borde, _marco);

                    _membrana = new MembranaTecho(parametrosMembrana);
                    _membrana.Construir(_perimetro, _registro, _borde, _tendido);
                }
                else
                {
                    _tendido = null;
                    _membrana = null;
                }

                _valido = true;
            }
            catch (Exception e)
            {
                _ultimoError = e.Message;
                Debug.LogError($"[VisorTechoGizmos] {e.Message}", this);
            }
        }

        /// <summary>
        /// Coronamiento sintetico: alto en los laterales, bajo en las cabeceras, con
        /// transicion suave por los codos. Es el perfil que describiste, para poder
        /// calibrar antes de conectar los generadores de tribuna.
        /// </summary>
        private void PublicarAnclajesSinteticos()
        {
            Vector2[] puntos = _perimetro.MuestrearPorSeparacion(separacionVigas, out float separacionReal);

            for (int i = 0; i < puntos.Length; i++)
            {
                float t = _perimetro.TDePunto(puntos[i]);
                // Con Z largo, las plateas laterales estan en x = +-a (t = 0, PI) y las
                // cabeceras en z = +-b (t = PI/2, 3PI/2).
                float mezcla = Mathf.Abs(Mathf.Cos(t));   // 0 en cabecera, 1 en lateral
                mezcla = mezcla * mezcla * (3f - 2f * mezcla);

                float altura = Mathf.Lerp(alturaCoronamientoCabecera, alturaCoronamientoLateral, mezcla);

                Vector2 normal = _perimetro.NormalExterior(t);
                Vector3 ejeViga = new Vector3(normal.x, 1.6f, normal.y).normalized;

                _registro.Publicar(new Vector3(puntos[i].x, altura, puntos[i].y),
                                   ejeViga, "sintetico", i);
            }
        }

        // ------------------------------------------------------------------
        //  Diagnostico
        // ------------------------------------------------------------------

        [ContextMenu("Imprimir diagnostico")]
        public void ImprimirDiagnostico()
        {
            if (!_valido) Reconstruir();
            if (!_valido)
            {
                Debug.LogError($"[Techo] No se pudo construir: {_ultimoError}", this);
                return;
            }

            var mensajes = new List<string>();

            Debug.Log(_perimetro.Diagnostico(), this);
            Debug.Log(_registro.Diagnostico(), this);
            Debug.Log(_borde.Diagnostico(), this);
            Debug.Log(_marco.Diagnostico(), this);
            if (_tendido != null) Debug.Log(_tendido.Diagnostico(), this);
            if (_membrana != null) Debug.Log(_membrana.Diagnostico(), this);

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

        // ------------------------------------------------------------------
        //  Dibujo
        // ------------------------------------------------------------------

        private void OnDrawGizmos()
        {
            if (!_valido) return;

            // La geometria se calcula en coordenadas locales, con el centro del campo en el
            // origen y los ejes alineados: es lo que hace que la superelipse y la simetria
            // de los tensores funcionen. La transformacion al mundo se aplica una sola vez,
            // aca, al dibujar.
            Matrix4x4 matrizPrevia = Gizmos.matrix;
            Gizmos.matrix = MatrizEstadio;

            try
            {
                DibujarCapas();
            }
            finally
            {
                Gizmos.matrix = matrizPrevia;
            }
        }

        /// <summary>Transformacion de coordenadas locales del techo a coordenadas de mundo.</summary>
        public Matrix4x4 MatrizEstadio =>
            Matrix4x4.TRS(centroEstadio, Quaternion.Euler(0f, rotacionYGrados, 0f), Vector3.one);

        private void DibujarCapas()
        {
            if (dibujarPerimetro) DibujarPerimetro();
            if (dibujarAnclajes) DibujarAnclajes();
            if (dibujarBordeInterior) DibujarBordeInterior();
            if (dibujarPuentes) DibujarPuentes();
            if (dibujarLongitudinales) DibujarLongitudinales();

            if (_tendido != null)
            {
                if (dibujarCablesTransversales) DibujarCables(_tendido.Transversales, new Color(0.11f, 0.62f, 0.46f));
                if (dibujarCablesLongitudinales) DibujarCables(_tendido.Longitudinales, new Color(0.20f, 0.55f, 0.70f));
            }

            if (_membrana != null)
            {
                if (dibujarMembrana) DibujarRejilla(_membrana.RejillaMembrana,
                                                    new Color(0.75f, 0.78f, 0.82f, 0.9f), pasoDibujoMembrana);
                if (dibujarFaldon && _membrana.HayFaldon)
                    DibujarRejilla(_membrana.RejillaFaldon, new Color(0.85f, 0.35f, 0.19f), pasoDibujoMembrana);
            }
        }

        private void DibujarPerimetro()
        {
            Gizmos.color = new Color(0.45f, 0.45f, 0.42f);
            const int pasos = 240;
            float longitud = _perimetro.LongitudTotal;

            Vector3 anterior = PuntoPerimetro(0f);
            for (int i = 1; i <= pasos; i++)
            {
                Vector3 actual = PuntoPerimetro(longitud * i / pasos);
                Gizmos.DrawLine(anterior, actual);
                anterior = actual;
            }
        }

        private Vector3 PuntoPerimetro(float s)
        {
            Vector2 xz = _perimetro.PuntoPorLongitud(s);
            return new Vector3(xz.x, _registro.AlturaCoronamiento(s), xz.y);
        }

        private void DibujarAnclajes()
        {
            Gizmos.color = new Color(0.37f, 0.37f, 0.35f);
            IReadOnlyList<AnclajeTecho> anclajes = _registro.Anclajes;

            for (int i = 0; i < anclajes.Count; i++)
            {
                Vector3 p = anclajes[i].posicion;
                Gizmos.DrawLine(p, p - anclajes[i].ejeViga * 4f);
                Gizmos.DrawSphere(p, 0.6f);
            }
        }

        private void DibujarBordeInterior()
        {
            Gizmos.color = new Color(0.85f, 0.35f, 0.19f);
            const int pasos = 200;
            float longitud = _borde.LongitudTotal;

            Vector3 anterior = _borde.PuntoEnS(0f);
            for (int i = 1; i <= pasos; i++)
            {
                Vector3 actual = _borde.PuntoEnS(longitud * i / pasos);
                Gizmos.DrawLine(anterior, actual);
                anterior = actual;
            }

            Gizmos.color = new Color(0.98f, 0.75f, 0.20f);
            foreach (Vector3 esquina in _borde.Esquinas)
                Gizmos.DrawSphere(esquina, 1.6f);
        }

        private void DibujarPuentes()
        {
            const int pasos = 40;

            foreach (PuenteConstruido puente in _marco.Puentes)
            {
                Gizmos.color = new Color(0.42f, 0.40f, 0.80f);

                Vector3 superiorAnterior = puente.PuntoCuerdaSuperior(0f);
                Vector3 inferiorAnterior = puente.PuntoCuerdaInferior(0f);

                for (int i = 1; i <= pasos; i++)
                {
                    float u = (float)i / pasos;
                    Vector3 superior = puente.PuntoCuerdaSuperior(u);
                    Vector3 inferior = puente.PuntoCuerdaInferior(u);

                    Gizmos.DrawLine(superiorAnterior, superior);
                    Gizmos.DrawLine(inferiorAnterior, inferior);
                    if (i % 5 == 0) Gizmos.DrawLine(superior, inferior);

                    superiorAnterior = superior;
                    inferiorAnterior = inferior;
                }

                // Pedestales
                Gizmos.color = new Color(0.85f, 0.35f, 0.19f);
                Gizmos.DrawLine(puente.apoyoXNegativo.posicionCuerdaSuperior,
                                puente.apoyoXNegativo.posicionCoronamiento);
                Gizmos.DrawLine(puente.apoyoXPositivo.posicionCuerdaSuperior,
                                puente.apoyoXPositivo.posicionCoronamiento);
            }
        }

        private void DibujarLongitudinales()
        {
            Gizmos.color = new Color(0.85f, 0.35f, 0.19f);

            foreach (LongitudinalConstruido longitudinal in _marco.Longitudinales)
                for (int i = 1; i < longitudinal.eje.Length; i++)
                    Gizmos.DrawLine(longitudinal.eje[i - 1], longitudinal.eje[i]);
        }

        private void DibujarCables(IReadOnlyList<Cable> cables, Color color)
        {
            Gizmos.color = color;

            for (int c = 0; c < cables.Count; c += pasoDibujoCables)
            {
                Vector3[] puntos = cables[c].Muestrear(10);
                for (int i = 1; i < puntos.Length; i++)
                    Gizmos.DrawLine(puntos[i - 1], puntos[i]);
            }
        }

        private void DibujarRejilla(RejillaSuperficie rejilla, Color color, int paso)
        {
            if (rejilla.vertices == null || rejilla.vertices.Length == 0) return;

            Gizmos.color = color;

            for (int c = 0; c < rejilla.columnas; c += paso)
            {
                for (int f = 0; f < rejilla.filas; f++)
                {
                    if (f < rejilla.filas - 1)
                        Gizmos.DrawLine(rejilla.Vertice(f, c), rejilla.Vertice(f + 1, c));

                    Gizmos.DrawLine(rejilla.Vertice(f, c), rejilla.Vertice(f, c + paso));
                }
            }
        }
    }
}
