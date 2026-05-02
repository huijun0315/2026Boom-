using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Image))]
[RequireComponent(typeof(CanvasGroup))]
public class LevelButton : MonoBehaviour
{
    [Header("Level Info")]
    public int levelIndex = 1;
    public string levelName = "关卡 1";
    [Tooltip("对应 Assets/Resources/Levels/<id>.json 的文件名（不含扩展名）")]
    public string levelId = "level_1";
    [Tooltip("点击后跳转到的场景名")]
    public string targetScene = "CubeScene";
    [Tooltip("前置关卡ID（为空表示首关，始终解锁）")]
    public string prerequisiteLevelId = "";

    [Header("State")]
    [Tooltip("此关卡是否解锁")]
    public bool isUnlocked = true;

    [Header("Stars (per this button)")]
    [Range(0, 5)]
    public int maxStars = 1;
    [Range(0, 5)]
    [Tooltip("已点亮的星星数量")]
    public int starsEarned = 0;

    [Header("Colors")]
    public Color unlockedColor = new Color(0.32f, 0.55f, 0.80f);
    public Color lockedColor = new Color(0.35f, 0.35f, 0.35f);
    public Color starOnColor = new Color(1f, 0.85f, 0.25f);
    public Color starOffColor = new Color(1f, 1f, 1f, 0.5f); // 半透明白，锁定/解锁按钮上都清晰可见

    [Header("BGM")]
    [Tooltip("进入该关卡后播放的背景音乐；为空则不切换 BGM")]
    public AudioClip levelBGM;

    [Header("Refs (auto-wired by builder)")]
    public Image buttonImage;
    public Text nameText;
    public Graphic[] starGraphics;   // 每个星星的 Graphic（用 StarGraphic 或其他）

    private CanvasGroup _cg;

    void Awake()
    {
        _cg = GetComponent<CanvasGroup>();
        if (buttonImage == null) buttonImage = GetComponent<Image>();

        // 从 PlayerPrefs 读取已保存的星星进度
        int saved = 0;
        if (!string.IsNullOrEmpty(levelId))
        {
            saved = PlayerPrefs.GetInt("star_" + levelId, 0);
            if (saved > starsEarned) starsEarned = Mathf.Min(saved, maxStars);
        }

        if (!string.IsNullOrEmpty(prerequisiteLevelId))
        {
            int prevClear = PlayerPrefs.GetInt("clear_" + prerequisiteLevelId, 0);
            int selfClear = !string.IsNullOrEmpty(levelId) ? PlayerPrefs.GetInt("clear_" + levelId, 0) : 0;
            isUnlocked = (prevClear > 0) || (selfClear > 0);
        }
        else
        {
            isUnlocked = true;
        }

        ApplyState();

        var btn = GetComponent<Button>();
        if (btn != null) btn.onClick.AddListener(OnClicked);
    }

    public void OnClicked()
    {
        if (!isUnlocked) return;

        // 切换到该关卡的 BGM
        if (levelBGM != null && BGMPlayer.Instance != null)
            BGMPlayer.Instance.SetClip(levelBGM, true);

        PipePuzzle.PendingLevelId = levelId;
        if (SceneTransitioner.Instance != null)
            SceneTransitioner.Instance.LoadSceneWithFade(targetScene);
        else
            UnityEngine.SceneManagement.SceneManager.LoadScene(targetScene);
    }

    void OnValidate()
    {
        if (_cg == null) _cg = GetComponent<CanvasGroup>();
        if (buttonImage == null) buttonImage = GetComponent<Image>();
        ApplyState();
    }

    public void SetUnlocked(bool unlocked)
    {
        isUnlocked = unlocked;
        ApplyState();
    }

    public void SetStars(int n)
    {
        starsEarned = Mathf.Clamp(n, 0, maxStars);
        ApplyStars();
    }

    private void ApplyState()
    {
        if (buttonImage != null)
            buttonImage.color = isUnlocked ? unlockedColor : lockedColor;

        if (_cg != null)
        {
            _cg.interactable = isUnlocked;
            _cg.blocksRaycasts = isUnlocked;
        }

        if (nameText != null)
            nameText.text = levelName;

        var numberTf = transform.Find("Number");
        var numberText = numberTf != null ? numberTf.GetComponent<Text>() : null;
        if (numberText != null)
            numberText.text = isUnlocked ? levelIndex.ToString() : "?";

        ApplyStars();
    }

    private void ApplyStars()
    {
        if (starGraphics == null) return;
        for (int i = 0; i < starGraphics.Length; i++)
        {
            if (starGraphics[i] == null) continue;
            bool on = isUnlocked && i < starsEarned;
            starGraphics[i].color = on ? starOnColor : starOffColor;
        }
    }
}
