$url = 'http://127.0.0.1:62724/'
$body = '{"jsonrpc":"2.0","id":1,"method":"tools/call","params":{"name":"get_scene_info","arguments":{}}}'
try {
    $resp = Invoke-WebRequest -UseBasicParsing -Uri $url -Method Post -ContentType 'application/json' -Body $body -TimeoutSec 30
    Write-Host $resp.Content
} catch {
    Write-Host ('Error: ' + $_.Exception.Message)
}





