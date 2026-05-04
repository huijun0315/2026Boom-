using UnityEngine;

public class SkinManager : MonoBehaviour
{
    public static SkinManager Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindObjectOfType<SkinManager>();
                if (_instance == null)
                {
                    var go = new GameObject("SkinManager");
                    _instance = go.AddComponent<SkinManager>();
                }
            }
            return _instance;
        }
    }

    static SkinManager _instance;

    [Header("Catalog")]
    [Tooltip("可选：直接拖拽目录资产；为空时会从 Resources 加载")]
    public SkinCatalog catalog;
    [Tooltip("Resources 下目录资源路径（不含扩展名）")]
    public string resourcesCatalogPath = "SkinCatalog";

    [Header("PlayerPrefs")]
    public string selectedSlotKey = "ach_selected_skin";

    void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }

        _instance = this;
        DontDestroyOnLoad(gameObject);
    }

    public SkinCatalog GetCatalog()
    {
        if (catalog != null) return catalog;
        catalog = Resources.Load<SkinCatalog>(resourcesCatalogPath);
        return catalog;
    }

    public int GetSelectedSlotIndex()
    {
        var c = GetCatalog();
        int def = c != null ? c.defaultSlotIndex : 0;
        return PlayerPrefs.GetInt(selectedSlotKey, def);
    }

    public void SetSelectedSlotIndex(int slotIndex)
    {
        PlayerPrefs.SetInt(selectedSlotKey, slotIndex);
        PlayerPrefs.Save();
    }

    public SkinConfig GetSelectedSkin()
    {
        var c = GetCatalog();
        if (c == null) return null;
        return c.GetSkinBySlot(GetSelectedSlotIndex());
    }

    public void ApplyByMuseumOption(int optionIndex)
    {
        var c = GetCatalog();
        if (c == null)
        {
            SetSelectedSlotIndex(optionIndex);
            ApplyCurrentSkinToScene();
            return;
        }

        int slot = c.GetSlotForOption(optionIndex);
        SetSelectedSlotIndex(slot);
        ApplyCurrentSkinToScene();
    }

    public void ApplyCurrentSkinToScene()
    {
        var skin = GetSelectedSkin();

        var cubes = FindObjectsOfType<RubikCube>(true);
        for (int i = 0; i < cubes.Length; i++)
            if (cubes[i] != null) cubes[i].ApplySkin(skin);

        var pipes = FindObjectsOfType<PipePuzzle>(true);
        for (int i = 0; i < pipes.Length; i++)
            if (pipes[i] != null) pipes[i].ApplySkin(skin);
    }
}
