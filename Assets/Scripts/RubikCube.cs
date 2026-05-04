using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// 3x3x3 魔方。Start 时程序化构建 27 个小块。
/// 鼠标左键拖拽：
///   - 拖空白（未命中小块）：绕屏幕轴旋转整体
///   - 拖某个小块：根据拖拽方向决定旋转轴，旋转该小块所在的那一层，松开后自动吸附到 90 度
/// </summary>
public class RubikCube : MonoBehaviour
{
    [Header("Build")]
    public bool buildOnStart = true;
    [Tooltip("小块之间的间距（==1 则紧贴，1.02 有轻微黑缝）")]
    public float spacing = 1.02f;

    [Header("Colors (Rubik 标准)")]
    public Color colorRight = new Color(0.96f, 0.96f, 0.96f); // +X white
    public Color colorLeft  = new Color(0.96f, 0.96f, 0.96f); // -X white
    public Color colorUp    = new Color(0.96f, 0.96f, 0.96f); // +Y white
    public Color colorDown  = new Color(0.96f, 0.96f, 0.96f); // -Y white
    public Color colorFront = new Color(0.96f, 0.96f, 0.96f); // +Z white
    public Color colorBack  = new Color(0.96f, 0.96f, 0.96f); // -Z white
    public Color colorBody  = new Color(0.08f, 0.08f, 0.08f);

    [Header("Interaction")]
    public Camera cam;
    [Tooltip("整体旋转 arcball 灵敏度，值越大转得越快")]
    public float wholeArcballSensitivity = 0.25f;
    [Tooltip("整体旋转惯性阻尼（0=无惯性，0.95=很滑，推荐 0.85~0.92）")]
    [Range(0f, 0.98f)] public float wholeDamping = 0.9f;
    [Tooltip("整体旋转开始前的死区（像素），避免轻触抖动")]
    public float wholeDeadZone = 4f;
    public enum WholeLockMode { Free = 0, Axis4 = 4, Axis8 = 8 }
    [Tooltip("整体旋转方向锁定：Free=不锁；Axis4=上下左右；Axis8=上下左右+4斜方向")]
    public WholeLockMode wholeLockMode = WholeLockMode.Free;
    [Tooltip("整体旋转：拖拽累计到这个像素才真正锁定方向（期间不会开始转），越大越稳")]
    public float wholeLockThreshold = 18f;
    [Tooltip("松手后把整体朝向吸附到最接近的\"轴对齐\"姿态（24 个正交方向之一）")]
    public bool snapWholeOnRelease = false;
    [Tooltip("整体朝向吸附动画时长")]
    public float wholeSnapDuration = 0.18f;
    [Tooltip("层旋转：决策下限像素阈值；低于此值完全不判定轴")]
    public float layerDragThreshold = 18f;
    [Tooltip("层旋转：决策上限像素；低于此值优先等待更明确的方向，超过此值强制按主方向锁定")]
    public float layerDragMaxWait = 45f;
    [Tooltip("层旋转：两个候选方向投影比大于此值才算\"明显占优\"可以锁轴")]
    public float layerDecisionRatio = 1.3f;
    [Tooltip("层旋转时每像素转多少度（仅备用线性模式生效）")]
    public float layerDragSpeed = 0.38f;
    [Tooltip("层旋转阻尼：arcball 角度的增益，1=跟手，<1 更稳；推荐 0.5~0.8")]
    [Range(0.1f, 1.5f)] public float layerDragGain = 0.6f;
    [Tooltip("层旋转单帧最大变化角度（度），防止瞬间大跳导致反向")]
    public float layerMaxAnglePerFrame = 25f;
    [Tooltip("层旋转最小有效半径（世界单位）：鼠标投影距轴小于此值时不更新（抖动放大区）")]
    public float layerMinRadius = 0.55f;
    [Tooltip("撤销一次层旋转时的动画时长")]
    public float snapDuration = 0.18f;
    [Tooltip("触发一次层旋转后，旋转 90° 所需时间（秒）")]
    public float layerTurn90Duration = 0.18f;
    [Tooltip("是否允许用户拖拽旋转某一层（关卡编辑器中关闭）")]
    public bool allowLayerRotation = true;
    [Tooltip("是否允许用户拖拽整体旋转")]
    public bool allowWholeRotation = true;
    [Tooltip("右键/中键拖拽 = 整体旋转（无论点在哪）")]
    public bool rightButtonForWhole = true;

    public event Action OnLayerSnapped;
    public event Action OnLayerUndone;
    public event Action OnBuilt;
    public bool IsBuilt { get { return _cubies != null && _cubies.Count > 0; } }
    public IReadOnlyList<Transform> Cubies { get { return _cubies; } }
    public bool CanUndo { get { return _history.Count > 0 && !_snapping && _mode == DragMode.None; } }

    struct MoveRecord { public Vector3 axisLocal; public int layerIndex; public int steps; }
    private readonly Stack<MoveRecord> _history = new Stack<MoveRecord>();

    private readonly List<Transform> _cubies = new List<Transform>();
    private Transform _layerHolder;

