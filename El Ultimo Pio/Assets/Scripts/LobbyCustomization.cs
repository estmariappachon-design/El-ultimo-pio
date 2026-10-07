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

    private int localPlayerSlot = 0;   // Slot de este jugador local (0 = Nido 1, 1 = Nido 2, etc.)
    private int confirmedChicken = 0;  // Pollo guardado del jugador local
    private int tempChicken = 0;       // Selección temporal mientras la paleta esté abierta

    private void Awake()
    {
        if (chickenOptionsPanel != null)
            chickenOptionsPanel.SetActive(false);
    }

    private void Start()
    {
        // Al iniciar, probamos simular que solo hay 1 jugador (nosotros en el Slot 0)
        UpdateConnectedPlayers(1);
    }

    // --- MANEJO DE NIDOS OCUPADOS/VACÍOS ---
    public void UpdateConnectedPlayers(int activePlayerCount)
    {
        for (int i = 0; i < chickenPreviews.Length; i++)
        {
            bool isSlotOccupied = i < activePlayerCount;
            bool isMySlot = (i == localPlayerSlot) && isSlotOccupied;

            // 1. Mostrar u ocultar la imagen del pollo
            if (chickenPreviews[i] != null)
            {
                chickenPreviews[i].gameObject.SetActive(isSlotOccupied);
            }

            // 2. El Lápiz SOLO se activa para TU propio nido
            if (editButtons != null && i < editButtons.Length && editButtons[i] != null)
            {
                editButtons[i].gameObject.SetActive(isMySlot);
            }

            // 3. El Chulo (✔) SOLO se activa para TU propio nido
            if (confirmButtons != null && i < confirmButtons.Length && confirmButtons[i] != null)
            {
                confirmButtons[i].gameObject.SetActive(isMySlot);
            }
        }
    }

    public void SetLocalPlayerSlot(int slot)
    {
        localPlayerSlot = Mathf.Clamp(slot, 0, chickenPreviews.Length - 1);
        Debug.Log("Mi slot asignado es: " + localPlayerSlot);
    }

    // --- ABRIR PALETA (LÁPIZ DE TU NIDO) ---
    public void OpenOptionsForSlot(int slotIndex)
    {
        // Doble validación: solo puedes abrir la paleta de tu propio nido
        if (slotIndex == localPlayerSlot)
        {
            tempChicken = confirmedChicken;
            chickenOptionsPanel.SetActive(true);
        }
    }

    // --- SELECCIONAR HUEVO DE LA PALETA ---
    public void SelectChicken(int chickenIndex)
    {
        if (chickenIndex < 0 || chickenIndex >= chickenSprites.Length)
            return;

        tempChicken = chickenIndex;
        UpdatePreviewSprite(tempChicken);
    }

    public void ConfirmSelection()
    {
        confirmedChicken = tempChicken;
        UpdatePreviewSprite(confirmedChicken);

        if (chickenOptionsPanel != null)
            chickenOptionsPanel.SetActive(false);

        Debug.Log("Jugador " + localPlayerSlot + " guardó el pollo " + (confirmedChicken + 1));

        // Notificamos a la red que cambiamos nuestro pollo
        // Ej: networkManager.SendChickenSelection(localPlayerSlot, confirmedChicken);
    }

    // --- CANCELAR SELECCIÓN (X) ---
    public void CancelSelection()
    {
        UpdatePreviewSprite(confirmedChicken);

        if (chickenOptionsPanel != null)
            chickenOptionsPanel.SetActive(false);
    }

    private void UpdatePreviewSprite(int spriteIndex)
    {
        if (chickenPreviews == null || localPlayerSlot >= chickenPreviews.Length)
            return;

        if (spriteIndex >= 0 && spriteIndex < chickenSprites.Length)
        {
            chickenPreviews[localPlayerSlot].sprite = chickenSprites[spriteIndex];
        }
    }
    // Cambia la imagen del nido de cualquier jugador cuando el servidor envía la actualización
    public void SetRemotePlayerChicken(int targetSlot, int chickenIndex)
    {
        if (chickenPreviews == null || targetSlot < 0 || targetSlot >= chickenPreviews.Length)
            return;

        if (chickenIndex >= 0 && chickenIndex < chickenSprites.Length)
        {
            chickenPreviews[targetSlot].sprite = chickenSprites[chickenIndex];
        }
    }
}