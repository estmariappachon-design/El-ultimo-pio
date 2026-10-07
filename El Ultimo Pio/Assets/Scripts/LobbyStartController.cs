using UnityEngine;
using UnityEngine.UI;

public class LobbyStartController : MonoBehaviour
{
    [SerializeField] private Button startButton;

    private bool isHost = false;
    private int playerCount = 1;

    private void Start()
    {
        UpdateStartButton();
    }

    // Llama a esto cuando el jugador se conecte y el servidor determine si es Host
    public void SetHost(bool value)
    {
        isHost = value;
        UpdateStartButton();
    }

    // Llama a esto cuando ingrese o salga alguien del lobby
    public void SetPlayerCount(int count)
    {
        playerCount = count;
        UpdateStartButton();
    }

    private void UpdateStartButton()
    {
        if (startButton == null) return;

        // Solo se activa si es Host Y hay mínimo 2 jugadores
        startButton.interactable = isHost && playerCount >= 2;
    }

    // Método para conectar al evento OnClick del botón EMPERZAR
    public void OnStartButtonClicked()
    {
        Debug.Log("¡Iniciando partida!");
        // Aquí cargas la escena de juego, ej: SceneManager.LoadScene("Game");
    }
}