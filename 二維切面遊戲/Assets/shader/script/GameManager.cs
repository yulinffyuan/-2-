using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [Header("遊戲狀態")]
    public bool isGameOver = false;

    [Header("UI 參照")]
    public GameObject gameOverUI; // GameOver 面板
    public Button retryButton;    // UI 上的 Retry 按鈕 (選填，腳本會自動監聽)

    [Header("鍵位設定")]
    public KeyCode restartKey = KeyCode.P; // 鍵盤 P 鍵重置

    private float gameStartTime;

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        // 開局強制將時間流速重置回 1，杜絕卡頓死鎖
        Time.timeScale = 1f;
        isGameOver = false;

        // 確保開局隱藏死亡面板
        if (gameOverUI != null)
        {
            gameOverUI.SetActive(false);
        }

        // 若 Inspector 有指派 Retry 按鈕，自動綁定點擊事件
        if (retryButton != null)
        {
            retryButton.onClick.RemoveAllListeners();
            retryButton.onClick.AddListener(RestartLevel);
        }
    }

    void Start()
    {
        gameStartTime = Time.time;
    }

    void Update()
    {
        // 鍵盤按下 P 鍵 (無論是否 GameOver 都能重開)
        if (Input.GetKeyDown(restartKey))
        {
            RestartLevel();
        }
    }

    /// <summary>
    /// 主角死亡時呼叫
    /// </summary>
    public void TriggerPlayerDeath()
    {
        if (isGameOver) return;

        // 開局前 0.2 秒防誤觸碰撞
        if (Time.time - gameStartTime < 0.2f) return;

        isGameOver = true;

        var player = FindFirstObjectByType<RotatableSlicePlayer>();
        if (player != null)
        {
            player.Die();
        }

        // 彈出 GameOver UI
        if (gameOverUI != null)
        {
            gameOverUI.SetActive(true);
        }

        Debug.Log("[GameManager] 玩家陣亡，請點擊 Retry 或按下 P 鍵");
    }

    /// <summary>
    /// 重置關卡核心方法 (可供 UI Button 的 OnClick 調用)
    /// </summary>
    public void RestartLevel()
    {
        // 1. 強制重置時間流速與音訊狀態
        Time.timeScale = 1f;
        isGameOver = false;

        // 2. 以場景名稱重新載入 (最穩健，避免 buildIndex 為 -1 導致失效)
        string currentSceneName = SceneManager.GetActiveScene().name;
        if (!string.IsNullOrEmpty(currentSceneName))
        {
            SceneManager.LoadScene(currentSceneName);
        }
        else
        {
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }
    }
}