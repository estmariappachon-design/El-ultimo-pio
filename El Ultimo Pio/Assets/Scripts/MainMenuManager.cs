using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;

public class MainMenuManager : MonoBehaviour
{
    [Header("Escenas")]
    [SerializeField] private string lobbySceneName = "Lobby";

    [Header("UI Unirse a Partida")]
    [SerializeField] private GameObject joinPanel;         // PanelUnirse
    [SerializeField] private TMP_InputField ipInputField;  // InputFieldIP
    [SerializeField] private string defaultIP = "192.168.";

    [Header("Mensajes (opcional)")]
    [SerializeField] private TextMeshProUGUI messageText;  // texto para errores ("No se pudo conectar...")

    [Header("SOLO PARA PRUEBAS")]
    [Tooltip("Permite que el host empiece la partida estando solo. DESACTIVAR antes de grabar el video.")]
    [SerializeField] private bool allowSoloForTesting = false;

    [Header("Maíces especiales (los aplica el anfitrión al crear la sala)")]
    [Tooltip("Cuántos maíces de VELOCIDAD hay en el mapa (de los 50)")]
    [SerializeField, Range(0, 15)] private int speedCornCount = 3;
    [Tooltip("Cuántos maíces de HIELO hay en el mapa (de los 50)")]
    [SerializeField, Range(0, 15)] private int freezeCornCount = 3;
    [Tooltip("Segundos que tarda un maíz especial en reaparecer después de que lo cogen (0 = al instante)")]
    [SerializeField, Range(0f, 60f)] private float specialRespawnDelay = 15f;

    private const string LastIPKey = "ultima_ip";

    private void Start()
    {
        // Los logs del servidor aparecen en la consola de Unity / Logcat
        GameServer.Log = msg => Debug.Log(msg);

        // En el celular: la pantalla no se apaga y el juego sigue aunque pierda el foco un momento
        Screen.sleepTimeout = SleepTimeout.NeverSleep;
        Application.runInBackground = true;

        if (joinPanel != null) joinPanel.SetActive(false);

        if (ipInputField != null)
        {
            ipInputField.contentType = TMP_InputField.ContentType.Standard;
            ipInputField.keyboardType = TouchScreenKeyboardType.NumbersAndPunctuation;
            ipInputField.text = PlayerPrefs.GetString(LastIPKey, defaultIP);
        }

        // Si venimos de una desconexión, mostramos el motivo
        if (!string.IsNullOrEmpty(GameSession.LastError))
        {
            ShowMessage(GameSession.LastError);
            GameSession.LastError = null;
        }
        else
        {
            ShowMessage("");
        }
    }

    // --- BOTÓN: CREAR PARTIDA (este celular será el ANFITRIÓN / HOST) ---
    public void OnClickCrearPartida()
    {
        // Por si quedó algo abierto de una partida anterior
        SocketClient.Instance?.Disconnect();
        GameServer.StopServer();

        GameServer.MinPlayers = allowSoloForTesting ? 1 : 2;
        MatchGenerator.SpeedItems = speedCornCount;
        MatchGenerator.FreezeItems = freezeCornCount;
        GameServer.SpecialRespawnDelay = specialRespawnDelay;

        if (!GameServer.StartServer(GameServer.DefaultPort))
        {
            ShowMessage("No se pudo crear la sala (¿el puerto " + GameServer.DefaultPort + " está ocupado?)");
            return;
        }

        // El host se conecta a su propio servidor por la IP local del aparato
        ConnectAndLoadLobby("127.0.0.1");
    }

    // --- BOTÓN: UNIRSE A PARTIDA ---
    public void OnClickUnirseAPartida()
    {
        if (joinPanel != null) joinPanel.SetActive(true);
        else ShowMessage("Falta asignar el PanelUnirse en el Inspector.");
    }

    // --- BOTÓN "Conectar" dentro del panel ---
    public void OnConfirmJoinIP()
    {
        string ip = ipInputField != null ? ipInputField.text.Trim() : "";
        if (string.IsNullOrEmpty(ip))
        {
            ShowMessage("Escribe la IP que aparece en el celular del anfitrión.");
            return;
        }

        PlayerPrefs.SetString(LastIPKey, ip);
        PlayerPrefs.Save();

        // Un cliente nunca debe tener un servidor propio corriendo
        GameServer.StopServer();
        ConnectAndLoadLobby(ip);
    }

    public void OnCancelJoin()
    {
        if (joinPanel != null) joinPanel.SetActive(false);
    }

    private void ConnectAndLoadLobby(string ip)
    {
        if (SocketClient.Instance == null)
            new GameObject("SocketClient").AddComponent<SocketClient>();

        SocketClient.Instance.Disconnect();
        SocketClient.Instance.serverIP = ip;

        ShowMessage("Conectando a " + ip + "...");

        if (!SocketClient.Instance.ConnectToServer())
        {
            ShowMessage("No se pudo conectar a " + ip + ". Revisa que estén en la misma red Wi-Fi.");
            if (GameServer.IsRunning) GameServer.StopServer();
            return;
        }

        SceneManager.LoadScene(lobbySceneName);
    }

    // --- BOTÓN: SALIR ---
    public void OnClickSalir()
    {
        SocketClient.Instance?.Disconnect();
        GameServer.StopServer();
        Application.Quit();
        Debug.Log("Saliendo del juego...");
    }

    private void ShowMessage(string text)
    {
        if (messageText != null) messageText.text = text;
        if (!string.IsNullOrEmpty(text)) Debug.Log("[Menú] " + text);
    }
}
