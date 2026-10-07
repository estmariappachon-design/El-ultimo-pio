using UnityEngine;

public class LobbyManager : MonoBehaviour
{
    [SerializeField] private LobbyStartController startController;
    [SerializeField] private LobbyCustomization customizationController;

    // Métodos para simular o recibir datos de la red
    public void OnPlayerJoined(int currentTotalPlayers, bool isLocalHost, int localSlot)
    {
        // 1. Actualiza qué nidos están visibles según la cantidad de jugadores
        if (customizationController != null)
        {
            customizationController.UpdateConnectedPlayers(currentTotalPlayers);
            customizationController.SetLocalPlayerSlot(localSlot);
        }

        // 2. Actualiza el estado del botón EMPERZAR
        if (startController != null)
        {
            startController.SetHost(isLocalHost);
            startController.SetPlayerCount(currentTotalPlayers);
        }
    }
}