    enum DragMode { None, Whole, PendingLayer }
    private DragMode _mode = DragMode.None;
    private Vector3 _dragStart;
    private Vector3 _lastMouse;

    // layer interaction state
    private Transform _hitCubie;
    private Vector3 _hitNormalWorld;
    private Vector3 _layerAxisLocal;
    private int _layerIndex;
    private Vector2 _layerSignedScreenTangent; // 指向"正旋转"方向的单位向量（备用：arcball 不可用时回落）
    private Vector3 _layerRefDirLocal;         // 锁轴时鼠标在旋转平面上的参考方向（cube 本地）
    private bool _layerArcballReady;
    private float _layerArcPrevRaw;
    private bool _layerArcHasPrev;
    private float _layerAngle;
    private readonly List<Transform> _layerCubies = new List<Transform>();
    private bool _snapping;

    private Material _skinBodyMaterial;
    private Material _skinStickerMaterial;

    void Start()
    {
        if (cam == null) cam = Camera.main;
        QualitySettings.antiAliasing = 8;
        if (buildOnStart) Build();
    }

    public void ApplySkin(SkinConfig skin)
    {
        _skinBodyMaterial = skin != null ? skin.cubeBodyMaterial : null;
        _skinStickerMaterial = skin != null ? skin.cubeStickerMaterial : null;

        if (_cubies != null && _cubies.Count > 0)
            ApplySkinToBuiltCube();
    }

    void ApplySkinToBuiltCube()
    {
        for (int i = 0; i < _cubies.Count; i++)
        {
            var c = _cubies[i];
            if (c == null) continue;

            var renderers = c.GetComponentsInChildren<Renderer>(true);
            for (int r = 0; r < renderers.Length; r++)
            {
                var rd = renderers[r];
                if (rd == null) continue;

                if (IsStickerRenderer(rd))
                {
                    if (_skinStickerMaterial != null)
                        rd.sharedMaterial = _skinStickerMaterial;
                }
                else
                {
                    if (_skinBodyMaterial != null)
                        rd.sharedMaterial = _skinBodyMaterial;
                }
            }
        }
    }

