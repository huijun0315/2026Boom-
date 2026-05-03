using System;
using System.IO;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEditor;
using UnityEditor.SceneManagement;

public static class AchievementSceneBuilder
{
    static int _skinCount = 5;
    static int[] _skinStars;

    [MenuItem("Tools/Create Achievement Scene")]
    public static void BuildMenu()
    {
        if (!AchievementConfigDialog.Show(_skinCount, _skinStars)) return; // 取消
        _skinCount = AchievementConfigDialog.resultSkinCount;
        _skinStars = AchievementConfigDialog.resultStars;
        Debug.Log(Build(_skinCount, _skinStars));
    }

    public static string Build(int skinCount = 5, int[] perSkinStars = null)
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
            cam.backgroundColor = new Color(0.09f, 0.10f, 0.13f);

            new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));

            // 场景 BGM 插槽
            var sceneBgmType = Type.GetType("SceneBGM, Assembly-CSharp");
            if (sceneBgmType == null) return "ERROR: SceneBGM not found.";
            var bgmGO = new GameObject("BGM");
            bgmGO.AddComponent(sceneBgmType);

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
            var title = MakeText(canvasGO.transform, "Title", "成就藏馆", font, 72, TextAnchor.MiddleCenter, Color.white);
            var tr = title.GetComponent<RectTransform>();
            tr.anchorMin = new Vector2(0.5f, 1f);
            tr.anchorMax = new Vector2(0.5f, 1f);
            tr.pivot = new Vector2(0.5f, 1f);
            tr.anchoredPosition = new Vector2(0, -36);
            tr.sizeDelta = new Vector2(860, 110);

            // Root split: left list + right preview
            var leftPanel = MakePanel(canvasGO.transform, "LeftPanel", new Color(0, 0, 0, 0.20f));
            var lrt = leftPanel.GetComponent<RectTransform>();
            lrt.anchorMin = new Vector2(0f, 0f);
            lrt.anchorMax = new Vector2(0f, 1f);
            lrt.pivot = new Vector2(0f, 0.5f);
            lrt.anchoredPosition = new Vector2(24, 0);
            lrt.sizeDelta = new Vector2(760, -140);
            lrt.offsetMin = new Vector2(0, 24);
            lrt.offsetMax = new Vector2(0, -120);

            var rightPanel = MakePanel(canvasGO.transform, "RightPanel", new Color(0, 0, 0, 0.20f));
            var rrt = rightPanel.GetComponent<RectTransform>();
            rrt.anchorMin = new Vector2(0.42f, 0f);
            rrt.anchorMax = new Vector2(1f, 1f);
            rrt.offsetMin = new Vector2(18, 24);
            rrt.offsetMax = new Vector2(-24, -120);

            // Bottom switch button
            var switchBtn = MakeButton(canvasGO.transform, "SwitchBtn", "切换", font, new Color(0.23f, 0.46f, 0.78f));
            var srt = switchBtn.GetComponent<RectTransform>();
            srt.anchorMin = new Vector2(0.5f, 0f);
            srt.anchorMax = new Vector2(0.5f, 0f);
            srt.pivot = new Vector2(0.5f, 0f);
            srt.anchoredPosition = new Vector2(0, 26);
            srt.sizeDelta = new Vector2(280, 82);

            // Back button (top-left)
            var backBtn = MakeButton(canvasGO.transform, "BackBtn", "返回", font, new Color(0.35f, 0.35f, 0.40f));
            var brt = backBtn.GetComponent<RectTransform>();
            brt.anchorMin = new Vector2(0f, 1f);
            brt.anchorMax = new Vector2(0f, 1f);
            brt.pivot = new Vector2(0f, 1f);
            brt.anchoredPosition = new Vector2(24, -24);
            brt.sizeDelta = new Vector2(180, 72);

            // Right preview image
            var previewGO = new GameObject("PreviewImage", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            previewGO.transform.SetParent(rightPanel.transform, false);
            var pr = previewGO.GetComponent<RectTransform>();
            pr.anchorMin = new Vector2(0.5f, 0.5f);
            pr.anchorMax = new Vector2(0.5f, 0.5f);
            pr.pivot = new Vector2(0.5f, 0.5f);
            pr.anchoredPosition = new Vector2(0, 0);
            pr.sizeDelta = new Vector2(640, 640);
            var previewImage = previewGO.GetComponent<Image>();
            previewImage.color = new Color(0.18f, 0.18f, 0.22f, 1f);
            previewImage.preserveAspect = true;

            var previewNum = MakeText(previewGO.transform, "PreviewNum", "1", font, 180, TextAnchor.MiddleCenter, new Color(1f, 1f, 1f, 0.8f));
            var pnrt = previewNum.GetComponent<RectTransform>();
            pnrt.anchorMin = Vector2.zero;
            pnrt.anchorMax = Vector2.one;
            pnrt.offsetMin = Vector2.zero;
            pnrt.offsetMax = Vector2.zero;

            var unlockText = MakeText(rightPanel.transform, "UnlockText", "还需解锁 0 个星星才能解锁", font, 30, TextAnchor.MiddleCenter, new Color(1f, 0.75f, 0.35f, 1f));
            var urt2 = unlockText.GetComponent<RectTransform>();
            urt2.anchorMin = new Vector2(0f, 0f);
            urt2.anchorMax = new Vector2(1f, 0f);
            urt2.pivot = new Vector2(0.5f, 0f);
            urt2.anchoredPosition = new Vector2(0, 18);
            urt2.sizeDelta = new Vector2(0, 56);

            // Left icon rows (skinCount)
            var iconButtons = new Button[skinCount];
            var iconImages = new Image[skinCount];
            var iconNums = new Text[skinCount];
            var iconLabels = new Text[skinCount];
            var iconConds = new Text[skinCount];

            float y0 = -70f;
            float rowH = (skinCount <= 5) ? 124f : Mathf.Max(60f, 620f / skinCount);
            for (int i = 0; i < skinCount; i++)
            {
                var row = MakePanel(leftPanel.transform, "Row_" + (i + 1), new Color(0, 0, 0, 0f));
                var rowRt = row.GetComponent<RectTransform>();
                rowRt.anchorMin = new Vector2(0f, 1f);
                rowRt.anchorMax = new Vector2(1f, 1f);
                rowRt.pivot = new Vector2(0.5f, 1f);
                rowRt.anchoredPosition = new Vector2(0, y0 - i * rowH);
                rowRt.sizeDelta = new Vector2(-24, 110);

                var iconBtn = MakeButton(row.transform, "IconBtn_" + (i + 1), "", font, new Color(0.30f, 0.32f, 0.38f));
                var ibRt = iconBtn.GetComponent<RectTransform>();
                ibRt.anchorMin = new Vector2(0f, 0.5f);
                ibRt.anchorMax = new Vector2(0f, 0.5f);
                ibRt.pivot = new Vector2(0f, 0.5f);
                ibRt.anchoredPosition = new Vector2(18, 0);
                ibRt.sizeDelta = new Vector2(92, 92);

                var iconImage = iconBtn.GetComponent<Image>();
                var iconNum = MakeText(iconBtn.transform, "Num", (i + 1).ToString(), font, 44, TextAnchor.MiddleCenter, Color.white);
                var inRt = iconNum.GetComponent<RectTransform>();
                inRt.anchorMin = Vector2.zero;
                inRt.anchorMax = Vector2.one;
                inRt.offsetMin = Vector2.zero;
                inRt.offsetMax = Vector2.zero;

                var label = MakeText(row.transform, "Label_" + (i + 1), "成就 " + (i + 1), font, 34, TextAnchor.MiddleLeft, Color.white);
                var lbRt = label.GetComponent<RectTransform>();
                lbRt.anchorMin = new Vector2(0f, 0.5f);
                lbRt.anchorMax = new Vector2(1f, 0.5f);
                lbRt.pivot = new Vector2(0f, 0.5f);
                lbRt.anchoredPosition = new Vector2(132, 18);
                lbRt.sizeDelta = new Vector2(-150, 52);

                var cond = MakeText(row.transform, "Condition_" + (i + 1), "还需解锁 0 个星星才能解锁", font, 22, TextAnchor.MiddleLeft, new Color(1f, 0.75f, 0.35f, 1f));
                var cRt = cond.GetComponent<RectTransform>();
                cRt.anchorMin = new Vector2(0f, 0.5f);
                cRt.anchorMax = new Vector2(1f, 0.5f);
                cRt.pivot = new Vector2(0f, 0.5f);
                cRt.anchoredPosition = new Vector2(132, -26);
                cRt.sizeDelta = new Vector2(-150, 40);

                iconButtons[i] = iconBtn.GetComponent<Button>();
                iconImages[i] = iconImage;
                iconNums[i] = iconNum.GetComponent<Text>();
                iconLabels[i] = label.GetComponent<Text>();
                iconConds[i] = cond.GetComponent<Text>();
            }

            // Controller
            var ctrlType = Type.GetType("AchievementPageController, Assembly-CSharp");
            if (ctrlType == null) return "ERROR: AchievementPageController not found.";
            var ctrlGO = new GameObject("AchievementPageController");
            var ctrl = ctrlGO.AddComponent(ctrlType) as MonoBehaviour;
            SetField(ctrlType, ctrl, "skinCount", skinCount);

            // 构建与 skinCount 匹配的默认数组
            var defaultLabels = new string[skinCount];
            var defaultSprites = new Sprite[skinCount];
            var defaultPreview = new Sprite[skinCount];
            var defaultStars = new int[skinCount];
            for (int i = 0; i < skinCount; i++)
            {
                defaultLabels[i] = "成就 " + (i + 1);
                defaultStars[i] = (perSkinStars != null && i < perSkinStars.Length)
                    ? perSkinStars[i]
                    : i * 2;
            }
            SetField(ctrlType, ctrl, "labels", defaultLabels);
            SetField(ctrlType, ctrl, "iconSprites", defaultSprites);
            SetField(ctrlType, ctrl, "previewSprites", defaultPreview);
            SetField(ctrlType, ctrl, "requiredStars", defaultStars);

            SetField(ctrlType, ctrl, "iconButtons", iconButtons);
            SetField(ctrlType, ctrl, "iconImages", iconImages);
            SetField(ctrlType, ctrl, "iconNumberTexts", iconNums);
            SetField(ctrlType, ctrl, "iconLabels", iconLabels);
            SetField(ctrlType, ctrl, "iconConditionTexts", iconConds);
            SetField(ctrlType, ctrl, "previewImage", previewImage);
            SetField(ctrlType, ctrl, "previewNumberText", previewNum.GetComponent<Text>());
            SetField(ctrlType, ctrl, "unlockConditionText", unlockText.GetComponent<Text>());
            SetField(ctrlType, ctrl, "switchButton", switchBtn.GetComponent<Button>());

            // Back button -> Back()
            var m = ctrlType.GetMethod("Back", BindingFlags.Public | BindingFlags.Instance);
            if (m != null)
            {
                var del = Delegate.CreateDelegate(typeof(UnityEngine.Events.UnityAction), ctrl, m);
                UnityEditor.Events.UnityEventTools.AddPersistentListener(backBtn.GetComponent<Button>().onClick, (UnityEngine.Events.UnityAction)del);
            }

            var scenePath = "Assets/Scenes/AchievementScene.unity";
            EditorSceneManager.SaveScene(scene, scenePath);

            // Ensure in Build Settings
            var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            bool present = false;
            foreach (var s in scenes) if (s.path == scenePath) { present = true; break; }
            if (!present)
            {
                scenes.Add(new EditorBuildSettingsScene(scenePath, true));
                EditorBuildSettings.scenes = scenes.ToArray();
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            return "OK: AchievementScene at " + scenePath;
        }
        catch (Exception ex)
        {
            return "EXCEPTION: " + ex.GetType().Name + ": " + ex.Message + "\n" + ex.StackTrace;
        }
    }

    static GameObject MakePanel(Transform parent, string name, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(parent, false);
        go.GetComponent<Image>().color = color;
        return go;
    }

    static GameObject MakeText(Transform parent, string name, string text, Font font, int size, TextAnchor align, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        go.transform.SetParent(parent, false);
        var t = go.GetComponent<Text>();
        t.text = text;
        t.font = font;
        t.fontSize = size;
        t.alignment = align;
        t.color = color;
        t.raycastTarget = false;
        return go;
    }

    static GameObject MakeButton(Transform parent, string name, string label, Font font, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        go.GetComponent<Image>().color = color;
        go.GetComponent<Button>().transition = Selectable.Transition.None;

        if (!string.IsNullOrEmpty(label))
        {
            var txt = MakeText(go.transform, "Text", label, font, 40, TextAnchor.MiddleCenter, Color.white);
            var rt = txt.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }
        return go;
    }

    static void SetField(Type t, object target, string name, object value)
    {
        var f = t.GetField(name, BindingFlags.Public | BindingFlags.Instance);
        if (f != null) f.SetValue(target, value);
    }
}
