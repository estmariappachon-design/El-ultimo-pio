using UnityEngine;
using UnityEngine.EventSystems;

public class VirtualJoystick : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
{
    // Dirección global que lee el PlayerController (valores de -1 a 1)
    public static Vector2 Direction { get; private set; }

    [Header("Asignar en el Inspector")]
    [SerializeField] private RectTransform background; // El círculo grande (Joystick_Outer)
    [SerializeField] private RectTransform handle;     // El círculo pequeño (hijo del grande)

    [SerializeField, Range(0.1f, 1f)] private float handleRange = 0.4f;

    // Evita que quede un valor viejo al volver a darle Play en el editor
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        Direction = Vector2.zero;
    }

    private void Awake()
    {
        if (background == null) background = GetComponent<RectTransform>();
        ResetJoystick();
    }

    private void OnDisable()
    {
        ResetJoystick();
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        UpdateJoystick(eventData);
    }

    public void OnDrag(PointerEventData eventData)
    {
        UpdateJoystick(eventData);
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        ResetJoystick();
    }

    private void UpdateJoystick(PointerEventData eventData)
    {
        // Para Canvas en Screen Space - Overlay la cámara es null, y eso es correcto
        Camera cam = eventData.pressEventCamera;

        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                background, eventData.position, cam, out Vector2 localPoint))
            return;

        // Calcula respecto al centro real del fondo, sin importar el pivot
        Vector2 center = background.rect.center;
        float radius = background.rect.width * 0.5f;

        Vector2 input = (localPoint - center) / radius;
        if (input.magnitude > 1f) input = input.normalized;

        Direction = input;

        if (handle != null)
            handle.anchoredPosition = input * (radius * handleRange);
    }

    private void ResetJoystick()
    {
        Direction = Vector2.zero;
        if (handle != null) handle.anchoredPosition = Vector2.zero;
    }
}