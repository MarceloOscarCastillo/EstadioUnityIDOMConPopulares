using System.Collections.Generic;
using UnityEngine;

public class FrontController : MonoBehaviour
{
    public enum DisenoFrente
    {
        FrenteIDOM,
        FrenteConPiel
    }

    [System.Serializable]
    public struct EdificioIDOM
    {
        public string nombre;
        public Vector3 posicionLocal;
        public Vector3 escala;
        public float rotacionY;
    }

    [Header("Diseno activo")]
    public DisenoFrente disenoActivo = DisenoFrente.FrenteIDOM;

    [Header("Niveles compartidos")]
    [Tooltip("Altura Y de cada piso. La comparten ambos frentes.")]
    public List<float> alturasPisos = new List<float>();

    [Header("Frente IDOM - Estructura")]
    public bool edificiosHuecos = true;
    public float espesorMuroInterior = 0.25f;
    public Material materialLosaInterior;

    [Header("Frente IDOM - Alturas")]
    public bool ajustarTopeANivel = true;

    [Header("Frente IDOM - Edificios")]
    public GameObject prefabEdificio;
    public List<EdificioIDOM> edificios = new List<EdificioIDOM>();
    public Material materialRevestimiento;
    public bool coordenadasEnMundo = true;

    [Header("Frente IDOM - Paneles de vidrio")]
    public bool generarPaneles = true;
    public GameObject prefabPanel;
    public float anchoPanel = 4f;
    public float alturaPanel = 2f;
    public float offsetYPanel = 0.2f;
    public float separacionCara = 0.02f;
    public Vector3 rotacionPanel = new Vector3(-90f, 0f, 0f);
    [Tooltip("Eje local del prefab que corresponde al ancho del panel (0=X, 1=Y, 2=Z)")]
    public int ejeAnchoPanel = 2;

    [Header("Frente IDOM - Balcones")]
    public bool generarBalcones = true;
    public float profundidadBalcon = 1.5f;
    public float espesorLosaBalcon = 0.2f;
    public float alturaBaranda = 1.1f;
    public float espesorBaranda = 0.15f;
    public Material materialBalcon;
    public bool invertirLadoSinBalcon = false;

    [Header("Frente IDOM - Balcones por nivel")]
    public bool omitirBalconPlantaBaja = true;
    public bool balconEnTerraza = true;

    [Header("Frente con Piel")]
    public PielEstadio pielFrente;
    public GameObject prefabPisoOficinaUrbana;
    public float offsetZPisos = 0f;

    private const string NOMBRE_CONTENEDOR = "Contenedor_Frente";

    [ContextMenu("Generar Frente")]
    public void GenerarFrente()
    {
        LimpiarFrente();

        GameObject contenedor = new GameObject(NOMBRE_CONTENEDOR);
        contenedor.transform.SetParent(this.transform, false);
        contenedor.tag = "SectorEstadio";

        if (disenoActivo == DisenoFrente.FrenteIDOM)
            GenerarFrenteIDOM(contenedor.transform);
        else
            GenerarFrenteConPiel(contenedor.transform);

        foreach (Transform hijo in contenedor.GetComponentsInChildren<Transform>())
        {
            if (hijo.gameObject != contenedor && Application.isPlaying)
                hijo.gameObject.isStatic = true;
        }

        if (Application.isPlaying)
            StaticBatchingUtility.Combine(contenedor);
    }

    public void CambiarDiseno(DisenoFrente nuevo)
    {
        disenoActivo = nuevo;
        GenerarFrente();
    }

    

