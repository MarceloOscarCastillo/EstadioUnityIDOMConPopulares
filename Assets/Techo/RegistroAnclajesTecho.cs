using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace Estadio.Techo
{
    /// <summary>
    /// Una cabeza de viga diagonal, publicada por un generador de tribuna.
    /// Es el punto donde se adosa un tensor de cable.
    /// </summary>
    [Serializable]
    public struct AnclajeTecho
    {
        // --- Publicado por el generador de tribuna ---
        public Vector3 posicion;      // punta superior de la viga, en coordenadas de mundo
        public Vector3 ejeViga;       // direccion del eje de la viga, para orientar el prefab del tensor
        public string idTribuna;
        public int indiceViga;

        // --- Derivado por el registro al indexar ---
        public float s;               // coordenada de longitud de arco sobre el perimetro
        public float t;               // parametro trigonometrico
        public Vector2 proyeccionXZ;  // punto ideal sobre la curva
        public float desvio;          // distancia XZ entre la viga real y la curva ideal

        public float Altura => posicion.y;
    }

    [Serializable]
    public struct ParametrosValidacionAnclajes
    {
        public int cantidadMinima;
        public float separacionMaximaMetros;   // hueco tolerado entre anclajes consecutivos
        public float separacionMinimaMetros;   // por debajo de esto se consideran duplicados
        public float desvioMaximoMetros;       // distancia tolerada a la curva ideal
        public float asimetriaMaximaMetros;    // diferencia de altura tolerada entre lados espejados

        public static ParametrosValidacionAnclajes PorDefecto => new ParametrosValidacionAnclajes
        {
            cantidadMinima = 12,
            separacionMaximaMetros = 15f,
            separacionMinimaMetros = 0.40f,
            desvioMaximoMetros = 1.50f,
            asimetriaMaximaMetros = 0.25f
        };
    }

    /// <summary>
    /// Base de datos del borde superior del estadio. Los generadores de tribuna publican
    /// sus cabezas de viga durante la construccion; despues se indexa contra el perimetro
    /// y queda disponible la curva de coronamiento altura(s), que es el dato del que
    /// dependen el faldon, el apoyo de los arcos y el snap de los tensores.
    ///
    /// IMPORTANTE: publicar SIEMPRE antes de aplicar Static Batching. El batching pone
    /// transform.position en cero y las posiciones se pierden. Misma regla que ya usas
    /// con mapaObjetos / mapaPosiciones.
    /// </summary>
    public sealed class RegistroAnclajesTecho
    {
        private readonly List<AnclajeTecho> _publicados = new List<AnclajeTecho>(256);

        private AnclajeTecho[] _ordenados = Array.Empty<AnclajeTecho>();
        private float[] _clavesS = Array.Empty<float>();

        private IPerimetroEstadio _perimetro;
        private int _versionPerimetroIndexada = -1;
        private int _versionRegistro;
        private bool _indiceValido;

        public int VersionRegistro => _versionRegistro;
        public int CantidadPublicados => _publicados.Count;
        public int CantidadAnclajes => _ordenados.Length;
        public bool IndiceValido => _indiceValido;

        public float AlturaMaxima { get; private set; }
        public float AlturaMinima { get; private set; }
        public float SeparacionMaximaObservada { get; private set; }

        public IReadOnlyList<AnclajeTecho> Anclajes => _ordenados;

        // ------------------------------------------------------------------
        //  Publicacion
        // ------------------------------------------------------------------

        public void Limpiar()
        {
            _publicados.Clear();
            _ordenados = Array.Empty<AnclajeTecho>();
            _clavesS = Array.Empty<float>();
            _indiceValido = false;
            _versionRegistro++;
        }

        public void Publicar(Vector3 posicion, Vector3 ejeViga, string idTribuna, int indiceViga)
        {
            _publicados.Add(new AnclajeTecho
            {
                posicion = posicion,
                ejeViga = ejeViga.sqrMagnitude > 1e-8f ? ejeViga.normalized : Vector3.up,
                idTribuna = idTribuna,
                indiceViga = indiceViga
            });

            _indiceValido = false;
            _versionRegistro++;
        }

        /// <summary>True si hay que volver a llamar a Indexar (cambio el perimetro o hubo publicaciones).</summary>
        public bool NecesitaIndexar(IPerimetroEstadio perimetro)
        {
            return !_indiceValido
                || !ReferenceEquals(perimetro, _perimetro)
                || perimetro.VersionGeometria != _versionPerimetroIndexada;
        }

        // ------------------------------------------------------------------
        //  Indexado
        // ------------------------------------------------------------------

        /// <summary>
        /// Proyecta todos los anclajes sobre el perimetro, los ordena por longitud de arco,
        /// descarta duplicados y deja lista la curva de coronamiento.
        /// </summary>
        public void Indexar(IPerimetroEstadio perimetro, float separacionMinimaMetros = 0.40f)
        {
            if (perimetro == null) throw new ArgumentNullException(nameof(perimetro));

            _perimetro = perimetro;
            _versionPerimetroIndexada = perimetro.VersionGeometria;

            var proyectados = new List<AnclajeTecho>(_publicados.Count);
            for (int i = 0; i < _publicados.Count; i++)
            {
                AnclajeTecho a = _publicados[i];
                perimetro.Proyectar(a.posicion.AXZ(), out a.t, out a.s, out a.proyeccionXZ, out a.desvio);
                proyectados.Add(a);
            }

            proyectados.Sort((p, q) => p.s.CompareTo(q.s));

            // Duplicados en las costuras entre tribunas: se conserva el que mejor
            // se ajusta a la curva ideal.
            var filtrados = new List<AnclajeTecho>(proyectados.Count);
            for (int i = 0; i < proyectados.Count; i++)
            {
                if (filtrados.Count > 0)
                {
                    AnclajeTecho ultimo = filtrados[filtrados.Count - 1];
                    if (proyectados[i].s - ultimo.s < separacionMinimaMetros)
                    {
                        if (proyectados[i].desvio < ultimo.desvio)
                            filtrados[filtrados.Count - 1] = proyectados[i];
                        continue;
                    }
                }
                filtrados.Add(proyectados[i]);
            }

            _ordenados = filtrados.ToArray();
            _clavesS = new float[_ordenados.Length];

            AlturaMaxima = float.NegativeInfinity;
            AlturaMinima = float.PositiveInfinity;

            for (int i = 0; i < _ordenados.Length; i++)
            {
                _clavesS[i] = _ordenados[i].s;
                float y = _ordenados[i].Altura;
                if (y > AlturaMaxima) AlturaMaxima = y;
                if (y < AlturaMinima) AlturaMinima = y;
            }

            SeparacionMaximaObservada = CalcularSeparacionMaxima();

            if (_ordenados.Length == 0)
            {
                AlturaMaxima = 0f;
                AlturaMinima = 0f;
            }

            _indiceValido = true;
        }

        private float CalcularSeparacionMaxima()
        {
            if (_ordenados.Length < 2) return 0f;

            float L = _perimetro.LongitudTotal;
            float maxima = 0f;

            for (int i = 1; i < _clavesS.Length; i++)
                maxima = Mathf.Max(maxima, _clavesS[i] - _clavesS[i - 1]);

            // tramo de cierre
            maxima = Mathf.Max(maxima, L - _clavesS[_clavesS.Length - 1] + _clavesS[0]);
            return maxima;
        }

        // ------------------------------------------------------------------
        //  Consultas
        // ------------------------------------------------------------------

        /// <summary>
        /// Altura del coronamiento del estadio a la longitud de arco s, interpolada
        /// linealmente entre anclajes y ciclica en el tramo de cierre.
        /// Esta es la curva inferior del faldon.
        /// </summary>
        public float AlturaCoronamiento(float s)
        {
            AsegurarIndice();

            int n = _ordenados.Length;
            if (n == 0) return 0f;
            if (n == 1) return _ordenados[0].Altura;

            float L = _perimetro.LongitudTotal;
            s = Mathf.Repeat(s, L);

            // Tramo de cierre entre el ultimo y el primero
            if (s < _clavesS[0] || s >= _clavesS[n - 1])
            {
                float sa = _clavesS[n - 1];
                float sb = _clavesS[0] + L;
                float sx = s < _clavesS[0] ? s + L : s;
                float f = Mathf.InverseLerp(sa, sb, sx);
                return Mathf.Lerp(_ordenados[n - 1].Altura, _ordenados[0].Altura, f);
            }

            int lo = 0, hi = n - 1;
            while (hi - lo > 1)
            {
                int mid = (lo + hi) >> 1;
                if (_clavesS[mid] <= s) lo = mid; else hi = mid;
            }

            float g = Mathf.InverseLerp(_clavesS[lo], _clavesS[hi], s);
            return Mathf.Lerp(_ordenados[lo].Altura, _ordenados[hi].Altura, g);
        }

        /// <summary>Anclaje mas cercano a la longitud de arco s, dentro de la tolerancia.</summary>
        public bool TryAnclajeCercano(float s, float toleranciaMetros, out AnclajeTecho anclaje)
        {
            AsegurarIndice();
            anclaje = default;

            int n = _ordenados.Length;
            if (n == 0) return false;

            float L = _perimetro.LongitudTotal;
            s = Mathf.Repeat(s, L);

            int lo = 0, hi = n - 1;
            while (hi - lo > 1)
            {
                int mid = (lo + hi) >> 1;
                if (_clavesS[mid] <= s) lo = mid; else hi = mid;
            }

            // Candidatos: los dos vecinos del intervalo, mas el cierre ciclico.
            int mejor = -1;
            float mejorDistancia = float.PositiveInfinity;

            EvaluarCandidato(lo, s, L, ref mejor, ref mejorDistancia);
            EvaluarCandidato(hi, s, L, ref mejor, ref mejorDistancia);
            EvaluarCandidato(0, s, L, ref mejor, ref mejorDistancia);
            EvaluarCandidato(n - 1, s, L, ref mejor, ref mejorDistancia);

            if (mejor < 0 || mejorDistancia > toleranciaMetros) return false;

            anclaje = _ordenados[mejor];
            return true;
        }

        private void EvaluarCandidato(int indice, float s, float L, ref int mejor, ref float mejorDistancia)
        {
            float d = Mathf.Abs(_clavesS[indice] - s);
            d = Mathf.Min(d, L - d);   // distancia ciclica
            if (d < mejorDistancia)
            {
                mejorDistancia = d;
                mejor = indice;
            }
        }

        /// <summary>Anclaje mas cercano al punto de salida de un cable paralelo al eje Z.</summary>
        public bool TryAnclajeParaCableX(float x0, bool ladoPositivoZ, float toleranciaMetros,
                                         out AnclajeTecho anclaje)
        {
            AsegurarIndice();
            anclaje = default;

            if (!_perimetro.IntersectarX(x0, out float zPos, out float zNeg)) return false;

            Vector2 salida = new Vector2(x0, ladoPositivoZ ? zPos : zNeg);
            return TryAnclajeCercano(_perimetro.SDePunto(salida), toleranciaMetros, out anclaje);
        }

        public bool TryAnclajeParaCableZ(float z0, bool ladoPositivoX, float toleranciaMetros,
                                         out AnclajeTecho anclaje)
        {
            AsegurarIndice();
            anclaje = default;

            if (!_perimetro.IntersectarZ(z0, out float xPos, out float xNeg)) return false;

            Vector2 salida = new Vector2(ladoPositivoX ? xPos : xNeg, z0);
            return TryAnclajeCercano(_perimetro.SDePunto(salida), toleranciaMetros, out anclaje);
        }

        private void AsegurarIndice()
        {
            if (!_indiceValido)
                throw new InvalidOperationException(
                    "El registro no esta indexado. Llamar a Indexar(perimetro) despues de que " +
                    "todos los generadores de tribuna hayan publicado sus anclajes.");
        }

        // ------------------------------------------------------------------
        //  Validacion
        // ------------------------------------------------------------------

        /// <summary>
        /// Devuelve true si el registro sirve para generar el techo. Los mensajes
        /// describen tanto errores como advertencias, para que una version nueva del
        /// estadio avise en vez de producir geometria rota en silencio.
        /// </summary>
        public bool Validar(ParametrosValidacionAnclajes parametros, List<string> mensajes)
        {
            if (mensajes == null) throw new ArgumentNullException(nameof(mensajes));

            if (!_indiceValido)
            {
                mensajes.Add("ERROR: el registro no esta indexado.");
                return false;
            }

            bool valido = true;
            int n = _ordenados.Length;

            if (n < parametros.cantidadMinima)
            {
                mensajes.Add($"ERROR: solo {n} anclajes, se esperaban al menos {parametros.cantidadMinima}.");
                valido = false;
            }

            if (n < 2) return valido;

            if (SeparacionMaximaObservada > parametros.separacionMaximaMetros)
            {
                mensajes.Add($"ERROR: hueco de {SeparacionMaximaObservada:F1} m entre anclajes consecutivos " +
                             $"(maximo {parametros.separacionMaximaMetros:F1} m). Probablemente una tribuna " +
                             "no publico sus vigas.");
                valido = false;
            }

            int desviados = 0;
            float peorDesvio = 0f;
            string tribunaPeor = null;
            for (int i = 0; i < n; i++)
            {
                if (_ordenados[i].desvio > parametros.desvioMaximoMetros)
                {
                    desviados++;
                    if (_ordenados[i].desvio > peorDesvio)
                    {
                        peorDesvio = _ordenados[i].desvio;
                        tribunaPeor = _ordenados[i].idTribuna;
                    }
                }
            }
            if (desviados > 0)
            {
                mensajes.Add($"ERROR: {desviados} anclajes se apartan mas de {parametros.desvioMaximoMetros:F2} m " +
                             $"de la curva ideal (peor: {peorDesvio:F2} m en '{tribunaPeor}'). " +
                             "Revisar que la tribuna use el mismo perimetro, o que se haya publicado " +
                             "antes del Static Batching.");
                valido = false;
            }

            // Asimetria: la curva de coronamiento deberia ser espejo respecto de ambos ejes.
            float L = _perimetro.LongitudTotal;
            float peorAsimetria = 0f;
            float sPeor = 0f;
            const int muestras = 200;
            for (int i = 1; i < muestras; i++)
            {
                float s = L * i / muestras;
                float d = Mathf.Abs(AlturaCoronamiento(s) - AlturaCoronamiento(L - s));
                if (d > peorAsimetria) { peorAsimetria = d; sPeor = s; }
            }
            if (peorAsimetria > parametros.asimetriaMaximaMetros)
            {
                mensajes.Add($"AVISO: asimetria de coronamiento de {peorAsimetria:F2} m en s={sPeor:F1} m. " +
                             "El emparejamiento de tensores va a funcionar igual, pero los cables " +
                             "van a quedar inclinados.");
            }

            if (AlturaMaxima - AlturaMinima < 0.01f)
                mensajes.Add("AVISO: todos los anclajes estan a la misma altura. No habra faldon.");

            return valido;
        }

        public string Diagnostico()
        {
            var sb = new StringBuilder();
            sb.AppendLine($"Registro de anclajes (version {_versionRegistro}, indexado: {_indiceValido})");
            sb.AppendLine($"Publicados: {_publicados.Count} | tras filtrar duplicados: {_ordenados.Length}");

            if (!_indiceValido) return sb.ToString();

            sb.AppendLine($"Altura: min {AlturaMinima:F2} m, max {AlturaMaxima:F2} m, " +
                          $"desnivel {AlturaMaxima - AlturaMinima:F2} m");
            sb.AppendLine($"Separacion maxima entre anclajes: {SeparacionMaximaObservada:F2} m");

            // Extension real de los anclajes, lado por lado. Una platea con mas filas que
            // la otra llega mas lejos del campo, asi que la asimetria puede ser legitima;
            // pero si los dos lados difieren mucho mas que eso, el origen esta corrido.
            float xMin = float.MaxValue, xMax = float.MinValue;
            float zMin = float.MaxValue, zMax = float.MinValue;

            for (int i = 0; i < _ordenados.Length; i++)
            {
                Vector3 p = _ordenados[i].posicion;
                xMin = Mathf.Min(xMin, p.x); xMax = Mathf.Max(xMax, p.x);
                zMin = Mathf.Min(zMin, p.z); zMax = Mathf.Max(zMax, p.z);
            }

            sb.AppendLine($"Extension de los anclajes:");
            sb.AppendLine($"  X: de {xMin:F1} a {xMax:F1} m  (centro en {(xMin + xMax) / 2f:F1}, " +
                          $"semieje {(xMax - xMin) / 2f:F1})");
            sb.AppendLine($"  Z: de {zMin:F1} a {zMax:F1} m  (centro en {(zMin + zMax) / 2f:F1}, " +
                          $"semieje {(zMax - zMin) / 2f:F1})");
            sb.AppendLine($"  Perimetro configurado: semiejeX={_perimetro.SemiejeX:F1}, " +
                          $"semiejeZ={_perimetro.SemiejeZ:F1}");

            var porTribuna = new Dictionary<string, int>();
            for (int i = 0; i < _ordenados.Length; i++)
            {
                string id = _ordenados[i].idTribuna ?? "(sin id)";
                porTribuna.TryGetValue(id, out int c);
                porTribuna[id] = c + 1;
            }
            foreach (var par in porTribuna)
                sb.AppendLine($"  {par.Key}: {par.Value} anclajes");

            return sb.ToString();
        }
    }
}