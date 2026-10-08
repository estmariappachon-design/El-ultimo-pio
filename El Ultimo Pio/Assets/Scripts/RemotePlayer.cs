using UnityEngine;

// Se agrega automáticamente a los pollos de los otros jugadores.
// Solo se mueve hacia la última posición que llegó por red, de forma suave.
public class RemotePlayer : MonoBehaviour
{
    [SerializeField] private float smoothing = 15f;

    private Vector3 target;

    public void Init(Vector2 position)
    {
        target = position;
        transform.position = target;
    }

    public void SetTarget(Vector2 position)
    {
        target = position;
    }

    private void Update()
    {
        float t = 1f - Mathf.Exp(-smoothing * Time.deltaTime);
        transform.position = Vector3.Lerp(transform.position, target, t);
    }
}