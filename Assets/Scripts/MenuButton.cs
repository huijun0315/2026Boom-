using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;

[RequireComponent(typeof(RectTransform))]
public class MenuButton : MonoBehaviour,
    IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
    [Header("Click Sound")]
    [Tooltip("插槽：放点击音效")]
    public AudioClip clickSound;

    [Header("Hover")]
    public float hoverScale = 1.08f;
    public float hoverDuration = 0.12f;

    [Header("Click Animation")]
    public float clickScale = 0.92f;
    public float clickDuration = 0.10f;

    [Header("BGM Fade On Click")]
    [Tooltip("点击后是否让 BGM 渐弱")]
    public bool fadeBGMOnClick = true;
    [Tooltip("渐弱总时长（秒）")]
    public float bgmFadeDuration = 1.5f;

    private RectTransform _rt;
    private Vector3 _baseScale = Vector3.one;
    private Coroutine _routine;
    private AudioSource _audio;
    private bool _hovered;

    void Awake()
    {
        _rt = transform as RectTransform;
        _baseScale = _rt.localScale;

        _audio = GetComponent<AudioSource>();
        if (_audio == null) _audio = gameObject.AddComponent<AudioSource>();
        _audio.playOnAwake = false;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        _hovered = true;
        StartScale(_baseScale * hoverScale, hoverDuration);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        _hovered = false;
        StartScale(_baseScale, hoverDuration);
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        AudioClip clip = clickSound;
        if (clip == null && BGMPlayer.Instance != null)
            clip = BGMPlayer.Instance.defaultButtonClick;

        if (clip != null && _audio != null)
            _audio.PlayOneShot(clip);

        if (fadeBGMOnClick && BGMPlayer.Instance != null)
            BGMPlayer.Instance.FadeOut(bgmFadeDuration, 0f, true);

        if (_routine != null) StopCoroutine(_routine);
        _routine = StartCoroutine(ClickOnce());
    }

    private IEnumerator ClickOnce()
    {
        yield return ScaleTo(_baseScale * clickScale, clickDuration);
        yield return ScaleTo(_hovered ? _baseScale * hoverScale : _baseScale, clickDuration);
        _routine = null;
    }

    private void StartScale(Vector3 target, float dur)
    {
        if (_routine != null) StopCoroutine(_routine);
        _routine = StartCoroutine(ScaleTo(target, dur));
    }

    private IEnumerator ScaleTo(Vector3 target, float dur)
    {
        Vector3 from = _rt.localScale;
        float t = 0f;
        while (t < dur)
        {
            t += Time.unscaledDeltaTime;
            _rt.localScale = Vector3.Lerp(from, target, Mathf.Clamp01(t / dur));
            yield return null;
        }
        _rt.localScale = target;
    }
}
