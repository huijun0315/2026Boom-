using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 程序化绘制五角星的 UI Graphic，不依赖字体/贴图。
/// 直接用 color 属性改颜色。
/// </summary>
public class StarGraphic : MaskableGraphic
{
    [Range(3, 12)]
    public int points = 5;

    [Range(0.1f, 0.9f)]
    [Tooltip("内半径 / 外半径")]
    public float innerRatio = 0.45f;

    [Range(-180f, 180f)]
    public float rotationDeg = 0f;

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        var rect = GetPixelAdjustedRect();
        float cx = rect.x + rect.width * 0.5f;
        float cy = rect.y + rect.height * 0.5f;
        float outerR = Mathf.Min(rect.width, rect.height) * 0.5f;
        float innerR = outerR * Mathf.Clamp(innerRatio, 0.05f, 0.95f);

        int total = points * 2;
        float step = Mathf.PI * 2f / total;
        // 从正上方开始：-90 度 + 自定义旋转
        float startAngle = -Mathf.PI * 0.5f + rotationDeg * Mathf.Deg2Rad;

        // center
        UIVertex vCenter = UIVertex.simpleVert;
        vCenter.color = color;
        vCenter.position = new Vector3(cx, cy);
        vh.AddVert(vCenter);

        for (int i = 0; i < total; i++)
        {
            float r = (i % 2 == 0) ? outerR : innerR;
            float a = startAngle + i * step;
            UIVertex v = UIVertex.simpleVert;
            v.color = color;
            v.position = new Vector3(cx + Mathf.Cos(a) * r, cy + Mathf.Sin(a) * r);
            vh.AddVert(v);
        }

        // triangles: center, i+1, next+1
        for (int i = 0; i < total; i++)
        {
            int a = i + 1;
            int b = ((i + 1) % total) + 1;
            vh.AddTriangle(0, a, b);
        }
    }
}
