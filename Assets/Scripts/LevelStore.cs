using System.IO;
using UnityEngine;

/// <summary>
/// 关卡 JSON 读取/保存。
/// 运行时通过 Resources.Load 读取；编辑器下保存到 Assets/Resources/Levels。
/// </summary>
public static class LevelStore
{
    public const string ResourcesFolder = "Levels";

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
        var files = Directory.GetFiles(EditorAssetFolder, "*.json");
        var ids = new string[files.Length];
        for (int i = 0; i < files.Length; i++) ids[i] = Path.GetFileNameWithoutExtension(files[i]);
        return ids;
    }
#endif
}
