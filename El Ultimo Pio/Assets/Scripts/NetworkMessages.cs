using System;

[Serializable]
public class NetworkMessage
{
    public string type; // "JOIN", "SET_CHICKEN", "START_GAME", "MOVE", "PICKUP", "POWERUP"
    public string payload; // Contenido en JSON según el tipo
}

[Serializable]
public class JoinPayload
{
    public int playerSlot;
    public bool isHost;
    public int totalPlayers;
}

[Serializable]
public class ChickenPayload
{
    public int playerSlot;
    public int chickenIndex;
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
public class MovePayload
{
    public int playerSlot;
    public float posX;
    public float posZ;
}

[Serializable]
public class PickupPayload
{
    public int playerSlot;
    public int itemID;
    public string itemType;
}

[Serializable]
public class PowerUpPayload
{
    public int senderSlot;
    public int targetSlot; // Para el power-up de congelar
    public string powerUpType; // "SPEED" o "FREEZE"
}