using UnityEngine;

// OPCIONAL. Si en el celular los pollos se ven muy pequeños con el mapa completo,
// pon este script en la Main Camera y DESACTIVA ResponsiveCamera2D.
// La cámara sigue al pollo local con un zoom más cercano y nunca muestra fuera del terreno.
[RequireComponent(typeof(Camera))]
public class CameraFollow2D : MonoBehaviour
{
    [SerializeField] private float zoom = 22f;          // orthographicSize (más pequeño = más cerca)
    [SerializeField] private float followSpeed = 6f;
    [SerializeField] private float mapHalfSize = 50f;

    private Camera cam;
    private Transform target;

    private void Awake()
    {
        cam = GetComponent<Camera>();
        cam.orthographic = true;
        cam.orthographicSize = zoom;
    }

    private void LateUpdate()
    {
        if (target == null)
        {
            foreach (var p in FindObjectsByType<PlayerController>(FindObjectsInactive.Exclude))
                if (p.isLocalPlayer) { target = p.transform; break; }
            if (target == null) return;
        }

        float halfH = cam.orthographicSize;
        float halfW = halfH * cam.aspect;

        Vector3 desired = target.position;
        desired.x = halfW >= mapHalfSize ? 0f : Mathf.Clamp(desired.x, -mapHalfSize + halfW, mapHalfSize - halfW);
        desired.y = halfH >= mapHalfSize ? 0f : Mathf.Clamp(desired.y, -mapHalfSize + halfH, mapHalfSize - halfH);
        desired.z = transform.position.z;

        transform.position = Vector3.Lerp(transform.position, desired, 1f - Mathf.Exp(-followSpeed * Time.deltaTime));
    }
}
