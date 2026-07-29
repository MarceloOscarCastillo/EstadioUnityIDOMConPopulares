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

        Vector3 puntoBase = hit.collider.bounds.max; // tope del objeto
        puntoBase.x = hit.collider.bounds.center.x;
        puntoBase.z = hit.collider.bounds.center.z;

        // Calcular posicion del cilindro usuario
        float hundirOffset = esPopular ? 0f : (alturaCilindro / 2f - 0.05f);
        Vector3 posCilindro = puntoBase + Vector3.up * (alturaCilindro / 2f - hundirOffset);

        // Instanciar cilindro usuario
        cilindroUsuario = CrearCilindro(posCilindro, materialCilindroUsuario);

        // Posicion camara: 13cm debajo de la cima
        Vector3 posCamera = puntoBase + Vector3.up * (alturaCilindro - hundirOffset - 0.13f);

        // Orientar camara hacia el campo (direccion -Z del objeto hit)
        Vector3 dirHaciaElCampo = -hit.collider.transform.forward;
        dirHaciaElCampo.y = 0;

        camaraFreeFly.transform.position = posCamera;
        camaraFreeFly.transform.rotation = Quaternion.LookRotation(dirHaciaElCampo, Vector3.up);

        // Desactivar movimiento pero mantener rotacion en FreeFly
        scriptFreeFly.soloRotacion = true;

        // Instanciar cilindros espectadores en fila de adelante
        InstanciarEspectadores(hit, esPopular, hundirOffset, dirHaciaElCampo);
    }

    void InstanciarEspectadores(RaycastHit hit, bool esPopular, float hundirOffset, Vector3 dirHaciaElCampo)
    {
        
        // La fila de adelante esta en direccion hacia el campo
        float separacion = 0.5f; // separacion lateral entre espectadores
        float distanciaFila = esPopular ? 0.4f : 0.8f; // profundidad del escalon

        Vector3 centroFilaAdelante = hit.collider.bounds.center + dirHaciaElCampo * distanciaFila;
        Vector3 dirLateral = Vector3.Cross(dirHaciaElCampo, Vector3.up).normalized;

        float[] offsetsLaterales = { 0f, separacion, -separacion, separacion * 2f, -separacion * 2f };

        foreach (float offset in offsetsLaterales)
        {
            Vector3 pos = centroFilaAdelante + dirLateral * offset;

            
            // Raycast hacia abajo para encontrar el escalon de adelante
            if (Physics.Raycast(pos + Vector3.up * 2f, Vector3.down, out RaycastHit hitEscalon, 5f))
            {
                Debug.Log($"Buscando espectador en pos={pos}, hit={hitEscalon.collider?.gameObject.name}");

                string nombre = hitEscalon.collider.gameObject.name;

                bool esValido = nombre == "Escalon_Cabecera" ||
                    nombre == "AsientoPlatea(Clone)" ||
                    nombre == "SeatersStandBlock(Clone)" ||
                    nombre == "Seat" ||
                    nombre == "Respaldo";

                if (esValido)
                {
                    Vector3 base_esp = new Vector3(pos.x, hitEscalon.collider.bounds.max.y, pos.z);
                    float hundirOffsetEsp = esPopular ? 0f : (alturaCilindro / 2f - 0.05f);
                    Vector3 posEsp = base_esp + Vector3.up * (alturaCilindro / 2f - hundirOffsetEsp);
                    cilindrosEspectadores.Add(CrearCilindro(posEsp, materialCilindro));
                }
            }
        }
    }

    GameObject CrearCilindro(Vector3 posicion, Material mat)
    {
        GameObject c = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        c.transform.position = posicion;
        c.transform.localScale = new Vector3(diametroCilindro, alturaCilindro / 2f, diametroCilindro);
        
        if (mat != null) c.GetComponent<Renderer>().sharedMaterial = mat;
        DestroyImmediate(c.GetComponent<CapsuleCollider>());
        return c;
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
