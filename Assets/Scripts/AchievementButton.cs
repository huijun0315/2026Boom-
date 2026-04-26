using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Image))]
[RequireComponent(typeof(CanvasGroup))]
public class AchievementButton : MonoBehaviour
{
    [Tooltip("是否激活：false=灰色未激活，true=有色激活")]
    public bool isActive = false;

    [Tooltip("激活状态颜色")]
    public Color activeColor = new Color(0.85f, 0.65f, 0.15f);

    [Tooltip("未激活状态颜色")]
    public Color inactiveColor = new Color(0.45f, 0.45f, 0.45f);

    private Image _image;
    private CanvasGroup _cg;

    void Awake()
    {
        _image = GetComponent<Image>();
        _cg = GetComponent<CanvasGroup>();
        ApplyState();
    }

    void OnValidate()
    {
        if (_image == null) _image = GetComponent<Image>();
        if (_cg == null) _cg = GetComponent<CanvasGroup>();
        ApplyState();
    }

    public void SetActive(bool active)
    {
        isActive = active;
        ApplyState();
    }

    public void Toggle()
    {
        SetActive(!isActive);
    }

    private void ApplyState()
    {
        if (_image != null)
            _image.color = isActive ? activeColor : inactiveColor;

        if (_cg != null)
        {
            // 未激活：阻止所有点击/悬浮（包括子物体上的 Text）
            _cg.interactable = isActive;
            _cg.blocksRaycasts = isActive;
        }
    }
}
