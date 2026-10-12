using UnityEngine;

public class BlinkItem : UsableItem
{
    [Header("閃現參數")]
    public float maxBlinkDistance = 7f;       // 最大瞬移距離
    public float slowMotionScale = 0.2f;      // 瞄準時的時緩倍率
    public LayerMask obstacleLayer;           // 障礙物與地表 (防穿牆)
    public Color vfxColor = new Color(0.85f, 0.88f, 0.92f, 0.9f); // 冷灰銀色粒子

    [Header("音效")]
    public AudioClip aimSFX;
    public AudioClip blinkSFX;

    private Vector3 targetPos;
    private GameObject aimIndicator;
    private LineRenderer aimRing;

    void Awake()
    {
        itemName = "相位閃現器 (Phase Blink)";
        cooldown = 2.0f;
        CreateAimIndicator();
    }

    void CreateAimIndicator()
    {
        aimIndicator = new GameObject("BlinkAimIndicator");
        aimIndicator.transform.SetParent(transform);

        aimRing = aimIndicator.AddComponent<LineRenderer>();
        aimRing.useWorldSpace = true;
        aimRing.loop = true;
        aimRing.positionCount = 24;
        aimRing.startWidth = 0.05f;
        aimRing.endWidth = 0.05f;
        
        Material mat = new Material(Shader.Find("Sprites/Default"));
        mat.color = vfxColor;
        aimRing.material = mat;

        // 預設關閉
        aimIndicator.SetActive(false);
    }

    public override void OnStartAim()
    {
        Time.timeScale = slowMotionScale;
        if (aimIndicator != null) aimIndicator.SetActive(true);

        if (AudioManager.Instance != null && aimSFX != null)
        {
            AudioManager.Instance.PlaySFX(aimSFX, 0.8f);
        }
    }

    public override void OnUpdateAim()
    {
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

        Vector3 startPos = player.transform.position;
        Vector3 aimDir = sliceRight * player.FacingDirection;
        float distance = maxBlinkDistance;

        if (cam != null)
        {
            Vector3 playerScreenPos = cam.WorldToScreenPoint(startPos);
            Vector3 mousePos = Input.mousePosition;
            Vector2 screenDir = (mousePos - playerScreenPos);

            // 根據滑鼠距離動態決定傳送距離（但不超過 maxBlinkDistance）
            float screenDistRatio = Mathf.Clamp01(screenDir.magnitude / (Screen.height * 0.35f));
            distance = maxBlinkDistance * Mathf.Max(0.2f, screenDistRatio);

            if (screenDir.magnitude > 0.05f)
            {
                screenDir.Normalize();
                aimDir = (sliceRight * screenDir.x + sliceUp * screenDir.y).normalized;
            }
        }

        targetPos = startPos + aimDir * distance;

        // 防穿牆與障礙物檢測
        LayerMask mask = (obstacleLayer.value != 0) ? obstacleLayer : player.groundLayer;
        if (Physics.Raycast(startPos, aimDir, out RaycastHit hit, distance, mask))
        {
            targetPos = hit.point - aimDir * 0.4f; // 撞牆時在牆面前停下
        }

        // 更新瞄準落地圓環位置
        UpdateIndicatorRing(targetPos, sliceRight, sliceUp, 0.6f);
    }

    void UpdateIndicatorRing(Vector3 center, Vector3 right, Vector3 up, float radius)
    {
        if (aimRing == null) return;

        for (int i = 0; i < 24; i++)
        {
            float angle = (i / 24f) * Mathf.PI * 2f;
            Vector3 p = center + (right * Mathf.Cos(angle) + up * Mathf.Sin(angle)) * radius;
            aimRing.SetPosition(i, p);
        }
    }

    public override void OnReleaseUse()
    {
        Time.timeScale = 1f;
        if (aimIndicator != null) aimIndicator.SetActive(false);

        lastUseTime = Time.time;

        Vector3 originPos = player.transform.position;

        // 1. 起點消散幾何粒子 (無連貫軌跡，純粹點對點)
        SpawnPhaseCollapseVFX(originPos);

        // 2. 瞬間重置物理並修改座標
        if (player.TryGetComponent<Rigidbody>(out var rb))
        {
            #if UNITY_6000_0_OR_NEWER
            rb.linearVelocity = Vector3.zero;
            #else
            rb.velocity = Vector3.zero;
            #endif
        }

        player.transform.position = targetPos;

        // 3. 終點重構幾何粒子
        SpawnPhaseCollapseVFX(targetPos);

        // 4. 音效
        if (AudioManager.Instance != null && blinkSFX != null)
        {
            AudioManager.Instance.PlaySFX(blinkSFX, 1f);
        }
    }

    public override void OnCancel()
    {
        Time.timeScale = 1f;
        if (aimIndicator != null) aimIndicator.SetActive(false);
    }

    void SpawnPhaseCollapseVFX(Vector3 pos)
    {
        int particleCount = 6;
        for (int i = 0; i < particleCount; i++)
        {
            GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = "BlinkPhaseCube";
            cube.transform.position = pos + Random.insideUnitSphere * 0.35f;
            cube.transform.localScale = Vector3.one * Random.Range(0.08f, 0.18f);

            var rend = cube.GetComponent<Renderer>();
            Material mat = new Material(Shader.Find("Sprites/Default"));
            mat.color = vfxColor;
            rend.material = mat;

            Collider c = cube.GetComponent<Collider>();
            if (c != null) c.enabled = false;

            Rigidbody crb = cube.AddComponent<Rigidbody>();
            crb.AddForce(Random.insideUnitSphere * 4f, ForceMode.Impulse);
            crb.AddTorque(Random.insideUnitSphere * 20f, ForceMode.Impulse);

            cube.AddComponent<DebrisFadeDestroy>();
        }
    }
}