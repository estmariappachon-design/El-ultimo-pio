using UnityEngine;

public enum ItemType
{
    Normal,     // Suma puntos
    SpeedBoost, // Otorga Turbo de velocidad al jugador
    Freeze      // Otorga la habilidad de congelar al rival
}

public class CollectibleItem : MonoBehaviour
{
    public int itemID;
    public ItemType type = ItemType.Normal;

    private bool requested = false;

    private void Awake()
    {
        // Garantiza que el maíz sea un Trigger aunque el prefab no lo tenga marcado
        Collider2D col = GetComponent<Collider2D>();
        if (col != null) col.isTrigger = true;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (requested) return;

        // Detecta al jugador por su script, sin depender del Tag
        PlayerController player = other.GetComponentInParent<PlayerController>();
        if (player == null || !player.isLocalPlayer) return;

        if (GameManager.Instance == null)
        {
            Debug.LogWarning("CollectibleItem: no hay GameManager en la escena.");
            return;
        }

        requested = true;
        GameManager.Instance.SendPickupRequest(itemID, type);

        // Si el servidor no responde, permite reintentar después de 1 segundo
        Invoke(nameof(ResetRequest), 1f);
    }

    private void ResetRequest()
    {
        requested = false;
    }
}