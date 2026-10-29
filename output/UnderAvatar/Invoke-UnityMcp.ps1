param(
    [Parameter(Mandatory=$true)][string]$Method,
    [string]$ParamsJson = '{}'
)
$ErrorActionPreference = 'Stop'
$sessionPath = Join-Path $PSScriptRoot 'mcp-session.txt'
$endpoint = 'http://127.0.0.1:8090/mcp'
function Read-RpcBody([string]$body) {
    $dataLine = ($body -split "`n" | Where-Object { $_.StartsWith('data: ') } | Select-Object -Last 1)
    if ($dataLine) { return $dataLine.Substring(6) | ConvertFrom-Json -Depth 100 }
    return $body | ConvertFrom-Json -Depth 100
}
if (!(Test-Path -LiteralPath $sessionPath) -or [string]::IsNullOrWhiteSpace((Get-Content -LiteralPath $sessionPath -Raw))) {
    $init = @{jsonrpc='2.0';id=1;method='initialize';params=@{protocolVersion='2025-03-26';capabilities=@{};clientInfo=@{name='under-avatar-validation';version='1.0'}}} | ConvertTo-Json -Depth 20 -Compress
    $response = Invoke-WebRequest -Uri $endpoint -Method Post -ContentType 'application/json' -Headers @{Accept='application/json, text/event-stream'} -Body $init -TimeoutSec 30
    [string]$session = $response.Headers['mcp-session-id'][0]
    Set-Content -LiteralPath $sessionPath -Value $session -NoNewline
} else { $session = Get-Content -LiteralPath $sessionPath -Raw }
$request = @{jsonrpc='2.0';id=2;method=$Method;params=($ParamsJson | ConvertFrom-Json -Depth 100)} | ConvertTo-Json -Depth 100 -Compress
$response = Invoke-WebRequest -Uri $endpoint -Method Post -ContentType 'application/json' -Headers @{Accept='application/json, text/event-stream';'mcp-session-id'=$session} -Body ([System.Text.Encoding]::UTF8.GetBytes($request)) -TimeoutSec 60
$parsed = Read-RpcBody $response.Content
if ($parsed.error) { throw ($parsed.error | ConvertTo-Json -Depth 20 -Compress) }
$parsed.result | ConvertTo-Json -Depth 100 -Compress
