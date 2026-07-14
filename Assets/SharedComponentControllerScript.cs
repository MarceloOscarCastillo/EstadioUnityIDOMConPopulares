using UnityEngine;
using System.Collections.Generic;

public class SharedComponentsController : MonoBehaviour
{
    [Header("Scripts compartidos")]
    public List<MonoBehaviour> componentesCompartidos;

    void Start()
    {
        //if (Application.isPlaying)
        //    GenerarComponentesCompartidos();
    }

    [ContextMenu("Generar Componentes")]
    public void GenerarComponentesCompartidos()
    {
        Debug.Log($"GenerarComponentes llamado, cantidad: {componentesCompartidos?.Count}");
        foreach (MonoBehaviour componente in componentesCompartidos)
        {
            Debug.Log($"Generando: {componente?.gameObject.name}, tipo: {componente?.GetType().Name}");
            if (componente is GeneradorEscaleraGiratoria geg) geg.GenerarEscalera();
            else if (componente is GeneradorEscaleraArquitectonica gea) gea.GenerarEscalera();
            else if (componente is PielEstadio pe) pe.GenerarPiel();
            else if (componente is CampoDeJuego cdj) cdj.GenerarCarteles();
            else if (componente is StandsDoorsAndWallsScript sdaw) sdaw.GenerarParedConPuertas();
        }
    }
}
