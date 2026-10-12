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

    [Header("Pollos del JUEGO, uno por color (MISMO ORDEN que la paleta del lobby)")]
    [Tooltip("Elemento 0 = ColorButton1, elemento 1 = ColorButton2 ... Cada uno con su imagen normal, de turbo y congelado")]
    [SerializeField] private ChickenSkin[] chickenSkins;
    [Tooltip("Solo se usa si Chicken Skins está vacío (versión anterior)")]
    [SerializeField] private Sprite[] chickenSprites;

    [Header("Rotación de los pollos")]
    [SerializeField] private bool rotateChickens = true;
    [Tooltip("Hacia dónde mira el pollo en la imagen: 90 = arriba, 0 = derecha, -90 = abajo, 180 = izquierda")]
    [SerializeField] private float chickenImageFacing = 90f;

    [Header("Maíces")]
    [Tooltip("Tamaño de los maíces respecto al prefab (1 = igual, 0.6 = 60 %)")]
    [SerializeField, Range(0.2f, 1.5f)] private float itemScale = 0.6f;
    [SerializeField] private Color[] playerColors =
    {
        Color.yellow, Color.cyan, Color.green, new Color(1f, 0.5f, 0f)
    }; // solo se usan si no hay sprite asignado
    [SerializeField] private float nameLabelHeight = 4f;

    [Header("Pantalla de resultados")]
    [Tooltip("Todos los textos de la pantalla final. Despliega para editarlos")]
    [SerializeField] private ResultsTexts resultsTexts = new ResultsTexts();
    [Tooltip("Imagen detrás del texto cuando GANAS (o empatas en el primer lugar)")]
    [SerializeField] private Sprite winImage;
    [Tooltip("Imagen detrás del texto cuando PIERDES. Si se deja vacía, se usa la de ganar")]
    [SerializeField] private Sprite loseImage;
    [Tooltip("Tamaño de la imagen en pantalla (referencia 1920x1080)")]
    [SerializeField] private Vector2 resultsImageSize = new Vector2(1500f, 750f);
    [Tooltip("Posición: 0,0 = centro. Súbela o bájala para cuadrarla con las letras")]
    [SerializeField] private Vector2 resultsImageOffset = new Vector2(0f, 60f);
    [Tooltip("Mantener la forma original de la imagen (no estirarla)")]
    [SerializeField] private bool resultsImageKeepAspect = true;

    [Header("Ranking dentro de la imagen (posiciones relativas a la imagen: 0,0 = centro; 0.5 = borde)")]
    [SerializeField] private Vector2 rankingPosition = new Vector2(0f, -0.06f);
    [Tooltip("Ancho y alto del área del ranking, en fracción de la imagen")]
    [SerializeField] private Vector2 rankingSize = new Vector2(0.55f, 0.2f);

    [Header("Botones dibujados en la imagen")]
    [Tooltip("Si tu imagen ya trae los botones (reintentar y casa), se vuelven tocables y no se crean los rectangulares")]
    [SerializeField] private bool imageHasButtons = true;
    [SerializeField] private Vector2 replayButtonPosition = new Vector2(-0.18f, -0.36f);
    [SerializeField] private Vector2 homeButtonPosition = new Vector2(0.19f, -0.35f);
    [Tooltip("Diámetro de la zona tocable, en fracción del ancho de la imagen")]
    [SerializeField, Range(0.05f, 0.5f)] private float imageButtonSize = 0.2f;
    [Tooltip("Marca esto para VER las zonas tocables en rojo y cuadrarlas; desmárcalo al terminar")]
    [SerializeField] private bool showButtonAreas = false;

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
            SpawnItem(i, (ItemType)GameSession.ItemT[i], new Vector2(GameSession.ItemX[i], GameSession.ItemY[i]), false);
    }

    private void SpawnItem(int id, ItemType type, Vector2 position, bool popIn)
    {
        if (maizNormalPrefab == null) return;

        // Si por algún motivo ya existe uno con ese número, se reemplaza
        if (items.TryGetValue(id, out CollectibleItem old) && old != null) Destroy(old.gameObject);

        GameObject prefab = maizNormalPrefab;
        if (type == ItemType.SpeedBoost && maizVelocidadPrefab != null) prefab = maizVelocidadPrefab;
        else if (type == ItemType.Freeze && maizCongelarPrefab != null) prefab = maizCongelarPrefab;

        GameObject itemObj = Instantiate(prefab, new Vector3(position.x, position.y, 0f), Quaternion.identity);
        itemObj.name = "Maiz_" + id + "_" + type;
        itemObj.transform.localScale = prefab.transform.localScale * itemScale; // maíces más pequeños

        CollectibleItem itemScript = itemObj.GetComponent<CollectibleItem>();
        if (itemScript == null) itemScript = itemObj.AddComponent<CollectibleItem>();
        itemScript.itemID = id;
        itemScript.type = type;
        if (popIn) itemScript.PlaySpawnAnimation();
        items[id] = itemScript;
    }

    // El servidor recolocó un maíz recogido en otro lugar del mapa (siempre hay 50)
    public void OnItemSpawned(int itemID, float x, float y, ItemType type)
    {
        if (gameOverHandled) return;
        SpawnItem(itemID, type, new Vector2(x, y), true);
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
        anim.SetFacingSettings(rotateChickens, chickenImageFacing);
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
        Color color = Color.white;

        ChickenSkin skin = (chickenSkins != null && idx >= 0 && idx < chickenSkins.Length) ? chickenSkins[idx] : null;
        if (skin != null && skin.normal != null)
        {
            // Pollo del color elegido, con sus imágenes de turbo y congelado
            sr.sprite = skin.normal;
            if (anim != null) anim.SetSkin(skin.normal, skin.speed, skin.frozen);
        }
        else if (chickenSprites != null && idx >= 0 && idx < chickenSprites.Length && chickenSprites[idx] != null)
        {
            sr.sprite = chickenSprites[idx];
            if (anim != null) anim.SetSkin(chickenSprites[idx], null, null);
        }
        else
        {
            Debug.LogWarning("[GM] No hay imagen para el pollo " + idx + " (jugador " + (slot + 1) + "). Revisa Chicken Skins.");
            if (tintIfNoSprite && playerColors != null && playerColors.Length > 0)
                color = playerColors[Mathf.Abs(slot) % playerColors.Length];
        }

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

        // El tamaño lo anima UIButtonFeedback; aquí solo se indica si está disponible
        UIButtonFeedback fb = btn.GetComponent<UIButtonFeedback>();
        if (fb == null) fb = btn.gameObject.AddComponent<UIButtonFeedback>();
        fb.SetDimmed(!active);
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
                winnerNames.Add((resultsTexts ?? new ResultsTexts()).PlayerName(r.slots[i]));
                if (r.slots[i] == MySlot) iWon = true;
            }
        }

        ResultsTexts txt = resultsTexts ?? new ResultsTexts();
        string hl = "#" + ColorUtility.ToHtmlStringRGB(txt.highlightColor);
        string winnersList = string.Join(", ", winnerNames);

        // ----- Título (solo si la imagen NO lo trae dibujado) -----
        string title, subtitle;
        if (winnersCount > 1)       { title = iWon ? txt.tieWinTitle : txt.loseTitle; subtitle = iWon ? txt.tieWinSubtitle : txt.tieLoseSubtitle; }
        else if (iWon)              { title = txt.winTitle;  subtitle = txt.winSubtitle; }
        else if (winnersCount == 1) { title = txt.loseTitle; subtitle = txt.loseSubtitle; }
        else                        { title = txt.noWinnerTitle; subtitle = ""; }

        Sprite bg = iWon ? winImage : (loseImage != null ? loseImage : winImage);
        bool useImage = bg != null;
        bool showTitles = !useImage || txt.showTitlesOverImage;

        StringBuilder sb = new StringBuilder();
        if (showTitles)
        {
            sb.AppendLine("<size=" + txt.titleSize + "%><color=" + hl + ">" + title + "</color></size>");
            if (!string.IsNullOrEmpty(subtitle))
                sb.AppendLine("<size=" + txt.subtitleSize + "%>" + subtitle.Replace("{winner}", winnersList) + "</size>");
            sb.AppendLine();
        }

        // ----- Ranking -----
        if (txt.showRanking)
        {
            for (int i = 0; i < r.slots.Length; i++)
            {
                int slot = r.slots[i];
                int score = r.scores != null && i < r.scores.Length ? r.scores[i] : 0;
                int its = r.items != null && i < r.items.Length ? r.items[i] : 0;
                int sp = r.specials != null && i < r.specials.Length ? r.specials[i] : 0;
                bool winner = r.winners != null && i < r.winners.Length && r.winners[i] == 1;

                string line = txt.rowFormat
                    .Replace("{pos}", (i + 1).ToString())
                    .Replace("{name}", txt.PlayerName(slot))
                    .Replace("{you}", slot == MySlot ? txt.youTag : "")
                    .Replace("{score}", score.ToString())
                    .Replace("{items}", its.ToString())
                    .Replace("{specials}", sp.ToString());

                bool colorRow = winner && txt.highlightWinnerRows;
                sb.AppendLine(colorRow ? "<color=" + hl + ">" + line + "</color>" : line);
            }
        }

        if (txt.showFooter && !string.IsNullOrEmpty(txt.footer))
        {
            sb.AppendLine();
            sb.Append("<size=" + txt.footerSize + "%>" + txt.footer + "</size>");
        }

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

        // Canvas propio con orden alto: así los pollos, sus etiquetas y los maíces
        // NUNCA se dibujan encima de la pantalla final
        Canvas panelCanvas = panel.AddComponent<Canvas>();
        panelCanvas.overrideSorting = true;
        panelCanvas.sortingOrder = 1000;
        panel.AddComponent<GraphicRaycaster>();

        RectTransform rt = panel.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        panel.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.85f);

        if (useImage)
        {
            // ----- Con imagen: el ranking va DENTRO de la imagen, pequeño y en blanco -----
            RectTransform irt = CreateResultsImage(panel.transform, bg);

            TextMeshProUGUI rank = CreateUIText(irt, "TextoRanking", new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero,
                txt.rankingFontSize, TextAlignmentOptions.Center);
            RectTransform rrt = rank.rectTransform;
            Vector2 c = new Vector2(0.5f, 0.5f) + rankingPosition;
            rrt.anchorMin = c - rankingSize * 0.5f;
            rrt.anchorMax = c + rankingSize * 0.5f;
            rrt.offsetMin = rrt.offsetMax = Vector2.zero;

            rank.enableAutoSizing = true;
            rank.fontSizeMin = 10f;
            rank.fontSizeMax = txt.rankingFontSize;
            rank.color = txt.textColor;
            rank.outlineWidth = txt.rankingOutline;
            rank.outlineColor = new Color32(0, 0, 0, 200);
            rank.text = sb.ToString().TrimEnd();

            if (imageHasButtons)
            {
                // Los botones redondos dibujados en la imagen se vuelven tocables
                CreateImageHotspot(irt, "BtnJugarOtraVez", replayButtonPosition, BackToLobby);
                CreateImageHotspot(irt, "BtnMenu", homeButtonPosition, GoToMainMenu);
                return;
            }
        }
        else
        {
            // ----- Sin imagen: texto grande en el centro -----
            TextMeshProUGUI tmp = CreateUIText(panel.transform, "TextoResultados",
                new Vector2(0.5f, 0.5f), new Vector2(0f, 60f), new Vector2(1500f, 650f), 46f,
                TextAlignmentOptions.Center);
            tmp.enableAutoSizing = true;
            tmp.fontSizeMin = 18f;
            tmp.fontSizeMax = 52f;
            tmp.text = sb.ToString();
            tmp.color = txt.textColor;
        }

        CreatePanelButton(panel.transform, "BtnJugarOtraVez", txt.playAgainButton, new Vector2(-260f, 60f),
            new Color(0.8f, 0.5f, 0.25f), BackToLobby);
        CreatePanelButton(panel.transform, "BtnMenu", txt.menuButton, new Vector2(260f, 60f),
            new Color(0.35f, 0.35f, 0.35f), GoToMainMenu);
    }

    // Crea la imagen de ganar/perder con su forma real (sin estirarla), dentro del tamaño máximo
    private RectTransform CreateResultsImage(Transform parent, Sprite sprite)
    {
        GameObject imgObj = new GameObject("ImagenResultados", typeof(RectTransform), typeof(Image));
        imgObj.transform.SetParent(parent, false);

        RectTransform irt = imgObj.GetComponent<RectTransform>();
        irt.anchorMin = irt.anchorMax = irt.pivot = new Vector2(0.5f, 0.5f);
        irt.anchoredPosition = resultsImageOffset;

        Vector2 size = resultsImageSize;
        if (resultsImageKeepAspect && sprite.rect.height > 0f)
        {
            float aspect = sprite.rect.width / sprite.rect.height;
            if (size.x / size.y > aspect) size.x = size.y * aspect;
            else size.y = size.x / aspect;
        }
        irt.sizeDelta = size;

        Image img = imgObj.GetComponent<Image>();
        img.sprite = sprite;
        img.raycastTarget = false;
        return irt;
    }

    // Zona tocable invisible sobre un botón dibujado en la imagen.
    // Al pasar el mouse se aclara y al tocar se oscurece un poco (círculo semitransparente).
    private void CreateImageHotspot(RectTransform image, string name, Vector2 position, UnityEngine.Events.UnityAction action)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(image, false);

        RectTransform hrt = go.GetComponent<RectTransform>();
        Vector2 c = new Vector2(0.5f, 0.5f) + position;
        hrt.anchorMin = hrt.anchorMax = c;
        hrt.pivot = new Vector2(0.5f, 0.5f);
        float d = image.sizeDelta.x * imageButtonSize;
        hrt.sizeDelta = new Vector2(d, d);
        hrt.anchoredPosition = Vector2.zero;

        Image img = go.GetComponent<Image>();
        img.sprite = CircleSprite();
        img.color = Color.white;

        Button b = go.GetComponent<Button>();
        b.targetGraphic = img;
        b.transition = Selectable.Transition.ColorTint;
        ColorBlock cb = b.colors;
        cb.normalColor = new Color(1f, 1f, 1f, 0f);
        cb.highlightedColor = new Color(1f, 1f, 1f, 0.18f);
        cb.selectedColor = new Color(1f, 1f, 1f, 0f);
        cb.pressedColor = new Color(0f, 0f, 0f, 0.3f);
        cb.fadeDuration = 0.08f;
        b.colors = cb;
        Navigation nav = b.navigation;
        nav.mode = Navigation.Mode.None;
        b.navigation = nav;
        b.onClick.AddListener(action);

        // Ver las zonas tocables para cuadrarlas con los dibujos
        if (showButtonAreas) cb.normalColor = new Color(1f, 0f, 0f, 0.35f);
        b.colors = cb;
    }

    private static Sprite circleSprite;

    private static Sprite CircleSprite()
    {
        if (circleSprite != null) return circleSprite;

        const int size = 128;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        float r = size * 0.5f;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = x + 0.5f - r, dy = y + 0.5f - r;
                float a = Mathf.Clamp01(r - Mathf.Sqrt(dx * dx + dy * dy)); // borde suave
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
        }
        tex.Apply();
        circleSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        return circleSprite;
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
        btnObj.AddComponent<UIButtonFeedback>();

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

