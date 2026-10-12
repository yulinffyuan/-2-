using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class SceneTransitionManager : MonoBehaviour
{
    public static SceneTransitionManager Instance { get; private set; }

    [Header("轉場視覺設定")]
    [Tooltip("轉場底色：冷灰深黑色調")]
    public Color fadeColor = new Color(0.08f, 0.09f, 0.11f, 1f); 
    public float fadeDuration = 0.5f; // 淡出/淡入各需多少秒

    private Canvas transitionCanvas;
    private Image fadeOverlayImage;
    private bool isTransitioning = false;

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            InitializeOverlay();
        }
        else
        {
            Destroy(gameObject);
        }
    }

    /// <summary>
    /// 動態建構最高優先級的遮罩 Canvas，避免場景切換時遺失物件
    /// </summary>
    void InitializeOverlay()
    {
        // 1. 建立獨立 Canvas (最高層級)
        GameObject canvasObj = new GameObject("TransitionCanvas");
        canvasObj.transform.SetParent(transform);

        transitionCanvas = canvasObj.AddComponent<Canvas>();
        transitionCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        transitionCanvas.sortingOrder = 999; // 確保疊在所有 UI 之上

        var scaler = canvasObj.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);

        canvasObj.AddComponent<GraphicRaycaster>();

        // 2. 建立全螢幕覆蓋的冷灰 Image
        GameObject imageObj = new GameObject("FadeOverlay");
        imageObj.transform.SetParent(canvasObj.transform, false);

        fadeOverlayImage = imageObj.AddComponent<Image>();
        fadeOverlayImage.color = new Color(fadeColor.r, fadeColor.g, fadeColor.b, 0f); // 初始全透明
        fadeOverlayImage.raycastTarget = false; // 初始不阻擋點擊

        // 滿版錨點設定 (Stretch All)
        RectTransform rt = fadeOverlayImage.rectTransform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.sizeDelta = Vector2.zero;
        rt.anchoredPosition = Vector2.zero;
    }

    /// <summary>
    /// 對外公開方法：帶冷灰淡出淡入效果切換場景
    /// </summary>
    public void LoadSceneWithFade(string sceneName)
    {
        if (isTransitioning) return;
        StartCoroutine(TransitionRoutine(sceneName));
    }

    IEnumerator TransitionRoutine(string sceneName)
    {
        isTransitioning = true;
        fadeOverlayImage.raycastTarget = true; // 淡出開始，鎖死畫面點擊避免重複觸發

        // 1. 淡出到冷灰黑幕 (Alpha: 0 -> 1)
        float elapsed = 0f;
        while (elapsed < fadeDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float alpha = Mathf.Clamp01(elapsed / fadeDuration);
            fadeOverlayImage.color = new Color(fadeColor.r, fadeColor.g, fadeColor.b, alpha);
            yield return null;
        }
        fadeOverlayImage.color = fadeColor;

        // 2. 非同步載入目標場景
        AsyncOperation asyncLoad = SceneManager.LoadSceneAsync(sceneName);
        while (!asyncLoad.isDone)
        {
            yield return null;
        }

        // 3. 確保時間流速正常
        Time.timeScale = 1f;

        // 稍微等待 1 幀讓新場景物件 (如對話框與玩家) 初始化完畢
        yield return null;

        // 4. 從冷灰黑幕淡入到新場景 (Alpha: 1 -> 0)
        elapsed = 0f;
        while (elapsed < fadeDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float alpha = 1f - Mathf.Clamp01(elapsed / fadeDuration);
            fadeOverlayImage.color = new Color(fadeColor.r, fadeColor.g, fadeColor.b, alpha);
            yield return null;
        }

        fadeOverlayImage.color = new Color(fadeColor.r, fadeColor.g, fadeColor.b, 0f);
        fadeOverlayImage.raycastTarget = false; // 解除點擊鎖定
        isTransitioning = false;
    }
}