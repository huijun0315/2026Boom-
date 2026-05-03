using UnityEngine;
using UnityEngine.UI;

public class AchievementPageController : MonoBehaviour
{
    public const string ReturnScenePrefKey = "achievement_return_scene";
    public const string ReturnPausePrefKey = "achievement_return_pause";

    [Header("Scene")]
    public string backSceneName = "StartScene";

    [Header("PlayerPrefs Keys")]
    [Tooltip("存储当前已选皮肤索引的 PlayerPrefs 键名")]
    public string keySelectedSkin = "ach_selected_skin";
    [Tooltip("存储每个皮肤解锁状态的 PlayerPrefs 键前缀（后接 0~n）")]
    public string keyUnlockPrefix = "ach_unlock_";
    [Tooltip("读取星星时的 PlayerPrefs 键前缀（后接 level id）")]
    public string starKeyPrefix = "star_";

    [Header("Left Icons (5)")]
    public Button[] iconButtons;
    public Image[] iconImages;
    public Text[] iconNumberTexts;
    public Text[] iconLabels;
    public Text[] iconConditionTexts;

    [Header("Right Preview")]
    public Image previewImage;
    public Text previewNumberText;
    public Text unlockConditionText;

    [Header("Bottom Switch")]
    public Button switchButton;

    [Header("Config (replace in Inspector)")]
    [Tooltip("皮肤总数（与下方数组长度对应）")]
    public int skinCount = 5;
    public string[] labels = new string[5] { "成就 1", "成就 2", "成就 3", "成就 4", "成就 5" };
    public Sprite[] iconSprites = new Sprite[5];
    public Sprite[] previewSprites = new Sprite[5];
    [Tooltip("每个皮肤需要的总星星数（可手动配置）")]
    public int[] requiredStars = new int[5] { 0, 2, 4, 7, 10 };
    [Tooltip("Resources/Levels 为空时，回退扫描 level_1 ~ level_N 的上限")]
    public int fallbackLevelScanMax = 50;

    [Header("Style")]
    public Color iconSelectedColor = new Color(0.95f, 0.76f, 0.28f, 1f);
    public Color iconNormalColor = new Color(0.30f, 0.32f, 0.38f, 1f);
    public Color iconLockedColor = new Color(0.22f, 0.22f, 0.24f, 1f);
    [Tooltip("未配置预览图时的占位背景色")]
    public Color previewPlaceholderColor = new Color(0.18f, 0.18f, 0.22f, 1f);
    [Tooltip("左侧 icon 行：未解锁文案颜色")]
    public Color conditionLockedColor = new Color(1f, 0.70f, 0.35f, 1f);
    [Tooltip("左侧 icon 行：已解锁文案颜色")]
    public Color conditionUnlockedColor = new Color(0.55f, 0.95f, 0.60f, 1f);

    int _current = 0;
    int _selectedSkin = 0;
    int _totalStars = 0;

    public static void SetReturnTarget(string sceneName, bool restorePausePanel)
    {
        if (string.IsNullOrEmpty(sceneName)) return;
        PlayerPrefs.SetString(ReturnScenePrefKey, sceneName);
        PlayerPrefs.SetInt(ReturnPausePrefKey, restorePausePanel ? 1 : 0);
        PlayerPrefs.Save();
    }

    public static bool ShouldRestorePausePanel(string currentSceneName)
    {
        if (PlayerPrefs.GetInt(ReturnPausePrefKey, 0) != 1) return false;
        var target = PlayerPrefs.GetString(ReturnScenePrefKey, string.Empty);
        if (string.IsNullOrEmpty(target) || target != currentSceneName) return false;

        PlayerPrefs.DeleteKey(ReturnPausePrefKey);
        PlayerPrefs.DeleteKey(ReturnScenePrefKey);
        PlayerPrefs.Save();
        return true;
    }

    void Start()
    {
        _totalStars = CountTotalStars();
        _selectedSkin = Mathf.Clamp(PlayerPrefs.GetInt(keySelectedSkin, 0), 0, skinCount - 1);
        WireButtons();
        ApplyAll();
        SaveUnlockStates();

        if (!IsUnlocked(_selectedSkin))
        {
            _selectedSkin = FindFirstUnlockedIndex();
            PlayerPrefs.SetInt(keySelectedSkin, _selectedSkin);
            PlayerPrefs.Save();
        }
        Select(_selectedSkin);
    }

    void WireButtons()
    {
        if (iconButtons != null)
        {
            for (int i = 0; i < iconButtons.Length; i++)
            {
                int idx = i;
                if (iconButtons[i] != null)
                {
                    iconButtons[i].onClick.RemoveAllListeners();
                    iconButtons[i].onClick.AddListener(() => { BGMPlayer.PlayDefaultButtonClick(); Select(idx); });
                }
            }
        }

        if (switchButton != null)
        {
            switchButton.onClick.RemoveAllListeners();
            switchButton.onClick.AddListener(() => { BGMPlayer.PlayDefaultButtonClick(); SwitchNext(); });
        }
    }

