using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 运行时关卡编辑器（挂在场景的 PipeEditor 根 GO 上）。
/// 左侧有 UI 画笔面板，中间是魔方（仅可整体旋转）。
/// 鼠标左键点击一个 cubie 面：
///   - 该面当前无 pipe -> 放置当前画笔（Start/End/Straight/Bend）
///   - 当前画笔与面上已有 pipe 类型相同 -> 循环朝向
///   - 类型不同 -> 替换为新类型
/// 鼠标右键点击 -> 删除该面上的 pipe
/// UI 面板按钮可切换画笔 / Erase（擦除）/ 保存 / 读取。
/// </summary>
public class PipeEditor : MonoBehaviour
{
    [Header("Refs")]
    public RubikCube cube;
    public PipePuzzle puzzle;
    public Camera cam;

    [Header("UI Refs (auto-wired)")]
    public Button[] brushButtons;   // 0=Start 1=End 2=Straight 3=Bend 4=Tee 5=Cross 6=Start2 7=PortalA 8=PortalB 9=Erase
    public Image[] brushButtonBgs;
    public Button saveButton;
    public Button loadButton;
    public Button clearButton;
    public Button backButton;
    public string backSceneName = "LevelSelectScene";
    public InputField levelIdField;
    public InputField levelNameField;
    public InputField moveLimitField;
    public InputField insertIndexField;
    public Button insertButton;
    public Text statusText;

    [Header("Preview Mode")]
    public Button previewButton;
    public Button resetOrientButton;
    public GameObject palettePanel;
    public Text hintText;

    private struct CubieState { public Vector3 localPos; public Quaternion localRot; }
    private List<CubieState> _savedStates;
    private Quaternion _savedCubeRotation;
    private bool _isPreview;

    public enum Brush { Start, End, Straight, Bend, Tee, Cross, Start2, PortalA, PortalB, Erase }
    public Brush currentBrush = Brush.Straight;

    void Awake()
    {
        if (cube == null) cube = FindObjectOfType<RubikCube>();
        if (puzzle == null && cube != null) puzzle = cube.GetComponent<PipePuzzle>();
        if (cam == null) cam = Camera.main;

        if (puzzle != null)
        {
            puzzle.buildSampleIfEmpty = false;
            puzzle.cells.Clear();
        }
        if (cube != null)
        {
            cube.allowLayerRotation = false;
            cube.allowWholeRotation = true;
        }
    }

    void Start()
    {
        if (brushButtons != null)
        {
            for (int i = 0; i < brushButtons.Length; i++)
            {
                int captured = i;
                if (brushButtons[i] != null)
                    brushButtons[i].onClick.AddListener(() => SetBrush(captured));
            }
        }
        if (saveButton  != null) saveButton .onClick.AddListener(OnSaveClicked);
        if (insertButton != null) insertButton.onClick.AddListener(OnInsertClicked);
        if (loadButton  != null) loadButton .onClick.AddListener(OnLoadClicked);
        if (clearButton != null) clearButton.onClick.AddListener(OnClearClicked);
        if (backButton  != null) backButton .onClick.AddListener(OnBackClicked);
        if (previewButton != null) previewButton.onClick.AddListener(TogglePreview);
        if (resetOrientButton != null) resetOrientButton.onClick.AddListener(OnResetOrientClicked);
        RefreshBrushUI();
    }

    public void OnBackClicked()
    {
        if (SceneTransitioner.Instance != null)
            SceneTransitioner.Instance.LoadSceneWithFade(backSceneName);
        else
            UnityEngine.SceneManagement.SceneManager.LoadScene(backSceneName);
    }

    public void SetBrush(int idx)
    {
        currentBrush = (Brush)idx;
        RefreshBrushUI();
    }

    void RefreshBrushUI()
    {
        if (brushButtonBgs == null) return;
        for (int i = 0; i < brushButtonBgs.Length; i++)
        {
            if (brushButtonBgs[i] == null) continue;
            bool on = (int)currentBrush == i;
            brushButtonBgs[i].color = on ? new Color(1f, 0.85f, 0.25f) : new Color(0.22f, 0.25f, 0.32f);
        }
    }

