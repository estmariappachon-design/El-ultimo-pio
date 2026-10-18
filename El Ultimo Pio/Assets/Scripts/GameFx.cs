using TMPro;
using UnityEngine;

// Efectos visuales sencillos hechos por código (no necesitan prefabs):
//  - Pop: el maíz recogido crece y se desvanece
//  - Texto flotante: "+1", "+2 ¡TURBO!", "¡CONGELADO!" sobre el pollo
public static class GameFx
{
    // El estilo de los textos emergentes se edita en Assets/Resources/EstiloTextos > Popups

    public static void Pop(SpriteRenderer source, float duration = 0.3f)
    {
        if (source == null || source.sprite == null) return;

        GameObject go = new GameObject("FX_Pop");
        go.transform.position = source.transform.position;
        go.transform.localScale = source.transform.lossyScale;

        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = source.sprite;
        sr.color = source.color;
        sr.sharedMaterial = source.sharedMaterial;
        sr.sortingLayerID = source.sortingLayerID;
        sr.sortingOrder = source.sortingOrder + 1;

        FxFade fade = go.AddComponent<FxFade>();
        fade.Init(duration, 1.8f, Vector3.zero, sr, null);
    }

    // color y size se ignoran: se usan los de EstiloTextos (así todos los emergentes se ven iguales)
    public static void FloatingText(Vector3 worldPos, string text, Color color, float size = 0f, float duration = 0.9f)
    {
        GameObject go = new GameObject("FX_Text");
        go.transform.position = worldPos + Vector3.up * 2f;

        TextMeshPro tmp = go.AddComponent<TextMeshPro>();
        tmp.text = text;
        tmp.alignment = TextAlignmentOptions.Center;
        TextStyle.Current.Apply(tmp, TextStyle.Current.popups);
        tmp.sortingOrder = 300;
        tmp.rectTransform.sizeDelta = new Vector2(60f, 12f);

        FxFade fade = go.AddComponent<FxFade>();
        fade.Init(duration, 1f, Vector3.up * 3f, null, tmp);
    }
}
