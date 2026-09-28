using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;
using UnityEngine.ProBuilder.Shapes;

public class ContadorDeCapacidad : MonoBehaviour
{
    [Header("Configuración de Aforo (Populares/Codos Inf)")]
    [Tooltip("Espectadores por metro lineal en sectores sin asientos físicos")]
    public float personasPorMetroLineal = 2.0f; // 1 persona cada 0.50m = 2 por metro

    [Header("Referencias de Prefabs para Conteo")]
    public GameObject prefabAsientoPlatea;
    public GameObject prefabPalco;

    [Header("Deteccion de asientos")]
    public List<string> nombresPrefabsAsiento = new List<string>()
    { "AsientoPlatea", "AsientoBidegain" };

    [Header("Filtros de Contenedores")]
    public string tagContenedores = "SectorEstadio";

    [Header("Anchos de pieza por sector")]
    public float anchoPiezaCabecera = 1.0f;


    [Header("Piso para eventos")]
    public bool esVersionParaEventos = false;
    public GameObject pisoParaEventos;
    public float espectadoresPorM2 = 3f;

    [HideInInspector] public int capacidadPisoEventos = 0;
    [HideInInspector] public float areaPisoEventos = 0f;

    [HideInInspector] public int capacidadTotal;
    [HideInInspector] public int capacidadPopulares;
    [HideInInspector] public int capacidadPlateas;
    [HideInInspector] public int capacidadPalcos;

    [ContextMenu("Calcular Capacidad Total")]
    public void CalcularCapacidad()
    {
        
        // 1. Buscamos todos los contenedores generados por tus scripts
        GameObject[] contenedores = GameObject.FindGameObjectsWithTag(tagContenedores);

        int aforoAsientos = 0;
        int aforoPorMetros = 0;
        int totalPalcos = 0;
        float metrosLinealesTotales = 0f;

       
        UnityEngine.Debug.Log($"Buscando con tag: {tagContenedores}. Encontrados: {contenedores.Length}");

        foreach (GameObject contenedor in contenedores)
        {
            UnityEngine.Debug.Log($"Analizando contenedor: {contenedor.name}");
            string nombreLower = contenedor.name.ToLower();

            // 1. INTENTAMOS CONTAR ASIENTOS PRIMERO
            int asientosEncontrados = ContarHijosPorNombre(contenedor, "Asiento");

            if (asientosEncontrados > 0)
            {
                // Si hay asientos, priorizamos el conteo físico (Plateas, Codos con asientos, etc.)
                aforoAsientos += asientosEncontrados;
            }
            else
            {
                // 2. SI NO HAY ASIENTOS, CALCULAMOS POR METROS LINEALES
               
                UpperCurveStandWithWalkpathScript generador = contenedor.transform.parent?.GetComponent<UpperCurveStandWithWalkpathScript>();
                if (generador != null)
                {
                    generador.CalcularMetrosLinealesInternos(); // siempre recalcular
                    metrosLinealesTotales += generador.metrosLinealesCalculados;
                    aforoPorMetros += Mathf.FloorToInt(generador.metrosLinealesCalculados * personasPorMetroLineal);
                }
                else
                {
                    float metrosSector = CalcularMetrosLineales(contenedor);
                    if (metrosSector > 0)
                    {
                        metrosLinealesTotales += metrosSector;
                        aforoPorMetros += Mathf.FloorToInt(metrosSector * personasPorMetroLineal);
                    }
                }


            }

            // CASO C: Palcos (Independiente de si el sector es popular o platea)
            totalPalcos += ContarHijosPorNombre(contenedor, "Palco");
        }

        // Asumimos una capacidad promedio por Palco (puedes ajustar este número)
        int aforoPalcos = totalPalcos * 8;


        capacidadPisoEventos = 0;
        areaPisoEventos = 0f;

        if (esVersionParaEventos && pisoParaEventos != null)
        {
            areaPisoEventos = CalcularAreaHorizontal(pisoParaEventos);
            capacidadPisoEventos = Mathf.FloorToInt(areaPisoEventos * espectadoresPorM2);
        }


        foreach (GameObject contenedor in contenedores)
        {
            UnityEngine.Debug.Log($"Contenedor: {contenedor.name}, activo: {contenedor.activeSelf}, activoEnJerarquia: {contenedor.activeInHierarchy}");
        }


        ImprimirReporteDeCapacidades(aforoPorMetros, aforoAsientos, totalPalcos, aforoPalcos, metrosLinealesTotales);        
    }


