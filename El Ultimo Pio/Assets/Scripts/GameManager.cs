using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// =====================================================================================
//  GAME MANAGER (escena Gameplay)
//  - Crea los 50 maíces y coloca a cada pollo en su posición aleatoria (datos del servidor)
//  - Envía la posición del pollo local y mueve a los pollos remotos
//  - Pide al servidor recoger maíces y usar power-ups; el servidor decide y responde
//  - Mantiene el HUD: tiempo, puntaje, maíces, ranking, cargas de power-ups y estado
//  - Muestra la pantalla de resultados con ganador / empate
// =====================================================================================
public class GameManager : MonoBehaviour
{
    public static GameManager Instance;

    [Header("Prefabs de Maíces")]
    [SerializeField] private GameObject maizNormalPrefab;
    [SerializeField] private GameObject maizVelocidadPrefab;
    [SerializeField] private GameObject maizCongelarPrefab;

    [Header("UI Gameplay (los opcionales se crean solos si se dejan vacíos)")]
    [SerializeField] private TextMeshProUGUI timerText;
    [SerializeField] private TextMeshProUGUI scoreText;
    [Tooltip("{0} = puntos, {1} = maíces recogidos")]
    [SerializeField] private string scoreFormat = "{0} pts  |  {1} maíces";
    [SerializeField] private TextMeshProUGUI rankingText;   // opcional (arriba a la derecha)
    [SerializeField] private TextMeshProUGUI statusText;    // opcional (centro, debajo del tiempo)

    [Header("Botones de Power-Up")]
    [SerializeField] private Button btnTurbo;
    [SerializeField] private Button btnCongelar;
    [SerializeField] private Color buttonOnColor = Color.white;
    [SerializeField] private Color buttonOffColor = new Color(0.35f, 0.35f, 0.35f, 0.5f);

    [Header("Pollos (MISMO ORDEN que en el lobby)")]
    [SerializeField] private Sprite[] chickenSprites;
    [SerializeField] private Color[] playerColors =
    {
        Color.yellow, Color.cyan, Color.green, new Color(1f, 0.5f, 0f)
    }; // solo se usan si no hay sprite asignado
    [SerializeField] private float nameLabelHeight = 4f;

    [Header("Escenas")]
    [SerializeField] private string lobbySceneName = "Lobby";
    [SerializeField] private string mainMenuSceneName = "MainMenu";

    [Header("Sonidos (opcionales)")]
    [SerializeField] private AudioClip pickupClip;   // maíz normal
    [SerializeField] private AudioClip specialClip;  // maíz especial
    [SerializeField] private AudioClip turboClip;    // activar turbo
    [SerializeField] private AudioClip freezeClip;   // lanzar hielo
    [SerializeField] private AudioClip frozenClip;   // me congelaron
    [SerializeField] private AudioClip endClip;      // fin de partida

    // ---------- Estado ----------
    private int myScore = 0;
    private int myItems = 0;
    private int turboCharges = 0;
    private int freezeCharges = 0;
    private float gameTime;
    private bool gameActive = false;
    private bool gameOverHandled = false;
    private float nextPowerUpRequest = 0f;
    private float statusUntil = 0f;

    private PlayerController localPlayer;
    private AudioSource audioSource;
    private TextMeshProUGUI turboBadge;
    private TextMeshProUGUI freezeBadge;

    private readonly Dictionary<int, CollectibleItem> items = new Dictionary<int, CollectibleItem>();
    private readonly Dictionary<int, RemotePlayer> remotePlayers = new Dictionary<int, RemotePlayer>();
    private readonly Dictionary<int, int> scoresBySlot = new Dictionary<int, int>();
    private readonly Dictionary<int, int> itemsBySlot = new Dictionary<int, int>();

    private float moveSendTimer = 0f;
    private Vector2 lastSentPos;

    private bool IsOnline => SocketClient.Instance != null && SocketClient.Instance.IsConnected;
    private int MySlot => GameSession.MySlot;

