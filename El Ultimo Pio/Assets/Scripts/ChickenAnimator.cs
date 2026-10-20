using UnityEngine;

// =====================================================================================
//  Animación del pollo en la partida (vista desde arriba)
//  - Usa la imagen del color que eligió el jugador, con 3 estados:
//      NORMAL, TURBO (velocidad) y CONGELADO  -> cambia de imagen sola
//  - ROTA hacia donde se mueve (joystick / flechas en el local, movimiento en los remotos)
//  - Al caminar "late" un poco; quieto "respira"; congelado tiembla; turbo deja estela
//  No necesita Animator. GameManager lo agrega y le pasa las imágenes con SetSkin().
// =====================================================================================
public class ChickenAnimator : MonoBehaviour
{
    [Header("Rotación")]
    [SerializeField] private bool rotateToMovement = true;
    [Tooltip("Hacia dónde mira el pollo en la imagen original: 90 = arriba, 0 = derecha, -90 = abajo, 180 = izquierda")]
    [SerializeField] private float spriteFacingAngle = 90f;
    [SerializeField] private float turnSpeed = 720f;          // grados por segundo
    [SerializeField] private float minSpeedToRotate = 1.5f;   // unidades/seg (pollos remotos)

    [Header("Idle")]
    [SerializeField] private float breathAmount = 0.04f;
    [SerializeField] private float breathSpeed = 3f;

    [Header("Caminar")]
    [SerializeField] private float walkPulse = 0.07f;          // cuánto "late" al caminar
    [SerializeField] private float walkWaddle = 7f;            // grados de balanceo
    [SerializeField] private float walkCycleSpeed = 14f;
    [SerializeField] private float movingThreshold = 0.4f;

    [Header("Efectos (si falta la imagen del estado, se usa un tinte)")]
    [SerializeField] private Color turboColor = new Color(1f, 0.85f, 0.2f);
    [SerializeField] private Color frozenColor = new Color(0.45f, 0.8f, 1f);
    [SerializeField] private float trailTime = 0.35f;

    public SpriteRenderer BodyRenderer { get; private set; }

    private Transform visual;
    private TrailRenderer trail;
    private Color baseColor = Color.white;
    private Vector3 baseScale = Vector3.one;

    private Sprite normalSprite, speedSprite, frozenSprite;

    private PlayerController controller;
    private Vector3 lastPos;
    private float smoothedSpeed;
    private float phase;
    private float turboUntil;
    private float frozenUntil;
    private float facing;         // ángulo actual (grados, 0 = derecha)
    private float targetFacing;

    private void Awake()
    {
        controller = GetComponent<PlayerController>();
        SetupVisual();
        lastPos = transform.position;
        facing = targetFacing = spriteFacingAngle;
    }

    // Separa el dibujo en un hijo "Visual": se anima/rota sin mover el Rigidbody ni el Collider,
    // y las etiquetas ("TÚ", "J2") no giran con el pollo.
    private void SetupVisual()
    {
        Transform existing = transform.Find("Visual");
        if (existing != null)
        {
            visual = existing;
            BodyRenderer = existing.GetComponent<SpriteRenderer>();
            trail = existing.GetComponent<TrailRenderer>();
        }
        else
        {
            SpriteRenderer rootSr = GetComponent<SpriteRenderer>();

            GameObject v = new GameObject("Visual");
            visual = v.transform;
            visual.SetParent(transform, false);

            BodyRenderer = v.AddComponent<SpriteRenderer>();
            if (rootSr != null)
            {
                BodyRenderer.sprite = rootSr.sprite;
                BodyRenderer.color = rootSr.color;
                BodyRenderer.sharedMaterial = rootSr.sharedMaterial;
                BodyRenderer.sortingLayerID = rootSr.sortingLayerID;
                BodyRenderer.sortingOrder = rootSr.sortingOrder;
                rootSr.enabled = false;
            }
        }

        if (BodyRenderer != null)
        {
            baseColor = BodyRenderer.color;
            BodyRenderer.flipX = false;
            if (normalSprite == null) normalSprite = BodyRenderer.sprite;
        }
        baseScale = visual.localScale;

        if (trail == null)
        {
            trail = visual.gameObject.AddComponent<TrailRenderer>();
            trail.time = trailTime;
            trail.minVertexDistance = 0.1f;
            trail.startWidth = 1.2f;
            trail.endWidth = 0f;
            if (BodyRenderer != null) trail.sharedMaterial = BodyRenderer.sharedMaterial;
            trail.startColor = new Color(turboColor.r, turboColor.g, turboColor.b, 0.7f);
            trail.endColor = new Color(turboColor.r, turboColor.g, turboColor.b, 0f);
            trail.sortingOrder = (BodyRenderer != null ? BodyRenderer.sortingOrder : 100) - 1;
        }
        trail.emitting = false;
    }

