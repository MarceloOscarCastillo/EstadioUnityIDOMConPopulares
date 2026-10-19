using System.Collections.Generic;
using Unity.Burst.CompilerServices;
using UnityEditor;
using UnityEngine;

public class GeneradorEscaleraGiratoria : MonoBehaviour
{
    [Header("Filtros de Contenedores")]
    public string tagContenedores = "SectorEstadio";

    [Header("Prefab del Escalón")]
    public GameObject prefabEscalon;

    [Header("Dimensiones Visuales (En Metros)")]
    public float anchoRealEscalon = 1.50f;
    public float altoContrahuella = 0.18f;
    public float profundidadHuella = 0.30f;
    public float anchoOriginalPrefab = 10f;

    [Header("Configuración de la Escalera")]
    [Range(2, 200)] public int escalonesTotalesPorRepeticion = 15;

    [Header("Configuración de Murallas")]
    public bool generarMurallas = true;
    public float altoMuralla = 1.10f;
    public float anchoMuralla = 0.15f;

    [Header("Descanso 1")]
    public bool usarPrimerDescanso = false;
    public int filaPreviaADescanso1 = 5;
    public float profundidadDescanso1 = 1.50f;
    public bool girarEnDescanso1 = false;
    public bool girarALaDerechaDescanso1 = true;

    [Header("Descanso 2 (Opcional)")]
    public bool usarSegundoDescanso = false;
    public int filaPreviaADescanso2 = 12;
    public float profundidadDescanso2 = 1.50f;
    public bool girarEnDescanso2 = false;
    public bool girarALaDerechaDescanso2 = true;

    [Header("Repeticiones")]
    public int cantidadRepeticiones = 1; // 1 = una sola U, 2 = dos U apiladas, etc.

    [Header("Escalera Final (Opcional)")]
    public bool usarEscaleraFinal = false;
    public int escalonesEscaleraFinal = 6;
    public bool usarRellanoFinal = false;
    public float profundidadRellanoFinal = 2.5f;
    public float anchoRellanoFinal = 3f;
    public bool salidaHaciaAdelante = false;

    private Material materialEscalera;
    private const string NOMBRE_CONTENEDOR = "Escalinata_Interna";

    // Tracking de direccion actual
    private bool subiendo_en_Z_positivo = true; // true = avanza en Z+, false = avanza en Z-
    private float offsetX = 0f; // desplazamiento acumulado en X por los giros
    private float ladoExterno = -1f;

