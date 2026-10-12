using UnityEngine;

public class DebrisFadeDestroy : MonoBehaviour
{
    public float lifetime = 1.2f;
    private float timer = 0f;
    private Vector3 initialScale;

    void Start()
    {
        initialScale = transform.localScale;
    }

    void Update()
    {
        timer += Time.deltaTime;
        
        // 後半段時間平滑縮小消失
        if (timer > lifetime * 0.6f)
        {
            float t = (timer - lifetime * 0.6f) / (lifetime * 0.4f);
            transform.localScale = Vector3.Lerp(initialScale, Vector3.zero, t);
        }

        if (timer >= lifetime)
        {
            Destroy(gameObject);
        }
    }
}