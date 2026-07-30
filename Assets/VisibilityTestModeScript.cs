using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using static UnityEditor.PlayerSettings;

public class ModoVisibilidadController : MonoBehaviour
{
    [Header("Configuración")]
    public float alturaCilindro = 1.75f;
    public float diametroCilindro = 0.5f;
    public Material materialCilindro;
    public Material materialCilindroUsuario;

    [Header("UI")]
    public GameObject crosshair;
    public Button botonModoVisibilidad;
    public TextMeshProUGUI textoBoton;

    [Header("Referencias")]
    public Camera camaraFreeFly;
    public FreeFlyCamera scriptFreeFly;

    private bool modoActivo = false;
    private GameObject cilindroUsuario;
    private List<GameObject> cilindrosEspectadores = new List<GameObject>();
    private bool enPuntoDeVista = false;

    void Start()
    {
        crosshair.SetActive(false);
        botonModoVisibilidad.onClick.AddListener(ToggleModoVisibilidad);
    }

    void Update()
    {
        if (!modoActivo) return;

        if (!enPuntoDeVista && Keyboard.current.mKey.wasPressedThisFrame)
            IntentarSeleccionar();

        if (enPuntoDeVista && Keyboard.current.escapeKey.wasPressedThisFrame)
            SalirDePuntoDeVista();
    }

    void ToggleModoVisibilidad()
    {
        modoActivo = !modoActivo;
        crosshair.SetActive(modoActivo);
        textoBoton.text = modoActivo ? "Salir Modo Visibilidad" : "Modo Visibilidad";

        if (!modoActivo) SalirDePuntoDeVista();
    }

    //void IntentarSeleccionar()
    //{
    //    Ray ray = camaraFreeFly.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0));
    //    Debug.Log($"Raycast desde {ray.origin} en dirección {ray.direction}");

    //    if (!Physics.Raycast(ray, out RaycastHit hit, 100f))
    //    {
    //        Debug.Log("Raycast no impactó nada");
    //        return;
    //    }

    //    Debug.Log($"Impactó: {hit.collider.gameObject.name}");

    //    GameObject objetoImpactado = hit.collider.gameObject;
    //    string nombre = objetoImpactado.name;

    //    if (nombre == "Cube" || nombre == "Cylinder" || nombre == "Sphere" ||
    //nombre == "Seat" || nombre == "Respaldo")
    //    {
    //        if (objetoImpactado.transform.parent != null)
    //        {
    //            objetoImpactado = objetoImpactado.transform.parent.gameObject;
    //            nombre = objetoImpactado.name;

    //            // Si el padre tampoco es el correcto, subir un nivel mas
    //            if (nombre != "Escalon_Cabecera" && nombre != "SeatersStandBlock(Clone)" && nombre != "AsientoPlatea(Clone)")
    //            {
    //                if (objetoImpactado.transform.parent != null)
    //                {
    //                    objetoImpactado = objetoImpactado.transform.parent.gameObject;
    //                    nombre = objetoImpactado.name;
    //                }
    //            }
    //        }
    //    }


    //    bool esPopular = nombre == "Escalon_Cabecera";
    //    bool esPlatea = nombre == "SeatersStandBlock(Clone)" ||
    //nombre == "Asiento" || nombre == "Respaldo";

    //    if (!esPopular && !esPlatea)
    //    {
    //        Debug.Log("Clickeá en un asiento o escalón de las tribunas");
    //        return;
    //    }

    //    UbicarEnPuntoDeVista(hit, esPopular);
    //}

