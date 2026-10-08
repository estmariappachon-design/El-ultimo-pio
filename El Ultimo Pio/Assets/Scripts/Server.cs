using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;

// Servidor de la partida. Se ejecuta como aplicación de consola, aparte de Unity.
// Formato de cada mensaje (una línea): {"type":"X","payload":"<json escapado como texto>"}
//
// Flujo de una partida:
//   Lobby:  JOIN_UPDATE / SET_CHICKEN
//   Inicio: host envía START_GAME -> todos cargan la escena y responden GAME_READY
//           -> cuando todos están listos (o pasan 15 s) el servidor envía GAME_BEGIN
//   Juego:  PICKUP_ITEM / USE_POWERUP / MOVE
//   Final:  a los 120 s el servidor envía GAME_OVER -> cada cliente responde FINAL_SCORE
//           -> el servidor envía RESULTS con los puntajes de todos
public class GameServer
{
    private const int Port = 8888;
    private const int MaxPlayers = 4;
    private const int TotalItems = 50;
    private const int GameDurationSeconds = 120;
    private const int ReadyTimeoutMs = 15000;
    private const int ResultsTimeoutMs = 5000;

    private static TcpListener server;
    private static readonly List<ClientHandler> clients = new List<ClientHandler>();
    private static readonly HashSet<int> collectedItemIDs = new HashSet<int>();
    private static readonly HashSet<int> readySlots = new HashSet<int>();
    private static readonly Dictionary<int, int> finalScores = new Dictionary<int, int>();
    private static readonly HashSet<string> validItemTypes = new HashSet<string> { "Normal", "SpeedBoost", "Freeze" };
    private static readonly Random rng = new Random();
    private static readonly object stateLock = new object(); // protege todo el estado de arriba y de abajo

    private static bool gameStarted = false; // el host pulsó EMPEZAR
    private static bool gameBegan = false;   // ya corre el reloj de la partida
    private static bool gameOver = false;    // se acabó el tiempo
    private static bool resultsSent = false;

    private static Timer readyTimer;
    private static Timer gameTimer;
    private static Timer resultsTimer;

    public static void Main(string[] args)
    {
        server = new TcpListener(IPAddress.Any, Port);
        server.Start();
        Console.WriteLine("Servidor de Sockets iniciado en el puerto " + Port + "...");

        while (true)
        {
            TcpClient tcp = server.AcceptTcpClient();
            tcp.NoDelay = true; // menos latencia para el movimiento

            ClientHandler handler = null;
            lock (stateLock)
            {
                if (!gameStarted && clients.Count < MaxPlayers)
                {
                    handler = new ClientHandler(tcp, GetFreeSlot());
                    clients.Add(handler);
                }
            }

            if (handler == null)
            {
                Console.WriteLine("Conexión rechazada (partida llena o ya iniciada).");
                tcp.Close();
                continue;
            }

            Console.WriteLine("Jugador conectado: slot " + handler.Slot);
            new Thread(handler.Run) { IsBackground = true }.Start();
            BroadcastJoinUpdate();
        }
    }

    // Se llama dentro de stateLock
    private static int GetFreeSlot()
    {
        for (int s = 0; s < MaxPlayers; s++)
        {
            bool used = false;
            foreach (var c in clients)
            {
                if (c.Slot == s) { used = true; break; }
            }
            if (!used) return s;
        }
        return -1;
    }

    private static List<ClientHandler> Snapshot()
    {
        lock (stateLock) { return new List<ClientHandler>(clients); }
    }

    // Se llama dentro de stateLock
    private static void ResetGameState()
    {
        gameStarted = false;
        gameBegan = false;
        gameOver = false;
        collectedItemIDs.Clear();
        readySlots.Clear();
        finalScores.Clear();

        if (readyTimer != null) { readyTimer.Dispose(); readyTimer = null; }
        if (gameTimer != null) { gameTimer.Dispose(); gameTimer = null; }
        if (resultsTimer != null) { resultsTimer.Dispose(); resultsTimer = null; }
    }

    // ---------- Envío ----------

    public static string BuildMessage(string type, string innerJson)
    {
        string escaped = innerJson.Replace("\\", "\\\\").Replace("\"", "\\\"");
        return "{\"type\":\"" + type + "\",\"payload\":\"" + escaped + "\"}\n";
    }

