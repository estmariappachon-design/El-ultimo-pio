using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;

public class LobbyNetworkManager : MonoBehaviour
{
    public static LobbyNetworkManager Instance;

    [SerializeField] private LobbyCustomization customizationController;
    [SerializeField] private LobbyStartController startController;

    [Header("UI Info Red")]
    [SerializeField] private TextMeshProUGUI roomIPText; // Text_IP_Sala

    [Header("Textos de la sala ({code} = código completo, {short} = final del código, {n} = jugadores)")]
    [TextArea] [SerializeField] private string hostRoomFormat = "CÓDIGO DE SALA: {code}\nEn la misma Wi-Fi basta: {short}\nJugadores: {n}/4";
    [TextArea] [SerializeField] private string clientRoomFormat = "Sala {code}\nJugadores: {n}/4";

    [Header("Escenas")]
    [SerializeField] private string mainMenuSceneName = "MainMenu";

    private int playerCount = 1;

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
        // Se llega al lobby SIEMPRE desde el menú (ya conectado) o volviendo de una partida
        if (SocketClient.Instance == null || !SocketClient.Instance.IsConnected)
        {
            GameSession.LastError = "No hay conexión con la sala. Vuelve a crearla o a unirte.";
            SceneManager.LoadScene(mainMenuSceneName);
            return;
        }

        RefreshRoomText();
        SocketClient.Instance.SendMessageToServer("LOBBY_REFRESH", new EmptyPayload());
    }

    private void RefreshRoomText()
    {
        if (roomIPText == null) return;

        // El anfitrión muestra el código de SU IP; los demás, el de la sala a la que se unieron
        string ip = GameServer.IsRunning ? IPManager.GetLocalIPAddress() : SocketClient.Instance.serverIP;
        string format = GameServer.IsRunning ? hostRoomFormat : clientRoomFormat;

        roomIPText.text = format
            .Replace("{code}", RoomCode.FromIP(ip))
            .Replace("{short}", RoomCode.ShortFromIP(ip))
            .Replace("{n}", playerCount.ToString());
    }

    // occupiedSlots: slots realmente ocupados
    public void OnPlayerListUpdated(int totalPlayers, bool isHost, int mySlot, int[] occupiedSlots)
    {
        GameSession.MySlot = mySlot;
        playerCount = totalPlayers;

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

        RefreshRoomText();
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

    // Conectado al OnClick de BtnEmpezar
    public void OnStartGamePressed()
    {
        if (startController != null && !startController.CanStart) return;
        SocketClient.Instance?.SendMessageToServer("START_GAME", new EmptyPayload());
    }

    // Conectado al botón Home
    public void LeaveLobby()
    {
        SocketClient.Instance?.Disconnect();
        if (GameServer.IsRunning) GameServer.StopServer(); // si era el anfitrión, cierra la sala
        SceneManager.LoadScene(mainMenuSceneName);
    }
}
