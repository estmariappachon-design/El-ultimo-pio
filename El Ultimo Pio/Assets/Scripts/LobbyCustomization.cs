using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// =====================================================================================
//  Elección del pollo en el Lobby
//  1. El jugador toca el LÁPIZ (BtnEditarN) -> se abre "OpcionesColores" AL LADO de su nido
//  2. Toca un pollo de la paleta (ColorButtonN) -> se ve en su nido (todavía sin guardar)
//  3. Toca el CHULO (BtnGuardarN) -> se guarda y se envía al servidor
//     (X = cancelar: vuelve al último pollo guardado)
//  Al empezar, el servidor manda a todos el pollo GUARDADO de cada jugador y
//  GameManager lo pone en la partida (por eso chickenSprites debe tener el MISMO
//  orden aquí y en el GameManager de Gameplay).
// =====================================================================================
public class LobbyCustomization : MonoBehaviour
{
    [Header("UI General")]
    [SerializeField] private GameObject chickenOptionsPanel; // 'OpcionesColores'

    [Header("Pollos disponibles (MISMO ORDEN que en GameManager)")]
    [SerializeField] private Sprite[] chickenSprites;

    [Header("Elementos de los 4 nidos")]
    [SerializeField] private Image[] chickenPreviews;  // Las 4 imágenes 'ChickenPreview'
    [SerializeField] private Button[] editButtons;     // Los 4 lápices (BtnEditarN)
    [SerializeField] private Button[] confirmButtons;  // Los 4 chulos (BtnGuardarN)

    [Header("Botones de la paleta (opcional)")]
    [Tooltip("Si se deja vacío, se buscan solos los hijos de OpcionesColores llamados ColorButton1, ColorButton2...")]
    [SerializeField] private Button[] optionButtons;

    [Header("Posición de la paleta")]
    [Tooltip("Separación entre el nido y la paleta")]
    [SerializeField] private float panelGap = 20f;

    [Header("Arreglos automáticos de la escena")]
    [Tooltip("Pone cada ✔ al lado de su lápiz (en la escena estaban todos debajo del Nido 1, tapados en el celular)")]
    [SerializeField] private bool autoPlaceConfirmButtons = true;
    [SerializeField] private float confirmSpacing = 15f;

    [Header("Aviso al guardar (opcional)")]
    [SerializeField] private string savedMessage = "¡Guardado!";
    [SerializeField] private Color savedTextColor = Color.black;
    [SerializeField] private Color savedOutlineColor = Color.white;
    [SerializeField, Range(0f, 0.5f)] private float savedOutlineWidth = 0.2f;

    private int localPlayerSlot = 0;
    private int confirmedChicken = 0; // el que tiene el servidor
    private int tempChicken = 0;      // el que se está probando en la paleta

    private bool PanelOpen => chickenOptionsPanel != null && chickenOptionsPanel.activeSelf;

    // =================================================================================
    //  Inicio
    // =================================================================================

    private void Awake()
    {
        if (chickenOptionsPanel != null)
            chickenOptionsPanel.SetActive(false);

        FindOptionButtons();
        FixTouchBlockers();
        if (autoPlaceConfirmButtons) PlaceConfirmButtons();
        AddFeedbackToButtons();
    }

    private void Start()
    {
        UpdateConnectedPlayers(new[] { GameSession.MySlot });
    }

    // Hover / animación al tocar en lápices, chulos, X y los pollos de la paleta
    private void AddFeedbackToButtons()
    {
        List<Button> all = new List<Button>();
        if (editButtons != null) all.AddRange(editButtons);
        if (confirmButtons != null) all.AddRange(confirmButtons);
        if (optionButtons != null) all.AddRange(optionButtons);
        if (chickenOptionsPanel != null) all.AddRange(chickenOptionsPanel.GetComponentsInChildren<Button>(true));

        foreach (Button b in all)
            if (b != null && b.GetComponent<UIButtonFeedback>() == null)
                b.gameObject.AddComponent<UIButtonFeedback>();
    }

