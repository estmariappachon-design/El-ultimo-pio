using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using UnityEngine;

public static class IPManager
{
    public static string GetLocalIPAddress()
    {
        try
        {
            var host = Dns.GetHostEntry(Dns.GetHostName());
            foreach (var ip in host.AddressList)
            {
                if (ip.AddressFamily == AddressFamily.InterNetwork)
                {
                    // Evita retornar la IP de bucle local (127.0.0.1)
                    if (!ip.ToString().StartsWith("127."))
                    {
                        return ip.ToString();
                    }
                }
            }

            // Alternativa recorriendo tarjetas de red físicas (útil en Android)
            foreach (NetworkInterface item in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (item.OperationalStatus == OperationalStatus.Up &&
                   (item.NetworkInterfaceType == NetworkInterfaceType.Wireless80211 ||
                    item.NetworkInterfaceType == NetworkInterfaceType.Ethernet))
                {
                    foreach (UnicastIPAddressInformation ip in item.GetIPProperties().UnicastAddresses)
                    {
                        if (ip.Address.AddressFamily == AddressFamily.InterNetwork)
                        {
                            if (!ip.Address.ToString().StartsWith("127."))
                            {
                                return ip.Address.ToString();
                            }
                        }
                    }
                }
            }
        }
        catch
        {
            // Ocurre si no hay conexión Wi-Fi activa
        }

        return "127.0.0.1";
    }
}