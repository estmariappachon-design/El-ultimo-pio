using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// =====================================================================================
//  Respuesta visual de los botones (sirve en PC y en celular)
//  - PC: al pasar el mouse el botón crece un poco (hover)
//  - Celular y PC: al tocar/presionar se "hunde" y se oscurece; al soltar rebota
//  - Estado "seleccionado" (para el pollo elegido en el lobby): más grande y con borde
//  - Estado "atenuado" (power-up sin cargas): más pequeño
//
//  No hay que agregarlo a mano: UIFeedbackInstaller lo pone en TODOS los botones
//  de cada escena al cargarla.
// =====================================================================================
[DisallowMultipleComponent]
public class UIButtonFeedback : MonoBehaviour,
    IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
{
    [SerializeField] private float hoverScale = 1.12f;
    [SerializeField] private float pressedScale = 0.88f;
    [SerializeField] private float selectedScale = 1.18f;
    [SerializeField] private float dimmedScale = 0.9f;
    [SerializeField] private float speed = 18f;
    [SerializeField] private Color selectedOutline = new Color(1f, 0.85f, 0.2f, 1f);

    private Button button;
    private Vector3 baseScale;
    private float current = 1f;
    private bool hovered, pressed, selected, dimmed;
    private Outline outline;

    private void Awake()
    {
        button = GetComponent<Button>();
        baseScale = transform.localScale;
        if (baseScale == Vector3.zero) baseScale = Vector3.one;

        if (button != null)
        {
            // En celular no existe el "hover", y el estado "Selected" se queda pegado
            // después de tocar: se iguala al normal para que el botón no se vea raro.
            Navigation nav = button.navigation;
            nav.mode = Navigation.Mode.None;
            button.navigation = nav;

            if (button.transition == Selectable.Transition.ColorTint)
            {
                ColorBlock c = button.colors;
                c.highlightedColor = Color.Lerp(c.normalColor, Color.white, 0.5f);
                c.pressedColor = c.normalColor * new Color(0.75f, 0.75f, 0.75f, 1f);
                c.selectedColor = c.normalColor;
                c.fadeDuration = 0.08f;
                button.colors = c;
            }
        }
    }

    private void OnDisable()
    {
        hovered = pressed = false;
        current = Target();
        transform.localScale = baseScale * current;
    }

    private bool Usable => button == null || button.IsInteractable();

    public void OnPointerEnter(PointerEventData e)
    {
        // En el celular un toque también dispara "enter": solo hay hover real en PC
        if (!Application.isMobilePlatform) hovered = true;
    }

    public void OnPointerExit(PointerEventData e)
    {
        hovered = false;
        pressed = false;
    }

    public void OnPointerDown(PointerEventData e)
    {
        if (Usable) pressed = true;

        // Para comprobar en la Console que el script SÍ está funcionando (poner en false al terminar)
        if (DebugLogs) Debug.Log("[UIButtonFeedback] Presionado: " + name);
    }

    public static bool DebugLogs = true;

    public void OnPointerUp(PointerEventData e)
    {
        if (pressed) current *= 1.12f; // pequeño rebote al soltar
        pressed = false;
    }

    public void SetSelected(bool value)
    {
        selected = value;
        if (value && outline == null)
        {
            outline = GetComponent<Outline>();
            if (outline == null) outline = gameObject.AddComponent<Outline>();
            outline.effectColor = selectedOutline;
            outline.effectDistance = new Vector2(6f, -6f);
        }
        if (outline != null) outline.enabled = value;
    }

    public void SetDimmed(bool value)
    {
        dimmed = value;
    }

    private float Target()
    {
        if (pressed) return pressedScale;
        float t = 1f;
        if (selected) t = selectedScale;
        else if (dimmed) t = dimmedScale;
        if (hovered && Usable) t *= hoverScale;
        return t;
    }

    private void Update()
    {
        current = Mathf.Lerp(current, Target(), 1f - Mathf.Exp(-speed * Time.unscaledDeltaTime));
        transform.localScale = baseScale * current;
    }
}

// Agrega UIButtonFeedback a todos los botones de cada escena que se cargue
public static class UIFeedbackInstaller
{
    private static bool registered;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        registered = false;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Init()
    {
        if (!registered)
        {
            SceneManager.sceneLoaded += OnSceneLoaded;
            registered = true;
        }
        InstallAll();
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        InstallAll();
    }

    public static void InstallAll()
    {
        foreach (Button b in Object.FindObjectsByType<Button>(FindObjectsInactive.Include))
        {
            if (b.GetComponent<UIButtonFeedback>() == null)
                b.gameObject.AddComponent<UIButtonFeedback>();
        }
    }
}
