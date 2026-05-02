using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

/// <summary>
/// 关卡场景左上角挑战信息 HUD：
///   "在 X 步内过关 (Y/X)" —— 步数限制存在时
///   "步数 Y"              —— 无限制时
/// 超出限制后变红。
/// </summary>
public class ChallengeHUD : MonoBehaviour
{
    public PipePuzzle puzzle;
    public RubikCube cube;
    public Text hudText;
    Button _undoButton;
    Button _resetOrientButton;
    GameObject _pausePanel;
    string _levelSelectSceneName = "LevelSelectScene";
    string _achievementSceneName = "AchievementScene";

    void Awake()
    {
        if (puzzle == null) puzzle = FindObjectOfType<PipePuzzle>();
        if (cube == null) cube = FindObjectOfType<RubikCube>();
        BuildUI();
        if (AchievementPageController.ShouldRestorePausePanel(SceneManager.GetActiveScene().name))
            OnPauseClicked();
        Refresh();
    }

    void OnEnable()
    {
        if (puzzle != null) puzzle.OnMoveCountChanged += Refresh;
    }

    void OnDisable()
    {
        if (puzzle != null) puzzle.OnMoveCountChanged -= Refresh;
        if (Time.timeScale == 0f) Time.timeScale = 1f;
    }

    void BuildUI()
    {
        Font font = null;
        try { font = (Font)Resources.GetBuiltinResource(typeof(Font), "LegacyRuntime.ttf"); } catch { }

        var canvasGO = new GameObject("ChallengeHUDCanvas",
            typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasGO.transform.SetParent(transform, false);
        var cv = canvasGO.GetComponent<Canvas>();
        cv.renderMode = RenderMode.ScreenSpaceOverlay;
        cv.sortingOrder = 100;
        var scaler = canvasGO.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;

        var bgGO = new GameObject("Bg", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        bgGO.transform.SetParent(canvasGO.transform, false);
        var brt = bgGO.GetComponent<RectTransform>();
        brt.anchorMin = new Vector2(0, 1);
        brt.anchorMax = new Vector2(0, 1);
        brt.pivot = new Vector2(0, 1);
        brt.anchoredPosition = new Vector2(24, -24);
        brt.sizeDelta = new Vector2(520, 88);
        bgGO.GetComponent<Image>().color = new Color(0, 0, 0, 0.50f);
        bgGO.GetComponent<Image>().raycastTarget = false;

        var txGO = new GameObject("Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        txGO.transform.SetParent(bgGO.transform, false);
        var trt = txGO.GetComponent<RectTransform>();
        trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one;
        trt.offsetMin = new Vector2(20, 8); trt.offsetMax = new Vector2(-20, -8);
        hudText = txGO.GetComponent<Text>();
        hudText.font = font;
        hudText.fontSize = 32;
        hudText.alignment = TextAnchor.MiddleLeft;
        hudText.color = Color.white;
        hudText.raycastTarget = false;

        // 右上角：重置按钮
        var btnGO = new GameObject("ResetBtn",
            typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        btnGO.transform.SetParent(canvasGO.transform, false);
        var rbrt = btnGO.GetComponent<RectTransform>();
        rbrt.anchorMin = new Vector2(1, 1);
        rbrt.anchorMax = new Vector2(1, 1);
        rbrt.pivot = new Vector2(1, 1);
        rbrt.anchoredPosition = new Vector2(-24, -24);
        rbrt.sizeDelta = new Vector2(160, 72);
        var btnImg = btnGO.GetComponent<Image>();
        btnImg.color = new Color(0.60f, 0.30f, 0.30f, 0.92f);
        var btn = btnGO.GetComponent<Button>();
        btn.targetGraphic = btnImg;

        var lblGO = new GameObject("Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        lblGO.transform.SetParent(btnGO.transform, false);
        var lrt = lblGO.GetComponent<RectTransform>();
        lrt.anchorMin = Vector2.zero; lrt.anchorMax = Vector2.one;
        lrt.offsetMin = Vector2.zero; lrt.offsetMax = Vector2.zero;
        var lbl = lblGO.GetComponent<Text>();
        lbl.text = "重置";
        lbl.font = font;
        lbl.fontSize = 30;
        lbl.color = Color.white;
        lbl.alignment = TextAnchor.MiddleCenter;
        lbl.raycastTarget = false;

        btn.onClick.AddListener(OnResetClicked);

        // 右上角左侧：撤销按钮
        var undoGO = new GameObject("UndoBtn",
            typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        undoGO.transform.SetParent(canvasGO.transform, false);
        var urt = undoGO.GetComponent<RectTransform>();
        urt.anchorMin = new Vector2(1, 1);
        urt.anchorMax = new Vector2(1, 1);
        urt.pivot = new Vector2(1, 1);
        urt.anchoredPosition = new Vector2(-24 - 160 - 12, -24);
        urt.sizeDelta = new Vector2(160, 72);
        var uimg = undoGO.GetComponent<Image>();
        uimg.color = new Color(0.30f, 0.45f, 0.80f, 0.92f);
        _undoButton = undoGO.GetComponent<Button>();
        _undoButton.targetGraphic = uimg;

        var ulblGO = new GameObject("Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        ulblGO.transform.SetParent(undoGO.transform, false);
        var ulrt = ulblGO.GetComponent<RectTransform>();
        ulrt.anchorMin = Vector2.zero; ulrt.anchorMax = Vector2.one;
        ulrt.offsetMin = Vector2.zero; ulrt.offsetMax = Vector2.zero;
        var ulbl = ulblGO.GetComponent<Text>();
        ulbl.text = "撤销";
        ulbl.font = font;
        ulbl.fontSize = 30;
        ulbl.color = Color.white;
        ulbl.alignment = TextAnchor.MiddleCenter;
        ulbl.raycastTarget = false;

        _undoButton.onClick.AddListener(OnUndoClicked);

        // 右上角再往左：回正按钮
        var resetOriGO = new GameObject("ResetOrientBtn",
            typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        resetOriGO.transform.SetParent(canvasGO.transform, false);
        var rort = resetOriGO.GetComponent<RectTransform>();
        rort.anchorMin = new Vector2(1, 1);
        rort.anchorMax = new Vector2(1, 1);
        rort.pivot = new Vector2(1, 1);
        rort.anchoredPosition = new Vector2(-24 - (160 + 12) * 2, -24);
        rort.sizeDelta = new Vector2(160, 72);
        var roimg = resetOriGO.GetComponent<Image>();
        roimg.color = new Color(0.25f, 0.55f, 0.40f, 0.92f);
        _resetOrientButton = resetOriGO.GetComponent<Button>();
        _resetOrientButton.targetGraphic = roimg;

        var rolblGO = new GameObject("Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        rolblGO.transform.SetParent(resetOriGO.transform, false);
        var rolrt = rolblGO.GetComponent<RectTransform>();
        rolrt.anchorMin = Vector2.zero; rolrt.anchorMax = Vector2.one;
        rolrt.offsetMin = Vector2.zero; rolrt.offsetMax = Vector2.zero;
        var rolbl = rolblGO.GetComponent<Text>();
        rolbl.text = "回正";
        rolbl.font = font;
        rolbl.fontSize = 30;
        rolbl.color = Color.white;
        rolbl.alignment = TextAnchor.MiddleCenter;
        rolbl.raycastTarget = false;
        _resetOrientButton.onClick.AddListener(OnResetOrientationClicked);

        // 右上角最左：暂停按钮
        var pauseGO = new GameObject("PauseBtn",
            typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        pauseGO.transform.SetParent(canvasGO.transform, false);
        var prt = pauseGO.GetComponent<RectTransform>();
        prt.anchorMin = new Vector2(1, 1);
        prt.anchorMax = new Vector2(1, 1);
        prt.pivot = new Vector2(1, 1);
        prt.anchoredPosition = new Vector2(-24 - (160 + 12) * 3, -24);
        prt.sizeDelta = new Vector2(160, 72);
        var pimg = pauseGO.GetComponent<Image>();
        pimg.color = new Color(0.35f, 0.35f, 0.35f, 0.92f);
        var pauseBtn = pauseGO.GetComponent<Button>();
        pauseBtn.targetGraphic = pimg;

        var plblGO = new GameObject("Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        plblGO.transform.SetParent(pauseGO.transform, false);
        var plrt = plblGO.GetComponent<RectTransform>();
        plrt.anchorMin = Vector2.zero; plrt.anchorMax = Vector2.one;
        plrt.offsetMin = Vector2.zero; plrt.offsetMax = Vector2.zero;
        var plbl = plblGO.GetComponent<Text>();
        plbl.text = "暂停";
        plbl.font = font;
        plbl.fontSize = 30;
        plbl.color = Color.white;
        plbl.alignment = TextAnchor.MiddleCenter;
        plbl.raycastTarget = false;
        pauseBtn.onClick.AddListener(OnPauseClicked);

        // 暂停面板（默认隐藏）
        _pausePanel = new GameObject("PausePanel", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        _pausePanel.transform.SetParent(canvasGO.transform, false);
        var part = _pausePanel.GetComponent<RectTransform>();
        part.anchorMin = Vector2.zero;
        part.anchorMax = Vector2.one;
        part.offsetMin = Vector2.zero;
        part.offsetMax = Vector2.zero;
        _pausePanel.GetComponent<Image>().color = new Color(0, 0, 0, 0.75f);

        var titleGO = new GameObject("Title", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        titleGO.transform.SetParent(_pausePanel.transform, false);
        var tr = titleGO.GetComponent<RectTransform>();
        tr.anchorMin = new Vector2(0.5f, 0.5f);
        tr.anchorMax = new Vector2(0.5f, 0.5f);
        tr.pivot = new Vector2(0.5f, 0.5f);
        tr.anchoredPosition = new Vector2(0, 250);
        tr.sizeDelta = new Vector2(560, 100);
        var t = titleGO.GetComponent<Text>();
        t.text = "已暂停";
        t.font = font;
        t.fontSize = 64;
        t.alignment = TextAnchor.MiddleCenter;
        t.color = Color.white;
        t.raycastTarget = false;

        var resumeBtn = CreatePauseButton(_pausePanel.transform, font, "继续游戏", new Vector2(0, 110), new Color(0.22f, 0.62f, 0.32f));
        resumeBtn.onClick.AddListener(OnPauseResumeClicked);

        var backBtn = CreatePauseButton(_pausePanel.transform, font, "返回", new Vector2(0, -10), new Color(0.24f, 0.45f, 0.78f));
        backBtn.onClick.AddListener(OnPauseBackClicked);

        var achBtn = CreatePauseButton(_pausePanel.transform, font, "成就藏馆", new Vector2(0, -130), new Color(0.40f, 0.40f, 0.40f));
        achBtn.onClick.AddListener(OnPauseAchievementClicked);

        _pausePanel.SetActive(false);
    }

    Button CreatePauseButton(Transform parent, Font font, string label, Vector2 pos, Color color)
    {
        var go = new GameObject(label + "Btn", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = new Vector2(420, 96);
        var img = go.GetComponent<Image>();
        img.color = color;
        var btn = go.GetComponent<Button>();
        btn.targetGraphic = img;

        var txtGO = new GameObject("Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        txtGO.transform.SetParent(go.transform, false);
        var txtRt = txtGO.GetComponent<RectTransform>();
        txtRt.anchorMin = Vector2.zero;
        txtRt.anchorMax = Vector2.one;
        txtRt.offsetMin = Vector2.zero;
        txtRt.offsetMax = Vector2.zero;
        var txt = txtGO.GetComponent<Text>();
        txt.text = label;
        txt.font = font;
        txt.fontSize = 44;
        txt.alignment = TextAnchor.MiddleCenter;
        txt.color = Color.white;
        txt.raycastTarget = false;
        return btn;
    }

    void OnResetOrientationClicked()
    {
        if (cube != null) cube.ResetOrientation();
    }

    void OnResetClicked()
    {
        if (puzzle == null) return;
        puzzle.RestartLevel();
        Refresh();
    }

    void OnUndoClicked()
    {
        if (puzzle == null) return;
        puzzle.UndoLastMove(); // 完成后 OnMoveCountChanged 会触发 Refresh()
    }

    void OnPauseClicked()
    {
        if (_pausePanel == null) return;
        _pausePanel.SetActive(true);
        Time.timeScale = 0f;
    }

    void OnPauseBackClicked()
    {
        Time.timeScale = 1f;
        if (SceneTransitioner.Instance != null)
            SceneTransitioner.Instance.LoadSceneWithFade(_levelSelectSceneName);
        else
            UnityEngine.SceneManagement.SceneManager.LoadScene(_levelSelectSceneName);
    }

    void OnPauseAchievementClicked()
    {
        AchievementPageController.SetReturnTarget(SceneManager.GetActiveScene().name, true);
        Time.timeScale = 1f;
        if (SceneTransitioner.Instance != null)
            SceneTransitioner.Instance.LoadSceneWithFade(_achievementSceneName);
        else
            UnityEngine.SceneManagement.SceneManager.LoadScene(_achievementSceneName);
    }

    void OnPauseResumeClicked()
    {
        Time.timeScale = 1f;
        if (_pausePanel != null) _pausePanel.SetActive(false);
    }

    public void Refresh()
    {
        if (_undoButton != null && puzzle != null) _undoButton.interactable = puzzle.CanUndo;
        if (hudText == null || puzzle == null) return;
        int y = puzzle.MoveCount;
        int x = puzzle.MoveLimit;
        if (x > 0)
        {
            hudText.text = "在 " + x + " 步内过关 (" + y + "/" + x + ")";
            hudText.color = (y > x) ? new Color(1f, 0.35f, 0.35f) : Color.white;
        }
        else
        {
            hudText.text = "步数 " + y;
            hudText.color = Color.white;
        }
    }
}
