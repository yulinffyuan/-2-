using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RoninIaiSkill : StyleSkill
{
    [Header("居合斬數值")]
    public float dashDistance = 6.5f;        // 居合突進距離
    public float dashSpeed = 38f;             // 居合瞬步速度
    public int iaiDamage = 25;                // 居合清算高額傷害
    public float iaiHitStop = 0.08f;          // 終點納刀頓幀時間
    public LayerMask targetLayer;             // 敵人物件與箱子圖層 (Enemy + Sliceable)
    public LayerMask obstacleLayer;           // 障礙物/地表圖層 (防穿牆)
    public Color iaiColor = new Color(0.85f, 0.88f, 0.92f, 0.95f); // 居合冷銀刃光

    [Header("蓄勢與時緩 (Aim & Bullet Time)")]
    public float slowMotionTimeScale = 0.15f; // 長按右鍵時的時間流速
    public float maxChargeTime = 2.5f;        // 最長蓄勢時間 (超時自動釋放)
    private bool isCharging = false;
    private float chargeTimer = 0f;
    private Vector3 aimDirection = Vector3.right;
    private LineRenderer aimTrajectoryLine;

    [Header("音效")]
    public AudioClip chargeSFX;               // 拔刀出鞘/時緩蓄勢音效
    public AudioClip dashSFX;                 // 破空居合瞬步音效
    public AudioClip sheathSFX;               // 終點納刀爆發音效

    private bool isExecuting = false;

    void Awake()
    {
        skillName = "居合・閃 (Iai Strike)";
        triggerKey = KeyCode.Mouse1; // 滑鼠右鍵
        cooldown = 1.8f;

        CreateTrajectoryLine();
    }

    void CreateTrajectoryLine()
    {
        GameObject lineObj = new GameObject("IaiTrajectoryLine");
        lineObj.transform.SetParent(transform);
        aimTrajectoryLine = lineObj.AddComponent<LineRenderer>();
        aimTrajectoryLine.useWorldSpace = true;
        aimTrajectoryLine.positionCount = 2;
        aimTrajectoryLine.startWidth = 0.05f;
        aimTrajectoryLine.endWidth = 0.02f;
        
        Material lineMat = new Material(Shader.Find("Sprites/Default"));
        aimTrajectoryLine.material = lineMat;
        aimTrajectoryLine.startColor = new Color(iaiColor.r, iaiColor.g, iaiColor.b, 0.7f);
        aimTrajectoryLine.endColor = new Color(iaiColor.r, iaiColor.g, iaiColor.b, 0.1f);
        aimTrajectoryLine.enabled = false;
    }

    void Update()
    {
        // 角色陣亡或正在執行居合位移時鎖定輸入
        if (player == null || player.IsDead || isExecuting)
        {
            if (isCharging) CancelCharge();
            return;
        }

        // 當小地圖開啟時，完全禁用右鍵蓄力與釋放，避免干擾地圖拖曳
        if (MapViewerController.Instance != null && MapViewerController.Instance.IsMapOpen)
        {
            if (isCharging) CancelCharge();
            return;
        }

        // 1. 按下滑鼠右鍵：開始居合蓄勢 + 進入子彈時間
        if (Input.GetKeyDown(triggerKey) && IsReady)
        {
            StartCharging();
        }

        // 2. 長按右鍵中：實時計算斜向瞄準射線並更新軌跡
        if (isCharging)
        {
            UpdateAimingTrajectory();

            chargeTimer += Time.unscaledDeltaTime;
            if (chargeTimer >= maxChargeTime)
            {
                ReleaseIai();
            }
        }

        // 3. 鬆開右鍵：解除時緩，發動居合穿刺
        if (Input.GetKeyUp(triggerKey) && isCharging)
        {
            ReleaseIai();
        }
    }

    void StartCharging()
    {
        isCharging = true;
        chargeTimer = 0f;
        Time.timeScale = slowMotionTimeScale;

        if (aimTrajectoryLine != null)
        {
            aimTrajectoryLine.enabled = true;
        }

        if (AudioManager.Instance != null && chargeSFX != null)
        {
            AudioManager.Instance.PlaySFX(chargeSFX, 0.9f);
        }
    }

    void UpdateAimingTrajectory()
    {
        // 取得當前 2D 切面的右向與上向軸基準
        Vector3 sliceRight = Vector3.right;
        Vector3 sliceUp = Vector3.up;

        if (TransformSliceManager.Instance != null && TransformSliceManager.Instance.mainOrthoCamera != null)
        {
            sliceRight = TransformSliceManager.Instance.mainOrthoCamera.transform.right;
            sliceUp = TransformSliceManager.Instance.mainOrthoCamera.transform.up;
        }

        Camera cam = (TransformSliceManager.Instance != null && TransformSliceManager.Instance.mainOrthoCamera != null)
                     ? TransformSliceManager.Instance.mainOrthoCamera 
                     : Camera.main;

        // 透過滑鼠相對角色位置計算斜向瞄準向量
        if (cam != null)
        {
            Vector3 playerScreenPos = cam.WorldToScreenPoint(player.transform.position);
            Vector3 mousePos = Input.mousePosition;
            Vector2 screenDir = (mousePos - playerScreenPos).normalized;

            aimDirection = (sliceRight * screenDir.x + sliceUp * screenDir.y).normalized;

            if (screenDir.magnitude < 0.1f)
            {
                aimDirection = sliceRight * player.FacingDirection;
            }
        }
        else
        {
            aimDirection = sliceRight * player.FacingDirection;
        }

        Vector3 startPos = player.transform.position;
        Vector3 endPos = startPos + aimDirection * dashDistance;

        // 射線偵測障礙物 (遇牆壁則截斷終點線)
        LayerMask mask = (obstacleLayer.value != 0) ? obstacleLayer : player.groundLayer;
        if (Physics.Raycast(startPos, aimDirection, out RaycastHit hit, dashDistance, mask))
        {
            endPos = hit.point - aimDirection * 0.3f;
        }

        if (aimTrajectoryLine != null)
        {
            aimTrajectoryLine.SetPosition(0, startPos);
            aimTrajectoryLine.SetPosition(1, endPos);
        }
    }

    void ReleaseIai()
    {
        isCharging = false;
        Time.timeScale = 1f;

        if (aimTrajectoryLine != null)
        {
            aimTrajectoryLine.enabled = false;
        }

        StartCoroutine(IaiExecutionRoutine());
    }

    void CancelCharge()
    {
        isCharging = false;
        Time.timeScale = 1f;
        if (aimTrajectoryLine != null)
        {
            aimTrajectoryLine.enabled = false;
        }
    }

    public override bool TryExecute()
    {
        if (!IsReady || isExecuting) return false;
        ReleaseIai();
        return true;
    }

    IEnumerator IaiExecutionRoutine()
    {
        isExecuting = true;
        lastUseTime = Time.time;

        Vector3 sliceRight = Vector3.right;
        Vector3 sliceNormal = Vector3.forward;
        if (TransformSliceManager.Instance != null && TransformSliceManager.Instance.mainOrthoCamera != null)
        {
            sliceRight = TransformSliceManager.Instance.mainOrthoCamera.transform.right;
            sliceNormal = TransformSliceManager.Instance.PlaneNormal;
        }

        Vector3 startPos = player.transform.position;
        Vector3 targetPos = startPos + aimDirection * dashDistance;

        LayerMask mask = (obstacleLayer.value != 0) ? obstacleLayer : player.groundLayer;
        if (Physics.Raycast(startPos, aimDirection, out RaycastHit hitWall, dashDistance, mask))
        {
            targetPos = hitWall.point - aimDirection * 0.3f;
        }

        // 1. 開啟玩家無敵狀態
        player.SetInvulnerable(true);

        // 2. 暫時關閉與範圍內敵人的實體碰撞 (杜絕穿透時觸發碰撞死亡)
        Collider playerCol = player.GetComponent<Collider>();
        Collider[] allEnemies = Physics.OverlapSphere(startPos, dashDistance + 3f, targetLayer);
        foreach (var ec in allEnemies)
        {
            if (ec != null && playerCol != null && ec != playerCol)
            {
                Physics.IgnoreCollision(playerCol, ec, true);
            }
        }

        if (AudioManager.Instance != null && dashSFX != null)
        {
            AudioManager.Instance.PlaySFX(dashSFX, 1f);
        }

        // 3. 高速斜向位移並記錄沿途碰觸的目標
        List<Collider> passedTargets = new List<Collider>();
        float elapsed = 0f;
        float expectedDuration = Vector3.Distance(startPos, targetPos) / dashSpeed;

        while (elapsed < expectedDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / expectedDuration);
            player.transform.position = Vector3.Lerp(startPos, targetPos, t);

            Collider[] hits = Physics.OverlapSphere(player.transform.position, 1.1f, targetLayer);
            foreach (var col in hits)
            {
                if (!passedTargets.Contains(col) && col.gameObject != player.gameObject)
                {
                    passedTargets.Add(col);
                }
            }
            yield return null;
        }

        player.transform.position = targetPos;

        // 4. 終點納刀頓幀 (Hit-Stop)
        Time.timeScale = 0.05f;
        yield return new WaitForSecondsRealtime(iaiHitStop);
        Time.timeScale = 1f;

        // 5. 播放納刀音效與沿線斬裂特效
        if (AudioManager.Instance != null && sheathSFX != null)
        {
            AudioManager.Instance.PlaySFX(sheathSFX, 1.1f);
        }

        IaiSlashVFX.PlayIaiCutEffect(startPos, targetPos, sliceRight, sliceNormal, iaiColor);

        // 6. 沿途所有目標同時結算傷害與定向擊退
        foreach (var col in passedTargets)
        {
            if (col == null) continue;

            Vector3 hitPoint = col.ClosestPoint(targetPos);

            if (col.TryGetComponent<BreakableCrate>(out var crate))
            {
                crate.TakeDamage(iaiDamage);
                HitEffectSpawner.CreateHitSpark(hitPoint, aimDirection, iaiColor);
            }

            if (col.TryGetComponent<SliceEnemy>(out var enemy))
            {
                enemy.TakeDamage(iaiDamage);
                enemy.ApplyKnockback(aimDirection, 7.5f, false);
                HitEffectSpawner.CreateHitSpark(hitPoint, aimDirection, iaiColor);
            }
        }

        // 7. 恢復與敵人的常規物理碰撞
        foreach (var ec in allEnemies)
        {
            if (ec != null && playerCol != null && ec != playerCol)
            {
                Physics.IgnoreCollision(playerCol, ec, false);
            }
        }

        player.SetInvulnerable(false);
        isExecuting = false;
    }
}