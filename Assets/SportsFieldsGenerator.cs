using System.Collections.Generic;
using UnityEngine;

public class SportsFieldsGenerator : MonoBehaviour
{
    public enum Deporte
    {
        Futsal,
        Voley,
        Basket,
        Tenis
    }

    [Header("Configuracion de la tira")]
    public Deporte deporte = Deporte.Futsal;
    public int cantidadCanchas = 4;
    [Tooltip("Separacion entre canchas, en metros")]
    public float separacionCanchas = 3f;
    [Tooltip("Direccion en que se repiten: 0 = a lo largo del eje X local")]
    public bool repetirEnX = true;

    [Header("Demarcacion")]
    public float anchoLinea = 0.08f;
    public Material materialLinea;
    [Tooltip("Altura sobre el piso, para evitar z-fighting")]
    public float alturaLineas = 0.01f;


    [Header("Superficie")]
    public bool generarSuperficie = true;
    public Material materialSuperficie;
    [Tooltip("Metros que la superficie sobresale del perimetro de la cancha")]
    public float margenSuperficie = 1f;
    public float alturaSuperficie = 0.005f;


    private const string NOMBRE_CONTENEDOR = "Contenedor_Canchas";

    [ContextMenu("Generar Canchas")]
    public void GenerarCanchas()
    {
        LimpiarCanchas();

        GameObject contenedor = new GameObject(NOMBRE_CONTENEDOR);
        contenedor.transform.SetParent(this.transform, false);
        contenedor.tag = "SectorEstadio";

        Vector2 medidas = MedidasDeporte(deporte);
        float paso = (repetirEnX ? medidas.x : medidas.y) + separacionCanchas;
        float largoTotal = cantidadCanchas * paso - separacionCanchas;
        float inicio = -largoTotal / 2f + (repetirEnX ? medidas.x : medidas.y) / 2f;

        for (int i = 0; i < cantidadCanchas; i++)
        {
            float d = inicio + i * paso;
            Vector3 centro = repetirEnX ? new Vector3(d, 0f, 0f) : new Vector3(0f, 0f, d);

            GameObject cancha = new GameObject($"Cancha_{deporte}_{i + 1}");
            cancha.transform.SetParent(contenedor.transform, false);
            cancha.transform.localPosition = centro;

            switch (deporte)
            {
                case Deporte.Futsal: GenerarFutsal(cancha.transform); break;
                case Deporte.Voley: GenerarVoley(cancha.transform); break;
                case Deporte.Basket: GenerarBasket(cancha.transform); break;
                case Deporte.Tenis: GenerarTenis(cancha.transform); break;
            }
        }
    }

    Vector2 MedidasDeporte(Deporte d)
    {
        switch (d)
        {
            case Deporte.Futsal: return new Vector2(40f, 20f);
            case Deporte.Voley: return new Vector2(18f, 9f);
            case Deporte.Basket: return new Vector2(28f, 15f);
            case Deporte.Tenis: return new Vector2(23.77f, 10.97f);
        }
        return new Vector2(20f, 10f);
    }

    void GenerarFutsal(Transform padre)
    {
        float largo = 40f, ancho = 20f;
        float ml = largo / 2f, ma = ancho / 2f;

        GenerarSuperficie(padre, largo, ancho);

        Perimetro(padre, largo, ancho);
        LineaRecta(padre, "Linea_Media", Vector3.zero, new Vector2(anchoLinea, ancho));
        Circulo(padre, "Circulo_Central", Vector3.zero, 3f, 360, 0);
        Punto(padre, "Punto_Central", Vector3.zero);

        foreach (int s in new[] { -1, 1 })
        {
            float xMeta = s * ml;

            // Cuartos de circulo de 6m desde cada poste, abriendose hacia el centro
            float inicioSup = (s > 0) ? 90f : 0f;
            float inicioInf = (s > 0) ? 180f : 270f;

            Circulo(padre, "Area_Arco", new Vector3(xMeta, 0f, 1.6f), 6f, 90, inicioSup);
            Circulo(padre, "Area_Arco", new Vector3(xMeta, 0f, -1.6f), 6f, 90, inicioInf);

            LineaRecta(padre, "Area_Frontal",
                       new Vector3(xMeta - s * 6f, 0f, 0f),
                       new Vector2(anchoLinea, 3.2f));

            Punto(padre, "Penal", new Vector3(xMeta - s * 6f, 0f, 0f));
            Punto(padre, "Doble_Penal", new Vector3(xMeta - s * 10f, 0f, 0f));
        }
    }

