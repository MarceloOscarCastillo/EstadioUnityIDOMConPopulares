using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

public class MenuSeleccionController : MonoBehaviour
{
    [Header("Referencias UI")]
    public GameObject canvasMenu;
    public Transform panelCards;
    public GameObject panelTooltip;
    public TextMeshProUGUI textTooltip;

    [Header("Prefabs")]
    public GameObject prefabCard;

    [Header("Referencias Sistema")]
    public EstadioConfigurator configurator;
    public UIEstadioController uiEstadio;

    [Header("Referencias Recaudaciones")]
    public Button buttonRecaudaciones;
    public RecaudacionesController recaudacionesController;

    // Diccionario de nombres cortos
    private Dictionary<EstadioConfigurator.TipoConfiguracion, string> nombresCortos =
        new Dictionary<EstadioConfigurator.TipoConfiguracion, string>()
    {
        { EstadioConfigurator.TipoConfiguracion.Inauguracion, "Inauguración" },
        { EstadioConfigurator.TipoConfiguracion.EstadioPopularesSoloCabecerasYCodosInferiores, "Populares Solo Cabeceras" },
        { EstadioConfigurator.TipoConfiguracion.EstadioPopularesEn2CodosSuperiores, "Populares Cabeceras y 2 Codos Sup." },
        { EstadioConfigurator.TipoConfiguracion.EstadioConPopularLateralBaja, "Popular Lateral Baja" },
        { EstadioConfigurator.TipoConfiguracion.EstadioTodosLosCodosPopulares, "Todos los Codos Populares" },
        { EstadioConfigurator.TipoConfiguracion.EstadioConPopularLateralAlta, "Popular Lateral Alta" },
        { EstadioConfigurator.TipoConfiguracion.PopularesAbajoPlateasArriba, "Populares Abajo, Plateas Arriba" },
        { EstadioConfigurator.TipoConfiguracion.CabecerasProlongadas, "Cabeceras Prolongadas" },
        { EstadioConfigurator.TipoConfiguracion.MaximaCapacidad, "Máxima Capacidad" },
        { EstadioConfigurator.TipoConfiguracion.Asimetrico, "Cabeceras asimetricas" },
        { EstadioConfigurator.TipoConfiguracion.IDOMOriginal, "IDOM Original" },
        { EstadioConfigurator.TipoConfiguracion.Sugerida, "Sugerida" },
        { EstadioConfigurator.TipoConfiguracion.SugeridaAmpliada, "SugeridaAmpliada" },
        { EstadioConfigurator.TipoConfiguracion.TerceraBandejaMarmol, "Tercera bandeja sobre José Mármol" }
    };

