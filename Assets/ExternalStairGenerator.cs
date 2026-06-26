using UnityEngine;
using System.Collections.Generic;
using UnityEditor;

public class GeneradorEscaleraArquitectonica : MonoBehaviour
{
    [Header("Prefab del Escalón")]
    public GameObject prefabEscalon;

    [Header("Dimensiones Visuales (En Metros)")]
    [Tooltip("Medida REAL del escalón en tu escena (tomando en cuenta su Scale).")]
    public float anchoRealEscalon = 1.50f;
    [Tooltip("Alto real de la contrahuella (lo que sube cada escalón)")]
    public float altoContrahuella = 0.18f;
    [Tooltip("Profundidad real de la huella (lo que avanza cada escalón)")]
    public float profundidadHuella = 0.30f;
    [Tooltip("Ancho original del prefab en unidades de Unity")]
    public float anchoOriginalPrefab = 10f;


    [Header("Configuración de la Escalera")]
    [Range(2, 200)] public int escalonesTotales = 15;

    [Header("Configuración de Murallas")]
    public bool generarMurallas = true;
    public float altoMuralla = 1.10f;
    public float anchoMuralla = 0.15f;

    [Header("Descanso 1")]
    public bool usarPrimerDescanso = false;
    public int filaPreviaADescanso1 = 5;
    public float profundidadDescanso1 = 1.50f;

    [Header("Descanso 2 (Opcional)")]
    public bool usarSegundoDescanso = false;
    public int filaPreviaADescanso2 = 12;
    public float profundidadDescanso2 = 1.50f;

    private Material materialEscalera;

    private const string NOMBRE_CONTENEDOR = "Escalinata_Externa";

