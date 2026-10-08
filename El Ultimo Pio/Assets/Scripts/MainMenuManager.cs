using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;

public class MainMenuManager : MonoBehaviour
{
    [Header("Escenas")]
    [SerializeField] private string lobbySceneName = "Lobby";

    [Header("UI Unirse a Partida (Opcional)")]
    [SerializeField] private GameObject joinPanel; // Panel emergente con el campo de IP
    [SerializeField] private TMP_InputField ipInputField; // Campo para escribir la IP
    [SerializeField] private string defaultIP = "127.0.0.1";

    private void Start()
    {
        if (joinPanel != null)
            joinPanel.SetActive(false);
    }

    // --- BOTÓN 1: CREAR PARTIDA ---
    public void OnClickCrearPartida()
    {
        // Obtiene automáticamente la IP del Wi-Fi del celular del Host
        string localIP = IPManager.GetLocalIPAddress();
        SetServerIPAndLoadLobby(localIP);
    }

    // --- BOTÓN 2: UNIRSE A PARTIDA ---
    public void OnClickUnirseAPartida()
    {
        if (joinPanel != null)
        {
            // Si tienes un panel con un InputField para poner la IP del Host
            joinPanel.SetActive(true);
        }
        else
        {
            // Si juegan en la misma red/máquina sin pedir IP
            SetServerIPAndLoadLobby(defaultIP);
        }
    }

    // Confirmar IP desde el panel emergente (Botón "Conectar" dentro del panel)
    public void OnConfirmJoinIP()
    {
        string ip = defaultIP;
        if (ipInputField != null && !string.IsNullOrWhiteSpace(ipInputField.text))
        {
            ip = ipInputField.text.Trim();
        }

        SetServerIPAndLoadLobby(ip);
    }

    public void OnCancelJoin()
    {
        if (joinPanel != null)
            joinPanel.SetActive(false);
    }

    private void SetServerIPAndLoadLobby(string ip)
    {
        // 1. Asegurar que SocketClient existe y asignar la IP objetivo
        if (SocketClient.Instance == null)
        {
            GameObject sc = new GameObject("SocketClient");
            sc.AddComponent<SocketClient>();
        }

        SocketClient.Instance.serverIP = ip;

        // 2. Cargar la escena del Lobby
        SceneManager.LoadScene(lobbySceneName);
    }

    // --- BOTÓN 3: SALIR ---
    public void OnClickSalir()
    {
        Application.Quit();
        Debug.Log("Saliendo del juego...");
    }
}