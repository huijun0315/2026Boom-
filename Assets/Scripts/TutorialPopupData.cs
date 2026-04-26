using UnityEngine;
using UnityEngine.Video;

/// <summary>
/// 引导弹窗配置 ScriptableObject。
/// 在 Assets 中右键 Create → Tutorial → Popup Data 创建实例，
/// 在 Inspector 中配置触发条件和内容，拖入 TutorialPopupManager 的 popups 列表。
/// </summary>
[CreateAssetMenu(fileName = "TutorialPopup", menuName = "Tutorial/Popup Data")]
public class TutorialPopupData : ScriptableObject
{
    [Header("唯一标识")]
    [Tooltip("弹窗唯一 ID，用于 PlayerPrefs 记录是否已弹出过")]
    public string popupId = "tutorial_01";

    public enum TriggerType
    {
        FirstEnterLevel,   // 首次进入指定关卡
        StarCountReached,  // 总星星数首次达到指定值
    }

    [Header("触发条件")]
    public TriggerType triggerType = TriggerType.FirstEnterLevel;

    [Tooltip("TriggerType = FirstEnterLevel 时：关卡 ID（如 level_1）")]
    public string triggerLevelId = "level_1";

    [Tooltip("TriggerType = StarCountReached 时：总星星数阈值")]
    public int triggerStarCount = 3;

    [Header("弹窗内容")]
    [Tooltip("弹窗标题")]
    public string title = "引导标题";

    [Tooltip("弹窗正文（支持换行）")]
    [TextArea(3, 10)]
    public string bodyText = "这里是引导正文内容。";

    [Tooltip("视频文件（可选，为空则只显示文字）\n推荐放入 Assets/StreamingAssets 或 Resources，也可直接拖入")]
    public VideoClip videoClip;

    [Tooltip("视频 URL（优先级低于 videoClip，两者都为空则不显示视频区域）\n可为本地 StreamingAssets 路径或网络 URL")]
    public string videoUrl;
}
