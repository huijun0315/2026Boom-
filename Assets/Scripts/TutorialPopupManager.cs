using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.Video;

/// <summary>
/// 引导弹窗管理器（单例 + DontDestroyOnLoad）。
/// 挂在任意 GameObject 上，把 TutorialPopupData 资产拖入 popups 列表。
/// 场景切换时自动检查 FirstEnterLevel 触发；
/// 外部调用 NotifyStarCountChanged() 检查 StarCountReached 触发。
/// </summary>
public class TutorialPopupManager : MonoBehaviour
{
    public static TutorialPopupManager Instance { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void AutoCreate()
    {
        if (FindObjectOfType<TutorialPopupManager>() != null) return;
        var go = new GameObject("TutorialPopupManager");
        go.AddComponent<TutorialPopupManager>();
    }

    [Header("弹窗配置列表")]
    [Tooltip("自动从 Resources/Tutorials 加载，无需手动拖入。也可手动添加")]
    public List<TutorialPopupData> popups = new List<TutorialPopupData>();

    [Tooltip("自动加载的 Resources 子文件夹名")]
    public string autoLoadFolder = "Tutorials";

    [Header("UI 引用（自动创建，也可手动拖入覆盖）")]
    public TutorialPopupUI popupUI;

    [Header("PlayerPrefs 前缀")]
    public string shownKeyPrefix = "tutorial_shown_";

    [Header("星星键前缀（与 AchievementPageController / LevelButton 一致）")]
    public string starKeyPrefix = "star_";

    [Tooltip("Resources/Levels 为空时回退扫描 level_1 ~ level_N 的上限")]
    public int fallbackLevelScanMax = 50;

    // 待弹队列：同一帧可能触发多个，依次弹出
    private readonly Queue<TutorialPopupData> _pending = new Queue<TutorialPopupData>();
    private bool _showing;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        AutoLoadPopups();
        if (popupUI == null) BuildPopupUI();
    }

    /// <summary>
    /// 自动从 Resources/Tutorials 加载所有 TutorialPopupData 资产。
    /// 只需把 Popup Data 资产放到 Assets/Resources/Tutorials/ 文件夹即可，无需手动拖入。
    /// </summary>
    void AutoLoadPopups()
    {
        if (popups == null) popups = new List<TutorialPopupData>();
        var loaded = Resources.LoadAll<TutorialPopupData>(autoLoadFolder);
        if (loaded != null)
        {
            foreach (var d in loaded)
            {
                if (d != null && !popups.Contains(d))
                    popups.Add(d);
            }
        }
    }

