using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using UnityEditor.Events;
using UnityEngine.Events;
using System.IO;
using System.Collections.Generic;

if (!Directory.Exists("Assets/Scenes")) Directory.CreateDirectory("Assets/Scenes");

var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

var camGO = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
camGO.tag = "MainCamera";
var cam = camGO.GetComponent<Camera>();
cam.clearFlags = CameraClearFlags.SolidColor;
cam.backgroundColor = new Color(0.10f, 0.10f, 0.12f);

var es = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));

var canvasGO = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
var canvas = canvasGO.GetComponent<Canvas>();
canvas.renderMode = RenderMode.ScreenSpaceOverlay;
var scaler = canvasGO.GetComponent<CanvasScaler>();
scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
scaler.referenceResolution = new Vector2(1920, 1080);
scaler.matchWidthOrHeight = 0.5f;

var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
if (font == null) font = Resources.GetBuiltinResource<Font>("Arial.ttf");

var titleGO = new GameObject("Title", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
titleGO.transform.SetParent(canvasGO.transform, false);
var titleRT = titleGO.GetComponent<RectTransform>();
titleRT.anchorMin = new Vector2(0.5f, 0.5f);
titleRT.anchorMax = new Vector2(0.5f, 0.5f);
titleRT.pivot = new Vector2(0.5f, 0.5f);
titleRT.anchoredPosition = new Vector2(0, 220);
titleRT.sizeDelta = new Vector2(1000, 140);
var titleText = titleGO.GetComponent<Text>();
titleText.text = "游戏标题";
titleText.alignment = TextAnchor.MiddleCenter;
titleText.font = font;
titleText.fontSize = 80;
titleText.color = Color.white;

var controllerType = System.Type.GetType("StartMenuController, Assembly-CSharp");
if (controllerType == null) return "ERROR: StartMenuController type not found. Script not compiled yet.";
var controllerGO = new GameObject("MenuController");
var controller = controllerGO.AddComponent(controllerType) as MonoBehaviour;

System.Func<string, Vector2, Color, GameObject> MakeButton = (label, pos, bg) => {
    var bgo = new GameObject(label, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
    bgo.transform.SetParent(canvasGO.transform, false);
    var rt = bgo.GetComponent<RectTransform>();
    rt.anchorMin = new Vector2(0.5f, 0.5f);
    rt.anchorMax = new Vector2(0.5f, 0.5f);
    rt.pivot = new Vector2(0.5f, 0.5f);
    rt.anchoredPosition = pos;
    rt.sizeDelta = new Vector2(420, 110);
    bgo.GetComponent<Image>().color = bg;
    var tgo = new GameObject("Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
    tgo.transform.SetParent(bgo.transform, false);
    var trt = tgo.GetComponent<RectTransform>();
    trt.anchorMin = Vector2.zero;
    trt.anchorMax = Vector2.one;
    trt.offsetMin = Vector2.zero;
    trt.offsetMax = Vector2.zero;
    var txt = tgo.GetComponent<Text>();
    txt.text = label;
    txt.alignment = TextAnchor.MiddleCenter;
    txt.font = font;
    txt.fontSize = 48;
    txt.color = Color.white;
    return bgo;
};

var startBtn = MakeButton("开始游戏", new Vector2(0, 30), new Color(0.25f, 0.55f, 0.35f));
var exitBtn  = MakeButton("结束游戏", new Vector2(0, -140), new Color(0.70f, 0.25f, 0.25f));

var startAction = (UnityAction)System.Delegate.CreateDelegate(typeof(UnityAction), controller, "StartGame");
UnityEventTools.AddPersistentListener(startBtn.GetComponent<Button>().onClick, startAction);

var exitAction = (UnityAction)System.Delegate.CreateDelegate(typeof(UnityAction), controller, "ExitGame");
UnityEventTools.AddPersistentListener(exitBtn.GetComponent<Button>().onClick, exitAction);

var scenePath = "Assets/Scenes/StartScene.unity";
EditorSceneManager.SaveScene(scene, scenePath);

var sampleScene = "Assets/Scenes/SampleScene.unity";
var scenes = new List<EditorBuildSettingsScene>();
scenes.Add(new EditorBuildSettingsScene(scenePath, true));
if (File.Exists(sampleScene))
    scenes.Add(new EditorBuildSettingsScene(sampleScene, true));
EditorBuildSettings.scenes = scenes.ToArray();

AssetDatabase.SaveAssets();
AssetDatabase.Refresh();

return "OK: StartScene created at " + scenePath + ", build scenes=" + scenes.Count;
