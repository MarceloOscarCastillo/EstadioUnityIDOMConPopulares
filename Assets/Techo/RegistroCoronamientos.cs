using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace Estadio.Techo
{
    /// <summary>
    /// Un punto del borde superior de una grada. No sostiene el techo: marca hasta donde
    /// tiene que bajar la tela para cerrar el estadio.
    /// </summary>
    [Serializable]
    public struct PuntoCoronamiento
    {
        public Vector3 posicion;      // coordenadas del techo
        public string idSector;

        // Derivado al indexar
        public float s;               // longitud de arco sobre el perimetro del estadio
        public float desvio;          // distancia XZ a la curva ideal
    }

    /// <summary>
    /// Borde superior de todo el estadio, publicado sector por sector.
    ///
    /// Es un registro aparte del de anclajes porque son cosas distintas: el anclaje es la
    /// punta del tensor, por donde pasa la tela; el coronamiento es el muro de la grada,
    /// hasta donde la tela baja. Entre uno y otro esta el faldon.
    ///
    /// Publican TODOS los sectores, no solo los que sostienen el techo: el faldon recorre
    /// el perimetro entero y en los laterales es minimo, no inexistente. Y como el
    /// coronamiento varia dentro de una misma seccion —recorte de filas, bandejas
    /// distintas— hace falta la polilinea completa y no un valor por sector.
    /// </summary>
    public sealed class RegistroCoronamientos
    {
        private readonly List<PuntoCoronamiento> _publicados = new List<PuntoCoronamiento>(512);

        private PuntoCoronamiento[] _ordenados = Array.Empty<PuntoCoronamiento>();
        private float[] _clavesS = Array.Empty<float>();

        private IPerimetroEstadio _perimetro;
        private int _versionRegistro;
        private bool _indiceValido;

        public int VersionRegistro => _versionRegistro;
        public int CantidadPublicados => _publicados.Count;
        public int CantidadPuntos => _ordenados.Length;
        public bool IndiceValido => _indiceValido;

        public float AlturaMaxima { get; private set; }
        public float AlturaMinima { get; private set; }
        public float SeparacionMaximaObservada { get; private set; }

        public IReadOnlyList<PuntoCoronamiento> Puntos => _ordenados;

        // ------------------------------------------------------------------
        //  Publicacion
        // ------------------------------------------------------------------

        public void Limpiar()
        {
            _publicados.Clear();
            _ordenados = Array.Empty<PuntoCoronamiento>();
            _clavesS = Array.Empty<float>();
            _indiceValido = false;
            _versionRegistro++;
        }

        public void Publicar(Vector3 posicion, string idSector)
        {
            _publicados.Add(new PuntoCoronamiento { posicion = posicion, idSector = idSector });
            _indiceValido = false;
            _versionRegistro++;
        }

        public void PublicarPolilinea(IReadOnlyList<Vector3> puntos, string idSector)
        {
            if (puntos == null) return;
            for (int i = 0; i < puntos.Count; i++) Publicar(puntos[i], idSector);
        }

        // ------------------------------------------------------------------
        //  Indexado
        // ------------------------------------------------------------------

        public void Indexar(IPerimetroEstadio perimetro, float separacionMinimaMetros = 0.30f)
        {
            if (perimetro == null) throw new ArgumentNullException(nameof(perimetro));

            _perimetro = perimetro;

            var proyectados = new List<PuntoCoronamiento>(_publicados.Count);
            for (int i = 0; i < _publicados.Count; i++)
            {
                PuntoCoronamiento p = _publicados[i];
                perimetro.Proyectar(p.posicion.AXZ(), out _, out p.s, out _, out p.desvio);
                proyectados.Add(p);
            }

            proyectados.Sort((a, b) => a.s.CompareTo(b.s));

            // Donde dos sectores se solapan en la costura se conserva el mas alto: es el que
            // manda para cerrar, porque la tela tiene que pasar por encima de todo.
            var filtrados = new List<PuntoCoronamiento>(proyectados.Count);
            for (int i = 0; i < proyectados.Count; i++)
            {
                if (filtrados.Count > 0)
                {
                    PuntoCoronamiento ultimo = filtrados[filtrados.Count - 1];
                    if (proyectados[i].s - ultimo.s < separacionMinimaMetros)
                    {
                        if (proyectados[i].posicion.y > ultimo.posicion.y)
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
                float y = _ordenados[i].posicion.y;
                if (y > AlturaMaxima) AlturaMaxima = y;
                if (y < AlturaMinima) AlturaMinima = y;
            }

            if (_ordenados.Length == 0) { AlturaMaxima = 0f; AlturaMinima = 0f; }

            SeparacionMaximaObservada = CalcularSeparacionMaxima();
            _indiceValido = true;
        }

        private float CalcularSeparacionMaxima()
        {
            if (_ordenados.Length < 2) return 0f;

            float L = _perimetro.LongitudTotal;
            float maxima = 0f;

            for (int i = 1; i < _clavesS.Length; i++)
                maxima = Mathf.Max(maxima, _clavesS[i] - _clavesS[i - 1]);

            return Mathf.Max(maxima, L - _clavesS[_clavesS.Length - 1] + _clavesS[0]);
        }

        // ------------------------------------------------------------------
        //  Consultas
        // ------------------------------------------------------------------

        /// <summary>
        /// Altura del muro superior a la longitud de arco s, interpolada y ciclica. Es el
        /// piso al que llega el faldon.
        /// </summary>
        public float AlturaEnS(float s)
        {
            AsegurarIndice();

            int n = _ordenados.Length;
            if (n == 0) return 0f;
            if (n == 1) return _ordenados[0].posicion.y;

            float L = _perimetro.LongitudTotal;
            s = Mathf.Repeat(s, L);

            if (s < _clavesS[0] || s >= _clavesS[n - 1])
            {
                float sa = _clavesS[n - 1];
                float sb = _clavesS[0] + L;
                float sx = s < _clavesS[0] ? s + L : s;
                return Mathf.Lerp(_ordenados[n - 1].posicion.y, _ordenados[0].posicion.y,
                                  Mathf.InverseLerp(sa, sb, sx));
            }

            int lo = 0, hi = n - 1;
            while (hi - lo > 1)
            {
                int mid = (lo + hi) >> 1;
                if (_clavesS[mid] <= s) lo = mid; else hi = mid;
            }

            return Mathf.Lerp(_ordenados[lo].posicion.y, _ordenados[hi].posicion.y,
                              Mathf.InverseLerp(_clavesS[lo], _clavesS[hi], s));
        }

        /// <summary>Altura del muro superior debajo de un punto del techo.</summary>
        public float AlturaBajoPunto(Vector2 puntoXZ)
        {
            AsegurarIndice();
            return AlturaEnS(_perimetro.SDePunto(puntoXZ));
        }

        private void AsegurarIndice()
        {
            if (!_indiceValido)
                throw new InvalidOperationException(
                    "El registro de coronamientos no esta indexado. Llamar a Indexar(perimetro) " +
                    "despues de que todos los sectores hayan publicado.");
        }

        // ------------------------------------------------------------------
        //  Validacion y diagnostico
        // ------------------------------------------------------------------

        public bool Validar(List<string> mensajes, int cantidadMinima = 40,
                            float separacionMaxima = 12f, float desvioMaximo = 6f)
        {
            if (mensajes == null) throw new ArgumentNullException(nameof(mensajes));

            if (!_indiceValido)
            {
                mensajes.Add("ERROR: el registro de coronamientos no esta indexado.");
                return false;
            }

            bool valido = true;

            if (_ordenados.Length < cantidadMinima)
            {
                mensajes.Add($"ERROR: solo {_ordenados.Length} puntos de coronamiento. El faldon " +
                             "necesita el borde superior de todo el perimetro para saber hasta " +
                             "donde bajar.");
                valido = false;
            }

            if (_ordenados.Length >= 2 && SeparacionMaximaObservada > separacionMaxima)
            {
                mensajes.Add($"ERROR: hueco de {SeparacionMaximaObservada:F1} m en el coronamiento " +
                             $"(maximo {separacionMaxima:F1} m). Algun sector no esta publicando.");
                valido = false;
            }

            int desviados = 0;
            float peor = 0f;
            string sectorPeor = null;

            for (int i = 0; i < _ordenados.Length; i++)
            {
                if (_ordenados[i].desvio <= desvioMaximo) continue;
                desviados++;
                if (_ordenados[i].desvio > peor)
                {
                    peor = _ordenados[i].desvio;
                    sectorPeor = _ordenados[i].idSector;
                }
            }

            if (desviados > 0)
            {
                mensajes.Add($"AVISO: {desviados} puntos de coronamiento se apartan mas de " +
                             $"{desvioMaximo:F1} m de la superelipse (peor: {peor:F1} m en " +
                             $"'{sectorPeor}'). Afecta la precision del faldon en esa zona.");
            }

            return valido;
        }

        public string Diagnostico()
        {
            var sb = new StringBuilder();
            sb.AppendLine($"Registro de coronamientos (version {_versionRegistro}, " +
                          $"indexado: {_indiceValido})");
            sb.AppendLine($"Publicados: {_publicados.Count} | tras filtrar costuras: {_ordenados.Length}");

            if (!_indiceValido) return sb.ToString();

            sb.AppendLine($"Altura: min {AlturaMinima:F2} m, max {AlturaMaxima:F2} m, " +
                          $"desnivel {AlturaMaxima - AlturaMinima:F2} m");
            sb.AppendLine($"Separacion maxima entre puntos: {SeparacionMaximaObservada:F2} m");

            var porSector = new Dictionary<string, int>();
            for (int i = 0; i < _ordenados.Length; i++)
            {
                string id = _ordenados[i].idSector ?? "(sin id)";
                porSector.TryGetValue(id, out int c);
                porSector[id] = c + 1;
            }

            foreach (var par in porSector)
                sb.AppendLine($"  {par.Key}: {par.Value} puntos");

            return sb.ToString();
        }
    }
}
