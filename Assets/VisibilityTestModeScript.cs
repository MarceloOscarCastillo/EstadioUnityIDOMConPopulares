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
    public GameObject prefabEspectadorParado;
    public GameObject prefabEspectadorSentado;
    public GameObject prefabUsuarioParado;
    public GameObject prefabUsuarioSentado;

    private bool modoActivo = false;
    private GameObject cilindroUsuario;
    private List<GameObject> cilindrosEspectadores = new List<GameObject>();
    private bool enPuntoDeVista = false;
    private Vector3 posicionOjos;
    private Quaternion rotacionOjos;

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

        if (enPuntoDeVista && Keyboard.current.vKey.wasPressedThisFrame)
        {
            if (scriptFreeFly.soloRotacion)
            {
                scriptFreeFly.soloRotacion = false;
            }
            else
            {
                scriptFreeFly.soloRotacion = true;
                camaraFreeFly.transform.position = posicionOjos;
                camaraFreeFly.transform.rotation = rotacionOjos;
            }
        }
    }

    void ToggleModoVisibilidad()
    {
        modoActivo = !modoActivo;
        crosshair.SetActive(modoActivo);
        textoBoton.text = modoActivo ? "Salir Modo Visibilidad" : "Modo Visibilidad";

        if (!modoActivo) SalirDePuntoDeVista();
    }
    
    void IntentarSeleccionar()
    {
        Ray ray = camaraFreeFly.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0));

        if (!Physics.Raycast(ray, out RaycastHit hit, 100f))
        {            
            return;
        }

        // Subir en la jerarquía hasta encontrar un objeto válido (máximo 3 niveles)
        GameObject objetoImpactado = hit.collider.gameObject;
        string nombre = objetoImpactado.name;
        
        for (int i = 0; i < 3; i++)
        {
            if (nombre == "Escalon_Cabecera" ||
                nombre == "AsientoPlatea(Clone)" ||
                nombre == "SeatersStandBlock(Clone)"
                || nombre == "BlockPlateaCurva(Clone)")
                break;

            if (objetoImpactado.transform.parent == null) break;
            objetoImpactado = objetoImpactado.transform.parent.gameObject;
            nombre = objetoImpactado.name;
        }

        Debug.Log($"Objeto final: {nombre}");

        bool esPopular = nombre == "Escalon_Cabecera" || nombre == "BlockPlateaCurva(Clone)";
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

        UpperCurveStandWithWalkpathScript codoScript =
   hit.collider.GetComponentInParent<UpperCurveStandWithWalkpathScript>();
        

        // Calcular direccion hacia el campo PRIMERO
        Vector3 dirHaciaElCampo;

        if (codoScript != null)
        {
            // Para codos: direccion radial hacia el centro del codo
            Vector3 centroCodo = codoScript.transform.position;
            dirHaciaElCampo = centroCodo - hit.point;
            dirHaciaElCampo.y = 0;
            dirHaciaElCampo.Normalize();
        }

        else if (esPopular)
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

        // Obtener Y del asiento
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

        // Instanciar usuario (prefab ya tiene proporciones correctas)
        Vector3 posUsuario = puntoBase - dirHaciaElCampo * 0.1f;
        cilindroUsuario = CrearEspectador(posUsuario, esPopular, true, dirHaciaElCampo);

        // Camara: altura de ojos
        float alturaOjos = esPopular ? alturaCilindro - 0.13f : alturaCilindro / 2f + 0.05f - 0.13f;
        Vector3 posCamera = new Vector3(puntoBase.x, yAsiento + alturaOjos, puntoBase.z);
        camaraFreeFly.transform.position = posCamera;
        camaraFreeFly.transform.rotation = Quaternion.LookRotation(dirHaciaElCampo, Vector3.up);
        
        posicionOjos = posCamera;
        rotacionOjos = Quaternion.LookRotation(dirHaciaElCampo, Vector3.up);

        // Desactivar movimiento en FreeFly
        scriptFreeFly.soloRotacion = true;

        // Instanciar espectadores en fila de adelante
               
        if (codoScript != null)
            InstanciarEspectadoresCodo(hit, esPopular, dirHaciaElCampo, codoScript);
        else
            InstanciarEspectadores(hit, esPopular, dirHaciaElCampo);

    }

    void InstanciarEspectadores(RaycastHit hit, bool esPopular, Vector3 dirHaciaElCampo)
    {
        float separacion = 0.5f;
        float distanciaFila = esPopular ? 0.42f : 0.85f;
        Vector3 centroFilaAdelante = hit.collider.bounds.center + dirHaciaElCampo * distanciaFila;
        Vector3 dirLateral = Vector3.Cross(dirHaciaElCampo, Vector3.up).normalized;
        float[] offsetsLaterales = { 0f, separacion, -separacion, separacion * 2f, -separacion * 2f };

        foreach (float offset in offsetsLaterales)
        {
            Vector3 pos = centroFilaAdelante + dirLateral * offset;

            if (Physics.Raycast(pos + Vector3.up * 2f, Vector3.down, out RaycastHit hitEscalon, 10f))
            {                
                GameObject objEsp = hitEscalon.collider.gameObject;
                string nombreEsp = objEsp.name;

                if (nombreEsp == "Cuerpo" || nombreEsp == "Cabeza" ||
    nombreEsp == "Cuello" || objEsp.transform.root.name == "Espectador")
                    continue;

                for (int i = 0; i < 3; i++)
                {
                    if (nombreEsp == "Escalon_Cabecera" ||
                        nombreEsp == "AsientoPlatea(Clone)" ||
                        nombreEsp == "SeatersStandBlock(Clone)" ||
                        nombreEsp == "BlockPlateaCurva(Clone)")
                        break;
                    if (objEsp.transform.parent == null) break;
                    objEsp = objEsp.transform.parent.gameObject;
                    nombreEsp = objEsp.name;
                }

                bool esValido = nombreEsp == "Escalon_Cabecera" ||
                                nombreEsp == "AsientoPlatea(Clone)" ||
                                nombreEsp == "SeatersStandBlock(Clone)" ||
                                nombreEsp == "BlockPlateaCurva(Clone)";

                
                if (esValido)
                {
                    // Obtener Y del Seat
                    float yAsientoEsp = hitEscalon.collider.bounds.max.y;
                    Transform tAsientoEsp = objEsp.transform;
                    Transform seatEsp = tAsientoEsp.Find("Seat");
                    if (seatEsp != null)
                    {
                        Collider seatCol = seatEsp.GetComponent<Collider>();
                        if (seatCol != null) yAsientoEsp = seatCol.bounds.max.y;
                    }

                    

                    Vector3 posEsp = new Vector3(pos.x, yAsientoEsp, pos.z) +
    (esPopular ? Vector3.zero : dirHaciaElCampo * 0.10f);


                    cilindrosEspectadores.Add(CrearEspectador(posEsp, esPopular, false, dirHaciaElCampo));
                }
            }

            else
            {
                Debug.Log($"offset={offset}, NO encontro nada");
            }
        }
    }

    void InstanciarEspectadoresCodo(RaycastHit hit, bool esPopular, Vector3 dirHaciaElCampo, UpperCurveStandWithWalkpathScript codoScript)
    {
        // Encontrar la celda del asiento impactado
        // Subir en jerarquia para encontrar el AsientoPlatea o bloque
        GameObject objImpactado = hit.collider.gameObject;
        string nombre = objImpactado.name;
        for (int i = 0; i < 3; i++)
        {
            if (nombre == "AsientoPlatea(Clone)" || nombre == "BlockPlateaCurva(Clone)") break;
            if (objImpactado.transform.parent == null) break;
            objImpactado = objImpactado.transform.parent.gameObject;
            nombre = objImpactado.name;
        }

        // Buscar en el diccionario la clave de este objeto
        (int fila, int columna, int asiento) claveUsuario = (-1, -1, -1);
        foreach (var kvp in codoScript.mapaObjetos)
        {
            if (kvp.Value == objImpactado)
            {
                claveUsuario = kvp.Key;
                break;
            }
        }

        if (claveUsuario.fila == -1)
        {
            Debug.Log("No encontré el objeto en el diccionario");
            return;
        }

        Debug.Log($"Usuario en fila={claveUsuario.fila}, columna={claveUsuario.columna}, asiento={claveUsuario.asiento}");

        // Buscar asientos en fila+1 (la fila de adelante, mas cerca del campo)
        int filaAdelante = claveUsuario.fila - 1;
     
        if (esPopular)
        {
            Debug.Log($"Usuario en fila={claveUsuario.fila}, columna={claveUsuario.columna}");
            Debug.Log($"Buscando filaAdelante={filaAdelante}");
            Debug.Log($"Total objetos en diccionario: {codoScript.mapaObjetos.Count}");

            InstanciarEspectadores(hit, esPopular, dirHaciaElCampo);           
        }
        else
        {
            int[] offsetsAsiento = { 0, 1, -1, 2, -2 };
            foreach (int offsetA in offsetsAsiento)
            {
                int asientoObjetivo = claveUsuario.asiento + offsetA;
                for (int deltaColumna = 0; deltaColumna <= 1; deltaColumna++)
                {
                    int[] columnas = deltaColumna == 0
                        ? new[] { claveUsuario.columna }
                        : new[] { claveUsuario.columna - 1, claveUsuario.columna + 1 };
                    foreach (int col in columnas)
                    {
                        if (codoScript.mapaObjetos.TryGetValue((filaAdelante, col, asientoObjetivo), out GameObject objAdelante))
                        {
                            float yEsp = objAdelante.transform.position.y - 0.6f;
                            Transform seatEsp = objAdelante.transform.Find("Seat");
                            if (seatEsp != null)
                            {
                                Collider seatCol = seatEsp.GetComponent<Collider>();
                                if (seatCol != null) yEsp = seatCol.bounds.max.y;
                            }
                            Vector3 posEsp = new Vector3(objAdelante.transform.position.x, yEsp, objAdelante.transform.position.z)
                                + dirHaciaElCampo * 0.10f;
                            cilindrosEspectadores.Add(CrearEspectador(posEsp, esPopular, false, dirHaciaElCampo));
                            goto siguiente;
                        }
                    }
                siguiente:;
                }
            }
        }
    }


    GameObject CrearEspectador(Vector3 posicionBase, bool esPopular, bool esUsuario, Vector3 dirMirada)
    {
        GameObject prefab = esUsuario
            ? (esPopular ? prefabUsuarioParado : prefabUsuarioSentado)
            : (esPopular ? prefabEspectadorParado : prefabEspectadorSentado);

        GameObject espectador = Instantiate(prefab, posicionBase, Quaternion.identity);

        if (dirMirada != Vector3.zero)
            espectador.transform.rotation = Quaternion.LookRotation(dirMirada, Vector3.up);

        return espectador;
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
