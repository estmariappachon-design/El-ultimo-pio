using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;

// =====================================================================================
//  SERVIDOR DE LA PARTIDA (TCP Sockets)
//  Corre DENTRO del celular que pulsa "Crear partida" (hilo en segundo plano).
//  También puede compilarse como app de consola (ver el Main al final del archivo).
//
//  Formato de cada mensaje (una línea): {"type":"X","payload":"<json escapado como texto>"}
//
//  Flujo:
//   Lobby : JOIN_UPDATE / SET_CHICKEN / LOBBY_REFRESH
//   Inicio: host envía START_GAME -> el servidor genera spawns + 50 maíces y los manda a todos
//           -> cada cliente carga la escena y responde GAME_READY -> el servidor envía GAME_BEGIN
//   Juego : MOVE / PICKUP_ITEM -> ITEM_PICKED_UP + SCORE_UPDATE + CHARGES
//           USE_POWERUP -> POWERUP_USED (a todos) + CHARGES (al que lo usó)
//   Final : GAME_OVER + RESULTS (el servidor calcula puntajes, ganador y desempate)
//
//  El servidor es la AUTORIDAD: decide quién recoge cada maíz, cuánto vale, cuántas cargas
//  de power-up tiene cada jugador y a quién congela el hielo (al rival más cercano).
// =====================================================================================
public static class GameServer
{
    // ---------- Configuración (variables de balance) ----------
    public const int DefaultPort = 8888;
    public const int MaxPlayers = 4;
    public static int MinPlayers = 2;               // se puede bajar a 1 SOLO para pruebas
    public static int GameDurationSeconds = 120;    // duración de la partida

    public static int PointsNormal = 1;             // maíz normal
    public static int PointsSpecial = 2;            // maíz arcoíris o de hielo
    public static int MaxCharges = 3;               // máximo de cargas acumuladas por power-up
    public static float SpeedDuration = 5f;         // segundos de turbo (doc: 3 a 6)
    public static float FreezeDuration = 3f;        // segundos congelado (doc: 2 a 4)
    public static float PowerUpCooldown = 2f;       // segundos entre usos de power-ups
    public static float MaxPickupDistance = 6f;     // validación anti-trampa del servidor
    public static bool RespawnItems = true;         // el maíz recogido reaparece en otro lugar
    public static float SpecialRespawnDelay = 15f;  // segundos que tarda en reaparecer un maíz ESPECIAL (0 = al instante)

    private const int ReadyTimeoutMs = 15000;

    // Para ver los mensajes del servidor en la consola de Unity (lo asigna MainMenuManager)
    public static Action<string> Log = s => Console.WriteLine(s);

    // ---------- Estado ----------
    private static TcpListener listener;
    private static Thread acceptThread;
    private static volatile bool running = false;

    private static readonly List<ClientHandler> clients = new List<ClientHandler>();
    private static readonly HashSet<int> readySlots = new HashSet<int>();
    private static readonly System.Random rng = new System.Random();
    private static readonly object stateLock = new object();

    private static bool gameStarted = false; // el host pulsó EMPEZAR
    private static bool gameBegan = false;   // ya corre el reloj
    private static bool gameOver = false;

    private static float[] itemX = new float[MatchGenerator.TotalItems];
    private static float[] itemY = new float[MatchGenerator.TotalItems];
    private static int[] itemT = new int[MatchGenerator.TotalItems];
    private static readonly HashSet<int> collectedItemIDs = new HashSet<int>();

    private static Timer readyTimer;
    private static Timer gameTimer;
    private static readonly List<Timer> respawnTimers = new List<Timer>(); // reaparición con retraso
    private static int matchId = 0; // evita que un temporizador de la partida anterior afecte a la nueva

    public static bool IsRunning => running;

    // =================================================================================
    //  Arranque y parada
    // =================================================================================

    public static bool StartServer(int port = DefaultPort)
    {
        if (running) return true;

        try
        {
            listener = new TcpListener(IPAddress.Any, port);
            listener.Start();
        }
        catch (Exception e)
        {
            Log("[Servidor] No se pudo abrir el puerto " + port + ": " + e.Message);
            listener = null;
            return false;
        }

        running = true;
        acceptThread = new Thread(AcceptLoop) { IsBackground = true, Name = "GameServerAccept" };
        acceptThread.Start();
        Log("[Servidor] Iniciado en el puerto " + port);
        return true;
    }

