using UnityEngine;

public class TransformSliceManager : MonoBehaviour
{
    public static TransformSliceManager Instance;

    [Header("目標與相機")]
    public Transform player;
    public Camera mainOrthoCamera;

    [Header("切面屬性")]
    public float thickness = 2.5f;

    [Header("旋轉控制設定")]
    public float snapTurnSpeed = 8f;        // Q/E 90度平滑過渡速度
    public float scrollSensitivity = 15f;    // 滾輪微調靈敏度

    // 切面狀態
    public Vector3 PlaneNormal { get; private set; } = Vector3.forward;
    public Vector3 PlaneOrigin => player != null ? player.position : Vector3.zero;

    private float currentYAngle = 0f;
    private float targetYAngle = 0f;

    void Awake()
    {
        Instance = this;
    }

    void Start()
    {
        targetYAngle = 0f;
        currentYAngle = 0f;
        UpdateSlicePlane(currentYAngle);
    }

    void Update()
    {
        // 若 Tab 地圖開啟中，暫停切面操作
        if (MapViewerController.Instance != null && MapViewerController.Instance.IsMapOpen)
            return;

        // 1. Q / E 鍵：90 度預設切換
        if (Input.GetKeyDown(KeyCode.Q))
        {
            targetYAngle -= 90f;
        }
        if (Input.GetKeyDown(KeyCode.E))
        {
            targetYAngle += 90f;
        }

        // 2. 滑鼠滾輪：連續角度微調
        float scroll = Input.GetAxis("Mouse ScrollWheel");
        if (Mathf.Abs(scroll) > 0.01f)
        {
            targetYAngle += scroll * scrollSensitivity * 10f;
        }

        // 3. 平滑插值至目標角度
        currentYAngle = Mathf.Lerp(currentYAngle, targetYAngle, Time.deltaTime * snapTurnSpeed);

        // 4. 更新切面與相機
        UpdateSlicePlane(currentYAngle);
    }

    void UpdateSlicePlane(float angleY)
    {
        // 繞 Y 軸旋轉求出新的法向量
        Quaternion rot = Quaternion.Euler(0f, angleY, 0f);
        PlaneNormal = rot * Vector3.forward;

        // 相機環繞跟隨主角正交投影
        if (mainOrthoCamera != null && player != null)
        {
            mainOrthoCamera.transform.position = player.position - (PlaneNormal * 20f);
            mainOrthoCamera.transform.rotation = Quaternion.LookRotation(PlaneNormal, Vector3.up);
        }

        // 同步 Shader 全域參數
        Shader.SetGlobalVector("_SlicePlaneOrigin", PlaneOrigin);
        Shader.SetGlobalVector("_SlicePlaneNormal", PlaneNormal);
        Shader.SetGlobalFloat("_SliceThickness", thickness);
    }
}