$url = 'http://127.0.0.1:62724/'
function Invoke-Mcp($n,$a) {
    $payload = @{ jsonrpc='2.0'; id=(Get-Random); method='tools/call'; params=@{ name=$n; arguments=$a } } | ConvertTo-Json -Depth 20 -Compress
    (Invoke-WebRequest -UseBasicParsing -Uri $url -Method Post -ContentType 'application/json' -Body $payload -TimeoutSec 60).Content
}
Write-Host (Invoke-Mcp 'request_recompile' @{})




