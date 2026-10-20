using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class VirtualJoystick : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
{
    // Dirección global que lee el PlayerController (valores de -1 a 1)
    public static Vector2 Direction { get; private set; }

    [Header("Asignar en el Inspector")]
    [SerializeField] private RectTransform background; // El círculo grande (Joystick_Outer)
    [SerializeField] private RectTransform handle;     // El círculo pequeño (hijo del grande)

    [SerializeField, Range(0.1f, 1f)] private float handleRange = 0.4f;

    [Header("Colores (la transparencia es el canal A del color)")]
    [Tooltip("Aplicar estos colores. Desmárcalo para dejar los colores que tienen las imágenes en la escena")]
    [SerializeField] private bool useCustomColors = true;
    [SerializeField] private Color backgroundColor = new Color(1f, 1f, 1f, 0.35f);   // círculo grande
    [SerializeField] private Color handleColor = new Color(1f, 0.85f, 0.25f, 0.9f);  // círculo pequeño
    [Tooltip("Color del círculo pequeño mientras lo arrastras")]
    [SerializeField] private Color handlePressedColor = new Color(1f, 0.6f, 0.1f, 1f);
    [Tooltip("Color del círculo grande mientras lo usas")]
    [SerializeField] private Color backgroundPressedColor = new Color(1f, 1f, 1f, 0.5f);

    [Header("Imágenes (opcional: cambiar el dibujo del joystick)")]
    [SerializeField] private Sprite backgroundSprite;
    [SerializeField] private Sprite handleSprite;

    private Image backgroundImage;
    private Image handleImage;

    // Evita que quede un valor viejo al volver a darle Play en el editor
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        Direction = Vector2.zero;
    }

    private void Awake()
    {
        if (background == null) background = GetComponent<RectTransform>();

        backgroundImage = background != null ? background.GetComponent<Image>() : null;
        handleImage = handle != null ? handle.GetComponent<Image>() : null;
        if (backgroundImage != null && backgroundSprite != null) backgroundImage.sprite = backgroundSprite;
        if (handleImage != null && handleSprite != null) handleImage.sprite = handleSprite;

        // El círculo pequeño no debe "robar" el toque al grande
        if (handleImage != null) handleImage.raycastTarget = false;

        ResetJoystick();
    }

    private void OnDisable()
    {
        ResetJoystick();
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        SetPressedColors(true);
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

    private void SetPressedColors(bool pressed)
    {
        if (!useCustomColors) return;
        if (backgroundImage != null) backgroundImage.color = pressed ? backgroundPressedColor : backgroundColor;
        if (handleImage != null) handleImage.color = pressed ? handlePressedColor : handleColor;
    }

    // Para ver los colores nuevos en el editor sin darle Play
    private void OnValidate()
    {
        if (!useCustomColors) return;
        Image bg = background != null ? background.GetComponent<Image>() : GetComponent<Image>();
        Image hd = handle != null ? handle.GetComponent<Image>() : null;
        if (bg != null) bg.color = backgroundColor;
        if (hd != null) hd.color = handleColor;
    }

    private void ResetJoystick()
    {
        Direction = Vector2.zero;
        SetPressedColors(false);
        if (handle != null) handle.anchoredPosition = Vector2.zero;
    }
}