    // =================================================================================
    //  Inicio
    // =================================================================================

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void Start()
    {
        if (btnTurbo != null) btnTurbo.onClick.AddListener(ActivateTurbo);
        if (btnCongelar != null) btnCongelar.onClick.AddListener(ActivateFreeze);

        audioSource = GetComponent<AudioSource>();
        if (audioSource == null) audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;

        EnsureHud();

        localPlayer = FindLocalPlayer();
        if (localPlayer == null)
        {
            Debug.LogError("[GM] No hay un PlayerController con 'Is Local Player' activo en la escena.");
            return;
        }

        if (!IsOnline || !GameSession.HasMatch)
        {
            // Se le dio Play directamente a la escena Gameplay: hay que entrar desde el menú
            SetLocalPlayerLocked(true);
            ShowStatus("Sin conexión. Inicia la partida desde el Menú.", 10f, Color.red);
            Invoke(nameof(GoToMainMenu), 3f);
            return;
        }

        // ----- Pollo local -----
        ChickenAnimator myAnim = EnsureAnimator(localPlayer.gameObject);
        localPlayer.SetAnimator(myAnim);
        localPlayer.playerSlot = MySlot;

        Vector2 start = GameSession.SpawnOf(MySlot);
        localPlayer.transform.position = new Vector3(start.x, start.y, 0f);
        Rigidbody2D rb = localPlayer.GetComponent<Rigidbody2D>();
        if (rb != null) rb.position = start;
        lastSentPos = start;

        ApplySkin(localPlayer.gameObject, MySlot, false);

        // ----- Mundo -----
        SpawnAllItems();
        IgnoreSolidColliders();
        SpawnRemotePlayers();
        CreateNameLabel(localPlayer.gameObject, "TÚ", new Color(1f, 0.85f, 0.2f));

        foreach (int slot in GameSession.Slots)
        {
            scoresBySlot[slot] = 0;
            itemsBySlot[slot] = 0;
        }

        gameTime = GameSession.Duration;
        UpdateScoreUI();
        UpdateRanking();
        RefreshPowerUpButtons();

        // Todos esperan quietos hasta que el servidor diga "ya" (GAME_BEGIN)
        SetLocalPlayerLocked(true);
        if (timerText != null) timerText.text = "Esperando...";
        ShowStatus("Esperando a los demás jugadores...", 30f, Color.white);

        SocketClient.Instance.SendMessageToServer("GAME_READY", new EmptyPayload());

        if (GameSession.BeginPending)
        {
            GameSession.BeginPending = false;
            OnGameBegin(GameSession.BeginDuration);
        }
    }

    private void Update()
    {
        UpdateStatusText();

        if (!gameActive) return;

        gameTime = Mathf.Max(0f, gameTime - Time.deltaTime);

        if (timerText != null)
        {
            int totalSeconds = Mathf.CeilToInt(gameTime);
            timerText.text = string.Format("{0}:{1:D2}", totalSeconds / 60, totalSeconds % 60);
            timerText.color = totalSeconds <= 10 ? new Color(1f, 0.3f, 0.3f) : Color.white;
        }

        // El final lo decide el servidor (GAME_OVER); aquí solo se muestra el reloj
        SendMyPosition();
    }

    // =================================================================================
    //  Inicio y final (llamados por SocketClient)
    // =================================================================================

    public void OnGameBegin(float duration)
    {
        if (gameOverHandled) return;

        gameTime = duration > 0f ? duration : GameSession.Duration;
        gameActive = true;
        SetLocalPlayerLocked(false);
        ShowStatus("¡A recolectar!", 1.5f, new Color(1f, 0.85f, 0.2f));
    }

    public void OnGameOver()
    {
        if (gameOverHandled) return;
        gameOverHandled = true;

        gameActive = false;
        SetLocalPlayerLocked(true);
        if (timerText != null) timerText.text = "0:00";
        ShowStatus("¡TIEMPO!", 5f, new Color(1f, 0.3f, 0.3f));
        Play(endClip);
    }

