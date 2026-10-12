using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class MainMenuController : MonoBehaviour
{
    [Header("目標場景名稱")]
    [Tooltip("按下開始遊戲後要載入的關卡場景名稱")]
    public string targetSceneName = "Playtest";

    [Header("選單按鈕")]
    public Button startButton;          // 開始遊戲
    public Button levelSelectButton;    // 選擇關卡 (尚未實作 -> 置灰)
    public Button recordsButton;        // 關卡紀錄 (尚未實作 -> 置灰)
    public Button exitButton;           // 退出遊戲

    [Header("按鈕置灰顏色")]
    public Color disabledTextColor = new Color(0.5f, 0.5f, 0.5f, 0.5f);

    void Start()
    {
        Time.timeScale = 1f;

        if (startButton != null)
        {
            startButton.onClick.AddListener(OnStartGame);
        }

        if (exitButton != null)
        {
            exitButton.onClick.AddListener(OnExitGame);
        }

        SetButtonDisabled(levelSelectButton);
        SetButtonDisabled(recordsButton);
    }

void OnStartGame()
    {
        // 重置開局對話播放狀態，讓新周目重新播放
        DialogueManager.ResetDialogueHistory();

        if (SceneTransitionManager.Instance != null)
        {
            SceneTransitionManager.Instance.LoadSceneWithFade(targetSceneName);
        }
        else
        {
            SceneManager.LoadScene(targetSceneName);
        }
    }

    void OnExitGame()
    {
        Debug.Log("[MainMenu] 退出遊戲");
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    void SetButtonDisabled(Button btn)
    {
        if (btn == null) return;

        btn.interactable = false;

        var txt = btn.GetComponentInChildren<Text>();
        if (txt != null)
        {
            txt.color = disabledTextColor;
        }

        var tmp = btn.GetComponentInChildren<TMPro.TMP_Text>();
        if (tmp != null)
        {
            tmp.color = disabledTextColor;
        }
    }
}