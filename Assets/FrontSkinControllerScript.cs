using System.Collections.Generic;
using UnityEngine;

public class StadiumFrontSkin : MonoBehaviour
{
    [System.Serializable]
    public struct ModuloPiel
    {
        public string nombre;
        public GameObject prefab;
        [Min(0f)] public float peso;
    }

    [Header("Dimensiones de la pared")]
    public float anchoPared = 180f;
    public float altoPared = 20f;
    public float alturaBase = 0f;

    [Header("Capa 2 - Modulos")]
    public float ladoModulo = 3.95f;
    public float separacionModulos = 0.05f;
    public float separacionCapas = 0.6f;
    public List<ModuloPiel> modulos = new List<ModuloPiel>();
    public int semilla = 12345;

    [Header("Capa 1 - Reticulado")]
    public bool generarReticulado = true;
    public float anchoRectangulo = 1.0f;
    public float altoRectangulo = 0.2f;
    public float diametroTubo = 0.05f;
    public Material materialTubo;

    private const string NOMBRE_CONTENEDOR = "Contenedor_Piel";

    [ContextMenu("Generar Piel")]
    public void GenerarPiel()
    {
        LimpiarPiel();

        GameObject contenedor = new GameObject(NOMBRE_CONTENEDOR);
        contenedor.transform.SetParent(this.transform, false);
        contenedor.tag = "SectorEstadio";

        GenerarModulos(contenedor.transform);
        if (generarReticulado) GenerarReticulado(contenedor.transform);

        foreach (Transform hijo in contenedor.GetComponentsInChildren<Transform>())
        {
            if (hijo.gameObject != contenedor && Application.isPlaying)
                hijo.gameObject.isStatic = true;
        }

        if (Application.isPlaying)
            StaticBatchingUtility.Combine(contenedor);
    }

    void GenerarModulos(Transform padre)
    {
        float paso = ladoModulo + separacionModulos;
        if (paso <= 0f) return;

        int columnas = Mathf.FloorToInt((anchoPared + separacionModulos) / paso);
        int filas = Mathf.FloorToInt((altoPared + separacionModulos) / paso);
        if (columnas <= 0 || filas <= 0) return;

        float anchoUsado = columnas * paso - separacionModulos;
        float altoUsado = filas * paso - separacionModulos;
        float xInicio = -anchoUsado / 2f + ladoModulo / 2f;
        float yInicio = alturaBase + (altoPared - altoUsado) / 2f + ladoModulo / 2f;

        System.Random rnd = new System.Random(semilla);

        for (int f = 0; f < filas; f++)
        {
            for (int c = 0; c < columnas; c++)
            {
                GameObject prefab = ElegirPrefab(rnd);
                if (prefab == null) continue;

                Vector3 posLocal = new Vector3(
                    xInicio + c * paso,
                    yInicio + f * paso,
                    separacionCapas);

                GameObject modulo = Instantiate(prefab, padre);
                modulo.name = $"Modulo_{f}_{c}";
                modulo.transform.position = transform.TransformPoint(posLocal);
                modulo.transform.rotation = transform.rotation;
            }
        }
    }

    GameObject ElegirPrefab(System.Random rnd)
    {
        float total = 0f;
        foreach (ModuloPiel m in modulos)
            if (m.prefab != null) total += Mathf.Max(0f, m.peso);

        if (total <= 0f) return null;

        float r = (float)rnd.NextDouble() * total;
        float acum = 0f;

        foreach (ModuloPiel m in modulos)
        {
            if (m.prefab == null) continue;
            acum += Mathf.Max(0f, m.peso);
            if (r <= acum) return m.prefab;
        }

        return null;
    }

    void GenerarReticulado(Transform padre)
    {
        if (anchoRectangulo <= 0f || altoRectangulo <= 0f) return;

        float xIzq = -anchoPared / 2f;
        float xDer = anchoPared / 2f;
        float yAbajo = alturaBase;
        float yArriba = alturaBase + altoPared;

        int cantVerticales = Mathf.FloorToInt(anchoPared / anchoRectangulo);
        int cantHorizontales = Mathf.FloorToInt(altoPared / altoRectangulo);

        float sobranteX = anchoPared - cantVerticales * anchoRectangulo;
        float sobranteY = altoPared - cantHorizontales * altoRectangulo;

        for (int i = 0; i <= cantVerticales; i++)
        {
            float x = xIzq + sobranteX / 2f + i * anchoRectangulo;
            CrearTubo(new Vector3(x, yAbajo, 0f), new Vector3(x, yArriba, 0f), padre);
        }

        for (int j = 0; j <= cantHorizontales; j++)
        {
            float y = yAbajo + sobranteY / 2f + j * altoRectangulo;
            CrearTubo(new Vector3(xIzq, y, 0f), new Vector3(xDer, y, 0f), padre);
        }
    }

    void CrearTubo(Vector3 aLocal, Vector3 bLocal, Transform padre)
    {
        GameObject tubo = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        tubo.name = "Tubo_Reticulado";
        tubo.transform.SetParent(padre);

        Collider col = tubo.GetComponent<Collider>();
        if (col != null)
        {
            if (Application.isPlaying) Destroy(col);
            else DestroyImmediate(col);
        }

        Vector3 a = transform.TransformPoint(aLocal);
        Vector3 b = transform.TransformPoint(bLocal);

        tubo.transform.position = (a + b) / 2f;
        tubo.transform.up = (b - a).normalized;
        tubo.transform.localScale = new Vector3(
            diametroTubo,
            Vector3.Distance(a, b) / 2f,
            diametroTubo);

        if (materialTubo != null)
            tubo.GetComponent<Renderer>().sharedMaterial = materialTubo;
    }

    [ContextMenu("Limpiar Piel")]
    public void LimpiarPiel()
    {
        Transform viejo = transform.Find(NOMBRE_CONTENEDOR);
        if (viejo == null) return;

        if (Application.isPlaying) Destroy(viejo.gameObject);
        else DestroyImmediate(viejo.gameObject);
    }
}
