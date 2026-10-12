using UnityEngine;

[RequireComponent(typeof(Collider))]
public class SliceEnemy : MonoBehaviour
{
    public enum MoveAxis 
    { 
        HorizontalX, // 僅沿世界 X 軸左右巡邏
        HorizontalZ, // 僅沿世界 Z 軸前後巡邏
        VerticalY    // 僅沿世界 Y 軸垂直升降巡邏
    }

    [Header("運動模式 (嚴格單軸移動，杜絕斜向)")]
    public MoveAxis patrolAxis = MoveAxis.HorizontalX;
    public float patrolDistance = 4f;   // 單向巡邏距離
    public float moveSpeed = 2f;        // 巡邏移動速度
    public float waitTimeAtTurn = 0.5f; // 抵達端點時的折返等待時間

    [Header("生命值與打擊")]
    public int maxHealth = 20;
    private int currentHealth;

    [Header("切面感應門檻")]
    public float sliceTolerance = 1.0f; // 膠囊表面距離切面多近算「進入 2D 世界」

    [Header("死亡碎裂效果")]
    public int deathDebrisCount = 8;
    public float explosionForce = 6f;
    public Material debrisMaterial;
    public AudioClip hitSFX;
    public AudioClip deathSFX;

    // 巡邏狀態
    private Vector3 startPosition;
    private float moveDirection = 1f;
    private float currentOffset = 0f;
    private float waitTimer = 0f;

    // 碰撞與切面可見性
    private Collider enemyCollider;
    private bool isInSlice = false;

    void Start()
    {
        currentHealth = maxHealth;
        startPosition = transform.position;
        enemyCollider = GetComponent<Collider>();
    }

    void Update()
    {
        // 1. 即時檢測膠囊體是否與當前 2D 切面相交或相切
        CheckSliceStatus();

        // 2. 嚴格單軸巡邏 (無論是否在切面內，3D 空間中均持續巡邏)
        HandlePatrolMovement();
    }

    /// <summary>
    /// 計算膠囊體中心與半徑，判斷表面是否與切面相切/相交
    /// </summary>
    void CheckSliceStatus()
    {
        if (TransformSliceManager.Instance == null) return;

        Vector3 planeOrigin = TransformSliceManager.Instance.PlaneOrigin;
        Vector3 planeNormal = TransformSliceManager.Instance.PlaneNormal;

        // 計算敵人中心到切面的絕對垂直距離
        float distToPlane = Mathf.Abs(Vector3.Dot(transform.position - planeOrigin, planeNormal));

        // 動態取得膠囊體半徑補償 (相切感應核心)
        float bodyRadius = 0.5f;
        if (TryGetComponent<CapsuleCollider>(out var capCol))
        {
            float maxScale = Mathf.Max(transform.localScale.x, transform.localScale.z);
            bodyRadius = capCol.radius * maxScale;
        }

        // 當「中心距離 - 半徑」小於門檻時，判定表面已切入 2D 切面
        float effectiveDist = Mathf.Max(0f, distToPlane - bodyRadius);
        isInSlice = (effectiveDist <= sliceTolerance);

        // 只有在切面內時才啟用 Collider (提供受擊與碰觸判定)
        if (enemyCollider != null && enemyCollider.enabled != isInSlice)
        {
            enemyCollider.enabled = isInSlice;
        }
    }

    /// <summary>
    /// 嚴格沿所選單一軸向移動，杜絕任何斜向合成
    /// </summary>
    void HandlePatrolMovement()
    {
        if (waitTimer > 0f)
        {
            waitTimer -= Time.deltaTime;
            return;
        }

        float delta = moveSpeed * Time.deltaTime * moveDirection;
        currentOffset += delta;

        // 抵達端點時折返
        if (Mathf.Abs(currentOffset) >= patrolDistance)
        {
            currentOffset = Mathf.Sign(currentOffset) * patrolDistance;
            moveDirection *= -1f;
            waitTimer = waitTimeAtTurn;
        }

        // 嚴格依選定軸更新位置
        Vector3 newPos = startPosition;
        switch (patrolAxis)
        {
            case MoveAxis.HorizontalX:
                newPos.x += currentOffset;
                break;
            case MoveAxis.HorizontalZ:
                newPos.z += currentOffset;
                break;
            case MoveAxis.VerticalY:
                newPos.y += currentOffset;
                break;
        }

        transform.position = newPos;
    }

    /// <summary>
    /// 受到玩家斬擊傷害
    /// </summary>
    public void TakeDamage(int damage)
    {
        if (!isInSlice) return; // 不在切面上無法受到傷害

        currentHealth -= damage;

        if (AudioManager.Instance != null && hitSFX != null)
        {
            AudioManager.Instance.PlaySFX(hitSFX, 0.85f);
        }

        // 受擊微幅縮放回饋
        transform.localScale = Vector3.one * 0.8f;
        Invoke(nameof(ResetScale), 0.08f);

        if (currentHealth <= 0)
        {
            Die();
        }
    }

