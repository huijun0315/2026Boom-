using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 管道谜题系统：按照 CellConfig 列表在魔方各 cubie 的某一面上放置 PipeCell。
/// 监听 RubikCube.OnLayerSnapped，在每次层旋转完成后从 Start 出发 BFS，
/// 更新每个 PipeCell 的 hasWater 与材质。
/// 放在 RubikCube 所在 GameObject 上即可自动挂钩。
/// </summary>
[RequireComponent(typeof(RubikCube))]
public class PipePuzzle : MonoBehaviour
{
    [System.Serializable]
    public struct CellConfig
    {
        public Vector3Int cubieCoord;  // 初始 cubie 网格坐标，每轴 ∈ {-1, 0, 1}
        public Vector3Int faceNormal;  // 面外法线，单位轴：±X/±Y/±Z
        public PipeKind kind;
        [Range(0, 3)] public int orientation;
    }

    [Header("References")]
    public RubikCube cube;

    [Header("Level")]
    [Tooltip("关卡的管道格列表。若为空且 buildSampleIfEmpty 打开，则构建示例关卡")]
    public List<CellConfig> cells = new List<CellConfig>();
    public bool buildSampleIfEmpty = true;

    [Header("Visual")]
    [Range(0.02f, 0.3f)] public float pipeRadius = 0.10f;
    [Tooltip("管道相对贴纸表面的抬起高度，防止 Z-fight")]
    public float pipeOffset = 0.015f;

    [Header("Colors")]
    public Color emptyColor = new Color(0.45f, 0.45f, 0.5f);
    public Color waterColor = new Color(0.20f, 0.70f, 1.00f);
    public Color startColor = new Color(0.20f, 1.00f, 0.35f);
    public Color endColor   = new Color(1.00f, 0.35f, 0.35f);

    private Material _matEmpty, _matWater, _matStart, _matEnd;
    private readonly List<PipeCell> _pipeCells = new List<PipeCell>();
    private bool _built;

    /// <summary>跨场景传递：点击关卡按钮时设置，本组件在 Start 时若非空则自动加载对应关卡。</summary>
    public static string PendingLevelId;
    public IReadOnlyList<PipeCell> SpawnedCells { get { return _pipeCells; } }

    /// <summary>当前已载入的关卡 id（从 PendingLevelId 或 LoadFromData 填入）。</summary>
    public string loadedLevelId;
    public string loadedDisplayName;

    /// <summary>是否通关：至少有 1 个 End，且所有 End 都 hasWater。</summary>
    public bool IsSolved { get; private set; }

    /// <summary>通关瞬间触发（从未通关切换到通关）。</summary>
    public event System.Action OnSolved;

    /// <summary>当前关卡步数限制（>0 启用挑战）。</summary>
    public int MoveLimit { get; private set; }
    /// <summary>本次已用步数。</summary>
    public int MoveCount { get; private set; }
    public event System.Action OnMoveCountChanged;
    /// <summary>通关时是否在步数限制内拿到星星（仅当 MoveLimit>0 且 MoveCount<=MoveLimit）。</summary>
    public bool EarnedStarThisRun { get; private set; }

    void Reset()
    {
        cube = GetComponent<RubikCube>();
    }

    void Awake()
    {
        if (cube == null) cube = GetComponent<RubikCube>();
    }

    void OnEnable()
    {
        if (cube == null) cube = GetComponent<RubikCube>();
        if (cube != null)
        {
            cube.OnBuilt        += HandleCubeBuilt;
            cube.OnLayerSnapped += OnLayerRotated;
            cube.OnLayerUndone  += OnLayerUndoneHandler;
        }
    }

    void OnDisable()
    {
        if (cube != null)
        {
            cube.OnBuilt        -= HandleCubeBuilt;
            cube.OnLayerSnapped -= OnLayerRotated;
            cube.OnLayerUndone  -= OnLayerUndoneHandler;
        }
    }

    /// <summary>撤销完成后：步数-1 并重算水流（不触发通关事件）。</summary>
    void OnLayerUndoneHandler()
    {
        MoveCount = Mathf.Max(0, MoveCount - 1);
        if (OnMoveCountChanged != null) OnMoveCountChanged();
        DoRecompute(fireEvent: false);
    }