    void GenerarFrenteIDOM(Transform padre)
    {
        if (prefabEdificio == null) return;

        foreach (EdificioIDOM e in edificios)
        {
            if (edificiosHuecos)
            {
                GenerarEstructuraHueca(e, padre);
            }
            else if (prefabEdificio != null)
            {
                GameObject edificio = Instantiate(prefabEdificio, padre);
                edificio.name = string.IsNullOrEmpty(e.nombre) ? "Edificio_IDOM" : e.nombre;
                edificio.transform.position = PosicionEdificio(e);
                edificio.transform.rotation = RotacionEdificio(e);
                edificio.transform.localScale = e.escala;
                edificio.SetActive(true);
                if (materialRevestimiento != null)
                    AplicarMaterialATodo(edificio, materialRevestimiento);
            }

            if (generarBalcones) GenerarBalconesEdificio(e, padre);
            if (generarPaneles) GenerarPanelesEdificio(e, padre);
        }
    }

    //void GenerarBalconesEdificio(EdificioIDOM e, Transform padre)
    //{
    //    float mitadX = e.escala.x / 2f;
    //    float mitadZ = e.escala.z / 2f;
    //    float p = profundidadBalcon;
    //    float eb = espesorBaranda;
    //    Quaternion rot = RotacionEdificio(e);

    //    float yCentro = PosicionEdificio(e).y;
    //    float yBaseEdificio = yCentro - e.escala.y / 2f;
    //    float yTopeEdificio = yCentro + e.escala.y / 2f;

    //    float s = invertirLadoSinBalcon ? -1f : 1f;

    //    foreach (float y in alturasPisos)
    //    {
    //        float yMundo = coordenadasEnMundo ? y : transform.TransformPoint(new Vector3(0f, y, 0f)).y;
    //        if (yMundo < yBaseEdificio || yMundo >= yTopeEdificio - 0.01f) continue;

    //        float yBaranda = y + espesorLosaBalcon / 2f + alturaBaranda / 2f;

    //        // Losas (el lado de X menor queda sin balcon)
    //        CrearLosa(padre, rot, PuntoBalcon(e, s*(mitadX + p / 2f), 0f, y),
    //            new Vector3(p, espesorLosaBalcon, e.escala.z + p * 2f));
    //        CrearLosa(padre, rot, PuntoBalcon(e, s*(p / 2f), -(mitadZ + p / 2f), y),
    //            new Vector3(e.escala.x + p, espesorLosaBalcon, p));
    //        CrearLosa(padre, rot, PuntoBalcon(e, s*(p / 2f), mitadZ + p / 2f, y),
    //            new Vector3(e.escala.x + p, espesorLosaBalcon, p));

    //        // Barandas
    //        CrearLosa(padre, rot, PuntoBalcon(e, s * (mitadX + p - eb / 2f), 0f, yBaranda),
    //            new Vector3(eb, alturaBaranda, e.escala.z + p * 2f));
    //        CrearLosa(padre, rot, PuntoBalcon(e, s*(p / 2f), -(mitadZ + p - eb / 2f), yBaranda),
    //            new Vector3(e.escala.x + p, alturaBaranda, eb));
    //        CrearLosa(padre, rot, PuntoBalcon(e, s*(p / 2f), mitadZ + p - eb / 2f, yBaranda),
    //            new Vector3(e.escala.x + p, alturaBaranda, eb));
    //    }
    //}

    void CrearLosa(Transform padre, Quaternion rotacion, Vector3 posMundo, Vector3 escala)
    {
        CrearCubo(padre, rotacion, posMundo, escala, materialBalcon, "Balcon");
    }

    void CrearCubo(Transform padre, Quaternion rotacion, Vector3 posMundo, Vector3 escala,
        Material mat, string nombre)
    {
        GameObject cubo = GameObject.CreatePrimitive(PrimitiveType.Cube);
        cubo.name = nombre;
        cubo.transform.SetParent(padre);
        cubo.transform.position = posMundo;
        cubo.transform.rotation = rotacion;
        cubo.transform.localScale = escala;

        Collider col = cubo.GetComponent<Collider>();
        if (col != null)
        {
            if (Application.isPlaying) Destroy(col);
            else DestroyImmediate(col);
        }

        if (mat != null)
            cubo.GetComponent<Renderer>().sharedMaterial = mat;
    }


