using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// =====================================================================================
//  INTERFAZ IGUAL EN CUALQUIER PANTALLA (celular largo, tableta, PC)
//
//  NO mueve nada: cada botón, texto y letrero se queda EXACTAMENTE donde está en el editor.
//  Solo hace tres cosas (se aplica sola a todos los Canvas al cargar cada escena):
//   1. Canvas Scaler en "Expand": todo el diseño de 1920x1080 se escala parejo y SIEMPRE
//      cabe completo, sin cortar botones. Si la pantalla es más larga, sobra un poco de
//      fondo a los lados; si es más cuadrada (tableta), arriba y abajo.
//   2. En "Screen Space - Camera", la interfaz y los letreros hechos con sprites se dibujan
//      AL FRENTE del mapa (antes el fondo los tapaba).
//   3. El juego se abre siempre en horizontal.
// =====================================================================================
[DisallowMultipleComponent]
public class ResponsiveUI : MonoBehaviour
{
    [Tooltip("Todo el diseño 1920x1080 cabe completo en cualquier pantalla (recomendado)")]
    public bool expandToFit = false;

    [Tooltip("Orden de dibujo de la interfaz en 'Screen Space - Camera' (más alto = más al frente)")]
    public int uiSortingOrder = 400;

    private bool applied;

    private void Start()
    {
        Apply();
    }

    public void Apply()
    {
        if (applied) return;
        applied = true;

        Canvas canvas = GetComponent<Canvas>();
        if (canvas == null || !canvas.isRootCanvas || canvas.renderMode == RenderMode.WorldSpace) return;

        CanvasScaler scaler = GetComponent<CanvasScaler>();
        if (scaler != null && expandToFit)
        {
            if (scaler.uiScaleMode != CanvasScaler.ScaleMode.ScaleWithScreenSize)
            {
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920f, 1080f);
            }
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
        }

        if (canvas.renderMode == RenderMode.ScreenSpaceCamera) BringToFront(canvas);
    }

    private void BringToFront(Canvas canvas)
    {
        if (canvas.sortingOrder < uiSortingOrder) canvas.sortingOrder = uiSortingOrder;

        // Letreros con SpriteRenderer dentro del Canvas: justo debajo de los textos del Canvas
        foreach (SpriteRenderer sr in GetComponentsInChildren<SpriteRenderer>(true))
        {
            sr.sortingLayerID = canvas.sortingLayerID;
            sr.sortingOrder = canvas.sortingOrder - 10 + Mathf.Clamp(sr.sortingOrder, 0, 9);
        }
    }
}

// Pone ResponsiveUI en todos los Canvas de cada escena que se carga
public static class ResponsiveUIInstaller
{
    private static bool registered;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        registered = false;
    }

    // El juego SIEMPRE en horizontal (las dos posiciones horizontales, nunca vertical)
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void ForceLandscape()
    {
        Screen.autorotateToPortrait = false;
        Screen.autorotateToPortraitUpsideDown = false;
        Screen.autorotateToLandscapeLeft = true;
        Screen.autorotateToLandscapeRight = true;

        if (Screen.orientation == ScreenOrientation.Portrait || Screen.orientation == ScreenOrientation.PortraitUpsideDown)
            Screen.orientation = ScreenOrientation.LandscapeLeft;
        Screen.orientation = ScreenOrientation.AutoRotation;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Init()
    {
        if (!registered)
        {
            SceneManager.sceneLoaded += OnSceneLoaded;
            registered = true;
        }
        InstallAll(SceneManager.GetActiveScene());
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        InstallAll(scene);
    }

    private static void InstallAll(Scene scene)
    {
        foreach (Canvas c in Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include))
        {
            if (!c.isRootCanvas || c.GetComponent<ResponsiveUI>() != null) continue;
            if (c.gameObject.scene != scene) continue;
            c.gameObject.AddComponent<ResponsiveUI>();
        }
    }
}
