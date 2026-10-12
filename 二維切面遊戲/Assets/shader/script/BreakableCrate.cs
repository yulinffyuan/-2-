using UnityEngine;

public class BreakableCrate : MonoBehaviour
{
    [Header("箱子屬性")]
    public int maxHealth = 10;
    private int currentHealth;

    [Header("碎裂效果")]
    public int fragmentCount = 6;      // 碎片數量
    public float explosionForce = 5f;  // 爆破噴散力道
    public Material crateMaterial;     // 箱子材質 (使用 M_SliceWorld)

    void Start()
    {
        currentHealth = maxHealth;
    }

    // 接收來自主角斬擊的傷害呼叫
    public void TakeDamage(int damage)
    {
        currentHealth -= damage;

        // 輕微受擊晃動回饋
        transform.localScale = Vector3.one * 0.9f;

        if (currentHealth <= 0)
        {
            BreakCrate();
        }
        else
        {
            // 播放被砍中但未碎裂的音效
            if (AudioManager.Instance != null && AudioManager.Instance.crateHitSFX != null)
            {
                AudioManager.Instance.PlaySFX(AudioManager.Instance.crateHitSFX, 0.8f);
            }
            Invoke(nameof(ResetScale), 0.05f);
        }
    }

    void ResetScale()
    {
        transform.localScale = Vector3.one;
    }

    void BreakCrate()
    {
        // 在當前箱子位置播放 3D 碎裂音效（隨距離衰減）
        if (AudioManager.Instance != null && AudioManager.Instance.crateBreakSFX != null)
        {
            AudioManager.Instance.PlaySFXAtPosition(
                AudioManager.Instance.crateBreakSFX, 
                transform.position, 
                1.0f
            );
        }

        SpawnFragments();
        Destroy(gameObject);
    }

    void SpawnFragments()
    {
        Vector3 center = transform.position;
        Vector3 size = transform.localScale;

        for (int i = 0; i < fragmentCount; i++)
        {
            GameObject fragment = GameObject.CreatePrimitive(PrimitiveType.Cube);
            fragment.name = "CrateDebris";
            
            float debrisScale = Random.Range(0.2f, 0.35f);
            fragment.transform.localScale = Vector3.one * debrisScale;

            Vector3 offset = new Vector3(
                Random.Range(-0.3f, 0.3f) * size.x,
                Random.Range(0f, 0.4f) * size.y,
                Random.Range(-0.3f, 0.3f) * size.z
            );
            fragment.transform.position = center + offset;

            if (crateMaterial != null)
            {
                fragment.GetComponent<Renderer>().sharedMaterial = crateMaterial;
            }

            Rigidbody rb = fragment.AddComponent<Rigidbody>();
            Vector3 blastDir = (offset.normalized + Vector3.up * 0.8f).normalized;
            rb.AddForce(blastDir * Random.Range(explosionForce * 0.7f, explosionForce * 1.3f), ForceMode.Impulse);
            rb.AddTorque(Random.insideUnitSphere * 15f, ForceMode.Impulse);

            fragment.layer = LayerMask.NameToLayer("Default");
            fragment.AddComponent<DebrisFadeDestroy>();
        }
    }
}