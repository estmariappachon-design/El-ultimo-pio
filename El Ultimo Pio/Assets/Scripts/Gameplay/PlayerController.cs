using UnityEngine;

public class PlayerController : MonoBehaviour
{
    [Header("Configuración de Movimiento")]
    public float moveSpeed = 15f;

    [SerializeField]
    private VirtualJoystick joystick;

    private Rigidbody2D rb;
    private Vector2 moveInput;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
    }

    private void Update()
    {
        // SI NO HAY JOYSTICK, NO SE MUEVE
        if (joystick == null)
        {
            moveInput = Vector2.zero;
            return;
        }

        // SI NO ESTAMOS TOCANDO EL JOYSTICK, NO SE MUEVE
        if (!joystick.IsPressed)
        {
            moveInput = Vector2.zero;
            return;
        }

        // Solamente ahora leemos la dirección
        moveInput = joystick.Direction;
    }

    private void FixedUpdate()
    {
        if (rb == null)
            return;

        rb.linearVelocity = moveInput * moveSpeed;
    }
}