    // Las imágenes decorativas (nidos y vistas previas) no deben "atrapar" los toques.
    // El Nido 1 tapaba los ✔ de los jugadores 2, 3 y 4: en el PC funcionaba porque
    // el anfitrión es el jugador 1, pero en los celulares no.
    private void FixTouchBlockers()
    {
        if (chickenPreviews != null)
        {
            foreach (Image preview in chickenPreviews)
            {
                if (preview == null) continue;
                preview.raycastTarget = false;

                Image nest = preview.transform.parent != null ? preview.transform.parent.GetComponent<Image>() : null;
                if (nest != null) nest.raycastTarget = false;
            }
        }

        // Los textos e imágenes DENTRO de los botones no necesitan recibir toques
        List<Button> buttons = new List<Button>();
        if (editButtons != null) buttons.AddRange(editButtons);
        if (confirmButtons != null) buttons.AddRange(confirmButtons);
        if (optionButtons != null) buttons.AddRange(optionButtons);

        foreach (Button b in buttons)
        {
            if (b == null) continue;
            foreach (Graphic g in b.GetComponentsInChildren<Graphic>(true))
                if (g.gameObject != b.gameObject) g.raycastTarget = false;
        }
    }

    // Coloca cada ✔ a la izquierda de su lápiz, dentro del mismo nido y por encima de todo
    private void PlaceConfirmButtons()
    {
        if (editButtons == null || confirmButtons == null) return;

        for (int i = 0; i < confirmButtons.Length && i < editButtons.Length; i++)
        {
            Button edit = editButtons[i];
            Button confirm = confirmButtons[i];
            if (edit == null || confirm == null) continue;

            RectTransform e = edit.GetComponent<RectTransform>();
            RectTransform c = confirm.GetComponent<RectTransform>();

            c.SetParent(e.parent, false);
            c.anchorMin = e.anchorMin;
            c.anchorMax = e.anchorMax;
            c.pivot = e.pivot;
            c.anchoredPosition = e.anchoredPosition - new Vector2(e.rect.width * 0.5f + c.rect.width * 0.5f + confirmSpacing, 0f);

            e.SetAsLastSibling();
            c.SetAsLastSibling();
        }
    }

    private void FindOptionButtons()
    {
        if ((optionButtons != null && optionButtons.Length > 0) || chickenOptionsPanel == null) return;

        List<Button> found = new List<Button>();
        foreach (Button b in chickenOptionsPanel.GetComponentsInChildren<Button>(true))
            if (b.name.StartsWith("ColorButton")) found.Add(b);

        // ColorButton1 -> pollo 0, ColorButton2 -> pollo 1, ... (orden por el número del nombre)
        found.Sort((a, b) => ExtractNumber(a.name).CompareTo(ExtractNumber(b.name)));
        optionButtons = found.ToArray();
    }

    private static int ExtractNumber(string name)
    {
        string digits = "";
        foreach (char ch in name) if (char.IsDigit(ch)) digits += ch;
        return int.TryParse(digits, out int n) ? n : 0;
    }

    // =================================================================================
    //  Jugadores conectados
    // =================================================================================

    public void SetLocalPlayerSlot(int slot)
    {
        if (chickenPreviews == null || chickenPreviews.Length == 0) return;
        localPlayerSlot = Mathf.Clamp(slot, 0, chickenPreviews.Length - 1);
    }

    public void UpdateConnectedPlayers(int activePlayerCount)
    {
        int[] slots = new int[Mathf.Max(0, activePlayerCount)];
        for (int i = 0; i < slots.Length; i++) slots[i] = i;
        UpdateConnectedPlayers(slots);
    }

    public void UpdateConnectedPlayers(int[] occupiedSlots)
    {
        if (chickenPreviews == null || occupiedSlots == null) return;

        for (int i = 0; i < chickenPreviews.Length; i++)
        {
            bool isSlotOccupied = System.Array.IndexOf(occupiedSlots, i) >= 0;
            bool isMySlot = (i == localPlayerSlot) && isSlotOccupied;

            if (chickenPreviews[i] != null)
                chickenPreviews[i].gameObject.SetActive(isSlotOccupied);

            if (editButtons != null && i < editButtons.Length && editButtons[i] != null)
                editButtons[i].gameObject.SetActive(isMySlot);

            if (confirmButtons != null && i < confirmButtons.Length && confirmButtons[i] != null)
                confirmButtons[i].gameObject.SetActive(isMySlot);
        }
    }

    // =================================================================================
    //  Paleta
    // =================================================================================

