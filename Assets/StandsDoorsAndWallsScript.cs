using UnityEngine;

public class StandsDoorsAndWallsScript : MonoBehaviour
{
    
    
    [Header("Prefab")]
    public GameObject prefabGateway;

    [Header("Paredes")]
    public float largoPared1 = 5f;
    public float largoPared2 = 5f;
    public float largoPared3 = 5f;
    public float profundidadPared = 0.1f;
    public Material materialPared;

    [Header("Gateway")]
    public float altoGateway = 5.4f;
    public float anchoGateway = 8f;
    public float medioPilar = 0.5f;

    private const float GROSOR = 0.1f;
    private const string NOMBRE_CONTENEDOR = "Contenedor_ParedConPuertas";

    [ContextMenu("Generar Pared Con Puertas")]
    public void GenerarParedConPuertas()
    {
        Transform contenedorViejo = transform.Find(NOMBRE_CONTENEDOR);
        if (contenedorViejo != null) DestroyImmediate(contenedorViejo.gameObject);

        GameObject contenedor = new GameObject(NOMBRE_CONTENEDOR);
        contenedor.transform.SetParent(this.transform, false);

        float mitadAltoGateway = altoGateway / 2f;
        float xActual = 0f;

        // Pared 1
        CrearPared(contenedor, xActual, altoGateway, largoPared1);
        xActual += largoPared1;

        // 3 Gateways
        xActual += medioPilar;
        for (int i = 0; i < 3; i++)
        {
            InstanciarGateway(contenedor, xActual, mitadAltoGateway);
            xActual += anchoGateway;
        }
        xActual += medioPilar;

        // Pared 2
        CrearPared(contenedor, xActual, altoGateway, largoPared2);
        xActual += largoPared2;

        // 4 Gateways
        xActual += medioPilar;
        for (int i = 0; i < 4; i++)
        {
            InstanciarGateway(contenedor, xActual, mitadAltoGateway);
            xActual += anchoGateway;
        }
        xActual += medioPilar;

        // Pared 3
        CrearPared(contenedor, xActual, altoGateway, largoPared3);
    }

    void CrearPared(GameObject contenedor, float xInicio, float alto, float largo)
    {
        GameObject paredGO = new GameObject("Pared");
        paredGO.transform.SetParent(contenedor.transform);
        paredGO.transform.localPosition = Vector3.zero;
        paredGO.transform.localRotation = Quaternion.identity;

        Mesh mesh = new Mesh();

        Vector3[] v = new Vector3[8];
        v[0] = new Vector3(xInicio, 0f, -GROSOR / 2f);
        v[1] = new Vector3(xInicio, alto, -GROSOR / 2f);
        v[2] = new Vector3(xInicio + largo, 0f, -GROSOR / 2f);
        v[3] = new Vector3(xInicio + largo, alto, -GROSOR / 2f);
        v[4] = new Vector3(xInicio, 0f, GROSOR / 2f);
        v[5] = new Vector3(xInicio, alto, GROSOR / 2f);
        v[6] = new Vector3(xInicio + largo, 0f, GROSOR / 2f);
        v[7] = new Vector3(xInicio + largo, alto, GROSOR / 2f);

        mesh.vertices = v;
        mesh.triangles = new int[] {
            0, 1, 2, 1, 3, 2,
            4, 6, 5, 5, 6, 7,
            0, 4, 1, 4, 5, 1,
            2, 3, 6, 3, 7, 6,
            1, 5, 3, 5, 7, 3,
            0, 2, 4, 2, 6, 4
        };
        mesh.RecalculateNormals();

        paredGO.AddComponent<MeshFilter>().mesh = mesh;
        paredGO.AddComponent<MeshRenderer>().sharedMaterial = materialPared;
    }

    void InstanciarGateway(GameObject contenedor, float xPos, float mitadAlto)
    {
        if (prefabGateway == null) return;

        Vector3 posLocal = new Vector3(xPos, mitadAlto, 0f);
        GameObject gateway = Instantiate(prefabGateway, contenedor.transform);
        gateway.transform.localPosition = posLocal;
        gateway.transform.localRotation = Quaternion.identity;
    }

    [ContextMenu("Limpiar")]
    public void Limpiar()
    {
        Transform contenedor = transform.Find(NOMBRE_CONTENEDOR);
        if (contenedor != null) DestroyImmediate(contenedor.gameObject);
    }
}
