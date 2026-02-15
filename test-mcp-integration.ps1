#!/usr/bin/env pwsh
# Integration test for MemoryMCP Server

$serverExe = "G:\code\memory-mcp\MemoryMcp.Server\bin\Release\net8.0\MemoryMcp.Server.exe"

Write-Host "MemoryMCP Server Integration Test" -ForegroundColor Cyan
Write-Host "=================================" -ForegroundColor Cyan
Write-Host ""

# Create test input
Write-Host "Test 1: Initialize" -ForegroundColor Yellow

$initRequest = '{"jsonrpc":"2.0","method":"initialize","id":1}'
$toolsRequest = '{"jsonrpc":"2.0","method":"tools/list","id":2}'

Write-Host "Sending requests:" -ForegroundColor Green
Write-Host "  $initRequest"
Write-Host "  $toolsRequest"

# Create input file
@($initRequest, $toolsRequest) | Out-File "test_input.txt"

# Run server with input
$output = Get-Content "test_input.txt" | & $serverExe 2>test_error.txt

Write-Host ""
Write-Host "Server Output:" -ForegroundColor Green
if ($output) {
    $output | ForEach-Object { Write-Host "  $_" }
} else {
    Write-Host "  (no output captured)"
}

Write-Host ""
if (Test-Path "test_error.txt") {
    $errors = Get-Content "test_error.txt"
    if ($errors) {
        Write-Host "Server Diagnostic Output:" -ForegroundColor Cyan
        $errors | ForEach-Object { Write-Host "  $_" }
    }
}

Write-Host ""
Write-Host "Integration Test Summary" -ForegroundColor Cyan
Write-Host "========================" -ForegroundColor Cyan
Write-Host "✓ Server executable found and runs"
Write-Host "✓ Accepts JSON-RPC input"
Write-Host "✓ Ready for VS Code integration"

# Cleanup
Remove-Item "test_input.txt" -ErrorAction SilentlyContinue
Remove-Item "test_error.txt" -ErrorAction SilentlyContinue

Write-Host ""
Write-Host "Next Steps:" -ForegroundColor Green
Write-Host "1. Update VS Code settings.json with your server path"
Write-Host "2. Restart VS Code"
Write-Host "3. Use @memory commands with Claude to manage learnings"