    void Update()
    {
        if (_isPreview) return;

        // 快捷键 1-8 切换画笔
        if (Input.GetKeyDown(KeyCode.Alpha1)) { SetBrush(0); return; }
        if (Input.GetKeyDown(KeyCode.Alpha2)) { SetBrush(1); return; }
        if (Input.GetKeyDown(KeyCode.Alpha3)) { SetBrush(2); return; }
        if (Input.GetKeyDown(KeyCode.Alpha4)) { SetBrush(3); return; }
        if (Input.GetKeyDown(KeyCode.Alpha5)) { SetBrush(4); return; }
        if (Input.GetKeyDown(KeyCode.Alpha6)) { SetBrush(5); return; }
        if (Input.GetKeyDown(KeyCode.Alpha7)) { SetBrush(6); return; }
        if (Input.GetKeyDown(KeyCode.Alpha8)) { SetBrush(7); return; }
        if (Input.GetKeyDown(KeyCode.Alpha9)) { SetBrush(8); return; }
        if (Input.GetKeyDown(KeyCode.Alpha0)) { SetBrush(9); return; }

        if (puzzle == null || cube == null || cam == null) return;

        bool lmb = Input.GetMouseButtonDown(0);
        bool rmb = Input.GetMouseButtonDown(1);
        if (!lmb && !rmb) return;
        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return;

        var ray = cam.ScreenPointToRay(Input.mousePosition);
        RaycastHit hit;
        if (!Physics.Raycast(ray, out hit, 1000f)) return;

        // 命中的必须是魔方的 cubie。pipe 本体已无 collider，所以此处必是 cubie body。
        var t = hit.collider.transform;
        if (!IsCubieOfCube(t)) return;

        // 取面法线 -> cube root 本地 -> snap
        Vector3 nWorld = hit.normal.normalized;
        Vector3 nLocal = Quaternion.Inverse(cube.transform.rotation) * nWorld;
        Vector3Int face = SnapAxisToInt(nLocal);

        // 取 cubie 当前 localPos -> snap 为坐标
        Vector3 lp = t.localPosition / cube.spacing;
        Vector3Int coord = new Vector3Int(Mathf.RoundToInt(lp.x), Mathf.RoundToInt(lp.y), Mathf.RoundToInt(lp.z));
        coord.x = Mathf.Clamp(coord.x, -1, 1);
        coord.y = Mathf.Clamp(coord.y, -1, 1);
        coord.z = Mathf.Clamp(coord.z, -1, 1);

        if (!PipePuzzle.IsValidOuterFace(coord, face))
        {
            SetStatus("无法放置：该面为内表面 " + coord + " " + face);
            return;
        }

        if (rmb)
        {
            puzzle.RemoveAt(coord, face);
            SetStatus("删除 " + coord + " 面 " + face);
            return;
        }

        if (currentBrush == Brush.Erase)
        {
            puzzle.RemoveAt(coord, face);
            SetStatus("删除 " + coord + " 面 " + face);
            return;
        }

        PipeKind kind = BrushToKind(currentBrush);

        int exist = puzzle.IndexOfCell(coord, face);
        if (exist >= 0 && puzzle.cells[exist].kind == kind)
        {
            puzzle.CycleOrientation(coord, face);
            SetStatus("旋转朝向 @ " + coord + " " + face);
        }
        else
        {
            puzzle.PlaceOrReplace(coord, face, kind, 0);
            SetStatus("放置 " + kind + " @ " + coord + " " + face);
        }
    }

    bool IsCubieOfCube(Transform t)
    {
        Transform cur = t;
        while (cur != null)
        {
            if (cur == cube.transform) return true;
            cur = cur.parent;
        }
        return false;
    }

    static Vector3Int SnapAxisToInt(Vector3 v)
    {
        float ax = Mathf.Abs(v.x), ay = Mathf.Abs(v.y), az = Mathf.Abs(v.z);
        if (ax >= ay && ax >= az) return new Vector3Int((int)Mathf.Sign(v.x), 0, 0);
        if (ay >= az)             return new Vector3Int(0, (int)Mathf.Sign(v.y), 0);
        return new Vector3Int(0, 0, (int)Mathf.Sign(v.z));
    }

