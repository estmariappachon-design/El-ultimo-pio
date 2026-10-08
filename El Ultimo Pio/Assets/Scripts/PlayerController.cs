using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class PlayerController : MonoBehaviour
{
    [Header("Movimiento")]
    [SerializeField] private float moveSpeed = 8f;

    [Header("Límites del mapa")]
    [SerializeField] private float mapHalfSize = 50f; // el mapa mide 100x100, así que va de -50 a 50
    [SerializeField] private float edgeMargin = 3f;   // el pollo se detiene un poco antes del borde

    [Header("Identificación de Red")]
    public int playerSlot = 0;
    public bool isLocalPlayer = true;

    private Rigidbody2D rb;
    private bool isFrozen = false;
    private float speedMultiplier = 1f;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();

        // Configuración física segura, sin depender del Inspector
        rb.gravityScale = 0f;
        rb.freezeRotation = true;
        rb.linearVelocity = Vector2.zero;
    }

    private void FixedUpdate()
    {
        if (!isLocalPlayer) return;

        ClampToMap();

        if (isFrozen)
        {
            rb.linearVelocity = Vector2.zero;
            return;
        }

        Vector2 input = VirtualJoystick.Direction;

        // Zona muerta: joystick suelto = pollo quieto
        if (input.sqrMagnitude < 0.02f)
        {
            rb.linearVelocity = Vector2.zero;
            return;
        }

        rb.linearVelocity = input * (moveSpeed * speedMultiplier);
    }

    // Mantiene al pollo dentro del mapa por código, sin depender de colliders en los bordes
    private void ClampToMap()
    {
        float limit = mapHalfSize - edgeMargin;
        Vector2 p = rb.position;
        Vector2 clamped = new Vector2(Mathf.Clamp(p.x, -limit, limit), Mathf.Clamp(p.y, -limit, limit));
        if (clamped != p) rb.position = clamped;
    }

    // Maíz Arcoíris (Turbo)
    public void ApplySpeedBoost(float duration)
    {
        StopCoroutine(nameof(SpeedBoostRoutine));
        StartCoroutine(SpeedBoostRoutine(duration));
    }

    private IEnumerator SpeedBoostRoutine(float duration)
    {
        speedMultiplier = 1.6f;
        yield return new WaitForSeconds(duration);
        speedMultiplier = 1f;
    }

    // Maíz de Hielo (Congelar)
    public void FreezePlayer(float duration)
    {
        StopCoroutine(nameof(FreezeRoutine));
        StartCoroutine(FreezeRoutine(duration));
    }

    private IEnumerator FreezeRoutine(float duration)
    {
        isFrozen = true;
        rb.linearVelocity = Vector2.zero;
        yield return new WaitForSeconds(duration);
        isFrozen = false;
    }
}