    void GenerarFrenteConPiel(Transform padre)
    {
        if (pielFrente != null)
            pielFrente.GenerarPiel();

        if (prefabPisoOficinaUrbana == null) return;

        foreach (float y in alturasPisos)
        {
            GameObject piso = Instantiate(prefabPisoOficinaUrbana, padre);
            piso.name = $"Piso_OficinaUrbana_{y:F1}";
            piso.transform.position = transform.TransformPoint(new Vector3(0f, y, offsetZPisos));
            piso.transform.rotation = transform.rotation;
        }
    }

    void AplicarMaterialATodo(GameObject obj, Material mat)
    {
        foreach (Renderer r in obj.GetComponentsInChildren<Renderer>())
            r.sharedMaterial = mat;
    }

    [ContextMenu("Limpiar Frente")]
    public void LimpiarFrente()
    {
        Transform viejo = transform.Find(NOMBRE_CONTENEDOR);
        if (viejo != null)
        {
            if (Application.isPlaying) Destroy(viejo.gameObject);
            else DestroyImmediate(viejo.gameObject);
        }

        if (pielFrente != null) pielFrente.LimpiarPiel();
    }

    Vector3 PosicionEdificio(EdificioIDOM e)
    {
        return coordenadasEnMundo ? e.posicionLocal : transform.TransformPoint(e.posicionLocal);
    }

    Quaternion RotacionEdificio(EdificioIDOM e)
    {
        Quaternion rotY = Quaternion.Euler(0f, e.rotacionY, 0f);
        return coordenadasEnMundo ? rotY : transform.rotation * rotY;
    }

    Vector3 PuntoBalcon(EdificioIDOM e, float ox, float oz, float y)
    {
        Vector3 p = PosicionEdificio(e) + RotacionEdificio(e) * new Vector3(ox, 0f, oz);
        p.y = coordenadasEnMundo ? y : transform.TransformPoint(new Vector3(0f, y, 0f)).y;
        return p;
    }

    void GenerarEstructuraHueca(EdificioIDOM e, Transform padre)
    {
        Quaternion rot = RotacionEdificio(e);
        float s = invertirLadoSinBalcon ? -1f : 1f;
        float mitadX = e.escala.x / 2f;

        //float yBase = e.posicionLocal.y - e.escala.y / 2f;
        //float yTope = e.posicionLocal.y + e.escala.y / 2f;

        float yBase = BaseEdificio(e);
        float yTope = TopeEdificio(e);

        Material matLosa = materialLosaInterior != null ? materialLosaInterior : materialBalcon;

        // Losas interiores, una por piso dentro de la altura del edificio
        foreach (float y in alturasPisos)
        {
            //if (y < yBase || y >= yTope - 0.01f) continue;
            if (y < yBase - 0.01f || y >= yTope - 0.01f) continue;

            CrearCubo(padre, rot, PuntoBalcon(e, 0f, 0f, y),
                new Vector3(e.escala.x, espesorLosaBalcon, e.escala.z), matLosa, "Losa_Interior");
        }

        // Losa de techo
        //CrearCubo(padre, rot, PuntoBalcon(e, 0f, 0f, yTope - espesorLosaBalcon / 2f),
        //    new Vector3(e.escala.x, espesorLosaBalcon, e.escala.z), matLosa, "Losa_Techo");

        float altoMuro = yTope - yBase;
        CrearCubo(padre, rot,
            PuntoBalcon(e, -s * (mitadX - espesorMuroInterior / 2f), 0f, yBase + altoMuro / 2f),
            new Vector3(espesorMuroInterior, altoMuro, e.escala.z),
            materialRevestimiento, "Muro_Interior");

        // Muro opaco de la cara interior (el lado sin balcon)
        CrearCubo(padre, rot,
            PuntoBalcon(e, -s * (mitadX - espesorMuroInterior / 2f), 0f, e.posicionLocal.y),
            new Vector3(espesorMuroInterior, e.escala.y, e.escala.z),
            materialRevestimiento, "Muro_Interior");
    }

