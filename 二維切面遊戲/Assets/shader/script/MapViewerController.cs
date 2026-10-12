using UnityEngine;
using UnityEngine.UI;

public class MapViewerController : MonoBehaviour
{
    public static MapViewerController Instance { get; private set; }

    [Header("3D 地圖攝影機與目標")]
    public Camera mapCamera;              
    public Transform targetPlayer;        
    [Tooltip("玩家軀幹中心補償 (避免相機只看腳底造成視角偏下)")]
    public Vector3 playerCenterOffset = new Vector3(0f, 0.9f, 0f);

    [Header("UI 容器")]
    public RectTransform mapDisplayUI;    // 顯示小地圖的 UI 容器
    public RawImage mapRawImage;          // 承載 RenderTexture 的 RawImage 組件
    public KeyCode toggleKey = KeyCode.Tab;

    [Header("UI 尺寸設定")]
    public Vector2 miniMapSize = new Vector2(260f, 260f);   
    public Vector2 fullMapSize = new Vector2(960f, 640f);   

    [Header("解析度與畫質優化")]
    public float resolutionScale = 1.5f;  
    public bool enableAntiAliasing = true;

    [Header("3D 等角預設角度 (Perspective)")]
    public float defaultPitch = 55f;      // 高俯視角度
    public float defaultYaw = 45f;        // 環視等角
    public float miniMapDistance = 32f;   // 小地圖高空視野距離
    public float fullMapDistance = 55f;   // 大地圖高空視野距離

    [Header("WASD 視角移動設定")]
    public float keyboardPanSpeed = 24f;  // WASD 移動速度 (單位/秒)
    public float keyboardFastMultiplier = 1.8f; // 按住 LeftShift 時的加速倍率

    [Header("手動控制靈敏度")]
    public float rotateSpeed = 3.5f;      // 滑鼠右鍵環視旋轉
    public float zoomSpeed = 8f;          // 滾輪縮放
    public float minDistance = 8f;        
    public float maxDistance = 90f;       
    public float idleReturnDelay = 2.0f;  // 停止輸入多久自動飄回主角 (設為 <= 0 則不自動回正)

    [Header("跟隨阻尼")]
    public float followSmoothTime = 0.12f;

    public bool IsMapOpen { get; private set; } = false;

    // 核心注視中心點 (Pivot)
    private Vector3 currentPivot;         
    private Vector3 pivotVelocity;
    private float currentPitch;
    private float currentYaw;
    private float currentDistance;

    private bool isKeyboardMoving = false;
    private bool isOrbiting = false;
    private float lastManualTime = -999f;
    private Vector3 lastMousePos;

    private RenderTexture dynamicRT;

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    void Start()
    {
        if (mapCamera == null)
        {
            mapCamera = GetComponent<Camera>();
        }

        if (targetPlayer == null)
        {
            var player = FindFirstObjectByType<RotatableSlicePlayer>();
            if (player != null) targetPlayer = player.transform;
        }

        if (mapRawImage == null && mapDisplayUI != null)
        {
            mapRawImage = mapDisplayUI.GetComponent<RawImage>();
        }

        if (mapCamera != null)
        {
            mapCamera.orthographic = false;
            mapCamera.fieldOfView = 50f;
        }

        currentPitch = defaultPitch;
        currentYaw = defaultYaw;
        currentDistance = miniMapDistance;

        if (targetPlayer != null)
        {
            currentPivot = GetPlayerTargetCenter();
        }

        ApplyMapMode(false, true);
    }

    Vector3 GetPlayerTargetCenter()
    {
        if (targetPlayer == null) return Vector3.zero;
        return targetPlayer.position + playerCenterOffset;
    }

    void Update()
    {
        // 1. Tab 切換大地圖/小地圖
        if (Input.GetKeyDown(toggleKey))
        {
            ToggleMapMode();
        }

        // 2. 僅在大地圖展開時監聽 WASD 與滾輪縮放
        if (IsMapOpen)
        {
            HandleKeyboardPan();
            HandleZoom();
        }
    }

    void LateUpdate()
    {
        if (targetPlayer == null || mapCamera == null) return;

        if (IsMapOpen)
        {
            HandleMouseOrbit();
        }

        bool isManualOperating = isKeyboardMoving || isOrbiting;

        // 非手動操作時，平滑跟隨主角
        if (!isManualOperating)
        {
            bool shouldFollow = true;
            if (IsMapOpen && idleReturnDelay > 0f)
            {
                shouldFollow = (Time.unscaledTime - lastManualTime >= idleReturnDelay);
            }

            if (shouldFollow)
            {
                Vector3 targetCenter = GetPlayerTargetCenter();

                currentPivot = Vector3.SmoothDamp(
                    currentPivot,
                    targetCenter,
                    ref pivotVelocity,
                    followSmoothTime,
                    Mathf.Infinity,
                    Time.unscaledDeltaTime
                );

                // 閒置時旋轉角度平滑復原至預設等角
                if (IsMapOpen && idleReturnDelay > 0f)
                {
                    currentPitch = Mathf.Lerp(currentPitch, defaultPitch, Time.unscaledDeltaTime * 3f);
                    currentYaw = Mathf.Lerp(currentYaw, defaultYaw, Time.unscaledDeltaTime * 3f);
                }
            }
        }

        // 按空白鍵隨時強制回正至主角
        if (IsMapOpen && Input.GetKeyDown(KeyCode.Space))
        {
            RecenterToPlayer();
        }

        Update3DCameraTransform();
    }

