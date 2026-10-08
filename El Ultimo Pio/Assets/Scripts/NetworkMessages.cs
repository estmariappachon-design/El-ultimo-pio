using System;
using UnityEngine;

[Serializable]
public class NetworkMessage
{
    public string type;    // "JOIN_UPDATE", "SET_CHICKEN", "START_GAME", "GAME_READY", "GAME_BEGIN", "MOVE", "PICKUP_ITEM",
                           // "ITEM_PICKED_UP", "USE_POWERUP", "PLAYER_LEFT", "GAME_OVER", "FINAL_SCORE", "RESULTS"
    public string payload; // JSON del contenido, guardado COMO TEXTO
}

[Serializable]
public class JoinPayload
{
    public int playerSlot;
    public bool isHost;
    public int totalPlayers;
    public int[] slots; // Slots ocupados (pueden no ser consecutivos si alguien se salió)
}

[Serializable]
public class ChickenPayload
{
    public int playerSlot;
    public int chickenIndex;
}

[Serializable]
public class StartGamePayload
{
    public int seed;       // Semilla común para que todos generen los mismos maíces
    public int[] slots;    // Slots de los jugadores que están en la partida
    public int[] chickens; // Pollo elegido por cada jugador (mismo orden que slots)
}

[Serializable]
public class GameInitPayload
{
    public float[] playerPositionsX;
    public float[] playerPositionsZ;
    public int[] itemIDs;
    public float[] itemPositionsX;
    public float[] itemPositionsZ;
}

[Serializable]
public class GameBeginPayload
{
    public float duration; // Segundos que dura la partida
}

[Serializable]
public class MovePayload
{
    public int playerSlot;
    public float posX;
    public float posY;
}

[Serializable]
public class PickupPayload
{
    public int playerSlot;
    public int itemID;
    public string itemType; // "Normal", "SpeedBoost" o "Freeze"
}

[Serializable]
public class PowerUpPayload
{
    public int senderSlot;
    public int targetSlot;     // Sin uso por ahora (el hielo afecta a todos los rivales)
    public string powerUpType; // "FREEZE"
}

[Serializable]
public class ScorePayload
{
    public int playerSlot;
    public int score;
}

[Serializable]
public class ResultsPayload
{
    public int[] slots;
    public int[] scores;
}

// Datos que viajan del lobby a la escena de juego
public static class GameSession
{
    public static bool HasSeed;
    public static int Seed;
    public static int[] Slots;
    public static int[] Chickens;
    public static int MySlot; // Mi slot en la partida (sobrevive al cambio de escena)

    // Por si el servidor avisa "empieza" antes de que la escena de juego termine de cargar
    public static bool BeginPending;
    public static float BeginDuration;

    public static int ChickenOf(int slot)
    {
        if (Slots == null || Chickens == null) return 0;
        for (int i = 0; i < Slots.Length && i < Chickens.Length; i++)
            if (Slots[i] == slot) return Chickens[i];
        return 0;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Reset()
    {
        HasSeed = false;
        Seed = 0;
        Slots = null;
        Chickens = null;
        MySlot = 0;
        BeginPending = false;
        BeginDuration = 0f;
    }
}