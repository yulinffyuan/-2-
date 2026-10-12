using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class RotatableSlicePlayer : MonoBehaviour
{
    [Header("基礎移動與跳躍")]
    public float moveSpeed = 6f;
    public float jumpForce = 8f;

    [Header("衝刺 (Dash) 設定")]
    public float dashSpeed = 16f;        
    public float dashDuration = 0.2f;    
    public float dashCooldown = 0.8f;    
    private bool isDashing = false;
    private bool canDash = true;
    private float facingDirection = 1f;  

    [Header("攻擊與 W/S 劈砍設定")]
    public float attackCooldown = 0.26f;   
    public float attackRange = 1.35f;     
    public int attackDamage = 10;
    public float forwardThrustForce = 3.2f;

    [Header("動能反饋力道 (空洞騎士風格)")]
    public float playerRecoilForce = 4.8f;  
    public float pogoBounceForce = 8.8f;    
    public float upSlashRecoilForce = 2.0f; 
    public float hitKnockbackForce = 6.0f;  
    public float hitStopDuration = 0.05f;   

    [Header("攻擊視覺顏色 (一律統一為冷灰色調)")]
    public LayerMask enemyLayer;         
    public Color slashColor = new Color(0.78f, 0.8f, 0.84f, 0.85f);    
    public Color hitSparkColor = new Color(0.85f, 0.85f, 0.88f, 0.9f); 

    private bool canAttack = true;

    [Header("死亡與邊界判定")]
    public float fallDeathThreshold = -10f; // 跌落深淵高度
    public int shatterPiecesCount = 8;
    public float shatterForce = 7f;
    public Material shatterMaterial;
    private bool isDead = false;

    [Header("地面檢測")]
    public Transform groundCheck;
    public LayerMask groundLayer;
    public float groundRadius = 0.25f;

    public float FacingDirection => facingDirection;
    public bool IsDead => isDead;
    public bool isInvulnerable = false; 

    private Rigidbody rb;
    private bool isGrounded;
    private float hInput;
    private float vInput;
    private GhostTrailEmitter trailEmitter;
    private SliceBillboardSprite billboardSprite;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        rb.freezeRotation = true; 

        trailEmitter = GetComponent<GhostTrailEmitter>();
        billboardSprite = GetComponentInChildren<SliceBillboardSprite>();

        var styleSkill = GetComponent<StyleSkill>();
        if (styleSkill != null)
        {
            styleSkill.Init(this);
        }
    }

