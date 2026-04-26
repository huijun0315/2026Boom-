using System;
using System.IO;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;

public static class StartSceneBuilder
{
    [MenuItem("Tools/Create Start Scene")]
    public static void BuildMenu()
    {
        Debug.Log(Build());
    }

    public static string Build()
    {
        try
        {
            if (!Directory.Exists("Assets/Scenes"))
                Directory.CreateDirectory("Assets/Scenes");

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var camGO = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
            camGO.tag = "MainCamera";
            var cam = camGO.GetComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.10f, 0.10f, 0.12f);

            new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));

            // BGM 播放器（带插槽）：在 Start 时自动播放，跨场景不打断
            var bgmType = Type.GetType("BGMPlayer, Assembly-CSharp");
            if (bgmType == null)
                return "ERROR: BGMPlayer type not found.";
            var bgmGO = new GameObject("BGM", typeof(AudioSource));
            bgmGO.AddComponent(bgmType);

            var canvasGO = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = canvasGO.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGO.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;

            Font font = null;
            try { font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); } catch { }
            if (font == null) { try { font = Resources.GetBuiltinResource<Font>("Arial.ttf"); } catch { } }

            // Title
            var titleGO = new GameObject("Title", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            titleGO.transform.SetParent(canvasGO.transform, false);
            var titleRT = titleGO.GetComponent<RectTransform>();
            titleRT.anchorMin = new Vector2(0.5f, 0.5f);
            titleRT.anchorMax = new Vector2(0.5f, 0.5f);
            titleRT.pivot = new Vector2(0.5f, 0.5f);
            titleRT.anchoredPosition = new Vector2(0, 320);
            titleRT.sizeDelta = new Vector2(1000, 140);
            var titleText = titleGO.GetComponent<Text>();
            titleText.text = "游戏标题";
            titleText.alignment = TextAnchor.MiddleCenter;
            titleText.font = font;
            titleText.fontSize = 80;
            titleText.color = Color.white;

            // Controller (only needed for ExitGame wiring; StartGame no longer navigates)
            var controllerType = Type.GetType("StartMenuController, Assembly-CSharp");
            if (controllerType == null)
                return "ERROR: StartMenuController type not found.";

            var menuBtnType = Type.GetType("MenuButton, Assembly-CSharp");
            if (menuBtnType == null)
                return "ERROR: MenuButton type not found.";

            var achievementBtnType = Type.GetType("AchievementButton, Assembly-CSharp");
            if (achievementBtnType == null)
                return "ERROR: AchievementButton type not found.";

            var controllerGO = new GameObject("MenuController");
            var controller = controllerGO.AddComponent(controllerType) as MonoBehaviour;

            // 4 buttons, stacked vertically
            float yStart = 120f;
            float yStep = 150f;
            var startBtn       = MakeButton(canvasGO.transform, "开始游戏",   new Vector2(0, yStart - 0 * yStep), new Color(0.25f, 0.55f, 0.35f), font, menuBtnType);
            var devBtn         = MakeButton(canvasGO.transform, "开发人员",   new Vector2(0, yStart - 1 * yStep), new Color(0.30f, 0.45f, 0.70f), font, menuBtnType);
            var achievementBtn = MakeButton(canvasGO.transform, "成就藏馆",   new Vector2(0, yStart - 2 * yStep), new Color(0.45f, 0.45f, 0.45f), font, menuBtnType);
            var exitBtn        = MakeButton(canvasGO.transform, "结束游戏",   new Vector2(0, yStart - 3 * yStep), new Color(0.70f, 0.25f, 0.25f), font, menuBtnType);

            // Achievement button gets extra AchievementButton component; default inactive (gray)
            var achComp = achievementBtn.AddComponent(achievementBtnType) as MonoBehaviour;
            // With isActive=false default, ApplyState() will force gray color.
            // Also disable UI Button color transition for achievement so it doesn't fight AchievementButton.
            var achUIButton = achievementBtn.GetComponent<Button>();
            achUIButton.transition = Selectable.Transition.None;

            // Wire 结束游戏 -> ExitGame, 开始游戏 -> StartGame, 成就藏馆 -> OpenAchievementMuseum
            var exitAction = (UnityAction)Delegate.CreateDelegate(typeof(UnityAction), controller, "ExitGame");
            UnityEventTools.AddPersistentListener(exitBtn.GetComponent<Button>().onClick, exitAction);

            var startAction = (UnityAction)Delegate.CreateDelegate(typeof(UnityAction), controller, "StartGame");
            UnityEventTools.AddPersistentListener(startBtn.GetComponent<Button>().onClick, startAction);

            var achAction = (UnityAction)Delegate.CreateDelegate(typeof(UnityAction), controller, "OpenAchievementMuseum");
            UnityEventTools.AddPersistentListener(achievementBtn.GetComponent<Button>().onClick, achAction);

            // 成就按钮入口默认可点击
            var achSetActive = achComp.GetType().GetMethod("SetActive", new[] { typeof(bool) });
            if (achSetActive != null) achSetActive.Invoke(achComp, new object[] { true });

            var scenePath = "Assets/Scenes/StartScene.unity";
            EditorSceneManager.SaveScene(scene, scenePath);

            var levelSelectScene = "Assets/Scenes/LevelSelectScene.unity";
            var achievementScene = "Assets/Scenes/AchievementScene.unity";
            var sampleScene = "Assets/Scenes/SampleScene.unity";
            var scenes = new List<EditorBuildSettingsScene>();
            scenes.Add(new EditorBuildSettingsScene(scenePath, true));
            if (File.Exists(levelSelectScene))
                scenes.Add(new EditorBuildSettingsScene(levelSelectScene, true));
            if (File.Exists(achievementScene))
                scenes.Add(new EditorBuildSettingsScene(achievementScene, true));
            if (File.Exists(sampleScene))
                scenes.Add(new EditorBuildSettingsScene(sampleScene, true));
            EditorBuildSettings.scenes = scenes.ToArray();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            return "OK: StartScene at " + scenePath + ", buildScenesCount=" + scenes.Count;
        }
        catch (Exception ex)
        {
            return "EXCEPTION: " + ex.GetType().Name + ": " + ex.Message + "\n" + ex.StackTrace;
        }
    }

    static GameObject MakeButton(Transform parent, string label, Vector2 pos, Color bg, Font font, Type menuBtnType)
    {
        var bgo = new GameObject(label, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        bgo.transform.SetParent(parent, false);
        var rt = bgo.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = new Vector2(460, 120);
        bgo.GetComponent<Image>().color = bg;

        // Remove default ColorTint transition to avoid flicker with our custom animation
        bgo.GetComponent<Button>().transition = Selectable.Transition.None;

        // Add MenuButton behavior
        bgo.AddComponent(menuBtnType);

        // Add AudioSource now so inspector shows the slot path nicely; MenuButton will use it
        if (bgo.GetComponent<AudioSource>() == null)
        {
            var src = bgo.AddComponent<AudioSource>();
            src.playOnAwake = false;
        }

        var tgo = new GameObject("Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        tgo.transform.SetParent(bgo.transform, false);
        var trt = tgo.GetComponent<RectTransform>();
        trt.anchorMin = Vector2.zero;
        trt.anchorMax = Vector2.one;
        trt.offsetMin = Vector2.zero;
        trt.offsetMax = Vector2.zero;
        var txt = tgo.GetComponent<Text>();
        txt.text = label;
        txt.alignment = TextAnchor.MiddleCenter;
        txt.font = font;
        txt.fontSize = 54;
        txt.color = Color.white;
        return bgo;
    }
}
