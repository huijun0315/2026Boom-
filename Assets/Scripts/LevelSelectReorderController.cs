using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class LevelSelectReorderController : MonoBehaviour
{
    [System.Serializable]
    class SavedOrderData
    {
        public List<string> levelIds = new List<string>();
    }

    const string PlayerPrefsOrderKey = "level_order_override_json";

    [Header("Refs")]
    public RectTransform tileRoot;
    public Button editModeButton;
    public Button saveOrderButton;
    public Button cancelButton;
    public Text statusText;

    readonly List<LevelTileDraggable> _workingOrder = new List<LevelTileDraggable>();
    readonly List<Vector2> _slots = new List<Vector2>();
    readonly List<string> _snapshotIds = new List<string>();
    bool _editMode;
    bool _dirty;
    LevelTileDraggable _dragging;
    Canvas _canvas;
    RectTransform _slotHighlight;

    void Awake()
    {
        if (tileRoot == null)
        {
            var cv = FindObjectOfType<Canvas>();
            if (cv != null) tileRoot = cv.GetComponent<RectTransform>();
        }

        _canvas = tileRoot != null ? tileRoot.GetComponentInParent<Canvas>() : null;

        if (editModeButton != null) editModeButton.onClick.AddListener(ToggleEditMode);
        if (saveOrderButton != null) saveOrderButton.onClick.AddListener(SaveOrder);
        if (cancelButton != null) cancelButton.onClick.AddListener(CancelEdit);

        RefreshTilesAndSlots();
        ApplyPersistedOrderOnLoad();
        _dirty = false;
        SetEditMode(false, false);
    }

    void OnDisable()
    {
        if (_dirty)
            SaveCurrentOrderSilent();
    }

    void OnApplicationQuit()
    {
        if (_dirty)
            SaveCurrentOrderSilent();
    }

    public void HandleBeginDrag(LevelTileDraggable tile, PointerEventData eventData)
    {
        if (!_editMode || tile == null || !_workingOrder.Contains(tile)) return;
        _dragging = tile;
        _dragging.SetDraggingVisual(true);
        _dragging.transform.SetAsLastSibling();
        UpdateSlotHighlight(_workingOrder.IndexOf(tile));
    }

    public void HandleDrag(LevelTileDraggable tile, PointerEventData eventData)
    {
        if (!_editMode || tile == null || tile != _dragging || tile.Rect == null) return;

        float scale = _canvas != null ? Mathf.Max(0.01f, _canvas.scaleFactor) : 1f;
        tile.Rect.anchoredPosition += eventData.delta / scale;

        int cur = _workingOrder.IndexOf(tile);
        int nearest = FindNearestSlot(tile.Rect.anchoredPosition);
        UpdateSlotHighlight(nearest);
        if (nearest >= 0 && nearest != cur)
        {
            _workingOrder.RemoveAt(cur);
            _workingOrder.Insert(nearest, tile);
            SnapTiles(exclude: tile);
            _dirty = true;
        }
    }

    public void HandleEndDrag(LevelTileDraggable tile, PointerEventData eventData)
    {
        if (!_editMode || tile == null || tile != _dragging) return;
        tile.SetDraggingVisual(false);
        _dragging = null;
        SnapTiles();
        HideSlotHighlight();
    }

    void ToggleEditMode()
    {
        if (_editMode)
            SaveOrder();
        else
            SetEditMode(true, true);
    }

    void SetEditMode(bool on, bool takeSnapshot)
    {
        _editMode = on;

        if (_editMode)
        {
            RefreshTilesAndSlots();
            if (takeSnapshot)
            {
                _snapshotIds.Clear();
                for (int i = 0; i < _workingOrder.Count; i++)
                {
                    var lb = _workingOrder[i] != null ? _workingOrder[i].LevelButton : null;
                    if (lb != null && !string.IsNullOrEmpty(lb.levelId))
                        _snapshotIds.Add(lb.levelId);
                }
            }
        }

        SetTileButtonsInteractable(!_editMode);

        if (saveOrderButton != null) saveOrderButton.gameObject.SetActive(_editMode);
        if (cancelButton != null) cancelButton.gameObject.SetActive(_editMode);
        if (editModeButton != null)
        {
            var t = editModeButton.GetComponentInChildren<Text>();
            if (t != null) t.text = _editMode ? "完成拖拽" : "排序模式";
        }

        if (statusText != null)
            statusText.text = _editMode ? "拖拽关卡卡片调整顺序，然后保存" : "";

        if (!_editMode)
            HideSlotHighlight();
    }

    void SetTileButtonsInteractable(bool on)
    {
        for (int i = 0; i < _workingOrder.Count; i++)
        {
            var tile = _workingOrder[i];
            if (tile == null) continue;
            var btn = tile.GetComponent<Button>();
            if (btn != null) btn.interactable = on;
        }
    }

    void CancelEdit()
    {
        if (!_editMode) return;

        var map = new Dictionary<string, LevelTileDraggable>();
        for (int i = 0; i < _workingOrder.Count; i++)
        {
            var tile = _workingOrder[i];
            var lb = tile != null ? tile.LevelButton : null;
            if (lb == null || string.IsNullOrEmpty(lb.levelId)) continue;
            if (!map.ContainsKey(lb.levelId)) map.Add(lb.levelId, tile);
        }

        var reordered = new List<LevelTileDraggable>();
        for (int i = 0; i < _snapshotIds.Count; i++)
        {
            LevelTileDraggable t;
            if (map.TryGetValue(_snapshotIds[i], out t))
                reordered.Add(t);
        }
        for (int i = 0; i < _workingOrder.Count; i++)
        {
            if (!reordered.Contains(_workingOrder[i]))
                reordered.Add(_workingOrder[i]);
        }

        _workingOrder.Clear();
        _workingOrder.AddRange(reordered);
        SnapTiles();
        SetEditMode(false, false);
    }

    void SaveOrder()
    {
        if (!_editMode) return;

        int savedCount = SaveCurrentOrderSilent();
        if (statusText != null)
            statusText.text = "已保存顺序（重启后仍生效），共 " + savedCount + " 关";

        RefreshIndexNumbers();
        SetEditMode(false, false);
    }

    int SaveCurrentOrderSilent()
    {
        var ids = new List<string>();
        for (int i = 0; i < _workingOrder.Count; i++)
        {
            var lb = _workingOrder[i] != null ? _workingOrder[i].LevelButton : null;
            if (lb == null || string.IsNullOrEmpty(lb.levelId)) continue;
            if (!ids.Contains(lb.levelId)) ids.Add(lb.levelId);
        }

        SaveOrderToPlayerPrefs(ids);
#if UNITY_EDITOR
        LevelStore.SaveLevelOrder(ids.ToArray());
#endif
        _dirty = false;
        return ids.Count;
    }

    void RefreshTilesAndSlots()
    {
        _workingOrder.Clear();
        _slots.Clear();

        if (tileRoot == null) return;

        var tiles = tileRoot.GetComponentsInChildren<LevelTileDraggable>(true);
        for (int i = 0; i < tiles.Length; i++)
        {
            if (tiles[i] == null || tiles[i].LevelButton == null) continue;
            _workingOrder.Add(tiles[i]);
        }

        _workingOrder.Sort((a, b) =>
        {
            int ai = a != null && a.LevelButton != null ? a.LevelButton.levelIndex : int.MaxValue;
            int bi = b != null && b.LevelButton != null ? b.LevelButton.levelIndex : int.MaxValue;
            return ai.CompareTo(bi);
        });

        for (int i = 0; i < _workingOrder.Count; i++)
            _slots.Add(_workingOrder[i].Rect.anchoredPosition);
    }

    void ApplyPersistedOrderOnLoad()
    {
        var desiredIds = LoadPersistedOrderIds();
        if (desiredIds.Count == 0 || _workingOrder.Count == 0) return;

        var map = new Dictionary<string, LevelTileDraggable>();
        for (int i = 0; i < _workingOrder.Count; i++)
        {
            var tile = _workingOrder[i];
            var lb = tile != null ? tile.LevelButton : null;
            if (lb == null || string.IsNullOrEmpty(lb.levelId)) continue;
            if (!map.ContainsKey(lb.levelId)) map.Add(lb.levelId, tile);
        }

        var reordered = new List<LevelTileDraggable>(_workingOrder.Count);
        for (int i = 0; i < desiredIds.Count; i++)
        {
            LevelTileDraggable tile;
            if (map.TryGetValue(desiredIds[i], out tile) && tile != null && !reordered.Contains(tile))
                reordered.Add(tile);
        }

        for (int i = 0; i < _workingOrder.Count; i++)
            if (_workingOrder[i] != null && !reordered.Contains(_workingOrder[i]))
                reordered.Add(_workingOrder[i]);

        _workingOrder.Clear();
        _workingOrder.AddRange(reordered);
        SnapTiles();
        RefreshIndexNumbers();
        _dirty = false;
    }

    List<string> LoadPersistedOrderIds()
    {
        var ids = new List<string>();

        var savedJson = PlayerPrefs.GetString(PlayerPrefsOrderKey, "");
        if (!string.IsNullOrEmpty(savedJson))
        {
            try
            {
                var saved = JsonUtility.FromJson<SavedOrderData>(savedJson);
                if (saved != null && saved.levelIds != null)
                {
                    for (int i = 0; i < saved.levelIds.Count; i++)
                    {
                        var id = saved.levelIds[i];
                        if (string.IsNullOrEmpty(id) || ids.Contains(id)) continue;
                        ids.Add(id);
                    }
                }
            }
            catch { }
        }

        if (ids.Count == 0)
        {
            var fromStore = LevelStore.LoadOrderedIds();
            for (int i = 0; i < fromStore.Length; i++)
                if (!string.IsNullOrEmpty(fromStore[i]) && !ids.Contains(fromStore[i]))
                    ids.Add(fromStore[i]);
        }

        return ids;
    }

    void SaveOrderToPlayerPrefs(List<string> ids)
    {
        var data = new SavedOrderData();
        if (ids != null)
        {
            for (int i = 0; i < ids.Count; i++)
            {
                var id = ids[i];
                if (string.IsNullOrEmpty(id) || data.levelIds.Contains(id)) continue;
                data.levelIds.Add(id);
            }
        }

        PlayerPrefs.SetString(PlayerPrefsOrderKey, JsonUtility.ToJson(data));
        PlayerPrefs.Save();
    }

    int FindNearestSlot(Vector2 p)
    {
        if (_slots.Count == 0) return -1;
        float best = float.MaxValue;
        int bestIdx = 0;
        for (int i = 0; i < _slots.Count; i++)
        {
            float d = (_slots[i] - p).sqrMagnitude;
            if (d < best)
            {
                best = d;
                bestIdx = i;
            }
        }
        return bestIdx;
    }

    void SnapTiles(LevelTileDraggable exclude = null)
    {
        int count = Mathf.Min(_workingOrder.Count, _slots.Count);
        for (int i = 0; i < count; i++)
        {
            var tile = _workingOrder[i];
            if (tile == null || tile == exclude || tile.Rect == null) continue;
            tile.Rect.anchoredPosition = _slots[i];
        }
    }

    void EnsureSlotHighlight()
    {
        if (_slotHighlight != null || tileRoot == null) return;

        var go = new GameObject("ReorderSlotHighlight", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(tileRoot, false);
        _slotHighlight = go.GetComponent<RectTransform>();
        _slotHighlight.SetAsFirstSibling();

        var img = go.GetComponent<Image>();
        img.color = new Color(1f, 0.82f, 0.25f, 0.22f);
        img.raycastTarget = false;

        go.SetActive(false);
    }

    void UpdateSlotHighlight(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= _slots.Count) { HideSlotHighlight(); return; }
        EnsureSlotHighlight();
        if (_slotHighlight == null) return;

        Vector2 size = Vector2.zero;
        if (_dragging != null && _dragging.Rect != null) size = _dragging.Rect.sizeDelta;
        if (size == Vector2.zero && _workingOrder.Count > 0 && _workingOrder[0] != null && _workingOrder[0].Rect != null)
            size = _workingOrder[0].Rect.sizeDelta;

        _slotHighlight.sizeDelta = size;
        _slotHighlight.anchoredPosition = _slots[slotIndex];
        if (!_slotHighlight.gameObject.activeSelf) _slotHighlight.gameObject.SetActive(true);
    }

    void HideSlotHighlight()
    {
        if (_slotHighlight != null && _slotHighlight.gameObject.activeSelf)
            _slotHighlight.gameObject.SetActive(false);
    }

    void RefreshIndexNumbers()
    {
        for (int i = 0; i < _workingOrder.Count; i++)
        {
            var tile = _workingOrder[i];
            var lb = tile != null ? tile.LevelButton : null;
            if (lb == null) continue;
            lb.levelIndex = i + 1;

            var numberTf = tile.transform.Find("Number");
            var numberText = numberTf != null ? numberTf.GetComponent<Text>() : null;
            if (numberText != null)
                numberText.text = lb.isUnlocked ? lb.levelIndex.ToString() : "?";
        }
    }
}