    public void OnResults(ResultsPayload r)
    {
        if (r == null || r.slots == null) return;
        if (!gameOverHandled) OnGameOver();
        BuildResultsPanel(r);
    }

    // =================================================================================
    //  Maíces
    // =================================================================================

    private void SpawnAllItems()
    {
        if (maizNormalPrefab == null)
        {
            Debug.LogError("[GM] Falta asignar el prefab de Maíz Normal.");
            return;
        }
        if (GameSession.ItemX == null || GameSession.ItemY == null || GameSession.ItemT == null) return;

        int count = Mathf.Min(GameSession.ItemX.Length, Mathf.Min(GameSession.ItemY.Length, GameSession.ItemT.Length));
        for (int i = 0; i < count; i++)
        {
            ItemType type = (ItemType)GameSession.ItemT[i];

            GameObject prefab = maizNormalPrefab;
            if (type == ItemType.SpeedBoost && maizVelocidadPrefab != null) prefab = maizVelocidadPrefab;
            else if (type == ItemType.Freeze && maizCongelarPrefab != null) prefab = maizCongelarPrefab;

            Vector3 pos = new Vector3(GameSession.ItemX[i], GameSession.ItemY[i], 0f);
            GameObject itemObj = Instantiate(prefab, pos, Quaternion.identity);
            itemObj.name = "Maiz_" + i + "_" + type;

            CollectibleItem itemScript = itemObj.GetComponent<CollectibleItem>();
            if (itemScript == null) itemScript = itemObj.AddComponent<CollectibleItem>();
            itemScript.itemID = i;
            itemScript.type = type;
            items[i] = itemScript;
        }
    }

    // Lo llama CollectibleItem cuando el pollo local toca un maíz
    public void SendPickupRequest(int id)
    {
        if (!gameActive || !IsOnline) return;

        PickupRequestPayload payload = new PickupRequestPayload { playerSlot = MySlot, itemID = id };
        SocketClient.Instance.SendMessageToServer("PICKUP_ITEM", payload);
    }

    // El servidor confirma quién se quedó con el maíz: desaparece en TODOS los celulares
    public void OnItemPickedUp(int itemID, int winnerSlot, ItemType type)
    {
        if (!items.TryGetValue(itemID, out CollectibleItem item)) return;
        items.Remove(itemID);
        if (item == null) return;

        SpriteRenderer sr = item.GetComponentInChildren<SpriteRenderer>();
        GameFx.Pop(sr);

        if (winnerSlot == MySlot && localPlayer != null)
        {
            Vector3 p = localPlayer.transform.position;
            if (type == ItemType.Normal)
            {
                Play(pickupClip);
                GameFx.FloatingText(p, "+1", Color.white);
            }
            else if (type == ItemType.SpeedBoost)
            {
                Play(specialClip != null ? specialClip : pickupClip);
                GameFx.FloatingText(p, "+2  TURBO", new Color(1f, 0.85f, 0.2f));
                ShowStatus("¡Ganaste una carga de TURBO!", 1.5f, new Color(1f, 0.85f, 0.2f));
            }
            else
            {
                Play(specialClip != null ? specialClip : pickupClip);
                GameFx.FloatingText(p, "+2  HIELO", new Color(0.5f, 0.85f, 1f));
                ShowStatus("¡Ganaste una carga de HIELO!", 1.5f, new Color(0.5f, 0.85f, 1f));
            }
        }

        Destroy(item.gameObject);
    }

    // =================================================================================
    //  Puntajes y cargas (el servidor es la autoridad)
    // =================================================================================