    static bool IsStickerRenderer(Renderer renderer)
    {
        if (renderer == null) return false;
        return renderer.name.IndexOf("Sticker", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    public void Build()
    {
        // Clear existing (同步销毁，避免同一帧中新旧共存造成 BuildVisuals 误命中)
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            var ch = transform.GetChild(i);
            DestroyImmediate(ch.gameObject);
        }
        _cubies.Clear();
        _history.Clear();

        for (int x = -1; x <= 1; x++)
            for (int y = -1; y <= 1; y++)
                for (int z = -1; z <= 1; z++)
                {
                    var cubie = BuildCubie(x, y, z);
                    cubie.SetParent(transform, false);
                    cubie.localPosition = new Vector3(x, y, z) * spacing;
                    _cubies.Add(cubie);
                }

        // (Re)create layer holder
        if (_layerHolder != null)
        {
            DestroyImmediate(_layerHolder.gameObject);
        }
        var holderGO = new GameObject("LayerHolder");
        holderGO.transform.SetParent(transform, false);
        holderGO.transform.localPosition = Vector3.zero;
        holderGO.transform.localRotation = Quaternion.identity;
        _layerHolder = holderGO.transform;

        if (OnBuilt != null) OnBuilt();
    }

    Transform BuildCubie(int x, int y, int z)
    {
        var body = GameObject.CreatePrimitive(PrimitiveType.Cube);
        body.name = "Cubie_" + x + "_" + y + "_" + z;
        body.transform.localScale = Vector3.one;
        var br = body.GetComponent<Renderer>();
        br.sharedMaterial = MakeMat(colorBody, "body");

        if (x ==  1) AddSticker(body.transform, Vector3.right,   colorRight);
        if (x == -1) AddSticker(body.transform, Vector3.left,    colorLeft);
        if (y ==  1) AddSticker(body.transform, Vector3.up,      colorUp);
        if (y == -1) AddSticker(body.transform, Vector3.down,    colorDown);
        if (z ==  1) AddSticker(body.transform, Vector3.forward, colorFront);
        if (z == -1) AddSticker(body.transform, Vector3.back,    colorBack);

        return body.transform;
    }

    void AddSticker(Transform parent, Vector3 normal, Color c)
    {
        // 圆角贴纸：用高段数圆柱模拟圆角矩形
        var sticker = new GameObject("Sticker");
        sticker.transform.SetParent(parent, false);
        sticker.transform.localPosition = normal * 0.502f;
        sticker.transform.localRotation = Quaternion.LookRotation(-normal);

        float size = 0.86f;
        float radius = 0.06f;
        float thick = 0.012f;

        // 中心平面
        var plane = GameObject.CreatePrimitive(PrimitiveType.Quad);
        plane.name = "StickerCenter";
        plane.transform.SetParent(sticker.transform, false);
        plane.transform.localScale = new Vector3(size - radius * 2, size - radius * 2, 1f);
        var pr = plane.GetComponent<Renderer>();
        pr.sharedMaterial = MakeStickerMat(c);
        var pc = plane.GetComponent<Collider>();
        if (pc != null) { if (Application.isPlaying) Destroy(pc); else DestroyImmediate(pc); }

        // 4 个边条
        float edgeW = size - radius * 2;
        float edgeH = radius * 2;
        AddStickerEdge(sticker.transform, new Vector3(0, (size - radius) / 2f, 0), edgeW, edgeH, c);   // top
        AddStickerEdge(sticker.transform, new Vector3(0, -(size - radius) / 2f, 0), edgeW, edgeH, c);   // bottom
        AddStickerEdge(sticker.transform, new Vector3((size - radius) / 2f, 0, 0), edgeH, edgeW, c);    // right
        AddStickerEdge(sticker.transform, new Vector3(-(size - radius) / 2f, 0, 0), edgeH, edgeW, c);   // left

        // 4 个圆角
        AddStickerCorner(sticker.transform, new Vector3((size - radius) / 2f, (size - radius) / 2f, 0), radius, c, 0);
        AddStickerCorner(sticker.transform, new Vector3(-(size - radius) / 2f, (size - radius) / 2f, 0), radius, c, 1);
        AddStickerCorner(sticker.transform, new Vector3(-(size - radius) / 2f, -(size - radius) / 2f, 0), radius, c, 2);
        AddStickerCorner(sticker.transform, new Vector3((size - radius) / 2f, -(size - radius) / 2f, 0), radius, c, 3);
    }

    void AddStickerEdge(Transform parent, Vector3 pos, float w, float h, Color c)
    {
        var q = GameObject.CreatePrimitive(PrimitiveType.Quad);
        q.transform.SetParent(parent, false);
        q.transform.localPosition = pos;
        q.transform.localScale = new Vector3(w, h, 1f);
        var r = q.GetComponent<Renderer>();
        r.sharedMaterial = MakeStickerMat(c);
        var col = q.GetComponent<Collider>();
        if (col != null) { if (Application.isPlaying) Destroy(col); else DestroyImmediate(col); }
    }

    void AddStickerCorner(Transform parent, Vector3 pos, float radius, Color c, int cornerIndex)
    {
        // 用 6 段扇形模拟 90° 圆角
        int segs = 6;
        float startAngle = cornerIndex * 90f + 45f; // 每个角起始角度
        var mesh = new Mesh();
        var verts = new List<Vector3>();
        var tris = new List<int>();
        var uvs = new List<Vector2>();
        verts.Add(pos); // center
        uvs.Add(new Vector2(0.5f, 0.5f));
        for (int i = 0; i <= segs; i++)
        {
            float a = Mathf.Deg2Rad * (startAngle + (float)i / segs * 90f);
            verts.Add(pos + new Vector3(Mathf.Cos(a) * radius, Mathf.Sin(a) * radius, 0));
            uvs.Add(new Vector2(0.5f + Mathf.Cos(a) * 0.5f, 0.5f + Mathf.Sin(a) * 0.5f));
        }
        for (int i = 1; i <= segs; i++)
        {
            tris.Add(0); tris.Add(i); tris.Add(i + 1);
        }
        mesh.SetVertices(verts); mesh.SetUVs(0, uvs); mesh.SetTriangles(tris, 0);
        mesh.RecalculateNormals();
        var go = new GameObject("StickerCorner");
        go.transform.SetParent(parent, false);
        var mf = go.AddComponent<MeshFilter>();
        mf.sharedMesh = mesh;
        var mr = go.AddComponent<MeshRenderer>();
        mr.sharedMaterial = MakeStickerMat(c);
    }

    Material MakeStickerMat(Color c)
    {
        Shader sh = RuntimeShaderResolver.ResolveColorShader();
        var m = new Material(sh) { name = "sticker" };
        ApplyColorToMaterial(m, c, 0.45f, 0.0f, Color.black);
        return m;
    }

    Material MakeMat(Color c, string name)
    {
        Shader sh = RuntimeShaderResolver.ResolveColorShader();
        var m = new Material(sh) { name = name };
        ApplyColorToMaterial(m, c, 0.05f, 0.0f, Color.black);
        return m;
    }

    static void ApplyColorToMaterial(Material m, Color baseColor, float smoothness, float metallic, Color emission)
    {
        if (m == null) return;

        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", baseColor);
        if (m.HasProperty("_Color")) m.SetColor("_Color", baseColor);

        if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smoothness);
        if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", smoothness);
        if (m.HasProperty("_GlossyScale")) m.SetFloat("_GlossyScale", smoothness);

        if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", metallic);

        if (m.HasProperty("_EmissionColor"))
        {
            if (emission.maxColorComponent > 0.0001f) m.EnableKeyword("_EMISSION");
            m.SetColor("_EmissionColor", emission);
        }
    }

    // ---------------- Input ----------------

