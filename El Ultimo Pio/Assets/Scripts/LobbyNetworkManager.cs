using UnityEngine;

public class LobbyNetworkManager : MonoBehaviour
{
    public static LobbyNetworkManager Instance;

    [SerializeField] private LobbyCustomization customizationController;
    [SerializeField] private LobbyStartController startController;

    private int myPlayerSlot = 0; // Guardará nuestro slot local

    // --- PROPIEDAD PÚBLICA PARA CONSULTAR DESDE GAMEMANAGER ---
    public int MySlot => myPlayerSlot;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject); // Para mantener el estado al cambiar a la escena de juego
        }
        else
        {
            Destroy(gameObject);
        }
    }

    public void OnPlayerListUpdated(int totalPlayers, bool isHost, int mySlot)
    {
        myPlayerSlot = mySlot; // Guardamos la asignación

        if (customizationController != null)
        {
            customizationController.SetLocalPlayerSlot(mySlot);
            customizationController.UpdateConnectedPlayers(totalPlayers);
        }

        if (startController != null)
        {
            startController.SetHost(isHost);
            startController.SetPlayerCount(totalPlayers);
        }
    }

    public void OnPlayerChangedChicken(int slot, int chickenIndex)
    {
        if (customizationController != null)
            customizationController.SetRemotePlayerChicken(slot, chickenIndex);
    }

    public void SendChickenChoice(int slot, int chickenIndex)
    {
        ChickenPayload payload = new ChickenPayload { playerSlot = slot, chickenIndex = chickenIndex };
        SocketClient.Instance?.SendMessageToServer("SET_CHICKEN", payload);
    }

    public void OnStartGamePressed()
    {
        SocketClient.Instance?.SendMessageToServer("START_GAME", new object());
    }
}