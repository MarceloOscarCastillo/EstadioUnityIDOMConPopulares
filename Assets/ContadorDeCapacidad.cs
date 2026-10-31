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
    { "AsientoPlatea", "AsientoBidegain", "AsientoCodoSurPopular" };

    [Header("Filtros de Contenedores")]
    public string tagContenedores = "SectorEstadio";

    [Header("Anchos de pieza por sector")]
    public float anchoPiezaCabecera = 1.0f;

    [Header("Correccion por paraavalanchas")]
    [Tooltip("Espectadores que se descuentan por cada paraavalanchas instanciado")]
    public int espectadoresPorParaavalancha = 4;

    [HideInInspector] public int totalParaavalanchas = 0;
    [HideInInspector] public int descuentoParaavalanchas = 0;


    public PisoOperativoController pisoOperativo;

    [HideInInspector] public int capacidadPisoEventos = 0;
    [HideInInspector] public float areaPisoEventos = 0f;

    [HideInInspector] public int capacidadTotal;
    [HideInInspector] public int capacidadPopulares;
    [HideInInspector] public int capacidadPlateas;
    [HideInInspector] public int capacidadPalcos;

    [System.Serializable]
    public class DetalleSubsector
    {
        public string sector;
        public string subsector;
        public int orden;
        public int capacidad;
    }

    [HideInInspector] public List<DetalleSubsector> detallePorSubsector = new List<DetalleSubsector>();



    [ContextMenu("Calcular Capacidad Total")]
    public void CalcularCapacidad()
    {

        totalParaavalanchas = 0;

        // 1. Buscamos todos los contenedores generados por tus scripts
        GameObject[] contenedores = GameObject.FindGameObjectsWithTag(tagContenedores);

        int aforoAsientos = 0;
        int aforoPorMetros = 0;
        int totalPalcos = 0;
        float metrosLinealesTotales = 0f;

       
        UnityEngine.Debug.Log($"Buscando con tag: {tagContenedores}. Encontrados: {contenedores.Length}");

        //foreach (GameObject contenedor in contenedores)
        //{
        //    UnityEngine.Debug.Log($"Analizando contenedor: {contenedor.name}");
        //    string nombreLower = contenedor.name.ToLower();

        //    // 1. INTENTAMOS CONTAR ASIENTOS PRIMERO
        //    int asientosEncontrados = ContarHijosPorNombre(contenedor, "Asiento");

        //    if (asientosEncontrados > 0)
        //    {
        //        // Si hay asientos, priorizamos el conteo físico (Plateas, Codos con asientos, etc.)
        //        aforoAsientos += asientosEncontrados;
        //    }
        //    else
        //    {
        //        // 2. SI NO HAY ASIENTOS, CALCULAMOS POR METROS LINEALES

        //        UpperCurveStandWithWalkpathScript generador = contenedor.transform.parent?.GetComponent<UpperCurveStandWithWalkpathScript>();
        //        if (generador != null)
        //        {
        //            generador.CalcularMetrosLinealesInternos(); // siempre recalcular
        //            metrosLinealesTotales += generador.metrosLinealesCalculados;
        //            aforoPorMetros += Mathf.FloorToInt(generador.metrosLinealesCalculados * personasPorMetroLineal);
        //        }
        //        else
        //        {
        //            float metrosSector = CalcularMetrosLineales(contenedor);
        //            if (metrosSector > 0)
        //            {
        //                metrosLinealesTotales += metrosSector;
        //                aforoPorMetros += Mathf.FloorToInt(metrosSector * personasPorMetroLineal);
        //            }
        //        }


        //    }

        //    // CASO C: Palcos y paraavalanchas (Independiente de si el sector es popular o platea)
        //    totalPalcos += ContarHijosPorNombre(contenedor, "Palco");
        //    totalParaavalanchas += ContarHijosPorNombre(contenedor, "Paraavalanchas");
        //}

        detallePorSubsector.Clear();

        foreach (GameObject contenedor in contenedores)
        {
            int aporteContenedor = 0;

            int asientosEncontrados = ContarHijosPorNombre(contenedor, "Asiento");

            if (asientosEncontrados > 0)
            {
                aforoAsientos += asientosEncontrados;
                aporteContenedor += asientosEncontrados;
            }
            else
            {
                float metrosDeEsteContenedor = 0f;

                UpperCurveStandWithWalkpathScript generador =
                    contenedor.transform.parent?.GetComponent<UpperCurveStandWithWalkpathScript>();

                if (generador != null)
                {
                    //generador.CalcularMetrosLinealesInternos();
                    metrosDeEsteContenedor = generador.metrosLinealesCalculados;
                }
                else
                {
                    metrosDeEsteContenedor = CalcularMetrosLineales(contenedor);
                }

                if (metrosDeEsteContenedor > 0)
                {
                    int personas = Mathf.FloorToInt(metrosDeEsteContenedor * personasPorMetroLineal);
                    metrosLinealesTotales += metrosDeEsteContenedor;
                    aforoPorMetros += personas;
                    aporteContenedor += personas;
                }
            }

            int palcosAqui = ContarHijosPorNombre(contenedor, "Palco");
            totalPalcos += palcosAqui;
            aporteContenedor += palcosAqui * 8;

            int paraAqui = ContarHijosPorNombre(contenedor, "Paraavalanchas");
            totalParaavalanchas += paraAqui;
            aporteContenedor -= paraAqui * espectadoresPorParaavalancha;

            IdentidadSector identidad = contenedor.transform.parent?.GetComponent<IdentidadSector>();

            if (identidad != null && !string.IsNullOrEmpty(identidad.sector))
            {
                detallePorSubsector.Add(new DetalleSubsector
                {
                    sector = identidad.sector,
                    subsector = identidad.subsector,
                    orden = identidad.orden,
                    capacidad = aporteContenedor
                });
            }
        }

        descuentoParaavalanchas = totalParaavalanchas * espectadoresPorParaavalancha;

        // Asumimos una capacidad promedio por Palco (puedes ajustar este número)
        int aforoPalcos = totalPalcos * 8;


        capacidadPisoEventos = 0;
        if (pisoOperativo != null)
        {
            pisoOperativo.CalcularCapacidadPiso();
            capacidadPisoEventos = pisoOperativo.capacidadPisoOperativo;
        }


        foreach (GameObject contenedor in contenedores)
        {
            UnityEngine.Debug.Log($"Contenedor: {contenedor.name}, activo: {contenedor.activeSelf}, activoEnJerarquia: {contenedor.activeInHierarchy}");
        }


        ImprimirReporteDeCapacidades(aforoPorMetros, aforoAsientos, totalPalcos, aforoPalcos, metrosLinealesTotales);

        UnityEngine.Debug.Log(ArmarTextoDetalle());

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
    
    private void ImprimirReporteDeCapacidades(int aforoMetros, int aforoAsientos, int palcos, int aforoPalcos, float metros)
    {
        int granTotal = aforoMetros + aforoAsientos + aforoPalcos + capacidadPisoEventos -descuentoParaavalanchas;

        capacidadPopulares = aforoMetros;
        capacidadPlateas = aforoAsientos;
        capacidadPalcos = aforoPalcos;
        capacidadTotal = granTotal;

        UnityEngine.Debug.Log($"<color=cyan><b>--- REPORTE TÉCNICO DE AFORO ---</b></color>");
        UnityEngine.Debug.Log($"<b>Sectores Populares:</b> {aforoMetros} personas ({metros:F1} metros lineales)");
        UnityEngine.Debug.Log($"<b>Sectores Plateas:</b> {aforoAsientos} asientos físicos");
        UnityEngine.Debug.Log($"<b>Palcos:</b> {palcos} unidades (Est. {aforoPalcos} personas)");

        if (totalParaavalanchas > 0)
            UnityEngine.Debug.Log($"<b>Paraavalanchas:</b> {totalParaavalanchas} unidades (-{descuentoParaavalanchas} personas)");

        if (pisoOperativo != null && pisoOperativo.SumaCapacidad)
            UnityEngine.Debug.Log($"<b>Piso operativo:</b> {pisoOperativo.capacidadPisoOperativo} personas ({pisoOperativo.areaPisoOperativo:F1} m² a {pisoOperativo.espectadoresPorM2} pers/m²)");

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

    public string ArmarTextoDetalle()
    {
        var porSector = new Dictionary<string, List<DetalleSubsector>>();

        foreach (DetalleSubsector d in detallePorSubsector)
        {
            if (!porSector.ContainsKey(d.sector))
                porSector[d.sector] = new List<DetalleSubsector>();
            porSector[d.sector].Add(d);
        }

        var sb = new System.Text.StringBuilder();

        foreach (var par in porSector)
        {
            int totalSector = 0;
            foreach (DetalleSubsector d in par.Value) totalSector += d.capacidad;

            sb.AppendLine($"<b>{par.Key}: {totalSector:N0} espectadores</b>");

            par.Value.Sort((a, b) => a.orden.CompareTo(b.orden));

            foreach (DetalleSubsector d in par.Value)
                sb.AppendLine($"    {d.subsector}: {d.capacidad:N0}");

            sb.AppendLine();
        }

        if (pisoOperativo != null && pisoOperativo.SumaCapacidad)
        {
            sb.AppendLine($"<b>Piso operativo: {pisoOperativo.capacidadPisoOperativo:N0} espectadores</b>");
            sb.AppendLine($"    {pisoOperativo.areaPisoOperativo:F0} m² a {pisoOperativo.espectadoresPorM2} personas por m²");
            sb.AppendLine();
        }

        sb.AppendLine($"<b>TOTAL: {capacidadTotal:N0} espectadores</b>");

        return sb.ToString();
    }

}