    public bool CanUndo { get { return cube != null && cube.CanUndo; } }

    /// <summary>外部 UI 调用：撤销一步。</summary>
    public bool UndoLastMove()
    {
        return cube != null && cube.UndoLastRotation();
    }

    /// <summary>由玩家旋转触发：步数+1，重算后允许触发 OnSolved。</summary>
    void OnLayerRotated()
    {
        MoveCount++;
        if (OnMoveCountChanged != null) OnMoveCountChanged();
        DoRecompute(fireEvent: true);
    }

    public void ResetMoveCount()
    {
        MoveCount = 0;
        EarnedStarThisRun = false;
        if (OnMoveCountChanged != null) OnMoveCountChanged();
    }

    public void SetMoveLimit(int v)
    {
        MoveLimit = Mathf.Max(0, v);
        if (OnMoveCountChanged != null) OnMoveCountChanged();
    }

    IEnumerator Start()
    {
        // 等一帧，确保 RubikCube.Start 先跑完（或者它已通过 OnBuilt 通知了我们）
        yield return null;
        if (!_built && cube != null && cube.IsBuilt) HandleCubeBuilt();
    }

    void HandleCubeBuilt()
    {
        if (_built) return;
        _built = true;
        PrepareMaterials();

        // 跨场景指定关卡优先
        if (!string.IsNullOrEmpty(PendingLevelId))
        {
            var data = LevelStore.Load(PendingLevelId);
            if (data != null) LoadFromData(data, false);
            else Debug.LogWarning("[PipePuzzle] PendingLevelId not found: " + PendingLevelId);
            loadedLevelId = PendingLevelId;
            PendingLevelId = null;
        }

        if ((cells == null || cells.Count == 0) && buildSampleIfEmpty) BuildSampleLevel();
        BuildVisuals();
        Recompute();
    }

    // -----------------------------------------------------------------------
    // 关卡数据 I/O
    // -----------------------------------------------------------------------

    /// <summary>用 LevelData 替换当前 cells。若 rebuild=true 会立刻重建可视化。</summary>
    public void LoadFromData(LevelData data, bool rebuild = true)
    {
        if (data == null) return;
        if (!string.IsNullOrEmpty(data.id)) loadedLevelId = data.id;
        loadedDisplayName = data.displayName;
        MoveLimit = Mathf.Max(0, data.moveLimit);
        MoveCount = 0;
        EarnedStarThisRun = false;
        if (OnMoveCountChanged != null) OnMoveCountChanged();
        cells.Clear();
        if (data.cells != null)
        {
            for (int i = 0; i < data.cells.Count; i++)
            {
                var d = data.cells[i];
                if (!IsValidOuterFace(d.cubieCoord, d.faceNormal))
                {
                    Debug.LogWarning("[PipePuzzle] 关卡数据含内表面，已跳过：" + d.cubieCoord + " 面 " + d.faceNormal);
                    continue;
                }
                cells.Add(new CellConfig
                {
                    cubieCoord = d.cubieCoord,
                    faceNormal = d.faceNormal,
                    kind = (PipeKind)d.kind,
                    orientation = d.orientation,
                });
            }
        }
        if (rebuild && _built) Rebuild();
    }

    public LevelData ExportData(string id, string displayName)
    {
        var d = new LevelData { id = id, displayName = displayName, moveLimit = Mathf.Max(0, MoveLimit) };
        for (int i = 0; i < cells.Count; i++)
        {
            var c = cells[i];
            d.cells.Add(new LevelData.CellData
            {
                cubieCoord = c.cubieCoord,
                faceNormal = c.faceNormal,
                kind = (int)c.kind,
                orientation = c.orientation,
            });
        }
        return d;
    }

    /// <summary>清除所有 PipeCell GameObject 并根据当前 cells 重新生成可视化，然后运算水流。</summary>
    public void Rebuild()
    {
        for (int i = 0; i < _pipeCells.Count; i++)
            if (_pipeCells[i] != null && _pipeCells[i].gameObject != null)
                Destroy(_pipeCells[i].gameObject);
        _pipeCells.Clear();
        if (_matEmpty == null) PrepareMaterials();
        BuildVisuals();
        Recompute();
    }

