$ErrorActionPreference = 'Continue'
$body = '{"jsonrpc":"2.0","id":1,"method":"get_scene_info","params":{}}'
$urls = @('http://127.0.0.1:62724/', 'http://127.0.0.1:62724/mcp')
foreach ($u in $urls) {
    Write-Host ('=== Testing ' + $u + ' ===')
    try {
        $resp = Invoke-WebRequest -UseBasicParsing -Uri $u -Method Post -ContentType 'application/json' -Body $body -TimeoutSec 5
        Write-Host ('Status: ' + $resp.StatusCode)
        Write-Host 'Body:'
        Write-Host $resp.Content
    } catch {
        Write-Host ('Error: ' + $_.Exception.Message)
        if ($_.Exception.Response) {
            try {
                $sr = New-Object System.IO.StreamReader($_.Exception.Response.GetResponseStream())
                Write-Host 'Response:'
                Write-Host $sr.ReadToEnd()
            } catch {}
        }
    }
    Write-Host ''
}

Write-Host '=== GET / (for server info) ==='
try {
    $r = Invoke-WebRequest -UseBasicParsing -Uri 'http://127.0.0.1:62724/' -Method Get -TimeoutSec 5
    Write-Host ('Status: ' + $r.StatusCode)
    Write-Host $r.Content
} catch {
    Write-Host ('Error: ' + $_.Exception.Message)
}