    // ---------- Configuración desde GameManager ----------

    public void SetSkin(Sprite normal, Sprite speed, Sprite frozen)
    {
        normalSprite = normal;
        speedSprite = speed;
        frozenSprite = frozen;
        if (BodyRenderer != null && normal != null) BodyRenderer.sprite = normal;
    }

    public void SetFacingSettings(bool rotate, float imageFacingAngle)
    {
        rotateToMovement = rotate;
        spriteFacingAngle = imageFacingAngle;
    }

    public void SetBaseColor(Color c)
    {
        baseColor = c;
        if (BodyRenderer != null) BodyRenderer.color = c;
    }

    public void SetTurbo(float duration)  { turboUntil = Time.time + duration; }

    // Después de un teletransporte: que el salto de posición no cuente como "caminar"
    public void ResetMotion()
    {
        lastPos = transform.position;
        smoothedSpeed = 0f;
        if (trail != null) trail.Clear();
    }
    public void SetFrozen(float duration) { frozenUntil = Time.time + duration; }
    public bool IsFrozenVisual => Time.time < frozenUntil;

    // ---------- Cada frame ----------

    private void Update()
    {
        if (visual == null || BodyRenderer == null) return;

        float dt = Mathf.Max(Time.deltaTime, 0.0001f);
        Vector3 delta = transform.position - lastPos;
        lastPos = transform.position;

        float speed = delta.magnitude / dt;
        smoothedSpeed = Mathf.Lerp(smoothedSpeed, speed, 1f - Mathf.Exp(-12f * dt));

        bool frozen = Time.time < frozenUntil || (controller != null && controller.isLocalPlayer && controller.IsFrozen);
        bool turbo = !frozen && (Time.time < turboUntil || (controller != null && controller.isLocalPlayer && controller.IsBoosted));
        bool moving = smoothedSpeed > movingThreshold && !frozen;

        UpdateFacing(delta, dt, frozen);
        UpdateSprite(frozen, turbo);

        // Pulso / respiración / temblor
        Vector3 pos = Vector3.zero;
        float waddle = 0f;
        Vector3 scale = baseScale;

        if (frozen)
        {
            pos.x = Mathf.Sin(Time.time * 60f) * 0.08f;
        }
        else if (moving)
        {
            phase += dt * walkCycleSpeed * (turbo ? 1.5f : 1f);
            float p = Mathf.Abs(Mathf.Sin(phase)) * walkPulse;
            scale = new Vector3(baseScale.x * (1f + p), baseScale.y * (1f + p), baseScale.z);
            waddle = Mathf.Sin(phase) * walkWaddle;
        }
        else
        {
            phase = 0f;
            float s = Mathf.Sin(Time.time * breathSpeed) * breathAmount;
            scale = new Vector3(baseScale.x * (1f - s), baseScale.y * (1f + s), baseScale.z);
        }

        float angle = rotateToMovement ? facing - spriteFacingAngle : 0f;
        visual.localPosition = pos;
        visual.localRotation = Quaternion.Euler(0f, 0f, angle + waddle);
        visual.localScale = scale;

        if (trail != null) trail.emitting = turbo && moving;
    }

    private void UpdateFacing(Vector3 delta, float dt, bool frozen)
    {
        if (!rotateToMovement || frozen) return;

        Vector2 dir = Vector2.zero;

        // Pollo local: usa directamente el joystick o las flechas (responde al instante)
        if (controller != null && controller.isLocalPlayer && controller.enabled)
            dir = controller.MoveInput;

        // Pollos remotos (o si no hay entrada): usa hacia dónde se movió
        if (dir.sqrMagnitude < 0.02f && delta.magnitude / dt > minSpeedToRotate)
            dir = delta;

        if (dir.sqrMagnitude > 0.0001f)
            targetFacing = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;

        facing = Mathf.MoveTowardsAngle(facing, targetFacing, turnSpeed * dt);
    }

    private void UpdateSprite(bool frozen, bool turbo)
    {
        Sprite wanted = normalSprite;
        Color color = baseColor;

        if (frozen)
        {
            if (frozenSprite != null) wanted = frozenSprite;
            else color = baseColor * frozenColor;
        }
        else if (turbo)
        {
            if (speedSprite != null) wanted = speedSprite;
            else color = Color.Lerp(baseColor, turboColor, 0.35f + 0.35f * Mathf.Sin(Time.time * 20f));
        }

        if (wanted != null && BodyRenderer.sprite != wanted) BodyRenderer.sprite = wanted;
        BodyRenderer.color = color;
    }
}