    public static void StopServer()
    {
        if (!running) return;
        running = false;

        try { listener?.Stop(); } catch (Exception) { }
        listener = null;

        List<ClientHandler> toClose;
        lock (stateLock)
        {
            toClose = new List<ClientHandler>(clients);
            clients.Clear();
            ResetGameState();
        }
        foreach (var c in toClose) c.Close();

        Log("[Servidor] Detenido.");
    }

    private static void AcceptLoop()
    {
        while (running)
        {
            TcpClient tcp;
            try
            {
                tcp = listener.AcceptTcpClient();
            }
            catch (Exception)
            {
                break; // listener.Stop() hace que AcceptTcpClient lance excepción: salimos
            }

            tcp.NoDelay = true;

            ClientHandler handler = null;
            lock (stateLock)
            {
                if (!gameStarted && clients.Count < MaxPlayers)
                {
                    bool loopback = false;
                    try { loopback = IPAddress.IsLoopback(((IPEndPoint)tcp.Client.RemoteEndPoint).Address); }
                    catch (Exception) { }

                    handler = new ClientHandler(tcp, GetFreeSlot(), loopback);
                    clients.Add(handler);
                }
            }

            if (handler == null)
            {
                Log("[Servidor] Conexión rechazada (sala llena o partida en curso).");
                try { tcp.Close(); } catch (Exception) { }
                continue;
            }

            Log("[Servidor] Jugador conectado: slot " + handler.Slot);
            new Thread(handler.Run) { IsBackground = true, Name = "Client" + handler.Slot }.Start();
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
                if (c.Slot == s) { used = true; break; }
            if (!used) return s;
        }
        return -1;
    }

    // El host es el primer cliente conectado desde el mismo celular (127.0.0.1).
    // Si no hay ninguno (servidor de consola), el host es el primero que se conectó.
    // Se llama dentro de stateLock
    private static ClientHandler GetHost()
    {
        foreach (var c in clients)
            if (c.IsLoopback) return c;
        return clients.Count > 0 ? clients[0] : null;
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

        if (readyTimer != null) { readyTimer.Dispose(); readyTimer = null; }
        if (gameTimer != null) { gameTimer.Dispose(); gameTimer = null; }
        foreach (Timer t in respawnTimers) t.Dispose();
        respawnTimers.Clear();
        matchId++;
    }

    // =================================================================================
    //  Envío
    // =================================================================================

    public static string BuildMessage(string type, string innerJson)
    {
        string escaped = innerJson.Replace("\\", "\\\\").Replace("\"", "\\\"");
        return "{\"type\":\"" + type + "\",\"payload\":\"" + escaped + "\"}\n";
    }

    public static void Broadcast(string message, ClientHandler except = null)
    {
        foreach (var c in Snapshot())
            if (c != except) c.Send(message);
    }

    private static string F(float v)
    {
        return v.ToString("0.##", CultureInfo.InvariantCulture);
    }

    private static string IntArray(IEnumerable<int> values)
    {
        List<string> list = new List<string>();
        foreach (int v in values) list.Add(v.ToString(CultureInfo.InvariantCulture));
        return "[" + string.Join(",", list) + "]";
    }

    private static string FloatArray(IEnumerable<float> values)
    {
        List<string> list = new List<string>();
        foreach (float v in values) list.Add(F(v));
        return "[" + string.Join(",", list) + "]";
    }

    private static string JoinJson(ClientHandler target, List<ClientHandler> list, ClientHandler host)
    {
        List<int> slots = new List<int>();
        foreach (var c in list) slots.Add(c.Slot);

        return "{\"playerSlot\":" + target.Slot +
               ",\"isHost\":" + (target == host ? "true" : "false") +
               ",\"totalPlayers\":" + list.Count +
               ",\"minPlayers\":" + MinPlayers +
               ",\"slots\":" + IntArray(slots) + "}";
    }

    // Estado del lobby para UN jugador (al entrar al lobby o al volver de una partida)
    private static void SendLobbyState(ClientHandler target)
    {
        List<ClientHandler> list;
        ClientHandler host;
        lock (stateLock)
        {
            list = new List<ClientHandler>(clients);
            host = GetHost();
        }
        if (!list.Contains(target)) return;

        target.Send(BuildMessage("JOIN_UPDATE", JoinJson(target, list, host)));

        // Incluye también su propio pollo, para que el lobby lo recuerde al volver de una partida
        foreach (var other in list)
            target.Send(BuildMessage("SET_CHICKEN",
                "{\"playerSlot\":" + other.Slot + ",\"chickenIndex\":" + other.Chicken + "}"));
    }