    void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        Debug.Log("[TutorialPopupManager] Scene loaded: " + scene.name + ", PendingLevelId=" + PipePuzzle.PendingLevelId);
        CheckFirstEnterTriggers();
    }

    void Update()
    {
        // 如果当前没有在显示弹窗，且队列有待弹出的，弹出下一个
        if (!_showing && _pending.Count > 0)
        {
            var data = _pending.Dequeue();
            ShowPopup(data);
        }
    }

    /// <summary>
    /// 外部调用：当总星星数可能变化时通知管理器检查 StarCountReached 触发。
    /// 例如 PipePuzzle 通关后调用。
    /// </summary>
    public void NotifyStarCountChanged()
    {
        CheckStarCountTriggers();
    }

    // ---- 内部检查 ----

    void CheckFirstEnterTriggers()
    {
        if (popups == null || popups.Count == 0)
        {
            Debug.Log("[TutorialPopupManager] No popups configured.");
            return;
        }
        string currentLevelId = PipePuzzle.PendingLevelId;
        Debug.Log("[TutorialPopupManager] Checking FirstEnter triggers, currentLevelId=" + currentLevelId);

        foreach (var p in popups)
        {
            if (p == null) continue;
            if (p.triggerType != TutorialPopupData.TriggerType.FirstEnterLevel) continue;
            if (HasShown(p)) { Debug.Log("[TutorialPopupManager] Already shown: " + p.popupId); continue; }
            if (p.triggerLevelId != currentLevelId) { Debug.Log("[TutorialPopupManager] Level mismatch: trigger=" + p.triggerLevelId + " vs current=" + currentLevelId); continue; }

            Debug.Log("[TutorialPopupManager] Triggered popup: " + p.popupId);
            MarkShown(p);
            _pending.Enqueue(p);
        }
    }

    void CheckStarCountTriggers()
    {
        if (popups == null) return;
        int totalStars = CountTotalStars();

        foreach (var p in popups)
        {
            if (p == null) continue;
            if (p.triggerType != TutorialPopupData.TriggerType.StarCountReached) continue;
            if (HasShown(p)) continue;
            if (totalStars < p.triggerStarCount) continue;

            MarkShown(p);
            _pending.Enqueue(p);
        }
    }

    // ---- 显示 / 关闭 ----

    void ShowPopup(TutorialPopupData data)
    {
        _showing = true;
        if (popupUI != null)
        {
            Debug.Log("[TutorialPopupManager] Showing popup: " + data.popupId + " - " + data.title);
            popupUI.Show(data, OnPopupClosed);
        }
        else
        {
            Debug.LogWarning("[TutorialPopupManager] popupUI is null, skipping popup: " + data.popupId);
            OnPopupClosed();
        }
    }

    void OnPopupClosed()
    {
        _showing = false;
    }

    // ---- 持久化 ----

    bool HasShown(TutorialPopupData p)
    {
        return PlayerPrefs.GetInt(shownKeyPrefix + p.popupId, 0) == 1;
    }

    void MarkShown(TutorialPopupData p)
    {
        PlayerPrefs.SetInt(shownKeyPrefix + p.popupId, 1);
        PlayerPrefs.Save();
    }

    /// <summary>
    /// 重置所有弹窗的已弹出状态（调试用）。
    /// </summary>
    public void ResetAllShown()
    {
        if (popups == null) return;
        foreach (var p in popups)
        {
            if (p != null) PlayerPrefs.DeleteKey(shownKeyPrefix + p.popupId);
        }
        PlayerPrefs.Save();
    }

    // ---- 自动构建弹窗 UI ----

    void BuildPopupUI()
    {
        Debug.Log("[TutorialPopupManager] Auto-building popup UI...");

        // 自带 Canvas（overlay），挂在 Manager 自身下，随 DontDestroyOnLoad 持久
        var canvasGO = new GameObject("TutorialCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasGO.transform.SetParent(transform, false);
        var canvas = canvasGO.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 9999;
        var scaler = canvasGO.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;

        Font font = null;
        try { font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); } catch { }

        // Root overlay（全屏遮罩）
        var rootGO = new GameObject("TutorialPopupUI", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(CanvasGroup));
        rootGO.transform.SetParent(canvasGO.transform, false);
        var rootRt = rootGO.GetComponent<RectTransform>();
        rootRt.anchorMin = Vector2.zero; rootRt.anchorMax = Vector2.one;
        rootRt.offsetMin = Vector2.zero; rootRt.offsetMax = Vector2.zero;
        var rootGroup = rootGO.GetComponent<CanvasGroup>();
        rootGroup.alpha = 0f; rootGroup.interactable = false; rootGroup.blocksRaycasts = false;
        var bgOverlay = rootGO.GetComponent<Image>();
        bgOverlay.color = new Color(0f, 0f, 0f, 0.65f);

        // Center panel（居中面板）
        var panelGO = new GameObject("Panel", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        panelGO.transform.SetParent(rootGO.transform, false);
        var panelRt = panelGO.GetComponent<RectTransform>();
        panelRt.anchorMin = new Vector2(0.5f, 0.5f); panelRt.anchorMax = new Vector2(0.5f, 0.5f);
        panelRt.pivot = new Vector2(0.5f, 0.5f);
        panelRt.anchoredPosition = Vector2.zero; panelRt.sizeDelta = new Vector2(800f, 600f);
        panelGO.GetComponent<Image>().color = new Color(0.12f, 0.13f, 0.17f, 0.95f);

        // ---- 面板内子元素，按从上到下顺序创建（sibling order = 渲染顺序）----

        // 1. Title（顶部 88%~98%）
        var titleGO = new GameObject("Title", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        titleGO.transform.SetParent(panelGO.transform, false);
        var titleRt = titleGO.GetComponent<RectTransform>();
        titleRt.anchorMin = new Vector2(0f, 0.88f); titleRt.anchorMax = new Vector2(1f, 0.98f);
        titleRt.offsetMin = new Vector2(24, 0); titleRt.offsetMax = new Vector2(-24, 0);
        var titleText = titleGO.GetComponent<Text>();
        titleText.text = ""; titleText.font = font; titleText.fontSize = 32; titleText.color = Color.white;
        titleText.alignment = TextAnchor.MiddleCenter;

        // 2. Video section（50%~86%，始终 active，用 CanvasGroup 控制可见性）
        var videoSectionGO = new GameObject("VideoSection", typeof(RectTransform), typeof(CanvasGroup));
        videoSectionGO.transform.SetParent(panelGO.transform, false);
        var videoRt = videoSectionGO.GetComponent<RectTransform>();
        videoRt.anchorMin = new Vector2(0f, 0.50f); videoRt.anchorMax = new Vector2(1f, 0.86f);
        videoRt.offsetMin = new Vector2(24, 4); videoRt.offsetMax = new Vector2(-24, -4);
        var videoGroup = videoSectionGO.GetComponent<CanvasGroup>();
        videoGroup.alpha = 0f; videoGroup.blocksRaycasts = false;

        var videoImgGO = new GameObject("VideoImage", typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage));
        videoImgGO.transform.SetParent(videoSectionGO.transform, false);
        var videoImgRt = videoImgGO.GetComponent<RectTransform>();
        videoImgRt.anchorMin = Vector2.zero; videoImgRt.anchorMax = Vector2.one;
        videoImgRt.offsetMin = Vector2.zero; videoImgRt.offsetMax = Vector2.zero;
        var videoImage = videoImgGO.GetComponent<RawImage>();
        videoImage.color = Color.black;

        var videoPlayer = videoSectionGO.AddComponent<VideoPlayer>();
        videoPlayer.playOnAwake = false;
        videoPlayer.waitForFirstFrame = true;
        videoPlayer.skipOnDrop = true;
        videoPlayer.renderMode = VideoRenderMode.RenderTexture; // 渲染到 RenderTexture
        var videoAudioSource = videoSectionGO.AddComponent<AudioSource>();
        videoPlayer.audioOutputMode = VideoAudioOutputMode.AudioSource;
        videoPlayer.SetTargetAudioSource(0, videoAudioSource);

        // 3. Body text（10%~48%，无视频时扩展到 10%~86%）
        var bodyGO = new GameObject("BodyText", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        bodyGO.transform.SetParent(panelGO.transform, false);
        var bodyRt = bodyGO.GetComponent<RectTransform>();
        bodyRt.anchorMin = new Vector2(0f, 0.10f); bodyRt.anchorMax = new Vector2(1f, 0.48f);
        bodyRt.offsetMin = new Vector2(24, 0); bodyRt.offsetMax = new Vector2(-24, 0);
        var bodyText = bodyGO.GetComponent<Text>();
        bodyText.text = ""; bodyText.font = font; bodyText.fontSize = 22;
        bodyText.color = new Color(1f, 1f, 1f, 0.9f);
        bodyText.alignment = TextAnchor.UpperLeft;
        bodyText.horizontalOverflow = HorizontalWrapMode.Wrap;
        bodyText.verticalOverflow = VerticalWrapMode.Overflow;

        // 4. Close button（底部 2%~10%）
        var closeGO = new GameObject("CloseButton", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        closeGO.transform.SetParent(panelGO.transform, false);
        var closeRt = closeGO.GetComponent<RectTransform>();
        closeRt.anchorMin = new Vector2(0.35f, 0.02f); closeRt.anchorMax = new Vector2(0.65f, 0.10f);
        closeRt.offsetMin = Vector2.zero; closeRt.offsetMax = Vector2.zero;
        var closeImg = closeGO.GetComponent<Image>();
        closeImg.color = new Color(0.22f, 0.55f, 0.85f);
        var closeBtn = closeGO.GetComponent<Button>();
        closeBtn.targetGraphic = closeImg; closeBtn.transition = Selectable.Transition.ColorTint;

        var closeLblGO = new GameObject("Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        closeLblGO.transform.SetParent(closeGO.transform, false);
        var closeLblRt = closeLblGO.GetComponent<RectTransform>();
        closeLblRt.anchorMin = Vector2.zero; closeLblRt.anchorMax = Vector2.one;
        closeLblRt.offsetMin = Vector2.zero; closeLblRt.offsetMax = Vector2.zero;
        var closeLbl = closeLblGO.GetComponent<Text>();
        closeLbl.text = "我知道了"; closeLbl.font = font; closeLbl.fontSize = 24;
        closeLbl.color = Color.white; closeLbl.alignment = TextAnchor.MiddleCenter;

        // Attach TutorialPopupUI component
        var ui = rootGO.AddComponent<TutorialPopupUI>();
        ui.rootGroup = rootGroup;
        ui.titleText = titleText;
        ui.bodyTextObj = bodyText;
        ui.videoGroup = videoGroup;
        ui.videoImage = videoImage;
        ui.videoPlayer = videoPlayer;
        ui.videoAudioSource = videoAudioSource;
        ui.closeButton = closeBtn;
        ui.Init();

        popupUI = ui;
        Debug.Log("[TutorialPopupManager] Popup UI built successfully.");
    }

    // ---- 星星计数（与 AchievementPageController 逻辑一致） ----

    int CountTotalStars()
    {
        int total = 0;
        var assets = Resources.LoadAll<TextAsset>("Levels");
        if (assets != null && assets.Length > 0)
        {
            foreach (var ta in assets)
                total += Mathf.Max(0, PlayerPrefs.GetInt(starKeyPrefix + ta.name, 0));
        }
        else
        {
            for (int i = 1; i <= fallbackLevelScanMax; i++)
                total += Mathf.Max(0, PlayerPrefs.GetInt(starKeyPrefix + "level_" + i, 0));
        }
        return total;
    }
}
