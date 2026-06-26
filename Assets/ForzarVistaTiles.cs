using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using CesiumForUnity;

public class ForzarCargaTiles : MonoBehaviour
{
    public Cesium3DTileset tileset;

    IEnumerator Start()
    {
        // Espera un frame para que Cesium inicialice
        yield return null;
        yield return null;
        tileset.RecreateTileset();
    }
}
