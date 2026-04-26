$ErrorActionPreference = 'Stop'
$url = 'http://127.0.0.1:62724/'

function Call-Tool($name, $args) {
    $payload = @{
        jsonrpc = '2.0'
        id = (Get-Random)
        method = 'tools/call'
        params = @{ name = $name; arguments = $args }
    } | ConvertTo-Json -Depth 20 -Compress
    $resp = Invoke-WebRequest -UseBasicParsing -Uri $url -Method Post -ContentType 'application/json' -Body $payload -TimeoutSec 120
    return $resp.Content
}

Write-Host '--- request_recompile ---'
Write-Host (Call-Tool 'request_recompile' @{})

Write-Host '--- wait_for_compilation ---'
Write-Host (Call-Tool 'wait_for_compilation' @{})

$code = Get-Content -Raw -Path '_mcp_build_scene2.cs'

Write-Host '--- execute_code (build scene) ---'
Write-Host (Call-Tool 'execute_code' @{ code = $code })

Write-Host '--- open_scene StartScene ---'
Write-Host (Call-Tool 'open_scene' @{ scene_path = 'Assets/Scenes/StartScene.unity' })





