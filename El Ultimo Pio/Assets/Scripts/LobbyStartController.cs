using UnityEngine;
using UnityEngine.UI;

public class LobbyStartController : MonoBehaviour
{
    [SerializeField] private Button startButton; // El botón EMPEZAR (BtnEmpezar)

    private bool isHost = false;
    private int playerCount = 1;

    private void Start()
    {
        if (startButton != null)
        {
            // El clic se conecta por código, no hace falta ponerlo en el OnClick del Inspector
            startButton.onClick.RemoveListener(OnStartButtonClicked);
            startButton.onClick.AddListener(OnStartButtonClicked);
        }

        UpdateStartButton();
    }

    // Lo llama LobbyNetworkManager cuando el servidor dice si este jugador es Host
    public void SetHost(bool value)
    {
        isHost = value;
        UpdateStartButton();
    }

    // Lo llama LobbyNetworkManager cuando entra o sale alguien del lobby
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

    public void OnStartButtonClicked()
    {
        if (!isHost || playerCount < 2) return;

        Debug.Log("¡Iniciando partida!");

        // Le pide al servidor que inicie: el servidor avisa a TODOS y cada uno carga la escena de juego
        LobbyNetworkManager.Instance?.OnStartGamePressed();
    }
}