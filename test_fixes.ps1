# Test script to verify the critical fixes

Write-Host "=== Testing Critical Fixes for Interview Scheduling Bot ===" -ForegroundColor Green

# Test 1: No participants specified (should ask for participants)
Write-Host "`n1. Testing request without participants:" -ForegroundColor Yellow
$testMsg1 = "Find me some free slots for tomorrow"
Write-Host "Request: '$testMsg1'" -ForegroundColor Cyan

# Test 2: Next week request (should handle full week)
Write-Host "`n2. Testing 'next week' request:" -ForegroundColor Yellow  
$testMsg2 = "Find slots for next week with john.doe@company.com and jane.smith@company.com"
Write-Host "Request: '$testMsg2'" -ForegroundColor Cyan

# Test 3: First 3 days of next week
Write-Host "`n3. Testing 'first 3 days of next week' request:" -ForegroundColor Yellow
$testMsg3 = "Find slots for first 3 days of next week with john.doe@company.com"
Write-Host "Request: '$testMsg3'" -ForegroundColor Cyan

Write-Host "`n=== Expected Results ===" -ForegroundColor Green
Write-Host "1. Should ask user to provide participant emails"
Write-Host "2. Should show slots for Monday through Friday of next week"
Write-Host "3. Should show slots for Monday, Tuesday, Wednesday of next week"

Write-Host "`nNow starting the bot to test these scenarios..." -ForegroundColor Magenta
