using System.IO;
using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 关卡 JSON 读取/保存。
/// 运行时通过 Resources.Load 读取；编辑器下保存到 Assets/Resources/Levels。
/// </summary>
public static class LevelStore
{
    public const string ResourcesFolder = "Levels";
    public const string LevelOrderId = "level_order";

    [Serializable]
    class LevelOrderData
    {
        public List<string> levelIds = new List<string>();
    }

#if UNITY_EDITOR
    public const string EditorAssetFolder = "Assets/Resources/Levels";
#endif

    public static LevelData Load(string id)
    {
        if (string.IsNullOrEmpty(id)) return null;
        var ta = Resources.Load<TextAsset>(ResourcesFolder + "/" + id);
        if (ta == null) return null;
        try { return JsonUtility.FromJson<LevelData>(ta.text); }
        catch { return null; }
    }

    public static string[] LoadOrderedIds()
    {
        var ids = new List<string>();
        var ta = Resources.Load<TextAsset>(ResourcesFolder + "/" + LevelOrderId);
        if (ta != null)
        {
            try
            {
                var data = JsonUtility.FromJson<LevelOrderData>(ta.text);
                if (data != null && data.levelIds != null)
                {
                    for (int i = 0; i < data.levelIds.Count; i++)
                    {
                        var id = data.levelIds[i];
                        if (string.IsNullOrEmpty(id)) continue;
                        if (!ids.Contains(id)) ids.Add(id);
                    }
                }
            }
            catch { }
        }

#if UNITY_EDITOR
        var all = ListIds();
        for (int i = 0; i < all.Length; i++)
            if (!ids.Contains(all[i])) ids.Add(all[i]);
#endif

        return ids.ToArray();
    }

#if UNITY_EDITOR
    public static void Save(LevelData data)
    {
        if (data == null || string.IsNullOrEmpty(data.id)) return;
        if (!Directory.Exists(EditorAssetFolder))
            Directory.CreateDirectory(EditorAssetFolder);
        string path = EditorAssetFolder + "/" + data.id + ".json";
        File.WriteAllText(path, JsonUtility.ToJson(data, true));
        UnityEditor.AssetDatabase.ImportAsset(path);
    }

    public static string[] ListIds()
    {
        if (!Directory.Exists(EditorAssetFolder)) return new string[0];
        var files = Directory.GetFiles(EditorAssetFolder, "level_*.json");
        var ids = new List<string>(files.Length);
        for (int i = 0; i < files.Length; i++)
        {
            var id = Path.GetFileNameWithoutExtension(files[i]);
            if (string.IsNullOrEmpty(id) || id == LevelOrderId) continue;
            ids.Add(id);
        }
        ids.Sort(CompareLevelId);
        return ids.ToArray();
    }

    public static void SaveLevelOrder(string[] orderedIds)
    {
        if (!Directory.Exists(EditorAssetFolder))
            Directory.CreateDirectory(EditorAssetFolder);

        var data = new LevelOrderData();
        if (orderedIds != null)
        {
            for (int i = 0; i < orderedIds.Length; i++)
            {
                var id = orderedIds[i];
                if (string.IsNullOrEmpty(id)) continue;
                if (!data.levelIds.Contains(id)) data.levelIds.Add(id);
            }
        }

        string path = EditorAssetFolder + "/" + LevelOrderId + ".json";
        File.WriteAllText(path, JsonUtility.ToJson(data, true));
        UnityEditor.AssetDatabase.ImportAsset(path);
    }

    static int CompareLevelId(string a, string b)
    {
        if (TryParseLevelNumber(a, out var an) && TryParseLevelNumber(b, out var bn))
            return an.CompareTo(bn);
        return string.Compare(a, b, StringComparison.OrdinalIgnoreCase);
    }

    static bool TryParseLevelNumber(string id, out int n)
    {
        n = 0;
        if (string.IsNullOrEmpty(id) || !id.StartsWith("level_")) return false;
        return int.TryParse(id.Substring(6), out n);
    }
#endif
}
