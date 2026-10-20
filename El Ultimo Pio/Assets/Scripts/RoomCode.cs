using System;
using System.Globalization;
using System.Text;

// =====================================================================================
//  CÓDIGO DE SALA EN HEXADECIMAL
//  Una IP como 192.168.1.23 son 4 números de 0 a 255. En hexadecimal cada número se
//  escribe con 2 caracteres (0-9, A-F), así que la IP completa queda en 8 caracteres:
//
//      192 . 168 . 1   . 23        ->   C0 A8 01 17   ->   "C0A8-0117"
//
//  Además, si los dos celulares están en la MISMA red Wi-Fi, casi siempre comparten los
//  primeros números, así que basta con escribir el FINAL del código:
//      "17"     -> usa los 3 primeros números de mi propia IP + 23
//      "0117"   -> usa los 2 primeros números de mi propia IP + 1.23
//  También se acepta la IP normal con puntos (192.168.1.23), por si acaso.
// =====================================================================================
public static class RoomCode
{
    // IP -> "C0A8-0117"
    public static string FromIP(string ip)
    {
        byte[] b = ParseDotted(ip);
        if (b == null) return "????";
        return b[0].ToString("X2") + b[1].ToString("X2") + "-" + b[2].ToString("X2") + b[3].ToString("X2");
    }

    // Parte corta que basta escribir en la misma Wi-Fi (último número), ej. "17"
    public static string ShortFromIP(string ip)
    {
        byte[] b = ParseDotted(ip);
        return b == null ? "??" : b[3].ToString("X2");
    }

    // Lo que escribe el jugador -> IP. Devuelve false si no se entiende.
    public static bool TryToIP(string input, string myIP, out string ip)
    {
        ip = null;
        if (string.IsNullOrWhiteSpace(input)) return false;

        string clean = input.Trim().ToUpperInvariant();

        // IP normal con puntos
        if (clean.Contains("."))
        {
            byte[] d = ParseDotted(clean);
            if (d == null) return false;
            ip = d[0] + "." + d[1] + "." + d[2] + "." + d[3];
            return true;
        }

        // Solo los caracteres hexadecimales (se ignoran guiones, espacios, dos puntos...)
        StringBuilder hex = new StringBuilder();
        foreach (char c in clean)
        {
            if (IsHex(c)) hex.Append(c);
            else if (c == '-' || c == ' ' || c == ':') continue;
            else return false;
        }

        string h = hex.ToString();
        if (h.Length == 0 || h.Length > 8) return false;
        if (h.Length % 2 == 1) h = "0" + h; // "7" -> "07"

        int given = h.Length / 2; // cuántos números escribió (1 a 4)
        byte[] result = new byte[4];

        // Los que no escribió se toman de mi propia IP (misma red Wi-Fi)
        if (given < 4)
        {
            byte[] mine = ParseDotted(myIP);
            if (mine == null) return false;
            for (int i = 0; i < 4 - given; i++) result[i] = mine[i];
        }

        for (int i = 0; i < given; i++)
            result[4 - given + i] = byte.Parse(h.Substring(i * 2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);

        ip = result[0] + "." + result[1] + "." + result[2] + "." + result[3];
        return true;
    }

    public static bool IsHex(char c)
    {
        return (c >= '0' && c <= '9') || (c >= 'A' && c <= 'F') || (c >= 'a' && c <= 'f');
    }

    private static byte[] ParseDotted(string ip)
    {
        if (string.IsNullOrEmpty(ip)) return null;
        string[] parts = ip.Trim().Split('.');
        if (parts.Length != 4) return null;

        byte[] b = new byte[4];
        for (int i = 0; i < 4; i++)
            if (!byte.TryParse(parts[i], NumberStyles.Integer, CultureInfo.InvariantCulture, out b[i])) return null;
        return b;
    }
}
