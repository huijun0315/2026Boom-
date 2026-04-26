$url = 'http://127.0.0.1:62724/'
$body = '{"jsonrpc":"2.0","id":2,"method":"tools/list","params":{}}'
$resp = Invoke-WebRequest -UseBasicParsing -Uri $url -Method Post -ContentType 'application/json' -Body $body -TimeoutSec 30
$obj = $resp.Content | ConvertFrom-Json
foreach ($t in $obj.result.tools) {
    if ($t.name -eq 'execute_code') {
        Write-Host ('--- ' + $t.name + ' ---')
        Write-Host ($t.description)
        Write-Host ($t.inputSchema | ConvertTo-Json -Depth 10)
    }
}





