using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 单例 + DontDestroyOnLoad。提供带淡入/淡出的场景切换。
/// 首次调用时懒加载一个全屏黑色 Image overlay。
/// </summary>
public class SceneTransitioner : MonoBehaviour
{
    private static SceneTransitioner _instance;
    public static SceneTransitioner Instance
    {
        get
        {
            if (_instance == null)
            {
                var go = new GameObject("[SceneTransitioner]");
                _instance = go.AddComponent<SceneTransitioner>();
                _instance.BuildOverlay();
                DontDestroyOnLoad(go);
            }
            return _instance;
        }
    }

    public Color overlayColor = Color.black;
    public float defaultFadeDuration = 0.5f;

    private Canvas _canvas;
    private Image _overlayImage;
    private CanvasGroup _group;
    private bool _busy;

    void Awake()
    {
        if (_instance != null && _instance != this) { Destroy(gameObject); return; }
        _instance = this;
        DontDestroyOnLoad(gameObject);
        if (_canvas == null) BuildOverlay();
    }

    private void BuildOverlay()
    {
        _canvas = gameObject.GetComponent<Canvas>();
        if (_canvas == null) _canvas = gameObject.AddComponent<Canvas>();
        _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _canvas.sortingOrder = 9999; // 永远在最上层

        if (gameObject.GetComponent<CanvasScaler>() == null)
        {
            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
        }
        if (gameObject.GetComponent<GraphicRaycaster>() == null)
            gameObject.AddComponent<GraphicRaycaster>();

        var imgGO = new GameObject("Overlay", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(CanvasGroup));
        imgGO.transform.SetParent(transform, false);
        var rt = imgGO.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        _overlayImage = imgGO.GetComponent<Image>();
        _overlayImage.color = overlayColor;
        _group = imgGO.GetComponent<CanvasGroup>();
        _group.alpha = 0f;
        _group.blocksRaycasts = false;
        _group.interactable = false;
    }

    /// <summary>
    /// 执行过渡：当前屏幕淡入到黑色 -> 加载场景 -> 淡出回透明。
    /// </summary>
    public void LoadSceneWithFade(string sceneName, float fadeOutIn = -1f)
    {
        if (_busy) return;
        float d = fadeOutIn > 0f ? fadeOutIn : defaultFadeDuration;
        StartCoroutine(Transition(sceneName, d));
    }

    private IEnumerator Transition(string sceneName, float d)
    {
        _busy = true;
        _group.blocksRaycasts = true;
        yield return Fade(0f, 1f, d);

        var op = SceneManager.LoadSceneAsync(sceneName);
        if (op != null)
            while (!op.isDone) yield return null;

        yield return Fade(1f, 0f, d);
        _group.blocksRaycasts = false;
        _busy = false;
    }

    private IEnumerator Fade(float from, float to, float duration)
    {
        if (duration <= 0f) { _group.alpha = to; yield break; }
        float t = 0f;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            _group.alpha = Mathf.Lerp(from, to, Mathf.Clamp01(t / duration));
            yield return null;
        }
        _group.alpha = to;
    }
}
