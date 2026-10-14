using UnityEngine;

// =====================================================================================
//  CÁMARA DEL JUEGO QUE SE ADAPTA A CUALQUIER PANTALLA
//  El mapa es cuadrado (100x100) y las pantallas no: siempre sobra espacio a los lados
//  (celular largo) o arriba/abajo (tableta). Dos formas de evitar los huecos grises:
//
//   - VerMapaCompleto: se ve TODO el mapa y el espacio sobrante se rellena con el mismo
//     fondo del terreno, un poco más oscuro (así se nota dónde termina el mapa).
//   - LlenarPantalla: el mapa ocupa toda la pantalla (sin bordes) y la cámara sigue a tu
//     pollo. Se ve un poco menos del mapa a la vez.
// =====================================================================================
[RequireComponent(typeof(Camera))]
public class ResponsiveCamera2D : MonoBehaviour
{
    public enum CameraMode { VerMapaCompleto, LlenarPantalla }

    [Header("Modo")]
    public CameraMode mode = CameraMode.VerMapaCompleto;

    [Header("Tamaño del Terreno")]
    public float mapWidth = 100f;
    public float mapHeight = 100f;

    [Header("Ver Mapa Completo: relleno de los lados")]
    [Tooltip("Sprite del terreno que se copia para rellenar los lados. Si se deja vacío se busca 'Fondo_Terreno'")]
    public SpriteRenderer sideFill;
    public string sideFillObjectName = "Fondo_Terreno";
    [Tooltip("Color del relleno (más oscuro = se nota más el borde del mapa)")]
    public Color sideFillTint = new Color(0.55f, 0.55f, 0.55f, 1f);

    [Header("Llenar Pantalla: seguir al pollo")]
    public float followSpeed = 6f;
    [Tooltip("Zoom extra (1 = el mapa justo llena la pantalla; menor = más cerca)")]
    [Range(0.4f, 1f)] public float zoom = 1f;

    private Camera cam;
    private float lastAspect = -1f;
    private Transform target;
    private GameObject fillCopy;

    private void Awake()
    {
        cam = GetComponent<Camera>();
        cam.orthographic = true;
        AdjustCamera();
    }

    private void LateUpdate()
    {
        if (!Mathf.Approximately(cam.aspect, lastAspect)) AdjustCamera();
        if (mode == CameraMode.LlenarPantalla) Follow();
    }

    private void AdjustCamera()
    {
        lastAspect = cam.aspect;
        float screenAspect = cam.aspect;
        float mapAspect = mapWidth / mapHeight;

        if (mode == CameraMode.VerMapaCompleto)
        {
            // Que quepa todo el mapa
            cam.orthographicSize = screenAspect >= mapAspect
                ? mapHeight / 2f
                : (mapWidth / 2f) / screenAspect;

            transform.position = new Vector3(0f, 0f, transform.position.z);
            UpdateSideFill();
        }
        else
        {
            // Que el mapa cubra TODA la pantalla (sin bordes)
            float sizeByHeight = mapHeight / 2f;
            float sizeByWidth = (mapWidth / 2f) / screenAspect;
            cam.orthographicSize = Mathf.Min(sizeByHeight, sizeByWidth) * zoom;

            if (fillCopy != null) fillCopy.SetActive(false);
        }
    }

    // Copia el fondo del terreno, más grande y oscuro, detrás del mapa
    private void UpdateSideFill()
    {
        if (sideFill == null && !string.IsNullOrEmpty(sideFillObjectName))
        {
            GameObject found = GameObject.Find(sideFillObjectName);
            if (found != null) sideFill = found.GetComponent<SpriteRenderer>();
        }
        if (sideFill == null || sideFill.sprite == null) return;

        if (fillCopy == null)
        {
            fillCopy = new GameObject("Relleno_Lados");
            SpriteRenderer sr = fillCopy.AddComponent<SpriteRenderer>();
            sr.sprite = sideFill.sprite;
            sr.sharedMaterial = sideFill.sharedMaterial;
            sr.sortingLayerID = sideFill.sortingLayerID;
            sr.sortingOrder = sideFill.sortingOrder - 1;
            sr.color = sideFillTint;
        }
        fillCopy.SetActive(true);

        // Tamaño visible de la cámara y tamaño real del terreno
        float viewH = cam.orthographicSize * 2f;
        float viewW = viewH * cam.aspect;
        Bounds b = sideFill.bounds;
        // Se hace MUY grande a propósito (3 veces la pantalla): así cubre cualquier celular,
        // aunque la pantalla gire o cambie de tamaño después
        float minSide = Mathf.Max(0.01f, Mathf.Min(b.size.x, b.size.y));
        float factor = Mathf.Max(viewW, viewH) * 3f / minSide;

        Vector3 pos = b.center;
        pos.z = sideFill.transform.position.z + 0.01f; // un poco detrás
        fillCopy.transform.position = pos;
        fillCopy.transform.rotation = sideFill.transform.rotation;
        fillCopy.transform.localScale = sideFill.transform.lossyScale * Mathf.Max(1f, factor);
    }

    private void Follow()
    {
        if (target == null)
        {
            foreach (var p in FindObjectsByType<PlayerController>(FindObjectsInactive.Exclude))
                if (p.isLocalPlayer) { target = p.transform; break; }
            if (target == null) return;
        }

        float halfH = cam.orthographicSize;
        float halfW = halfH * cam.aspect;
        float limitX = Mathf.Max(0f, mapWidth / 2f - halfW);
        float limitY = Mathf.Max(0f, mapHeight / 2f - halfH);

        Vector3 desired = new Vector3(
            Mathf.Clamp(target.position.x, -limitX, limitX),
            Mathf.Clamp(target.position.y, -limitY, limitY),
            transform.position.z);

        transform.position = Vector3.Lerp(transform.position, desired, 1f - Mathf.Exp(-followSpeed * Time.deltaTime));
    }
}