    void IntentarSeleccionar()
    {
        Ray ray = camaraFreeFly.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0));

        if (!Physics.Raycast(ray, out RaycastHit hit, 100f))
        {
            Debug.Log("Raycast no impactó nada");
            return;
        }

        // Subir en la jerarquía hasta encontrar un objeto válido (máximo 3 niveles)
        GameObject objetoImpactado = hit.collider.gameObject;
        string nombre = objetoImpactado.name;

        for (int i = 0; i < 3; i++)
        {
            if (nombre == "Escalon_Cabecera" ||
                nombre == "AsientoPlatea(Clone)" ||
                nombre == "SeatersStandBlock(Clone)")
                break;

            if (objetoImpactado.transform.parent == null) break;
            objetoImpactado = objetoImpactado.transform.parent.gameObject;
            nombre = objetoImpactado.name;
        }

        Debug.Log($"Objeto final: {nombre}");

        bool esPopular = nombre == "Escalon_Cabecera";
        bool esPlatea = nombre == "AsientoPlatea(Clone)" || nombre == "SeatersStandBlock(Clone)";

        if (!esPopular && !esPlatea)
        {
            Debug.Log("Clickeá en un asiento o escalón de las tribunas");
            return;
        }

        UbicarEnPuntoDeVista(hit, esPopular);
    }

    void UbicarEnPuntoDeVista(RaycastHit hit, bool esPopular)
    {
        LimpiarCilindros();
        enPuntoDeVista = true;

        // Calcular direccion hacia el campo PRIMERO
        Vector3 dirHaciaElCampo;
        if (esPopular)
        {
            dirHaciaElCampo = -hit.collider.transform.forward;
            dirHaciaElCampo.y = 0;
            dirHaciaElCampo.Normalize();
        }
        else
        {
            Transform t = hit.collider.transform;
            while (t != null && t.name != "SeatersStandBlock(Clone)")
                t = t.parent;
            dirHaciaElCampo = t != null ? -t.forward : -hit.collider.transform.forward;
            dirHaciaElCampo.y = 0;
            dirHaciaElCampo.Normalize();
        }

        // Obtener Y del asiento (cara superior del Seat)
        Collider colRef = hit.collider;
        float yAsiento = colRef.bounds.max.y;
        Transform tAsiento = colRef.transform.parent;
        if (tAsiento != null)
        {
            Transform seat = tAsiento.Find("Seat");
            if (seat != null)
            {
                Collider seatCol = seat.GetComponent<Collider>();
                if (seatCol != null) yAsiento = seatCol.bounds.max.y;
            }
        }

        // Punto base: cara superior del asiento + offset hacia el campo
        Vector3 puntoBase = new Vector3(
            hit.collider.bounds.center.x,
            yAsiento,
            hit.collider.bounds.center.z) + dirHaciaElCampo * 0.25f;

        // Hundimiento para platea (sentado): dejar 0.9m visible
        float hundirOffset = esPopular ? 0f : (alturaCilindro - 0.9f);

        // Posicion base del cilindro usuario
        Vector3 posCilindroBase = puntoBase - Vector3.up * hundirOffset;

        // Instanciar espectador usuario
        cilindroUsuario = CrearEspectador(posCilindroBase, materialCilindroUsuario, dirHaciaElCampo);

        // Camara: altura de ojos = 0.78m sobre el asiento
        float yOjos;

        if (esPopular)
        {
            // Popular: parado, ojos a alturaCilindro - 0.13f desde la base
            yOjos = puntoBase.y + alturaCilindro - 0.13f;
        }
        else
        {
            // Platea: sentado, ojos a 0.78m sobre el seat
            yOjos = yAsiento + 0.78f;
        }

        Vector3 posCamera = new Vector3(puntoBase.x, yOjos, puntoBase.z);

        camaraFreeFly.transform.position = posCamera;

        camaraFreeFly.transform.rotation = Quaternion.LookRotation(dirHaciaElCampo, Vector3.up);

        // Desactivar movimiento en FreeFly
        scriptFreeFly.soloRotacion = true;

        // Instanciar espectadores en fila de adelante
        InstanciarEspectadores(hit, esPopular, hundirOffset, dirHaciaElCampo);
    }

    void InstanciarEspectadores(RaycastHit hit, bool esPopular, float hundirOffset, Vector3 dirHaciaElCampo)
    {
        
        float separacion = 0.5f;
        float distanciaFila = esPopular ? 0.4f : 0.85f;
        Vector3 centroFilaAdelante = hit.collider.bounds.center + dirHaciaElCampo * distanciaFila;
        Vector3 dirLateral = Vector3.Cross(dirHaciaElCampo, Vector3.up).normalized;
        float[] offsetsLaterales = { 0f, separacion, -separacion, separacion * 2f, -separacion * 2f };

        foreach (float offset in offsetsLaterales)
        {
            Vector3 pos = centroFilaAdelante + dirLateral * offset;

            if (Physics.Raycast(pos + Vector3.up * 2f, Vector3.down, out RaycastHit hitEscalon, 5f))
            {
                GameObject objEsp = hitEscalon.collider.gameObject;
                string nombreEsp = objEsp.name;

                for (int i = 0; i < 3; i++)
                {
                    if (nombreEsp == "Escalon_Cabecera" ||
                        nombreEsp == "AsientoPlatea(Clone)" ||
                        nombreEsp == "SeatersStandBlock(Clone)")
                        break;
                    if (objEsp.transform.parent == null) break;
                    objEsp = objEsp.transform.parent.gameObject;
                    nombreEsp = objEsp.name;
                }

                bool esValido = nombreEsp == "Escalon_Cabecera" ||
                                nombreEsp == "AsientoPlatea(Clone)" ||
                                nombreEsp == "SeatersStandBlock(Clone)";

                if (esValido)
                {
                    // Obtener Y del Seat correctamente
                    float yAsientoEsp = hitEscalon.collider.bounds.max.y;
                    Transform tAsientoEsp = objEsp.transform;
                    Transform seatEsp = tAsientoEsp.Find("Seat");
                    if (seatEsp != null)
                    {
                        Collider seatCol = seatEsp.GetComponent<Collider>();
                        if (seatCol != null) yAsientoEsp = seatCol.bounds.max.y;
                    }

                    // Aplicar offset hacia el campo y hundimiento
                    Vector3 puntoBaseEsp = new Vector3(pos.x, yAsientoEsp, pos.z)
                                         + dirHaciaElCampo * 0.25f;
                    Vector3 posEsp = puntoBaseEsp - Vector3.up * hundirOffset;

                    cilindrosEspectadores.Add(CrearEspectador(posEsp, materialCilindro, dirHaciaElCampo));

                    Debug.Log($"yAsientoEsp={yAsientoEsp}, hundirOffset={hundirOffset}, posEsp.y={posEsp.y}");
                }
            }
        }
    }

    GameObject CrearEspectador(Vector3 posicionBase, Material mat, Vector3 dirMirada)
    {
        GameObject contenedor = new GameObject("Espectador");
        contenedor.transform.position = posicionBase;

        // Cuerpo: cilindro achatado (50cm ancho, 20cm profundidad)
        GameObject cuerpo = GameObject.CreatePrimitive(PrimitiveType.Cylinder);

        cuerpo.transform.SetParent(contenedor.transform);

        //cuerpo.transform.localPosition = new Vector3(0, alturaCilindro / 2f, 0);
        cuerpo.transform.localPosition = new Vector3(0, 0, 0);
        cuerpo.transform.localScale = new Vector3(0.5f, alturaCilindro / 2f, 0.2f);

        if (dirMirada != Vector3.zero)
            contenedor.transform.rotation = Quaternion.LookRotation(dirMirada, Vector3.up);

        if (mat != null) cuerpo.GetComponent<Renderer>().sharedMaterial = mat;
        DestroyImmediate(cuerpo.GetComponent<CapsuleCollider>());

        // Cabeza: esfera de 25cm
        GameObject cabeza = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        cabeza.transform.SetParent(contenedor.transform);
        //cabeza.transform.localPosition = new Vector3(0, alturaCilindro + 0.125f, 0);
        cabeza.transform.localPosition = new Vector3(0, alturaCilindro + 0.125f, 0);

        cabeza.transform.localScale = new Vector3(0.25f, 0.25f, 0.25f);
        if (mat != null) cabeza.GetComponent<Renderer>().sharedMaterial = mat;
        DestroyImmediate(cabeza.GetComponent<SphereCollider>());

        return contenedor;
    }

    void SalirDePuntoDeVista()
    {
        LimpiarCilindros();
        enPuntoDeVista = false;
        if (scriptFreeFly != null)
            scriptFreeFly.soloRotacion = false;
    }

    void LimpiarCilindros()
    {
        if (cilindroUsuario != null) Destroy(cilindroUsuario);
        foreach (var c in cilindrosEspectadores) if (c != null) Destroy(c);
        cilindrosEspectadores.Clear();
    }
}
