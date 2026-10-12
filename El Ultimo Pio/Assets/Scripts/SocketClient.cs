using System;
using System.Net.Sockets;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;

// Cliente TCP de cada celular. Vive entre escenas (DontDestroyOnLoad).
// Lee los mensajes en un hilo secundario y los procesa en el hilo principal
// mediante LobbyThreadDispatcher (Unity solo permite tocar objetos desde el hilo principal).
public class SocketClient : MonoBehaviour
{
    public static SocketClient Instance;

    [Header("Conexión")]
    public string serverIP = "127.0.0.1";
    public int serverPort = GameServer.DefaultPort;

    [Header("Escenas")]
    [SerializeField] private string gameSceneName = "Gameplay";
    [SerializeField] private string mainMenuSceneName = "MainMenu";

    private TcpClient client;
    private NetworkStream stream;
    private readonly byte[] buffer = new byte[8192];
    private readonly StringBuilder incoming = new StringBuilder();
    private readonly object sendLock = new object();
    private volatile bool connected = false;
    private volatile bool leavingOnPurpose = false;

    public bool IsConnected => connected;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        // El despachador debe existir siempre (antes solo estaba en la escena Lobby)
        if (FindAnyObjectByType<LobbyThreadDispatcher>() == null)
            new GameObject("ThreadDispatcher").AddComponent<LobbyThreadDispatcher>();
    }

    // Devuelve true si logró conectarse
    public bool ConnectToServer()
    {
        if (connected) return true;

        try
        {
            leavingOnPurpose = false;
            client = new TcpClient();
            client.NoDelay = true;

            // Máximo 3 segundos esperando, para que el juego no se quede congelado
            IAsyncResult result = client.BeginConnect(serverIP, serverPort, null, null);
            if (!result.AsyncWaitHandle.WaitOne(3000))
            {
                client.Close();
                throw new TimeoutException("el servidor no respondió en 3 s (" + serverIP + ":" + serverPort + ")");
            }
            client.EndConnect(result);

            stream = client.GetStream();
            lock (incoming) { incoming.Clear(); }
            connected = true;
            stream.BeginRead(buffer, 0, buffer.Length, OnDataReceived, null);
            Debug.Log("[Cliente] Conectado a " + serverIP + ":" + serverPort);
            return true;
        }
        catch (Exception e)
        {
            connected = false;
            Debug.LogWarning("[Cliente] Error al conectar: " + e.Message);
            return false;
        }
    }

    public void SendMessageToServer(string type, object payloadObj)
    {
        if (!connected || stream == null) return;

        try
        {
            NetworkMessage msg = new NetworkMessage
            {
                type = type,
                payload = JsonUtility.ToJson(payloadObj ?? new EmptyPayload())
            };

            byte[] data = Encoding.UTF8.GetBytes(JsonUtility.ToJson(msg) + "\n");
            lock (sendLock) { stream.Write(data, 0, data.Length); }
        }
        catch (Exception e)
        {
            Debug.LogWarning("[Cliente] Error enviando: " + e.Message);
            HandleDisconnect();
        }
    }

    // Hilo secundario: solo separa las líneas
    private void OnDataReceived(IAsyncResult ar)
    {
        try
        {
            int bytesRead = stream.EndRead(ar);
            if (bytesRead <= 0)
            {
                HandleDisconnect();
                return;
            }

            lock (incoming)
            {
                incoming.Append(Encoding.UTF8.GetString(buffer, 0, bytesRead));

                string data = incoming.ToString();
                int start = 0;
                int idx;
                while ((idx = data.IndexOf('\n', start)) >= 0)
                {
                    string line = data.Substring(start, idx - start).Trim();
                    start = idx + 1;
                    if (line.Length > 0) LobbyThreadDispatcher.Enqueue(() => ProcessLine(line));
                }
                incoming.Remove(0, start);
            }

            stream.BeginRead(buffer, 0, buffer.Length, OnDataReceived, null);
        }
        catch (Exception e)
        {
            if (connected && !leavingOnPurpose) Debug.LogWarning("[Cliente] Error recibiendo: " + e.Message);
            HandleDisconnect();
        }
    }

    // Salida voluntaria (botón Home o Menú): NO muestra el aviso de "conexión perdida"
    public void Disconnect()
    {
        leavingOnPurpose = true;
        HandleDisconnect();
    }

    private void HandleDisconnect()
    {
        if (!connected) return;
        connected = false;
        try { client?.Close(); } catch (Exception) { }

        if (!leavingOnPurpose)
            LobbyThreadDispatcher.Enqueue(OnConnectionLost);
    }

    // Hilo principal: si se cae el anfitrión, todos vuelven al menú con un mensaje
    private void OnConnectionLost()
    {
        Debug.LogWarning("[Cliente] Se perdió la conexión con el servidor.");
        if (GameServer.IsRunning) GameServer.StopServer();

        if (SceneManager.GetActiveScene().name != mainMenuSceneName)
        {
            GameSession.LastError = "Se perdió la conexión con el anfitrión.";
            SceneManager.LoadScene(mainMenuSceneName);
        }
    }

    // =================================================================================
    //  Procesamiento de mensajes (hilo principal)
    // =================================================================================

    private void ProcessLine(string line)
    {
        NetworkMessage msg;
        try { msg = JsonUtility.FromJson<NetworkMessage>(line); }
        catch (Exception) { return; }

        if (msg == null || string.IsNullOrEmpty(msg.type)) return;

        try { ProcessMessage(msg); }
        catch (Exception e) { Debug.LogError("[Cliente] Error procesando " + msg.type + ": " + e); }
    }

    private void ProcessMessage(NetworkMessage msg)
    {
        GameManager gm = GameManager.Instance;

        switch (msg.type)
        {
            case "JOIN_UPDATE":
                {
                    JoinPayload d = JsonUtility.FromJson<JoinPayload>(msg.payload);
                    GameSession.MySlot = d.playerSlot;
                    GameSession.IsHost = d.isHost;
                    GameSession.MinPlayers = d.minPlayers > 0 ? d.minPlayers : 2;
                    LobbyNetworkManager.Instance?.OnPlayerListUpdated(d.totalPlayers, d.isHost, d.playerSlot, d.slots);
                    break;
                }

            case "SET_CHICKEN":
                {
                    ChickenPayload d = JsonUtility.FromJson<ChickenPayload>(msg.payload);
                    LobbyNetworkManager.Instance?.OnPlayerChangedChicken(d.playerSlot, d.chickenIndex);
                    break;
                }

            case "START_GAME":
                {
                    StartGamePayload d = JsonUtility.FromJson<StartGamePayload>(msg.payload);
                    GameSession.Slots = d.slots;
                    GameSession.Chickens = d.chickens;
                    GameSession.SpawnX = d.spawnX;
                    GameSession.SpawnY = d.spawnY;
                    GameSession.ItemX = d.itemX;
                    GameSession.ItemY = d.itemY;
                    GameSession.ItemT = d.itemT;
                    GameSession.Duration = d.duration > 0 ? d.duration : 120f;
                    GameSession.HasMatch = true;
                    GameSession.BeginPending = false;
                    SceneManager.LoadScene(gameSceneName);
                    break;
                }

            case "GAME_BEGIN":
                {
                    GameBeginPayload d = JsonUtility.FromJson<GameBeginPayload>(msg.payload);
                    if (gm != null) gm.OnGameBegin(d.duration);
                    else
                    {
                        GameSession.BeginPending = true;
                        GameSession.BeginDuration = d.duration;
                    }
                    break;
                }

            case "MOVE":
                {
                    MovePayload d = JsonUtility.FromJson<MovePayload>(msg.payload);
                    gm?.OnRemotePlayerMoved(d.playerSlot, new Vector2(d.posX, d.posY));
                    break;
                }

            case "ITEM_PICKED_UP":
                {
                    ItemPickedPayload d = JsonUtility.FromJson<ItemPickedPayload>(msg.payload);
                    gm?.OnItemPickedUp(d.itemID, d.playerSlot, (ItemType)d.itemType);
                    break;
                }

            case "ITEM_SPAWNED":
                {
                    ItemSpawnedPayload d = JsonUtility.FromJson<ItemSpawnedPayload>(msg.payload);
                    gm?.OnItemSpawned(d.itemID, d.posX, d.posY, (ItemType)d.itemType);
                    break;
                }

            case "SCORE_UPDATE":
                {
                    ScoreUpdatePayload d = JsonUtility.FromJson<ScoreUpdatePayload>(msg.payload);
                    gm?.OnScoreUpdate(d.slots, d.scores, d.items);
                    break;
                }

            case "CHARGES":
                {
                    ChargesPayload d = JsonUtility.FromJson<ChargesPayload>(msg.payload);
                    gm?.OnChargesUpdate(d.speedCharges, d.freezeCharges);
                    break;
                }

            case "POWERUP_USED":
                {
                    PowerUpUsedPayload d = JsonUtility.FromJson<PowerUpUsedPayload>(msg.payload);
                    gm?.OnPowerUpUsed(d.senderSlot, d.targetSlot, d.powerUpType, d.duration);
                    break;
                }

            case "PLAYER_LEFT":
                {
                    PlayerLeftPayload d = JsonUtility.FromJson<PlayerLeftPayload>(msg.payload);
                    gm?.OnPlayerLeft(d.playerSlot);
                    break;
                }

            case "GAME_OVER":
                gm?.OnGameOver();
                break;

            case "RESULTS":
                {
                    ResultsPayload d = JsonUtility.FromJson<ResultsPayload>(msg.payload);
                    gm?.OnResults(d);
                    break;
                }
        }
    }

    private void OnApplicationQuit()
    {
        leavingOnPurpose = true;
        connected = false;
        try { client?.Close(); } catch (Exception) { }
        if (GameServer.IsRunning) GameServer.StopServer();
    }
}
