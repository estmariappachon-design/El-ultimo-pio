using System;
using System.Net.Sockets;
using System.Text;
using UnityEngine;

public class SocketClient : MonoBehaviour
{
    public static SocketClient Instance;

    private TcpClient client;
    private NetworkStream stream;
    private byte[] buffer = new byte[4096];

    [Header("Conexión")]
    public string serverIP = "127.0.0.1"; // Cambiar a la IP del servidor en red local
    public int serverPort = 8888;

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
        try
        {
            client = new TcpClient(serverIP, serverPort);
            stream = client.GetStream();
            stream.BeginRead(buffer, 0, buffer.Length, OnDataReceived, null);
            Debug.Log("Conectado al servidor de Sockets.");
        }
        catch (Exception e)
        {
            Debug.LogError("Error al conectar: " + e.Message);
        }
    }

    public void SendMessageToServer(string type, object payloadObj)
    {
        if (client == null || !client.Connected) return;

        NetworkMessage msg = new NetworkMessage
        {
            type = type,
            payload = JsonUtility.ToJson(payloadObj)
        };

        string jsonMsg = JsonUtility.ToJson(msg) + "\n";
        byte[] data = Encoding.UTF8.GetBytes(jsonMsg);
        stream.Write(data, 0, data.Length);
    }

    private void OnDataReceived(IAsyncResult ar)
    {
        try
        {
            int bytesRead = stream.EndRead(ar);
            if (bytesRead <= 0) return;

            string jsonStr = Encoding.UTF8.GetString(buffer, 0, bytesRead);
            string[] messages = jsonStr.Split('\n');

            foreach (var msgStr in messages)
            {
                if (string.IsNullOrEmpty(msgStr.Trim())) continue;

                NetworkMessage msg = JsonUtility.FromJson<NetworkMessage>(msgStr);
                // Procesar mensaje en el hilo principal de Unity
                LobbyThreadDispatcher.Enqueue(() => ProcessMessage(msg));
            }

            stream.BeginRead(buffer, 0, buffer.Length, OnDataReceived, null);
        }
        catch (Exception e)
        {
            Debug.LogError("Error recibiendo datos: " + e.Message);
        }
    }

    private void ProcessMessage(NetworkMessage msg)
    {
        switch (msg.type)
        {
            case "JOIN_UPDATE":
                JoinPayload joinData = JsonUtility.FromJson<JoinPayload>(msg.payload);
                LobbyNetworkManager.Instance.OnPlayerListUpdated(joinData.totalPlayers, joinData.isHost, joinData.playerSlot);
                break;

            case "SET_CHICKEN":
                ChickenPayload chickenData = JsonUtility.FromJson<ChickenPayload>(msg.payload);
                LobbyNetworkManager.Instance.OnPlayerChangedChicken(chickenData.playerSlot, chickenData.chickenIndex);
                break;

            case "START_GAME":
                // Cargar la escena de juego 100x100
                UnityEngine.SceneManagement.SceneManager.LoadScene("GameScene");
                break;
        }
    }

    private void OnApplicationQuit()
    {
        if (client != null) client.Close();
    }
}