    void GenerarPanelesEdificio(EdificioIDOM e, Transform padre)
    {
        if (prefabPanel == null || anchoPanel <= 0f) return;

        float mitadX = e.escala.x / 2f;
        float mitadZ = e.escala.z / 2f;
        float s = invertirLadoSinBalcon ? -1f : 1f;
        float sep = separacionCara;
        Quaternion rotEdif = RotacionEdificio(e);

        //float yBase = e.posicionLocal.y - e.escala.y / 2f;
        //float yTope = e.posicionLocal.y + e.escala.y / 2f;

        float yBase = BaseEdificio(e);
        float yTope = TopeEdificio(e);

        foreach (float y in alturasPisos)
        {
            //if (y < yBase || y >= yTope - 0.01f) continue;
            if (y < yBase - 0.01f || y >= yTope - 0.01f) continue;

            float yPanel = y + offsetYPanel + alturaPanel / 2f;

            GenerarFilaPaneles(e, padre, rotEdif, s * (mitadX + sep), 0f,
                Vector3.forward, e.escala.z, new Vector3(s, 0f, 0f), yPanel);

            GenerarFilaPaneles(e, padre, rotEdif, 0f, -(mitadZ + sep),
                Vector3.right, e.escala.x, Vector3.back, yPanel);

            GenerarFilaPaneles(e, padre, rotEdif, 0f, mitadZ + sep,
                Vector3.right, e.escala.x, Vector3.forward, yPanel);
        }
    }

    //void GenerarFilaPaneles(EdificioIDOM e, Transform padre, Quaternion rotEdif,
    //    float centroOx, float centroOz, Vector3 dirAvance, float largoCara,
    //    Vector3 normal, float y)
    //{
    //    int cantidad = Mathf.FloorToInt(largoCara / anchoPanel);
    //    if (cantidad <= 0) return;

    //    float inicio = -(cantidad * anchoPanel) / 2f + anchoPanel / 2f;

    //    Quaternion orient = rotEdif
    //        * Quaternion.LookRotation(normal, Vector3.up)
    //        * Quaternion.Euler(rotacionPanel); 

    //    for (int i = 0; i < cantidad; i++)
    //    {
    //        float d = inicio + i * anchoPanel;
    //        float ox = centroOx + dirAvance.x * d;
    //        float oz = centroOz + dirAvance.z * d;

    //        GameObject panel = Instantiate(prefabPanel, padre);
    //        panel.name = "Panel_Vidrio";
    //        panel.transform.position = PuntoBalcon(e, ox, oz, y);
    //        panel.transform.rotation = orient;
    //        panel.SetActive(true);
    //    }
    //}

    void GenerarFilaPaneles(EdificioIDOM e, Transform padre, Quaternion rotEdif,
    float centroOx, float centroOz, Vector3 dirAvance, float largoCara,
    Vector3 normal, float y)
    {
        int cantidad = Mathf.Max(1, Mathf.RoundToInt(largoCara / anchoPanel));
        float anchoReal = largoCara / cantidad;
        float factor = anchoReal / anchoPanel;
        float inicio = -largoCara / 2f + anchoReal / 2f;

        Quaternion orient = rotEdif
            * Quaternion.LookRotation(normal, Vector3.up)
            * Quaternion.Euler(rotacionPanel);

        for (int i = 0; i < cantidad; i++)
        {
            float d = inicio + i * anchoReal;
            float ox = centroOx + dirAvance.x * d;
            float oz = centroOz + dirAvance.z * d;

            GameObject panel = Instantiate(prefabPanel, padre);
            panel.name = "Panel_Vidrio";
            panel.transform.position = PuntoBalcon(e, ox, oz, y);
            panel.transform.rotation = orient;

            Vector3 esc = panel.transform.localScale;
            esc[ejeAnchoPanel] *= factor;
            panel.transform.localScale = esc;

            panel.SetActive(true);
        }
    }

