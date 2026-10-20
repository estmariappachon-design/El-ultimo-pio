using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

// =====================================================================================
//  ESTILO DE TODOS LOS TEXTOS DEL JUEGO (un solo lugar para editar fuente, tamaños y colores)
//
//  Cómo usarlo:
//   1. En la ventana Project crea la carpeta  Assets/Resources  (si no existe).
//   2. Clic derecho dentro de Resources > Create > El Ultimo Pio > Estilo de textos.
//   3. Déjale el nombre  EstiloTextos  (exactamente así).
//   4. Selecciónalo y cambia todo en el Inspector. Los cambios se ven al darle Play.
//  Si el archivo no existe, el juego usa los valores por defecto de este script.
// =====================================================================================
[CreateAssetMenu(fileName = "EstiloTextos", menuName = "El Ultimo Pio/Estilo de textos")]
public class TextStyle : ScriptableObject
{
    [System.Serializable]
    public class Style
    {
        [Tooltip("Tamaño de la letra")]
        public float size = 40f;
        public Color color = Color.black;
        [Tooltip("Borde de las letras (para que se lean sobre cualquier fondo)")]
        public Color outlineColor = Color.white;
        [Range(0f, 0.5f)] public float outlineWidth = 0.2f;
        public bool bold = true;

        public Style() { }

        public Style(float size, Color color, Color outline, float outlineWidth, bool bold)
        {
            this.size = size;
            this.color = color;
            this.outlineColor = outline;
            this.outlineWidth = outlineWidth;
            this.bold = bold;
        }
    }

    [Header("FUENTE DEL JUEGO")]
    [Tooltip("Font Asset de TextMeshPro (ej. 'LuckiestGuy-Regular SDF'). Vacío = la fuente que ya tiene cada texto")]
    public TMP_FontAsset gameFont;
    [Tooltip("Poner esta fuente también en TODOS los textos que hiciste en las escenas (menú, lobby, botones...)")]
    public bool applyFontToSceneTexts = true;

    [Header("TEXTOS DE LAS ESCENAS (los que pusiste en el editor)")]
    [Tooltip("Agranda TODOS los textos de las escenas a la vez. 1 = igual que en el editor, 1.3 = 30 % más grandes")]
    [Range(0.5f, 3f)] public float sceneTextScale = 1f;
    [Tooltip("Cambiar el color de todos los textos de las escenas")]
    public bool overrideSceneTextColor = false;
    public Color sceneTextColor = Color.black;

    [Header("RELOJ DE LA PARTIDA")]
    public Color timerColor = Color.black;
    [Tooltip("Color cuando quedan 10 segundos o menos")]
    public Color timerWarningColor = new Color(0.85f, 0.1f, 0.1f);

    [Header("TEXTOS QUE CREA EL JUEGO")]
    [Tooltip("+1, +2 TURBO, ¡CONGELADO! sobre los pollos")]
    public Style popups = new Style(45f, Color.black, Color.white, 0.2f, true);
    [Tooltip("Avisos debajo del reloj (¡A recolectar!, ¡Te congelaron!...)")]
    public Style avisos = new Style(52f, Color.black, Color.white, 0.2f, true);
    [Tooltip("Ranking de arriba a la derecha durante la partida")]
    public Style rankingPartida = new Style(38f, Color.black, Color.white, 0.2f, false);
    [Tooltip("Contadores x2 de los botones Velocidad y Congelar")]
    public Style contadores = new Style(46f, Color.black, Color.white, 0.25f, true);
    [Tooltip("Etiquetas TÚ / J2 sobre los pollos")]
    public Style etiquetas = new Style(28f, Color.black, Color.white, 0.2f, true);
    [Tooltip("Ranking de la pantalla final")]
    public Style rankingFinal = new Style(40f, Color.black, Color.white, 0.2f, true);
    [Tooltip("Textos de los botones de la pantalla final (si no usas imagen)")]
    public Style botonesFinal = new Style(40f, Color.white, Color.black, 0f, true);
    [Tooltip("Aviso ¡Guardado! en el lobby")]
    public Style guardadoLobby = new Style(48f, Color.black, Color.white, 0.2f, true);

    // ---------------------------------------------------------------------------------

    private static TextStyle current;

    // Estilo activo: el archivo Resources/EstiloTextos, o uno con los valores por defecto
    public static TextStyle Current
    {
        get
        {
            if (current == null)
            {
                current = Resources.Load<TextStyle>("EstiloTextos");
                if (current == null) current = CreateInstance<TextStyle>();
            }
            return current;
        }
    }

    // Aplica fuente, tamaño, color, borde y negrita a un texto
    public void Apply(TMP_Text t, Style s, bool setSize = true)
    {
        if (t == null || s == null) return;
        if (gameFont != null) t.font = gameFont;
        if (setSize) t.fontSize = s.size;
        t.color = s.color;
        t.fontStyle = s.bold ? FontStyles.Bold : FontStyles.Normal;
        t.outlineColor = s.outlineColor;
        t.outlineWidth = s.outlineWidth;
    }

    // Solo la fuente (para textos cuyo tamaño/color maneja otro script)
    public void ApplyFont(TMP_Text t)
    {
        if (t != null && gameFont != null) t.font = gameFont;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        current = null;
        registered = false;
    }

    // ---------- Aplicar a los textos de cada escena al cargarla ----------

    private static bool registered;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Init()
    {
        if (!registered)
        {
            SceneManager.sceneLoaded += OnSceneLoaded;
            registered = true;
        }
        StyleSceneTexts(SceneManager.GetActiveScene());
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        StyleSceneTexts(scene);
    }

    private static readonly HashSet<int> styled = new HashSet<int>();

    private static void StyleSceneTexts(Scene scene)
    {
        TextStyle st = Current;
        foreach (TMP_Text t in Object.FindObjectsByType<TMP_Text>(FindObjectsInactive.Include))
        {
            if (t == null || t.gameObject.scene != scene) continue;
            if (!styled.Add(t.GetInstanceID())) continue; // no aplicar dos veces al mismo texto

            if (st.applyFontToSceneTexts && st.gameFont != null) t.font = st.gameFont;

            if (!Mathf.Approximately(st.sceneTextScale, 1f))
            {
                t.fontSize *= st.sceneTextScale;
                if (t.enableAutoSizing)
                {
                    t.fontSizeMax *= st.sceneTextScale;
                    t.fontSizeMin *= st.sceneTextScale;
                }
            }

            if (st.overrideSceneTextColor) t.color = st.sceneTextColor;
        }
    }
}
