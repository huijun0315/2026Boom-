Get-ChildItem -Path . -Filter '_mcp_*.ps1' | ForEach-Object {
    $p = $_.FullName
    (Get-Content $p -Raw) -replace '127\.0\.0\.1:\d+', '127.0.0.1:62724' | Set-Content -Path $p -Encoding UTF8
    Write-Host ('updated: ' + $p)
}
