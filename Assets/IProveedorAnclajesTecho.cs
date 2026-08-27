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
    IReadOnlyList<Vector3> CabezasTensoresLocales { get; }
    IReadOnlyList<Vector3> CoronamientoLocal { get; }

    /// <summary>Geometria del soporte de este sector. Solo tiene sentido en los que
    /// publican anclajes; el resto devuelve default.</summary>
    GeometriaSoporte GeometriaDelSoporte { get; }

    Transform TransformSector { get; }
}

public struct GeometriaSoporte
{
    public float distanciaVerticalExterior;
    public float distanciaVerticalInterior;

    /// <summary>Pendiente de la viga diagonal: metros que sube por metro que avanza hacia
    /// afuera. Los soportes de codo la continuan para que no haya quiebre.</summary>
    public float pendienteDiagonal;

    public bool EsValida => distanciaVerticalInterior > distanciaVerticalExterior;
}