    static PipeKind BrushToKind(Brush b)
    {
        switch (b)
        {
            case Brush.Start:   return PipeKind.Start;
            case Brush.End:     return PipeKind.End;
            case Brush.Bend:    return PipeKind.Bend;
            case Brush.Tee:     return PipeKind.Tee;
            case Brush.Cross:   return PipeKind.Cross;
            case Brush.Start2:  return PipeKind.Start2;
            case Brush.PortalA: return PipeKind.PortalA;
            case Brush.PortalB: return PipeKind.PortalB;
            default:            return PipeKind.Straight;
        }
    }

    // ----------------------- Save / Load -----------------------

    public void OnSaveClicked()
    {
#if UNITY_EDITOR
        if (puzzle == null) return;
        string id = (levelIdField != null && !string.IsNullOrEmpty(levelIdField.text)) ? levelIdField.text.Trim() : "level_1";
        string dn = (levelNameField != null && !string.IsNullOrEmpty(levelNameField.text)) ? levelNameField.text : id;
        int lim = 0;
        if (moveLimitField != null && !string.IsNullOrEmpty(moveLimitField.text))
            int.TryParse(moveLimitField.text.Trim(), out lim);
        puzzle.SetMoveLimit(lim);
        var data = puzzle.ExportData(id, dn);
        LevelStore.Save(data);
        bool appended = EnsureInLevelOrder(id);
        string rebuildMsg = RebuildLevelSelectScene();
        SetStatus("已保存：Assets/Resources/Levels/" + id + ".json  (限步=" + lim + ")"
                  + (appended ? "，并已自动新增关卡Icon顺序" : "")
                  + "\n" + rebuildMsg);
#else
        SetStatus("运行时构建不支持保存（仅 Editor 下可写入 Assets）");
#endif
    }

#if UNITY_EDITOR
    bool EnsureInLevelOrder(string id)
    {
        if (string.IsNullOrEmpty(id)) return false;

        var ordered = new List<string>(LevelStore.LoadOrderedIds());
        bool appended = !ordered.Contains(id);
        if (appended) ordered.Add(id);
        LevelStore.SaveLevelOrder(ordered.ToArray());
        return appended;
    }

    string RebuildLevelSelectScene()
    {
        var t = System.Type.GetType("LevelSelectBuilder, Assembly-CSharp-Editor");
        if (t == null)
        {
            var assemblies = System.AppDomain.CurrentDomain.GetAssemblies();
            for (int i = 0; i < assemblies.Length; i++)
            {
                t = assemblies[i].GetType("LevelSelectBuilder", false);
                if (t != null) break;
            }
        }
        if (t == null) return "自动重建选关失败：找不到 LevelSelectBuilder";

        var m = t.GetMethod("Build", BindingFlags.Public | BindingFlags.Static);
        if (m == null) return "自动重建选关失败：找不到 Build()";

        try
        {
            var ret = m.Invoke(null, null) as string;
            if (!string.IsNullOrEmpty(ret)) return "自动重建选关：" + ret;
            return "自动重建选关：完成";
        }
        catch (System.Exception ex)
        {
            return "自动重建选关异常：" + ex.GetType().Name;
        }
    }
#endif

    public void OnLoadClicked()
    {
        if (puzzle == null) return;
        string id = (levelIdField != null && !string.IsNullOrEmpty(levelIdField.text)) ? levelIdField.text.Trim() : "level_1";
        var data = LevelStore.Load(id);
        if (data == null) { SetStatus("未找到：" + id); return; }
        puzzle.LoadFromData(data, true);
        if (levelNameField != null) levelNameField.text = data.displayName;
        if (moveLimitField != null) moveLimitField.text = data.moveLimit.ToString();
        SetStatus("已载入：" + id + "（" + data.cells.Count + " 格，限步=" + data.moveLimit + "）");
    }

