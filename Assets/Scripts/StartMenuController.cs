using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor.SceneManagement;
#endif

public class StartMenuController : MonoBehaviour
{
    [Tooltip("开始游戏要跳转到的场景名（必须加入 Build Settings）")]
    public string nextSceneName = "LevelSelectScene";
    [Tooltip("成就藏馆要跳转到的场景名（必须加入 Build Settings）")]
    public string achievementSceneName = "AchievementScene";
    [Tooltip("是否启用首页的玩家编辑器入口")]
    public bool enablePlayerEditorEntry = false;
    [Tooltip("玩家编辑器要跳转到的场景名（必须加入 Build Settings）")]
    public string playerEditorSceneName = "PlayerEditorScene";

    void Awake()
    {
        // 兼容旧场景：若成就按钮被置灰，运行时强制激活
        var ach = FindObjectOfType<AchievementButton>();
        if (ach != null) ach.SetActive(true);

        // 兼容旧场景：若按钮未在 Builder 中连事件，这里补上
        Button achBtn = null;
        if (ach != null) achBtn = ach.GetComponent<Button>();
        if (achBtn == null)
        {
            var go = GameObject.Find("成就藏馆");
            if (go != null) achBtn = go.GetComponent<Button>();
        }
        if (achBtn != null)
        {
            achBtn.onClick.AddListener(OpenAchievementMuseum);
        }

        SetupPlayerEditorEntry();
    }

    public void StartGame()
    {
        SceneTransitioner.Instance.LoadSceneWithFade(nextSceneName, 0.6f);
    }

    public void ExitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    public void OpenAchievementMuseum()
    {
        AchievementPageController.SetReturnTarget(SceneManager.GetActiveScene().name, false);
        SceneTransitioner.Instance.LoadSceneWithFade(achievementSceneName, 0.6f);
    }

    public void OpenPlayerEditor()
    {
        if (!enablePlayerEditorEntry)
        {
            Debug.Log("[StartMenuController] 玩家编辑器入口已关闭。");
            return;
        }

        string targetScene = playerEditorSceneName;
        if (!Application.CanStreamedLevelBeLoaded(targetScene))
        {
#if UNITY_EDITOR
            const string playerEditorPath = "Assets/Scenes/PlayerEditorScene.unity";
            if (System.IO.File.Exists(playerEditorPath))
            {
                EditorSceneManager.LoadSceneInPlayMode(playerEditorPath, new LoadSceneParameters(LoadSceneMode.Single));
                return;
            }
#endif
            const string fallbackScene = "PipeEditorScene";
            if (Application.CanStreamedLevelBeLoaded(fallbackScene))
            {
                targetScene = fallbackScene;
                Debug.LogWarning("[StartMenuController] PlayerEditorScene 未加入 BuildSettings，已回退到 PipeEditorScene。");
            }
            else
            {
#if UNITY_EDITOR
                const string pipeEditorPath = "Assets/Scenes/PipeEditorScene.unity";
                if (System.IO.File.Exists(pipeEditorPath))
                {
                    EditorSceneManager.LoadSceneInPlayMode(pipeEditorPath, new LoadSceneParameters(LoadSceneMode.Single));
                    return;
                }
#endif
                Debug.LogError("[StartMenuController] 无法进入玩家编辑器：PlayerEditorScene 与 PipeEditorScene 都不可加载。请先加入 Build Settings。");
                return;
            }
        }

        if (SceneTransitioner.Instance != null)
            SceneTransitioner.Instance.LoadSceneWithFade(targetScene, 0.6f);
        else
            SceneManager.LoadScene(targetScene);
    }

    void SetupPlayerEditorEntry()
    {
        if (!enablePlayerEditorEntry)
        {
            var hiddenBtn = FindPlayerEditorButton();
            if (hiddenBtn != null) hiddenBtn.gameObject.SetActive(false);
            return;
        }

        bool unlocked = HasClearedAllLevels();
        var btn = unlocked ? EnsurePlayerEditorButton() : FindPlayerEditorButton();
        if (btn == null) return;

        btn.gameObject.SetActive(unlocked);
        if (!unlocked) return;

        var txt = btn.GetComponentInChildren<Text>();
        if (txt != null) txt.text = "玩家编辑器";
        btn.onClick.RemoveListener(OpenPlayerEditor);
        btn.onClick.AddListener(OpenPlayerEditor);
    }

    Button EnsurePlayerEditorButton()
    {
        var existing = FindPlayerEditorButton();
        if (existing != null)
        {
            ConfigurePlayerEditorButton(existing);
            return existing;
        }

        var startGo = GameObject.Find("开始游戏");
        if (startGo == null) return null;
        var startBtn = startGo.GetComponent<Button>();
        if (startBtn == null) return null;

        var clone = Instantiate(startGo, startGo.transform.parent);
        clone.name = "玩家编辑器";
        var rt = clone.GetComponent<RectTransform>();
        var startRt = startGo.GetComponent<RectTransform>();
        if (rt != null && startRt != null)
            rt.anchoredPosition = startRt.anchoredPosition + new Vector2(0f, -150f);

        var txt = clone.GetComponentInChildren<Text>();
        if (txt != null) txt.text = "玩家编辑器";

        var btn = clone.GetComponent<Button>();
        if (btn == null) return null;
        btn.onClick.RemoveAllListeners();
        ConfigurePlayerEditorButton(btn);
        return btn;
    }

    void ConfigurePlayerEditorButton(Button btn)
    {
        if (btn == null) return;
        var rt = btn.GetComponent<RectTransform>();
        if (rt != null)
        {
            rt.anchorMin = new Vector2(1f, 0f);
            rt.anchorMax = new Vector2(1f, 0f);
            rt.pivot = new Vector2(1f, 0f);
            rt.anchoredPosition = new Vector2(-36f, 28f);
            rt.sizeDelta = new Vector2(260f, 90f);
        }

        var img = btn.GetComponent<Image>();
        if (img != null) img.color = new Color(0.25f, 0.45f, 0.78f);

        var txt = btn.GetComponentInChildren<Text>();
        if (txt != null)
        {
            txt.text = "玩家编辑器";
            txt.fontSize = 36;
        }
    }

    Button FindPlayerEditorButton()
    {
        var go = GameObject.Find("玩家编辑器");
        if (go != null)
        {
            var b = go.GetComponent<Button>();
            if (b != null) return b;
        }
        return null;
    }

    bool HasClearedAllLevels()
    {
        var ordered = LevelStore.LoadOrderedIds();
        if (ordered == null || ordered.Length == 0) return false;

        int validCount = 0;
        for (int i = 0; i < ordered.Length; i++)
        {
            var id = ordered[i];
            if (string.IsNullOrEmpty(id)) continue;
            if (LevelStore.Load(id) == null) continue;
            validCount++;
            if (PlayerPrefs.GetInt("clear_" + id, 0) == 0) return false;
        }
        return validCount > 0;
    }
}