    public static void BroadcastJoinUpdate()
    {
        List<ClientHandler> list;
        ClientHandler host;
        lock (stateLock)
        {
            list = new List<ClientHandler>(clients);
            host = GetHost();
        }

        foreach (var target in list)
            target.Send(BuildMessage("JOIN_UPDATE", JoinJson(target, list, host)));

        foreach (var target in list)
            foreach (var other in list)
                target.Send(BuildMessage("SET_CHICKEN",
                    "{\"playerSlot\":" + other.Slot + ",\"chickenIndex\":" + other.Chicken + "}"));
    }

    // Puntajes de todos (para el ranking del HUD)
    private static string BuildScoreUpdate()
    {
        List<int> slots = new List<int>(), scores = new List<int>(), items = new List<int>();
        lock (stateLock)
        {
            foreach (var c in clients)
            {
                slots.Add(c.Slot);
                scores.Add(c.Score);
                items.Add(c.Items);
            }
        }
        return BuildMessage("SCORE_UPDATE",
            "{\"slots\":" + IntArray(slots) + ",\"scores\":" + IntArray(scores) + ",\"items\":" + IntArray(items) + "}");
    }

    // Cargas de power-up de UN jugador
    private static void SendCharges(ClientHandler c)
    {
        int speed, freeze;
        lock (stateLock)
        {
            speed = c.SpeedCharges;
            freeze = c.FreezeCharges;
        }
        c.Send(BuildMessage("CHARGES", "{\"speedCharges\":" + speed + ",\"freezeCharges\":" + freeze + "}"));
    }

    // =================================================================================
    //  Mensajes entrantes
    // =================================================================================

    public static void HandleMessage(ClientHandler from, string line)
    {
        Match typeMatch = Regex.Match(line, "^\\s*\\{\\s*\"type\"\\s*:\\s*\"([^\"]+)\"");
        if (!typeMatch.Success) return;

        switch (typeMatch.Groups[1].Value)
        {
            case "SET_CHICKEN":
                if (TryReadInt(line, "chickenIndex", out int chicken) && chicken >= 0 && chicken < 100)
                {
                    from.Chicken = chicken;
                    Broadcast(BuildMessage("SET_CHICKEN",
                        "{\"playerSlot\":" + from.Slot + ",\"chickenIndex\":" + chicken + "}"));
                }
                break;

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

            case "MOVE":
                if (TryReadFloat(line, "posX", out float x) && TryReadFloat(line, "posY", out float y))
                {
                    float limit = MatchGenerator.MapHalfSize;
                    x = Math.Max(-limit, Math.Min(limit, x));
                    y = Math.Max(-limit, Math.Min(limit, y));
                    lock (stateLock)
                    {
                        from.PosX = x;
                        from.PosY = y;
                    }
                    Broadcast(BuildMessage("MOVE",
                        "{\"playerSlot\":" + from.Slot + ",\"posX\":" + F(x) + ",\"posY\":" + F(y) + "}"), from);
                }
                break;

            case "PICKUP_ITEM":
                if (TryReadInt(line, "itemID", out int itemID))
                    ProcessPickupRequest(from, itemID);
                break;

            case "USE_POWERUP":
                if (TryReadString(line, "powerUpType", out string powerUp))
                    ProcessPowerUp(from, powerUp);
                break;
        }
    }

    // =================================================================================
    //  Ciclo de la partida
    // =================================================================================

