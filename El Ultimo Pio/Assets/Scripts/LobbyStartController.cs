using UnityEngine;
using UnityEngine.UI;
using TMPro;

// Controla si el botón EMPEZAR se puede pulsar: solo el host y con 2 a 4 jugadores.
// El clic se conecta en el Inspector (OnClick de BtnEmpezar -> LobbyNetworkManager.OnStartGamePressed).
public class LobbyStartController : MonoBehaviour
{
    [SerializeField] private Button startButton;               // BtnEmpezar
    [SerializeField] private TextMeshProUGUI waitingText;      // opcional: "Esperando jugadores..."

    private bool isHost = false;
    private int playerCount = 1;

    public bool CanStart => isHost && playerCount >= GameSession.MinPlayers && playerCount <= GameServer.MaxPlayers;

    private void Start()
    {
        UpdateStartButton();
    }

    public void SetHost(bool value)
    {
        isHost = value;
        UpdateStartButton();
    }

    public void SetPlayerCount(int count)
    {
        playerCount = count;
        UpdateStartButton();
    }

    private void UpdateStartButton()
    {
        if (startButton != null)
        {
            // Solo el host ve el botón; los demás esperan a que él empiece
            startButton.gameObject.SetActive(isHost);
            startButton.interactable = CanStart;
        }

        if (waitingText != null)
        {
            if (!isHost)
                waitingText.text = "Esperando a que el anfitrión inicie...";
            else if (playerCount < GameSession.MinPlayers)
                waitingText.text = "Esperando jugadores (mínimo " + GameSession.MinPlayers + ")...";
            else
                waitingText.text = "¡Listos! Pulsa EMPEZAR";
        }
    }
}
