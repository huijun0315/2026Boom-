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
            var dragTileType = Type.GetType("LevelTileDraggable, Assembly-CSharp");
            var reorderCtrlType = Type.GetType("LevelSelectReorderController, Assembly-CSharp");
            if (menuBtnType == null) return "ERROR: MenuButton not found";
            if (levelBtnType == null) return "ERROR: LevelButton not found";
            if (dragTileType == null) return "ERROR: LevelTileDraggable not found";
            if (reorderCtrlType == null) return "ERROR: LevelSelectReorderController not found";

            var orderedIds = new List<string>(LevelStore.LoadOrderedIds());
            if (orderedIds.Count == 0)
                orderedIds.Add("level_1");

            int levelCount = orderedIds.Count;

            // 动态计算网格布局
            int cols = 5;
            int rows = Mathf.CeilToInt((float)levelCount / cols);
            // 根据行数缩小按钮
            float cellW, cellH, spacingX, spacingY;
            if (rows <= 2)
            {
                cellW = 240f; cellH = 240f; spacingX = 40f; spacingY = 60f;
            }
            else if (rows <= 3)
            {
                cellW = 200f; cellH = 200f; spacingX = 32f; spacingY = 40f;
            }
            else
            {
                cellW = 160f; cellH = 160f; spacingX = 24f; spacingY = 28f;
            }

            float totalW = cols * cellW + (cols - 1) * spacingX;
            float totalH = rows * cellH + (rows - 1) * spacingY;

            float gridCenterY = -60f;
            float startX = -totalW / 2f + cellW / 2f;
            float startY = gridCenterY + totalH / 2f - cellH / 2f;

            for (int i = 0; i < levelCount; i++)
            {
                int row = i / cols;
                int col = i % cols;
                float x = startX + col * (cellW + spacingX);
                float y = startY - row * (cellH + spacingY);

                string levelId = orderedIds[i];
                string prerequisiteLevelId = i > 0 ? orderedIds[i - 1] : "";
                var data = LevelStore.Load(levelId);
                string displayName = (data != null && !string.IsNullOrEmpty(data.displayName)) ? data.displayName : ("关卡 " + (i + 1));
                bool unlocked = (i < levelCount - 1); // 最后一个未解锁
                BuildLevelTile(canvasGO.transform, i + 1, levelId, displayName, new Vector2(x, y), new Vector2(cellW, cellH),
                               unlocked, prerequisiteLevelId, font, menuBtnType, levelBtnType, dragTileType);
            }

            var orderBtn = CreateButton(canvasGO.transform, "OrderModeBtn", "排序模式", font,
                new Color(0.72f, 0.52f, 0.24f),
                anchorMin: new Vector2(1f, 1f), anchorMax: new Vector2(1f, 1f), pivot: new Vector2(1f, 1f),
                anchored: new Vector2(-20f, -20f), size: new Vector2(160f, 56f));

            var saveBtn = CreateButton(canvasGO.transform, "OrderSaveBtn", "保存顺序", font,
                new Color(0.22f, 0.62f, 0.32f),
                anchorMin: new Vector2(1f, 1f), anchorMax: new Vector2(1f, 1f), pivot: new Vector2(1f, 1f),
                anchored: new Vector2(-20f, -86f), size: new Vector2(160f, 56f));

            var cancelBtn = CreateButton(canvasGO.transform, "OrderCancelBtn", "取消", font,
                new Color(0.45f, 0.45f, 0.50f),
                anchorMin: new Vector2(1f, 1f), anchorMax: new Vector2(1f, 1f), pivot: new Vector2(1f, 1f),
                anchored: new Vector2(-20f, -152f), size: new Vector2(160f, 56f));

            var orderStatus = CreateText(canvasGO.transform, "OrderStatus", "",
                anchorMin: new Vector2(1f, 1f), anchorMax: new Vector2(1f, 1f), pivot: new Vector2(1f, 1f),
                anchored: new Vector2(-20f, -218f), size: new Vector2(320f, 80f),
                font: font, fontSize: 20, color: new Color(1f, 1f, 1f, 0.9f), align: TextAnchor.UpperRight);

            var reorderGO = new GameObject("LevelOrderEditor");
            var reorderComp = reorderGO.AddComponent(reorderCtrlType);
            SetField(reorderCtrlType, reorderComp, "tileRoot", canvasGO.GetComponent<RectTransform>());
            SetField(reorderCtrlType, reorderComp, "editModeButton", orderBtn);
            SetField(reorderCtrlType, reorderComp, "saveOrderButton", saveBtn);
            SetField(reorderCtrlType, reorderComp, "cancelButton", cancelBtn);
            SetField(reorderCtrlType, reorderComp, "statusText", orderStatus);

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

    static void BuildLevelTile(Transform parent, int idx, string levelId, string displayName, Vector2 pos, Vector2 size,
        bool unlocked, string prerequisiteLevelId, Font font, Type menuBtnType, Type levelBtnType, Type dragTileType)
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
        if (dragTileType != null) tile.AddComponent(dragTileType);

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
        numText.fontSize = Mathf.RoundToInt(96 * (size.x / 240f));
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
        nameText.text = displayName;
        nameText.alignment = TextAnchor.MiddleCenter;
        nameText.font = font;
        nameText.fontSize = Mathf.RoundToInt(30 * (size.x / 240f));
        nameText.color = Color.white;
        nameText.raycastTarget = false;

        // Stars container top-right (一颗星，使用 StarGraphic 自绘，不依赖字体)
        var starsGO = new GameObject("Stars", typeof(RectTransform));
        starsGO.transform.SetParent(tile.transform, false);
        var starsRT = starsGO.GetComponent<RectTransform>();
        starsRT.anchorMin = new Vector2(1f, 1f);
        starsRT.anchorMax = new Vector2(1f, 1f);
        starsRT.pivot = new Vector2(1f, 1f);
        float starSize = Mathf.RoundToInt(70 * (size.x / 240f));
        starsRT.anchoredPosition = new Vector2(-12, -12);
        starsRT.sizeDelta = new Vector2(starSize, starSize);

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
        SetField(t, levelComp, "levelName", displayName);
        SetField(t, levelComp, "levelId", levelId);
        SetField(t, levelComp, "prerequisiteLevelId", prerequisiteLevelId);
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

    static Button CreateButton(Transform parent, string name, string label, Font font, Color color,
        Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 anchored, Vector2 size)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.pivot = pivot;
        rt.anchoredPosition = anchored;
        rt.sizeDelta = size;

        var img = go.GetComponent<Image>();
        img.color = color;
        var btn = go.GetComponent<Button>();
        btn.targetGraphic = img;

        var txtGO = new GameObject("Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        txtGO.transform.SetParent(go.transform, false);
        var txtRT = txtGO.GetComponent<RectTransform>();
        txtRT.anchorMin = Vector2.zero;
        txtRT.anchorMax = Vector2.one;
        txtRT.offsetMin = Vector2.zero;
        txtRT.offsetMax = Vector2.zero;

        var txt = txtGO.GetComponent<Text>();
        txt.text = label;
        txt.font = font;
        txt.fontSize = 28;
        txt.alignment = TextAnchor.MiddleCenter;
        txt.color = Color.white;
        txt.raycastTarget = false;
        return btn;
    }

    static Text CreateText(Transform parent, string name, string text,
        Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 anchored, Vector2 size,
        Font font, int fontSize, Color color, TextAnchor align)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.pivot = pivot;
        rt.anchoredPosition = anchored;
        rt.sizeDelta = size;

        var t = go.GetComponent<Text>();
        t.text = text;
        t.font = font;
        t.fontSize = fontSize;
        t.color = color;
        t.alignment = align;
        t.raycastTarget = false;
        return t;
    }
}