    void Update()
    {
        if (_snapping) { UpdateWholeInertia(); return; }

        // 惯性衰减（无论是否在层吸附中都要跑）
        UpdateWholeInertia();

        // 整体旋转：右键 / 中键（任何位置），支持与左键独立
        if (rightButtonForWhole && allowWholeRotation)
        {
            HandleWholeButton(1); // 右键
            HandleWholeButton(2); // 中键
        }

        // 左键：层旋转 / 或空白处整体旋转
        if (Input.GetMouseButtonDown(0))
        {
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
            {
                _mode = DragMode.None;
                return;
            }
            _dragStart = _lastMouse = Input.mousePosition;

            if (cam == null) cam = Camera.main;
            var ray = cam.ScreenPointToRay(Input.mousePosition);
            RaycastHit hit;
            if (allowLayerRotation && TryPickLayerHit(ray, out hit))
            {
                _hitCubie = hit.collider.transform;
                _hitNormalWorld = hit.normal.normalized;
                _mode = DragMode.PendingLayer;
            }
            else if (allowWholeRotation)
            {
                _mode = DragMode.Whole;
                ResetWholeAxisLock();
            }
            else
            {
                _mode = DragMode.None;
            }
        }
        else if (Input.GetMouseButton(0))
        {
            Vector3 m = Input.mousePosition;
            Vector3 delta = m - _lastMouse;
            _lastMouse = m;

            if (_mode == DragMode.Whole)
            {
                ApplyWholeArcball((Vector2)(m - _dragStart), delta);
            }
            else if (_mode == DragMode.PendingLayer)
            {
                Vector2 total = (Vector2)(m - _dragStart);
                float mag = total.magnitude;
                if (mag >= layerDragThreshold)
                {
                    // 第一阶段：两候选方向投影必须明显占优才锁轴；
                    // 如果含糊不清则继续等，等到超过 max 时再强制决定。
                    bool force = mag >= layerDragMaxWait;
                    if (DecideLayer(total, force))
                    {
                        _mode = DragMode.None;
                        StartCoroutine(RotateLayerBySteps(1));
                    }
                    // 未锁定：继续停在 PendingLayer 等下一帧
                }
            }
        }
        else if (Input.GetMouseButtonUp(0))
        {
            bool wasWhole = (_mode == DragMode.Whole);
            _mode = DragMode.None;
            ResetWholeAxisLock();
            if (wasWhole)
            {
                if (_wholeAngularVelocity.sqrMagnitude > 1f)
                    _wholeInertiaActive = true;
            }
        }
    }

    // ------ Whole rotation via arcball + damping ------

    private bool[] _wholeBtnActive = new bool[3];
    private Vector3[] _wholeBtnStart = new Vector3[3];
    private Vector3[] _wholeBtnLast = new Vector3[3];
    private Vector3 _wholeAngularVelocity; // 惯性角速度（度/秒）
    private bool _wholeInertiaActive;

    void HandleWholeButton(int btn)
    {
        if (Input.GetMouseButtonDown(btn))
        {
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return;
            _wholeBtnActive[btn] = true;
            _wholeBtnStart[btn] = _wholeBtnLast[btn] = Input.mousePosition;
            ResetWholeAxisLock();
            _wholeInertiaActive = false; // 按下时取消惯性
            _wholeAngularVelocity = Vector3.zero;
        }
        else if (_wholeBtnActive[btn] && Input.GetMouseButton(btn))
        {
            Vector3 m = Input.mousePosition;
            Vector3 delta = m - _wholeBtnLast[btn];
            _wholeBtnLast[btn] = m;
            ApplyWholeArcball((Vector2)(m - _wholeBtnStart[btn]), delta);
        }
        else if (Input.GetMouseButtonUp(btn))
        {
            _wholeBtnActive[btn] = false;
            ResetWholeAxisLock();
            // 启动惯性（如果有角速度的话）
            if (_wholeAngularVelocity.sqrMagnitude > 1f)
                _wholeInertiaActive = true;
        }
    }

    private Vector2 _wholeLockDir = Vector2.zero;
    void ResetWholeAxisLock() { _wholeLockDir = Vector2.zero; }