    public void OnScoreUpdate(int[] slots, int[] scores, int[] itemCounts)
    {
        if (slots == null || scores == null) return;

        scoresBySlot.Clear();
        itemsBySlot.Clear();
        for (int i = 0; i < slots.Length && i < scores.Length; i++)
        {
            scoresBySlot[slots[i]] = scores[i];
            itemsBySlot[slots[i]] = (itemCounts != null && i < itemCounts.Length) ? itemCounts[i] : 0;
        }

        scoresBySlot.TryGetValue(MySlot, out myScore);
        itemsBySlot.TryGetValue(MySlot, out myItems);

        UpdateScoreUI();
        UpdateRanking();
    }

    public void OnChargesUpdate(int speed, int freeze)
    {
        turboCharges = speed;
        freezeCharges = freeze;
        RefreshPowerUpButtons();
    }

    // =================================================================================
    //  Power-ups
    // =================================================================================

    // Botón TURBO
    public void ActivateTurbo()
    {
        if (!CanRequestPowerUp()) return;
        if (turboCharges <= 0)
        {
            ShowStatus("Recoge un maíz arcoíris para tener TURBO", 1.5f, Color.white);
            return;
        }

        nextPowerUpRequest = Time.time + 0.4f;
        SocketClient.Instance.SendMessageToServer("USE_POWERUP",
            new PowerUpRequestPayload { senderSlot = MySlot, powerUpType = "SPEED" });
    }

    // Botón CONGELAR (afecta al rival más cercano; lo elige el servidor)
    public void ActivateFreeze()
    {
        if (!CanRequestPowerUp()) return;
        if (freezeCharges <= 0)
        {
            ShowStatus("Recoge un maíz de hielo para poder congelar", 1.5f, Color.white);
            return;
        }
        if (remotePlayers.Count == 0)
        {
            ShowStatus("No hay rivales para congelar", 1.5f, Color.white);
            return;
        }

        nextPowerUpRequest = Time.time + 0.4f;
        SocketClient.Instance.SendMessageToServer("USE_POWERUP",
            new PowerUpRequestPayload { senderSlot = MySlot, powerUpType = "FREEZE" });
    }

    // Compatibilidad con el OnClick antiguo del BtnCongelar
    public void TryFreezeTargetPlayer(int targetSlot)
    {
        ActivateFreeze();
    }

    private bool CanRequestPowerUp()
    {
        if (!gameActive || !IsOnline || localPlayer == null) return false;
        if (Time.time < nextPowerUpRequest) return false;
        if (localPlayer.IsFrozen)
        {
            ShowStatus("¡Estás congelado!", 1f, new Color(0.5f, 0.85f, 1f));
            return false;
        }
        return true;
    }

    // El servidor confirma un power-up (se envía a TODOS para que todos vean el efecto)
    public void OnPowerUpUsed(int senderSlot, int targetSlot, string type, float duration)
    {
        if (!gameActive) return;

        if (type == "SPEED")
        {
            if (senderSlot == MySlot)
            {
                localPlayer.ApplySpeedBoost(duration);
                Play(turboClip);
                GameFx.FloatingText(localPlayer.transform.position, "¡TURBO!", new Color(1f, 0.85f, 0.2f));
            }
            else if (remotePlayers.TryGetValue(senderSlot, out RemotePlayer rp) && rp != null && rp.Anim != null)
            {
                rp.Anim.SetTurbo(duration);
            }
        }
        else if (type == "FREEZE")
        {
            if (targetSlot == MySlot)
            {
                localPlayer.FreezePlayer(duration);
                Play(frozenClip);
                ShowStatus("¡" + GameSession.PlayerName(senderSlot) + " te congeló!", duration, new Color(0.5f, 0.85f, 1f));
                GameFx.FloatingText(localPlayer.transform.position, "¡CONGELADO!", new Color(0.5f, 0.85f, 1f));
            }
            else if (remotePlayers.TryGetValue(targetSlot, out RemotePlayer rp) && rp != null)
            {
                if (rp.Anim != null) rp.Anim.SetFrozen(duration);
                GameFx.FloatingText(rp.transform.position, "¡CONGELADO!", new Color(0.5f, 0.85f, 1f));
            }

            if (senderSlot == MySlot)
            {
                Play(freezeClip);
                ShowStatus("¡Congelaste a " + GameSession.PlayerName(targetSlot) + "!", 2f, new Color(0.5f, 0.85f, 1f));
            }
        }
    }