    private static void StartGame(ClientHandler from)
    {
        string message;
        lock (stateLock)
        {
            // Solo el host, una sola vez, y con el mínimo de jugadores
            if (gameStarted || from != GetHost()) return;
            if (clients.Count < MinPlayers || clients.Count > MaxPlayers)
            {
                Log("[Servidor] No se puede iniciar: hay " + clients.Count + " jugador(es), mínimo " + MinPlayers);
                return;
            }

            ResetGameState();
            gameStarted = true;

            int count = clients.Count;
            float[] spawnX = new float[count];
            float[] spawnY = new float[count];
            MatchGenerator.GenerateSpawns(rng, count, spawnX, spawnY);
            MatchGenerator.GenerateItems(rng, itemX, itemY, itemT, spawnX, spawnY, count);

            List<int> slots = new List<int>(), chickens = new List<int>();
            for (int i = 0; i < count; i++)
            {
                ClientHandler c = clients[i];
                c.ResetMatchStats();
                c.PosX = spawnX[i];
                c.PosY = spawnY[i];
                slots.Add(c.Slot);
                chickens.Add(c.Chicken);
            }

            string inner = "{\"slots\":" + IntArray(slots) +
                           ",\"chickens\":" + IntArray(chickens) +
                           ",\"spawnX\":" + FloatArray(spawnX) +
                           ",\"spawnY\":" + FloatArray(spawnY) +
                           ",\"itemX\":" + FloatArray(itemX) +
                           ",\"itemY\":" + FloatArray(itemY) +
                           ",\"itemT\":" + IntArray(itemT) +
                           ",\"duration\":" + GameDurationSeconds + "}";
            message = BuildMessage("START_GAME", inner);

            // Si algún celular tarda demasiado en cargar, se empieza igual
            readyTimer = new Timer(_ => BeginGame(), null, ReadyTimeoutMs, Timeout.Infinite);
            Log("[Servidor] ¡Partida iniciada! Jugadores: " + count);
        }
        Broadcast(message);
    }

    private static void BeginGame()
    {
        lock (stateLock)
        {
            if (!gameStarted || gameBegan) return;
            gameBegan = true;

            if (readyTimer != null) { readyTimer.Dispose(); readyTimer = null; }
            gameTimer = new Timer(_ => EndGame(), null, GameDurationSeconds * 1000, Timeout.Infinite);
        }

        Log("[Servidor] Comienza la cuenta de " + GameDurationSeconds + " s");
        Broadcast(BuildMessage("GAME_BEGIN", "{\"duration\":" + GameDurationSeconds + "}"));
        Broadcast(BuildScoreUpdate());
    }

    private static void EndGame()
    {
        string results;
        lock (stateLock)
        {
            if (!gameBegan || gameOver) return;
            gameOver = true;
            results = BuildResults();

            // Queda listo para otra ronda con los mismos jugadores
            ResetGameState();
        }

        Log("[Servidor] Se acabó el tiempo.");
        Broadcast(BuildMessage("GAME_OVER", "{}"));
        Broadcast(results);
    }

    // Ranking final. Regla de desempate: 1) mayor puntaje, 2) más maíces especiales.
    // Si siguen empatados -> VICTORIA COMPARTIDA (todos los empatados son ganadores).
    // Se llama dentro de stateLock
    private static string BuildResults()
    {
        List<ClientHandler> ranking = new List<ClientHandler>(clients);
        ranking.Sort((a, b) =>
        {
            int byScore = b.Score.CompareTo(a.Score);
            return byScore != 0 ? byScore : b.Specials.CompareTo(a.Specials);
        });

        List<int> slots = new List<int>(), scores = new List<int>(), items = new List<int>(),
                  specials = new List<int>(), winners = new List<int>();

        for (int i = 0; i < ranking.Count; i++)
        {
            ClientHandler c = ranking[i];
            bool isWinner = c.Score == ranking[0].Score && c.Specials == ranking[0].Specials;

            slots.Add(c.Slot);
            scores.Add(c.Score);
            items.Add(c.Items);
            specials.Add(c.Specials);
            winners.Add(isWinner ? 1 : 0);
        }

        return BuildMessage("RESULTS",
            "{\"slots\":" + IntArray(slots) +
            ",\"scores\":" + IntArray(scores) +
            ",\"items\":" + IntArray(items) +
            ",\"specials\":" + IntArray(specials) +
            ",\"winners\":" + IntArray(winners) + "}");
    }

