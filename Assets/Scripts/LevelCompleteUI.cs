using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 通关界面：半透明黑底 + 中央卡片（标题 + "下一关" + "重玩此关卡"），
/// 以及一个全屏礼花 ParticleSystem。
/// 自动构建 UI，监听 PipePuzzle.OnSolved。
/// </summary>
public class LevelCompleteUI : MonoBehaviour
{
    public PipePuzzle puzzle;
    public string homeSceneName = "StartScene";

    [Header("Audio")]
    [Tooltip("通关时播放的 BGM（默认 tongguanBGM）")]
    public AudioClip completionBgmClip;

    private Canvas _canvas;
    private CanvasGroup _group;
    private Text _titleText;
    private Text _subText;
    private Button _nextButton;
    private Button _replayButton;
    private Text _nextLabel;
    private Graphic _starGraphic;
    private Text _starCaption;
    private ParticleSystem _confetti;
    private bool _isFinalLevelMode;

    void Awake()
    {
        if (puzzle == null) puzzle = FindObjectOfType<PipePuzzle>();
        BuildUI();
        BuildConfetti();
        Hide(immediate: true);
    }

    void OnEnable()
    {
        if (puzzle != null) puzzle.OnSolved += HandleSolved;
    }

    void OnDisable()
    {
        if (puzzle != null) puzzle.OnSolved -= HandleSolved;
    }

    void HandleSolved()
    {
        PlayCompletionBgm();
        Show();
    }

    void PlayCompletionBgm()
    {
        if (completionBgmClip == null) return;
        var player = BGMPlayer.Instance;
        if (player == null) return;
        player.SetClip(completionBgmClip, true);
    }

    // ---------------- UI ----------------

