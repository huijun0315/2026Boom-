using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

[Serializable]
public class PlayerLevelStoreData
{
    public List<LevelData> levels = new List<LevelData>();
}

public static class PlayerLevelStore
{
    static string FilePath
    {
        get { return Path.Combine(Application.persistentDataPath, "player_levels.json"); }
    }

    public static string GetStoragePath()
    {
        return FilePath;
    }

    public static List<string> LoadIds()
    {
        var data = LoadAll();
        var ids = new List<string>();
        if (data == null || data.levels == null) return ids;
        for (int i = 0; i < data.levels.Count; i++)
        {
            var lv = data.levels[i];
            if (lv == null || string.IsNullOrEmpty(lv.id)) continue;
            ids.Add(lv.id);
        }
        return ids;
    }

    public static List<LevelData> LoadLevels()
    {
        var data = LoadAll();
        if (data == null || data.levels == null) return new List<LevelData>();
        return new List<LevelData>(data.levels);
    }

    public static LevelData Load(string id)
    {
        if (string.IsNullOrEmpty(id)) return null;
        var data = LoadAll();
        if (data == null || data.levels == null) return null;
        for (int i = 0; i < data.levels.Count; i++)
        {
            var lv = data.levels[i];
            if (lv != null && lv.id == id) return lv;
        }
        return null;
    }

    public static void Save(LevelData level)
    {
        if (level == null || string.IsNullOrEmpty(level.id)) return;
        var data = LoadAll();
        if (data == null) data = new PlayerLevelStoreData();
        if (data.levels == null) data.levels = new List<LevelData>();

        bool replaced = false;
        for (int i = 0; i < data.levels.Count; i++)
        {
            var lv = data.levels[i];
            if (lv != null && lv.id == level.id)
            {
                data.levels[i] = level;
                replaced = true;
                break;
            }
        }
        if (!replaced) data.levels.Add(level);

        var json = JsonUtility.ToJson(data, true);
        File.WriteAllText(FilePath, json);
    }

    public static bool Delete(string id)
    {
        if (string.IsNullOrEmpty(id)) return false;
        var data = LoadAll();
        if (data == null || data.levels == null) return false;

        int removed = data.levels.RemoveAll(lv => lv != null && lv.id == id);
        if (removed <= 0) return false;

        var json = JsonUtility.ToJson(data, true);
        File.WriteAllText(FilePath, json);
        return true;
    }

    static PlayerLevelStoreData LoadAll()
    {
        if (!File.Exists(FilePath)) return new PlayerLevelStoreData();
        try
        {
            var json = File.ReadAllText(FilePath);
            if (string.IsNullOrEmpty(json)) return new PlayerLevelStoreData();
            var data = JsonUtility.FromJson<PlayerLevelStoreData>(json);
            if (data == null) data = new PlayerLevelStoreData();
            if (data.levels == null) data.levels = new List<LevelData>();
            return data;
        }
        catch
        {
            return new PlayerLevelStoreData();
        }
    }
}