    // LÁPIZ (OnClick de BtnEditarN con el número de nido 0..3)
    public void OpenOptionsForSlot(int slotIndex)
    {
        if (slotIndex != localPlayerSlot || chickenOptionsPanel == null) return;

        // Si ya está abierta, el lápiz la cierra (como un interruptor)
        if (PanelOpen)
        {
            CancelSelection();
            return;
        }

        tempChicken = confirmedChicken;

        chickenOptionsPanel.SetActive(true);
        chickenOptionsPanel.transform.SetAsLastSibling(); // siempre por encima
        PlacePanelBesideNest(slotIndex);
        HighlightOption(tempChicken);
    }

    // Pone la paleta AL LADO del nido (a la derecha en los nidos 1 y 2, a la izquierda en el 3 y 4),
    // centrada en altura con el nido y sin salirse de la pantalla.
    private void PlacePanelBesideNest(int slotIndex)
    {
        if (chickenPreviews == null || slotIndex >= chickenPreviews.Length || chickenPreviews[slotIndex] == null) return;

        RectTransform panel = chickenOptionsPanel.GetComponent<RectTransform>();
        RectTransform area = panel != null ? panel.parent as RectTransform : null;
        if (panel == null || area == null) return;

        RectTransform nest = chickenPreviews[slotIndex].transform.parent as RectTransform;
        if (nest == null) nest = chickenPreviews[slotIndex].rectTransform;

        // Esquinas del nido en el espacio del Canvas
        Vector3[] corners = new Vector3[4];
        nest.GetWorldCorners(corners);
        Vector2 nestMin = area.InverseTransformPoint(corners[0]);
        Vector2 nestMax = area.InverseTransformPoint(corners[2]);

        float w = panel.rect.width * panel.localScale.x;
        float h = panel.rect.height * panel.localScale.y;
        Rect bounds = area.rect;

        float nestCenterX = (nestMin.x + nestMax.x) * 0.5f;
        bool toTheRight = nestCenterX < bounds.center.x;

        // Posición del borde izquierdo de la paleta
        float left = toTheRight ? nestMax.x + panelGap : nestMin.x - panelGap - w;

        // Si no cabe por ese lado, se prueba el otro
        if (left + w > bounds.xMax) left = nestMin.x - panelGap - w;
        if (left < bounds.xMin) left = nestMax.x + panelGap;
        left = Mathf.Clamp(left, bounds.xMin, bounds.xMax - w);

        float bottom = (nestMin.y + nestMax.y) * 0.5f - h * 0.5f;
        bottom = Mathf.Clamp(bottom, bounds.yMin, bounds.yMax - h);

        // De esquina inferior izquierda a la posición del pivote
        Vector3 pos = new Vector3(left + w * panel.pivot.x, bottom + h * panel.pivot.y, panel.localPosition.z);
        panel.localPosition = pos;
    }

    // POLLO DE LA PALETA (OnClick de ColorButtonN con el índice 0..8): solo vista previa
    public void SelectChicken(int chickenIndex)
    {
        if (chickenSprites == null || chickenIndex < 0 || chickenIndex >= chickenSprites.Length)
            return;

        tempChicken = chickenIndex;
        HighlightOption(tempChicken);
        ShowInMyNest(tempChicken);
    }

    // CHULO ✔ (OnClick de BtnGuardarN): guarda y avisa al servidor
    public void ConfirmSelection()
    {
        // Si la paleta está cerrada, se guarda el que ya se ve en el nido
        if (!PanelOpen) tempChicken = confirmedChicken;

        confirmedChicken = tempChicken;
        ShowInMyNest(confirmedChicken);

        if (chickenOptionsPanel != null)
            chickenOptionsPanel.SetActive(false);

        // También se guarda localmente, por si el servidor tarda en responder
        SaveToSession(localPlayerSlot, confirmedChicken);

        LobbyNetworkManager.Instance?.SendChickenChoice(localPlayerSlot, confirmedChicken);
        ShowSavedMessage();
    }

    // X: descarta lo que se estaba probando
    public void CancelSelection()
    {
        tempChicken = confirmedChicken;
        ShowInMyNest(confirmedChicken);

        if (chickenOptionsPanel != null)
            chickenOptionsPanel.SetActive(false);
    }

