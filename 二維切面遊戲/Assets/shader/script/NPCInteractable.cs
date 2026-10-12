using UnityEngine;
using TMPro;

[RequireComponent(typeof(Collider))]
public class NPCInteractable : MonoBehaviour
{
    [Header("NPC 設定與外觀")]
    public string npcName = "流浪武士";
    public SpriteRenderer spriteRenderer;     // NPC 的 2D 貼圖組件
    public bool facePlayer = true;            // 是否隨玩家位置動態左右翻轉

    [Header("對話劇本模式")]
    [Tooltip("勾選後將從 randomSequences 池中隨機抽一段播放；未勾選則固定播放 defaultSequence")]
    public bool useRandomDialogue = false;

    [Tooltip("預設/單一對話劇本 (當未開啟隨機時使用)")]
    public DialogueSequence defaultSequence;

    [Tooltip("隨機劇本池 (開啟隨機模式時抽樣)")]
    public DialogueSequence[] randomSequences;

    [Tooltip("避免連續兩次觸發相同的隨機劇本")]
    public bool avoidRepeatingPrevious = true;

    [Header("互動範圍與按鍵")]
    public float interactDistance = 2.2f;     // 玩家靠近多少距離內顯示提示
    public KeyCode interactKey = KeyCode.T;   // 互動按鍵改為 T 鍵

    [Header("頭頂互動 UI 提示 (World Space Canvas)")]
    public GameObject interactPromptUI;       // 浮動提示容器 (例如包含 [T] 對話)
    public TMP_Text promptText;               // 提示文字 (選填)
    public Vector3 promptOffset = new Vector3(0f, 1.8f, 0f); // 浮標相對於 NPC 的高度

    [Header("音效 (選填)")]
    public AudioClip interactSFX;

    private Transform playerTransform;
    private bool isPlayerInRange = false;
    private int lastPlayedIndex = -1;         // 記錄上次播放的隨機索引

    void Start()
    {
        // 確保 Collider 為 Trigger 供重疊檢測
        Collider col = GetComponent<Collider>();
        if (col != null)
        {
            col.isTrigger = true;
        }

        if (spriteRenderer == null)
        {
            spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        }

        // 預設隱藏提示框
        if (interactPromptUI != null)
        {
            interactPromptUI.SetActive(false);
        }

        // 預設提示文字改為 [T]
        if (promptText != null)
        {
            promptText.text = "[T] 對話";
        }
    }

    void Update()
    {
        // 若當前正處於對話中，隱藏提示並禁止重複觸發
        if (DialogueManager.Instance != null && DialogueManager.Instance.IsDialogueActive)
        {
            if (interactPromptUI != null && interactPromptUI.activeSelf)
            {
                interactPromptUI.SetActive(false);
            }
            return;
        }

        // 快取尋找玩家 Transform
        if (playerTransform == null)
        {
            var player = FindFirstObjectByType<RotatableSlicePlayer>();
            if (player != null) playerTransform = player.transform;
            return;
        }

        // 計算與玩家之間的距離
        float dist = Vector3.Distance(transform.position, playerTransform.position);
        bool inRange = dist <= interactDistance;

        if (inRange != isPlayerInRange)
        {
            isPlayerInRange = inRange;
            if (interactPromptUI != null)
            {
                interactPromptUI.SetActive(isPlayerInRange);
            }
        }

        if (isPlayerInRange)
        {
            // 更新提示 UI 位置 (維持在 NPC 頭頂)
            if (interactPromptUI != null)
            {
                interactPromptUI.transform.position = transform.position + promptOffset;
                AlignPromptToCamera();
            }

            // 依據玩家切面方位動態翻轉貼圖朝向
            if (facePlayer && spriteRenderer != null)
            {
                UpdateFacingDirection();
            }

            // 按下 T 鍵觸發對話
            if (Input.GetKeyDown(interactKey))
            {
                TriggerInteraction();
            }
        }
    }

    void UpdateFacingDirection()
    {
        Vector3 sliceRight = Vector3.right;
        if (TransformSliceManager.Instance != null && TransformSliceManager.Instance.mainOrthoCamera != null)
        {
            sliceRight = TransformSliceManager.Instance.mainOrthoCamera.transform.right;
        }

        Vector3 toPlayer = playerTransform.position - transform.position;
        float dot = Vector3.Dot(toPlayer, sliceRight);

        if (Mathf.Abs(dot) > 0.1f)
        {
            spriteRenderer.flipX = dot < 0f;
        }
    }

    void AlignPromptToCamera()
    {
        Camera targetCam = (TransformSliceManager.Instance != null && TransformSliceManager.Instance.mainOrthoCamera != null)
            ? TransformSliceManager.Instance.mainOrthoCamera
            : Camera.main;

        if (targetCam != null && interactPromptUI != null)
        {
            interactPromptUI.transform.rotation = targetCam.transform.rotation;
        }
    }

    /// <summary>
    /// 依據模式挑選要播放的劇本 (支援單一或不重複隨機)
    /// </summary>
    DialogueSequence GetTargetDialogueSequence()
    {
        if (!useRandomDialogue)
        {
            return defaultSequence;
        }

        if (randomSequences == null || randomSequences.Length == 0)
        {
            return defaultSequence;
        }

        if (randomSequences.Length == 1)
        {
            return randomSequences[0];
        }

        // 抽取隨機索引 (包含防連續重複邏輯)
        int selectedIndex;
        if (avoidRepeatingPrevious)
        {
            do
            {
                selectedIndex = Random.Range(0, randomSequences.Length);
            } while (selectedIndex == lastPlayedIndex);
        }
        else
        {
            selectedIndex = Random.Range(0, randomSequences.Length);
        }

        lastPlayedIndex = selectedIndex;
        return randomSequences[selectedIndex];
    }

    void TriggerInteraction()
    {
        DialogueSequence sequenceToPlay = GetTargetDialogueSequence();

        if (sequenceToPlay == null)
        {
            Debug.LogWarning($"[NPC] {npcName} 未配置有效的 DialogueSequence 劇本！");
            return;
        }

        if (AudioManager.Instance != null && interactSFX != null)
        {
            AudioManager.Instance.PlaySFX(interactSFX, 0.8f);
        }

        // 進入對話時先收起頭頂提示
        if (interactPromptUI != null)
        {
            interactPromptUI.SetActive(false);
        }

        // 啟動對話劇本
        if (DialogueManager.Instance != null)
        {
            DialogueManager.Instance.StartDialogue(sequenceToPlay);
        }
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.35f);
        Gizmos.DrawWireSphere(transform.position, interactDistance);
    }
}