using UnityEngine;

public class InvalidMeshDetector : MonoBehaviour
{
    [ContextMenu("Buscar Meshes Invalidos")]
    void Buscar()
    {
        int encontrados = 0;

        foreach (MeshFilter mf in Object.FindObjectsByType<MeshFilter>(FindObjectsSortMode.None))
        {
            if (mf.sharedMesh == null) continue;
            Bounds b = mf.sharedMesh.bounds;

            bool invalido = float.IsNaN(b.center.x) || float.IsNaN(b.center.y) || float.IsNaN(b.center.z)
                         || float.IsNaN(b.size.x) || float.IsNaN(b.size.y) || float.IsNaN(b.size.z)
                         || float.IsInfinity(b.size.x) || float.IsInfinity(b.size.y) || float.IsInfinity(b.size.z);

            if (invalido)
            {
                Debug.LogError($"Mesh invalido: {mf.gameObject.name}", mf.gameObject);
                encontrados++;
            }
        }

        Debug.Log($"Busqueda terminada: {encontrados} meshes invalidos");
    }
}