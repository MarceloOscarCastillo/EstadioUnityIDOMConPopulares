using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace Estadio.Techo
{
    // Convencion de ejes: Z = eje LARGO del campo, X = ANCHO.

    public enum FamiliaCable
    {
        /// <summary>Cruza la cancha por lo ancho: a z constante, de una viga longitudinal a
        /// la otra. Es la familia que carga: de ella cuelgan las tubulares del vano.</summary>
        Transversal,
        /// <summary>Corre a lo largo de la cancha, a x constante. Vive sobre la superficie
        /// que definen los transversales, no la define.</summary>
        Longitudinal
    }

    public enum TipoCableLongitudinal
    {
        /// <summary>Sobre las plateas laterales: cruza el estadio entero, de punta a punta.</summary>
        DePuntaAPunta,
        /// <summary>Sobre una cabecera: nace del ultimo cable transversal y muere en el
        /// puente que cierra el vano. Nunca cruza el campo.</summary>
        DeCabecera
    }

    public enum TipoApoyoCable
    {
        Anclaje,        // tensor sobre la viga longitudinal
        BordeInterior,  // cruce con el borde del vano
        CableCruzado    // nudo de red: apoyo sobre un cable de la otra familia
    }

    public struct ApoyoCable
    {
        public Vector3 posicion;
        public TipoApoyoCable tipo;
        public AnclajeTecho anclaje;
    }

    public sealed class Cable
    {
        public FamiliaCable familia;
        public TipoCableLongitudinal tipoLongitudinal;
        public int indice;
        public float coordenada;          // z0 para transversales, x0 para longitudinales

        public ApoyoCable[] apoyos;
        public float[] flechaPorTramo;
        public float[] luzPorTramo;
        public float longitudHorizontal;

        public int CantidadTramos => apoyos.Length - 1;

        public Vector3 PuntoEnTramo(int tramo, float t)
        {
            t = Mathf.Clamp01(t);
            Vector3 a = apoyos[tramo].posicion;
            Vector3 b = apoyos[tramo + 1].posicion;

            Vector3 p = Vector3.Lerp(a, b, t);
            p.y -= 4f * flechaPorTramo[tramo] * t * (1f - t);
            return p;
        }

        public Vector3 Punto(float u)
        {
            u = Mathf.Clamp01(u);
            float objetivo = u * longitudHorizontal;
            float acumulado = 0f;

            for (int i = 0; i < CantidadTramos; i++)
            {
                if (objetivo <= acumulado + luzPorTramo[i] || i == CantidadTramos - 1)
                {
                    float t = luzPorTramo[i] > 1e-4f ? (objetivo - acumulado) / luzPorTramo[i] : 0f;
                    return PuntoEnTramo(i, Mathf.Clamp01(t));
                }
                acumulado += luzPorTramo[i];
            }

            return apoyos[apoyos.Length - 1].posicion;
        }

        /// <summary>Punto del cable a una coordenada del eje sobre el que corre:
        /// X para los transversales, Z para los longitudinales.</summary>
        public bool TryPuntoEnEje(float coordenadaEje, out Vector3 punto)
        {
            punto = default;

            for (int i = 0; i < CantidadTramos; i++)
            {
                Vector3 a = apoyos[i].posicion;
                Vector3 b = apoyos[i + 1].posicion;

                float ca = familia == FamiliaCable.Transversal ? a.x : a.z;
                float cb = familia == FamiliaCable.Transversal ? b.x : b.z;

                if (coordenadaEje < Mathf.Min(ca, cb) || coordenadaEje > Mathf.Max(ca, cb)) continue;

                float t = Mathf.Approximately(cb, ca) ? 0f : (coordenadaEje - ca) / (cb - ca);
                punto = PuntoEnTramo(i, t);
                return true;
            }

            return false;
        }

        public Vector3[] Muestrear(int segmentosPorTramo)
        {
            segmentosPorTramo = Mathf.Max(1, segmentosPorTramo);
            var puntos = new Vector3[CantidadTramos * segmentosPorTramo + 1];

            int k = 0;
            for (int i = 0; i < CantidadTramos; i++)
                for (int j = 0; j < segmentosPorTramo; j++)
                    puntos[k++] = PuntoEnTramo(i, (float)j / segmentosPorTramo);

            puntos[k] = apoyos[apoyos.Length - 1].posicion;
            return puntos;
        }
    }

    [Serializable]
    public struct ParametrosTendido
    {
        [Header("Separacion objetivo entre cables (m)")]
        public float separacionTransversal;
        public float separacionLongitudinal;

        [Header("Tension")]
        [Tooltip("Panza del cable transversal, relativa a la luz entre sus dos tensores. " +
                 "Mas tension = menos panza = borde del vano mas alto.")]
        public float flechaRelativaTransversal;
        [Tooltip("Panza de los longitudinales. Casi cero: van tensos.")]
        public float flechaRelativaLongitudinal;

        public static ParametrosTendido PorDefecto => new ParametrosTendido
        {
            separacionTransversal = 4.5f,
            separacionLongitudinal = 6.0f,
            flechaRelativaTransversal = 0.045f,
            flechaRelativaLongitudinal = 0.004f
        };
    }

    /// <summary>
    /// Las dos familias de cables, en dos fases.
    ///
    /// FASE 1 - ConstruirTransversales: cada cable va de una viga longitudinal a la otra en
    /// un solo tramo. Esto define la superficie del techo, y de aca sale la altura del borde
    /// interior: las tubulares cuelgan del cable, no al reves.
    ///
    /// FASE 2 - Completar: se parten los transversales en los cruces con el borde ya
    /// construido, y se tienden los longitudinales. Partir el cable NO cambia su forma: la
    /// flecha de cada tramo sale de f_global * (fraccion de luz)^2, que es la misma parabola
    /// expresada por tramos.
    ///
    /// Con el perimetro de dos rectas desaparecen el snap por tolerancia, el descarte por
    /// incidencia rasante y la tabla de longitud de arco: los extremos de cada cable son la
    /// interseccion exacta con dos rectas.
    /// </summary>
    public sealed class TendidoCables : ISuperficieCables
    {
        private ParametrosTendido _parametros;

        private readonly List<Cable> _transversales = new List<Cable>(64);
        private readonly List<Cable> _longitudinales = new List<Cable>(64);
        private Cable[] _transversalesPorZ = Array.Empty<Cable>();

        private Cable _cierreZNegativo;
        private Cable _cierreZPositivo;

        private int _versionTendido;
        private bool _fase1Lista;
        private bool _construido;

        public IReadOnlyList<Cable> Transversales => _transversales;
        public IReadOnlyList<Cable> Longitudinales => _longitudinales;

        /// <summary>Los dos cables que cierran el techo en las cabeceras. De ellos nacen los
        /// longitudinales de cabecera y cuelga el faldon.</summary>
        public Cable CierreZNegativo => _cierreZNegativo;
        public Cable CierreZPositivo => _cierreZPositivo;

        public int VersionTendido => _versionTendido;
        public bool Construido => _construido;

        public TendidoCables(ParametrosTendido parametros)
        {
            _parametros = parametros;
        }

        public void Configurar(ParametrosTendido parametros)
        {
            _parametros = parametros;
            _fase1Lista = false;
            _construido = false;
            _versionTendido++;
        }

        // ------------------------------------------------------------------
        //  FASE 1: transversales de viga a viga
        // ------------------------------------------------------------------

        /// <summary>
        /// Reparte transversales a paso constante entre los dos cierres. Los dos extremos
        /// —z = +-semiLargoTecho— existen siempre: son los que gobiernan cada cabecera.
        /// </summary>
        public void ConstruirTransversales(PerimetroTecho perimetro, RegistroAnclajesTecho registro)
        {
            if (perimetro == null) throw new ArgumentNullException(nameof(perimetro));
            if (registro == null) throw new ArgumentNullException(nameof(registro));

            _transversales.Clear();
            _longitudinales.Clear();
            _cierreZNegativo = null;
            _cierreZPositivo = null;

            float semiLargo = perimetro.SemiLargo;
            int cantidad = Mathf.Max(2, Mathf.RoundToInt(2f * semiLargo / _parametros.separacionTransversal));

            for (int i = 0; i <= cantidad; i++)
            {
                float z0 = Mathf.Lerp(-semiLargo, semiLargo, (float)i / cantidad);

                perimetro.ExtremosTransversal(z0, out Vector2 xzNegativo, out Vector2 xzPositivo);

                var apoyos = new List<ApoyoCable>(2)
                {
                    ApoyoExtremo(xzNegativo, false, perimetro, registro),
                    ApoyoExtremo(xzPositivo, true, perimetro, registro)
                };

                Cable cable = Ensamblar(FamiliaCable.Transversal, i, z0, apoyos,
                                        _parametros.flechaRelativaTransversal);
                _transversales.Add(cable);

                if (i == 0) _cierreZNegativo = cable;
                if (i == cantidad) _cierreZPositivo = cable;
            }

            _transversalesPorZ = _transversales.ToArray();
            Array.Sort(_transversalesPorZ, (a, b) => a.coordenada.CompareTo(b.coordenada));

            _fase1Lista = true;
            _construido = false;
            _versionTendido++;
        }

        /// <summary>
        /// Un extremo de cable transversal. La posicion en planta sale de la recta; la altura,
        /// del anclaje publicado si lo hay, o de la altura de la viga longitudinal si estamos
        /// en la zona del codo, donde la estructura de techo no se apoya en la grada y por lo
        /// tanto NO baja con ella.
        /// </summary>
        private static ApoyoCable ApoyoExtremo(Vector2 xz, bool ladoPositivo,
                                               PerimetroTecho perimetro, RegistroAnclajesTecho registro)
        {
            RectaViga recta = ladoPositivo ? perimetro.RectaXPositivo : perimetro.RectaXNegativo;

            // Cota Z acotada al tramo donde hay anclajes: mas alla, la viga sigue recta y
            // a la altura del ultimo anclaje, sin acompañar la caida de la grada.
            float zParaAltura = Mathf.Clamp(xz.y, recta.zPrimerAnclaje, recta.zUltimoAnclaje);
            float altura = AlturaViga(zParaAltura, ladoPositivo, registro);

            return new ApoyoCable
            {
                posicion = new Vector3(xz.x, altura, xz.y),
                tipo = TipoApoyoCable.Anclaje
            };
        }

        /// <summary>
        /// Altura de la viga longitudinal a la cota Z dada, interpolando entre los anclajes
        /// publicados de ese lado.
        /// </summary>
        private static float AlturaViga(float z, bool ladoPositivo, RegistroAnclajesTecho registro)
        {
            IReadOnlyList<AnclajeTecho> anclajes = registro.Anclajes;

            float mejorInferiorZ = float.NegativeInfinity, mejorInferiorY = 0f;
            float mejorSuperiorZ = float.PositiveInfinity, mejorSuperiorY = 0f;
            bool hayInferior = false, haySuperior = false;

            for (int i = 0; i < anclajes.Count; i++)
            {
                Vector3 p = anclajes[i].posicion;
                if ((p.x > 0f) != ladoPositivo) continue;

                if (p.z <= z && p.z > mejorInferiorZ) { mejorInferiorZ = p.z; mejorInferiorY = p.y; hayInferior = true; }
                if (p.z >= z && p.z < mejorSuperiorZ) { mejorSuperiorZ = p.z; mejorSuperiorY = p.y; haySuperior = true; }
            }

            if (hayInferior && haySuperior)
            {
                float span = mejorSuperiorZ - mejorInferiorZ;
                if (span < 1e-4f) return mejorInferiorY;
                return Mathf.Lerp(mejorInferiorY, mejorSuperiorY, (z - mejorInferiorZ) / span);
            }

            if (hayInferior) return mejorInferiorY;
            if (haySuperior) return mejorSuperiorY;
            return 0f;
        }

        /// <summary>Altura de la superficie de cables en (x, z), interpolando entre los dos
        /// transversales que flanquean el punto. Es lo que consulta el borde interior.</summary>
        public bool TryAltura(float x, float z, out float altura)
        {
            altura = 0f;
            if (_transversalesPorZ.Length < 2) return false;

            int siguiente = -1;
            for (int i = 0; i < _transversalesPorZ.Length; i++)
                if (_transversalesPorZ[i].coordenada >= z) { siguiente = i; break; }

            if (siguiente <= 0) return false;
            int anterior = siguiente - 1;

            if (!_transversalesPorZ[anterior].TryPuntoEnEje(x, out Vector3 pa)) return false;
            if (!_transversalesPorZ[siguiente].TryPuntoEnEje(x, out Vector3 pb)) return false;

            float separacion = _transversalesPorZ[siguiente].coordenada
                             - _transversalesPorZ[anterior].coordenada;

            if (separacion < 1e-4f) { altura = pa.y; return true; }

            float u = Mathf.Clamp01((z - _transversalesPorZ[anterior].coordenada) / separacion);
            altura = Mathf.Lerp(pa.y, pb.y, u);
            return true;
        }

        // ------------------------------------------------------------------
        //  FASE 2
        // ------------------------------------------------------------------

        public void Completar(PerimetroTecho perimetro, BordeInteriorTecho borde)
        {
            if (!_fase1Lista)
                throw new InvalidOperationException(
                    "Llamar primero a ConstruirTransversales, y con esos cables construir el borde.");

            if (borde == null) throw new ArgumentNullException(nameof(borde));

            PartirTransversalesEnElBorde(borde);
            ConstruirLongitudinales(perimetro, borde);

            _construido = true;
            _versionTendido++;
        }

        /// <summary>
        /// Inserta los dos cruces con el borde como apoyos intermedios. La curva no cambia:
        /// los puntos ya estan sobre la parabola y la flecha de cada tramo se deduce de la
        /// global por f_tramo = f_global * (fraccion)^2.
        /// </summary>
        private void PartirTransversalesEnElBorde(BordeInteriorTecho borde)
        {
            foreach (Cable cable in _transversales)
            {
                if (cable.apoyos.Length != 2) continue;
                if (!borde.IntersectarZ(cable.coordenada, out Vector3 bordeXNeg, out Vector3 bordeXPos))
                    continue;

                Vector3 a = cable.apoyos[0].posicion;
                Vector3 b = cable.apoyos[1].posicion;

                float luzTotal = cable.luzPorTramo[0];
                if (luzTotal < 1e-3f) continue;

                float flechaGlobal = cable.flechaPorTramo[0];

                float uNeg = Mathf.Clamp01((bordeXNeg.x - a.x) / (b.x - a.x));
                float uPos = Mathf.Clamp01((bordeXPos.x - a.x) / (b.x - a.x));
                if (uPos < uNeg)
                {
                    float t = uNeg; uNeg = uPos; uPos = t;
                    Vector3 q = bordeXNeg; bordeXNeg = bordeXPos; bordeXPos = q;
                }

                cable.apoyos = new[]
                {
                    cable.apoyos[0],
                    new ApoyoCable { posicion = bordeXNeg, tipo = TipoApoyoCable.BordeInterior },
                    new ApoyoCable { posicion = bordeXPos, tipo = TipoApoyoCable.BordeInterior },
                    cable.apoyos[1]
                };

                float[] fracciones = { uNeg, uPos - uNeg, 1f - uPos };

                cable.luzPorTramo = new float[3];
                cable.flechaPorTramo = new float[3];

                for (int i = 0; i < 3; i++)
                {
                    cable.luzPorTramo[i] = luzTotal * fracciones[i];
                    cable.flechaPorTramo[i] = flechaGlobal * fracciones[i] * fracciones[i];
                }
            }
        }

        /// <summary>
        /// Dos tipos de longitudinal, segun su cota X.
        ///
        /// Fuera del vano cruzan el estadio de punta a punta, sobre las plateas laterales, y
        /// sostienen la membrana. Dentro del vano no pueden cruzar el campo: nacen del cable
        /// de cierre de una cabecera y mueren en el borde del vano, y hay dos por cada X, uno
        /// por cabecera. No tienen por que ser simetricos: si las cabeceras no lo son, cada
        /// uno nace a su altura.
        /// </summary>
        private void ConstruirLongitudinales(PerimetroTecho perimetro, BordeInteriorTecho borde)
        {
            float semiVanoX = borde.Parametros.SemiVanoX;
            float semiLargo = perimetro.SemiLargo;

            // Ancho util minimo entre las dos vigas, para no salirse del techo.
            float xLimite = 0.5f * Mathf.Min(
                Mathf.Abs(perimetro.AnchoEnZ(0f)),
                Mathf.Min(Mathf.Abs(perimetro.AnchoEnZ(semiLargo)),
                          Mathf.Abs(perimetro.AnchoEnZ(-semiLargo))));

            int pasos = Mathf.FloorToInt(xLimite / _parametros.separacionLongitudinal);
            int indice = 0;

            for (int k = -pasos; k <= pasos; k++)
            {
                float x0 = k * _parametros.separacionLongitudinal;

                if (Mathf.Abs(x0) > semiVanoX)
                    ConstruirLongitudinalDePuntaAPunta(x0, ref indice, perimetro);
                else
                    ConstruirLongitudinalesDeCabecera(x0, ref indice, borde);
            }
        }

        private void ConstruirLongitudinalDePuntaAPunta(float x0, ref int indice, PerimetroTecho perimetro)
        {
            if (!_cierreZNegativo.TryPuntoEnEje(x0, out Vector3 inicio)) return;
            if (!_cierreZPositivo.TryPuntoEnEje(x0, out Vector3 fin)) return;

            var apoyos = new List<ApoyoCable>(2)
            {
                new ApoyoCable { posicion = inicio, tipo = TipoApoyoCable.CableCruzado },
                new ApoyoCable { posicion = fin, tipo = TipoApoyoCable.CableCruzado }
            };

            Cable cable = Ensamblar(FamiliaCable.Longitudinal, indice++, x0, apoyos,
                                    _parametros.flechaRelativaLongitudinal);
            cable.tipoLongitudinal = TipoCableLongitudinal.DePuntaAPunta;
            _longitudinales.Add(cable);
        }

        private void ConstruirLongitudinalesDeCabecera(float x0, ref int indice, BordeInteriorTecho borde)
        {
            if (!borde.IntersectarX(x0, out Vector3 bordeZNeg, out Vector3 bordeZPos)) return;

            if (_cierreZNegativo.TryPuntoEnEje(x0, out Vector3 nacimientoNeg))
            {
                var apoyos = new List<ApoyoCable>(2)
                {
                    new ApoyoCable { posicion = nacimientoNeg, tipo = TipoApoyoCable.CableCruzado },
                    new ApoyoCable { posicion = bordeZNeg, tipo = TipoApoyoCable.BordeInterior }
                };

                Cable cable = Ensamblar(FamiliaCable.Longitudinal, indice++, x0, apoyos,
                                        _parametros.flechaRelativaLongitudinal);
                cable.tipoLongitudinal = TipoCableLongitudinal.DeCabecera;
                _longitudinales.Add(cable);
            }

            if (_cierreZPositivo.TryPuntoEnEje(x0, out Vector3 nacimientoPos))
            {
                var apoyos = new List<ApoyoCable>(2)
                {
                    new ApoyoCable { posicion = bordeZPos, tipo = TipoApoyoCable.BordeInterior },
                    new ApoyoCable { posicion = nacimientoPos, tipo = TipoApoyoCable.CableCruzado }
                };

                Cable cable = Ensamblar(FamiliaCable.Longitudinal, indice++, x0, apoyos,
                                        _parametros.flechaRelativaLongitudinal);
                cable.tipoLongitudinal = TipoCableLongitudinal.DeCabecera;
                _longitudinales.Add(cable);
            }
        }

        private static Cable Ensamblar(FamiliaCable familia, int indice, float coordenada,
                                       List<ApoyoCable> apoyos, float flechaRelativa)
        {
            var cable = new Cable
            {
                familia = familia,
                indice = indice,
                coordenada = coordenada,
                apoyos = apoyos.ToArray()
            };

            int tramos = cable.CantidadTramos;
            cable.luzPorTramo = new float[tramos];
            cable.flechaPorTramo = new float[tramos];
            cable.longitudHorizontal = 0f;

            for (int i = 0; i < tramos; i++)
            {
                Vector3 a = cable.apoyos[i].posicion;
                Vector3 b = cable.apoyos[i + 1].posicion;

                float luz = Vector2.Distance(new Vector2(a.x, a.z), new Vector2(b.x, b.z));
                cable.luzPorTramo[i] = luz;
                cable.flechaPorTramo[i] = flechaRelativa * luz;
                cable.longitudHorizontal += luz;
            }

            return cable;
        }

        // ------------------------------------------------------------------
        //  Validacion y diagnostico
        // ------------------------------------------------------------------

        public bool Validar(List<string> mensajes)
        {
            if (mensajes == null) throw new ArgumentNullException(nameof(mensajes));

            if (!_construido)
            {
                mensajes.Add("ERROR: el tendido no esta completo.");
                return false;
            }

            bool valido = true;

            if (_transversales.Count < 3)
            {
                mensajes.Add($"ERROR: solo {_transversales.Count} cables transversales.");
                valido = false;
            }

            int deCabecera = 0, dePuntaAPunta = 0;
            foreach (Cable c in _longitudinales)
            {
                if (c.tipoLongitudinal == TipoCableLongitudinal.DeCabecera) deCabecera++;
                else dePuntaAPunta++;
            }

            if (dePuntaAPunta == 0)
                mensajes.Add("AVISO: no hay longitudinales de punta a punta. El vano ocupa todo " +
                             "el ancho util del techo.");

            if (deCabecera == 0)
                mensajes.Add("AVISO: no hay longitudinales de cabecera.");

            return valido;
        }

        public string Diagnostico()
        {
            var sb = new StringBuilder();
            sb.AppendLine($"Tendido de cables (version {_versionTendido}, completo: {_construido})");

            if (!_fase1Lista) return sb.ToString();

            int deCabecera = 0, dePuntaAPunta = 0;
            foreach (Cable c in _longitudinales)
            {
                if (c.tipoLongitudinal == TipoCableLongitudinal.DeCabecera) deCabecera++;
                else dePuntaAPunta++;
            }

            sb.AppendLine($"Transversales: {_transversales.Count}");
            sb.AppendLine($"Longitudinales: {dePuntaAPunta} de punta a punta, {deCabecera} de cabecera");

            if (_transversales.Count > 0)
            {
                Cable central = _transversales[_transversales.Count / 2];
                float alturaA = central.apoyos[0].posicion.y;
                float alturaB = central.apoyos[central.apoyos.Length - 1].posicion.y;

                sb.AppendLine($"Transversal central: {central.CantidadTramos} tramos, " +
                              $"luz {central.longitudHorizontal:F1} m");
                sb.AppendLine($"  tensores a {alturaA:F1} y {alturaB:F1} m " +
                              $"(desnivel {Mathf.Abs(alturaA - alturaB):F1} m)");
            }

            if (_cierreZNegativo != null && _cierreZPositivo != null)
            {
                sb.AppendLine($"Cierre z-: luz {_cierreZNegativo.longitudHorizontal:F1} m, " +
                              $"extremos a {_cierreZNegativo.apoyos[0].posicion.y:F1} y " +
                              $"{_cierreZNegativo.apoyos[_cierreZNegativo.apoyos.Length - 1].posicion.y:F1} m");
                sb.AppendLine($"Cierre z+: luz {_cierreZPositivo.longitudHorizontal:F1} m, " +
                              $"extremos a {_cierreZPositivo.apoyos[0].posicion.y:F1} y " +
                              $"{_cierreZPositivo.apoyos[_cierreZPositivo.apoyos.Length - 1].posicion.y:F1} m");
            }

            return sb.ToString();
        }
    }
}
