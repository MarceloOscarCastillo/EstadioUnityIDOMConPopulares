using UnityEngine;

public class ArcoModularController : MonoBehaviour
{
    [Header("Modo Histórico (Reglamento vs Nostalgia)")]
    [Tooltip("Si está activo, pinta las franjas del Viejo Gasómetro. Si está apagado, el arco queda 100% blanco reglamentario.")]
    public bool activarFranjasHistoricas = true;

    [Header("Configuración de Tensores Traseros")]
    [Tooltip("¿Los tensores van hacia el Z positivo o negativo? (Ajustar según qué arco sea)")]
    public bool invertirDireccionTensores = false;
    public float distanciaTensoresAtras = 2.5f;

    [Header("Nombres de los Objetos en TU Prefab")]
    [Tooltip("Escribí acá exactamente cómo se llama el objeto del poste izquierdo dentro de tu jerarquía")]
    public string nombrePosteIzquierdo = "PosteIzquierdo";
    [Tooltip("Escribí acá exactamente cómo se llama el objeto del poste derecho dentro de tu jerarquía")]
    public string nombrePosteDerecho = "PosteDerecho";

    [Header("Materiales")]
    public Material redColour;
    public Material blueColour;
    public Material materialCañoPiso;
    [Tooltip("Asigná acá un material con textura tipo Grid/Net con transparencia (Alpha Cutout o Transparent)")]
    public Material materialRedFutbol;


    [Header("Medidas para Cables")]
    public float anchoArco = 7.32f;
    public float alturaArco = 2.44f;

    

    void Start()
    {
        ProcesarArco();
    }

    // Esto te permite testear el quita y pon desde el Inspector en tiempo de ejecución
    private void OnValidate()
    {
        // Solo actúa en pleno juego si cambiás el booleano en el Inspector
        if (Application.isPlaying)
        {
            
        }
    }

    [ContextMenu("ProcesarArco")]
    public void ProcesarArco()
    {
        LimpiarElementosProcedurales();

        // 1. BUSCAR LOS POSTES EN TU PREFAB AUTOMÁTICAMENTE
        Transform posteIzq = BuscarHijoPorNombre(this.transform, nombrePosteIzquierdo);
        Transform posteDer = BuscarHijoPorNombre(this.transform, nombrePosteDerecho);

        // 2. SI EL MODO HISTÓRICO ESTÁ ACTIVO, INYECTAMOS LAS FRANJAS
        if (activarFranjasHistoricas)
        {
            if (posteIzq != null) GenerarFranjasEnPoste(posteIzq);
            if (posteDer != null) GenerarFranjasEnPoste(posteDer);
        }

        // 2. GENERAR CAÑOS DE METAL EN EL PISO (Estructura Bidegain)
        //GenerarEstructuraPiso(posteIzq.localPosition.x, posteDer.localPosition.x);

        //// 3. GENERAR LA RED DE FUTBOL (Malla Procedural)
        //GenerarMeshRed(posteIzq.localPosition.x, posteDer.localPosition.x);
    }

    private void GenerarFranjasEnPoste(Transform posteTarget)
    {
        // Franja Roja Base
        GameObject baseRoja = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        baseRoja.name = "PROCEDURAL_Base_Roja";
        baseRoja.transform.SetParent(posteTarget, false);
        baseRoja.transform.localScale = new Vector3(1.05f, 0.15f, 1.05f);
        baseRoja.transform.localPosition = new Vector3(0f, -0.85f, 0f); // Ajustar según pivote de tu poste
        AplicarMaterial(baseRoja, redColour);
        DestroyImmediate(baseRoja.GetComponent<CapsuleCollider>());

        // Franja Azul Fina
        GameObject lineaAzul = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        lineaAzul.name = "PROCEDURAL_Linea_Azul";
        lineaAzul.transform.SetParent(posteTarget, false);
        lineaAzul.transform.localScale = new Vector3(1.06f, 0.04f, 1.06f);
        lineaAzul.transform.localPosition = new Vector3(0f, -0.65f, 0f);
        AplicarMaterial(lineaAzul, blueColour);
        DestroyImmediate(lineaAzul.GetComponent<CapsuleCollider>());
    }

    

    

    private Transform BuscarHijoPorNombre(Transform padre, string nombre)
    {
        // Búsqueda recursiva por si tus postes están metidos adentro de sub-objetos
        if (padre.name == nombre) return padre;
        foreach (Transform hijo in padre)
        {
            Transform resultado = BuscarHijoPorNombre(hijo, nombre);
            if (resultado != null) return resultado;
        }
        return null;
    }

    private void LimpiarElementosProcedurales()
    {
        // Eliminamos todo lo que tenga el prefijo "PROCEDURAL_" para evitar duplicados
        for (int i = this.transform.childCount - 1; i >= 0; i--)
        {
            GameObject hijo = this.transform.GetChild(i).gameObject;
            if (hijo.name.StartsWith("PROCEDURAL_")) DestroyImmediate(hijo);
        }

        // Buscamos dentro de los postes para limpiar las franjas también
        Transform posteIzq = BuscarHijoPorNombre(this.transform, nombrePosteIzquierdo);
        Transform posteDer = BuscarHijoPorNombre(this.transform, nombrePosteDerecho);

        if (posteIzq != null) LimpiarFranjasDePoste(posteIzq);
        if (posteDer != null) LimpiarFranjasDePoste(posteDer);
    }