    /// <summary>判断 cubieCoord + faceNormal 是否为魔方外表面（只有外表面才能放管道）。</summary>
    public static bool IsValidOuterFace(Vector3Int cubieCoord, Vector3Int faceNormal)
    {
        if (faceNormal.x ==  1) return cubieCoord.x ==  1;
        if (faceNormal.x == -1) return cubieCoord.x == -1;
        if (faceNormal.y ==  1) return cubieCoord.y ==  1;
        if (faceNormal.y == -1) return cubieCoord.y == -1;
        if (faceNormal.z ==  1) return cubieCoord.z ==  1;
        if (faceNormal.z == -1) return cubieCoord.z == -1;
        return false;
    }

    /// <summary>查找（或为空返回 -1）某个 cubieCoord + faceNormal 位置的 cell 索引。</summary>
    public int IndexOfCell(Vector3Int cubieCoord, Vector3Int faceNormal)
    {
        for (int i = 0; i < cells.Count; i++)
            if (cells[i].cubieCoord == cubieCoord && cells[i].faceNormal == faceNormal) return i;
        return -1;
    }

    public void PlaceOrReplace(Vector3Int cubieCoord, Vector3Int faceNormal, PipeKind kind, int orientation)
    {
        if (!IsValidOuterFace(cubieCoord, faceNormal))
        {
            Debug.LogWarning("[PipePuzzle] 拒绝放置：" + cubieCoord + " 面 " + faceNormal + " 不是外表面");
            return;
        }
        int i = IndexOfCell(cubieCoord, faceNormal);
        var cfg = new CellConfig { cubieCoord = cubieCoord, faceNormal = faceNormal, kind = kind, orientation = orientation };
        if (i >= 0) cells[i] = cfg; else cells.Add(cfg);
        if (_built) Rebuild();
    }

    public void RemoveAt(Vector3Int cubieCoord, Vector3Int faceNormal)
    {
        int i = IndexOfCell(cubieCoord, faceNormal);
        if (i < 0) return;
        cells.RemoveAt(i);
        if (_built) Rebuild();
    }

    public bool CycleOrientation(Vector3Int cubieCoord, Vector3Int faceNormal)
    {
        int i = IndexOfCell(cubieCoord, faceNormal);
        if (i < 0) return false;
        var c = cells[i];
        int mod;
        if (c.kind == PipeKind.Straight) mod = 2;
        else if (c.kind == PipeKind.Cross) mod = 1; // 十字管无需旋转
        else mod = 4; // Start/End/Bend/Tee/Start2
        c.orientation = (c.orientation + 1) % mod;
        cells[i] = c;
        if (_built) Rebuild();
        return true;
    }

    // -----------------------------------------------------------------------
    // 关卡 API
    // -----------------------------------------------------------------------

    public void AddCell(Vector3Int cubieCoord, Vector3Int faceNormal, PipeKind kind, int orientation)
    {
        if (!IsValidOuterFace(cubieCoord, faceNormal))
        {
            Debug.LogWarning("[PipePuzzle] 拒绝添加：" + cubieCoord + " 面 " + faceNormal + " 不是外表面");
            return;
        }
        cells.Add(new CellConfig { cubieCoord = cubieCoord, faceNormal = faceNormal, kind = kind, orientation = orientation });
    }

    public void ClearLevel()
    {
        cells.Clear();
        for (int i = 0; i < _pipeCells.Count; i++)
            if (_pipeCells[i] != null && _pipeCells[i].gameObject != null)
                Destroy(_pipeCells[i].gameObject);
        _pipeCells.Clear();
    }

