using System;
using System.Collections.Generic;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

// Obtiene la IP del celular en la red Wi-Fi (la que los demás deben escribir para unirse).
// Reúne todas las IPv4 posibles y elige la más probable: 192.168.x.x > 172.16-31.x.x > 10.x.x.x
public static class IPManager
{
    public static string GetLocalIPAddress()
    {
        List<string> all = GetAllLocalIPv4();
        if (all.Count == 0) return "127.0.0.1";

        string best = all[0];
        int bestScore = Score(best);
        foreach (string ip in all)
        {
            int s = Score(ip);
            if (s > bestScore)
            {
                best = ip;
                bestScore = s;
            }
        }
        return best;
    }

    public static List<string> GetAllLocalIPv4()
    {
        List<string> result = new List<string>();

        // 1) Tarjetas de red (Wi-Fi, Ethernet, punto de acceso)
        try
        {
            foreach (NetworkInterface ni in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (ni.OperationalStatus != OperationalStatus.Up) continue;
                if (ni.NetworkInterfaceType == NetworkInterfaceType.Loopback) continue;

                foreach (UnicastIPAddressInformation info in ni.GetIPProperties().UnicastAddresses)
                    Add(result, info.Address);
            }
        }
        catch (Exception) { }

        // 2) DNS del propio equipo
        try
        {
            foreach (IPAddress ip in Dns.GetHostEntry(Dns.GetHostName()).AddressList)
                Add(result, ip);
        }
        catch (Exception) { }

        // 3) Truco UDP: pregunta al sistema qué IP usaría para salir (no envía nada)
        try
        {
            using (Socket s = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp))
            {
                s.Connect("8.8.8.8", 65530);
                if (s.LocalEndPoint is IPEndPoint ep) Add(result, ep.Address);
            }
        }
        catch (Exception) { }

        return result;
    }

    private static void Add(List<string> list, IPAddress ip)
    {
        if (ip == null || ip.AddressFamily != AddressFamily.InterNetwork) return;
        string s = ip.ToString();
        if (s.StartsWith("127.") || s.StartsWith("169.254.") || s == "0.0.0.0") return;
        if (!list.Contains(s)) list.Add(s);
    }

    private static int Score(string ip)
    {
        if (ip.StartsWith("192.168.")) return 3;
        if (ip.StartsWith("172."))
        {
            string[] parts = ip.Split('.');
            if (parts.Length > 1 && int.TryParse(parts[1], out int second) && second >= 16 && second <= 31) return 2;
        }
        if (ip.StartsWith("10.")) return 1;
        return 0;
    }
}
