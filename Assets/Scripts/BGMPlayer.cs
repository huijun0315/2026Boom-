using System.Collections;
using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public class BGMPlayer : MonoBehaviour
{
    private Coroutine _fadeRoutine;


    public static BGMPlayer Instance { get; private set; }

    [Header("BGM 插槽")]
    [Tooltip("拖一个 AudioClip 进来作为背景音乐")]
    public AudioClip bgmClip;

    [Header("全局按钮默认点击音效")]
    [Tooltip("按钮自身 Click Sound 为空时，会回退到这里。所有按钮共享。")]
    public AudioClip defaultButtonClick;

    [Header("播放设置")]
    [Range(0f, 1f)] public float volume = 0.6f;
    public bool loop = true;
    public bool playOnStart = true;
    [Tooltip("跨场景持续播放，不会因切场景中断")]
    public bool dontDestroyOnLoad = true;

    private AudioSource _src;

    void Awake()
    {
        // 单例：避免切场景或重复实例化时同时播多份
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        if (dontDestroyOnLoad)
            DontDestroyOnLoad(gameObject);

        _src = GetComponent<AudioSource>();
        _src.playOnAwake = false;
        _src.loop = loop;
        _src.volume = volume;
        _src.clip = bgmClip;
    }

    void Start()
    {
        if (playOnStart) Play();
    }

    public void Play()
    {
        if (_src == null) _src = GetComponent<AudioSource>();
        if (bgmClip == null) return;
        _src.clip = bgmClip;
        _src.loop = loop;
        _src.volume = volume;
        if (!_src.isPlaying) _src.Play();
    }

    public void Stop()
    {
        if (_src != null) _src.Stop();
    }

    public void Pause()
    {
        if (_src != null) _src.Pause();
    }

    public void Resume()
    {
        if (_src != null) _src.UnPause();
    }

    public void SetClip(AudioClip clip, bool playImmediately = true)
    {
        if (_src == null) _src = GetComponent<AudioSource>();
        // 若正在执行渐弱，先停掉并恢复原始音量
        if (_fadeRoutine != null) { StopCoroutine(_fadeRoutine); _fadeRoutine = null; }
        _src.volume = volume;

        bgmClip = clip;
        _src.clip = clip;
        if (playImmediately && clip != null) _src.Play();
    }

    public void SetVolume(float v)
    {
        volume = Mathf.Clamp01(v);
        if (_src != null) _src.volume = volume;
    }

    /// <summary>
    /// 在 duration 秒内把 BGM 音量渐弱至 targetVolume，结束后若 stopWhenZero 为 true 且目标为 0 则停止播放。
    /// </summary>
    public void FadeOut(float duration = 1.5f, float targetVolume = 0f, bool stopWhenZero = true)
    {
        if (_src == null) _src = GetComponent<AudioSource>();
        if (_fadeRoutine != null) StopCoroutine(_fadeRoutine);
        _fadeRoutine = StartCoroutine(FadeRoutine(targetVolume, duration, stopWhenZero));
    }

    private IEnumerator FadeRoutine(float target, float duration, bool stopWhenZero)
    {
        float from = _src.volume;
        target = Mathf.Clamp01(target);
        if (duration <= 0f)
        {
            _src.volume = target;
        }
        else
        {
            float t = 0f;
            while (t < duration)
            {
                t += Time.unscaledDeltaTime;
                _src.volume = Mathf.Lerp(from, target, Mathf.Clamp01(t / duration));
                yield return null;
            }
            _src.volume = target;
        }
        if (stopWhenZero && target <= 0.0001f)
            _src.Stop();
        _fadeRoutine = null;
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        if (_src == null) _src = GetComponent<AudioSource>();
        if (_src != null)
        {
            _src.volume = volume;
            _src.loop = loop;
        }
    }
#endif
}
