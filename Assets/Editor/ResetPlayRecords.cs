using UnityEditor;
using UnityEngine;

public static class ResetPlayRecords
{
    [MenuItem("Tools/Reset All Play Records")]
    public static void ResetAll()
    {
        PlayerPrefs.DeleteAll();
        PlayerPrefs.Save();
        Debug.Log("[ResetPlayRecords] 所有游玩记录已清除（PlayerPrefs.DeleteAll）");
    }
}
