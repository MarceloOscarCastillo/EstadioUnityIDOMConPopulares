using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace Estadio.Techo
{
    // Convencion de ejes: Z = eje LARGO del campo, X = ANCHO.

    public enum FamiliaCable
    {
        /// <summary>Cruza la cancha por lo ancho: esta a z constante y varia en X.
        /// Es la familia que carga, la que sostiene las vigas borde. Con panza.</summary>
        Transversal,
        /// <summary>Corre a lo largo de la cancha: esta a x constante y varia en Z.
        /// Sobrevuela las plateas laterales y se apoya en los puentes.</summary>
        Longitudinal
    }

    public enum TipoApoyoCable
    {
        Anclaje,        // tensor sobre una viga diagonal del perimetro
        BordeLibre,     // punto del perimetro sin viga cercana
        BordeInterior,  // cruce con el borde del vano (viga borde o celosia)
        Puente,         // cruce con un puente transversal
        CableCruzado    // nudo de red: apoyo sobre un cable de la otra familia
    }

    public struct ApoyoCable
    {
        public Vector3 posicion;
        public TipoApoyoCable tipo;
        public AnclajeTecho anclaje;   // valido solo si tipo == Anclaje
    }

    /// <summary>
    /// Un cable completo, de tensor a tensor. El "par" de cada tensor no se busca: son los
    /// dos apoyos extremos de este mismo cable, que salen de la misma interseccion cerrada
    /// con la superelipse y por lo tanto son simetricos por construccion.
    /// </summary>
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

        /// <summary>
        /// Punto del cable a una coordenada dada del eje sobre el que corre: X para los
        /// transversales, Z para los longitudinales. Es lo que permite armar los nudos de
        /// red, apoyando un cable sobre otro.
        /// </summary>
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

        [Header("Panza: flecha relativa a la luz del tramo")]
        public float flechaRelativaTransversalExterior;
        public float flechaRelativaTransversalSobreVano;
        public float flechaRelativaLongitudinal;

        [Header("Red sobre las cabeceras")]
        public bool tenderTransversalesEnCabecera;

        [Header("Descartes")]
        public float anguloIncidenciaMinimo;
        public float toleranciaSnapAnclaje;
        public float margenAlPuente;
        public float margenAlPerimetro;

        public static ParametrosTendido PorDefecto => new ParametrosTendido
        {
            separacionTransversal = 4.5f,
            separacionLongitudinal = 6.0f,
            flechaRelativaTransversalExterior = 0.055f,
            flechaRelativaTransversalSobreVano = 0.008f,
            flechaRelativaLongitudinal = 0.004f,
            tenderTransversalesEnCabecera = true,
            anguloIncidenciaMinimo = 30f,
            toleranciaSnapAnclaje = 3.0f,
            margenAlPuente = 2.5f,
            margenAlPerimetro = 3.0f
        };
    }

    /// <summary>
    /// Las dos familias de cables del Diseno 1. Se construyen primero las longitudinales,
    /// porque las transversales de cabecera se apoyan sobre ellas formando una red.
    /// </summary>
    public sealed class TendidoCables
    {
        private ParametrosTendido _parametros;

        private readonly List<Cable> _transversales = new List<Cable>(64);
        private readonly List<Cable> _longitudinales = new List<Cable>(64);

        private Cable _longitudinalInternoXPositivo;
        private Cable _longitudinalInternoXNegativo;

        private int _versionTendido;
        private int _versionMarcoUsada = -1;
        private int _versionBordeUsada = -1;
        private bool _construido;

        private int _descartadosPorAngulo;
        private int _apoyosSinViga;
        private int _apoyosTotales;
        private int _transversalesCabeceraSinRed;

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
            _construido = false;
            _versionTendido++;
        }

        public bool NecesitaConstruir(MarcoRigidoTecho marco, BordeInteriorTecho borde)
        {
            return !_construido
                || marco.VersionMarco != _versionMarcoUsada
                || borde.VersionBorde != _versionBordeUsada;
        }

        // ------------------------------------------------------------------
        //  Construccion
        // ------------------------------------------------------------------

        public void Construir(IPerimetroEstadio perimetro, RegistroAnclajesTecho registro,
                              BordeInteriorTecho borde, MarcoRigidoTecho marco)
        {
            if (perimetro == null) throw new ArgumentNullException(nameof(perimetro));
            if (registro == null) throw new ArgumentNullException(nameof(registro));
            if (borde == null) throw new ArgumentNullException(nameof(borde));
            if (marco == null) throw new ArgumentNullException(nameof(marco));

            _transversales.Clear();
            _longitudinales.Clear();
            _longitudinalInternoXPositivo = null;
            _longitudinalInternoXNegativo = null;

            _descartadosPorAngulo = 0;
            _apoyosSinViga = 0;
            _apoyosTotales = 0;
            _transversalesCabeceraSinRed = 0;

            ConstruirLongitudinales(perimetro, registro, borde, marco);
            ConstruirTransversales(perimetro, registro, borde);

            _versionMarcoUsada = marco.VersionMarco;
            _versionBordeUsada = borde.VersionBorde;
            _construido = true;
            _versionTendido++;
        }

        /// <summary>
        /// Familia longitudinal: a x constante, fuera del vano, sobre las plateas laterales.
        /// Se apoya en la cuerda superior de cada puente que alcance, y por eso puede ir
        /// recta. Funciona igual con dos puentes que con cuatro.
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

                        apoyos.Add(new ApoyoCable
                        {
                            posicion = puente.PuntoCuerdaSuperior(puente.UDeX(x0)),
                            tipo = TipoApoyoCable.Puente
                        });
                    }

                    apoyos.Add(ApoyoExtremo(new Vector2(x0, zPositivo), perimetro, registro));

                    var flechas = new float[apoyos.Count - 1];
                    for (int i = 0; i < flechas.Length; i++)
                        flechas[i] = _parametros.flechaRelativaLongitudinal;

                    Cable cable = Ensamblar(FamiliaCable.Longitudinal, indice++, x0, angulo, apoyos, flechas);
                    _longitudinales.Add(cable);

                    if (j == 1)
                    {
                        if (signo > 0) _longitudinalInternoXPositivo = cable;
                        else _longitudinalInternoXNegativo = cable;
                    }
                }
            }
        }

        /// <summary>
        /// Familia transversal: a z constante, cruzando la cancha por lo ancho. Dentro del
        /// vano toma el borde interior en sus dos cruces, que es donde sostiene la viga
        /// borde. Sobre las cabeceras, si esta habilitado, se apoya en los dos longitudinales
        /// internos formando un nudo de red.
        /// </summary>
        private void ConstruirTransversales(IPerimetroEstadio perimetro, RegistroAnclajesTecho registro,
                                            BordeInteriorTecho borde)
        {
            float semiVanoZ = borde.Parametros.SemiVanoZ;
            float limite = _parametros.tenderTransversalesEnCabecera
                ? perimetro.SemiejeZ - _parametros.margenAlPerimetro
                : semiVanoZ - _parametros.margenAlPuente;

            int pasos = Mathf.FloorToInt(limite / _parametros.separacionTransversal);
            int indice = 0;

            for (int k = -pasos; k <= pasos; k++)
            {
                float z0 = k * _parametros.separacionTransversal;

                float angulo = perimetro.AnguloIncidenciaZ(z0);
                if (angulo < _parametros.anguloIncidenciaMinimo) { _descartadosPorAngulo++; continue; }

                if (!perimetro.IntersectarZ(z0, out float xPositivo, out float xNegativo)) continue;

                var apoyos = new List<ApoyoCable>(4)
                {
                    ApoyoExtremo(new Vector2(xNegativo, z0), perimetro, registro)
                };

                if (borde.IntersectarZ(z0, out Vector3 bordeXNegativo, out Vector3 bordeXPositivo))
                {
                    apoyos.Add(new ApoyoCable { posicion = bordeXNegativo, tipo = TipoApoyoCable.BordeInterior });
                    apoyos.Add(new ApoyoCable { posicion = bordeXPositivo, tipo = TipoApoyoCable.BordeInterior });
                }
                else if (TryNudosDeRed(z0, out Vector3 nudoXNegativo, out Vector3 nudoXPositivo))
                {
                    apoyos.Add(new ApoyoCable { posicion = nudoXNegativo, tipo = TipoApoyoCable.CableCruzado });
                    apoyos.Add(new ApoyoCable { posicion = nudoXPositivo, tipo = TipoApoyoCable.CableCruzado });
                }
                else
                {
                    // Cable de cabecera sin nada donde apoyarse: queda de un solo tramo.
                    _transversalesCabeceraSinRed++;
                }

                apoyos.Add(ApoyoExtremo(new Vector2(xPositivo, z0), perimetro, registro));

                float[] flechas = apoyos.Count == 4
                    ? new[]
                    {
                        _parametros.flechaRelativaTransversalExterior,
                        _parametros.flechaRelativaTransversalSobreVano,
                        _parametros.flechaRelativaTransversalExterior
                    }
                    : new[] { _parametros.flechaRelativaTransversalExterior };

                _transversales.Add(Ensamblar(FamiliaCable.Transversal, indice++, z0, angulo, apoyos, flechas));
            }
        }

        /// <summary>Cruce del cable transversal con los dos longitudinales internos.</summary>
        private bool TryNudosDeRed(float z0, out Vector3 nudoXNegativo, out Vector3 nudoXPositivo)
        {
            nudoXNegativo = default;
            nudoXPositivo = default;

            if (_longitudinalInternoXNegativo == null || _longitudinalInternoXPositivo == null) return false;

            return _longitudinalInternoXNegativo.TryPuntoEnEje(z0, out nudoXNegativo)
                && _longitudinalInternoXPositivo.TryPuntoEnEje(z0, out nudoXPositivo);
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
                                       float angulo, List<ApoyoCable> apoyos, float[] flechasRelativas)
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
                cable.flechaPorTramo[i] = flechasRelativas[Mathf.Min(i, flechasRelativas.Length - 1)] * luz;
                cable.longitudHorizontal += luz;
            }

            return cable;
        }

        // ------------------------------------------------------------------
        //  Validacion y diagnostico
        // ------------------------------------------------------------------

        public bool Validar(List<string> mensajes, float fraccionMaximaSinViga = 0.15f)
        {
            if (mensajes == null) throw new ArgumentNullException(nameof(mensajes));

            if (!_construido)
            {
                mensajes.Add("ERROR: el tendido no esta construido.");
                return false;
            }

            bool valido = true;

            if (_transversales.Count < 3)
            {
                mensajes.Add($"ERROR: solo {_transversales.Count} cables transversales.");
                valido = false;
            }

            if (_longitudinales.Count < 4)
            {
                mensajes.Add($"ERROR: solo {_longitudinales.Count} cables longitudinales.");
                valido = false;
            }

            if (_apoyosTotales > 0)
            {
                float fraccion = (float)_apoyosSinViga / _apoyosTotales;
                if (fraccion > fraccionMaximaSinViga)
                {
                    mensajes.Add($"ERROR: el {fraccion * 100f:F0}% de los tensores no encontro viga " +
                                 $"diagonal dentro de {_parametros.toleranciaSnapAnclaje:F1} m " +
                                 $"({_apoyosSinViga} de {_apoyosTotales}). Acercar la separacion de " +
                                 "cables a la de las vigas.");
                    valido = false;
                }
                else if (_apoyosSinViga > 0)
                {
                    mensajes.Add($"AVISO: {_apoyosSinViga} tensores quedaron sin viga diagonal cercana.");
                }
            }

            if (_transversalesCabeceraSinRed > 0)
            {
                mensajes.Add($"AVISO: {_transversalesCabeceraSinRed} cables transversales de cabecera " +
                             "quedaron de un solo tramo, sin longitudinal donde apoyarse. Van a " +
                             "colgar mucho mas que el resto.");
            }

            if (_descartadosPorAngulo > 0)
            {
                mensajes.Add($"AVISO: {_descartadosPorAngulo} cables descartados por llegar rasantes " +
                             $"al perimetro (menos de {_parametros.anguloIncidenciaMinimo:F0} grados).");
            }

            return valido;
        }

        public string Diagnostico()
        {
            var sb = new StringBuilder();
            sb.AppendLine($"Tendido de cables (version {_versionTendido}, construido: {_construido})");

            if (!_construido) return sb.ToString();

            sb.AppendLine($"Transversales: {_transversales.Count} | Longitudinales: {_longitudinales.Count}");
            sb.AppendLine($"Tensores: {_apoyosTotales} ({_apoyosSinViga} sin viga diagonal cercana)");
            sb.AppendLine($"Descartados por incidencia rasante: {_descartadosPorAngulo}");
            sb.AppendLine($"Transversales de cabecera sin red: {_transversalesCabeceraSinRed}");

            if (_transversales.Count > 0)
            {
                Cable central = _transversales[_transversales.Count / 2];
                sb.AppendLine($"Transversal central: {central.CantidadTramos} tramos, " +
                              $"luz total {central.longitudHorizontal:F1} m, " +
                              $"panza exterior {central.flechaPorTramo[0]:F2} m");
            }

            if (_longitudinales.Count > 0)
            {
                Cable interno = _longitudinalInternoXPositivo ?? _longitudinales[0];
                sb.AppendLine($"Longitudinal interno: {interno.CantidadTramos} tramos, " +
                              $"x={interno.coordenada:F1} m, luz total {interno.longitudHorizontal:F1} m");
            }

            return sb.ToString();
        }
    }
}