    // Diccionario de descripciones (tooltips)
    private Dictionary<EstadioConfigurator.TipoConfiguracion, string> descripciones =
        new Dictionary<EstadioConfigurator.TipoConfiguracion, string>()
    {
        { EstadioConfigurator.TipoConfiguracion.Inauguracion,
            "Configuración que permite alcanzar los 60.000 espectadores sin haber construido los codos superiores.\n\nConsiste en 2 cabeceras de populares, los 4 codos inferiores también son populares, así como los dos sectores sobre José Mármol.\n\nLas plateas están todas sobre Avenida La Plata.\n\nEl estadio podría inaugurarse así." },
        { EstadioConfigurator.TipoConfiguracion.EstadioPopularesSoloCabecerasYCodosInferiores,
            "Esta versión tiene populares sólo en las cabeceras y en los codos inferiores. Los 4 codos superiores son plateas.\n\nTodas las tribunas laterales tienen asientos." },
        { EstadioConfigurator.TipoConfiguracion.EstadioPopularesEn2CodosSuperiores,
            "Esta versión tiene populares en las cabeceras, en los codos inferiores y en 2 de los codos superiores, del lado de la calle Inclán.\n\nTodas las tribunas laterales tienen asientos." },
        { EstadioConfigurator.TipoConfiguracion.EstadioConPopularLateralBaja,
            "Esta versión tiene populares en las cabeceras, en los codos inferiores y en 2 de los codos superiores, del lado de la calle Inclán.\n\nTodas las tribunas laterales tienen asientos con la excepción de la tribuna baja sobre Mármol." },
        { EstadioConfigurator.TipoConfiguracion.EstadioTodosLosCodosPopulares,
            "En esta versión las cabeceras son populares, así como los codos interiores y superiores.\n\nTodas las tribunas laterales son plateas con asientos." },
        { EstadioConfigurator.TipoConfiguracion.EstadioConPopularLateralAlta,
            "En esta versión las cabeceras son populares, así como los codos interiores y superiores.\n\nTodas las tribunas laterales son plateas con asientos con la excepción de la segunda bandeja sobre José Mármol." },
        { EstadioConfigurator.TipoConfiguracion.PopularesAbajoPlateasArriba,
            "Esta versión tiene todo el anillo inferior de populares, mientras que el nivel superior es de plateas" },
        { EstadioConfigurator.TipoConfiguracion.CabecerasProlongadas,
            "En esta versión las populares de las cabeceras se prolongan, ocupando parte de las plateas laterales inferiores." },
        { EstadioConfigurator.TipoConfiguracion.MaximaCapacidad,
            "En esta versión además de prolongarse las cabeceras se agrega una popular lateral alta." },
        { EstadioConfigurator.TipoConfiguracion.Asimetrico,
            "En esta versión una cabecera tiene una bandeja y la otra dos" },
        { EstadioConfigurator.TipoConfiguracion.IDOMOriginal,
            "Es la versión original del proyecto IDOM. Es all seater, no hay tribunas sin asientos.\n\nLas cabeceras tienen dos bandejas." },
        { EstadioConfigurator.TipoConfiguracion.Sugerida,
            "Presenta un equilibrio entre capacidad, perfecta visibilidad y variantes para ubicar al público. Es asimétrico, con dos bandejas en una cabecera y una sola en la otra. Tiene muchas plateas bajas y una popular lateral alta." },
        { EstadioConfigurator.TipoConfiguracion.SugeridaAmpliada,
            "Es la ampliación de la variante llamada 'Sugerida'. La platea lateral alta tiene 20 escalones más, los codos superiores sobre Mármol son más amplios y tiene una segunda bandeja sobre la popular local" },
        { EstadioConfigurator.TipoConfiguracion.TerceraBandejaMarmol,
            "Esta versión tiene como rasgo distintivo una tercera bandeja de plateas sobre la calle José Mármol. Sería una posibla ampliación del estadio" }
    };

    void Start()
    {
        panelTooltip.SetActive(false);
        GenerarCards();

        buttonRecaudaciones.onClick.AddListener(OnRecaudacionesClick);
    }

    void GenerarCards()
    {
        foreach (var variante in nombresCortos)
        {
            GameObject cardGO = Instantiate(prefabCard, panelCards);
            TextMeshProUGUI texto = cardGO.GetComponentInChildren<TextMeshProUGUI>();
            texto.text = variante.Value;

            // Capturar variante para el closure
            EstadioConfigurator.TipoConfiguracion varianteCapturada = variante.Key;

            // Click: cargar estadio
            Button boton = cardGO.GetComponent<Button>();
            boton.onClick.AddListener(() => OnCardClick(varianteCapturada));

            // Hover: mostrar tooltip
            var trigger = cardGO.AddComponent<UnityEngine.EventSystems.EventTrigger>();

            var entradaEnter = new UnityEngine.EventSystems.EventTrigger.Entry();
            entradaEnter.eventID = UnityEngine.EventSystems.EventTriggerType.PointerEnter;
            entradaEnter.callback.AddListener((e) => MostrarTooltip(varianteCapturada));
            trigger.triggers.Add(entradaEnter);

            var entradaExit = new UnityEngine.EventSystems.EventTrigger.Entry();
            entradaExit.eventID = UnityEngine.EventSystems.EventTriggerType.PointerExit;
            entradaExit.callback.AddListener((e) => OcultarTooltip());
            trigger.triggers.Add(entradaExit);
        }
    }

    void MostrarTooltip(EstadioConfigurator.TipoConfiguracion variante)
    {
        panelTooltip.SetActive(true);
        textTooltip.text = descripciones[variante];
    }

    void OcultarTooltip()
    {
        panelTooltip.SetActive(false);
    }

    void OnCardClick(EstadioConfigurator.TipoConfiguracion variante)
    {
        configurator.varianteAActivar = variante;
        canvasMenu.SetActive(false);
        StartCoroutine(configurator.GenerarYConfigurar());
    }

    void OnRecaudacionesClick()
    {
        Debug.Log($"OnRecaudacionesClick, recaudacionesController={recaudacionesController}");
        canvasMenu.SetActive(false);
        recaudacionesController.MostrarPantalla();
    }
}
