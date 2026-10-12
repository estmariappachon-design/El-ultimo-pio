using UnityEngine;

public enum ItemType
{
    Normal = 0,     // +1 punto
    SpeedBoost = 1, // +2 puntos y 1 carga de TURBO
    Freeze = 2      // +2 puntos y 1 carga de HIELO
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
        transform.localScale = baseScale * pulse;
    }

    private void OnTriggerEnter2D(Collider2D other) { TryRequest(other); }

    // Por si el pollo ya estaba encima del maíz cuando se liberó el movimiento o se descongeló
    private void OnTriggerStay2D(Collider2D other) { TryRequest(other); }

    private void TryRequest(Collider2D other)
    {
        if (requested) return;

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
