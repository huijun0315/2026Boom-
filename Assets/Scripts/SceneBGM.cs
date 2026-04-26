using UnityEngine;

/// <summary>
/// 挂在任一场景物体上，暴露一个 BGM 插槽。
/// 场景开始时把这首曲子塞给全局 BGMPlayer 播放（不打断也不重复创建实例）。
/// </summary>
public class SceneBGM : MonoBehaviour
{
    [Header("本场景 BGM 插槽")]
    [Tooltip("拖一个 AudioClip 进来作为该场景的背景音乐")]
    public AudioClip bgmClip;

    [Tooltip("是否在 Start 时自动切换并播放")]
    public bool playOnStart = true;

    void Start()
    {
        if (!playOnStart || bgmClip == null) return;

        // 使用全局单例；若不存在会懒加载
        var player = BGMPlayer.Instance;
        if (player == null) return;

        // 只有当前 BGM 不是这首，或已停止时才切换；避免重复 restart
        if (player.bgmClip != bgmClip || !IsPlaying(player))
            player.SetClip(bgmClip, true);
    }

    private static bool IsPlaying(BGMPlayer p)
    {
        var src = p.GetComponent<AudioSource>();
        return src != null && src.isPlaying;
    }
}