    void GenerarVoley(Transform padre)
    {
        float largo = 18f, ancho = 9f;
        Perimetro(padre, largo, ancho);
        LineaRecta(padre, "Linea_Media", Vector3.zero, new Vector2(anchoLinea, ancho));

        GenerarSuperficie(padre, largo, ancho);

        foreach (int s in new[] { -1, 1 })
            LineaRecta(padre, "Linea_Ataque", new Vector3(s * 3f, 0f, 0f),
                       new Vector2(anchoLinea, ancho));
    }
    
    void GenerarBasket(Transform padre)
    {
        float largo = 28f, ancho = 15f;
        float ml = largo / 2f, ma = ancho / 2f;

        float radioCentral = 1.8f;
        float radioTres = 6.75f;
        float distanciaTresFondo = 0.9f;   // separacion del lateral en el tramo recto
        float largoRectoTres = 2.99f;      // tramo recto desde el fondo
        float anchoZona = 4.9f;
        float largoZona = 5.8f;
        float distanciaAro = 1.575f;       // del fondo al centro del aro

        GenerarSuperficie(padre, largo, ancho);
        Perimetro(padre, largo, ancho);

        LineaRecta(padre, "Linea_Media", Vector3.zero, new Vector2(anchoLinea, ancho));
        Circulo(padre, "Circulo_Central", Vector3.zero, radioCentral, 360, 0);

        foreach (int s in new[] { -1, 1 })
        {
            float xFondo = s * ml;
            float xAro = xFondo - s * distanciaAro;

            // Zona restringida (la "botella")
            LineaRecta(padre, "Zona_Lateral_1",
                       new Vector3(xFondo - s * largoZona / 2f, 0f, anchoZona / 2f),
                       new Vector2(largoZona, anchoLinea));
            LineaRecta(padre, "Zona_Lateral_2",
                       new Vector3(xFondo - s * largoZona / 2f, 0f, -anchoZona / 2f),
                       new Vector2(largoZona, anchoLinea));
            LineaRecta(padre, "Linea_Tiro_Libre",
                       new Vector3(xFondo - s * largoZona, 0f, 0f),
                       new Vector2(anchoLinea, anchoZona));

            // Semicirculo de tiro libre
            Circulo(padre, "Circulo_Tiro_Libre",
                    new Vector3(xFondo - s * largoZona, 0f, 0f), 1.8f,
                    180, s > 0 ? 90 : 270);

            // Triple: dos tramos rectos desde el fondo
            LineaRecta(padre, "Triple_Recto_1",
                       new Vector3(xFondo - s * largoRectoTres / 2f, 0f, ma - distanciaTresFondo),
                       new Vector2(largoRectoTres, anchoLinea));
            LineaRecta(padre, "Triple_Recto_2",
                       new Vector3(xFondo - s * largoRectoTres / 2f, 0f, -(ma - distanciaTresFondo)),
                       new Vector2(largoRectoTres, anchoLinea));

            // Arco de triple, centrado en el aro
            Circulo(padre, "Triple_Arco", new Vector3(xAro, 0f, 0f), radioTres,
                    180, s > 0 ? 90 : 270);

            // Semicirculo de no carga
            Circulo(padre, "No_Carga", new Vector3(xAro, 0f, 0f), 1.25f,
                    180, s > 0 ? 90 : 270);
        }
    }
    void GenerarTenis(Transform padre)
    {
        float largo = 23.77f;
        float anchoDobles = 10.97f;
        float anchoIndividual = 8.23f;

        float ml = largo / 2f;
        float maD = anchoDobles / 2f;
        float maI = anchoIndividual / 2f;
        float zServicio = 6.40f;

        GenerarSuperficie(padre, largo, anchoDobles);
        Perimetro(padre, largo, anchoDobles);

        // Lineas laterales de individuales (pasillos de dobles)
        LineaRecta(padre, "Lateral_Individual_1", new Vector3(0f, 0f, maI),
                   new Vector2(largo, anchoLinea));
        LineaRecta(padre, "Lateral_Individual_2", new Vector3(0f, 0f, -maI),
                   new Vector2(largo, anchoLinea));

        // Lineas de servicio
        foreach (int s in new[] { -1, 1 })
            LineaRecta(padre, "Linea_Servicio", new Vector3(s * zServicio, 0f, 0f),
                       new Vector2(anchoLinea, anchoIndividual));

        // Linea central de servicio
        LineaRecta(padre, "Centro_Servicio", Vector3.zero,
                   new Vector2(zServicio * 2f, anchoLinea));

        // Marcas centrales en las lineas de fondo
        foreach (int s in new[] { -1, 1 })
            LineaRecta(padre, "Marca_Central", new Vector3(s * (ml - 0.05f), 0f, 0f),
                       new Vector2(0.10f, anchoLinea));
    }