    void ApplyAll()
    {
        for (int i = 0; i < skinCount; i++)
        {
            if (iconLabels != null && i < iconLabels.Length && iconLabels[i] != null)
            {
                iconLabels[i].text = GetLabel(i);
            }

            if (iconImages != null && i < iconImages.Length && iconImages[i] != null)
            {
                var sp = GetIconSprite(i);
                iconImages[i].sprite = sp;
                iconImages[i].color = (sp == null) ? iconNormalColor : Color.white;
            }

            if (iconNumberTexts != null && i < iconNumberTexts.Length && iconNumberTexts[i] != null)
            {
                iconNumberTexts[i].text = (i + 1).ToString();
                iconNumberTexts[i].gameObject.SetActive(GetIconSprite(i) == null);
            }

            // 未解锁皮肤也可点击查看，不禁用按钮

            if (iconConditionTexts != null && i < iconConditionTexts.Length && iconConditionTexts[i] != null)
            {
                int left = Mathf.Max(0, GetRequiredStars(i) - _totalStars);
                iconConditionTexts[i].text = (left > 0)
                    ? ("还需解锁 " + left + " 个星星才能解锁")
                    : "已解锁";
                iconConditionTexts[i].color = (left > 0)
                    ? conditionLockedColor
                    : conditionUnlockedColor;
            }
        }
    }

    string GetLabel(int idx)
    {
        if (labels != null && idx >= 0 && idx < labels.Length && !string.IsNullOrEmpty(labels[idx]))
            return labels[idx];
        return "成就 " + (idx + 1);
    }

    Sprite GetIconSprite(int idx)
    {
        if (iconSprites != null && idx >= 0 && idx < iconSprites.Length)
            return iconSprites[idx];
        return null;
    }

    Sprite GetPreviewSprite(int idx)
    {
        if (previewSprites != null && idx >= 0 && idx < previewSprites.Length)
            return previewSprites[idx];
        return null;
    }

    int GetRequiredStars(int idx)
    {
        if (requiredStars != null && idx >= 0 && idx < requiredStars.Length)
            return Mathf.Max(0, requiredStars[idx]);
        return 0;
    }

    bool IsUnlocked(int idx)
    {
        return _totalStars >= GetRequiredStars(idx);
    }

    int FindFirstUnlockedIndex()
    {
        for (int i = 0; i < skinCount; i++)
            if (IsUnlocked(i)) return i;
        return 0;
    }

    void SaveUnlockStates()
    {
        for (int i = 0; i < skinCount; i++)
            PlayerPrefs.SetInt(keyUnlockPrefix + i, IsUnlocked(i) ? 1 : 0);
        PlayerPrefs.Save();
    }

    int CountTotalStars()
    {
        int total = 0;
        // 自动扫描 Resources/Levels 下所有关卡文件，按文件名提取 id 读 PlayerPrefs
        var assets = Resources.LoadAll<TextAsset>("Levels");
        if (assets != null && assets.Length > 0)
        {
            foreach (var ta in assets)
            {
                string id = ta.name; // 文件名即 level id（如 level_1）
                total += Mathf.Max(0, PlayerPrefs.GetInt(starKeyPrefix + id, 0));
            }
        }
        else
        {
            // 兜底：若 Resources/Levels 为空，回退扫描 level_1 ~ level_N
            for (int i = 1; i <= fallbackLevelScanMax; i++)
            {
                total += Mathf.Max(0, PlayerPrefs.GetInt(starKeyPrefix + "level_" + i, 0));
            }
        }
        return total;
    }

    void RefreshUnlockText()
    {
        if (unlockConditionText == null) return;
        int need = GetRequiredStars(_current);
        int left = Mathf.Max(0, need - _totalStars);
        if (left > 0)
            unlockConditionText.text = "还需解锁 " + left + " 个星星才能解锁";
        else
            unlockConditionText.text = "已解锁（当前总星星：" + _totalStars + "）";
    }

    public void Select(int idx)
    {
        _current = Mathf.Clamp(idx, 0, skinCount - 1);

        for (int i = 0; i < skinCount; i++)
        {
            if (iconButtons != null && i < iconButtons.Length && iconButtons[i] != null)
            {
                var img = iconButtons[i].GetComponent<Image>();
                if (img != null)
                {
                    if (i == _current) img.color = iconSelectedColor;
                    else if (!IsUnlocked(i)) img.color = iconLockedColor;
                    else img.color = iconNormalColor;
                }
            }
        }

        var ps = GetPreviewSprite(_current);
        if (previewImage != null)
        {
            previewImage.sprite = ps;
            previewImage.color = (ps == null) ? previewPlaceholderColor : Color.white;
        }

        if (previewNumberText != null)
        {
            previewNumberText.text = (_current + 1).ToString();
            previewNumberText.gameObject.SetActive(ps == null);
        }

        if (switchButton != null)
            switchButton.interactable = IsUnlocked(_current);

        RefreshUnlockText();
    }

    public void SwitchNext()
    {
        if (!IsUnlocked(_current)) return;
        // 切换按钮语义：应用当前选中的皮肤
        _selectedSkin = _current;
        PlayerPrefs.SetInt(keySelectedSkin, _selectedSkin);
        PlayerPrefs.Save();
        Select(_selectedSkin);
    }

    public void Back()
    {
        BGMPlayer.PlayDefaultButtonClick();

        string targetScene = PlayerPrefs.GetString(ReturnScenePrefKey, backSceneName);
        bool restorePausePanel = PlayerPrefs.GetInt(ReturnPausePrefKey, 0) == 1;
        if (!restorePausePanel)
        {
            PlayerPrefs.DeleteKey(ReturnPausePrefKey);
            PlayerPrefs.DeleteKey(ReturnScenePrefKey);
            PlayerPrefs.Save();
        }

        if (SceneTransitioner.Instance != null)
            SceneTransitioner.Instance.LoadSceneWithFade(targetScene);
        else
            UnityEngine.SceneManagement.SceneManager.LoadScene(targetScene);
    }
}
