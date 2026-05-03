using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

public static class PlayerEditorSceneBuilder
{
    [MenuItem("Tools/Create Player Editor Scene")]
    public static void BuildMenu()
    {
        Debug.Log(Build());
    }

    public static string Build()
    {
        const string sourcePath = "Assets/Scenes/PipeEditorScene.unity";
        const string targetPath = "Assets/Scenes/PlayerEditorScene.unity";

        if (!Directory.Exists("Assets/Scenes"))
            Directory.CreateDirectory("Assets/Scenes");

        if (!File.Exists(sourcePath))
        {
            var sourceResult = PipeEditorSceneBuilder.Build();
            if (!File.Exists(sourcePath))
                return "ERROR: PipeEditorScene missing. Build result: " + sourceResult;
        }

        var scene = EditorSceneManager.OpenScene(sourcePath, OpenSceneMode.Single);
        if (scene == null || !scene.IsValid())
            return "ERROR: failed to open " + sourcePath;

        var pipeEditor = Object.FindObjectOfType<PipeEditor>();
        if (pipeEditor == null)
            return "ERROR: PipeEditor component not found in source scene.";

        var go = pipeEditor.gameObject;
        var playerEditor = go.GetComponent<PlayerEditor>();
        if (playerEditor == null)
            playerEditor = go.AddComponent<PlayerEditor>();

        EditorUtility.CopySerialized(pipeEditor, playerEditor);
        Object.DestroyImmediate(pipeEditor, true);
        playerEditor.backSceneName = "StartScene";
        go.name = "PlayerEditor";

        var title = GameObject.Find("Title");
        if (title != null)
        {
            var text = title.GetComponent<Text>();
            if (text != null) text.text = "玩家编辑器";
        }

        EditorSceneManager.SaveScene(scene, targetPath);
        EnsureInBuildSettings(targetPath);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        return "OK: PlayerEditorScene at " + targetPath;
    }

    static void EnsureInBuildSettings(string scenePath)
    {
        var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
        for (int i = 0; i < scenes.Count; i++)
            if (scenes[i].path == scenePath) return;

        scenes.Add(new EditorBuildSettingsScene(scenePath, true));
        EditorBuildSettings.scenes = scenes.ToArray();
    }
}
