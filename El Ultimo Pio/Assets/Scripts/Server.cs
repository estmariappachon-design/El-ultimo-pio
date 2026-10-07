using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

public class GameServer
{
    private static TcpListener server;
    private static List<ClientHandler> clients = new List<ClientHandler>();
    private static HashSet<int> collectedItemIDs = new HashSet<int>(); // Registro para evitar duplicados
    private static int nextPlayerSlot = 0;

    public static void Main(string[] args)
    {
        server = new TcpListener(IPAddress.Any, 8888);
        server.Start();
        Console.WriteLine("Servidor de Sockets iniciado en el puerto 8888...");

        while (true)
        {
            TcpClient client = server.AcceptTcpClient();
            if (clients.Count < 4) // Máximo 4 jugadores
            {
                ClientHandler handler = new ClientHandler(client, nextPlayerSlot++);
                clients.Add(handler);
                new Thread(handler.Run).Start();

                // Notificar a todos sobre la nueva lista de jugadores
                BroadcastJoinUpdate();
            }
        }
    }

    public static void BroadcastJoinUpdate()
    {
        for (int i = 0; i < clients.Count; i++)
        {
            bool isHost = (i == 0); // El primero en conectar es el Host
            string jsonMsg = $"{{\"type\":\"JOIN_UPDATE\",\"payload\":{{\"playerSlot\":{clients[i].Slot},\"isHost\":{isHost.ToString().ToLower()},\"totalPlayers\":{clients.Count}}}}}\n";
            clients[i].Send(jsonMsg);
        }
    }

    public static void Broadcast(string message)
    {
        foreach (var client in clients)
        {
            client.Send(message + "\n");
        }
    }

    // RESOLUCIÓN DE CONFLICTOS FIFO AL RECOGER ÍTENS
    public static void ProcessPickupRequest(int playerSlot, int itemID)
    {
        lock (collectedItemIDs)
        {
            // Si el objeto NO ha sido recogido por nadie todavía
            if (!collectedItemIDs.Contains(itemID))
            {
                collectedItemIDs.Add(itemID); // El servidor lo reclama
                Console.WriteLine($"Jugador {playerSlot} recogió el ítem {itemID}");

                // Confirmar a TODOS los jugadores quién ganó el objeto
                string msg = $"{{\"type\":\"ITEM_PICKED_UP\",\"payload\":{{\"playerSlot\":{playerSlot},\"itemID\":{itemID}}}}}";
                Broadcast(msg);
            }
        }
    }
}

public class ClientHandler
{
    public TcpClient Client;
    public int Slot;
    private NetworkStream stream;

    public ClientHandler(TcpClient client, int slot)
    {
        Client = client;
        Slot = slot;
        stream = client.GetStream();
    }

    public void Run()
    {
        StreamReader reader = new StreamReader(stream, Encoding.UTF8);
        while (true)
        {
            try
            {
                string line = reader.ReadLine();
                if (line == null) break;

                // Procesar comandos del cliente
                if (line.Contains("PICKUP_ITEM"))
                {
                    // Extraer ID y procesar recolección autoritaria
                    // GameServer.ProcessPickupRequest(Slot, itemID);
                }
                else
                {
                    // Reenviar mensajes de personalización/movimiento a los demás clientes
                    GameServer.Broadcast(line);
                }
            }
            catch { break; }
        }
    }

    public void Send(string data)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(data);
        stream.Write(bytes, 0, bytes.Length);
    }
}