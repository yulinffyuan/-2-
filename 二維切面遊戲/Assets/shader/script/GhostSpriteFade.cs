using UnityEngine;

public class GhostSpriteFade : MonoBehaviour
{
    private Material matInstance;
    private Color initialColor;
    private float fadeDuration;
    private float timer = 0f;

    public void Init(Color color, float duration)
    {
        fadeDuration = duration;
        initialColor = color;

        // 建立獨立材質實例，避免修改全域屬性
        Renderer rend = GetComponent<Renderer>();
        if (rend != null)
        {
            matInstance = rend.material;
            matInstance.SetColor("_Color", initialColor);
        }
    }

    void Update()
    {
        timer += Time.deltaTime;
        float progress = timer / fadeDuration;

        if (matInstance != null)
        {
            Color c = initialColor;
            c.a = Mathf.Lerp(initialColor.a, 0f, progress);
            matInstance.SetColor("_Color", c);
        }

        if (progress >= 1f)
        {
            Destroy(gameObject);
        }
    }

    void OnDestroy()
    {
        if (matInstance != null)
        {
            Destroy(matInstance);
        }
    }
}