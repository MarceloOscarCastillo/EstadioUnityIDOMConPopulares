using System.Collections.Generic;
using UnityEngine;

namespace Estadio.Techo
{
    /// <summary>
    /// Superficie horizontal a cota fija. El Diseno 2 no tiene cables de los que derivar el
    /// borde, asi que hasta modelar su parrilla reticulada se usa esto.
    /// </summary>
    public sealed class SuperficiePlana : ISuperficieCables
    {
        private readonly float _altura;
        public SuperficiePlana(float altura) { _altura = altura; }
        public bool TryAltura(float x, float z, out float altura) { altura = _altura; return true; }
    }

    /// <summary>
    /// Dibuja con Gizmos la geometria que calcula ControladorTecho. No calcula nada por su
    /// cuenta: si lo hiciera, lo que se ve en escena podria diferir de lo que se genera en
    /// modo juego.
    /// </summary>
    [RequireComponent(typeof(ControladorTecho))]
    [DisallowMultipleComponent]
    public sealed class VisorTechoGizmos : MonoBehaviour
    {
        [Header("Capas a dibujar")]
        [SerializeField] private bool dibujarPerimetroEstadio = true;
        [SerializeField] private bool dibujarVigasLongitudinales = true;
        [SerializeField] private bool dibujarAnclajes = true;
        [SerializeField] private bool dibujarBordeInterior = true;
        [SerializeField] private bool dibujarMarco = true;
        [SerializeField] private bool dibujarCablesTransversales = true;
        [SerializeField] private bool dibujarCablesLongitudinales = false;
        [SerializeField] private bool dibujarMembrana = true;
        [SerializeField] private bool dibujarFaldon = true;

        [Header("Resolucion del dibujo")]
        [SerializeField, Range(1, 8)] private int pasoDibujoMembrana = 4;
        [SerializeField, Range(1, 6)] private int pasoDibujoCables = 1;

        private ControladorTecho _controlador;

        private ControladorTecho Controlador
        {
            get
            {
                if (_controlador == null) _controlador = GetComponent<ControladorTecho>();
                return _controlador;
            }
        }

        private void OnDrawGizmos()
        {
            ControladorTecho c = Controlador;
            if (c == null || !c.GeometriaLista) return;

            if (c.Registro != null && !c.Registro.IndiceValido)
                c.Registro.Indexar(c.PerimetroEstadio);

            Matrix4x4 previa = Gizmos.matrix;
            Gizmos.matrix = c.MatrizEstadio;

            try { DibujarCapas(c); }
            finally { Gizmos.matrix = previa; }
        }

        private void DibujarCapas(ControladorTecho c)
        {
            if (dibujarPerimetroEstadio) DibujarPerimetroEstadio(c);
            if (dibujarVigasLongitudinales) DibujarVigasLongitudinales(c);
            if (dibujarAnclajes) DibujarAnclajes(c);
            if (dibujarBordeInterior) DibujarBordeInterior(c);
            if (dibujarMarco) DibujarMarco(c);

            if (c.Tendido != null)
            {
                if (dibujarCablesTransversales)
                    DibujarCables(c.Tendido.Transversales, new Color(0.11f, 0.62f, 0.46f));
                if (dibujarCablesLongitudinales)
                    DibujarCables(c.Tendido.Longitudinales, new Color(0.20f, 0.55f, 0.70f));
            }

            if (c.Membrana != null && c.Membrana.Construida)
            {
                if (dibujarMembrana)
                    DibujarRejilla(c.Membrana.RejillaPano,
                                   new Color(0.75f, 0.78f, 0.82f, 0.9f), pasoDibujoMembrana);

                if (dibujarFaldon)
                    DibujarRejilla(c.Membrana.RejillaFaldon,
                                   new Color(0.85f, 0.35f, 0.19f), pasoDibujoMembrana);
            }
        }

        // ------------------------------------------------------------------

