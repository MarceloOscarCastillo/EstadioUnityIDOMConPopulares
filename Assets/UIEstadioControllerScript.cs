using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class UIEstadioController : MonoBehaviour
{
    [Header("Referencias UI")]
    public GameObject panelCarga;
    public TextMeshProUGUI textGenerando;

    public GameObject panelStats;
    public TextMeshProUGUI textCapacidad;
    public TextMeshProUGUI textPopulares;
    public TextMeshProUGUI textPlateas;
    public TextMeshProUGUI textPalcos;
    public TextMeshProUGUI textNombreVariante;
    public Button buttonHome;

    [Header("Referencias Techo")]
    public Estadio.Techo.ControladorTecho controladorTecho;
    public Button buttonVerTecho;
    [Tooltip("Panel con las dos opciones de techo. Se despliega al pasar por Ver Techo y se " +
             "cierra al elegir una o al salir.")]
    public GameObject panelOpcionesTecho;
    public Button buttonTechoIdom;
    public Button buttonTechoOficinaUrbana;
    public Button buttonOcultarTecho;


    [Header("Referencias Frente")]

    public FrontController controladorFrente;
    public Button buttonVerFrente;
    [Tooltip("Panel con las dos opciones de frente. Se despliega al pasar por Ver Frente y se " +
             "cierra al elegir una o al salir.")]
    public GameObject panelOpcionesFrente;
    public Button buttonFrenteIdom;
    public Button buttonFrenteOficinaUrbana;
    public Button buttonOcultarFrente;

    [Header("Referencias de Sistema")]
    public EstadioConfigurator configurator;
    public ContadorDeCapacidad contador;

    [Header("Referencias Menu")]
    public GameObject canvasMenu;

    void Start()
    {
        Debug.Log("UIEstadioController Start");

        // Estado inicial: mostrando carga
        panelCarga.SetActive(false);

        panelStats.SetActive(false);

        buttonHome.onClick.AddListener(OnHomeClick);

        if (panelOpcionesTecho != null) panelOpcionesTecho.SetActive(false);

        if (buttonVerTecho != null)
            buttonVerTecho.onClick.AddListener(AlternarPanelTecho);

        if (buttonTechoIdom != null)
            buttonTechoIdom.onClick.AddListener(() => ElegirTecho(Estadio.Techo.DisenoTecho.Diseno1Membrana));

        if (buttonTechoOficinaUrbana != null)
            buttonTechoOficinaUrbana.onClick.AddListener(() => ElegirTecho(Estadio.Techo.DisenoTecho.Diseno2Reticulado));

        if (buttonOcultarTecho != null)
            buttonOcultarTecho.onClick.AddListener(OcultarTecho);

        // El modo de visibilidad necesita enterarse: un espectador ve cosas muy distintas con
        // y sin techo encima.
        if (controladorTecho != null)
            controladorTecho.TechoCambio += OnTechoCambio;

        ActualizarBotonesTecho();
        
        if (panelOpcionesFrente != null) panelOpcionesFrente.SetActive(false);


        if (buttonVerFrente != null)
            buttonVerFrente.onClick.AddListener(AlternarPanelFrente);

        if (buttonFrenteIdom != null)
            buttonFrenteIdom.onClick.AddListener(() => ElegirFrente(FrontController.DisenoFrente.FrenteIDOM));

        if (buttonFrenteOficinaUrbana != null)
            buttonFrenteOficinaUrbana.onClick.AddListener(() => ElegirFrente(FrontController.DisenoFrente.FrenteConPiel));

        if (buttonOcultarFrente != null)
            buttonOcultarFrente.onClick.AddListener(OcultarFrente);

        ActualizarBotonesFrente();


    }

    public void MostrarCarga()
    {
        panelCarga.SetActive(true);
        panelStats.SetActive(false);
    }

    public void MostrarStats(string nombreVariante = "")
    {
        panelCarga.SetActive(false);
        panelStats.SetActive(true);

        
        if (textNombreVariante != null) 
        {
            textNombreVariante.text = $"Variante: {nombreVariante}";            
        }
            

        ActualizarTextos();
        ActualizarBotonesTecho();
    }

    public void ActualizarTextos()
    {
        textCapacidad.text = $"Capacidad: {contador.capacidadTotal:N0}";
        textPopulares.text = $"Populares: {contador.capacidadPopulares:N0}";
        textPlateas.text = $"Plateas: {contador.capacidadPlateas:N0}";
        textPalcos.text = $"Palcos: {contador.capacidadPalcos:N0}";
    }

    void OnHomeClick()
    {
        panelStats.SetActive(false);
        if (canvasMenu != null)
            canvasMenu.SetActive(true);
        else
        {
            // Fallback: buscar por nombre
            GameObject menu = GameObject.Find("Canvas_Menu");
            if (menu != null) menu.SetActive(true);
        }
    }

    void OnDestroy()
    {
        if (controladorTecho != null)
            controladorTecho.TechoCambio -= OnTechoCambio;
    }

    void AlternarPanelTecho()
    {
        if (panelOpcionesTecho == null) return;
        panelOpcionesTecho.SetActive(!panelOpcionesTecho.activeSelf);
    }

    void ElegirTecho(Estadio.Techo.DisenoTecho cual)
    {
        panelOpcionesTecho?.SetActive(false);

        if (controladorTecho == null)
        {
            Debug.LogWarning("[UI] Falta la referencia al ControladorTecho.");
            return;
        }

        // Cambia directo aunque haya otro techo visible: el usuario elige y lo ve, sin tener
        // que ocultar primero.
        controladorTecho.MostrarDiseno(cual);
    }

    void OcultarTecho()
    {
        panelOpcionesTecho?.SetActive(false);
        controladorTecho?.Ocultar();
    }

    void OnTechoCambio(bool visible)
    {
        ActualizarBotonesTecho();
    }

    /// <summary>
    /// Ocultar techo solo tiene sentido si hay uno puesto. Y el boton de Ver Techo cambia de
    /// texto para que se lea que ya hay uno elegido.
    /// </summary>
    void ActualizarBotonesTecho()
    {
        bool hayTecho = controladorTecho != null && controladorTecho.TechoVisible;

        if (buttonOcultarTecho != null)
            buttonOcultarTecho.gameObject.SetActive(hayTecho);

        if (buttonVerTecho != null)
        {
            var texto = buttonVerTecho.GetComponentInChildren<TextMeshProUGUI>();
            if (texto != null) texto.text = hayTecho ? "Cambiar Techo" : "Ver Techo";
        }
    }
    
    void AlternarPanelFrente()
    {
        if (panelOpcionesFrente == null) return;
        panelOpcionesFrente.SetActive(!panelOpcionesFrente.activeSelf);
    }

    void ElegirFrente(FrontController.DisenoFrente diseno)
    {
        FrontController frente = controladorFrente;
        if (frente != null) frente.CambiarDiseno(diseno);

        panelOpcionesFrente?.SetActive(false);
        ActualizarBotonesFrente();
    }

    void OcultarFrente()
    {
        FrontController frente = controladorFrente;
        if (frente != null) frente.LimpiarFrente();

        panelOpcionesFrente?.SetActive(false);
        ActualizarBotonesFrente();
    }

    void ActualizarBotonesFrente()
    {
        Debug.Log($"controladorFrente={controladorFrente}, FrenteVisible={controladorFrente?.FrenteVisible}");

        bool hayFrente = controladorFrente != null && controladorFrente.FrenteVisible;

        if (buttonOcultarFrente != null)
            buttonOcultarFrente.gameObject.SetActive(hayFrente);

        if (buttonVerFrente != null)
        {
            var texto = buttonVerFrente.GetComponentInChildren<TextMeshProUGUI>();
            if (texto != null) texto.text = hayFrente ? "Cambiar Frente" : "Ver Frente";
        }
    }

}