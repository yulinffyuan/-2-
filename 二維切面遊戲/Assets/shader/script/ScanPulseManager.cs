using System.Collections;
using UnityEngine;

public class ScanPulseManager : MonoBehaviour
{
    public static ScanPulseManager Instance;

    [Header("波形核心設定")]
    [Range(1, 8)]
    public int pulseCount = 3;             // 波的次數（段數）
    public float maxRadius = 40f;          // 波的範圍（最大半徑）
    
    [Range(0.1f, 5.0f)]
    public float waveWidth = 1.2f;         // 波的大小（光環厚度/光帶寬度）

    [Header("動態節奏設定")]
    public float singleWaveDuration = 2.4f;// 單道波擴散持續時間（控制速度）
    public float waveInterval = 0.4f;      // 每道波的釋放間隔 (秒)
    public float cooldown = 2.0f;          // 全部發射完後的冷卻時間

    [Header("控制與音效")]
    public KeyCode scanKey = KeyCode.F;    // 觸發鍵
    public AudioClip scanSFX;              // 聲納發射音效

    private bool isScanning = false;
    private bool canScan = true;

    private const int MAX_WAVES = 8;
    private Vector4[] waveDataArray = new Vector4[MAX_WAVES];

    void Awake()
    {
        Instance = this;
    }

    void Start()
    {
        ResetShaderScan();
    }

    void Update()
    {
        // 隨時更新波的大小到 Shader（即使沒觸發掃描也保持參數同步）
        Shader.SetGlobalFloat("_ScanWaveWidth", waveWidth);

        // 任何時候（包含 Tab 地圖模式）均可按下 F 發動掃描
        if (Input.GetKeyDown(scanKey) && canScan)
        {
            StartCoroutine(TriggerScanRoutine());
        }
    }

    IEnumerator TriggerScanRoutine()
    {
        canScan = false;
        isScanning = true;

        // 1. 設定掃描波源中心（主角當前世界位置）
        Vector3 origin = transform.position;
        if (TransformSliceManager.Instance != null && TransformSliceManager.Instance.player != null)
        {
            origin = TransformSliceManager.Instance.player.position;
        }

        Shader.SetGlobalVector("_ScanOrigin", origin);
        Shader.SetGlobalInt("_ScanActiveCount", pulseCount);
        Shader.SetGlobalFloat("_ScanWaveWidth", waveWidth);

        // 2. 初始化各波計時器（依 waveInterval 遞進推遲）
        float[] waveTimers = new float[pulseCount];
        for (int i = 0; i < pulseCount; i++)
        {
            waveTimers[i] = -i * waveInterval;
        }

        // 3. 播放發射音效
        PlayPulseAudio();

        bool anyWaveAlive = true;

        while (anyWaveAlive)
        {
            anyWaveAlive = false;

            // 每幀持續同步 Inspector 可能被微調的波寬
            Shader.SetGlobalFloat("_ScanWaveWidth", waveWidth);

            // 清空當前幀數據
            for (int k = 0; k < MAX_WAVES; k++)
            {
                waveDataArray[k] = Vector4.zero;
            }

            for (int i = 0; i < pulseCount; i++)
            {
                waveTimers[i] += Time.deltaTime;

                if (waveTimers[i] >= 0f && waveTimers[i] <= singleWaveDuration)
                {
                    anyWaveAlive = true;
                    float progress = waveTimers[i] / singleWaveDuration;

                    // 平滑擴散曲線 (Sine Ease-Out)
                    float easeT = Mathf.Sin(progress * Mathf.PI * 0.5f);
                    float currentRadius = Mathf.Lerp(0f, maxRadius, easeT);

                    // 前半段維持強度，後半段自然淡出
                    float intensity = progress < 0.45f ? 1f : Mathf.Lerp(1f, 0f, (progress - 0.45f) / 0.55f);

                    waveDataArray[i] = new Vector4(currentRadius, intensity, 0f, 0f);
                }
                else if (waveTimers[i] < 0f)
                {
                    anyWaveAlive = true; // 仍在排隊等待釋放
                }
            }

            // 同步波數據到 Shader
            Shader.SetGlobalVectorArray("_ScanWaveData", waveDataArray);

            yield return null;
        }

        ResetShaderScan();
        isScanning = false;

        // 冷卻時間
        yield return new WaitForSeconds(cooldown);
        canScan = true;
    }

    void PlayPulseAudio()
    {
        if (AudioManager.Instance != null && scanSFX != null)
        {
            AudioManager.Instance.PlaySFX(scanSFX, 0.85f, false);
        }
    }

    void ResetShaderScan()
    {
        for (int k = 0; k < MAX_WAVES; k++)
        {
            waveDataArray[k] = Vector4.zero;
        }
        Shader.SetGlobalInt("_ScanActiveCount", 0);
        Shader.SetGlobalVectorArray("_ScanWaveData", waveDataArray);
    }
}