    // RESOLUCIÓN DE CONFLICTOS: el primer pedido que llega al servidor se queda con el maíz.
    private static void ProcessPickupRequest(ClientHandler from, int itemID)
    {
        if (itemID < 0 || itemID >= MatchGenerator.TotalItems) return;

        int type;
        bool chargeChanged = false;
        string respawnMessage = null;
        lock (stateLock)
        {
            if (!gameBegan || gameOver) return;
            if (collectedItemIDs.Contains(itemID)) return;          // ya lo recogió otro
            if (DateTime.UtcNow < from.FrozenUntil) return;          // congelado no recoge

            float dx = itemX[itemID] - from.PosX;
            float dy = itemY[itemID] - from.PosY;
            if (Math.Sqrt(dx * dx + dy * dy) > MaxPickupDistance) return; // demasiado lejos

            type = itemT[itemID];

            if (RespawnItems)
            {
                bool special = type != MatchGenerator.TypeNormal;
                if (special && SpecialRespawnDelay > 0f)
                {
                    // Los especiales desaparecen un rato: así salen menos seguido
                    collectedItemIDs.Add(itemID);
                    int id = itemID, match = matchId;
                    Timer t = new Timer(_ => DelayedRespawn(id, match), null, (int)(SpecialRespawnDelay * 1000f), Timeout.Infinite);
                    respawnTimers.Add(t);
                }
                else
                {
                    // Los normales reaparecen al instante en otro lugar: el mapa nunca se queda vacío
                    respawnMessage = RelocateItem(itemID);
                }
            }
            else
            {
                collectedItemIDs.Add(itemID);
            }

            from.Items++;
            if (type == MatchGenerator.TypeNormal)
            {
                from.Score += PointsNormal;
            }
            else
            {
                from.Score += PointsSpecial;
                from.Specials++;

                if (type == MatchGenerator.TypeSpeed && from.SpeedCharges < MaxCharges)
                {
                    from.SpeedCharges++;
                    chargeChanged = true;
                }
                else if (type == MatchGenerator.TypeFreeze && from.FreezeCharges < MaxCharges)
                {
                    from.FreezeCharges++;
                    chargeChanged = true;
                }
            }
        }

        Broadcast(BuildMessage("ITEM_PICKED_UP",
            "{\"playerSlot\":" + from.Slot + ",\"itemID\":" + itemID + ",\"itemType\":" + type + "}"));
        if (respawnMessage != null) Broadcast(respawnMessage);
        Broadcast(BuildScoreUpdate());
        if (chargeChanged) SendCharges(from);
    }

    // Mueve el maíz a un lugar nuevo (lejos de los pollos) y devuelve el mensaje ITEM_SPAWNED.
    // Se llama dentro de stateLock
    private static string RelocateItem(int itemID)
    {
        float[] px = new float[clients.Count], py = new float[clients.Count];
        for (int k = 0; k < clients.Count; k++) { px[k] = clients[k].PosX; py[k] = clients[k].PosY; }

        MatchGenerator.RandomRespawnPosition(rng, itemID, itemX, itemY, MatchGenerator.TotalItems,
                                             px, py, clients.Count, out float nx, out float ny);
        itemX[itemID] = nx;
        itemY[itemID] = ny;
        collectedItemIDs.Remove(itemID);

        return BuildMessage("ITEM_SPAWNED",
            "{\"itemID\":" + itemID + ",\"posX\":" + F(nx) + ",\"posY\":" + F(ny) + ",\"itemType\":" + itemT[itemID] + "}");
    }

    // Reaparición de un maíz especial después de SpecialRespawnDelay segundos
    private static void DelayedRespawn(int itemID, int match)
    {
        string message;
        lock (stateLock)
        {
            if (match != matchId || !gameBegan || gameOver) return; // la partida ya terminó
            message = RelocateItem(itemID);
        }
        Broadcast(message);
    }

    // SPEED: turbo para quien lo usa. FREEZE: congela al rival MÁS CERCANO que no esté congelado.
    private static void ProcessPowerUp(ClientHandler from, string powerUp)
    {
        string message = null;
        lock (stateLock)
        {
            if (!gameBegan || gameOver) return;

            DateTime now = DateTime.UtcNow;
            if (now < from.NextPowerUpTime || now < from.FrozenUntil)
            {
                // No se puede usar todavía: igual le reenviamos sus cargas para que el HUD quede bien
            }
            else if (powerUp == "SPEED" && from.SpeedCharges > 0)
            {
                from.SpeedCharges--;
                from.NextPowerUpTime = now.AddSeconds(PowerUpCooldown);
                message = BuildPowerUpMessage(from.Slot, from.Slot, "SPEED", SpeedDuration);
                Log("[Servidor] Jugador " + from.Slot + " usó TURBO");
            }
            else if (powerUp == "FREEZE" && from.FreezeCharges > 0)
            {
                ClientHandler target = FindNearestRival(from, now);
                if (target != null)
                {
                    from.FreezeCharges--;
                    from.NextPowerUpTime = now.AddSeconds(PowerUpCooldown);
                    target.FrozenUntil = now.AddSeconds(FreezeDuration);
                    message = BuildPowerUpMessage(from.Slot, target.Slot, "FREEZE", FreezeDuration);
                    Log("[Servidor] Jugador " + from.Slot + " congeló al jugador " + target.Slot);
                }
            }
        }

        if (message != null) Broadcast(message);
        SendCharges(from);
    }