    [ContextMenu("Generar Escalera Completa")]
    public void GenerarEscalera()
    {
        if (prefabEscalon == null) {  return; }

        LimpiarEscaleraAntigua();
        ObtenerMaterialDelPrefab();

        GameObject contenedorPadre = new GameObject(NOMBRE_CONTENEDOR);
        contenedorPadre.transform.SetParent(this.transform, false);
        contenedorPadre.tag = tagContenedores;
        Undo.RegisterCreatedObjectUndo(contenedorPadre, "Generar Escalera");

        // Resetear estado
        subiendo_en_Z_positivo = true;
        offsetX = 0f;


        ladoExterno = girarALaDerechaDescanso1 ? -1f : 1f;

        List<Vector3> nodosExt = new List<Vector3>();
        List<List<Vector3>> tramosInt = new List<List<Vector3>>();
        List<Vector3> nodosInt = new List<Vector3>();
        tramosInt.Add(nodosInt);


        Vector3 posPuntero = Vector3.zero;
       
        float signoIni = 1f; // arranca subiendo en Z+
        float xExtIni = (anchoRealEscalon / 2f) * ladoExterno * signoIni + offsetX;
        float xIntIni = -(anchoRealEscalon / 2f) * ladoExterno * signoIni + offsetX;

        nodosExt.Add(new Vector3(xExtIni, posPuntero.y, -profundidadHuella / 2f));
        nodosInt.Add(new Vector3(xIntIni, posPuntero.y, -profundidadHuella / 2f));



        for (int rep = 0; rep < cantidadRepeticiones; rep++)
        {
            int escalonesEnEstaRep = 0;

            while (escalonesEnEstaRep < escalonesTotalesPorRepeticion)
            {
                int filaActual = escalonesEnEstaRep + 1;

                bool esPrevioAlDescanso1 = usarPrimerDescanso && (filaActual == filaPreviaADescanso1);
                bool esPrevioAlDescanso2 = usarSegundoDescanso && (filaActual == filaPreviaADescanso2);

                
                GameObject escalonObj = Application.isPlaying
                ? Instantiate(prefabEscalon, contenedorPadre.transform)
                : PrefabUtility.InstantiatePrefab(prefabEscalon, contenedorPadre.transform) as GameObject;


                escalonObj.name = $"Escalon_Rep{rep}_Fila_{filaActual}";

                float dirZ = subiendo_en_Z_positivo ? 1f : -1f;
                float factorEscalaX = anchoRealEscalon / anchoOriginalPrefab;

                escalonObj.transform.localPosition = new Vector3(offsetX, posPuntero.y, posPuntero.z);
                escalonObj.transform.localRotation = subiendo_en_Z_positivo ?
                    Quaternion.identity : Quaternion.Euler(0, 180, 0);
                escalonObj.transform.localScale = new Vector3(factorEscalaX, 1f, 1f);
                

                float signo = subiendo_en_Z_positivo ? 1f : -1f;
                float xExt = (anchoRealEscalon / 2f) * ladoExterno * signo + offsetX;
                float xInt = -(anchoRealEscalon / 2f) * ladoExterno * signo + offsetX;


                if (!esPrevioAlDescanso1 && !esPrevioAlDescanso2)
                {
                    nodosExt.Add(new Vector3(xExt, posPuntero.y, posPuntero.z));
                    nodosInt.Add(new Vector3(xInt, posPuntero.y, posPuntero.z));

                    posPuntero.y += altoContrahuella;
                    posPuntero.z += profundidadHuella * dirZ;
                }
                else
                {
                    nodosExt.Add(new Vector3(xExt, posPuntero.y, posPuntero.z));
                    nodosInt.Add(new Vector3(xInt, posPuntero.y, posPuntero.z));

                    posPuntero.z += (profundidadHuella / 2f) * dirZ;
                    posPuntero.y += altoContrahuella / 2f;
                }

                escalonesEnEstaRep++;

                if (usarPrimerDescanso && filaActual == filaPreviaADescanso1)
                    
                    ProcesarDescanso(ref posPuntero, profundidadDescanso1, girarEnDescanso1,
    girarALaDerechaDescanso1, nodosExt, ref nodosInt, tramosInt, contenedorPadre, $"Descanso1_Rep{rep}");
                else if (usarSegundoDescanso && filaActual == filaPreviaADescanso2)
                    
                    ProcesarDescanso(ref posPuntero, profundidadDescanso1, girarEnDescanso1,
    girarALaDerechaDescanso1, nodosExt, ref nodosInt, tramosInt, contenedorPadre, $"Descanso1_Rep{rep}");
            }

            // Descanso intermedio entre repeticiones (excepto en la ultima)
            if (rep < cantidadRepeticiones - 1)
            {
                
                ProcesarDescanso(ref posPuntero, profundidadDescanso1, girarEnDescanso1,
    girarALaDerechaDescanso1, nodosExt, ref nodosInt, tramosInt, contenedorPadre, $"Descanso1_Rep{rep}");
            }
        }

        GenerarTramoFinal(ref posPuntero, nodosExt, ref nodosInt, tramosInt, contenedorPadre, 0);

        // Generar murallas
        if (generarMurallas)
        {
            

            if (generarMurallas)
            {
                GenerarMurallaRampaContinua(nodosExt, "Muralla_Externa", contenedorPadre);

                for (int i = 0; i < tramosInt.Count; i++)
                    GenerarMurallaRampaContinua(tramosInt[i], $"Muralla_Interna_{i}", contenedorPadre);
            }
        }

        

        if (!Application.isPlaying)
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(this.gameObject.scene);
        //UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(this.gameObject.scene);
    }
    
    void ProcesarDescanso(ref Vector3 posPuntero, float profDescanso, bool girar, bool girarDerecha,
    List<Vector3> nodosExt, ref List<Vector3> nodosInt, List<List<Vector3>> tramosInt,
    GameObject padre, string nombre)