    public static void Broadcast(string message, ClientHandler except = null)
    {
        if (!message.EndsWith("\n")) message += "\n";
        foreach (var c in Snapshot())
        {
            if (c != except) c.Send(message);
        }
    }

    // Le manda a UN jugador el estado actual del lobby (al entrar al lobby o volver de una partida)
    private static void SendLobbyState(ClientHandler target)
    {
        List<ClientHandler> list = Snapshot();
        if (!list.Contains(target)) return;

        List<string> slotList = new List<string>();
        foreach (var c in list) slotList.Add(c.Slot.ToString());

        bool isHost = (list[0] == target);
        string inner = "{\"playerSlot\":" + target.Slot +
                       ",\"isHost\":" + (isHost ? "true" : "false") +
                       ",\"totalPlayers\":" + list.Count +
                       ",\"slots\":[" + string.Join(",", slotList) + "]}";
        target.Send(BuildMessage("JOIN_UPDATE", inner));

        foreach (var other in list)
        {
            if (other == target) continue;
            target.Send(BuildMessage("SET_CHICKEN",
                "{\"playerSlot\":" + other.Slot + ",\"chickenIndex\":" + other.Chicken + "}"));
        }
    }

    // Avisa a todos quién está conectado y qué pollo tiene cada uno
    public static void BroadcastJoinUpdate()
    {
        List<ClientHandler> list = Snapshot();

        List<string> slotList = new List<string>();
        foreach (var c in list) slotList.Add(c.Slot.ToString());
        string slotsJson = "[" + string.Join(",", slotList) + "]";

        for (int i = 0; i < list.Count; i++)
        {
            bool isHost = (i == 0); // El primero en conectarse es el Host
            string inner = "{\"playerSlot\":" + list[i].Slot +
                           ",\"isHost\":" + (isHost ? "true" : "false") +
                           ",\"totalPlayers\":" + list.Count +
                           ",\"slots\":" + slotsJson + "}";
            list[i].Send(BuildMessage("JOIN_UPDATE", inner));
        }

        // Cada jugador recibe el pollo que ya eligieron los demás (así el que entra tarde los ve bien)
        foreach (var target in list)
        {
            foreach (var other in list)
            {
                if (other == target) continue;
                string inner = "{\"playerSlot\":" + other.Slot + ",\"chickenIndex\":" + other.Chicken + "}";
                target.Send(BuildMessage("SET_CHICKEN", inner));
            }
        }
    }

    // ---------- Mensajes entrantes ----------

    public static void HandleMessage(ClientHandler from, string line)
    {
        Match typeMatch = Regex.Match(line, "^\\s*\\{\\s*\"type\"\\s*:\\s*\"([^\"]+)\"");
        if (!typeMatch.Success) return;

        switch (typeMatch.Groups[1].Value)
        {
            case "SET_CHICKEN":
                {
                    if (TryReadInt(line, "chickenIndex", out int chicken) && chicken >= 0 && chicken < 100)
                    {
                        from.Chicken = chicken;
                        // El slot lo pone el servidor
                        string inner = "{\"playerSlot\":" + from.Slot + ",\"chickenIndex\":" + chicken + "}";
                        Broadcast(BuildMessage("SET_CHICKEN", inner));
                    }
                    break;
                }

            case "LOBBY_REFRESH":
                SendLobbyState(from);
                break;

            case "START_GAME":
                StartGame(from);
                break;

            case "GAME_READY":
                {
                    bool allReady = false;
                    lock (stateLock)
                    {
                        if (gameStarted && !gameBegan)
                        {
                            readySlots.Add(from.Slot);
                            allReady = readySlots.Count >= clients.Count;
                        }
                    }
                    if (allReady) BeginGame();
                    break;
                }

            case "PICKUP_ITEM":
                {
                    if (TryReadInt(line, "itemID", out int itemID) && TryReadString(line, "itemType", out string itemType))
                        ProcessPickupRequest(from.Slot, itemID, itemType);
                    break;
                }

            case "USE_POWERUP":
                {
                    bool active;
                    lock (stateLock) { active = gameBegan && !gameOver; }

                    if (active && TryReadString(line, "powerUpType", out string powerUp) && powerUp == "FREEZE")
                    {
                        // El slot lo pone el servidor, para que nadie pueda suplantar a otro jugador
                        string inner = "{\"senderSlot\":" + from.Slot + ",\"targetSlot\":-1,\"powerUpType\":\"FREEZE\"}";
                        Console.WriteLine("Jugador " + from.Slot + " usó HIELO");
                        Broadcast(BuildMessage("USE_POWERUP", inner), from);
                    }
                    break;
                }

            case "MOVE":
                {
                    if (TryReadFloat(line, "posX", out float x) && TryReadFloat(line, "posY", out float y))
                    {
                        string inner = "{\"playerSlot\":" + from.Slot +
                                       ",\"posX\":" + x.ToString(CultureInfo.InvariantCulture) +
                                       ",\"posY\":" + y.ToString(CultureInfo.InvariantCulture) + "}";
                        Broadcast(BuildMessage("MOVE", inner), from);
                    }
                    break;
                }

            case "FINAL_SCORE":
                {
                    bool allScores = false;
                    if (TryReadInt(line, "score", out int score))
                    {
                        lock (stateLock)
                        {
                            if (gameOver && !resultsSent)
                            {
                                finalScores[from.Slot] = score;
                                allScores = finalScores.Count >= clients.Count;
                            }
                        }
                    }
                    if (allScores) SendResults();
                    break;
                }
        }
    }

