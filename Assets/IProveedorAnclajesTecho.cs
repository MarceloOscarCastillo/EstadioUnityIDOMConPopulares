using System.Collections.Generic;
using UnityEngine;

public enum RolEstructuralTecho
{
    /// <summary>Plateas y tribunas paralelas al eje largo del campo. Son las que
    /// sostienen el techo.</summary>
    ParaleloAlCampo,
    /// <summary>Cabeceras. Sus cables longitudinales mueren en el puente en vez de
    /// continuar sobre el campo.</summary>
    DetrasDelArco,
    /// <summary>Codo. Estructura propia de techo, no sostenida por la grada.</summary>
    Codo
}

/// <summary>
/// Lo unico que el techo necesita saber de un sector. El techo no conoce StandGenerator
/// ni SeatedStandGenerator: solo pide cabezas de tensor y un rol.
/// </summary>
public interface IProveedorAnclajesTecho
{
    bool PublicaAnclajesTecho { get; }
    RolEstructuralTecho RolEnElTecho { get; }
    string IdParaTecho { get; }

    /// <summary>
    /// Cabezas de tensor en coordenadas locales del sector, cacheadas DURANTE la
    /// generacion. Se cachean y no se recalculan porque los overrides por variante
    /// restauran numFilas despues de generar: recalcular mas tarde daria alturas
    /// equivocadas justo en las variantes con override.
    /// </summary>
    IReadOnlyList<Vector3> CabezasTensoresLocales { get; }

    Transform TransformSector { get; }
}

