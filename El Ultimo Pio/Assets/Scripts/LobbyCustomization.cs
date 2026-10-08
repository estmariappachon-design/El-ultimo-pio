using UnityEngine;
using UnityEngine.UI;

public class LobbyCustomization : MonoBehaviour
{
    [Header("UI General")]
    [SerializeField] private GameObject chickenOptionsPanel; // Objeto 'OpcionesColores'

    [Header("Pollos disponibles")]
    [SerializeField] private Sprite[] chickenSprites;

    [Header("Elementos de los 4 nidos")]
    [SerializeField] private Image[] chickenPreviews;  // Las 4 imágenes 'ChickenPreview'
    [SerializeField] private Button[] editButtons;     // Los 4 botones de Lápiz
    [SerializeField] private Button[] confirmButtons;  // Los 4 botones de Chulo (✔)

    private int localPlayerSlot = 0;   // Slot de este jugador
    private int confirmedChicken = 0;  // Pollo guardado
    private int tempChicken = 0;       // Selección temporal

    private void Awake()
    {
        if (chickenOptionsPanel != null)
            chickenOptionsPanel.SetActive(false);
    }

    private void Start()
    {
        UpdateConnectedPlayers(1);
    }

    public void SetLocalPlayerSlot(int slot)
    {
        localPlayerSlot = Mathf.Clamp(slot, 0, chickenPreviews.Length - 1);
        Debug.Log("Mi slot asignado en LobbyCustomization es: " + localPlayerSlot);
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
        if (slotIndex == localPlayerSlot)
        {
            tempChicken = confirmedChicken;

            // Reposiciona la ventana sobre el nido actual
            if (chickenPreviews != null && slotIndex < chickenPreviews.Length && chickenPreviews[slotIndex] != null)
            {
                Vector3 nestPos = chickenPreviews[slotIndex].transform.position;
                chickenOptionsPanel.transform.position = new Vector3(nestPos.x, chickenOptionsPanel.transform.position.y, nestPos.z);
            }

            chickenOptionsPanel.SetActive(true);
        }
    }

    // --- SELECCIONAR HUEVO/COLOR DE LA PALETA ---
    public void SelectChicken(int chickenIndex)
    {
        if (chickenSprites == null || chickenIndex < 0 || chickenIndex >= chickenSprites.Length)
            return;

        tempChicken = chickenIndex;

        // Muestra la vista previa del nido local
        if (chickenPreviews != null && localPlayerSlot < chickenPreviews.Length && chickenPreviews[localPlayerSlot] != null)
        {
            chickenPreviews[localPlayerSlot].sprite = chickenSprites[tempChicken];
        }
    }

    // --- CONFIRMAR SELECCIÓN (BOTÓN CHULO ✔) ---
    // En LobbyCustomization.cs
    public void ConfirmSelection()
    {
        confirmedChicken = tempChicken;

        // Guardar localmente
        SaveChickenToSession(localPlayerSlot, confirmedChicken);

        // Actualizar el sprite local
        if (chickenPreviews != null && localPlayerSlot < chickenPreviews.Length)
            chickenPreviews[localPlayerSlot].sprite = chickenSprites[confirmedChicken];

        if (chickenOptionsPanel != null)
            chickenOptionsPanel.SetActive(false);

        // AVISAR AL SERVIDO ENVIANDO EL SLOT ASIGNADO
        LobbyNetworkManager.Instance?.SendChickenChoice(localPlayerSlot, confirmedChicken);
    }

    // --- CANCELAR SELECCIÓN (BOTÓN X) ---
    public void CancelSelection()
    {
        if (chickenPreviews != null && localPlayerSlot < chickenPreviews.Length && chickenPreviews[localPlayerSlot] != null)
        {
            if (confirmedChicken >= 0 && confirmedChicken < chickenSprites.Length)
                chickenPreviews[localPlayerSlot].sprite = chickenSprites[confirmedChicken];
        }

        if (chickenOptionsPanel != null)
            chickenOptionsPanel.SetActive(false);
    }

    // --- RECIBIR EL POLLO DE OTRO JUGADOR DESDE LA RED ---
    public void SetRemotePlayerChicken(int targetSlot, int chickenIndex)
    {
        // Guardar la elección remota en la sesión
        SaveChickenToSession(targetSlot, chickenIndex);

        if (chickenPreviews == null || targetSlot < 0 || targetSlot >= chickenPreviews.Length)
            return;

        if (chickenSprites != null && chickenIndex >= 0 && chickenIndex < chickenSprites.Length)
        {
            if (chickenPreviews[targetSlot] != null)
                chickenPreviews[targetSlot].sprite = chickenSprites[chickenIndex];
        }
    }

    private void SaveChickenToSession(int slot, int chickenIndex)
    {
        if (GameSession.Slots == null || GameSession.Chickens == null || GameSession.Slots.Length == 0)
        {
            GameSession.Slots = new int[] { 0, 1, 2, 3 };
            GameSession.Chickens = new int[] { 0, 0, 0, 0 };
        }

        for (int i = 0; i < GameSession.Slots.Length; i++)
        {
            if (GameSession.Slots[i] == slot)
            {
                GameSession.Chickens[i] = chickenIndex;
                break;
            }
        }
    }
}