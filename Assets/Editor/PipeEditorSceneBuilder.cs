using System;
using System.IO;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEditor;
using UnityEditor.SceneManagement;

public static class PipeEditorSceneBuilder
{
    [MenuItem("Tools/Create Pipe Editor Scene")]
    public static void BuildMenu() { Debug.Log(Build()); }

    public static string Build()
    {
        try
        {
            if (!Directory.Exists("Assets/Scenes")) Directory.CreateDirectory("Assets/Scenes");
            if (!Directory.Exists("Assets/Resources/Levels")) Directory.CreateDirectory("Assets/Resources/Levels");

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // ----- Camera -----
            var camGO = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
            camGO.tag = "MainCamera";
            camGO.transform.position = new Vector3(4.2f, 4.2f, -5.4f);
            camGO.transform.LookAt(Vector3.zero);
            var cam = camGO.GetComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.10f, 0.11f, 0.15f);
            cam.fieldOfView = 45f;

            // ----- Lights -----
            var keyGO = new GameObject("Key Light", typeof(Light));
            var kl = keyGO.GetComponent<Light>();
            kl.type = LightType.Directional;
            kl.intensity = 1.1f;
            keyGO.transform.rotation = Quaternion.Euler(45, -30, 0);

            var fillGO = new GameObject("Fill Light", typeof(Light));
            var fl = fillGO.GetComponent<Light>();
            fl.type = LightType.Directional;
            fl.intensity = 0.4f;
            fl.color = new Color(0.8f, 0.85f, 1f);
            fillGO.transform.rotation = Quaternion.Euler(30, 150, 0);

            new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));

            // ----- Cube + Puzzle -----
            var cubeType = Type.GetType("RubikCube, Assembly-CSharp");
            var puzzleType = Type.GetType("PipePuzzle, Assembly-CSharp");
            var editorType = Type.GetType("PipeEditor, Assembly-CSharp");
            if (cubeType == null || puzzleType == null || editorType == null)
                return "ERROR: scripts not compiled: RubikCube/PipePuzzle/PipeEditor";

            var cubeGO = new GameObject("RubikCube");
            var rc = cubeGO.AddComponent(cubeType) as MonoBehaviour;
            SetField(cubeType, rc, "cam", cam);
            SetField(cubeType, rc, "useModelPrefab", true);
            SetField(cubeType, rc, "cubeModelPrefab", AssetDatabase.LoadAssetAtPath<GameObject>("Assets/art/3D/27mofang.fbx"));
            SetField(cubeType, rc, "autoAddModelColliders", true);
            SetField(cubeType, rc, "allowLayerRotation", false);
            SetField(cubeType, rc, "allowWholeRotation", true);

            var pz = cubeGO.AddComponent(puzzleType) as MonoBehaviour;
            SetField(puzzleType, pz, "buildSampleIfEmpty", false);

            // ----- UI Canvas -----
            var canvasGO = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = canvasGO.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGO.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;

            Font font = null;
            try { font = (Font)Resources.GetBuiltinResource(typeof(Font), "LegacyRuntime.ttf"); } catch { }

            // --- Left palette panel ---
            var panel = CreatePanel(canvasGO.transform, "PalettePanel",
                anchorMin: new Vector2(0f, 0f), anchorMax: new Vector2(0f, 1f),
                pivot: new Vector2(0f, 0.5f), size: new Vector2(320f, 0f),
                offset: new Vector2(20f, 0f),
                bg: new Color(0f, 0f, 0f, 0.55f));
            panel.gameObject.AddComponent<CanvasGroup>();

            // Title
            CreateText(panel, "Title", "关卡编辑器",
                anchorMin: new Vector2(0, 1), anchorMax: new Vector2(1, 1), pivot: new Vector2(0.5f, 1f),
                anchored: new Vector2(0, -16), size: new Vector2(-20, 44),
                font: font, fontSize: 30, color: Color.white, align: TextAnchor.MiddleCenter);

            var hintText = CreateText(panel, "Hint",
                "左键：放置/循环朝向\n右键：删除\n空白处拖拽：整体旋转魔方",
                anchorMin: new Vector2(0, 1), anchorMax: new Vector2(1, 1), pivot: new Vector2(0.5f, 1f),
                anchored: new Vector2(0, -64), size: new Vector2(-20, 90),
                font: font, fontSize: 18, color: new Color(1, 1, 1, 0.8f), align: TextAnchor.UpperLeft);

            // Brush buttons
            string[] names = { "起点 Start", "终点 End", "直管 Straight", "弯管 Bend", "三通 Tee", "十字 Cross", "二层起点 Start2", "传送入口 PortalA", "传送出口 PortalB", "擦除 Erase" };
            Color[] cols = {
                new Color(0.20f, 0.80f, 0.35f),
                new Color(0.90f, 0.35f, 0.35f),
                new Color(0.30f, 0.55f, 0.85f),
                new Color(0.60f, 0.45f, 0.85f),
                new Color(0.85f, 0.60f, 0.20f),
                new Color(0.50f, 0.75f, 0.80f),
                new Color(0.40f, 0.90f, 0.55f),
                new Color(0.85f, 0.45f, 1.00f),
                new Color(0.65f, 0.30f, 0.85f),
                new Color(0.45f, 0.45f, 0.45f),
            };
            var brushButtons = new Button[names.Length];
            var brushBgs = new Image[names.Length];
            float y0 = -170f;
            float h = 52f, gap = 6f;
            for (int i = 0; i < names.Length; i++)
            {
                var bt = CreateButton(panel, "Brush_" + i, names[i], font, cols[i],
                    anchorMin: new Vector2(0, 1), anchorMax: new Vector2(1, 1), pivot: new Vector2(0.5f, 1f),
                    anchored: new Vector2(0, y0 + -(h + gap) * i), size: new Vector2(-20, h));
                brushButtons[i] = bt.btn;
                brushBgs[i] = bt.img;
            }

            // Divider / level ID field
            float yIO = y0 - (h + gap) * names.Length - 30f;
            CreateText(panel, "IdLabel", "关卡ID (文件名):",
                anchorMin: new Vector2(0, 1), anchorMax: new Vector2(1, 1), pivot: new Vector2(0.5f, 1f),
                anchored: new Vector2(0, yIO), size: new Vector2(-20, 28),
                font: font, fontSize: 18, color: Color.white, align: TextAnchor.MiddleLeft, paddingLeft: 14);

            var idField = CreateInputField(panel, "LevelIdField", "level_1", font,
                anchorMin: new Vector2(0, 1), anchorMax: new Vector2(1, 1), pivot: new Vector2(0.5f, 1f),
                anchored: new Vector2(0, yIO - 34f), size: new Vector2(-20, 40));

            CreateText(panel, "NameLabel", "关卡显示名:",
                anchorMin: new Vector2(0, 1), anchorMax: new Vector2(1, 1), pivot: new Vector2(0.5f, 1f),
                anchored: new Vector2(0, yIO - 82f), size: new Vector2(-20, 28),
                font: font, fontSize: 18, color: Color.white, align: TextAnchor.MiddleLeft, paddingLeft: 14);

            var nameField = CreateInputField(panel, "LevelNameField", "关卡 1", font,
                anchorMin: new Vector2(0, 1), anchorMax: new Vector2(1, 1), pivot: new Vector2(0.5f, 1f),
                anchored: new Vector2(0, yIO - 116f), size: new Vector2(-20, 40));

            CreateText(panel, "MoveLimitLabel", "步数限制 (0 = 不挑战):",
                anchorMin: new Vector2(0, 1), anchorMax: new Vector2(1, 1), pivot: new Vector2(0.5f, 1f),
                anchored: new Vector2(0, yIO - 164f), size: new Vector2(-20, 28),
                font: font, fontSize: 18, color: Color.white, align: TextAnchor.MiddleLeft, paddingLeft: 14);

            var limitField = CreateInputField(panel, "MoveLimitField", "0", font,
                anchorMin: new Vector2(0, 1), anchorMax: new Vector2(1, 1), pivot: new Vector2(0.5f, 1f),
                anchored: new Vector2(0, yIO - 198f), size: new Vector2(-20, 40));
            limitField.contentType = InputField.ContentType.IntegerNumber;

            var saveBtn = CreateButton(panel, "SaveBtn", "保存", font, new Color(0.22f, 0.60f, 0.30f),
                anchorMin: new Vector2(0, 1), anchorMax: new Vector2(0.5f, 1f), pivot: new Vector2(0f, 1f),
                anchored: new Vector2(10, yIO - 252f), size: new Vector2(-15, 52));

            var loadBtn = CreateButton(panel, "LoadBtn", "载入", font, new Color(0.24f, 0.45f, 0.78f),
                anchorMin: new Vector2(0.5f, 1f), anchorMax: new Vector2(1f, 1f), pivot: new Vector2(0f, 1f),
                anchored: new Vector2(5, yIO - 252f), size: new Vector2(-15, 52));

            var clearBtn = CreateButton(panel, "ClearBtn", "清空", font, new Color(0.60f, 0.30f, 0.30f),
                anchorMin: new Vector2(0, 1), anchorMax: new Vector2(1, 1), pivot: new Vector2(0.5f, 1f),
                anchored: new Vector2(0, yIO - 314f), size: new Vector2(-20, 52));

            var statusTxt = CreateText(panel, "Status", "",
                anchorMin: new Vector2(0, 0), anchorMax: new Vector2(1, 0), pivot: new Vector2(0.5f, 0f),
                anchored: new Vector2(0, 16), size: new Vector2(-20, 60),
                font: font, fontSize: 16, color: new Color(1, 1, 1, 0.85f), align: TextAnchor.LowerLeft, paddingLeft: 14);

            // Back-to-level-select button top-right
            var backBtn = CreateButton(canvasGO.transform, "BackBtn", "返回", font, new Color(0.3f, 0.3f, 0.35f),
                anchorMin: new Vector2(1, 1), anchorMax: new Vector2(1, 1), pivot: new Vector2(1, 1),
                anchored: new Vector2(-20, -20), size: new Vector2(140, 56));

            // Preview button below back button
            var previewBtn = CreateButton(canvasGO.transform, "PreviewBtn", "预览", font, new Color(0.20f, 0.55f, 0.80f),
                anchorMin: new Vector2(1, 1), anchorMax: new Vector2(1, 1), pivot: new Vector2(1, 1),
                anchored: new Vector2(-20, -86), size: new Vector2(140, 56));

            // Reset orientation button below preview
            var resetOrientBtn = CreateButton(canvasGO.transform, "ResetOrientBtn", "回正", font, new Color(0.25f, 0.55f, 0.40f),
                anchorMin: new Vector2(1, 1), anchorMax: new Vector2(1, 1), pivot: new Vector2(1, 1),
                anchored: new Vector2(-20, -152), size: new Vector2(140, 56));

            CreateText(canvasGO.transform, "InsertIndexLabel", "插入位次", 
                anchorMin: new Vector2(1, 1), anchorMax: new Vector2(1, 1), pivot: new Vector2(1, 1),
                anchored: new Vector2(-20, -222), size: new Vector2(140, 28),
                font: font, fontSize: 16, color: Color.white, align: TextAnchor.MiddleCenter);

            var insertIndexField = CreateInputField(canvasGO.transform, "InsertIndexField", "8", font,
                anchorMin: new Vector2(1, 1), anchorMax: new Vector2(1, 1), pivot: new Vector2(1, 1),
                anchored: new Vector2(-20, -252), size: new Vector2(140, 40));
            insertIndexField.contentType = InputField.ContentType.IntegerNumber;

            var insertBtn = CreateButton(canvasGO.transform, "InsertBtn", "插入顺序", font, new Color(0.72f, 0.50f, 0.20f),
                anchorMin: new Vector2(1, 1), anchorMax: new Vector2(1, 1), pivot: new Vector2(1, 1),
                anchored: new Vector2(-20, -302), size: new Vector2(140, 56));

            // ----- Attach PipeEditor -----
            var editorGO = new GameObject("PipeEditor");
            var pe = editorGO.AddComponent(editorType) as MonoBehaviour;
            SetField(editorType, pe, "cube", rc);
            SetField(editorType, pe, "puzzle", pz);
            SetField(editorType, pe, "cam", cam);
            SetField(editorType, pe, "brushButtons", brushButtons);
            SetField(editorType, pe, "brushButtonBgs", brushBgs);
            SetField(editorType, pe, "saveButton",  saveBtn.btn);
            SetField(editorType, pe, "loadButton",  loadBtn.btn);
            SetField(editorType, pe, "clearButton", clearBtn.btn);
            SetField(editorType, pe, "backButton",  backBtn.btn);
            SetField(editorType, pe, "levelIdField", idField);
            SetField(editorType, pe, "levelNameField", nameField);
            SetField(editorType, pe, "moveLimitField", limitField);
            SetField(editorType, pe, "insertIndexField", insertIndexField);
            SetField(editorType, pe, "insertButton", insertBtn.btn);
            SetField(editorType, pe, "statusText", statusTxt);
            SetField(editorType, pe, "previewButton", previewBtn.btn);
            SetField(editorType, pe, "resetOrientButton", resetOrientBtn.btn);
            SetField(editorType, pe, "palettePanel", panel.gameObject);
            SetField(editorType, pe, "hintText", hintText);

            // Save scene
            var scenePath = "Assets/Scenes/PipeEditorScene.unity";
            EditorSceneManager.SaveScene(scene, scenePath);

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
            return "OK: PipeEditorScene at " + scenePath;
        }
        catch (Exception ex)
        {
            return "EXCEPTION: " + ex.GetType().Name + ": " + ex.Message + "\n" + ex.StackTrace;
        }
    }

    // ----------------- UI helpers -----------------

    static Transform CreatePanel(Transform parent, string name,
        Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 size, Vector2 offset, Color bg)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.pivot = pivot;
        rt.anchoredPosition = offset;
        rt.sizeDelta = size;
        if (anchorMin.y == 0 && anchorMax.y == 1)
        {
            rt.offsetMin = new Vector2(offset.x, 20);
            rt.offsetMax = new Vector2(offset.x + size.x, -20);
        }
        go.GetComponent<Image>().color = bg;
        return go.transform;
    }

    static Text CreateText(Transform parent, string name, string text,
        Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 anchored, Vector2 size,
        Font font, int fontSize, Color color, TextAnchor align, float paddingLeft = 0f)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.pivot = pivot;
        rt.anchoredPosition = new Vector2(anchored.x + paddingLeft * 0.5f, anchored.y);
        rt.sizeDelta = size;
        var t = go.GetComponent<Text>();
        t.text = text;
        t.font = font;
        t.fontSize = fontSize;
        t.color = color;
        t.alignment = align;
        t.horizontalOverflow = HorizontalWrapMode.Wrap;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        t.raycastTarget = false;
        return t;
    }

    struct UiButton { public Button btn; public Image img; public Text label; }

    static UiButton CreateButton(Transform parent, string name, string label, Font font, Color col,
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
        img.color = col;
        var btn = go.GetComponent<Button>();
        btn.targetGraphic = img;
        btn.transition = Selectable.Transition.ColorTint;

        var tx = new GameObject("Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        tx.transform.SetParent(go.transform, false);
        var trt = tx.GetComponent<RectTransform>();
        trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one;
        trt.offsetMin = Vector2.zero; trt.offsetMax = Vector2.zero;
        var t = tx.GetComponent<Text>();
        t.text = label; t.font = font; t.fontSize = 22; t.color = Color.white;
        t.alignment = TextAnchor.MiddleCenter;
        t.raycastTarget = false;
        return new UiButton { btn = btn, img = img, label = t };
    }

    static InputField CreateInputField(Transform parent, string name, string defaultText, Font font,
        Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 anchored, Vector2 size)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(InputField));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = anchorMin; rt.anchorMax = anchorMax; rt.pivot = pivot;
        rt.anchoredPosition = anchored; rt.sizeDelta = size;
        go.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.15f);

        var txGO = new GameObject("Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        txGO.transform.SetParent(go.transform, false);
        var trt = txGO.GetComponent<RectTransform>();
        trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one;
        trt.offsetMin = new Vector2(12, 6); trt.offsetMax = new Vector2(-12, -6);
        var tt = txGO.GetComponent<Text>();
        tt.text = defaultText; tt.font = font; tt.fontSize = 22; tt.color = Color.white;
        tt.supportRichText = false;
        tt.alignment = TextAnchor.MiddleLeft;

        var phGO = new GameObject("Placeholder", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        phGO.transform.SetParent(go.transform, false);
        var prt = phGO.GetComponent<RectTransform>();
        prt.anchorMin = Vector2.zero; prt.anchorMax = Vector2.one;
        prt.offsetMin = new Vector2(12, 6); prt.offsetMax = new Vector2(-12, -6);
        var pt = phGO.GetComponent<Text>();
        pt.text = ""; pt.font = font; pt.fontSize = 22; pt.color = new Color(1, 1, 1, 0.35f);
        pt.alignment = TextAnchor.MiddleLeft;

        var inp = go.GetComponent<InputField>();
        inp.textComponent = tt;
        inp.placeholder = pt;
        inp.targetGraphic = go.GetComponent<Image>();
        inp.text = defaultText;
        return inp;
    }

    static void SetField(Type t, object target, string name, object value)
    {
        var f = t.GetField(name, BindingFlags.Public | BindingFlags.Instance);
        if (f != null) { f.SetValue(target, value); return; }
        var p = t.GetProperty(name, BindingFlags.Public | BindingFlags.Instance);
        if (p != null) p.SetValue(target, value, null);
    }
}