    void BuildUI()
    {
        Font font = null;
        try { font = (Font)Resources.GetBuiltinResource(typeof(Font), "LegacyRuntime.ttf"); } catch { }

        var canvasGO = new GameObject("LevelCompleteCanvas",
            typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(CanvasGroup));
        canvasGO.transform.SetParent(transform, false);
        _canvas = canvasGO.GetComponent<Canvas>();
        _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _canvas.sortingOrder = 500;
        var scaler = canvasGO.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;
        _group = canvasGO.GetComponent<CanvasGroup>();

        // 背景遮罩
        var bg = new GameObject("Dim", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        bg.transform.SetParent(canvasGO.transform, false);
        var brt = bg.GetComponent<RectTransform>();
        brt.anchorMin = Vector2.zero; brt.anchorMax = Vector2.one;
        brt.offsetMin = Vector2.zero; brt.offsetMax = Vector2.zero;
        bg.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.55f);

        // 中央卡片
        var cardGO = new GameObject("Card", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        cardGO.transform.SetParent(canvasGO.transform, false);
        var crt = cardGO.GetComponent<RectTransform>();
        crt.anchorMin = new Vector2(0.5f, 0.5f);
        crt.anchorMax = new Vector2(0.5f, 0.5f);
        crt.pivot = new Vector2(0.5f, 0.5f);
        crt.sizeDelta = new Vector2(720, 520);
        cardGO.GetComponent<Image>().color = new Color(0.12f, 0.14f, 0.20f, 0.96f);

        // 标题
        var titleGO = new GameObject("Title", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        titleGO.transform.SetParent(cardGO.transform, false);
        var trt = titleGO.GetComponent<RectTransform>();
        trt.anchorMin = new Vector2(0, 1); trt.anchorMax = new Vector2(1, 1);
        trt.pivot = new Vector2(0.5f, 1);
        trt.anchoredPosition = new Vector2(0, -40);
        trt.sizeDelta = new Vector2(-40, 120);
        _titleText = titleGO.GetComponent<Text>();
        _titleText.text = "通关！";
        _titleText.font = font;
        _titleText.fontSize = 72;
        _titleText.alignment = TextAnchor.MiddleCenter;
        _titleText.color = new Color(1f, 0.85f, 0.2f);
        _titleText.raycastTarget = false;

        // 子标题
        var subGO = new GameObject("Sub", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        subGO.transform.SetParent(cardGO.transform, false);
        var srt = subGO.GetComponent<RectTransform>();
        srt.anchorMin = new Vector2(0, 1); srt.anchorMax = new Vector2(1, 1);
        srt.pivot = new Vector2(0.5f, 1);
        srt.anchoredPosition = new Vector2(0, -160);
        srt.sizeDelta = new Vector2(-40, 40);
        var sub = subGO.GetComponent<Text>();
        sub.text = "所有终点已通水";
        sub.font = font; sub.fontSize = 26;
        sub.color = new Color(1, 1, 1, 0.85f);
        sub.alignment = TextAnchor.MiddleCenter;
        sub.raycastTarget = false;
        _subText = sub;

        // 星星（通关挑战达成时点亮）
        var starGO = new GameObject("Star", typeof(RectTransform), typeof(CanvasRenderer));
        starGO.transform.SetParent(cardGO.transform, false);
        var strt = starGO.GetComponent<RectTransform>();
        strt.anchorMin = new Vector2(0.5f, 1); strt.anchorMax = new Vector2(0.5f, 1);
        strt.pivot = new Vector2(0.5f, 1);
        strt.anchoredPosition = new Vector2(0, -210);
        strt.sizeDelta = new Vector2(90, 90);
        _starGraphic = starGO.AddComponent<StarGraphic>();
        _starGraphic.color = new Color(1, 1, 1, 0.25f);
        _starGraphic.raycastTarget = false;

        // 星星下面的说明文字
        var stcGO = new GameObject("StarCaption", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        stcGO.transform.SetParent(cardGO.transform, false);
        var ctrt = stcGO.GetComponent<RectTransform>();
        ctrt.anchorMin = new Vector2(0, 1); ctrt.anchorMax = new Vector2(1, 1);
        ctrt.pivot = new Vector2(0.5f, 1);
        ctrt.anchoredPosition = new Vector2(0, -300);
        ctrt.sizeDelta = new Vector2(-40, 28);
        _starCaption = stcGO.GetComponent<Text>();
        _starCaption.font = font; _starCaption.fontSize = 20;
        _starCaption.alignment = TextAnchor.MiddleCenter;
        _starCaption.color = new Color(1, 1, 1, 0.7f);
        _starCaption.raycastTarget = false;

        // 按钮
        _nextButton = CreateButton(cardGO.transform, "NextBtn", "下一关",
            font, new Color(0.22f, 0.60f, 0.30f),
            new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
            new Vector2(-160, 80), new Vector2(280, 84), out _nextLabel);

        Text _;
        _replayButton = CreateButton(cardGO.transform, "ReplayBtn", "重玩此关卡",
            font, new Color(0.30f, 0.45f, 0.80f),
            new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
            new Vector2(160, 80), new Vector2(280, 84), out _);

        _nextButton.onClick.AddListener(OnNextClicked);
        _replayButton.onClick.AddListener(OnReplayClicked);
    }

    static Button CreateButton(Transform parent, string name, string label, Font font, Color col,
        Vector2 aMin, Vector2 aMax, Vector2 pivot, Vector2 anchored, Vector2 size, out Text labelText)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = aMin; rt.anchorMax = aMax; rt.pivot = pivot;
        rt.anchoredPosition = anchored; rt.sizeDelta = size;
        var img = go.GetComponent<Image>(); img.color = col;
        var btn = go.GetComponent<Button>();
        btn.targetGraphic = img;
        btn.transition = Selectable.Transition.ColorTint;

        var tx = new GameObject("Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        tx.transform.SetParent(go.transform, false);
        var trt = tx.GetComponent<RectTransform>();
        trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one;
        trt.offsetMin = Vector2.zero; trt.offsetMax = Vector2.zero;
        labelText = tx.GetComponent<Text>();
        labelText.text = label;
        labelText.font = font; labelText.fontSize = 30;
        labelText.color = Color.white;
        labelText.alignment = TextAnchor.MiddleCenter;
        labelText.raycastTarget = false;
        return btn;
    }

    // ---------------- Confetti ----------------

    void BuildConfetti()
    {
        var psGO = new GameObject("Confetti");
        psGO.transform.SetParent(transform, false);

        var cam = Camera.main;
        if (cam != null)
        {
            psGO.transform.SetParent(cam.transform, false);
            psGO.transform.localPosition = new Vector3(0f, 2.5f, 8f); // 摄像机前方上方
            psGO.transform.localRotation = Quaternion.identity;
        }
        else
        {
            psGO.transform.position = new Vector3(0f, 8f, 0f);
        }

        var ps = psGO.AddComponent<ParticleSystem>();
        var main = ps.main;
        main.startLifetime = 3.5f;
        main.startSpeed = 0.5f;
        main.startSize = new ParticleSystem.MinMaxCurve(0.1f, 0.22f);
        main.gravityModifier = 1.0f;
        main.maxParticles = 600;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.loop = false;
        main.playOnAwake = false;

        // 随机彩色
        var grad = new Gradient();
        grad.colorKeys = new[]
        {
            new GradientColorKey(new Color(1.00f, 0.25f, 0.30f), 0.00f),
            new GradientColorKey(new Color(1.00f, 0.80f, 0.20f), 0.20f),
            new GradientColorKey(new Color(0.30f, 1.00f, 0.35f), 0.40f),
            new GradientColorKey(new Color(0.25f, 0.85f, 1.00f), 0.60f),
            new GradientColorKey(new Color(0.70f, 0.45f, 1.00f), 0.80f),
            new GradientColorKey(new Color(1.00f, 0.40f, 0.85f), 1.00f),
        };
        grad.alphaKeys = new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) };
        main.startColor = new ParticleSystem.MinMaxGradient(grad) { mode = ParticleSystemGradientMode.RandomColor };

        var emission = ps.emission;
        emission.enabled = true;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[] {
            new ParticleSystem.Burst(0f, 220),
            new ParticleSystem.Burst(0.25f, 160),
            new ParticleSystem.Burst(0.55f, 120),
        });

        var shape = ps.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(18f, 0.3f, 0.3f);

        var vel = ps.velocityOverLifetime;
        vel.enabled = true;
        vel.space = ParticleSystemSimulationSpace.Local;
        vel.x = new ParticleSystem.MinMaxCurve(-2.5f, 2.5f);
        vel.y = new ParticleSystem.MinMaxCurve(-3.5f, -0.5f);
        vel.z = new ParticleSystem.MinMaxCurve(-0.5f, 0.5f);

        var rot = ps.rotationOverLifetime;
        rot.enabled = true;
        rot.z = new ParticleSystem.MinMaxCurve(-180f, 180f);

        var col = ps.colorOverLifetime;
        col.enabled = true;
        var fade = new Gradient();
        fade.colorKeys = new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) };
        fade.alphaKeys = new[] {
            new GradientAlphaKey(1f, 0f),
            new GradientAlphaKey(1f, 0.7f),
            new GradientAlphaKey(0f, 1f),
        };
        col.color = fade;

