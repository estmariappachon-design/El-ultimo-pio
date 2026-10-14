using System;
using UnityEngine;

// =====================================================================================
//  Mensajes que viajan por el socket. Cada línea es un NetworkMessage cuyo "payload"
//  es OTRO JSON guardado como texto. Los nombres de los campos deben coincidir EXACTAMENTE
//  con los que escribe Server.cs.
// =====================================================================================

[Serializable]
public class NetworkMessage
{
    public string type;
    public string payload;
}

// ---------- Lobby ----------

[Serializable]
public class JoinPayload
{
    public int playerSlot;
    public bool isHost;
    public int totalPlayers;
    public int minPlayers;
    public int[] slots;
}

[Serializable]
public class ChickenPayload
{
    public int playerSlot;
    public int chickenIndex;
}

// ---------- Inicio de partida ----------

[Serializable]
public class StartGamePayload
{
    public int[] slots;      // slots de los jugadores en la partida
    public int[] chickens;   // pollo elegido por cada uno (mismo orden que slots)
    public float[] spawnX;   // posición inicial aleatoria de cada uno (mismo orden que slots)
    public float[] spawnY;
    public float[] itemX;    // los 50 maíces
    public float[] itemY;
    public int[] itemT;      // 0 = Normal, 1 = SpeedBoost, 2 = Freeze
    public float duration;
}

[Serializable]
public class GameBeginPayload
{
    public float duration;
}

// ---------- Durante la partida ----------

[Serializable]
public class MovePayload
{
    public int playerSlot;
    public float posX;
    public float posY;
}

[Serializable]
public class PickupRequestPayload
{
    public int playerSlot;
    public int itemID;
}

[Serializable]
public class ItemPickedPayload
{
    public int playerSlot;
    public int itemID;
    public int itemType;
}

[Serializable]
public class ItemSpawnedPayload
{
    public int itemID;
    public float posX;
    public float posY;
    public int itemType;
}

[Serializable]
public class ScoreUpdatePayload
{
    public int[] slots;
    public int[] scores;
    public int[] items;
}

[Serializable]
public class ChargesPayload
{
    public int speedCharges;
    public int freezeCharges;
}

[Serializable]
public class PowerUpRequestPayload
{
    public int senderSlot;
    public string powerUpType; // "SPEED" o "FREEZE"
}

[Serializable]
public class PowerUpUsedPayload
{
    public int senderSlot;
    public int targetSlot;
    public string powerUpType;
    public float duration;
}

[Serializable]
public class PlayerLeftPayload
{
    public int playerSlot;
}

// ---------- Final ----------

[Serializable]
public class ResultsPayload
{
    public int[] slots;    // ya vienen ordenados del 1.º al último
    public int[] scores;
    public int[] items;
    public int[] specials;
    public int[] winners;  // 1 = ganador (puede haber varios si hay empate)
}

[Serializable]
public class EmptyPayload { }

// =====================================================================================
//  Datos que sobreviven al cambio de escena (Lobby -> Gameplay -> Lobby/Menú)
// =====================================================================================
public static class GameSession
{
    public static int MySlot;
    public static bool IsHost;
    public static int MinPlayers = 2;

    public static bool HasMatch;
    public static int[] Slots;
    public static int[] Chickens;
    public static float[] SpawnX, SpawnY;
    public static float[] ItemX, ItemY;
    public static int[] ItemT;
    public static float Duration = 120f;

    // Por si GAME_BEGIN llega antes de que termine de cargar la escena de juego
    public static bool BeginPending;
    public static float BeginDuration;

    // Mensaje para mostrar en el menú (ej. "Se perdió la conexión")
    public static string LastError;

    public static int IndexOf(int slot)
    {
        if (Slots == null) return -1;
        for (int i = 0; i < Slots.Length; i++)
            if (Slots[i] == slot) return i;
        return -1;
    }

    public static int ChickenOf(int slot)
    {
        int i = IndexOf(slot);
        return (i >= 0 && Chickens != null && i < Chickens.Length) ? Chickens[i] : 0;
    }

    public static Vector2 SpawnOf(int slot)
    {
        int i = IndexOf(slot);
        if (i < 0 || SpawnX == null || SpawnY == null || i >= SpawnX.Length || i >= SpawnY.Length)
            return Vector2.zero;
        return new Vector2(SpawnX[i], SpawnY[i]);
    }

    public static string PlayerName(int slot)
    {
        return "Jugador " + (slot + 1);
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Reset()
    {
        MySlot = 0;
        IsHost = false;
        MinPlayers = 2;
        HasMatch = false;
        Slots = null;
        Chickens = null;
        SpawnX = SpawnY = null;
        ItemX = ItemY = null;
        ItemT = null;
        Duration = 120f;
        BeginPending = false;
        BeginDuration = 0f;
        LastError = null;
    }
}