    void GenerarBalconesEdificio(EdificioIDOM e, Transform padre)
    {
        //float yBase = e.posicionLocal.y - e.escala.y / 2f;
        //float yTope = e.posicionLocal.y + e.escala.y / 2f;

        float yBase = BaseEdificio(e);
        float yTope = TopeEdificio(e);

        List<float> niveles = new List<float>();
        foreach (float y in alturasPisos)
            if (y >= yBase - 0.01f && y < yTope - 0.01f)
                niveles.Add(y);

        niveles.Sort();

        if (omitirBalconPlantaBaja && niveles.Count > 0)
            niveles.RemoveAt(0);

        if (balconEnTerraza)
            niveles.Add(yTope - espesorLosaBalcon / 2f);

        foreach (float y in niveles)
            GenerarBalconEnNivel(e, padre, y);
    }

    void GenerarBalconEnNivel(EdificioIDOM e, Transform padre, float y)
    {
        float mitadX = e.escala.x / 2f;
        float mitadZ = e.escala.z / 2f;
        float p = profundidadBalcon;
        float eb = espesorBaranda;
        float s = invertirLadoSinBalcon ? -1f : 1f;
        Quaternion rot = RotacionEdificio(e);

        float yBaranda = y + espesorLosaBalcon / 2f + alturaBaranda / 2f;

        // Losas
        CrearLosa(padre, rot, PuntoBalcon(e, s * (mitadX + p / 2f), 0f, y),
            new Vector3(p, espesorLosaBalcon, e.escala.z + p * 2f));
        CrearLosa(padre, rot, PuntoBalcon(e, s * (p / 2f), -(mitadZ + p / 2f), y),
            new Vector3(e.escala.x + p, espesorLosaBalcon, p));
        CrearLosa(padre, rot, PuntoBalcon(e, s * (p / 2f), mitadZ + p / 2f, y),
            new Vector3(e.escala.x + p, espesorLosaBalcon, p));

        // Barandas
        CrearLosa(padre, rot, PuntoBalcon(e, s * (mitadX + p - eb / 2f), 0f, yBaranda),
            new Vector3(eb, alturaBaranda, e.escala.z + p * 2f));
        CrearLosa(padre, rot, PuntoBalcon(e, s * (p / 2f), -(mitadZ + p - eb / 2f), yBaranda),
            new Vector3(e.escala.x + p, alturaBaranda, eb));
        CrearLosa(padre, rot, PuntoBalcon(e, s * (p / 2f), mitadZ + p - eb / 2f, yBaranda),
            new Vector3(e.escala.x + p, alturaBaranda, eb));
    }

    //float BaseEdificio(EdificioIDOM e)
    //{
    //    return e.posicionLocal.y - e.escala.y / 2f;
    //}

    float BaseEdificio(EdificioIDOM e)
    {
        float baseCruda = e.posicionLocal.y - e.escala.y / 2f;
        if (!ajustarTopeANivel || alturasPisos.Count == 0) return baseCruda;

        float mejor = baseCruda;
        float menorDistancia = float.MaxValue;

        foreach (float y in alturasPisos)
        {
            float d = Mathf.Abs(y - baseCruda);
            if (d < menorDistancia)
            {
                menorDistancia = d;
                mejor = y;
            }
        }

        return mejor;
    }

    float TopeEdificio(EdificioIDOM e)
    {
        float topeCrudo = e.posicionLocal.y + e.escala.y / 2f;
        if (!ajustarTopeANivel) return topeCrudo;

        float mejor = float.MaxValue;
        foreach (float y in alturasPisos)
            if (y >= topeCrudo - 0.05f && y < mejor)
                mejor = y;

        return mejor == float.MaxValue ? topeCrudo : mejor;
    }
}