    private float CalcularMetrosLineales(GameObject padre)
    {
        float metros = 0;

        Transform[] todosLosHijos = padre.GetComponentsInChildren<Transform>(false);

        foreach (Transform hijo in todosLosHijos)
        {
            if (hijo.name == "Escalon_Cabecera")
                metros += anchoPiezaCabecera;
        }
        return metros;
    }

   
    private int ContarHijosPorNombre(GameObject padre, string fragmentoNombre)
    {
        int cuenta = 0;
        Transform[] todosLosHijos = padre.GetComponentsInChildren<Transform>(false);

        foreach (Transform hijo in todosLosHijos)
        {
            if (fragmentoNombre == "Asiento")
            {
                foreach (string nombre in nombresPrefabsAsiento)
                {
                    if (hijo.name.StartsWith(nombre))
                    {
                        cuenta++;
                        break;
                    }
                }
            }
            else
            {
                if (hijo.name.Contains(fragmentoNombre)) cuenta++;
            }
        }

        return cuenta;
    }

    //private void ImprimirReportePro(int aforoMetros, int aforoAsientos, int palcos, int aforoPalcos, float metros)
    //{
    //    int granTotal = aforoMetros + aforoAsientos + aforoPalcos;

    //    capacidadPopulares = aforoMetros;
    //    capacidadPlateas = aforoAsientos;
    //    capacidadPalcos = aforoPalcos;
    //    capacidadTotal = granTotal;

    //    UnityEngine.Debug.Log($"<color=cyan><b>--- REPORTE TÉCNICO DE AFORO ---</b></color>");
    //    UnityEngine.Debug.Log($"<b>Sectores Populares:</b> {aforoMetros} personas ({metros:F1} metros lineales)");
    //    UnityEngine.Debug.Log($"<b>Sectores Plateas:</b> {aforoAsientos} asientos físicos");
    //    UnityEngine.Debug.Log($"<b>Palcos:</b> {palcos} unidades (Est. {aforoPalcos} personas)");
    //    UnityEngine.Debug.Log($"<color=yellow><b>CAPACIDAD TOTAL FINAL: {granTotal} espectadores</b></color>");
    //    UnityEngine.Debug.Log($"-------------------------------------------");
    //}


    private void ImprimirReporteDeCapacidades(int aforoMetros, int aforoAsientos, int palcos, int aforoPalcos, float metros)
    {
        int granTotal = aforoMetros + aforoAsientos + aforoPalcos + capacidadPisoEventos;

        capacidadPopulares = aforoMetros;
        capacidadPlateas = aforoAsientos;
        capacidadPalcos = aforoPalcos;
        capacidadTotal = granTotal;

        UnityEngine.Debug.Log($"<color=cyan><b>--- REPORTE TÉCNICO DE AFORO ---</b></color>");
        UnityEngine.Debug.Log($"<b>Sectores Populares:</b> {aforoMetros} personas ({metros:F1} metros lineales)");
        UnityEngine.Debug.Log($"<b>Sectores Plateas:</b> {aforoAsientos} asientos físicos");
        UnityEngine.Debug.Log($"<b>Palcos:</b> {palcos} unidades (Est. {aforoPalcos} personas)");

        if (esVersionParaEventos)
            UnityEngine.Debug.Log($"<b>Piso para eventos:</b> {capacidadPisoEventos} personas ({areaPisoEventos:F1} m² a {espectadoresPorM2} pers/m²)");

        UnityEngine.Debug.Log($"<color=yellow><b>CAPACIDAD TOTAL FINAL: {granTotal} espectadores</b></color>");
        UnityEngine.Debug.Log($"-------------------------------------------");
    }


    float CalcularAreaHorizontal(GameObject obj)
    {
        if (obj == null) return 0f;

        float area = 0f;

        foreach (MeshFilter mf in obj.GetComponentsInChildren<MeshFilter>())
        {
            Mesh mesh = mf.sharedMesh;
            if (mesh == null) continue;

            Vector3[] v = mesh.vertices;
            int[] tris = mesh.triangles;

            for (int i = 0; i < tris.Length; i += 3)
            {
                Vector3 a = mf.transform.TransformPoint(v[tris[i]]);
                Vector3 b = mf.transform.TransformPoint(v[tris[i + 1]]);
                Vector3 c = mf.transform.TransformPoint(v[tris[i + 2]]);

                // Proyeccion horizontal: ignoramos Y
                float ax = b.x - a.x, az = b.z - a.z;
                float bx = c.x - a.x, bz = c.z - a.z;

                area += Mathf.Abs(ax * bz - az * bx) / 2f;
            }
        }

        return area;
    }
}