using System.Collections.Generic;
using UnityEngine;

public class TechoBidegain : MonoBehaviour
{
    [Header("Dimensiones")]
    public float largoTecho = 94f;
    [Tooltip("Profundidad: del borde trasero al borde sobre el campo")]
    public float profundidadTecho = 28f;

    [Tooltip("Desplaza el techo en profundidad respecto al origen del controller")]
    public float offsetZTecho = 0f;

    [Header("Vinculo con las torres")]
    public GeneradorTorresTechoBidegain torres;
    [Tooltip("Cuanto cuelga el techo por debajo de las vigas")]
    public float colgadoBajoVigas = 0.3f;
    [Tooltip("Se usa solo si no hay torres asignadas")]
    public float alturaTechoManual = 30f;


    [Tooltip("Diferencia de altura en el borde sobre el campo. Positivo = asciende")]
    public float desnivelHaciaElCampo = 0.5f;

    [Header("Reticulados longitudinales")]
    public bool generarReticulados = true;
    public int cantidadReticulados = 12;
    [Tooltip("Canto del reticulado: del vertice inferior al plano de chapas")]
    public float cantoReticulado = 0.75f;
    public float diametroCordon = 0.12f;
    public float diametroDiagonal = 0.06f;
    [Tooltip("Paso del zigzag de las diagonales")]
    public float pasoDiagonales = 1.5f;
    public Material materialReticulado;

    [Header("Cubierta de chapa")]
    public bool generarCubierta = true;
    [Tooltip("Ancho de cada tira de chapa, en metros")]
    public float anchoTiraChapa = 0.5f;
    [Tooltip("Acanaladuras por tira")]
    public int acanaladurasPorTira = 5;
    public Material materialChapa;
    public Color colorChapaClaro = new Color(0.82f, 0.82f, 0.78f);
    public Color colorChapaOscuro = new Color(0.68f, 0.68f, 0.64f);
    public int resolucionTextura = 256;
    public string nombreTexturaChapa = "ChapaTechoBidegain";

    [Header("Faldones del borde")]
    public bool generarFaldones = true;
    public float alturaFaldon = 0.75f;
    public int chapasPorFaldon = 3;
    public Material materialFaldon;

    [Header("Textura del faldon")]
    public Material materialFaldonTextura;
    public string nombreTexturaFaldon = "FaldonTechoBidegain";
    public Color colorLineaFaldon = new Color(0.5f, 0.5f, 0.48f);

    [Header("Reflectores")]
    public bool generarReflectores = true;
    public int cantidadReflectores = 52;
    [Tooltip("Tramo de cada extremo con reflectores cada 1 metro")]
    public float tramoDenso = 10f;
    public float separacionTramoDenso = 1f;
    public float anchoReflector = 0.5f;
    public float altoReflector = 0.4f;
    public float profundidadReflector = 0.35f;
    [Tooltip("Inclinacion hacia el campo, en grados")]
    public float inclinacionReflector = 30f;
    public Material materialCarcasa;
    public Material materialLente;


    private const string NOMBRE_CONTENEDOR = "Contenedor_Techo";

    [ContextMenu("Generar Techo")]
    public void GenerarTecho()
    {
        LimpiarTecho();

        Transform padreContenedor = (torres != null) ? torres.transform : this.transform;

        GameObject contenedor = new GameObject(NOMBRE_CONTENEDOR);

        contenedor.transform.SetParent(padreContenedor, false);

        contenedor.tag = "SectorEstadio";

        if (generarReticulados) GenerarReticuladosLongitudinales(contenedor.transform);

        GenerarCubierta(contenedor.transform);

        GenerarFaldones(contenedor.transform);

        GenerarReflectores(contenedor.transform);

        foreach (Transform hijo in contenedor.GetComponentsInChildren<Transform>())
        {
            if (hijo.gameObject != contenedor && Application.isPlaying)
                hijo.gameObject.isStatic = true;
        }

        if (Application.isPlaying)
            StaticBatchingUtility.Combine(contenedor);
    }

    void GenerarReticuladosLongitudinales(Transform padre)
    {
        float anchoRet = 1f; // ancho del reticulado, entre cordones superiores

        float paso = (cantidadReticulados > 1)
            ? (profundidadTecho - anchoRet) / (cantidadReticulados - 1)
            : 0f;

        for (int i = 0; i < cantidadReticulados; i++)
        {
            float z = offsetZTecho + anchoRet / 2f + paso * i;
            GenerarUnReticulado(padre, z, i);
        }
    }

