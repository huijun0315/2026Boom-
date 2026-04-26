using System;
using System.IO;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

public static class CubeSceneBuilder
{
    [MenuItem("Tools/Create Cube Scene")]
    public static void BuildMenu()
    {
        Debug.Log(Build());
    }

    public static string Build()
    {
        try
        {
            if (!Directory.Exists("Assets/Scenes"))
                Directory.CreateDirectory("Assets/Scenes");

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Camera
            var camGO = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
            camGO.tag = "MainCamera";
            camGO.transform.position = new Vector3(4f, 4f, -6f);
            camGO.transform.LookAt(Vector3.zero);
            var cam = camGO.GetComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.13f, 0.14f, 0.18f);
            cam.allowMSAA = true;
            // 开启 8x MSAA 抗锯齿
            QualitySettings.antiAliasing = 8;

            // Directional Light
            var lightGO = new GameObject("Directional Light", typeof(Light));
            var lt = lightGO.GetComponent<Light>();
            lt.type = LightType.Directional;
            lt.intensity = 1.1f;
            lightGO.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

            // Fill light from other side
            var fillGO = new GameObject("Fill Light", typeof(Light));
            var fl = fillGO.GetComponent<Light>();
            fl.type = LightType.Directional;
            fl.intensity = 0.35f;
            fl.color = new Color(0.8f, 0.85f, 1f);
            fillGO.transform.rotation = Quaternion.Euler(30f, 150f, 0f);

            // RubikCube root
            var cubeType = Type.GetType("RubikCube, Assembly-CSharp");
            if (cubeType == null) return "ERROR: RubikCube type not found.";
            var cubeGO = new GameObject("RubikCube");
            cubeGO.transform.position = Vector3.zero;
            var rc = cubeGO.AddComponent(cubeType) as MonoBehaviour;
            // Assign camera via reflection
            var camField = cubeType.GetField("cam");
            if (camField != null) camField.SetValue(rc, cam);

            // EventSystem (UI 按钮输入需要)
            new GameObject("EventSystem",
                typeof(UnityEngine.EventSystems.EventSystem),
                typeof(UnityEngine.EventSystems.StandaloneInputModule));

            // PipePuzzle (attaches, will auto-build a sample level on Start)
            var pipeType = Type.GetType("PipePuzzle, Assembly-CSharp");
            if (pipeType != null) cubeGO.AddComponent(pipeType);

            // LevelCompleteUI (自建 UI + 礼花)
            var lcType = Type.GetType("LevelCompleteUI, Assembly-CSharp");
            if (lcType != null)
            {
                var lcGO = new GameObject("LevelCompleteUI");
                lcGO.AddComponent(lcType);
            }

            // ChallengeHUD (左上角步数显示)
            var hudType = Type.GetType("ChallengeHUD, Assembly-CSharp");
            if (hudType != null)
            {
                var hudGO = new GameObject("ChallengeHUD");
                hudGO.AddComponent(hudType);
            }

            var scenePath = "Assets/Scenes/CubeScene.unity";
            EditorSceneManager.SaveScene(scene, scenePath);

            // Add to Build Settings if missing
            var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            bool present = false;
            foreach (var s in scenes) if (s.path == scenePath) { present = true; break; }
            if (!present)
            {
                scenes.Add(new EditorBuildSettingsScene(scenePath, true));
                EditorBuildSettings.scenes = scenes.ToArray();
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            return "OK: CubeScene at " + scenePath;
        }
        catch (Exception ex)
        {
            return "EXCEPTION: " + ex.GetType().Name + ": " + ex.Message + "\n" + ex.StackTrace;
        }
    }
}
