#!/usr/bin/env pwsh
# Test the MCP Server locally

$serverPath = "G:\code\memory-mcp\MemoryMcp.Server\bin\Debug\net8.0\MemoryMcp.Server.dll"

# Start the server in background
Write-Host "Starting MemoryMCP Server..." -ForegroundColor Green
$process = Start-Process -FilePath "dotnet" -ArgumentList $serverPath -NoNewWindow -PassThru -RedirectStandardInput "NUL" -RedirectStandardOutput ".\server_out.txt" -RedirectStandardError ".\server_err.txt"

Start-Sleep -Milliseconds 500

if ($process.HasExited) {
    Write-Host "Server failed to start. Error log:" -ForegroundColor Red
    Get-Content ".\server_err.txt"
    exit 1
}

Write-Host "Server started (PID: $($process.Id))" -ForegroundColor Green

# Test initialization
Write-Host "`nTest 1: Initialize" -ForegroundColor Yellow
$init = @{
    jsonrpc = "2.0"
    method = "initialize"
    id = 1
} | ConvertTo-Json -Compress
Write-Host "Request: $init"

# Send to stdin and read response
# (This is a simplified test - real integration would use proper IPC)

Write-Host "`nServer is ready. Check server output:" -ForegroundColor Green
Write-Host "  Standard output: .\server_out.txt"
Write-Host "  Standard error: .\server_err.txt"

Write-Host "`nTest scenario:" -ForegroundColor Cyan
Write-Host "1. The server listens on stdin for JSON-RPC 2.0 requests"
Write-Host "2. Responses are sent to stdout"
Write-Host "3. Use 'Ctrl+C' to stop the server"

# Keep server running
while (-not $process.HasExited) {
    Start-Sleep -Seconds 1
}

Write-Host "`nServer stopped" -ForegroundColor Green
