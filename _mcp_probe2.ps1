$ErrorActionPreference = 'Continue'
$url = 'http://127.0.0.1:62724/'

function Post-Json($u, $body) {
    try {
        $resp = Invoke-WebRequest -UseBasicParsing -Uri $u -Method Post -ContentType 'application/json' -Body $body -TimeoutSec 10
        Write-Host ('Status: ' + $resp.StatusCode)
        Write-Host $resp.Content
    } catch {
        Write-Host ('Error: ' + $_.Exception.Message)
    }
}

Write-Host '=== initialize ==='
Post-Json $url '{"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":"2024-11-05","capabilities":{},"clientInfo":{"name":"probe","version":"0.1"}}}'
Write-Host ''

Write-Host '=== tools/list ==='
Post-Json $url '{"jsonrpc":"2.0","id":2,"method":"tools/list","params":{}}'
Write-Host ''

Write-Host '=== tools/call get_scene_info ==='
Post-Json $url '{"jsonrpc":"2.0","id":3,"method":"tools/call","params":{"name":"get_scene_info","arguments":{}}}'
Write-Host ''

Write-Host '=== tools/call create_game_object name=Test111_GO ==='
Post-Json $url '{"jsonrpc":"2.0","id":4,"method":"tools/call","params":{"name":"create_game_object","arguments":{"name":"Test111_GO"}}}'
Write-Host ''





