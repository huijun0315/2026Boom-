using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[RequireComponent(typeof(RectTransform))]
public class LevelTileDraggable : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    public LevelSelectReorderController controller;

    RectTransform _rect;
    CanvasGroup _cg;
    LevelButton _levelButton;

    public RectTransform Rect { get { return _rect; } }
    public LevelButton LevelButton { get { return _levelButton; } }

    void Awake()
    {
        _rect = GetComponent<RectTransform>();
        _cg = GetComponent<CanvasGroup>();
        _levelButton = GetComponent<LevelButton>();
        if (controller == null)
            controller = FindObjectOfType<LevelSelectReorderController>();
    }

    public void SetDraggingVisual(bool on)
    {
        if (_cg != null)
            _cg.blocksRaycasts = !on;
        transform.localScale = on ? new Vector3(1.06f, 1.06f, 1f) : Vector3.one;
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (controller != null) controller.HandleBeginDrag(this, eventData);
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (controller != null) controller.HandleDrag(this, eventData);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (controller != null) controller.HandleEndDrag(this, eventData);
    }
}