    // =================================================================================
    //  Otros jugadores
    // =================================================================================

    private void SpawnRemotePlayers()
    {
        if (GameSession.Slots == null || localPlayer == null) return;

        foreach (int slot in GameSession.Slots)
        {
            if (slot == MySlot) continue;
            CreateRemotePlayer(slot);
        }
    }

    private void CreateRemotePlayer(int slot)
    {
        Vector2 pos = GameSession.SpawnOf(slot);

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
        if (rb != null)
        {
            rb.bodyType = RigidbodyType2D.Kinematic;
            rb.linearVelocity = Vector2.zero;
        }

        Collider2D col = go.GetComponent<Collider2D>();
        if (col != null) col.isTrigger = true;

        ChickenAnimator anim = EnsureAnimator(go);
        ApplySkin(go, slot, true);
        CreateNameLabel(go, "J" + (slot + 1), Color.white);

        RemotePlayer rp = go.AddComponent<RemotePlayer>();
        rp.Init(slot, pos, anim);
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

        SocketClient.Instance.SendMessageToServer("MOVE",
            new MovePayload { playerSlot = MySlot, posX = pos.x, posY = pos.y });
    }

    public void OnRemotePlayerMoved(int slot, Vector2 position)
    {
        if (slot == MySlot) return;
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
        ShowStatus(GameSession.PlayerName(slot) + " salió de la partida", 2f, Color.white);
    }

    // =================================================================================
    //  Utilidades de jugadores
    // =================================================================================

    private PlayerController FindLocalPlayer()
    {
        foreach (var p in FindObjectsByType<PlayerController>(FindObjectsInactive.Exclude))
            if (p.isLocalPlayer) return p;
        return null;
    }

    private ChickenAnimator EnsureAnimator(GameObject go)
    {
        ChickenAnimator anim = go.GetComponent<ChickenAnimator>();
        if (anim == null) anim = go.AddComponent<ChickenAnimator>();
        return anim;
    }

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

    // Pone el pollo que cada jugador eligió en el lobby
    private void ApplySkin(GameObject pollo, int slot, bool tintIfNoSprite)
    {
        ChickenAnimator anim = pollo.GetComponent<ChickenAnimator>();
        SpriteRenderer sr = anim != null && anim.BodyRenderer != null ? anim.BodyRenderer : pollo.GetComponent<SpriteRenderer>();
        if (sr == null) return;

        sr.enabled = true;
        sr.sortingOrder = 100;

        int idx = GameSession.ChickenOf(slot);
        bool hasSprite = chickenSprites != null && idx >= 0 && idx < chickenSprites.Length && chickenSprites[idx] != null;

        Color color = Color.white;
        if (hasSprite)
            sr.sprite = chickenSprites[idx];
        else if (tintIfNoSprite && playerColors != null && playerColors.Length > 0)
            color = playerColors[Mathf.Abs(slot) % playerColors.Length];

        if (anim != null) anim.SetBaseColor(color);
        else sr.color = color;
    }

    // Etiqueta flotante sobre cada pollo ("TÚ", "J2"...) para relacionarlos con el ranking
    private void CreateNameLabel(GameObject pollo, string text, Color color)
    {
        GameObject label = new GameObject("Label");
        label.transform.SetParent(pollo.transform, false);

        Vector3 s = pollo.transform.lossyScale;
        float sx = Mathf.Approximately(s.x, 0f) ? 1f : s.x;
        float sy = Mathf.Approximately(s.y, 0f) ? 1f : s.y;
        label.transform.localScale = new Vector3(1f / Mathf.Abs(sx), 1f / Mathf.Abs(sy), 1f);
        label.transform.localPosition = new Vector3(0f, nameLabelHeight / Mathf.Abs(sy), 0f);

        TextMeshPro tmp = label.AddComponent<TextMeshPro>();
        tmp.text = text;
        tmp.fontSize = 22f;
        tmp.fontStyle = FontStyles.Bold;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = color;
        tmp.outlineWidth = 0.2f;
        tmp.outlineColor = new Color32(0, 0, 0, 220);
        tmp.sortingOrder = 250;
        tmp.rectTransform.sizeDelta = new Vector2(20f, 5f);
    }

