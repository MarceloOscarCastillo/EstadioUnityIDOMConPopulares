using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace Estadio.Techo
{
    // Convencion de ejes: Z = eje LARGO del campo, X = ANCHO.

    public enum FamiliaCable
    {
        /// <summary>Cruza la cancha por lo ancho: a z constante, varia en X. Es la familia
        /// que carga: de ella cuelgan las estructuras tubulares del borde del vano.</summary>
        Transversal,
        /// <summary>Corre a lo largo de la cancha: a x constante, varia en Z. Vive sobre la
        /// superficie que definen los transversales, no la define.</summary>
        Longitudinal
    }

    public enum TipoApoyoCable
    {
        Anclaje,
        BordeLibre,
        BordeInterior,
        Puente,
        CableCruzado
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
        public int indice;
        public float coordenada;          // z0 para transversales, x0 para longitudinales
        public float anguloIncidencia;

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
                 "Mas tension = menos panza = borde del vano mas alto. Es el parametro que " +
                 "reemplaza a la vieja alturaEsquinas.")]
        public float flechaRelativaTransversal;
        [Tooltip("Panza de los longitudinales entre apoyos. Casi cero: van tensos.")]
        public float flechaRelativaLongitudinal;

        [Header("Descartes")]
        public float anguloIncidenciaMinimo;
        public float toleranciaSnapAnclaje;
        public float margenAlPuente;
        public float margenAlPerimetro;

        public static ParametrosTendido PorDefecto => new ParametrosTendido
        {
            separacionTransversal = 4.5f,
            separacionLongitudinal = 6.0f,
            flechaRelativaTransversal = 0.045f,
            flechaRelativaLongitudinal = 0.004f,
            anguloIncidenciaMinimo = 30f,
            toleranciaSnapAnclaje = 3.0f,
            margenAlPuente = 2.5f,
            margenAlPerimetro = 3.0f
        };
    }

    /// <summary>
    /// Las dos familias de cables, en dos fases.
    ///
    /// FASE 1 - ConstruirTransversales: cada cable va de tensor a tensor en un solo tramo,
    /// con una parabola cuya flecha depende de la tension. Esto define la superficie del
    /// techo. De aca sale la altura del borde interior: las tubulares cuelgan del cable,
    /// no al reves.
    ///
    /// FASE 2 - Completar: se parten los transversales en los cruces con el borde ya
    /// construido, y se tienden los longitudinales. Partir el cable NO cambia su forma:
    /// la flecha de cada tramo se calcula como f_global * (fraccion de luz)^2, que es
    /// exactamente la misma parabola expresada por tramos.
    /// </summary>
    public sealed class TendidoCables : ISuperficieCables
    {
        private ParametrosTendido _parametros;

        private readonly List<Cable> _transversales = new List<Cable>(64);
        private readonly List<Cable> _longitudinales = new List<Cable>(64);
        private Cable[] _transversalesPorZ = Array.Empty<Cable>();

        private int _versionTendido;
        private bool _fase1Lista;
        private bool _construido;

        private int _descartadosPorAngulo;
        private int _apoyosSinViga;
        private int _apoyosTotales;

        public IReadOnlyList<Cable> Transversales => _transversales;
        public IReadOnlyList<Cable> Longitudinales => _longitudinales;
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
        //  FASE 1: transversales de tensor a tensor
        // ------------------------------------------------------------------

        public void ConstruirTransversales(IPerimetroEstadio perimetro, RegistroAnclajesTecho registro)
        {
            if (perimetro == null) throw new ArgumentNullException(nameof(perimetro));
            if (registro == null) throw new ArgumentNullException(nameof(registro));

            _transversales.Clear();
            _longitudinales.Clear();
            _descartadosPorAngulo = 0;
            _apoyosSinViga = 0;
            _apoyosTotales = 0;

            float limite = perimetro.SemiejeZ - _parametros.margenAlPerimetro;
            int pasos = Mathf.FloorToInt(limite / _parametros.separacionTransversal);
            int indice = 0;

            for (int k = -pasos; k <= pasos; k++)
            {
                float z0 = k * _parametros.separacionTransversal;

                float angulo = perimetro.AnguloIncidenciaZ(z0);
                if (angulo < _parametros.anguloIncidenciaMinimo) { _descartadosPorAngulo++; continue; }

                if (!perimetro.IntersectarZ(z0, out float xPositivo, out float xNegativo)) continue;

                var apoyos = new List<ApoyoCable>(2)
                {
                    ApoyoExtremo(new Vector2(xNegativo, z0), perimetro, registro),
                    ApoyoExtremo(new Vector2(xPositivo, z0), perimetro, registro)
                };

                _transversales.Add(Ensamblar(FamiliaCable.Transversal, indice++, z0, angulo,
                                             apoyos, _parametros.flechaRelativaTransversal));
            }

            _transversalesPorZ = _transversales.ToArray();
            Array.Sort(_transversalesPorZ, (a, b) => a.coordenada.CompareTo(b.coordenada));

            _fase1Lista = true;
            _construido = false;
            _versionTendido++;
        }

        /// <summary>
        /// Altura de la superficie de cables en (x, z), interpolando entre los dos
        /// transversales que flanquean el punto. Es lo que consulta el borde interior.
        /// </summary>
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
        //  FASE 2: partir transversales en el borde y tender longitudinales
        // ------------------------------------------------------------------

        public void Completar(IPerimetroEstadio perimetro, RegistroAnclajesTecho registro,
                              BordeInteriorTecho borde, MarcoRigidoTecho marco)
        {
            if (!_fase1Lista)
                throw new InvalidOperationException(
                    "Llamar primero a ConstruirTransversales, y con esos cables construir el borde.");

            if (borde == null) throw new ArgumentNullException(nameof(borde));
            if (marco == null) throw new ArgumentNullException(nameof(marco));

            PartirTransversalesEnElBorde(borde);
            ConstruirLongitudinales(perimetro, registro, borde, marco);

            _construido = true;
            _versionTendido++;
        }

        /// <summary>
        /// Inserta los dos cruces con el borde como apoyos intermedios. La curva no cambia:
        /// los puntos insertados ya estan sobre la parabola, y la flecha de cada tramo se
        /// deduce de la global por f_tramo = f_global * (Δu)^2.
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
                if (uPos < uNeg) { float t = uNeg; uNeg = uPos; uPos = t; Vector3 q = bordeXNeg; bordeXNeg = bordeXPos; bordeXPos = q; }

                var apoyos = new ApoyoCable[4];
                apoyos[0] = cable.apoyos[0];
                apoyos[1] = new ApoyoCable { posicion = bordeXNeg, tipo = TipoApoyoCable.BordeInterior };
                apoyos[2] = new ApoyoCable { posicion = bordeXPos, tipo = TipoApoyoCable.BordeInterior };
                apoyos[3] = cable.apoyos[1];

                float[] fracciones = { uNeg, uPos - uNeg, 1f - uPos };

                cable.apoyos = apoyos;
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
        /// Longitudinales: a x constante, fuera del vano, sobre las plateas laterales. Sus
        /// apoyos toman la altura de la superficie que definen los transversales, porque
        /// viven sobre ella en lugar de definirla.
        /// </summary>
        private void ConstruirLongitudinales(IPerimetroEstadio perimetro, RegistroAnclajesTecho registro,
                                             BordeInteriorTecho borde, MarcoRigidoTecho marco)
        {
            float semiVanoX = borde.Parametros.SemiVanoX;
            float limite = perimetro.SemiejeX - _parametros.margenAlPerimetro;
            int pasos = Mathf.FloorToInt((limite - semiVanoX) / _parametros.separacionLongitudinal);

            var puentesPorZ = new List<PuenteConstruido>(marco.Puentes);
            puentesPorZ.Sort((a, b) => a.z.CompareTo(b.z));

            int indice = 0;

            for (int signo = -1; signo <= 1; signo += 2)
            {
                for (int j = 1; j <= pasos; j++)
                {
                    float x0 = signo * (semiVanoX + j * _parametros.separacionLongitudinal);

                    float angulo = perimetro.AnguloIncidenciaX(x0);
                    if (angulo < _parametros.anguloIncidenciaMinimo) { _descartadosPorAngulo++; continue; }

                    if (!perimetro.IntersectarX(x0, out float zPositivo, out float zNegativo)) continue;

                    var apoyos = new List<ApoyoCable>(6)
                    {
                        ApoyoExtremo(new Vector2(x0, zNegativo), perimetro, registro)
                    };

                    foreach (PuenteConstruido puente in puentesPorZ)
                    {
                        if (puente.z <= zNegativo + _parametros.margenAlPuente) continue;
                        if (puente.z >= zPositivo - _parametros.margenAlPuente) continue;
                        if (!puente.AlcanzaX(x0)) continue;

                        Vector3 p = puente.PuntoCuerdaSuperior(puente.UDeX(x0));
                        if (TryAltura(x0, puente.z, out float ySuperficie))
                            p.y = Mathf.Min(p.y, ySuperficie);

                        apoyos.Add(new ApoyoCable { posicion = p, tipo = TipoApoyoCable.Puente });
                    }

                    apoyos.Add(ApoyoExtremo(new Vector2(x0, zPositivo), perimetro, registro));

                    _longitudinales.Add(Ensamblar(FamiliaCable.Longitudinal, indice++, x0, angulo,
                                                  apoyos, _parametros.flechaRelativaLongitudinal));
                }
            }
        }

        private ApoyoCable ApoyoExtremo(Vector2 puntoXZ, IPerimetroEstadio perimetro,
                                        RegistroAnclajesTecho registro)
        {
            _apoyosTotales++;
            float s = perimetro.SDePunto(puntoXZ);

            if (registro.TryAnclajeCercano(s, _parametros.toleranciaSnapAnclaje, out AnclajeTecho anclaje))
            {
                return new ApoyoCable
                {
                    posicion = anclaje.posicion,
                    tipo = TipoApoyoCable.Anclaje,
                    anclaje = anclaje
                };
            }

            _apoyosSinViga++;
            return new ApoyoCable
            {
                posicion = new Vector3(puntoXZ.x, registro.AlturaCoronamiento(s), puntoXZ.y),
                tipo = TipoApoyoCable.BordeLibre
            };
        }

        private static Cable Ensamblar(FamiliaCable familia, int indice, float coordenada,
                                       float angulo, List<ApoyoCable> apoyos, float flechaRelativa)
        {
            var cable = new Cable
            {
                familia = familia,
                indice = indice,
                coordenada = coordenada,
                anguloIncidencia = angulo,
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

        public bool Validar(List<string> mensajes, float fraccionMaximaSinViga = 0.60f)
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

            if (_apoyosTotales > 0)
            {
                float fraccion = (float)_apoyosSinViga / _apoyosTotales;
                if (fraccion > fraccionMaximaSinViga)
                {
                    mensajes.Add($"ERROR: el {fraccion * 100f:F0}% de los tensores no encontro viga " +
                                 $"diagonal ({_apoyosSinViga} de {_apoyosTotales}).");
                    valido = false;
                }
                else if (_apoyosSinViga > 0)
                {
                    mensajes.Add($"AVISO: {_apoyosSinViga} de {_apoyosTotales} tensores sin viga " +
                                 "diagonal cercana. En los codos es esperado.");
                }
            }

            if (_descartadosPorAngulo > 0)
                mensajes.Add($"AVISO: {_descartadosPorAngulo} cables descartados por incidencia rasante.");

            return valido;
        }

        public string Diagnostico()
        {
            var sb = new StringBuilder();
            sb.AppendLine($"Tendido de cables (version {_versionTendido}, completo: {_construido})");

            if (!_fase1Lista) return sb.ToString();

            sb.AppendLine($"Transversales: {_transversales.Count} | Longitudinales: {_longitudinales.Count}");
            sb.AppendLine($"Tensores: {_apoyosTotales} ({_apoyosSinViga} sin viga diagonal cercana)");
            sb.AppendLine($"Descartados por incidencia rasante: {_descartadosPorAngulo}");

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

            return sb.ToString();
        }
    }
}