        private static void DibujarPerimetroEstadio(ControladorTecho c)
        {
            Gizmos.color = new Color(0.45f, 0.45f, 0.42f);
            const int pasos = 240;
            float longitud = c.PerimetroEstadio.LongitudTotal;

            Vector3 anterior = PuntoPerimetro(c, 0f);
            for (int i = 1; i <= pasos; i++)
            {
                Vector3 actual = PuntoPerimetro(c, longitud * i / pasos);
                Gizmos.DrawLine(anterior, actual);
                anterior = actual;
            }
        }

        private static Vector3 PuntoPerimetro(ControladorTecho c, float s)
        {
            Vector2 xz = c.PerimetroEstadio.PuntoPorLongitud(s);
            return new Vector3(xz.x, c.Registro.AlturaCoronamiento(s), xz.y);
        }

        /// <summary>Las dos rectas que forman el perimetro del techo, a la altura de los
        /// cables extremos.</summary>
        private static void DibujarVigasLongitudinales(ControladorTecho c)
        {
            if (c.Tendido == null) return;

            Gizmos.color = new Color(0.98f, 0.55f, 0.10f);

            Cable cierreNeg = c.Tendido.CierreZNegativo;
            Cable cierrePos = c.Tendido.CierreZPositivo;
            if (cierreNeg == null || cierrePos == null) return;

            // Viga x negativo: del primer apoyo de un cierre al primero del otro.
            Gizmos.DrawLine(cierreNeg.apoyos[0].posicion, cierrePos.apoyos[0].posicion);

            int ultimoNeg = cierreNeg.apoyos.Length - 1;
            int ultimoPos = cierrePos.apoyos.Length - 1;
            Gizmos.DrawLine(cierreNeg.apoyos[ultimoNeg].posicion, cierrePos.apoyos[ultimoPos].posicion);
        }

        private static void DibujarAnclajes(ControladorTecho c)
        {
            Gizmos.color = new Color(0.37f, 0.37f, 0.35f);
            IReadOnlyList<AnclajeTecho> anclajes = c.Registro.Anclajes;

            for (int i = 0; i < anclajes.Count; i++)
            {
                Vector3 p = anclajes[i].posicion;
                Gizmos.DrawLine(p, p - anclajes[i].ejeViga * 4f);
                Gizmos.DrawSphere(p, 0.6f);
            }
        }

        private static void DibujarBordeInterior(ControladorTecho c)
        {
            Gizmos.color = new Color(0.85f, 0.35f, 0.19f);
            const int pasos = 200;
            float longitud = c.Borde.LongitudTotal;

            Vector3 anterior = c.Borde.PuntoEnS(0f);
            for (int i = 1; i <= pasos; i++)
            {
                Vector3 actual = c.Borde.PuntoEnS(longitud * i / pasos);
                Gizmos.DrawLine(anterior, actual);
                anterior = actual;
            }

            Gizmos.color = new Color(0.98f, 0.75f, 0.20f);
            foreach (Vector3 esquina in c.Borde.Esquinas)
                Gizmos.DrawSphere(esquina, 1.6f);
        }

        private static void DibujarMarco(ControladorTecho c)
        {
            foreach (ElementoBordeConstruido elemento in c.Marco.Elementos)
            {
                Gizmos.color = elemento.tipo == TipoElementoBorde.TubularLateral
                    ? new Color(0.85f, 0.35f, 0.19f)
                    : new Color(0.42f, 0.40f, 0.80f);

                for (int i = 1; i < elemento.eje.Length; i++)
                    Gizmos.DrawLine(elemento.eje[i - 1], elemento.eje[i]);
            }
        }

        private void DibujarCables(IReadOnlyList<Cable> cables, Color color)
        {
            Gizmos.color = color;

            for (int i = 0; i < cables.Count; i += pasoDibujoCables)
            {
                Vector3[] puntos = cables[i].Muestrear(10);
                for (int j = 1; j < puntos.Length; j++)
                    Gizmos.DrawLine(puntos[j - 1], puntos[j]);
            }
        }

        private static void DibujarRejilla(RejillaSuperficie rejilla, Color color, int paso)
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
