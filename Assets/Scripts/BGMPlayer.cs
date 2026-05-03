using System.Collections;
using UnityEngine;
using UnityEngine.Audio;

[RequireComponent(typeof(AudioSource))]
public class BGMPlayer : MonoBehaviour
{
    private Coroutine _fadeRoutine;
    private Coroutine _pauseAudioRoutine;
    const string DefaultButtonClickResourceName = "buttonSOngd";


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

    [Header("Pause Audio (AudioMixer Exposed)")]
    public AudioMixer bgmMixer;
    [Tooltip("BGM 音源输出到的 Mixer Group（必须挂到上面 bgmMixer 内的组）")]
    public AudioMixerGroup bgmOutputGroup;
    public string lowPassCutoffParam = "BGM_LowPassCutoff";
    public string reverbWetLevelParam = "BGM_ReverbWet";
    public string volumeDbParam = "BGM_Volume";
    [Tooltip("Low Pass cutoff：正常 22000Hz")]
    public float normalLowPassCutoff = 22000f;
    [Tooltip("Low Pass cutoff：暂停 1000Hz")]
    public float pausedLowPassCutoff = 1000f;
    [Tooltip("Reverb Wet Level：正常 -40dB")]
    public float normalReverbWetDb = -40f;
    [Tooltip("Reverb Wet Level：暂停 -10dB")]
    public float pausedReverbWetDb = -10f;
    [Tooltip("Volume：正常 0dB")]
    public float normalVolumeDb = 0f;
    [Tooltip("Volume：暂停 -5dB")]
    public float pausedVolumeDb = -5f;

    private AudioSource _src;
    private AudioLowPassFilter _lowPass;
    private AudioReverbFilter _reverb;

    void Awake()
    {
        // 单例：避免切场景或重复实例化时同时播多份
        if (Instance != null && Instance != this)
        {
            if (playOnStart && bgmClip != null)
            {
                var instSrc = Instance.GetComponent<AudioSource>();
                bool instPlaying = instSrc != null && instSrc.isPlaying;
                if (Instance.bgmClip != bgmClip || !instPlaying)
                    Instance.SetClip(bgmClip, true);
            }
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
        if (bgmOutputGroup != null) _src.outputAudioMixerGroup = bgmOutputGroup;

        EnsurePauseFallbackFilters();

        EnsureDefaultButtonClickAssigned();
    }

    void Start()
    {
        if (playOnStart) Play();
        ApplyPauseStateImmediate(paused: false);
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

        bool sameClipAlreadyPlaying = (_src.clip == clip) && _src.isPlaying;
        if (sameClipAlreadyPlaying)
        {
            bgmClip = clip;
            return;
        }

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

    public static void PlayDefaultButtonClick(float volumeScale = 1f)
    {
        var inst = Instance;
        if (inst == null) return;
        if (inst.defaultButtonClick == null)
            inst.EnsureDefaultButtonClickAssigned();
        if (inst.defaultButtonClick == null) return;
        if (inst._src == null) inst._src = inst.GetComponent<AudioSource>();
        if (inst._src == null) return;
        inst._src.PlayOneShot(inst.defaultButtonClick, Mathf.Clamp01(volumeScale));
    }

    void EnsureDefaultButtonClickAssigned()
    {
        if (defaultButtonClick != null) return;
        defaultButtonClick = Resources.Load<AudioClip>(DefaultButtonClickResourceName);
    }

    public void SetPauseAudioState(bool paused)
    {
        float duration = paused ? 0.5f : 0.3f;
        StartPauseAudioTransition(paused, duration);
    }

    void StartPauseAudioTransition(bool paused, float duration)
    {
        if (bgmMixer == null)
            EnsurePauseFallbackFilters();
        if (_pauseAudioRoutine != null) StopCoroutine(_pauseAudioRoutine);
        _pauseAudioRoutine = StartCoroutine(PauseAudioRoutine(paused, Mathf.Max(0f, duration)));
    }

    IEnumerator PauseAudioRoutine(bool paused, float duration)
    {
        float targetCutoff = paused ? pausedLowPassCutoff : normalLowPassCutoff;
        float targetReverb = paused ? pausedReverbWetDb : normalReverbWetDb;
        float targetVolume = paused ? pausedVolumeDb : normalVolumeDb;

        float fromCutoff = GetMixerFloatOrDefault(lowPassCutoffParam, targetCutoff);
        float fromReverb = GetMixerFloatOrDefault(reverbWetLevelParam, targetReverb);
        float fromVolume = GetMixerFloatOrDefault(volumeDbParam, targetVolume);

        if (duration <= 0f)
        {
            ApplyPauseState(targetCutoff, targetReverb, targetVolume);
            _pauseAudioRoutine = null;
            yield break;
        }

        float t = 0f;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / duration);

            ApplyPauseState(
                Mathf.Lerp(fromCutoff, targetCutoff, k),
                Mathf.Lerp(fromReverb, targetReverb, k),
                Mathf.Lerp(fromVolume, targetVolume, k));

            yield return null;
        }

        ApplyPauseState(targetCutoff, targetReverb, targetVolume);
        _pauseAudioRoutine = null;
    }