    public void OnInsertClicked()
    {
#if UNITY_EDITOR
        if (puzzle == null) return;

        string id = (levelIdField != null && !string.IsNullOrEmpty(levelIdField.text)) ? levelIdField.text.Trim() : "";
        if (string.IsNullOrEmpty(id))
        {
            SetStatus("请先填写关卡ID");
            return;
        }

        string dn = (levelNameField != null && !string.IsNullOrEmpty(levelNameField.text)) ? levelNameField.text : id;
        int lim = 0;
        if (moveLimitField != null && !string.IsNullOrEmpty(moveLimitField.text))
            int.TryParse(moveLimitField.text.Trim(), out lim);

        puzzle.SetMoveLimit(lim);
        var data = puzzle.ExportData(id, dn);
        LevelStore.Save(data);

        var ordered = new List<string>(LevelStore.LoadOrderedIds());
        for (int i = ordered.Count - 1; i >= 0; i--)
            if (ordered[i] == id) ordered.RemoveAt(i);

        int insertAt = ordered.Count;
        int oneBasedIndex;
        if (insertIndexField != null && int.TryParse(insertIndexField.text, out oneBasedIndex))
            insertAt = Mathf.Clamp(oneBasedIndex - 1, 0, ordered.Count);

        ordered.Insert(insertAt, id);
        LevelStore.SaveLevelOrder(ordered.ToArray());

        SetStatus("已插入顺序：" + id + " -> 第" + (insertAt + 1) + "位（共" + ordered.Count + "关）");
#else
        SetStatus("运行时构建不支持插入顺序（仅 Editor 下可写入 Assets）");
#endif
    }

    public void OnClearClicked()
    {
        if (puzzle == null) return;
        puzzle.ClearLevel();
        SetStatus("已清空");
    }

    void OnResetOrientClicked()
    {
        if (cube != null) cube.ResetOrientation();
    }

    void SetStatus(string s)
    {
        if (statusText != null) statusText.text = s;
    }

    // ----------------------- Preview Mode -----------------------

    const string EDITOR_HINT = "左键：放置/循环朝向\n右键：删除\n空白处拖拽：整体旋转魔方";
    const string PREVIEW_HINT = "预览模式\n左键拖拽小块：旋转层\n右键/空白处拖拽：整体旋转\n再次点击预览按钮退出";

    public void TogglePreview()
    {
        if (_isPreview) ExitPreview();
        else EnterPreview();
    }

    void EnterPreview()
    {
        _isPreview = true;

        // Save cubie states
        _savedStates = new List<CubieState>();
        _savedCubeRotation = cube.transform.rotation;
        foreach (var c in cube.Cubies)
            _savedStates.Add(new CubieState { localPos = c.localPosition, localRot = c.localRotation });

        // Enable layer rotation
        cube.allowLayerRotation = true;

        // Disable palette panel interaction (keep visible)
        if (palettePanel != null)
        {
            var cg = palettePanel.GetComponent<CanvasGroup>();
            if (cg != null) cg.interactable = false;
        }

        // Update hint
        if (hintText != null) hintText.text = PREVIEW_HINT;

        // Update button appearance
        if (previewButton != null)
        {
            var label = previewButton.GetComponentInChildren<Text>();
            if (label != null) label.text = "退出预览";
            previewButton.image.color = new Color(0.85f, 0.35f, 0.20f);
        }

        SetStatus("预览模式：可旋转魔方层");
    }

    void ExitPreview()
    {
        _isPreview = false;

        if (cube != null && _savedStates != null)
        {
            // Reset any in-progress interaction
            cube.ResetInteraction();

            // Restore cubie states
            var cubies = cube.Cubies;
            for (int i = 0; i < cubies.Count && i < _savedStates.Count; i++)
            {
                cubies[i].localPosition = _savedStates[i].localPos;
                cubies[i].localRotation = _savedStates[i].localRot;
            }
            cube.transform.rotation = _savedCubeRotation;
            cube.allowLayerRotation = false;
        }

        // Re-enable palette panel interaction
        if (palettePanel != null)
        {
            var cg = palettePanel.GetComponent<CanvasGroup>();
            if (cg != null) cg.interactable = true;
        }

        // Update hint
        if (hintText != null) hintText.text = EDITOR_HINT;

        // Update button appearance
        if (previewButton != null)
        {
            var label = previewButton.GetComponentInChildren<Text>();
            if (label != null) label.text = "预览";
            previewButton.image.color = new Color(0.20f, 0.55f, 0.80f);
        }

        SetStatus("已退出预览模式");
    }
}