    // Se llama dentro de stateLock
    private static ClientHandler FindNearestRival(ClientHandler from, DateTime now)
    {
        ClientHandler best = null;
        float bestDist = float.MaxValue;
        foreach (var c in clients)
        {
            if (c == from || now < c.FrozenUntil) continue;
            float dx = c.PosX - from.PosX;
            float dy = c.PosY - from.PosY;
            float d = dx * dx + dy * dy;
            if (d < bestDist)
            {
                bestDist = d;
                best = c;
            }
        }
        return best;
    }

    private static string BuildPowerUpMessage(int sender, int target, string type, float duration)
    {
        return BuildMessage("POWERUP_USED",
            "{\"senderSlot\":" + sender + ",\"targetSlot\":" + target +
            ",\"powerUpType\":\"" + type + "\",\"duration\":" + F(duration) + "}");
    }

    public static void RemoveClient(ClientHandler handler)
    {
        bool wasStarted;
        bool shouldBegin = false;

        lock (stateLock)
        {
            if (!clients.Remove(handler)) return;
            wasStarted = gameStarted;
            readySlots.Remove(handler.Slot);

            if (clients.Count == 0)
                ResetGameState();
            else if (gameStarted)
                shouldBegin = !gameBegan && readySlots.Count >= clients.Count;
        }

        handler.Close();
        Log("[Servidor] Jugador " + handler.Slot + " desconectado.");

        if (wasStarted)
        {
            Broadcast(BuildMessage("PLAYER_LEFT", "{\"playerSlot\":" + handler.Slot + "}"));
            Broadcast(BuildScoreUpdate());
        }
        else
        {
            BroadcastJoinUpdate();
        }

        if (shouldBegin) BeginGame();
    }

    // =================================================================================
    //  Lectura de campos del JSON (funciona con comillas normales y escapadas \")
    // =================================================================================

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

// =====================================================================================
//  Un jugador conectado al servidor
// =====================================================================================
public class ClientHandler
{
    public readonly int Slot;
    public readonly bool IsLoopback;
    public int Chicken = 0;

    // Datos de la partida (los protege el stateLock del GameServer)
    public float PosX, PosY;
    public int Score, Items, Specials;
    public int SpeedCharges, FreezeCharges;
    public DateTime NextPowerUpTime = DateTime.MinValue;
    public DateTime FrozenUntil = DateTime.MinValue;

    private readonly TcpClient client;
    private readonly NetworkStream stream;
    private readonly object sendLock = new object();

    public ClientHandler(TcpClient client, int slot, bool isLoopback)
    {
        this.client = client;
        Slot = slot;
        IsLoopback = isLoopback;
        stream = client.GetStream();
    }

    public void ResetMatchStats()
    {
        Score = Items = Specials = 0;
        SpeedCharges = FreezeCharges = 0;
        NextPowerUpTime = DateTime.MinValue;
        FrozenUntil = DateTime.MinValue;
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
            // Si falla, el hilo de lectura detecta la caída y lo elimina
        }
    }

    public void Close()
    {
        try { client.Close(); } catch (Exception) { }
    }
}

#if !UNITY_5_3_OR_NEWER
// Solo se compila FUERA de Unity (si quieren correr el servidor en un PC como consola).
// Dentro de Unity no existe este Main: el servidor lo arranca MainMenuManager.
public static class ServerProgram
{
    public static void Main(string[] args)
    {
        if (args.Length > 0 && args[0] == "--solo") GameServer.MinPlayers = 1;
        GameServer.StartServer();
        Console.WriteLine("Presiona ENTER para detener el servidor.");
        Console.ReadLine();
        GameServer.StopServer();
    }
}
#endif