    /// <summary>默认示例：+Y 顶面沿 X 轴一条直线：起点 + 3 直管 + 终点（已解）</summary>
    void BuildSampleLevel()
    {
        var up = new Vector3Int(0, 1, 0);
        cells.Clear();
        cells.Add(new CellConfig { cubieCoord = new Vector3Int(-1, 1, 0), faceNormal = up, kind = PipeKind.Start,    orientation = 0 });
        cells.Add(new CellConfig { cubieCoord = new Vector3Int( 0, 1, 0), faceNormal = up, kind = PipeKind.Straight, orientation = 0 });
        cells.Add(new CellConfig { cubieCoord = new Vector3Int( 1, 1, 0), faceNormal = up, kind = PipeKind.Straight, orientation = 0 });
        cells.Add(new CellConfig { cubieCoord = new Vector3Int( 1, 1, 1), faceNormal = up, kind = PipeKind.Bend,     orientation = 3 });
        cells.Add(new CellConfig { cubieCoord = new Vector3Int( 0, 1, 1), faceNormal = up, kind = PipeKind.End,      orientation = 0 });
    }

    // -----------------------------------------------------------------------
    // 视觉 & 连通计算
    // -----------------------------------------------------------------------

    static (Vector3 U, Vector3 V) FaceBasis(Vector3Int n)
    {
        // 约定：U × V = normal（右手系），便于后面 LookRotation + Cross 基矩阵推导一致
        if (n.x ==  1) return (new Vector3(0, 0, -1), new Vector3(0, 1, 0));
        if (n.x == -1) return (new Vector3(0, 0,  1), new Vector3(0, 1, 0));
        if (n.y ==  1) return (new Vector3(1, 0,  0), new Vector3(0, 0, -1));
        if (n.y == -1) return (new Vector3(1, 0,  0), new Vector3(0, 0,  1));
        if (n.z ==  1) return (new Vector3(1, 0,  0), new Vector3(0, 1, 0));
        return (new Vector3(-1, 0, 0), new Vector3(0, 1, 0));
    }

