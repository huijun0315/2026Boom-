$url = 'http://127.0.0.1:62724/'
function Invoke-Mcp($code) {
    $payload = @{
        jsonrpc='2.0'; id=(Get-Random); method='tools/call'
        params=@{ name='execute_code'; arguments=@{ code=$code } }
    } | ConvertTo-Json -Depth 20 -Compress
    (Invoke-WebRequest -UseBasicParsing -Uri $url -Method Post -ContentType 'application/json' -Body $payload -TimeoutSec 60).Content
}

# Minimal scene creation snippet
$code = @'
using System;
using UnityEngine;

public class T {
    public static string Run() {
        var t = Type.GetType("StartMenuController, Assembly-CSharp");
        return "type=" + (t == null ? "NULL" : t.FullName);
    }
}
'@
Write-Host (Invoke-Mcp $code)