    {
        float dirZ = subiendo_en_Z_positivo ? 1f : -1f;
        

        float signo = subiendo_en_Z_positivo ? 1f : -1f;
        float xExt = (anchoRealEscalon / 2f) * ladoExterno * signo + offsetX;
        float xInt = -(anchoRealEscalon / 2f) * ladoExterno * signo + offsetX;

        Vector3 inicioDescanso = posPuntero;

        if (!girar)
        {
            // Descanso simple sin giro (igual que antes)
            posPuntero.z += profDescanso * dirZ;
            GenerarDescansoSolido(inicioDescanso, posPuntero, padre, nombre, false);

            nodosExt.Add(new Vector3(xExt, inicioDescanso.y, inicioDescanso.z));
            nodosExt.Add(new Vector3(xExt, posPuntero.y, posPuntero.z));
            nodosInt.Add(new Vector3(xInt, inicioDescanso.y, inicioDescanso.z));
            nodosInt.Add(new Vector3(xInt, posPuntero.y, posPuntero.z));

            posPuntero.z += (profundidadHuella / 2f) * dirZ;
            posPuntero.y += altoContrahuella / 2f;
        }
        else
        {
            // Descanso con giro 180°
            float dirGiroX = girarDerecha ? 1f : -1f;
            if (!subiendo_en_Z_positivo) dirGiroX *= -1f;

            float anchoDescanso = anchoRealEscalon + anchoMuralla;
            
            float xCentro = offsetX;
            float anchoTotalDescanso = anchoRealEscalon * 2f + anchoMuralla;

            float xDescansoInicio = xCentro - (anchoRealEscalon / 2f) * dirGiroX;
            float xDescansoFin = xDescansoInicio + anchoTotalDescanso * dirGiroX;


            Vector3 esquina1 = new Vector3(xDescansoInicio, inicioDescanso.y,
                inicioDescanso.z + profDescanso * dirZ);
            Vector3 esquina2 = new Vector3(xDescansoFin, inicioDescanso.y,
                inicioDescanso.z + profDescanso * dirZ);

            // Generar piso del descanso
            GenerarDescansoSolido(
                new Vector3(xDescansoInicio, inicioDescanso.y, inicioDescanso.z),
                esquina2,
                padre, nombre, true);

            nodosExt.Add(new Vector3(xDescansoInicio, inicioDescanso.y, inicioDescanso.z));
            nodosExt.Add(new Vector3(xDescansoInicio, inicioDescanso.y, esquina1.z));
            nodosExt.Add(new Vector3(xDescansoFin, inicioDescanso.y, esquina1.z));
            nodosExt.Add(new Vector3(xDescansoFin, inicioDescanso.y, inicioDescanso.z));


            Debug.Log($"{nombre}: dirGiroX={dirGiroX}, subiendoZ+={subiendo_en_Z_positivo}, offsetX={offsetX}, xIni={xDescansoInicio:F2}, xFin={xDescansoFin:F2}, zIni={inicioDescanso.z:F2}, zFin={esquina1.z:F2}");

            // Muralla INTERNA se interrumpe en el descanso
           
            // Cerrar la tira interna actual
            nodosInt.Add(new Vector3(xInt, inicioDescanso.y, inicioDescanso.z));

            offsetX += anchoDescanso * dirGiroX;
            subiendo_en_Z_positivo = !subiendo_en_Z_positivo;

            posPuntero = new Vector3(offsetX, inicioDescanso.y,
                inicioDescanso.z + (profundidadHuella / 2f) * (subiendo_en_Z_positivo ? 1f : -1f));
            posPuntero.y += altoContrahuella / 2f;

            // Abrir una tira nueva para el tramo siguiente
            nodosInt = new List<Vector3>();
            tramosInt.Add(nodosInt);

            
            float signoNuevo = subiendo_en_Z_positivo ? 1f : -1f;
            float xIntNuevo = -(anchoRealEscalon / 2f) * ladoExterno * signoNuevo + offsetX;



            nodosInt.Add(new Vector3(xIntNuevo, posPuntero.y, posPuntero.z));

            

        }
    }


    private void GenerarDescansoSolido(Vector3 inicio, Vector3 fin, GameObject padre,
        string nombre, bool esAncho)
    {
        GameObject descanso = new GameObject($"PROCEDURAL_{nombre}");
        descanso.transform.SetParent(padre.transform, false);
        MeshFilter mf = descanso.AddComponent<MeshFilter>();
        MeshRenderer mr = descanso.AddComponent<MeshRenderer>();
        mr.sharedMaterial = materialEscalera;

        Mesh mesh = new Mesh();

        float xIzq, xDer;
        if (esAncho)
        {
            // El descanso con giro ocupa desde el inicio hasta fin en X y Z
            xIzq = Mathf.Min(inicio.x, fin.x) - anchoMuralla;
            xDer = Mathf.Max(inicio.x, fin.x) + anchoMuralla;
        }
        else
        {
            xIzq = -anchoRealEscalon / 2f;
            xDer = anchoRealEscalon / 2f;
        }

        float zMin = Mathf.Min(inicio.z, fin.z);
        float zMax = Mathf.Max(inicio.z, fin.z);
        float espesorBajo = altoContrahuella;

        Vector3[] vertices = new Vector3[8]
        {
            new Vector3(xIzq, inicio.y, zMin),
            new Vector3(xDer, inicio.y, zMin),
            new Vector3(xIzq, inicio.y, zMax),
            new Vector3(xDer, inicio.y, zMax),
            new Vector3(xIzq, inicio.y - espesorBajo, zMin),
            new Vector3(xDer, inicio.y - espesorBajo, zMin),
            new Vector3(xIzq, inicio.y - espesorBajo, zMax),
            new Vector3(xDer, inicio.y - espesorBajo, zMax)
        };

        int[] tri = new int[36] {
            0, 2, 1, 1, 2, 3,
            4, 6, 5, 5, 6, 7,
            0, 4, 1, 1, 4, 5,
            2, 3, 6, 3, 7, 6,
            0, 2, 4, 2, 6, 4,
            1, 5, 3, 3, 5, 7
        };

        mesh.vertices = vertices;
        mesh.triangles = tri;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        mf.mesh = mesh;
    }