    void PrepareMaterials()
    {
        Shader sh = Shader.Find("Standard");
        if (sh == null) sh = Shader.Find("Universal Render Pipeline/Lit");
        if (sh == null) sh = Shader.Find("Unlit/Color");
        _matEmpty = new Material(sh) { name = "PipeEmpty", color = emptyColor };
        _matWater = new Material(sh) { name = "PipeWater", color = waterColor };
        _matStart = new Material(sh) { name = "PipeStart", color = startColor };
        _matEnd   = new Material(sh) { name = "PipeEnd",   color = endColor   };
        // 管道材质：光滑半金属质感
        foreach (var m in new[] { _matEmpty, _matWater, _matStart, _matEnd })
        {
            if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", 0.65f);
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0.65f);
            if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", 0.15f);
        }
        // 水管发光
        if (_matWater.HasProperty("_EmissionColor"))
        {
            _matWater.EnableKeyword("_EMISSION");
            _matWater.SetColor("_EmissionColor", waterColor * 0.3f);
        }
        if (_matStart.HasProperty("_EmissionColor"))
        {
            _matStart.EnableKeyword("_EMISSION");
            _matStart.SetColor("_EmissionColor", startColor * 0.2f);
        }
        if (_matEnd.HasProperty("_EmissionColor"))
        {
            _matEnd.EnableKeyword("_EMISSION");
            _matEnd.SetColor("_EmissionColor", endColor * 0.2f);
        }
    }

    Transform FindCubieByInitialCoord(Vector3Int coord)
    {
        if (cube == null) return null;
        float sp = cube.spacing;
        Vector3 target = new Vector3(coord.x, coord.y, coord.z) * sp;
        foreach (Transform c in cube.transform)
        {
            if (c.name == "LayerHolder") continue;
            if ((c.localPosition - target).sqrMagnitude < 0.01f) return c;
        }
        return null;
    }

    void BuildVisuals()
    {
        _pipeCells.Clear();
        for (int i = 0; i < cells.Count; i++)
        {
            var cfg = cells[i];
            var cubie = FindCubieByInitialCoord(cfg.cubieCoord);
            if (cubie == null)
            {
                Debug.LogWarning("[PipePuzzle] Cubie not found: " + cfg.cubieCoord);
                continue;
            }
            var basis = FaceBasis(cfg.faceNormal);
            Vector3 n = new Vector3(cfg.faceNormal.x, cfg.faceNormal.y, cfg.faceNormal.z);

            var cellGO = new GameObject("PipeCell_" + cfg.cubieCoord + "_" + cfg.faceNormal);
            cellGO.transform.SetParent(cubie, false);
            cellGO.transform.localPosition = n * (0.5f + pipeOffset);
            // 让 cell 局部坐标：forward=n, up=V, right=U
            cellGO.transform.localRotation = Quaternion.LookRotation(n, basis.V);

            var pc = cellGO.AddComponent<PipeCell>();
            pc.kind = cfg.kind;
            pc.orientation = cfg.orientation;
            pc.matEmpty = _matEmpty;
            pc.matWater = _matWater;
            pc.matStart = _matStart;
            pc.matEnd   = _matEnd;

            var rends = new List<Renderer>();
            var eps = pc.LocalEndpoints2D();
            for (int e = 0; e < eps.Length; e++)
            {
                Vector3 epLocal = new Vector3(eps[e].x, eps[e].y, 0f);
                float len = epLocal.magnitude;
                if (len < 1e-4f) continue;
                // 高段数圆柱，管道更圆滑
                var cylGO = new GameObject("PipeTube");
                cylGO.transform.SetParent(cellGO.transform, false);
                cylGO.transform.localPosition = epLocal * 0.5f;
                cylGO.transform.localRotation = Quaternion.FromToRotation(Vector3.up, epLocal / len);
                var mf = cylGO.AddComponent<MeshFilter>();
                mf.sharedMesh = CreateCylinderMesh(16, pipeRadius, len);
                var mr = cylGO.AddComponent<MeshRenderer>();
                rends.Add(mr);
            }

            // 起点/终点/二层起点加一个圆滑标记
            if (cfg.kind == PipeKind.Start || cfg.kind == PipeKind.End || cfg.kind == PipeKind.Start2)
            {
                var markerGO = new GameObject("Marker");
                markerGO.transform.SetParent(cellGO.transform, false);
                markerGO.transform.localPosition = Vector3.zero;
                // 圆柱形标记（面法线方向为 forward）
                var mmf = markerGO.AddComponent<MeshFilter>();
                mmf.sharedMesh = CreateCylinderMesh(16, pipeRadius * 1.6f, pipeRadius * 1.6f);
                var mmr = markerGO.AddComponent<MeshRenderer>();
                mmr.sharedMaterial = (cfg.kind == PipeKind.End) ? _matEnd : _matStart;
                rends.Add(mmr);

                // 起点/二层起点上显示容量数字
                int cap = PipeCell.CapacityForKind(cfg.kind);
                if (cap > 0)
                {
                    var labelGO = new GameObject("CapacityLabel");
                    labelGO.transform.SetParent(cellGO.transform, false);
                    labelGO.transform.localPosition = Vector3.forward * 0.01f; // 略微浮起防 Z-fight
                    var tm = labelGO.AddComponent<TextMesh>();
                    tm.text = cap.ToString();
                    tm.fontSize = 48;
                    tm.characterSize = pipeRadius * 0.7f;
                    tm.color = Color.white;
                    tm.anchor = TextAnchor.MiddleCenter;
                    tm.alignment = TextAlignment.Center;
                    pc.capacityLabel = tm;
                }
            }

            // 三通管/十字管中心加一个球形接头
            if (cfg.kind == PipeKind.Tee || cfg.kind == PipeKind.Cross)
            {
                // 高段数球形接头
                var jGO = new GameObject("Junction");
                jGO.transform.SetParent(cellGO.transform, false);
                jGO.transform.localPosition = Vector3.zero;
                jGO.transform.localScale = Vector3.one * (pipeRadius * 3.6f);
                var jmf = jGO.AddComponent<MeshFilter>();
                jmf.sharedMesh = CreateSphereMesh(16, 12);
                var jmr = jGO.AddComponent<MeshRenderer>();
                jmr.sharedMaterial = (cfg.kind == PipeKind.Tee || cfg.kind == PipeKind.Cross) ? _matEmpty : _matWater;
                rends.Add(jmr);
            }

            pc.pipeRenderers = rends.ToArray();
            _pipeCells.Add(pc);
        }
    }

    /// <summary>静默重算（不触发 OnSolved 事件），供内部/重建/重玩使用。</summary>
    public void Recompute() { DoRecompute(fireEvent: false); }

    // ── 高段数 Mesh 生成 ──

    static Mesh CreateCylinderMesh(int segments, float radius, float height)
    {
        var mesh = new Mesh();
        var verts = new List<Vector3>();
        var normals = new List<Vector3>();
        var uvs = new List<Vector2>();
        var tris = new List<int>();

        float halfH = height * 0.5f;
        int vCount = (segments + 1) * 2;

        // 侧面
        for (int i = 0; i <= segments; i++)
        {
            float a = (float)i / segments * Mathf.PI * 2f;
            float cos = Mathf.Cos(a), sin = Mathf.Sin(a);
            float u = (float)i / segments;
            // bottom ring
            verts.Add(new Vector3(cos * radius, -halfH, sin * radius));
            normals.Add(new Vector3(cos, 0, sin));
            uvs.Add(new Vector2(u, 0f));
            // top ring
            verts.Add(new Vector3(cos * radius, halfH, sin * radius));
            normals.Add(new Vector3(cos, 0, sin));
            uvs.Add(new Vector2(u, 1f));
        }
        for (int i = 0; i < segments; i++)
        {
            int bl = i * 2, tl = i * 2 + 1, br = (i + 1) * 2, tr_ = (i + 1) * 2 + 1;
            tris.Add(bl); tris.Add(tl); tris.Add(br);
            tris.Add(br); tris.Add(tl); tris.Add(tr_);
        }

        // top cap
        int topCenter = verts.Count;
        verts.Add(new Vector3(0, halfH, 0)); normals.Add(Vector3.up); uvs.Add(new Vector2(0.5f, 0.5f));
        for (int i = 0; i <= segments; i++)
        {
            float a = (float)i / segments * Mathf.PI * 2f;
            verts.Add(new Vector3(Mathf.Cos(a) * radius, halfH, Mathf.Sin(a) * radius));
            normals.Add(Vector3.up);
            uvs.Add(new Vector2(0.5f + Mathf.Cos(a) * 0.5f, 0.5f + Mathf.Sin(a) * 0.5f));
        }
        for (int i = 1; i <= segments; i++)
        {
            tris.Add(topCenter); tris.Add(topCenter + i + 1); tris.Add(topCenter + i);
        }

        // bottom cap
        int botCenter = verts.Count;
        verts.Add(new Vector3(0, -halfH, 0)); normals.Add(Vector3.down); uvs.Add(new Vector2(0.5f, 0.5f));
        for (int i = 0; i <= segments; i++)
        {
            float a = (float)i / segments * Mathf.PI * 2f;
            verts.Add(new Vector3(Mathf.Cos(a) * radius, -halfH, Mathf.Sin(a) * radius));
            normals.Add(Vector3.down);
            uvs.Add(new Vector2(0.5f + Mathf.Cos(a) * 0.5f, 0.5f + Mathf.Sin(a) * 0.5f));
        }
        for (int i = 1; i <= segments; i++)
        {
            tris.Add(botCenter); tris.Add(botCenter + i); tris.Add(botCenter + i + 1);
        }

        mesh.SetVertices(verts); mesh.SetNormals(normals); mesh.SetUVs(0, uvs); mesh.SetTriangles(tris, 0);
        return mesh;
    }

    static Mesh CreateSphereMesh(int latSeg, int lonSeg)
    {
        var mesh = new Mesh();
        var verts = new List<Vector3>();
        var normals = new List<Vector3>();
        var uvs = new List<Vector2>();
        var tris = new List<int>();

        for (int lat = 0; lat <= latSeg; lat++)
        {
            float theta = (float)lat / latSeg * Mathf.PI;
            float sinT = Mathf.Sin(theta), cosT = Mathf.Cos(theta);
            for (int lon = 0; lon <= lonSeg; lon++)
            {
                float phi = (float)lon / lonSeg * Mathf.PI * 2f;
                float sinP = Mathf.Sin(phi), cosP = Mathf.Cos(phi);
                var v = new Vector3(sinT * cosP, cosT, sinT * sinP) * 0.5f;
                verts.Add(v); normals.Add(v.normalized);
                uvs.Add(new Vector2((float)lon / lonSeg, (float)lat / latSeg));
            }
        }
        for (int lat = 0; lat < latSeg; lat++)
        {
            for (int lon = 0; lon < lonSeg; lon++)
            {
                int a = lat * (lonSeg + 1) + lon;
                int b = a + lonSeg + 1;
                tris.Add(a); tris.Add(b); tris.Add(a + 1);
                tris.Add(a + 1); tris.Add(b); tris.Add(b + 1);
            }
        }
        mesh.SetVertices(verts); mesh.SetNormals(normals); mesh.SetUVs(0, uvs); mesh.SetTriangles(tris, 0);
        return mesh;
    }

    void DoRecompute(bool fireEvent)
    {
        if (cube == null || _pipeCells.Count == 0) { IsSolved = false; return; }

        int n = _pipeCells.Count;
        var eps = new List<Vector3[]>(n);
        for (int i = 0; i < n; i++)
        {
            var cell = _pipeCells[i];
            var e2 = cell.LocalEndpoints2D();
            var arr = new Vector3[e2.Length];
            for (int k = 0; k < e2.Length; k++)
            {
                Vector3 epCellLocal = new Vector3(e2[k].x, e2[k].y, 0f);
                Vector3 world = cell.transform.TransformPoint(epCellLocal);
                Vector3 rootLocal = cube.transform.InverseTransformPoint(world);
                // 吸附到 0.5 网格，吸收浮点漂移
                arr[k] = new Vector3(
                    Mathf.Round(rootLocal.x * 2f) * 0.5f,
                    Mathf.Round(rootLocal.y * 2f) * 0.5f,
                    Mathf.Round(rootLocal.z * 2f) * 0.5f);
            }
            eps.Add(arr);
        }

        // ----- 全局 BFS：确定哪些格有水 -----
        var filled = new bool[n];
        var q = new Queue<int>();
        for (int i = 0; i < n; i++)
            if (_pipeCells[i].kind == PipeKind.Start || _pipeCells[i].kind == PipeKind.Start2) { filled[i] = true; q.Enqueue(i); }

        while (q.Count > 0)
        {
            int u = q.Dequeue();
            var ua = eps[u];
            for (int v = 0; v < n; v++)
            {
                if (filled[v] || v == u) continue;
                var va = eps[v];
                bool connect = false;
                for (int i = 0; i < ua.Length && !connect; i++)
                    for (int j = 0; j < va.Length; j++)
                        if ((ua[i] - va[j]).sqrMagnitude < 0.02f) { connect = true; break; }
                if (connect) { filled[v] = true; q.Enqueue(v); }
            }
        }

        // ----- 按源 BFS：统计每个起点能到达的终点数 -----
        var sourceIndices = new List<int>();
        for (int i = 0; i < n; i++)
            if (_pipeCells[i].kind == PipeKind.Start || _pipeCells[i].kind == PipeKind.Start2)
                sourceIndices.Add(i);

        // 统计每个有水终点被几个起点可达（用于分配）
        var endsReachedBySources = new int[n]; // 仅 End 格有效
        var endsPerSource = new int[sourceIndices.Count];
        for (int s = 0; s < sourceIndices.Count; s++)
        {
            int src = sourceIndices[s];
            var visited = new bool[n];
            visited[src] = true;
            var sq = new Queue<int>();
            sq.Enqueue(src);
            while (sq.Count > 0)
            {
                int u = sq.Dequeue();
                if (_pipeCells[u].kind == PipeKind.End) { endsPerSource[s]++; endsReachedBySources[u]++; }
                var ua = eps[u];
                for (int v = 0; v < n; v++)
                {
                    if (visited[v] || v == u) continue;
                    var va = eps[v];
                    bool connect = false;
                    for (int i = 0; i < ua.Length && !connect; i++)
                        for (int j = 0; j < va.Length; j++)
                            if ((ua[i] - va[j]).sqrMagnitude < 0.02f) { connect = true; break; }
                    if (connect) { visited[v] = true; sq.Enqueue(v); }
                }
            }
        }

        // ----- 更新每个起点的剩余容量 -----
        // 容量约束仅对 Start2 生效（最多供 2 个终点，不能分三条路）
        // 普通 Start 无硬性容量限制
        bool capacityOk = true;
        for (int s = 0; s < sourceIndices.Count; s++)
        {
            var cell = _pipeCells[sourceIndices[s]];
            int cap = PipeCell.CapacityForKind(cell.kind);
            cell.remainingCapacity = Mathf.Max(0, cap - endsPerSource[s]);
            // 仅 Start2 有硬性容量上限（2），普通 Start 不限制
            if (cell.kind == PipeKind.Start2 && endsPerSource[s] > cap)
                capacityOk = false;
        }

        // ----- 应用视觉 -----
        for (int i = 0; i < n; i++)
        {
            _pipeCells[i].hasWater = filled[i];
            _pipeCells[i].ApplyWaterVisual();
        }

        // ----- 通关判定：所有终点有水 -----
        int endCount = 0, endWithWater = 0;
        for (int i = 0; i < n; i++)
        {
            if (_pipeCells[i].kind == PipeKind.End)
            {
                endCount++;
                if (_pipeCells[i].hasWater) endWithWater++;
            }
        }
        bool solved = endCount > 0 && endWithWater == endCount;
        bool wasSolved = IsSolved;
        IsSolved = solved;

        if (solved && !wasSolved)
        {
            EarnedStarThisRun = MoveLimit > 0 && MoveCount <= MoveLimit;
            if (!string.IsNullOrEmpty(loadedLevelId))
            {
                string key = "star_" + loadedLevelId;
                int prev = PlayerPrefs.GetInt(key, 0);
                int now = EarnedStarThisRun ? 1 : 0;
                if (now > prev) { PlayerPrefs.SetInt(key, now); PlayerPrefs.Save(); }
            }
            // 通知引导弹窗管理器检查星星数触发
            if (TutorialPopupManager.Instance != null)
                TutorialPopupManager.Instance.NotifyStarCountChanged();
            if (fireEvent && OnSolved != null) OnSolved();
        }
    }

    // -----------------------------------------------------------------------
    // 重玩 / 下一关
    // -----------------------------------------------------------------------

    /// <summary>
    /// 重玩当前关卡：重建魔方（撤销所有层旋转），保持当前 cells 配置并重新生成管道与水流。
    /// </summary>
    public void RestartLevel()
    {
        if (cube == null) return;
        cube.transform.rotation = Quaternion.identity;
        cube.Build();
        _pipeCells.Clear();
        IsSolved = false;
        MoveCount = 0;
        EarnedStarThisRun = false;
        if (OnMoveCountChanged != null) OnMoveCountChanged();
        if (_matEmpty == null) PrepareMaterials();
        BuildVisuals();
        Recompute();
    }

    /// <summary>
    /// 尝试进入下一关：解析 loadedLevelId 末尾数字 +1，若 Resources/Levels/ 下存在就切场景载入。
    /// 返回 false 表示没有下一关。
    /// </summary>
    public bool LoadNextLevel(string sceneName = "CubeScene")
    {
        string nextId = ComputeNextLevelId(loadedLevelId);
        if (string.IsNullOrEmpty(nextId)) return false;
        var data = LevelStore.Load(nextId);
        if (data == null) return false;

        PendingLevelId = nextId;
        if (SceneTransitioner.Instance != null)
            SceneTransitioner.Instance.LoadSceneWithFade(sceneName);
        else
            UnityEngine.SceneManagement.SceneManager.LoadScene(sceneName);
        return true;
    }

    static string ComputeNextLevelId(string id)
    {
        if (string.IsNullOrEmpty(id)) return null;
        int i = id.Length - 1;
        while (i >= 0 && char.IsDigit(id[i])) i--;
        if (i == id.Length - 1) return null; // 末尾不是数字
        string prefix = id.Substring(0, i + 1);
        int num;
        if (!int.TryParse(id.Substring(i + 1), out num)) return null;
        return prefix + (num + 1);
    }
}
