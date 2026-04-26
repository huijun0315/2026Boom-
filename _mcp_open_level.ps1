$url = 'http://127.0.0.1:62724/'
$payload = @{ jsonrpc='2.0'; id=(Get-Random); method='tools/call'; params=@{ name='open_scene'; arguments=@{ path='Assets/Scenes/LevelSelectScene.unity' } } } | ConvertTo-Json -Depth 20 -Compress
(Invoke-WebRequest -UseBasicParsing -Uri $url -Method Post -ContentType 'application/json' -Body $payload -TimeoutSec 30).Content



