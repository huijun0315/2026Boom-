$url = 'http://127.0.0.1:62724/'
function Invoke-Mcp($n,$a) {
    $payload = @{ jsonrpc='2.0'; id=(Get-Random); method='tools/call'; params=@{ name=$n; arguments=$a } } | ConvertTo-Json -Depth 20 -Compress
    (Invoke-WebRequest -UseBasicParsing -Uri $url -Method Post -ContentType 'application/json' -Body $payload -TimeoutSec 60).Content
}

$code = @'
using UnityEngine;
using UnityEditor;

public class T {
    public static string Run() {
        string path = "Assets/art/3D/mofang.fbx";
        var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (go == null) return "NOT FOUND at " + path;
        string s = "Root: " + go.name + "\n";
        s += DumpHierarchy(go.transform, 0);
        // Count meshes
        var mfs = go.GetComponentsInChildren<MeshFilter>(true);
        s += "\nTotal MeshFilter count: " + mfs.Length + "\n";
        for (int i = 0; i < Mathf.Min(mfs.Length, 30); i++) {
            var mf = mfs[i];
            s += "  [" + i + "] " + GetPath(mf.transform) + " meshName=" + (mf.sharedMesh != null ? mf.sharedMesh.name : "null");
            if (mf.sharedMesh != null) {
                var b = mf.sharedMesh.bounds;
                s += " localBounds center=" + b.center + " size=" + b.size;
            }
            s += " worldPos=" + mf.transform.position;
            s += "\n";
        }
        return s;
    }
    static string DumpHierarchy(Transform t, int depth) {
        string pad = new string(' ', depth * 2);
        string line = pad + "- " + t.name + " localPos=" + t.localPosition + "\n";
        foreach (Transform c in t) line += DumpHierarchy(c, depth + 1);
        return line;
    }
    static string GetPath(Transform t) {
        string p = t.name;
        while (t.parent != null) { t = t.parent; p = t.name + "/" + p; }
        return p;
    }
}
'@
Write-Host (Invoke-Mcp 'execute_code' @{ code = $code })



