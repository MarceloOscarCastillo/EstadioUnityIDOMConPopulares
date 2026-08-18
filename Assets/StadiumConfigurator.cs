using UnityEngine;
using System.Collections.Generic;
using Unity.VisualScripting;
using Estadio.Techo;

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
        Sugerida,
        SugeridaAmpliada,
        TerceraBandejaMarmol,
        PlateasYCodosMarmolAmpliados,
        Preinauguracion,
        Recitales,
        AmpliacionFinal,
        TooMuch
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

    [System.NonSerialized]
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

    [Header("Techo")]
    [Tooltip("Objeto que define el origen y la orientacion del sistema del techo: " +
         "centro del campo, ejes alineados. Si queda vacio se usa el mundo.")]
    public Transform origenTecho;

    private readonly RegistroAnclajesTecho registroTecho = new RegistroAnclajesTecho();
    public RegistroAnclajesTecho RegistroTecho => registroTecho;

    public Matrix4x4 MatrizTecho => origenTecho != null
        ? origenTecho.worldToLocalMatrix
        : Matrix4x4.identity;


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

                if (sector is StandGenerator sg)
                {
                    var ovrd = sg.overridesNumFilas.Find(o => o.variante == varianteAActivar);
                    if (ovrd.variante == varianteAActivar)
                    {
                        int numFilasOriginal = sg.numFilas;
                        sg.numFilas = ovrd.numFilas;
                        sg.GenerarSector();
                        sg.numFilas = numFilasOriginal;
                    }
                    else
                        sg.GenerarSector();
                }

                else if (sector is SeatedStandGenerator ssg)
                {
                    // Aplicar override si existe para esta variante
                    var ovrd = ssg.overridesNumFilas.Find(o => o.variante == varianteAActivar);
                    if (ovrd.variante == varianteAActivar)
                    {
                        int numFilasOriginal = ssg.numFilas;
                        ssg.numFilas = ovrd.numFilas;
                        ssg.GenerarSector();
                        ssg.numFilas = numFilasOriginal; // restaurar valor original
                    }
                    else
                        ssg.GenerarSector();

                }
                else if (sector is UpperCurveStandWithWalkpathScript uc)
                {
                    var ovrd = uc.overridesParametros.Find(o => o.variante == varianteAActivar);
                    if (ovrd.variante == varianteAActivar)
                    {
                        float radioOriginal = uc.radioInferior;
                        int filasMaxOriginal = uc.filasMaximas;
                        int filasMinOriginal = uc.filasMinimas;

                        uc.radioInferior = ovrd.radioInferior;
                        uc.filasMaximas = ovrd.filasMaximas;
                        uc.filasMinimas = ovrd.filasMinimas;

                        uc.GenerarCodo();

                        uc.radioInferior = radioOriginal;
                        uc.filasMaximas = filasMaxOriginal;
                        uc.filasMinimas = filasMinOriginal;
                    }
                    else
                        uc.GenerarCodo();
                }
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

        RecolectarAnclajesTecho(perfilElegido);

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
            foreach (MonoBehaviour sectorEnPerfilAnterior in perfilAnterior.sectoresActivos)
            {
                if (!perfilNuevo.sectoresActivos.Contains(sectorEnPerfilAnterior))
                {
                    // Destruir contenedor
                    foreach (Transform hijo in sectorEnPerfilAnterior.transform)
                    {
                        if (hijo.CompareTag("SectorEstadio"))
                            Destroy(hijo.gameObject);
                    }

                    yield return null; // esperar que se destruyan
                }

                else
                {
                    bool tieneOverride = TieneOverrideEnVariante(sectorEnPerfilAnterior, varianteAnterior);

                    if (tieneOverride)
                    {
                        // Si tiene override, destruirlo primero

                        foreach (Transform hijo in sectorEnPerfilAnterior.transform)
                        {
                            if (hijo.CompareTag("SectorEstadio"))
                                Destroy(hijo.gameObject);
                        }
                        yield return null;

                        GenerarSector(sectorEnPerfilAnterior);

                        yield return null;
                    }
                }
            }
            // Generar los sectores específicos de esta variante
            foreach (MonoBehaviour sectorEnPerfilNuevo in perfilNuevo.sectoresActivos)
            {
                if (perfilAnterior.sectoresActivos.Contains(sectorEnPerfilNuevo))
                {
                    bool tieneOverrideAnterior = TieneOverrideEnVariante(sectorEnPerfilNuevo, varianteAnterior);
                    bool tieneOverrideNuevo = TieneOverrideEnVariante(sectorEnPerfilNuevo, varianteAActivar);

                    if (tieneOverrideAnterior || tieneOverrideNuevo)
                    {
                        GenerarSector(sectorEnPerfilNuevo);
                        yield return null;
                    }
                }
                else
                {
                    GenerarSector(sectorEnPerfilNuevo);
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

        if (sector is StandGenerator sg)
        {
            var ovrd = sg.overridesNumFilas.Find(o => o.variante == varianteAActivar);
            if (ovrd.variante == varianteAActivar)
            {
                int original = sg.numFilas;
                sg.numFilas = ovrd.numFilas;
                sg.GenerarSector();
                sg.numFilas = original;
            }
            else sg.GenerarSector();
        }
        else if (sector is SeatedStandGenerator ssg)
        {
            var ovrd = ssg.overridesNumFilas.Find(o => o.variante == varianteAActivar);
            if (ovrd.variante == varianteAActivar)
            {
                int original = ssg.numFilas;
                ssg.numFilas = ovrd.numFilas;
                ssg.GenerarSector();
                ssg.numFilas = original;
            }
            else ssg.GenerarSector();
        }
        else if (sector is UpperCurveStandWithWalkpathScript uc)
        {
            var ovrd = uc.overridesParametros.Find(o => o.variante == varianteAActivar);
            if (ovrd.variante == varianteAActivar)
            {
                float radioOriginal = uc.radioInferior;
                int filasMaxOriginal = uc.filasMaximas;
                int filasMinOriginal = uc.filasMinimas;
                uc.radioInferior = ovrd.radioInferior;
                uc.filasMaximas = ovrd.filasMaximas;
                uc.filasMinimas = ovrd.filasMinimas;
                uc.GenerarCodo();
                uc.radioInferior = radioOriginal;
                uc.filasMaximas = filasMaxOriginal;
                uc.filasMinimas = filasMinOriginal;
            }
            else uc.GenerarCodo();
        }
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

    private bool TieneOverrideEnVariante(MonoBehaviour sector, TipoConfiguracion? variante)
    {
        if(variante == null) return false;

        if (sector is StandGenerator sg)
            return sg.overridesNumFilas.Exists(o => o.variante == variante);
        if (sector is SeatedStandGenerator ssg)
            return ssg.overridesNumFilas.Exists(o => o.variante == variante);
        if (sector is UpperCurveStandWithWalkpathScript uc)
            return uc.overridesParametros.Exists(o => o.variante == variante);
        return false;
    }

    
    /// <summary>
    /// Recolecta las cabezas de tensor de todos los sectores activos que sostienen el
    /// techo. Se llama despues de generar todos los sectores; es seguro porque las
    /// cabezas estan cacheadas desde la generacion y no dependen del estado actual de
    /// los parametros ni de los transforms, que a esta altura ya fueron batcheados.
    /// </summary>
    public void RecolectarAnclajesTecho(PerfilEstadio perfil)
    {
        registroTecho.Limpiar();

        Matrix4x4 mundoALocal = MatrizTecho;
        int publicados = 0;

        foreach (MonoBehaviour sector in perfil.sectoresActivos)
        {
            if (!(sector is IProveedorAnclajesTecho proveedor)) continue;
            if (!proveedor.PublicaAnclajesTecho) continue;

            IReadOnlyList<Vector3> cabezas = proveedor.CabezasTensoresLocales;
            if (cabezas == null || cabezas.Count == 0) continue;

            Transform t = proveedor.TransformSector;

            for (int i = 0; i < cabezas.Count; i++)
            {
                Vector3 mundo = t.TransformPoint(cabezas[i]);
                Vector3 local = mundoALocal.MultiplyPoint3x4(mundo);
                Vector3 eje = mundoALocal.MultiplyVector(t.TransformVector(Vector3.up));

                registroTecho.Publicar(local, eje, proveedor.IdParaTecho, i);
                publicados++;
            }
        }

        Debug.Log($"[Techo] {publicados} anclajes recolectados de {perfil.sectoresActivos.Count} sectores.");
    }

}