    void GenerarUnReticulado(Transform padre, float z, int indice)
    {
        GameObject ret = new GameObject($"Reticulado_{indice + 1}");
        ret.transform.SetParent(padre, false);

        float y = AlturaEnZ(z);
        float xIni = -largoTecho / 2f;
        float xFin = largoTecho / 2f;

        // Dos cordones superiores, en el plano de las chapas
        float medioAncho = 0.5f;

        Vector3 supIzqA = new Vector3(xIni, y, z - medioAncho);
        Vector3 supIzqB = new Vector3(xFin, y, z - medioAncho);
        Vector3 supDerA = new Vector3(xIni, y, z + medioAncho);
        Vector3 supDerB = new Vector3(xFin, y, z + medioAncho);

        CrearCanio(ret.transform, supIzqA, supIzqB, diametroCordon, "Cordon_Superior");
        CrearCanio(ret.transform, supDerA, supDerB, diametroCordon, "Cordon_Superior");

        // Cordon inferior, en el vertice de la V
        Vector3 infA = new Vector3(xIni, y - cantoReticulado, z);
        Vector3 infB = new Vector3(xFin, y - cantoReticulado, z);

        CrearCanio(ret.transform, infA, infB, diametroCordon, "Cordon_Inferior");

        // Diagonales en zigzag entre cada cordon superior y el inferior
        int cantidad = Mathf.Max(1, Mathf.RoundToInt(largoTecho / pasoDiagonales));
        float pasoReal = largoTecho / cantidad;

        for (int i = 0; i < cantidad; i++)
        {
            float xA = xIni + i * pasoReal;
            float xB = xA + pasoReal / 2f;
            float xC = xA + pasoReal;

            foreach (float lado in new float[] { -medioAncho, medioAncho })
            {
                Vector3 pA = new Vector3(xA, y, z + lado);
                Vector3 pB = new Vector3(xB, y - cantoReticulado, z);
                Vector3 pC = new Vector3(xC, y, z + lado);

                CrearCanio(ret.transform, pA, pB, diametroDiagonal, "Diagonal");
                CrearCanio(ret.transform, pB, pC, diametroDiagonal, "Diagonal");
            }
        }
    }

    float AlturaEnZ(float z)
    {
        float t = (z - offsetZTecho) / profundidadTecho;
        return AlturaBaseTecho() + desnivelHaciaElCampo * t;
    }

    void CrearCanio(Transform padre, Vector3 a, Vector3 b, float diametro, string nombre)
    {
        GameObject c = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        c.name = nombre;
        c.transform.SetParent(padre, false);
        c.transform.localPosition = (a + b) / 2f;
        c.transform.localRotation = Quaternion.FromToRotation(Vector3.up, (b - a).normalized);
        c.transform.localScale = new Vector3(diametro, Vector3.Distance(a, b) / 2f, diametro);

        Collider col = c.GetComponent<Collider>();
        if (col != null)
        {
            if (Application.isPlaying) Destroy(col);
            else DestroyImmediate(col);
        }

        if (materialReticulado != null)
            c.GetComponent<Renderer>().sharedMaterial = materialReticulado;
    }

    [ContextMenu("Limpiar Techo")]
    public void LimpiarTecho()
    {
        Transform padreContenedor = (torres != null) ? torres.transform : this.transform;
        Transform viejo = padreContenedor.Find(NOMBRE_CONTENEDOR);
        if (viejo == null) return;

        if (Application.isPlaying) Destroy(viejo.gameObject);
        else DestroyImmediate(viejo.gameObject);
    }

    float AlturaBaseTecho()
    {
        if (torres != null) return torres.AlturaInferiorVigas - colgadoBajoVigas;
        return alturaTechoManual;
    }