    // ---------- Ciclo de la partida ----------

    private static void StartGame(ClientHandler from)
    {
        string message;
        lock (stateLock)
        {
            // Solo el host puede iniciar, y solo una vez
            if (gameStarted || clients.Count == 0 || clients[0] != from) return;

            ResetGameState();
            gameStarted = true;
            resultsSent = false;

            int seed = rng.Next();
            List<string> slots = new List<string>();
            List<string> chickens = new List<string>();
            foreach (var c in clients)
            {
                slots.Add(c.Slot.ToString());
                chickens.Add(c.Chicken.ToString());
            }

            string inner = "{\"seed\":" + seed +
                           ",\"slots\":[" + string.Join(",", slots) + "]" +
                           ",\"chickens\":[" + string.Join(",", chickens) + "]}";
            message = BuildMessage("START_GAME", inner);

            // Si algún cliente tarda demasiado en cargar, se empieza igual
            readyTimer = new Timer(_ => BeginGame(), null, ReadyTimeoutMs, Timeout.Infinite);
            Console.WriteLine("¡Partida iniciada! Jugadores: " + clients.Count + " | semilla: " + seed);
        }
        Broadcast(message);
    }

    // Todos cargaron la escena: arranca el reloj para todos al mismo tiempo
    private static void BeginGame()
    {
        lock (stateLock)
        {
            if (!gameStarted || gameBegan) return;
            gameBegan = true;

            if (readyTimer != null) { readyTimer.Dispose(); readyTimer = null; }
            gameTimer = new Timer(_ => EndGame(), null, GameDurationSeconds * 1000, Timeout.Infinite);
        }

        Console.WriteLine("¡Comienza la cuenta de " + GameDurationSeconds + " segundos!");
        Broadcast(BuildMessage("GAME_BEGIN", "{\"duration\":" + GameDurationSeconds + "}"));
    }

    private static void EndGame()
    {
        lock (stateLock)
        {
            if (!gameBegan || gameOver) return;
            gameOver = true;

            // Si algún cliente no manda su puntaje, se muestran resultados igual
            resultsTimer = new Timer(_ => SendResults(), null, ResultsTimeoutMs, Timeout.Infinite);
        }

        Console.WriteLine("Se acabó el tiempo.");
        Broadcast(BuildMessage("GAME_OVER", "{}"));
    }

    private static void SendResults()
    {
        string message;
        lock (stateLock)
        {
            if (!gameOver || resultsSent) return;
            resultsSent = true;

            List<string> slots = new List<string>();
            List<string> scores = new List<string>();
            foreach (var c in clients)
            {
                int s;
                if (!finalScores.TryGetValue(c.Slot, out s)) s = 0;
                slots.Add(c.Slot.ToString());
                scores.Add(s.ToString());
            }

            message = BuildMessage("RESULTS",
                "{\"slots\":[" + string.Join(",", slots) + "],\"scores\":[" + string.Join(",", scores) + "]}");

            // Queda listo para jugar otra ronda con los mismos jugadores
            ResetGameState();
        }

        Console.WriteLine("Resultados enviados.");
        Broadcast(message);
    }

