using System;
using System.Net.Sockets;
using System.Text;
using UnityEngine;

public class SocketClient : MonoBehaviour
{
    public static SocketClient Instance;

    private TcpClient client;
    private NetworkStream stream;
    private readonly byte[] buffer = new byte[4096];
    private readonly StringBuilder incoming = new StringBuilder(); // acumula texto hasta completar cada línea
    private readonly object sendLock = new object();
    private volatile bool connected = false;

    [Header("Conexión")]
    public string serverIP = "127.0.0.1"; // Cambiar a la IP de la PC donde corre el servidor
    public int serverPort = 8888;

    [Header("Escenas")]
    [SerializeField] private string gameSceneName = "Gameplay"; // Debe estar en Build Profiles > Scene List

    public bool IsConnected => connected;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    public void ConnectToServer()
    {
        if (connected) return;

        try
        {
            client = new TcpClient();
            client.NoDelay = true;

            // Conexión con límite de 3 segundos, para que el juego no se quede congelado si el servidor no está
            IAsyncResult connectResult = client.BeginConnect(serverIP, serverPort, null, null);
            if (!connectResult.AsyncWaitHandle.WaitOne(3000))
            {
                client.Close();
                throw new TimeoutException("El servidor no respondió en 3 segundos (" + serverIP + ":" + serverPort + ")");
            }
            client.EndConnect(connectResult);
            stream = client.GetStream();
            connected = true;
            incoming.Clear();
            stream.BeginRead(buffer, 0, buffer.Length, OnDataReceived, null);
            Debug.Log("Conectado al servidor de Sockets.");
        }
        catch (Exception e)
        {
            connected = false;
            Debug.LogError("Error al conectar: " + e.Message);
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
                payload = JsonUtility.ToJson(payloadObj)
            };

            byte[] data = Encoding.UTF8.GetBytes(JsonUtility.ToJson(msg) + "\n");
            lock (sendLock) { stream.Write(data, 0, data.Length); }
        }
        catch (Exception e)
        {
            Debug.LogError("Error enviando datos: " + e.Message);
            HandleDisconnect();
        }
    }

    // Corre en un hilo secundario: solo separa las líneas, el resto se procesa en el hilo principal
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

            incoming.Append(Encoding.UTF8.GetString(buffer, 0, bytesRead));

            string data = incoming.ToString();
            int start = 0;
            int idx;
            while ((idx = data.IndexOf('\n', start)) >= 0)
            {
                string line = data.Substring(start, idx - start).Trim();
                start = idx + 1;
                if (line.Length == 0) continue;

                LobbyThreadDispatcher.Enqueue(() => ProcessLine(line));
            }
            incoming.Remove(0, start); // conserva el pedazo de línea que aún no está completo

            stream.BeginRead(buffer, 0, buffer.Length, OnDataReceived, null);
        }
        catch (Exception e)
        {
            if (connected) Debug.LogError("Error recibiendo datos: " + e.Message);
            HandleDisconnect();
        }
    }

    // Para el botón Home del lobby: libera tu lugar en el servidor
    public void Disconnect()
    {
        HandleDisconnect();
    }

    private void HandleDisconnect()
    {
        if (!connected) return;
        connected = false;
        try { client.Close(); } catch (Exception) { }
        Debug.LogWarning("Desconectado del servidor.");
    }

    // ---------- Hilo principal de Unity ----------

    private void ProcessLine(string line)
    {
        NetworkMessage msg = JsonUtility.FromJson<NetworkMessage>(line);
        if (msg == null || string.IsNullOrEmpty(msg.type)) return;
        ProcessMessage(msg);
    }

    private void ProcessMessage(NetworkMessage msg)
    {
        switch (msg.type)
        {
            case "JOIN_UPDATE":
                {
                    JoinPayload joinData = JsonUtility.FromJson<JoinPayload>(msg.payload);
                    LobbyNetworkManager.Instance?.OnPlayerListUpdated(
                        joinData.totalPlayers, joinData.isHost, joinData.playerSlot, joinData.slots);
                    break;
                }

            case "SET_CHICKEN":
                {
                    ChickenPayload chickenData = JsonUtility.FromJson<ChickenPayload>(msg.payload);
                    LobbyNetworkManager.Instance?.OnPlayerChangedChicken(chickenData.playerSlot, chickenData.chickenIndex);
                    break;
                }

            case "START_GAME":
                {
                    if (!string.IsNullOrEmpty(msg.payload))
                    {
                        StartGamePayload start = JsonUtility.FromJson<StartGamePayload>(msg.payload);
                        GameSession.Seed = start.seed;
                        GameSession.Slots = start.slots;
                        GameSession.Chickens = start.chickens;
                        GameSession.HasSeed = true;
                        GameSession.BeginPending = false;
                    }
                    UnityEngine.SceneManagement.SceneManager.LoadScene(gameSceneName);
                    break;
                }

            case "GAME_BEGIN":
                {
                    GameBeginPayload begin = JsonUtility.FromJson<GameBeginPayload>(msg.payload);
                    if (GameManager.Instance != null)
                    {
                        GameManager.Instance.OnGameBegin(begin.duration);
                    }
                    else
                    {
                        // La escena todavía está cargando: GameManager lo recoge en su Start
                        GameSession.BeginPending = true;
                        GameSession.BeginDuration = begin.duration;
                    }
                    break;
                }

            case "ITEM_PICKED_UP":
                {
                    PickupPayload pick = JsonUtility.FromJson<PickupPayload>(msg.payload);
                    if (!Enum.TryParse(pick.itemType, out ItemType itemType)) itemType = ItemType.Normal;
                    GameManager.Instance?.OnItemPickedUp(pick.itemID, pick.playerSlot, itemType);
                    break;
                }

            case "USE_POWERUP":
                {
                    PowerUpPayload power = JsonUtility.FromJson<PowerUpPayload>(msg.payload);
                    GameManager.Instance?.OnPowerUpUsed(power.senderSlot, power.powerUpType);
                    break;
                }

            case "MOVE":
                {
                    MovePayload move = JsonUtility.FromJson<MovePayload>(msg.payload);
                    GameManager.Instance?.OnRemotePlayerMoved(move.playerSlot, new Vector2(move.posX, move.posY));
                    break;
                }

            case "PLAYER_LEFT":
                {
                    JoinPayload left = JsonUtility.FromJson<JoinPayload>(msg.payload);
                    GameManager.Instance?.OnPlayerLeft(left.playerSlot);
                    break;
                }

            case "GAME_OVER":
                {
                    GameManager.Instance?.OnGameOver();
                    break;
                }

            case "RESULTS":
                {
                    ResultsPayload results = JsonUtility.FromJson<ResultsPayload>(msg.payload);
                    GameManager.Instance?.OnResults(results.slots, results.scores);
                    break;
                }
        }
    }

    private void OnApplicationQuit()
    {
        connected = false;
        if (client != null) client.Close();
    }
}