    // El pollo no debe chocar con el terreno ni con los muros: el límite se controla por código
    private void IgnoreSolidColliders()
    {
        if (localPlayer == null) return;

        Collider2D myCollider = localPlayer.GetComponent<Collider2D>();
        if (myCollider == null) return;

        foreach (Collider2D other in FindObjectsByType<Collider2D>(FindObjectsInactive.Exclude))
        {
            if (other == myCollider || other.isTrigger) continue;
            Physics2D.IgnoreCollision(myCollider, other);
        }
    }

    private void Play(AudioClip clip)
    {
        if (clip != null && audioSource != null) audioSource.PlayOneShot(clip);
    }

    // =================================================================================
    //  HUD
    // =================================================================================

    private void UpdateScoreUI()
    {
        if (scoreText != null)
            scoreText.text = string.Format(scoreFormat, myScore, myItems);
    }

    private void UpdateRanking()
    {
        if (rankingText == null) return;

        List<KeyValuePair<int, int>> list = new List<KeyValuePair<int, int>>(scoresBySlot);
        list.Sort((a, b) => b.Value.CompareTo(a.Value));

        StringBuilder sb = new StringBuilder("<b>RANKING</b>\n");
        for (int i = 0; i < list.Count; i++)
        {
            bool me = list[i].Key == MySlot;
            string name = me ? "Tú" : "J" + (list[i].Key + 1);
            string line = (i + 1) + ". " + name + "  " + list[i].Value;
            sb.AppendLine(me ? "<color=#FFD54A>" + line + "</color>" : line);
        }
        rankingText.text = sb.ToString();
    }

    private void RefreshPowerUpButtons()
    {
        SetButtonState(btnTurbo, turboCharges > 0);
        SetButtonState(btnCongelar, freezeCharges > 0);

        if (turboBadge != null) turboBadge.text = "x" + turboCharges;
        if (freezeBadge != null) freezeBadge.text = "x" + freezeCharges;
    }

    private void SetButtonState(Button btn, bool active)
    {
        if (btn == null) return;

        // Se deja interactuable siempre para poder mostrar el mensaje "recoge un maíz..."
        btn.interactable = true;
        btn.transition = Selectable.Transition.None;

        if (btn.targetGraphic != null)
            btn.targetGraphic.color = active ? buttonOnColor : buttonOffColor;

        btn.transform.localScale = active ? Vector3.one : Vector3.one * 0.9f;
    }

    private void ShowStatus(string text, float seconds, Color color)
    {
        if (statusText == null) return;
        statusText.text = text;
        statusText.color = color;
        statusUntil = Time.time + seconds;
    }

    // Cuando no hay un mensaje temporal, muestra el estado del jugador
    private void UpdateStatusText()
    {
        if (statusText == null || Time.time < statusUntil) return;

        if (gameActive && localPlayer != null && localPlayer.IsFrozen)
        {
            statusText.text = "¡CONGELADO!";
            statusText.color = new Color(0.5f, 0.85f, 1f);
        }
        else if (gameActive && localPlayer != null && localPlayer.IsBoosted)
        {
            statusText.text = "TURBO ACTIVO";
            statusText.color = new Color(1f, 0.85f, 0.2f);
        }
        else
        {
            statusText.text = "";
        }
    }

