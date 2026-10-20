using UnityEngine;

public enum ItemType
{
    Normal = 0,     // +1 punto
    SpeedBoost = 1, // +2 puntos y 1 carga de TURBO
    Freeze = 2,     // +2 puntos y 1 carga de HIELO
    Teleport = 3    // +5 puntos y 1 carga de TELETRANSPORTE (aparece cada 20 s y dura 8 s)
}

public class CollectibleItem : MonoBehaviour
{
    public int itemID;
    public ItemType type = ItemType.Normal;

    [Header("Animación (flotar y brillar)")]
    [SerializeField] private float floatHeight = 0.3f;
    [SerializeField] private float floatSpeed = 2.5f;
    [SerializeField] private float pulseAmount = 0.08f;
    [SerializeField] private float wobbleDegrees = 8f;

    private bool requested = false;
    private Vector3 basePos;
    private Vector3 baseScale;
    private float phaseOffset;
    private float spawnAnim = 1f; // 0 -> 1 cuando aparece (reaparición)
    private const float SpawnDuration = 0.35f;

    // Al reaparecer en otro lugar: crece desde cero con un pequeño rebote
    public void PlaySpawnAnimation()
    {
        spawnAnim = 0f;
    }

    // Para el maíz de teletransporte: cuándo desaparece (parpadea los últimos 2 segundos)
    private float expiresAt = -1f;
    private SpriteRenderer[] renderers;

    public void SetLifetime(float seconds)
    {
        expiresAt = Time.time + seconds;
    }

    private void Awake()
    {
        Collider2D col = GetComponent<Collider2D>();
        if (col != null) col.isTrigger = true;
    }

    private void Start()
    {
        basePos = transform.position;
        baseScale = transform.localScale;
        phaseOffset = itemID * 0.73f; // cada maíz se mueve a destiempo
    }

    private void Update()
    {
        float t = Time.time * floatSpeed + phaseOffset;
        bool special = type != ItemType.Normal;

        transform.position = basePos + Vector3.up * (Mathf.Sin(t) * floatHeight);
        transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Sin(t * 0.8f) * wobbleDegrees);

        // Los especiales "laten" más fuerte para distinguirse a distancia
        float pulse = 1f + Mathf.Sin(t * 2f) * pulseAmount * (special ? 2.5f : 1f);

        if (spawnAnim < 1f)
        {
            spawnAnim = Mathf.Min(1f, spawnAnim + Time.deltaTime / SpawnDuration);
            // "Ease out back": pasa un poco del tamaño final y vuelve
            float k = spawnAnim - 1f;
            pulse *= 1f + 2.7f * k * k * k + 1.7f * k * k;
        }

        transform.localScale = baseScale * pulse;

        if (expiresAt > 0f)
        {
            if (renderers == null) renderers = GetComponentsInChildren<SpriteRenderer>();
            float left = expiresAt - Time.time;
            bool visible = left > 2f || Mathf.Repeat(Time.time * 8f, 1f) > 0.35f; // parpadeo final
            foreach (SpriteRenderer r in renderers) if (r != null) r.enabled = visible;
        }
    }

    private void OnTriggerEnter2D(Collider2D other) { TryRequest(other); }

    // Por si el pollo ya estaba encima del maíz cuando se liberó el movimiento o se descongeló
    private void OnTriggerStay2D(Collider2D other) { TryRequest(other); }

    private void TryRequest(Collider2D other)
    {
        if (requested || spawnAnim < 1f) return; // no se puede coger mientras aparece

        PlayerController player = other.GetComponentInParent<PlayerController>();
        if (player == null || !player.isLocalPlayer || player.IsFrozen) return;
        if (GameManager.Instance == null) return;

        requested = true;
        GameManager.Instance.SendPickupRequest(itemID);

        // Si el servidor lo rechaza (otro llegó primero o hubo retraso), se puede reintentar
        Invoke(nameof(ResetRequest), 0.5f);
    }

    private void ResetRequest()
    {
        requested = false;
    }
}
