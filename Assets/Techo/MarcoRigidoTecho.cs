using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace Estadio.Techo
{
    // Convencion de ejes: Z = eje LARGO del campo, X = ANCHO.

    public enum TipoElementoBorde
    {
        /// <summary>Lado largo del vano, sobre una platea lateral. Estructura tubular de la
        /// que cuelga la membrana; cuelga a su vez de los cables transversales.</summary>
        TubularLateral,
        /// <summary>Lado corto del vano, sobre una cabecera. En el Diseno 1 es el "puente":
        /// une las dos tubulares a la misma cota y cierra el vano. No cruza el estadio ni
        /// se apoya en las plateas: es el cuarto lado del rectangulo.</summary>
        PuenteCabecera
    }

    [Serializable]
    public struct DefinicionElementoBorde
    {
        public string id;
        public TipoElementoBorde tipo;
        [Tooltip("Arco del borde interior que recorre. 1 y 3 son los lados largos (plateas); " +
                 "0 y 2 las cabeceras.")]
        public int indiceArcoBorde;
        public float canto;
        public float ancho;
    }

    /// <summary>
    /// Lo unico que cambia entre los dos proyectos. En el Diseno 1 los cuatro lados del vano
    /// son elementos del borde: dos tubulares laterales y dos puentes de cabecera, todos a
    /// la misma cota, la que fijan los cables.
    /// </summary>
    [Serializable]
    public sealed class DescriptorMarco
    {
        public string nombre;
        public List<DefinicionElementoBorde> elementos = new List<DefinicionElementoBorde>();

        /// <summary>
        /// Diseno 1 (consultora): membrana tensada. Los cuatro lados del vano cuelgan de los
        /// cables transversales; no hay ninguna viga que cruce el estadio.
        /// </summary>
        public static DescriptorMarco Diseno1(float cantoTubular = 1.8f, float anchoTubular = 1.2f,
                                              float cantoPuente = 1.8f, float anchoPuente = 1.2f)
        {
            var d = new DescriptorMarco { nombre = "Diseno 1 - membrana tensada" };

            d.elementos.Add(new DefinicionElementoBorde
            {
                id = "tubular_X-", tipo = TipoElementoBorde.TubularLateral,
                indiceArcoBorde = 1, canto = cantoTubular, ancho = anchoTubular
            });
            d.elementos.Add(new DefinicionElementoBorde
            {
                id = "tubular_X+", tipo = TipoElementoBorde.TubularLateral,
                indiceArcoBorde = 3, canto = cantoTubular, ancho = anchoTubular
            });
            d.elementos.Add(new DefinicionElementoBorde
            {
                id = "puente_Z+", tipo = TipoElementoBorde.PuenteCabecera,
                indiceArcoBorde = 0, canto = cantoPuente, ancho = anchoPuente
            });
            d.elementos.Add(new DefinicionElementoBorde
            {
                id = "puente_Z-", tipo = TipoElementoBorde.PuenteCabecera,
                indiceArcoBorde = 2, canto = cantoPuente, ancho = anchoPuente
            });

            return d;
        }
    }

    /// <summary>
    /// Un lado del vano ya resuelto. Los dos tipos terminan en lo mismo —una polilinea de
    /// eje— para que el generador de mallas no tenga que distinguirlos.
    /// </summary>
    public struct ElementoBordeConstruido
    {
        public string id;
        public TipoElementoBorde tipo;
        public Vector3[] eje;
        public float longitud;
        public float canto;
        public float ancho;

        public Vector3 Inicio => eje[0];
        public Vector3 Fin => eje[eje.Length - 1];

        public float AlturaMinima
        {
            get
            {
                float minima = float.PositiveInfinity;
                for (int i = 0; i < eje.Length; i++) minima = Mathf.Min(minima, eje[i].y);
                return minima;
            }
        }

        public float AlturaMaxima
        {
            get
            {
                float maxima = float.NegativeInfinity;
                for (int i = 0; i < eje.Length; i++) maxima = Mathf.Max(maxima, eje[i].y);
                return maxima;
            }
        }
    }

    [Serializable]
    public struct ParametrosValidacionMarco
    {
        public float alturaLibreMinimaSobreCampo;
        public float desajusteMaximoEnEsquinas;

        public static ParametrosValidacionMarco PorDefecto => new ParametrosValidacionMarco
        {
            alturaLibreMinimaSobreCampo = 25f,
            desajusteMaximoEnEsquinas = 0.5f
        };
    }

    /// <summary>
    /// Los cuatro elementos rigidos que delimitan el vano. En el Diseno 1 no hay puentes que
    /// crucen el estadio: el "puente" es el lado corto del vano, une las dos tubulares y esta
    /// a la misma cota que ellas. Todo sale del borde interior, que a su vez sale de los
    /// cables: ningun elemento tiene cota propia.
    /// </summary>
    public sealed class MarcoRigidoTecho
    {
        private DescriptorMarco _descriptor;
        private const int SegmentosPorElemento = 48;

        private readonly List<ElementoBordeConstruido> _elementos = new List<ElementoBordeConstruido>(4);

        private int _versionMarco;
        private int _versionBordeUsada = -1;
        private bool _construido;

        public DescriptorMarco Descriptor => _descriptor;
        public IReadOnlyList<ElementoBordeConstruido> Elementos { get { AsegurarConstruido(); return _elementos; } }
        public int VersionMarco => _versionMarco;
        public bool Construido => _construido;

        public MarcoRigidoTecho(DescriptorMarco descriptor)
        {
            Configurar(descriptor);
        }

        public void Configurar(DescriptorMarco descriptor)
        {
            _descriptor = descriptor ?? throw new ArgumentNullException(nameof(descriptor));
            _construido = false;
            _versionMarco++;
        }

        public bool NecesitaConstruir(BordeInteriorTecho borde)
        {
            return !_construido || borde.VersionBorde != _versionBordeUsada;
        }

        public bool TryElemento(string id, out ElementoBordeConstruido elemento)
        {
            AsegurarConstruido();

            for (int i = 0; i < _elementos.Count; i++)
                if (_elementos[i].id == id) { elemento = _elementos[i]; return true; }

            elemento = default;
            return false;
        }

        // ------------------------------------------------------------------
        //  Construccion
        // ------------------------------------------------------------------

        public void Construir(BordeInteriorTecho borde)
        {
            if (borde == null) throw new ArgumentNullException(nameof(borde));

            _elementos.Clear();

            foreach (DefinicionElementoBorde definicion in _descriptor.elementos)
            {
                int arco = Mathf.Clamp(definicion.indiceArcoBorde, 0, 3);
                Vector3[] eje = borde.MuestrearArco(arco, SegmentosPorElemento);

                float longitud = 0f;
                for (int i = 1; i < eje.Length; i++)
                    longitud += Vector3.Distance(eje[i - 1], eje[i]);

                _elementos.Add(new ElementoBordeConstruido
                {
                    id = definicion.id,
                    tipo = definicion.tipo,
                    eje = eje,
                    longitud = longitud,
                    canto = definicion.canto,
                    ancho = definicion.ancho
                });
            }

            _versionBordeUsada = borde.VersionBorde;
            _construido = true;
            _versionMarco++;
        }

        private void AsegurarConstruido()
        {
            if (!_construido)
                throw new InvalidOperationException(
                    "El marco no esta construido. Llamar a Construir(borde).");
        }

        // ------------------------------------------------------------------
        //  Validacion y diagnostico
        // ------------------------------------------------------------------

        public bool Validar(ParametrosValidacionMarco criterios, List<string> mensajes)
        {
            if (mensajes == null) throw new ArgumentNullException(nameof(mensajes));

            if (!_construido)
            {
                mensajes.Add("ERROR: el marco no esta construido.");
                return false;
            }

            bool valido = true;

            foreach (ElementoBordeConstruido elemento in _elementos)
            {
                float minima = elemento.AlturaMinima;

                if (minima < criterios.alturaLibreMinimaSobreCampo)
                {
                    mensajes.Add($"ERROR: '{elemento.id}' baja hasta {minima:F1} m sobre el campo " +
                                 $"(minimo {criterios.alturaLibreMinimaSobreCampo:F1} m). Aumentar la " +
                                 "tension de los cables transversales para reducir la panza.");
                    valido = false;
                }
            }

            // Los cuatro elementos tienen que cerrar el rectangulo: el fin de cada arco es
            // el inicio del siguiente. Si no coinciden, el borde interior no es continuo.
            valido &= ValidarEsquinas(criterios, mensajes);

            return valido;
        }

        private bool ValidarEsquinas(ParametrosValidacionMarco criterios, List<string> mensajes)
        {
            bool valido = true;

            foreach (ElementoBordeConstruido a in _elementos)
            {
                float mejorDistancia = float.PositiveInfinity;

                foreach (ElementoBordeConstruido b in _elementos)
                {
                    if (b.id == a.id) continue;
                    mejorDistancia = Mathf.Min(mejorDistancia, Vector3.Distance(a.Fin, b.Inicio));
                }

                if (mejorDistancia > criterios.desajusteMaximoEnEsquinas)
                {
                    mensajes.Add($"ERROR: el extremo de '{a.id}' queda a {mejorDistancia:F2} m del " +
                                 "arranque del elemento contiguo. El vano no cierra.");
                    valido = false;
                }
            }

            return valido;
        }

        public string Diagnostico()
        {
            var sb = new StringBuilder();
            sb.AppendLine($"Marco '{_descriptor.nombre}' (version {_versionMarco}, construido: {_construido})");

            if (!_construido) return sb.ToString();

            sb.AppendLine($"Elementos del borde: {_elementos.Count}");

            foreach (ElementoBordeConstruido e in _elementos)
            {
                sb.AppendLine($"  {e.id} ({e.tipo}): {e.longitud:F1} m, " +
                              $"canto {e.canto:F2} m, ancho {e.ancho:F2} m");
                sb.AppendLine($"    cotas de {e.AlturaMinima:F2} a {e.AlturaMaxima:F2} m " +
                              $"(panza {e.AlturaMaxima - e.AlturaMinima:F2} m)");
            }

            return sb.ToString();
        }
    }
}