    /// <summary>
    /// WASD 畫面相對空間平移 (Screen-Relative Navigation)
    /// </summary>
    void HandleKeyboardPan()
    {
        float h = 0f;
        float v = 0f;

        if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow)) h -= 1f;
        if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow)) h += 1f;
        if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow)) v += 1f;
        if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow)) v -= 1f;

        if (h != 0f || v != 0f)
        {
            isKeyboardMoving = true;
            lastManualTime = Time.unscaledTime;

            float speed = keyboardPanSpeed;
            if (Input.GetKey(KeyCode.LeftShift))
            {
                speed *= keyboardFastMultiplier;
            }

            // 取得相機畫面的右向與上向向量，並消除相機仰角對水平移動的歪斜影響
            Vector3 camRight = mapCamera.transform.right;
            Vector3 camUp = mapCamera.transform.up;

            // 將移動向量合成在當前視野平面
            Vector3 moveDir = (camRight * h + camUp * v).normalized;

            // 根據縮放距離適度縮放移動速度 (拉遠時移得更快)
            float zoomRatio = currentDistance / miniMapDistance;
            currentPivot += moveDir * (speed * zoomRatio * Time.unscaledDeltaTime);
        }
        else
        {
            isKeyboardMoving = false;
        }
    }

    /// <summary>
    /// 滑鼠右鍵 3D 環視軌道旋轉
    /// </summary>
    void HandleMouseOrbit()
    {
        if (Input.GetMouseButtonDown(1))
        {
            isOrbiting = true;
            lastMousePos = Input.mousePosition;
            lastManualTime = Time.unscaledTime;
        }

        if (isOrbiting)
        {
            if (Input.GetMouseButton(1))
            {
                Vector3 delta = Input.mousePosition - lastMousePos;
                currentYaw += delta.x * rotateSpeed * 0.1f;
                currentPitch -= delta.y * rotateSpeed * 0.1f;
                currentPitch = Mathf.Clamp(currentPitch, 10f, 85f);

                lastMousePos = Input.mousePosition;
                lastManualTime = Time.unscaledTime;
            }

            if (Input.GetMouseButtonUp(1))
            {
                isOrbiting = false;
                lastManualTime = Time.unscaledTime;
            }
        }
    }

    void HandleZoom()
    {
        float scroll = Input.GetAxis("Mouse ScrollWheel");
        if (Mathf.Abs(scroll) > 0.01f)
        {
            currentDistance = Mathf.Clamp(currentDistance - scroll * zoomSpeed, minDistance, maxDistance);
            lastManualTime = Time.unscaledTime;
        }
    }

    void Update3DCameraTransform()
    {
        Quaternion rotation = Quaternion.Euler(currentPitch, currentYaw, 0f);
        Vector3 offset = rotation * new Vector3(0f, 0f, -currentDistance);

        mapCamera.transform.position = currentPivot + offset;
        mapCamera.transform.LookAt(currentPivot);
    }

    public void ToggleMapMode()
    {
        ApplyMapMode(!IsMapOpen, false);
    }

    void ApplyMapMode(bool openFull, bool immediate)
    {
        IsMapOpen = openFull;

        Vector2 targetSize = IsMapOpen ? fullMapSize : miniMapSize;

        if (mapDisplayUI != null)
        {
            mapDisplayUI.sizeDelta = targetSize;
        }

        currentDistance = IsMapOpen ? fullMapDistance : miniMapDistance;

        UpdateRenderTextureResolution(targetSize);

        if (immediate && targetPlayer != null)
        {
            currentPivot = GetPlayerTargetCenter();
            currentPitch = defaultPitch;
            currentYaw = defaultYaw;
            Update3DCameraTransform();
        }
        else
        {
            RecenterToPlayer();
        }
    }

    void UpdateRenderTextureResolution(Vector2 uiSize)
    {
        if (mapCamera == null || mapRawImage == null) return;

        int rtWidth = Mathf.RoundToInt(uiSize.x * resolutionScale);
        int rtHeight = Mathf.RoundToInt(uiSize.y * resolutionScale);

        rtWidth = Mathf.Max(64, rtWidth);
        rtHeight = Mathf.Max(64, rtHeight);

        if (dynamicRT != null && dynamicRT.width == rtWidth && dynamicRT.height == rtHeight)
        {
            return;
        }

        if (dynamicRT != null)
        {
            mapCamera.targetTexture = null;
            dynamicRT.Release();
            Destroy(dynamicRT);
        }

        dynamicRT = new RenderTexture(rtWidth, rtHeight, 24, RenderTextureFormat.ARGB32)
        {
            antiAliasing = enableAntiAliasing ? 4 : 1,
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
            useMipMap = false
        };
        dynamicRT.Create();

        mapCamera.targetTexture = dynamicRT;
        mapRawImage.texture = dynamicRT;
    }

    public void RecenterToPlayer()
    {
        if (targetPlayer != null)
        {
            currentPivot = GetPlayerTargetCenter();
        }
        currentPitch = defaultPitch;
        currentYaw = defaultYaw;
        currentDistance = IsMapOpen ? fullMapDistance : miniMapDistance;
        lastManualTime = -999f;
        isKeyboardMoving = false;
        isOrbiting = false;
    }

    void OnDestroy()
    {
        if (dynamicRT != null)
        {
            if (mapCamera != null) mapCamera.targetTexture = null;
            dynamicRT.Release();
            Destroy(dynamicRT);
        }
    }
}