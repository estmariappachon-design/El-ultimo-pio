using UnityEngine;

// Ponlo en un objeto "SafeArea" hijo directo del Canvas, con anclas en Stretch (0,0 -> 1,1)
// y Left/Right/Top/Bottom = 0. Mete DENTRO los botones, textos y joystick.
// Evita que el notch, la cámara frontal o las esquinas redondeadas tapen la interfaz.
[RequireComponent(typeof(RectTransform))]
public class SafeAreaPanel : MonoBehaviour
{
    private RectTransform rt;
    private Rect lastSafe;
    private Vector2Int lastScreen;

    private void Awake()
    {
        rt = GetComponent<RectTransform>();
        Apply();
    }

    private void Update()
    {
        if (Screen.safeArea != lastSafe || lastScreen.x != Screen.width || lastScreen.y != Screen.height)
            Apply();
    }

    private void Apply()
    {
        lastSafe = Screen.safeArea;
        lastScreen = new Vector2Int(Screen.width, Screen.height);
        if (Screen.width <= 0 || Screen.height <= 0) return;

        Vector2 min = lastSafe.position;
        Vector2 max = lastSafe.position + lastSafe.size;
        min.x /= Screen.width; min.y /= Screen.height;
        max.x /= Screen.width; max.y /= Screen.height;

        rt.anchorMin = min;
        rt.anchorMax = max;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }
}