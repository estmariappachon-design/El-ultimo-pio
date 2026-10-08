using UnityEngine;
using TMPro;

public class LobbyNetworkManager : MonoBehaviour
{
    public static LobbyNetworkManager Instance;

    [SerializeField] private LobbyCustomization customizationController;
    [SerializeField] private LobbyStartController startController;

    [Header("UI Info Red")]
    [SerializeField] private TextMeshProUGUI roomIPText; // Arrastra un texto TMP en el Lobby

    [Header("Conexión (opcional)")]
    [SerializeField] private string serverIP = "";

    public int MySlot => GameSession.MySlot;

    private void Awake()
    {
        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void Start()
    {
        if (SocketClient.Instance == null)
            new GameObject("SocketClient").AddComponent<SocketClient>();

        if (!string.IsNullOrWhiteSpace(serverIP))
            SocketClient.Instance.serverIP = serverIP.Trim();

        // Mostrar la IP en pantalla para que los demás la copien
        if (roomIPText != null)
        {
            string myIP = IPManager.GetLocalIPAddress();
            roomIPText.text = "IP SALA: " + myIP;
        }

        if (!SocketClient.Instance.IsConnected)
            SocketClient.Instance.ConnectToServer();

        SocketClient.Instance.SendMessageToServer("LOBBY_REFRESH", new object());
    }

    // occupiedSlots: slots realmente ocupados (puede venir null con servidores viejos)
    public void OnPlayerListUpdated(int totalPlayers, bool isHost, int mySlot, int[] occupiedSlots = null)
    {
        GameSession.MySlot = mySlot; // Guardamos la asignación

        if (customizationController != null)
        {
            customizationController.SetLocalPlayerSlot(mySlot);

            if (occupiedSlots != null && occupiedSlots.Length > 0)
                customizationController.UpdateConnectedPlayers(occupiedSlots);
            else
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

    // Conéctalo al botón Home (además de SceneLoader.LoadMainMenu) para liberar tu lugar en el servidor
    public void LeaveLobby()
    {
        // 1. Desconecta el socket para liberar el slot en el servidor
        SocketClient.Instance?.Disconnect();

        // 2. Carga la escena del menú principal
        UnityEngine.SceneManagement.SceneManager.LoadScene("MainMenu");
    }
}