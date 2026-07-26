using UnityEngine;
using System.Collections.Generic;

public class EstadioConfigurator : MonoBehaviour
{
    // Las distintas variantes de tu estadio
    public enum TipoConfiguracion
    {
        Inauguracion,
        EstadioPopularesSoloCabecerasYCodosInferiores,
        EstadioPopularesEn2CodosSuperiores,
        EstadioConPopularLateralBaja,
        EstadioTodosLosCodosPopulares,
        EstadioConPopularLateralAlta,
        PopularesAbajoPlateasArriba,
        CabecerasProlongadas,
        MaximaCapacidad,
        Asimetrico,
        IDOMOriginal,
        TerceraBandejaMarmol
    }

    [System.Serializable]
    public struct DatosVariante
    {
        public TipoConfiguracion variante;
        public string nombre;
        public int capacidadTotal;
        public int capacidadPopulares;
        public int capacidadPlateas;
        public int capacidadPalcos;
    }

    // Como variable de instancia:
    [HideInInspector]
    public List<DatosVariante> variantesConsultadas = new List<DatosVariante>();

    [System.Serializable]
    public struct PerfilEstadio
    {
        [Tooltip("Nombre de la variante que estás configurando (Ej: Estadio Grande)")]
        public TipoConfiguracion nombreVariante;

        [Tooltip("Arrastrá acá todos los objetos de tu jerarquía que querés que se VEAN en esta variante.")]
        // Al usar MonoBehaviour, Unity guarda la referencia exacta a ESE objeto de la escena.
        // Un mismo objeto (Ej: Platea Norte) puede estar metido en las listas de varios perfiles a la vez.
        public List<MonoBehaviour> sectoresActivos;
    }

    [Header("Variante del Estadio Seleccionada")]
    [Tooltip("Elegí qué variante querés aplicar en el Inspector.")]
    public TipoConfiguracion varianteAActivar;

    [Header("Perfiles de Configuración")]
    [Tooltip("Definí acá tus variantes y qué objetos (controllers) se prenden en cada una.")]
    public List<PerfilEstadio> perfilesDeEstadio = new List<PerfilEstadio>();

    private TipoConfiguracion? varianteAnterior = null;
    private PerfilEstadio perfilAnterior;

    [ContextMenu("Aplicar Configuración Seleccionada")]
public void AplicarConfiguracionEstadio()
{
    if (perfilesDeEstadio == null || perfilesDeEstadio.Count == 0)
    {
        Debug.LogWarning("No hay perfiles configurados en el Inspector.");
        return;
    }

    // 1. Buscamos el perfil que elegiste en el menú desplegable
    PerfilEstadio perfilElegido = default;
    bool encontrado = false;
    foreach (PerfilEstadio perfil in perfilesDeEstadio)
    {
        if (perfil.nombreVariante == varianteAActivar)
        {
            perfilElegido = perfil;
            encontrado = true;
            break;
        }
    }

    if (!encontrado)
    {
        Debug.LogError($"No creaste ningún perfil en la lista para la variante: {varianteAActivar}");
        return;
    }

    // En modo diseño, regenerar primero los sectores del perfil
    if (!Application.isPlaying)
    {
        foreach (MonoBehaviour sector in perfilElegido.sectoresActivos)
        {
                Debug.Log($"Sector en perfil: {sector?.gameObject.name}, tipo: {sector?.GetType().Name}");
                if (sector is StandGenerator sg) sg.GenerarSector();
            else if (sector is SeatedStandGenerator ssg) ssg.GenerarSector();
            else if (sector is UpperCurveStandWithWalkpathScript uc) uc.GenerarCodo();
            else if (sector is PalcosBuilderScript pb) pb.GenerarPalcos();
                else if (sector is SharedComponentsController sc) sc.GenerarComponentesCompartidos();
            }
    }

    // 2. Juntamos una lista maestra de TODOS los controladores que existen en tu estadio
    HashSet<MonoBehaviour> todosLosSectoresDelEstadio = ObtenerTodosLosSectores();
    

    // 3. Prendemos o apagamos cada objeto de la escena según corresponda
    foreach (MonoBehaviour sector in todosLosSectoresDelEstadio)
    {
        if (sector == null) continue;
        bool debeActivarse = perfilElegido.sectoresActivos.Contains(sector);
        
        SetearEstadoSector(sector, debeActivarse);
    }

    // Recalcular la capacidad global automáticamente
    ContadorDeCapacidad contador = Object.FindFirstObjectByType<ContadorDeCapacidad>();
        if (contador != null) 
        {  
            contador.CalcularCapacidad();

            DatosVariante datos = new DatosVariante
            {
                variante = varianteAActivar,
                nombre = varianteAActivar.ToString(),
                capacidadTotal = contador.capacidadTotal,
                capacidadPopulares = contador.capacidadPopulares,
                capacidadPlateas = contador.capacidadPlateas,
                capacidadPalcos = contador.capacidadPalcos
            };

            int idx = variantesConsultadas.FindIndex(v => v.nombre == datos.nombre);
            if (idx >= 0) variantesConsultadas[idx] = datos;
            else variantesConsultadas.Add(datos);

            Debug.Log($"Variante guardada: {datos.nombre}, Total: {datos.capacidadTotal}, Populares: {datos.capacidadPopulares}, Plateas: {datos.capacidadPlateas}, Palcos: {datos.capacidadPalcos}");
        }

    Debug.Log($"[EstadioConfigurator] Se aplicó la variante '{varianteAActivar}'. Se encendieron {perfilElegido.sectoresActivos.Count} controladores.");
}