    // Crea por código lo que falte del HUD (ranking, estado y contadores de cargas)
    private void EnsureHud()
    {
        Canvas canvas = null;
        if (timerText != null) canvas = timerText.canvas;
        else if (scoreText != null) canvas = scoreText.canvas;
        if (canvas == null) canvas = FindAnyObjectByType<Canvas>();
        if (canvas == null) return;
        canvas = canvas.rootCanvas;

        if (scoreText != null)
        {
            scoreText.enableAutoSizing = true;
            scoreText.fontSizeMax = Mathf.Max(scoreText.fontSize, 20f);
            scoreText.fontSizeMin = 12f;
        }

        if (rankingText == null)
        {
            rankingText = CreateUIText(canvas.transform, "RankingText",
                new Vector2(1f, 1f), new Vector2(-30f, -30f), new Vector2(360f, 230f), 30f,
                TextAlignmentOptions.TopRight);
        }

        if (statusText == null)
        {
            statusText = CreateUIText(canvas.transform, "StatusText",
                new Vector2(0.5f, 1f), new Vector2(0f, -150f), new Vector2(1000f, 80f), 42f,
                TextAlignmentOptions.Center);
        }

        if (btnTurbo != null) turboBadge = CreateBadge(btnTurbo);
        if (btnCongelar != null) freezeBadge = CreateBadge(btnCongelar);
    }

    private TextMeshProUGUI CreateBadge(Button btn)
    {
        Transform existing = btn.transform.Find("Badge");
        if (existing != null) return existing.GetComponent<TextMeshProUGUI>();

        TextMeshProUGUI t = CreateUIText(btn.transform, "Badge",
            new Vector2(1f, 1f), new Vector2(10f, 10f), new Vector2(110f, 60f), 40f,
            TextAlignmentOptions.TopRight);
        t.fontStyle = FontStyles.Bold;
        t.outlineWidth = 0.25f;
        t.outlineColor = new Color32(0, 0, 0, 255);
        t.raycastTarget = false;
        return t;
    }