// Imágenes de UN pollo del juego (vista desde arriba) en sus 3 estados
[System.Serializable]
public class ChickenSkin
{
    public string name = "Pollo";
    public Sprite normal;   // caminando normal
    public Sprite speed;    // con turbo (velocidad)
    public Sprite frozen;   // congelado
}

// Textos de la pantalla final (se editan en el Inspector del GameManager)
// Comodines que se reemplazan solos:
//   {winner}  -> nombre del ganador (o ganadores, separados por coma)
//   {pos} {name} {you} {score} {items} {specials} -> en cada fila del ranking
//   {n}       -> número del jugador en "Player Name Format"
// Se pueden usar etiquetas de TextMeshPro: <b>negrita</b>, <i>cursiva</i>, <color=#FF0000>rojo</color>
[System.Serializable]
public class ResultsTexts
{
    [Header("Títulos")]
    public string winTitle = "¡GANASTE!";
    [TextArea] public string winSubtitle = "¡Eres el pollo más rápido de la granja!";
    public string loseTitle = "¡PERDISTE!";
    [TextArea] public string loseSubtitle = "Ganó {winner}";
    public string tieWinTitle = "¡EMPATE!";
    [TextArea] public string tieWinSubtitle = "Victoria compartida con {winner}";
    [TextArea] public string tieLoseSubtitle = "Empataron en el primer lugar: {winner}";
    public string noWinnerTitle = "FIN DEL JUEGO";

