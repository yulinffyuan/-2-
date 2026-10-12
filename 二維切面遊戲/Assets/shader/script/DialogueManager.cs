using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro; // 引入 TextMeshPro

public class DialogueManager : MonoBehaviour
{
    public static DialogueManager Instance { get; private set; }

    // 靜態標記跨關卡重載保留，保證死亡重置時不重複播放
    private static bool hasPlayedOpeningDialogue = false;

    [Header("UI 面板組件 (位於畫面下半部)")]
    public GameObject dialoguePanel;          // 對話框母容器
    public TMP_Text contentText;              // 對話內文 (TextMeshPro)
    public TMP_Text speakerNameText;          // 講者名稱 (TextMeshPro)

    [Header("跳過按鈕設定 (Button + TMP)")]
    public Button skipButton;                 // Button 組件 (支援 Button - TextMeshPro)
    public TMP_Text skipButtonText;           // 按鈕內的 TextMeshPro 文字 (選填，若未拖入會自動抓取)
    public string defaultSkipText = "SKIP ▶"; // 預設顯示文字

    [Header("左側 NPC 頭像設定")]
    public Image leftAvatarImage;
    public CanvasGroup leftAvatarGroup;

    [Header("右側玩家頭像設定")]
    public Image rightAvatarImage;
    public CanvasGroup rightAvatarGroup;

    [Header("推進與操作設定")]
    public GameObject nextIndicator;          // 提示點擊的小圖示
    public KeyCode skipKey = KeyCode.Escape;  // 按 Escape 鍵直接跳過整段對話
    public float typeSpeed = 0.035f;
    public AudioClip typingSFX;

    [Header("開局對話設定")]
    public DialogueSequence openingDialogue;
    [Tooltip("勾選後只會在遊戲首次進入關卡時播放，死掉重來不播放")]
    public bool playOnlyOnceOnFirstEntry = true;

    private Queue<DialogueLine> lineQueue = new Queue<DialogueLine>();
    private bool isTyping = false;
    private string currentFullText = "";
    private Coroutine typingCoroutine;