    private TextMeshProUGUI CreateUIText(Transform parent, string name, Vector2 anchor, Vector2 position,
                                         Vector2 size, float fontSize, TextAlignmentOptions align)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);

        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = anchor;
        rt.anchorMax = anchor;
        rt.pivot = anchor;
        rt.anchoredPosition = position;
        rt.sizeDelta = size;

        TextMeshProUGUI t = go.AddComponent<TextMeshProUGUI>();
        t.fontSize = fontSize;
        t.alignment = align;
        t.color = Color.white;
        t.raycastTarget = false;
        t.text = "";
        return t;
    }

    // =================================================================================
    //  Pantalla de resultados
    // =================================================================================

    private void BuildResultsPanel(ResultsPayload r)
    {
        int winnersCount = 0;
        bool iWon = false;
        List<string> winnerNames = new List<string>();
        for (int i = 0; i < r.slots.Length; i++)
        {
            if (r.winners != null && i < r.winners.Length && r.winners[i] == 1)
            {
                winnersCount++;
                winnerNames.Add(GameSession.PlayerName(r.slots[i]));
                if (r.slots[i] == MySlot) iWon = true;
            }
        }

        StringBuilder sb = new StringBuilder();
        if (winnersCount > 1)
            sb.AppendLine("<size=130%><color=#FFD54A>¡EMPATE! Victoria compartida</color></size>");
        else if (iWon)
            sb.AppendLine("<size=130%><color=#FFD54A>¡GANASTE!</color></size>");
        else if (winnersCount == 1)
            sb.AppendLine("<size=130%><color=#FFD54A>GANADOR: " + winnerNames[0] + "</color></size>");
        else
            sb.AppendLine("<size=130%>FIN DEL JUEGO</size>");

        sb.AppendLine();

        for (int i = 0; i < r.slots.Length; i++)
        {
            int slot = r.slots[i];
            int score = r.scores != null && i < r.scores.Length ? r.scores[i] : 0;
            int its = r.items != null && i < r.items.Length ? r.items[i] : 0;
            int sp = r.specials != null && i < r.specials.Length ? r.specials[i] : 0;
            bool winner = r.winners != null && i < r.winners.Length && r.winners[i] == 1;

            string line = (i + 1) + ". " + GameSession.PlayerName(slot) + (slot == MySlot ? " (tú)" : "") +
                          "  -  " + score + " pts  (" + its + " maíces, " + sp + " especiales)";
            sb.AppendLine(winner ? "<color=#FFD54A>" + line + "</color>" : line);
        }

        sb.AppendLine();
        sb.Append("<size=70%>Desempate: más maíces especiales. Si siguen iguales, victoria compartida.</size>");

        Canvas canvas = scoreText != null ? scoreText.canvas : FindAnyObjectByType<Canvas>();
        if (canvas == null)
        {
            Debug.Log(sb.ToString());
            return;
        }
        canvas = canvas.rootCanvas;

        Transform old = canvas.transform.Find("PanelResultados");
        if (old != null) Destroy(old.gameObject);

        GameObject panel = new GameObject("PanelResultados", typeof(RectTransform), typeof(Image));
        panel.transform.SetParent(canvas.transform, false);
        panel.transform.SetAsLastSibling();

        RectTransform rt = panel.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        panel.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.85f);

        TextMeshProUGUI tmp = CreateUIText(panel.transform, "TextoResultados",
            new Vector2(0.5f, 0.5f), new Vector2(0f, 60f), new Vector2(1500f, 650f), 46f,
            TextAlignmentOptions.Center);
        tmp.enableAutoSizing = true;
        tmp.fontSizeMin = 18f;
        tmp.fontSizeMax = 52f;
        tmp.text = sb.ToString();

        CreatePanelButton(panel.transform, "BtnJugarOtraVez", "Jugar otra vez", new Vector2(-260f, 60f),
            new Color(0.8f, 0.5f, 0.25f), BackToLobby);
        CreatePanelButton(panel.transform, "BtnMenu", "Menú principal", new Vector2(260f, 60f),
            new Color(0.35f, 0.35f, 0.35f), GoToMainMenu);
    }

    private void CreatePanelButton(Transform parent, string name, string label, Vector2 pos, Color color,
                                   UnityEngine.Events.UnityAction action)
    {
        GameObject btnObj = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        btnObj.transform.SetParent(parent, false);

        RectTransform brt = btnObj.GetComponent<RectTransform>();
        brt.anchorMin = new Vector2(0.5f, 0f);
        brt.anchorMax = new Vector2(0.5f, 0f);
        brt.pivot = new Vector2(0.5f, 0f);
        brt.anchoredPosition = pos;
        brt.sizeDelta = new Vector2(440f, 110f);

        Image img = btnObj.GetComponent<Image>();
        img.color = color;

        Button b = btnObj.GetComponent<Button>();
        b.targetGraphic = img;
        b.onClick.AddListener(action);

        TextMeshProUGUI t = CreateUIText(btnObj.transform, "Texto", new Vector2(0.5f, 0.5f), Vector2.zero,
            new Vector2(440f, 110f), 40f, TextAlignmentOptions.Center);
        t.text = label;
    }

    private void BackToLobby()
    {
        if (IsOnline) SceneManager.LoadScene(lobbySceneName);
        else GoToMainMenu();
    }

    private void GoToMainMenu()
    {
        SocketClient.Instance?.Disconnect();
        if (GameServer.IsRunning) GameServer.StopServer();
        SceneManager.LoadScene(mainMenuSceneName);
    }

    // =================================================================================
    //  Pruebas en el editor (clic derecho en el componente GameManager, en Play)
    // =================================================================================

    [ContextMenu("PRUEBA: efecto turbo local")]
    private void TestTurbo()
    {
        if (localPlayer != null) localPlayer.ApplySpeedBoost(5f);
    }

    [ContextMenu("PRUEBA: efecto congelado local")]
    private void TestFrozen()
    {
        if (localPlayer != null) localPlayer.FreezePlayer(3f);
    }
}
