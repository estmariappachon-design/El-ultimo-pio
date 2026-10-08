using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance;

    private const int TotalItems = 50;
    private const float OfflineGameTime = 120f;

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
    [SerializeField] private Color buttonOnColor = Color.white;                            // botón con power-up
    [SerializeField] private Color buttonOffColor = new Color(0.35f, 0.35f, 0.35f, 0.5f);  // botón sin power-up

    [Header("Turbo")]
    [SerializeField] private float turboDuration = 5f;

    [Header("Hielo")]
    [SerializeField] private float freezeDuration = 4f;    // segundos que quedan congelados los rivales
    [SerializeField] private int freezeBonusPoints = 2;    // puntos EXTRA que gana quien activa el hielo
    [SerializeField] private int freezePenaltyPoints = 2;  // puntos que pierden los rivales

    [Header("Pollos (MISMO ORDEN que en el lobby)")]
    [SerializeField] private Sprite[] chickenSprites;

    [Header("Escenas")]
    [SerializeField] private string lobbySceneName = "Lobby";

    [Header("Multijugador")]
    [SerializeField] private Vector2[] startPositions =
    {
        new Vector2(-6f, -6f), new Vector2(6f, -6f), new Vector2(-6f, 6f), new Vector2(6f, 6f)
    };
    [SerializeField] private Color[] playerColors =
    {
        Color.yellow, Color.cyan, Color.green, new Color(1f, 0.5f, 0f)
    }; // Solo se usan si no hay sprites de pollos asignados

    private int myScore = 0;
    private int turboCharges = 0;
    private int freezeCharges = 0;
    private float gameTime = OfflineGameTime;
    private bool gameActive = false;
    private bool gameOverHandled = false;

    private PlayerController localPlayer;
    private readonly Dictionary<int, CollectibleItem> items = new Dictionary<int, CollectibleItem>();
    private readonly Dictionary<int, RemotePlayer> remotePlayers = new Dictionary<int, RemotePlayer>();
    private float moveSendTimer = 0f;
    private Vector2 lastSentPos;

    // En línea = hay un servidor conectado. Si no, se juega en modo local (solo tú).
    private bool IsOnline => SocketClient.Instance != null && SocketClient.Instance.IsConnected;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    private void Start()
    {
        if (btnTurbo != null) btnTurbo.onClick.AddListener(ActivateTurbo);
        if (btnCongelar != null) btnCongelar.onClick.AddListener(ActivateFreeze);

        localPlayer = FindLocalPlayer();
        if (localPlayer == null)
        {
            Debug.LogError("[GM] No encontré el pollo local (un PlayerController con Is Local Player activo) en la escena.");
        }
        else
        {
            localPlayer.playerSlot = GetMySlot();

            Vector2 start = StartPosition(GetMySlot());
            localPlayer.transform.position = new Vector3(start.x, start.y, 0f);
            lastSentPos = start;

            // APLICAR SKIN AL JUGADOR LOCAL:
            ApplySkin(localPlayer.gameObject, GetMySlot(), false);
        }

        UpdateScoreUI();
        RefreshPowerUpButtons();
        SpawnAllItems();
        IgnoreSolidColliders();
        SpawnRemotePlayers();

        string slotsInfo = GameSession.Slots == null ? "ninguno" : string.Join(",", GameSession.Slots);
        Debug.Log("[GM] enLinea=" + IsOnline + " | miSlot=" + GetMySlot() +
                  " | slotsEnPartida=" + slotsInfo +
                  " | spritesAsignados=" + (chickenSprites == null ? 0 : chickenSprites.Length) +
                  " | pollosRemotos=" + remotePlayers.Count);
        Invoke(nameof(LogPlayerPosition), 3f);

        if (IsOnline)
        {
            // En línea: todos esperan quieto hasta que el servidor diga "ya" (GAME_BEGIN)
            SetLocalPlayerLocked(true);
            if (timerText != null) timerText.text = "Esperando jugadores...";

            SocketClient.Instance.SendMessageToServer("GAME_READY", new object());

            // Si el servidor ya había dicho "ya" mientras cargaba la escena
            if (GameSession.BeginPending)
            {
                GameSession.BeginPending = false;
                OnGameBegin(GameSession.BeginDuration);
            }
        }
        else
        {
            gameActive = true; // Modo local: arranca de inmediato
        }
    }

    // CÓDIGO CON FORMATO MINUTOS Y SEGUNDOS:
    private void Update()
    {
        if (!gameActive) return;

        gameTime -= Time.deltaTime;

        if (timerText != null)
        {
            int totalSeconds = Mathf.Max(0, Mathf.CeilToInt(gameTime));
            int minutes = totalSeconds / 60; // Calcula los minutos
            int seconds = totalSeconds % 60; // Calcula los segundos restantes

            // El formato D2 fuerza a que los segundos siempre tengan 2 dígitos (ej. 02 en vez de 2)
            timerText.text = string.Format("{0}:{1:D2}", minutes, seconds);
        }

        if (gameTime <= 0f)
        {
            gameTime = 0f;
            if (!IsOnline) OnGameOver();
        }

        SendMyPosition();
    }

    // ---------- Utilidades ----------

    private void UpdateScoreUI()
    {
        if (scoreText != null)
            scoreText.text = string.Format("{0}/{1}", myScore, TotalItems);
    }

    private int GetMySlot()
    {
        return GameSession.MySlot;
    }

    private Vector2 StartPosition(int slot)
    {
        if (startPositions == null || startPositions.Length == 0) return Vector2.zero;
        return startPositions[Mathf.Abs(slot) % startPositions.Length];
    }

    private PlayerController FindLocalPlayer()
    {
        PlayerController[] players = FindObjectsByType<PlayerController>(FindObjectsInactive.Exclude);
        foreach (var p in players)
            if (p.isLocalPlayer) return p;
        return null;
    }

    // El pollo solo debe recoger maíces (que son triggers): no puede chocar con el terreno ni con nada sólido.
    // Si el terreno tiene un collider sólido, empujaba al pollo fuera del mapa y por eso "desaparecía".
    private void IgnoreSolidColliders()
    {
        if (localPlayer == null) return;

        Collider2D myCollider = localPlayer.GetComponent<Collider2D>();
        if (myCollider == null) return;

        List<string> ignored = new List<string>();
        Collider2D[] all = FindObjectsByType<Collider2D>(FindObjectsInactive.Exclude);
        foreach (Collider2D other in all)
        {
            if (other == myCollider || other.isTrigger) continue;
            Physics2D.IgnoreCollision(myCollider, other);
            ignored.Add(other.gameObject.name);
        }

        Debug.Log("[GM] Colliders sólidos que el pollo ignora: " + ignored.Count +
                  (ignored.Count > 0 ? " (" + string.Join(", ", ignored) + ")" : ""));
    }

    private void LogPlayerPosition()
    {
        if (localPlayer == null) return;

        SpriteRenderer sr = localPlayer.GetComponent<SpriteRenderer>();
        Debug.Log("[GM] Posición del pollo a los 3 s: " + localPlayer.transform.position +
                  " | sprite=" + (sr != null && sr.sprite != null ? sr.sprite.name : "NINGUNO") +
                  " | visible=" + (sr != null && sr.enabled));
    }

    // Bloquea o libera el movimiento del pollo local (antes de empezar y al terminar)
    private void SetLocalPlayerLocked(bool locked)
    {
        if (localPlayer == null) return;

        localPlayer.enabled = !locked;
        if (locked)
        {
            Rigidbody2D rb = localPlayer.GetComponent<Rigidbody2D>();
            if (rb != null) rb.linearVelocity = Vector2.zero;
        }
    }

    // Pone el sprite del pollo que ese jugador eligió en el lobby
    private void ApplySkin(GameObject pollo, int slot, bool tintIfNoSprite)
    {
        SpriteRenderer sr = pollo.GetComponent<SpriteRenderer>();
        if (sr == null) return;

        // Los pollos siempre se dibujan por encima del terreno y de los maíces
        sr.enabled = true;
        sr.sortingOrder = 100;

        // Lee el pollo guardado desde la GameSession
        int idx = GameSession.ChickenOf(slot);
        bool hasSprite = chickenSprites != null && idx >= 0 && idx < chickenSprites.Length && chickenSprites[idx] != null;

        if (hasSprite)
        {
            sr.sprite = chickenSprites[idx];
            sr.color = Color.white; // Mantiene los colores originales del Sprite sin tinte
        }
        else if (tintIfNoSprite && playerColors != null && playerColors.Length > 0)
        {
            sr.color = playerColors[Mathf.Abs(slot) % playerColors.Length];
        }
    }

    // ---------- Inicio y final de la partida ----------

    // El servidor avisa que todos están listos: empieza el reloj para todos a la vez
    public void OnGameBegin(float duration)
    {
        gameTime = duration > 0f ? duration : OfflineGameTime;
        gameActive = true;
        SetLocalPlayerLocked(false);
    }

    // Se acabó el tiempo (lo llama el servidor en línea, o el reloj local en modo local)
    public void OnGameOver()
    {
        if (gameOverHandled) return;
        gameOverHandled = true;

        gameActive = false;
        SetLocalPlayerLocked(true);
        if (timerText != null) timerText.text = "FIN DEL JUEGO";

        if (IsOnline)
        {
            // Mandamos nuestro puntaje; el servidor reúne los de todos y responde con RESULTS
            ScorePayload payload = new ScorePayload { playerSlot = GetMySlot(), score = myScore };
            SocketClient.Instance.SendMessageToServer("FINAL_SCORE", payload);
        }
        else
        {
            ShowResults(new[] { GetMySlot() }, new[] { myScore });
        }
    }

    public void OnResults(int[] slots, int[] scores)
    {
        if (slots == null || scores == null) return;
        ShowResults(slots, scores);
    }

    private void ShowResults(int[] slots, int[] scores)
    {
        List<KeyValuePair<int, int>> ranking = new List<KeyValuePair<int, int>>();
        for (int i = 0; i < slots.Length && i < scores.Length; i++)
            ranking.Add(new KeyValuePair<int, int>(slots[i], scores[i]));
        ranking.Sort((a, b) => b.Value.CompareTo(a.Value));

        StringBuilder sb = new StringBuilder("FIN DEL JUEGO\n\n");
        for (int i = 0; i < ranking.Count; i++)
        {
            string you = ranking[i].Key == GetMySlot() ? "  (tu)" : "";
            sb.AppendLine((i + 1) + ". Jugador " + (ranking[i].Key + 1) + " - " + ranking[i].Value + " pts" + you);
        }

        BuildResultsPanel(sb.ToString());
    }

    // Crea por código un panel oscuro con el ranking, encima de toda la interfaz
    private void BuildResultsPanel(string content)
    {
        Canvas canvas = scoreText != null ? scoreText.canvas : FindAnyObjectByType<Canvas>();
        if (canvas == null)
        {
            Debug.Log(content);
            return;
        }
        canvas = canvas.rootCanvas;

        GameObject panel = new GameObject("PanelResultados", typeof(RectTransform), typeof(Image));
        panel.transform.SetParent(canvas.transform, false);
        panel.transform.SetAsLastSibling();

        RectTransform rt = panel.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        panel.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.85f);

        GameObject textObj = new GameObject("TextoResultados", typeof(RectTransform));
        textObj.transform.SetParent(panel.transform, false);

        RectTransform trt = textObj.GetComponent<RectTransform>();
        trt.anchorMin = Vector2.zero;
        trt.anchorMax = Vector2.one;
        trt.offsetMin = new Vector2(40f, 220f);
        trt.offsetMax = new Vector2(-40f, -40f);

        TextMeshProUGUI tmp = textObj.AddComponent<TextMeshProUGUI>();
        tmp.text = content;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = Color.white;
        tmp.enableAutoSizing = true;
        tmp.fontSizeMin = 20f;
        tmp.fontSizeMax = 60f;

        // Botón para volver al lobby
        GameObject btnObj = new GameObject("BtnVolverLobby", typeof(RectTransform), typeof(Image), typeof(Button));
        btnObj.transform.SetParent(panel.transform, false);

        RectTransform brt = btnObj.GetComponent<RectTransform>();
        brt.anchorMin = new Vector2(0.5f, 0f);
        brt.anchorMax = new Vector2(0.5f, 0f);
        brt.pivot = new Vector2(0.5f, 0f);
        brt.anchoredPosition = new Vector2(0f, 60f);
        brt.sizeDelta = new Vector2(420f, 110f);

        Image btnImage = btnObj.GetComponent<Image>();
        btnImage.color = new Color(0.8f, 0.5f, 0.25f, 1f);

        Button backButton = btnObj.GetComponent<Button>();
        backButton.targetGraphic = btnImage;
        backButton.onClick.AddListener(BackToLobby);

        GameObject labelObj = new GameObject("Texto", typeof(RectTransform));
        labelObj.transform.SetParent(btnObj.transform, false);

        RectTransform lrt = labelObj.GetComponent<RectTransform>();
        lrt.anchorMin = Vector2.zero;
        lrt.anchorMax = Vector2.one;
        lrt.offsetMin = Vector2.zero;
        lrt.offsetMax = Vector2.zero;

        TextMeshProUGUI label = labelObj.AddComponent<TextMeshProUGUI>();
        label.text = "Volver al lobby";
        label.alignment = TextAlignmentOptions.Center;
        label.color = Color.white;
        label.fontSize = 40f;
    }

    private void BackToLobby()
    {
        UnityEngine.SceneManagement.SceneManager.LoadScene(lobbySceneName);
    }

    // ---------- Botones de power-up ----------

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

    // ---------- Maíces ----------

    // Todos los jugadores usan la misma semilla, así que generan EXACTAMENTE los mismos 50 maíces
    private void SpawnAllItems()
    {
        if (maizNormalPrefab == null)
        {
            Debug.LogError("GameManager: falta asignar el prefab de Maíz Normal.");
            return;
        }

        int seed = GameSession.HasSeed ? GameSession.Seed : System.Environment.TickCount;
        System.Random rng = new System.Random(seed);

        for (int i = 0; i < TotalItems; i++)
        {
            float x = (float)(rng.NextDouble() * 90.0 - 45.0);
            float y = (float)(rng.NextDouble() * 90.0 - 45.0);
            double rand = rng.NextDouble();

            // 70% Normal, 15% Velocidad, 15% Congelar
            GameObject selectedPrefab = maizNormalPrefab;
            ItemType selectedType = ItemType.Normal;

            if (rand > 0.85 && maizCongelarPrefab != null)
            {
                selectedPrefab = maizCongelarPrefab;
                selectedType = ItemType.Freeze;
            }
            else if (rand > 0.70 && maizVelocidadPrefab != null)
            {
                selectedPrefab = maizVelocidadPrefab;
                selectedType = ItemType.SpeedBoost;
            }

            GameObject itemObj = Instantiate(selectedPrefab, new Vector3(x, y, 0f), Quaternion.identity);
            CollectibleItem itemScript = itemObj.GetComponent<CollectibleItem>();
            if (itemScript == null) itemScript = itemObj.AddComponent<CollectibleItem>();

            itemScript.itemID = i;
            itemScript.type = selectedType;
            items[i] = itemScript;
        }
    }

    // Cuando el pollo local toca un maíz
    public void SendPickupRequest(int id, ItemType type)
    {
        if (!gameActive) return;

        // Modo local: se resuelve aquí mismo
        if (!IsOnline)
        {
            OnItemPickedUp(id, GetMySlot(), type);
            return;
        }

        // En línea: el servidor decide quién llegó primero
        PickupPayload payload = new PickupPayload
        {
            playerSlot = GetMySlot(),
            itemID = id,
            itemType = type.ToString()
        };
        SocketClient.Instance.SendMessageToServer("PICKUP_ITEM", payload);
    }

    // Resultado final de quién recogió el maíz (lo llaman el modo local y el servidor)
    public void OnItemPickedUp(int itemID, int winnerSlot, ItemType type)
    {
        if (!items.TryGetValue(itemID, out CollectibleItem item) || item == null) return;
        items.Remove(itemID);

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

        Destroy(item.gameObject); // desaparece en la pantalla de TODOS
    }

    // ---------- Activación de power-ups ----------

    public void ActivateTurbo()
    {
        if (!gameActive || turboCharges <= 0) return;

        turboCharges--;
        RefreshPowerUpButtons();

        if (localPlayer != null) localPlayer.ApplySpeedBoost(turboDuration);
    }

    public void ActivateFreeze()
    {
        if (!gameActive || freezeCharges <= 0) return;

        freezeCharges--;
        myScore += freezeBonusPoints; // Ventaja para quien congela
        UpdateScoreUI();
        RefreshPowerUpButtons();

        if (IsOnline)
        {
            PowerUpPayload payload = new PowerUpPayload
            {
                senderSlot = GetMySlot(),
                targetSlot = -1,
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

    // El servidor avisa que otro jugador usó un power-up
    public void OnPowerUpUsed(int senderSlot, string powerUpType)
    {
        if (!gameActive || senderSlot == GetMySlot()) return;

        if (powerUpType == "FREEZE") OnFrozenByRival();
    }

    private void OnFrozenByRival()
    {
        myScore = Mathf.Max(0, myScore - freezePenaltyPoints);
        UpdateScoreUI();

        if (localPlayer != null) localPlayer.FreezePlayer(freezeDuration);
    }

    // ---------- Otros jugadores ----------

    private void SpawnRemotePlayers()
    {
        if (!IsOnline || GameSession.Slots == null || localPlayer == null) return;

        foreach (int slot in GameSession.Slots)
        {
            if (slot == GetMySlot()) continue;
            CreateRemotePlayer(slot);
        }
    }

    private void CreateRemotePlayer(int slot)
    {
        Vector2 pos = StartPosition(slot);

        GameObject go = Instantiate(localPlayer.gameObject, pos, Quaternion.identity);
        go.name = "Pollo_Remoto_" + slot;

        PlayerController pc = go.GetComponent<PlayerController>();
        if (pc != null)
        {
            pc.isLocalPlayer = false;
            pc.playerSlot = slot;
            pc.enabled = false;
        }

        Rigidbody2D rb = go.GetComponent<Rigidbody2D>();
        if (rb != null) rb.bodyType = RigidbodyType2D.Kinematic;

        Collider2D col = go.GetComponent<Collider2D>();
        if (col != null) col.isTrigger = true;

        // Aplicar la skin del pollo según la elección guardada en GameSession
        ApplySkin(go, slot, true);

        RemotePlayer rp = go.AddComponent<RemotePlayer>();
        rp.Init(pos);
        remotePlayers[slot] = rp;
    }

    // Envía mi posición unas 20 veces por segundo, solo si me moví
    private void SendMyPosition()
    {
        if (!IsOnline || localPlayer == null) return;

        moveSendTimer += Time.deltaTime;
        if (moveSendTimer < 0.05f) return;
        moveSendTimer = 0f;

        Vector2 pos = localPlayer.transform.position;
        if ((pos - lastSentPos).sqrMagnitude < 0.0001f) return;
        lastSentPos = pos;

        MovePayload payload = new MovePayload { playerSlot = GetMySlot(), posX = pos.x, posY = pos.y };
        SocketClient.Instance.SendMessageToServer("MOVE", payload);
    }

    public void OnRemotePlayerMoved(int slot, Vector2 position)
    {
        if (slot == GetMySlot()) return;
        if (remotePlayers.TryGetValue(slot, out RemotePlayer rp) && rp != null)
            rp.SetTarget(position);
    }

    public void OnPlayerLeft(int slot)
    {
        if (remotePlayers.TryGetValue(slot, out RemotePlayer rp))
        {
            if (rp != null) Destroy(rp.gameObject);
            remotePlayers.Remove(slot);
        }
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
}