    // Resalta en la paleta el pollo que se está probando (más grande y con borde dorado)
    private void HighlightOption(int index)
    {
        if (optionButtons == null) return;
        for (int i = 0; i < optionButtons.Length; i++)
        {
            if (optionButtons[i] == null) continue;
            UIButtonFeedback fb = optionButtons[i].GetComponent<UIButtonFeedback>();
            if (fb == null) fb = optionButtons[i].gameObject.AddComponent<UIButtonFeedback>();
            fb.SetSelected(i == index);
        }
    }

    private void ShowInMyNest(int chickenIndex)
    {
        if (chickenPreviews == null || localPlayerSlot >= chickenPreviews.Length || chickenPreviews[localPlayerSlot] == null) return;
        if (chickenSprites == null || chickenIndex < 0 || chickenIndex >= chickenSprites.Length) return;
        chickenPreviews[localPlayerSlot].sprite = chickenSprites[chickenIndex];
    }

    // =================================================================================
    //  Red
    // =================================================================================

    // Pollo que llega del servidor (de cualquier jugador, incluido yo)
    public void SetRemotePlayerChicken(int targetSlot, int chickenIndex)
    {
        SaveToSession(targetSlot, chickenIndex);

        if (targetSlot == localPlayerSlot)
        {
            confirmedChicken = chickenIndex;

            // Si estoy probando pollos en la paleta, no le borro lo que está mirando
            if (PanelOpen) return;
            tempChicken = chickenIndex;
        }

        if (chickenPreviews == null || targetSlot < 0 || targetSlot >= chickenPreviews.Length) return;

        if (chickenSprites != null && chickenIndex >= 0 && chickenIndex < chickenSprites.Length && chickenPreviews[targetSlot] != null)
            chickenPreviews[targetSlot].sprite = chickenSprites[chickenIndex];
    }

    // Copia de seguridad en GameSession (el valor definitivo llega del servidor en START_GAME)
    private static void SaveToSession(int slot, int chickenIndex)
    {
        if (GameSession.Slots == null || GameSession.Chickens == null || GameSession.Slots.Length != GameSession.Chickens.Length)
        {
            GameSession.Slots = new[] { 0, 1, 2, 3 };
            GameSession.Chickens = new[] { 0, 0, 0, 0 };
        }

        int i = GameSession.IndexOf(slot);
        if (i >= 0) GameSession.Chickens[i] = chickenIndex;
    }

    // =================================================================================
    //  Aviso "¡Guardado!" sobre el nido
    // =================================================================================

    private void ShowSavedMessage()
    {
        if (string.IsNullOrEmpty(savedMessage) || chickenPreviews == null || localPlayerSlot >= chickenPreviews.Length) return;
        Image preview = chickenPreviews[localPlayerSlot];
        if (preview == null) return;

        GameObject go = new GameObject("AvisoGuardado", typeof(RectTransform));
        go.transform.SetParent(preview.transform.parent != null ? preview.transform.parent : preview.transform, false);

        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchoredPosition = preview.rectTransform.anchoredPosition + new Vector2(0f, preview.rectTransform.rect.height * 0.5f + 30f);
        rt.sizeDelta = new Vector2(300f, 60f);

        TextMeshProUGUI t = go.AddComponent<TextMeshProUGUI>();
        t.text = savedMessage;
        t.fontSize = 40f;
        t.fontStyle = FontStyles.Bold;
        t.alignment = TextAlignmentOptions.Center;
        t.color = savedTextColor;
        t.outlineWidth = savedOutlineWidth;
        t.outlineColor = savedOutlineColor;
        t.raycastTarget = false;

        StartCoroutine(FadeAndDestroy(t, rt, 1.2f));
    }

    private IEnumerator FadeAndDestroy(TextMeshProUGUI t, RectTransform rt, float duration)
    {
        float time = 0f;
        Vector2 start = rt.anchoredPosition;
        Color c = t.color;
        while (time < duration && t != null)
        {
            time += Time.unscaledDeltaTime;
            float k = time / duration;
            rt.anchoredPosition = start + new Vector2(0f, 40f * k);
            t.color = new Color(c.r, c.g, c.b, 1f - k * k);
            yield return null;
        }
        if (t != null) Destroy(t.gameObject);
    }
}
