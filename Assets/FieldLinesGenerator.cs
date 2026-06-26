
using UnityEngine;

public class FieldLinesGenerator : MonoBehaviour
{
    [Header("Dimensiones del Campo")]
    public float largoCampo = 105f;
    public float anchoCampo = 68f;
    public float anchoLinea = 0.12f;

    [Header("Materiales")]
    public Material materialLineaBlanca;

    [ContextMenu("Generar Lineas Completas")]
    public void GenerarLineas()
    {
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            GameObject objetoHijo = transform.GetChild(i).gameObject;
            if (Application.isPlaying) Destroy(objetoHijo);
            else DestroyImmediate(objetoHijo);
        }

        GameObject contenedor = new GameObject("Contenedor_Lineas");
        contenedor.transform.SetParent(this.transform, false);

        float mZ = 1f; // Multiplicador para espejar áreas
        float yH = 0.005f; // Altura para evitar Z-fighting

        // 1. Perímetro y Línea Media
        CrearLineaRecta("Lateral_1", new Vector3(anchoCampo / 2, yH, 0), new Vector3(anchoLinea, largoCampo), contenedor.transform);
        CrearLineaRecta("Lateral_2", new Vector3(-anchoCampo / 2, yH, 0), new Vector3(anchoLinea, largoCampo), contenedor.transform);
        CrearLineaRecta("Meta_1", new Vector3(0, yH, largoCampo / 2), new Vector3(anchoCampo, anchoLinea), contenedor.transform);
        CrearLineaRecta("Meta_2", new Vector3(0, yH, -largoCampo / 2), new Vector3(anchoCampo, anchoLinea), contenedor.transform);
        CrearLineaRecta("Linea_Media", new Vector3(0, yH, 0), new Vector3(anchoCampo, anchoLinea), contenedor.transform);

        // 2. Círculo Central
        CrearCirculo("Circulo_Central", Vector3.zero, 9.15f, 360, 0, contenedor.transform);

        // 3. Áreas (Meta 1 y Meta 2)
        GenerarComplejoArea(largoCampo / 2, 1, contenedor.transform);  // Norte
        GenerarComplejoArea(-largoCampo / 2, -1, contenedor.transform); // Sur

        //puntos de penal y saque

        CrearPunto("PuntoPenal_1", new Vector3(0, yH + 0.001f, largoCampo / 2 - 11f), contenedor.transform);
        CrearPunto("PuntoPenal_2", new Vector3(0, yH + 0.001f, -largoCampo / 2 + 11f), contenedor.transform);
        // Punto Central
        CrearPunto("PuntoCentral", new Vector3(0, yH + 0.001f, 0), contenedor.transform);
    }

    void GenerarComplejoArea(float zMeta, float direccion, Transform padre)
    {
        float yH = 0.005f;
        // Área Grande (16.5m profundidad, 40.32m ancho total)
        float anchoAreaG = 40.32f;
        float profAreaG = 16.5f;

        // Líneas laterales área grande
        CrearLineaRecta("AreaG_Lat_Izqa", new Vector3(-anchoAreaG / 2, yH, zMeta - (profAreaG / 2 * direccion)), new Vector3(anchoLinea, profAreaG), padre);
        CrearLineaRecta("AreaG_Lat_Dcha", new Vector3(anchoAreaG / 2, yH, zMeta - (profAreaG / 2 * direccion)), new Vector3(anchoLinea, profAreaG), padre);
        // Línea frontal área grande
        CrearLineaRecta("AreaG_Frontal", new Vector3(0, yH, zMeta - (profAreaG * direccion)), new Vector3(anchoAreaG, anchoLinea), padre);

        // Área Chica (5.5m profundidad, 18.32m ancho total)
        float anchoAreaC = 18.32f;
        float profAreaC = 5.5f;
        CrearLineaRecta("AreaC_Lat_Izqa", new Vector3(-anchoAreaC / 2, yH, zMeta - (profAreaC / 2 * direccion)), new Vector3(anchoLinea, profAreaC), padre);
        CrearLineaRecta("AreaC_Lat_Dcha", new Vector3(anchoAreaC / 2, yH, zMeta - (profAreaC / 2 * direccion)), new Vector3(anchoLinea, profAreaC), padre);
        CrearLineaRecta("AreaC_Frontal", new Vector3(0, yH, zMeta - (profAreaC * direccion)), new Vector3(anchoAreaC, anchoLinea), padre);

        // Medialuna (Radio 9.15m desde el punto penal que está a 11m)
        Vector3 posPuntoPenal = new Vector3(0, yH, zMeta - (11f * direccion));
        // Dibujamos solo el arco que mira al centro (aprox 120 grados)
        float anguloInicio = (direccion > 0) ? 217 : 37;
        CrearCirculo("Medialuna", posPuntoPenal, 9.15f, 106, anguloInicio, padre);
    }

    void CrearLineaRecta(string nombre, Vector3 pos, Vector3 escalaXZ, Transform padre)
    {
        GameObject linea = GameObject.CreatePrimitive(PrimitiveType.Quad);
        linea.name = nombre;
        linea.transform.SetParent(padre, false);
        linea.transform.localRotation = Quaternion.Euler(90, 0, 0);
        linea.transform.localPosition = pos;
        linea.transform.localScale = new Vector3(escalaXZ.x, escalaXZ.y, 1);
        if (materialLineaBlanca != null) linea.GetComponent<Renderer>().sharedMaterial = materialLineaBlanca;
        DestroyImmediate(linea.GetComponent<MeshCollider>());
    }

    void CrearCirculo(string nombre, Vector3 centro, float radio, int grados, float anguloOffset, Transform padre)
    {
        GameObject go = new GameObject(nombre);
        go.transform.SetParent(padre, false);
        go.transform.localPosition = centro;

        LineRenderer line = go.AddComponent<LineRenderer>();
        line.useWorldSpace = false;
        line.startWidth = anchoLinea;
        line.endWidth = anchoLinea;
        line.sharedMaterial = materialLineaBlanca;
        line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        line.receiveShadows = false;

        int segmentos = 50;
        line.positionCount = segmentos + 1;

        for (int i = 0; i <= segmentos; i++)
        {
            float ang = ((float)i / segmentos) * grados + anguloOffset;
            float x = radio * Mathf.Cos(ang * Mathf.Deg2Rad);
            float z = radio * Mathf.Sin(ang * Mathf.Deg2Rad);
            line.SetPosition(i, new Vector3(x, 0, z));
        }
    }

    void CrearPunto(string nombre, Vector3 pos, Transform padre)
    {
        GameObject punto = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        punto.name = nombre;
        punto.transform.SetParent(padre, false);
        punto.transform.localPosition = pos;
        punto.transform.localScale = new Vector3(0.25f, 0.001f, 0.25f); // Un punto de 25cm de diámetro
        punto.GetComponent<Renderer>().sharedMaterial = materialLineaBlanca;
        DestroyImmediate(punto.GetComponent<CapsuleCollider>());
    }
}