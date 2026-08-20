using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace Estadio.Techo
{
    // ==================================================================
    //  Convencion de ejes
    //     Z = eje LARGO del campo (de arco a arco)
    //     X = ANCHO (de platea lateral a platea lateral)
    //  Un puente transversal cruza la cancha por lo ancho: esta a z constante
    //  y va de una platea lateral a la otra, variando en X.
    // ==================================================================

    /// <summary>
    /// Un puente transversal: viga de canto variable paralela al eje X, con cuerda
    /// superior recta y horizontal y cuerda inferior parabolica. Vive siempre fuera del
    /// vano (|z| >= semiVanoZ), asi que nunca cruza la abertura sobre el campo.
    /// </summary>
    [Serializable]
    public struct DefinicionPuente
    {
        public string id;
        public float z;
        public float alturaCuerdaSuperior;
        public float cantoMaximo;
        public float cantoEnApoyos;
    }

    public enum TipoLongitudinal
    {
        /// <summary>Sigue uno de los cuatro arcos del borde interior. Curvo en planta y en
        /// alzado. Es la viga borde tubular del Diseno 1 y la celosia perimetral del 2.</summary>
        SigueBordeInterior,
        /// <summary>Recto a cota X constante, de un puente a otro. Son los tubulares que
        /// unen los puentes exteriores en el Diseno 2.</summary>
        RectoEntrePuentes
    }

    [Serializable]
    public struct DefinicionLongitudinal
    {
        public string id;
        public TipoLongitudinal tipo;

        // SigueBordeInterior
        public int indiceArcoBorde;      // 1 y 3 = lados largos (plateas); 0 y 2 = cabeceras

        // RectoEntrePuentes
        public float x;
        public string idPuenteInicio;
        public string idPuenteFin;
        public float flechaRelativa;     // 0 = recto; > 0 cuelga

        public float canto;
    }

    /// <summary>
    /// Lo unico que cambia entre los dos proyectos: cuantos elementos hay, donde, y de
    /// que tipo. Todo lo demas —perimetro, anclajes, coronamiento, borde interior— es
    /// compartido.
    /// </summary>
    [Serializable]
    public sealed class DescriptorMarco
    {
        public string nombre;
        public List<DefinicionPuente> puentes = new List<DefinicionPuente>();
        public List<DefinicionLongitudinal> longitudinales = new List<DefinicionLongitudinal>();

        /// <summary>
        /// Diseno 1 (consultora): dos puentes livianos junto al vano y las dos vigas borde
        /// tubulares sobre los lados largos, colgadas de los cables. Valores tentativos.
        /// </summary>
        public static DescriptorMarco Diseno1(BordeInteriorTecho borde, float retiroPuente = 1.5f, float holguraSobreBorde = 1.0f)
        {
            float z = borde.Parametros.SemiVanoZ + retiroPuente;
            float altura = borde.AlturaMaxima + holguraSobreBorde;

            var d = new DescriptorMarco { nombre = "Diseno 1 - membrana tensada" };

            d.puentes.Add(new DefinicionPuente
            {
                id = "puente_Z-", z = -z, alturaCuerdaSuperior = altura,
                cantoMaximo = 3.5f, cantoEnApoyos = 1.2f
            });
            d.puentes.Add(new DefinicionPuente
            {
                id = "puente_Z+", z = +z, alturaCuerdaSuperior = altura,
                cantoMaximo = 3.5f, cantoEnApoyos = 1.2f
            });

            // Arcos 1 y 3: los lados largos del vano, sobre las plateas laterales.
            d.longitudinales.Add(new DefinicionLongitudinal
            {
                id = "viga_borde_X-", tipo = TipoLongitudinal.SigueBordeInterior,
                indiceArcoBorde = 1, canto = 1.8f
            });
            d.longitudinales.Add(new DefinicionLongitudinal
            {
                id = "viga_borde_X+", tipo = TipoLongitudinal.SigueBordeInterior,
                indiceArcoBorde = 3, canto = 1.8f
            });

            return d;
        }

        /// <summary>
        /// Diseno 2 (variante): cuatro puentes, dos por cabecera, mas los tubulares rectos
        /// que unen los exteriores. Valores tentativos: la separacion entre el puente
        /// interior y el exterior de cada cabecera hay que medirla de las axonometrias.
        /// </summary>
        public static DescriptorMarco Diseno2(BordeInteriorTecho borde, IPerimetroEstadio perimetro,
                                              float retiroInterior = 1.5f, float retiroExterior = 12f,
                                              float holguraSobreBorde = 1.0f)
        {
            float zInterior = borde.Parametros.SemiVanoZ + retiroInterior;
            float zExterior = Mathf.Min(borde.Parametros.SemiVanoZ + retiroExterior,
                                        perimetro.SemiejeZ * 0.92f);
            float altura = borde.AlturaMaxima + holguraSobreBorde;

            var d = new DescriptorMarco { nombre = "Diseno 2 - reticulado rigido" };

            foreach (int signo in new[] { -1, 1 })
            {
                string sufijo = signo < 0 ? "Z-" : "Z+";

                d.puentes.Add(new DefinicionPuente
                {
                    id = $"puente_int_{sufijo}", z = signo * zInterior, alturaCuerdaSuperior = altura,
                    cantoMaximo = 5.5f, cantoEnApoyos = 2.2f
                });
                d.puentes.Add(new DefinicionPuente
                {
                    id = $"puente_ext_{sufijo}", z = signo * zExterior, alturaCuerdaSuperior = altura,
                    cantoMaximo = 5.5f, cantoEnApoyos = 2.2f
                });
            }

            for (int arco = 0; arco < 4; arco++)
            {
                d.longitudinales.Add(new DefinicionLongitudinal
                {
                    id = $"celosia_arco_{arco}", tipo = TipoLongitudinal.SigueBordeInterior,
                    indiceArcoBorde = arco, canto = 2.5f
                });
            }

            foreach (int signo in new[] { -1, 1 })
            {
                d.longitudinales.Add(new DefinicionLongitudinal
                {
                    id = signo < 0 ? "tubular_X-" : "tubular_X+",
                    tipo = TipoLongitudinal.RectoEntrePuentes,
                    x = signo * borde.Parametros.SemiVanoX * 1.35f,
                    idPuenteInicio = "puente_ext_Z-", idPuenteFin = "puente_ext_Z+",
                    flechaRelativa = 0f, canto = 2.0f
                });
            }

            return d;
        }
    }

    // ==================================================================
    //  Resultados
    // ==================================================================

    public struct ApoyoPuente
    {
        public Vector3 posicionCuerdaSuperior;
        public Vector3 posicionCoronamiento;
        public float longitudPedestal;
        public float s;
        public float anguloIncidencia;
    }

    public struct PuenteConstruido
    {
        public string id;
        public float z;
        public ApoyoPuente apoyoXNegativo;
        public ApoyoPuente apoyoXPositivo;
        public float luz;
        public float cantoMaximo;
        public float cantoEnApoyos;

        public float AlturaCuerdaSuperior => apoyoXNegativo.posicionCuerdaSuperior.y;
        public float AlturaMinimaCuerdaInferior => AlturaCuerdaSuperior - cantoMaximo;

        public float Canto(float u)
        {
            u = Mathf.Clamp01(u);
            return cantoEnApoyos + (cantoMaximo - cantoEnApoyos) * 4f * u * (1f - u);
        }

        public Vector3 PuntoCuerdaSuperior(float u)
        {
            return Vector3.Lerp(apoyoXNegativo.posicionCuerdaSuperior,
                                apoyoXPositivo.posicionCuerdaSuperior, Mathf.Clamp01(u));
        }

        public Vector3 PuntoCuerdaInferior(float u)
        {
            Vector3 p = PuntoCuerdaSuperior(u);
            p.y -= Canto(u);
            return p;
        }

        public float UDeX(float x)
        {
            float x0 = apoyoXNegativo.posicionCuerdaSuperior.x;
            float x1 = apoyoXPositivo.posicionCuerdaSuperior.x;
            return Mathf.Approximately(x1, x0) ? 0f : (x - x0) / (x1 - x0);
        }

        public bool AlcanzaX(float x) => Mathf.Abs(x) < Mathf.Abs(apoyoXPositivo.posicionCuerdaSuperior.x);
    }

    /// <summary>
    /// Elemento longitudinal ya resuelto. Los dos tipos terminan en lo mismo —una
    /// polilinea de eje— para que el generador de mallas no tenga que distinguirlos.
    /// </summary>
    public struct LongitudinalConstruido
    {
        public string id;
        public TipoLongitudinal tipo;
        public Vector3[] eje;
        public float longitud;
        public float canto;

        public Vector3 Inicio => eje[0];
        public Vector3 Fin => eje[eje.Length - 1];
    }

    // ==================================================================
    //  Marco
    // ==================================================================

    [Serializable]
    public struct ParametrosValidacionMarco
    {
        public float anguloIncidenciaMinimo;
        public float longitudPedestalMaxima;
        public float alturaLibreMinimaSobreCampo;

        public static ParametrosValidacionMarco PorDefecto => new ParametrosValidacionMarco
        {
            anguloIncidenciaMinimo = 60f,
            longitudPedestalMaxima = 12f,
            alturaLibreMinimaSobreCampo = 28f
        };
    }

    /// <summary>
    /// Los elementos rigidos del techo: puentes transversales y longitudinales. Generico
    /// respecto del diseno: recibe un DescriptorMarco y deriva todo lo demas del perimetro,
    /// del registro de anclajes y del borde interior.
    ///
    /// Ningun apoyo se busca. La cota Z de un puente determina sus dos apoyos por
    /// interseccion cerrada con la superelipse, y el pedestal es la resta contra el
    /// coronamiento de la tribuna en ese punto.
    /// </summary>
    public sealed class MarcoRigidoTecho
    {
        private DescriptorMarco _descriptor;
        private const int SegmentosPorLongitudinal = 48;

        private readonly List<PuenteConstruido> _puentes = new List<PuenteConstruido>(4);
        private readonly List<LongitudinalConstruido> _longitudinales = new List<LongitudinalConstruido>(6);
        private readonly Dictionary<string, int> _indicePuentes = new Dictionary<string, int>();

        private int _versionMarco;
        private int _versionPerimetroUsada = -1;
        private int _versionRegistroUsada = -1;
        private int _versionBordeUsada = -1;
        private bool _construido;

        public DescriptorMarco Descriptor => _descriptor;
        public IReadOnlyList<PuenteConstruido> Puentes { get { AsegurarConstruido(); return _puentes; } }
        public IReadOnlyList<LongitudinalConstruido> Longitudinales { get { AsegurarConstruido(); return _longitudinales; } }
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

        public bool NecesitaConstruir(IPerimetroEstadio perimetro, RegistroAnclajesTecho registro,
                                      BordeInteriorTecho borde)
        {
            return !_construido
                || perimetro.VersionGeometria != _versionPerimetroUsada
                || registro.VersionRegistro != _versionRegistroUsada
                || borde.VersionBorde != _versionBordeUsada;
        }

        public bool TryPuente(string id, out PuenteConstruido puente)
        {
            AsegurarConstruido();
            if (_indicePuentes.TryGetValue(id, out int i)) { puente = _puentes[i]; return true; }
            puente = default;
            return false;
        }

        // ------------------------------------------------------------------
        //  Construccion
        // ------------------------------------------------------------------

        public void Construir(IPerimetroEstadio perimetro, RegistroAnclajesTecho registro,
                              BordeInteriorTecho borde)
        {
            if (perimetro == null) throw new ArgumentNullException(nameof(perimetro));
            if (registro == null) throw new ArgumentNullException(nameof(registro));
            if (borde == null) throw new ArgumentNullException(nameof(borde));

            _puentes.Clear();
            _longitudinales.Clear();
            _indicePuentes.Clear();

            foreach (DefinicionPuente definicion in _descriptor.puentes)
            {
                _indicePuentes[definicion.id] = _puentes.Count;
                _puentes.Add(ConstruirPuente(definicion, perimetro, registro));
            }

            foreach (DefinicionLongitudinal definicion in _descriptor.longitudinales)
                _longitudinales.Add(ConstruirLongitudinal(definicion, borde));

            _versionPerimetroUsada = perimetro.VersionGeometria;
            _versionRegistroUsada = registro.VersionRegistro;
            _versionBordeUsada = borde.VersionBorde;
            _construido = true;
            _versionMarco++;
        }

        private static PuenteConstruido ConstruirPuente(DefinicionPuente definicion,
                                                        IPerimetroEstadio perimetro,
                                                        RegistroAnclajesTecho registro)
        {
            if (!perimetro.IntersectarZ(definicion.z, out float xPositivo, out float xNegativo))
                throw new InvalidOperationException(
                    $"El puente '{definicion.id}' en z={definicion.z:F1} cae fuera del perimetro " +
                    $"(semieje Z = {perimetro.SemiejeZ:F1} m).");

            float angulo = perimetro.AnguloIncidenciaZ(definicion.z);

            var puente = new PuenteConstruido
            {
                id = definicion.id,
                z = definicion.z,
                cantoMaximo = definicion.cantoMaximo,
                cantoEnApoyos = definicion.cantoEnApoyos,
                apoyoXNegativo = ConstruirApoyo(definicion, xNegativo, perimetro, registro, angulo),
                apoyoXPositivo = ConstruirApoyo(definicion, xPositivo, perimetro, registro, angulo)
            };

            puente.luz = Mathf.Abs(xPositivo - xNegativo);
            return puente;
        }

        private static ApoyoPuente ConstruirApoyo(DefinicionPuente definicion, float x,
                                                  IPerimetroEstadio perimetro,
                                                  RegistroAnclajesTecho registro, float angulo)
        {
            var puntoXZ = new Vector2(x, definicion.z);
            float s = perimetro.SDePunto(puntoXZ);
            float coronamiento = registro.AlturaCoronamiento(s);

            return new ApoyoPuente
            {
                posicionCuerdaSuperior = new Vector3(x, definicion.alturaCuerdaSuperior, definicion.z),
                posicionCoronamiento = new Vector3(x, coronamiento, definicion.z),
                longitudPedestal = definicion.alturaCuerdaSuperior - coronamiento,
                s = s,
                anguloIncidencia = angulo
            };
        }

        private LongitudinalConstruido ConstruirLongitudinal(DefinicionLongitudinal definicion,
                                                            BordeInteriorTecho borde)
        {
            return definicion.tipo == TipoLongitudinal.SigueBordeInterior
                ? ConstruirSobreBorde(definicion, borde)
                : ConstruirRecto(definicion);
        }

        private static LongitudinalConstruido ConstruirSobreBorde(DefinicionLongitudinal definicion,
                                                                  BordeInteriorTecho borde)
        {
            int arco = Mathf.Clamp(definicion.indiceArcoBorde, 0, 3);
            float longitudBorde = borde.LongitudTotal;

            // Las esquinas estan en t = PI/4 + k*PI/2; el arco k va de la esquina k a la k+1.
            float sInicio = borde.Planta.LongitudDeT((0.25f + 0.5f * arco) * Mathf.PI);
            float sFin = borde.Planta.LongitudDeT((0.75f + 0.5f * arco) * Mathf.PI);
            if (sFin <= sInicio) sFin += longitudBorde;

            var eje = new Vector3[SegmentosPorLongitudinal + 1];
            for (int i = 0; i <= SegmentosPorLongitudinal; i++)
            {
                float s = Mathf.Lerp(sInicio, sFin, (float)i / SegmentosPorLongitudinal);
                eje[i] = borde.PuntoEnS(s);
            }

            return new LongitudinalConstruido
            {
                id = definicion.id,
                tipo = definicion.tipo,
                eje = eje,
                longitud = sFin - sInicio,
                canto = definicion.canto
            };
        }

        private LongitudinalConstruido ConstruirRecto(DefinicionLongitudinal definicion)
        {
            if (!_indicePuentes.TryGetValue(definicion.idPuenteInicio, out int i0) ||
                !_indicePuentes.TryGetValue(definicion.idPuenteFin, out int i1))
                throw new InvalidOperationException(
                    $"El longitudinal '{definicion.id}' referencia puentes inexistentes " +
                    $"('{definicion.idPuenteInicio}', '{definicion.idPuenteFin}').");

            PuenteConstruido a = _puentes[i0];
            PuenteConstruido b = _puentes[i1];

            Vector3 inicio = a.PuntoCuerdaSuperior(a.UDeX(definicion.x));
            Vector3 fin = b.PuntoCuerdaSuperior(b.UDeX(definicion.x));

            float luz = Vector2.Distance(new Vector2(inicio.x, inicio.z), new Vector2(fin.x, fin.z));
            float flecha = definicion.flechaRelativa * luz;

            var eje = new Vector3[SegmentosPorLongitudinal + 1];
            for (int i = 0; i <= SegmentosPorLongitudinal; i++)
            {
                float u = (float)i / SegmentosPorLongitudinal;
                Vector3 p = Vector3.Lerp(inicio, fin, u);
                p.y -= 4f * flecha * u * (1f - u);
                eje[i] = p;
            }

            return new LongitudinalConstruido
            {
                id = definicion.id,
                tipo = definicion.tipo,
                eje = eje,
                longitud = luz,
                canto = definicion.canto
            };
        }

        private void AsegurarConstruido()
        {
            if (!_construido)
                throw new InvalidOperationException(
                    "El marco no esta construido. Llamar a Construir(perimetro, registro, borde).");
        }

        // ------------------------------------------------------------------
        //  Validacion
        // ------------------------------------------------------------------

        public bool Validar(ParametrosValidacionMarco criterios, BordeInteriorTecho borde,
                            List<string> mensajes)
        {
            if (mensajes == null) throw new ArgumentNullException(nameof(mensajes));

            if (!_construido)
            {
                mensajes.Add("ERROR: el marco no esta construido.");
                return false;
            }

            bool valido = true;
            float semiVanoZ = borde.Parametros.SemiVanoZ;

            foreach (PuenteConstruido puente in _puentes)
            {
                if (Mathf.Abs(puente.z) < semiVanoZ)
                {
                    mensajes.Add($"ERROR: el puente '{puente.id}' esta en z={puente.z:F1}, dentro del " +
                                 $"vano (semiVanoZ = {semiVanoZ:F1} m). Cruzaria la abertura sobre el campo.");
                    valido = false;
                }

                if (puente.apoyoXNegativo.anguloIncidencia < criterios.anguloIncidenciaMinimo)
                {
                    mensajes.Add($"ERROR: el puente '{puente.id}' llega al perimetro con " +
                                 $"{puente.apoyoXNegativo.anguloIncidencia:F0} grados " +
                                 $"(minimo {criterios.anguloIncidenciaMinimo:F0}). El apoyo cae en el codo.");
                    valido = false;
                }

                valido &= ValidarPedestal(puente, puente.apoyoXNegativo, "X-", criterios, mensajes);
                valido &= ValidarPedestal(puente, puente.apoyoXPositivo, "X+", criterios, mensajes);

                if (puente.cantoEnApoyos > puente.cantoMaximo)
                {
                    mensajes.Add($"ERROR: el puente '{puente.id}' tiene cantoEnApoyos mayor que " +
                                 "cantoMaximo. La panza quedaria invertida.");
                    valido = false;
                }

                if (puente.AlturaMinimaCuerdaInferior < criterios.alturaLibreMinimaSobreCampo)
                {
                    mensajes.Add($"AVISO: la panza del puente '{puente.id}' baja hasta " +
                                 $"{puente.AlturaMinimaCuerdaInferior:F1} m " +
                                 $"(minimo sugerido {criterios.alturaLibreMinimaSobreCampo:F1} m).");
                }
            }

            foreach (LongitudinalConstruido longitudinal in _longitudinales)
            {
                if (longitudinal.tipo != TipoLongitudinal.RectoEntrePuentes) continue;

                foreach (PuenteConstruido puente in _puentes)
                {
                    if (!EsExtremoDe(longitudinal, puente)) continue;
                    if (puente.AlcanzaX(longitudinal.Inicio.x)) continue;

                    mensajes.Add($"ERROR: el longitudinal '{longitudinal.id}' arranca en x=" +
                                 $"{longitudinal.Inicio.x:F1} m, fuera del alcance del puente " +
                                 $"'{puente.id}' (luz {puente.luz:F1} m).");
                    valido = false;
                }
            }

            return valido;
        }

        private bool EsExtremoDe(LongitudinalConstruido longitudinal, PuenteConstruido puente)
        {
            foreach (DefinicionLongitudinal definicion in _descriptor.longitudinales)
            {
                if (definicion.id != longitudinal.id) continue;
                return definicion.idPuenteInicio == puente.id || definicion.idPuenteFin == puente.id;
            }
            return false;
        }

        private static bool ValidarPedestal(PuenteConstruido puente, ApoyoPuente apoyo, string lado,
                                            ParametrosValidacionMarco criterios, List<string> mensajes)
        {
            if (apoyo.longitudPedestal < 0f)
            {
                mensajes.Add($"ERROR: el apoyo {lado} del puente '{puente.id}' necesita un pedestal " +
                             $"negativo ({apoyo.longitudPedestal:F1} m): el coronamiento de la tribuna " +
                             "esta por encima de la cuerda superior. Subir alturaCuerdaSuperior.");
                return false;
            }

            if (apoyo.longitudPedestal > criterios.longitudPedestalMaxima)
            {
                mensajes.Add($"AVISO: el apoyo {lado} del puente '{puente.id}' necesita un pedestal de " +
                             $"{apoyo.longitudPedestal:F1} m (maximo sugerido " +
                             $"{criterios.longitudPedestalMaxima:F1} m).");
            }

            return true;
        }

        public string Diagnostico()
        {
            var sb = new StringBuilder();
            sb.AppendLine($"Marco '{_descriptor.nombre}' (version {_versionMarco}, construido: {_construido})");

            if (!_construido) return sb.ToString();

            sb.AppendLine($"Puentes: {_puentes.Count} | Longitudinales: {_longitudinales.Count}");

            foreach (PuenteConstruido p in _puentes)
            {
                sb.AppendLine($"  {p.id}: z={p.z:F1} m, luz {p.luz:F1} m, " +
                              $"incidencia {p.apoyoXNegativo.anguloIncidencia:F0} grados");
                sb.AppendLine($"    pedestales X- {p.apoyoXNegativo.longitudPedestal:F2} m | " +
                              $"X+ {p.apoyoXPositivo.longitudPedestal:F2} m | " +
                              $"panza hasta {p.AlturaMinimaCuerdaInferior:F1} m");
            }

            foreach (LongitudinalConstruido l in _longitudinales)
                sb.AppendLine($"  {l.id} ({l.tipo}): {l.longitud:F1} m, canto {l.canto:F2} m");

            return sb.ToString();
        }
    }
}