    private void LimpiarFranjasDePoste(Transform poste)
    {
        for (int i = poste.childCount - 1; i >= 0; i--)
        {
            if (poste.GetChild(i).name.StartsWith("PROCEDURAL_"))
                DestroyImmediate(poste.GetChild(i).gameObject);
        }
    }

    private void AplicarMaterial(GameObject obj, Material mat)
    {
        if (mat == null) return;
        Renderer rend = obj.GetComponent<Renderer>();
        if (rend != null) rend.sharedMaterial = mat;
    }

    //private void GenerarEstructuraPiso(float xIzq, float xDer)
    //{
    //    float zDireccion = invertirDireccionZ ? -profundidadRed : profundidadRed;
    //    float radioCaño = 0.04f; // Caño fino de metal

    //    // Caño Trasero Paralelo al Piso
    //    GameObject cañoTrasero = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
    //    cañoTrasero.name = "PROCEDURAL_CañoTrasero";
    //    cañoTrasero.transform.SetParent(this.transform, false);

    //    float largoCaño = Mathf.Abs(xDer - xIzq);
    //    cañoTrasero.transform.localPosition = new Vector3((xIzq + xDer) / 2f, radioCaño, zDireccion);
    //    cañoTrasero.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
    //    cañoTrasero.transform.localScale = new Vector3(radioCaño * 2, largoCaño / 2f, radioCaño * 2);
    //    AplicarMaterial(cañoTrasero, materialCañoPiso);
    //    DestroyImmediate(cañoTrasero.GetComponent<CapsuleCollider>());

    //    // Caños Laterales (Unen la base de tus postes con el caño trasero)
    //    //GenerarCañoLateral(xIzq, 0f, zDireccion, radioCaño, "Izq");
    //    //GenerarCañoLateral(xDer, 0f, zDireccion, radioCaño, "Der");
    //}

    //private void GenerarCañoLateral(float xPos, float zInicio, float zFin, float radio, string lado)
    //{
    //    GameObject cañoLat = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
    //    cañoLat.name = "PROCEDURAL_CañoLateral_" + lado;
    //    cañoLat.transform.SetParent(this.transform, false);

    //    float largo = Mathf.Abs(zFin - zInicio);
    //    cañoLat.transform.localPosition = new Vector3(xPos, radio, (zInicio + zFin) / 2f);
    //    cañoLat.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
    //    cañoLat.transform.localScale = new Vector3(radio * 2, largo / 2f, radio * 2);
    //    AplicarMaterial(cañoLat, materialCañoPiso);
    //    DestroyImmediate(cañoLat.GetComponent<CapsuleCollider>());
    //}

    //private void GenerarMeshRed(float xIzq, float xDer)
    //{
    //    GameObject redObj = new GameObject("PROCEDURAL_Malla_Red");
    //    redObj.transform.SetParent(this.transform, false);

    //    MeshFilter meshFilter = redObj.AddComponent<MeshFilter>();
    //    MeshRenderer meshRenderer = redObj.AddComponent<MeshRenderer>();
    //    AplicarMaterial(redObj, materialRedFutbol);

    //    Mesh mesh = new Mesh();
    //    mesh.name = "RedProcedural";

    //    float zDireccion = invertirDireccionZ ? -profundidadRed : profundidadRed;

    //    // Definimos los 4 vértices clave del arco para la red trasera caída
    //    Vector3[] vertices = new Vector3[4];
    //    vertices[0] = new Vector3(xIzq, alturaArco, 0f);         // Arriba Izquierda (Travesaño)
    //    vertices[1] = new Vector3(xDer, alturaArco, 0f);         // Arriba Derecha (Travesaño)
    //    vertices[2] = new Vector3(xIzq, 0f, zDireccion);         // Abajo Izquierda (Caño Piso)
    //    vertices[3] = new Vector3(xDer, 0f, zDireccion);         // Abajo Derecha (Caño Piso)

    //    // Triángulos (Dobles para que se vea de adentro y de afuera del arco)
    //    int[] tri = new int[12]
    //    {
    //        // Cara delantera
    //        0, 1, 2,
    //        2, 1, 3,
    //        // Cara trasera (para que no desaparezca por Backface Culling)
    //        2, 1, 0,
    //        3, 1, 2
    //    };

    //    // Mapeo de UVs para que la textura de la red se estire correctamente
    //    Vector2[] uv = new Vector2[4]
    //    {
    //        new Vector2(0, 1),
    //        new Vector2(1, 1),
    //        new Vector2(0, 0),
    //        new Vector2(1, 0)
    //    };

    //    mesh.vertices = vertices;
    //    mesh.triangles = tri;
    //    mesh.uv = uv;
    //    mesh.RecalculateNormals();

    //    meshFilter.mesh = mesh;
    //}
}

