$url = 'http://127.0.0.1:62724/'
function Invoke-Mcp($code) {
    $payload = @{
        jsonrpc='2.0'; id=(Get-Random); method='tools/call'
        params=@{ name='execute_code'; arguments=@{ code=$code } }
    } | ConvertTo-Json -Depth 20 -Compress
    (Invoke-WebRequest -UseBasicParsing -Uri $url -Method Post -ContentType 'application/json' -Body $payload -TimeoutSec 60).Content
}

$code = @'
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEditor;
using UnityEditor.SceneManagement;

public class T {
    public static string Run() {
        try {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var camGO = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
            camGO.tag = "MainCamera";
            var canvasGO = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var t = System.Type.GetType("StartMenuController, Assembly-CSharp");
            return "type=" + (t == null ? "NULL" : t.FullName);
        } catch (System.Exception ex) {
            return "EX:" + ex.GetType().Name + ":" + ex.Message;
        }
    }
}
'@

Write-Host (Invoke-Mcp $code)





