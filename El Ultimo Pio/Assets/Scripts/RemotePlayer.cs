using UnityEngine;

// Se agrega automáticamente a los pollos de los otros jugadores.
// Se mueve suavemente hacia la última posición que llegó por red (interpolación).
public class RemotePlayer : MonoBehaviour
{
    [SerializeField] private float smoothing = 15f;

    public int Slot { get; private set; }
    public ChickenAnimator Anim { get; private set; }

    private Vector3 target;

    public void Init(int slot, Vector2 position, ChickenAnimator anim)
    {
        Slot = slot;
        Anim = anim;
        target = position;
        transform.position = target;
    }

    public void SetTarget(Vector2 position)
    {
        target = position;
    }

    // Teletransporte: aparece de golpe en la nueva posición (sin deslizarse)
    public void Snap(Vector2 position)
    {
        target = position;
        transform.position = target;
        if (Anim != null) Anim.ResetMotion();
    }

    private void Update()
    {
        float t = 1f - Mathf.Exp(-smoothing * Time.deltaTime);
        transform.position = Vector3.Lerp(transform.position, target, t);
    }
}
