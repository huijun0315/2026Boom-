$url = 'http://127.0.0.1:62724/'
function Invoke-Mcp($n,$a) {
    $payload = @{ jsonrpc='2.0'; id=(Get-Random); method='tools/call'; params=@{ name=$n; arguments=$a } } | ConvertTo-Json -Depth 20 -Compress
    (Invoke-WebRequest -UseBasicParsing -Uri $url -Method Post -ContentType 'application/json' -Body $payload -TimeoutSec 60).Content
}

Write-Host (Invoke-Mcp 'open_scene' @{ path = 'Assets/Scenes/LevelSelectScene.unity' })

$code = @'
using UnityEngine;
using UnityEngine.UI;

public class T {
    public static string Run() {
        string s = "";
        var lb = GameObject.Find("Level_1");
        if (lb == null) return "Level_1 NOT FOUND";
        s += "Level_1 children:\n";
        foreach (Transform c in lb.transform) {
            s += "  - " + c.name + " active=" + c.gameObject.activeInHierarchy + "\n";
            foreach (Transform c2 in c) {
                s += "      - " + c2.name + " active=" + c2.gameObject.activeInHierarchy;
                var txt = c2.GetComponent<Text>();
                if (txt != null) s += " text='" + txt.text + "' color=" + txt.color + " font=" + (txt.font != null ? txt.font.name : "NULL") + " size=" + txt.fontSize;
                s += "\n";
            }
        }
        var lbComp = lb.GetComponent<LevelButton>();
        if (lbComp != null) {
            s += "starGraphics.len=" + (lbComp.starGraphics != null ? lbComp.starGraphics.Length : -1) + "\n";
            if (lbComp.starGraphics != null && lbComp.starGraphics.Length > 0 && lbComp.starGraphics[0] != null)
                s += "star[0].type=" + lbComp.starGraphics[0].GetType().Name + " color=" + lbComp.starGraphics[0].color + "\n";
            s += "maxStars=" + lbComp.maxStars + " earned=" + lbComp.starsEarned + " unlocked=" + lbComp.isUnlocked + "\n";
        }
        return s;
    }
}
'@
Write-Host (Invoke-Mcp 'execute_code' @{ code = $code })



