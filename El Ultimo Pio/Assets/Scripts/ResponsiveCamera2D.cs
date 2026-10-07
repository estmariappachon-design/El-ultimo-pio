using UnityEngine;

[RequireComponent(typeof(Camera))]
public class ResponsiveCamera2D : MonoBehaviour
{
    [Header("Tamaño del Terreno")]
    public float mapWidth = 100f;
    public float mapHeight = 100f;

    void Awake()
    {
        AdjustCamera();
    }

    void AdjustCamera()
    {
        Camera cam = GetComponent<Camera>();
        float screenAspect = (float)Screen.width / (float)Screen.height;
        float targetAspect = mapWidth / mapHeight;

        if (screenAspect >= targetAspect)
        {
            // La pantalla es más ancha que el mapa: ajustamos basándonos en la altura
            cam.orthographicSize = mapHeight / 2f;
        }
        else
        {
            // La pantalla es más angosta: ajustamos basándonos en el ancho para no recortar nada
            float differenceInSize = targetAspect / screenAspect;
            cam.orthographicSize = (mapHeight / 2f) * differenceInSize;
        }
    }
}