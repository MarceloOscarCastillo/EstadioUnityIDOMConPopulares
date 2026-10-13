using System.Collections.Generic;
using UnityEngine;

public class PisoOperativoController : MonoBehaviour
{
    public enum TipoPisoOperativo
    {
        CampoDeFutbol,
        ZonaPublico,
        ZonaPublicoCorta
    }

    [Header("Tipo activo")]
    public TipoPisoOperativo tipoActivo = TipoPisoOperativo.CampoDeFutbol;

    [Header("Campo de futbol")]
    [Tooltip("El piso en si: FutbolField")]
    public GameObject pisoCampoFutbol;
    [Tooltip("Lineas, cesped, arcos, carteles: todo lo que solo existe con campo de futbol")]
    public List<GameObject> elementosCampoFutbol = new List<GameObject>();

    [Header("Zona de publico")]
    [Tooltip("El piso en si: PisoZonaPublico")]
    public GameObject pisoZonaPublico;
    [Tooltip("Canchas y elementos propios de la version arena")]
    public List<GameObject> elementosZonaPublico = new List<GameObject>();

    [Header("Zona de publico - corta")]
    public GameObject pisoZonaPublicoCorta;
    public List<GameObject> elementosZonaPublicoCorta = new List<GameObject>();


    [Header("Capacidad")]
    [Tooltip("Solo la zona de publico suma espectadores de pie")]
    public float espectadoresPorM2 = 3f;

    [HideInInspector] public float areaPisoOperativo = 0f;
    [HideInInspector] public int capacidadPisoOperativo = 0;

    //public bool SumaCapacidad => tipoActivo == TipoPisoOperativo.ZonaPublico;

    public bool SumaCapacidad => tipoActivo != TipoPisoOperativo.CampoDeFutbol;

    GameObject PisoActivo()
    {
        switch (tipoActivo)
        {
            case TipoPisoOperativo.ZonaPublicoCorta: return pisoZonaPublicoCorta;
            case TipoPisoOperativo.ZonaPublico: return pisoZonaPublico;
        }
        return null;
    }

    [ContextMenu("Aplicar Piso")]
    public void AplicarPiso()
    {
        bool esFutbol = tipoActivo == TipoPisoOperativo.CampoDeFutbol;
        bool esCorta = tipoActivo == TipoPisoOperativo.ZonaPublicoCorta;
        bool esLarga = tipoActivo == TipoPisoOperativo.ZonaPublico;

        if (pisoCampoFutbol != null) pisoCampoFutbol.SetActive(esFutbol);
        foreach (GameObject go in elementosCampoFutbol)
            if (go != null) go.SetActive(esFutbol);

        if (pisoZonaPublico != null) pisoZonaPublico.SetActive(!esFutbol);
        foreach (GameObject go in elementosZonaPublico)
            if (go != null) go.SetActive(!esFutbol);

        if (pisoZonaPublicoCorta != null) pisoZonaPublicoCorta.SetActive(esCorta);
        foreach (GameObject go in elementosZonaPublicoCorta)
            if (go != null) go.SetActive(esCorta);

        CalcularCapacidadPiso();
    }

    public void CambiarTipo(TipoPisoOperativo nuevo)
    {
        tipoActivo = nuevo;
        AplicarPiso();
    }

    //public void CalcularCapacidadPiso()
    //{
    //    areaPisoOperativo = 0f;
    //    capacidadPisoOperativo = 0;

    //    if (!SumaCapacidad || pisoZonaPublico == null) return;

    //    areaPisoOperativo = CalcularAreaHorizontal(pisoZonaPublico);
    //    capacidadPisoOperativo = Mathf.FloorToInt(areaPisoOperativo * espectadoresPorM2);
    //}

    public void CalcularCapacidadPiso()
    {
        areaPisoOperativo = 0f;
        capacidadPisoOperativo = 0;

        GameObject piso = PisoActivo();
        if (piso == null) return;

        areaPisoOperativo = CalcularAreaHorizontal(piso);
        capacidadPisoOperativo = Mathf.FloorToInt(areaPisoOperativo * espectadoresPorM2);
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

                float ax = b.x - a.x, az = b.z - a.z;
                float bx = c.x - a.x, bz = c.z - a.z;

                area += Mathf.Abs(ax * bz - az * bx) / 2f;
            }
        }

        return area;
    }
}