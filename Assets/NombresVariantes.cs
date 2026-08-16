using System.Collections.Generic;

public static class NombresVariantes
{
    public static Dictionary<EstadioConfigurator.TipoConfiguracion, string> Nombres =
        new Dictionary<EstadioConfigurator.TipoConfiguracion, string>()
    {
        { EstadioConfigurator.TipoConfiguracion.IDOMOriginal, "IDOM Original" },
        { EstadioConfigurator.TipoConfiguracion.Preinauguracion, "Preinauguración" },
        { EstadioConfigurator.TipoConfiguracion.EstadioPopularesSoloCabecerasYCodosInferiores, "Populares Solo Cabeceras y Codos Inferiores" },
        { EstadioConfigurator.TipoConfiguracion.EstadioPopularesEn2CodosSuperiores, "Populares Cabeceras y 2 Codos Superiores" },
        { EstadioConfigurator.TipoConfiguracion.EstadioConPopularLateralBaja, "Popular Lateral Baja" },
        { EstadioConfigurator.TipoConfiguracion.EstadioTodosLosCodosPopulares, "Todos los Codos Populares" },
        { EstadioConfigurator.TipoConfiguracion.EstadioConPopularLateralAlta, "Popular Lateral Alta" },
        { EstadioConfigurator.TipoConfiguracion.PopularesAbajoPlateasArriba, "Populares Abajo, Plateas Arriba" },
        { EstadioConfigurator.TipoConfiguracion.PlateasYCodosMarmolAmpliados, "Plateas y codos ampliados sector José Mármol" },
        { EstadioConfigurator.TipoConfiguracion.CabecerasProlongadas, "Cabeceras Prolongadas" },
        { EstadioConfigurator.TipoConfiguracion.MaximaCapacidad, "Máxima Capacidad" },
        { EstadioConfigurator.TipoConfiguracion.Asimetrico, "Asimétrico" },

        { EstadioConfigurator.TipoConfiguracion.Sugerida, "Sugerida" },
        { EstadioConfigurator.TipoConfiguracion.SugeridaAmpliada, "Sugerida Ampliada" },
        { EstadioConfigurator.TipoConfiguracion.TerceraBandejaMarmol, "Tercera bandeja sobre José Mármol" },

        { EstadioConfigurator.TipoConfiguracion.Recitales, "Recitales" },
        { EstadioConfigurator.TipoConfiguracion.AmpliacionFinal, "Ampliación final" },
        { EstadioConfigurator.TipoConfiguracion.TooMuch, "No será demasiado?" }
    };

    public static string ObtenerNombre(EstadioConfigurator.TipoConfiguracion variante)
    {
        if (Nombres.TryGetValue(variante, out string nombre))
            return nombre;
        return variante.ToString();
    }
}