    public bool IsDialogueActive { get; private set; } = false;

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);

        EnsureCanvasGroup(ref leftAvatarGroup, leftAvatarImage);
        EnsureCanvasGroup(ref rightAvatarGroup, rightAvatarImage);
        SetupSkipButtonTMP();
    }

    void SetupSkipButtonTMP()
    {
        if (skipButton != null)
        {
            // 若 Inspector 沒手動拖入 TMP 文字，自動從子物件尋找
            if (skipButtonText == null)
            {
                skipButtonText = skipButton.GetComponentInChildren<TMP_Text>();
            }

            if (skipButtonText != null && !string.IsNullOrEmpty(defaultSkipText))
            {
                skipButtonText.text = defaultSkipText;
            }

            skipButton.onClick.RemoveAllListeners();
            skipButton.onClick.AddListener(SkipAllDialogue);
        }
    }

    void EnsureCanvasGroup(ref CanvasGroup group, Image avatarImg)
    {
        if (group == null && avatarImg != null)
        {
            group = avatarImg.GetComponent<CanvasGroup>();
            if (group == null && avatarImg.transform.parent != null)
            {
                group = avatarImg.transform.parent.GetComponent<CanvasGroup>();
            }
            if (group == null)
            {
                group = avatarImg.gameObject.AddComponent<CanvasGroup>();
            }
        }
    }

    void Start()
    {
        if (dialoguePanel != null)
        {
            dialoguePanel.SetActive(false);
        }

        if (nextIndicator != null)
        {
            nextIndicator.SetActive(false);
        }

        // 判斷是否播放開局對話
        if (openingDialogue != null && openingDialogue.lines != null && openingDialogue.lines.Length > 0)
        {
            if (playOnlyOnceOnFirstEntry)
            {
                if (!hasPlayedOpeningDialogue)
                {
                    hasPlayedOpeningDialogue = true;
                    StartDialogue(openingDialogue);
                }
            }
            else
            {
                StartDialogue(openingDialogue);
            }
        }
    }

    void Update()
    {
        if (!IsDialogueActive) return;

        // 1. 按下 Escape 鍵跳過整段對話
        if (Input.GetKeyDown(skipKey))
        {
            SkipAllDialogue();
            return;
        }

        // 2. 空白鍵、Enter 或滑鼠左鍵推進對話
        if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return) || Input.GetMouseButtonDown(0))
        {
            if (isTyping)
            {
                CompleteCurrentSentence();
            }
            else
            {
                DisplayNextLine();
            }
        }
    }

    public void StartDialogue(DialogueSequence sequence)
    {
        if (sequence == null || sequence.lines == null || sequence.lines.Length == 0) return;

        IsDialogueActive = true;
        lineQueue.Clear();

        foreach (var line in sequence.lines)
        {
            lineQueue.Enqueue(line);
        }

        if (dialoguePanel != null)
        {
            dialoguePanel.SetActive(true);
        }

        DisplayNextLine();
    }

    void DisplayNextLine()
    {
        if (lineQueue.Count == 0)
        {
            EndDialogue();
            return;
        }

        DialogueLine line = lineQueue.Dequeue();

        if (nextIndicator != null)
        {
            nextIndicator.SetActive(false);
        }

        if (speakerNameText != null)
        {
            speakerNameText.text = line.speakerName;
            speakerNameText.alignment = (line.speakerSide == SpeakerSide.NPC_Left) 
                ? TextAlignmentOptions.Left 
                : TextAlignmentOptions.Right;
        }

        UpdateSpeakerVisuals(line);

        currentFullText = line.content;
        if (typingCoroutine != null) StopCoroutine(typingCoroutine);
        typingCoroutine = StartCoroutine(TypeTextRoutine(currentFullText));
    }

    void UpdateSpeakerVisuals(DialogueLine line)
    {
        if (line.speakerSide == SpeakerSide.NPC_Left)
        {
            if (leftAvatarImage != null && line.avatarSprite != null)
            {
                leftAvatarImage.sprite = line.avatarSprite;
            }
            SetAvatarState(leftAvatarGroup, true);
            SetAvatarState(rightAvatarGroup, false);
        }
        else
        {
            if (rightAvatarImage != null && line.avatarSprite != null)
            {
                rightAvatarImage.sprite = line.avatarSprite;
            }
            SetAvatarState(leftAvatarGroup, false);
            SetAvatarState(rightAvatarGroup, true);
        }
    }

    void SetAvatarState(CanvasGroup group, bool isSpeaking)
    {
        if (group == null) return;
        group.alpha = isSpeaking ? 1.0f : 0.38f;
        group.transform.localScale = isSpeaking ? Vector3.one * 1.04f : Vector3.one * 0.96f;
    }

    IEnumerator TypeTextRoutine(string sentence)
    {
        isTyping = true;
        if (contentText != null) contentText.text = "";

        foreach (char letter in sentence.ToCharArray())
        {
            if (contentText != null) contentText.text += letter;

            if (AudioManager.Instance != null && typingSFX != null && !char.IsWhiteSpace(letter))
            {
                AudioManager.Instance.PlaySFX(typingSFX, 0.35f);
            }

            yield return new WaitForSecondsRealtime(typeSpeed);
        }

        isTyping = false;

        if (nextIndicator != null)
        {
            nextIndicator.SetActive(true);
        }
    }

    void CompleteCurrentSentence()
    {
        if (typingCoroutine != null) StopCoroutine(typingCoroutine);
        if (contentText != null) contentText.text = currentFullText;
        isTyping = false;

        if (nextIndicator != null)
        {
            nextIndicator.SetActive(true);
        }
    }

    public void SkipAllDialogue()
    {
        if (!IsDialogueActive) return;

        if (typingCoroutine != null)
        {
            StopCoroutine(typingCoroutine);
        }

        lineQueue.Clear();
        isTyping = false;
        EndDialogue();
    }

    public void EndDialogue()
    {
        IsDialogueActive = false;
        if (dialoguePanel != null)
        {
            dialoguePanel.SetActive(false);
        }
    }

    public static void ResetDialogueHistory()
    {
        hasPlayedOpeningDialogue = false;
    }
}