    void Perimetro(Transform padre, float largo, float ancho)
    {
        float ml = largo / 2f, ma = ancho / 2f;

        LineaRecta(padre, "Lateral_1", new Vector3(0f, 0f, ma), new Vector2(largo, anchoLinea));
        LineaRecta(padre, "Lateral_2", new Vector3(0f, 0f, -ma), new Vector2(largo, anchoLinea));
        LineaRecta(padre, "Fondo_1", new Vector3(ml, 0f, 0f), new Vector2(anchoLinea, ancho));
        LineaRecta(padre, "Fondo_2", new Vector3(-ml, 0f, 0f), new Vector2(anchoLinea, ancho));
    }

    void LineaRecta(Transform padre, string nombre, Vector3 posLocal, Vector2 escalaXZ)
    {
        GameObject linea = GameObject.CreatePrimitive(PrimitiveType.Quad);
        linea.name = nombre;
        linea.transform.SetParent(padre, false);
        linea.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        linea.transform.localPosition = posLocal + Vector3.up * alturaLineas;
        linea.transform.localScale = new Vector3(escalaXZ.x, escalaXZ.y, 1f);

        if (materialLinea != null)
            linea.GetComponent<Renderer>().sharedMaterial = materialLinea;

        DestroyImmediate(linea.GetComponent<MeshCollider>());
    }

    void Circulo(Transform padre, string nombre, Vector3 centro, float radio,
                 int grados, float anguloOffset)
    {
        GameObject go = new GameObject(nombre);
        go.transform.SetParent(padre, false);
        go.transform.localPosition = centro + Vector3.up * alturaLineas;

        LineRenderer line = go.AddComponent<LineRenderer>();
        line.useWorldSpace = false;
        line.startWidth = anchoLinea;
        line.endWidth = anchoLinea;
        line.sharedMaterial = materialLinea;
        line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        line.receiveShadows = false;

        int segmentos = Mathf.Max(8, grados / 6);
        line.positionCount = segmentos + 1;

        for (int i = 0; i <= segmentos; i++)
        {
            float ang = ((float)i / segmentos) * grados + anguloOffset;
            line.SetPosition(i, new Vector3(
                radio * Mathf.Cos(ang * Mathf.Deg2Rad), 0f,
                radio * Mathf.Sin(ang * Mathf.Deg2Rad)));
        }
    }

    void Punto(Transform padre, string nombre, Vector3 posLocal)
    {
        GameObject punto = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        punto.name = nombre;
        punto.transform.SetParent(padre, false);
        punto.transform.localPosition = posLocal + Vector3.up * alturaLineas;
        punto.transform.localScale = new Vector3(0.15f, 0.001f, 0.15f);

        if (materialLinea != null)
            punto.GetComponent<Renderer>().sharedMaterial = materialLinea;

        DestroyImmediate(punto.GetComponent<CapsuleCollider>());
    }

    void GenerarSuperficie(Transform padre, float largo, float ancho)
    {
        if (!generarSuperficie) return;

        GameObject piso = GameObject.CreatePrimitive(PrimitiveType.Quad);
        piso.name = "Superficie";
        piso.transform.SetParent(padre, false);
        piso.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        piso.transform.localPosition = Vector3.up * alturaSuperficie;
        piso.transform.localScale = new Vector3(
            largo + margenSuperficie * 2f,
            ancho + margenSuperficie * 2f,
            1f);

        if (materialSuperficie != null)
            piso.GetComponent<Renderer>().sharedMaterial = materialSuperficie;

        DestroyImmediate(piso.GetComponent<MeshCollider>());
    }


    [ContextMenu("Limpiar Canchas")]
    public void LimpiarCanchas()
    {
        Transform viejo = transform.Find(NOMBRE_CONTENEDOR);
        if (viejo == null) return;

        if (Application.isPlaying) Destroy(viejo.gameObject);
        else DestroyImmediate(viejo.gameObject);
    }
}
