using UnityEngine;

public class CespedGenerator : MonoBehaviour
{
    public int anchoFranja = 50;
    public Color colorClaro = new Color(0.2f, 0.6f, 0.1f);
    public Color colorOscuro = new Color(0.15f, 0.45f, 0.08f);
    public int resolucion = 512;
    public float ruido = 0.02f;
    public float escalaRuido = 5f;
    public Renderer rendererCesped; // arrastrás FutbolField aquí

    [ContextMenu("Generar Cesped")]
    public void GenerarCesped()
    {
        if (rendererCesped == null) return;

        Texture2D textura = new Texture2D(resolucion, resolucion);

        // Calcular en pixeles donde empieza y termina el campo de juego
        float margenPorcentajeZ = 7.15f / 119.3f;
        float margenPorcentajeX = 6f / 80f;
        int margenZ = Mathf.RoundToInt(margenPorcentajeZ * resolucion);
        int margenX = Mathf.RoundToInt(margenPorcentajeX * resolucion);
        int campoInicioZ = margenZ;
        int campoFinZ = resolucion - margenZ;
        int campoInicioX = margenX;
        int campoFinX = resolucion - margenX;


        for (int y = 0; y < resolucion; y++)
        {
            for (int x = 0; x < resolucion; x++)
            {
                Color color;

                bool enMargen = y < campoInicioZ || y >= campoFinZ ||
                                x < campoInicioX || x >= campoFinX;

                if (enMargen)
                {
                    color = colorClaro;
                }
                else
                {
                    float noise = Mathf.PerlinNoise(x * escalaRuido / resolucion, y * escalaRuido / resolucion);
                    float yConRuido = y + (noise - 0.5f) * ruido * resolucion;
                    bool esClaro = ((int)(yConRuido / anchoFranja)) % 2 == 0;
                    float variacion = Mathf.PerlinNoise(x * 3f / resolucion, y * 3f / resolucion) * 0.05f;
                    color = esClaro ? colorClaro : colorOscuro;
                    color = new Color(color.r + variacion, color.g + variacion, color.b + variacion);
                }

                textura.SetPixel(x, y, color);
            }
        }

        textura.Apply();
        rendererCesped.sharedMaterial.SetTexture("_BaseMap", textura);

#if UNITY_EDITOR
        if (!System.IO.Directory.Exists("Assets/Textures"))
            System.IO.Directory.CreateDirectory("Assets/Textures");
        UnityEditor.AssetDatabase.CreateAsset(textura, "Assets/Textures/CespedGenerado.asset");
        UnityEditor.AssetDatabase.SaveAssets();
        Debug.Log("Textura guardada en Assets/Textures/CespedGenerado.asset");
#endif
    }
}