    [Header("Ranking")]
    public bool showRanking = true;
    [Tooltip("Comodines: {pos} {name} {you} {score} {items} {specials}")]
    public string rowFormat = "{pos}. {name}{you}  -  {score} pts  ({items} maíces, {specials} especiales)";
    public string youTag = " (tú)";
    [Tooltip("{n} = número del jugador (1 a 4)")]
    public string playerNameFormat = "Jugador {n}";

    [Header("Nota al final (vacío = no se muestra)")]
    [TextArea] public string footer = "Desempate: más maíces especiales. Si siguen iguales, victoria compartida.";

    [Header("Botones")]
    public string playAgainButton = "Jugar otra vez";
    public string menuButton = "Menú principal";

    [Header("Estilo")]
    public Color textColor = Color.white;
    [Tooltip("Mostrar los títulos (¡GANASTE!...) aunque haya imagen. Déjalo apagado si la imagen ya los trae")]
    public bool showTitlesOverImage = false;
    [Tooltip("Mostrar la nota del desempate")]
    public bool showFooter = false;
    [Tooltip("Pintar con el color destacado las filas de los ganadores (apagado = todo blanco)")]
    public bool highlightWinnerRows = false;
    [Tooltip("Tamaño máximo de letra del ranking dentro de la imagen")]
    [Range(10, 80)] public float rankingFontSize = 30f;
    [Range(0f, 0.5f)] public float rankingOutline = 0.2f;
    public Color highlightColor = new Color(1f, 0.835f, 0.29f);  // título y ganadores
    [Range(50, 250)] public int titleSize = 130;     // % del tamaño normal
    [Range(50, 200)] public int subtitleSize = 90;
    [Range(30, 150)] public int footerSize = 70;

    public string PlayerName(int slot)
    {
        return playerNameFormat.Replace("{n}", (slot + 1).ToString());
    }
}