    void ResetScale()
    {
        transform.localScale = Vector3.one;
    }

    /// <summary>
    /// 接收玩家攻擊造成的擊退位移或下劈定身停滯
    /// </summary>
    public void ApplyKnockback(Vector3 direction, float force, bool isDownSlash = false)
    {
        if (isDownSlash)
        {
            // --- 下劈命中：不給予向下衝量防穿地，改為原地停滯 ---
            waitTimer = 0.35f; // 原地凍結巡邏 0.35 秒

            if (TryGetComponent<Rigidbody>(out var rb))
            {
                #if UNITY_6000_0_OR_NEWER
                rb.linearVelocity = Vector3.zero;
                #else
                rb.velocity = Vector3.zero;
                #endif
            }

            // 視覺知覺回饋：向下壓扁的 Squash 效果
            transform.localScale = new Vector3(1.2f, 0.65f, 1.2f);
            Invoke(nameof(ResetScale), 0.12f);
            return;
        }

        // --- 一般水平/上劈命中 ---
        waitTimer = 0.15f; // 一般受擊硬直

        // 鎖定 Y 軸推力，防止任何常規攻擊將敵人推入地下
        direction.y = Mathf.Max(0f, direction.y);

        if (TryGetComponent<Rigidbody>(out var enemyRb) && !enemyRb.isKinematic)
        {
            enemyRb.AddForce(direction * force, ForceMode.Impulse);
        }
        else
        {
            transform.position += direction * (force * 0.04f);
            startPosition += direction * (force * 0.04f);
        }
    }

    /// <summary>
    /// 死亡時碎裂爆破
    /// </summary>
    void Die()
    {
        if (AudioManager.Instance != null && deathSFX != null)
        {
            AudioManager.Instance.PlaySFXAtPosition(deathSFX, transform.position, 1f);
        }

        SpawnDeathFragments();
        Destroy(gameObject);
    }

    void SpawnDeathFragments()
    {
        for (int i = 0; i < deathDebrisCount; i++)
        {
            GameObject frag = GameObject.CreatePrimitive(PrimitiveType.Cube);
            frag.name = "EnemyDebris";
            frag.transform.position = transform.position + Random.insideUnitSphere * 0.35f;
            frag.transform.localScale = Vector3.one * Random.Range(0.2f, 0.35f);

            if (debrisMaterial != null)
            {
                frag.GetComponent<Renderer>().sharedMaterial = debrisMaterial;
            }

            Rigidbody rb = frag.AddComponent<Rigidbody>();
            rb.AddForce(Random.insideUnitSphere * explosionForce + Vector3.up * 2.5f, ForceMode.Impulse);
            rb.AddTorque(Random.insideUnitSphere * 20f, ForceMode.Impulse);

            frag.AddComponent<DebrisFadeDestroy>();
        }
    }

    // 碰撞致死判定
    void OnTriggerEnter(Collider other)
    {
        HandlePlayerCollision(other.gameObject);
    }

    void OnCollisionEnter(Collision collision)
    {
        HandlePlayerCollision(collision.gameObject);
    }

  void HandlePlayerCollision(GameObject target)
    {
        // 只有同在切面可見狀態下碰撞才生效
        if (!isInSlice) return;

        // 先取得 RotatableSlicePlayer 元件
        RotatableSlicePlayer player = target.GetComponent<RotatableSlicePlayer>();

        // 如果碰撞目標是主角
        if (player != null || target.CompareTag("Player"))
        {
            // 防暴斃判定：若主角處於居合斬無敵狀態或已死亡，不觸發碰撞死亡
            if (player != null && (player.isInvulnerable || player.IsDead))
            {
                return;
            }

            if (GameManager.Instance != null)
            {
                GameManager.Instance.TriggerPlayerDeath();
            }
        }
    }

    void OnDrawGizmosSelected()
    {
        // Scene 視窗紅線繪製巡邏路線輔助視覺
        Gizmos.color = Color.red;
        Vector3 start = Application.isPlaying ? startPosition : transform.position;
        Vector3 endA = start;
        Vector3 endB = start;

        switch (patrolAxis)
        {
            case MoveAxis.HorizontalX:
                endA.x -= patrolDistance;
                endB.x += patrolDistance;
                break;
            case MoveAxis.HorizontalZ:
                endA.z -= patrolDistance;
                endB.z += patrolDistance;
                break;
            case MoveAxis.VerticalY:
                endA.y -= patrolDistance;
                endB.y += patrolDistance;
                break;
        }

        Gizmos.DrawLine(endA, endB);
        Gizmos.DrawWireSphere(endA, 0.2f);
        Gizmos.DrawWireSphere(endB, 0.2f);
    }
}