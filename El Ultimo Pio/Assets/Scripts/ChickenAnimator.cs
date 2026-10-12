using UnityEngine;

// Animaciones procedurales del pollo (no necesita Animator ni hojas de sprites):
//  - IDLE: "respira" (se estira y encoge suavemente)
//  - CAMINAR: rebota y se balancea; mira hacia donde camina
//  - TURBO: brillo dorado intermitente + estela
//  - CONGELADO: color hielo + temblor
// Funciona igual para el pollo local y para los remotos (calcula la velocidad por el cambio de posición).
// GameManager lo agrega automáticamente si el pollo no lo tiene.
public class ChickenAnimator : MonoBehaviour
{
    [Header("Idle")]
    [SerializeField] private float breathAmount = 0.04f;
    [SerializeField] private float breathSpeed = 3f;

    [Header("Caminar")]
    [SerializeField] private float walkBounce = 0.25f;     // altura del saltito (unidades)
    [SerializeField] private float walkWaddle = 9f;        // grados de balanceo
    [SerializeField] private float walkCycleSpeed = 14f;
    [SerializeField] private float movingThreshold = 0.4f;
    [SerializeField] private bool spriteLooksRight = true; // ¿el dibujo original mira a la derecha?

    [Header("Efectos")]
    [SerializeField] private Color turboColor = new Color(1f, 0.85f, 0.2f);
    [SerializeField] private Color frozenColor = new Color(0.45f, 0.8f, 1f);
    [SerializeField] private float trailTime = 0.35f;

    public SpriteRenderer BodyRenderer { get; private set; }

    private Transform visual;
    private TrailRenderer trail;
    private Color baseColor = Color.white;
    private Vector3 baseScale = Vector3.one;

    private Vector3 lastPos;
    private float smoothedSpeed;
    private float phase;
    private float turboUntil;
    private float frozenUntil;

    private void Awake()
    {
        SetupVisual();
        lastPos = transform.position;
    }

    // Separa el dibujo en un hijo "Visual": así se puede animar sin mover el Rigidbody/Collider
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
                BodyRenderer.flipX = rootSr.flipX;
                rootSr.enabled = false;
            }
        }

        if (BodyRenderer != null) baseColor = BodyRenderer.color;
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

    // ApplySkin del GameManager llama esto al cambiar el sprite/color del jugador
    public void SetBaseColor(Color c)
    {
        baseColor = c;
        if (BodyRenderer != null) BodyRenderer.color = c;
    }

    public void SetTurbo(float duration)
    {
        turboUntil = Time.time + duration;
    }

    public void SetFrozen(float duration)
    {
        frozenUntil = Time.time + duration;
    }

    public bool IsFrozenVisual => Time.time < frozenUntil;

    private void Update()
    {
        if (visual == null || BodyRenderer == null) return;

        float dt = Mathf.Max(Time.deltaTime, 0.0001f);
        Vector3 delta = transform.position - lastPos;
        lastPos = transform.position;

        float speed = delta.magnitude / dt;
        smoothedSpeed = Mathf.Lerp(smoothedSpeed, speed, 1f - Mathf.Exp(-12f * dt));

        bool frozen = Time.time < frozenUntil;
        bool turbo = Time.time < turboUntil && !frozen;
        bool moving = smoothedSpeed > movingThreshold && !frozen;

        // Mirar hacia donde camina
        if (!frozen && Mathf.Abs(delta.x) > 0.01f)
            BodyRenderer.flipX = spriteLooksRight ? delta.x < 0f : delta.x > 0f;

        Vector3 pos = Vector3.zero;
        float angle = 0f;
        Vector3 scale = baseScale;

        if (frozen)
        {
            // Temblor de frío
            pos.x = Mathf.Sin(Time.time * 60f) * 0.08f;
        }
        else if (moving)
        {
            phase += dt * walkCycleSpeed * (turbo ? 1.5f : 1f);
            pos.y = Mathf.Abs(Mathf.Sin(phase)) * walkBounce;
            angle = Mathf.Sin(phase) * walkWaddle;
        }
        else
        {
            phase = 0f;
            float s = Mathf.Sin(Time.time * breathSpeed) * breathAmount;
            scale = new Vector3(baseScale.x * (1f - s), baseScale.y * (1f + s), baseScale.z);
        }

        visual.localPosition = pos;
        visual.localRotation = Quaternion.Euler(0f, 0f, angle);
        visual.localScale = scale;

        // Color según estado
        if (frozen)
            BodyRenderer.color = baseColor * frozenColor;
        else if (turbo)
            BodyRenderer.color = Color.Lerp(baseColor, turboColor, 0.35f + 0.35f * Mathf.Sin(Time.time * 20f));
        else
            BodyRenderer.color = baseColor;

        if (trail != null) trail.emitting = turbo && moving;
    }
}
