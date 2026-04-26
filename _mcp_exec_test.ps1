$url = 'http://127.0.0.1:62724/'
function Call-It($code) {
    $payload = @{
        jsonrpc='2.0'; id=(Get-Random); method='tools/call'
        params=@{ name='execute_code'; arguments=@{ code=$code } }
    } | ConvertTo-Json -Depth 20 -Compress
    (Invoke-WebRequest -UseBasicParsing -Uri $url -Method Post -ContentType 'application/json' -Body $payload -TimeoutSec 60).Content
}

Write-Host '--- test 1: snippet with return ---'
Write-Host (Call-It 'return "hello " + UnityEngine.Application.unityVersion;')

Write-Host '--- test 2: class with Run() ---'
Write-Host (Call-It @'
public class T { public static string Run() { return "classRun: " + UnityEngine.Application.unityVersion; } }
'@)

Write-Host '--- test 3: throw to see error format ---'
Write-Host (Call-It 'throw new System.Exception("hello_exception");')