    private HashSet<MonoBehaviour> ObtenerTodosLosSectores()
    {
        HashSet<MonoBehaviour> listaMaestra = new HashSet<MonoBehaviour>();

        // Buscar todos los controllers en la escena directamente
        foreach (var s in Object.FindObjectsByType<StandGenerator>(FindObjectsSortMode.None)) listaMaestra.Add(s);
        foreach (var s in Object.FindObjectsByType<SeatedStandGenerator>(FindObjectsSortMode.None)) listaMaestra.Add(s);
        foreach (var s in Object.FindObjectsByType<UpperCurveStandWithWalkpathScript>(FindObjectsSortMode.None)) listaMaestra.Add(s);
        foreach (var s in Object.FindObjectsByType<PalcosBuilderScript>(FindObjectsSortMode.None)) listaMaestra.Add(s);
        foreach (var s in Object.FindObjectsByType<SharedComponentsController>(FindObjectsSortMode.None))
            listaMaestra.Add(s);

        return listaMaestra;
    }

    private void SetearEstadoSector(MonoBehaviour controller, bool activar)
    {
        foreach (Transform hijo in controller.transform)
        {
            if (hijo.CompareTag("SectorEstadio"))
            {
                if (activar)
                    hijo.gameObject.SetActive(true);
                else
                {
                    if (Application.isPlaying)
                        Destroy(hijo.gameObject);
                    else
                        DestroyImmediate(hijo.gameObject);
                }
            }
        }

        if (controller is SharedComponentsController sc)
        {
            foreach (MonoBehaviour componente in sc.componentesCompartidos)
            {
                if (componente == null) continue;
                foreach (Transform hijo in componente.transform)
                {
                    if (hijo.CompareTag("SectorEstadio"))
                    {
                        if (activar)
                            hijo.gameObject.SetActive(true);
                        else
                            DestroyImmediate(hijo.gameObject);
                    }
                }
            }
        }
    }


    void Start()
    {

        //Comentar todos los Start() de los generadores antes de usar esto
        //StartCoroutine(GenerarYConfigurar());
    }

    public System.Collections.IEnumerator GenerarYConfigurar()
    {
        UIEstadioController ui = Object.FindFirstObjectByType<UIEstadioController>();
        if (ui != null) ui.MostrarCarga();

        // Buscar perfil nuevo
        PerfilEstadio perfilNuevo = default;
        bool encontrado = false;
        foreach (PerfilEstadio perfil in perfilesDeEstadio)
        {
            if (perfil.nombreVariante == varianteAActivar)
            {
                perfilNuevo = perfil;
                encontrado = true;
                break;
            }
        }

        if (!encontrado)
        {
            Debug.LogError($"No encontre perfil para: {varianteAActivar}");
            yield break;
        }

        if (varianteAnterior == null)
        {
            // Primera vez: generar todo
            foreach (MonoBehaviour sector in perfilNuevo.sectoresActivos)
            {
                GenerarSector(sector);
                yield return null;
            }
        }
        else
        {
            // Comparar con variante anterior
            // Desactivar/destruir los que estaban y ya no estan
            foreach (MonoBehaviour sector in perfilAnterior.sectoresActivos)
            {
                if (!perfilNuevo.sectoresActivos.Contains(sector))
                {
                    // Destruir contenedor
                    foreach (Transform hijo in sector.transform)
                    {
                        if (hijo.CompareTag("SectorEstadio"))
                            Destroy(hijo.gameObject);
                    }
                }
            }

            yield return null; // esperar que se destruyan

            // Generar los que son nuevos en esta variante
            foreach (MonoBehaviour sector in perfilNuevo.sectoresActivos)
            {
                if (!perfilAnterior.sectoresActivos.Contains(sector))
                {
                    GenerarSector(sector);
                    yield return null;
                }
            }
        }

        // Guardar variante actual como anterior
        varianteAnterior = varianteAActivar;
        perfilAnterior = perfilNuevo;

        AplicarConfiguracionEstadio();
        //if (ui != null) ui.MostrarStats();
        if (ui != null) ui.MostrarStats(NombresVariantes.ObtenerNombre(varianteAActivar));
    }

    private void GenerarSector(MonoBehaviour sector)
    {
        Debug.Log($"GenerarSector llamado para: {sector?.gameObject.name}, tipo: {sector?.GetType().Name}");
        if (sector is StandGenerator sg) sg.GenerarSector();
        else if (sector is SeatedStandGenerator ssg) ssg.GenerarSector();
        else if (sector is UpperCurveStandWithWalkpathScript uc) uc.GenerarCodo();
        else if (sector is PalcosBuilderScript pb) pb.GenerarPalcos();
        else if (sector is SharedComponentsController sc) sc.GenerarComponentesCompartidos();
    }

    [ContextMenu("Limpiar Escena")]
    public void LimpiarEscena()
    {
        HashSet<MonoBehaviour> todos = ObtenerTodosLosSectores();
        foreach (MonoBehaviour sector in todos)
        {
            foreach (Transform hijo in sector.transform)
            {
                if (hijo.CompareTag("SectorEstadio"))
                    DestroyImmediate(hijo.gameObject);
            }

            if (sector is SharedComponentsController sc)
            {
                foreach (MonoBehaviour componente in sc.componentesCompartidos)
                {
                    if (componente == null) continue;
                    foreach (Transform hijo in componente.transform)
                    {
                        if (hijo.CompareTag("SectorEstadio"))
                            DestroyImmediate(hijo.gameObject);
                    }
                }
            }
        }
        
        Debug.Log("[EstadioConfigurator] Escena limpiada.");
    }

}