    [ContextMenu("Generar Escalera Completa")]
    public void GenerarEscalera()
    {
        if (prefabEscalon == null) { Debug.LogError("Asigná el prefab del escalón."); return; }

        LimpiarEscaleraAntigua();

        ObtenerMaterialDelPrefab();

        GameObject contenedorPadre = new GameObject(NOMBRE_CONTENEDOR);

        contenedorPadre.transform.SetParent(this.transform, false);

        Undo.RegisterCreatedObjectUndo(contenedorPadre, "Generar Escalera");

        // Lista de nodos limpia para la rampa recta de la muralla (solo esquinas)
        List<Vector3> nodosPendiente = new List<Vector3>();

        Vector3 posPuntero = Vector3.zero;

        // Punto inicial de la muralla (base inferior)
        nodosPendiente.Add(new Vector3(posPuntero.x, posPuntero.y, -profundidadHuella / 2f));


        int escalonesColocados = 0;

        while (escalonesColocados < escalonesTotales)
        {
            int filaActual = escalonesColocados + 1;

            // Instanciar el escalón
            GameObject escalonObj = PrefabUtility.InstantiatePrefab(prefabEscalon, contenedorPadre.transform) as GameObject;

            escalonObj.name = $"Escalon_Fila_{filaActual}";

            escalonObj.transform.localPosition = posPuntero;

            float factorEscalaX = anchoRealEscalon / 10f;

            escalonObj.transform.localScale = new Vector3(factorEscalaX, 1f, 1f);

            bool esPrevioAlDescanso1 = usarPrimerDescanso && (filaActual == filaPreviaADescanso1);
            
            bool esPrevioAlDescanso2 = usarSegundoDescanso && (filaActual == filaPreviaADescanso2);

            if (!esPrevioAlDescanso1 && !esPrevioAlDescanso2)
            {
                nodosPendiente.Add(posPuntero);

                posPuntero.y += altoContrahuella;

                posPuntero.z += profundidadHuella;

            }

            else 
            {
                nodosPendiente.Add(posPuntero);

                posPuntero.z += profundidadHuella/2f;

                posPuntero.y += altoContrahuella / 2f;
                
            }

            escalonesColocados++;

            // --- PROCESAR DESCANSO 1 ---
            if (usarPrimerDescanso &&filaActual == filaPreviaADescanso1)
            {
                Vector3 inicioD = posPuntero;

                posPuntero.z += profundidadDescanso1;

                GenerarDescansoSolido(inicioD, posPuntero, contenedorPadre, "Descanso_1");


                posPuntero.z += profundidadHuella / 2f;

                posPuntero.y += altoContrahuella / 2f;

                // En el descanso, la muralla debe quebrar horizontal
                nodosPendiente.Add(inicioD);

                nodosPendiente.Add(posPuntero);
            }
            // --- PROCESAR DESCANSO 2 ---
            else if (usarSegundoDescanso && filaActual == filaPreviaADescanso2)
            {
                Vector3 inicioD = posPuntero;

                posPuntero.z += profundidadDescanso2;

                GenerarDescansoSolido(inicioD, posPuntero, contenedorPadre, "Descanso_2");

                posPuntero.z += profundidadHuella / 2f;

                posPuntero.y += altoContrahuella / 2f;

                nodosPendiente.Add(inicioD);

                nodosPendiente.Add(posPuntero);
            }                   
        }

        // Generar las murallas fluidas en los extremos
        if (generarMurallas && nodosPendiente.Count > 1)
        {
            float xIzquierda = -anchoRealEscalon / 2f;
            float xDerecha = anchoRealEscalon / 2f;

            // Izquierda: se desplaza hacia afuera (-anchoMuralla)
            GenerarMurallaRampaContinua(nodosPendiente, xIzquierda, -anchoMuralla, "Muralla_Izquierda", contenedorPadre);
            // Derecha: se desplaza hacia afuera (+anchoMuralla)
            GenerarMurallaRampaContinua(nodosPendiente, xDerecha, anchoMuralla, "Muralla_Derecha", contenedorPadre);
        }

        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(this.gameObject.scene);
    }
    private void GenerarDescansoSolido(Vector3 inicio, Vector3 fin, GameObject padre, string nombre)
    {
        GameObject descanso = new GameObject($"PROCEDURAL_{nombre}");

        descanso.transform.SetParent(padre.transform, false);

        MeshFilter mf = descanso.AddComponent<MeshFilter>();

        MeshRenderer mr = descanso.AddComponent<MeshRenderer>();

        mr.sharedMaterial = materialEscalera; // material directo del Inspector 

        Mesh mesh = new Mesh();

        float xIzq = -anchoRealEscalon / 2f;
        float xDer = anchoRealEscalon / 2f;

        float espesorBajo = altoContrahuella; // mismo grosor que un escalon 

        Vector3[] vertices = new Vector3[8]
        {
new Vector3(xIzq, inicio.y, inicio.z),
new Vector3(xDer, inicio.y, inicio.z),
new Vector3(xIzq, inicio.y, fin.z), // fin.y = inicio.y (plano) 
new Vector3(xDer, inicio.y, fin.z),
 new Vector3(xIzq, inicio.y - espesorBajo, inicio.z),
new Vector3(xDer, inicio.y - espesorBajo, inicio.z),
new Vector3(xIzq, inicio.y - espesorBajo, fin.z),
new Vector3(xDer, inicio.y - espesorBajo, fin.z) };

        int[] tri = new int[36] { 0, 2, 1, 1, 2, 3, // Techo 
           4, 6, 5, 5, 6, 7, // Piso 
           0, 4, 1, 1, 4, 5, // Frente 
           2, 3, 6, 3, 7, 6, // Fondo 
           0, 2, 4, 2, 6, 4, // Izq 
           1, 5, 3, 3, 5, 7 // Der
                             };

        mesh.vertices = vertices;

        mesh.triangles = tri;

        mesh.RecalculateNormals();

        mesh.RecalculateBounds();

        mf.mesh = mesh;

    }

