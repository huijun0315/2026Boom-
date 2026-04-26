using UnityEditor;
using UnityEngine;

public static class CreateGameObjectMenu
{
    [MenuItem("Tools/Create GameObject/Test111 GO")]
    private static void CreateTest111GameObject()
    {
        var go = new GameObject("Test111_GO");
        Undo.RegisterCreatedObjectUndo(go, "Create Test111_GO");
        Selection.activeGameObject = go;
        EditorGUIUtility.PingObject(go);
    }
}
