$url = 'http://127.0.0.1:62724/'
$body = '{"jsonrpc":"2.0","id":1,"method":"tools/call","params":{"name":"create_primitive","arguments":{"primitive_type":"Cube","name":"Cube","position":"0,0.5,0"}}}'
$resp = Invoke-WebRequest -UseBasicParsing -Uri $url -Method Post -ContentType 'application/json' -Body $body -TimeoutSec 10
Write-Host $resp.Content