    private void GenerarMurallaRampaContinua(List<Vector3> camino, float xPos, float offsetX, string nombre, GameObject padre)
    {
        GameObject muralla = new GameObject($"PROCEDURAL_{nombre}");
        muralla.transform.SetParent(padre.transform, false);

        MeshFilter mf = muralla.AddComponent<MeshFilter>();
        MeshRenderer mr = muralla.AddComponent<MeshRenderer>();
        mr.sharedMaterial = materialEscalera;

        Mesh mesh = new Mesh();
        List<Vector3> verts = new List<Vector3>();
        List<int> tris = new List<int>();

        float enterrarMuro = 0.50f; // Margen hacia abajo para encastrarse en la tribuna

        // 1. Generar los vértices a lo largo de la rampa limpia
        for (int i = 0; i < camino.Count; i++)
        {
            Vector3 pt = camino[i];

            Vector3 bInt = new Vector3(xPos, pt.y - enterrarMuro, pt.z);
            Vector3 bExt = new Vector3(xPos + offsetX, pt.y - enterrarMuro, pt.z);
            Vector3 tInt = new Vector3(xPos, pt.y + altoMuralla, pt.z);
            Vector3 tExt = new Vector3(xPos + offsetX, pt.y + altoMuralla, pt.z);

            verts.Add(bInt); // 4*i + 0
            verts.Add(bExt); // 4*i + 1
            verts.Add(tInt); // 4*i + 2
            verts.Add(tExt); // 4*i + 3
        }

        // 2. Armar las caras uniendo los tramos longitudinales de la rampa
        for (int i = 0; i < camino.Count - 1; i++)
        {
            int act = i * 4;
            int sig = (i + 1) * 4;

            if (offsetX < 0) // Muralla Izquierda
            {
                tris.AddRange(new int[] { act + 0, act + 2, sig + 0, act + 2, sig + 2, sig + 0 }); // Cara Interna
                tris.AddRange(new int[] { act + 1, sig + 1, act + 3, act + 3, sig + 1, sig + 3 }); // Cara Externa
                tris.AddRange(new int[] { act + 2, act + 3, sig + 2, act + 3, sig + 3, sig + 2 }); // Techo plano/inclinado
                tris.AddRange(new int[] { act + 0, sig + 0, act + 1, act + 1, sig + 0, sig + 1 }); // Base inferior
            }
            else // Muralla Derecha
            {
                tris.AddRange(new int[] { act + 0, sig + 0, act + 2, act + 2, sig + 0, sig + 2 }); // Cara Interna
                tris.AddRange(new int[] { act + 1, act + 3, sig + 1, act + 3, sig + 3, sig + 1 }); // Cara Externa
                tris.AddRange(new int[] { act + 2, sig + 2, act + 3, act + 3, sig + 2, sig + 3 }); // Techo plano/inclinado
                tris.AddRange(new int[] { act + 0, act + 1, sig + 0, act + 1, sig + 1, sig + 0 }); // Base inferior
            }
        }

        // Tapas extremas (Inicio y fin de la pared)
        int fin = (camino.Count - 1) * 4;
        tris.AddRange(new int[] { 0, 2, 1, 1, 2, 3 }); // Tapa inicial inferior
        tris.AddRange(new int[] { fin + 0, fin + 1, fin + 2, fin + 1, fin + 3, fin + 2 }); // Tapa final superior

        mesh.vertices = verts.ToArray();
        mesh.triangles = tris.ToArray();
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        mf.mesh = mesh;
        muralla.AddComponent<MeshCollider>().sharedMesh = mesh;
    }

    private void ObtenerMaterialDelPrefab()
    {
        Renderer[] renderers = prefabEscalon.GetComponentsInChildren<Renderer>();
        foreach (Renderer r in renderers)
        {
            if (r.sharedMaterial != null && r.sharedMaterial.name != "Default-Material")
            {
                materialEscalera = r.sharedMaterial; return;
            }
        }
    }

    private void LimpiarEscaleraAntigua()
    {
        Transform t = this.transform.Find(NOMBRE_CONTENEDOR);
        if (t != null) Undo.DestroyObjectImmediate(t.gameObject);
    }
}
