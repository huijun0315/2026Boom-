using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

/// <summary>
/// 引导弹窗 UI 组件。由 Manager 自动构建。
/// 调用 Show(data, onClose) 弹出，点关闭按钮后回调 onClose。
/// </summary>
public class TutorialPopupUI : MonoBehaviour
{
    [Header("UI Refs")]
    public CanvasGroup rootGroup;
    public Text titleText;
    public Text bodyTextObj;
    public CanvasGroup videoGroup;
    public RawImage videoImage;
    public VideoPlayer videoPlayer;
    public AudioSource videoAudioSource;
    public Button closeButton;

    private Action _onClose;
    private bool _hasVideo;
    private bool _initialized;
    private RenderTexture _rt;

    public void Init()
    {
        if (_initialized) return;
        _initialized = true;

        if (closeButton != null)
            closeButton.onClick.AddListener(Hide);

        if (rootGroup != null)
        {
            rootGroup.alpha = 0f;
            rootGroup.interactable = false;
            rootGroup.blocksRaycasts = false;
        }

        if (videoGroup != null)
        {
            videoGroup.alpha = 0f;
            videoGroup.blocksRaycasts = false;
        }
    }

    public void Show(TutorialPopupData data, Action onClose)
    {
        Init();
        _onClose = onClose;

        if (titleText != null) titleText.text = data.title ?? "";
        if (bodyTextObj != null) bodyTextObj.text = data.bodyText ?? "";

        _hasVideo = false;
        if (videoPlayer != null)
        {
            if (data.videoClip != null)
            {
                SetupVideoClip(data.videoClip);
                _hasVideo = true;
            }
            else if (!string.IsNullOrEmpty(data.videoUrl))
            {
                SetupVideoUrl(data.videoUrl);
                _hasVideo = true;
            }
        }

        if (videoGroup != null)
        {
            videoGroup.alpha = _hasVideo ? 1f : 0f;
            videoGroup.blocksRaycasts = _hasVideo;
        }

        if (bodyTextObj != null)
        {
            var rt = bodyTextObj.rectTransform;
            if (_hasVideo)
            {
                rt.anchorMin = new Vector2(0f, 0.10f);
                rt.anchorMax = new Vector2(1f, 0.48f);
                rt.offsetMin = new Vector2(24, 0);
                rt.offsetMax = new Vector2(-24, 0);
            }
            else
            {
                rt.anchorMin = new Vector2(0f, 0.10f);
                rt.anchorMax = new Vector2(1f, 0.88f);
                rt.offsetMin = new Vector2(24, 0);
                rt.offsetMax = new Vector2(-24, 0);
            }
        }

        if (rootGroup != null)
        {
            rootGroup.alpha = 1f;
            rootGroup.interactable = true;
            rootGroup.blocksRaycasts = true;
        }

        if (_hasVideo)
        {
            StartCoroutine(PlayVideoCoroutine());
        }
    }

    IEnumerator PlayVideoCoroutine()
    {
        // 释放旧的 RenderTexture
        ReleaseRT();

        // 创建持久 RenderTexture
        _rt = new RenderTexture(1280, 720, 0);
        _rt.Create();

        videoPlayer.targetTexture = _rt;
        videoImage.texture = _rt;

        // 等一帧
        yield return null;

        videoPlayer.Prepare();

        // 等待 isPrepared，最多 8 秒
        float timeout = 8f;
        while (!videoPlayer.isPrepared && timeout > 0f)
        {
            timeout -= Time.deltaTime;
            yield return null;
        }

        if (videoPlayer.isPrepared)
        {
            Debug.Log("[TutorialPopupUI] Video prepared OK. Playing. hasAudio=" + videoPlayer.audioTrackCount);
            videoPlayer.Play();
        }
        else
        {
            Debug.LogWarning("[TutorialPopupUI] Video prepare TIMEOUT after 8s. clip=" + videoPlayer.clip + " url=" + videoPlayer.url);
        }
    }

    void ReleaseRT()
    {
        if (_rt != null)
        {
            _rt.Release();
            Destroy(_rt);
            _rt = null;
        }
    }

    public void Hide()
    {
        StopAllCoroutines();

        if (videoPlayer != null)
        {
            if (videoPlayer.isPlaying) videoPlayer.Stop();
            videoPlayer.targetTexture = null;
            videoPlayer.clip = null;
            videoPlayer.url = "";
        }

        ReleaseRT();

        if (videoImage != null) videoImage.texture = null;

        if (rootGroup != null)
        {
            rootGroup.alpha = 0f;
            rootGroup.interactable = false;
            rootGroup.blocksRaycasts = false;
        }

        var cb = _onClose;
        _onClose = null;
        if (cb != null) cb();
    }

    void SetupVideoClip(VideoClip clip)
    {
        videoPlayer.source = VideoSource.VideoClip;
        videoPlayer.clip = clip;
        videoPlayer.audioOutputMode = VideoAudioOutputMode.AudioSource;
        if (videoAudioSource != null)
            videoPlayer.SetTargetAudioSource(0, videoAudioSource);
        videoPlayer.isLooping = false;
    }

    void SetupVideoUrl(string url)
    {
        videoPlayer.source = VideoSource.Url;
        videoPlayer.url = url;
        videoPlayer.audioOutputMode = VideoAudioOutputMode.AudioSource;
        if (videoAudioSource != null)
            videoPlayer.SetTargetAudioSource(0, videoAudioSource);
        videoPlayer.isLooping = false;
    }

    void OnDestroy()
    {
        ReleaseRT();
    }
}
