using System;
using System.IO;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEditor;
using UnityEditor.SceneManagement;

public static class LevelSelectBuilder
{
    [MenuItem("Tools/Create Level Select Scene")]
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
            cam.backgroundColor = new Color(0.08f, 0.09f, 0.13f);

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
            var titleGO = new GameObject("Title", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            titleGO.transform.SetParent(canvasGO.transform, false);
            var titleRT = titleGO.GetComponent<RectTransform>();
            titleRT.anchorMin = new Vector2(0.5f, 1f);
            titleRT.anchorMax = new Vector2(0.5f, 1f);
            titleRT.pivot = new Vector2(0.5f, 1f);
            titleRT.anchoredPosition = new Vector2(0, -40);
            titleRT.sizeDelta = new Vector2(1000, 120);
            var titleText = titleGO.GetComponent<Text>();
            titleText.text = "关卡选择";
            titleText.alignment = TextAnchor.MiddleCenter;
            titleText.font = font;
            titleText.fontSize = 72;
            titleText.color = Color.white;

            var menuBtnType = Type.GetType("MenuButton, Assembly-CSharp");
            var levelBtnType = Type.GetType("LevelButton, Assembly-CSharp");
            if (menuBtnType == null) return "ERROR: MenuButton not found";
            if (levelBtnType == null) return "ERROR: LevelButton not found";

            // Grid: 5 cols x 2 rows
            int cols = 5;
            int rows = 2;
            float cellW = 240f;
            float cellH = 240f;
            float spacingX = 40f;
            float spacingY = 60f;

            float totalW = cols * cellW + (cols - 1) * spacingX;
            float totalH = rows * cellH + (rows - 1) * spacingY;

            // Offset: center of grid at (0, -40) ish
            float gridCenterY = -60f;
            float startX = -totalW / 2f + cellW / 2f;
            float startY = gridCenterY + totalH / 2f - cellH / 2f;

            for (int i = 0; i < 10; i++)
            {
                int row = i / cols;
                int col = i % cols;
                float x = startX + col * (cellW + spacingX);
                float y = startY - row * (cellH + spacingY);

                bool unlocked = (i < 9); // 前九个解锁，第十个未解锁
                BuildLevelTile(canvasGO.transform, i + 1, new Vector2(x, y), new Vector2(cellW, cellH),
                               unlocked, font, menuBtnType, levelBtnType);
            }

            var scenePath = "Assets/Scenes/LevelSelectScene.unity";
            EditorSceneManager.SaveScene(scene, scenePath);

            // Ensure scene is in Build Settings
            var existing = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            bool present = false;
            foreach (var s in existing) if (s.path == scenePath) { present = true; break; }
            if (!present)
            {
                existing.Add(new EditorBuildSettingsScene(scenePath, true));
                EditorBuildSettings.scenes = existing.ToArray();
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            return "OK: LevelSelectScene at " + scenePath;
        }
        catch (Exception ex)
        {
            return "EXCEPTION: " + ex.GetType().Name + ": " + ex.Message + "\n" + ex.StackTrace;
        }
    }

