<#
Simple API smoke tests for the enhanced scheduling service
Assumes the API is already running at http://localhost:5443
#>

Write-Host "Testing Enhanced Interview Scheduling API" -ForegroundColor Green

$baseUrl = "http://localhost:5443"

# Test 1: Request without participants (should return guidance asking for participants)
Write-Host "`nTest 1: Request without participants" -ForegroundColor Yellow
try {
    $payload1 = @{ UserMessage = "Find me some free slots for tomorrow" } | ConvertTo-Json
    $response1 = Invoke-RestMethod -Uri "$baseUrl/api/scheduling/find-slots" -Method POST -ContentType "application/json" -Body $payload1
    Write-Host "Response:" -ForegroundColor Cyan
    $response1 | Out-String | Write-Host
} catch {
    Write-Host "Error: $($_.Exception.Message)" -ForegroundColor Red
}

# Test 2: Request with participants for next week (should show Mon-Fri coverage)
Write-Host "`nTest 2: Request with participants for next week" -ForegroundColor Yellow
try {
    $payload2 = @{ UserMessage = "Find slots for next week with john.doe@company.com and jane.smith@company.com" } | ConvertTo-Json
    $response2 = Invoke-RestMethod -Uri "$baseUrl/api/scheduling/find-slots" -Method POST -ContentType "application/json" -Body $payload2
    Write-Host "Response:" -ForegroundColor Cyan
    $response2 | Out-String | Write-Host
} catch {
    Write-Host "Error: $($_.Exception.Message)" -ForegroundColor Red
}

# Test 3: Basic API health check via Swagger JSON
Write-Host "`nTest 3: Basic API health check (Swagger JSON)" -ForegroundColor Yellow
try {
    $response3 = Invoke-RestMethod -Uri "$baseUrl/swagger/v1/swagger.json" -Method GET
    if ($response3.info.title) {
        Write-Host "API reachable. Swagger title: $($response3.info.title)" -ForegroundColor Green
    } else {
        Write-Host "Swagger JSON fetched but missing expected fields" -ForegroundColor Yellow
    }
} catch {
    Write-Host "API Health Check Error: $($_.Exception.Message)" -ForegroundColor Red
}

Write-Host "`nTests completed!" -ForegroundColor Magenta
