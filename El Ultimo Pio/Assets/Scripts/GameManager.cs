using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance;

    [Header("Modo de juego")]
    [Tooltip("Marcado: los maíces se recogen localmente, sin servidor. Desmarcar cuando la red esté lista.")]
    [SerializeField] private bool offlineMode = true;

    [Header("Prefabs de Maíces")]
    [SerializeField] private GameObject maizNormalPrefab;
    [SerializeField] private GameObject maizVelocidadPrefab;
    [SerializeField] private GameObject maizCongelarPrefab;

    [Header("UI Gameplay")]
    [SerializeField] private TextMeshProUGUI timerText;
    [SerializeField] private TextMeshProUGUI scoreText;

    [Header("Botones de Power-Up")]
    [SerializeField] private Button btnTurbo;
    [SerializeField] private Button btnCongelar;
    [SerializeField] private Color buttonOnColor = Color.white;                       // botón con power-up
    [SerializeField] private Color buttonOffColor = new Color(0.35f, 0.35f, 0.35f, 0.5f); // botón sin power-up

    [Header("Turbo")]
    [SerializeField] private float turboDuration = 5f;

    [Header("Hielo")]
    [SerializeField] private float freezeDuration = 4f;    // segundos que quedan congelados los rivales
    [SerializeField] private int freezeBonusPoints = 2;    // puntos EXTRA que gana quien activa el hielo
    [SerializeField] private int freezePenaltyPoints = 2;  // puntos que pierden los rivales

    private int myScore = 0;
    private int turboCharges = 0;
    private int freezeCharges = 0;
    private float gameTime = 120f;
    private bool gameActive = true;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    private void Start()
    {
        if (btnTurbo != null) btnTurbo.onClick.AddListener(ActivateTurbo);
        if (btnCongelar != null) btnCongelar.onClick.AddListener(ActivateFreeze);

        UpdateScoreUI();
        RefreshPowerUpButtons();
        SpawnAllItems();
    }

    private void Update()
    {
        if (!gameActive) return;

        gameTime -= Time.deltaTime;
        if (timerText != null)
            timerText.text = "Tiempo: " + Mathf.Max(0, Mathf.CeilToInt(gameTime)) + "s";

        if (gameTime <= 0)
        {
            gameActive = false;
            Debug.Log("¡Fin de la partida! Puntaje final: " + myScore);
        }
    }

    private void UpdateScoreUI()
    {
        if (scoreText != null) scoreText.text = "Puntos: " + myScore;
    }

    // Los botones solo se pueden presionar si el jugador tiene el power-up, y se ven encendidos o apagados
    private void RefreshPowerUpButtons()
    {
        SetButtonState(btnTurbo, turboCharges > 0);
        SetButtonState(btnCongelar, freezeCharges > 0);
    }

    private void SetButtonState(Button btn, bool active)
    {
        if (btn == null) return;

        btn.interactable = active;
        btn.transition = Selectable.Transition.None; // el color lo controlamos nosotros

        if (btn.targetGraphic != null)
            btn.targetGraphic.color = active ? buttonOnColor : buttonOffColor;

        btn.transform.localScale = active ? Vector3.one : Vector3.one * 0.9f;
    }

    private int GetMySlot()
    {
        return LobbyNetworkManager.Instance != null ? LobbyNetworkManager.Instance.MySlot : 0;
    }

    // Generación aleatoria de los 50 maíces en el terreno
    private void SpawnAllItems()
    {
        if (maizNormalPrefab == null)
        {
            Debug.LogError("GameManager: falta asignar el prefab de Maíz Normal.");
            return;
        }

        int totalItems = 50;

        for (int i = 0; i < totalItems; i++)
        {
            Vector3 spawnPos = new Vector3(Random.Range(-45f, 45f), Random.Range(-45f, 45f), 0);

            // 70% Normal, 15% Velocidad, 15% Congelar
            GameObject selectedPrefab = maizNormalPrefab;
            ItemType selectedType = ItemType.Normal;
            float rand = Random.value;

            if (rand > 0.85f && maizCongelarPrefab != null)
            {
                selectedPrefab = maizCongelarPrefab;
                selectedType = ItemType.Freeze;
            }
            else if (rand > 0.70f && maizVelocidadPrefab != null)
            {
                selectedPrefab = maizVelocidadPrefab;
                selectedType = ItemType.SpeedBoost;
            }

            GameObject itemObj = Instantiate(selectedPrefab, spawnPos, Quaternion.identity);
            CollectibleItem itemScript = itemObj.GetComponent<CollectibleItem>();
            if (itemScript == null) itemScript = itemObj.AddComponent<CollectibleItem>();

            itemScript.itemID = i;
            itemScript.type = selectedType; // El tipo lo fija el código, no el prefab
        }
    }

    // Cuando el pollo toca un maíz
    public void SendPickupRequest(int id, ItemType type)
    {
        if (!gameActive) return;

        // Modo local: se resuelve aquí mismo, sin servidor
        if (offlineMode || SocketClient.Instance == null)
        {
            OnItemPickedUp(id, GetMySlot(), type);
            return;
        }

        PickupPayload payload = new PickupPayload
        {
            playerSlot = GetMySlot(),
            itemID = id,
            itemType = type.ToString()
        };
        SocketClient.Instance.SendMessageToServer("PICKUP_ITEM", payload);
    }

    // Resultado de quién recogió el maíz (local o confirmado por el servidor)
    public void OnItemPickedUp(int itemID, int winnerSlot, ItemType type)
    {
        CollectibleItem[] items = FindObjectsByType<CollectibleItem>(FindObjectsInactive.Exclude);
        foreach (var item in items)
        {
            if (item.itemID != itemID) continue;

            if (winnerSlot == GetMySlot())
            {
                // Los 50 maíces suman 1 punto, sin importar el tipo
                myScore++;
                UpdateScoreUI();

                // Los especiales además se guardan para activarlos con su botón
                if (type == ItemType.SpeedBoost) turboCharges++;
                else if (type == ItemType.Freeze) freezeCharges++;

                RefreshPowerUpButtons();
            }

            Destroy(item.gameObject);
            break;
        }
    }

    // ---------- Botones ----------

    public void ActivateTurbo()
    {
        if (!gameActive || turboCharges <= 0) return;

        turboCharges--;
        RefreshPowerUpButtons();

        PlayerController myPlayer = FindLocalPlayer();
        if (myPlayer != null) myPlayer.ApplySpeedBoost(turboDuration);
    }

    public void ActivateFreeze()
    {
        if (!gameActive || freezeCharges <= 0) return;

        freezeCharges--;
        myScore += freezeBonusPoints; // Ventaja para quien congela
        UpdateScoreUI();
        RefreshPowerUpButtons();

        // Avisa a los demás jugadores (en modo local no hay nadie más)
        if (!offlineMode && SocketClient.Instance != null)
        {
            PowerUpPayload payload = new PowerUpPayload
            {
                senderSlot = GetMySlot(),
                powerUpType = "FREEZE"
            };
            SocketClient.Instance.SendMessageToServer("USE_POWERUP", payload);
        }
    }

    // Compatibilidad: si el BtnCongelar aún tiene este método en su OnClick, funciona igual
    public void TryFreezeTargetPlayer(int targetSlot)
    {
        ActivateFreeze();
    }

    // El servidor avisa que un jugador usó un power-up (lo llamará SocketClient cuando hagamos la red)
    public void OnPowerUpUsed(int senderSlot, string powerUpType)
    {
        if (senderSlot == GetMySlot()) return;

        if (powerUpType == "FREEZE") OnFrozenByRival();
    }

    private void OnFrozenByRival()
    {
        myScore = Mathf.Max(0, myScore - freezePenaltyPoints);
        UpdateScoreUI();

        PlayerController myPlayer = FindLocalPlayer();
        if (myPlayer != null) myPlayer.FreezePlayer(freezeDuration);
    }

    // ---------- Pruebas sin red (clic derecho en el componente GameManager, en Play) ----------

    [ContextMenu("PRUEBA: darme 1 turbo y 1 hielo")]
    private void TestGivePowerUps()
    {
        turboCharges++;
        freezeCharges++;
        RefreshPowerUpButtons();
    }

    [ContextMenu("PRUEBA: simular que un rival me congela")]
    private void TestFrozenByRival()
    {
        OnFrozenByRival();
    }

    private PlayerController FindLocalPlayer()
    {
        PlayerController[] players = FindObjectsByType<PlayerController>(FindObjectsInactive.Exclude);
        foreach (var p in players)
            if (p.isLocalPlayer) return p;
        return null;
    }
}