    private void GenerarMurallaRampaContinua(List<Vector3> camino, string nombre, GameObject padre)
    {
        if (camino.Count < 2) return;

        GameObject muralla = new GameObject($"PROCEDURAL_{nombre}");
        muralla.transform.SetParent(padre.transform, false);
        MeshFilter mf = muralla.AddComponent<MeshFilter>();
        MeshRenderer mr = muralla.AddComponent<MeshRenderer>();
        mr.sharedMaterial = materialEscalera;

        Mesh mesh = new Mesh();
        List<Vector3> verts = new List<Vector3>();
        List<int> tris = new List<int>();

        float enterrarMuro = 0.50f;

        for (int i = 0; i < camino.Count; i++)
        {
            Vector3 pt = camino[i];
            verts.Add(new Vector3(pt.x, pt.y - enterrarMuro, pt.z));          // base interior
            verts.Add(new Vector3(pt.x + anchoMuralla, pt.y - enterrarMuro, pt.z)); // base exterior
            verts.Add(new Vector3(pt.x, pt.y + altoMuralla, pt.z));           // top interior
            verts.Add(new Vector3(pt.x + anchoMuralla, pt.y + altoMuralla, pt.z)); // top exterior
        }

        for (int i = 0; i < camino.Count - 1; i++)
        {
            int act = i * 4;
            int sig = (i + 1) * 4;

            tris.AddRange(new int[] { act, act + 2, sig, act + 2, sig + 2, sig });
            tris.AddRange(new int[] { act + 1, sig + 1, act + 3, act + 3, sig + 1, sig + 3 });
            tris.AddRange(new int[] { act + 2, act + 3, sig + 2, act + 3, sig + 3, sig + 2 });
            tris.AddRange(new int[] { act, sig, act + 1, act + 1, sig, sig + 1 });
        }

        // Tapas
        tris.AddRange(new int[] { 0, 2, 1, 1, 2, 3 });
        int f = (camino.Count - 1) * 4;
        tris.AddRange(new int[] { f, f + 1, f + 2, f + 1, f + 3, f + 2 });

        mesh.vertices = verts.ToArray();
        mesh.triangles = tris.ToArray();
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        mf.mesh = mesh;
    }

    private void ObtenerMaterialDelPrefab()
    {
        Renderer[] renderers = prefabEscalon.GetComponentsInChildren<Renderer>();
        foreach (Renderer r in renderers)
        {
            if (r.sharedMaterial != null && r.sharedMaterial.name != "Default-Material")
            {
                materialEscalera = r.sharedMaterial;
                return;
            }
        }
    }

    private void LimpiarEscaleraAntigua()
    {
        Transform t = this.transform.Find(NOMBRE_CONTENEDOR);

        if (t == null) return;

        if (Application.isPlaying) Destroy(t.gameObject);
        else Undo.DestroyObjectImmediate(t.gameObject);        
    }

