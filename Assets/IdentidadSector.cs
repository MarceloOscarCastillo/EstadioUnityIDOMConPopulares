using UnityEngine;

public class IdentidadSector : MonoBehaviour
{
    [Tooltip("Sector al que contribuye este controller. Ej: Platea Sur")]
    public string sector = "";

    [Tooltip("Nombre de este subsector. Ej: Codo Este")]
    public string subsector = "";

    [Tooltip("Orden dentro del sector, para el listado")]
    public int orden = 0;
}