    void GenerarCubierta(Transform padre)
    {
        if (!generarCubierta || materialChapa == null) return;

        float xIni = -largoTecho / 2f;
        float xFin = largoTecho / 2f;
        float zIni = offsetZTecho;
        float zFin = offsetZTecho + profundidadTecho;

        Vector3 a = new Vector3(xIni, AlturaEnZ(zIni), zIni);
        Vector3 b = new Vector3(xFin, AlturaEnZ(zIni), zIni);
        Vector3 c = new Vector3(xIni, AlturaEnZ(zFin), zFin);
        Vector3 d = new Vector3(xFin, AlturaEnZ(zFin), zFin);

        Mesh mesh = new Mesh();
        mesh.vertices = new Vector3[] { a, b, c, d };
        mesh.triangles = new int[] { 0, 2, 1, 1, 2, 3 };

        // UV en metros: una repeticion de textura por tira
        float repeticionesX = largoTecho / anchoTiraChapa;
        float repeticionesZ = profundidadTecho / anchoTiraChapa;

        mesh.uv = new Vector2[]
        {
        new Vector2(0f, 0f),
        new Vector2(repeticionesX, 0f),
        new Vector2(0f, repeticionesZ),
        new Vector2(repeticionesX, repeticionesZ)
        };

        mesh.RecalculateNormals();

        GameObject cubierta = new GameObject("Cubierta_Chapa");
        cubierta.transform.SetParent(padre);
        cubierta.transform.localPosition = Vector3.zero;
        cubierta.transform.localRotation = Quaternion.identity;
        cubierta.AddComponent<MeshFilter>().mesh = mesh;
        cubierta.AddComponent<MeshRenderer>().sharedMaterial = materialChapa;
    }

    void GenerarFaldones(Transform padre)
    {
        if (!generarFaldones) return;

        float xIni = -largoTecho / 2f;
        float xFin = largoTecho / 2f;
        float zIni = offsetZTecho;
        float zFin = offsetZTecho + profundidadTecho;

        float yIni = AlturaEnZ(zIni);
        float yFin = AlturaEnZ(zFin);

        // Borde sobre el campo
        CrearFaldon(padre,
            new Vector3(xIni, yFin, zFin),
            new Vector3(xFin, yFin, zFin),
            "Faldon_Frente");

        // Bordes laterales
        CrearFaldon(padre,
            new Vector3(xIni, yIni, zIni),
            new Vector3(xIni, yFin, zFin),
            "Faldon_Lateral_Izq");

        CrearFaldon(padre,
            new Vector3(xFin, yIni, zIni),
            new Vector3(xFin, yFin, zFin),
            "Faldon_Lateral_Der");
    }

    void CrearFaldon(Transform padre, Vector3 a, Vector3 b, string nombre)
    {
        Mesh mesh = new Mesh();

        Vector3 abajoA = a - Vector3.up * alturaFaldon;
        Vector3 abajoB = b - Vector3.up * alturaFaldon;

        mesh.vertices = new Vector3[] { a, b, abajoA, abajoB };
        mesh.triangles = new int[] { 0, 2, 1, 1, 2, 3 };

        float largo = Vector3.Distance(a, b);

        mesh.uv = new Vector2[]
        {
        new Vector2(0f, chapasPorFaldon),
        new Vector2(largo / alturaFaldon, chapasPorFaldon),
        new Vector2(0f, 0f),
        new Vector2(largo / alturaFaldon, 0f)
        };

        mesh.RecalculateNormals();

        GameObject faldon = new GameObject(nombre);
        faldon.transform.SetParent(padre);
        faldon.transform.localPosition = Vector3.zero;
        faldon.transform.localRotation = Quaternion.identity;
        faldon.AddComponent<MeshFilter>().mesh = mesh;
        faldon.AddComponent<MeshRenderer>().sharedMaterial =
            materialFaldon != null ? materialFaldon : materialChapa;
    }

    void GenerarReflectores(Transform padre)
    {
        if (!generarReflectores) return;

        float zBorde = offsetZTecho + profundidadTecho;
        float y = AlturaEnZ(zBorde);
        float xIni = -largoTecho / 2f;
        float xFin = largoTecho / 2f;

        List<float> posiciones = new List<float>();

        // Tramos densos en los dos extremos
        int porTramoDenso = Mathf.FloorToInt(tramoDenso / separacionTramoDenso);

        for (int i = 0; i < porTramoDenso; i++)
        {
            posiciones.Add(xIni + i * separacionTramoDenso);
            posiciones.Add(xFin - i * separacionTramoDenso);
        }

        // Tramo central con el resto, repartido parejo
        int restantes = cantidadReflectores - posiciones.Count;

        if (restantes > 0)
        {
            float xCentroIni = xIni + tramoDenso;
            float xCentroFin = xFin - tramoDenso;
            float pasoCentral = (xCentroFin - xCentroIni) / (restantes + 1);

            for (int i = 1; i <= restantes; i++)
                posiciones.Add(xCentroIni + i * pasoCentral);
        }

        foreach (float x in posiciones)
            CrearReflector(padre, new Vector3(x, y, zBorde));
    }