    // RESOLUCIÓN FIFO: el primer jugador cuya petición llega al servidor gana el maíz
    private static void ProcessPickupRequest(int playerSlot, int itemID, string itemType)
    {
        if (itemID < 0 || itemID >= TotalItems) return;
        if (!validItemTypes.Contains(itemType)) return;

        lock (stateLock)
        {
            if (!gameBegan || gameOver) return;
            if (!collectedItemIDs.Add(itemID)) return; // Ya lo recogió otro
        }

        Console.WriteLine("Jugador " + playerSlot + " recogió el ítem " + itemID + " (" + itemType + ")");

        string inner = "{\"playerSlot\":" + playerSlot + ",\"itemID\":" + itemID + ",\"itemType\":\"" + itemType + "\"}";
        Broadcast(BuildMessage("ITEM_PICKED_UP", inner)); // A TODOS, incluido el ganador
    }

    public static void RemoveClient(ClientHandler handler)
    {
        bool wasStarted;
        bool shouldBegin = false;
        bool shouldSendResults = false;

        lock (stateLock)
        {
            if (!clients.Remove(handler)) return;
            wasStarted = gameStarted;

            readySlots.Remove(handler.Slot);
            finalScores.Remove(handler.Slot);

            if (clients.Count == 0)
            {
                // Si no queda nadie, el servidor queda listo para una partida nueva
                ResetGameState();
            }
            else if (gameStarted)
            {
                // Quizá los que quedan ya estaban todos listos / ya habían enviado su puntaje
                shouldBegin = !gameBegan && readySlots.Count >= clients.Count;
                shouldSendResults = gameOver && finalScores.Count >= clients.Count;
            }
        }

        handler.Close();
        Console.WriteLine("Jugador " + handler.Slot + " desconectado.");

        if (wasStarted) Broadcast(BuildMessage("PLAYER_LEFT", "{\"playerSlot\":" + handler.Slot + "}"));
        else BroadcastJoinUpdate();

        if (shouldBegin) BeginGame();
        if (shouldSendResults) SendResults();
    }

    // ---------- Lectura de campos dentro del JSON ----------
    // Funcionan tanto con comillas normales como escapadas (\"), que es como llega el payload

    private static bool TryReadInt(string line, string key, out int value)
    {
        Match m = Regex.Match(line, key + @"\\?""\s*:\s*(-?\d+)");
        value = 0;
        return m.Success && int.TryParse(m.Groups[1].Value, out value);
    }

    private static bool TryReadFloat(string line, string key, out float value)
    {
        Match m = Regex.Match(line, key + @"\\?""\s*:\s*(-?\d+(?:\.\d+)?(?:[eE][-+]?\d+)?)");
        value = 0f;
        return m.Success && float.TryParse(m.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
    }

    private static bool TryReadString(string line, string key, out string value)
    {
        Match m = Regex.Match(line, key + @"\\?""\s*:\s*\\?""(\w+)");
        value = m.Success ? m.Groups[1].Value : null;
        return m.Success;
    }
}

public class ClientHandler
{
    public readonly int Slot;
    public int Chicken = 0; // Índice del pollo que eligió en el lobby

    private readonly TcpClient client;
    private readonly NetworkStream stream;
    private readonly object sendLock = new object();

    public ClientHandler(TcpClient client, int slot)
    {
        this.client = client;
        Slot = slot;
        stream = client.GetStream();
    }

    public void Run()
    {
        try
        {
            StreamReader reader = new StreamReader(stream, Encoding.UTF8);
            string line;
            while ((line = reader.ReadLine()) != null)
            {
                if (line.Length == 0) continue;
                GameServer.HandleMessage(this, line);
            }
        }
        catch (Exception)
        {
            // Conexión cortada: se limpia en el finally
        }
        finally
        {
            GameServer.RemoveClient(this);
        }
    }

    public void Send(string data)
    {
        try
        {
            byte[] bytes = Encoding.UTF8.GetBytes(data);
            lock (sendLock) { stream.Write(bytes, 0, bytes.Length); }
        }
        catch (Exception)
        {
            // Si falla el envío, el hilo de lectura detecta la caída y lo elimina
        }
    }

    public void Close()
    {
        try { client.Close(); } catch (Exception) { }
    }
}