    /// <summary>回正：平滑旋转魔方整体到初始朝向（identity）。</summary>
    private Coroutine _resetCo;
    public void ResetOrientation()
    {
        // 停掉惯性
        _wholeInertiaActive = false;
        _wholeAngularVelocity = Vector3.zero;
        if (_resetCo != null) StopCoroutine(_resetCo);
        _resetCo = StartCoroutine(ResetOrientationCo());
    }
    IEnumerator ResetOrientationCo()
    {
        Quaternion start = transform.rotation;
        Quaternion target = Quaternion.identity;
        float dur = 0.35f;
        float t = 0f;
        while (t < dur)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / dur);
            k = 1f - (1f - k) * (1f - k); // ease-out
            transform.rotation = Quaternion.Slerp(start, target, k);
            yield return null;
        }
        transform.rotation = target;
        _resetCo = null;
    }

    /// <summary>Arcball 整体旋转：把屏幕拖拽映射为绕魔方中心的球面旋转。</summary>
    void ApplyWholeArcball(Vector2 totalFromStart, Vector3 frameDelta)
    {
        float mag = totalFromStart.magnitude;
        if (mag < wholeDeadZone) return;

        Vector2 fd = new Vector2(frameDelta.x, frameDelta.y);

        if (wholeLockMode != WholeLockMode.Free)
        {
            if (_wholeLockDir.sqrMagnitude < 1e-6f)
            {
                if (mag < wholeLockThreshold) return;
                _wholeLockDir = SnapDirection(totalFromStart, (int)wholeLockMode);
            }
            float proj = Vector2.Dot(fd, _wholeLockDir);
            fd = _wholeLockDir * proj;
        }

        // Arcball: 把屏幕 dx/dy 映射为绕过魔方中心的世界轴旋转
        // 水平拖 → 绕世界 up 旋转，垂直拖 → 绕相机 right 旋转
        float dxDeg = -fd.x * wholeArcballSensitivity;
        float dyDeg =  fd.y * wholeArcballSensitivity;

        // 记录角速度供惯性使用
        _wholeAngularVelocity = new Vector3(dxDeg, dyDeg, 0f) / Time.deltaTime;

        Quaternion rx = Quaternion.AngleAxis(dxDeg, Vector3.up);
        Quaternion ry = Quaternion.AngleAxis(dyDeg, cam.transform.right);
        transform.rotation = rx * ry * transform.rotation;
    }

    /// <summary>惯性衰减 + 自动吸附</summary>
    void UpdateWholeInertia()
    {
        if (!_wholeInertiaActive) return;

        // 衰减
        _wholeAngularVelocity *= Mathf.Pow(wholeDamping, Time.deltaTime * 60f);
        float speed = _wholeAngularVelocity.magnitude;

        if (speed < 5f) // 低于阈值，停止惯性
        {
            _wholeInertiaActive = false;
            _wholeAngularVelocity = Vector3.zero;
            return;
        }

        // 应用旋转
        float dxDeg = _wholeAngularVelocity.x * Time.deltaTime;
        float dyDeg = _wholeAngularVelocity.y * Time.deltaTime;
        Quaternion rx = Quaternion.AngleAxis(dxDeg, Vector3.up);
        Quaternion ry = Quaternion.AngleAxis(dyDeg, cam.transform.right);
        transform.rotation = rx * ry * transform.rotation;
    }

    // ------ 整体朝向吸附到最近的"轴对齐"姿态 ------

    private Coroutine _wholeSnapCo;

    void TrySnapWholeOrientation()
    {
        if (!snapWholeOnRelease) return;
        if (_snapping) return; // 层吸附中不要插手
        // 如果仍有其它 whole 按键按着，不要吸附
        for (int i = 0; i < _wholeBtnActive.Length; i++) if (_wholeBtnActive[i]) return;
        if (_mode == DragMode.Whole) return; // LMB 还没松

        Quaternion target = SnapToCardinalOrientation(transform.rotation);
        if (Quaternion.Angle(transform.rotation, target) < 0.01f) return;

        if (_wholeSnapCo != null) StopCoroutine(_wholeSnapCo);
        _wholeSnapCo = StartCoroutine(WholeSnapCoroutine(target));
    }

    IEnumerator WholeSnapCoroutine(Quaternion target)
    {
        Quaternion start = transform.rotation;
        float dur = Mathf.Max(0.01f, wholeSnapDuration);
        float t = 0f;
        while (t < dur)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / dur);
            // 轻微缓出
            k = 1f - (1f - k) * (1f - k);
            transform.rotation = Quaternion.Slerp(start, target, k);
            yield return null;
        }
        transform.rotation = target;
        _wholeSnapCo = null;
    }

    /// <summary>把 q 吸附到 24 个正交朝向之一：cube 的局部 X/Y/Z 各自对齐到一个世界 ±X/±Y/±Z。</summary>
    static Quaternion SnapToCardinalOrientation(Quaternion q)
    {
        Vector3 lx = q * Vector3.right;
        Vector3 ly = q * Vector3.up;

        // 贪心：先给局部 X 选最近的世界轴；再给局部 Y 选除掉同轴外剩下的最近；Z 由 X×Y 推出
        int usedAxis; // 0=X, 1=Y, 2=Z
        int usedSign; // +1 / -1
        Vector3 sx = NearestWorldAxis(lx, -1, out usedAxis, out usedSign);
        Vector3 sy = NearestWorldAxisExcluding(ly, usedAxis);
        Vector3 sz = Vector3.Cross(sx, sy).normalized; // 保证右手坐标系与局部一致
        return Quaternion.LookRotation(sz, sy);
    }

    static Vector3 NearestWorldAxis(Vector3 v, int excludeAxis, out int axis, out int sign)
    {
        Vector3[] axes = { Vector3.right, Vector3.up, Vector3.forward };
        axis = 0; sign = 1;
        float best = -2f;
        Vector3 result = Vector3.right;
        for (int i = 0; i < 3; i++)
        {
            if (i == excludeAxis) continue;
            float d = Vector3.Dot(v, axes[i]);
            float ad = Mathf.Abs(d);
            if (ad > best) { best = ad; axis = i; sign = d >= 0 ? 1 : -1; result = axes[i] * sign; }
        }
        return result;
    }

    static Vector3 NearestWorldAxisExcluding(Vector3 v, int excludeAxis)
    {
        int a, s;
        return NearestWorldAxis(v, excludeAxis, out a, out s);
    }

    /// <summary>把任意向量吸附到 n 等分的单位方向之一（n=4 或 8）。</summary>
    static Vector2 SnapDirection(Vector2 v, int n)
    {
        if (v.sqrMagnitude < 1e-6f || n <= 0) return Vector2.right;
        Vector2 t = v.normalized;
        float bestDot = -2f;
        Vector2 bestDir = Vector2.right;
        for (int i = 0; i < n; i++)
        {
            float a = i * Mathf.PI * 2f / n;
            Vector2 d = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
            float dot = Vector2.Dot(t, d);
            if (dot > bestDot) { bestDot = dot; bestDir = d; }
        }
        return bestDir;
    }

    bool IsOwnCubie(Transform t)
    {
        // cubie 是 transform 的子（可能被临时放到 _layerHolder 下，_layerHolder 是 transform 的子）
        Transform cur = t;
        while (cur != null)
        {
            if (cur.parent == transform || cur.parent == _layerHolder) return true;
            cur = cur.parent;
        }
        return false;
    }

    bool TryPickLayerHit(Ray ray, out RaycastHit bestHit)
    {
        bestHit = default;
        var hits = Physics.RaycastAll(ray, 1000f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        if (hits == null || hits.Length == 0) return false;

        float bestDistance = float.PositiveInfinity;
        bool found = false;
        Vector3 invRay = -ray.direction.normalized;

        for (int i = 0; i < hits.Length; i++)
        {
            var h = hits[i];
            if (h.collider == null) continue;
            if (!IsOwnCubie(h.collider.transform)) continue;

            Vector3 fromCenter = h.point - transform.position;
            if (fromCenter.sqrMagnitude > 1e-6f)
            {
                float frontness = Vector3.Dot(fromCenter.normalized, invRay);
                if (frontness < -0.1f) continue;
            }

            if (h.distance < bestDistance)
            {
                bestDistance = h.distance;
                bestHit = h;
                found = true;
            }
        }

        return found;
    }

    bool DecideLayer(Vector2 totalDragScreen, bool force)
    {
        // 将面法线转换到 cube 本地空间并吸附到标准轴
        Vector3 nLocal = Quaternion.Inverse(transform.rotation) * _hitNormalWorld;
        nLocal = SnapToAxis(nLocal);

        // 两个切线方向（与 nLocal 垂直的两条 cube 本地轴）
        Vector3 ta, tb;
        if (Mathf.Abs(nLocal.x) > 0.5f) { ta = Vector3.up;    tb = Vector3.forward; }
        else if (Mathf.Abs(nLocal.y) > 0.5f) { ta = Vector3.right; tb = Vector3.forward; }
        else                                  { ta = Vector3.right; tb = Vector3.up; }

        Vector3 hitWorld = _hitCubie.position;
        Vector2 sOrigin = cam.WorldToScreenPoint(hitWorld);
        Vector2 sA = (Vector2)cam.WorldToScreenPoint(hitWorld + transform.rotation * ta) - sOrigin;
        Vector2 sB = (Vector2)cam.WorldToScreenPoint(hitWorld + transform.rotation * tb) - sOrigin;
        if (sA.sqrMagnitude < 1e-4f || sB.sqrMagnitude < 1e-4f) return false;
        sA.Normalize();
        sB.Normalize();

        float dotA = Vector2.Dot(totalDragScreen, sA);
        float dotB = Vector2.Dot(totalDragScreen, sB);
        float absA = Mathf.Abs(dotA), absB = Mathf.Abs(dotB);

        // 未达到"明显占优"且尚未到强制阈值：推迟决定
        if (!force)
        {
            float bigger  = Mathf.Max(absA, absB);
            float smaller = Mathf.Min(absA, absB);
            if (smaller > 1e-3f && bigger / Mathf.Max(smaller, 1e-3f) < layerDecisionRatio)
                return false;
        }

        Vector3 chosenLocal;
        Vector2 screenTangent;
        if (absA > absB)
        {
            chosenLocal = ta * Mathf.Sign(dotA);
            screenTangent = sA * Mathf.Sign(dotA);
        }
        else
        {
            chosenLocal = tb * Mathf.Sign(dotB);
            screenTangent = sB * Mathf.Sign(dotB);
        }

        // 旋转轴（cube 本地）：法线 × 拖拽方向
        Vector3 axisLocal = Vector3.Cross(nLocal, chosenLocal).normalized;
        axisLocal = SnapToAxis(axisLocal);

        // 决定哪一层：用被击中的小块当前 localPosition 在 axis 上的投影
        Vector3 hitLocalPos = transform.InverseTransformPoint(hitWorld);
        float hitCoord = Vector3.Dot(hitLocalPos, axisLocal);
        int layerIndex = Mathf.RoundToInt(hitCoord / spacing);

        // 收集这一层的小块，重父到 _layerHolder（保留世界变换）
        _layerCubies.Clear();
        _layerHolder.localRotation = Quaternion.identity;

        for (int i = 0; i < _cubies.Count; i++)
        {
            var c = _cubies[i];
            if (c == null) continue;
            // 小块当前可能已经不是 transform 的直接子（之前层旋转过），需从当前 localPos 相对 cube root 来判断
            Vector3 lp = transform.InverseTransformPoint(c.position);
            float coord = Vector3.Dot(lp, axisLocal);
            int idx = Mathf.RoundToInt(coord / spacing);
            if (idx == layerIndex)
            {
                _layerCubies.Add(c);
                c.SetParent(_layerHolder, true);
            }
        }

        _layerAxisLocal = axisLocal;
        _layerIndex = layerIndex;
        _layerSignedScreenTangent = screenTangent;
        _layerArcPrevRaw = 0f;
        _layerArcHasPrev = false;
        _layerAngle = 0f;

        // 准备 arcball：把当前鼠标位置投影到"绕轴的那个平面"（平面过魔方中心，法向 = 轴的世界方向）
        _layerArcballReady = false;
        Vector3 axisWorld = (transform.rotation * _layerAxisLocal).normalized;
        Vector3 refHit;
        if (TryRayToAxisPlane(Input.mousePosition, axisWorld, transform.position, out refHit))
        {
            Vector3 refDirWorld = Vector3.ProjectOnPlane(refHit - transform.position, axisWorld);
            if (refDirWorld.sqrMagnitude > 1e-4f)
            {
                _layerRefDirLocal = Quaternion.Inverse(transform.rotation) * refDirWorld.normalized;
                _layerArcballReady = true;
                _layerArcPrevRaw = 0f;
                _layerArcHasPrev = true;
            }
        }
        return _layerCubies.Count > 0;
    }

    /// <summary>基于 arcball 的层旋转角度：在垂直于旋转轴的平面上测鼠标相对参考方向扫过的有向角。
    /// 轴/参考方向都随魔方整体 transform 转动。当 arcball 不可用（视线近乎平行于轴）时回落到线性像素模式。
    /// </summary>
    float ComputeLayerAngle(Vector3 mouseScreen)
    {
        Vector3 axisWorld = (transform.rotation * _layerAxisLocal).normalized;

        if (_layerArcballReady)
        {
            Vector3 curHit;
            if (TryRayToAxisPlane(mouseScreen, axisWorld, transform.position, out curHit))
            {
                Vector3 curDir = Vector3.ProjectOnPlane(curHit - transform.position, axisWorld);
                float radius = curDir.magnitude;
                if (radius >= layerMinRadius)
                {
                    Vector3 refDirWorld = (transform.rotation * _layerRefDirLocal).normalized;
                    float raw = Vector3.SignedAngle(refDirWorld, curDir.normalized, axisWorld);

                    // 1:1 跟手：用连续累加解决 ±180 跳跃，不加 gain / 不限单帧角度
                    float deltaRaw;
                    if (_layerArcHasPrev)
                        deltaRaw = Mathf.DeltaAngle(_layerArcPrevRaw, raw);
                    else
                        deltaRaw = Mathf.DeltaAngle(_layerAngle % 360f, raw);

                    _layerArcPrevRaw = raw;
                    _layerArcHasPrev = true;

                    // 微小抖动死区：角度变化太小时不更新，防止手指静止时噪声
                    if (Mathf.Abs(deltaRaw) < 0.15f) return _layerAngle;
                    return _layerAngle + deltaRaw;
                }
                // 鼠标太靠近旋转轴：角度会对像素噪声高度敏感，冻结到上一帧
                return _layerAngle;
            }
            // arcball 本帧失败（例如鼠标划到屏外/视线切向），退回使用上一帧的角度
            return _layerAngle;
        }

        // 回落：线性像素模式 —— 同样 1:1 跟手
        float pixelAlong = Vector2.Dot((Vector2)(mouseScreen - _dragStart), _layerSignedScreenTangent);
        return pixelAlong * layerDragSpeed;
    }

    bool TryRayToAxisPlane(Vector2 screenPos, Vector3 axisWorld, Vector3 planePoint, out Vector3 hitPoint)
    {
        hitPoint = Vector3.zero;
        if (cam == null) return false;
        // 视线与平面法线夹角太小（接近平行于轴）时投影不稳定，放弃
        Ray r = cam.ScreenPointToRay(screenPos);
        float nd = Vector3.Dot(r.direction, axisWorld);
        if (Mathf.Abs(nd) < 0.15f) return false;
        var plane = new Plane(axisWorld, planePoint);
        float t;
        if (!plane.Raycast(r, out t) || t < 0f) return false;
        hitPoint = r.GetPoint(t);
        return true;
    }

    IEnumerator RotateLayerBySteps(int steps)
    {
        _snapping = true;
        float target = steps * 90f;
        float start = 0f;

        Quaternion startQ = Quaternion.AngleAxis(start, _layerAxisLocal);
        Quaternion endQ   = Quaternion.AngleAxis(target, _layerAxisLocal);
        float t = 0f;
        float quarterTurns = Mathf.Max(1f, Mathf.Abs(target - start) / 90f);
        float dur = Mathf.Max(0.01f, layerTurn90Duration * quarterTurns);
        while (t < dur)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / dur);
            _layerHolder.localRotation = Quaternion.Slerp(startQ, endQ, k);
            yield return null;
        }
        _layerHolder.localRotation = endQ;

        // 把小块重父回 cube root，并把位置吸附到整数网格（消除浮点漂移）
        for (int i = 0; i < _layerCubies.Count; i++)
        {
            var c = _layerCubies[i];
            if (c == null) continue;
            c.SetParent(transform, true);
            Vector3 p = c.localPosition;
            p.x = Mathf.Round(p.x / spacing) * spacing;
            p.y = Mathf.Round(p.y / spacing) * spacing;
            p.z = Mathf.Round(p.z / spacing) * spacing;
            c.localPosition = p;
        }
        _layerCubies.Clear();
        _layerHolder.localRotation = Quaternion.identity;
        _mode = DragMode.None;
        _snapping = false;

        // 只有真正完成了 90° 倍数的旋转（吸附不是回到 0°）才算一步
        if (steps != 0)
        {
            // 规范化 steps 到 {-1, 1, 2}（180° 可正可负都记为 2，撤销时转 -2 即 +2 同效果）
            int norm = ((steps % 4) + 4) % 4;
            int signedSteps = (norm == 3) ? -1 : norm; // 0->0, 1->1, 2->2, 3->-1
            _history.Push(new MoveRecord
            {
                axisLocal = _layerAxisLocal,
                layerIndex = _layerIndex,
                steps = signedSteps
            });
            if (OnLayerSnapped != null) OnLayerSnapped();
        }
    }

    // -----------------------------------------------------------------------
    // 撤销
    // -----------------------------------------------------------------------

    /// <summary>撤回最近一次完成的层旋转。成功时开启动画并在结束后触发 OnLayerUndone。</summary>
    public bool UndoLastRotation()
    {
        if (!CanUndo) return false;
        var m = _history.Pop();
        StartCoroutine(UndoCoroutine(m));
        return true;
    }

    IEnumerator UndoCoroutine(MoveRecord m)
    {
        _snapping = true;

        // 按记录的轴 + 层索引重新收集 cubies
        _layerCubies.Clear();
        _layerHolder.localRotation = Quaternion.identity;
        for (int i = 0; i < _cubies.Count; i++)
        {
            var c = _cubies[i];
            if (c == null) continue;
            Vector3 lp = transform.InverseTransformPoint(c.position);
            float coord = Vector3.Dot(lp, m.axisLocal);
            int idx = Mathf.RoundToInt(coord / spacing);
            if (idx == m.layerIndex)
            {
                _layerCubies.Add(c);
                c.SetParent(_layerHolder, true);
            }
        }

        float startAngle = 0f;
        float targetAngle = -m.steps * 90f;
        Quaternion startQ = Quaternion.AngleAxis(startAngle, m.axisLocal);
        Quaternion endQ   = Quaternion.AngleAxis(targetAngle, m.axisLocal);
        float t = 0f;
        float dur = Mathf.Max(0.01f, snapDuration);
        while (t < dur)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / dur);
            _layerHolder.localRotation = Quaternion.Slerp(startQ, endQ, k);
            yield return null;
        }
        _layerHolder.localRotation = endQ;

        // reparent + 位置吸附
        for (int i = 0; i < _layerCubies.Count; i++)
        {
            var c = _layerCubies[i];
            if (c == null) continue;
            c.SetParent(transform, true);
            Vector3 p = c.localPosition;
            p.x = Mathf.Round(p.x / spacing) * spacing;
            p.y = Mathf.Round(p.y / spacing) * spacing;
            p.z = Mathf.Round(p.z / spacing) * spacing;
            c.localPosition = p;
        }
        _layerCubies.Clear();
        _layerHolder.localRotation = Quaternion.identity;
        _layerAngle = 0f;
        _mode = DragMode.None;
        _snapping = false;

        if (OnLayerUndone != null) OnLayerUndone();
    }

    /// <summary>
    /// 重置所有交互状态，把可能还在 LayerHolder 下的 cubie 还原到 cube root，
    /// 清空撤销历史。用于编辑器预览模式退出时恢复魔方。
    /// </summary>
    public void ResetInteraction()
    {
        StopAllCoroutines();
        _wholeSnapCo = null;
        _mode = DragMode.None;
        _snapping = false;

        if (_layerHolder != null)
        {
            for (int i = _cubies.Count - 1; i >= 0; i--)
            {
                if (_cubies[i] != null && _cubies[i].parent == _layerHolder)
                    _cubies[i].SetParent(transform, true);
            }
            _layerHolder.localRotation = Quaternion.identity;
        }
        _layerCubies.Clear();
        _layerAngle = 0f;
        _history.Clear();

        for (int i = 0; i < _wholeBtnActive.Length; i++) _wholeBtnActive[i] = false;
    }

    static Vector3 SnapToAxis(Vector3 v)
    {
        float ax = Mathf.Abs(v.x), ay = Mathf.Abs(v.y), az = Mathf.Abs(v.z);
        if (ax >= ay && ax >= az) return new Vector3(Mathf.Sign(v.x), 0, 0);
        if (ay >= ax && ay >= az) return new Vector3(0, Mathf.Sign(v.y), 0);
        return new Vector3(0, 0, Mathf.Sign(v.z));
    }
}