    void CrearReflector(Transform padre, Vector3 pos)
    {
        GameObject refl = new GameObject("Reflector");
        refl.transform.SetParent(padre, false);
        refl.transform.localPosition = pos;
        refl.transform.localRotation = Quaternion.Euler(inclinacionReflector, 0f, 0f);

        // Carcasa
        GameObject carcasa = GameObject.CreatePrimitive(PrimitiveType.Cube);
        carcasa.name = "Carcasa";
        carcasa.transform.SetParent(refl.transform, false);
        carcasa.transform.localPosition = Vector3.zero;
        carcasa.transform.localScale =
            new Vector3(anchoReflector, altoReflector, profundidadReflector);
        QuitarCollider(carcasa);
        if (materialCarcasa != null)
            carcasa.GetComponent<Renderer>().sharedMaterial = materialCarcasa;

        // Lente, apenas por delante
        GameObject lente = GameObject.CreatePrimitive(PrimitiveType.Cube);
        lente.name = "Lente";
        lente.transform.SetParent(refl.transform, false);
        lente.transform.localPosition = new Vector3(0f, 0f, profundidadReflector / 2f);
        lente.transform.localScale =
            new Vector3(anchoReflector * 0.9f, altoReflector * 0.9f, 0.04f);
        QuitarCollider(lente);
        if (materialLente != null)
            lente.GetComponent<Renderer>().sharedMaterial = materialLente;
    }

    void QuitarCollider(GameObject go)
    {
        Collider col = go.GetComponent<Collider>();
        if (col == null) return;
        if (Application.isPlaying) Destroy(col);
        else DestroyImmediate(col);
    }


    [ContextMenu("Generar Textura Chapa")]
    public void GenerarTexturaChapa()
    {
        if (materialChapa == null) return;

        int res = resolucionTextura;
        Texture2D textura = new Texture2D(res, res);

        int pixelesPorAcanaladura = Mathf.Max(1, res / acanaladurasPorTira);

        for (int y = 0; y < res; y++)
        {
            for (int x = 0; x < res; x++)
            {
                // El acanalado corre a lo largo de la tira: varia en X
                float posEnAcanaladura = (x % pixelesPorAcanaladura) / (float)pixelesPorAcanaladura;

                // Perfil suave: oscuro en el valle, claro en la cresta
                float t = Mathf.Sin(posEnAcanaladura * Mathf.PI);
                Color color = Color.Lerp(colorChapaOscuro, colorChapaClaro, t);

                textura.SetPixel(x, y, color);
            }
        }

        textura.Apply();
        materialChapa.SetTexture("_BaseMap", textura);

#if UNITY_EDITOR
        if (!System.IO.Directory.Exists("Assets/Textures"))
            System.IO.Directory.CreateDirectory("Assets/Textures");

        UnityEditor.AssetDatabase.CreateAsset(textura, $"Assets/Textures/{nombreTexturaChapa}.asset");
        UnityEditor.AssetDatabase.SaveAssets();
#endif
    }

    [ContextMenu("Generar Textura Faldon")]
    public void GenerarTexturaFaldon()
    {
        if (materialFaldon == null) return;

        int res = resolucionTextura;
        Texture2D textura = new Texture2D(res, res);

        int alto = res / chapasPorFaldon;
        int grosorLinea = Mathf.Max(1, res / 100);

        for (int y = 0; y < res; y++)
        {
            bool esLinea = (y % alto) < grosorLinea;

            for (int x = 0; x < res; x++)
                textura.SetPixel(x, y, esLinea ? colorLineaFaldon : colorChapaClaro);
        }

        textura.Apply();
        materialFaldon.SetTexture("_BaseMap", textura);

#if UNITY_EDITOR
        if (!System.IO.Directory.Exists("Assets/Textures"))
            System.IO.Directory.CreateDirectory("Assets/Textures");

        UnityEditor.AssetDatabase.CreateAsset(textura, $"Assets/Textures/{nombreTexturaFaldon}.asset");
        UnityEditor.AssetDatabase.SaveAssets();
#endif
    }

}
