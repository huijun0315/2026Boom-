$ErrorActionPreference = 'Stop'
$url = 'http://127.0.0.1:62724/'

function Invoke-Mcp($n,$a) {
    $payload = @{ jsonrpc='2.0'; id=(Get-Random); method='tools/call'; params=@{ name=$n; arguments=$a } } | ConvertTo-Json -Depth 20 -Compress
    (Invoke-WebRequest -UseBasicParsing -Uri $url -Method Post -ContentType 'application/json' -Body $payload -TimeoutSec 120).Content
}

Write-Host '--- request_recompile ---'
Write-Host (Invoke-Mcp 'request_recompile' @{})

Write-Host '--- wait_for_compilation ---'
Write-Host (Invoke-Mcp 'wait_for_compilation' @{})

$codeLevel = @'
public class T { public static string Run() { return LevelSelectBuilder.Build(); } }
'@
Write-Host '--- execute_code: LevelSelectBuilder.Build() ---'
Write-Host (Invoke-Mcp 'execute_code' @{ code = $codeLevel })

$codeStart = @'
public class T { public static string Run() { return StartSceneBuilder.Build(); } }
'@
Write-Host '--- execute_code: StartSceneBuilder.Build() ---'
Write-Host (Invoke-Mcp 'execute_code' @{ code = $codeStart })

Write-Host '--- open_scene StartScene ---'
Write-Host (Invoke-Mcp 'open_scene' @{ path = 'Assets/Scenes/StartScene.unity' })