void Update()
    {
        if (isDead)
        {
            hInput = 0f;
            vInput = 0f;
            return;
        }

        // 對話系統進行中時，鎖死一切玩家輸入
        if (DialogueManager.Instance != null && DialogueManager.Instance.IsDialogueActive)
        {
            hInput = 0f;
            vInput = 0f;
            return;
        }

        // Tab 全景地圖開啟時暫停動作
        if (MapViewerController.Instance != null && MapViewerController.Instance.IsMapOpen)
        {
            hInput = 0f;
            vInput = 0f;
            return;
        }
        
        // ... 原本的移動、跳躍、斬擊維持不變
        // 墜落深淵死亡檢測 (跌出最低邊界即死)
        if (transform.position.y < fallDeathThreshold)
        {
            isInvulnerable = false;
            if (GameManager.Instance != null)
            {
                GameManager.Instance.TriggerPlayerDeath();
            }
            else
            {
                Die();
            }
            return;
        }

        if (MapViewerController.Instance != null && MapViewerController.Instance.IsMapOpen)
        {
            hInput = 0f;
            vInput = 0f;
            return;
        }

        if (isDashing) return;

        hInput = 0f;
        if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow)) hInput -= 1f;
        if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow)) hInput += 1f;

        vInput = 0f;
        if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow)) vInput += 1f;
        if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow)) vInput -= 1f;

        if (hInput != 0f)
        {
            facingDirection = Mathf.Sign(hInput);
            if (billboardSprite != null)
            {
                billboardSprite.SetFacing(facingDirection);
            }
        }

        if (groundCheck != null)
        {
            isGrounded = Physics.CheckSphere(groundCheck.position, groundRadius, groundLayer);
        }
        else
        {
            isGrounded = true;
        }

        if (Input.GetButtonDown("Jump") && isGrounded)
        {
            ApplyJump();
        }

        if (Input.GetKeyDown(KeyCode.LeftShift) && canDash)
        {
            StartCoroutine(PerformDash());
        }

        if ((Input.GetKeyDown(KeyCode.J) || Input.GetMouseButtonDown(0)) && canAttack)
        {
            StartCoroutine(PerformDirectionalAttack());
        }
    }

    void FixedUpdate()
    {
        if (isDead || (MapViewerController.Instance != null && MapViewerController.Instance.IsMapOpen))
        {
            ResetHorizontalVelocity();
            return;
        }

        if (isDashing) return;

        Vector3 sliceRight = Vector3.right;
        if (TransformSliceManager.Instance != null && TransformSliceManager.Instance.mainOrthoCamera != null)
        {
            sliceRight = TransformSliceManager.Instance.mainOrthoCamera.transform.right;
        }

        Vector3 moveVelocity = sliceRight * (hInput * moveSpeed);

        #if UNITY_6000_0_OR_NEWER
        float currentYVel = rb.linearVelocity.y;
        #else
        float currentYVel = rb.velocity.y;
        #endif

        Vector3 finalVelocity = moveVelocity;
        finalVelocity.y = currentYVel;

        #if UNITY_6000_0_OR_NEWER
        rb.linearVelocity = finalVelocity;
        #else
        rb.velocity = finalVelocity;
        #endif
    }

    IEnumerator PerformDirectionalAttack()
    {
        canAttack = false;

        if (AudioManager.Instance != null && AudioManager.Instance.slashSFX != null)
        {
            AudioManager.Instance.PlaySFX(AudioManager.Instance.slashSFX, 0.9f);
        }

        Vector3 sliceRight = Vector3.right;
        Vector3 sliceNormal = Vector3.forward;

        if (TransformSliceManager.Instance != null && TransformSliceManager.Instance.mainOrthoCamera != null)
        {
            sliceRight = TransformSliceManager.Instance.mainOrthoCamera.transform.right;
            sliceNormal = TransformSliceManager.Instance.PlaneNormal;
        }

        SlashEffect.SlashType slashType = SlashEffect.SlashType.Forward;
        Vector3 attackDir = sliceRight * facingDirection;

        if (vInput > 0.2f)
        {
            slashType = SlashEffect.SlashType.Upward;
            attackDir = Vector3.up;
        }
        else if (!isGrounded && vInput < -0.2f)
        {
            slashType = SlashEffect.SlashType.Downward;
            attackDir = Vector3.down;
        }
        else
        {
            #if UNITY_6000_0_OR_NEWER
            rb.linearVelocity += attackDir * forwardThrustForce;
            #else
            rb.velocity += attackDir * forwardThrustForce;
            #endif
        }

        Vector3 slashCenter = transform.position + (attackDir * 0.35f);
        SlashEffect.CreateSlash(slashCenter, sliceRight, sliceNormal, attackRange * 1.35f, slashColor, facingDirection, slashType);

        Vector3 hitboxCenter = transform.position + (attackDir * (attackRange * 0.6f));
        Collider[] hits = Physics.OverlapSphere(hitboxCenter, attackRange, enemyLayer);

        bool hitTarget = false;

        foreach (var hit in hits)
        {
            if (hit.gameObject != gameObject)
            {
                Vector3 contactPoint = hit.ClosestPoint(hitboxCenter);

                if (hit.TryGetComponent<BreakableCrate>(out var crate))
                {
                    crate.TakeDamage(attackDamage);
                    HitEffectSpawner.CreateHitSpark(contactPoint, attackDir, hitSparkColor);
                    hitTarget = true;
                }

                if (hit.TryGetComponent<SliceEnemy>(out var enemy))
                {
                    enemy.TakeDamage(attackDamage);
                    bool isDown = (slashType == SlashEffect.SlashType.Downward);
                    enemy.ApplyKnockback(attackDir, hitKnockbackForce, isDown);

                    HitEffectSpawner.CreateHitSpark(contactPoint, attackDir, hitSparkColor);
                    hitTarget = true;
                }
            }
        }

        if (hitTarget)
        {
            if (slashType == SlashEffect.SlashType.Downward)
            {
                #if UNITY_6000_0_OR_NEWER
                Vector3 vel = rb.linearVelocity;
                vel.y = pogoBounceForce;
                rb.linearVelocity = vel;
                #else
                Vector3 vel = rb.velocity;
                vel.y = pogoBounceForce;
                rb.velocity = vel;
                #endif

                canDash = true;
            }
            else if (slashType == SlashEffect.SlashType.Upward)
            {
                #if UNITY_6000_0_OR_NEWER
                rb.linearVelocity += Vector3.down * upSlashRecoilForce;
                #else
                rb.velocity += Vector3.down * upSlashRecoilForce;
                #endif
            }
            else
            {
                Vector3 recoilDir = -attackDir;
                #if UNITY_6000_0_OR_NEWER
                rb.linearVelocity = recoilDir * playerRecoilForce + Vector3.up * 1.2f;
                #else
                rb.velocity = recoilDir * playerRecoilForce + Vector3.up * 1.2f;
                #endif
            }

            if (hitStopDuration > 0f)
            {
                StartCoroutine(TriggerHitStopRoutine(hitStopDuration));
            }
        }

        yield return new WaitForSeconds(attackCooldown);
        canAttack = true;
    }

    IEnumerator TriggerHitStopRoutine(float duration)
    {
        Time.timeScale = 0.05f;
        yield return new WaitForSecondsRealtime(duration);
        Time.timeScale = 1f;
    }

    public void SetInvulnerable(bool state)
    {
        isInvulnerable = state;
    }

    public void Die()
    {
        if (isDead) return;
        isDead = true;

        rb.isKinematic = true;

        if (billboardSprite != null) billboardSprite.SetVisible(false);

        var col = GetComponent<Collider>();
        if (col != null) col.enabled = false;

        SpawnShatterDebris();
    }

    void SpawnShatterDebris()
    {
        Vector3 sliceRight = Vector3.right;
        if (TransformSliceManager.Instance != null && TransformSliceManager.Instance.mainOrthoCamera != null)
        {
            sliceRight = TransformSliceManager.Instance.mainOrthoCamera.transform.right;
        }

        for (int i = 0; i < shatterPiecesCount; i++)
        {
            GameObject piece = GameObject.CreatePrimitive(PrimitiveType.Cube);
            piece.name = "PlayerDeathDebris";
            piece.transform.position = transform.position + (Vector3)Random.insideUnitCircle * 0.4f;
            piece.transform.localScale = Vector3.one * Random.Range(0.2f, 0.35f);

            if (shatterMaterial != null)
            {
                piece.GetComponent<Renderer>().sharedMaterial = shatterMaterial;
            }

            Rigidbody pieceRb = piece.AddComponent<Rigidbody>();
            float hForce = Random.Range(-1f, 1f);
            Vector3 forceDir = (sliceRight * hForce + Vector3.up * Random.Range(0.8f, 1.8f)).normalized;
            pieceRb.AddForce(forceDir * Random.Range(shatterForce * 0.7f, shatterForce * 1.3f), ForceMode.Impulse);
            pieceRb.AddTorque(Random.insideUnitSphere * 20f, ForceMode.Impulse);

            piece.AddComponent<DebrisFadeDestroy>();
        }
    }

    #region 衝刺與跳躍

    IEnumerator PerformDash()
    {
        canDash = false;
        isDashing = true;

        if (AudioManager.Instance != null && AudioManager.Instance.dashSFX != null)
        {
            AudioManager.Instance.PlaySFX(AudioManager.Instance.dashSFX, 0.85f);
        }

        if (trailEmitter != null) trailEmitter.StartTrail(dashDuration);

        Vector3 sliceRight = Vector3.right;
        if (TransformSliceManager.Instance != null && TransformSliceManager.Instance.mainOrthoCamera != null)
        {
            sliceRight = TransformSliceManager.Instance.mainOrthoCamera.transform.right;
        }

        Vector3 dashVelocity = sliceRight * (facingDirection * dashSpeed);
        dashVelocity.y = 0f;

        #if UNITY_6000_0_OR_NEWER
        rb.linearVelocity = dashVelocity;
        #else
        rb.velocity = dashVelocity;
        #endif

        yield return new WaitForSeconds(dashDuration);

        isDashing = false;
        yield return new WaitForSeconds(dashCooldown);
        canDash = true;
    }

    void ApplyJump()
    {
        if (AudioManager.Instance != null && AudioManager.Instance.jumpSFX != null)
        {
            AudioManager.Instance.PlaySFX(AudioManager.Instance.jumpSFX, 0.75f);
        }

        #if UNITY_6000_0_OR_NEWER
        Vector3 v = rb.linearVelocity;
        v.y = jumpForce;
        rb.linearVelocity = v;
        #else
        Vector3 v = rb.velocity;
        v.y = jumpForce;
        rb.velocity = v;
        #endif
    }

    void ResetHorizontalVelocity()
    {
        #if UNITY_6000_0_OR_NEWER
        Vector3 zeroVel = rb.linearVelocity;
        zeroVel.x = 0f;
        zeroVel.z = 0f;
        rb.linearVelocity = zeroVel;
        #else
        Vector3 zeroVel = rb.velocity;
        zeroVel.x = 0f;
        zeroVel.z = 0f;
        rb.velocity = zeroVel;
        #endif
    }

    #endregion

    void OnDrawGizmosSelected()
    {
        if (groundCheck != null)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(groundCheck.position, groundRadius);
        }
    }
}