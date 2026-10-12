using UnityEngine;

public class HitEffectSpawner : MonoBehaviour
{
    public static void CreateHitSpark(Vector3 hitPos, Vector3 hitNormal, Color sparkColor)
    {
        // 取得當前 2D 切面的右向與上向向量，確保特效平貼在切面內
        Vector3 sliceNormal = Vector3.forward;
        Vector3 sliceUp = Vector3.up;

        if (TransformSliceManager.Instance != null && TransformSliceManager.Instance.mainOrthoCamera != null)
        {
            sliceNormal = TransformSliceManager.Instance.PlaneNormal;
            sliceUp = TransformSliceManager.Instance.mainOrthoCamera.transform.up;
        }

        Vector3 sliceRight = Vector3.Cross(sliceUp, sliceNormal).normalized;

        // 1. 生成 5~7 顆沿切面呈扇形噴射的幾何碎星火花
        int sparkCount = Random.Range(5, 8);
        for (int i = 0; i < sparkCount; i++)
        {
            GameObject spark = GameObject.CreatePrimitive(PrimitiveType.Cube);
            spark.name = "HitSpark";
            spark.transform.position = hitPos;
            spark.transform.localScale = Vector3.one * Random.Range(0.08f, 0.16f);

            // 隨機在 2D 切面平面內決定噴射方向
            float angle = Random.Range(0f, 360f) * Mathf.Deg2Rad;
            Vector3 burstDir = (sliceRight * Mathf.Cos(angle) + sliceUp * Mathf.Sin(angle)).normalized;

            // 稍微偏向受擊反衝方向
            burstDir = (burstDir + hitNormal * 0.6f).normalized;

            // 加入物理衝力
            Rigidbody rb = spark.AddComponent<Rigidbody>();
            rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
            rb.AddForce(burstDir * Random.Range(5f, 9f), ForceMode.Impulse);
            rb.AddTorque(Random.insideUnitSphere * 30f, ForceMode.Impulse);

            // 移除碰撞避免推擠主角
            Collider col = spark.GetComponent<Collider>();
            if (col != null) col.enabled = false;

            // 掛載自發光著色與自動銷毀
            var sparkFX = spark.AddComponent<SparkParticleAnimator>();
            sparkFX.Init(sparkColor);
        }
    }
}

// 控制單顆碎星火花的縮小淡出動畫
public class SparkParticleAnimator : MonoBehaviour
{
    private float lifeTime = 0.22f;
    private float timer = 0f;
    private Vector3 initialScale;
    private Material matInstance;

    public void Init(Color color)
    {
        initialScale = transform.localScale;
        var rend = GetComponent<Renderer>();
        if (rend != null)
        {
            // 使用無光照或頂點發光材質
            matInstance = new Material(Shader.Find("Sprites/Default"));
            matInstance.color = color;
            rend.material = matInstance;
        }
    }

    void Update()
    {
        timer += Time.unscaledDeltaTime; // 受 Hit-Stop 影響時依然流暢縮小
        float t = timer / lifeTime;

        // 隨時間快速等比縮小
        transform.localScale = Vector3.Lerp(initialScale, Vector3.zero, t);

        if (timer >= lifeTime)
        {
            if (matInstance != null) Destroy(matInstance);
            Destroy(gameObject);
        }
    }
}