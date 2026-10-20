using System;
using System.Collections.Generic;

// Genera la partida: posiciones iniciales aleatorias de los jugadores (sin superponerse)
// y los 50 maíces (posición y tipo). Lo usa el SERVIDOR, que luego envía el resultado a todos.
// No depende de UnityEngine, así que también sirve si el servidor se compila como consola.
public static class MatchGenerator
{
    public const float MapHalfSize = 50f;  // terreno 100x100 -> de -50 a 50
    public const float ItemArea = 45f;     // los maíces aparecen entre -45 y 45
    public const float SpawnArea = 40f;    // los pollos aparecen entre -40 y 40

    public const int TotalItems = 50;
    public static int SpeedItems = 3;      // maíces arcoíris (turbo) en el mapa (lo cambia MainMenuManager)
    public static int FreezeItems = 3;     // maíces de hielo (congelar) en el mapa

    public const float MinDistanceBetweenPlayers = 25f;
    public const float MinDistanceBetweenItems = 4f;
    public const float MinDistanceItemToPlayer = 5f;

    // Tipos de ítem como enteros para viajar por la red: 0 = Normal, 1 = SpeedBoost, 2 = Freeze
    public const int TypeNormal = 0;
    public const int TypeSpeed = 1;
    public const int TypeFreeze = 2;
    public const int TypeTeleport = 3;

    // El maíz de teletransporte usa un número fuera de los 50 normales
    public const int TeleportItemID = TotalItems;

    public static void GenerateSpawns(Random rng, int count, float[] xs, float[] ys)
    {
        for (int i = 0; i < count; i++)
        {
            float bestX = 0f, bestY = 0f, bestDist = -1f;

            // Intenta varias veces encontrar un punto lejos de los demás jugadores;
            // si no lo logra, se queda con el punto más alejado que encontró.
            for (int attempt = 0; attempt < 60; attempt++)
            {
                float x = RandomRange(rng, -SpawnArea, SpawnArea);
                float y = RandomRange(rng, -SpawnArea, SpawnArea);
                float minDist = MinDistanceTo(x, y, xs, ys, i);

                if (minDist > bestDist)
                {
                    bestDist = minDist;
                    bestX = x;
                    bestY = y;
                }
                if (minDist >= MinDistanceBetweenPlayers) break;
            }

            xs[i] = Round2(bestX);
            ys[i] = Round2(bestY);
        }
    }

    public static void GenerateItems(Random rng, float[] itemX, float[] itemY, int[] itemT,
                                     float[] spawnX, float[] spawnY, int spawnCount)
    {
        // 1) Lista de tipos con cantidades fijas, luego se baraja
        List<int> types = new List<int>();
        for (int i = 0; i < SpeedItems; i++) types.Add(TypeSpeed);
        for (int i = 0; i < FreezeItems; i++) types.Add(TypeFreeze);
        while (types.Count < TotalItems) types.Add(TypeNormal);

        for (int i = types.Count - 1; i > 0; i--)
        {
            int j = rng.Next(i + 1);
            int tmp = types[i];
            types[i] = types[j];
            types[j] = tmp;
        }

        // 2) Posiciones separadas entre sí y de los pollos
        for (int i = 0; i < TotalItems; i++)
        {
            float x = 0f, y = 0f;
            for (int attempt = 0; attempt < 40; attempt++)
            {
                x = RandomRange(rng, -ItemArea, ItemArea);
                y = RandomRange(rng, -ItemArea, ItemArea);

                bool farFromItems = MinDistanceTo(x, y, itemX, itemY, i) >= MinDistanceBetweenItems;
                bool farFromPlayers = MinDistanceTo(x, y, spawnX, spawnY, spawnCount) >= MinDistanceItemToPlayer;
                if (farFromItems && farFromPlayers) break;
            }

            itemX[i] = Round2(x);
            itemY[i] = Round2(y);
            itemT[i] = types[i];
        }
    }

    // Nueva posición para un maíz recogido: lejos de los pollos (para que no lo vuelvan a coger
    // al instante) y separada de los demás maíces.
    public const float MinRespawnDistanceToPlayer = 10f;

    public static void RandomRespawnPosition(Random rng, int itemIndex, float[] itemX, float[] itemY, int itemCount,
                                             float[] playerX, float[] playerY, int playerCount,
                                             out float x, out float y)
    {
        x = 0f; y = 0f;
        float bestScore = -1f;

        for (int attempt = 0; attempt < 40; attempt++)
        {
            float cx = RandomRange(rng, -ItemArea, ItemArea);
            float cy = RandomRange(rng, -ItemArea, ItemArea);

            float toPlayers = MinDistanceTo(cx, cy, playerX, playerY, playerCount);
            float toItems = float.MaxValue;
            for (int k = 0; k < itemCount; k++)
            {
                if (k == itemIndex) continue;
                float dx = itemX[k] - cx, dy = itemY[k] - cy;
                float d = (float)Math.Sqrt(dx * dx + dy * dy);
                if (d < toItems) toItems = d;
            }

            bool ok = toPlayers >= MinRespawnDistanceToPlayer && toItems >= MinDistanceBetweenItems;
            float score = Math.Min(toPlayers / MinRespawnDistanceToPlayer, toItems / MinDistanceBetweenItems);
            if (score > bestScore)
            {
                bestScore = score;
                x = cx;
                y = cy;
            }
            if (ok) break;
        }

        x = Round2(x);
        y = Round2(y);
    }

    private static float MinDistanceTo(float x, float y, float[] xs, float[] ys, int count)
    {
        float min = float.MaxValue;
        for (int k = 0; k < count; k++)
        {
            float dx = xs[k] - x;
            float dy = ys[k] - y;
            float d = (float)Math.Sqrt(dx * dx + dy * dy);
            if (d < min) min = d;
        }
        return min;
    }

    private static float RandomRange(Random rng, float min, float max)
    {
        return (float)(min + rng.NextDouble() * (max - min));
    }

    private static float Round2(float v)
    {
        return (float)Math.Round(v, 2);
    }
}
