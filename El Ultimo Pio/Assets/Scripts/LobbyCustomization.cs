using UnityEngine;
using UnityEngine.UI;

public class LobbyCustomization : MonoBehaviour
{
    [Header("UI General")]
    [SerializeField] private GameObject chickenOptionsPanel; // Objeto 'OpcionesColores'

    [Header("Pollos disponibles (MISMO ORDEN que en GameManager)")]
    [SerializeField] private Sprite[] chickenSprites;

    [Header("Elementos de los 4 nidos")]
    [SerializeField] private Image[] chickenPreviews;  // Las 4 imágenes 'ChickenPreview'
    [SerializeField] private Button[] editButtons;     // Los 4 botones de Lápiz
    [SerializeField] private Button[] confirmButtons;  // Los 4 botones de Chulo (✔)

    private int localPlayerSlot = 0;
    private int confirmedChicken = 0;
    private int tempChicken = 0;

    private void Awake()
    {
        if (chickenOptionsPanel != null)
            chickenOptionsPanel.SetActive(false);
    }

    private void Start()
    {
        UpdateConnectedPlayers(new[] { GameSession.MySlot });
    }

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

    // --- ABRIR PALETA DE OPCIONES ---
    public void OpenOptionsForSlot(int slotIndex)
    {
        if (slotIndex != localPlayerSlot || chickenOptionsPanel == null) return;

        tempChicken = confirmedChicken;

        if (chickenPreviews != null && slotIndex < chickenPreviews.Length && chickenPreviews[slotIndex] != null)
        {
            Vector3 nestPos = chickenPreviews[slotIndex].transform.position;
            chickenOptionsPanel.transform.position = new Vector3(nestPos.x, chickenOptionsPanel.transform.position.y, nestPos.z);
        }

        chickenOptionsPanel.SetActive(true);
    }

    // --- SELECCIONAR POLLO DE LA PALETA ---
    public void SelectChicken(int chickenIndex)
    {
        if (chickenSprites == null || chickenIndex < 0 || chickenIndex >= chickenSprites.Length)
            return;

        tempChicken = chickenIndex;

        if (chickenPreviews != null && localPlayerSlot < chickenPreviews.Length && chickenPreviews[localPlayerSlot] != null)
            chickenPreviews[localPlayerSlot].sprite = chickenSprites[tempChicken];
    }

    // --- CONFIRMAR (✔) ---
    public void ConfirmSelection()
    {
        confirmedChicken = tempChicken;

        if (chickenPreviews != null && localPlayerSlot < chickenPreviews.Length && chickenPreviews[localPlayerSlot] != null
            && chickenSprites != null && confirmedChicken < chickenSprites.Length)
            chickenPreviews[localPlayerSlot].sprite = chickenSprites[confirmedChicken];

        if (chickenOptionsPanel != null)
            chickenOptionsPanel.SetActive(false);

        // El servidor guarda la elección y se la reenvía a todos (también al que vuelve de una partida)
        LobbyNetworkManager.Instance?.SendChickenChoice(localPlayerSlot, confirmedChicken);
    }

    // --- CANCELAR (X) ---
    public void CancelSelection()
    {
        if (chickenPreviews != null && localPlayerSlot < chickenPreviews.Length && chickenPreviews[localPlayerSlot] != null
            && chickenSprites != null && confirmedChicken >= 0 && confirmedChicken < chickenSprites.Length)
            chickenPreviews[localPlayerSlot].sprite = chickenSprites[confirmedChicken];

        if (chickenOptionsPanel != null)
            chickenOptionsPanel.SetActive(false);
    }

    // --- POLLO QUE LLEGA DESDE LA RED (de cualquier jugador, incluido yo) ---
    public void SetRemotePlayerChicken(int targetSlot, int chickenIndex)
    {
        if (targetSlot == localPlayerSlot)
        {
            confirmedChicken = chickenIndex;
            tempChicken = chickenIndex;
        }

        if (chickenPreviews == null || targetSlot < 0 || targetSlot >= chickenPreviews.Length) return;

        if (chickenSprites != null && chickenIndex >= 0 && chickenIndex < chickenSprites.Length && chickenPreviews[targetSlot] != null)
            chickenPreviews[targetSlot].sprite = chickenSprites[chickenIndex];
    }
}