    void GenerarTramoFinal(ref Vector3 posPuntero, List<Vector3> nodosExt,
    ref List<Vector3> nodosInt, List<List<Vector3>> tramosInt,
    GameObject padre, int rep)
    {
        if (!usarEscaleraFinal) return;

        float dirZ = subiendo_en_Z_positivo ? 1f : -1f;
        float signo = subiendo_en_Z_positivo ? 1f : -1f;
        float factorEscalaX = anchoRealEscalon / anchoOriginalPrefab;

        for (int i = 0; i < escalonesEscaleraFinal; i++)
        {
            GameObject escalonObj = Application.isPlaying
                ? Instantiate(prefabEscalon, padre.transform)
                : PrefabUtility.InstantiatePrefab(prefabEscalon, padre.transform) as GameObject;

            escalonObj.name = $"Escalon_Final_{i + 1}";
            escalonObj.transform.localPosition = new Vector3(offsetX, posPuntero.y, posPuntero.z);
            escalonObj.transform.localRotation = subiendo_en_Z_positivo ?
                Quaternion.identity : Quaternion.Euler(0, 180, 0);
            escalonObj.transform.localScale = new Vector3(factorEscalaX, 1f, 1f);

            float xE = (anchoRealEscalon / 2f) * ladoExterno * signo + offsetX;
            float xI = -(anchoRealEscalon / 2f) * ladoExterno * signo + offsetX;

            nodosExt.Add(new Vector3(xE, posPuntero.y, posPuntero.z));
            nodosInt.Add(new Vector3(xI, posPuntero.y, posPuntero.z));

            posPuntero.y += altoContrahuella;
            posPuntero.z += profundidadHuella * dirZ;
        }

        if (!usarRellanoFinal) return;

        GenerarRellanoFinal(posPuntero, nodosExt, ref nodosInt, tramosInt, padre);
    }

    void GenerarRellanoFinal(Vector3 inicio, List<Vector3> nodosExt,
    ref List<Vector3> nodosInt, List<List<Vector3>> tramosInt, GameObject padre)
    {
        float dirZ = subiendo_en_Z_positivo ? 1f : -1f;
        float signo = subiendo_en_Z_positivo ? 1f : -1f;

        if (salidaHaciaAdelante == false)
        {
            // El rellano se extiende hacia el lado de giro
            float dirSalida = -ladoExterno * signo;

            float xBordeExterno = (anchoRealEscalon / 2f) * ladoExterno * signo + offsetX;
            float xBordeSalida = xBordeExterno + anchoRellanoFinal * dirSalida;

            float zFondo = inicio.z + profundidadRellanoFinal * dirZ;

            GenerarDescansoSolido(
                new Vector3(xBordeExterno, inicio.y, inicio.z),
                new Vector3(xBordeSalida, inicio.y, zFondo),
                padre, "Rellano_Final", true);

            // Muralla externa: sigue por el lado externo y cierra el fondo.
            // El lado de salida queda abierto hacia la tribuna.
            nodosExt.Add(new Vector3(xBordeExterno, inicio.y, inicio.z));
            nodosExt.Add(new Vector3(xBordeExterno, inicio.y, zFondo));
            nodosExt.Add(new Vector3(xBordeSalida, inicio.y, zFondo));

            // Cerrar la tira interna de la escalera
            float xInt = -(anchoRealEscalon / 2f) * ladoExterno * signo + offsetX;
            nodosInt.Add(new Vector3(xInt, inicio.y, inicio.z));

            // Medio muro sobre el lado de la boca: va desde el borde interno
            // de la escalera hasta el extremo de salida del rellano
            List<Vector3> medioMuro = new List<Vector3>();
            medioMuro.Add(new Vector3(xInt, inicio.y, inicio.z));
            medioMuro.Add(new Vector3(xBordeSalida, inicio.y, inicio.z));
            tramosInt.Add(medioMuro);
        }


        else 
        {
            // El rellano se extiende hacia el lado de giro
            float dirSalida = -ladoExterno * signo;

            float xBordeExterno = (anchoRealEscalon / 2f) * ladoExterno + offsetX;
            float xBordeInterno = xBordeExterno + anchoRellanoFinal;

            float zFondo = inicio.z + profundidadRellanoFinal * dirZ;

            GenerarDescansoSolido(
                new Vector3(xBordeExterno, inicio.y, inicio.z),
                new Vector3(xBordeInterno, inicio.y, zFondo),
                padre, "Rellano_Final", true); ;

            // Muralla externa: sigue por el lado externo y cierra el fondo.
            // El lado de salida queda abierto hacia la tribuna.
            nodosExt.Add(new Vector3(xBordeExterno, inicio.y, inicio.z));
            nodosInt.Add(new Vector3(xBordeInterno, inicio.y, zFondo));


            nodosExt.Add(new Vector3(xBordeExterno, inicio.y, inicio.z));
            nodosExt.Add(new Vector3(xBordeExterno, inicio.y, zFondo));


            //Cerrar la tira interna de la escalera

            //float xInt = -(anchoRealEscalon / 2f) * ladoExterno * signo + offsetX;
            //nodosInt.Add(new Vector3(xInt, inicio.y, inicio.z));

        }
            
    }
}