    static void BuildLevelTile(Transform parent, int idx, Vector2 pos, Vector2 size,
        bool unlocked, Font font, Type menuBtnType, Type levelBtnType)
    {
        // Root tile: Image + Button + CanvasGroup + LevelButton + MenuButton + AudioSource
        var tile = new GameObject("Level_" + idx,
            typeof(RectTransform), typeof(CanvasRenderer),
            typeof(Image), typeof(Button));
        tile.transform.SetParent(parent, false);

        var rt = tile.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;

        tile.GetComponent<Button>().transition = Selectable.Transition.None;

        // MenuButton (hover/click/sound)
        tile.AddComponent(menuBtnType);
        if (tile.GetComponent<AudioSource>() == null)
        {
            var src = tile.AddComponent<AudioSource>();
            src.playOnAwake = false;
        }

        // LevelButton (lock + stars)
        var levelComp = tile.AddComponent(levelBtnType) as MonoBehaviour;

        // Big number label in center
        var numGO = new GameObject("Number", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        numGO.transform.SetParent(tile.transform, false);
        var numRT = numGO.GetComponent<RectTransform>();
        numRT.anchorMin = Vector2.zero;
        numRT.anchorMax = Vector2.one;
        numRT.offsetMin = Vector2.zero;
        numRT.offsetMax = Vector2.zero;
        var numText = numGO.GetComponent<Text>();
        numText.text = unlocked ? idx.ToString() : "?";
        numText.alignment = TextAnchor.MiddleCenter;
        numText.font = font;
        numText.fontSize = 96;
        numText.color = Color.white;
        numText.raycastTarget = false;

        // Name text below button
        var nameGO = new GameObject("Name", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        nameGO.transform.SetParent(tile.transform, false);
        var nameRT = nameGO.GetComponent<RectTransform>();
        nameRT.anchorMin = new Vector2(0f, 0f);
        nameRT.anchorMax = new Vector2(1f, 0f);
        nameRT.pivot = new Vector2(0.5f, 1f);
        nameRT.anchoredPosition = new Vector2(0, -8);
        nameRT.sizeDelta = new Vector2(0, 44);
        var nameText = nameGO.GetComponent<Text>();
        nameText.text = "关卡 " + idx;
        nameText.alignment = TextAnchor.MiddleCenter;
        nameText.font = font;
        nameText.fontSize = 30;
        nameText.color = Color.white;
        nameText.raycastTarget = false;

        // Stars container top-right (一颗星，使用 StarGraphic 自绘，不依赖字体)
        var starsGO = new GameObject("Stars", typeof(RectTransform));
        starsGO.transform.SetParent(tile.transform, false);
        var starsRT = starsGO.GetComponent<RectTransform>();
        starsRT.anchorMin = new Vector2(1f, 1f);
        starsRT.anchorMax = new Vector2(1f, 1f);
        starsRT.pivot = new Vector2(1f, 1f);
        starsRT.anchoredPosition = new Vector2(-12, -12);
        starsRT.sizeDelta = new Vector2(70, 70);

        var starGfxType = Type.GetType("StarGraphic, Assembly-CSharp");
        if (starGfxType == null)
            throw new Exception("StarGraphic not found");

        var starGraphics = new Graphic[1];
        var starGO = new GameObject("Star_0", typeof(RectTransform), typeof(CanvasRenderer));
        starGO.transform.SetParent(starsGO.transform, false);
        var srt = starGO.GetComponent<RectTransform>();
        srt.anchorMin = Vector2.zero;
        srt.anchorMax = Vector2.one;
        srt.offsetMin = Vector2.zero;
        srt.offsetMax = Vector2.zero;
        var starGfx = starGO.AddComponent(starGfxType) as Graphic;
        starGfx.color = new Color(1f, 1f, 1f, 0.35f); // 默认未点亮：半透明白（对比更强）
        starGfx.raycastTarget = false;
        starGraphics[0] = starGfx;

        // Wire fields on LevelButton via reflection (to avoid compile-time dep here)
        var t = levelComp.GetType();
        SetField(t, levelComp, "levelIndex", idx);
        SetField(t, levelComp, "levelName", "关卡 " + idx);
        SetField(t, levelComp, "levelId", "level_" + idx);
        SetField(t, levelComp, "targetScene", "CubeScene");
        SetField(t, levelComp, "isUnlocked", unlocked);
        SetField(t, levelComp, "starsEarned", 0);
        SetField(t, levelComp, "buttonImage", tile.GetComponent<Image>());
        SetField(t, levelComp, "nameText", nameText);
        SetField(t, levelComp, "starGraphics", starGraphics);
    }

    static void SetField(Type t, object target, string name, object value)
    {
        var f = t.GetField(name, BindingFlags.Public | BindingFlags.Instance);
        if (f != null) f.SetValue(target, value);
    }
}
