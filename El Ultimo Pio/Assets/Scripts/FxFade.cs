using TMPro;
using UnityEngine;

// Hace crecer/subir y desvanecer un efecto y luego lo destruye (lo usa GameFx)
public class FxFade : MonoBehaviour
{
    private float duration;
    private float endScale;
    private Vector3 velocity;
    private SpriteRenderer sr;
    private TextMeshPro tmp;
    private float t;
    private Vector3 startScale;
    private Color startColor;

    public void Init(float duration, float endScale, Vector3 velocity, SpriteRenderer sr, TextMeshPro tmp)
    {
        this.duration = Mathf.Max(0.05f, duration);
        this.endScale = endScale;
        this.velocity = velocity;
        this.sr = sr;
        this.tmp = tmp;
        startScale = transform.localScale;
        startColor = sr != null ? sr.color : (tmp != null ? tmp.color : Color.white);
    }

    private void Update()
    {
        t += Time.deltaTime;
        float k = Mathf.Clamp01(t / duration);

        transform.position += velocity * Time.deltaTime;
        transform.localScale = startScale * Mathf.Lerp(1f, endScale, k);

        Color c = startColor;
        c.a = startColor.a * (1f - k);
        if (sr != null) sr.color = c;
        if (tmp != null) tmp.color = c;

        if (k >= 1f) Destroy(gameObject);
    }
}
