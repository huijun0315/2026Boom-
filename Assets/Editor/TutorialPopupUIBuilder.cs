using System;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;
using UnityEditor;
using UnityEditor.SceneManagement;

/// <summary>
/// 在当前场景中生成引导弹窗 UI + TutorialPopupManager。
/// 菜单：Tools → Create Tutorial Popup UI
/// </summary>
public static class TutorialPopupUIBuilder
{
    [MenuItem("Tools/Create Tutorial Popup UI")]
    public static void BuildMenu()
    {
        Debug.Log(Build());
    }

    public static string Build()
    {
        try
        {
            // 找到或创建 Canvas
            var existingCanvas = UnityEngine.Object.FindObjectOfType<Canvas>();
            GameObject canvasGO;
            Canvas canvas;
            if (existingCanvas != null && existingCanvas.renderMode == RenderMode.ScreenSpaceOverlay)
            {
                canvasGO = existingCanvas.gameObject;
                canvas = existingCanvas;
            }
            else
            {
                canvasGO = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
                canvas = canvasGO.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                var scaler = canvasGO.GetComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920, 1080);
                scaler.matchWidthOrHeight = 0.5f;
            }

            Font font = null;
            try { font = (Font)Resources.GetBuiltinResource(typeof(Font), "LegacyRuntime.ttf"); } catch { }

            // ----- Root overlay (full screen, dark) -----
            var rootGO = new GameObject("TutorialPopupUI", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(CanvasGroup));
            rootGO.transform.SetParent(canvasGO.transform, false);
            var rootRt = rootGO.GetComponent<RectTransform>();
            rootRt.anchorMin = Vector2.zero;
            rootRt.anchorMax = Vector2.one;
            rootRt.offsetMin = Vector2.zero;
            rootRt.offsetMax = Vector2.zero;
            var rootGroup = rootGO.GetComponent<CanvasGroup>();
            rootGroup.alpha = 0f;
            rootGroup.interactable = false;
            rootGroup.blocksRaycasts = false;
            var bgOverlay = rootGO.GetComponent<Image>();
            bgOverlay.color = new Color(0f, 0f, 0f, 0.65f);
            bgOverlay.raycastTarget = true;

            // ----- Center panel -----
            var panelGO = new GameObject("Panel", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            panelGO.transform.SetParent(rootGO.transform, false);
            var panelRt = panelGO.GetComponent<RectTransform>();
            panelRt.anchorMin = new Vector2(0.5f, 0.5f);
            panelRt.anchorMax = new Vector2(0.5f, 0.5f);
            panelRt.pivot = new Vector2(0.5f, 0.5f);
            panelRt.anchoredPosition = Vector2.zero;
            panelRt.sizeDelta = new Vector2(800f, 600f);
            var panelBg = panelGO.GetComponent<Image>();
            panelBg.color = new Color(0.12f, 0.13f, 0.17f, 0.95f);

            // ----- Title -----
            var titleGO = new GameObject("Title", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            titleGO.transform.SetParent(panelGO.transform, false);
            var titleRt = titleGO.GetComponent<RectTransform>();
            titleRt.anchorMin = new Vector2(0f, 1f);
            titleRt.anchorMax = new Vector2(1f, 1f);
            titleRt.pivot = new Vector2(0.5f, 1f);
            titleRt.anchoredPosition = new Vector2(0, -24);
            titleRt.sizeDelta = new Vector2(-48, 52);
            var titleText = titleGO.GetComponent<Text>();
            titleText.text = "引导标题";
            titleText.font = font;
            titleText.fontSize = 32;
            titleText.color = Color.white;
            titleText.alignment = TextAnchor.MiddleCenter;
            titleText.raycastTarget = false;

            // ----- Body text -----
            var bodyGO = new GameObject("BodyText", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            bodyGO.transform.SetParent(panelGO.transform, false);
            var bodyRt = bodyGO.GetComponent<RectTransform>();
            bodyRt.anchorMin = new Vector2(0f, 0f);
            bodyRt.anchorMax = new Vector2(1f, 0.5f);
            bodyRt.pivot = new Vector2(0.5f, 0.5f);
            bodyRt.offsetMin = new Vector2(24, 10);
            bodyRt.offsetMax = new Vector2(-24, -10);
            var bodyText = bodyGO.GetComponent<Text>();
            bodyText.text = "这里是引导正文内容。";
            bodyText.font = font;
            bodyText.fontSize = 22;
            bodyText.color = new Color(1f, 1f, 1f, 0.9f);
            bodyText.alignment = TextAnchor.UpperLeft;
            bodyText.horizontalOverflow = HorizontalWrapMode.Wrap;
            bodyText.verticalOverflow = VerticalWrapMode.Overflow;
            bodyText.raycastTarget = false;

            // ----- Video section (upper half of panel) -----
            var videoSectionGO = new GameObject("VideoSection", typeof(RectTransform));
            videoSectionGO.transform.SetParent(panelGO.transform, false);
            var videoRt = videoSectionGO.GetComponent<RectTransform>();
            videoRt.anchorMin = new Vector2(0f, 0.5f);
            videoRt.anchorMax = new Vector2(1f, 1f);
            videoRt.pivot = new Vector2(0.5f, 0.5f);
            videoRt.offsetMin = new Vector2(24, -52);
            videoRt.offsetMax = new Vector2(-24, -80);
            videoSectionGO.SetActive(false);

            // Video Image (RenderTexture target)
            var videoImgGO = new GameObject("VideoImage", typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage));
            videoImgGO.transform.SetParent(videoSectionGO.transform, false);
            var videoImgRt = videoImgGO.GetComponent<RectTransform>();
            videoImgRt.anchorMin = Vector2.zero;
            videoImgRt.anchorMax = Vector2.one;
            videoImgRt.offsetMin = Vector2.zero;
            videoImgRt.offsetMax = Vector2.zero;
            var videoImage = videoImgGO.GetComponent<RawImage>();
            videoImage.color = Color.black;
            videoImage.raycastTarget = false;

            // VideoPlayer + AudioSource
            var videoPlayer = videoSectionGO.AddComponent<VideoPlayer>();
            videoPlayer.playOnAwake = false;
            videoPlayer.waitForFirstFrame = true;
            videoPlayer.skipOnDrop = true;
            videoPlayer.renderMode = VideoRenderMode.APIOnly;
            videoPlayer.isLooping = false;
            var videoAudioSource = videoSectionGO.AddComponent<AudioSource>();
            videoPlayer.audioOutputMode = VideoAudioOutputMode.AudioSource;
            videoPlayer.SetTargetAudioSource(0, videoAudioSource);

            // ----- Close button (bottom center) -----
            var closeGO = new GameObject("CloseButton", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            closeGO.transform.SetParent(panelGO.transform, false);
            var closeRt = closeGO.GetComponent<RectTransform>();
            closeRt.anchorMin = new Vector2(0.5f, 0f);
            closeRt.anchorMax = new Vector2(0.5f, 0f);
            closeRt.pivot = new Vector2(0.5f, 0f);
            closeRt.anchoredPosition = new Vector2(0, 20);
            closeRt.sizeDelta = new Vector2(200, 52);
            var closeImg = closeGO.GetComponent<Image>();
            closeImg.color = new Color(0.22f, 0.55f, 0.85f);
            var closeBtn = closeGO.GetComponent<Button>();
            closeBtn.targetGraphic = closeImg;
            closeBtn.transition = Selectable.Transition.ColorTint;

            var closeLblGO = new GameObject("Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            closeLblGO.transform.SetParent(closeGO.transform, false);
            var closeLblRt = closeLblGO.GetComponent<RectTransform>();
            closeLblRt.anchorMin = Vector2.zero;
            closeLblRt.anchorMax = Vector2.one;
            closeLblRt.offsetMin = Vector2.zero;
            closeLblRt.offsetMax = Vector2.zero;
            var closeLbl = closeLblGO.GetComponent<Text>();
            closeLbl.text = "我知道了";
            closeLbl.font = font;
            closeLbl.fontSize = 24;
            closeLbl.color = Color.white;
            closeLbl.alignment = TextAnchor.MiddleCenter;
            closeLbl.raycastTarget = false;

            // ----- Attach TutorialPopupUI -----
            var uiType = Type.GetType("TutorialPopupUI, Assembly-CSharp");
            if (uiType == null)
                return "ERROR: TutorialPopupUI script not compiled. Open it first and let Unity compile.";

            var popupUI = rootGO.AddComponent(uiType) as MonoBehaviour;
            SetField(uiType, popupUI, "rootGroup", rootGroup);
            SetField(uiType, popupUI, "bgOverlay", bgOverlay);
            SetField(uiType, popupUI, "panelBg", panelBg);
            SetField(uiType, popupUI, "titleText", titleText);
            SetField(uiType, popupUI, "bodyTextObj", bodyText);
            SetField(uiType, popupUI, "videoSection", videoSectionGO);
            SetField(uiType, popupUI, "videoImage", videoImage);
            SetField(uiType, popupUI, "videoPlayer", videoPlayer);
            SetField(uiType, popupUI, "videoAudioSource", videoAudioSource);
            SetField(uiType, popupUI, "closeButton", closeBtn);

            // ----- Create TutorialPopupManager (DontDestroyOnLoad) -----
            var mgrType = Type.GetType("TutorialPopupManager, Assembly-CSharp");
            if (mgrType == null)
                return "ERROR: TutorialPopupManager script not compiled.";

            // Check if one already exists
            var existingMgr = UnityEngine.Object.FindObjectOfType<TutorialPopupManager>();
            if (existingMgr == null)
            {
                var mgrGO = new GameObject("TutorialPopupManager");
                var mgr = mgrGO.AddComponent(mgrType) as MonoBehaviour;
                SetField(mgrType, mgr, "popupUI", popupUI as TutorialPopupUI);
            }

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            return "OK: TutorialPopupUI + TutorialPopupManager created in current scene.";
        }
        catch (Exception ex)
        {
            return "EXCEPTION: " + ex.GetType().Name + ": " + ex.Message + "\n" + ex.StackTrace;
        }
    }

    static void SetField(Type t, object target, string name, object value)
    {
        var f = t.GetField(name, BindingFlags.Public | BindingFlags.Instance);
        if (f != null) { f.SetValue(target, value); return; }
        var p = t.GetProperty(name, BindingFlags.Public | BindingFlags.Instance);
        if (p != null) p.SetValue(target, value, null);
    }
}
