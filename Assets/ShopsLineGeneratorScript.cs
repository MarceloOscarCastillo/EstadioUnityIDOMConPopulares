using System.Collections.Generic;
using UnityEngine;

public class ShopsLineGenerator : MonoBehaviour
{
    [System.Serializable]
    public struct ComercioPrefab
    {
        public string nombre;
        public GameObject prefab;
        [Min(0.1f)] public float largo;
        [Min(0f)] public float peso;
    }

    [System.Serializable]
    public struct Tira
    {
        public string nombre;
        public float largo;
        public float alturaY;
        public float offsetX;
        public float offsetZ;
    }

    [Header("Prefabs de comercios")]
    public List<ComercioPrefab> comercios = new List<ComercioPrefab>();

    [Header("Tiras a generar")]
    public List<Tira> tiras = new List<Tira>();

    [Header("Materiales del cubo principal")]
    public string nombreCuboPrincipal = "CuboPrincipal";
    public List<Material> materialesPosibles = new List<Material>();
    public bool evitarRepetirMaterialContiguo = true;

    [Header("Orientacion")]
    public float rotacionYComercios = 0f;
    public float rotacionYAvance = 0f;

    [Header("Aleatoriedad")]
    public int semilla = 7777;

    private const string NOMBRE_CONTENEDOR = "Contenedor_Comercios";

    [ContextMenu("Generar Comercios")]
    public void GenerarComercios()
    {
        LimpiarComercios();

        GameObject contenedor = new GameObject(NOMBRE_CONTENEDOR);
        contenedor.transform.SetParent(this.transform, false);
        contenedor.tag = "SectorEstadio";

        System.Random rnd = new System.Random(semilla);

        foreach (Tira tira in tiras)
            GenerarTira(tira, contenedor.transform, rnd);

        foreach (Transform hijo in contenedor.GetComponentsInChildren<Transform>())
        {
            if (hijo.gameObject != contenedor && Application.isPlaying)
                hijo.gameObject.isStatic = true;
        }

        if (Application.isPlaying)
            StaticBatchingUtility.Combine(contenedor);
    }

    void GenerarTira(Tira tira, Transform padre, System.Random rnd)
    {
        List<ComercioPrefab> elegidos = SortearSecuencia(tira.largo, rnd);
        if (elegidos.Count == 0) return;

        float largoUsado = 0f;
        foreach (ComercioPrefab c in elegidos) largoUsado += c.largo;

        Quaternion rotLocal = Quaternion.Euler(0f, rotacionYComercios, 0f);
        Vector3 dirAvanceLocal = Quaternion.Euler(0f, rotacionYAvance, 0f) * Vector3.right;
        Vector3 origenLocal = new Vector3(tira.offsetX, tira.alturaY, tira.offsetZ)
                            - dirAvanceLocal * (largoUsado / 2f);

        float recorrido = 0f;
        int indiceMaterialAnterior = -1;

        for (int i = 0; i < elegidos.Count; i++)
        {
            ComercioPrefab c = elegidos[i];

            Vector3 posLocal = origenLocal + dirAvanceLocal * (recorrido + c.largo / 2f);

            GameObject local = Instantiate(c.prefab, padre);
            local.name = $"Comercio_{tira.nombre}_{i}";
            local.transform.position = transform.TransformPoint(posLocal);
            local.transform.rotation = transform.rotation * rotLocal;

            AplicarMaterialPrincipal(local, rnd, ref indiceMaterialAnterior);

            recorrido += c.largo;
        }
    }

    List<ComercioPrefab> SortearSecuencia(float largoDisponible, System.Random rnd)
    {
        List<ComercioPrefab> resultado = new List<ComercioPrefab>();
        float restante = largoDisponible;

        while (true)
        {
            List<ComercioPrefab> candidatos = new List<ComercioPrefab>();
            float pesoTotal = 0f;

            foreach (ComercioPrefab c in comercios)
            {
                if (c.prefab == null) continue;
                if (c.largo > restante + 0.001f) continue;
                if (c.peso <= 0f) continue;
                candidatos.Add(c);
                pesoTotal += c.peso;
            }

            if (candidatos.Count == 0 || pesoTotal <= 0f) break;

            float r = (float)rnd.NextDouble() * pesoTotal;
            float acum = 0f;
            ComercioPrefab elegido = candidatos[candidatos.Count - 1];

            foreach (ComercioPrefab c in candidatos)
            {
                acum += c.peso;
                if (r <= acum) { elegido = c; break; }
            }

            resultado.Add(elegido);
            restante -= elegido.largo;
        }

        return resultado;
    }

    void AplicarMaterialPrincipal(GameObject local, System.Random rnd, ref int indiceAnterior)
    {
        if (materialesPosibles == null || materialesPosibles.Count == 0) return;

        Transform cubo = BuscarHijoPorNombre(local.transform, nombreCuboPrincipal);
        if (cubo == null) return;

        Renderer r = cubo.GetComponent<Renderer>();
        if (r == null) return;

        int indice = rnd.Next(materialesPosibles.Count);

        if (evitarRepetirMaterialContiguo && materialesPosibles.Count > 1)
        {
            int intentos = 0;
            while (indice == indiceAnterior && intentos < 10)
            {
                indice = rnd.Next(materialesPosibles.Count);
                intentos++;
            }
        }

        if (materialesPosibles[indice] != null)
            r.sharedMaterial = materialesPosibles[indice];

        indiceAnterior = indice;
    }

    Transform BuscarHijoPorNombre(Transform raiz, string nombre)
    {
        if (raiz.name == nombre) return raiz;

        foreach (Transform hijo in raiz)
        {
            Transform encontrado = BuscarHijoPorNombre(hijo, nombre);
            if (encontrado != null) return encontrado;
        }

        return null;
    }

    [ContextMenu("Limpiar Comercios")]
    public void LimpiarComercios()
    {
        Transform viejo = transform.Find(NOMBRE_CONTENEDOR);
        if (viejo == null) return;

        if (Application.isPlaying) Destroy(viejo.gameObject);
        else DestroyImmediate(viejo.gameObject);
    }
}
