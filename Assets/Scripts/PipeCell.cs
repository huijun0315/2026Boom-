using UnityEngine;

public enum PipeKind
{
    /// <summary>起点：始终有水，有 1 个出口端点</summary>
    Start,
    /// <summary>终点：有 1 个入口端点，被水流填充时 HasWater 为 true</summary>
    End,
    /// <summary>直管：连接一对平行边的中点</summary>
    Straight,
    /// <summary>弯管：连接一对相邻边的中点（转角 90°）</summary>
    Bend,
    /// <summary>三通管：连接三条边的中点（T 型接头）</summary>
    Tee,
    /// <summary>十字管：连接四条边的中点（+ 型接头）</summary>
    Cross,
    /// <summary>二层起点：始终有水，1 个出口，容量为 2（可供 2 个终点用水，上面显示剩余容量）</summary>
    Start2,
    /// <summary>传送入口：水从此处进入，传送到同 portalGroup 的 PortalB 出口</summary>
    PortalA,
    /// <summary>传送出口：水从同 portalGroup 的 PortalA 传送到此处流出</summary>
    PortalB
}

/// <summary>
/// 单个"水管格子"。挂在 cube root 下某个 cubie 的子物体上，
/// 其 transform 的 forward = 该面的外法线，right = U 轴，up = V 轴。
/// </summary>
public class PipeCell : MonoBehaviour
{
    public PipeKind kind;
    [Tooltip("Straight: 0=沿 U, 1=沿 V。Bend/Tee/Start2: 0..3 象限。Start/End/PortalA/PortalB: 0..3 方向。Cross: 0 (无需旋转)")]
    [Range(0, 3)] public int orientation;
    [Tooltip("传送门配对组号（同组号的 PortalA 和 PortalB 配对）")]
    public int portalGroup;

    [System.NonSerialized] public bool hasWater;

    // 视觉
    public Renderer[] pipeRenderers;
    [System.NonSerialized] public Material matEmpty;
    [System.NonSerialized] public Material matWater;
    [System.NonSerialized] public Material matStart;
    [System.NonSerialized] public Material matEnd;
    [System.NonSerialized] public Material matPortal;

    /// <summary>起点剩余容量（Start=1, Start2=2），由 PipePuzzle.DoRecompute 更新。</summary>
    [System.NonSerialized] public int remainingCapacity;
    /// <summary>Start2 上显示容量的 3D 文字。</summary>
    [System.NonSerialized] public TextMesh capacityLabel;

    /// <summary>
    /// 返回本格面上（面局部 2D）所有端点坐标。±0.5 为该面四条边的中点。
    /// </summary>
    public Vector2[] LocalEndpoints2D()
    {
        switch (kind)
        {
            case PipeKind.Straight:
                if (orientation == 0) return new[] { new Vector2(-0.5f, 0f), new Vector2(0.5f, 0f) };
                return new[] { new Vector2(0f, -0.5f), new Vector2(0f, 0.5f) };

            case PipeKind.Bend:
                switch (orientation & 3)
                {
                    case 0: return new[] { new Vector2(-0.5f, 0f), new Vector2(0f, 0.5f) };  // -U & +V
                    case 1: return new[] { new Vector2( 0.5f, 0f), new Vector2(0f, 0.5f) };  // +U & +V
                    case 2: return new[] { new Vector2( 0.5f, 0f), new Vector2(0f, -0.5f) }; // +U & -V
                    default: return new[] { new Vector2(-0.5f, 0f), new Vector2(0f, -0.5f) }; // -U & -V
                }

            case PipeKind.Tee:
                switch (orientation & 3)
                {
                    case 0: return new[] { new Vector2(-0.5f, 0f), new Vector2(0f, 0.5f), new Vector2(0f, -0.5f) }; // 堵 +U
                    case 1: return new[] { new Vector2( 0.5f, 0f), new Vector2(-0.5f, 0f), new Vector2(0f, -0.5f) };  // 堵 +V
                    case 2: return new[] { new Vector2( 0.5f, 0f), new Vector2(0f, 0.5f), new Vector2(0f, -0.5f) };   // 堵 -U
                    default: return new[] { new Vector2( 0.5f, 0f), new Vector2(-0.5f, 0f), new Vector2(0f, 0.5f) };  // 堵 -V
                }

            case PipeKind.Cross:
                return new[] { new Vector2(-0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, -0.5f), new Vector2(0f, 0.5f) };

            case PipeKind.PortalA:
            case PipeKind.PortalB:
            case PipeKind.Start2:
            case PipeKind.Start:
            case PipeKind.End:
                switch (orientation & 3)
                {
                    case 0: return new[] { new Vector2( 0.5f, 0f) }; // +U
                    case 1: return new[] { new Vector2( 0f, 0.5f) }; // +V
                    case 2: return new[] { new Vector2(-0.5f, 0f) }; // -U
                    default: return new[] { new Vector2( 0f, -0.5f) }; // -V
                }

        }
        return new Vector2[0];
    }

    public void ApplyWaterVisual()
    {
        if (pipeRenderers == null) return;
        Material m;
        if (kind == PipeKind.Start || kind == PipeKind.Start2) m = matStart != null ? matStart : matWater;
        else if (kind == PipeKind.End)    m = hasWater ? (matEnd != null ? matEnd : matWater) : matEmpty;
        else if (kind == PipeKind.PortalA || kind == PipeKind.PortalB)
            m = hasWater ? matWater : (matPortal != null ? matPortal : matEmpty);
        else                              m = hasWater ? matWater : matEmpty;
        for (int i = 0; i < pipeRenderers.Length; i++)
            if (pipeRenderers[i] != null) pipeRenderers[i].sharedMaterial = m;
        UpdateCapacityDisplay();
    }

    /// <summary>返回该类型起点的最大容量（Start=1, Start2=2，其它=0）。</summary>
    public static int CapacityForKind(PipeKind k)
    {
        if (k == PipeKind.Start2) return 2;
        if (k == PipeKind.Start) return 1;
        return 0;
    }

    /// <summary>更新 Start/Start2 上的容量数字显示。</summary>
    public void UpdateCapacityDisplay()
    {
        if (capacityLabel == null) return;
        // 传送门标签由 BuildVisuals 写入，不在此覆盖
        if (kind == PipeKind.PortalA || kind == PipeKind.PortalB) return;
        int cap = CapacityForKind(kind);
        if (cap > 0)
            capacityLabel.text = remainingCapacity.ToString();
        else
            capacityLabel.text = "";
    }

    void LateUpdate()
    {
        if (capacityLabel == null) return;
        var cam = Camera.main;
        if (cam == null) return;

        // 标签贴在面上：forward 始终 = 面法线，只绕法线旋转使文字从摄像头看是正的
        Vector3 faceNormal = transform.forward;
        Vector3 camUp = cam.transform.up;
        // 把摄像头 up 投影到面平面上，作为文字的"上"方向
        Vector3 projectedUp = Vector3.ProjectOnPlane(camUp, faceNormal);
        if (projectedUp.sqrMagnitude < 0.001f) return;
        capacityLabel.transform.rotation = Quaternion.LookRotation(faceNormal, projectedUp);
        // 镜像 X 轴：标签正面朝内，摄像头从外面看是背面，X 取反补偿镜像
        Vector3 s = capacityLabel.transform.localScale;
        s.x = -Mathf.Abs(s.x);
        capacityLabel.transform.localScale = s;
    }
}
