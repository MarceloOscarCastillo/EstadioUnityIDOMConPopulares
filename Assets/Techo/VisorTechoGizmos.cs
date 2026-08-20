using System.Collections.Generic;
using UnityEngine;

namespace Estadio.Techo
{
    /// <summary>
    /// Dibuja con Gizmos la geometria que calcula ControladorTecho. No calcula nada por su
    /// cuenta: si lo hiciera, lo que se ve en la vista de escena podria diferir de lo que
    /// se genera en modo juego.
    ///
    /// Los Gizmos son lineas de editor, no GameObjects: esto no puede engordar el archivo
    /// de escena ni quedar serializado.
    /// </summary>
    [RequireComponent(typeof(ControladorTecho))]
    [DisallowMultipleComponent]
    public sealed class VisorTechoGizmos : MonoBehaviour
    {
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

            // El configurador republica anclajes cada vez que se aplica una variante, y eso
            // invalida el indice. Como el registro es compartido, hay que verificarlo aca.
            if (c.Registro != null && !c.Registro.IndiceValido)
                c.Registro.Indexar(c.Perimetro);

            // La geometria vive en coordenadas locales del techo: centro del campo en el
            // origen y ejes alineados. La transformacion al mundo se aplica solo al dibujar.
            Matrix4x4 previa = Gizmos.matrix;
            Gizmos.matrix = c.MatrizEstadio;

            try { DibujarCapas(c); }
            finally { Gizmos.matrix = previa; }
        }

        private void DibujarCapas(ControladorTecho c)
        {
            if (dibujarPerimetro) DibujarPerimetro(c);
            if (dibujarAnclajes) DibujarAnclajes(c);
            if (dibujarBordeInterior) DibujarBordeInterior(c);
            if (dibujarPuentes) DibujarPuentes(c);
            if (dibujarLongitudinales) DibujarLongitudinales(c);

            if (c.Tendido != null)
            {
                if (dibujarCablesTransversales)
                    DibujarCables(c.Tendido.Transversales, new Color(0.11f, 0.62f, 0.46f));
                if (dibujarCablesLongitudinales)
                    DibujarCables(c.Tendido.Longitudinales, new Color(0.20f, 0.55f, 0.70f));
            }

            if (c.Membrana != null)
            {
                if (dibujarMembrana)
                    DibujarRejilla(c.Membrana.RejillaMembrana,
                                   new Color(0.75f, 0.78f, 0.82f, 0.9f), pasoDibujoMembrana);

                if (dibujarFaldon && c.Membrana.HayFaldon)
                    DibujarRejilla(c.Membrana.RejillaFaldon,
                                   new Color(0.85f, 0.35f, 0.19f), pasoDibujoMembrana);
            }
        }

        // ------------------------------------------------------------------

        private void DibujarPerimetro(ControladorTecho c)
        {
            Gizmos.color = new Color(0.45f, 0.45f, 0.42f);
            const int pasos = 240;
            float longitud = c.Perimetro.LongitudTotal;

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
            Vector2 xz = c.Perimetro.PuntoPorLongitud(s);
            return new Vector3(xz.x, c.Registro.AlturaCoronamiento(s), xz.y);
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

        private static void DibujarPuentes(ControladorTecho c)
        {
            const int pasos = 40;

            foreach (PuenteConstruido puente in c.Marco.Puentes)
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

                Gizmos.color = new Color(0.85f, 0.35f, 0.19f);
                Gizmos.DrawLine(puente.apoyoXNegativo.posicionCuerdaSuperior,
                                puente.apoyoXNegativo.posicionCoronamiento);
                Gizmos.DrawLine(puente.apoyoXPositivo.posicionCuerdaSuperior,
                                puente.apoyoXPositivo.posicionCoronamiento);
            }
        }

        private static void DibujarLongitudinales(ControladorTecho c)
        {
            Gizmos.color = new Color(0.85f, 0.35f, 0.19f);

            foreach (LongitudinalConstruido longitudinal in c.Marco.Longitudinales)
                for (int i = 1; i < longitudinal.eje.Length; i++)
                    Gizmos.DrawLine(longitudinal.eje[i - 1], longitudinal.eje[i]);
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