    void ApplyPauseStateImmediate(bool paused)
    {
        ApplyPauseState(
            paused ? pausedLowPassCutoff : normalLowPassCutoff,
            paused ? pausedReverbWetDb : normalReverbWetDb,
            paused ? pausedVolumeDb : normalVolumeDb);
    }

    void ApplyPauseState(float cutoff, float reverbDb, float volumeDb)
    {
        bool mixerOk = bgmMixer != null;
        if (mixerOk)
        {
            SetMixerFloat(lowPassCutoffParam, cutoff);
            SetMixerFloat(reverbWetLevelParam, reverbDb);
            SetMixerFloat(volumeDbParam, volumeDb);
            return;
        }

        EnsurePauseFallbackFilters();
        if (_lowPass != null) _lowPass.cutoffFrequency = cutoff;
        if (_reverb != null) _reverb.reverbLevel = reverbDb;
        if (_src != null)
        {
            float linear = DbToLinear(volumeDb);
            _src.volume = Mathf.Clamp01(volume * linear);
        }
    }

    void EnsurePauseFallbackFilters()
    {
        if (_lowPass == null) _lowPass = GetComponent<AudioLowPassFilter>();
        if (_lowPass == null) _lowPass = gameObject.AddComponent<AudioLowPassFilter>();
        if (_reverb == null) _reverb = GetComponent<AudioReverbFilter>();
        if (_reverb == null) _reverb = gameObject.AddComponent<AudioReverbFilter>();
        _lowPass.enabled = true;
        _reverb.enabled = true;
    }

    static float DbToLinear(float db)
    {
        return Mathf.Pow(10f, db / 20f);
    }

    float GetMixerFloatOrDefault(string param, float fallback)
    {
        if (bgmMixer == null || string.IsNullOrEmpty(param)) return fallback;
        float v;
        if (bgmMixer.GetFloat(param, out v)) return v;
        return fallback;
    }

    void SetMixerFloat(string param, float value)
    {
        if (bgmMixer == null || string.IsNullOrEmpty(param)) return;
        if (!bgmMixer.SetFloat(param, value))
            Debug.LogWarning("[BGMPlayer] Exposed 参数未找到或不可写：" + param);
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
        if (defaultButtonClick == null)
        {
            var atPath = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/art/audio/UI/buttonSOngd.mp3");
            if (atPath != null) defaultButtonClick = atPath;
        }

        if (_src == null) _src = GetComponent<AudioSource>();
        if (_src != null)
        {
            _src.volume = volume;
            _src.loop = loop;
            if (bgmOutputGroup != null) _src.outputAudioMixerGroup = bgmOutputGroup;
        }
    }
#endif
}
