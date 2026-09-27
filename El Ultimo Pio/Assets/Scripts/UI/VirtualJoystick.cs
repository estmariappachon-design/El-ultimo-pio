using UnityEngine;
using UnityEngine.EventSystems;

public class VirtualJoystick : MonoBehaviour,
    IPointerDownHandler,
    IDragHandler,
    IPointerUpHandler
{
    [SerializeField] private RectTransform handleRect;

    private RectTransform bgRect;

    private Vector2 inputVector = Vector2.zero;

    // Indica si realmente estamos tocando el joystick
    public bool IsPressed { get; private set; }

    // Dirección del joystick
    public Vector2 Direction => inputVector;

    private void Awake()
    {
        bgRect = GetComponent<RectTransform>();

        if (handleRect == null && transform.childCount > 0)
        {
            handleRect = transform.GetChild(0).GetComponent<RectTransform>();
        }

        ResetJoystick();
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        IsPressed = true;

        UpdateJoystick(eventData);
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (!IsPressed)
            return;

        UpdateJoystick(eventData);
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        IsPressed = false;

        ResetJoystick();
    }

    private void UpdateJoystick(PointerEventData eventData)
    {
        Vector2 localPoint;

        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
            bgRect,
            eventData.position,
            eventData.pressEventCamera,
            out localPoint))
        {
            return;
        }

        float width = bgRect.rect.width;
        float height = bgRect.rect.height;

        if (width <= 0 || height <= 0)
            return;

        float x = localPoint.x / (width * 0.5f);
        float y = localPoint.y / (height * 0.5f);

        inputVector = new Vector2(x, y);

        // Limitar el joystick al círculo
        inputVector = Vector2.ClampMagnitude(inputVector, 1f);

        // Zona muerta
        if (inputVector.magnitude < 0.15f)
        {
            inputVector = Vector2.zero;
        }

        if (handleRect != null)
        {
            float radius = Mathf.Min(width, height) * 0.5f * 0.7f;

            handleRect.anchoredPosition = inputVector * radius;
        }
    }

    private void ResetJoystick()
    {
        inputVector = Vector2.zero;

        if (handleRect != null)
        {
            handleRect.anchoredPosition = Vector2.zero;
        }
    }
}