        // 材质
        var renderer = ps.GetComponent<ParticleSystemRenderer>();
        Shader sh = Shader.Find("Sprites/Default");
        if (sh == null) sh = Shader.Find("Unlit/Transparent");
        if (sh == null) sh = Shader.Find("UI/Default");
        var mat = new Material(sh) { name = "ConfettiMat" };
        renderer.material = mat;
        renderer.renderMode = ParticleSystemRenderMode.Billboard;

        // 强制停住，避免 playOnAwake 时序导致进场就喷
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        ps.Clear(true);

        _confetti = ps;
    }

    // ---------------- Show/Hide ----------------

    public void Show()
    {
        // 星星状态
        if (_starGraphic != null && puzzle != null)
        {
            bool hasChallenge = puzzle.MoveLimit > 0;
            bool earned = puzzle.EarnedStarThisRun;
            if (hasChallenge)
            {
                _starGraphic.gameObject.SetActive(true);
                _starGraphic.color = earned
                    ? new Color(1f, 0.85f, 0.25f)
                    : new Color(1f, 1f, 1f, 0.22f);
                if (_starCaption != null)
                    _starCaption.text = earned
                        ? "挑战达成！ (" + puzzle.MoveCount + " / " + puzzle.MoveLimit + " 步)"
                        : "未达成：" + puzzle.MoveCount + " 步，限制 " + puzzle.MoveLimit + " 步";
            }
            else
            {
                _starGraphic.gameObject.SetActive(false);
                if (_starCaption != null) _starCaption.text = "";
            }
        }

        if (_nextLabel != null)
        {
            // 下一关不存在则禁用"下一关"按钮
            bool hasNext = false;
            if (puzzle != null)
            {
                string nextId = PipePuzzle.GetNextLevelId(puzzle.loadedLevelId);
                if (!string.IsNullOrEmpty(nextId) && LevelStore.Load(nextId) != null) hasNext = true;
            }
            _isFinalLevelMode = !hasNext;
            _nextButton.interactable = true;
            _nextLabel.text = hasNext ? "下一关" : "回到首页";
            if (_titleText != null)
                _titleText.text = hasNext ? "通关！" : "全部通关！";
            if (_subText != null)
                _subText.text = hasNext ? "所有终点已通水" : "已通关所有关卡，解锁关卡编辑器，可以在首页进入";
            if (_replayButton != null)
                _replayButton.gameObject.SetActive(hasNext);
            var nextRt = _nextButton != null ? _nextButton.GetComponent<RectTransform>() : null;
            if (nextRt != null)
                nextRt.anchoredPosition = hasNext ? new Vector2(-160, 80) : new Vector2(0, 80);
        }

        _group.alpha = 0f;
        _group.blocksRaycasts = true;
        _group.interactable = true;
        _canvas.enabled = true;
        StopAllCoroutines();
        StartCoroutine(FadeIn());

        if (_confetti != null) { _confetti.Clear(); _confetti.Play(); }
    }

    public void Hide(bool immediate = false)
    {
        if (_canvas == null) return;
        _group.blocksRaycasts = false;
        _group.interactable = false;
        if (immediate) { _group.alpha = 0f; _canvas.enabled = false; }
        else StartCoroutine(FadeOut());
    }

    IEnumerator FadeIn()
    {
        float t = 0f, dur = 0.25f;
        while (t < dur)
        {
            t += Time.unscaledDeltaTime;
            _group.alpha = Mathf.Clamp01(t / dur);
            yield return null;
        }
        _group.alpha = 1f;
    }

    IEnumerator FadeOut()
    {
        float t = 0f, dur = 0.2f;
        float from = _group.alpha;
        while (t < dur)
        {
            t += Time.unscaledDeltaTime;
            _group.alpha = Mathf.Lerp(from, 0f, t / dur);
            yield return null;
        }
        _group.alpha = 0f;
        _canvas.enabled = false;
    }

    // ---------------- Button handlers ----------------

    void OnNextClicked()
    {
        BGMPlayer.PlayDefaultButtonClick();
        if (puzzle == null) return;
        Hide();
        if (_isFinalLevelMode)
        {
            if (SceneTransitioner.Instance != null)
                SceneTransitioner.Instance.LoadSceneWithFade(homeSceneName);
            else
                UnityEngine.SceneManagement.SceneManager.LoadScene(homeSceneName);
            return;
        }
        if (!puzzle.LoadNextLevel()) { /* 没有下一关，保持在当前界面 */ }
    }

    void OnReplayClicked()
    {
        BGMPlayer.PlayDefaultButtonClick();
        if (puzzle == null) return;
        Hide();
        puzzle.RestartLevel();
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        if (completionBgmClip == null)
        {
            var atPath = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/art/audio/BGM/tongguanBGM.mp3");
            if (atPath != null) completionBgmClip = atPath;
        }
    }
#endif

}
