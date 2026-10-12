using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class PlayerController : MonoBehaviour
{
    [Header("Movimiento")]
    [SerializeField] private float moveSpeed = 8f;
    [SerializeField] private float turboMultiplier = 1.7f;

    [Header("Límites del mapa")]
    [SerializeField] private float mapHalfSize = 50f; // mapa 100x100 -> de -50 a 50
    [SerializeField] private float edgeMargin = 3f;

    [Header("Identificación de Red")]
    public int playerSlot = 0;
    public bool isLocalPlayer = true;

    private Rigidbody2D rb;
    private ChickenAnimator chickenAnimator;
    private Coroutine speedRoutine;
    private Coroutine freezeRoutine;
    private float speedMultiplier = 1f;

    public bool IsFrozen { get; private set; }
    public bool IsBoosted => speedMultiplier > 1f;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        rb.gravityScale = 0f;
        rb.freezeRotation = true;
        rb.interpolation = RigidbodyInterpolation2D.Interpolate; // movimiento más suave en celular
        rb.linearVelocity = Vector2.zero;

        chickenAnimator = GetComponent<ChickenAnimator>();
    }

    private void FixedUpdate()
    {
        if (!isLocalPlayer) return;

        ClampToMap();

        if (IsFrozen)
        {
            rb.linearVelocity = Vector2.zero;
            return;
        }

        Vector2 input = ReadInput();

        if (input.sqrMagnitude < 0.02f)
        {
            rb.linearVelocity = Vector2.zero;
            return;
        }

        rb.linearVelocity = Vector2.ClampMagnitude(input, 1f) * (moveSpeed * speedMultiplier);
    }

    // Joystick virtual en el celular. En el editor/PC también se puede usar WASD o flechas para probar.
    private Vector2 ReadInput()
    {
        Vector2 input = VirtualJoystick.Direction;

#if (UNITY_EDITOR || UNITY_STANDALONE) && ENABLE_INPUT_SYSTEM
        // Teclado solo para probar en el PC (Input System nuevo)
        if (input.sqrMagnitude < 0.02f && UnityEngine.InputSystem.Keyboard.current != null)
        {
            var kb = UnityEngine.InputSystem.Keyboard.current;
            float x = 0f, y = 0f;
            if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) x -= 1f;
            if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) x += 1f;
            if (kb.sKey.isPressed || kb.downArrowKey.isPressed) y -= 1f;
            if (kb.wKey.isPressed || kb.upArrowKey.isPressed) y += 1f;
            input = new Vector2(x, y);
        }
#endif
        return input;
    }

    private void ClampToMap()
    {
        float limit = mapHalfSize - edgeMargin;
        Vector2 p = rb.position;
        Vector2 clamped = new Vector2(Mathf.Clamp(p.x, -limit, limit), Mathf.Clamp(p.y, -limit, limit));
        if (clamped != p) rb.position = clamped;
    }

    // ---------- Power-up: TURBO ----------
    public void ApplySpeedBoost(float duration)
    {
        // CORRECCIÓN: antes se usaba StopCoroutine(nameof(...)), que NO detiene corrutinas
        // iniciadas con StartCoroutine(IEnumerator). Ahora se guarda la referencia.
        if (speedRoutine != null) StopCoroutine(speedRoutine);
        speedRoutine = StartCoroutine(SpeedBoostRoutine(duration));
    }

    private IEnumerator SpeedBoostRoutine(float duration)
    {
        speedMultiplier = turboMultiplier;
        if (chickenAnimator != null) chickenAnimator.SetTurbo(duration);
        yield return new WaitForSeconds(duration);
        speedMultiplier = 1f;
        speedRoutine = null;
    }

    // ---------- Power-up: CONGELADO ----------
    public void FreezePlayer(float duration)
    {
        if (freezeRoutine != null) StopCoroutine(freezeRoutine);
        freezeRoutine = StartCoroutine(FreezeRoutine(duration));
    }

    private IEnumerator FreezeRoutine(float duration)
    {
        IsFrozen = true;
        rb.linearVelocity = Vector2.zero;
        if (chickenAnimator != null) chickenAnimator.SetFrozen(duration);
        yield return new WaitForSeconds(duration);
        IsFrozen = false;
        freezeRoutine = null;
    }

    public void SetAnimator(ChickenAnimator anim)
    {
        chickenAnimator = anim;
    }
}
